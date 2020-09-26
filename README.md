# CompileScore

[![Clang](https://img.shields.io/badge/Clang-Full-green)]() [![MSVC](https://img.shields.io/badge/MSVC-Partial-yellow)]() 

VisualStudio extension used to display and highlight compilation profiling data. Know the real compilation cost of your code directly inside Visual Studio. Keep the compile times in check. 

[Download latest from the Visual Studio Marketplace](https://marketplace.visualstudio.com/items?itemName=RamonViladomat.CompileScore)

## Motivation

Compile times are one of the most important things that affect productivity and iterations while developing in C/C++. Slow compile times can be very frustrating, as they are usual case scenarios in big code productions. Being able to identify which pieces are expensive in the same place you code is key in order to keep tech debt under control.

## Features

### Text Highlight on include costs
![Hihglight screenshot](https://github.com/Viladoman/CompileScore/wiki/data/highlightScreenshot.png?raw=true)

### Tool window with full project aggregated data

#### Translation Units
![Overview screenshot](https://github.com/Viladoman/CompileScore/wiki/data/overview.png?raw=true)

#### Include data
![Includes screenshot](https://github.com/Viladoman/CompileScore/wiki/data/includes.png?raw=true)

### Detailed Timeline Graph for each translation unit
![Timeline screenshot](https://github.com/Viladoman/CompileScore/wiki/data/timeline.png?raw=true)

Double-click any entry in the compile score window to open its timeline. 

Navigation controls:
- Zoom: Control + Mouse Wheel
- Scroll: Middle mouse press and drag

## How it works

Setup the C++ compiler to output a trace for what happened during the build. We can then aggregate all that data using the DataExtractor in this repository, and use it together with the VS plugin in order to visualize the data inside Visual Studio. 

The data extraction is a different process, due to the fact that in big codebases you might want to just extract the data in a build machine. Also, you might want to have the programmers just sync the reports to avoid having to do expensive full compilations locally. 

![pipeline flow](https://github.com/Viladoman/CompileScore/wiki/data/Dataextraction.png?raw=true)

In the VS extension settings there is a field to tell the plugin where to find the report file (this is next to the solution file or root folder by default). 

For small projects, like the Test Projects included in this repo, you can perform the data extraction as a build post process in a given configuration in order to keep your compile data up to date.

### Clang

Clang 9 (or newer) is needed in order to use the '-ftime-trace' option. This flag outputs a detailed report for each translation unit executed by the compiler.

The following command will parse all the directories recursively, and process all clang trace files. 
#### C++ Extractor
```
ScoreDataExtractor.exe -clang -i TempFolderWithObj/ -o compileData.scor
```
#### JavaScript Extractor
```
node DataExtractorFolder/main.js -i TempFolderWithObj/ -o compileData.scor
```

### MSVC

For the Microsoft compiler we are using [C++ Build Insights SDK](https://docs.microsoft.com/cpp/build-insights/get-started-with-cpp-build-insights) and [vcperf](https://github.com/microsoft/vcperf). You can vcperf directly from the github repository or by downloading the **Microsoft.Cpp.BuildInsights** package inside VS using the Nuget manager.

#### Generate the ETL build trace
- Open an elevated command-line prompt.
- Run the following command: ```vcperf /start SessionName``` (Add ```/level3``` to gather template instance data).
- Build your C++ project from anywhere, even from within Visual Studio (vcperf collects events system-wide).
- Run the following command: ```vcperf /stopnoanalyze SessionName buildTraceFile.etl```

> :warning: **If you are doing incremental builds**: *vcperf* only tracks what is being compiled in the current session. This means that incremental builds will only display the data from the last build, not the overall project data. 

#### C++ Data Exporter
The following command will extract and pack the build data from the *.etl* trace file generated
```
ScoreDataExtractor.exe -msvc -i buildTraceFile.etl -o compileData.scor
```

## Building the C++ Data Extractor

The Data Extractor can be build using the Visual Studio solution located at **DataExtractor/Cpp/ScoreDataExtractor.sln**. This solution contains the extractors for both pipelines (Clang and MSVC). 

## Data Extractor Options

| Executable Flag       | Arguments and description                                                          | Mandatory |
|-----------------------|------------------------------------------------------------------------------------|-----------|
| `-clang` or `-msvc`   | Sets the system to use the Clang (.json traces) or MSVC (.etl traces) importer     | Yes       |
| `-input` (`-i`)       | `Path to Input File`                                                               | Yes       |
|                       | The path to the input folder to parse for -ftime-trace data or .etl file           |           |
| `-output` (`-o`)      | `Output file`                                                                      | No        |
|                       | The output file full path for the results (**compileData.scor** by default)        |           |
| `-detail` (`-d`)      | `Level`                                                                            | No        |
|                       | The exported detail level, useful to reduce the *.scor* file size on big projects: |           |
|                       | 0 : None                                                                           |           |
|                       | 1 : Basic - w/ include                                                             |           |
|                       | 2 : FrontEnd - w/ include, parse, instatiate                                       |           |
|                       | 3 : Full (**default**)                                                             |           |
| `-notimeline` (`-nt`) | No timeline files will be generated                                                | No        |
| `-verbosity` (`-v`)   | `Level`                                                                            | No        |
|                       | Sets the verbosity level:                                                          |           |
|                       | 0 : Silent                                                                         |           |
|                       | 1 : Progress (**default**)                                                         |           |
|                       | 2 - Full"                                                                          |           |

## Running the Test Project 

### Clang & Javascript DataExtractor
- Install the plugin. 
- Make sure you have node.js installed in your machine. 
- Select 'Open Folder' in VS 2019 to the 'TestProject/ClangJS' folder directy. This will open VS and setup using the CMake configuration provided. 
- Compile 'x64-Clang-Debug-Profile' in order to generate the compilation data and see the plugin in action.

This setup uses the JavaScript DataExporter.
The configurations provided with the suffix 'Profile' add the *-ftime-trace* flag to clang and extract the data once the build is finished. 

### Clang & C++ DataExtractor
- Install the plugin. 
- Build the 'DataExtractor/Cpp' project (**x64** & **Release** configuration)
- Select 'Open Folder' in VS 2019 to the 'TestProject/ClangCpp' folder directy. This will open VS and setup using the CMake configuration provided. 
- Compile 'x64-Clang-Debug-Profile' in order to generate the compilation data and see the plugin in action.

This setup uses the JavaScript DataExporter.
The configurations provided with the suffix 'Profile' add the *-ftime-trace* flag to clang and extract the data once the build is finished. 

### MSVC
- Install the plugin
- Build the 'DataExtractor/Cpp' project (**x64** & **Release** configuration)
- Open the VS solution located in 'TestProject/MSVC' with Visual Studio in **Elevated** mode.
- Compile 'Debug-Profile' in order to generate the compilation data and see the plugin in action

The configurations provided with the suffix 'Profile' add the required prebuild and postbuild steps.

## References
- [Visual Studio 2019](https://visualstudio.microsoft.com/vs/)
- [Clang 9+](https://releases.llvm.org/download.html) ( support for -ftime-trace ) 
- [Microsoft vcperf](https://github.com/microsoft/vcperf)
- [Microsoft C++ Build Insights SDK](https://docs.microsoft.com/cpp/build-insights/get-started-with-cpp-build-insights)
- [node.js](https://nodejs.org/) to run the data extraction script

## Related 

If you're not using Visual Studio but are still interested in the data aggregation, you can use [SeeProfiler](https://github.com/Viladoman/SeeProfiler), a standalone C++ compiler profiler which aggregates all the exported data from clang for a global view.
