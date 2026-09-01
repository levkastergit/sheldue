using Microsoft.EntityFrameworkCore;

namespace SchoolSchedule.App.Helpers;

/// <summary>Превращает низкоуровневый текст ошибки SQLite в понятное сообщение для Snackbar.</summary>
public static class DbErrorFormatter
{
    public static string Format(string actionDescription, DbUpdateException ex)
    {
        var raw = ex.InnerException?.Message ?? ex.Message;

        if (raw.Contains("UNIQUE constraint failed"))
            return "Такое название уже используется — выберите другое.";

        if (raw.Contains("FOREIGN KEY constraint failed"))
            return $"Не удалось {actionDescription} — запись используется в другом месте (расписание, назначения и т.п.).";

        return $"Не удалось {actionDescription}: {raw}";
    }
}
