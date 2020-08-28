#pragma once

struct ExportParams 
{ 
    //TODO ~ ramonv ~ have a thought about MSVC

    enum class Source
    { 
        Clang, 
        MSVC
    };

    ExportParams()
        : input(nullptr)
        , output("compileData.scor")
        , source(Source::MSVC)
    {}

    const char* input; 
    const char* output;
    Source      source; 
};

namespace CommandLine
{ 
    int Parse(ExportParams& args, int argc, char* argv[]);
}