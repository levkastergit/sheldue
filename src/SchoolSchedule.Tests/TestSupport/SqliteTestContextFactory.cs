using System.IO;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SchoolSchedule.Data;

namespace SchoolSchedule.Tests.TestSupport;

/// <summary>
/// Фабрика DbContext поверх временного файла SQLite для интеграционных тестов ViewModel'ей —
/// те же запросы/ограничения, что и в реальном приложении (в отличие от InMemory-провайдера,
/// который не проверяет уникальные индексы и внешние ключи так же строго).
/// </summary>
public sealed class SqliteTestContextFactory : IDbContextFactory<SchoolScheduleDbContext>, IDisposable
{
    private readonly string _dbPath;
    private readonly DbContextOptions<SchoolScheduleDbContext> _options;

    public SqliteTestContextFactory()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"schoolschedule_test_{Guid.NewGuid():N}.db");
        _options = new DbContextOptionsBuilder<SchoolScheduleDbContext>()
            .UseSqlite($"Data Source={_dbPath}")
            .Options;

        using var context = CreateDbContext();
        context.Database.Migrate();
    }

    public SchoolScheduleDbContext CreateDbContext() => new(_options);

    public void Dispose()
    {
        // Microsoft.Data.Sqlite пулит соединения — файл остаётся открытым, пока пул не очищен явно.
        SqliteConnection.ClearAllPools();

        try
        {
            if (File.Exists(_dbPath))
                File.Delete(_dbPath);
        }
        catch (IOException)
        {
            // Временный файл — не критично, если не удалился сразу; ОС подчистит TEMP.
        }
    }
}
