#pragma once

#define LOG_ALWAYS(...)   IO::Log(IO::Verbosity::Always,__VA_ARGS__)
#define LOG_ERROR(...)    IO::Log(IO::Verbosity::Always,"[ERROR] "##__VA_ARGS__)
#define LOG_PROGRESS(...) IO::Log(IO::Verbosity::Progress,__VA_ARGS__)
#define LOG_INFO(...)     IO::Log(IO::Verbosity::Info,__VA_ARGS__)

struct ScoreData;

namespace IO
{ 
    //////////////////////////////////////////////////////////////////////////////////////////
    // Logging

    enum class Verbosity
    { 
        Always, 
        Progress,
        Info
    };

    void SetVerbosityLevel(const Verbosity level);
    void Log(const Verbosity level, const char* format,...);

    //////////////////////////////////////////////////////////////////////////////////////////
    // File Input

    using FileBuffer = char*; 
    
    FileBuffer ReadFile(const char* filename);
    void DestroyBuffer(FileBuffer& buffer);

    //////////////////////////////////////////////////////////////////////////////////////////
    // File Output

    void Binarize(const char* filename, const ScoreData& data);
}
