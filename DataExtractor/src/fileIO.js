var fs = require('fs');
var path = require('path');

var extension = 'json';

function IsExtensionFile(file,extension)
{
  var extensionLength = extension.length+1;
  return file.length > extensionLength && file.indexOf('.'+extension) == file.length-extensionLength;
}

function ParseFile(file,parseCallback,doneCallback)
{ 
  fs.readFile(file, 'utf8', function(err, data){
    if (err){ doneCallback(err); console.log(err); }
    else{ parseCallback(file,data); }
    doneCallback();
  });
}

function OpenFiles(files,parseCallback,doneCallback)
{ 
  //TODO ~ limit this to a max open files to avoid nodejs crashing for too many files open.

  var pending = files.length;
  for (var i=0,sz=files.length;i<sz;++i)
  { 
    ParseFile(files[i],parseCallback,function(err){
      if (err){ doneCallback(err); return; }
       if (!--pending) doneCallback();
    })
  }
}

function SearchSingleDir(dir,folders,files,doneCallback)
{
  console.log('Searching folder '+ dir);
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

  console.log('Scanning folder ' + path + '...')

  SearchNextFolder(folders,files,function(error){
    if(error) { doneCallback(error); }
    else { OpenFiles(files,parseCallback,doneCallback); }
  });
}

function SaveFile(file,content,doneCallback)
{ 
  fs.writeFile(file, content, 'utf8', doneCallback);
}

exports.SearchFolder = SearchFolder; 
exports.SaveFile     = SaveFile;