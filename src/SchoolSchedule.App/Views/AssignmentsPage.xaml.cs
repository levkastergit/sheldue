using System.Windows.Controls;
using SchoolSchedule.App.ViewModels;

namespace SchoolSchedule.App.Views;

public partial class AssignmentsPage : UserControl
{
    public AssignmentsPage(AssignmentsViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    private async void AssignmentsPage_OnLoaded(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is AssignmentsViewModel vm)
            await vm.LoadCommand.ExecuteAsync(null);
    }

    private void DataGrid_OnRowEditEnding(object sender, DataGridRowEditEndingEventArgs e)
    {
        if (e.EditAction == DataGridEditAction.Commit
            && DataContext is AssignmentsViewModel vm
            && e.Row.Item is AssignmentRow row)
        {
            vm.SaveCommand.Execute(row);
        }
    }
}
