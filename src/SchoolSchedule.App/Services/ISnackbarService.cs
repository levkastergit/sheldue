using MaterialDesignThemes.Wpf;

namespace SchoolSchedule.App.Services;

/// <summary>Всплывающие уведомления (ошибки сохранения, подтверждения) — единая точка для всех страниц.</summary>
public interface ISnackbarService
{
    SnackbarMessageQueue MessageQueue { get; }
    void Show(string message);
}

public class SnackbarService : ISnackbarService
{
    public SnackbarMessageQueue MessageQueue { get; } = new(TimeSpan.FromSeconds(4));

    public void Show(string message) => MessageQueue.Enqueue(message);
}
