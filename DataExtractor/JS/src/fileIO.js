var fs = require('fs');
var path = require('path');

var extension = 'json';
var batchMaxSize = 100; 

var logFunc = function(level,data){}; //dummy function

function LogError(data)    { logFunc(0,data); }
function LogProgress(data) { logFunc(1,data); }
function LogInfo(data)     { logFunc(2,data); }

function IsExtensionFile(file,extension)
{
  return (file.split('.').pop()) == extension;
}

function ParseFile(file,parseCallback,doneCallback)
{ 
  fs.readFile(file, 'utf8', function(err, data){
    if (err){ doneCallback(err); LogError(err); }
    else{ parseCallback(file,data); }
    doneCallback();
  });
}

function OpenNextFileBatch(files,parseCallback,doneCallback)
{ 
  if (files.length == 0)
  { 
    doneCallback(); 
  }
  else 
  { 
    var batchSize = Math.min(batchMaxSize,files.length); 
    var batch = files.slice(0,batchSize);
    files.splice(0,batchSize); 

    LogProgress('Open file batch of '+batchSize+' files (remaining '+files.length+')');

    var pending = batchSize; 
    for (var i=0,sz=batch.length;i<sz;++i)
    { 
      ParseFile(batch[i],parseCallback,function(err){
        if (err){ doneCallback(err); return; }
        if (!--pending) 
        {
          OpenNextFileBatch(files,parseCallback,doneCallback);
        }
      })
    }
  }
}

function OpenFiles(files,parseCallback,doneCallback)
{ 
  OpenNextFileBatch(files,parseCallback,doneCallback);
}

function SearchSingleDir(dir,folders,files,doneCallback)
{
  LogInfo('Searching folder '+ dir);
  fs.readdir(dir, function(err, list) {
    if (err) { doneCallback(err); return; }
    var pending = list.length;
    if (pending == 0) doneCallback();
    list.forEach(function(file) {
      file = path.resolve(dir, file);
      fs.stat(file, function(err, stat) {
        if (err) doneCallback(err);
        if (stat && stat.isDirectory()) { 
          folders.push(file); 
          if (!--pending) doneCallback();
        }
        else
        {
          if (IsExtensionFile(file,extension)) files.push(file);
          if (!--pending) doneCallback();
        };
      });
    });
  });

}

function SearchNextFolder(folders,files,doneCallback)
{ 
  if (folders.length == 0)
  {
     doneCallback();
  }
  else 
  { 
    //parse that dir and add new folders and files
    let dir = folders[0];
    folders.splice(0, 1);
    SearchSingleDir(dir,folders,files,function(){ SearchNextFolder(folders,files,doneCallback); });
  }
}

function SearchFolder(path,parseCallback,doneCallback)
{ 
  var files = []; 
  var folders = [path];

  LogProgress('Scanning folder ' + path + '...')

  SearchNextFolder(folders,files,function(error){
    if(error) { doneCallback(error); }
    else { OpenFiles(files,parseCallback,doneCallback); }
  });
}

function SaveFileStream(file,writeCallback,doneCallback)
{ 
  var stream = fs.createWriteStream(file);
  stream.once('open', function(fd) {
    writeCallback(stream);
    stream.end(doneCallback);
  });
}

exports.SetLogFunc     = function(func){ logFunc = func; }
exports.SearchFolder   = SearchFolder; 
exports.SaveFileStream = SaveFileStream;