#include "IO.h"

#include <cstdio>
#include <cstdarg>

#include "Processor.h"
#include "ParserDefinitions.h"

namespace IO
{
	enum { DATA_VERSION = 2 };

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

        // -----------------------------------------------------------------------------------------------------------
        void BinarizeFileLocation(FILE* stream, CompileScore::FileLocation location)
        {
            Binarize(stream, static_cast<unsigned int>(location.row));
            Binarize(stream, static_cast<unsigned int>(location.column));
        }

        // -----------------------------------------------------------------------------------------------------------
        void BinarizeFileLocations(FILE* stream, const CompileScore::TFileLocations& fileLocations)
        {
            Binarize(stream, static_cast<unsigned int>(fileLocations.size()));
            for (const CompileScore::FileLocation location : fileLocations)
            {
                BinarizeFileLocation(stream, location);
            }
        }

        // -----------------------------------------------------------------------------------------------------------
        void BinarizeRequirements(FILE* stream, const CompileScore::TRequirements& requirements)
        {
            Binarize(stream, static_cast<unsigned int>(requirements.size()));
            for (const CompileScore::CodeRequirement& requirement : requirements)
            {
                BinarizeString(stream, requirement.name);
                BinarizeFileLocation(stream, requirement.defLocation);
                BinarizeFileLocations(stream, requirement.useLocations);
            }
        }

        // -----------------------------------------------------------------------------------------------------------
        void BinarizeStructure(FILE* stream, const CompileScore::StructureRequirement& structure)
        {
            BinarizeString(stream, structure.name);
            BinarizeFileLocation(stream, structure.defLocation);

            for (int i = 0; i < CompileScore::StructureSimpleRequirementType::Count; ++i)
            {
                BinarizeFileLocations(stream, structure.simpleRequirements[i]);
            }

            for (int i = 0; i < CompileScore::StructureNamedRequirementType::Count; ++i)
            {
                BinarizeRequirements(stream, structure.namedRequirements[i]);
            }

        }

        // -----------------------------------------------------------------------------------------------------------
        void BinarizeFile(FILE* stream, const CompileScore::File& file)
        {
            BinarizeString(stream, file.name);

            for (int i = 0; i < CompileScore::GlobalRequirementType::Count; ++i)
            {
                BinarizeRequirements(stream, file.global[i]);
            }

            Binarize(stream, static_cast<unsigned int>(file.structures.size()));
            for (const CompileScore::StructureRequirement& structure : file.structures)
            {
                BinarizeStructure(stream, structure);
            }
        }

        // -----------------------------------------------------------------------------------------------------------
        void BinarizeFiles(FILE* stream, const CompileScore::TFilePtrs& files)
        {
            Binarize(stream, static_cast<unsigned int>(files.size()));
            for (const CompileScore::File* file : files)
            {
                BinarizeFile(stream, *file);
            }
        }

        // -----------------------------------------------------------------------------------------------------------
        void BinarizeIncludes(FILE* stream, const CompileScore::TIncludeLinks& links)
        {
            Binarize(stream, static_cast<unsigned int>(links.size()));
            for (CompileScore::IncludeLink link : links)
            {
                Binarize(stream, static_cast<unsigned int>(link.includer));
                Binarize(stream, static_cast<unsigned int>(link.includee));
            }
        }

        // -----------------------------------------------------------------------------------------------------------
        void BinarizeIncludes(FILE* stream, const CompileScore::TFileIndices& links)
        {
            Binarize(stream, static_cast<unsigned int>(links.size()));
            for (int index : links)
            {
                Binarize(stream, static_cast<unsigned int>(index));
            }
        }

        // -----------------------------------------------------------------------------------------------------------
        void BinarizeMain(FILE* stream, const CompileScore::Result& result)
        {
            BinarizeString(stream, result.files.empty()? "" : result.files[0].name);
        }
    }

    //////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // Print
    //////////////////////////////////////////////////////////////////////////////////////////////////////////////

    namespace PrintUtils
    {
        // -----------------------------------------------------------------------------------------------------------
        const char* GetGlobalRequirementName(CompileScore::GlobalRequirementType::Enumeration input)
        {
            switch (input)
            {
            case CompileScore::GlobalRequirementType::MacroExpansion:     return "Macro Expansions";    
            case CompileScore::GlobalRequirementType::FreeFunctionCall:   return "Free Function Calls";
            case CompileScore::GlobalRequirementType::FreeVariable:       return "Free Variable";
            case CompileScore::GlobalRequirementType::EnumInstance:       return "Enum Instances";
            case CompileScore::GlobalRequirementType::EnumConstant:       return "Enum Constants";
            case CompileScore::GlobalRequirementType::ForwardDeclaration: return "Forward Declaration";
            case CompileScore::GlobalRequirementType::TypeDefinition:     return "Type Definition";
            default: return "???";
            }
        }

        // -----------------------------------------------------------------------------------------------------------
        const char* GetStructureSimpleRequirementName(CompileScore::StructureSimpleRequirementType::Enumeration input)
        {
            switch (input)
            {
            case CompileScore::StructureSimpleRequirementType::Instance:         return "Instances";
            case CompileScore::StructureSimpleRequirementType::Reference:        return "References";
            case CompileScore::StructureSimpleRequirementType::Allocation:       return "Allocation";
            case CompileScore::StructureSimpleRequirementType::Destruction:      return "Destruction";
            case CompileScore::StructureSimpleRequirementType::Inheritance:      return "Inheritances";
            case CompileScore::StructureSimpleRequirementType::MemberField:      return "Member Field";
            case CompileScore::StructureSimpleRequirementType::Cast:             return "Cast";
            case CompileScore::StructureSimpleRequirementType::FunctionArgument: return "Function Arguments";
            case CompileScore::StructureSimpleRequirementType::FunctionReturn:   return "Function Returns";
            default: return "???";
            }
        }

        // -----------------------------------------------------------------------------------------------------------
        const char* GetStructureNamedRequirementName(CompileScore::StructureNamedRequirementType::Enumeration input)
        {
            switch (input)
            {
            case CompileScore::StructureNamedRequirementType::MethodCall:  return "Method Call";
            case CompileScore::StructureNamedRequirementType::FieldAccess: return "Field Access";
            default: return "???";
            }
        }

        // -----------------------------------------------------------------------------------------------------------
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

        // -----------------------------------------------------------------------------------------------------------
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

        // -----------------------------------------------------------------------------------------------------------
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

        // -----------------------------------------------------------------------------------------------------------
        void Print(const CompileScore::Result& result, const CompileScore::File& file, int tab = 0)
        {
            Log("File %s:\n", file.name.empty() ? "?????" : file.name.c_str());

            if (CompileScore::IsFileEmpty(file))
            {
                Log("\tNo File Requirements Found!");
            }

            for (int i = 0; i < CompileScore::GlobalRequirementType::Count; ++i)
            {
                Print(file.global[i], GetGlobalRequirementName(CompileScore::GlobalRequirementType::Enumeration(i)), tab + 1);
            }

            for (const CompileScore::StructureRequirement& structure : file.structures)
            {
                Print(structure, tab + 1);
            }

            int numIncludesFound = 0;
            for (CompileScore::IncludeLink link : result.indirectIncludes)
            {
                if (link.includer == file.exportIndex)
                {
                    if (numIncludesFound == 0)
                    {
                        Log("\tNeeded Includes:\n");
                    }

                    ++numIncludesFound;

                    Log("\t\t%s\n", result.finalFiles[link.includee]->name.c_str());
                }
            }
        }
    }

    //////////////////////////////////////////////////////////////////////////////////////////////////////////////

    // -----------------------------------------------------------------------------------------------------------
    bool ToFile(const CompileScore::Result& result, const char* filename)
    {
        FILE* stream;
        const errno_t openResult = fopen_s(&stream, filename, "wb");
        if (openResult)
        {
            return false;
        }

        BinUtils::Binarize(stream, DATA_VERSION);

        BinUtils::BinarizeMain(stream, result);
        BinUtils::BinarizeFiles(stream, result.finalFiles);
        BinUtils::BinarizeIncludes(stream, result.directIncludes);
        BinUtils::BinarizeIncludes(stream, result.indirectIncludes);

        fclose(stream);
        return true;
    }

    // -----------------------------------------------------------------------------------------------------------
    void ToPrint(const CompileScore::Result& result)
    {
        Log("Dependency Requirements found: \n");

        for( const CompileScore::File* file : result.finalFiles )
        {
            PrintUtils::Print(result, *file);
        }
    }

}