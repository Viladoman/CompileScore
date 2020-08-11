

//Binarization
function ValidateStringSize(str) {
  const StringMaxSize = 127;
  if (str.length > StringMaxSize) {
      throw new Error("Messages cannot be longer than " + UInt32MaxSize);
  }
}

function ConcatTypedArrays(a, b) { // a, b TypedArray of same type
  var c = new (a.constructor)(a.length + b.length);
  c.set(a, 0);
  c.set(b, a.length);
  return c;
}

function stringToArrayBuffer(str){
  if(/[\u0080-\uffff]/.test(str)){
      throw new Error("this needs encoding, like UTF-8");
  }
  var arr = new Uint8Array(str.length);
  for(var i=str.length; i--; )
      arr[i] = str.charCodeAt(i);
  return arr;
}

function BinarizeNumber(val,size) {
  // TODO ~ ramonv ~ check for out of bounds number
  // we want to represent the input as a 8-bytes array
  var array = new Uint8Array(size);
  for ( var index = 0; index < array.length; index ++ ) {
      var byte = val & 0xff;
      array[ index ] = byte;
      val = (val - byte) / 256 ;
  }
  return array;
};

function BinarizeStr(str)
{
  //TODO ~ ramonv ~ Clamp string length if too long

  ValidateStringSize(str);
  var len = BinarizeNumber(str.length,1);
  return ConcatTypedArrays(len,stringToArrayBuffer(str));
}

exports.Str = BinarizeStr;
exports.Num = BinarizeNumber;
exports.Concat = ConcatTypedArrays;