using LafazFlow.Windows.Core;

namespace LafazFlow.Windows.Services;

public static class LocalModelCatalog
{
    public static IReadOnlyList<LocalModelDefinition> Models { get; } =
    [
        new(
            "ggml-base.en",
            "Base English",
            "ggml-base.en.bin",
            "142 MB",
            "English",
            0.95,
            0.75,
            "~500 MB",
            "Fast everyday English dictation. Best default for low-latency writing.",
            "https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-base.en.bin",
            true,
            false),
        new(
            "ggml-small.en",
            "Small English",
            "ggml-small.en.bin",
            "466 MB",
            "English",
            0.82,
            0.84,
            "~1 GB",
            "Better English accuracy while staying light enough for frequent dictation.",
            "https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-small.en.bin",
            true,
            false),
        new(
            "ggml-medium.en",
            "Medium English",
            "ggml-medium.en.bin",
            "1.5 GB",
            "English",
            0.58,
            0.91,
            "~2.6 GB",
            "Higher English accuracy for longer dictation when latency matters less.",
            "https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-medium.en.bin",
            false,
            false),
        new(
            "ggml-large-v3-turbo-q5_0",
            "Large v3 Turbo Quantized",
            "ggml-large-v3-turbo-q5_0.bin",
            "547 MB",
            "Multilingual",
            0.75,
            0.95,
            "~1 GB",
            "Balanced quality model with stronger recognition for difficult phrases.",
            "https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-large-v3-turbo-q5_0.bin",
            true,
            true)
    ];
}
