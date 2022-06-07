#pragma once

struct ScoreData;
struct ScoreTimeline;
struct CompileUnitContext;

namespace CompileScore
{ 
	U64 StoreString(ScoreData& scoreData, const char* str);
	U64 StoreString(ScoreData& scoreData, const char* str, size_t length);
	void ProcessTimeline(ScoreData& scoreData, ScoreTimeline& timeline, const CompileUnitContext& context);
	void FinalizeScoreData(ScoreData& scoreData);
}