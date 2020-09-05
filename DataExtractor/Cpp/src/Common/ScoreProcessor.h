#pragma once

struct ScoreData;
struct ScoreTimeline;

namespace CompileScore
{ 
	void ProcessTimeline(ScoreData& scoreData, ScoreTimeline& timeline); 
}