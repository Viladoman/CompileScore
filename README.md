# CompileScore
VisualStudio extension used to display and highlight compilation profiling data. Know the real compilation cost of your code directly inside Visual Studio. Keep the compile times in check. 

[Download latest from the Visual Studio Marketplace](https://marketplace.visualstudio.com/items?itemName=RamonViladomat.CompileScore)

## Motivation

Compile times are one of the most important things that affect productivity and iterations while developing in C/C++. Slow compile times can be very frustrating, as they are usual case scenarios in big code productions. Being able to identify which pieces are expensive in the same place you code is key in order to keep tech debt in check.

## Features

### Text Highlight on include costs
![Hihglight screenshot](https://github.com/Viladoman/CompileScore/wiki/data/highlightScreenshot.png?raw=true)

### Tool window with full project aggregated data

#### Translation Units
![Overview screenshot](https://github.com/Viladoman/CompileScore/wiki/data/overview.png?raw=true)

#### Include data
![Includes screenshot](https://github.com/Viladoman/CompileScore/wiki/data/includes.png?raw=true)

## How it works

Clang 9+ added the '-ftime-trace' flag. This flag outputs a detailed report for each translation unit executed by the compiler. We can then aggregate all that data using the DataExtractor in this repository, and use it together with the VS plugin in order to visualize the data inside Visual Studio. 

The data extraction is a different process, due to the fact that in big codebases you might want to just extract the data in a build machine. Also, you might want to have the programmers just sync the reports to avoid having to do expensive full compilations locally. 

![pipeline flow](https://github.com/Viladoman/CompileScore/wiki/data/Dataextraction.png?raw=true)

The following command will parse all the directories recursively, and process all clang trace files. 
```
node DataExtractorFolder/main.js -i TempFolderWithObj/ -o compileData.scor
```
In the VS extension settings there is a field to tell the plugin where to find the report file (this is next to the solution file or root folder by default). 

For small projects, like the Test Project included in this repo, you can perform the data extraction as a build post process in a given configuration in order to keep your compile data up to date.

## Requirements

- Visual Studio 2019
- [Clang 9+](https://releases.llvm.org/download.html) ( support for -ftime-trace ) 
- [node.js](https://nodejs.org/) to run the data extraction script

## Running the Test Project 

- Install the plugin. 
- Select 'Open Folder' in VS 2019 to the TestProject folder directy. This will open VS and setup using the CMake configuration provided. 
- Compile 'x64-Clang-Debug-Profile' in order to generate the compilation data and see the plugin in action.

The configurations provided with 'Profile' will add the '-ftime-trace' flag to clang and extract the data once the build is finished. 

## Related 

If you're not using Visual Studio but are still interested in the data aggregation, you can use [SeeProfiler](https://github.com/Viladoman/SeeProfiler), a standalone C++ compiler profiler which aggregates all the exported data from clang for a global view.
