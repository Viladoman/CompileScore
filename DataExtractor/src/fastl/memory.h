#pragma once

#include <stddef.h>

namespace fastl
{
	void  memcpy(void* dest, void* src, size_t n);
	void* memset(void* dest, int c, size_t n);
}
