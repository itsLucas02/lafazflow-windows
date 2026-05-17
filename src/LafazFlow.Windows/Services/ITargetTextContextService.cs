namespace LafazFlow.Windows.Services;

public interface ITargetTextContextService
{
    string GetTextBeforeCaret(IntPtr targetWindow);
}
