#include "../Common/Context.h"
#include "../Common/ScoreDefinitions.h"

#include "IOStream.h"

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
		//TODO ~ ramonv ~ use hashes for this

		const CompileCategoryType globalIndex = ToUnderlying(element.category);
		TCompileDatas& global = scoreData.globals[globalIndex];
		TCompileDataDictionary& dictionary = scoreData.globalsDictionary[globalIndex];
		TCompileDataDictionary::iterator found = dictionary.find(element.name);

		if (found == dictionary.end())
		{ 
			//insert new
			element.nameId = global.size(); 
			dictionary[element.name] = element.nameId; //double lookup not great
			global.emplace_back();
			return global.back();
		} 

		element.nameId = found->second;
		return global[element.nameId];
	}

	// -----------------------------------------------------------------------------------------------------------
	void ProcessTimeline(ScoreData& scoreData, ScoreTimeline& timeline)
	{
		U32 overlapThreshold[ToUnderlying(CompileCategory::DisplayCount)] = {};

		//Create new unit
		scoreData.units.emplace_back();
		CompileUnit& unit = scoreData.units.back();
		unit.name = timeline.name;

		//Process Timeline elements
		for (CompileEvent& element : timeline.events)
		{ 
			if (element.category < CompileCategory::GahterCount)
			{ 
				CompileData& compileData = CreateGlobalEntry(scoreData,element);
				compileData.name = element.name;
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
		/*
		if (IO::Binarizer* binarizer = Context::Get<IO::Binarizer>()) 
		{ 
			binarizer->Binarize(timeline);
		}
		*/
	}
}