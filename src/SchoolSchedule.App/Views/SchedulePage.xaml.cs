using System.Windows.Controls;
using SchoolSchedule.App.ViewModels;

namespace SchoolSchedule.App.Views;

public partial class SchedulePage : UserControl
{
    public SchedulePage(ScheduleViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    private async void SchedulePage_OnLoaded(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is ScheduleViewModel vm)
            await vm.LoadCommand.ExecuteAsync(null);
    }
}
