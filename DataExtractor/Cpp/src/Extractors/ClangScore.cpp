#include "ClangScore.h"

#include "../Common/CommandLine.h"
#include "../Common/Context.h"
#include "../Common/DirectoryUtils.h"
#include "../Common/JsonParser.h"
#include "../Common/IOStream.h"
#include "../Common/ScoreDefinitions.h"
#include "../Common/ScoreProcessor.h"
#include "../Common/StringUtils.h"

#include "../fastl/algorithm.h"

namespace Clang 
{ 
	constexpr int FAILURE = -1;
	constexpr int SUCCESS = 0;

	namespace Utils
	{ 
		//------------------------------------------------------------------------------------------
		constexpr size_t StrLength(const char* str)
		{
			size_t ret = 0;
			for(;*str!='\0';++str,++ret){}
			return ret;
		}

		namespace Impl
		{ 
			//------------------------------------------------------------------------------------------
			bool EqualStr(const char* a, const char* b, const size_t length)
			{
				//this assumes strlen(a) >= length && strlen(b) >= length
				for (size_t i = 0; i<length ;++i)
				{
					if (a[i] != b[i]) return false;
				}
				return true;
			}

		}
		//------------------------------------------------------------------------------------------
		bool EqualTokens(const Json::Token a, const Json::Token b)
		{
			return a.length == b.length && Impl::EqualStr(a.str,b.str,a.length);
		}

		//------------------------------------------------------------------------------------------
		bool StartsWithToken(const Json::Token a, const Json::Token prefix)
		{
			return a.length >= prefix.length && Impl::EqualStr(a.str,prefix.str,prefix.length);
		}

		//------------------------------------------------------------------------------------------
		constexpr Json::Token CreateLiteralToken(const char* str)
		{ 
			return Json::Token(str,StrLength(str),Json::Token::Type::String);
		}

		//------------------------------------------------------------------------------------------
		U32 TokenToU32(const Json::Token token)
		{
			U32 ret = 0u;
			const char* buff = token.str;
			if (token.type == Json::Token::Type::Number && *buff!= '-')
			{
				for(size_t lenCount = 0;*buff != '.' && lenCount < token.length; ++lenCount,++buff)
				{
					ret=((*buff)-'0')+ret*10u;
				}
			} 
			return ret;
		}
	}

	// -----------------------------------------------------------------------------------------------------------
	CompileCategory ToCompileCategory(const Json::Token token)
	{ 
		//Accepted Tags
		constexpr static Json::Token tagInclude               = Utils::CreateLiteralToken("Source");
		constexpr static Json::Token tagParseClass            = Utils::CreateLiteralToken("ParseClass");
		constexpr static Json::Token tagParseTemplate         = Utils::CreateLiteralToken("ParseTemplate");
		constexpr static Json::Token tagInstantiateClass      = Utils::CreateLiteralToken("InstantiateClass");
		constexpr static Json::Token tagInstantiateFunction   = Utils::CreateLiteralToken("InstantiateFunction");
		constexpr static Json::Token tagCodeGenFunction       = Utils::CreateLiteralToken("CodeGen Function");
		constexpr static Json::Token tagPendingInstantiations = Utils::CreateLiteralToken("PerformPendingInstantiations");
		constexpr static Json::Token tagOptModule             = Utils::CreateLiteralToken("OptModule");
		constexpr static Json::Token tagOptFunction           = Utils::CreateLiteralToken("OptFunction");
		constexpr static Json::Token tagFrontend              = Utils::CreateLiteralToken("Frontend");
		constexpr static Json::Token tagBackend               = Utils::CreateLiteralToken("Backend");
		constexpr static Json::Token tagTotal                 = Utils::CreateLiteralToken("ExecuteCompiler");

		constexpr static Json::Token tagRunPass               = Utils::CreateLiteralToken("RunPass");
		constexpr static Json::Token tagCodeGenPasses         = Utils::CreateLiteralToken("CodeGenPasses");
		constexpr static Json::Token tagDebugType             = Utils::CreateLiteralToken("DebugType");
		constexpr static Json::Token tagDebugGlobalVariable   = Utils::CreateLiteralToken("DebugGlobalVariable");
		constexpr static Json::Token tagPerFunctionPasses     = Utils::CreateLiteralToken("PerFunctionPasses");
		constexpr static Json::Token tagPerModulePasses       = Utils::CreateLiteralToken("PerModulePasses");

		//Invalid Tags
		constexpr static Json::Token tagInvalidA              = Utils::CreateLiteralToken("process_name");
		constexpr static Json::Token tagInvalidB              = Utils::CreateLiteralToken("thread_name");
		constexpr static Json::Token prefixInvalidA           = Utils::CreateLiteralToken("Total");

		if (token.type == Json::Token::Type::String)
		{ 
			if (Utils::EqualTokens(token,tagInclude))               return CompileCategory::Include;
			if (Utils::EqualTokens(token,tagParseClass))            return CompileCategory::ParseClass; 
			if (Utils::EqualTokens(token,tagParseTemplate))         return CompileCategory::ParseTemplate; 
			if (Utils::EqualTokens(token,tagInstantiateClass))      return CompileCategory::InstantiateClass; 
			if (Utils::EqualTokens(token,tagInstantiateFunction))   return CompileCategory::InstantiateFunction; 
			if (Utils::EqualTokens(token,tagCodeGenFunction))       return CompileCategory::CodeGenFunction; 
			if (Utils::EqualTokens(token,tagPendingInstantiations)) return CompileCategory::PendingInstantiations; 
			if (Utils::EqualTokens(token,tagOptFunction))           return CompileCategory::OptimizeFunction; 
			if (Utils::EqualTokens(token,tagOptModule))             return CompileCategory::OptimizeModule; 
			if (Utils::EqualTokens(token,tagRunPass))               return CompileCategory::RunPass; 
			if (Utils::EqualTokens(token,tagCodeGenPasses))         return CompileCategory::CodeGenPasses; 
			if (Utils::EqualTokens(token,tagPerFunctionPasses))     return CompileCategory::PerFunctionPasses; 
			if (Utils::EqualTokens(token,tagPerModulePasses))       return CompileCategory::PerModulePasses; 
			if (Utils::EqualTokens(token,tagDebugType))             return CompileCategory::DebugType; 
			if (Utils::EqualTokens(token,tagDebugGlobalVariable))   return CompileCategory::DebugGlobalVariable; 
			if (Utils::EqualTokens(token,tagFrontend))              return CompileCategory::FrontEnd; 
			if (Utils::EqualTokens(token,tagBackend))               return CompileCategory::BackEnd; 
			if (Utils::EqualTokens(token,tagTotal))                 return CompileCategory::ExecuteCompiler; 
			
			if (Utils::EqualTokens(token,tagInvalidA) || 
				Utils::EqualTokens(token,tagInvalidB) || 
				Utils::StartsWithToken(token,prefixInvalidA)) 
			{ 
				return CompileCategory::Invalid; 
			}

			return CompileCategory::Other;
		}

		return CompileCategory::Invalid;
	}

	// -----------------------------------------------------------------------------------------------------------
	bool ProcessEvent(CompileEvent& output, Json::Reader& reader)
	{ 
		constexpr static Json::Token tagName     = Utils::CreateLiteralToken("name");
		constexpr static Json::Token tagStart    = Utils::CreateLiteralToken("ts");
		constexpr static Json::Token tagDuration = Utils::CreateLiteralToken("dur");
		constexpr static Json::Token tagArgs     = Utils::CreateLiteralToken("args");
		constexpr static Json::Token tagDetail   = Utils::CreateLiteralToken("detail");

		//Open Object token already parsed by the caller
		Json::Token token; 
		while (reader.NextToken(token) && token.type != Json::Token::Type::ObjectClose)
		{ 			
			if (Utils::EqualTokens(token,tagName))
			{
				if (!reader.NextToken(token) || token.type != Json::Token::Type::String) return false;
				output.category = ToCompileCategory(token);
				if (output.name.empty()) output.name = fastl::string(token.str,token.length);
			}
			else if (Utils::EqualTokens(token,tagStart))
			{
				if (!reader.NextToken(token) || token.type != Json::Token::Type::Number) return false; 
				output.start = Utils::TokenToU32(token);
			}
			else if (Utils::EqualTokens(token,tagDuration))
			{
				if (!reader.NextToken(token) || token.type != Json::Token::Type::Number) return false; 
				output.duration = Utils::TokenToU32(token);
			}
			else if (Utils::EqualTokens(token,tagArgs))
			{
				//Check the internal object
				if (!reader.NextToken(token) || token.type != Json::Token::Type::ObjectOpen) return false; 
				while(reader.NextToken(token) && token.type != Json::Token::Type::ObjectClose)
				{ 
					if (Utils::EqualTokens(token,tagDetail))
					{ 
						if (!reader.NextToken(token) || token.type != Json::Token::Type::String) return false;  
						output.name = fastl::string(token.str,token.length);
					}
					else 
					{ 
						reader.SkipObject();
					}
				}
			}
			else 
			{
				//unknown or uninteresting field - skip it
				reader.SkipObject();
			}
		}

		//process filenames
		if (output.category == CompileCategory::Include || output.category == CompileCategory::OptimizeModule)
		{ 
			StringUtils::ToPathBaseName(output.name); 
		}
	
		//TODO ~ ramonv ~ demangle optimize function names

		//all names should be lowercase to improve filter performance later
		StringUtils::ToLower(output.name);

		return true;
	}

	// -----------------------------------------------------------------------------------------------------------
	void AddEventToTimeline(ScoreTimeline& timeline, const CompileEvent& compileEvent)
	{ 
		//inject in a sorted position
		TCompileEvents& events = timeline.tracks[0]; 
		TCompileEvents::iterator found = fastl::lower_bound(events.begin(),events.end(),compileEvent,
			[=](const CompileEvent& input, const CompileEvent& value)
			{ 
				return (value.start == input.start)? value.duration <= input.duration : value.start >= input.start; 
			});
		TCompileEvents::iterator elem = events.emplace(found,compileEvent);    
	}

	// -----------------------------------------------------------------------------------------------------------
	void NormalizeStartTimes(ScoreTimeline& timeline)
	{ 
		TCompileEvents& events = timeline.tracks[0]; 

		if (!events.empty())
		{
			const U32 offset = events[0].start;
			for (CompileEvent& entry : events)
			{ 
				entry.start -= offset;
			}
		}
	}

	// -----------------------------------------------------------------------------------------------------------
	bool ProcessFile(ScoreData& scoreData, const char* path, const char* content)
	{ 
		constexpr Json::Token literalTraceEvents = Utils::CreateLiteralToken("traceEvents");

		ScoreTimeline timeline;
		timeline.tracks.emplace_back(); //we only use one events track in Clang

		timeline.name = path;
		StringUtils::ToPathBaseName(timeline.name); //remove the path
		StringUtils::RemoveExtension(timeline.name); //remove the .json
		StringUtils::ToLower(timeline.name);

		//Parse JSON
		Json::Reader reader(content);
		Json::Token token; 
		
		//check this is a trackEvents or a different json file
		if (!reader.NextToken(token) || token.type != Json::Token::Type::ObjectOpen) return false;
		if (!reader.NextToken(token)) return false;
		
		// We are assuming that the json node starts with the traceEvents
		if (!Utils::EqualTokens(token,literalTraceEvents)) return false;

		if (!reader.NextToken(token) || token.type != Json::Token::Type::ArrayOpen)  return false;
		
		while (reader.NextToken(token) && token.type != Json::Token::Type::ArrayClose)
		{ 
			CompileEvent compileEvent; 
			if (token.type != Json::Token::Type::ObjectOpen || !ProcessEvent(compileEvent,reader)) return false;
			
			if (compileEvent.category != CompileCategory::Invalid)
			{ 
				AddEventToTimeline(timeline,compileEvent);
			}
		}

		//From here we can ignore the rest of the file

		NormalizeStartTimes(timeline);

		CompileScore::ProcessTimeline(scoreData,timeline);
		return true;
	}

	//////////////////////////////////////////////////////////////////////////////////////////////////////////////

	// -----------------------------------------------------------------------------------------------------------
	int StopRecordingTrace(const ExportParams& params)
	{ 
		//TODO ~ ramonv ~ to be implemented
		LOG_ERROR("Clang output trace not implemented!");

		//Open input folder and search for the timestamp placed by the start function
		//Parse directory and compare against the timestamp
		//store all files as a trace and save it on the output file

		return FAILURE;
	}

	// -----------------------------------------------------------------------------------------------------------
	int StopRecordingGenerate(const ExportParams& params)
	{ 
		//TODO ~ ramonv ~ to be implemented
		LOG_ERROR("Clang output trace not implemented!");

		//Open input folder and search for the timestamp placed by the start function
		//Parse directory and compare against the timestamp
		//Process all files

		return FAILURE; 
	}

	// -----------------------------------------------------------------------------------------------------------
	int GenerateScoreTrace(const ExportParams& params)
	{
		//TODO ~ ramonv ~ to be implemented
		LOG_ERROR("Clang output trace not implemented!");

		//Process all files found in the input trace 

		return FAILURE; 
	}

	// -----------------------------------------------------------------------------------------------------------
	int GenerateScoreDirectory(const ExportParams& params)
	{ 
		ScoreData scoreData;

		LOG_PROGRESS("Scanning dir: %s",params.input);

		Context::Scoped<IO::Binarizer> binarizer(params.output,params.timelinePacking);

		size_t filesFound = 0u;
		IO::DirectoryScanner dirScan(params.input,".json");
		while (const char* path = dirScan.SeekNext())
		{ 
			LOG_INFO("Found file %s", path);
			if (IO::FileBuffer fileBuffer = IO::ReadFile(path))
			{ 
				ProcessFile(scoreData,path,fileBuffer);
				IO::DestroyBuffer(fileBuffer);
			}
			else
			{ 
				LOG_ERROR("Invalid file buffer for %s", path);
			}

			IO::Log(IO::Verbosity::Progress,"Parsing... %u files\r",++filesFound);
		}

		binarizer.Get().Binarize(scoreData);

		return SUCCESS;
	}

	//////////////////////////////////////////////////////////////////////////////////////////////////////////////

	// -----------------------------------------------------------------------------------------------------------
	int Extractor::StartRecording(const ExportParams& params)
	{
		//TODO ~ ramonv ~ to be implemented
		LOG_ERROR("Clang output trace not implemented!");

		//Got to the input folder and store a timestamp for comparison later

		return FAILURE;
	}

	// -----------------------------------------------------------------------------------------------------------
	int Extractor::CancelRecording(const ExportParams& params)
	{
		//TODO ~ ramonv ~ to be implemented
		LOG_ERROR("Clang output trace not implemented!");

		//Got to the input folder and store a timestamp for comparison later

		return FAILURE;
	}

	// -----------------------------------------------------------------------------------------------------------
	int Extractor::StopRecording(const ExportParams& params)
	{
		//Check extension
		if (params.output == nullptr)
		{ 
			LOG_ERROR("No output file provided.");
			return FAILURE;
		}

		if (IO::IsExtension(params.output,".scor"))
		{ 
			return StopRecordingGenerate(params);
		}

		if (IO::IsExtension(params.output,".ctl"))
		{ 
			return StopRecordingTrace(params);
		}

		LOG_ERROR("Unknown output file extension provided. The Clang generator only knows how to generate .scor or .ctl files.");
		return FAILURE;
	}

	// -----------------------------------------------------------------------------------------------------------
	int Extractor::GenerateScore(const ExportParams& params)
	{ 
		if (params.input == nullptr)
		{ 
			LOG_ERROR("No input path provided.");
			return FAILURE;
		}

		if (IO::IsDirectory(params.input))
		{ 
			return GenerateScoreDirectory(params);
		}

		if (IO::IsExtension(params.input,".ctl"))
		{ 
			return GenerateScoreTrace(params);
		}

		if (!IO::Exists(params.input))
		{
			LOG_ERROR("Could not find input: %s",params.input);
			return FAILURE;
		}

		LOG_ERROR("Input file is not a folder or a .ctl file.");
	    return FAILURE;
	} 
}