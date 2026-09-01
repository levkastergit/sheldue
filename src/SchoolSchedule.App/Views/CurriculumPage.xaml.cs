using System.Windows.Controls;
using SchoolSchedule.App.ViewModels;
using SchoolSchedule.Core.Models;

namespace SchoolSchedule.App.Views;

public partial class CurriculumPage : UserControl
{
    public CurriculumPage(CurriculumViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    private async void CurriculumPage_OnLoaded(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is CurriculumViewModel vm)
            await vm.LoadCommand.ExecuteAsync(null);
    }

    private void DataGrid_OnRowEditEnding(object sender, DataGridRowEditEndingEventArgs e)
    {
        if (e.EditAction == DataGridEditAction.Commit
            && DataContext is CurriculumViewModel vm
            && e.Row.Item is ClassSubjectGroup group)
        {
            vm.SaveGroupCommand.Execute(group);
        }
    }
}
