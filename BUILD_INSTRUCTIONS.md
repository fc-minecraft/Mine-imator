# Building Mine-imator
This document describes how to build Mine-imator from source on Windows, Linux, and macOS.

## Prerequisites
* **.NET 8.0 SDK**: Required to build the `CppGen` tool.
* **CMake 3.16+**: Required for configuring the C++ project.
* **Qt 5.15.2**: Required framework for the application.
* **C++ Compiler**:
  * Windows: MSVC (Visual Studio 2019/2022)
  * Linux: GCC or Clang
  * macOS: Clang (Xcode)

## Build Process Overview
The build process consists of two main steps:
1. **Transpilation**: Converting the GameMaker Language (GML) source code (`GmProject`) into C++ code using the custom `CppGen` tool.
2. **Compilation**: Compiling the generated C++ code and the static C++ codebase (`CppProject`) into the final executable using CMake and Qt.

## Detailed Build Instructions

### 1. Build CppGen
First, you need to build the C# tool that converts GML to C++.

```bash
cd CppGen
dotnet build -c Release
```
This will create `CppGen/bin/Release/net8.0/CppGen.dll` (or `CppGen.exe` on Windows).

### 2. Run CppGen
Run the tool to generate the C++ source files. You must specify the paths to the GameMaker project and the C++ project.

```bash
# From the root of the repository
dotnet run --project CppGen/CppGen/CppGen.csproj -c Release -- -game "GmProject" -cpp "CppProject" -gml "CppGen/CppGen/gml.json"
```

Or if you built the binary:
```bash
dotnet CppGen/CppGen/bin/Release/net8.0/CppGen.dll -game "GmProject" -cpp "CppProject" -gml "CppGen/CppGen/gml.json"
```

This will populate `CppProject/Generated`, `CppProject/Asset/Sprites`, and `CppProject/Asset/Shaders`.

### 3. Build the C++ Project

#### Linux / macOS
```bash
# Create build directory
cmake -S CppProject -B build -DCMAKE_BUILD_TYPE=Release

# Build
cmake --build build --config Release
```

#### Windows
You can use CMake GUI or the command line.
```cmd
cmake -S CppProject -B build -DCMAKE_BUILD_TYPE=Release
cmake --build build --config Release
```

### 4. Running the Application
After a successful build, the executable will be in `build/Release/` (Windows) or `build/` (Linux/macOS).
You need to ensure the `Data` folder is available next to the executable.

```bash
# Example for Linux
cp -r GmProject/datafiles/Data build/
./build/Mine-imator
```

## Troubleshooting
* **"Could not find gml.json"**: Ensure you pass the correct path to `gml.json` using the `-gml` argument to `CppGen`.
* **"No GameMaker project found"**: Ensure the `-game` path points to the directory containing the `.yyp` file.
* **Missing Qt modules**: Ensure you have `qtcharts` and `qtwebengine` installed.
