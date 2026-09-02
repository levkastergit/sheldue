using Google.OrTools.Sat;
using Google.OrTools.Util;
using SchoolSchedule.Core.Models;

namespace SchoolSchedule.Scheduling;

/// <summary>
/// Строит базовое недельное расписание (CP-SAT). Для каждой строки учебного плана
/// (класс+предмет[+подгруппа]) с назначенным учителем — LessonsPerWeek занятий, каждое выбирает
/// урочный слот и кабинет так, чтобы:
///   - учитель/кабинет/класс не были заняты дважды в один слот (жёсткое);
///   - слот принадлежал смене класса (жёсткое);
///   - кабинет подходил по типу предмету, если задан (жёсткое);
///   - слот не попадал в окно недоступности учителя (жёсткое);
///   - для строки учебного плана не больше ClassSubjectGroup.MaxLessonsPerDay её уроков в один
///     день (по умолчанию 1; жёсткое), и если включено PairedLessons — любые два её урока в один
///     день идут подряд, без окна между ними (жёсткое, только при MaxLessonsPerDay больше 1).
/// Подгруппы одного предмета одного класса (несколько строк с разным GroupLabel) на "общей" части
/// (min часов среди подгрупп) встают в один и тот же слот (учатся параллельно, с разными
/// учителями/кабинетами) — как и требуется на деле (иностранный язык, информатика и т.п.). Если у
/// какой-то подгруппы часов больше — "лишние" занятия сверх общего расставляются независимо: в это
/// время у другой подгруппы может не быть урока вовсе, это нормально и учитывается при минимизации
/// окон (см. ниже) — окно считается для КАЖДОЙ подгруппы отдельно (плюс уроки всего класса).
///
/// Дальше — три уровня МЯГКИХ ограничений (не запрещают решение, но solver ищет решение с
/// минимумом нарушений, в строгом приоритетном порядке — через один objective с заведомо
/// доминирующими весами, а не через отдельные "мягкие" и "жёсткие" фазы):
///   1) окна ("пустые" уроки между двумя занятыми в один день) у учеников — считаются отдельно на
///      каждый класс и, если у класса есть подгруппы, ОТДЕЛЬНО на каждую подгруппу (её собственные
///      уроки + уроки всего класса, потому что именно это видят в своём дневном расписании
///      реальные ученики этой подгруппы) — высший приоритет;
///   2) окна у учителей — вторым приоритетом;
///   3) нарушение приоритета закрепления кабинета за учителем (кабинет, закреплённый за кем-то,
///      используется другим учителем, хотя не обязан был) — третьим, низшим приоритетом.
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

        // Таблицы для CP-SAT AddElement: по индексу слота — день/номер урока. Нужны для лимита
        // уроков в день, парных уроков и минимизации окон.
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
        // предпочитаются (см. NonPreferredRoomsFor + минимизация нарушений в objective).
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
        var occClassId = new List<int>();
        var occShift = new List<Shift>();
        var slotVars = new List<IntVar>();
        var roomVars = new List<IntVar>();
        var roomSlotVars = new List<IntVar>();
        var occDayVars = new List<IntVar>();
        var occPeriodVars = new List<IntVar>();

        var teacherSlotSets = new Dictionary<int, HashSet<IntVar>>();
        var classSlotSets = new Dictionary<int, HashSet<IntVar>>();
        var pinningViolations = new List<IntVar>();

        // Все occurrence-индексы одной строки учебного плана (ClassSubjectGroup.Id) — нужны
        // отдельно на группу, чтобы применить её собственные MaxLessonsPerDay/PairedLessons.
        var groupOccurrenceIndices = new Dictionary<int, List<int>>();

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

            var dayVar = model.NewIntVar(1, 6, $"day_{occIndex}");
            model.AddElement(slotVar, slotToDay, dayVar);
            var periodVar = model.NewIntVar(1, maxPeriodNumber, $"per_{occIndex}");
            model.AddElement(slotVar, slotToPeriod, periodVar);

            occGroupId.Add(g.Id);
            occTeacherId.Add(g.TeacherId!.Value);
            occClassId.Add(g.ClassId);
            occShift.Add(g.Class.Shift);
            slotVars.Add(slotVar);
            roomVars.Add(roomVar);
            roomSlotVars.Add(roomSlotVar);
            occDayVars.Add(dayVar);
            occPeriodVars.Add(periodVar);

            AddTeacherSlot(g.TeacherId.Value, slotVar);
            AddClassSlot(g.ClassId, slotVar);

            if (!groupOccurrenceIndices.TryGetValue(g.Id, out var ownIndices))
                groupOccurrenceIndices[g.Id] = ownIndices = [];
            ownIndices.Add(occIndex);

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
        void ApplyDayLimitAndPairing(ClassSubjectGroup g, List<int> indices)
        {
            if (indices.Count <= 1) return;

            var needsDayLimit = g.MaxLessonsPerDay < indices.Count;
            var needsPairing = g.PairedLessons && g.MaxLessonsPerDay >= 2;
            if (!needsDayLimit && !needsPairing) return;

            if (needsDayLimit)
            {
                foreach (var day in distinctDays)
                {
                    var indicators = new List<IntVar>();
                    foreach (var idx in indices)
                    {
                        var isOnDay = model.NewBoolVar($"dind_{g.Id}_{idx}_{day}");
                        model.Add(occDayVars[idx] == day).OnlyEnforceIf(isOnDay);
                        model.Add(occDayVars[idx] != day).OnlyEnforceIf(isOnDay.Not());
                        indicators.Add(isOnDay);
                    }
                    model.Add(LinearExpr.Sum(indicators) <= g.MaxLessonsPerDay);
                }
            }

            if (needsPairing)
            {
                for (var a = 0; a < indices.Count; a++)
                {
                    for (var b = a + 1; b < indices.Count; b++)
                    {
                        var (i, j) = (indices[a], indices[b]);
                        var sameDay = model.NewBoolVar($"sd_{g.Id}_{i}_{j}");
                        model.Add(occDayVars[i] == occDayVars[j]).OnlyEnforceIf(sameDay);
                        model.Add(occDayVars[i] != occDayVars[j]).OnlyEnforceIf(sameDay.Not());

                        var diff = model.NewIntVar(-maxPeriodNumber, maxPeriodNumber, $"diff_{g.Id}_{i}_{j}");
                        model.Add(diff == occPeriodVars[i] - occPeriodVars[j]);
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
                // часов больше остальных — "лишние" сверх минимума занятия расставляются независимо:
                // в это время у другой подгруппы может и не быть урока (см. учёт окон на подгруппу
                // отдельно от учёта окон на весь класс — BuildStudentGapEntities ниже).
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
                // Именно здесь и возникает "у одной подгруппы урок есть, а у другой в это время нет".
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
            if (groupOccurrenceIndices.TryGetValue(g.Id, out var ownIndices))
                ApplyDayLimitAndPairing(g, ownIndices);
        }

        foreach (var set in teacherSlotSets.Values.Where(s => s.Count > 1))
            model.AddAllDifferent(set);
        foreach (var set in classSlotSets.Values.Where(s => s.Count > 1))
            model.AddAllDifferent(set);
        model.AddAllDifferent(roomSlotVars);

        // --- Минимизация окон (приоритеты 1 и 2) + нарушений закрепления кабинета (приоритет 3) ---
        //
        // Окно на (сущность, день, смена) считается как "длина промежутка от первого до последнего
        // урока этого дня минус число реально стоящих в нём уроков": gap = (last-first+1) - count.
        // Это 0, если уроки идут подряд, и 0 же для пустого дня (гасится через hasAnyLesson) — при
        // этом сами first/last не обязаны быть заданы явно верным способом: достаточно "first <=
        // период любого урока этого дня" и "last >= период любого урока этого дня", а к точному
        // минимуму/максимуму их подтягивает сам objective (минимизация gap только выигрывает от
        // того, чтобы first/last были как можно ближе друг к другу).
        var groupById = eligible.ToDictionary(g => g.Id);

        List<IntVar> BuildGapPenalties(List<int> indices, string label)
        {
            var penalties = new List<IntVar>();
            var byShift = indices.GroupBy(i => occShift[i]);
            foreach (var shiftGroup in byShift)
            {
                if (!slotsByShift.TryGetValue(shiftGroup.Key, out var shiftSlotIdx) || shiftSlotIdx.Length == 0)
                    continue;
                var occInShift = shiftGroup.ToList();
                var daysForShift = shiftSlotIdx.Select(idx => (int)timeSlots[idx].Day).Distinct();

                foreach (var day in daysForShift)
                {
                    var tag = $"{label}_{shiftGroup.Key}_{day}_{penalties.Count}";
                    var onDay = new List<BoolVar>();
                    foreach (var idx in occInShift)
                    {
                        var b = model.NewBoolVar($"od_{tag}_{idx}");
                        model.Add(occDayVars[idx] == day).OnlyEnforceIf(b);
                        model.Add(occDayVars[idx] != day).OnlyEnforceIf(b.Not());
                        onDay.Add(b);
                    }

                    var hasAny = model.NewBoolVar($"has_{tag}");
                    model.Add(LinearExpr.Sum(onDay) >= 1).OnlyEnforceIf(hasAny);
                    model.Add(LinearExpr.Sum(onDay) == 0).OnlyEnforceIf(hasAny.Not());

                    var first = model.NewIntVar(1, maxPeriodNumber, $"first_{tag}");
                    var last = model.NewIntVar(1, maxPeriodNumber, $"last_{tag}");
                    for (var k = 0; k < occInShift.Count; k++)
                    {
                        model.Add(first <= occPeriodVars[occInShift[k]]).OnlyEnforceIf(onDay[k]);
                        model.Add(last >= occPeriodVars[occInShift[k]]).OnlyEnforceIf(onDay[k]);
                    }

                    var gap = model.NewIntVar(0, maxPeriodNumber, $"gap_{tag}");
                    model.Add(gap == last - first + 1 - LinearExpr.Sum(onDay)).OnlyEnforceIf(hasAny);
                    model.Add(gap == 0).OnlyEnforceIf(hasAny.Not());
                    penalties.Add(gap);
                }
            }
            return penalties;
        }

        // Ученики (приоритет 1): по классу — все его occurrence'ы; если у класса есть подгруппы —
        // отдельная "сущность" на каждую подгруппу (уроки всего класса + именно её уроки), потому
        // что реальные ученики этой подгруппы в свой день видят и то, и другое.
        var studentGapPenalties = new List<IntVar>();
        foreach (var classGroup in Enumerable.Range(0, occGroupId.Count).GroupBy(i => occClassId[i]))
        {
            var classId = classGroup.Key;
            var allIndices = classGroup.ToList();
            var labels = allIndices.Select(i => groupById[occGroupId[i]].GroupLabel).Where(l => l is not null).Distinct().ToList();

            if (labels.Count == 0)
            {
                studentGapPenalties.AddRange(BuildGapPenalties(allIndices, $"cls{classId}"));
            }
            else
            {
                var wholeClassIndices = allIndices.Where(i => groupById[occGroupId[i]].GroupLabel is null).ToList();
                foreach (var label in labels)
                {
                    var subgroupIndices = allIndices.Where(i => groupById[occGroupId[i]].GroupLabel == label).ToList();
                    studentGapPenalties.AddRange(BuildGapPenalties(wholeClassIndices.Concat(subgroupIndices).ToList(), $"cls{classId}_{label}"));
                }
            }
        }

        // Учителя (приоритет 2): все occurrence'ы этого учителя, независимо от класса/подгруппы.
        var teacherGapPenalties = new List<IntVar>();
        foreach (var teacherGroup in Enumerable.Range(0, occGroupId.Count).GroupBy(i => occTeacherId[i]))
            teacherGapPenalties.AddRange(BuildGapPenalties(teacherGroup.ToList(), $"tch{teacherGroup.Key}"));

        // Веса подобраны так, чтобы приоритет был СТРОГИМ: любое улучшение на уровне N важнее
        // абсолютно любой комбинации ухудшений на уровнях N+1, N+2 — а не просто "в среднем важнее".
        var maxPinningSum = (long)pinningViolations.Count; // каждая переменная — 0 или 1
        var maxTeacherGapSum = (long)teacherGapPenalties.Count * maxPeriodNumber; // грубая, но надёжная верхняя оценка
        const long wPinning = 1L;
        var wTeacherGap = maxPinningSum * wPinning + 1;
        var wStudentGap = maxTeacherGapSum * wTeacherGap + maxPinningSum * wPinning + 1;

        var objectiveTerms = new List<LinearExpr>();
        foreach (var p in studentGapPenalties) objectiveTerms.Add(p * wStudentGap);
        foreach (var p in teacherGapPenalties) objectiveTerms.Add(p * wTeacherGap);
        foreach (var p in pinningViolations) objectiveTerms.Add(p * wPinning);
        if (objectiveTerms.Count > 0)
            model.Minimize(LinearExpr.Sum(objectiveTerms));

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

            var studentGaps = studentGapPenalties.Sum(p => solver.Value(p));
            var teacherGaps = teacherGapPenalties.Sum(p => solver.Value(p));
            var gapsSummary = $"Окон у учеников: {studentGaps}, у учителей: {teacherGaps}.";

            var status2 = warnings.Count > 0 ? ScheduleGenerationStatus.PartialSuccess : ScheduleGenerationStatus.Success;
            return new ScheduleGenerationResult
            {
                Status = status2,
                Message = status2 == ScheduleGenerationStatus.Success
                    ? $"Расписание построено: {lessons.Count} уроков расставлено без конфликтов. {gapsSummary}"
                    : $"Расписание построено для {lessons.Count} уроков, но {warnings.Count} строк учебного плана в него не попали — см. список ниже. {gapsSummary}",
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
