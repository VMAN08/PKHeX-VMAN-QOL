PKHeX - VMAN QOL Fork
=====
<div>
  <span>English</span> / <a href=".github/README-es.md">Español</a> / <a href=".github/README-fr.md">Français</a> / <a href=".github/README-de.md">Deutsch</a> / <a href=".github/README-it.md">Italiano</a> / <a href=".github/README-ko.md">한국어</a> / <a href=".github/README-zh-Hant.md">繁體中文</a> / <a href=".github/README-zh-Hans.md">简体中文</a>
</div>

![License](https://img.shields.io/badge/License-GPLv3-blue.svg)
![Release](https://img.shields.io/github/v/release/VMAN08/PKHeX-VMAN-QOL?label=Latest%20Release)

> **🎯 Custom Fork Notice:** This is a quality-of-life enhanced fork of the original [PKHeX by Kaphotics](https://github.com/kwsch/PKHeX) with additional multi-Pokémon selection and drag-drop features. See [Custom Features](#custom-features) below for details.

Pokémon core series save editor, programmed in [C#](https://en.wikipedia.org/wiki/C_Sharp_%28programming_language%29).

Supports the following files:
* Save files ("main", \*.sav, \*.dsv, \*.dat, \*.gci, \*.bin)
* GameCube Memory Card files (\*.raw, \*.bin) containing GC Pokémon savegames.
* Individual Pokémon entity files (.pk\*, \*.ck3, \*.xk3, \*.pb7, \*.sk2, \*.bk4, \*.rk4)
* Mystery Gift files (\*.pgt, \*.pcd, \*.pgf, .wc\*) including conversion to .pk\*
* Importing GO Park entities (\*.gp1) including conversion to .pb7
* Importing teams from Decrypted 3DS Battle Videos
* Transferring from one generation to another, converting formats along the way.

Data is displayed in a view which can be edited and saved.
The interface can be translated with resource/external text files so that different languages can be supported.

Pokémon Showdown sets and QR codes can be imported/exported to assist in sharing.

PKHeX expects save files that are not encrypted with console-specific keys. Use a savedata manager to import and export savedata from the console ([Checkpoint](https://github.com/FlagBrew/Checkpoint), save_manager, [JKSM](https://github.com/J-D-K/JKSM), or SaveDataFiler).

**We do not support or condone cheating at the expense of others. Do not use significantly hacked Pokémon in battle or in trades with those who are unaware hacked Pokémon are in use.**

## Screenshots

![Main Window](https://i.imgur.com/pIHdoTp.png)

## Custom Features

This fork adds enhanced multi-Pokémon selection and drag-drop functionality to improve workflow efficiency when organizing boxes:

### Multi-Selection System
- **Ctrl+Click** on any box slot to toggle multi-selection
- Selected Pokémon display a **blue border** for clear visual feedback
- Can only select Pokémon from the same box (prevents cross-box confusion)
- Selection automatically clears when switching boxes or after completing a drag operation

### Multi-Drag Operations
Once multiple Pokémon are selected, drag them together with modifier keys:

| Action | Description |
|--------|-------------|
| **Drag** | Move all selected Pokémon to destination sequentially |
| **Shift+Drag** | Clone all selected Pokémon (sources remain intact) |
| **Alt+Drag** | Overwrite occupied destination slots |
| **Alt+Shift+Drag** | Clone AND overwrite (best of both worlds) |

### Improved Slot Interaction
- **Click** any slot to view the Pokémon in the editor
- Drag threshold uses proper Windows system settings (no more accidental drags)
- Visual distinction between selection (blue border) and keyboard focus (dotted border)

### Technical Implementation
- **New Class:** `SlotSelectionManager` - Centralized multi-selection state management
- **New Enum Value:** `DropModifier.CloneAndOverwrite` - Supports Alt+Shift modifier combination
- **Enhanced Components:** `SelectablePictureBox`, `SlotChangeManager`, `BoxEditor`, `DragManager`
- **Updated Documentation:** Shortcuts and changelog reflect all new features

### Why This Fork?
The original PKHeX only supports dragging one Pokémon at a time. This fork addresses the tedious workflow of reorganizing many Pokémon by allowing batch operations while maintaining full compatibility with the original PKHeX save file format.

---

## Building

PKHeX is a Windows Forms application which requires [.NET 10](https://dotnet.microsoft.com/download/dotnet/10.0).

The executable can be built with any compiler that supports C# 14.

### Build Configurations

Use the Debug or Release build configurations when building. There isn't any platform specific code to worry about!

## Dependencies

PKHeX's QR code generation code is taken from [QRCoder](https://github.com/codebude/QRCoder), which is licensed under [the MIT license](https://github.com/codebude/QRCoder/blob/master/LICENSE.txt).

PKHeX's shiny sprite collection is taken from [pokesprite](https://github.com/msikma/pokesprite), which is licensed under [the MIT license](https://github.com/msikma/pokesprite/blob/master/LICENSE).

PKHeX's Pokémon Legends: Arceus sprite collection is taken from the [National Pokédex - Icon Dex](https://www.deviantart.com/pikafan2000/art/National-Pokedex-Version-Delta-Icon-Dex-824897934) project and its abundance of collaborators and contributors.

### IDE

PKHeX can be opened with IDEs such as [Visual Studio](https://visualstudio.microsoft.com/downloads/) by opening the .sln or .csproj file.
