#pragma once

#include "BasicTypes.h"

namespace IO
{ 
	using FileTimeStamp = U64;
	constexpr static FileTimeStamp NO_TIMESTAMP = 0ull;

	class DirectoryScanner
	{ 
	public:
		DirectoryScanner(const char* pathToScan, const char* extension, FileTimeStamp threshold = NO_TIMESTAMP);
		~DirectoryScanner();

		const char* SeekNext();
	private: 
		 struct Impl; 
		 Impl* m_impl;
	};

	bool Exists(const char* path);
	bool IsDirectory(const char* path);
	bool IsExtension(const char* path, const char* extension);
	FileTimeStamp GetCurrentTime();
}
