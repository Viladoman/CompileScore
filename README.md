# CompileScore
VisualStudio extension to highlight compilation profiling data. Know directly inside the VS IDE the real compilation cost of your code ( WIP: At the moment only highlights #include data )

( expand description )

( add links to download )

( add screenshot )

## Motivation

Compile times are one of the most important things that affect productivity and iterations while developing in C/C++. When compile times are slow it can be very frustating at it is a usual case scenario in big code productions. Being able to know in the same spot where you code what pieces are expensive is key in order to keep that tech debt in check.

## How it works

Clang 9+ added the '-ftime-trace' flag. This flag outputs a detailed report for each translation unit executed by the compiler. We can then aggregate all that data using the DataExtractor in this repository and use it with the VS plugin to visualize the data inside Visual Studio. 

The data extraction is a different process due to the fact that in big codebases you might want to just extract the data in a build machine and have the programmers just sync the reports to avoid having to do expensive full compilations locally. 

... ( add small graph here / 2 graphs ) 

## Requirements

Visual Studio 2019
Clang 9+ ( support for -ftime-trace ) 
node.js to extract the data

## Running the Test Project 

- Install the plugin. 
- Chose 'Open Folder' option in VS 2019 to the TestProject directy. This will open VS and setup using the CMake configuration provided. 
- Compile 'x64-Clang-Debug-Profile' in order to generate the data compilation data and see the plugin in action.

The configurations provided with 'Profile' will add the '-ftime-trace' flag to clang and extract the data once the build is finished. 

## Related 

If not using Visual Studio but still interested in the data aggregation you can use [SeeProfiler](https://github.com/Viladoman/SeeProfiler), a standalone C++ compiler profiler which aggregates all the exported data from clang for a global view.
