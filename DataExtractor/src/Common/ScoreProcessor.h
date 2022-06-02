#pragma once

struct ScoreData;
struct ScoreTimeline;
struct CompileUnitContext;

namespace CompileScore
{ 
	void ProcessTimeline(ScoreData& scoreData, ScoreTimeline& timeline, const CompileUnitContext& context);
	void FinalizeScoreData(ScoreData& scoreData);
}