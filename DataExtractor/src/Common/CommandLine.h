#pragma once

struct ExportParams 
{ 
    enum class Source
    { 
        Clang, 
        MSVC,

        Invalid
    };

    enum class Command
    { 
        Start, 
        Cancel,
        Stop,
        Generate,
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

    enum class Includers
    {
        Enabled,
        Disabled
    };

    enum class TemplateArgs
    {
        Collapse, 
        Keep,
    };

    ExportParams();

    const char*  input; 
    const char*  output;
    Source       source; 
    Command      command;
    Detail       detail;
    Includers    includers;
    TemplateArgs templateArgs;
    Timeline     timeline;
    Detail       timelineDetail;
    unsigned int timelinePacking;
};

namespace CommandLine
{ 
    int Parse(ExportParams& args, int argc, char* argv[]);
}