var FileIO = require('./fileIO.js')

var path = require('path');

var sources = {};

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
    targetContainer.push({ name: key, min: entry.min, max: entry.max, num: entry.num, avg: Math.round((entry.acc*100)/entry.num)/100 });
  }
}

function AddToDatabase(events)
{ 
  for (var i=0,sz=events.length;i<sz;++i)
  {
    var element = events[i];

    //Check for includes 
    if (element.tid == 0 && element.name == 'Source')
    {
      FillDatabaseData(sources,path.basename(element.args.detail),element.dur);
    }
  }
}

function ParseFile(file,content)
{ 
  if (content.startsWith('{"traceEvents":') || content.startsWith('{ "traceEvents":'))
  {
    console.log('PARSING: '+file);

    var obj = JSON.parse(content);
    if (obj.traceEvents)
    {
      AddToDatabase(obj.traceEvents);
    }
  }
}

function GenerateExportFile(list)
{ 
  var ret = '';
  for (var i=0;i<list.length;++i)
  { 
    var entry = list[i];
    ret += entry.name+':'+entry.avg+':'+entry.min+':'+entry.max+':'+entry.num+'\n';
  }
  return ret;
}

function Extract(inputFolder,outputFile,doneCallback)
{ 
  FileIO.SearchFolder(inputFolder,ParseFile,function(error){
    if (error) { console.log(error); doneCallback(error); }
    else
    { 
      var finalList = [];
      FinalizeDatabaseData(sources,finalList);
  
      var str = GenerateExportFile(finalList);
  
      FileIO.SaveFile(outputFile,str,function(error){
        if (error){ console.log(error); doneCallback(error); }
        else doneCallback();
      })
    } 
  })
} 

exports.Extract = Extract;