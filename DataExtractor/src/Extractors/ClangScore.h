#pragma once

struct ExportParams;

namespace Clang 
{ 
	struct Extractor
	{ 
		static int StartRecording(const ExportParams& params);
		static int CancelRecording(const ExportParams& params);
		static int StopRecording(const ExportParams& params);
		static int GenerateScore(const ExportParams& params);
	};
}