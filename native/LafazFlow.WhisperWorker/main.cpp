// LafazFlow persistent Whisper worker - M4 named-pipe protocol.
//
// The worker is a named-pipe CLIENT. The LafazFlow supervisor creates the
// pipe server (with a current-user-only security descriptor), starts this
// process with --pipe <name>, and drives it with versioned binary frames.
//
// Frame: [4-byte little-endian payload length][payload]
// Payload header (80 bytes):
//   0  byte   version = 1
//   1  byte   kind    (1..6 request ops; 0x80|op for responses)
//   2  byte   status  (responses)
//   3  byte   reserved
//   4  byte[16] requestId
//  20  byte[16] sessionId
//  36  uint32 deadlineMs (requests)
//  40  byte[32] settings fingerprint
//  72  uint32 audioFormat (1 = 16 kHz mono s16)
//  76  uint32 sampleCount
//  80  ...    data (PCM for Final/Preview; UTF-8 text for Final/Preview
//              responses; u64 load_ms for Initialize; text for Health)

#include "whisper.h"

#include <windows.h>
#include <sddl.h>

#include <atomic>
#include <chrono>
#include <condition_variable>
#include <cstdint>
#include <cstdio>
#include <cstring>
#include <deque>
#include <mutex>
#include <string>
#include <thread>
#include <vector>

namespace {

constexpr std::uint8_t kProtocolVersion = 1;
constexpr std::uint32_t kMaxFrameBytes = 16u * 1024u * 1024u;
constexpr std::uint32_t kHeaderBytes = 80u;

enum Op : std::uint8_t {
    OpInitialize = 1,
    OpPreview = 2,
    OpFinal = 3,
    OpCancel = 4,
    OpHealth = 5,
    OpShutdown = 6
};

enum Status : std::uint8_t {
    StatusOk = 0,
    StatusAborted = 1,
    StatusInvalidRequest = 2,
    StatusBusy = 3,
    StatusInternalError = 4,
    StatusTimeout = 5,
    StatusUnavailable = 6
};

constexpr std::uint32_t kAudioFormatPcm16kMono = 1;

std::atomic<bool> g_abort{false};
std::atomic<bool> g_shutdown{false};

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

whisper_context* g_ctx = nullptr;
std::string g_model_path;
std::string g_model_name;
std::string g_fingerprint(32, '\0');
long long g_uptime_start_ms = 0;
long long g_completed_requests = 0;
std::string g_last_failure = "none";

struct Frame {
    std::uint8_t kind = 0;
    std::uint8_t status = StatusOk;
    std::uint8_t request_id[16] = {0};
    std::uint8_t session_id[16] = {0};
    std::uint32_t deadline_ms = 0;
    std::string fingerprint;
    std::uint32_t audio_format = 0;
    std::uint32_t sample_count = 0;
    std::vector<std::uint8_t> data;
};

std::mutex g_queue_mutex;
std::condition_variable g_queue_cv;
std::deque<Frame> g_queue;
constexpr std::size_t kMaxQueue = 8;

bool AbortCallback(void*) {
    return g_abort.load();
}

long long ElapsedMs(long long start) {
    return std::chrono::duration_cast<std::chrono::milliseconds>(
               std::chrono::steady_clock::now().time_since_epoch())
               .count() -
           start;
}

void PrintUsage() {
    std::fprintf(
        stderr,
        "usage: lafazflow-whisper-worker --pipe <name> --model <path> [--vad-model <path>] "
        "[--threads N] [--prompt <text>] [--max-context N] [--best-of N] "
        "[--temperature X] [--no-fallback] [--no-suppress-nst] "
        "[--no-carry-prompt] [--no-vad] [--vad-params vt,vspd,vsd,vp,vo] "
        "[--gpu-device N] [--cpu] [--version]\n");
}

// Returns 1 on success, 0 on a hard error, 2 on an idle timeout.
int ReadExact(HANDLE pipe, void* buffer, std::size_t size, DWORD idleTimeoutMs = 250) {
    auto* bytes = static_cast<std::uint8_t*>(buffer);
    std::size_t offset = 0;
    while (offset < size) {
        OVERLAPPED overlapped = {};
        overlapped.hEvent = CreateEvent(nullptr, TRUE, FALSE, nullptr);
        DWORD read = 0;
        BOOL ok = ReadFile(pipe, bytes + offset, static_cast<DWORD>(size - offset), &read, &overlapped);
        const DWORD error = GetLastError();
        if (!ok && error == ERROR_IO_PENDING) {
            const DWORD wait = WaitForSingleObject(overlapped.hEvent, idleTimeoutMs);
            if (wait == WAIT_TIMEOUT) {
                CancelIoEx(pipe, &overlapped);
                WaitForSingleObject(overlapped.hEvent, INFINITE);
                CloseHandle(overlapped.hEvent);
                return 2;
            }
            ok = GetOverlappedResult(pipe, &overlapped, &read, FALSE);
        }
        CloseHandle(overlapped.hEvent);
        if (!ok) {
            return 0;
        }
        if (read == 0) {
            return 0;
        }
        offset += read;
    }
    return 1;
}

bool WriteAll(HANDLE pipe, const void* buffer, std::size_t size) {
    const auto* bytes = static_cast<const std::uint8_t*>(buffer);
    std::size_t offset = 0;
    while (offset < size) {
        OVERLAPPED overlapped = {};
        overlapped.hEvent = CreateEvent(nullptr, TRUE, FALSE, nullptr);
        DWORD written = 0;
        BOOL ok = WriteFile(pipe, bytes + offset, static_cast<DWORD>(size - offset), &written, &overlapped);
        const DWORD error = GetLastError();
        if (!ok && error == ERROR_IO_PENDING) {
            WaitForSingleObject(overlapped.hEvent, INFINITE);
            ok = GetOverlappedResult(pipe, &overlapped, &written, FALSE);
        }
        CloseHandle(overlapped.hEvent);
        if (!ok) {
            std::fprintf(stderr, "worker: write failed error=%lu\n", GetLastError());
            return false;
        }
        offset += written;
    }
    return true;
}

void PutU32(std::vector<std::uint8_t>& out, std::size_t offset, std::uint32_t value) {
    out[offset] = static_cast<std::uint8_t>(value & 0xFF);
    out[offset + 1] = static_cast<std::uint8_t>((value >> 8) & 0xFF);
    out[offset + 2] = static_cast<std::uint8_t>((value >> 16) & 0xFF);
    out[offset + 3] = static_cast<std::uint8_t>((value >> 24) & 0xFF);
}

std::uint32_t GetU32(const std::vector<std::uint8_t>& in, std::size_t offset) {
    return static_cast<std::uint32_t>(in[offset])
         | (static_cast<std::uint32_t>(in[offset + 1]) << 8)
         | (static_cast<std::uint32_t>(in[offset + 2]) << 16)
         | (static_cast<std::uint32_t>(in[offset + 3]) << 24);
}

bool SendResponse(
    HANDLE pipe,
    std::uint8_t op,
    std::uint8_t status,
    const std::uint8_t request_id[16],
    const std::uint8_t session_id[16],
    const std::string& fingerprint,
    const std::vector<std::uint8_t>& data) {
    std::vector<std::uint8_t> payload(kHeaderBytes + data.size());
    payload[0] = kProtocolVersion;
    payload[1] = static_cast<std::uint8_t>(0x80 | op);
    payload[2] = status;
    payload[3] = 0;
    std::memcpy(payload.data() + 4, request_id, 16);
    std::memcpy(payload.data() + 20, session_id, 16);
    PutU32(payload, 36, 0);
    std::memcpy(payload.data() + 40, fingerprint.data(), 32);
    PutU32(payload, 72, 0);
    PutU32(payload, 76, 0);
    if (!data.empty()) {
        std::memcpy(payload.data() + kHeaderBytes, data.data(), data.size());
    }

    std::uint32_t length = static_cast<std::uint32_t>(payload.size());
    std::uint8_t header[4] = {
        static_cast<std::uint8_t>(length & 0xFF),
        static_cast<std::uint8_t>((length >> 8) & 0xFF),
        static_cast<std::uint8_t>((length >> 16) & 0xFF),
        static_cast<std::uint8_t>((length >> 24) & 0xFF)
    };
    return WriteAll(pipe, header, 4) && WriteAll(pipe, payload.data(), payload.size());
}

bool ParseFrame(const std::vector<std::uint8_t>& payload, Frame& frame) {
    if (payload.size() < kHeaderBytes || payload[0] != kProtocolVersion) {
        return false;
    }
    frame.kind = payload[1];
    frame.status = payload[2];
    std::memcpy(frame.request_id, payload.data() + 4, 16);
    std::memcpy(frame.session_id, payload.data() + 20, 16);
    frame.deadline_ms = GetU32(payload, 36);
    frame.fingerprint.assign(reinterpret_cast<const char*>(payload.data() + 40), 32);
    frame.audio_format = GetU32(payload, 72);
    frame.sample_count = GetU32(payload, 76);
    frame.data.assign(payload.begin() + kHeaderBytes, payload.end());
    return true;
}

void ReaderThread(HANDLE pipe) {
    while (!g_shutdown.load()) {
        std::uint8_t lengthBytes[4] = {0};
        const int readResult = ReadExact(pipe, lengthBytes, 4);
        if (readResult == 2) {
            continue;
        }
        if (readResult == 0) {
            if (g_shutdown.load()) {
                break;
            }
            break;
        }
        const std::uint32_t length =
            static_cast<std::uint32_t>(lengthBytes[0])
            | (static_cast<std::uint32_t>(lengthBytes[1]) << 8)
            | (static_cast<std::uint32_t>(lengthBytes[2]) << 16)
            | (static_cast<std::uint32_t>(lengthBytes[3]) << 24);
        if (length < kHeaderBytes || length > kMaxFrameBytes) {
            g_shutdown.store(true);
            g_queue_cv.notify_all();
            break;
        }

        std::vector<std::uint8_t> payload(length);
        const int payloadRead = ReadExact(pipe, payload.data(), length);
        if (payloadRead != 1) {
            g_shutdown.store(true);
            g_queue_cv.notify_all();
            break;
        }

        Frame frame;
        if (!ParseFrame(payload, frame)) {
            std::uint8_t zero[16] = {0};
            SendResponse(pipe, 0, StatusInvalidRequest, zero, zero, g_fingerprint, {});
            continue;
        }
        std::fprintf(stderr, "worker: frame kind=%u len=%zu\n", (unsigned)frame.kind, payload.size());

        if (frame.kind == OpCancel) {
            g_abort.store(true);
            SendResponse(pipe, OpCancel, StatusOk, frame.request_id, frame.session_id, g_fingerprint, {});
            continue;
        }

        if (frame.kind == OpFinal) {
            // Final transcription preempts live preview: abort any in-flight
            // preview decode and drop queued preview work.
            g_abort.store(true);
            std::unique_lock<std::mutex> lock(g_queue_mutex);
            g_queue.erase(
                std::remove_if(
                    g_queue.begin(),
                    g_queue.end(),
                    [](const Frame& queued) { return queued.kind == OpPreview; }),
                g_queue.end());
            if (g_queue.size() >= kMaxQueue) {
                SendResponse(pipe, OpFinal, StatusBusy, frame.request_id, frame.session_id, g_fingerprint, {});
                continue;
            }
            g_queue.push_back(std::move(frame));
            g_queue_cv.notify_one();
            continue;
        }

        {
            std::unique_lock<std::mutex> lock(g_queue_mutex);
            if (g_queue.size() >= kMaxQueue) {
                SendResponse(pipe, frame.kind, StatusBusy, frame.request_id, frame.session_id, g_fingerprint, {});
                continue;
            }
            g_queue.push_back(std::move(frame));
        }
        g_queue_cv.notify_one();
    }
}

bool LoadModel(const std::string& model_path) {
    if (g_ctx != nullptr) {
        return true;
    }
    whisper_context_params cparams = whisper_context_default_params();
    cparams.use_gpu = g_use_gpu;
    cparams.gpu_device = g_gpu_device;
    g_ctx = whisper_init_from_file_with_params(model_path.c_str(), cparams);
    if (!g_ctx) {
        return false;
    }
    std::size_t slash = model_path.find_last_of("/\\");
    g_model_name = slash == std::string::npos ? model_path : model_path.substr(slash + 1);
    return true;
}

std::vector<float> PcmFromBytes(const std::vector<std::uint8_t>& bytes, std::uint32_t expectedSamples) {
    std::vector<float> pcm;
    const std::size_t availableSamples = bytes.size() / 2;
    if (availableSamples != expectedSamples) {
        return pcm;
    }
    pcm.resize(availableSamples);
    for (std::size_t i = 0; i < availableSamples; i++) {
        const std::int16_t sample = static_cast<std::int16_t>(
            static_cast<std::int16_t>(bytes[i * 2])
            | (static_cast<std::int16_t>(bytes[i * 2 + 1]) << 8));
        pcm[i] = static_cast<float>(sample) / 32768.0f;
    }
    return pcm;
}

whisper_full_params BuildParams() {
    whisper_full_params params = whisper_full_default_params(WHISPER_SAMPLING_GREEDY);
    params.n_threads = g_threads;
    params.language = g_language.c_str();
    params.no_timestamps = true;
    params.no_context = true;
    params.suppress_nst = g_suppress_nst;
    params.temperature = g_temperature;
    if (g_no_fallback) {
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
    return params;
}

void Transcribe(HANDLE pipe, const Frame& frame) {
    if (g_ctx == nullptr) {
        std::vector<std::uint8_t> data{'n', 'o', '_', 'm', 'o', 'd', 'e', 'l'};
        SendResponse(pipe, frame.kind, StatusUnavailable, frame.request_id, frame.session_id, g_fingerprint, data);
        return;
    }
    if (frame.audio_format != kAudioFormatPcm16kMono) {
        SendResponse(pipe, frame.kind, StatusInvalidRequest, frame.request_id, frame.session_id, g_fingerprint, {});
        return;
    }
    const auto pcm = PcmFromBytes(frame.data, frame.sample_count);
    if (pcm.empty()) {
        SendResponse(pipe, frame.kind, StatusInvalidRequest, frame.request_id, frame.session_id, g_fingerprint, {});
        return;
    }

    g_abort.store(false);
    whisper_full_params params = BuildParams();
    const int result = whisper_full(g_ctx, params, pcm.data(), static_cast<int>(pcm.size()));
    if (result != 0) {
        g_last_failure = g_abort.load() ? "aborted" : "decode_failed";
        if (g_abort.load()) {
            SendResponse(pipe, frame.kind, StatusAborted, frame.request_id, frame.session_id, g_fingerprint, {});
        } else {
            std::string reason = "decode_failed";
            SendResponse(pipe, frame.kind, StatusInternalError, frame.request_id, frame.session_id, g_fingerprint,
                std::vector<std::uint8_t>(reason.begin(), reason.end()));
        }
        return;
    }

    std::string text;
    const int segments = whisper_full_n_segments(g_ctx);
    for (int i = 0; i < segments; i++) {
        const char* segment = whisper_full_get_segment_text(g_ctx, i);
        if (segment) {
            if (i > 0) {
                text += ' ';
            }
            text += segment;
        }
    }
    g_completed_requests++;
    g_last_failure = "none";
    whisper_reset_timings(g_ctx);
    SendResponse(pipe, frame.kind, StatusOk, frame.request_id, frame.session_id, g_fingerprint,
        std::vector<std::uint8_t>(text.begin(), text.end()));
}

void HandleInitialize(HANDLE pipe, const Frame& frame) {
    if (frame.fingerprint.size() != 32) {
        SendResponse(pipe, OpInitialize, StatusInvalidRequest, frame.request_id, frame.session_id, g_fingerprint, {});
        return;
    }
    const auto load_start = std::chrono::steady_clock::now();
    if (!LoadModel(g_model_path)) {
        SendResponse(pipe, OpInitialize, StatusInternalError, frame.request_id, frame.session_id, g_fingerprint, {});
        return;
    }
    const auto load_ms = std::chrono::duration_cast<std::chrono::milliseconds>(
                             std::chrono::steady_clock::now() - load_start)
                             .count();
    std::fprintf(stderr, "worker: initialize load_ms=%lld\n", (long long)load_ms);
    g_fingerprint = frame.fingerprint;
    std::uint8_t loadBytes[8] = {
        static_cast<std::uint8_t>(load_ms & 0xFF),
        static_cast<std::uint8_t>((load_ms >> 8) & 0xFF),
        static_cast<std::uint8_t>((load_ms >> 16) & 0xFF),
        static_cast<std::uint8_t>((load_ms >> 24) & 0xFF),
        static_cast<std::uint8_t>((load_ms >> 32) & 0xFF),
        static_cast<std::uint8_t>((load_ms >> 40) & 0xFF),
        static_cast<std::uint8_t>((load_ms >> 48) & 0xFF),
        static_cast<std::uint8_t>((load_ms >> 56) & 0xFF)
    };
    const bool responseSent = SendResponse(
        pipe, OpInitialize, StatusOk, frame.request_id, frame.session_id, g_fingerprint,
        std::vector<std::uint8_t>(loadBytes, loadBytes + 8));
    std::fprintf(stderr, "worker: initialize response sent=%d\n", responseSent ? 1 : 0);
}

void EngineLoop(HANDLE pipe) {
    while (!g_shutdown.load()) {
        Frame frame;
        {
            std::unique_lock<std::mutex> lock(g_queue_mutex);
            g_queue_cv.wait(lock, [] { return g_shutdown.load() || !g_queue.empty(); });
            if (g_shutdown.load() && g_queue.empty()) {
                break;
            }
            frame = std::move(g_queue.front());
            g_queue.pop_front();
        }

        switch (frame.kind) {
            case OpInitialize:
                HandleInitialize(pipe, frame);
                break;
            case OpFinal:
            case OpPreview:
                Transcribe(pipe, frame);
                break;
            case OpHealth: {
                std::string stats = "uptime_ms=" + std::to_string(ElapsedMs(g_uptime_start_ms))
                    + " completed=" + std::to_string(g_completed_requests)
                    + " last_failure=" + g_last_failure
                    + " model=" + g_model_name
                    + " backend=" + (g_use_gpu ? "cuda" : "cpu")
                    + " fingerprint=" + g_fingerprint;
                SendResponse(pipe, OpHealth, StatusOk, frame.request_id, frame.session_id, g_fingerprint,
                    std::vector<std::uint8_t>(stats.begin(), stats.end()));
                break;
            }
            case OpShutdown:
                SendResponse(pipe, OpShutdown, StatusOk, frame.request_id, frame.session_id, g_fingerprint, {});
                g_shutdown.store(true);
                g_queue_cv.notify_all();
                break;
            default:
                SendResponse(pipe, frame.kind, StatusInvalidRequest, frame.request_id, frame.session_id, g_fingerprint, {});
                break;
        }
    }
}

} // namespace

int main(int argc, char** argv) {
    std::string model_path;
    std::string pipe_name;

    for (int i = 1; i < argc; i++) {
        std::string arg = argv[i];
        auto next = [&]() -> std::string {
            return i + 1 < argc ? std::string(argv[++i]) : std::string();
        };

        if (arg == "--pipe") {
            pipe_name = next();
        } else if (arg == "--model") {
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
        } else if (arg == "--version") {
            std::printf(
                "lafazflow-whisper-worker 0.2.0 protocol=1 backend=%s whisper=968eebe7\n",
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

    if (model_path.empty() || pipe_name.empty()) {
        PrintUsage();
        return 2;
    }
    g_model_path = model_path;

    g_uptime_start_ms = std::chrono::duration_cast<std::chrono::milliseconds>(
                            std::chrono::steady_clock::now().time_since_epoch())
                            .count();

    std::string pipe_path = "\\\\.\\pipe\\" + pipe_name;

    std::string sddl;
    HANDLE token = nullptr;
    if (OpenProcessToken(GetCurrentProcess(), TOKEN_QUERY, &token)) {
        DWORD tokenSize = 0;
        GetTokenInformation(token, TokenUser, nullptr, 0, &tokenSize);
        std::vector<BYTE> tokenBuffer(tokenSize);
        if (GetTokenInformation(token, TokenUser, tokenBuffer.data(), tokenSize, &tokenSize)) {
            const auto* tokenUser = reinterpret_cast<TOKEN_USER*>(tokenBuffer.data());
            LPWSTR sidString = nullptr;
            if (ConvertSidToStringSidW(tokenUser->User.Sid, &sidString)) {
                const int length = WideCharToMultiByte(
                    CP_UTF8, 0, sidString, -1, nullptr, 0, nullptr, nullptr);
                std::string sid(length - 1, '\0');
                WideCharToMultiByte(
                    CP_UTF8, 0, sidString, -1, sid.data(), length, nullptr, nullptr);
                LocalFree(sidString);
                sddl = "D:P(A;;GA;;;SY)(A;;GA;;;BA)(A;;GA;;;" + sid + ")";
            }
        }
        CloseHandle(token);
    }

    SECURITY_ATTRIBUTES securityAttributes = {};
    securityAttributes.nLength = sizeof(SECURITY_ATTRIBUTES);
    securityAttributes.bInheritHandle = FALSE;
    if (!sddl.empty()) {
        ConvertStringSecurityDescriptorToSecurityDescriptorA(
            sddl.c_str(), 1, &securityAttributes.lpSecurityDescriptor, nullptr);
    }

    HANDLE pipe = CreateNamedPipeW(
        std::wstring(pipe_path.begin(), pipe_path.end()).c_str(),
        PIPE_ACCESS_DUPLEX | FILE_FLAG_OVERLAPPED,
        PIPE_TYPE_BYTE | PIPE_READMODE_BYTE | PIPE_WAIT,
        1,
        65536,
        65536,
        0,
        &securityAttributes);
    if (securityAttributes.lpSecurityDescriptor != nullptr) {
        LocalFree(securityAttributes.lpSecurityDescriptor);
    }
    if (pipe == INVALID_HANDLE_VALUE) {
        std::fprintf(stderr, "worker: failed to create pipe %s (error %lu)\n", pipe_path.c_str(), GetLastError());
        return 4;
    }
    if (!ConnectNamedPipe(pipe, nullptr)) {
        const DWORD error = GetLastError();
        if (error != ERROR_PIPE_CONNECTED) {
            std::fprintf(stderr, "worker: ConnectNamedPipe failed (error %lu)\n", error);
            CloseHandle(pipe);
            return 4;
        }
    }
    std::fprintf(stderr, "worker: client connected to pipe\n");

    std::thread reader(ReaderThread, pipe);
    EngineLoop(pipe);

    reader.join();
    if (g_ctx != nullptr) {
        whisper_free(g_ctx);
    }
    CloseHandle(pipe);
    return 0;
}
