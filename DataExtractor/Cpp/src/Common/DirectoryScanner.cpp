#include "DirectoryScanner.h"

//TODO ~ ramonv ~ This include hurts a lot - I need to find a substitution that works on all platforms
#include <filesystem>

#include "IOStream.h"

namespace fs = std::filesystem;

namespace IO
{ 
    //////////////////////////////////////////////////////////////////////////////////////////////////////////////

    struct DirectoryScanner::Impl
    { 
        DirectoryScanner::Impl(const char* _extension)
            : extension(_extension)
        {}

        const char* extension;    
        fs::recursive_directory_iterator cursor; 
        std::string cursorPath;
    };

    //////////////////////////////////////////////////////////////////////////////////////////////////////////////

    // -----------------------------------------------------------------------------------------------------------
    DirectoryScanner::DirectoryScanner(const char* pathToScan, const char* extension)
        : m_impl( new Impl(extension) )
    {
        m_impl->cursor = fs::recursive_directory_iterator(pathToScan);
    }

    // -----------------------------------------------------------------------------------------------------------
    const char* DirectoryScanner::SeekNext()
    {
        for(; m_impl->cursor != fs::recursive_directory_iterator() && m_impl->cursor->path().extension().string() != m_impl->extension; ++m_impl->cursor ){}

        if (m_impl->cursor == fs::recursive_directory_iterator())
        { 
            m_impl->cursorPath.clear();
            return nullptr;
        }
         
        m_impl->cursorPath = m_impl->cursor->path().string().c_str();
        ++m_impl->cursor;
        return m_impl->cursorPath.c_str();
    }
}


