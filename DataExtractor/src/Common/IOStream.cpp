#include "IOStream.h"

#include <cstdio>
#include <cstdarg>

#include "ScoreDefinitions.h"

constexpr U32 SCORE_VERSION = 4;
constexpr U32 TIMELINE_FILE_NUM_DIGITS = 4;

static_assert(TIMELINE_FILE_NUM_DIGITS > 0);

namespace IO
{ 
    namespace Utils
    { 
        // -----------------------------------------------------------------------------------------------------------
        size_t StringLength(const char* s)
        {
            size_t len = 0u;
            while(*s){++s; ++len;}
            return len;
        }
    }

    //////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // Logging
    //////////////////////////////////////////////////////////////////////////////////////////////////////////////

    struct GlobalParams
    { 
        GlobalParams()
            : verbosity(Verbosity::Progress)
        {}

        Verbosity verbosity;
    };
    
    GlobalParams g_globals;

    //////////////////////////////////////////////////////////////////////////////////////////////////////////////

    // -----------------------------------------------------------------------------------------------------------
    void SetVerbosityLevel(const Verbosity level)
    { 
        g_globals.verbosity = level;
    }

    // -----------------------------------------------------------------------------------------------------------
    void Log(const Verbosity level, const char* format,...)
    { 
        if (level <= g_globals.verbosity)
        { 
            va_list argptr;
            va_start(argptr, format);
            vfprintf(stderr, format, argptr);
            va_end(argptr);
        }
    }

    // -----------------------------------------------------------------------------------------------------------
    void LogTime(const Verbosity level, const char* prefix, long miliseconds)
    { 
        long seconds = miliseconds/1000; 
        miliseconds  = miliseconds - (seconds*1000);

        long minutes = seconds/60; 
        seconds      = seconds - (minutes*60);

        long hours   = minutes/60; 
        minutes      = minutes - (hours*60);

             if (hours)   Log(level, "%s%02uh %02um",  prefix, hours,   minutes);
        else if (minutes) Log(level, "%s%02um %02us",  prefix, minutes, seconds);
        else if (seconds) Log(level, "%s%02us %02ums", prefix, seconds, miliseconds);
        else              Log(level, "%s%02ums",       prefix, miliseconds);
    }

    //////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // File System 
    //////////////////////////////////////////////////////////////////////////////////////////////////////////////

    // -----------------------------------------------------------------------------------------------------------
    bool DeleteFile(const char* filename)
    { 
        return remove(filename) == 0;
    }

    //////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // Binary File IO
    //////////////////////////////////////////////////////////////////////////////////////////////////////////////

    // -----------------------------------------------------------------------------------------------------------
    RawBuffer::RawBuffer()
        : buff(nullptr) 
        , size(0u)
    {}

    // -----------------------------------------------------------------------------------------------------------
    RawBuffer ReadRawFile(const char* filename)
    { 
        RawBuffer content; 

        FILE* stream;
        const errno_t result = fopen_s(&stream,filename,"rb");

        if (result) 
        { 
            LOG_ERROR("Unable to open input file: %s.", filename);
        }
        else 
        { 
            fseek(stream, 0, SEEK_END);
            long fsize = ftell(stream);
            fseek(stream, 0, SEEK_SET);  // same as rewind(f);

            content.size = fsize;
            content.buff = new char[fsize];
            if (fread(content.buff, 1, fsize, stream) == 0)
            { 
                LOG_ERROR("Something went wrong while reading the file %s.",filename);
                DestroyBuffer(content.buff);
            }

            fclose(stream);
        }

        return content;
    }

    bool WriteRawFile(const char* filename, RawBuffer buffer)
    {
        FILE* stream;
        const errno_t result = fopen_s(&stream,filename,"wb");

        if (result) 
        { 
            LOG_ERROR("Unable to open output file: %s.", filename);
            return false;
        }

        fwrite(buffer.buff,sizeof(char),buffer.size,stream);

        fclose(stream);
        return true;
    } 

    // -----------------------------------------------------------------------------------------------------------
    void DestroyBuffer(RawBuffer& buffer)
    {
        delete [] buffer.buff;
        buffer.buff = nullptr;
        buffer.size = 0;
    }

    //////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // Text File IO
    //////////////////////////////////////////////////////////////////////////////////////////////////////////////

    // -----------------------------------------------------------------------------------------------------------
    FileTextBuffer ReadTextFile(const char* filename)
    {
        FileTextBuffer content = nullptr; 

        FILE* stream;
        const errno_t result = fopen_s(&stream,filename,"rb");

        if (result) 
        { 
            LOG_ERROR("Unable to open the file %s", filename);
        }
        else 
        { 
            fseek(stream, 0, SEEK_END);
            long fsize = ftell(stream);
            fseek(stream, 0, SEEK_SET);  // same as rewind(f);
            
            content = new char[(fsize+1ull)];
            if (fread(content, 1, fsize, stream) == 0)
            { 
                LOG_ERROR("Something went wrong while reading the file %s.",filename);
                DestroyBuffer(content);
            }
            else 
            { 
                content[fsize] = '\0';
            }
        }
        
        fclose(stream);
        
        return content;
    }

    //////////////////////////////////////////////////////////////////////////////////////////////////////////////
    class TextOutputStream::Impl
    {
    public: 
        Impl(const char* filename) : file(nullptr) { Open(filename); }
        ~Impl(){ Close(); }

        Impl(const Impl& input) = delete;
        Impl(Impl&& input) = delete;
        Impl& operator = (const Impl& input) = delete;
        Impl& operator = (Impl&& input) = delete;

        bool IsValid() const { return file != nullptr; }
        void Open(const char* filename);
        void Close();
        void Write(const void* buffer, const size_t elementSize, const size_t elementCount);

    private: 
        bool AppendTimelineExtension(fastl::string& filename);

    private:
        FILE* file; 
    };

    // -----------------------------------------------------------------------------------------------------------
    void TextOutputStream::Impl::Open(const char* filename)
    { 
        const errno_t result = fopen_s(&file,filename,"wb");

        if (result) 
        { 
            LOG_ERROR("Unable to open output file: %s.", filename);
        }
    }

    // -----------------------------------------------------------------------------------------------------------
    void TextOutputStream::Impl::Close()
    {
        if (file)
        { 
            fclose(file);
        }
    }

    // -----------------------------------------------------------------------------------------------------------
    void TextOutputStream::Impl::Write(const void* buffer, const size_t elementSize, const size_t elementCount)
    {
        if (file)
        { 
            fwrite(buffer,elementSize,elementCount,file);
        }
    }

    //////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // -----------------------------------------------------------------------------------------------------------
    TextOutputStream::TextOutputStream(const char* filename)
        : m_impl( new Impl(filename) )
    {}

    // -----------------------------------------------------------------------------------------------------------
    TextOutputStream::~TextOutputStream()
    { 
        delete m_impl;
    }

    // -----------------------------------------------------------------------------------------------------------
    void TextOutputStream::Append(const char* txt, const size_t length)
    { 
        m_impl->Write(txt,sizeof(char),length);
    }

    // -----------------------------------------------------------------------------------------------------------
    bool TextOutputStream::IsValid() const
    { 
        return m_impl->IsValid();
    }

    // -----------------------------------------------------------------------------------------------------------
    void TextOutputStream::Append(const char* txt)
    { 
        Append(txt,Utils::StringLength(txt));
    }

    // -----------------------------------------------------------------------------------------------------------
    void TextOutputStream::Append(const char c)
    { 
        m_impl->Write(&c,sizeof(char),1);
    }

    // -----------------------------------------------------------------------------------------------------------
    void DestroyBuffer(FileTextBuffer& buffer)
    {
        delete [] buffer;
        buffer = nullptr;
    }

    //////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // Score Output
    //////////////////////////////////////////////////////////////////////////////////////////////////////////////

    namespace Utils
    { 
        // -----------------------------------------------------------------------------------------------------------
        void BinarizeString(FILE* stream, const fastl::string& str)
        { 
            //Perform size encoding in 7bitSize format
            size_t strSize = str.length(); 
            do 
            { 
                const U8 val = strSize < 0x80? strSize & 0x7F : (strSize & 0x7F) | 0x80;
                fwrite(&val,sizeof(U8),1,stream);
                strSize >>= 7;
            }
            while(strSize);

            fwrite(str.c_str(),str.length(),1,stream);
        }

        // -----------------------------------------------------------------------------------------------------------
        void BinarizeU8(FILE* stream, const U8 input)
        { 
            fwrite(&input,sizeof(U8),1,stream);
        }

        // -----------------------------------------------------------------------------------------------------------
        void BinarizeU32(FILE* stream, const U32 input)
        { 
            fwrite(&input,sizeof(U32),1,stream);
        }

        // -----------------------------------------------------------------------------------------------------------
        void BinarizeU64(FILE* stream, const U64 input)
        { 
            fwrite(&input,sizeof(U64),1,stream);
        }

        // -----------------------------------------------------------------------------------------------------------
        void BinarizeUnit(FILE* stream, const CompileUnit unit)
        { 
            BinarizeString(stream,unit.name); 
            for (U32 value : unit.values)
            { 
                BinarizeU32(stream, value);
            }
        }

        // -----------------------------------------------------------------------------------------------------------
        void BinarizeUnits(FILE* stream, const TCompileUnits& units)
        {
            //TODO ~ ramonv ~ check for U32 overflow
            BinarizeU32(stream,static_cast<U32>(units.size()));
            for (const CompileUnit& unit : units)
            { 
                BinarizeUnit(stream,unit);
            }
        }

        // -----------------------------------------------------------------------------------------------------------
        void BinarizeGlobals(FILE* stream, const TCompileDatas& globals)
        {
            //TODO ~ ramonv ~ check for U32 overflow
            BinarizeU32(stream,static_cast<unsigned int>(globals.size()));
            for (const auto& entry : globals)
            { 
                const CompileData& data = entry;
                BinarizeString(stream,entry.name);
                BinarizeU64(stream,data.accumulated);
                BinarizeU32(stream,data.min);
                BinarizeU32(stream,data.max);
                BinarizeU32(stream,data.count);
                BinarizeU32(stream,data.maxId);
            }
        }

        // -----------------------------------------------------------------------------------------------------------
        void BinarizeTimelineEvents(FILE* stream, const TCompileEvents& events)
        { 
            BinarizeU32(stream,static_cast<unsigned int>(events.size()));
            for (const CompileEvent& evt : events)
            { 
                BinarizeU32(stream,evt.start);
                BinarizeU32(stream,evt.duration);
                BinarizeU32(stream,static_cast<U32>(evt.nameId)); //TODO ~ ramonv ~ careful with overflows
                BinarizeU8(stream,static_cast<CompileCategoryType>(evt.category));
            }
        }
    }

    //////////////////////////////////////////////////////////////////////////////////////////////////////////////

    class ScoreBinarizer::Impl
    {
    public: 
        Impl(const char* _path, unsigned int _timelinesPerFile)
            : path(_path)
            , timelinesPerFile(_timelinesPerFile)
            , timelineStream(nullptr)
            , timelineCount(0u)
        {}

        FILE* NextTimelineStream();
        void CloseTimelineStream();

        U32 GetTimelinesPerFile() const { return timelinesPerFile; }

    private: 
        bool AppendTimelineExtension(fastl::string& filename);

    public: 
        const char* path;

    private:
        FILE*       timelineStream; 
        size_t      timelineCount;
        U32         timelinesPerFile;
    };

    //////////////////////////////////////////////////////////////////////////////////////////////////////////////

    // -----------------------------------------------------------------------------------------------------------
    bool ScoreBinarizer::Impl::AppendTimelineExtension(fastl::string& filename)
    { 
        size_t extensionNumber = timelineCount / timelinesPerFile;

        char digits[TIMELINE_FILE_NUM_DIGITS];
        for(int i=TIMELINE_FILE_NUM_DIGITS-1;i>=0;--i,extensionNumber/=10)
        { 
            digits[i]= (extensionNumber % 10) + '0';
        }

        if (extensionNumber > 0) 
        { 
            LOG_ERROR("Reached timeline file number limit");
            return false; 
        } 

        filename += ".t"; 
        for(int i=0;i<TIMELINE_FILE_NUM_DIGITS;++i)
        { 
            filename += digits[i];
        }

        return true;
    }

    // -----------------------------------------------------------------------------------------------------------
    FILE* ScoreBinarizer::Impl::NextTimelineStream()
    {
        if ((timelineCount % timelinesPerFile) == 0)
        { 
            CloseTimelineStream();

            fastl::string filename = path;
            if (AppendTimelineExtension(filename))
            { 
                const errno_t result = fopen_s(&timelineStream,filename.c_str(),"wb");
                if (result) 
                { 
                    LOG_ERROR("Unable to create output file %s",filename);
                    timelineStream = nullptr;
                }

                //Add the file header
                Utils::BinarizeU32(timelineStream,SCORE_VERSION);
            }
        }

        ++timelineCount;
        return timelineStream;
    } 

    // -----------------------------------------------------------------------------------------------------------
    void ScoreBinarizer::Impl::CloseTimelineStream()
    { 
        if (timelineStream)
        {
            fclose(timelineStream);
            timelineStream = nullptr;
        }
    }

    //////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // -----------------------------------------------------------------------------------------------------------
    ScoreBinarizer::ScoreBinarizer(const char* path, unsigned int timelinesPacking)
        : m_impl( new Impl(path, timelinesPacking))
    {}

    // -----------------------------------------------------------------------------------------------------------
    ScoreBinarizer::~ScoreBinarizer()
    { 
        m_impl->CloseTimelineStream();
        delete m_impl;
    }

    // -----------------------------------------------------------------------------------------------------------
    void ScoreBinarizer::Binarize(const ScoreData& data)
    { 
        const char* filename = m_impl->path;
        LOG_PROGRESS("Writing to file %s",filename);

        FILE* stream;
        const errno_t result = fopen_s(&stream,filename,"wb");

        if (result) 
        { 
            LOG_ERROR("Unable to create output file!");
            return;
        }

        //Header
        Utils::BinarizeU32(stream,SCORE_VERSION);
        Utils::BinarizeU32(stream,m_impl->GetTimelinesPerFile());

        Utils::BinarizeUnits(stream,data.units);
        for (int i=0;i<ToUnderlying(CompileCategory::GatherFull);++i)
        { 
            Utils::BinarizeGlobals(stream,data.globals[i]);
        }    

        fclose(stream);

        LOG_PROGRESS("Done!");
    }

    // -----------------------------------------------------------------------------------------------------------
    void ScoreBinarizer::Binarize(const ScoreTimeline& timeline)
    { 
        if (FILE* stream = m_impl->NextTimelineStream())
        { 
            Utils::BinarizeU32(stream,static_cast<unsigned int>(timeline.tracks.size()));
            for (const TCompileEvents& events : timeline.tracks)
            { 
                Utils::BinarizeTimelineEvents(stream,events);
            }

            LOG_INFO("Timeline for %s exported", timeline.name.c_str());
        }
    }
}
