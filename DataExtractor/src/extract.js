var FileIO = require('./fileIO.js')
var Bin    = require('./binary.js');

var path = require('path');

const version = 1;

var NodeNature = {
  SOURCE:                0,
  PARSECLASS:            1,
  PARSETEMPLATE:         2,
  INSTANTIATECLASS:      3,
  INSTANTIATEFUNCTION:   4,
  CODEGENFUNCTION:       5,
  OPTMODULE:             6,
  OPTFUNCTION:           7,
  OTHER:                 8,
  RUNPASS:               9,
  PENDINGINSTANTIATIONS: 10,
  FRONTEND:              11,
  BACKEND:               12,
  EXECUTECOMPILER:       13,
  INVALID:               14,
}

var NodeNatureData = {
  GLOBAL_GATHER_THRESHOLD:  NodeNature.RUNPASS,
  GLOBAL_DISPLAY_THRESHOLD: NodeNature.INVALID,
  COUNT:                    NodeNature.INVALID
}

function NodeNatureFromString(natureName)
{
       if (natureName == 'Source')                      { return NodeNature.SOURCE; }
  else if (natureName == 'ParseClass')                  { return NodeNature.PARSECLASS; }
  else if (natureName == 'ParseTemplate')               { return NodeNature.PARSETEMPLATE; }
  else if (natureName == 'InstantiateClass')            { return NodeNature.INSTANTIATECLASS; }
  else if (natureName == 'InstantiateFunction')         { return NodeNature.INSTANTIATEFUNCTION; }
  else if (natureName == 'CodeGen Function')            { return NodeNature.CODEGENFUNCTION; }
  else if (natureName == 'PerformPendingInstantiations'){ return NodeNature.PENDINGINSTANTIATIONS; }
  else if (natureName == 'OptModule')                   { return NodeNature.OPTMODULE; }
  else if (natureName == 'OptFunction')                 { return NodeNature.OPTFUNCTION; }
  else if (natureName == 'RunPass')                     { return NodeNature.RUNPASS; }
  else if (natureName == 'Frontend')                    { return NodeNature.FRONTEND; }
  else if (natureName == 'Backend')                     { return NodeNature.BACKEND; }
  else if (natureName == 'ExecuteCompiler')             { return NodeNature.EXECUTECOMPILER; }
  else if (natureName == 'Other')                       { return NodeNature.OTHER; }
  return NodeNature.OTHER;
}

function CreateObjectArray(size)
{ 
  var ret = new Array();
  for (var i = 0; i < size; ++i)
    ret.push(new Object());
  return ret;
}

function CreateArrayArray(size)
{
  var ret = new Array();
  for (var i = 0; i < size; ++i)
    ret.push(new Array());
  return ret;
}

var globals = CreateObjectArray(NodeNatureData.GLOBAL_GATHER_THRESHOLD);
var units = [];

function FillDatabaseData(collection,name,duration)
{
  var label = name.toLowerCase();
  var found = collection[label];
  if (found == undefined)
  {
    collection[label] = {min: duration, max: duration, acc: duration, num: 1};
  }
  else
  {
    found.min = Math.min(found.min,duration);
    found.max = Math.max(found.max,duration);
    found.acc += duration; 
    ++found.num;
  }
}

function FinalizeDatabaseData(container, targetContainer)
{
  for (var key in container)
  {
    var entry = container[key];
    targetContainer.push({ name: key, min: entry.min, max: entry.max, num: entry.num, acc: entry.acc });
  }
}

function ComputeUnit(filename,nodes)
{ 
  var ret = { name: path.basename(filename).replace(/\.[^/.]+$/, ""), totals: Array(NodeNatureData.GLOBAL_DISPLAY_THRESHOLD).fill(0)};
  
  for (var i=0;i<NodeNatureData.GLOBAL_DISPLAY_THRESHOLD;i++)
  {
    var thisNodes = nodes[i];
    thisNodes.sort(function(a,b){ return a.start == b.start? b.duration - a.duration : a.start - b.start; });

    var nodesLength = thisNodes.length;
    if (nodesLength > 0)
    { 
      var first = thisNodes[0];
      var total = first.duration;
      var threshold = first.start + total;
      for (var k=1;k<nodesLength;++k)
      { 
        var element = thisNodes[k];
        if ( element.start >= threshold ) //remove overlapping nodes with the same nature
        { 
          threshold = element.start + element.duration;
          total += element.duration;
        }
      }

      ret.totals[i] = total;
    }
  }

  return ret;
}

function AddToDatabase(filename,events)
{ 
  var nodes = CreateArrayArray(NodeNatureData.GLOBAL_DISPLAY_THRESHOLD);

  for (var i=0,sz=events.length;i<sz;++i)
  {
    var element = events[i];

    //Check for includes 
    if (element.tid == 0 && element.name != 'process_name') 
    { 
      var nature = NodeNatureFromString(element.name);
      if (nature < NodeNatureData.GLOBAL_GATHER_THRESHOLD) 
      {
        var name = element.args == undefined? element.name : element.args.detail;
        name = nature == NodeNature.SOURCE || nature == NodeNature.OPTMODULE? path.basename(name) : name;
        FillDatabaseData(globals[nature],name,element.dur); 
      }

      if (nature < NodeNatureData.GLOBAL_DISPLAY_THRESHOLD)
      { 
        nodes[nature].push({start: element.ts, duration: element.dur});
      }
    }
  }

  units.push(ComputeUnit(filename,nodes));
}

function ParseFile(file,content)
{ 
  if (content.startsWith('{"traceEvents":') || content.startsWith('{ "traceEvents":'))
  {
    console.log('PARSING: '+file);

    var obj = JSON.parse(content);
    if (obj.traceEvents)
    {
      AddToDatabase(file,obj.traceEvents);
    }
  }
}

function BinarizeUnit(unit)
{
  var ret = Bin.Str(unit.name);
  for (var i=0;i<NodeNatureData.GLOBAL_DISPLAY_THRESHOLD;i++) ret = Bin.Concat(ret,Bin.Num(unit.totals[i],4)); 
  return ret;
}

function BinarizeEntry(entry)
{ 
  return Bin.Concat(Bin.Str(entry.name),Bin.Concat(Bin.Concat(Bin.Num(entry.acc,8),Bin.Num(entry.min,4)),Bin.Concat(Bin.Num(entry.max,4),Bin.Num(entry.num,4))));
}

function Extract(inputFolder,outputFile,doneCallback)
{ 
  FileIO.SearchFolder(inputFolder,ParseFile,function(error){
    if (error) { console.log(error); doneCallback(error); }
    else
    { 
      FileIO.SaveFileStream(outputFile,function(stream){

        stream.write(Bin.Num(version,4));

        //Export units
        stream.write(Bin.Num(units.length,4));
        for (var i=0,sz=units.length;i<sz;++i)
        { 
          stream.write(BinarizeUnit(units[i]));
        }

        //Export entries
        for (var i=0;i<NodeNatureData.GLOBAL_GATHER_THRESHOLD;++i)
        { 
          var finalList = [];
          FinalizeDatabaseData(globals[i],finalList); 
          stream.write(Bin.Num(finalList.length,4));
          for (var k=0;k<finalList.length;++k)
          { 
            stream.write(BinarizeEntry(finalList[k])); 
          }
        }
      },doneCallback)
    } 
  })
} 

exports.Extract = Extract;