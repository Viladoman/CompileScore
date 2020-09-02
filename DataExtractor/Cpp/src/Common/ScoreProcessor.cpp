#include "../Common/ScoreDefinitions.h"

namespace CompileScore
{ 
	namespace Utils
	{ 
		// -----------------------------------------------------------------------------------------------------------
		template <typename T> inline constexpr T Min(const T a, const T b) { return a < b? a : b; }
		template <typename T> inline constexpr T Max(const T a, const T b) { return a < b? b : a; }
	}

	// -----------------------------------------------------------------------------------------------------------
	void ProcessTimeline(ScoreData& scoreData, const ScoreTimeline& timeline)
	{
		U32 overlapThreshold[ToUnderlying(CompileCategory::DisplayCount)] = {};

		//Create new unit
		scoreData.units.emplace_back();
		CompileUnit& unit = scoreData.units.back();
		unit.name = timeline.name;

		//Process Timeline elements
		for (const CompileEvent& element : timeline.events)
		{ 
			if (element.category < CompileCategory::GahterCount)
			{ 
				//Add Data to Globals
				CompileData& compileData = scoreData.globals[ToUnderlying(element.category)][element.name];
				compileData.accumulated += element.duration;
				compileData.min = Utils::Min(element.duration,compileData.min);
				compileData.max = Utils::Max(element.duration,compileData.max);
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

		//TODO ~ ramonv ~ Export Timeline for timeline viewer 
	}
}