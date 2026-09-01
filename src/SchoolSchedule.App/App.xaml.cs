using System.IO;
using System.Windows;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SchoolSchedule.App.Services;
using SchoolSchedule.App.ViewModels;
using SchoolSchedule.App.Views;
using SchoolSchedule.Data;

namespace SchoolSchedule.App;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    private IHost? _host;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var appDataDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SchoolSchedule");
        Directory.CreateDirectory(appDataDir);
        var dbPath = Path.Combine(appDataDir, "school.db");

        _host = Host.CreateDefaultBuilder()
            .ConfigureServices((_, services) =>
            {
                services.AddDbContextFactory<SchoolScheduleDbContext>(options =>
                    options.UseSqlite($"Data Source={dbPath}"));

                services.AddSingleton<ISnackbarService, SnackbarService>();

                services.AddSingleton<RoomsViewModel>();
                services.AddSingleton<RoomsPage>();
                services.AddSingleton<SubjectsViewModel>();
                services.AddSingleton<SubjectsPage>();
                services.AddSingleton<TeachersViewModel>();
                services.AddSingleton<TeachersPage>();
                services.AddSingleton<ClassesViewModel>();
                services.AddSingleton<ClassesPage>();
                services.AddSingleton<CurriculumViewModel>();
                services.AddSingleton<CurriculumPage>();
                services.AddSingleton<AssignmentsViewModel>();
                services.AddSingleton<AssignmentsPage>();

                services.AddSingleton<MainViewModel>();
                services.AddSingleton<MainWindow>();
            })
            .Build();

        await _host.StartAsync();

        // Применяем миграции при каждом запуске — база сама создаётся/обновляется без ручных шагов.
        var dbFactory = _host.Services.GetRequiredService<IDbContextFactory<SchoolScheduleDbContext>>();
        await using (var db = await dbFactory.CreateDbContextAsync())
        {
            await db.Database.MigrateAsync();
        }

        var mainWindow = _host.Services.GetRequiredService<MainWindow>();
        mainWindow.Show();
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        if (_host is not null)
        {
            await _host.StopAsync();
            _host.Dispose();
        }

        base.OnExit(e);
    }
}
