# LuaJIT Decompiler v2 GUI

A C# WinForms / .NET 8 GUI wrapper for `luajit-decompiler-v2.exe`.

Current project version: **0.9.0 pre-release**

## Features

- Modern Windows 10 / 11-oriented WinForms interface
- Drag/drop files and folders
- Recursive folder scanning
- Two-stage scan progress: file discovery + bytecode analysis
- LuaJIT 2.0 / 2.1 bytecode header detection
- Per-file Ready / Unsupported / Processing / Success / Failed / Skipped / Cancelled states
- Output-folder selection
- Optional directory-structure preservation
- Sequential batch decompilation
- Cancellation during scanning and decompilation
- Preview of generated Lua
- GUI logging
- Native options: `-f`, `-s`, `-i`, `-m`, `-u`, `-e`
- Product/file version metadata embedded in the Windows executable

## Bundled decompiler

This project includes the native `luajit-decompiler-v2.exe` executable at:

`third_party\luajit-decompiler-v2.exe`

The executable is provided by the upstream
[LuaJIT Decompiler v2](https://github.com/marsinator358/luajit-decompiler-v2)
project by marsinator358 and is distributed under the MIT License.

The upstream license is included at:

`third_party\LICENSE-luajit-decompiler-v2.txt`

See `THIRD_PARTY_NOTICES.md` for additional attribution.

## Pre-release metadata

The project is currently marked as `0.9.0-pre-release`. Before the public v1.0.0 build, update these values in `LuajitDecompilerGui.csproj`:

- `Version` → `1.0.0`
- `FileVersion` → `1.0.0.0`
- `AssemblyVersion` → `1.0.0.0`
- `InformationalVersion` → `1.0.0`

## Architecture note

The GUI invokes the native decompiler one file at a time so it can track progress, preserve paths, cancel batches, and determine per-file success independently.

## License

LuaJIT Decompiler v2 GUI is licensed under the MIT License.
See `LICENSE.md`.

The bundled LuaJIT Decompiler v2 executable is third-party software
distributed under its own MIT License.
See `THIRD_PARTY_NOTICES.md`.