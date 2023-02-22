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

	// -----------------------------------------------------------------------------------------------------------
	void NormalizePath(fastl::string& input)
	{
        size_t writeIndex = 0; 
        bool wasForwardSlash = false;
        const size_t sz = input.length();
        for (size_t i = 0; i < sz; ++i)
        {
            //Convert everything to forward slashes
            if (input[i] == '\\')
            {
                input[i] = '/';
            }

            //remove consecutive forward slashes
            const bool isForwardSlash = input[i] == '/';
            if (wasForwardSlash && isForwardSlash)
            {
                continue;
            }

            //lower case the full path 
			input[writeIndex] = ToLower(input[i]);
            ++writeIndex; 
            wasForwardSlash = isForwardSlash;
		}

        if (writeIndex < sz)
        {
            input.erase(writeIndex, sz - writeIndex);
        }
	}

	// -----------------------------------------------------------------------------------------------------------
    void CollapseTemplates(fastl::string& input)
    {
        int indentLevel = 0; 
		size_t writeIndex = 0;
        const size_t sz = input.length();
		for (size_t i = 0; i < sz; ++i)
		{
			if (input[i] == '<') ++indentLevel;
            if (indentLevel == 0)
            {
			    input[writeIndex] = input[i];
                ++writeIndex;
            }
			if (input[i] == '>') --indentLevel;
		}

		if (writeIndex < sz)
		{
			input.erase(writeIndex, sz - writeIndex);
		}
    }
}
