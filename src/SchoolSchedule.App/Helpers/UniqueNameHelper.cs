namespace SchoolSchedule.App.Helpers;

/// <summary>
/// Подбирает свободное имя для новой записи там, где имя уникально в базе (кабинеты, классы,
/// предметы) — иначе повторное "Добавить" сразу падало бы на нарушении уникального индекса
/// с одним и тем же дефолтным именем, и добавить вторую запись было бы нельзя, не переименовав
/// первую.
/// </summary>
public static class UniqueNameHelper
{
    public static string NextAvailable(string baseName, IEnumerable<string> existingNames)
    {
        var existing = existingNames.ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (!existing.Contains(baseName))
            return baseName;

        var i = 2;
        while (existing.Contains($"{baseName} {i}"))
            i++;
        return $"{baseName} {i}";
    }
}
