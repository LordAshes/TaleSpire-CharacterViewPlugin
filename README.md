# CharacterView Plugin

This unofficial TaleSpire plugin is for viewing the map from a character point of view.

## Install

Go to the releases folder and download the latest and extract to the contents of your TaleSpire game folder.

## Usage

Select a character and press the ? key to activate Character View and see the map from the selected characters point of view.
Press the ? key again to switch to normal view. It is recommended not to move the camera around when in character view.
Note: Post processing is turned off while in Character View mode to elimninate fuzzy views. It is turned back on when returning to Normal View.

## How to Compile / Modify

Open ```CharacterView.sln``` in Visual Studio.

You will need to add references to:

```
* BepInEx.dll  (Download from the BepInEx project.)
* Bouncyrock.TaleSpire.Runtime (found in Steam\steamapps\common\TaleSpire\TaleSpire_Data\Managed)
* UnityEngine.dll
* UnityEngine.CoreModule.dll
* UnityEngine.InputLegacyModule.dll 
```

Build the project.

Browse to the newly created ```bin/Debug``` or ```bin/Release``` folders and copy the ```CharacterViewPlugin.dll``` to ```Steam\steamapps\common\TaleSpire\BepInEx\plugins```
