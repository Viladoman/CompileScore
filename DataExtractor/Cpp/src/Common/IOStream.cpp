#include "IOStream.h"

#include <cstdio>
#include <cstdarg>

#include "ScoreDefinitions.h"

constexpr U32 SCORE_VERSION = 1;

namespace IOStream
{ 
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
            fprintf(stderr, "\n"); // New Line 
        }
    }

    //////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // Binarization
    //////////////////////////////////////////////////////////////////////////////////////////////////////////////

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
    void BinarizeDictionary(FILE* stream, const TCompileDataDictionary& dictionary)
    { 
        //TODO ~ ramonv ~ check for U32 overflow
        BinarizeU32(stream,static_cast<unsigned int>(dictionary.size()));
        for (const auto& entry : dictionary)
        { 
            BinarizeString(stream,entry.first);
            const CompileData& data = entry.second;

            BinarizeU64(stream,data.accumulated);
            BinarizeU32(stream,data.min);
            BinarizeU32(stream,data.max);
            BinarizeU32(stream,data.count);
        }
    }

    // -----------------------------------------------------------------------------------------------------------
    void Binarize(const char* filename, const ScoreData& data)
    { 
        LOG_PROGRESS("Writing to file %s",filename);

        FILE* stream;
        const errno_t result = fopen_s(&stream,filename,"wb");

        if (result) 
        { 
            LOG_ERROR("Unable to create output file!");
            return;
        }

        BinarizeU32(stream,SCORE_VERSION);

        BinarizeUnits(stream,data.units);
        for (int i=0;i<ToUnderlying(CompileCategory::GahterCount);++i)
        { 
            BinarizeDictionary(stream,data.globals[i]);
        }    

        fclose(stream);

        LOG_PROGRESS("Done!");
    }
}
