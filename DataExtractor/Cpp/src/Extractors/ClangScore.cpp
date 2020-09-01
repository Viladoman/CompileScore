#include "ClangScore.h"

#include "../Common/CommandLine.h"
#include "../Common/DirectoryScanner.h"
#include "../Common/JsonParser.h"
#include "../Common/IOStream.h"
#include "../Common/ScoreDefinitions.h"
#include "../Common/ScoreProcessor.h"
#include "../Common/StringUtils.h"

#include "../fastl/algorithm.h"

namespace Clang 
{ 
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
		constexpr Json::Token tagInclude               = Utils::CreateLiteralToken("Source");
		constexpr Json::Token tagParseClass            = Utils::CreateLiteralToken("ParseClass");
		constexpr Json::Token tagParseTemplate         = Utils::CreateLiteralToken("ParseTemplate");
		constexpr Json::Token tagInstantiateClass      = Utils::CreateLiteralToken("InstantiateClass");
		constexpr Json::Token tagInstantiateFunction   = Utils::CreateLiteralToken("InstantiateFunction");
		constexpr Json::Token tagCodeGenFunction       = Utils::CreateLiteralToken("CodeGen Function");
		constexpr Json::Token tagPendingInstantiations = Utils::CreateLiteralToken("PerformPendingInstantiations");
		constexpr Json::Token tagOptModule             = Utils::CreateLiteralToken("OptModule");
		constexpr Json::Token tagOptFunction           = Utils::CreateLiteralToken("OptFunction");
		constexpr Json::Token tagRunPass               = Utils::CreateLiteralToken("RunPass");
		constexpr Json::Token tagFrontend              = Utils::CreateLiteralToken("Frontend");
		constexpr Json::Token tagBackend               = Utils::CreateLiteralToken("Backend");
		constexpr Json::Token tagTotal                 = Utils::CreateLiteralToken("ExecuteCompiler");

		//Invalid Tags
		constexpr Json::Token tagInvalidA              = Utils::CreateLiteralToken("process_name");
		constexpr Json::Token tagInvalidB              = Utils::CreateLiteralToken("thread_name");
		constexpr Json::Token prefixInvalidA           = Utils::CreateLiteralToken("Total");

		if (token.type == Json::Token::Type::String)
		{ 
			if (Utils::EqualTokens(token,tagInclude))               return CompileCategory::Include;
			if (Utils::EqualTokens(token,tagParseClass))            return CompileCategory::ParseClass; 
			if (Utils::EqualTokens(token,tagParseTemplate))         return CompileCategory::ParseTemplate; 
			if (Utils::EqualTokens(token,tagInstantiateClass))      return CompileCategory::InstantiateClass; 
			if (Utils::EqualTokens(token,tagInstantiateFunction))   return CompileCategory::InstantiateFunction; 
			if (Utils::EqualTokens(token,tagCodeGenFunction))       return CompileCategory::CodeGenFunction; 
			if (Utils::EqualTokens(token,tagPendingInstantiations)) return CompileCategory::PendingInstantiations; 
			if (Utils::EqualTokens(token,tagOptModule))             return CompileCategory::OptimizeModule; 
			if (Utils::EqualTokens(token,tagOptFunction))           return CompileCategory::OptimizeFunction; 
			if (Utils::EqualTokens(token,tagRunPass))               return CompileCategory::RunPass; 
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
		constexpr Json::Token tagName     = Utils::CreateLiteralToken("name");
		constexpr Json::Token tagStart    = Utils::CreateLiteralToken("ts");
		constexpr Json::Token tagDuration = Utils::CreateLiteralToken("dur");
		constexpr Json::Token tagArgs     = Utils::CreateLiteralToken("args");
		constexpr Json::Token tagDetail   = Utils::CreateLiteralToken("detail");

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
		TCompileEvents& events = timeline.events; 
		TCompileEvents::iterator found = fastl::lower_bound(events.begin(),events.end(),compileEvent.start,[=](const CompileEvent& input, U32 value){ return value >= input.start; });
		TCompileEvents::iterator elem = events.emplace(found,compileEvent);    
	}

	// -----------------------------------------------------------------------------------------------------------
	void NormalizeStartTimes(ScoreTimeline& timeline)
	{ 
		TCompileEvents& events = timeline.events; 

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

	// -----------------------------------------------------------------------------------------------------------
	int ExtractScore(const ExportParams& params)
	{ 
		ScoreData scoreData;

		LOG_PROGRESS("Scanning dir: %s",params.input);

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
		}

		IO::Binarize(params.output, scoreData);

		return 0;
	} 
}