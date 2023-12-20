
#pragma warning(push, 0)    

// Declares clang::SyntaxOnlyAction.
#include <clang/AST/RecursiveASTVisitor.h>
#include <clang/Frontend/CompilerInstance.h>
#include <clang/Frontend/FrontendActions.h>
#include <clang/Tooling/CommonOptionsParser.h>
#include <clang/Tooling/Tooling.h>
// Declares llvm::cl::extrahelp.
#include <llvm/Support/CommandLine.h>

#pragma warning(pop)    

#include <unordered_map>

#include "IO.h"
#include "ParserDefinitions.h"
#include "Processor.h"

#pragma optimize("",off) //TODO ~ Ramonv ~ remove 

//TODO List: 
// Typedef type presence ( typedef CustomType NewType )
// Typedef detection not working
// 
// = Gather invalid vardecl that it could not figure out
//
// Test std::shared_ptr, std::unique_ptr...
// Test new delete
// Test using, tempalted using and typedefs
// Test constexpr functions on arrays

namespace CompileScore
{
    //Global Vars
    using TFilenameLookup = std::unordered_map<unsigned int, size_t>;
    TFilenameLookup g_filenameLookup;
    Result g_result;

    //////////////////////////////////////////////////////////////////////////////////////////////////////////////
    namespace Helpers
    {
        // -----------------------------------------------------------------------------------------------------------
        int GetFileIndex(const clang::FileID fileId, const char* filename)
        {
            const size_t nextIndex = g_result.files.size();
            std::pair<TFilenameLookup::iterator, bool> const& result = g_filenameLookup.insert(TFilenameLookup::value_type(fileId.getHashValue(), nextIndex));
            if (result.second)
            {
                g_result.files.emplace_back(filename);
            }
            return static_cast<int>(result.first->second);
        }

        // -----------------------------------------------------------------------------------------------------------
        int GetFileIndex(clang::SourceLocation location, const clang::SourceManager& sourceManager)
        {
            if (!location.isValid())
            {
                return kInvalidFileIndex;
            }

            const clang::PresumedLoc startLocation = sourceManager.getPresumedLoc(location);
            const clang::FileID fileId = startLocation.getFileID();

            if (!startLocation.isValid() || !fileId.isValid())
            {
                return kInvalidFileIndex;
            }

            return static_cast<int>(GetFileIndex(fileId, startLocation.getFilename()));
        }

        // -----------------------------------------------------------------------------------------------------------
        File& GetFile(int fileIndex)
        {
            static File dummyFile( "dummy" );
            return fileIndex < 0 ? dummyFile : g_result.files[fileIndex];
        }

        // -----------------------------------------------------------------------------------------------------------
        FileLocation CreateFileLocation(clang::SourceLocation location, const clang::SourceManager& sourceManager)
        {
            const clang::PresumedLoc presumedLocation = sourceManager.getPresumedLoc(location);
            return FileLocation(presumedLocation.getLine(), presumedLocation.getColumn());
        }

        // -----------------------------------------------------------------------------------------------------------
        StructureRequirement& GetStructRequirement(const clang::RecordDecl* recordDecl, const clang::SourceManager& sourceManager)
        {
            const clang::NamedDecl* namedDecl = recordDecl;

            //Join all template instances into the same type
            const clang::ClassTemplateSpecializationDecl* templateInstance = clang::dyn_cast<clang::ClassTemplateSpecializationDecl>(recordDecl);
            if (templateInstance)
            {
                namedDecl = templateInstance->getSpecializedTemplate();
            }

            File& file = Helpers::GetFile(Helpers::GetFileIndex(namedDecl->getLocation(), sourceManager));
            for (StructureRequirement& structure : file.structures)
            {
                if (structure.clangPtr == namedDecl)
                {
                    return structure;
                }
            }

            file.structures.emplace_back(namedDecl, namedDecl->getQualifiedNameAsString().c_str(), CreateFileLocation(namedDecl->getLocation(), sourceManager));
            return file.structures.back();
        }

        // -----------------------------------------------------------------------------------------------------------
        CodeRequirement& GetCodeRequirement(TRequirements& requirements, const void* clangPtr, const char* name, clang::SourceLocation defLocation, const clang::SourceManager& sourceManager)
        {
            for (CodeRequirement& entry : requirements)
            {
                if (entry.clangPtr == clangPtr)
                {                    
                    return entry;
                }
            }

            requirements.emplace_back(clangPtr, name, CreateFileLocation(defLocation, sourceManager));
            return requirements.back();
        }

        // -----------------------------------------------------------------------------------------------------------
        void AddCodeRequirement(TRequirements& requirements, const void* clangPtr, const char* name, clang::SourceLocation defLocation, clang::SourceLocation useLocation, const clang::SourceManager& sourceManager)
        {
            CodeRequirement& requirement = GetCodeRequirement(requirements, clangPtr, name, defLocation, sourceManager);
            requirement.useLocations.emplace_back(CreateFileLocation(useLocation, sourceManager));
        }
    }

    /////////////////////////////////////////////////////////////////////////////////////////////////////////////

    class CollectSymbolUsage : public clang::RecursiveASTVisitor<CollectSymbolUsage>
    {
    public:
        CollectSymbolUsage(clang::ASTContext& context)
            : m_context(context)
            , m_sourceManager(context.getSourceManager())
            , m_mainFileId(context.getSourceManager().getMainFileID())
        {}

        bool VisitCXXRecordDecl(clang::CXXRecordDecl* declaration)
        {
            if (!IsDeclaredInMainFile(declaration->getLocation()))
                return true;
            
            if (!declaration->isCompleteDefinition())
                return true;

            ProcessStructDeclaration(declaration);
            return true;
        }

        bool VisitVarDecl(clang::VarDecl* declaration)
        {
            if (!IsDeclaredInMainFile(declaration->getLocation()))
                return true;

            const clang::ParmVarDecl* funcArg = clang::dyn_cast<clang::ParmVarDecl>(declaration);
            RefineType(declaration->getType(), declaration->getSourceRange().getBegin(), funcArg ? StructureSimpleRequirementType::FunctionArgument : StructureSimpleRequirementType::Instance);
            return true;
        }

        bool VisitFunctionDecl(clang::FunctionDecl* declaration)
        {
            if (!IsDeclaredInMainFile(declaration->getLocation()))
                return true;
            
            RefineType(declaration->getReturnType(), declaration->getReturnTypeSourceRange().getBegin(), StructureSimpleRequirementType::FunctionReturn);
            return true;
        }

        bool VisitDeclRefExpr(clang::DeclRefExpr* expr)
        {
            if (!IsDeclaredInMainFile(expr->getLocation()))
                return true;
 
            clang::ValueDecl* valueDecl = expr->getDecl();
            if (!valueDecl || IsDeclaredInMainFile(valueDecl->getLocation()))
                return true;

            //Check for enum constant
            if (clang::EnumConstantDecl* enumConstantDecl = clang::dyn_cast<clang::EnumConstantDecl>(valueDecl))
            {
                File& file = Helpers::GetFile(Helpers::GetFileIndex(enumConstantDecl->getLocation(), m_sourceManager));
                Helpers::AddCodeRequirement(file.global[GlobalRequirementType::EnumConstant], enumConstantDecl, enumConstantDecl->getQualifiedNameAsString().c_str(), enumConstantDecl->getLocation(), expr->getBeginLoc(), m_sourceManager);
            }
            else if (clang::VarDecl* varDecl = clang::dyn_cast<clang::VarDecl>(valueDecl))
            {
                //check for global variable or similar
                File& file = Helpers::GetFile(Helpers::GetFileIndex(varDecl->getLocation(), m_sourceManager));
                Helpers::AddCodeRequirement(file.global[GlobalRequirementType::FreeVariable], varDecl, varDecl->getQualifiedNameAsString().c_str(), varDecl->getLocation(), expr->getBeginLoc(), m_sourceManager);
            }

            return true;
        }

        bool VisitCXXNewExpr(clang::CXXNewExpr* expr)
        {
            if (!IsDeclaredInMainFile(expr->getBeginLoc()))
                return true;
            
            if (clang::CXXRecordDecl* declaration = expr->getAllocatedType()->getAsCXXRecordDecl())
            {
                StructureRequirement& structure = Helpers::GetStructRequirement(declaration, m_sourceManager);
                structure.simpleRequirements[StructureSimpleRequirementType::Allocation].emplace_back(Helpers::CreateFileLocation(expr->getBeginLoc(),m_sourceManager));
            }

            return true;
        }

        bool VisitMemberExpr(clang::MemberExpr* expr)
        {
            if (!IsDeclaredInMainFile(expr->getBeginLoc()))
                return true;
            
            clang::ValueDecl* declaration = expr->getMemberDecl();
            if (!declaration || IsDeclaredInMainFile(declaration->getLocation()))
                return true;

            const clang::RecordDecl* recordDecl = nullptr;
            StructureNamedRequirementType::Enumeration requirementType = StructureNamedRequirementType::Count;
            if (const clang::CXXMethodDecl* methodDecl = clang::dyn_cast<clang::CXXMethodDecl>(declaration))
            {                        
                recordDecl = methodDecl->getParent();
                requirementType = StructureNamedRequirementType::MethodCall;
            }
            else if (const clang::FieldDecl* fieldDecl = clang::dyn_cast<clang::FieldDecl>(declaration))
            {
                recordDecl = fieldDecl->getParent();
                requirementType = StructureNamedRequirementType::FieldAccess;
            }

            if (recordDecl && requirementType < StructureNamedRequirementType::Count)
            {
                StructureRequirement& structure = Helpers::GetStructRequirement(recordDecl, m_sourceManager);
                Helpers::AddCodeRequirement(structure.namedRequirements[requirementType], declaration, declaration->getQualifiedNameAsString().c_str(), declaration->getLocation(), expr->getBeginLoc(), m_sourceManager);
            }

            return true;
        }

        bool VisitCallExpr(clang::CallExpr* expr)
        {
            if (!IsDeclaredInMainFile(expr->getBeginLoc()))
                return true;
            
            clang::NamedDecl* callee = clang::dyn_cast<clang::NamedDecl>(expr->getCalleeDecl());
            if (!callee || IsDeclaredInMainFile(callee->getLocation()))
                return true;

            //CXXMethodDecl handled by MemberExpr visitor ( avoid duplicate processing )
            if (clang::dyn_cast<clang::CXXMethodDecl>(callee) != nullptr)
                return true;

            File& file = Helpers::GetFile(Helpers::GetFileIndex(callee->getLocation(), m_sourceManager));
            Helpers::AddCodeRequirement(file.global[GlobalRequirementType::FreeFunctionCall], callee, callee->getQualifiedNameAsString().c_str(), callee->getLocation(), expr->getBeginLoc(), m_sourceManager);

            return true;
        }

    private:

        bool IsDeclaredInMainFile(const clang::SourceLocation& location)
        {
            return m_sourceManager.getFileID(location) == m_mainFileId;
        }

        void ProcessStructDeclaration(clang::CXXRecordDecl* declaration)
        {
            //Check for bases
            for (const clang::CXXBaseSpecifier& base : declaration->bases())
            {
                RefineType(base.getType(), base.getBeginLoc(), StructureSimpleRequirementType::Inheritance);
            }

            //check for fields
            unsigned int fieldNo = 0;
            for (clang::RecordDecl::field_iterator I = declaration->field_begin(), E = declaration->field_end(); I != E; ++I, ++fieldNo)
            {
                const clang::FieldDecl& field = **I;
                RefineType(field.getType(), field.getBeginLoc(), StructureSimpleRequirementType::MemberField);
            }
        }

        void RefineType(clang::QualType qualType, const clang::SourceLocation& location, StructureSimpleRequirementType::Enumeration requirement)
        {
            if (qualType->isArrayType())
            {
                //Keep digging 
                RefineType(qualType->getAsArrayTypeUnsafe()->getElementType(), location, requirement);
                return;
            }
            else if (qualType->isPointerType() || qualType->isReferenceType())
            {
                //doesn't matter the context, a pointer or reference will always be marked down as such
                RefineType(qualType->getPointeeType(), location, StructureSimpleRequirementType::Reference);
                return;
            }

            AddCleanType(qualType->getAsTagDecl(), location, requirement);
        }

        void AddCleanType(clang::TagDecl* declaration, const clang::SourceLocation& location, StructureSimpleRequirementType::Enumeration requirement)
        {
            if (!declaration || IsDeclaredInMainFile(declaration->getLocation()))
                return;

            //this is an incomplete type ( forward declaration )
            if (!declaration->isCompleteDefinition())
            {
                File& file = Helpers::GetFile(Helpers::GetFileIndex(declaration->getLocation(), m_sourceManager));
                Helpers::AddCodeRequirement(file.global[GlobalRequirementType::ForwardDeclaration], declaration, declaration->getQualifiedNameAsString().c_str(), declaration->getLocation(), location, m_sourceManager);
            }
            else if (const clang::TypedefNameDecl* typedefDecl = clang::dyn_cast<clang::TypedefNameDecl>(declaration))
            {
                //this is a typedef or using
                File& file = Helpers::GetFile(Helpers::GetFileIndex(declaration->getLocation(), m_sourceManager));
                Helpers::AddCodeRequirement(file.global[GlobalRequirementType::TypeDefinition], declaration, declaration->getQualifiedNameAsString().c_str(), declaration->getLocation(), location, m_sourceManager);

                //process the real type ( removing pointers, qualifiers and more typedefinitions )
                RefineType( typedefDecl->getUnderlyingType(), location, requirement);
            }
            else if (const clang::EnumDecl* enumDecl = clang::dyn_cast<clang::EnumDecl>(declaration))
            {
                //This is an enumeration type            
                File& file = Helpers::GetFile(Helpers::GetFileIndex(declaration->getLocation(), m_sourceManager));
                Helpers::AddCodeRequirement(file.global[GlobalRequirementType::EnumInstance], declaration, declaration->getQualifiedNameAsString().c_str(), declaration->getLocation(), location, m_sourceManager);
            }
            else if (const clang::CXXRecordDecl* recordDecl = clang::dyn_cast<clang::CXXRecordDecl>(declaration))
            {
                //This is a custom type
                StructureRequirement& structure = Helpers::GetStructRequirement(recordDecl, m_sourceManager);
                structure.simpleRequirements[requirement].emplace_back(Helpers::CreateFileLocation(location, m_sourceManager));
            }
        }

    private:
        const clang::ASTContext& m_context;
        const clang::SourceManager& m_sourceManager;
        const clang::FileID         m_mainFileId;
    };

    //////////////////////////////////////////////////////////////////////////////////////////////////////////////

    class Consumer : public clang::ASTConsumer
    {
    public:
        virtual void HandleTranslationUnit(clang::ASTContext& context) override
        {
            auto Decls = context.getTranslationUnitDecl()->decls();

            CollectSymbolUsage visitor(context);
            for (auto& Decl : Decls)
            {
                visitor.TraverseDecl(Decl);
            }
        }
    };

    //////////////////////////////////////////////////////////////////////////////////////////////////////////////

    class PPIncludeTracer : public clang::PPCallbacks
    {
    public:

        explicit PPIncludeTracer(const clang::SourceManager& sourceManager)
            : m_sourceManager(sourceManager)
            , m_currentMainIncludee(kInvalidFileIndex)
        {}

    private:
        virtual void LexedFileChanged(clang::FileID FID, LexedFileChangeReason Reason, clang::SrcMgr::CharacteristicKind FileType, clang::FileID PrevFID, clang::SourceLocation Loc) override
        {
            if (FID.isValid() && Reason == LexedFileChangeReason::EnterFile)
            {
                m_mainFileId = m_mainFileId.isValid() ? m_mainFileId : FID;

                const clang::FileID includer = PrevFID;
                const clang::FileID includee = FID;

                if (!includer.isValid())
                {
                    return;
                }

                std::optional<clang::StringRef> includerFileName = m_sourceManager.getNonBuiltinFilenameForID(includer);
                std::optional<clang::StringRef> includeeFileName = m_sourceManager.getNonBuiltinFilenameForID(includee);

                if (!includerFileName.has_value() || !includeeFileName.has_value())
                {
                    return;
                }

                const int includerIndex = Helpers::GetFileIndex(includer, includerFileName->data());
                const int includeeIndex = Helpers::GetFileIndex(includee, includeeFileName->data());

                if (includerIndex < 0 || includeeIndex < 0)
                {
                    return;
                }

                if (includerIndex == 0)
                {
                    //we just entered a new main includee of the main file
                    m_currentMainIncludee = includeeIndex;
                }
                
                g_result.files[includeeIndex].mainIncludeeIndex = m_currentMainIncludee;
            }
        }

        virtual void MacroExpands(const clang::Token& MacroNameTok, const clang::MacroDefinition& MD, clang::SourceRange Range, const clang::MacroArgs* Args) override
        {
            clang::DefMacroDirective* macroDirective = MD.getLocalDirective();
            const clang::PresumedLoc expLocation = m_sourceManager.getPresumedLoc(Range.getBegin());
            if (macroDirective && m_mainFileId == expLocation.getFileID())
            {
                File& file = Helpers::GetFile( Helpers::GetFileIndex( macroDirective->getLocation(), m_sourceManager ) );
                const char* macroName = MacroNameTok.getIdentifierInfo()->getName().data();
                Helpers::AddCodeRequirement(file.global[GlobalRequirementType::MacroExpansion], macroDirective, macroName, macroDirective->getLocation(), Range.getBegin(), m_sourceManager);
            }
        }

    private:
        const clang::SourceManager& m_sourceManager;
        clang::FileID m_mainFileId;
        int           m_currentMainIncludee;
    };

    //////////////////////////////////////////////////////////////////////////////////////////////////////////////

    class Action : public clang::SyntaxOnlyAction
    {
    public:
        using ASTConsumerPointer = std::unique_ptr<clang::ASTConsumer>;
        ASTConsumerPointer CreateASTConsumer(clang::CompilerInstance&, llvm::StringRef) override { return std::make_unique<Consumer>(); }

        //Preprocessor setup
        bool BeginSourceFileAction(clang::CompilerInstance& ci)
        {
            clang::Preprocessor& pp = ci.getPreprocessor();
            std::unique_ptr<PPIncludeTracer> find_includes_callback = std::make_unique <PPIncludeTracer>(ci.getSourceManager());
            pp.addPPCallbacks(std::move(find_includes_callback));

            return true;
        }
    };
}

//////////////////////////////////////////////////////////////////////////////////////////////////////////////

namespace CommandLine
{
    //group
    llvm::cl::OptionCategory g_commandLineCategory("CompileScore Parser Options");

    //commands
    llvm::cl::opt<std::string>  g_outputFilename("output", llvm::cl::desc("Specify output filename"),       llvm::cl::value_desc("filename"), llvm::cl::cat(g_commandLineCategory));
    llvm::cl::opt<bool>         g_printResults("print",    llvm::cl::desc("Prints the findings on stdout"), llvm::cl::value_desc("flag"),     llvm::cl::cat(g_commandLineCategory));

    //aliases
    llvm::cl::alias g_shortOutputFilenameOption("o", llvm::cl::desc("Alias for -output"), llvm::cl::aliasopt(g_outputFilename));

     // CommonOptionsParser declares HelpMessage with a description of the common
    // command-line options related to the compilation database and input files.
    // It's nice to have this help message in all tools.
    static llvm::cl::extrahelp CommonHelp(clang::tooling::CommonOptionsParser::HelpMessage);
}

//////////////////////////////////////////////////////////////////////////////////////////////////////////////

namespace Parser
{
    // -----------------------------------------------------------------------------------------------------------
    bool Parse(int argc, const char** argv)
    {
        llvm::Expected<clang::tooling::CommonOptionsParser> optionsParser = clang::tooling::CommonOptionsParser::create(argc, argv, CommandLine::g_commandLineCategory);
        if (!optionsParser)
        {
            llvm::errs() << "Failed to create options parser: " << llvm::toString(optionsParser.takeError()) << "\n";
            return false;
        }

        clang::tooling::ClangTool tool(optionsParser->getCompilations(), optionsParser->getSourcePathList());
        tool.run(clang::tooling::newFrontendActionFactory<CompileScore::Action>().get());
        
        CompileScore::Finalize(CompileScore::g_result);

        const char* outputFileName = CommandLine::g_outputFilename.size() == 0 ? "output.cspbin" : CommandLine::g_outputFilename.c_str();
        
        if (CommandLine::g_printResults)
        {
            IO::ToPrint(CompileScore::g_result);
        }
        
        return IO::ToFile(CompileScore::g_result, outputFileName);
    }
}
