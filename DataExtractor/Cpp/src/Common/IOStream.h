#pragma once

#define LOG_ALWAYS(...)   { IO::Log(IO::Verbosity::Always,__VA_ARGS__);             IO::Log(IO::Verbosity::Always,"\n");}
#define LOG_ERROR(...)    { IO::Log(IO::Verbosity::Always,"[ERROR] "##__VA_ARGS__); IO::Log(IO::Verbosity::Always,"\n");}
#define LOG_PROGRESS(...) { IO::Log(IO::Verbosity::Progress,__VA_ARGS__);           IO::Log(IO::Verbosity::Progress,"\n");}
#define LOG_INFO(...)     { IO::Log(IO::Verbosity::Info,__VA_ARGS__);               IO::Log(IO::Verbosity::Info,"\n");}

struct ScoreData;
struct ScoreTimeline;

namespace IO
{ 
    //////////////////////////////////////////////////////////////////////////////////////////
    // Logging

    enum class Verbosity
    { 
        Always, 
        Progress,
        Info, 

        Invalid
    };

    void SetVerbosityLevel(const Verbosity level);
    void Log(const Verbosity level, const char* format,...);
    void LogTime(const Verbosity level, const char* prefix, long miliseconds);

    //////////////////////////////////////////////////////////////////////////////////////////
    // File Input

    using FileBuffer = char*; 
    
    FileBuffer ReadFile(const char* filename);
    void DestroyBuffer(FileBuffer& buffer);

    //////////////////////////////////////////////////////////////////////////////////////////
    // File Output

    class Binarizer
    { 
    public: 
        Binarizer(const char* baseFileName, unsigned int timelinePacking);
        ~Binarizer();
        
        void Binarize(const ScoreData& data);
        void Binarize(const ScoreTimeline& timeline);

    private: 
        class Impl; 
        Impl* m_impl;
    };
}
