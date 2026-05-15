using LafazFlow.Windows.Core;

namespace LafazFlow.Windows.Services;

public sealed record DictationJob(
    string AudioPath,
    IntPtr TargetWindow,
    AppSettings Settings);
