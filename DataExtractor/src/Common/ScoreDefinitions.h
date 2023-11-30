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
    Other,   //TODO ~ ramonv ~ this should not be part of the display section as it is just leftovers 
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
        , selfAccumulated(0ull)
        , unitAccumulated(0ull)
        , minimum(0xffffffff)
        , maximum(0u)
        , selfMaximum(0u)
        , maxId(InvalidCompileId)
        , selfMaxId(InvalidCompileId)
        , count(0u)
        , unitCount(0u)
    {}

    CompileData(const U64 _nameHash)
        : nameHash(_nameHash)
        , accumulated(0ull)
        , selfAccumulated(0ull)
        , unitAccumulated(0ull)
        , minimum(0xffffffff)
        , maximum(0u)
        , selfMaximum(0u)
        , maxId(InvalidCompileId)
        , selfMaxId(InvalidCompileId)
        , count(0u)
        , unitCount(0u)
    {}

    U64 nameHash; 
    U64 accumulated; 
    U64 selfAccumulated;
    U64 unitAccumulated;
    U32 minimum; 
    U32 maximum; 
    U32 selfMaximum; // Without children's time
    U32 maxId; //filled by the ScoreProcessor
    U32 selfMaxId;
    U32 count;
    U32 unitCount;
};

struct CompileEvent
{ 
    CompileEvent()
        : nameHash(0ull)
        , selfDuration(0u)
        , nameId(InvalidCompileId)
        , start(0u)
        , duration(0u)
        , category(CompileCategory::Invalid)
    {}

    CompileEvent(CompileCategory _category, U32 _start, U32 _duration, U64 _nameHash)
        : nameHash(_nameHash)
        , selfDuration(_duration)
        , nameId(InvalidCompileId)
        , start(_start)
        , duration(_duration)
        , category(_category)
    {}

    //NOT EXPORTED
    U64             nameHash;
    U32             selfDuration;

    //EXPORTED
    U32             nameId; //filled by the ScoreProcessor
    U32             start; 
    U32             duration;
    CompileCategory category; 
};

struct CompileIncluderInclData
{
    CompileIncluderInclData()
        : maximum(0u)
        , count(0u)
        , maxId(InvalidCompileId)
        , accumulated(0u)
    {}

    U64 accumulated;
    U32 count;
    U32 maximum;
    U32 maxId;
};

using TCompileIncluderInclMap = fastl::unordered_map<U32,CompileIncluderInclData>;
using TCompileIncluderUnitMap = fastl::unordered_map<U32,U32>;

struct CompileIncluder
{
    TCompileIncluderInclMap  includes;
    TCompileIncluderUnitMap  units;
};

using TCompileDataDictionary = fastl::unordered_map<U64,U32>;

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

