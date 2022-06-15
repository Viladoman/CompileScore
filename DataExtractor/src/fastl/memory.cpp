#include "memory.h"

#ifndef USE_FASTL
#include <cstring>
#endif

namespace fastl
{
#ifdef USE_FASTL
	void memcpy(void* dest, void* src, size_t n)
	{
		unsigned char* csrc = (unsigned char*)src;
		unsigned char* cdest = (unsigned char*)dest;
		for (size_t i = 0; i < n; ++i) cdest[i] = csrc[i];
	}

	void* memset(void* dest, int c, size_t n)
	{
		unsigned char cval = (unsigned char)c;
		unsigned char* cdest = (unsigned char*) dest;
		for (size_t i = 0; i < n; ++i) cdest[i] = cval;
		return dest;
	}
#else 
	void memcpy(void* dest, void* src, size_t n)
	{
		::memcpy(dest, src, n);
	}

	void* memset(void* dest, int c, size_t n)
	{
		return ::memset(dest, c, n);
	}

#endif

}