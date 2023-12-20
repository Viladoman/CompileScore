#pragma once

namespace CompileScore
{
	struct File;
	struct Result;

	bool IsFileEmpty(const File& file);
	void Finalize(Result& result);
}