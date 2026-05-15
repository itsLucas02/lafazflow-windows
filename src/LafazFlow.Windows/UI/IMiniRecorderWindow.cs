namespace LafazFlow.Windows.UI;

public interface IMiniRecorderWindow
{
    void ShowBottomCenter();

    void Hide();

    Task InvokeAsync(Action action);
}
