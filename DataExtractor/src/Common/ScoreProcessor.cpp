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
		auto const& result = dictionary.insert(TCompileDataDictionary::value_type(element.name,nextIndex));
		if (result.second) 
		{ 
			//the element got inserted
			element.nameId = nextIndex;
			global.emplace_back(element.name);

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
	void ProcessParent( ScoreData& scoreData, CompileEvent& child, const CompileEvent* parent, const CompileUnit& unit )
	{
		//only includes for now
		if( parent == nullptr || child.category != CompileCategory::Include )
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
	void ProcessTimelineTrack(ScoreData& scoreData, CompileUnit& unit, TCompileEvents& events, const CompileCategory gatherLimit)
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

				ProcessParent( scoreData, element, parent, unit );

				//store parent to this as (parent->nameId / parent->category / count)

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