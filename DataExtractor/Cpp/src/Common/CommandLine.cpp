#include "CommandLine.h"

#include "IOStream.h"

ExportParams::ExportParams()
    : input(nullptr)
    , output("compileData.scor")
    , source(Source::Invalid)
    , detail(Detail::Full)
    , timeline(Timeline::Enabled)
    , timelinePacking(100)
{}

namespace CommandLine
{ 
    namespace Utils
    { 
        // -----------------------------------------------------------------------------------------------------------
        int StringCompare(const char* s1, const char* s2)
        {
            for(;*s1 && (*s1 == *s2);++s1,++s2){}
            return *(const unsigned char*)s1 - *(const unsigned char*)s2;
        }

        // -----------------------------------------------------------------------------------------------------------
        bool StringToUInt(unsigned int& output, const char* str)
        { 
            unsigned int ret = 0; 
            while (char c = *str)
            { 
                if (c < '0' || c > '9') 
                { 
                    return false;
                }

                ret=ret*10+(c-'0'); 
                ++str;
            }

            output = ret;
            return true;
        } 
    }

    //////////////////////////////////////////////////////////////////////////////////////////////////////////////

    // -----------------------------------------------------------------------------------------------------------
    void DisplayHelp()
    {
        ExportParams defaultParams;
        LOG_ALWAYS("Compile Score Data Extractor"); 
        LOG_ALWAYS("");
        LOG_ALWAYS("Converts the compiler build trace data into 'scor' format."); 
        LOG_ALWAYS("");
        LOG_ALWAYS("Command Legend:"); 

        LOG_ALWAYS("-input        (-i)  : The path to the input folder to parse for -ftime-trace data or the direct path to the .etl file"); 
        LOG_ALWAYS("-output       (-o)  : The output file full path for the results ('%s' by default)",defaultParams.output); 

        LOG_ALWAYS("-clang  |  -msvc    : Sets the system to use the Clang (.json traces) or MSVC (.etl traces) importer"); 

        LOG_ALWAYS("-detail       (-d)  : Sets the level of detail exported - example: '-d 1'"); 
        LOG_ALWAYS("\t0 - None"); 
        LOG_ALWAYS("\t1 - Basic - w/ include"); 
        LOG_ALWAYS("\t2 - FrontEnd - w/ include, parse, instantiate"); 
        LOG_ALWAYS("\t3 - Full (default)"); 

        LOG_ALWAYS("-notimeline   (-nt) : No timeline files will be generated"); 
        LOG_ALWAYS("-timelinepack (-tp) : Sets the number of timelines packed in the same file - example '-tp 200' (100 by default)");

        LOG_ALWAYS("-verbosity    (-v)  : Sets the verbosity level - example: '-v 1'"); 
        LOG_ALWAYS("\t0 - Silent"); 
        LOG_ALWAYS("\t1 - Progress (default)"); 
        LOG_ALWAYS("\t2 - Full"); 
    }

    // -----------------------------------------------------------------------------------------------------------
    int Parse(ExportParams& params, int argc, char* argv[])
    { 
        //No args
        if (argc <= 1) 
        {
            LOG_ERROR("No arguments found. Type '?' for help.");
            return -1;
        }

        //Check for Help
        for (int i=1;i<argc;++i)
        { 
            if (Utils::StringCompare(argv[i],"?") == 0)
            { 
                DisplayHelp();
                return -1;
            }
        }

        //Parse arguments
        for(int i=1;i < argc;++i)
        { 
            char* argValue = argv[i];
            if (argValue[0] == '-')
            { 
                if ((Utils::StringCompare(argValue,"-i")==0 || Utils::StringCompare(argValue,"-input")==0) && (i+1) < argc)
                { 
                    ++i;
                    params.input = argv[i];
                }
                else if ((Utils::StringCompare(argValue,"-o")==0 || Utils::StringCompare(argValue,"-output")==0) && (i+1) < argc)
                { 
                    ++i;
                    params.output = argv[i];
                }
                else if (Utils::StringCompare(argValue,"-msvc") == 0)
                { 
                    params.source = ExportParams::Source::MSVC; 
                }
                else if (Utils::StringCompare(argValue,"-clang") == 0)
                { 
                    params.source = ExportParams::Source::Clang; 
                }
                else if ((Utils::StringCompare(argValue,"-nt")==0 || Utils::StringCompare(argValue,"-notimeline")==0))
                {
                    params.timeline = ExportParams::Timeline::Disabled;
                }
                else if ((Utils::StringCompare(argValue,"-tp")==0 || Utils::StringCompare(argValue,"-timelinepack")==0) && (i+1) < argc)
                { 
                    ++i;
                    unsigned int value = 0;
                    if (Utils::StringToUInt(value,argv[i]) && value > 0)
                    { 
                        params.timelinePacking = value;
                    }
                    
                }
                else if ((Utils::StringCompare(argValue,"-d")==0 || Utils::StringCompare(argValue,"-detail")==0) && (i+1) < argc)
                { 
                    ++i;
                    unsigned int value = 0;
                    if (Utils::StringToUInt(value,argv[i]) && value < static_cast<unsigned int>(ExportParams::Detail::Invalid))
                    { 
                        params.detail = ExportParams::Detail(value);
                    }
                }
                else if ((Utils::StringCompare(argValue,"-v")==0 || Utils::StringCompare(argValue,"-verbosity")==0) && (i+1) < argc)
                {
                    ++i;
                    unsigned int value = 0;
                    if (Utils::StringToUInt(value,argv[i]) && value < static_cast<unsigned int>(IO::Verbosity::Invalid))
                    { 
                        IO::SetVerbosityLevel(IO::Verbosity(value));
                    }
                } 
            }
            else if (params.input == nullptr)
            { 
                //We assume that the first free argument is the actual input file
                params.input = argValue;
            }
        }

        if (params.input == nullptr)
        { 
            LOG_ERROR("No input found. Type '?' for help.");
            return -1;
        }

        //TODO ~ validation and auto type deduction if not present 
        // - .etl == MSVC 
        // - isFolder == Clang
        
        if (params.source == ExportParams::Source::Invalid)
        {
            LOG_ERROR("Ambigous input. Define the input source using '-clang' or '-msvc'");
            return -1;
        }

        return 0;
    }
}