using Google.OrTools.Sat;
using Google.OrTools.Util;
using SchoolSchedule.Core.Models;

namespace SchoolSchedule.Scheduling;

/// <summary>
/// Строит базовое недельное расписание (constraint satisfaction через CP-SAT). Для каждой строки
/// учебного плана (класс+предмет[+подгруппа]) с назначенным учителем — LessonsPerWeek занятий,
/// каждое выбирает урочный слот и кабинет так, чтобы:
///   - учитель/кабинет/класс не были заняты дважды в один слот (жёсткое);
///   - слот принадлежал смене класса (жёсткое);
///   - кабинет подходил по типу предмету, если задан (жёсткое);
///   - слот не попадал в окно недоступности учителя (жёсткое);
///   - кабинет, закреплённый за учителем(-ями), по возможности используется именно ими — но это
///     ПРИОРИТЕТ, а не запрет: если так эффективнее (или иначе расписание не сходится), в
///     закреплённый кабинет всё равно может встать урок другого учителя (мягкое — минимизируется
///     число таких "нарушений" через objective функцию, но полностью не запрещается);
///   - для строки учебного плана не больше ClassSubjectGroup.MaxLessonsPerDay её уроков в один
///     день (по умолчанию 1 — не больше одного в день; жёсткое);
///   - если у строки включено ClassSubjectGroup.PairedLessons и в какой-то день всё же встали два
///     её урока — они обязаны идти подряд, без окна между ними (жёсткое, действует только при
///     MaxLessonsPerDay больше 1).
/// Подгруппы одного предмета одного класса (несколько строк с разным GroupLabel) встают в один и
/// тот же слот (учатся параллельно), но с разными учителями/кабинетами — как и требуется на деле
/// (иностранный язык, информатика и т.п.).
///
/// Остальные мягкие ограничения (минимизация "окон", равномерное распределение по неделе) сюда
/// сознательно не входят — модель ищет решение с минимумом нарушений закрепления кабинетов, а
/// если закреплений нет вовсе, откатывается к чистой задаче выполнимости первым найденным
/// решением (без objective), что для типовой школьной нагрузки обычно решается почти мгновенно.
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

        // Таблицы для CP-SAT AddElement: по индексу слота — день/номер урока. Нужны для
        // ограничений "не больше N уроков этой строки в день" и "парные уроки — подряд".
        var slotToDay = timeSlots.Select(t => (long)(int)t.Day).ToArray();
        var slotToPeriod = timeSlots.Select(t => (long)t.PeriodNumber).ToArray();
        var distinctDays = timeSlots.Select(t => (int)t.Day).Distinct().ToArray();
        var maxPeriodNumber = (int)timeSlots.Max(t => t.PeriodNumber);

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

        // Закрепление кабинета за учителем — это приоритет, а не запрет: кабинет остаётся годным
        // по типу для любого учителя, кто мог бы там вести, просто закреплённые учителя
        // предпочитаются (см. NonPreferredRoomsFor + минимизация нарушений ниже).
        int[] AllowedRoomsFor(Subject subject, int teacherId)
        {
            return rooms
                .Where(r => subject.RequiredRoomTypeId is null || r.RoomTypeId == subject.RequiredRoomTypeId)
                .Select(r => roomIndexById[r.Id])
                .ToArray();
        }

        // Из кабинетов, годных по типу, — те, что закреплены за кем-то другим (не за teacherId).
        // Использование такого кабинета этим учителем — не запрещено, но штрафуется в objective.
        int[] NonPreferredRoomsFor(Subject subject, int teacherId)
        {
            return rooms
                .Where(r => subject.RequiredRoomTypeId is null || r.RoomTypeId == subject.RequiredRoomTypeId)
                .Where(r => r.AssignedTeachers.Count > 0 && r.AssignedTeachers.All(a => a.TeacherId != teacherId))
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
        var pinningViolations = new List<IntVar>();

        // Все occurrence-слоты одной строки учебного плана (ClassSubjectGroup.Id) — в т.ч. те,
        // что физически являются общей переменной с подгруппой-соседом (см. RegisterOccurrence)
        // — нужны отдельно на группу, чтобы применить её собственные MaxLessonsPerDay/PairedLessons.
        var groupOccurrenceSlots = new Dictionary<int, List<IntVar>>();

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

        void RegisterOccurrence(ClassSubjectGroup g, IntVar slotVar, IntVar roomVar, int[] allowedRooms)
        {
            var occIndex = occGroupId.Count;
            var roomSlotVar = model.NewIntVar(0, (long)rooms.Count * numSlots - 1, $"rs_{g.Id}_{occIndex}");
            model.Add(roomSlotVar == (roomVar * numSlots) + slotVar);

            occGroupId.Add(g.Id);
            occTeacherId.Add(g.TeacherId!.Value);
            slotVars.Add(slotVar);
            roomVars.Add(roomVar);
            roomSlotVars.Add(roomSlotVar);

            AddTeacherSlot(g.TeacherId.Value, slotVar);
            AddClassSlot(g.ClassId, slotVar);

            if (!groupOccurrenceSlots.TryGetValue(g.Id, out var ownSlots))
                groupOccurrenceSlots[g.Id] = ownSlots = [];
            ownSlots.Add(slotVar);

            // Закрепление кабинета — приоритет, не запрет: если среди годных по типу кабинетов
            // есть закреплённые за ДРУГИМИ учителями, заведи булеву переменную "нарушил
            // закрепление" и минимизируй сумму таких нарушений в objective — solver предпочтёт
            // не закреплённые (или закреплённые именно за этим учителем) кабинеты там, где это
            // возможно, но не откажется от закреплённого, если другого выхода нет.
            var nonPreferred = NonPreferredRoomsFor(g.Subject, g.TeacherId.Value);
            if (nonPreferred.Length > 0)
            {
                var preferred = allowedRooms.Except(nonPreferred).ToArray();
                var violatesPinning = model.NewBoolVar($"vp_{g.Id}_{occIndex}");
                model.AddLinearExpressionInDomain(roomVar, ToDomain(nonPreferred)).OnlyEnforceIf(violatesPinning);
                model.AddLinearExpressionInDomain(roomVar, ToDomain(preferred)).OnlyEnforceIf(violatesPinning.Not());
                pinningViolations.Add(violatesPinning);
            }
        }

        // Ограничивает, сколько occurrence-слотов ОДНОЙ строки учебного плана может попасть на один
        // день (MaxLessonsPerDay), и, если включено PairedLessons, требует, чтобы любые два её
        // урока в один день шли подряд (без окна между ними — "сдвоенный" урок).
        void ApplyDayLimitAndPairing(ClassSubjectGroup g, List<IntVar> occSlots)
        {
            if (occSlots.Count <= 1) return;

            var needsDayLimit = g.MaxLessonsPerDay < occSlots.Count;
            var needsPairing = g.PairedLessons && g.MaxLessonsPerDay >= 2;
            if (!needsDayLimit && !needsPairing) return;

            var dayVars = new List<IntVar>();
            var periodVars = new List<IntVar>();
            for (var i = 0; i < occSlots.Count; i++)
            {
                var dayVar = model.NewIntVar(1, 6, $"day_{g.Id}_{i}");
                model.AddElement(occSlots[i], slotToDay, dayVar);
                dayVars.Add(dayVar);

                if (needsPairing)
                {
                    var periodVar = model.NewIntVar(1, maxPeriodNumber, $"per_{g.Id}_{i}");
                    model.AddElement(occSlots[i], slotToPeriod, periodVar);
                    periodVars.Add(periodVar);
                }
            }

            if (needsDayLimit)
            {
                foreach (var day in distinctDays)
                {
                    var indicators = new List<IntVar>();
                    for (var i = 0; i < occSlots.Count; i++)
                    {
                        var isOnDay = model.NewBoolVar($"dind_{g.Id}_{i}_{day}");
                        model.Add(dayVars[i] == day).OnlyEnforceIf(isOnDay);
                        model.Add(dayVars[i] != day).OnlyEnforceIf(isOnDay.Not());
                        indicators.Add(isOnDay);
                    }
                    model.Add(LinearExpr.Sum(indicators) <= g.MaxLessonsPerDay);
                }
            }

            if (needsPairing)
            {
                for (var i = 0; i < occSlots.Count; i++)
                {
                    for (var j = i + 1; j < occSlots.Count; j++)
                    {
                        var sameDay = model.NewBoolVar($"sd_{g.Id}_{i}_{j}");
                        model.Add(dayVars[i] == dayVars[j]).OnlyEnforceIf(sameDay);
                        model.Add(dayVars[i] != dayVars[j]).OnlyEnforceIf(sameDay.Not());

                        var diff = model.NewIntVar(-maxPeriodNumber, maxPeriodNumber, $"diff_{g.Id}_{i}_{j}");
                        model.Add(diff == periodVars[i] - periodVars[j]);
                        var absDiff = model.NewIntVar(0, maxPeriodNumber, $"absdiff_{g.Id}_{i}_{j}");
                        model.AddAbsEquality(absDiff, diff);
                        // Подряд — |разница номеров урока| == 1, но только если оба урока в этот день.
                        model.Add(absDiff == 1).OnlyEnforceIf(sameDay);
                    }
                }
            }
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
                    warnings.Add(DescribeGroup(g) + ": нет ни одного кабинета подходящего типа — урок не включён.");
                    continue;
                }

                for (var occ = 0; occ < g.LessonsPerWeek; occ++)
                {
                    var slotVar = model.NewIntVarFromDomain(ToDomain(allowedSlots), $"s_{g.Id}_{occ}");
                    var roomVar = model.NewIntVarFromDomain(ToDomain(allowedRooms), $"r_{g.Id}_{occ}");
                    RegisterOccurrence(g, slotVar, roomVar, allowedRooms);
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
                            RegisterOccurrence(r, sharedSlotVar, roomVar, perRowAllowedRooms[r.Id]);
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
                        RegisterOccurrence(r, slotVar, roomVar, perRowAllowedRooms[r.Id]);
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

        foreach (var g in eligible)
        {
            if (groupOccurrenceSlots.TryGetValue(g.Id, out var ownSlots))
                ApplyDayLimitAndPairing(g, ownSlots);
        }

        foreach (var set in teacherSlotSets.Values.Where(s => s.Count > 1))
            model.AddAllDifferent(set);
        foreach (var set in classSlotSets.Values.Where(s => s.Count > 1))
            model.AddAllDifferent(set);
        model.AddAllDifferent(roomSlotVars);

        // Если в школе вообще есть закреплённые кабинеты — ищем решение с минимумом нарушений
        // закрепления (мягкий приоритет). Если закреплений нет, objective не добавляется — модель
        // остаётся чистой задачей выполнимости и решается первым найденным решением (быстрее).
        if (pinningViolations.Count > 0)
            model.Minimize(LinearExpr.Sum(pinningViolations));

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
