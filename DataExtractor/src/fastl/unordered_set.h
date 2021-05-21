#pragma once

#ifdef USE_FASTL

#include "set.h"

namespace fastl
{
	// Build unordered_map as a map 
	template<typename TKey> using unordered_set = fastl::set<TKey>;
}

#else 

#include <unordered_set>

namespace fastl
{
	template<typename TKey> using unordered_set = std::unordered_set<TKey>;
}

#endif //USE_FASTL

#ifdef FASTL_EXPOSE_PLAIN_ALIAS

template<typename TKey, typename TValue> using unordered_set = fastl::unordered_set<TKey>;

#endif //FASTL_EXPOSE_PLAIN_ALIAS