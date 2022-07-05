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
    // General

    int GetDataVersion();

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
    // File System 

    bool DeleteFile(const char* filename);

    //////////////////////////////////////////////////////////////////////////////////////////
    // Binary File IO

    struct RawBuffer
    { 
        RawBuffer();

        char*  buff; 
        size_t size;
    };

    RawBuffer ReadRawFile(const char* filename);
    bool WriteRawFile(const char* filename, RawBuffer buffer);
    void DestroyBuffer(RawBuffer& buffer);

    //////////////////////////////////////////////////////////////////////////////////////////
    // Text File IO

    using FileTextBuffer = char*; 
    
    FileTextBuffer ReadTextFile(const char* filename);
    void DestroyBuffer(FileTextBuffer& buffer);
    
    //////////////////////////////////////////////////////////////////////////////////////////
    class TextOutputStream
    { 
    public:
        TextOutputStream(const char* filename);
        ~TextOutputStream();

        TextOutputStream(const TextOutputStream& input) = delete;
        TextOutputStream(TextOutputStream&& input) = delete;
        TextOutputStream& operator = (const TextOutputStream& input) = delete;
        TextOutputStream& operator = (TextOutputStream&& input) = delete;

        bool IsValid() const;
        void Append(const char* txt, const size_t length);
        void Append(const char* txt);
        void Append(const char c);

    private:
        class Impl;
        Impl* m_impl;
    };
    
    //////////////////////////////////////////////////////////////////////////////////////////
    // Score Output

    class ScoreBinarizer
    { 
    public: 
        ScoreBinarizer(const char* baseFileName, unsigned int timelinePacking);
        ~ScoreBinarizer();

        ScoreBinarizer(const ScoreBinarizer& input) = delete;
        ScoreBinarizer(ScoreBinarizer&& input) = delete;
        ScoreBinarizer& operator = (const ScoreBinarizer& input) = delete;
        ScoreBinarizer& operator = (ScoreBinarizer&& input) = delete;

        void Binarize(const ScoreData& data);
        void Binarize(const ScoreTimeline& timeline);

    private: 
        class Impl; 
        Impl* m_impl;
    };

}
