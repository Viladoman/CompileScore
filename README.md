# CompileScore
VisualStudio extension used to display and highlight compilation profiling data. Know directly inside Visual Studio the real compilation cost of your code. Keep the compile times in check. 

( add link to marketplace )

## Motivation

Compile times are one of the most important things that affect productivity and iterations while developing in C/C++. When compile times are slow it can be very frustating at it is a usual case scenario in big code productions. Being able to know in the same spot where you code what pieces are expensive is key in order to keep tech debt in check.

## Features

### Text Highlight on include costs
![Hihglight screenshot](https://github.com/Viladoman/CompileScore/wiki/data/highlightScreenshot.png?raw=true)

### Tool window with full project aggregated data

#### Translation Units
![Overview screenshot](https://github.com/Viladoman/CompileScore/wiki/data/overview.png?raw=true)

#### Include data
![Includes screenshot](https://github.com/Viladoman/CompileScore/wiki/data/includes.png?raw=true)

## How it works

Clang 9+ added the '-ftime-trace' flag. This flag outputs a detailed report for each translation unit executed by the compiler. We can then aggregate all that data using the DataExtractor in this repository and use it with the VS plugin to visualize the data inside Visual Studio. 

The data extraction is a different process due to the fact that in big codebases you might want to just extract the data in a build machine and have the programmers just sync the reports to avoid having to do expensive full compilations locally. 

![pipeline flow](https://github.com/Viladoman/CompileScore/wiki/data/Dataextraction.png?raw=true)

The following command will parse recursively all the directories and process all clang trace files. 
```
node MyProject/DataExtractorFolder/main.js -i MyProject/TempFolderWithObj/ -o MyProject/compileData.scor
```
In the VS extension settings there is a field to tell the plugin where to find the report file (by default next to the solution file or root folder). 

For small projects, like the Test Project included in this repo, you can perfrom the data extraction as a build post process in a given configuration in order to keep your compile data up to date.

## Requirements

- Visual Studio 2019
- Clang 9+ ( support for -ftime-trace ) 
- [node.js](https://nodejs.org/) to run the data extraction script

## Running the Test Project 

- Install the plugin. 
- Chose 'Open Folder' option in VS 2019 to the TestProject directy. This will open VS and setup using the CMake configuration provided. 
- Compile 'x64-Clang-Debug-Profile' in order to generate the data compilation data and see the plugin in action.

The configurations provided with 'Profile' will add the '-ftime-trace' flag to clang and extract the data once the build is finished. 

## Related 

If not using Visual Studio but still interested in the data aggregation you can use [SeeProfiler](https://github.com/Viladoman/SeeProfiler), a standalone C++ compiler profiler which aggregates all the exported data from clang for a global view.
