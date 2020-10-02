var FileIO = require('./fileIO.js')
var Bin    = require('./binary.js');

var path = require('path');

var exportParams = {};

const VERSION = 4;
const TIMELINE_FILE_NUM_DIGITS = 4;
const TIMELINES_PER_FILE_FALLBACK = 100;

const maxU32 = 0xffffffff;

var NodeNature = {
  SOURCE:                0,
  PARSECLASS:            1,
  PARSETEMPLATE:         2,
  INSTANTIATECLASS:      3,
  INSTANTIATEFUNCTION:   4,
  INSTANTIATEVARIABLE:   5, 
  INSTANTIATECONCEPT:    6,
  CODEGENFUNCTION:       7,
  OPTFUNCTION:           8,
  
  PENDINGINSTANTIATIONS: 9,
  OPTMODULE:             10,
  FRONTEND:              11,
  BACKEND:               12,
  EXECUTECOMPILER:       13,
  OTHER:                 14,

  RUNPASS:               15,
  CODEGENPASSES:         16,
  PERFUNCTIONPASSES:     17,
  PERMODULEPASSES:       18,
  DEBUGTYPE:             19,
  DEBUGGLOBALVARIABLE:   20,

  INVALID:               21,
}

var NodeNatureData = {

  GLOBAL_GATHER_NONE:       NodeNature.SOURCE, 
  GLOBAL_GATHER_BASIC:      NodeNature.PARSECLASS,
  GLOBAL_GATHER_FRONTEND:   NodeNature.CODEGENFUNCTION,
  GLOBAL_GATHER_FULL:       NodeNature.PENDINGINSTANTIATIONS,
  GLOBAL_DISPLAY_THRESHOLD: NodeNature.RUNPASS,
  COUNT:                    NodeNature.INVALID
}

var logFunc = function(level,data){}; //dummy function

function LogError(data)    { logFunc(0,data); }
function LogProgress(data) { logFunc(1,data); }
function LogInfo(data)     { logFunc(2,data); }

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
  else if (natureName == 'CodeGenPasses')               { return NodeNature.CODEGENPASSES; }
  else if (natureName == 'PerFunctionPasses')           { return NodeNature.PERFUNCTIONPASSES; }
  else if (natureName == 'PerModulePasses')             { return NodeNature.PERMODULEPASSES; }
  else if (natureName == 'DebugType')                   { return NodeNature.DEBUGTYPE; }
  else if (natureName == 'DebugGlobalVariable')         { return NodeNature.DEBUGGLOBALVARIABLE; }
  else if (natureName == 'Frontend')                    { return NodeNature.FRONTEND; }
  else if (natureName == 'Backend')                     { return NodeNature.BACKEND; }
  else if (natureName == 'ExecuteCompiler')             { return NodeNature.EXECUTECOMPILER; }
  else if (natureName == 'Other')                       { return NodeNature.OTHER; }

  if (natureName == 'process_name' || natureName == 'thread_name' || natureName.startsWith('Total')) { return NodeNature.INVALID; }

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

var globals = CreateArrayArray(NodeNatureData.GLOBAL_GATHER_FULL);
var globalsDictionary = CreateObjectArray(NodeNatureData.GLOBAL_GATHER_FULL);
var units = [];

var activeBundle = undefined;
var nextBundleNumber = 0;
var bundleBasePath = '';

function GetTimelinesPerFile() { return exportParams.timelinePacking != undefined? exportParams.timelinePacking : TIMELINES_PER_FILE_FALLBACK; }

function GetGlobal(nature, name)
{ 
  var dict = globalsDictionary[nature];
  var found = dict[name];
  if (found != undefined)
  {
    return found;
  }

  //create new entry 
  var newObj = {name: name, nameId: globals[nature].length, min: maxU32, max: 0, acc: 0, num: 0};
  globals[nature].push(newObj);
  dict[name] = newObj;
  return newObj;
}

function GetLimitFromDetail(detail)
{ 
  switch(detail)
  { 
    case 0: return NodeNatureData.GLOBAL_GATHER_NONE;
    case 1: return NodeNatureData.GLOBAL_GATHER_BASIC;
    case 2: return NodeNatureData.GLOBAL_GATHER_FRONTEND; 
    default: return NodeNatureData.GLOBAL_GATHER_FULL;
  }
}

function ProcessTimeline(filename, timeline)
{ 
  var unitId = units.length; 
  var unit = { name: path.basename(filename).replace(/\.[^/.]+$/, ""), totals: Array(NodeNatureData.GLOBAL_DISPLAY_THRESHOLD).fill(0)};
  units.push(unit);

  var overlapThreshold = Array(NodeNatureData.GLOBAL_DISPLAY_THRESHOLD).fill(-1);
  var gatherLimit = exportParams.detail != undefined? GetLimitFromDetail(exportParams.detail) : NodeNatureData.GLOBAL_GATHER_FULL;

  for (var i=0,sz=timeline.length;i<sz;++i)
  {
    var element = timeline[i];
    if (element.category < gatherLimit) 
    {
      var global = GetGlobal(element.category,element.name);

      element.nameId = global.nameId;

      global.acc += element.duration;
      global.min = Math.min(element.duration,global.min);
      if (element.duration > global.max)
      { 
        global.maxId = unitId;
        global.max = element.duration;
      }
      ++(global.num);
    }

    if (element.category < NodeNatureData.GLOBAL_DISPLAY_THRESHOLD)
    { 
      if (element.start >= overlapThreshold[element.category])
      { 
        unit.totals[element.category] += element.duration;
        overlapThreshold[element.category] = element.start+element.duration;
      }
    }
  }

  ExportTimeline(timeline);
}

function AddToDatabase(filename,events)
{  
  var timeline = [];

  for (var i=0,sz=events.length;i<sz;++i)
  { 
    var element = events[i];
    var nature = NodeNatureFromString(element.name);
    if (nature != NodeNature.INVALID)
    { 
      var name = element.args == undefined? element.name : element.args.detail;
      name = nature == NodeNature.SOURCE? path.basename(name) : name;
      timeline.push({name: name.toLowerCase(), nameId: maxU32, start: element.ts, duration: element.dur, category: nature});
    }
  }
  
  //sort timeline
  timeline.sort(function(a,b){ return a.start == b.start? b.duration - a.duration : a.start - b.start; });
  
  //Process timeline
  ProcessTimeline(filename,timeline);
}

function ParseFile(file,content)
{ 
  if (content.startsWith('{"traceEvents":') || content.startsWith('{ "traceEvents":'))
  {
    LogInfo('PARSING: '+file);

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
  return Bin.Concat
  (
    Bin.Str(entry.name),
    Bin.Concat(
      Bin.Concat(
        Bin.Concat(
          Bin.Num(entry.acc,8),
          Bin.Num(entry.min,4)
        ),
        Bin.Concat(
          Bin.Num(entry.max,4),
          Bin.Num(entry.num,4)
        )
      ),
      Bin.Num(entry.maxId,4)
    )
  );
}

function BinarizeTimelineEvent(timelineEvent)
{ 
  return Bin.Concat(
    Bin.Concat(
      Bin.Num(timelineEvent.start,4), 
      Bin.Num(timelineEvent.duration,4)
    ),
    Bin.Concat(
      Bin.Num(timelineEvent.nameId,4),
      Bin.Num(timelineEvent.category,1)
    )
  );
}

function WriteTimeline(stream, timeline)
{ 
  //Clang outputs a single thread
  stream.write(Bin.Num(1,4));

  //Single task serialization
  stream.write(Bin.Num(timeline.length,4));
  for (var i=0,sz=timeline.length;i<sz;++i)
  { 
    stream.write(BinarizeTimelineEvent(timeline[i]));
  }
}

function ExportBundle(bundle)
{ 
  FileIO.SaveFileStream(bundle.filename,function(stream){
    stream.write(Bin.Num(VERSION,4));
    for (var i=0,sz=bundle.timelines.length;i<sz;++i)
    { 
      WriteTimeline(stream,bundle.timelines[i]);
    }
  },function(){});
}

function NextTimelineBundle()
{ 
  //check if current bundle is empty and create
  if (activeBundle == undefined)
  { 
    var zeroStr = '';
    for (var i=1;i<TIMELINE_FILE_NUM_DIGITS;++i) zeroStr += '0';

    var extension = '.t'+(zeroStr+nextBundleNumber).slice(-TIMELINE_FILE_NUM_DIGITS);
    activeBundle = { filename: bundleBasePath+extension, timelines: [] };
    ++nextBundleNumber;
  }
  return activeBundle;
}

function AddToBundle(bundle, timeline)
{ 
  bundle.timelines.push(timeline);

  if (bundle.timelines.length >= GetTimelinesPerFile())
  { 
    ExportBundle(activeBundle);
    activeBundle = undefined; 
  }
}

function FilterTimeline(timeline)
{ 
  var timelineLimit = exportParams.timelineDetail != undefined? GetLimitFromDetail(exportParams.timelineDetail) : NodeNatureData.GLOBAL_GATHER_FULL;
  if (timelineLimit >= NodeNatureData.GLOBAL_GATHER_FULL)
  {
     return timeline;
  }

  var ret = [];
  for (var i=0,sz=timeline.length;i<sz;++i)
  { 
    var element = timeline[i];
    if (element.category < timelineLimit || element.category >= NodeNatureData.GLOBAL_GATHER_FULL) 
    { 
      ret.push(timeline[i]);
    } 
  }
  return ret;
}

function ExportTimeline(timeline)
{
  if (exportParams.timeline == true)
  { 
    AddToBundle(NextTimelineBundle(),FilterTimeline(timeline));
  }
}

function Extract(inputFolder,outputFile,params,doneCallback)
{ 
  exportParams = params;
  bundleBasePath = outputFile;

  FileIO.SearchFolder(inputFolder,ParseFile,function(error){
    if (error) { LogError(error); doneCallback(error); }
    else
    { 
      //Write the last timeline file
      if (activeBundle) ExportBundle(activeBundle);

      //Write final report
      FileIO.SaveFileStream(outputFile,function(stream){

        stream.write(Bin.Num(VERSION,4));
        stream.write(Bin.Num(GetTimelinesPerFile(),4));

        //Export units
        stream.write(Bin.Num(units.length,4));
        for (var i=0,sz=units.length;i<sz;++i)
        { 
          stream.write(BinarizeUnit(units[i]));
        }

        //Export entries
        for (var i=0;i<NodeNatureData.GLOBAL_GATHER_FULL;++i)
        { 
          var finalList = globals[i];
          stream.write(Bin.Num(finalList.length,4));
          for (var k=0;k<finalList.length;++k)
          { 
            stream.write(BinarizeEntry(finalList[k])); 
          }
        }
      },function(error)
      {
        if (error == undefined){ LogProgress("Done!"); }
        doneCallback();
      })
    } 
  })
} 

exports.SetLogFunc = function(func){ logFunc = func; FileIO.SetLogFunc(func); }
exports.Extract = Extract;