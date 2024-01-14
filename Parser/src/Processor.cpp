#include "Processor.h"

#include "ParserDefinitions.h"

//#pragma optimize("",off)

namespace CompileScore
{
    // -----------------------------------------------------------------------------------------------------------
    bool IsFileEmpty(const File& file)
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

    // -----------------------------------------------------------------------------------------------------------
	void CreateFinalIndices(Result& result)
	{
        //Add files with requirements to the export list
        int finalIndex = 0;
        
        for (size_t i=0,sz=result.files.size();i<sz;++i)
        {
            File& file = result.files[i];

            if ( file.mainIncludeeIndex != i && IsFileEmpty(file) )
            {
                // Skip non direct empty includes
                continue;
            }

            result.finalFiles.emplace_back(&file);
            file.exportIndex = finalIndex++;
        }
	}

    // -----------------------------------------------------------------------------------------------------------
    void ProcessIncludeRequirements(Result& result)
    {
        //TODO ~ ramonv ~ also store orphaned includes ( already included ) 

        //Check from the exported files which ones included indirectly and add those requirements to the direct file
        for (size_t i = 0, sz = result.finalFiles.size(); i < sz; ++i)
        {
            File* file = result.finalFiles[i];
            if (file->mainIncludeeIndex < 0 && i != 0 ) 
            {
                //preinclude: ( not the main file but not in the include trees )
                result.preIncludes.emplace_back(file->exportIndex);
            }
            else
            {
                File* mainIncludee = &(result.files[file->mainIncludeeIndex]);
                if (file == mainIncludee)
                {
                    //direct include
                    result.directIncludes.emplace_back(mainIncludee->exportIndex);
                }
                else 
                {
                    //indirect include
                    result.indirectIncludes.emplace_back(mainIncludee->exportIndex, file->exportIndex);
                }
            }
        }
    }

    // -----------------------------------------------------------------------------------------------------------
	void Finalize(Result& result)
	{
		CreateFinalIndices(result);
        ProcessIncludeRequirements(result);

        //TODO ~ ramonv ~ finalize result ( normalize paths - removing ../ and ./ from them )
	}
}