using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using MaterialDesignThemes.Wpf;
using SchoolSchedule.App.Services;
using SchoolSchedule.App.Views;

namespace SchoolSchedule.App.ViewModels;

/// <summary>
/// ViewModel главного окна: список пунктов бокового меню (сгруппированных по разделам) и
/// текущая выбранная страница. Реализованные разделы получают свою страницу через DI;
/// остальные — заглушки, которые сменятся реальными экранами в следующих фазах.
/// </summary>
public partial class MainViewModel : ObservableObject
{
    public ObservableCollection<NavigationItem> NavigationItems { get; }

    /// <summary>Тот же список, но сгруппированный по NavigationItem.Group — для сайдбара.</summary>
    public ICollectionView NavigationView { get; }

    public SnackbarMessageQueue SnackbarMessageQueue { get; }

    [ObservableProperty]
    private NavigationItem? _selectedItem;

    [ObservableProperty]
    private object? _currentPage;

    private const string GroupReference = "СПРАВОЧНИКИ";
    private const string GroupPlanning = "ПЛАНИРОВАНИЕ";
    private const string GroupManagement = "УПРАВЛЕНИЕ";

    public MainViewModel(
        RoomsPage roomsPage,
        SubjectsPage subjectsPage,
        TeachersPage teachersPage,
        ClassesPage classesPage,
        CurriculumPage curriculumPage,
        AssignmentsPage assignmentsPage,
        ISnackbarService snackbarService)
    {
        SnackbarMessageQueue = snackbarService.MessageQueue;

        NavigationItems =
        [
            new() { Title = "Кабинеты", Icon = PackIconKind.DoorOpen, Group = GroupReference, Page = roomsPage },
            new() { Title = "Предметы", Icon = PackIconKind.BookOpenVariant, Group = GroupReference, Page = subjectsPage },
            new() { Title = "Учителя", Icon = PackIconKind.AccountTie, Group = GroupReference, Page = teachersPage },
            new() { Title = "Классы", Icon = PackIconKind.AccountGroup, Group = GroupReference, Page = classesPage },

            new() { Title = "Учебный план", Icon = PackIconKind.ClipboardText, Group = GroupPlanning, Page = curriculumPage },
            new() { Title = "Назначения", Icon = PackIconKind.AccountSwitch, Group = GroupPlanning, Page = assignmentsPage },
            BuildPlaceholder("Расписание", PackIconKind.CalendarClock, GroupPlanning),

            BuildPlaceholder("Замены и отпуска", PackIconKind.CalendarRemove, GroupManagement),
            BuildPlaceholder("Настройки", PackIconKind.Cog, GroupManagement),
        ];

        NavigationView = CollectionViewSource.GetDefaultView(NavigationItems);
        NavigationView.GroupDescriptions.Add(new PropertyGroupDescription(nameof(NavigationItem.Group)));

        SelectedItem = NavigationItems.FirstOrDefault();
    }

    private static NavigationItem BuildPlaceholder(string title, PackIconKind icon, string group) => new()
    {
        Title = title,
        Icon = icon,
        Group = group,
        Page = new PlaceholderPage { SectionTitle = title, Icon = icon },
    };

    partial void OnSelectedItemChanged(NavigationItem? value)
    {
        CurrentPage = value?.Page;
    }
}
