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
    using TSymbolId  = unsigned long long;

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

    //////////////////////////////////////////////////////////////////////////////////////////////////////////////

    class Gatherer : public MSBI::IAnalyzer
    {
    private: 
        enum { InvalidIndex = 0xffffffff };

        struct TUEntry
        { 
            using TSymbolsMap = fastl::unordered_map<TSymbolId,fastl::string>;

            TUEntry()
                : sectionStartIndex(0u)
                , timestampOffset(0u)
                , timeSectionAccumulator(0u)
            {}

            fastl::string      name;
            TMSVCCompileEvents events;
            TSymbolsMap        symbols;
            size_t             sectionStartIndex; 
            TTimeStamp         timestampOffset;
            U32                timeSectionAccumulator;
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
        MSBI::AnalysisControl OnSimpleEvent(const MSBI::EventStack& eventStack) override;
        void OnCompilerPassStart(const MSBI::Activities::CompilerPass& activity);
        void OnCompilerPassEnded(const MSBI::Activities::CompilerPass& activity);
        void OnIncludeEnded(const MSBI::Activities::FrontEndFile& activity);
        void OnTemplateInstantiationEnded(const MSBI::Activities::TemplateInstantiation& activity);
        void OnFunctionEnded(const MSBI::Activities::Function& activity);
        void OnCodeGenerationEnded(const MSBI::Activities::CodeGeneration& activity);
        void OnCodeGenerationThreadEnded(const MSBI::Activities::Thread& activity);
        void OnSymbolName(const MSBI::SimpleEvents::SymbolName& symbolName);

        const ScoreData& GetScoreData(){ return m_scoreData; }

    private: 
        U32 ConvertDuration(std::chrono::nanoseconds nanos) const;
        U32 ComputeEventStartTime(TUEntry* activeTU, const MSBI::Activities::Activity& activity) const;
        MSVCCompileEvent* AddEvent(TUEntry* activeTU, const CompileCategory category, const MSBI::Activities::Activity& activity, const fastl::string& name = "", const TSymbolId nameSymbol = 0u);
        
        void FinalizeActiveTU(TUProcess& process);
        void MergePendingEvents(TUEntry& entry);

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
            dict.erase(activeTU->name);

            //remove suballocation on activetU but keep to not destroy the dictionary indices
            activeTU->events.clear();
            activeTU->name.clear();
            activeTU->symbols.clear();

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
        MSBI::MatchEventInMemberFunction(eventStack.Back(), this, &Gatherer::OnTemplateInstantiationEnded);
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
        fastl::string path = Utils::GetPath(activity);
        StringUtils::ToPathBaseName(path); 
        StringUtils::ToLower(path);

        TUProcess& process = GetProcess(activity.ProcessId());
        process.ActivateTU(path);
        TUEntry* activeTU = process.GetActiveTU();

        activeTU->timestampOffset = activity.StartTimestamp();
        if (activeTU->events.empty())
        {
            activeTU->name = path;
            activeTU->events.emplace_back(MSVCCompileEvent(CompileEvent(CompileCategory::ExecuteCompiler,0u,0u,path)));
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
        if (MSVCCompileEvent* newEvent = AddEvent(activeTU,category,activity)) 
        { 
            activeTU->timeSectionAccumulator = (newEvent->event.start + newEvent->event.duration)+1;
            activeTU->sectionStartIndex = activeTU->events.size();
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
    void Gatherer::OnTemplateInstantiationEnded(const MSBI::Activities::TemplateInstantiation& activity)
    {
        CompileCategory category = CompileCategory::Invalid;

        switch(activity.Kind())
        { 
        case MSBI::Activities::TemplateInstantiation::Kind::CLASS: category = CompileCategory::InstantiateClass; break;
        case MSBI::Activities::TemplateInstantiation::Kind::FUNCTION: category = CompileCategory::InstantiateFunction; break;

        //TODO ~ ramonv ~ add once we move to next data version so we can expand the categories
        //case MSBI::Activities::TemplateInstantiation::Kind::VARIABLE: category = CompileCategory::DebugGlobalVariable; break; 
        //case MSBI::Activities::TemplateInstantiation::Kind::CONCEPT: category = CompileCategory::DebugType; break;
        }
             

        //TODO ~ ramonv ~ Get real names / store and wait for the simple events with the resolved names to push these in 

        if (category != CompileCategory::Invalid)
        { 
            AddEvent(GetProcess(activity.ProcessId()).GetActiveTU(),category,activity,"<unknown>",activity.SpecializationSymbolKey());
        }
            
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
    void Gatherer::OnCodeGenerationThreadEnded(const MSBI::Activities::Thread& activity)
    { 
        //AddEvent(GetProcess(activity.ProcessId()).GetActiveTU(),CompileCategory::CodeGenPasses,activity);
    }

    // -----------------------------------------------------------------------------------------------------------
    void Gatherer::OnSymbolName(const MSBI::SimpleEvents::SymbolName& symbolName)
    {
        if (TUEntry* entry = GetProcess(symbolName.ProcessId()).GetActiveTU()) 
        {
            entry->symbols[symbolName.Key()] = symbolName.Name();
        }
    }

    // -----------------------------------------------------------------------------------------------------------
    U32 Gatherer::ConvertDuration(std::chrono::nanoseconds nanos) const
    { 
        //Maybe upgrade to nanoseconds 
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
            const U32 startTime = ComputeEventStartTime(activeTU,activity);

            TMSVCCompileEvents& events = activeTU->events;
            TMSVCCompileEvents::iterator startSearch = events.begin()+activeTU->sectionStartIndex;
            TMSVCCompileEvents::iterator found = fastl::lower_bound(startSearch,events.end(),startTime,[=](const MSVCCompileEvent& input, U32 value){ return value >= input.event.start; });
            TMSVCCompileEvents::iterator elem = events.emplace(found,MSVCCompileEvent(CompileEvent(category,startTime,ConvertDuration(activity.Duration()),name),nameSymbol));    
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
            entry.events[0].event.duration = entry.timeSectionAccumulator; //Fix the root node

            //Create standard timeline
            ScoreTimeline timeline; 
            timeline.name = entry.name;
            timeline.events.reserve(entry.events.size());

            for(MSVCCompileEvent& element : entry.events)
            { 
                //assign name from symbols table if needed
                if (element.nameSymbol > 0u)
                { 
                    element.event.name = entry.symbols[element.nameSymbol];
                }

                timeline.events.push_back(element.event);
            }

            CompileScore::ProcessTimeline(m_scoreData,timeline);

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

