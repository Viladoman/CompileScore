#include "CommandLine.h"

#include "IOStream.h"

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
    }

    //////////////////////////////////////////////////////////////////////////////////////////////////////////////

    // -----------------------------------------------------------------------------------------------------------
    void DisplayHelp()
    {
        ExportParams defaultParams;
        LOG_ALWAYS("Compile Score Data Extractor"); 
        LOG_ALWAYS("");
        LOG_ALWAYS("Converts the comilers build trace data into 'scor' format."); 
        LOG_ALWAYS("");
        LOG_ALWAYS("Command Legend:"); 
        LOG_ALWAYS("-input     (-i) : The path to the input folder to parse for -ftime-trace data"); 
        LOG_ALWAYS("-output    (-o) : The output file full path for the results ('%s' by default)",defaultParams.output); 
        LOG_ALWAYS("-msvc           : Forces the system to use the MSVC importer for .etl traces"); 
        LOG_ALWAYS("-clang          : Forces the system to use the Clang importer searching for .json traces in the input path"); 
        LOG_ALWAYS("-verbosity (-v) : Sets the verbosity level: 0 - only errors, 1 - progress (default), 2 - full"); 
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
                else if ((Utils::StringCompare(argValue,"-v")==0 || Utils::StringCompare(argValue,"-verbosity")==0) && (i+1) < argc)
                {
                    ++i;
                    char digit = argv[i][0];
                    if ( digit >= '0' || digit <= '2' )
                    { 
                        IO::SetVerbosityLevel(IO::Verbosity(digit-'0'));
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