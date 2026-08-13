// LafazFlow persistent Whisper worker - proof-of-concept milestone (M3).
// Loads one whisper.cpp context at startup and serves sequential transcription
// requests from stdin. Fresh decode state per request; no transcript context
// leaks between dictations. M4 replaces stdin/stdout with the versioned
// named-pipe protocol.

#include "whisper.h"

#include <atomic>
#include <chrono>
#include <cstdint>
#include <cstdio>
#include <cstring>
#include <fstream>
#include <iostream>
#include <string>
#include <thread>
#include <vector>

namespace {

std::atomic<bool> g_abort{false};
std::atomic<bool> g_shutdown{false};
std::string g_abort_file;

bool g_use_gpu = true;
int g_gpu_device = 0;
int g_threads = 16;
std::string g_language = "en";
bool g_no_fallback = true;
bool g_suppress_nst = true;
bool g_carry_initial_prompt = true;
std::string g_initial_prompt;
float g_temperature = 0.0f;
int g_best_of = -1;
int g_max_context = -1;
bool g_vad = true;
std::string g_vad_model;
float g_vad_threshold = 0.50f;
int g_vad_min_speech_duration_ms = 250;
int g_vad_min_silence_duration_ms = 100;
int g_vad_speech_pad_ms = 30;
float g_vad_samples_overlap = 0.10f;

bool AbortCallback(void*) {
    return g_abort.load();
}

long long ElapsedMs(std::chrono::steady_clock::time_point start) {
    return std::chrono::duration_cast<std::chrono::milliseconds>(
               std::chrono::steady_clock::now() - start)
        .count();
}

void PrintUsage() {
    std::fprintf(
        stderr,
        "usage: lafazflow-whisper-worker --model <path> [--vad-model <path>] "
        "[--threads N] [--prompt <text>] [--max-context N] [--best-of N] "
        "[--temperature X] [--no-fallback] [--no-suppress-nst] "
        "[--no-carry-prompt] [--no-vad] [--vad-params vt,vspd,vsd,vp,vo] "
        "[--gpu-device N] [--cpu] [--version]\n");
}

struct WavData {
    std::vector<float> pcm;
    bool ok = false;
};

WavData ReadWav16kMono(const std::string& path) {
    WavData result;
    std::FILE* file = std::fopen(path.c_str(), "rb");
    if (!file) {
        return result;
    }

    std::vector<std::uint8_t> bytes;
    std::uint8_t buffer[65536];
    std::size_t read = 0;
    while ((read = std::fread(buffer, 1, sizeof(buffer), file)) > 0) {
        bytes.insert(bytes.end(), buffer, buffer + read);
    }
    std::fclose(file);

    if (bytes.size() < 44 || std::memcmp(bytes.data(), "RIFF", 4) != 0
        || std::memcmp(bytes.data() + 8, "WAVE", 4) != 0) {
        return result;
    }

    auto read32 = [&](std::size_t offset) -> std::uint32_t {
        return static_cast<std::uint32_t>(bytes[offset])
             | (static_cast<std::uint32_t>(bytes[offset + 1]) << 8)
             | (static_cast<std::uint32_t>(bytes[offset + 2]) << 16)
             | (static_cast<std::uint32_t>(bytes[offset + 3]) << 24);
    };
    auto read16 = [&](std::size_t offset) -> std::uint16_t {
        return static_cast<std::uint16_t>(bytes[offset])
             | (static_cast<std::uint16_t>(bytes[offset + 1]) << 8);
    };

    std::size_t position = 12;
    std::uint32_t sample_rate = 0;
    std::uint16_t channels = 0;
    std::uint16_t bits = 0;
    std::uint32_t data_size = 0;
    bool found_data = false;

    while (position + 8 <= bytes.size()) {
        char id[5] = {0};
        std::memcpy(id, bytes.data() + position, 4);
        std::uint32_t chunk_size = read32(position + 4);
        if (std::strcmp(id, "fmt ") == 0 && position + 8 + 16 <= bytes.size()) {
            channels = read16(position + 8 + 2);
            sample_rate = read32(position + 8 + 4);
            bits = read16(position + 8 + 14);
        } else if (std::strcmp(id, "data") == 0) {
            data_size = chunk_size;
            found_data = true;
            break;
        }
        position += 8 + chunk_size + (chunk_size % 2);
    }

    if (!found_data || sample_rate != 16000 || channels != 1 || bits != 16) {
        return result;
    }

    const std::size_t data_offset = position + 8;
    if (data_offset + data_size > bytes.size()) {
        return result;
    }

    result.pcm.resize(data_size / 2);
    for (std::size_t i = 0; i < result.pcm.size(); i++) {
        const std::int16_t sample = static_cast<std::int16_t>(
            static_cast<std::int16_t>(bytes[data_offset + i * 2])
            | (static_cast<std::int16_t>(bytes[data_offset + i * 2 + 1]) << 8));
        result.pcm[i] = static_cast<float>(sample) / 32768.0f;
    }
    result.ok = true;
    return result;
}

std::string Trim(std::string line) {
    // PowerShell pipes can prepend a UTF-8 BOM to the first stdin line.
    if (line.size() >= 3
        && static_cast<unsigned char>(line[0]) == 0xEF
        && static_cast<unsigned char>(line[1]) == 0xBB
        && static_cast<unsigned char>(line[2]) == 0xBF) {
        line.erase(0, 3);
    }
    std::size_t start = line.find_first_not_of(" \t\r\n");
    if (start == std::string::npos) {
        return "";
    }
    std::size_t end = line.find_last_not_of(" \t\r\n");
    return line.substr(start, end - start + 1);
}

std::string Flatten(const std::string& text) {
    std::string flat;
    flat.reserve(text.size());
    for (char c : text) {
        flat.push_back(c == '\n' || c == '\r' ? ' ' : c);
    }
    return flat;
}

void Transcribe(whisper_context* ctx, const std::string& id, const std::string& path) {
    auto wav = ReadWav16kMono(path);
    if (!wav.ok) {
        std::printf("E %s invalid_audio\n", id.c_str());
        std::fflush(stdout);
        return;
    }

    whisper_full_params params = whisper_full_default_params(WHISPER_SAMPLING_GREEDY);
    params.n_threads = g_threads;
    params.language = g_language.c_str();
    params.no_timestamps = true;
    // Fresh-context isolation for a persistent worker: never reuse past
    // transcript tokens across requests (equivalent to a fresh decode state).
    params.no_context = true;
    params.suppress_nst = g_suppress_nst;
    params.temperature = g_temperature;
    if (g_no_fallback) {
        // The CLI sets temperature_inc to 0 when --no-fallback is used; replicate
        // that so decoding is a single deterministic pass at the initial temperature.
        params.temperature_inc = 0.0f;
    }
    if (g_best_of >= 0) {
        params.greedy.best_of = g_best_of;
    }
    if (g_max_context >= 0) {
        params.n_max_text_ctx = g_max_context;
    }
    if (!g_initial_prompt.empty()) {
        params.initial_prompt = g_initial_prompt.c_str();
        params.carry_initial_prompt = g_carry_initial_prompt;
    }
    if (g_vad) {
        params.vad = true;
        params.vad_model_path = g_vad_model.c_str();
        params.vad_params.threshold = g_vad_threshold;
        params.vad_params.min_speech_duration_ms = g_vad_min_speech_duration_ms;
        params.vad_params.min_silence_duration_ms = g_vad_min_silence_duration_ms;
        params.vad_params.speech_pad_ms = g_vad_speech_pad_ms;
        params.vad_params.samples_overlap = g_vad_samples_overlap;
    }
    params.abort_callback = AbortCallback;
    params.abort_callback_user_data = nullptr;
    g_abort = false;

    const auto start = std::chrono::steady_clock::now();
    const int result = whisper_full(ctx, params, wav.pcm.data(), static_cast<int>(wav.pcm.size()));
    const long long total_ms = ElapsedMs(start);

    if (result != 0) {
        std::printf("F %s %s\n", id.c_str(), g_abort.load() ? "aborted" : "decode_failed");
        std::fflush(stdout);
        return;
    }

    std::string text;
    const int segments = whisper_full_n_segments(ctx);
    for (int i = 0; i < segments; i++) {
        const char* segment = whisper_full_get_segment_text(ctx, i);
        if (segment) {
            if (i > 0) {
                text += ' ';
            }
            text += segment;
        }
    }

    const whisper_timings* timings = whisper_get_timings(ctx);
    std::printf("R %s %s\n", id.c_str(), Flatten(text).c_str());
    std::printf(
        "M %s load_ms=0 sample_ms=%.0f encode_ms=%.0f decode_ms=%.0f batchd_ms=%.0f prompt_ms=%.0f total_ms=%lld tokens=%d\n",
        id.c_str(),
        timings ? timings->sample_ms : 0.0f,
        timings ? timings->encode_ms : 0.0f,
        timings ? timings->decode_ms : 0.0f,
        timings ? timings->batchd_ms : 0.0f,
        timings ? timings->prompt_ms : 0.0f,
        static_cast<long long>(total_ms),
        segments);
    std::fflush(stdout);
    whisper_reset_timings(ctx);
}

} // namespace

int main(int argc, char** argv) {
    std::string model_path;

    for (int i = 1; i < argc; i++) {
        std::string arg = argv[i];
        auto next = [&]() -> std::string {
            return i + 1 < argc ? std::string(argv[++i]) : std::string();
        };

        if (arg == "--model") {
            model_path = next();
        } else if (arg == "--vad-model") {
            g_vad_model = next();
        } else if (arg == "--threads") {
            g_threads = std::atoi(next().c_str());
        } else if (arg == "--prompt") {
            g_initial_prompt = next();
        } else if (arg == "--language") {
            g_language = next();
        } else if (arg == "--max-context") {
            g_max_context = std::atoi(next().c_str());
        } else if (arg == "--best-of") {
            g_best_of = std::atoi(next().c_str());
        } else if (arg == "--temperature") {
            g_temperature = static_cast<float>(std::atof(next().c_str()));
        } else if (arg == "--no-fallback") {
            g_no_fallback = true;
        } else if (arg == "--no-suppress-nst") {
            g_suppress_nst = false;
        } else if (arg == "--no-carry-prompt") {
            g_carry_initial_prompt = false;
        } else if (arg == "--no-vad") {
            g_vad = false;
        } else if (arg == "--gpu-device") {
            g_gpu_device = std::atoi(next().c_str());
        } else if (arg == "--cpu") {
            g_use_gpu = false;
        } else if (arg == "--abort-file") {
            g_abort_file = next();
        } else if (arg == "--version") {
            std::printf(
                "lafazflow-whisper-worker 0.1.0 backend=%s whisper=968eebe7\n",
                g_use_gpu ? "cuda" : "cpu");
            return 0;
        } else if (arg == "--vad-params") {
            std::string raw = next();
            std::size_t start = 0;
            while (start <= raw.size()) {
                std::size_t comma = raw.find(',', start);
                std::string pair = raw.substr(
                    start,
                    comma == std::string::npos ? std::string::npos : comma - start);
                std::size_t eq = pair.find('=');
                if (eq != std::string::npos) {
                    std::string key = pair.substr(0, eq);
                    std::string value = pair.substr(eq + 1);
                    if (key == "vt") {
                        g_vad_threshold = static_cast<float>(std::atof(value.c_str()));
                    } else if (key == "vspd") {
                        g_vad_min_speech_duration_ms = std::atoi(value.c_str());
                    } else if (key == "vsd") {
                        g_vad_min_silence_duration_ms = std::atoi(value.c_str());
                    } else if (key == "vp") {
                        g_vad_speech_pad_ms = std::atoi(value.c_str());
                    } else if (key == "vo") {
                        g_vad_samples_overlap = static_cast<float>(std::atof(value.c_str()));
                    }
                }
                if (comma == std::string::npos) {
                    break;
                }
                start = comma + 1;
            }
        } else {
            std::fprintf(stderr, "worker: unknown argument '%s'\n", arg.c_str());
            PrintUsage();
            return 2;
        }
    }

    if (model_path.empty()) {
        PrintUsage();
        return 2;
    }

    if (g_abort_file.empty()) {
        const char* envAbortFile = std::getenv("LAFAZFLOW_ABORT_FILE");
        if (envAbortFile) {
            g_abort_file = envAbortFile;
        }
    }

    whisper_context_params cparams = whisper_context_default_params();
    cparams.use_gpu = g_use_gpu;
    cparams.gpu_device = g_gpu_device;

    const auto load_start = std::chrono::steady_clock::now();
    // Use the with-state init: whisper_full's VAD path requires ctx->state.
    // Per-request isolation is provided by no_context=true (never reuse past
    // transcript tokens), matching the CLI's single-file semantics.
    whisper_context* ctx = whisper_init_from_file_with_params(model_path.c_str(), cparams);
    if (!ctx) {
        std::fprintf(stderr, "worker: failed to load model '%s'\n", model_path.c_str());
        return 3;
    }
    const long long load_ms = ElapsedMs(load_start);

    std::size_t slash = model_path.find_last_of("/\\");
    std::string model_name = slash == std::string::npos ? model_path : model_path.substr(slash + 1);
    std::printf("READY model=%s backend=%s\n", model_name.c_str(), g_use_gpu ? "cuda" : "cpu");
    std::printf("LOAD %lld\n", load_ms);
    std::fflush(stdout);

    std::thread abortWatcher;
    if (!g_abort_file.empty()) {
        abortWatcher = std::thread([] {
            while (!g_shutdown.load()) {
                bool exists = false;
                {
                    std::ifstream probe(g_abort_file.c_str());
                    exists = probe.good();
                }
                if (exists) {
                    g_abort.store(true);
                    std::remove(g_abort_file.c_str());
                }
                std::this_thread::sleep_for(std::chrono::milliseconds(20));
            }
        });
    }

    std::string line;
    while (std::getline(std::cin, line)) {
        line = Trim(line);
        if (line.empty()) {
            continue;
        }
        if (line == "Q") {
            break;
        }
        if (line == "PING") {
            std::printf("PONG\n");
            std::fflush(stdout);
            continue;
        }
        if (line.rfind("C ", 0) == 0) {
            g_abort = true;
            std::printf("A %s\n", line.substr(2).c_str());
            std::fflush(stdout);
            continue;
        }
        if (line.rfind("T ", 0) == 0) {
            std::size_t space = line.find(' ', 2);
            if (space == std::string::npos) {
                std::fprintf(stderr, "worker: malformed T command\n");
                continue;
            }
            std::string id = line.substr(2, space - 2);
            std::string path = line.substr(space + 1);
            Transcribe(ctx, id, path);
            continue;
        }
        std::fprintf(stderr, "worker: unknown command '%s'\n", line.c_str());
    }

    g_shutdown.store(true);
    if (abortWatcher.joinable()) {
        abortWatcher.join();
    }
    whisper_free(ctx);
    return 0;
}
