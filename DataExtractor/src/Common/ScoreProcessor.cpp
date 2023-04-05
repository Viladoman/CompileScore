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
		enum : U32 { kInvalidIndex = 0xffffffff };

		// -----------------------------------------------------------------------------------------------------------
		template <typename T> inline constexpr T Min(const T a, const T b) { return a < b? a : b; }
		template <typename T> inline constexpr T Max(const T a, const T b) { return a < b? b : a; }
	}

	// -----------------------------------------------------------------------------------------------------------
	U64 StoreString(ScoreData& scoreData, const fastl::string& str)
	{
		const U64 strHash = Hash::AppendToCRC64(0ull, str.c_str(), str.length() );
		if (strHash)
		{
			scoreData.strings.insert(TCompileStrings::value_type(strHash, str));
		}
		return strHash;
	}

	// -----------------------------------------------------------------------------------------------------------
	U64 StoreString(ScoreData& scoreData, const char* str, size_t length)
	{
		return StoreString(scoreData, fastl::string(str, length));
	}

	// -----------------------------------------------------------------------------------------------------------
	U64 StoreString(ScoreData& scoreData, const char* str)
	{
		U32 length = 0u;
		for (; str[length] != '\0'; ++length) {}
		return StoreString(scoreData, str, length);
	}

	// -----------------------------------------------------------------------------------------------------------
	U64 StorePathString(ScoreData& scoreData, const char* str, size_t length)
	{
		fastl::string path(str, length);
		StringUtils::NormalizePath(path);
		return StoreString(scoreData, path);
	}

	// -----------------------------------------------------------------------------------------------------------
	U64 StoreSymbolString(ScoreData& scoreData, const char* str, size_t length)
	{
		ExportParams* exportParams = Context::Get<ExportParams>();
		if (exportParams && exportParams->templateArgs == ExportParams::TemplateArgs::Keep) 
		{
			return StoreString(scoreData, str, length);
		}
		else
		{
			fastl::string symbolName(str, length);
			StringUtils::CollapseTemplates(symbolName);
			return StoreString(scoreData, symbolName);
		}
	}

	// -----------------------------------------------------------------------------------------------------------
	U64 StoreSymbolString(ScoreData& scoreData, const char* str)
	{
		U32 length = 0u;
		for (; str[length] != '\0'; ++length) {}
		return StoreSymbolString(scoreData, str, length);
	}

	// -----------------------------------------------------------------------------------------------------------
	U64 StoreCategoryValueString(ScoreData& scoreData, const char* str, size_t length, CompileCategory category)
	{
		switch (category)
		{
		case CompileCategory::Include:
		case CompileCategory::ExecuteCompiler:
			return StorePathString(scoreData, str, length); 

		case CompileCategory::ParseClass:
		case CompileCategory::ParseTemplate:
		case CompileCategory::InstantiateClass:
		case CompileCategory::InstantiateConcept:
		case CompileCategory::InstantiateVariable:
		case CompileCategory::InstantiateFunction:
		case CompileCategory::CodeGenFunction:
		case CompileCategory::OptimizeFunction:
			return StoreSymbolString(scoreData, str, length);

		default: 
			return StoreString(scoreData, str, length); 
		}
	}

	// -----------------------------------------------------------------------------------------------------------
	U64 StoreCategoryValueString(ScoreData& scoreData, const char* str, CompileCategory category)
	{
		U32 length = 0u;
		for (; str[length] != '\0'; ++length) {}
		return StoreCategoryValueString(scoreData, str, length, category);
	}

	// -----------------------------------------------------------------------------------------------------------
	U32 CreateGlobalEntry(ScoreData& scoreData, CompileEvent& element)
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

			return nextIndex;
		} 
		
		element.nameId = result.first->second;
		return element.nameId;
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
	}

	void PopTimelineStackEvent(ScoreData& scoreData, const CompileUnit& unit, fastl::vector<CompileEvent*>& eventStack, fastl::vector<U32>& dataIdStack, const CompileCategory gatherLimit, const ExportParams::Includers includersMode)
	{
		//Check what happened with the children and fixup any remaining parent data
		CompileEvent* thisEvent = eventStack.back();
		const U32 thisIndex = dataIdStack.back();
		if (thisIndex != Utils::kInvalidIndex && thisEvent->category < gatherLimit)
		{
			// Finalize post computation when closing this event ( self duration calculations )
			TCompileDatas& global = scoreData.globals[ToUnderlying(thisEvent->category)];
			CompileData& thisCompileData = global[thisIndex];

			if (thisEvent->selfDuration >= thisCompileData.selfMaximum)
			{
				thisCompileData.selfMaximum = thisEvent->selfDuration;
				thisCompileData.selfMaxId = unit.unitId;
			}

			thisCompileData.selfAccumulated += thisEvent->selfDuration;
		}

		eventStack.pop_back();
		dataIdStack.pop_back();

		CompileEvent* parent = eventStack.empty() ? nullptr : eventStack.back();
		if (parent != nullptr && thisEvent->category == parent->category) {
			parent->selfDuration -= thisEvent->duration;
		}

		ProcessParent(scoreData, *thisEvent, parent, unit, includersMode);
	}


	// -----------------------------------------------------------------------------------------------------------
	void ProcessTimelineTrack(ScoreData& scoreData, CompileUnit& unit, TCompileEvents& events, const CompileCategory gatherLimit, const ExportParams::Includers includersMode )
	{ 
		fastl::vector<CompileEvent*> eventStack;
		fastl::vector<U32>           dataIdStack;

		//Process Timeline elements
		for (CompileEvent& element : events)
		{ 		
			//update stack 
			while (!eventStack.empty() && (element.start >= eventStack.back()->start + eventStack.back()->duration))
			{
				PopTimelineStackEvent(scoreData, unit, eventStack, dataIdStack, gatherLimit, includersMode);
			}
			CompileEvent* parent = eventStack.empty() ? nullptr : eventStack.back();
			eventStack.push_back( &element );

			if (element.category < gatherLimit)
			{ 
				const U32 globalIndex = CreateGlobalEntry(scoreData,element);
				CompileData& compileData = scoreData.globals[ToUnderlying(element.category)][globalIndex];
				dataIdStack.push_back(globalIndex);

				compileData.accumulated += element.duration;
				compileData.minimum = Utils::Min(element.duration,compileData.minimum);

				if (element.duration >= compileData.maximum)
				{ 
					compileData.maximum = element.duration;
					compileData.maxId = unit.unitId;
				}

				++compileData.count;
			}
			else
			{
				dataIdStack.push_back(Utils::kInvalidIndex);  
			}

			if (element.category < CompileCategory::DisplayCount)
			{
				if ( parent == nullptr || parent->category != element.category )
				{
					unit.values[ToUnderlying(element.category)] += element.duration;
				}
			}
		}

		//Pop the remaining stack
		while (!eventStack.empty())
		{
			PopTimelineStackEvent(scoreData, unit, eventStack, dataIdStack, gatherLimit, includersMode);
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
	void ReconstructThreadModel(ScoreData& scoreData)
	{
		//sort all units by time start via back insertion sort
		fastl::vector<CompileUnit*> units;
		units.resize(scoreData.units.size());

		U32 numInsertions = 0u;
		U32 insertionPoint = 0u;
		for (CompileUnit& unit : scoreData.units)
		{
			const U64 thisUnitTime = unit.context.startTime[0];
			while (insertionPoint > 0 && units[insertionPoint - 1]->context.startTime[0] > thisUnitTime)
			{
				units[insertionPoint] = units[insertionPoint - 1];
				--insertionPoint;
			}

			units[insertionPoint] = &unit;
			insertionPoint = ++numInsertions;
		}

		fastl::vector<U64> threads;
		threads.emplace_back(0u);
		U32 lowestThreadIndex = 0u;
		U32 numThreads = 1u;

		for (CompileUnit* unit : units)
		{
			//Check if we need to use a new thread
			const U64 startTime = unit->context.startTime[0]; 
			const U64 lowestThreadTime = threads[lowestThreadIndex];
			if ( lowestThreadTime > startTime)
			{
				//assign unit to new thread - update/keep lowest thread
				unit->context.threadId[0] = numThreads;
				unit->context.threadId[1] = numThreads;

				const U64 endTime = startTime + unit->values[ToUnderlying(CompileCategory::ExecuteCompiler)];
				threads.emplace_back(endTime);
				lowestThreadIndex = lowestThreadTime > endTime ? numThreads : lowestThreadIndex;
				++numThreads;
			}
			else
			{
				//assign unit to lowest thread
				threads[lowestThreadIndex] = startTime + unit->values[ToUnderlying(CompileCategory::ExecuteCompiler)];
				unit->context.threadId[0] = lowestThreadIndex;
				unit->context.threadId[1] = lowestThreadIndex;

				//find the next lowest thread
				U64 lowestTime = 0xffffffffffffffff;
				for (U32 i = 0u; i < numThreads; ++i)
				{
					if (threads[i] < lowestTime)
					{
						lowestTime = threads[i];
						lowestThreadIndex = i;
					}
				}
			}
		}

		scoreData.session.numThreads = numThreads;
	}

	// -----------------------------------------------------------------------------------------------------------
	void NormalizeThreadIds(ScoreData& scoreData)
	{
		typedef fastl::unordered_map<U32, U32> TThreadDictionary;
		TThreadDictionary threadDict;
		U32 nextThreadIndex = 0;

		for (CompileUnit& unit : scoreData.units)
		{
			//Add Threads and prepare for total time computations
			for (size_t k = 0; k < 2; ++k)
			{
				auto const& result = threadDict.insert(TThreadDictionary::value_type(unit.context.threadId[k], nextThreadIndex));
				unit.context.threadId[k] = result.first->second;
				nextThreadIndex += result.second ? 1 : 0;
			}
		}

		scoreData.session.numThreads = nextThreadIndex;
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

		//Normalize unit start times
		U64 minStartTime = 0xffffffffffffffff;

		for (CompileUnit& unit : scoreData.units)
		{
			//Add Threads and prepare for total time computations
			for (size_t k = 0; k < 2; ++k)
			{
				minStartTime = unit.context.startTime[k] < minStartTime ? unit.context.startTime[k] : minStartTime;
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

		for (CompileUnit& unit : scoreData.units)
		{
			unit.context.startTime[0] -= minStartTime;
			unit.context.startTime[1] -= minStartTime;
			const U64 frontEndFinish = unit.context.startTime[0] + unit.values[ToUnderlying(CompileCategory::FrontEnd)];
			const U64 backEndFinish  = unit.context.startTime[1] + unit.values[ToUnderlying(CompileCategory::BackEnd)];
			scoreData.session.fullDuration = scoreData.session.fullDuration > frontEndFinish ? scoreData.session.fullDuration : frontEndFinish;
			scoreData.session.fullDuration = scoreData.session.fullDuration > backEndFinish ? scoreData.session.fullDuration : backEndFinish;
		}

		//Normalize the threadIds 
		ExportParams* exportParams = Context::Get<ExportParams>();
		if (exportParams && exportParams->source == ExportParams::Source::Clang)
		{
			//Clang threadIds are not consistent. 
			//We reconstruct the thread timeline from the unit timers that got provided.
			ReconstructThreadModel(scoreData); 
		}
		else
		{
			//We trust MSVC threadIds to be consistent.
			//this means we just need to normalize the ids
			NormalizeThreadIds(scoreData);
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