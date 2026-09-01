using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
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
        // Ставим обработчики необработанных исключений до всего остального — если что-то
        // упадёт хоть на каком шаге запуска, пользователь увидит окно с сообщением и путём
        // к логу, а не просто "ничего не произошло".
        AppDomain.CurrentDomain.UnhandledException += OnAppDomainUnhandledException;
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

        StartupLogger.Log("=== Запуск приложения ===");
        StartupLogger.Log($"ОС: {Environment.OSVersion}, 64-бит процесс: {Environment.Is64BitProcess}, .NET: {Environment.Version}");
        StartupLogger.Log($"Папка запуска: {AppContext.BaseDirectory}");

        try
        {
            base.OnStartup(e);
            StartupLogger.Log("Базовая инициализация WPF пройдена");

            var appDataDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "SchoolSchedule");
            Directory.CreateDirectory(appDataDir);
            var dbPath = Path.Combine(appDataDir, "school.db");
            StartupLogger.Log($"Путь к базе данных: {dbPath}");

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
            StartupLogger.Log("DI-контейнер собран (Host.Build)");

            await _host.StartAsync();
            StartupLogger.Log("Host запущен (StartAsync)");

            // Применяем миграции при каждом запуске — база сама создаётся/обновляется без ручных шагов.
            var dbFactory = _host.Services.GetRequiredService<IDbContextFactory<SchoolScheduleDbContext>>();
            StartupLogger.Log("Применяю миграции БД...");
            await using (var db = await dbFactory.CreateDbContextAsync())
            {
                await db.Database.MigrateAsync();
            }
            StartupLogger.Log("Миграции БД применены успешно");

            var mainWindow = _host.Services.GetRequiredService<MainWindow>();
            StartupLogger.Log("MainWindow создан через DI, показываю окно...");
            mainWindow.Show();
            StartupLogger.Log("Окно показано — запуск завершён успешно");
        }
        catch (Exception ex)
        {
            StartupLogger.LogException("OnStartup", ex);
            ShowFatalErrorAndShutdown(ex);
        }
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        StartupLogger.Log("=== Завершение приложения (OnExit) ===");
        if (_host is not null)
        {
            await _host.StopAsync();
            _host.Dispose();
        }

        base.OnExit(e);
    }

    private void OnAppDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
        {
            StartupLogger.LogException("AppDomain.UnhandledException", ex);
            ShowFatalErrorAndShutdown(ex);
        }
        else
        {
            StartupLogger.Log($"ОШИБКА [AppDomain.UnhandledException]: {e.ExceptionObject}");
        }
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        StartupLogger.LogException("DispatcherUnhandledException (UI-поток)", e.Exception);
        ShowFatalErrorAndShutdown(e.Exception);
        e.Handled = true;
    }

    private static void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        StartupLogger.LogException("UnobservedTaskException (фоновая задача)", e.Exception);
        e.SetObserved();
    }

    private static void ShowFatalErrorAndShutdown(Exception ex)
    {
        try
        {
            MessageBox.Show(
                $"Не удалось запустить приложение.\n\n" +
                $"{ex.GetType().Name}: {ex.Message}\n\n" +
                $"Подробный журнал сохранён в файле:\n{StartupLogger.LogFilePath}\n\n" +
                "Пришлите этот файл разработчику для диагностики.",
                "Расписание школы — ошибка запуска",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        catch
        {
            // Если даже MessageBox показать не удалось — по крайней мере в логе всё уже есть.
        }

        Current?.Shutdown(-1);
    }
}
