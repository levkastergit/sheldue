using System.IO;

namespace SchoolSchedule.App.Services;

/// <summary>
/// Простой файловый лог запуска приложения — пишется до того, как поднят DI-контейнер
/// (и продолжает писаться после), чтобы при падении на старте можно было понять, на каком
/// именно шаге всё сломалось, а не просто видеть "ничего не произошло".
/// </summary>
public static class StartupLogger
{
    public static readonly string LogDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "SchoolSchedule", "logs");

    public static readonly string LogFilePath = Path.Combine(LogDirectory, $"startup-{DateTime.Now:yyyyMMdd-HHmmss}.log");

    public static void Log(string message)
    {
        try
        {
            Directory.CreateDirectory(LogDirectory);
            File.AppendAllText(LogFilePath, $"[{DateTime.Now:HH:mm:ss.fff}] {message}{Environment.NewLine}");
        }
        catch
        {
            // Если писать лог не получилось — это не должно валить само приложение.
        }
    }

    public static void LogException(string context, Exception ex)
    {
        Log($"ОШИБКА [{context}]: {ex}");
    }
}
