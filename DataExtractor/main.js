var Extract = require('./src/extract.js')

function DisplayHelp()
{
  console.log('Clang to Compile Score Data Extractor'); 
  console.log('Command Legend:'); 
  console.log('-input  (-i): The path to the input folder to parse for -ftime-trace data'); 
  console.log('-output (-o): The output file full path for the results'); 

  process.exit(0);
}

function ProcessCommandLine()
{
  //Check for command line arguments
  var args = process.argv;

  if (args.indexOf('?') >= 0) { DisplayHelp(); }

  var inputFolder = '.';
  var outputFile = 'output.txt';

  for (var i=0;i<args.length;++i)
  { 
    if ((i+1) < args.length)
    { 
      if (args[i] == '-i' || args[i] == '-input') inputFolder = args[i+1];
      if (args[i] == '-o' || args[i] == '-output') outputFile = args[i+1];
    }
  }

  //EXECUTE
  Extract.Extract(inputFolder,outputFile,function(error){ 
    if (error){ }
    else { console.log("DONE"); }
  });

}

ProcessCommandLine(); 