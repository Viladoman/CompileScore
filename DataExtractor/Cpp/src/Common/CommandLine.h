#pragma once

struct ExportParams 
{ 
    enum class Source
    { 
        Clang, 
        MSVC,
        Invalid
    };

    ExportParams()
        : input(nullptr)
        , output("compileData.scor")
        , source(Source::Invalid)
    {}

    const char* input; 
    const char* output;
    Source      source; 
};

namespace CommandLine
{ 
    int Parse(ExportParams& args, int argc, char* argv[]);
}