using System;
using System.IO;

namespace ProgramManager;

/// <summary>
/// Command-line entry point for ProgramManager.
/// This version avoids any Windows specific UI and
/// allows running the core logic in a cross-platform
/// manner, which is helpful for Codex based testing.
/// </summary>
public static class CliProgram
{
    public static void Main(string[] args)
    {
        var stateManager = new StateManager();
        var state = stateManager.LoadState();
        Console.WriteLine($"Loaded {state.ResultsCount} results.");

        if (args.Length > 0 && File.Exists(args[0]))
        {
            Console.WriteLine($"Loading alternate state file: {args[0]}");
            stateManager = new StateManager(stateFilePath: args[0]);
            state = stateManager.LoadState();
            Console.WriteLine($"Loaded {state.ResultsCount} results from {args[0]}.");
        }
    }
}
