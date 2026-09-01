using Google.OrTools.Sat;
using Google.OrTools.Util;
using SchoolSchedule.Core.Models;

namespace SchoolSchedule.Scheduling;

/// <summary>
/// Строит базовое недельное расписание (constraint satisfaction через CP-SAT). Для каждой строки
/// учебного плана (класс+предмет[+подгруппа]) с назначенным учителем — LessonsPerWeek занятий,
/// каждое выбирает урочный слот и кабинет так, чтобы:
///   - учитель/кабинет/класс не были заняты дважды в один слот;
///   - слот принадлежал смене класса;
///   - кабинет подходил по типу предмету (если задан) и по закреплению за учителем (если кабинет
///     закреплён хоть за кем-то — только за закреплёнными учителями);
///   - слот не попадал в окно недоступности учителя.
/// Подгруппы одного предмета одного класса (несколько строк с разным GroupLabel) встают в один и
/// тот же слот (учатся параллельно), но с разными учителями/кабинетами — как и требуется на деле
/// (иностранный язык, информатика и т.п.).
///
/// Мягкие ограничения (минимизация "окон", равномерное распределение по неделе) сюда сознательно
/// не входят — это чистая задача выполнимости первым найденным решением, что для типовой школьной
/// нагрузки обычно решается почти мгновенно. Если у школы данных достаточно, чтобы решение вообще
/// существовало, добавить "мягкую" оптимизацию поверх несложно вторым шагом.
/// </summary>
public class ScheduleSolver
{
    public ScheduleGenerationResult Generate(ScheduleInput input)
    {
        var warnings = new List<string>();

        if (input.TimeSlots.Count == 0)
        {
            return new ScheduleGenerationResult
            {
                Status = ScheduleGenerationStatus.NoData,
                Message = "Сначала настройте сетку расписания (дни/уроки в смену) на вкладке «Расписание» — сейчас нет ни одного урочного слота.",
            };
        }

        if (input.Rooms.Count == 0)
        {
            return new ScheduleGenerationResult
            {
                Status = ScheduleGenerationStatus.NoData,
                Message = "Сначала добавьте хотя бы один кабинет на странице «Кабинеты».",
            };
        }

        var timeSlots = input.TimeSlots;
        var numSlots = timeSlots.Count;
        // Индексируем слоты по позиции в списке, а не по TimeSlot.Id — на входе это всегда уже
        // сохранённые в базе сущности с настоящими Id, но полагаться на их уникальность самому
        // солверу незачем: он использует Id только на выходе, чтобы сослаться обратно на запись.
        var slotsByShift = timeSlots
            .Select((s, i) => (s, i))
            .GroupBy(t => t.s.Shift)
            .ToDictionary(g => g.Key, g => g.Select(t => t.i).ToArray());

        var rooms = input.Rooms;
        var roomIndexById = rooms.Select((r, i) => (r, i)).ToDictionary(t => t.r.Id, t => t.i);

        var unavailableByTeacher = input.Unavailabilities
            .GroupBy(u => u.TeacherId)
            .ToDictionary(g => g.Key, g => g.Select(u => (u.Day, u.PeriodNumber)).ToHashSet());

        int[] AllowedSlotsFor(Shift shift, int teacherId)
        {
            var byShift = slotsByShift.TryGetValue(shift, out var arr) ? arr : Array.Empty<int>();
            if (!unavailableByTeacher.TryGetValue(teacherId, out var blocked) || blocked.Count == 0)
                return byShift;
            return byShift.Where(i => !blocked.Contains((timeSlots[i].Day, timeSlots[i].PeriodNumber))).ToArray();
        }

        int[] AllowedRoomsFor(Subject subject, int teacherId)
        {
            return rooms
                .Where(r => subject.RequiredRoomTypeId is null || r.RoomTypeId == subject.RequiredRoomTypeId)
                .Where(r => r.AssignedTeachers.Count == 0 || r.AssignedTeachers.Any(a => a.TeacherId == teacherId))
                .Select(r => roomIndexById[r.Id])
                .ToArray();
        }

        // Годные к расстановке строки: часы > 0 и учитель назначен. Остальное — сразу в предупреждения.
        var eligible = new List<ClassSubjectGroup>();
        foreach (var g in input.Groups)
        {
            if (g.LessonsPerWeek <= 0) continue;
            if (g.TeacherId is null)
            {
                warnings.Add(DescribeGroup(g) + ": не назначен учитель — урок не включён в расписание.");
                continue;
            }
            eligible.Add(g);
        }

        if (eligible.Count == 0)
        {
            return new ScheduleGenerationResult
            {
                Status = ScheduleGenerationStatus.NoData,
                Message = "Нет ни одной строки учебного плана с назначенным учителем — сначала заполните «Учебный план» и «Назначения».",
                Warnings = warnings,
            };
        }

        var model = new CpModel();

        var occGroupId = new List<int>();
        var occTeacherId = new List<int>();
        var slotVars = new List<IntVar>();
        var roomVars = new List<IntVar>();
        var roomSlotVars = new List<IntVar>();

        var teacherSlotSets = new Dictionary<int, HashSet<IntVar>>();
        var classSlotSets = new Dictionary<int, HashSet<IntVar>>();

        void AddTeacherSlot(int teacherId, IntVar v)
        {
            if (!teacherSlotSets.TryGetValue(teacherId, out var set))
                teacherSlotSets[teacherId] = set = [];
            set.Add(v);
        }

        void AddClassSlot(int classId, IntVar v)
        {
            if (!classSlotSets.TryGetValue(classId, out var set))
                classSlotSets[classId] = set = [];
            set.Add(v);
        }

        void RegisterOccurrence(ClassSubjectGroup g, IntVar slotVar, IntVar roomVar)
        {
            var roomSlotVar = model.NewIntVar(0, (long)rooms.Count * numSlots - 1, $"rs_{g.Id}_{occGroupId.Count}");
            model.Add(roomSlotVar == (roomVar * numSlots) + slotVar);

            occGroupId.Add(g.Id);
            occTeacherId.Add(g.TeacherId!.Value);
            slotVars.Add(slotVar);
            roomVars.Add(roomVar);
            roomSlotVars.Add(roomSlotVar);

            AddTeacherSlot(g.TeacherId.Value, slotVar);
            AddClassSlot(g.ClassId, slotVar);
        }

        foreach (var block in eligible.GroupBy(g => (g.ClassId, g.SubjectId)))
        {
            var rows = block.ToList();
            var schoolClass = rows[0].Class;
            var subject = rows[0].Subject;

            if (rows.Count == 1)
            {
                var g = rows[0];
                var allowedSlots = AllowedSlotsFor(schoolClass.Shift, g.TeacherId!.Value);
                var allowedRooms = AllowedRoomsFor(subject, g.TeacherId.Value);

                if (allowedSlots.Length == 0)
                {
                    warnings.Add(DescribeGroup(g) + ": нет ни одного урочного слота смены этого класса (проверьте недоступность учителя) — урок не включён.");
                    continue;
                }
                if (allowedRooms.Length == 0)
                {
                    warnings.Add(DescribeGroup(g) + ": нет подходящего кабинета (тип кабинета или закрепление за учителем) — урок не включён.");
                    continue;
                }

                for (var occ = 0; occ < g.LessonsPerWeek; occ++)
                {
                    var slotVar = model.NewIntVarFromDomain(ToDomain(allowedSlots), $"s_{g.Id}_{occ}");
                    var roomVar = model.NewIntVarFromDomain(ToDomain(allowedRooms), $"r_{g.Id}_{occ}");
                    RegisterOccurrence(g, slotVar, roomVar);
                }
            }
            else
            {
                // Подгруппы: синхронизированные занятия (min часов среди подгрупп) встают в один и тот
                // же слот, но с индивидуальным кабинетом/учителем на подгруппу. Если у какой-то строки
                // часов больше остальных — "лишние" сверх минимума занятия расставляются независимо.
                var perRowAllowedSlots = rows.ToDictionary(r => r.Id, r => AllowedSlotsFor(schoolClass.Shift, r.TeacherId!.Value));
                var perRowAllowedRooms = rows.ToDictionary(r => r.Id, r => AllowedRoomsFor(subject, r.TeacherId!.Value));

                var rowsWithNoRoom = rows.Where(r => perRowAllowedRooms[r.Id].Length == 0).ToList();
                if (rowsWithNoRoom.Count > 0)
                {
                    foreach (var r in rows)
                        warnings.Add(DescribeGroup(r) + ": подгруппы должны идти параллельно, но у одной из них нет подходящего кабинета — предмет не включён в расписание ни для одной из подгрупп.");
                    continue;
                }

                var sharedSlots = perRowAllowedSlots.Values
                    .Aggregate((IEnumerable<int>)perRowAllowedSlots.Values.First(), (acc, next) => acc.Intersect(next))
                    .ToArray();

                var synced = rows.Min(r => r.LessonsPerWeek);

                if (sharedSlots.Length == 0 && synced > 0)
                {
                    foreach (var r in rows)
                        warnings.Add(DescribeGroup(r) + ": у подгрупп нет общего доступного слота (смена/недоступность учителей) — предмет не включён в расписание ни для одной из подгрупп.");
                }
                else
                {
                    for (var occ = 0; occ < synced; occ++)
                    {
                        var sharedSlotVar = model.NewIntVarFromDomain(ToDomain(sharedSlots), $"sblk_{schoolClass.Id}_{subject.Id}_{occ}");
                        foreach (var r in rows)
                        {
                            var roomVar = model.NewIntVarFromDomain(ToDomain(perRowAllowedRooms[r.Id]), $"r_{r.Id}_{occ}");
                            RegisterOccurrence(r, sharedSlotVar, roomVar);
                        }
                    }
                }

                // Занятия сверх синхронизированного минимума — независимо, как обычная одиночная строка.
                foreach (var r in rows.Where(r => r.LessonsPerWeek > synced))
                {
                    var allowedSlots = perRowAllowedSlots[r.Id];
                    if (allowedSlots.Length == 0)
                    {
                        warnings.Add(DescribeGroup(r) + ": нет доступного слота для занятий сверх общего с подгруппами — часть уроков не включена.");
                        continue;
                    }
                    for (var occ = synced; occ < r.LessonsPerWeek; occ++)
                    {
                        var slotVar = model.NewIntVarFromDomain(ToDomain(allowedSlots), $"s_{r.Id}_{occ}");
                        var roomVar = model.NewIntVarFromDomain(ToDomain(perRowAllowedRooms[r.Id]), $"r_{r.Id}_{occ}");
                        RegisterOccurrence(r, slotVar, roomVar);
                    }
                }
            }
        }

        if (slotVars.Count == 0)
        {
            return new ScheduleGenerationResult
            {
                Status = ScheduleGenerationStatus.NoData,
                Message = "Ни одну строку учебного плана не удалось подготовить к расстановке — см. список причин.",
                Warnings = warnings,
            };
        }

        foreach (var set in teacherSlotSets.Values.Where(s => s.Count > 1))
            model.AddAllDifferent(set);
        foreach (var set in classSlotSets.Values.Where(s => s.Count > 1))
            model.AddAllDifferent(set);
        model.AddAllDifferent(roomSlotVars);

        var solver = new CpSolver
        {
            StringParameters = $"max_time_in_seconds:{input.TimeLimit.TotalSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture)};num_search_workers:8",
        };
        var status = solver.Solve(model);

        if (status is CpSolverStatus.Optimal or CpSolverStatus.Feasible)
        {
            var lessons = new List<GeneratedLesson>();
            for (var i = 0; i < slotVars.Count; i++)
            {
                var slotIdx = (int)solver.Value(slotVars[i]);
                var roomIdx = (int)solver.Value(roomVars[i]);
                lessons.Add(new GeneratedLesson(occGroupId[i], occTeacherId[i], rooms[roomIdx].Id, timeSlots[slotIdx].Id));
            }

            var status2 = warnings.Count > 0 ? ScheduleGenerationStatus.PartialSuccess : ScheduleGenerationStatus.Success;
            return new ScheduleGenerationResult
            {
                Status = status2,
                Message = status2 == ScheduleGenerationStatus.Success
                    ? $"Расписание построено: {lessons.Count} уроков расставлено без конфликтов."
                    : $"Расписание построено для {lessons.Count} уроков, но {warnings.Count} строк учебного плана в него не попали — см. список ниже.",
                Lessons = lessons,
                Warnings = warnings,
            };
        }

        if (status == CpSolverStatus.Infeasible)
        {
            return new ScheduleGenerationResult
            {
                Status = ScheduleGenerationStatus.Infeasible,
                Message = "Расписание без конфликтов построить невозможно с текущими данными: слишком много уроков на имеющееся число слотов/кабинетов, либо учителя/кабинеты конфликтуют по нагрузке. Проверьте часы в учебном плане, число кабинетов подходящего типа и недоступность учителей.",
                Warnings = warnings,
            };
        }

        return new ScheduleGenerationResult
        {
            Status = ScheduleGenerationStatus.TimedOut,
            Message = "Солвер не успел найти решение за отведённое время. Попробуйте ещё раз — иногда помогает; если не помогает, вероятно, данных слишком много для текущих ограничений сетки.",
            Warnings = warnings,
        };
    }

    private static Domain ToDomain(int[] values) => Domain.FromValues(values.Select(v => (long)v).ToArray());

    private static string DescribeGroup(ClassSubjectGroup g) =>
        g.GroupLabel is null
            ? $"{g.Class.Name} — {g.Subject.Name}"
            : $"{g.Class.Name} — {g.Subject.Name} ({g.GroupLabel})";
}
