#include "IOStream.h"

#include <cstdio>
#include <stdarg.h>

#include "StringUtils.h"

#include "ScoreDefinitions.h"

constexpr U32 SCORE_VERSION = 13;
constexpr U32 TIMELINE_FILE_NUM_DIGITS = 4;

static_assert(TIMELINE_FILE_NUM_DIGITS > 0);

namespace IO
{ 
    namespace Utils
    { 
        // -----------------------------------------------------------------------------------------------------------
        U64 StringLength(const char* s)
        {
            U64 len = 0u;
            while(*s){++s; ++len;}
            return len;
        }

        FILE* OpenFile( const char* filename, const char* mode )
        {
#if defined(WIN32) || defined(_WIN32) || defined(__WIN32__)
            FILE* file;
            const errno_t result = fopen_s(&file, filename, mode);
            return result ? nullptr : stream;
#else
            return fopen(filename, mode);
#endif
        }
    }

    //////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // General
    //////////////////////////////////////////////////////////////////////////////////////////////////////////////

    int GetDataVersion() { return static_cast<int>(SCORE_VERSION); }

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

        FILE* stream = Utils::OpenFile(filename, "rb");
        
        if (stream == nullptr)
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
        FILE* stream = Utils::OpenFile(filename, "wb");

        if (stream == nullptr) 
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

        FILE* stream = Utils::OpenFile(filename, "rb");

        if (stream == nullptr) 
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
        void Write(const void* buffer, const U64 elementSize, const U64 elementCount);

    private: 
        bool AppendTimelineExtension(fastl::string& filename);

    private:
        FILE* file; 
    };

    // -----------------------------------------------------------------------------------------------------------
    void TextOutputStream::Impl::Open(const char* filename)
    { 
        file = Utils::OpenFile(filename, "wb");

        if (file == nullptr) 
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
    void TextOutputStream::Impl::Write(const void* buffer, const U64 elementSize, const U64 elementCount)
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
    void TextOutputStream::Append(const char* txt, const U64 length)
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
            U64 strSize = str.length();
            do
            {
                const U8 val = strSize < 0x80 ? strSize & 0x7F : (strSize & 0x7F) | 0x80;
                fwrite(&val, sizeof(U8), 1, stream);
                strSize >>= 7;
            } while (strSize);

            fwrite(str.c_str(), str.length(), 1, stream);
        }

        // -----------------------------------------------------------------------------------------------------------
        void BinarizeStringHash(FILE* stream, const TCompileStrings& strings, U64 strHash)
        {
            TCompileStrings::const_iterator found = strings.find(strHash);
            if (found != strings.end())
            {
                BinarizeString(stream, found->second);
            }
            else
            {
                BinarizeString(stream, fastl::string("?"));
            }
        }

        // -----------------------------------------------------------------------------------------------------------
        void BinarizeStringPath(FILE* stream, const TCompileStrings& strings, U64 strHash)
        {
            TCompileStrings::const_iterator found = strings.find(strHash);
            if (found != strings.end())
            {
                fastl::string pathBase = found->second;
                StringUtils::ToPathBaseName(pathBase);
                BinarizeString(stream, pathBase);
            }
            else
            {
                BinarizeString(stream, fastl::string("?"));
            }
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
        void BinarizeIncluderUnitMap(FILE* stream, const TCompileIncluderUnitMap& includerMap)
        {
            BinarizeU32(stream, static_cast<U32>(includerMap.size()));
            for (const TCompileIncluderUnitMap::value_type& pair : includerMap)
            {
                BinarizeU32(stream, pair.first);
                BinarizeU32(stream, pair.second);
            }
        }

        // -----------------------------------------------------------------------------------------------------------
        void BinarizeIncluderInclMap(FILE* stream, const TCompileIncluderInclMap& includerMap)
        {
            BinarizeU32(stream, static_cast<U32>(includerMap.size()));
            for (const TCompileIncluderInclMap::value_type& pair : includerMap)
            {
                BinarizeU32(stream, pair.first);
                BinarizeU64(stream, pair.second.accumulated);
                BinarizeU32(stream, pair.second.count);
                BinarizeU32(stream, pair.second.maximum);
                BinarizeU32(stream, pair.second.maxId);
            }
        }

        // -----------------------------------------------------------------------------------------------------------
        void BinarizeIncluder( FILE* stream, const CompileIncluder& includer )
        {
            BinarizeIncluderInclMap( stream, includer.includes );
            BinarizeIncluderUnitMap( stream, includer.units );
        }

        // -----------------------------------------------------------------------------------------------------------
        void BinarizeIncluders(FILE* stream, const TCompileIncluders& includers )
        {
			BinarizeU32( stream, static_cast< U32 >( includers.size() ) );
			for( const CompileIncluder& includer : includers )
			{
                BinarizeIncluder( stream, includer );
			}
        }

        // -----------------------------------------------------------------------------------------------------------
        void BinarizeUnit(FILE* stream, const TCompileStrings& strings, const CompileUnit& unit)
        { 
            //Name
            BinarizeStringPath(stream, strings, unit.nameHash);
            
            //values
            for (U32 value : unit.values)
            { 
                BinarizeU32(stream, value);
            }
        }

        // -----------------------------------------------------------------------------------------------------------
        void BinarizeUnits(FILE* stream, const TCompileStrings& strings, const TCompileUnits& units)
        {
            BinarizeU32(stream,static_cast<U32>(units.size()));
            for (const CompileUnit& unit : units)
            { 
                BinarizeUnit(stream,strings,unit);
            }
        }

        // -----------------------------------------------------------------------------------------------------------
        void BinarizeGlobalsStr(FILE* stream, const TCompileStrings& strings, const TCompileDatas& globals)
        {
            BinarizeU32(stream,static_cast<U32>(globals.size()));
            for (const CompileData& data : globals)
            { 
                BinarizeStringHash(stream,strings,data.nameHash);
				BinarizeU64(stream, data.accumulated);
				BinarizeU64(stream, data.selfAccumulated);
				BinarizeU32(stream, data.minimum);
				BinarizeU32(stream, data.maximum);
				BinarizeU32(stream, data.selfMaximum);
				BinarizeU32(stream, data.count);
				BinarizeU32(stream, data.maxId);
                BinarizeU32(stream, data.selfMaxId);
                BinarizeU64(stream, data.unitAccumulated);
                BinarizeU32(stream, data.unitCount);
            }
        }

        // -----------------------------------------------------------------------------------------------------------
        void BinarizeGlobalsPath(FILE* stream, const TCompileStrings& strings, const TCompileDatas& globals)
        {
            BinarizeU32(stream, static_cast<U32>(globals.size()));
            for (const CompileData& data : globals)
            {
                BinarizeStringPath(stream, strings, data.nameHash);
                BinarizeU64(stream, data.accumulated);
                BinarizeU64(stream, data.selfAccumulated);
                BinarizeU32(stream, data.minimum);
                BinarizeU32(stream, data.maximum);
                BinarizeU32(stream, data.selfMaximum);
                BinarizeU32(stream, data.count);
                BinarizeU32(stream, data.maxId);
                BinarizeU32(stream, data.selfMaxId);
                BinarizeU64(stream, data.unitAccumulated);
                BinarizeU32(stream, data.unitCount);
            }
        }

        // -----------------------------------------------------------------------------------------------------------
        void BinarizeTags( FILE* stream, const TCompileStrings& strings, const TTags& tags )
        {
			BinarizeU32( stream, static_cast< U32 >( tags.size() ) );
            for( U64 nameHash : tags )
            {
                BinarizeStringHash(stream, strings, nameHash);
            }
        }

        // -----------------------------------------------------------------------------------------------------------
        void BinarizeFolders(FILE* stream, const TCompileFolders& folders)
        {
            BinarizeU32(stream, static_cast<U32>(folders.size()));
            for (const CompileFolder& folder : folders)
            {
                BinarizeString(stream, folder.name);
                BinarizeU32(stream, static_cast<U32>(folder.children.size()));
                for (const auto& child : folder.children)
                {
                    BinarizeU32(stream, child.second);
                }
                
                BinarizeU32(stream, static_cast<U32>(folder.unitIds.size()));
                for (const U32 id : folder.unitIds)
                {
                    BinarizeU32(stream, id);
                }

                BinarizeU32(stream, static_cast<U32>(folder.includeIds.size()));
                for (const U32 id : folder.includeIds)
                {
                    BinarizeU32(stream, id);
                }
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
                BinarizeU32(stream,evt.nameId);
                BinarizeU8(stream,static_cast<CompileCategoryType>(evt.category));
            }
        }

        // -----------------------------------------------------------------------------------------------------------
        void BinarizeSession(FILE* stream, const CompileSession& session)
        {
            BinarizeU64(stream, session.fullDuration);

            for (U64 i = 0; i < ToUnderlying(CompileCategory::DisplayCount); ++i)
            {
                BinarizeU64(stream, session.totals[i]);
            }
        }
    }

    //////////////////////////////////////////////////////////////////////////////////////////////////////////////

    class ScoreBinarizer::Impl
    {
    public: 
        Impl(const char* _path, unsigned int _timelinesPerFile)
            : path(_path)
            , timelineStream(nullptr)
            , timelineCount(0u)
            , timelinesPerFile(_timelinesPerFile)
        {}

        FILE* NextTimelineStream();
        void CloseTimelineStream();

        U32 GetTimelinesPerFile() const { return timelinesPerFile; }

        void BinarizeGlobals( const ScoreData& data );
        void BinarizeMain( const ScoreData& data );

    private: 
        bool AppendTimelineExtension(fastl::string& filename);

    public: 
        const char* path;

    private:
        FILE*       timelineStream; 
        U64         timelineCount;
        U32         timelinesPerFile;
    };

    //////////////////////////////////////////////////////////////////////////////////////////////////////////////

    // -----------------------------------------------------------------------------------------------------------
    bool ScoreBinarizer::Impl::AppendTimelineExtension(fastl::string& filename)
    { 
        U64 extensionNumber = timelineCount / timelinesPerFile;

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
                timelineStream = Utils::OpenFile(filename.c_str(), "wb");
                if (timelineStream == nullptr) 
                { 
                    LOG_ERROR("Unable to create output file %s",filename.c_str());
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

    // -----------------------------------------------------------------------------------------------------------
    void ScoreBinarizer::Impl::BinarizeGlobals(const ScoreData& data)
    {
        bool hasContent = false;
        constexpr U64 firstIndex = ToUnderlying(CompileCategory::Include) + 1;
        constexpr U64 lastIndex = ToUnderlying(CompileCategory::GatherFull);
        for (U64 i = firstIndex; i < lastIndex; ++i)
        {
            hasContent = hasContent || data.globals[i].empty();
        }

        if (!hasContent)
        {
            return;
        }

        fastl::string filename = path;
        filename.append(".gbl");

        LOG_INFO("Writing to file %s", filename.c_str());

        FILE* stream = Utils::OpenFile(filename.c_str(), "wb");
       
        if (stream == nullptr)
        {
            LOG_ERROR("Unable to create output file %s", filename.c_str());
            return;
        }

        //Header
        Utils::BinarizeU32(stream, SCORE_VERSION);

        for (U64 i = firstIndex; i < lastIndex; ++i)
        {
            if (i == ToUnderlying(CompileCategory::OptimizeModule))
            {
                Utils::BinarizeGlobalsPath(stream, data.strings, data.globals[i]);
            }
            else
            {
                Utils::BinarizeGlobalsStr(stream, data.strings, data.globals[i]);
            }
        }

        Utils::BinarizeTags(stream, data.strings, data.otherTags);

        fclose(stream);

        LOG_INFO("Global datas exported!");
    }

    // -----------------------------------------------------------------------------------------------------------
    void ScoreBinarizer::Impl::BinarizeMain(const ScoreData& data)
    {
        const char* filename = path;

        LOG_PROGRESS("Writing to file %s", filename);

        FILE* stream = Utils::OpenFile(filename, "wb");

        if (stream == nullptr)
        {
            LOG_ERROR("Unable to create output file %s", filename);
            return;
        }

        //Header
        Utils::BinarizeU32(stream, SCORE_VERSION);
        Utils::BinarizeU32(stream, GetTimelinesPerFile());

        //Session
        Utils::BinarizeSession(stream, data.session);

        //Content
        Utils::BinarizeUnits(stream, data.strings, data.units);
        Utils::BinarizeGlobalsPath(stream, data.strings, data.globals[ToUnderlying(CompileCategory::Include)]);
        
        Utils::BinarizeFolders(stream, data.folders);

        Utils::BinarizeIncluders(stream, data.includers);

        fclose(stream);

        LOG_INFO("Units exported!");
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
        //do this one first as the Scoredata file close might trigger refreshers on listeners ( it needs to be the last file to be created ) 
        m_impl->BinarizeGlobals( data );
        m_impl->BinarizeMain( data );

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

            LOG_INFO("Timeline exported (Hash: 0x%llx)", timeline.nameHash);
        }
    }
}
