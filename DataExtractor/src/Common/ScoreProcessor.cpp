#include "../Common/Context.h"
#include "../Common/CRC64.h"
#include "../Common/ScoreDefinitions.h"
#include "../Common/StringUtils.h"
#include "../fastl/algorithm.h"
#include "../fastl/memory.h"

#include "IOStream.h"
#include "CommandLine.h"

namespace CompileScore
{ 
	namespace Utils
	{ 
		// -----------------------------------------------------------------------------------------------------------
		template <typename T> inline constexpr T Min(const T a, const T b) { return a < b? a : b; }
		template <typename T> inline constexpr T Max(const T a, const T b) { return a < b? b : a; }
	}

	// -----------------------------------------------------------------------------------------------------------
	U64 StoreString(ScoreData& scoreData, const char* str, size_t length)
	{
		const U64 strHash = Hash::AppendToCRC64(0ull, str, length);
		if (strHash)
		{
			auto const& result = scoreData.strings.insert(TCompileStrings::value_type(strHash, fastl::string(str, length)));
			if (result.second)
			{
				//Convert to lower case to improve search performance later
				StringUtils::ToLower(result.first->second);
			}
		}
		return strHash;
	}

	// -----------------------------------------------------------------------------------------------------------
	U64 StoreString(ScoreData& scoreData, const char* str)
	{
		U32 length = 0u;
		for (; str[length] != '\0'; ++length) {}
		return StoreString(scoreData, str, length);
	}

	// -----------------------------------------------------------------------------------------------------------
	CompileData& CreateGlobalEntry(ScoreData& scoreData, CompileEvent& element)
	{ 
		const CompileCategoryType globalIndex = ToUnderlying(element.category);
		TCompileDatas& global = scoreData.globals[globalIndex];
		TCompileDataDictionary& dictionary = scoreData.globalsDictionary[globalIndex];

		const U32 nextIndex = static_cast<U32>(global.size());
		auto const& result = dictionary.insert(TCompileDataDictionary::value_type(element.nameHash,nextIndex));
		if (result.second) 
		{ 
			//the element got inserted
			element.nameId = nextIndex;
			global.emplace_back(element.nameHash);

			//for now we only have users entry for Includes
			if( element.category == CompileCategory::Include )
			{
				scoreData.includers.emplace_back();
			}

			return global.back();
		} 
		
		element.nameId = result.first->second;
		return global[element.nameId];
	}

	// -----------------------------------------------------------------------------------------------------------
	CompileCategory GetDetailCategory(const ExportParams::Detail detail)
	{ 
		switch(detail)
		{ 
			case ExportParams::Detail::None:     return CompileCategory::GatherNone;
			case ExportParams::Detail::Basic:    return CompileCategory::GatherBasic;
			case ExportParams::Detail::FrontEnd: return CompileCategory::GatherFrontEnd; 
			default: return CompileCategory::GatherFull;
		}
	}

	// -----------------------------------------------------------------------------------------------------------
	void ProcessParent( ScoreData& scoreData, CompileEvent& child, const CompileEvent* parent, const CompileUnit& unit, const ExportParams::Includers includersMode )
	{
		//only includes for now	
		if( parent == nullptr || child.category != CompileCategory::Include || includersMode != ExportParams::Includers::Enabled )
		{
			return;
		} 
		
		if( parent->category == CompileCategory::Include )
		{
			scoreData.includers[ child.nameId ].includes.emplace( parent->nameId ); 
		}
		else
		{
			scoreData.includers[ child.nameId ].units.emplace( unit.unitId );
		}

		//TODO ~ ramonv ~ process self computation to compile event
	}


	// -----------------------------------------------------------------------------------------------------------
	void ProcessTimelineTrack(ScoreData& scoreData, CompileUnit& unit, TCompileEvents& events, const CompileCategory gatherLimit, const ExportParams::Includers includersMode )
	{ 
		fastl::vector<CompileEvent*> eventStack;

		//Process Timeline elements
		for (CompileEvent& element : events)
		{ 		
			//update stack 
			while (!eventStack.empty() && ( element.start >= eventStack.back()->start + eventStack.back()->duration ) ) eventStack.pop_back();
			CompileEvent* parent = eventStack.empty() ? nullptr : eventStack.back();
			eventStack.push_back( &element );

			if (element.category < gatherLimit)
			{ 
				CompileData& compileData = CreateGlobalEntry(scoreData,element);
				compileData.accumulated += element.duration;
				compileData.minimum = Utils::Min(element.duration,compileData.minimum);

				if (element.duration >= compileData.maximum)
				{ 
					compileData.maximum = element.duration;
					compileData.maxId =unit.unitId;
				}

				ProcessParent( scoreData, element, parent, unit, includersMode );

				++compileData.count;
			}

			if (element.category < CompileCategory::DisplayCount)
			{
				if ( parent == nullptr || parent->category != element.category )
				{
					unit.values[ToUnderlying(element.category)] += element.duration;
				}
			}
		}
	}

	// -----------------------------------------------------------------------------------------------------------
	void ProcessTimeline(ScoreData& scoreData, ScoreTimeline& timeline, const CompileUnitContext& context)
	{
		//Get Gather limit
		ExportParams* exportParams = Context::Get<ExportParams>(); 
		const CompileCategory gatherLimit = exportParams? GetDetailCategory(exportParams->detail) : CompileCategory::GatherFull;
		ExportParams::Includers includersMode = exportParams ? exportParams->includers : ExportParams::Includers::Enabled;

		//Create new unit
		const U32 unitId = static_cast<U32>(scoreData.units.size());
		scoreData.units.emplace_back(unitId);
		CompileUnit& unit = scoreData.units.back();
		unit.nameHash = timeline.nameHash;
		unit.context = context;
		
		for (TCompileEvents& track : timeline.tracks)
		{ 
			ProcessTimelineTrack(scoreData,unit,track,gatherLimit,includersMode);
		}

		IO::ScoreBinarizer* binarizer = Context::Get<IO::ScoreBinarizer>(); 
		if (binarizer && exportParams && exportParams->timeline == ExportParams::Timeline::Enabled) 
		{
			//Remove unwanted elements from the timeline
			const CompileCategory timelineLimit = GetDetailCategory(exportParams->timelineDetail);
			if (timelineLimit < CompileCategory::GatherFull)
			{ 
				for (TCompileEvents& track : timeline.tracks)
				{
					track.erase(fastl::remove_if(track.begin(),track.end(),
						[=](const CompileEvent& input)
						{ 
							return input.category >= timelineLimit && input.category < CompileCategory::GatherFull; 
						}
					),track.end());
				}
			}

			binarizer->Binarize(timeline);
		}
	}

	// -----------------------------------------------------------------------------------------------------------
	size_t AddFolder(TCompileFolders& folders, const char* path)
	{
		//Find and create the current folder node
		size_t folderIndex = 0;
		const char* folderStart = path;
		const char* folderEnd = path;
		for (; *folderEnd != '\0'; ++folderEnd)
		{
			if (*folderEnd == '/' || *folderEnd == '\\')
			{
				//folder found, move or create
				const size_t folderNameLength = folderEnd - folderStart;
				const U64 strHash = Hash::AppendToCRC64(0ull, folderStart, folderNameLength);

				CompileFolder& currentFolder = folders[folderIndex];

				auto const& result = currentFolder.children.insert(TCompileDataDictionary::value_type(strHash, static_cast<U32>(folders.size())));
				folderIndex = result.first->second; //move to the child folder
				if (result.second)
				{
					//New folder found, add to list of project folders
					folders.emplace_back(folderStart, folderNameLength);
				}
				
				folderStart = folderEnd + 1;
			}
		}

		return folderIndex;
	}

	// -----------------------------------------------------------------------------------------------------------
	void FinalizeScoreData(ScoreData& scoreData)
	{
		//setup the scoredata
		scoreData.folders.clear();
		scoreData.folders.emplace_back();
		scoreData.session.fullDuration = 0u; 
		scoreData.session.numThreads = 0u;
		fastl::memset(scoreData.session.totals, 0, sizeof(U64) * ToUnderlying(CompileCategory::DisplayCount));

		//Normalize unit start times and threads
		typedef fastl::unordered_map<U32,U32> TThreadDictionary; 
		TThreadDictionary threadDict;
		U32 nextThreadIndex = 0;
		U64 minStartTime = 0xffffffffffffffff;

		for (CompileUnit& unit : scoreData.units)
		{
			//Add Threads and prepare for total time computations
			for (size_t k = 0; k < 2; ++k)
			{
				minStartTime = unit.context.startTime[k] < minStartTime ? unit.context.startTime[k] : minStartTime;

				auto const& result = threadDict.insert(TThreadDictionary::value_type(unit.context.threadId[k], nextThreadIndex));
				unit.context.threadId[k] = result.first->second;
				nextThreadIndex += result.second ? 1 : 0;
			}

			//Add Totals
			for (size_t i = 0; i < ToUnderlying(CompileCategory::DisplayCount); ++i)
			{
				scoreData.session.totals[i] += unit.values[i];
			}

			//Add path to folders
			TCompileStrings::const_iterator found = scoreData.strings.find(unit.nameHash);
			if (found != scoreData.strings.end())
			{
				const size_t folderIndex = AddFolder(scoreData.folders,found->second.c_str());
				scoreData.folders[folderIndex].unitIds.emplace_back(unit.unitId);
			}
		}

		scoreData.session.numThreads = nextThreadIndex;

		for (CompileUnit& unit : scoreData.units)
		{
			unit.context.startTime[0] -= minStartTime;
			unit.context.startTime[1] -= minStartTime;
			const U64 frontEndFinish = unit.context.startTime[0] + unit.values[ToUnderlying(CompileCategory::FrontEnd)];
			const U64 backEndFinish  = unit.context.startTime[1] + unit.values[ToUnderlying(CompileCategory::BackEnd)];
			scoreData.session.fullDuration = scoreData.session.fullDuration > frontEndFinish ? scoreData.session.fullDuration : frontEndFinish;
			scoreData.session.fullDuration = scoreData.session.fullDuration > backEndFinish ? scoreData.session.fullDuration : backEndFinish;
		}

		//Add folder paths for the includes
		const TCompileDatas& includeData = scoreData.globals[ToUnderlying(CompileCategory::Include)];
		const U32 numIncludes = static_cast<U32>(includeData.size());
		for (U32 i=0;i<numIncludes;++i)
		{
			const CompileData& data = includeData[i];
			TCompileStrings::const_iterator found = scoreData.strings.find(data.nameHash);
			if (found != scoreData.strings.end())
			{
				const size_t folderIndex = AddFolder(scoreData.folders, found->second.c_str());
				scoreData.folders[folderIndex].includeIds.emplace_back(i);
			}
		}
	}
}