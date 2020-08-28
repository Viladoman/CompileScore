#pragma once

typedef unsigned char U8;
typedef unsigned int  U32;
typedef long long     U64;

static_assert(sizeof(U8)  == 1,"wrong native type size");
static_assert(sizeof(U32) == 4,"wrong native type size");
static_assert(sizeof(U64) == 8,"wrong native type size");