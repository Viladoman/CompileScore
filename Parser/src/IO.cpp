#include "IO.h"

#include <cstdio>
#include <cstdarg>

#include "ParserDefinitions.h"

#pragma optimize("",off) //TODO ~ Ramonv ~ remove 

namespace IO
{
	enum { DATA_VERSION = 1 };

	using TBuffer = FILE*;
	using U8 = char;

    //////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // Logging
    //////////////////////////////////////////////////////////////////////////////////////////////////////////////

    // -----------------------------------------------------------------------------------------------------------
    void Log(const char* format, ...)
    {
        va_list argptr;
        va_start(argptr, format);
        vfprintf(stderr, format, argptr);
        va_end(argptr);
    }

    // -----------------------------------------------------------------------------------------------------------
    void LogTime(const char* prefix, long miliseconds)
    {
        long seconds = miliseconds / 1000;
        miliseconds = miliseconds - (seconds * 1000);

        long minutes = seconds / 60;
        seconds = seconds - (minutes * 60);

        long hours = minutes / 60;
        minutes = minutes - (hours * 60);

        if (hours)        Log("%s%02uh %02um", prefix, hours, minutes);
        else if (minutes) Log("%s%02um %02us", prefix, minutes, seconds);
        else if (seconds) Log("%s%02us %02ums", prefix, seconds, miliseconds);
        else              Log("%s%02ums", prefix, miliseconds);
    }

    //////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // Binarize
    //////////////////////////////////////////////////////////////////////////////////////////////////////////////

    namespace BinUtils
    {
        // -----------------------------------------------------------------------------------------------------------------
        template<typename T> void Binarize(FILE* stream, T input)
        {
            fwrite(&input, sizeof(T), 1, stream);
        }

        // -----------------------------------------------------------------------------------------------------------
        void BinarizeString(FILE* stream, const std::string& str)
        {
            //Perform size encoding in 7bitSize format
            size_t strSize = str.length();
            do
            {
                const U8 val = strSize < 0x80 ? strSize & 0x7F : (strSize & 0x7F) | 0x80;
                fwrite(&val, sizeof(U8), 1, stream);
                strSize >>= 7;
            } while (strSize);

            fwrite(str.c_str(), str.length(), 1, stream);
        }
    }

    //////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // Print
    //////////////////////////////////////////////////////////////////////////////////////////////////////////////

    namespace PrintUtils
    {
        bool IsFileEmpty(const CompileScore::File& file)
        {
            for (int i = 0; i < CompileScore::GlobalRequirementType::Count; ++i)
            {
                if (!file.global[i].empty()) return false;
            }
            
            for (const CompileScore::StructureRequirement& structure : file.structures)
            {
                for (int i = 0; i < CompileScore::StructureSimpleRequirementType::Count; ++i)
                {
                    if (!structure.simpleRequirements[i].empty()) return false;
                }

                for (int i = 0; i < CompileScore::StructureNamedRequirementType::Count; ++i)
                {
                    if (!structure.namedRequirements[i].empty()) return false;
                }
            }

            return true;
        }

        const char* GetGlobalRequirementName(CompileScore::GlobalRequirementType::Enumeration input)
        {
            switch (input)
            {
            case CompileScore::GlobalRequirementType::MacroExpansion:   return "Macro Expansions";    
            case CompileScore::GlobalRequirementType::FreeFunctionCall: return "Free Function Calls";
            case CompileScore::GlobalRequirementType::EnumInstance:     return "Enum Instances";
            case CompileScore::GlobalRequirementType::EnumConstant:     return "Enum Constants";
            default: return "???";
            }
        }

        const char* GetStructureSimpleRequirementName(CompileScore::StructureSimpleRequirementType::Enumeration input)
        {
            switch (input)
            {
            case CompileScore::StructureSimpleRequirementType::Instance:         return "Instances";
            case CompileScore::StructureSimpleRequirementType::Reference:        return "References";
            case CompileScore::StructureSimpleRequirementType::Inheritance:      return "Inheritances";
            case CompileScore::StructureSimpleRequirementType::MemberField:      return "Member Field";
            case CompileScore::StructureSimpleRequirementType::FunctionArgument: return "Function Arguments";
            case CompileScore::StructureSimpleRequirementType::FunctionReturn:   return "Function Returns";
            default: return "???";
            }
        }

        const char* GetStructureNamedRequirementName(CompileScore::StructureNamedRequirementType::Enumeration input)
        {
            switch (input)
            {
            case CompileScore::StructureNamedRequirementType::MethodCall:  return "Method Call";
            case CompileScore::StructureNamedRequirementType::FieldAccess: return "Field Access";
            default: return "???";
            }
        }

        void Print(const CompileScore::CodeRequirement& requirement, int tab)
        {        
            for (int i = 0; i < tab; ++i) Log("\t");
            Log("%s (%d:%d) - found %d @ ", requirement.name.c_str(), requirement.defLocation.row, requirement.defLocation.column, requirement.useLocations.size() );

            for (CompileScore::FileLocation loc : requirement.useLocations)
            {
                Log("(%d:%d) ", loc.row, loc.column);
            }

            Log("\n");
        }

        void Print(const CompileScore::TRequirements& requirements, const char* label, int tab)
        {
            if (requirements.empty())
                return;

            for (int i = 0; i < tab; ++i) Log("\t");

            Log("%s: \n", label);

            for (const CompileScore::CodeRequirement& requirement : requirements)
            {
                Print(requirement, tab + 1);
            }
        }

        void Print(const CompileScore::StructureRequirement& structure, int tab)
        {
            for (int i = 0; i < tab; ++i) Log("\t");
            Log("Structure %s:\n", structure.name.c_str());

            for (int i = 0; i < CompileScore::StructureSimpleRequirementType::Count; ++i)
            {
                if (!structure.simpleRequirements[i].empty())
                {
                    for (int i = 0; i < tab + 1; ++i) Log("\t");
                    Log("%s - found %d @ ", GetStructureSimpleRequirementName(CompileScore::StructureSimpleRequirementType::Enumeration(i)), structure.simpleRequirements[i].size() );

                    for (CompileScore::FileLocation loc : structure.simpleRequirements[i])
                    {
                        Log("(%d:%d) ", loc.row, loc.column);
                    }

                    Log("\n");
                }
            }

            for (int i = 0; i < CompileScore::StructureNamedRequirementType::Count; ++i)
            {
                Print(structure.namedRequirements[i], GetStructureNamedRequirementName(CompileScore::StructureNamedRequirementType::Enumeration(i)), tab + 1);
            }
        }

        void Print(const CompileScore::File& file, int tab = 0)
        {
            if (PrintUtils::IsFileEmpty(file))
                return;

            Log("File %s:\n", file.name.empty() ? "?????" : file.name.c_str());

            for (int i = 0; i < CompileScore::GlobalRequirementType::Count; ++i)
            {
                Print(file.global[i], GetGlobalRequirementName(CompileScore::GlobalRequirementType::Enumeration(i)), tab + 1);
            }

            for (const CompileScore::StructureRequirement& structure : file.structures)
            {
                Print(structure, tab + 1);
            }
        }
    }

    //////////////////////////////////////////////////////////////////////////////////////////////////////////////

    bool ToFile(const CompileScore::Result& result, const char* filename)
    {
        FILE* stream;
        const errno_t openResult = fopen_s(&stream, filename, "wb");
        if (openResult)
        {
            return false;
        }

        BinUtils::Binarize(stream, DATA_VERSION);
    
        //TODO ~ ramonv ~ to be implemented

        fclose(stream);
        return true;
    }

    void ToPrint(const CompileScore::Result& result)
    {
        Log("Dependency Requirements found: \n");

        for( const CompileScore::File& file : result.files )
        {
            if (!PrintUtils::IsFileEmpty(file))
            {
                PrintUtils::Print(file);
            }
        }

        PrintUtils::Print(result.otherFile);
    }

}