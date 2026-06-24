using AppCobros.Models;
using CommunityToolkit.Mvvm.Input;

namespace AppCobros.PageModels
{
    public interface IProjectTaskPageModel
    {
        IAsyncRelayCommand<ProjectTask> NavigateToTaskCommand { get; }
        bool IsBusy { get; }
    }
}