#pragma once

#ifdef USE_FASTL

#include "map.h"

namespace fastl
{
	// Build unordered_map as a map 
	template<typename TKey, typename TValue> using unordered_map = fastl::map<TKey, TValue>;
}

#else 

#include <unordered_map>

namespace fastl
{
	template<typename TKey, typename TValue> using unordered_map = std::unordered_map<TKey, TValue>;
}

#endif //USE_FASTL

#ifdef FASTL_EXPOSE_PLAIN_ALIAS

template<typename TKey, typename TValue> using unordered_map = fastl::unordered_map<TKey, TValue>;

#endif //FASTL_EXPOSE_PLAIN_ALIAS