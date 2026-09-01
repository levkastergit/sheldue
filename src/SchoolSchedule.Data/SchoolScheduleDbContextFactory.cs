using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace SchoolSchedule.Data;

/// <summary>
/// Используется только инструментом `dotnet ef migrations` при разработке — чтобы создавать
/// миграции без запуска полного WPF-приложения. В рантайме приложение конфигурирует
/// DbContext само (см. SchoolSchedule.App), указывая реальный путь к файлу БД.
/// </summary>
public class SchoolScheduleDbContextFactory : IDesignTimeDbContextFactory<SchoolScheduleDbContext>
{
    public SchoolScheduleDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<SchoolScheduleDbContext>();
        optionsBuilder.UseSqlite("Data Source=design_time.db");
        return new SchoolScheduleDbContext(optionsBuilder.Options);
    }
}
