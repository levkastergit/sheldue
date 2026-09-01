using System.Windows.Controls;
using SchoolSchedule.App.ViewModels;
using SchoolSchedule.Core.Models;

namespace SchoolSchedule.App.Views;

public partial class RoomsPage : UserControl
{
    public RoomsPage(RoomsViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    private async void RoomsPage_OnLoaded(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is RoomsViewModel vm)
            await vm.LoadCommand.ExecuteAsync(null);
    }

    private void DataGrid_OnRowEditEnding(object sender, DataGridRowEditEndingEventArgs e)
    {
        if (e.EditAction == DataGridEditAction.Commit
            && DataContext is RoomsViewModel vm
            && e.Row.Item is Room room)
        {
            vm.SaveRoomCommand.Execute(room);
        }
    }
}
