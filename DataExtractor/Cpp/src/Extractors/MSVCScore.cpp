#include "MSVCScore.h"

#include <chrono>
#include <CppBuildInsights.hpp>

#include "../fastl/algorithm.h"
#include "../fastl/string.h"
#include "../Common/CommandLine.h"
#include "../Common/Context.h"
#include "../Common/IOStream.h"
#include "../Common/ScoreDefinitions.h"
#include "../Common/ScoreProcessor.h"
#include "../Common/StringUtils.h"

namespace MSVC
{ 
    namespace MSBI = Microsoft::Cpp::BuildInsights; 

    using TTimeStamp = unsigned long long;

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
            size_t inputLength = wcslen(fileNameW);

            size_t charsConverted = 0;
            wcstombs_s(&charsConverted, str, BUFF_SIZE, fileNameW, inputLength);
            return str;
        }
    }

    //////////////////////////////////////////////////////////////////////////////////////////////////////////////

    class Gatherer : public MSBI::IAnalyzer
    {
    private: 
        enum { InvalidIndex = 0xffffffff };

        struct TUEntry
        { 
            TUEntry()
                : timeSectionAccumulator(0u)
                , sectionStartIndex(0u)
                , timestampOffset(0u)
            {}

            ScoreTimeline  timeline;
            U32            timeSectionAccumulator;
            size_t         sectionStartIndex; 
            TTimeStamp     timestampOffset;
        };
        using TTUCollection = fastl::vector<TUEntry>;
        using TTUDictionary = fastl::unordered_map<fastl::string,U32>;

        struct TUProcess
        { 
            TUProcess(const unsigned long _processId = 0u);

            TUEntry* GetActiveTU();         
            void ActivateTU(const fastl::string& path);
            void ClearActiveTU();
            void DeactivateTU();

            TTUCollection data; 
            TTUDictionary dict;
            unsigned long processId; 
            U32 activeIndex;
        };
        using TUProcessContainer = fastl::vector<TUProcess>;

    public: 

        Gatherer();
        MSBI::AnalysisControl OnStartActivity(const MSBI::EventStack& eventStack) override; 
        MSBI::AnalysisControl OnStopActivity(const MSBI::EventStack& eventStack) override;    
        void OnCompilerPassStart(const MSBI::Activities::CompilerPass& activity);
        void OnCompilerPassEnded(const MSBI::Activities::CompilerPass& activity);
        void OnIncludeEnded(const MSBI::Activities::FrontEndFile& activity);
        void OnFunctionEnded(const MSBI::Activities::Function& activity);
        void OnCodeGenerationEnded(const MSBI::Activities::CodeGeneration& activity);
        void OnBackEndThreadEnded(const MSBI::Activities::Thread& activity);

        const ScoreData& GetScoreData(){ return m_scoreData; }

    private: 
        U32 ConvertDuration(std::chrono::nanoseconds nanos);
        CompileEvent* AddEvent(TUEntry* activeTU, const CompileCategory category, const MSBI::Activities::Activity& activity, const fastl::string& name = "");
        void FinalizeActiveTU(TUProcess& process);

        TUProcess& GetProcess(unsigned long processId);

    private:
        TUProcessContainer m_processes;
        ScoreData          m_scoreData;
    };

    //////////////////////////////////////////////////////////////////////////////////////////////////////////////
    //Implementation
    //////////////////////////////////////////////////////////////////////////////////////////////////////////////

    // -----------------------------------------------------------------------------------------------------------
    Gatherer::TUProcess::TUProcess(const unsigned long _processId)
        : processId(_processId)
        , activeIndex(InvalidIndex)
    {}

    // -----------------------------------------------------------------------------------------------------------
    Gatherer::TUEntry* Gatherer::TUProcess::GetActiveTU() 
    { 
        return activeIndex < data.size()? &data[activeIndex] : nullptr; 
    }

    // -----------------------------------------------------------------------------------------------------------
    void Gatherer::TUProcess::ActivateTU(const fastl::string& path)
    {
        activeIndex = InvalidIndex;
        TTUDictionary::iterator found = dict.find(path);
        if (found == dict.end())
        { 
            activeIndex = static_cast<U32>(data.size()); 
            dict[path] = activeIndex;
            data.emplace_back();
        }
        else 
        { 
            activeIndex = found->second;
        }
    }

    // -----------------------------------------------------------------------------------------------------------
    void Gatherer::TUProcess::ClearActiveTU()
    { 
        if (TUEntry* activeTU = GetActiveTU())
        { 
            //remove entry from dictionary 
            dict.erase(activeTU->timeline.name);

            //remove suballocation on activetU but keep to not destroy the dictionary indices
            activeTU->timeline.events.clear();
            activeTU->timeline.name.clear();

            DeactivateTU();
        }
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

        //Specifics
        MSBI::MatchEventInMemberFunction(eventStack.Back(), this, &Gatherer::OnIncludeEnded);
        MSBI::MatchEventInMemberFunction(eventStack.Back(), this, &Gatherer::OnFunctionEnded);
        MSBI::MatchEventInMemberFunction(eventStack.Back(), this, &Gatherer::OnCodeGenerationEnded);

        return MSBI::AnalysisControl::CONTINUE;
    }

    // -----------------------------------------------------------------------------------------------------------
    void Gatherer::OnCompilerPassStart(const MSBI::Activities::CompilerPass& activity)
    { 
        fastl::string path = Utils::GetPath(activity);
        StringUtils::ToPathBaseName(path); 
        StringUtils::ToLower(path);

        TUProcess& process = GetProcess(activity.ProcessId());

        process.ActivateTU(path);

        TUEntry* activeTU = process.GetActiveTU();

        activeTU->timestampOffset = activity.StartTimestamp();

        if (activeTU->timeline.events.empty())
        {
            activeTU->timeline.name = path;
            activeTU->timeline.events.emplace_back(CompileEvent(CompileCategory::ExecuteCompiler,0u,0u,path));
        }
    }

    // -----------------------------------------------------------------------------------------------------------
    void Gatherer::OnCompilerPassEnded(const MSBI::Activities::CompilerPass& activity)
    {
        const CompileCategory category = activity.PassCode() == MSBI::Activities::CompilerPass::PassCode::BACK_END? CompileCategory::BackEnd : CompileCategory::FrontEnd;

        unsigned long threadId = activity.ThreadId();
        unsigned long processId = activity.ProcessId();

        TUProcess& process = GetProcess(activity.ProcessId());
        TUEntry* activeTU = process.GetActiveTU();

        //Close current section and prepare for future ones
        if (CompileEvent* newEvent = AddEvent(activeTU,category,activity)) 
        { 
            activeTU->timeSectionAccumulator = (newEvent->start + newEvent->duration)+1;
            activeTU->sectionStartIndex = activeTU->timeline.events.size();
        }

        if (category == CompileCategory::BackEnd)
        { 
            FinalizeActiveTU(process);
        }

        //Disable active TU
        process.DeactivateTU();
    }

    // -----------------------------------------------------------------------------------------------------------
    void Gatherer::OnIncludeEnded(const MSBI::Activities::FrontEndFile& activity)
    {   
        fastl::string path = activity.Path();
        StringUtils::ToPathBaseName(path); 
        StringUtils::ToLower(path);

        AddEvent(GetProcess(activity.ProcessId()).GetActiveTU(),CompileCategory::Include,activity,path);
    }

    // -----------------------------------------------------------------------------------------------------------
    void Gatherer::OnFunctionEnded(const MSBI::Activities::Function& activity)
    {
        //TODO ~ ramonv ~ we need to fix threadId multitimeline export first ( better no data than bad data ) 
        return;

        fastl::string name = activity.Name();

        //TODO ~ ramonv ~ undecorateName - windows function: UnDecorateSymbolName()

        StringUtils::ToLower(name);
        AddEvent(GetProcess(activity.ProcessId()).GetActiveTU(),CompileCategory::CodeGenFunction,activity,name);

    }

    // -----------------------------------------------------------------------------------------------------------
    void Gatherer::OnCodeGenerationEnded(const MSBI::Activities::CodeGeneration& activity)
    { 
        AddEvent(GetProcess(activity.ProcessId()).GetActiveTU(),CompileCategory::CodeGenPasses,activity);
    }

    // -----------------------------------------------------------------------------------------------------------
    void Gatherer::OnBackEndThreadEnded(const MSBI::Activities::Thread& activity)
    { 
        //AddEvent(GetProcess(activity.ProcessId()).GetActiveTU(),CompileCategory::CodeGenPasses,activity);
    }

    // -----------------------------------------------------------------------------------------------------------
    U32 Gatherer::ConvertDuration(std::chrono::nanoseconds nanos)
    { 
        //Maybe upgrade to nanoseconds 
        //TODO ~ ramonv ~ careful with overflows here 
        return static_cast<U32>(nanos.count()/1000);
    }

    // -----------------------------------------------------------------------------------------------------------
    CompileEvent* Gatherer::AddEvent(TUEntry* activeTU, const CompileCategory category, const MSBI::Activities::Activity& activity, const fastl::string& name)
    { 
        if (activeTU)
        { 
            TCompileEvents& events = activeTU->timeline.events;

            auto startDelta = std::chrono::nanoseconds{MSBI::Internal::ConvertTickPrecision(activity.StartTimestamp()-activeTU->timestampOffset, activity.TickFrequency(), std::chrono::nanoseconds::period::den)}; 
            const U32 startTime = activeTU->timeSectionAccumulator + ConvertDuration(startDelta);

            TCompileEvents::iterator startSearch = events.begin()+activeTU->sectionStartIndex;
            TCompileEvents::iterator found = fastl::lower_bound(startSearch,events.end(),startTime,[=](const CompileEvent& input, U32 value){ return value >= input.start; });
            TCompileEvents::iterator elem = events.emplace(found,CompileEvent(category,startTime,ConvertDuration(activity.Duration()),name));    
            return &(*elem);
        }

        return nullptr;
    }

    // -----------------------------------------------------------------------------------------------------------
    void Gatherer::FinalizeActiveTU(TUProcess& process)
    { 
        //Combine front end with 
        if (TUEntry* tu = process.GetActiveTU())
        { 
            TUEntry& entry = *tu;
            entry.timeline.events[0].duration = entry.timeSectionAccumulator; //Fix the root node

            CompileScore::ProcessTimeline(m_scoreData,entry.timeline);

            process.ClearActiveTU();
        }
    }

    // -----------------------------------------------------------------------------------------------------------
    Gatherer::TUProcess& Gatherer::GetProcess(unsigned long processId)
    { 
        TUProcessContainer::iterator found = fastl::lower_bound(m_processes.begin(),m_processes.end(),processId,[](const TUProcess& process, const unsigned long processId){ return processId > process.processId; });
        if (found == m_processes.end() || found->processId != processId)
        {
            found = m_processes.insert(found,processId);
        }

        return *found; 
    }

    //////////////////////////////////////////////////////////////////////////////////////////////////////////////

    // -----------------------------------------------------------------------------------------------------------
    int ExtractScore(const ExportParams& params)
    { 
        constexpr int numberOfPasses = 1;

        Context::Scoped<IO::Binarizer> binarizer(params.output);
        
        LOG_PROGRESS("Analyzing trace file %s",params.input);

        Gatherer gatherer;
        auto group = MSBI::MakeStaticAnalyzerGroup(&gatherer);
        int result = MSBI::Analyze(params.input, numberOfPasses, group); 

        if (result == 0) 
        { 
            binarizer.Get().Binarize(gatherer.GetScoreData());
        }

        return result;
    } 
}

