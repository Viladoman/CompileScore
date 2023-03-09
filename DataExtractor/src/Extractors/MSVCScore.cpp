#include "MSVCScore.h"

#define VC_EXTRALEAN
#define WIN32_LEAN_AND_MEAN
#include <Windows.h>
#include <DbgHelp.h>
#include <chrono>
#include <CppBuildInsights.hpp>

#include "../fastl/algorithm.h"
#include "../fastl/string.h"
#include "../Common/CommandLine.h"
#include "../Common/Context.h"
#include "../Common/DirectoryUtils.h"
#include "../Common/IOStream.h"
#include "../Common/ScoreDefinitions.h"
#include "../Common/ScoreProcessor.h"
#include "../Common/StringUtils.h"

#include "../Common/CRC64.h"

namespace MSVC
{ 
    constexpr int         FAILURE = EXIT_FAILURE;
    constexpr int         SUCCESS = EXIT_SUCCESS;
    constexpr const char* MSBI_SessionName = "COMPILE_SCORE";
    constexpr int         MSBI_NumberOfPasses = 1;

    namespace MSBI = Microsoft::Cpp::BuildInsights; 

    using TTimeStamp = unsigned long long;
    using TSymbolId  = unsigned long long;
    using TProcessId = unsigned long;
    using TThreadId  = unsigned long;

    namespace Utils
    { 
         // -----------------------------------------------------------------------------------------------------------
        const char* GetPath(const MSBI::Activities::CompilerPass& compilerPass)
        {
            //TODO ~ ramonv ~ technically windows paths can go up to 32k 
            //TODO ~ ramonv ~ check for overflow
            constexpr size_t BUFF_SIZE = 2048;
            static char* str = new char[BUFF_SIZE];

            const wchar_t* fileNameW = compilerPass.InputSourcePath() == nullptr? compilerPass.OutputObjectPath() : compilerPass.InputSourcePath();

            if (fileNameW == nullptr)
            { 
                //Edge case were there is not path to be recovered
                return ""; //return empty but valid path
            }

            size_t inputLength = wcslen(fileNameW);
            size_t charsConverted = 0;
            wcstombs_s(&charsConverted, str, BUFF_SIZE, fileNameW, inputLength);
            return str;
        }
    }

    //////////////////////////////////////////////////////////////////////////////////////////////////////////////

    struct MSVCCompileEvent
    { 
        MSVCCompileEvent()
            : nameSymbol(0u)
        {}

        MSVCCompileEvent(const MSVCCompileEvent&) = default;

        MSVCCompileEvent(CompileEvent&& _event, const TSymbolId _nameSymbol = 0u)
            : event(_event)
            , nameSymbol(_nameSymbol)
        {}

        CompileEvent event; 
        TSymbolId    nameSymbol; 
    };
    using TMSVCCompileEvents = fastl::vector<MSVCCompileEvent>;

    struct MSVCCompileTrack
    { 
        MSVCCompileTrack(const TThreadId _threadId = 0u)
            : threadId(_threadId)
            , sectionStartIndex(0u)
        {}

        TMSVCCompileEvents events;
        TThreadId          threadId; 
        size_t             sectionStartIndex; 
    };
    using TMSVCCompileTracks = fastl::vector<MSVCCompileTrack>;

    //////////////////////////////////////////////////////////////////////////////////////////////////////////////

    class DummyGatherer : public MSBI::IAnalyzer{};

    //////////////////////////////////////////////////////////////////////////////////////////////////////////////

    class Gatherer : public MSBI::IAnalyzer
    {
    private: 
        enum { InvalidIndex = static_cast<size_t>(-1) };


        struct TUEntry
        { 
            using TSymbolsMap = fastl::unordered_map<TSymbolId,U64>;

            TUEntry()
                : nameHash(0ull)
                , timestampOffset(0u)
                , timeSectionAccumulator(0u)
            {}

            MSVCCompileTrack& GetTrack(const TThreadId threadId);

            U64                nameHash;
            TMSVCCompileTracks tracks;
            TSymbolsMap        symbols;
            CompileUnitContext context;
            TTimeStamp         timestampOffset;
            U32                timeSectionAccumulator;
        };
        using TTUCollection = fastl::vector<TUEntry>;
        using TTUDictionary = fastl::unordered_map<U64,size_t>;

        struct TUProcess
        { 
            TUProcess(const TProcessId _processId = 0u);

            TUEntry* GetActiveTU();         
            void ActivateTU(U64 pathHash);
            void ActivateNew();
            void ActivateFirstUnnamed();
            void ClearTU(TUEntry& entry);
            void DeactivateTU();

            TTUCollection data; 
            TTUDictionary dict;
            TProcessId    processId; 
            size_t        activeIndex;
        };
        using TUProcessContainer = fastl::vector<TUProcess>;

    public: 

        Gatherer();
        MSBI::AnalysisControl OnStartActivity(const MSBI::EventStack& eventStack) override; 
        MSBI::AnalysisControl OnStopActivity(const MSBI::EventStack& eventStack) override;    
        MSBI::AnalysisControl OnSimpleEvent(const MSBI::EventStack& eventStack) override;
        void OnCompilerPassStart(const MSBI::Activities::CompilerPass& activity);
        void OnCompilerPassEnded(const MSBI::Activities::CompilerPass& activity);
        void OnIncludeEnded(const MSBI::Activities::FrontEndFile& activity);
        void OnTemplateInstantiationEnded(const MSBI::Activities::TemplateInstantiation& activity);
        void OnFunctionEnded(const MSBI::Activities::Function& activity);
        void OnCodeGenerationEnded(const MSBI::Activities::CodeGeneration& activity);
        void OnCodeGenerationThreadEnded(const MSBI::Activities::Thread& activity);
        void OnSymbolName(const MSBI::SimpleEvents::SymbolName& symbolName);

        ScoreData& GetScoreData(){ return m_scoreData; }
        const ScoreData& GetScoreData() const { return m_scoreData; }

    private: 
        U32 ConvertDuration(std::chrono::nanoseconds nanos) const;
        U32 ComputeEventStartTime(TUEntry* activeTU, const MSBI::Activities::Activity& activity) const;
        MSVCCompileEvent* AddEvent(TUEntry* activeTU, const CompileCategory category, const MSBI::Activities::Activity& activity, const fastl::string& name = "", const TSymbolId nameSymbol = 0u);
        
        void FixNameTU(TUEntry& entry);
        void FinalizeTU(TUEntry& entry);
        void MergePendingEvents(TUEntry& entry);

        TUProcess& GetProcess(TProcessId processId);

        fastl::string UndecorateFunctionName(const char* functionName) const;

    private:
        TUProcessContainer m_processes;
        ScoreData          m_scoreData;
    };

    //////////////////////////////////////////////////////////////////////////////////////////////////////////////
    //Implementation
    //////////////////////////////////////////////////////////////////////////////////////////////////////////////

    // -----------------------------------------------------------------------------------------------------------
    MSVCCompileTrack& Gatherer::TUEntry::GetTrack(const TThreadId threadId)
    { 
        for (MSVCCompileTrack& track : tracks)
        {
            if (track.threadId == threadId)
            { 
                return track;
            }
        }

        tracks.emplace_back(threadId);
        return tracks.back();
    }

    //////////////////////////////////////////////////////////////////////////////////////////////////////////////

    // -----------------------------------------------------------------------------------------------------------
    Gatherer::TUProcess::TUProcess(const TProcessId _processId)
        : processId(_processId)
        , activeIndex(InvalidIndex)
    {}

    // -----------------------------------------------------------------------------------------------------------
    Gatherer::TUEntry* Gatherer::TUProcess::GetActiveTU() 
    { 
        return activeIndex < data.size()? &data[activeIndex] : nullptr; 
    }

    // -----------------------------------------------------------------------------------------------------------
    void Gatherer::TUProcess::ActivateTU(U64 pathHash)
    {
        activeIndex = InvalidIndex;
        TTUDictionary::iterator found = dict.find(pathHash);
        if (found == dict.end())
        { 
            ActivateNew();
            dict[pathHash] = activeIndex;
        }
        else 
        { 
            activeIndex = found->second;
        }
    }

    // -----------------------------------------------------------------------------------------------------------
    void Gatherer::TUProcess::ActivateNew()
    { 
        activeIndex = data.size();
        data.emplace_back();
    }

    // -----------------------------------------------------------------------------------------------------------
    void Gatherer::TUProcess::ActivateFirstUnnamed()
    { 
        for (size_t index = 0u, sz = data.size(); index<sz;++index)
        {
            const TUEntry& entry = data[index];
            if (!entry.tracks.empty() && entry.nameHash == 0ull)
            { 
                activeIndex = index;
                return;
            }
        }

        //if previous unnamed found, just crete a new one
        ActivateNew();
    }
    
    // -----------------------------------------------------------------------------------------------------------
    void Gatherer::TUProcess::ClearTU(TUEntry& entry)
    { 
        //remove entry from dictionary 
        dict.erase(entry.nameHash);

        //remove suballocation on activeTU but keep to not destroy the dictionary indices
        entry.nameHash = 0ull;
        entry.tracks.clear();
        entry.symbols.clear();
    }

    // -----------------------------------------------------------------------------------------------------------
    void Gatherer::TUProcess::DeactivateTU() 
    { 
        activeIndex = InvalidIndex; 
    }

    //////////////////////////////////////////////////////////////////////////////////////////////////////////////

    // -----------------------------------------------------------------------------------------------------------
    Gatherer::Gatherer(){}

    // -----------------------------------------------------------------------------------------------------------
    MSBI::AnalysisControl Gatherer::OnStartActivity(const MSBI::EventStack& eventStack)
    {
        MSBI::MatchEventInMemberFunction(eventStack.Back(), this, &Gatherer::OnCompilerPassStart);

        return MSBI::AnalysisControl::CONTINUE;
    }

    // -----------------------------------------------------------------------------------------------------------
    MSBI::AnalysisControl Gatherer::OnStopActivity(const MSBI::EventStack& eventStack)
    { 
        MSBI::MatchEventInMemberFunction(eventStack.Back(), this, &Gatherer::OnCompilerPassEnded);

        //TODO ~ check detail data and just ignore already here stuff that we are not interested in ( avoid memory and computation ) 

        //Specifics
        MSBI::MatchEventInMemberFunction(eventStack.Back(), this, &Gatherer::OnIncludeEnded);
        MSBI::MatchEventInMemberFunction(eventStack.Back(), this, &Gatherer::OnTemplateInstantiationEnded);
        MSBI::MatchEventInMemberFunction(eventStack.Back(), this, &Gatherer::OnFunctionEnded);

        MSBI::MatchEventInMemberFunction(eventStack.Back(), this, &Gatherer::OnCodeGenerationEnded);
        MSBI::MatchEventInMemberFunction(eventStack.Back(), this, &Gatherer::OnCodeGenerationThreadEnded);

        return MSBI::AnalysisControl::CONTINUE;
    }

    // -----------------------------------------------------------------------------------------------------------
    MSBI::AnalysisControl Gatherer::OnSimpleEvent(const MSBI::EventStack& eventStack)
    {
        MSBI::MatchEventInMemberFunction(eventStack.Back(), this, &Gatherer::OnSymbolName);

        return MSBI::AnalysisControl::CONTINUE;
    }

    // -----------------------------------------------------------------------------------------------------------
    void Gatherer::OnCompilerPassStart(const MSBI::Activities::CompilerPass& activity)
    { 
        TUProcess& process = GetProcess(activity.ProcessId());

        const fastl::string path = Utils::GetPath(activity);
        const U64 pathHash = CompileScore::StorePathString(m_scoreData, path.c_str(), path.length());

        //Activate the TU depending on the data we have ( handling unnamed passes ) 
        if (pathHash)
        { 
            process.ActivateTU(pathHash);
        }
        else if (activity.PassCode() == MSBI::Activities::CompilerPass::PassCode::FRONT_END) 
        {
            //create a new unnamed front end
            process.ActivateNew();
        }
        else
        { 
            //if we don't know the name the backend will be linked to the first encounted unnamed frontend
            process.ActivateFirstUnnamed();
        }

        TUEntry* activeTU = process.GetActiveTU();
        MSVCCompileTrack& track = activeTU->GetTrack(activity.ThreadId());

        activeTU->timestampOffset = activity.StartTimestamp();
        if (track.events.empty())
        {
            activeTU->nameHash = pathHash;
            track.events.emplace_back(MSVCCompileEvent(CompileEvent(CompileCategory::ExecuteCompiler,0u,0u,pathHash)));
        }
    }

    // -----------------------------------------------------------------------------------------------------------
    void Gatherer::OnCompilerPassEnded(const MSBI::Activities::CompilerPass& activity)
    {
        const CompileCategory category = activity.PassCode() == MSBI::Activities::CompilerPass::PassCode::BACK_END? CompileCategory::BackEnd : CompileCategory::FrontEnd;

        const TProcessId processId = activity.ProcessId();

        TUProcess& process = GetProcess(activity.ProcessId());
        TUEntry* activeTU = process.GetActiveTU();

        if (activeTU)
        { 
            //Close current section and prepare for future ones
            if (MSVCCompileEvent* newEvent = AddEvent(activeTU,category,activity)) 
            { 
                activeTU->timeSectionAccumulator = (newEvent->event.start + newEvent->event.duration)+1;
                for (MSVCCompileTrack& track : activeTU->tracks)
                { 
                    track.sectionStartIndex = track.events.size();
                }
            }

            const TTimeStamp startTime = std::chrono::microseconds{ MSBI::Internal::ConvertTickPrecision(activity.StartTimestamp(), activity.TickFrequency(), std::chrono::microseconds::period::den) }.count();

            if (category == CompileCategory::BackEnd)
            {
                activeTU->context.startTime[1] = startTime;
                activeTU->context.threadId[1] = activity.ThreadId();

                FixNameTU(*activeTU);
                FinalizeTU(*activeTU);
                process.ClearTU(*activeTU);
            }
            else
            {
                activeTU->context.startTime[0] = startTime;
                activeTU->context.threadId[0] = activity.ThreadId();
            }
        }

        //Disable active TU
        process.DeactivateTU();
    }

    // -----------------------------------------------------------------------------------------------------------
    void Gatherer::OnIncludeEnded(const MSBI::Activities::FrontEndFile& activity)
    {   
        fastl::string path = activity.Path();
        AddEvent(GetProcess(activity.ProcessId()).GetActiveTU(),CompileCategory::Include,activity,path);
    }

    // -----------------------------------------------------------------------------------------------------------
    void Gatherer::OnTemplateInstantiationEnded(const MSBI::Activities::TemplateInstantiation& activity)
    {
        CompileCategory category = CompileCategory::Invalid;

        switch(activity.Kind())
        { 
        case MSBI::Activities::TemplateInstantiation::Kind::CLASS:    category = CompileCategory::InstantiateClass; break;
        case MSBI::Activities::TemplateInstantiation::Kind::FUNCTION: category = CompileCategory::InstantiateFunction; break;
        case MSBI::Activities::TemplateInstantiation::Kind::VARIABLE: category = CompileCategory::InstantiateVariable; break; 
        case MSBI::Activities::TemplateInstantiation::Kind::CONCEPT:  category = CompileCategory::InstantiateConcept; break;
        }

        if (category != CompileCategory::Invalid)
        { 
            AddEvent(GetProcess(activity.ProcessId()).GetActiveTU(),category,activity,"<unknown>",activity.SpecializationSymbolKey());
        }
            
    }

    // -----------------------------------------------------------------------------------------------------------
    void Gatherer::OnFunctionEnded(const MSBI::Activities::Function& activity)
    {
        fastl::string name = UndecorateFunctionName(activity.Name());
        AddEvent(GetProcess(activity.ProcessId()).GetActiveTU(),CompileCategory::CodeGenFunction,activity, name);
    }

    // -----------------------------------------------------------------------------------------------------------
    void Gatherer::OnCodeGenerationEnded(const MSBI::Activities::CodeGeneration& activity)
    { 
        AddEvent(GetProcess(activity.ProcessId()).GetActiveTU(),CompileCategory::CodeGenPasses,activity);
    }

    // -----------------------------------------------------------------------------------------------------------
    void Gatherer::OnCodeGenerationThreadEnded(const MSBI::Activities::Thread& activity)
    { 
        AddEvent(GetProcess(activity.ProcessId()).GetActiveTU(),CompileCategory::CodeGenPasses,activity);
    }

    // -----------------------------------------------------------------------------------------------------------
    void Gatherer::OnSymbolName(const MSBI::SimpleEvents::SymbolName& symbolName)
    {
        if (TUEntry* entry = GetProcess(symbolName.ProcessId()).GetActiveTU()) 
        {
           entry->symbols[symbolName.Key()] = CompileScore::StoreSymbolString(m_scoreData,symbolName.Name());
        }
    }

    // -----------------------------------------------------------------------------------------------------------
    U32 Gatherer::ConvertDuration(std::chrono::nanoseconds nanos) const
    { 
        //TODO ~ ramonv ~ careful with overflows here 
        return static_cast<U32>(nanos.count()/1000);
    }

    // -----------------------------------------------------------------------------------------------------------
    U32 Gatherer::ComputeEventStartTime(TUEntry* activeTU, const MSBI::Activities::Activity& activity) const
    {
        auto startDelta = std::chrono::nanoseconds{MSBI::Internal::ConvertTickPrecision(activity.StartTimestamp()-activeTU->timestampOffset, activity.TickFrequency(), std::chrono::nanoseconds::period::den)}; 
        return activeTU->timeSectionAccumulator + ConvertDuration(startDelta);
    }

    // -----------------------------------------------------------------------------------------------------------
    MSVCCompileEvent* Gatherer::AddEvent(TUEntry* activeTU, const CompileCategory category, const MSBI::Activities::Activity& activity, const fastl::string& name, const TSymbolId nameSymbol)
    { 
        if (activeTU)
        { 
            const U64 nameHash = CompileScore::StoreCategoryValueString(m_scoreData,name.c_str(),category);
            const U32 startTime = ComputeEventStartTime(activeTU,activity);

            MSVCCompileTrack& track = activeTU->GetTrack(activity.ThreadId());
            TMSVCCompileEvents& events = track.events;
            TMSVCCompileEvents::iterator startSearch = events.begin()+track.sectionStartIndex;
            TMSVCCompileEvents::iterator found = fastl::lower_bound(startSearch,events.end(),startTime,[=](const MSVCCompileEvent& input, U32 value){ return value >= input.event.start; });
            TMSVCCompileEvents::iterator elem = events.emplace(found,MSVCCompileEvent(CompileEvent(category,startTime,ConvertDuration(activity.Duration()), nameHash),nameSymbol));
            return &(*elem);
        }

        return nullptr;
    }
  
    // -----------------------------------------------------------------------------------------------------------
    void Gatherer::FixNameTU(TUEntry& entry)
    { 
        //Placeholder to fix the issue when Build Insights does not give any TU name. 
        //We check for the frontend the first include as the same input file will be parsed 
        //Checking the first track and the first include to steal the name. 
        if (entry.nameHash == 0ull && !entry.tracks.empty()) 
        {
            TMSVCCompileEvents& mainTrackEvents = entry.tracks[0].events;
            for (const MSVCCompileEvent& event : mainTrackEvents)
            {
                if (event.event.category == CompileCategory::Include)
                {
                    entry.nameHash = event.event.nameHash;
                    return;
                }
            }
        }
    }

    // -----------------------------------------------------------------------------------------------------------
    void Gatherer::FinalizeTU(TUEntry& entry)
    { 
        if (entry.nameHash == 0ull || entry.tracks.empty() || entry.tracks[0].events.empty()) return; // avoid exporting any unnamed TU

        //Combine front end with  
        entry.tracks[0].events[0].event.duration = entry.timeSectionAccumulator; //Fix the root node

        //Create standard timeline
        ScoreTimeline timeline; 
        timeline.nameHash = entry.nameHash;
        timeline.tracks.reserve(entry.tracks.size());

        for (MSVCCompileTrack& track : entry.tracks)
        {
            timeline.tracks.emplace_back();
            TCompileEvents& timelineEvents = timeline.tracks.back();

            timelineEvents.reserve(track.events.size());
            for(MSVCCompileEvent& element : track.events)
            { 
                //assign name from symbols table if needed
                if (element.nameSymbol > 0u)
                { 
                    element.event.nameHash = entry.symbols[element.nameSymbol];
                }

                timelineEvents.push_back(element.event);
            }
        }

        CompileScore::ProcessTimeline(m_scoreData,timeline,entry.context);
    }

    // -----------------------------------------------------------------------------------------------------------
    Gatherer::TUProcess& Gatherer::GetProcess(const TProcessId processId)
    { 
        TUProcessContainer::iterator found = fastl::lower_bound(m_processes.begin(),m_processes.end(),processId,[](const TUProcess& process, const TProcessId processId){ return processId > process.processId; });
        if (found == m_processes.end() || found->processId != processId)
        {
            found = m_processes.insert(found,processId);
        }

        return *found; 
    }

    // -----------------------------------------------------------------------------------------------------------
    fastl::string Gatherer::UndecorateFunctionName(const char* functionName) const
    {
        constexpr unsigned long undecoratedNameMaxLength = 512;
        char undecoratedFunctionName[undecoratedNameMaxLength];

        const unsigned long undecorateFlags = UNDNAME_COMPLETE | UNDNAME_32_BIT_DECODE | UNDNAME_NO_ACCESS_SPECIFIERS;

        unsigned long result = UnDecorateSymbolName(functionName, undecoratedFunctionName, undecoratedNameMaxLength, undecorateFlags);

        return result != 0 ? undecoratedFunctionName : functionName;
    }


    //////////////////////////////////////////////////////////////////////////////////////////////////////////////

    const char* GetErrorText(const MSBI::RESULT_CODE failureCode)
    {
        switch (failureCode)
        {
        case MSBI::RESULT_CODE_FAILURE_INSUFFICIENT_PRIVILEGES:
            return "This operation requires administrator privileges.";

        case MSBI::RESULT_CODE_FAILURE_DROPPED_EVENTS:
            return "Events were dropped during the recording. Please try recording again.";

        case MSBI::RESULT_CODE_FAILURE_UNSUPPORTED_OS:
            return "The version of Microsoft Visual C++ Build Insights that CompileScore is using does not support the version of the operating system used for the recording.";

        case MSBI::RESULT_CODE_FAILURE_START_SYSTEM_TRACE:
        case MSBI::RESULT_CODE_FAILURE_START_MSVC_TRACE:
            return "A recording is currently in progress on your system is preventing CompileScore from starting a new one."
                   "This can occur if you forgot to stop a CompileScore recording prior to running the start command, or if other processes have started ETW traces of their own."
                   "Please try running the CompileScore -stop command.\n"
                   "You can use the 'tracelog -l' command from an elevated command prompt to list all ongoing tracing sessions on your system."
                   "Your currently ongoing CompileScore recording will show up as MSVC_BUILD_INSIGHTS_SESSION_COMPILE_SCORE." 
                   "If no MSVC_BUILD_INSIGHTS_SESSION_COMPILE_SCORE is found, it could mean a kernel ETW trace is currently being collected."
                   "This trace will show up as 'NT Kernel Logger' in your tracelog output, and will also prevent you from starting a new trace."
                   "You can stop the 'NT Kernel Logger' session by running 'xperf -stop' from an elevated command prompt.";
        default:
            return nullptr;
        }
    }

    void PrintError(const MSBI::RESULT_CODE errorCode)
    { 
        if (const char* errorMsg = GetErrorText(errorCode))
        { 
            LOG_ERROR("%s",errorMsg);
        }
        else 
        {
            LOG_ERROR("ERROR CODE: %d", errorCode);
        }
    }

    void PrintTraceStatistics(const MSBI::TRACING_SESSION_STATISTICS& stats)
    {
        if (stats.MSVCEventsLost)    LOG_PROGRESS("Dropped MSVC events: %lu",stats.MSVCEventsLost);
        if (stats.MSVCBuffersLost)   LOG_PROGRESS("Dropped MSVC buffers: %lu",stats.MSVCBuffersLost);
        if (stats.SystemEventsLost)  LOG_PROGRESS("Dropped system events: %lu",stats.SystemEventsLost);
        if (stats.SystemBuffersLost) LOG_PROGRESS("Dropped system buffers: %lu",stats.SystemBuffersLost);
    }

    //////////////////////////////////////////////////////////////////////////////////////////////////////////////

    // -----------------------------------------------------------------------------------------------------------
    int StopRecordingTrace(const ExportParams& params)
    { 
        MSBI::TRACING_SESSION_STATISTICS statistics{};

        LOG_PROGRESS("Stopping MSVC recording...");

        auto result = StopTracingSession(MSBI_SessionName, params.output, &statistics);

        PrintTraceStatistics(statistics);

        if (result != MSBI::RESULT_CODE_SUCCESS)
        {
            LOG_ERROR("Failed to stop the recording.");
            PrintError(result);
            return FAILURE;
        }

        LOG_ALWAYS("The trace file %s may contain personally identifiable information. This includes, but is not limited to, paths of files that were accessed and names of processes that were running during the collection. Please be aware of this when sharing this trace with others.",params.output);
        LOG_PROGRESS("Recording has stopped successfully!");

        return SUCCESS;
    }

    // -----------------------------------------------------------------------------------------------------------
    int StopRecordingGenerate(const ExportParams& params)
    { 
        Context::Scoped<IO::ScoreBinarizer> binarizer(params.output,params.timelinePacking);

        LOG_PROGRESS("Stopping MSVC recording and Generating Score...");

        MSBI::TRACING_SESSION_STATISTICS statistics{};

        Gatherer gatherer;
        auto group = MSBI::MakeStaticAnalyzerGroup(&gatherer);
        MSBI::RESULT_CODE result = MSBI::StopAndAnalyzeTracingSession(MSBI_SessionName, MSBI_NumberOfPasses, &statistics, group);

        if (result == MSBI::RESULT_CODE_SUCCESS)
        { 
            ScoreData& scoreData = gatherer.GetScoreData();
            CompileScore::FinalizeScoreData(scoreData);
            binarizer.Get().Binarize(scoreData);
        }
        else 
        {
            LOG_ERROR("Failed to stop the recording.");
            PrintError(result);
            return FAILURE;
        }

        return SUCCESS;
    }

    //////////////////////////////////////////////////////////////////////////////////////////////////////////////

    // -----------------------------------------------------------------------------------------------------------
    int Extractor::StartRecording(const ExportParams& params)
    {
        LOG_PROGRESS("Starting MSVC recording...");

        MSBI::TRACING_SESSION_OPTIONS options{};

        const ExportParams::Detail recordingDetail = params.detail > params.timelineDetail? params.detail : params.timelineDetail;

        switch(recordingDetail)
        { 
        case ExportParams::Detail::Full:
            options.MsvcEventFlags |= MSBI::TRACING_SESSION_MSVC_EVENT_FLAGS_BACKEND_FUNCTIONS;
        case ExportParams::Detail::FrontEnd:
            options.MsvcEventFlags |= MSBI::TRACING_SESSION_MSVC_EVENT_FLAGS_FRONTEND_TEMPLATE_INSTANTIATIONS;
        case ExportParams::Detail::Basic:
            options.MsvcEventFlags |= MSBI::TRACING_SESSION_MSVC_EVENT_FLAGS_FRONTEND_FILES;
        case ExportParams::Detail::None:
            options.MsvcEventFlags |= MSBI::TRACING_SESSION_MSVC_EVENT_FLAGS_BASIC;
        }

        const MSBI::RESULT_CODE result = StartTracingSession(MSBI_SessionName, options);

        if (result != MSBI::RESULT_CODE_SUCCESS) 
        {
            LOG_ERROR("Failed to start recording.");
            PrintError(result);
            return FAILURE;
        }

        LOG_PROGRESS("Recording session started successfully!");
        return SUCCESS;
    }

    // -----------------------------------------------------------------------------------------------------------
    int Extractor::CancelRecording(const ExportParams& params)
    { 
        LOG_PROGRESS("Cancelling MSVC recording and Generating Score...");

        MSBI::TRACING_SESSION_STATISTICS statistics{};

        DummyGatherer gatherer;
        auto group = MSBI::MakeStaticAnalyzerGroup(&gatherer);
        MSBI::RESULT_CODE result = MSBI::StopAndAnalyzeTracingSession(MSBI_SessionName, MSBI_NumberOfPasses, &statistics, group);

        if (result != MSBI::RESULT_CODE_SUCCESS)
        { 
            LOG_ERROR("Failed to cancel the recording.");
            PrintError(result);
            return FAILURE;
        }

        LOG_PROGRESS("Recording session cancelled successfully!");
        return SUCCESS;
    }

    // -----------------------------------------------------------------------------------------------------------
    int Extractor::StopRecording(const ExportParams& params)
    { 
        //Check extension
        if (params.output == nullptr)
        { 
            LOG_ERROR("No output file provided.");
            return FAILURE;
        }

        if (IO::IsExtension(params.output,".scor"))
        { 
            return StopRecordingGenerate(params);
        }
        
        if (IO::IsExtension(params.output,".etl"))
        { 
            return StopRecordingTrace(params);
        }

        LOG_ERROR("Unknown output file extension provided. The MSVC generator only knows how to generate .scor or .etl files.");
        LOG_ERROR("Cancelling Recording...");
        CancelRecording(params);
        return FAILURE;
    }

    // -----------------------------------------------------------------------------------------------------------
    int Extractor::GenerateScore(const ExportParams& params)
    { 
        if (params.input == nullptr)
        { 
            LOG_ERROR("No input path provided.");
            return FAILURE;
        }

        if (!IO::IsExtension(params.input,".etl"))
        { 
            LOG_ERROR("Input file is not an .etl trace log file.");
            return FAILURE;
        }

        if (!IO::Exists(params.input))
        {
            LOG_ERROR("Could not find input: %s",params.input);
            return FAILURE;
        }

        Context::Scoped<IO::ScoreBinarizer> binarizer(params.output,params.timelinePacking);
        
        LOG_PROGRESS("Analyzing trace file %s",params.input);

        Gatherer gatherer;
        auto group = MSBI::MakeStaticAnalyzerGroup(&gatherer);
        const MSBI::RESULT_CODE result = MSBI::Analyze(params.input, MSBI_NumberOfPasses, group); 

        if (result == 0) 
        { 
            ScoreData& scoreData = gatherer.GetScoreData();
            CompileScore::FinalizeScoreData(scoreData);
            binarizer.Get().Binarize(scoreData);
        }

        return result;
    }

    // -----------------------------------------------------------------------------------------------------------
    int Extractor::Clean(const ExportParams& params)
    {
		LOG_ERROR("Clean command not supported on MSVC mode.");
		return FAILURE;
    }
}

