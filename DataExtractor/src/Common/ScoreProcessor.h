#pragma once

struct ScoreData;
struct ScoreTimeline;
struct CompileUnitContext;

namespace CompileScore
{
	U64 StoreString(ScoreData& scoreData, const char* str);
	U64 StoreString(ScoreData& scoreData, const char* str, size_t length);
	U64 StorePathString(ScoreData& scoreData, const char* str, size_t length);
	U64 StoreSymbolString(ScoreData& scoreData, const char* str);
	U64 StoreCategoryValueString(ScoreData& scoreData, const char* str, CompileCategory category);
	U64 StoreCategoryValueString(ScoreData& scoreData, const char* str, size_t length, CompileCategory category);

	void ProcessTimeline(ScoreData& scoreData, ScoreTimeline& timeline, const CompileUnitContext& context);
	void FinalizeScoreData(ScoreData& scoreData);
}