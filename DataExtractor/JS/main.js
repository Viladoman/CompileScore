var Extract = require('./src/extract.js')

var verbosityLevel = 1;

function DisplayHelp()
{
  console.log('Clang to Compile Score Data Extractor'); 
  console.log('Command Legend:'); 
  console.log('-input  (-i): The path to the input folder to parse for -ftime-trace data'); 
  console.log('-output (-o): The output file full path for the results (\'compileData.scor\' by default)'); 
  console.log('-verbosity (-v): Set the level of verbosity - 0: silent - 1: progress - 2: full (default is 0)')

  process.exit(0);
}

function Log(level,data){ if (level <= verbosityLevel) console.log(data); }

function ProcessCommandLine()
{
  //Setup
  Extract.SetLogFunc(Log);

  //Check for command line arguments
  var args = process.argv;

  if (args.indexOf('?') >= 0) { DisplayHelp(); }

  var inputFolder = '.';
  var outputFile = 'compileData.scor';

  for (var i=0;i<args.length;++i)
  { 
    if ((i+1) < args.length)
    { 
      if (args[i] == '-i' || args[i] == '-input') inputFolder = args[i+1];
      if (args[i] == '-o' || args[i] == '-output') outputFile = args[i+1];
      if (args[i] == '-v' || args[i] == '-verbosity') 
      {
        var level = Number(args[i+1]);
        verbosityLevel = isNaN(level)? 0 : Math.max(0,level);
      }
    }
  }

  //EXECUTE
  Extract.Extract(inputFolder,outputFile,function(){});

}

ProcessCommandLine(); 