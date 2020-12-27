#include "StringUtils.h"

namespace StringUtils
{ 
    // -----------------------------------------------------------------------------------------------------------
    void ToPathBaseName(fastl::string& path)
    { 
        size_t foundIndex = fastl::string::npos;
        for(size_t i=0u,sz=path.length();i<sz;++i)
        { 
            const char c = path[i];
            if (c=='/' || c=='\\') foundIndex = i; 
        }

        if (foundIndex != fastl::string::npos)
        {
            path.erase(0, foundIndex + 1);
        }
    }

    // -----------------------------------------------------------------------------------------------------------
    void RemoveExtension(fastl::string& path)
    { 
        size_t foundIndex = fastl::string::npos;
        const size_t length = path.length();

        for(size_t i=0u;i<length;++i)
        { 
            if (path[i]=='.') foundIndex = i; 
        }

        if (foundIndex != fastl::string::npos)
        {
            path.erase(foundIndex,length-foundIndex);
        }
    }

    // -----------------------------------------------------------------------------------------------------------
    char ToLower(char c)
    {
        constexpr char diff = ('a'-'A');
        return (c >= 'A' && c <= 'Z')? c+diff : c;
    }

    // -----------------------------------------------------------------------------------------------------------
    void ToLower(fastl::string& input)
    {
        for (size_t i = 0,sz=input.length(); i < sz; ++ i) 
        {
            input[i] = ToLower(input[i]);  
        }
    }
}
