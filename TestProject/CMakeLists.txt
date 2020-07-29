cmake_minimum_required(VERSION 3.5)

project(CompileScoreTest VERSION 1.0.0 LANGUAGES CXX)

###########
# Options #
###########

set(MAIN_TARGET CompileScoreTest)
option(COMPILER_PROFILING "Enables the data extraction for compilation times" OFF)
option(WARNINGS_AS_ERRORS "Treat warnings as errors" ON)

#This will generate compile_comands.json for visualization on what gets executed on the compiler
set(CMAKE_EXPORT_COMPILE_COMMANDS ON)

###########
# Globals #
###########

set(BASE_CODE_DIR ${CMAKE_CURRENT_SOURCE_DIR}/src)
set(BASE_INCLUDE_PATHS ${BASE_CODE_DIR})

#########
# Utils #
#########

function(BaseCompilerSetup TARGET_NAME)

	#include paths
    target_include_directories(${TARGET_NAME} PRIVATE ${BASE_INCLUDE_PATHS})

	# Compiler flags #
	target_compile_features(${TARGET_NAME} PRIVATE cxx_std_17)

	if(CMAKE_CXX_COMPILER_ID STREQUAL "MSVC")
		target_compile_options(${TARGET_NAME} PRIVATE /W4)

		if (${WARNINGS_AS_ERRORS})
			target_compile_options(${TARGET_NAME} PRIVATE /WX)
		endif()
	else()
		if (MSVC)
			#using W4 instead of -Wall (as clang-cl converts -Wall to -Weverything)
			target_compile_options(${TARGET_NAME} PRIVATE -W4 -Wextra -Wpedantic)
		else()
			target_compile_options(${TARGET_NAME} PRIVATE -Wall -Wextra -Wpedantic)
		endif()

		if (${WARNINGS_AS_ERRORS})
			target_compile_options(${TARGET_NAME} PRIVATE -Werror)
		endif()

	endif()

	# Preprocessor 
	if(CMAKE_BUILD_TYPE STREQUAL "Debug")
	  target_compile_definitions(${TARGET_NAME} PRIVATE TARGET_DEBUG=1 )
	elseif(CMAKE_BUILD_TYPE STREQUAL "Release" OR CMAKE_BUILD_TYPE STREQUAL "RelWithDebInfo")
	  target_compile_definitions(${TARGET_NAME} PRIVATE TARGET_RELEASE=1 )
	endif()
endfunction()


###############
# Main Target #
###############

#Common settings for MAIN_TARGET
add_executable(${MAIN_TARGET})
target_sources(${MAIN_TARGET} PRIVATE "src/main.cpp")

BaseCompilerSetup(${MAIN_TARGET})

############################
# Compiler Profiling Trace #
############################

if (CMAKE_CXX_COMPILER_ID STREQUAL "Clang" AND ${COMPILER_PROFILING})
	target_compile_options(${MAIN_TARGET} PRIVATE -ftime-trace)

	#Command to gather the data
	add_custom_command(TARGET ${MAIN_TARGET} POST_BUILD
		COMMAND node ${CMAKE_CURRENT_SOURCE_DIR}/../DataExtractor/main.js -i CMakeFiles/${MAIN_TARGET}.dir -o ${CMAKE_CURRENT_SOURCE_DIR}/compileIncludeData.txt
		WORKING_DIRECTORY ${CMAKE_BINARY_DIR}
		COMMENT "Parsing compile time trace data..."
	)
endif()
