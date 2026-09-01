using System.Windows.Controls;
using SchoolSchedule.App.ViewModels;
using SchoolSchedule.Core.Models;

namespace SchoolSchedule.App.Views;

public partial class SubjectsPage : UserControl
{
    public SubjectsPage(SubjectsViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    private async void SubjectsPage_OnLoaded(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is SubjectsViewModel vm)
            await vm.LoadCommand.ExecuteAsync(null);
    }

    private void DataGrid_OnRowEditEnding(object sender, DataGridRowEditEndingEventArgs e)
    {
        if (e.EditAction == DataGridEditAction.Commit
            && DataContext is SubjectsViewModel vm
            && e.Row.Item is Subject subject)
        {
            vm.SaveSubjectCommand.Execute(subject);
        }
    }
}
