#include "ClangScore.h"

#include "../Common/CommandLine.h"
#include "../Common/IOStream.h"
#include "../Common/ScoreDefinitions.h"

namespace Clang 
{ 
	// -----------------------------------------------------------------------------------------------------------
	int ExtractScore(const ExportParams& params)
	{ 
		ScoreData scoreData;

		//TODO ~ ramonv ~ to be implemented
		//Scan Directory 
		//Open all files 
		//Find elements 

		IOStream::Binarize(params.output, scoreData);

		return 0;
	} 
}