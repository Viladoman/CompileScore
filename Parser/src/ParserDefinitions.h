#pragma once

#include <vector>
#include <string>

namespace clang
{
    class DefMacroDirective;
}

namespace CompileScore
{
    enum { kInvalidFileIndex = -1 };
    
    namespace GlobalRequirementType
    {
        enum Enumeration
        {
            MacroExpansion, 
            FreeFunctionCall, 
            FreeConstExprFunctionCall,
            FreeVariable,

            EnumInstance, 
            EnumConstant,

            ForwardDeclaration,
            TypeDefinition,

            Count
        };
    }

    namespace StructureSimpleRequirementType
    {
        enum Enumeration
        {
            Instance,
            Reference,
            Allocation,
            Destruction,
            Inheritance,
            MemberField,
            FunctionArgument,
            FunctionReturn,
            Cast,

            Count
        };
    }

    namespace StructureNamedRequirementType
    {
        enum Enumeration
        {
            MethodCall,
            FieldAccess,

            Count
        };
    }

    // ----------------------------------------------------------------------------------------------------------
    struct IncludeLink
    {
        IncludeLink(int _includer, int _includee) 
            : includer(_includer)
            , includee(_includee) 
        {}

        int includer;
        int includee;
    };

    using TIncludeLinks = std::vector<IncludeLink>;

    // ----------------------------------------------------------------------------------------------------------

    struct FileLocation
    {
        FileLocation(int r, int c) 
            : row(r)
            , column(c)
        {}

        int row; 
        int column; 
    };

    using TFileLocations = std::vector<FileLocation>;

    // ----------------------------------------------------------------------------------------------------------
    struct CodeRequirement
    {
        CodeRequirement(const void* ptr, const char* label, FileLocation definitonLoc) 
            : clangPtr(ptr)
            , name(label)
            , defLocation(definitonLoc) 
        {}

        const void* clangPtr; //pointer used to make sure we point to the same 

        std::string    name;
        FileLocation   defLocation;
        TFileLocations useLocations;
    };

    using TRequirements = std::vector<CodeRequirement>;

    // ----------------------------------------------------------------------------------------------------------
    struct StructureRequirement
    {
        StructureRequirement(const void* ptr, const char* label, FileLocation definitonLoc) 
            : clangPtr(ptr)
            , name(label)
            , defLocation(definitonLoc) 
        {}

        const void*    clangPtr; //pointer used to make sure we point to the same 

        std::string    name;
        FileLocation   defLocation;

        TFileLocations simpleRequirements[ StructureSimpleRequirementType::Count ];
        TRequirements  namedRequirements [ StructureNamedRequirementType::Count ];
    };

    using TStructureRequirements = std::vector<StructureRequirement>;

    // ----------------------------------------------------------------------------------------------------------
    struct File
    {
        File(const char* filename) 
            : name(filename)
            , mainIncludeeIndex(kInvalidFileIndex)
            , exportIndex(kInvalidFileIndex) 
        {}

        std::string name;
        int         mainIncludeeIndex; 
        int         exportIndex; // filled by the Finalize process

        TRequirements          global[GlobalRequirementType::Count];
        TStructureRequirements structures;

    };

    using TFiles       = std::vector<File>;
    using TFilePtrs    = std::vector<File*>;
    using TFileIndices = std::vector<int>;

    // ----------------------------------------------------------------------------------------------------------
    struct Result
    {
        //filled by parser
        TFiles          files;

        //filled by processor
        TFilePtrs       finalFiles;
        TFileIndices    directIncludes;
        TIncludeLinks   indirectIncludes;
    };


}