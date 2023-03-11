#include "Common/CommandLine.h"
#include "Common/Context.h"
#include "Common/IOStream.h"
#include "Common/Timers.h"
#include "Extractors/MSVCScore.h"
#include "Extractors/ClangScore.h"

constexpr int FAILURE = -1;

// -----------------------------------------------------------------------------------------------------------
template<typename Extractor>
int ExecuteCommand(const ExportParams& params)
{ 
    switch(params.command)
    { 
    case ExportParams::Command::Start:    
        return Extractor::StartRecording(params);
    case ExportParams::Command::Cancel:    
        return Extractor::CancelRecording(params);
    case ExportParams::Command::Stop:     
        return Extractor::StopRecording(params);
    case ExportParams::Command::Generate: 
        return Extractor::GenerateScore(params);
    case ExportParams::Command::Clean:
        return Extractor::Clean(params);
    default:
        LOG_ERROR("Unknown command provided.");
        return FAILURE;
    }
}

// -----------------------------------------------------------------------------------------------------------
int main(int argc, char* argv[])
{
    Time::Timer timer;
    timer.Capture();

    //Parse Command Line arguments
    Context::Scoped<ExportParams> params;    
    if (CommandLine::Parse(params.Get(),argc,argv) != 0) 
    {     
        return FAILURE;
    } 

    //Execute exporter
    int result = FAILURE;

    switch(params.Get().source)
    { 
    case ExportParams::Source::Clang:  
        result = ExecuteCommand<Clang::Extractor>(params.Get()); 
        break;

    case ExportParams::Source::MSVC:  
        result = ExecuteCommand<MSVC::Extractor>(params.Get()); 
        break;

    default: 
        LOG_ERROR("No compiler specificed. Define the input source using '-clang' or '-msvc'");
        return FAILURE;
    }

    timer.Capture();
    IO::LogTime(IO::Verbosity::Progress,"Execution Time: ",timer.GetElapsed());

    return result;
}