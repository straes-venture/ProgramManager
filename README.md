# ProgramManager

This repository now provides a cross-platform command-line interface for working with
saved state and settings, while keeping the original Windows Forms user interface
restricted to Windows builds.

## CLI Usage

Run the CLI application to load the default `state.json` and print the number of
stored results:

```bash
dotnet run --framework net8.0 --project ProgramManager.csproj
```

You can also supply a custom state file path:

```bash
dotnet run --framework net8.0 --project ProgramManager.csproj /path/to/other-state.json
```

## Windows UI

When targeting Windows, the classic WinForms interface is compiled. Use the
`net8.0-windows` target framework to build and run the UI:

```bash
dotnet run --framework net8.0-windows --project ProgramManager.csproj
```
