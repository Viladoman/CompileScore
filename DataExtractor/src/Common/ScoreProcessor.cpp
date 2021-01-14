#include "../Common/Context.h"
#include "../Common/ScoreDefinitions.h"
#include "../fastl/algorithm.h"

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
	CompileData& CreateGlobalEntry(ScoreData& scoreData, CompileEvent& element)
	{ 
		const CompileCategoryType globalIndex = ToUnderlying(element.category);
		TCompileDatas& global = scoreData.globals[globalIndex];
		TCompileDataDictionary& dictionary = scoreData.globalsDictionary[globalIndex];

		const U32 nextIndex = static_cast<U32>(global.size());
		std::pair<TCompileDataDictionary::iterator,bool> const& result = dictionary.insert(TCompileDataDictionary::value_type(element.name,nextIndex));
		if (result.second) 
		{ 
			//the element got inserted
			element.nameId = nextIndex;
			global.emplace_back(element.name);
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
	void ProcessTimelineTrack(ScoreData& scoreData, CompileUnit& unit, TCompileEvents& events, const CompileCategory gatherLimit)
	{ 
		U32 overlapThreshold[ToUnderlying(CompileCategory::DisplayCount)] = {};
	
		//Process Timeline elements
		for (CompileEvent& element : events)
		{ 
			if (element.category < gatherLimit)
			{ 
				CompileData& compileData = CreateGlobalEntry(scoreData,element);
				compileData.accumulated += element.duration;
				compileData.min = Utils::Min(element.duration,compileData.min);

				if (element.duration >= compileData.max)
				{ 
					compileData.max = element.duration;
					compileData.maxId =unit.unitId;
				}

				++compileData.count;
			}

			if (element.category < CompileCategory::DisplayCount)
			{
				if (element.start >= overlapThreshold[ToUnderlying(element.category)])
				{
					unit.values[ToUnderlying(element.category)] += element.duration;
					overlapThreshold[ToUnderlying(element.category)] = element.start+element.duration;
				}
			}
		}
	}

	// -----------------------------------------------------------------------------------------------------------
	void ProcessTimeline(ScoreData& scoreData, ScoreTimeline& timeline)
	{
		//Get Gather limit
		ExportParams* exportParams = Context::Get<ExportParams>(); 
		const CompileCategory gatherLimit = exportParams? GetDetailCategory(exportParams->detail) : CompileCategory::GatherFull;

		//Create new unit
		const U32 unitId = static_cast<U32>(scoreData.units.size());
		scoreData.units.emplace_back(unitId);
		CompileUnit& unit = scoreData.units.back();
		unit.name = timeline.name;
		
		for (TCompileEvents& track : timeline.tracks)
		{ 
			ProcessTimelineTrack(scoreData,unit,track,gatherLimit);
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
}