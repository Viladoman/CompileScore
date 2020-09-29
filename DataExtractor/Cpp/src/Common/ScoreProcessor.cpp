#include "../Common/Context.h"
#include "../Common/ScoreDefinitions.h"

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
		//TODO ~ ramonv ~ use a string hash for this double lookups

		const CompileCategoryType globalIndex = ToUnderlying(element.category);
		TCompileDatas& global = scoreData.globals[globalIndex];
		TCompileDataDictionary& dictionary = scoreData.globalsDictionary[globalIndex];
		TCompileDataDictionary::iterator found = dictionary.find(element.name);

		if (found == dictionary.end())
		{ 
			//insert new
			element.nameId = static_cast<U32>(global.size()); //TODO ~ ramonv ~ careful with overflow
			dictionary[element.name] = element.nameId; //double lookup not great
			global.emplace_back(element.name);
			return global.back();
		} 

		element.nameId = found->second;
		return global[element.nameId];
	}

	// -----------------------------------------------------------------------------------------------------------
	CompileCategory GetGatherLimit()
	{ 
		if (ExportParams* exportParams = Context::Get<ExportParams>()) 
		{ 
			switch(exportParams->detail)
			{ 
				case ExportParams::Detail::None:     return CompileCategory::GatherNone;
				case ExportParams::Detail::Basic:    return CompileCategory::GatherBasic;
				case ExportParams::Detail::FrontEnd: return CompileCategory::GatherFrontEnd; 
				default: return CompileCategory::GatherFull;
			}
		}

		return CompileCategory::GatherFull; 
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
		const CompileCategory gatherLimit = GetGatherLimit(); 

		//Create new unit
		const U32 unitId = static_cast<U32>(scoreData.units.size());
		scoreData.units.emplace_back(unitId);
		CompileUnit& unit = scoreData.units.back();
		unit.name = timeline.name;
		
		for (TCompileEvents& track : timeline.tracks)
		{ 
			ProcessTimelineTrack(scoreData,unit,track,gatherLimit);
		}
		
		IO::Binarizer* binarizer = Context::Get<IO::Binarizer>(); 
		ExportParams* exportParams = Context::Get<ExportParams>(); 

		if (binarizer && exportParams && exportParams->timeline == ExportParams::Timeline::Enabled) 
		{ 
			binarizer->Binarize(timeline);
		}
	}
}