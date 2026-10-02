# LuaJIT Decompiler v2 GUI

A modern Windows 10/11 C# WinForms front-end for [`luajit-decompiler-v2.exe`](https://github.com/marsinator358/luajit-decompiler-v2).


## Features

- Drag/drop individual files or entire folders
- Recursive folder scanning with discovery and bytecode-analysis progress
- LuaJIT 2.0 / 2.1 bytecode header detection
- Per-file Ready / Unsupported / Processing / Success / Failed / Skipped / Cancelled states
- Output-folder selection and optional directory-structure preservation
- Sequential batch decompilation with cancellation
- Preview of generated Lua
- GUI logging and error status
- Native options: `-f`, `-s`, `-i`, `-m`, `-u`, `-e`
- Open output folder directly from the GUI
- Right-click result actions for preview, open, reveal, copy paths, and remove
- Double-click a successful result to open the generated Lua file
- Remembers the last output folder, options, and window size/state
- Product/file version metadata embedded in the Windows executable

## Bundled decompiler

This project uses and may distribute the native decompiler at:

`third_party\luajit-decompiler-v2.exe`

The executable is provided by the upstream [LuaJIT Decompiler v2](https://github.com/marsinator358/luajit-decompiler-v2) project by marsinator358 and is distributed under the MIT License.

The complete upstream license is included at:

`third_party\LICENSE-luajit-decompiler-v2.txt`

See `THIRD_PARTY_NOTICES.md` for additional attribution.

## Build

Requires the .NET 8 SDK.

```powershell
cd LuajitDecompilerGui
dotnet restore
dotnet build
dotnet run
```

The repository includes `global.json` so the project uses the .NET 8 SDK even when newer SDKs are also installed.

## Publish a self-contained Windows x64 build

From `LuajitDecompilerGui`:

```powershell
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true -o .\publish\win-x64
```

The publish folder will contain the GUI executable together with the bundled native decompiler and license/notice files.

## Architecture

The GUI invokes the native decompiler one file at a time. This gives the application reliable per-file progress, cancellation, output-path control, directory preservation, and success/failure tracking.

Files are classified from their contents rather than only from their extensions. LuaJIT bytecode starts with the `1B 4C 4A` signature followed by the bytecode version byte (`01` for LuaJIT 2.0 and `02` for LuaJIT 2.1).

## License

LuaJIT Decompiler v2 GUI is licensed under the MIT License. See `LICENSE.md`.

The bundled LuaJIT Decompiler v2 executable is third-party software distributed under its upstream MIT License. See `THIRD_PARTY_NOTICES.md` and `third_party\LICENSE-luajit-decompiler-v2.txt`.

This GUI project is independently developed and is not affiliated with or endorsed by the upstream LuaJIT Decompiler v2 author.
