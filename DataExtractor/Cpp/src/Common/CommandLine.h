#pragma once

struct ExportParams 
{ 
    enum class Source
    { 
        Clang, 
        MSVC,

        Invalid
    };

    enum class Detail
    { 
        None,
        Basic,
        FrontEnd, 
        Full,

        Invalid
    };

    enum class Timeline
    { 
        Enabled, 
        Disabled,
    };

    ExportParams();

    const char*  input; 
    const char*  output;
    Source       source; 
    Detail       detail;
    Timeline     timeline;
    unsigned int timelinePacking;
};

namespace CommandLine
{ 
    int Parse(ExportParams& args, int argc, char* argv[]);
}