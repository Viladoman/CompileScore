
#include "Parser.h"

int main(int argc, const char **argv) 
{
    if (Parser::Parse(argc, &argv[0]))
    {
        return 0;
    }

    return -1;
}
