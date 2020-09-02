#include "Common/CommandLine.h"
#include "Common/IOStream.h"
#include "Common/Timers.h"
#include "Extractors/MSVCScore.h"
#include "Extractors/ClangScore.h"

constexpr int FAILURE = -1;

// -----------------------------------------------------------------------------------------------------------
int main(int argc, char* argv[])
{
    Time::Timer timer;
    timer.Capture();

    //Parse Command Line arguments
    ExportParams params;
    if (CommandLine::Parse(params,argc,argv) != 0) 
    {     
        LOG_ERROR("Unable to parse the arguments!");
        return FAILURE;
    } 

    //Execute exporter
    int result = FAILURE;

    switch(params.source)
    { 
    case ExportParams::Source::Clang: 
        result = Clang::ExtractScore(params); 
        break;

    case ExportParams::Source::MSVC:  
        result = MSVC::ExtractScore(params); 
        break;

    default: 
        LOG_ERROR("Unknown exporter requested"); 
        return FAILURE;
    }

    timer.Capture();
    IO::LogTime(IO::Verbosity::Progress,"Execution Time: ",timer.GetElapsed());

    return result;
}