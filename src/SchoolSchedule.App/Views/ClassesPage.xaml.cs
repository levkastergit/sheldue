using System.Windows.Controls;
using SchoolSchedule.App.ViewModels;
using SchoolSchedule.Core.Models;

namespace SchoolSchedule.App.Views;

public partial class ClassesPage : UserControl
{
    public ClassesPage(ClassesViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    private async void ClassesPage_OnLoaded(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is ClassesViewModel vm)
            await vm.LoadCommand.ExecuteAsync(null);
    }

    private void DataGrid_OnRowEditEnding(object sender, DataGridRowEditEndingEventArgs e)
    {
        if (e.EditAction == DataGridEditAction.Commit
            && DataContext is ClassesViewModel vm
            && e.Row.Item is SchoolClass schoolClass)
        {
            vm.SaveClassCommand.Execute(schoolClass);
        }
    }
}
