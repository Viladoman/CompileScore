#pragma once

namespace CompileScore
{
    struct Result;
}

namespace IO
{
    //////////////////////////////////////////////////////////////////////////////////////////
    // Logging

    void Log(const char* format, ...);

    //////////////////////////////////////////////////////////////////////////////////////////
    // Export

    bool ToFile(const CompileScore::Result& result, const char* filename);
    void ToPrint(const CompileScore::Result& result);
}