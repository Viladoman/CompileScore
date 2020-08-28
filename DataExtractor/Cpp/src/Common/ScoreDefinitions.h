#pragma once

#include "BasicTypes.h"
#include "../fastl/vector.h"
#include "../fastl/string.h"
#include "../fastl/unordered_map.h"

using CompileCategoryType = U8; 

enum class CompileCategory : CompileCategoryType
{ 
    Include = 0, 
    ParseClass,
    ParseTemplate,
    InstantiateClass, 
    InstantiateFunction, 
    CodeGenFunction, 
    OptimizeModule, 
    OptimizeFunction, 
    Other,
    RunPass,
    PendingInstantiations,
    FrontEnd,
    BackEnd,
    ExecuteCompiler,
    Invalid,

    FullCount, 
    GahterCount = RunPass,
    DisplayCount = Invalid,
};

constexpr CompileCategoryType ToUnderlying(CompileCategory input){ return static_cast<CompileCategoryType>(input);}

struct CompileUnit
{ 
    fastl::string name;
    U32 values[ToUnderlying(CompileCategory::DisplayCount)];
};

struct CompileData
{ 
    CompileData()
        : accumulated(0u)
        , min(0xffffffff)
        , max(0u)
        , count(0u)
    {}

    U64 accumulated; 
    U32 min; 
    U32 max; 
    U32 count;
};

struct CompileEvent
{ 
    CompileEvent()
        : category(CompileCategory::Invalid)
        , start(0u)
        , duration(0u)
    {}

    CompileEvent(CompileCategory _category, U32 _start, U32 _duration, const fastl::string& _name)
        : category(_category)
        , start(_start)
        , duration(_duration)
        , name(_name)
    {}

    fastl::string   name; 
    CompileCategory category; 
    U32             start; 
    U32             duration;
};

using TCompileDataDictionary  = fastl::unordered_map<fastl::string,CompileData>;
using TCompileUnits  = fastl::vector<CompileUnit>;
using TCompileEvents = fastl::vector<CompileEvent>;

struct ScoreTimeline
{ 
    fastl::string  name;
    TCompileEvents events;
};

struct ScoreData
{ 
    TCompileUnits          units;
    TCompileDataDictionary globals[ToUnderlying(CompileCategory::GahterCount)];
};

