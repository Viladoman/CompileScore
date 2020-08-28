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
        LOG_ALWAYS("MSVC vcperf trace to Compile Score Data Extractor\n"); 
        LOG_ALWAYS("Command Legend:\n"); 
        LOG_ALWAYS("-input  (-i) : The path to the input folder to parse for -ftime-trace data\n"); 
        LOG_ALWAYS("-output (-o) : The output file full path for the results ('%s' by default)\n",defaultParams.output); 
        LOG_ALWAYS("-msvc        : Forces the system to use the MSVC importer for .etl traces\n"); 
        LOG_ALWAYS("-clang       : Forces the system to use the Clang importer searching for .json traces in the input path\n"); 
    }

    // -----------------------------------------------------------------------------------------------------------
    int Parse(ExportParams& params, int argc, char* argv[])
    { 
        //No args
        if (argc <= 1) 
        {
            LOG_ALWAYS("No input found. Type '?' for help.\n");
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
                else if (Utils::StringCompare(argValue,"-msvc") == 0){ params.source = ExportParams::Source::MSVC; }
                else if (Utils::StringCompare(argValue,"-clang") == 0){ params.source = ExportParams::Source::Clang; }

                //TODO ~ ramonv ~ add verbosity options
            }
            else if (params.input == nullptr)
            { 
                //We assume that the first free argument is the actual input file
                params.input = argValue;
            }
        }

        //TODO ~ ramonv ~ validate Params

        return 0;
    }
}