#include "Common/CommandLine.h"
#include "Common/IOStream.h"
#include "Extractors/MSVCScore.h"
#include "Extractors/ClangScore.h"

int main(int argc, char* argv[])
{
    //Parse Command Line arguments
    ExportParams params;
    if (CommandLine::Parse(params,argc,argv) != 0) 
    {     
        LOG_ERROR("Unable to parse the arguments!");
        return -1;
    } 

    //Execute exporter
    switch(params.source)
    { 
    case ExportParams::Source::Clang: 
        return Clang::ExtractScore(params);

    case ExportParams::Source::MSVC:  
        return MSVC::ExtractScore(params);

    default: 
        LOG_ERROR("Unknown exporter requested"); break;
    }

    return -1;
}