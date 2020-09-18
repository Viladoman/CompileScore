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
    };

    enum class Timeline
    { 
        Enabled, 
        Disabled,
    };

    ExportParams()
        : input(nullptr)
        , output("compileData.scor")
        , source(Source::Invalid)
        , detail(Detail::Full)
        , timeline(Timeline::Enabled)
    {}

    const char* input; 
    const char* output;
    Source      source; 
    Detail      detail;
    Timeline    timeline;
};

namespace CommandLine
{ 
    int Parse(ExportParams& args, int argc, char* argv[]);
}