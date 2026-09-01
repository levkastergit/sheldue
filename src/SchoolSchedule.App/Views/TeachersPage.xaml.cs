using System.Windows.Controls;
using SchoolSchedule.App.ViewModels;
using SchoolSchedule.Core.Models;

namespace SchoolSchedule.App.Views;

public partial class TeachersPage : UserControl
{
    public TeachersPage(TeachersViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    private async void TeachersPage_OnLoaded(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is TeachersViewModel vm)
            await vm.LoadCommand.ExecuteAsync(null);
    }

    private void DataGrid_OnRowEditEnding(object sender, DataGridRowEditEndingEventArgs e)
    {
        if (e.EditAction == DataGridEditAction.Commit
            && DataContext is TeachersViewModel vm
            && e.Row.Item is Teacher teacher)
        {
            vm.SaveTeacherCommand.Execute(teacher);
        }
    }
}
