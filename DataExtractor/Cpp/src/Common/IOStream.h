#pragma once

#define LOG_ALWAYS(...)   IOStream::Log(IOStream::Verbosity::Always,__VA_ARGS__)
#define LOG_ERROR(...)    IOStream::Log(IOStream::Verbosity::Always,__VA_ARGS__)
#define LOG_PROGRESS(...) IOStream::Log(IOStream::Verbosity::Progress,__VA_ARGS__)
#define LOG_INFO(...)     IOStream::Log(IOStream::Verbosity::Info,__VA_ARGS__)

struct ScoreData;

namespace IOStream
{ 
    // Logging
    enum class Verbosity
    { 
        Always, 
        Progress,
        Info
    };

    void SetVerbosityLevel(const Verbosity level);
    void Log(const Verbosity level, const char* format,...);

    // File Output
    void Binarize(const char* filename, const ScoreData& data);
}
