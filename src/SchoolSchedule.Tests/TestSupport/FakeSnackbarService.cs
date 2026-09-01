using MaterialDesignThemes.Wpf;
using SchoolSchedule.App.Services;

namespace SchoolSchedule.Tests.TestSupport;

/// <summary>Собирает показанные сообщения для проверки в тестах, не трогая реальную очередь Snackbar.</summary>
public class FakeSnackbarService : ISnackbarService
{
    public List<string> Messages { get; } = [];

    // Реальный SnackbarMessageQueue требует поток с Dispatcher (WPF UI-поток), которого нет в тестах,
    // а сами тесты проверяют только переданный текст через Show() — очередь им не нужна.
    public SnackbarMessageQueue MessageQueue => throw new NotSupportedException(
        "MessageQueue недоступен в тестовом окружении без WPF Dispatcher — используйте Messages.");

    public void Show(string message) => Messages.Add(message);
}
