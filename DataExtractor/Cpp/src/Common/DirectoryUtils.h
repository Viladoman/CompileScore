#pragma once

namespace IO
{ 
	class DirectoryScanner
	{ 
	public:
		DirectoryScanner(const char* pathToScan, const char* extension);
		~DirectoryScanner();

		const char* SeekNext();
	private: 
		 struct Impl; 
		 Impl* m_impl;
	};

	bool Exists(const char* path);
	bool IsDirectory(const char* path);
	bool IsExtension(const char* path, const char* extension);
}
