#pragma once

#include "../fastl/string.h"

namespace StringUtils
{ 
    void ToPathBaseName(fastl::string& path);    
    void RemoveExtension(fastl::string& path);
    void ToLower(fastl::string& input);
    void NormalizePath(fastl::string& path);
    void CollapseTemplates(fastl::string& input);
}