function ConcatTypedArrays(a, b) { // a, b TypedArray of same type
  var c = new (a.constructor)(a.length + b.length);
  c.set(a, 0);
  c.set(b, a.length);
  return c;
}

function BinarizeNumber(val,size) {
  var val = Math.min(val,(2 ** (8*size))-1); //Avoid overflows
  var array = new Uint8Array(size);
  for ( var index = 0; index < array.length; index ++ ) {
      var byte = val & 0xff;
      array[ index ] = byte;
      val = (val - byte) / 256 ;
  }
  return array;
};

function StringToArrayBuffer(str,length){
  var arr = new Uint8Array(length);
  for(var i=0; i<length; ++i) arr[i] = str.charCodeAt(i);
  return arr;
}

function BinarizeStr(str)
{
  const StringMaxSize = 127;
  var finalLen = Math.min(str.length,StringMaxSize);
  var len = BinarizeNumber(finalLen,1);
  return ConcatTypedArrays(len,StringToArrayBuffer(str,finalLen));
}

exports.Str = BinarizeStr;
exports.Num = BinarizeNumber;
exports.Concat = ConcatTypedArrays;