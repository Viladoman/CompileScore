var Extract = require('./src/extract.js')

var verbosityLevel = 1;

var params = { detail: 3, timeline: true, timelinePacking: 100};

function FormatTime(miliseconds)
{ 
  var seconds = Math.floor(miliseconds/1000); 
  miliseconds = miliseconds % 1000;
  
  var minutes = Math.floor(seconds/60); 
  seconds     = seconds % 60;
  
  var hours   = Math.floor(minutes/60); 
  minutes     = minutes % 60;
  
  if (hours > 0)        return hours       + 'h ' + minutes     + 'm';
  else if (minutes > 0) return minutes     + 'm ' + seconds     + 's';
  else if (seconds > 0) return seconds     + 's ' + miliseconds + 'ms';
  else                  return miliseconds + 'ms';
}

function DisplayHelp()
{
  console.log("Compile Score Data Extractor"); 
  console.log("");
  console.log("Converts the compiler build trace data into 'scor' format."); 
  console.log("");
  console.log("Command Legend:"); 

  console.log("-input       (-i) : The path to the input folder to parse for -ftime-trace data or the direct path to the .etl file"); 
  console.log("-output      (-o) : The output file full path for the results (\'compileData.scor\' by default)"); 

  console.log("-detail      (-d) : Sets the level of detail exported - example: '-d 1'"); 
  console.log("\t0 - None"); 
  console.log("\t1 - Basic - w/ include"); 
  console.log("\t2 - FrontEnd - w/ include, parse, instantiate"); 
  console.log("\t3 - Full (default)"); 

  console.log("-notimeline (-nt) : No timeline files will be generated"); 

  console.log("-verbosity   (-v) : Sets the verbosity level - example: '-v 1'"); 
  console.log("\t0 - Silent"); 
  console.log("\t1 - Progress (default)"); 
  console.log("\t2 - Full"); 

  process.exit(0);
}

function Log(level,data){ if (level <= verbosityLevel) console.log(data); }

function ProcessCommandLine()
{
  var startTime = new Date();

  //Setup
  Extract.SetLogFunc(Log);

  //Check for command line arguments
  var args = process.argv;

  if (args.indexOf('?') >= 0) { DisplayHelp(); }

  var inputFolder = '.';
  var outputFile = 'compileData.scor';

  for (var i=0;i<args.length;++i)
  { 
    if (args[i] == '-nt' || args[i] == '-notimeline') 
    {
      params.timeline = false;
    }
    else if ((i+1) < args.length)
    { 
      if (args[i] == '-i'  || args[i] == '-input') inputFolder = args[i+1];
      if (args[i] == '-o'  || args[i] == '-output') outputFile = args[i+1];
      if (args[i] == '-tp' || args[i] == '-timelinepack')
      { 
        var packing = Number(args[i+1]);
        params.timelinePacking = isNaN(packing) || packing <= 0? params.timelinePacking : packing;
      }
      if (args[i] == '-d'  || args[i] == '-detail')
      { 
        var level = Number(args[i+1]);
        params.detail = isNaN(level)? params.detail : Math.max(0,level);
      }
      if (args[i] == '-v' || args[i] == '-verbosity') 
      {
        var level = Number(args[i+1]);
        verbosityLevel = isNaN(level)? verbosityLevel : Math.max(0,level);
      }
    }
  }

  //EXECUTE
  Extract.Extract(inputFolder,outputFile,params,function()
  {  
    Log(1,'Execution Time: '+FormatTime(new Date() - startTime))
  });

}

ProcessCommandLine(); 