#include "ClangScore.h"

#include "../Common/CommandLine.h"
#include "../Common/Context.h"
#include "../Common/CRC64.h"
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
		// -----------------------------------------------------------------------------------------------------------
		template <typename T> inline constexpr T Min( const T a, const T b ) { return a < b ? a : b; }

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
	enum class ProcessEventPhase
	{
		Failure, 
		Start, 
		End,
		Single,
		Drop,
	};

	// -----------------------------------------------------------------------------------------------------------
	ProcessEventPhase ProcessEvent(ScoreData& scoreData, CompileEvent& output, CompileUnitContext& context, Json::Reader& reader, fastl::vector<CompileEvent>& pendingStack )
	{ 
		constexpr static Json::Token tagName     = Utils::CreateLiteralToken("name");
		constexpr static Json::Token tagStart    = Utils::CreateLiteralToken("ts");
		constexpr static Json::Token tagDuration = Utils::CreateLiteralToken("dur");
		constexpr static Json::Token tagArgs     = Utils::CreateLiteralToken("args");
		constexpr static Json::Token tagDetail   = Utils::CreateLiteralToken("detail");
		constexpr static Json::Token tagThread   = Utils::CreateLiteralToken("tid");
		constexpr static Json::Token tagPhase    = Utils::CreateLiteralToken("ph");

		//we assume a token that we want to drop unless we got a start/end phase or a complete event one
		ProcessEventPhase phase = ProcessEventPhase::Drop;

		//Open Object token already parsed by the caller
		Json::Token token; 
		while (reader.NextToken(token) && token.type != Json::Token::Type::ObjectClose)
		{ 			
			if (Utils::EqualTokens(token,tagName))
			{
				if (!reader.NextToken(token) || token.type != Json::Token::Type::String) return ProcessEventPhase::Failure;
				output.category = ToCompileCategory(token);
				if (output.nameHash == 0ull) output.nameHash = CompileScore::StoreCategoryTagString(scoreData,token.str,token.length, output.category);
			}
			else if (Utils::EqualTokens(token,tagStart))
			{
				if (!reader.NextToken(token) || token.type != Json::Token::Type::Number) return ProcessEventPhase::Failure;
				output.start = Utils::TokenToU32(token);
			}
			else if (Utils::EqualTokens(token,tagDuration))
			{
				if (!reader.NextToken(token) || token.type != Json::Token::Type::Number) return ProcessEventPhase::Failure;
				output.duration     = Utils::TokenToU32(token);
				output.selfDuration = output.duration;
			}
			else if (Utils::EqualTokens(token,tagArgs))
			{
				//Check the internal object
				if (!reader.NextToken(token) || token.type != Json::Token::Type::ObjectOpen) return ProcessEventPhase::Failure;
				while(reader.NextToken(token) && token.type != Json::Token::Type::ObjectClose)
				{ 
					if (Utils::EqualTokens(token,tagDetail))
					{ 
						if (!reader.NextToken(token) || token.type != Json::Token::Type::String) return ProcessEventPhase::Failure;

						if( output.category < CompileCategory::GatherFull )
						{
							output.nameHash = CompileScore::StoreCategoryValueString(scoreData,token.str,token.length, output.category);
						}
					}
					else 
					{ 
						reader.SkipObject();
					}
				}
			}
			else if (Utils::EqualTokens(token, tagThread))
			{
				if (!reader.NextToken(token) || token.type != Json::Token::Type::Number) return ProcessEventPhase::Failure;
			}
			else if( Utils::EqualTokens( token, tagPhase ) )
			{
				if( !reader.NextToken( token ) || token.type != Json::Token::Type::String ) return ProcessEventPhase::Failure;

				if( token.length == 1 )
				{
					if( ( *token.str == 'b' || *token.str == 'B' || *token.str == 's' || *token.str == 'S' ) )
					{
						phase = ProcessEventPhase::Start;
					}
					else if( ( *token.str == 'e' || *token.str == 'E' || *token.str == 'f' || *token.str == 'F' ) )
					{
						phase = ProcessEventPhase::End;
					}
					else if( *token.str == 'X' ) 
					{
						phase = ProcessEventPhase::Single;
					}
				}
			}
			else 
			{
				//unknown or uninteresting field - skip it
				reader.SkipObject();
			}
		}

		//TODO ~ ramonv ~ demangle optimize function names

		//Process Start/End events
		if ( phase == ProcessEventPhase::Start )
		{
			//Start event, we don't know the duration yet, store into the stack until we recieve the corresponding end event
			pendingStack.emplace_back( output );
		}
		else if ( phase == ProcessEventPhase::End )
		{
			if ( pendingStack.empty() )
			{
				LOG_ERROR( "Found End tracing event with an empty pending Stack");
				return ProcessEventPhase::Failure;
			}

			// Fix the self duration and replace with event with full data
			CompileEvent& originalEvent = pendingStack.back();
			originalEvent.duration = output.start - originalEvent.start;
			originalEvent.selfDuration = originalEvent.duration;
			output = originalEvent;

			pendingStack.pop_back();
		}		

		return phase;
	}

	// -----------------------------------------------------------------------------------------------------------
	U8 GetTrack( const CompileEvent& compileEvent )
	{
		//Move some events to a different track 

		enum AlternativeTrackNames : U64 
		{ 
			ParseDeclaration = Hash::CreateCompileTimeCRC64( "ParseDeclarationOrFunctionDefinition" ),
			ParseFunction    = Hash::CreateCompileTimeCRC64( "ParseFunctionDefinition" ),
		};

		if( compileEvent.category == CompileCategory::Other && ( compileEvent.nameHash == ParseDeclaration || compileEvent.nameHash == ParseFunction ) )
		{
			return 1u;
		}

		return 0u;
	}

	// -----------------------------------------------------------------------------------------------------------
	void AddEventToTimeline(ScoreTimeline& timeline, const CompileEvent& compileEvent)
	{ 
		const U8 track = GetTrack(compileEvent);

		//Make sure we have the track ready for this upcoming event 
		while( track >= timeline.tracks.size() ) { timeline.tracks.emplace_back(); }

		//inject in a sorted position
		TCompileEvents& events = timeline.tracks[ track ]; 
		TCompileEvents::iterator found = fastl::lower_bound(events.begin(),events.end(),compileEvent,
			[=](const CompileEvent& input, const CompileEvent& value)
			{ 
				return (value.start == input.start)? value.duration <= input.duration : value.start >= input.start; 
			});
		events.emplace(found,compileEvent);    
	}

	// -----------------------------------------------------------------------------------------------------------
	void NormalizeStartTimes(const char* path, CompileUnitContext& context, ScoreTimeline& timeline)
	{ 
		//Retrieve the first event start time
		U32 offset = 0u; 
		for ( size_t i = 0, sz = timeline.tracks.size(); i < sz; ++i)
		{
			const TCompileEvents& events = timeline.tracks[i];
			offset = events.empty() ? offset : Utils::Min( events[0].start, offset);
		}

		//Offset all events 
		if ( offset > 0u )
		{
			for( size_t i = 0, sz = timeline.tracks.size(); i < sz; ++i )
			{
				TCompileEvents& events = timeline.tracks[i]; 
				if( !events.empty() )
				{
					//Base the start times on the .json creation time instead of relying on consistent in json wall clock time
					const U64 fileEndTime = IO::GetLastWriteTimeInMicros( path );
					const U64 fileStartTime = fileEndTime - events[ 0 ].duration;
					context.startTime[ 0 ] = fileStartTime + ( context.startTime[ 0 ] - offset );
					context.startTime[ 1 ] = fileStartTime + ( context.startTime[ 1 ] - offset );

					for( CompileEvent& entry : events )
					{
						entry.start -= offset;
					}
				}
			}
		}
	}

	// -----------------------------------------------------------------------------------------------------------
	bool CheckClangTraceJson(Json::Reader& reader)
	{
		constexpr Json::Token literalTraceEvents = Utils::CreateLiteralToken("traceEvents");

		Json::Token token;

		//check this is a trackEvents or a different json file
		if (!reader.NextToken(token) || token.type != Json::Token::Type::ObjectOpen) return false;
		if (!reader.NextToken(token)) return false;

		// We are assuming that the json node starts with the traceEvents
		if (!Utils::EqualTokens(token, literalTraceEvents)) return false;

		return true;
	}

	// -----------------------------------------------------------------------------------------------------------
	bool ProcessFile(ScoreData& scoreData, const char* path, const char* content)
	{ 
		CompileUnitContext context;

		ScoreTimeline timeline;

		fastl::string inputPath{path};
		StringUtils::NormalizePath(inputPath);
		StringUtils::RemoveExtension(inputPath); //remove the .json
		timeline.nameHash = CompileScore::StoreString(scoreData, inputPath.c_str(), inputPath.length());

		//Parse JSON
		Json::Reader reader(content);

		//Read the first bits and validate we are reading a clang trace
		if (!CheckClangTraceJson(reader)) return false;

		Json::Token token; 
		if (!reader.NextToken(token) || token.type != Json::Token::Type::ArrayOpen)  return false;
		
		fastl::vector<CompileEvent> pendingEventStack;

		while (reader.NextToken(token) && token.type != Json::Token::Type::ArrayClose)
		{ 
			CompileEvent compileEvent; 
			if (token.type != Json::Token::Type::ObjectOpen) 
				return false;

			const ProcessEventPhase processResult = ProcessEvent( scoreData, compileEvent, context, reader, pendingEventStack );

			if ( processResult == ProcessEventPhase::Failure ) 
				return false;

			if( processResult == ProcessEventPhase::Start || processResult == ProcessEventPhase::Drop )
				continue;
			
			if (compileEvent.category == CompileCategory::FrontEnd)
			{
				context.startTime[0] = compileEvent.start;
			}
			else if (compileEvent.category == CompileCategory::BackEnd)
			{
				context.startTime[1] = compileEvent.start;
			}

			if (compileEvent.category != CompileCategory::Invalid)
			{ 
				AddEventToTimeline(timeline,compileEvent);
			}
		}

		if ( !pendingEventStack.empty() )
		{
			LOG_ERROR( "Mismatching Begin/End tracing events for %s ( pending stack size of %d )", path, pendingEventStack.size() );
			return false;
		}

		//From here we can ignore the rest of the file
		NormalizeStartTimes(path, context, timeline);

		CompileScore::ProcessTimeline(scoreData,timeline,context);
		return true;
	}

	// -----------------------------------------------------------------------------------------------------------
	bool ProcessFile(ScoreData& scoreData, const char* path)
	{ 
		if (IO::FileTextBuffer fileBuffer = IO::ReadTextFile(path))
		{ 
			ProcessFile(scoreData,path,fileBuffer);
			IO::DestroyBuffer(fileBuffer);
			return true;
		}
		 
		LOG_ERROR("Invalid file buffer for %s", path);
		return false;
	}

	//////////////////////////////////////////////////////////////////////////////////////////////////////////////

	// -----------------------------------------------------------------------------------------------------------
	fastl::string GetTimestampTokenPath(const char* directory)
	{ 
		fastl::string path = directory;
		
		if (!path.empty())
		{ 
			char lastChar = path[path.length()-1];
			
			//Fix end of directory if missing
			if (lastChar != '/' && lastChar != '\\')
			{ 
				path += '/';
			}
			path += "CompileScoreToken";
		}

		return path;
	}

	// -----------------------------------------------------------------------------------------------------------
	bool SaveRecordToken(const char* directory, const IO::FileTimeStamp timeStamp)
	{
		IO::RawBuffer outputBuffer; 
		outputBuffer.buff = (char*)&timeStamp;
		outputBuffer.size = sizeof(IO::FileTimeStamp);

		if (!IO::WriteRawFile(GetTimestampTokenPath(directory).c_str(),outputBuffer)) 
		{ 
			LOG_ERROR("Failed to start recording.");
			return false;
		}

		return true;
	}

	// -----------------------------------------------------------------------------------------------------------
	bool LoadRecordToken(IO::FileTimeStamp& output, const char* directory)
	{ 
		IO::RawBuffer buffer = IO::ReadRawFile(GetTimestampTokenPath(directory).c_str());

		if (buffer.buff != nullptr && buffer.size >= sizeof(IO::FileTimeStamp))
		{ 
			output = reinterpret_cast<IO::FileTimeStamp&>(*buffer.buff);
			return true;
		}

		return false;
	}

	// -----------------------------------------------------------------------------------------------------------
	bool DeleteRecordToken(const char* directory)
	{ 
		return IO::DeleteFile(GetTimestampTokenPath(directory).c_str());
	}

	// -----------------------------------------------------------------------------------------------------------
	bool CheckInputPath(const char* directory)
	{ 
		if (directory == nullptr)
		{ 
			LOG_ERROR("No input path provided.");
			return false;
		}

		if (!IO::IsDirectory(directory))
		{ 
			LOG_ERROR("input path is not a directory.");
			return false;
		}

		return true;
	}

	// -----------------------------------------------------------------------------------------------------------
	int GenerateScoreTrace(const ExportParams& params)
	{
		ScoreData scoreData;

		IO::FileTextBuffer fileBuffer = IO::ReadTextFile(params.input);
		if (fileBuffer == nullptr)
		{ 
			return FAILURE;
		}

		Context::Scoped<IO::ScoreBinarizer> binarizer(params.output,params.timelinePacking);

		const char* pathStart = fileBuffer;

		while(*fileBuffer)
		{ 
			if (*fileBuffer == '\n')
			{ 
				*fileBuffer = '\0'; //Generate an end of string
				
				if (pathStart < fileBuffer)
				{ 
					ProcessFile(scoreData,pathStart);
				}

				pathStart = ++fileBuffer;
			}
			else
			{ 
				++fileBuffer; 
			}
		}

		if (pathStart < fileBuffer)
		{ 
			ProcessFile(scoreData,pathStart);
		}

		CompileScore::FinalizeScoreData(scoreData);
		binarizer.Get().Binarize(scoreData);

		return SUCCESS;
	}

	// -----------------------------------------------------------------------------------------------------------
	int GenerateScoreDirectory(const ExportParams& params, IO::FileTimeStamp timeThreshold = IO::NO_TIMESTAMP)
	{ 
		ScoreData scoreData;

		LOG_PROGRESS("Scanning dir: %s",params.input);

		Context::Scoped<IO::ScoreBinarizer> binarizer(params.output,params.timelinePacking);
		size_t filesFound = 0u;
		
		IO::DirectoryScanner dirScan(params.input,".json",timeThreshold);
		while (const char* path = dirScan.SeekNext())
		{ 
			++filesFound;
			if (IO::FileTextBuffer fileBuffer = IO::ReadTextFile(path))
			{ 
				ProcessFile(scoreData,path,fileBuffer);
				IO::DestroyBuffer(fileBuffer);
			}
			else
			{ 
				LOG_ERROR("Invalid file buffer for %s", path);
			}
			LOG_INFO("Parsed file %u: (%s)\n",filesFound, path);
		}
		LOG_PROGRESS("Found %u files.\n",filesFound);

		CompileScore::FinalizeScoreData(scoreData);
		binarizer.Get().Binarize(scoreData);

		return SUCCESS;
	}

	// -----------------------------------------------------------------------------------------------------------
	int CleanScoreDirectory(const ExportParams& params)
	{
		LOG_PROGRESS("Scanning dir: %s", params.input);

		size_t filesFound = 0u;
		IO::DirectoryScanner dirScan(params.input, ".json");
		while (const char* path = dirScan.SeekNext())
		{
			if (IO::FileTextBuffer fileBuffer = IO::ReadTextFile(path))
			{
				//Read the first bits and validate we are reading a clang trace
				Json::Reader reader(fileBuffer);
				if (CheckClangTraceJson(reader)) 
				{
					IO::DeleteFile(path);
					++filesFound;
					LOG_INFO("Removed file %u: (%s)\n", filesFound, path);
				}
				IO::DestroyBuffer(fileBuffer);
			}
			else
			{
				LOG_ERROR("Invalid file buffer for %s", path);
			}
		}

		LOG_PROGRESS("Removed %u files.\n",filesFound);

		return SUCCESS;
	}

	// -----------------------------------------------------------------------------------------------------------
	int StopRecordingTrace(const ExportParams& params)
	{ 
		IO::FileTimeStamp timethreshold; 
		if (!LoadRecordToken(timethreshold,params.input))
		{ 
			return FAILURE;
		}

		DeleteRecordToken(params.input);

		LOG_PROGRESS("Scanning dir: %s",params.input);

		IO::TextOutputStream fileStream(params.output);

		if (!fileStream.IsValid())
		{ 
			return FAILURE;
		}

		size_t filesFound = 0u;
		IO::DirectoryScanner dirScan(params.input,".json",timethreshold);
		while (const char* path = dirScan.SeekNext())
		{ 
			LOG_INFO("Found file %s", path);
			fileStream.Append(path);
			fileStream.Append('\n');
			IO::Log(IO::Verbosity::Info,"Parsing... %u files\n",++filesFound);
		}

		LOG_PROGRESS("Found %d files", filesFound);
		return SUCCESS;
	}

	// -----------------------------------------------------------------------------------------------------------
	int StopRecordingGenerate(const ExportParams& params)
	{ 
		IO::FileTimeStamp timethreshold; 
		if (!LoadRecordToken(timethreshold,params.input))
		{ 
			LOG_ERROR("Unable to find the recording token file in path %s.", params.input);
			return FAILURE;
		}

		DeleteRecordToken(params.input);

		return GenerateScoreDirectory(params,timethreshold);
	}

	//////////////////////////////////////////////////////////////////////////////////////////////////////////////

	// -----------------------------------------------------------------------------------------------------------
	int Extractor::StartRecording(const ExportParams& params)
	{
		if (!CheckInputPath(params.input))
		{ 
			return FAILURE;
		}

		LOG_PROGRESS("Starting Clang recording...");

		SaveRecordToken(params.input,IO::GetCurrentTime());

		LOG_PROGRESS("Recording session started successfully!");
		return SUCCESS;
	}

	// -----------------------------------------------------------------------------------------------------------
	int Extractor::CancelRecording(const ExportParams& params)
	{
		if (!CheckInputPath(params.input))
		{ 
			return FAILURE;
		}

		if (!DeleteRecordToken(params.input))
		{ 
			LOG_ERROR("Unable to delete the token file at %s.", params.input);
			return FAILURE;
		}

		LOG_PROGRESS("Clang Recording session cancelled successfully!");
		return SUCCESS;
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

		if (!CheckInputPath(params.input))
		{ 
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

	// -----------------------------------------------------------------------------------------------------------
	int Extractor::Clean(const ExportParams& params)
	{
		if (params.input == nullptr)
		{
			LOG_ERROR("No input path provided.");
			return FAILURE;
		}
	
		if (!IO::IsDirectory(params.input))
		{
			LOG_ERROR("Input provided is not a directory.");
			return FAILURE;
		}

		return CleanScoreDirectory(params);
	}
}