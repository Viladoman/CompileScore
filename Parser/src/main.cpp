
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

#include <vector>
#include <string>
#include <unordered_map>

#pragma optimize("",off) //DO NOT SUBMIT

namespace CompileScore
{
    // ----------------------------------------------------------------------------------------------------------
    namespace Requirement
    {
        //requirement and nature ( enum / CXXrecord... ) as different properties

        //make requirement per categroy: instance .. function calls... enum usage 

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
            FunctionCall,


            EnumUse, //Also variable use, externs... static vars
            MemberUse,


            MacroExpansion,
            //MacroExpansion

            //TODO
            // check how to deal with using and typedef

            Count,
        };
    }
   
    //struct Dependency
    //{
    //    //Type Declaration pointer 
    //    //Typedecl line/col 
    //
    //    //vector of occurences
    //    //occurence line/col
    //};
    //
    //struct Dependencies
    //{
    //    void AddDependency()
    //    {
    //        //TODO ~ ramonv ~ give type and origin instance location 
    //        //Maybe here I should just store name, line, col 
    //
    //        
    //
    //        //clang::NamedDecl* decl;
    //        //decl->getQualifiedNameAsString();
    //
    //    }
    //
    //    std::vector<Dependency> m_entries;
    //};

    // ----------------------------------------------------------------------------------------------------------
    struct File
    {
        File(const char* filename = ""):name(filename){}

        std::string name;

        //void AddTypeInstanceDependency()
        //{
            // have multiple arrays for these 
        //}

        //TODO ~ Maybe this should be structed as 'Type', 'Function', 'Method'... and have the requirement inside 

        //Dependencies dependencies[Requirement::Count]; //Too generic and broad ( make it specific ) 
    };

    using TFiles = std::vector<File>;
    using TFilenameLookup = std::unordered_map<unsigned int, size_t>;

    // ----------------------------------------------------------------------------------------------------------
    class Result
    {
    public:
        enum { kOtherFileIndex = -1 };

        File& GetFile(size_t fileIndex)
        {
            return fileIndex < 0 ? otherFile : files[fileIndex];
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

        int GetFileIndex(const clang::FileID fileId, const char* filename)
        {
            const size_t nextIndex = files.size();
            std::pair<TFilenameLookup::iterator, bool> const& result = filenameLookup.insert(TFilenameLookup::value_type(fileId.getHashValue(), nextIndex));
            if (result.second)
            {
                files.emplace_back(filename);
            }
            return static_cast<int>(result.first->second);
        }

    private:
        TFilenameLookup filenameLookup;
        TFiles          files;
        File            otherFile;
    };

    Result g_result;

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
            if ( IsDeclaredInMainFile( declaration->getLocation()) )
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
                    const int fileIndex = g_result.GetFileIndex(declaration->getLocation(), m_sourceManager);
                    File& dependentFile = g_result.GetFile(fileIndex);

                    //PLACEHOLDER for collection

                    //Dependency dependency = dependentFile.dependencies[requirement];
                    const clang::PresumedLoc startLocation = m_sourceManager.getPresumedLoc(expr->getLocation());
                    const unsigned int startLine = startLocation.getLine();
                    const unsigned int startCol = startLocation.getColumn();
                    printf("Found: Needed file %s for enum constant %s (%d) in %d:%d\n", dependentFile.name.c_str(), declaration->getQualifiedNameAsString().c_str(), Requirement::EnumUse, startLine, startCol);
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
                    const int fileIndex = g_result.GetFileIndex(declaration->getLocation(), m_sourceManager);
                    File& dependentFile = g_result.GetFile(fileIndex);

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
                    const int fileIndex = g_result.GetFileIndex(callee->getLocation(), m_sourceManager);
                    File& dependentFile = g_result.GetFile(fileIndex);

                    //PLACEHOLDER for collection

                    //Dependency dependency = dependentFile.dependencies[requirement];
                    const clang::PresumedLoc startLocation = m_sourceManager.getPresumedLoc(expr->getBeginLoc());
                    const unsigned int startLine = startLocation.getLine();
                    const unsigned int startCol = startLocation.getColumn();
                    printf("Found: Needed file %s for function %s (%d) in %d:%d\n", dependentFile.name.c_str(), callee->getQualifiedNameAsString().c_str(), Requirement::FunctionCall, startLine, startCol);
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
                AddInstance( finalType->getAsTagDecl(), location, requirement );
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
                //TODO ~ ramonv ~ check that the pointer is forward declared or not
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

        void AddInstance(clang::TagDecl* declaration, const clang::SourceLocation& location, Requirement::Enumeration requirement )
        {
            //this can be EnumDecl or CXXRecordDecl
            //TODO~ ramonv go through the declaration and check if this is a templated type

            if (declaration && !IsDeclaredInMainFile(declaration->getLocation()))
            {
                const int fileIndex = g_result.GetFileIndex(declaration->getLocation(), m_sourceManager);
                File& dependentFile = g_result.GetFile(fileIndex);

                //PLACEHOLDER for collection

                //Dependency dependency = dependentFile.dependencies[requirement];
                const clang::PresumedLoc startLocation = m_sourceManager.getPresumedLoc(location);
                const unsigned int startLine = startLocation.getLine();
                const unsigned int startCol = startLocation.getColumn();
                printf("Found: Needed file %s for instance of %s (%d) in %d:%d\n", dependentFile.name.c_str(), declaration->getQualifiedNameAsString().c_str(), requirement, startLine, startCol);
            }
        }

    private:
        const clang::ASTContext&    m_context;
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
                auto newFilename = m_sourceManager.getNonBuiltinFilenameForID(FID);
                auto prevFilename = m_sourceManager.getNonBuiltinFilenameForID(PrevFID);
                if ( newFilename.has_value() && prevFilename.has_value() )
                {
                    m_mainFileId = m_mainFileId.isValid() ? m_mainFileId : FID;
                    printf("PREPROCESSOR: FILE %s iNCLUDES %s\n", prevFilename->data(), newFilename->data() );
                }
            }
        }

        virtual void MacroExpands(const clang::Token& MacroNameTok, const clang::MacroDefinition& MD, clang::SourceRange Range, const clang::MacroArgs* Args) override
        {
            const clang::PresumedLoc expLocation = m_sourceManager.getPresumedLoc(Range.getBegin());
            if ( m_mainFileId == expLocation.getFileID() )
            {
                const clang::PresumedLoc defLocation = m_sourceManager.getPresumedLoc(MD.getLocalDirective()->getLocation());
                printf("Found: Needed file %s for MACRO EXPANSION %s (%d) in %d:%d\n", defLocation.getFilename(), "???", Requirement::MacroExpansion, defLocation.getLine(), defLocation.getColumn());
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
            std::unique_ptr<PPIncludeTracer> find_includes_callback = std::make_unique <PPIncludeTracer> (ci.getSourceManager());
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

int main(int argc, const char **argv) {
  llvm::Expected<clang::tooling::CommonOptionsParser> optionsParser = clang::tooling::CommonOptionsParser::create(argc, argv, CommandLine::g_commandLineCategory);
  if (!optionsParser)
  {
      llvm::errs() << "Failed to create options parser: " << llvm::toString(optionsParser.takeError()) << "\n";
      return false;
  }

  clang::tooling::ClangTool tool(optionsParser->getCompilations(), optionsParser->getSourcePathList());
  tool.run(clang::tooling::newFrontendActionFactory<CompileScore::Action>().get());

  return 0;

}
