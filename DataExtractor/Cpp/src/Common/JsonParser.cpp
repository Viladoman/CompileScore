#include "JsonParser.h"

#include "../fastl/string.h"

namespace Json
{
    //////////////////////////////////////////////////////////////////////////////////////////////////////////////

    // -----------------------------------------------------------------------------------------------------------
    Reader::Reader(const char* content)
        : cursor(content)
    {}

    // -----------------------------------------------------------------------------------------------------------
    bool Reader::NextToken(Token& token)
    { 
        //skip all whitespace and separators
        while(*cursor == ' ' || *cursor == '\n') ++cursor;
        if (*cursor == ',' || *cursor == ':') ++cursor;
        while(*cursor == ' ' || *cursor == '\n') ++cursor;

        switch(*cursor)
        { 
        case '\0': token.type = Token::Type::EndOfFile;   return false;
        case '{':  token.type = Token::Type::ObjectOpen;  ++cursor; return true;
        case '}':  token.type = Token::Type::ObjectClose; ++cursor; return true;
        case '[':  token.type = Token::Type::ArrayOpen;  ++cursor; return true;
        case ']':  token.type = Token::Type::ArrayClose; ++cursor; return true;

        // Literal Values
        case 't':  token.type = Token::Type::True;      cursor += 4; return true;
        case 'f':  token.type = Token::Type::False;     cursor += 5; return true;
        case 'n':  token.type = Token::Type::Null;      cursor += 4; return true;
        case 'u':  token.type = Token::Type::Undefined; cursor += 9; return true;

        // String && tags
        case '\"': 
        { 
            token.str = ++cursor;
            while(*cursor != '\"' || *(cursor-1) == '\\') ++cursor; //find the end of the string
            token.length = cursor-token.str;
            token.type = Token::Type::String;
            ++cursor; //advance the closing '"'
            return true;
        }
        default: break;
        }

        //check for literal number
        if (*cursor == '-' || (*cursor >= '0' && *cursor <= '9'))
        { 
            token.type = Token::Type::Number;
            token.str = cursor++;
            while((*cursor >= '0' && *cursor <= '9') || *cursor == '.') ++cursor;
            token.length = cursor-token.str;
            return true;
        }

        token.type = Token::Type::Invalid;
        return false;
    }

    // -----------------------------------------------------------------------------------------------------------
    void Reader::SkipObject()
    { 
        Token token;
        int level = 0;
        do
        { 
            NextToken(token); 
            if (token.type == Token::Type::ObjectOpen  || token.type == Token::Type::ArrayOpen)  ++level;
            if (token.type == Token::Type::ObjectClose || token.type == Token::Type::ArrayClose) --level;
        }
        while(level > 0);
    }
}