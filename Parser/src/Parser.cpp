
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

#include "ParserDefinitions.h"

#pragma optimize("",off) //DO NOT SUBMIT

namespace CompileScore
{
    //Remove once replaced
    namespace Requirement
    {
        enum Enumeration : unsigned char
        {
            Inheritance,
            MemberField,

            FunctionArgument,
            FunctionReturn,

            //Template Param
            //TemplateInstance

            //in code usage
            Instance,
            MethodCall,    //on ptr or reference

            MemberUse,

            //TODO
            // check how to deal with using and typedef

            Count,
        };
    }

    //Global Vars
    using TFilenameLookup = std::unordered_map<unsigned int, size_t>;
    TFilenameLookup g_filenameLookup;
    Result g_result;

    // ----------------------------------------------------------------------------------------------------------
    namespace Helpers
    {
        File& GetFile(size_t fileIndex)
        {
            return fileIndex < 0 ? g_result.otherFile : g_result.files[fileIndex];
        }

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

        int GetFileIndex(clang::SourceLocation location, const clang::SourceManager& sourceManager)
        {
            if (!location.isValid())
            {
                return kOtherFileIndex;
            }

            const clang::PresumedLoc startLocation = sourceManager.getPresumedLoc(location);
            const clang::FileID fileId = startLocation.getFileID();

            if (!startLocation.isValid() || !fileId.isValid())
            {
                return kOtherFileIndex;
            }

            return static_cast<int>(GetFileIndex(fileId, startLocation.getFilename()));
        }

        FileLocation CreateFileLocation(clang::SourceLocation location, const clang::SourceManager& sourceManager)
        {
            const clang::PresumedLoc presumedLocation = sourceManager.getPresumedLoc(location);
            return FileLocation(presumedLocation.getLine(), presumedLocation.getColumn());
        }

        void AddCodeRequirement(TRequirements& requirements, const void* clangPtr, const char* name, clang::SourceLocation defLocation, clang::SourceLocation useLocation, const clang::SourceManager& sourceManager)
        {
            for (CodeRequirement& entry : requirements)
            {
                if (entry.clangPtr == clangPtr)
                {
                    entry.useLocations.emplace_back(CreateFileLocation(useLocation, sourceManager));
                    return;
                }
            }

            requirements.emplace_back(clangPtr, name, CreateFileLocation(defLocation,sourceManager) );
            requirements.back().useLocations.emplace_back(CreateFileLocation(useLocation, sourceManager));
        }

        void AddIncludeLink(const clang::FileID includer, const clang::FileID includee, const clang::SourceManager& sourceManager)
        {
            if (!includer.isValid() || !includee.isValid())
            {
                return;
            }

            std::optional<clang::StringRef> includerFileName = sourceManager.getNonBuiltinFilenameForID(includer);
            std::optional<clang::StringRef> includeeFileName = sourceManager.getNonBuiltinFilenameForID(includee);

            if (!includerFileName.has_value() || !includeeFileName.has_value())
            {
                return;
            }

            const int includerIndex = GetFileIndex(includer, includerFileName->data());
            const int includeeIndex = GetFileIndex(includee, includeeFileName->data());

            if (includerIndex < 0 || includeeIndex < 0)
            {
                return;
            }

            g_result.links.emplace_back(includerIndex, includeeIndex);
        }
    }

    // ----------------------------------------------------------------------------------------------------------

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
            if (IsDeclaredInMainFile(declaration->getLocation()))
            {
                ProcessStructDeclaration(declaration);
            }
            return true;
        }

        bool VisitVarDecl(clang::VarDecl* declaration)
        {
            if (IsDeclaredInMainFile(declaration->getLocation()))
            {
                const clang::ParmVarDecl* funcArg = clang::dyn_cast<clang::ParmVarDecl>(declaration);
                ProcessInstance(declaration->getType(), declaration->getSourceRange().getBegin(), funcArg ? Requirement::FunctionArgument : Requirement::Instance);
            }
            return true;
        }

        bool VisitFunctionDecl(clang::FunctionDecl* declaration)
        {
            if (IsDeclaredInMainFile(declaration->getLocation()))
            {
                ProcessInstance(declaration->getReturnType(), declaration->getReturnTypeSourceRange().getBegin(), Requirement::FunctionReturn);
            }
            return true;
        }

        bool VisitDeclRefExpr(clang::DeclRefExpr* expr)
        {
            if (IsDeclaredInMainFile(expr->getLocation()))
            {
                clang::EnumConstantDecl* declaration = clang::dyn_cast<clang::EnumConstantDecl>(expr->getDecl());
                if (declaration && !IsDeclaredInMainFile(declaration->getLocation()))
                {
                    File& file = Helpers::GetFile(Helpers::GetFileIndex(declaration->getLocation(), m_sourceManager));
                    Helpers::AddCodeRequirement(file.global[GlobalRequirementType::EnumConstant], declaration, declaration->getQualifiedNameAsString().c_str(), declaration->getLocation(), expr->getBeginLoc(), m_sourceManager);
                }
            }
            return true;
        }

        bool VisitMemberExpr(clang::MemberExpr* expr)
        {
            if (IsDeclaredInMainFile(expr->getBeginLoc()))
            {
                clang::ValueDecl* declaration = expr->getMemberDecl();

                if (clang::dyn_cast<clang::CXXMethodDecl>(declaration) != nullptr)
                {
                    //handled by CallExpr
                    return true;
                }

                if (declaration && !IsDeclaredInMainFile(declaration->getLocation()))
                {
                    const int fileIndex = Helpers::GetFileIndex(declaration->getLocation(), m_sourceManager);
                    File& dependentFile = Helpers::GetFile(fileIndex);

                    //PLACEHOLDER for collection

                    //Dependency dependency = dependentFile.dependencies[requirement];
                    const clang::PresumedLoc startLocation = m_sourceManager.getPresumedLoc(expr->getBeginLoc());
                    const unsigned int startLine = startLocation.getLine();
                    const unsigned int startCol = startLocation.getColumn();
                    printf("Found: Needed file %s for member field access %s (%d) in %d:%d\n", dependentFile.name.c_str(), declaration->getQualifiedNameAsString().c_str(), Requirement::MemberUse, startLine, startCol);
                }
            }
            return true;
        }

        bool VisitCallExpr(clang::CallExpr* expr)
        {
            if (IsDeclaredInMainFile(expr->getBeginLoc()))
            {
                //we can check if this is clang::CXXMemberCallExpr for methods

                clang::NamedDecl* callee = clang::dyn_cast<clang::NamedDecl>(expr->getCalleeDecl());
                if (callee && !IsDeclaredInMainFile(callee->getLocation()))
                {
                    File& file = Helpers::GetFile(Helpers::GetFileIndex(callee->getLocation(), m_sourceManager));
                    Helpers::AddCodeRequirement(file.global[GlobalRequirementType::FreeFunctionCall], callee, callee->getQualifiedNameAsString().c_str(), callee->getLocation(), expr->getBeginLoc(), m_sourceManager);
                }
            }
            return true;
        }

    private:

        void ProcessStructDeclaration(clang::CXXRecordDecl* declaration)
        {
            //Check for bases
            for (const clang::CXXBaseSpecifier& base : declaration->bases())
            {
                clang::QualType baseType;
                if (ProcessType(baseType, base.getType()))
                {
                    AddInstance(baseType->getAsCXXRecordDecl(), base.getBeginLoc(), Requirement::Inheritance);
                }
            }

            //check for fields
            unsigned int fieldNo = 0;
            for (clang::RecordDecl::field_iterator I = declaration->field_begin(), E = declaration->field_end(); I != E; ++I, ++fieldNo)
            {
                const clang::FieldDecl& field = **I;

                clang::QualType fieldType;
                if (ProcessType(fieldType, field.getType()))
                {
                    AddInstance(fieldType->getAsTagDecl(), field.getBeginLoc(), Requirement::MemberField);
                }
            }
        }

        void ProcessInstance(clang::QualType input, const clang::SourceLocation& location, Requirement::Enumeration requirement)
        {
            clang::QualType finalType;
            if (ProcessType(finalType, input))
            {
                AddInstance(finalType->getAsTagDecl(), location, requirement);
            }
        }

        bool ProcessType(clang::QualType& output, clang::QualType input)
        {
            if (input->isArrayType())
            {
                //Keep digging 
                return ProcessType(output, input->getAsArrayTypeUnsafe()->getElementType());
            }
            else if (input->isPointerType() || input->isReferenceType())
            {
                //TODO ~ ramovn ~ check if forward declared ( if so, where, if not... pointer to... )

                // Or pointer to inner class 
                return false;
            }

            output = input;
            return true;
        }

        bool IsDeclaredInMainFile(const clang::SourceLocation& location)
        {
            return m_sourceManager.getFileID(location) == m_mainFileId;
        }

        void AddInstance(clang::TagDecl* declaration, const clang::SourceLocation& location, Requirement::Enumeration requirement)
        {
            if (declaration && !IsDeclaredInMainFile(declaration->getLocation()))
            {
                //this can be EnumDecl or CXXRecordDecl
                //TODO~ ramonv go through the declaration and check if this is a templated type

                const int fileIndex = Helpers::GetFileIndex(declaration->getLocation(), m_sourceManager);
                File& dependentFile = Helpers::GetFile(fileIndex);

                //PLACEHOLDER for collection

                //Dependency dependency = dependentFile.dependencies[requirement];
                const clang::PresumedLoc startLocation = m_sourceManager.getPresumedLoc(location);
                const unsigned int startLine = startLocation.getLine();
                const unsigned int startCol = startLocation.getColumn();
                printf("Found: Needed file %s for instance of %s (%d) in %d:%d\n", dependentFile.name.c_str(), declaration->getQualifiedNameAsString().c_str(), requirement, startLine, startCol);
            }
        }

    private:
        const clang::ASTContext& m_context;
        const clang::SourceManager& m_sourceManager;
        const clang::FileID         m_mainFileId;
    };

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

            //TODO ~ recollect results from the visitor if needed
        }
    };

    class PPIncludeTracer : public clang::PPCallbacks
    {
    public:

        explicit PPIncludeTracer(const clang::SourceManager& sourceManager)
            : m_sourceManager(sourceManager)
        {}

    private:
        virtual void LexedFileChanged(clang::FileID FID, LexedFileChangeReason Reason, clang::SrcMgr::CharacteristicKind FileType, clang::FileID PrevFID, clang::SourceLocation Loc) override
        {
            if (FID.isValid() && Reason == LexedFileChangeReason::EnterFile)
            {
                m_mainFileId = m_mainFileId.isValid() ? m_mainFileId : FID;
                Helpers::AddIncludeLink(PrevFID, FID, m_sourceManager);
            }
        }

        virtual void MacroExpands(const clang::Token& MacroNameTok, const clang::MacroDefinition& MD, clang::SourceRange Range, const clang::MacroArgs* Args) override
        {
            clang::DefMacroDirective* macroDirective = MD.getLocalDirective();
            const clang::PresumedLoc expLocation = m_sourceManager.getPresumedLoc(Range.getBegin());
            if (macroDirective && m_mainFileId == expLocation.getFileID())
            {
                File& file = Helpers::GetFile(Helpers::GetFileIndex(macroDirective->getLocation(), m_sourceManager));
                const char* macroName = MacroNameTok.getIdentifierInfo()->getName().data();
                Helpers::AddCodeRequirement(file.global[GlobalRequirementType::MacroExpansion], macroDirective, macroName, macroDirective->getLocation(), Range.getBegin(), m_sourceManager);
            }
        }

    private:
        const clang::SourceManager& m_sourceManager;
        clang::FileID m_mainFileId;
    };

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

        void EndSourceFileAction()
        {
            clang::CompilerInstance& ci = getCompilerInstance();
            clang::Preprocessor& pp = ci.getPreprocessor();
            PPIncludeTracer* includeTracer = static_cast<PPIncludeTracer*>(pp.getPPCallbacks());

            // do whatever you want with the callback now
            (void)includeTracer;
        }
    };
}

namespace CommandLine
{
    //group
    llvm::cl::OptionCategory g_commandLineCategory("CompileScore Parser Options");

    //commands
    //llvm::cl::opt<std::string>  g_outputFilename("output", llvm::cl::desc("Specify output filename"), llvm::cl::value_desc("filename"), llvm::cl::cat(g_commandLineCategory));
    //llvm::cl::opt<unsigned int> g_locationRow("locationRow", llvm::cl::desc("Specify input filename row to inspect"), llvm::cl::value_desc("number"), llvm::cl::cat(g_commandLineCategory));
    //llvm::cl::opt<unsigned int> g_locationCol("locationCol", llvm::cl::desc("Specify input filename column to inspect"), llvm::cl::value_desc("number"), llvm::cl::cat(g_commandLineCategory));

    //aliases
    //llvm::cl::alias g_shortOutputFilenameOption("o", llvm::cl::desc("Alias for -output"), llvm::cl::aliasopt(g_outputFilename));
    //llvm::cl::alias g_shortLocationRowOption("r", llvm::cl::desc("Alias for -locationRow"), llvm::cl::aliasopt(g_locationRow));
    //llvm::cl::alias g_shortLocationColOption("c", llvm::cl::desc("Alias for -locationCol"), llvm::cl::aliasopt(g_locationCol));


    // CommonOptionsParser declares HelpMessage with a description of the common
    // command-line options related to the compilation database and input files.
    // It's nice to have this help message in all tools.
    //static llvm::cl::extrahelp CommonHelp(clang::tooling::CommonOptionsParser::HelpMessage);

    // A help message for this specific tool can be added afterwards.
    //static llvm::cl::extrahelp MoreHelp("\nMore help text...\n");
}

namespace Parser
{
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

        //TODO ~ ramovn ~ process result and binarize it to the output

        return true;
    }
}
