#pragma once 

#ifdef USE_FASTL

namespace fastl
{
	template<typename T1, typename T2>
	struct pair
	{
		typedef T1 first_type;
		typedef T2 second_type;

		pair():first(),second(){}
		pair(const T1& _first, const T2& _second):first(_first),second(_second) {}

		T1 first; 
		T2 second;
	};
}

#else 

#include <utility>

namespace fastl
{
	template<typename TFirst, typename TSecond> using pair = std::pair<TFirst, TSecond>;
}

#endif //USE_FASTL

#ifdef FASTL_EXPOSE_PLAIN_ALIAS

template<typename TFirst,typename TSecond> using pair = fastl::pair<TFirst,TSecond>;

#endif //FASTL_EXPOSE_PLAIN_ALIAS