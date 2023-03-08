#include "DirectoryUtils.h"

//TODO ~ ramonv ~ This include hurts a lot - I need to find a substitution that works on all platforms
#include <filesystem>

#include "IOStream.h"

//#define USE_STL_ISEXTENSION

namespace fs = std::filesystem;

namespace IO
{ 
    //////////////////////////////////////////////////////////////////////////////////////////////////////////////

    struct DirectoryScanner::Impl
    { 
        using TTimestamp = fs::file_time_type::clock::time_point;

        DirectoryScanner::Impl(const char* _extension, TTimestamp _timeThreshold)
            : extension(_extension)
            , timeThreshold(_timeThreshold)
        {}

        bool IsValidPath(const fs::path& path) const
        {
            return path.extension().string() == extension && fs::last_write_time(path) >= timeThreshold;
        }

        const char* extension;    
        fs::recursive_directory_iterator cursor; 
        TTimestamp timeThreshold;
        std::string cursorPath;
    };

    //////////////////////////////////////////////////////////////////////////////////////////////////////////////

    // -----------------------------------------------------------------------------------------------------------
    DirectoryScanner::DirectoryScanner(const char* pathToScan, const char* extension, FileTimeStamp threshold)
        : m_impl( new Impl(extension,reinterpret_cast<Impl::TTimestamp&>(threshold)) )
    {     
        m_impl->cursor = fs::recursive_directory_iterator(pathToScan);
    }

    // -----------------------------------------------------------------------------------------------------------
    DirectoryScanner::~DirectoryScanner()
    { 
        delete m_impl;
    }

    // -----------------------------------------------------------------------------------------------------------
    const char* DirectoryScanner::SeekNext()
    {
        for(; m_impl->cursor != fs::recursive_directory_iterator() && !m_impl->IsValidPath(m_impl->cursor->path()); ++m_impl->cursor ){}

        if (m_impl->cursor == fs::recursive_directory_iterator())
        { 
            m_impl->cursorPath.clear();
            return nullptr;
        }
         
        m_impl->cursorPath = m_impl->cursor->path().string().c_str();
        ++m_impl->cursor;
        return m_impl->cursorPath.c_str();
    }

    //////////////////////////////////////////////////////////////////////////////////////////////////////////////

    // -----------------------------------------------------------------------------------------------------------
    bool Exists(const char* path)
    { 
        return fs::exists(path);
    }

    // -----------------------------------------------------------------------------------------------------------
    bool IsDirectory(const char* path)
    { 
        return fs::is_directory(path);
    }

    // -----------------------------------------------------------------------------------------------------------
    bool IsExtension(const char* path, const char* extension)
    { 
#ifdef USE_STL_ISEXTENSION
        return fs::path(path).extension() == extension;
#else
        const size_t extensionLength = strlen(extension);
        size_t pathIndex = strlen(path);

        //Find the real pathLength ( trim end spaces )
        while (pathIndex > 0 && path[pathIndex - 1] == ' ') --pathIndex;

        if (extensionLength > pathIndex) return false;
        
        pathIndex -= extensionLength;

        for (size_t i = 0; i < extensionLength; ++i,++pathIndex)
        {
            if (extension[i] != path[pathIndex]) return false;
        }

        return true; 
#endif
    }

    // -----------------------------------------------------------------------------------------------------------
    FileTimeStamp GetCurrentTime()
    { 
		auto timestamp = fs::file_time_type::clock::now();
		return reinterpret_cast<FileTimeStamp&>(timestamp);
    }

    // -----------------------------------------------------------------------------------------------------------
    U64 GetLastWriteTimeInMicros(const char* path)
    {
        const fs::file_time_type timestamp = fs::last_write_time(path);;
        std::chrono::microseconds duration = std::chrono::duration_cast<std::chrono::microseconds>(timestamp.time_since_epoch());
        return static_cast<U64>(duration.count());
    }
}


