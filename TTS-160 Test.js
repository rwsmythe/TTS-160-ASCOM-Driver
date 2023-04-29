//*** CHECK THIS ProgID ***
var X = new ActiveXObject("ASCOM.TTS-160.Telescope");
WScript.Echo("This is " + X.Name + ")");
// You may want to uncomment this...
// X.Connected = true;
X.SetupDialog();
