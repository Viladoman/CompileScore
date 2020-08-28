#include "MSVCScore.h"

#include <chrono>
#include <CppBuildInsights.hpp>

#include "../fastl/algorithm.h"
#include "../fastl/string.h"
#include "../Common/CommandLine.h"
#include "../Common/IOStream.h"
#include "../Common/ScoreDefinitions.h"
#include "../Common/ScoreProcessor.h"

namespace MSVC
{ 
    namespace MSBI = Microsoft::Cpp::BuildInsights; 

    using TTimeStamp = unsigned long long;

    //TODO ~ ramonv ~ move this to StringUtils and Math Utils
    namespace Utils
    { 
        // -----------------------------------------------------------------------------------------------------------
        void ToPathBaseName(fastl::string& input)
        { 
            size_t foundIndex = fastl::string::npos;
            for(size_t i=0u,sz=input.length();i<sz;++i)
            { 
                const char c = input[i];
                if (c=='/' || c=='\\') foundIndex = i; 
            }

            if (foundIndex != fastl::string::npos)
            {
                input.erase(0, foundIndex + 1);
            }
        }

        // -----------------------------------------------------------------------------------------------------------
        inline constexpr char ToLower(char c)
        {
            constexpr U8 diff = ('a'-'A');
            return (c >= 'A' && c <= 'Z')? c+diff : c;
        }

        // -----------------------------------------------------------------------------------------------------------
        void ToLower(fastl::string& input)
        {
            for (size_t i = 0,sz=input.length(); i < sz; ++ i) 
            {
                input[i] = ToLower(input[i]);  
            }
        }

        // -----------------------------------------------------------------------------------------------------------
        const char* GetPath(const MSBI::Activities::CompilerPass& compilerPass)
        {
            //TODO ~ Ramonv ~ technically windows paths can go up to 32k 
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
        using TTUContainer = fastl::unordered_map<fastl::string,TUEntry>;

    public: 

        Gatherer();
        MSBI::AnalysisControl OnStartActivity(const MSBI::EventStack& eventStack) override; 
        MSBI::AnalysisControl OnStopActivity(const MSBI::EventStack& eventStack) override;    
        void OnCompilerPassStart(const MSBI::Activities::CompilerPass& activity);
        void OnCompilerPassEnded(const MSBI::Activities::CompilerPass& activity);
        void OnIncludeEnded(const MSBI::Activities::FrontEndFile& parentActivity, const MSBI::Activities::FrontEndFile& activity);
        void OnFunctionEnded(const MSBI::Activities::Function& activity);

        const ScoreData& GetScoreData(){ return m_scoreData; }

    private: 
        U32 ConvertDuration(std::chrono::nanoseconds nanos);
        CompileEvent* AddEvent(const CompileCategory category, const MSBI::Activities::Activity& activity, const fastl::string& name = "");
        void FinalizeTU(const fastl::string& path);

    private:
        TTUContainer m_TUs;
        TUEntry* m_activeTU;
        ScoreData m_scoreData;
    };

    //////////////////////////////////////////////////////////////////////////////////////////////////////////////
    //Implementation
    //////////////////////////////////////////////////////////////////////////////////////////////////////////////

    // -----------------------------------------------------------------------------------------------------------
    Gatherer::Gatherer()
        : m_activeTU(nullptr)
    {}

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
        MSBI::MatchEventStackInMemberFunction(eventStack, this, &Gatherer::OnIncludeEnded);
        MSBI::MatchEventInMemberFunction(eventStack.Back(), this, &Gatherer::OnFunctionEnded);

        return MSBI::AnalysisControl::CONTINUE;
    }

    // -----------------------------------------------------------------------------------------------------------
    void Gatherer::OnCompilerPassStart(const MSBI::Activities::CompilerPass& activity)
    { 
        fastl::string path = Utils::GetPath(activity);
        Utils::ToPathBaseName(path); 
        Utils::ToLower(path);

        m_activeTU = &(m_TUs[path]);
        m_activeTU->timestampOffset = activity.StartTimestamp();

        if (m_activeTU->timeline.events.empty())
        {
            m_activeTU->timeline.name = path;
            m_activeTU->timeline.events.emplace_back(CompileEvent(CompileCategory::ExecuteCompiler,0u,0u,path));
        }
    }

    // -----------------------------------------------------------------------------------------------------------
    void Gatherer::OnCompilerPassEnded(const MSBI::Activities::CompilerPass& activity)
    {
        const CompileCategory category = activity.PassCode() == MSBI::Activities::CompilerPass::PassCode::BACK_END? CompileCategory::BackEnd : CompileCategory::FrontEnd;

        //Close current section and prepare for future ones
        if (CompileEvent* newEvent = AddEvent(category,activity)) 
        { 
            m_activeTU->timeSectionAccumulator = (newEvent->start + newEvent->duration)+1;
            m_activeTU->sectionStartIndex = m_activeTU->timeline.events.size();
        }

        //Backend pass done -> Finalize TU and release its memory
        if (category == CompileCategory::BackEnd)
        { 
            FinalizeTU(m_activeTU->timeline.name);
        }

        //Disable active TU
        m_activeTU = nullptr;
    }

    // -----------------------------------------------------------------------------------------------------------
    void Gatherer::OnIncludeEnded(const MSBI::Activities::FrontEndFile& parentActivity, const MSBI::Activities::FrontEndFile& activity)
    {   
        fastl::string path = activity.Path();
        Utils::ToPathBaseName(path); 
        Utils::ToLower(path);
        AddEvent(CompileCategory::Include,activity,path);
    }

    // -----------------------------------------------------------------------------------------------------------
    void Gatherer::OnFunctionEnded(const MSBI::Activities::Function& activity)
    {
        fastl::string name = activity.Name();

        //TODO ~ ramonv ~ undecorateName - windows function: UnDecorateSymbolName()

        Utils::ToLower(name);
        AddEvent(CompileCategory::OptimizeFunction,activity,name);

    }

    // -----------------------------------------------------------------------------------------------------------
    U32 Gatherer::ConvertDuration(std::chrono::nanoseconds nanos)
    { 
        //Maybe upgrade to nanoseconds 
        //TODO ~ ramonv ~ careful with overflows here 
        return static_cast<U32>(nanos.count()/1000);
    }

    // -----------------------------------------------------------------------------------------------------------
    CompileEvent* Gatherer::AddEvent(const CompileCategory category, const MSBI::Activities::Activity& activity, const fastl::string& name)
    { 
        if (m_activeTU)
        { 
            TCompileEvents& events = m_activeTU->timeline.events;

            auto startDelta = std::chrono::nanoseconds{MSBI::Internal::ConvertTickPrecision(activity.StartTimestamp()-m_activeTU->timestampOffset, activity.TickFrequency(), std::chrono::nanoseconds::period::den)}; 
            const U32 startTime = m_activeTU->timeSectionAccumulator + ConvertDuration(startDelta);

            TCompileEvents::iterator startSearch = events.begin()+m_activeTU->sectionStartIndex;
            TCompileEvents::iterator found = fastl::lower_bound(startSearch,events.end(),startTime,[=](const CompileEvent& input, U32 value){ return value >= input.start; });
            TCompileEvents::iterator elem = events.emplace(found,CompileEvent(category,startTime,ConvertDuration(activity.Duration()),name));    
            return &(*elem);
        }

        return nullptr;
    }

    // -----------------------------------------------------------------------------------------------------------
    void Gatherer::FinalizeTU(const fastl::string& path)
    { 
        //Combine front end with 
        TTUContainer::iterator found = m_TUs.find(path);
        if (found != m_TUs.end())
        { 
            TUEntry& entry = found->second;
            entry.timeline.events[0].duration = entry.timeSectionAccumulator; //Fix the root node

            CompileScore::ProcessTimeline(m_scoreData,entry.timeline);

            m_TUs.erase(found);           
        }
    }

    //////////////////////////////////////////////////////////////////////////////////////////////////////////////

    // -----------------------------------------------------------------------------------------------------------
    int ExtractScore(const ExportParams& params)
    { 
        //TODO ~ ramonv ~ Rethink how to avoid copying the data around
        constexpr int numberOfPasses = 1;

        LOG_PROGRESS("Analyzing trace file %s",params.input);

        Gatherer gatherer;
        auto group = MSBI::MakeStaticAnalyzerGroup(&gatherer);
        int result = MSBI::Analyze(params.input, numberOfPasses, group); 

        if (result == 0) 
        { 
            IOStream::Binarize(params.output, gatherer.GetScoreData());
        }

        return result;
    } 
}

