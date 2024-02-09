#include "CRC64.h"

namespace Hash
{
    U64 AppendToCRC64(U64 previousCRC, const char* rawData, U64 size)
    {
        U64 crc = previousCRC ^ 0xFFFFFFFFFFFFFFFFull;
        for (const U8* p = (const U8*)rawData; size > 0; size--)
        {
            crc = (crc >> 8) ^ CRC64table[(U8)((U8)crc ^ *p++)];
        }
        return (crc ^ 0xFFFFFFFFFFFFFFFFull);
    }

    U64 CreateCRC64(const char* buf)
    {
        U64 length = 0u;
        for (; buf[length] != '\0'; ++length) {}
        return AppendToCRC64(0ull, buf, length);
    }
}
