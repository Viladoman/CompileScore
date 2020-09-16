#pragma once

#include "BasicTypes.h"
#include "../fastl/vector.h"
#include "../fastl/string.h"
#include "../fastl/unordered_map.h"

constexpr U32 InvalidCompileId = 0xffffffff;

using CompileCategoryType = U8; 

enum class CompileCategory : CompileCategoryType
{ 
    Include = 0, 
    ParseClass,
    ParseTemplate,
    InstantiateClass, 
    InstantiateFunction, 
    CodeGenFunction, 
    OptimizeFunction, 
    //Gather End

    PendingInstantiations,
    OptimizeModule, 
    FrontEnd,
    BackEnd,
    ExecuteCompiler,
    Other,
    //Display End

    RunPass,
    CodeGenPasses,
    PerModulePasses,
    DebugType,
    DebugGlobalVariable,
    Invalid,

    FullCount, 
    GahterCount = PendingInstantiations,
    DisplayCount = RunPass,
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
        , maxId(InvalidCompileId)
        , count(0u)
    {}

    CompileData(const fastl::string& _name)
        : name(_name)
        , accumulated(0u)
        , min(0xffffffff)
        , max(0u)
        , maxId(InvalidCompileId)
        , count(0u)
    {}

    fastl::string name; 
    U64           accumulated; 
    U32           min; 
    U32           max; 
    U32           maxId; //filled by the ScoreProcessor
    U32           count;
};

struct CompileEvent
{ 
    CompileEvent()
        : category(CompileCategory::Invalid)
        , start(0u)
        , duration(0u)
        , nameId(InvalidCompileId)
    {}

    CompileEvent(CompileCategory _category, U32 _start, U32 _duration, const fastl::string& _name)
        : category(_category)
        , start(_start)
        , duration(_duration)
        , name(_name)
        , nameId(InvalidCompileId)
    {}

    fastl::string   name; 
    U32             nameId; //filled by the ScoreProcessor
    U32             start; 
    U32             duration;
    CompileCategory category; 
};

using TCompileDataDictionary  = fastl::unordered_map<fastl::string,U32>;
using TCompileDatas  = fastl::vector<CompileData>;
using TCompileUnits  = fastl::vector<CompileUnit>;
using TCompileEvents = fastl::vector<CompileEvent>;

struct ScoreTimeline
{ 
    TCompileEvents events;
    fastl::string  name;
};

struct ScoreData
{ 
    TCompileUnits units;
    TCompileDatas globals[ToUnderlying(CompileCategory::GahterCount)];
    TCompileDataDictionary globalsDictionary[ToUnderlying(CompileCategory::GahterCount)];
};

