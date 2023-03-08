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

    enum class TemplateArguments
    {
        Enabled,
        Disabled
    };
    ExportParams();

    const char*        input;
    const char*        output;
    Source             source;
    Command            command;
    Detail             detail;
    Includers          includers;
    Timeline           timeline;
    Detail             timelineDetail;
    TemplateArguments  templateArguments;
    unsigned int       timelinePacking;
};

namespace CommandLine
{ 
    int Parse(ExportParams& args, int argc, char* argv[]);
}