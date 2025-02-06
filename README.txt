Minimum supported Unity version: 2018.1
Current project version: v2.1

Documentation is located in the folder Assets/PictureColoring/Documentation

If you run into any issues/bugs please email us at support@bizzybeegames.com

Thanks for purchasing!

*** Version Changes ***

v2.1 [Current]
- Fixed bug where coloring one region would also color a second region.
- Fixed bug where the level creator would sometimes get stuck and not progress or generate the level files.
- Fixed exception on the level creator window when creating levels with the single import mode.

v2.0
- Completely re-engineered how level files are generated and displayed in the game. Pictures are no longer displayed using Texture2Ds, instead level files contain vector/triangle information which is used to display images using a custom UI component. This drastically lowers the amount of memory used and makes level loading almost instant. NOTE: Level files will have to be re-generated with this update. Also any previously generated level PNG files in the Resources folder can be deleted.
- Fixed small sound bug where sound would turn back on if turned off previously.

v1.1
- Fixed hint bug where it was zooming in on the wrong location for some devices.
- Added check for low memory warnings, when a low memory warning happens the game will lower the max amount of cached images so devices with less memory available will not crash.

v1.0
First release