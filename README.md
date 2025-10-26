# FindClassesImplementingInterface.NET

Console utility that searches .NET assemblies for types implementing a specified interface and writes the results to JSON.

## Solution layout

- **FindClassesImplementingInterface.App** – console host. It reads search parameters from a JSON file, walks through the target directory, and invokes a worker process for each matching assembly file.
- **FindClassesImplementingInterface.Scanner** – worker process. It loads the assembly using `MetadataLoadContext` and emits the full names of types that implement the requested interface.
- **FindClassesImplementingInterface.Core** – shared library with DTOs and helpers used by both executables.

## Requirements

- .NET SDK 9.0 or later on Windows (the application is designed for Windows execution, although the code can be built elsewhere).

## Preparing the parameter file

The main application does not accept command-line arguments. Instead, it expects `FindClassesImplementingInterface.NET.parameters.json` to be placed next to the executable (`FindClassesImplementingInterface.App.exe`). The file is not part of the project and must be created manually before running the program.

See `FindClassesImplementingInterface.NET.parameters.sample.json` for a sample configuration:

```json
{
  "searchFolder": "C:/Path/To/Assemblies",
  "fileSearchPattern": "*.dll",
  "interfaceName": "InsERT.Mox.Validation.IDataError"
}
```

Parameter description:

- `searchFolder` – root directory that will be scanned recursively.
- `fileSearchPattern` – file mask applied when enumerating assemblies, for example `Alpha*.dll`.
- `interfaceName` – fully-qualified interface name to locate inside assemblies.

## Running the application

1. Build the solution in Release mode:
   ```bash
   dotnet build -c Release
   ```
2. Copy `FindClassesImplementingInterface.NET.parameters.json` next to the built executable (by default located in `src/FindClassesImplementingInterface.App/bin/Release/net9.0/`).
3. Launch the application without arguments:
   ```bash
   FindClassesImplementingInterface.App.exe
   ```

Each matching assembly is scanned by a dedicated `FindClassesImplementingInterface.Scanner.exe` process so that memory is released immediately after the analysis completes.

## Output

Results are written to `FindClassesImplementingInterface.NET.output.json` next to the executable. The JSON file contains an ordered list of entries with the following properties:

- `fileName` – path to the assembly relative to `searchFolder`.
- `typeName` – fully qualified name of a type implementing the requested interface.
