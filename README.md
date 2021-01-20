# CompileScore

VisualStudio extension and utilities used to display and highlight compilation profiling data. Know the real compilation cost of your code directly inside Visual Studio. Keep the compile times in check. 

[![MarketPlace](https://img.shields.io/badge/Visual_Studio_Marketplace-v1.5.4-green.svg)](https://marketplace.visualstudio.com/items?itemName=RamonViladomat.CompileScore)
[![Donate](https://img.shields.io/badge/Donate-PayPal-green.svg)](https://www.paypal.com/donate?hosted_button_id=QWTUS8PNK5X5A)

[Download latest VSIX from the Visual Studio Marketplace](https://marketplace.visualstudio.com/items?itemName=RamonViladomat.CompileScore)

[Download latest Data Extractor Executable](https://github.com/Viladoman/CompileScore/releases/latest/download/CompileScoreExtractor.zip)

## Motivation

Compile times are one of the most important things that affect productivity and iterations while developing in C/C++. Slow compile times can be very frustrating, as they are usual case scenarios in big code productions. Being able to identify which pieces are expensive in the same place you code is key in order to keep tech debt under control.

## Features

### Build and Profile
![Build And Profile](https://github.com/Viladoman/CompileScore/wiki/data/BuildAndProfileCommand.gif?raw=true)

### Text Highlight on include costs
![Highlight screenshot](https://github.com/Viladoman/CompileScore/wiki/data/highlightScreenshot.png?raw=true)

### Tool window with full project aggregated data
![Overview image](https://github.com/Viladoman/CompileScore/wiki/data/Overview.gif?raw=true)

### Detailed Timeline Graph for each translation unit
![Timeline image](https://github.com/Viladoman/CompileScore/wiki/data/CompileScoreTimeline.gif?raw=true)

Double-click any entry in the compile score window to open its timeline. 

Navigation controls:
- Zoom: Control + Mouse Wheel
- Scroll: Middle mouse press and drag

### Standalone App 

This repository also contains a standalone app with the same visualization and code as the VS extension. It can prove useful to compare results or open reports without having to open Visual Studio. 

The app needs to be build. The solution can be found at [CompileScore/WPF/CompileScoreWPF.sln](https://github.com/Viladoman/CompileScore/tree/master/CompileScore/WPF).

## How it works

The main idea is to get the C++ compiler to output a trace for what happened during the build. We can then aggregate all that data using the Data Extractor in this repository, and consume it with the VS plugin or the standalone app. 

The data extraction is an independent process in order to allow things like building the score file on a build server and consume it remotely. This can be useful in big codebases where we want the production floor to just use the reports from last night inside VS without having to profile locally.

![pipeline flow](https://github.com/Viladoman/CompileScore/wiki/data/Dataextraction.png?raw=true)

In the VS extension [options](https://github.com/Viladoman/CompileScore/wiki/Configurations) there is a field to tell the plugin where to find the report file (this is next to the solution file or root folder by default). 

For more information check the [Score Generation Page](https://github.com/Viladoman/CompileScore/wiki/Score-Generation).

## Documentation
- [Configurations and Options](https://github.com/Viladoman/CompileScore/wiki/Configurations)
- [Score Generaton And Data Extractor](https://github.com/Viladoman/CompileScore/wiki/Score-Generation)

## Building the Project 
The [Release workflow action](https://github.com/Viladoman/CompileScore/blob/master/.github/workflows/Release.yml) contains a step by step process for building the Data Extractor, the VISX and the Standalone App. 

Several [Test Projects](https://github.com/Viladoman/CompileScore/tree/master/TestProjects) have been included in the repository. 

## References
- [Visual Studio 2019](https://visualstudio.microsoft.com/vs/)
- [Clang 9+](https://releases.llvm.org/download.html) ( support for -ftime-trace ) 
- [Microsoft C++ Build Insights SDK](https://docs.microsoft.com/cpp/build-insights/get-started-with-cpp-build-insights)

## Related 
If you're not using Visual Studio but are still interested in the data aggregation, you can use [SeeProfiler](https://github.com/Viladoman/SeeProfiler), a standalone C++ compiler profiler which aggregates all the exported data from clang for a global view.

## Contributing
This project is open to code contributions. 

If you found this extension useful you can always buy me a cup coffee. 

[![paypal](https://www.paypalobjects.com/en_US/i/btn/btn_donate_SM.gif)](https://www.paypal.com/donate?hosted_button_id=QWTUS8PNK5X5A)
