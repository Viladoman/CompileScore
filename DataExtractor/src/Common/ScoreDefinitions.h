#pragma once

#include "BasicTypes.h"
#include "../fastl/vector.h"
#include "../fastl/string.h"
#include "../fastl/unordered_map.h"
#include "../fastl/unordered_set.h"

constexpr U32 InvalidCompileId = 0xffffffff;

using CompileCategoryType = U8; 

enum class CompileCategory : CompileCategoryType
{ 
    Include = 0, 
    ParseClass,
    ParseTemplate,
    InstantiateClass, 
    InstantiateFunction, 
    InstantiateVariable,
    InstantiateConcept,
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
    PerFunctionPasses,
    PerModulePasses,
    DebugType,
    DebugGlobalVariable,
    Invalid,

    FullCount,
    GatherNone     = Include, 
    GatherBasic    = ParseClass, 
    GatherFrontEnd = CodeGenFunction,
    GatherFull     = PendingInstantiations,
    DisplayCount   = RunPass,
};

constexpr CompileCategoryType ToUnderlying(CompileCategory input){ return static_cast<CompileCategoryType>(input);}

struct CompileUnitContext
{
    CompileUnitContext()
        : startTime{ 0u,0u }
        , threadId{ 0u,0u }
    {}

    U64 startTime[2];
    U32 threadId[2];
};

struct CompileUnit
{ 
    CompileUnit(const U32 _unitId = 0u)
        : unitId(_unitId)
        , nameHash(0u)
        , values()
    {}

    U64                nameHash;
    CompileUnitContext context;
    U32                unitId; //filled by the ScoreProcessor
    U32                values[ToUnderlying(CompileCategory::DisplayCount)];
};

struct CompileData
{ 
    CompileData()
        : nameHash(0ull)
        , accumulated(0ull)
        , minimum(0xffffffff)
        , maximum(0u)
        , selfMaximum(0u)
        , maxId(InvalidCompileId)
        , count(0u)
    {}

    CompileData(const U64 _nameHash)
        : nameHash(_nameHash)
        , accumulated(0ull)
        , minimum(0xffffffff)
        , maximum(0u)
        , selfMaximum(0u)
        , maxId(InvalidCompileId)
        , count(0u)
    {}

    U64 nameHash; 
    U64 accumulated; 
    U32 minimum; 
    U32 maximum; 
    U32 selfMaximum; // Without children's time
    U32 maxId; //filled by the ScoreProcessor
    U32 count;
};

struct CompileEvent
{ 
    CompileEvent()
        : category(CompileCategory::Invalid)
        , start(0u)
        , duration(0u)
        , nameHash(0ull)
        , nameId(InvalidCompileId)
        , selfDuration(0u)
    {}

    CompileEvent(CompileCategory _category, U32 _start, U32 _duration, U64 _nameHash)
        : category(_category)
        , start(_start)
        , duration(_duration)
        , nameHash(_nameHash)
        , nameId(InvalidCompileId)
        , selfDuration(_duration)
    {}

    U64             nameHash;
    U32             nameId; //filled by the ScoreProcessor
    U32             start; 
    U32             duration;
    U32             selfDuration; // Without children's duration
    CompileCategory category; 
};

using TCompileIndexSet       = fastl::unordered_set<U32>;
using TCompileDataDictionary = fastl::unordered_map<U64,U32>;

struct CompileIncluder
{
    TCompileIndexSet includes;
    TCompileIndexSet units;
};

struct CompileFolder
{
    CompileFolder(){}

    CompileFolder(const char* nameStr, size_t length)
        : name(nameStr,length)
    {}

    fastl::string          name;
    TCompileDataDictionary children;
    fastl::vector<U32>     unitIds;
    fastl::vector<U32>     includeIds;
};

struct CompileSession
{
    CompileSession()
        : fullDuration(0u)
        , numThreads(0u)
    {}

    U64 totals[ToUnderlying(CompileCategory::DisplayCount)];
    U64 fullDuration;
    U32 numThreads;
};

using TCompileDatas            = fastl::vector<CompileData>;
using TCompileUnits            = fastl::vector<CompileUnit>;
using TCompileIncluders        = fastl::vector<CompileIncluder>;
using TCompileEvents           = fastl::vector<CompileEvent>;
using TCompileEventTracks      = fastl::vector<TCompileEvents>;
using TCompileStrings          = fastl::unordered_map<U64,fastl::string>;
using TCompileFolders          = fastl::vector<CompileFolder>;

struct ScoreTimeline
{ 
    TCompileEventTracks tracks;
    U64                 nameHash;
};

struct ScoreData
{ 
    //exported data
    CompileSession    session;
    TCompileUnits     units;
    TCompileDatas     globals[ToUnderlying(CompileCategory::GatherFull)];
    TCompileIncluders includers;
    TCompileStrings   strings;
    TCompileFolders   folders;

    //helper data
    TCompileDataDictionary globalsDictionary[ToUnderlying(CompileCategory::GatherFull)];
};

