#region license
// UOSagas Razor: An Ultima Online Assistant for the UOSagas shard
// Copyright (C) 2026 UOSagas (3HMonkey)
//
// Based on Razor: An Ultima Online Assistant
// Copyright (c) 2022 Razor Development Community on GitHub <https://github.com/markdwags/Razor>
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with this program.  If not, see <http://www.gnu.org/licenses/>.
#endregion

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Assistant.LuaEngine.API;
using Assistant.VScripts.Core;
using Lua;
using Lua.Standard;

namespace Assistant.LuaEngine;

/// <summary>
/// New Lua Engine Service using Lua-CSharp (pure managed implementation)
/// Replaces the old KeraLua-based implementation for better stability and AOT compatibility
/// </summary>
public static class LuaEngineService
{
    // Lua state
    private static LuaState _state;
    private static CancellationTokenSource _cancellationTokenSource;
    private static Task _executionTask;

    // Script state
    private static bool _isRunning;
    private static bool _isPaused;
    private static bool _loopScript;
    private static int _currentLine = -1;
    private static string _currentScriptContent;

    // Debugging
    private static readonly HashSet<int> _breakpoints = new HashSet<int>();
    private static bool _stepMode;
    private static bool _stepRequested;
    private static readonly Dictionary<string, LuaValue> _watchedVariables = new Dictionary<string, LuaValue>();
    private static readonly List<LuaCallStackFrame> _callStack = new List<LuaCallStackFrame>();
    private static readonly ManualResetEventSlim _pauseEvent = new ManualResetEventSlim(true); // Initially signaled (not paused)

    // File management
    private const string FILETYPE = "lua";
    private static string _rootPath;

    // Error tracking
    private static readonly List<LuaError> _currentErrors = new List<LuaError>();

    // Events
    public static event EventHandler<bool> RunningStateChanged;
    public static event EventHandler<int> CurrentLineChanged;
    public static event EventHandler<List<LuaError>> ScriptErrorsChanged;
    public static event EventHandler<List<LuaCallStackFrame>> CallStackChanged;
    public static event EventHandler<Dictionary<string, string>> VariablesChanged;

    // Properties
    public static Dictionary<string, string> Files { get; private set; }
    public static Tuple<string, string, bool> CurrentFile { get; set; }
    public static bool IsRunning => _isRunning;
    public static bool IsPaused => _isPaused;
    public static bool LoopScript { get => _loopScript; set => _loopScript = value; }
    public static int CurrentLine => _currentLine;
    public static IReadOnlyCollection<int> Breakpoints => _breakpoints;
    public static IReadOnlyList<LuaError> Errors => _currentErrors;

    public static string GetScriptRootPath() => _rootPath;
    public static string GetScriptFileType() => FILETYPE;

    /// <summary>
    /// Initialize the Lua engine
    /// </summary>
    public static void Initialize()
    {
        Console.WriteLine("[LuaEngine] Initializing Lua-CSharp engine...");

        // Create the Lua state
        _state = LuaState.Create();

        // Open standard libraries (limited set for sandboxing)
        OpenSandboxedLibraries(_state);

        // Register all API bindings
        RegisterBindings(_state);

        // Initialize file management
        Files = new Dictionary<string, string>();

        // Razor: Lua-Scripts leben in der RAZOR-Ordnerstruktur
        // (<Plugin>/Data/LuaScripts), nicht mehr im Data-Ordner des Clients.
        // Austausch mit dem integrierten Assistant = Datei kopieren.
        try
        {
            _rootPath = Config.GetInstallDirectory("LuaScripts");
        }
        catch
        {
            // Config nicht initialisiert (Harness/Tests) — relativer Fallback.
            _rootPath = Path.Combine("Data", "LuaScripts");
        }

        Directory.CreateDirectory(_rootPath);
        GetFileNamesWithoutExtension();

        Console.WriteLine("[LuaEngine] Initialization complete");
    }

    /// <summary>
    /// Open only safe standard libraries
    /// </summary>
    private static void OpenSandboxedLibraries(LuaState state)
    {
        // Only open safe libraries - no io, os.execute, etc.
        state.OpenStandardLibraries();

        // Remove dangerous functions
        state.Environment["dofile"] = LuaValue.Nil;
        state.Environment["loadfile"] = LuaValue.Nil;
        state.Environment["load"] = LuaValue.Nil;
        state.Environment["require"] = LuaValue.Nil;
        state.Environment["module"] = LuaValue.Nil;
        state.Environment["package"] = LuaValue.Nil;
        state.Environment["io"] = LuaValue.Nil;
        state.Environment["rawget"] = LuaValue.Nil;
        state.Environment["rawset"] = LuaValue.Nil;

        // Sandbox debug library - keep only safe functions needed for debugging
        var debug = state.Environment["debug"];
        if (debug.Type == LuaValueType.Table)
        {
            var debugTable = debug.Read<LuaTable>();
            // Keep: getinfo, traceback (needed for debugging)
            // Remove: dangerous functions that can modify execution
            debugTable["setfenv"] = LuaValue.Nil;
            debugTable["setlocal"] = LuaValue.Nil;
            debugTable["setupvalue"] = LuaValue.Nil;
            debugTable["setmetatable"] = LuaValue.Nil;
            debugTable["sethook"] = LuaValue.Nil; // Users shouldn't set their own hooks
            debugTable["debug"] = LuaValue.Nil;   // Interactive debugger
        }

        // Sandbox os library - only allow safe functions
        var os = state.Environment["os"];
        if (os.Type == LuaValueType.Table)
        {
            var osTable = os.Read<LuaTable>();
            // Remove dangerous os functions
            osTable["execute"] = LuaValue.Nil;
            osTable["remove"] = LuaValue.Nil;
            osTable["rename"] = LuaValue.Nil;
            osTable["exit"] = LuaValue.Nil;
            osTable["setlocale"] = LuaValue.Nil;
            osTable["tmpname"] = LuaValue.Nil;
            osTable["getenv"] = LuaValue.Nil;
        }
    }

    /// <summary>
    /// Register all API bindings
    /// </summary>
    private static void RegisterBindings(LuaState state)
    {
        // Register core functions
        RegisterCoreFunctions(state);

        // Register Player API
        LuaPlayerAPI.Register(state);

        // Register Items API
        LuaItemsAPI.Register(state);

        // Register Mobiles API
        LuaMobilesAPI.Register(state);

        // Register Gumps API
        LuaGumpsAPI.Register(state);

        // Register Messages API
        LuaMessagesAPI.Register(state);

        // Register Spells API
        LuaSpellsAPI.Register(state);

        // Register Targeting API
        LuaTargetingAPI.Register(state);

        // Register Skills API
        LuaSkillsAPI.Register(state);

        // Register Journal API
        LuaJournalAPI.Register(state);

        // Register Config API (Razor-Zusatz: Script-Einstellungen speichern/laden)
        LuaConfigAPI.Register(state);

        // Register UI API
        LuaUIAPI.Register(state);
    }

    /// <summary>
    /// Sandbox + alle API-Bindings auf einem frischen, eigenstaendigen State —
    /// fuer tools/LuaExport (Doku-Inventar) und Tests. Beruehrt weder Dateien
    /// noch Config; die Closures greifen erst zur Laufzeit auf World/Client zu.
    /// </summary>
    public static LuaState CreateStateForExport()
    {
        var state = LuaState.Create();
        OpenSandboxedLibraries(state);
        RegisterBindings(state);
        return state;
    }

    /// <summary>
    /// Helper to get any Lua argument as a string (handles string, number, boolean, nil, table)
    /// </summary>
    private static string GetArgumentAsString(LuaFunctionExecutionContext context, int index)
    {
        if (context.ArgumentCount <= index)
            return "";

        // Try string first (most common)
        try { return context.GetArgument<string>(index); }
        catch { }

        // Try number
        try { return context.GetArgument<double>(index).ToString(); }
        catch { }

        // Try boolean
        try { return context.GetArgument<bool>(index).ToString().ToLower(); }
        catch { }

        // Try table
        try
        {
            context.GetArgument<LuaTable>(index);
            return "[table]";
        }
        catch { }

        // Check for nil
        try
        {
            var val = context.GetArgument<LuaValue>(index);
            if (val.Type == LuaValueType.Nil)
                return "nil";
        }
        catch { }

        return "[unknown]";
    }

    /// <summary>
    /// Register core utility functions
    /// </summary>
    private static void RegisterCoreFunctions(LuaState state)
    {
        // Pause function - native async
        // Razor: pumpt waehrend der Wartezeit die Script-UI (Button-Callbacks,
        // OnChange, Variablen-Bindings) in 50ms-Scheiben — UI-Scripts brauchen
        // dadurch kein manuelles Dispatch, eine Pause-Schleife genuegt.
        state.Environment["Pause"] = new LuaFunction("Pause", async (context, ct) =>
        {
            var milliseconds = context.GetArgument<double>(0);
            var remaining = TimeSpan.FromMilliseconds(milliseconds);

            while (remaining > TimeSpan.Zero)
            {
                TimeSpan slice = remaining > TimeSpan.FromMilliseconds(50)
                    ? TimeSpan.FromMilliseconds(50)
                    : remaining;

                await Task.Delay(slice, ct);
                remaining -= slice;

                await LuaUIAPI.PumpAsync(ct);
            }

            return 0;
        });

        // StopScript function
        state.Environment["StopScript"] = new LuaFunction("StopScript", (context, ct) =>
        {
            StopScript();
            context.Return(true);
            return new ValueTask<int>(1);
        });

        // ExecuteMacro function
        state.Environment["ExecuteMacro"] = new LuaFunction("ExecuteMacro", (context, ct) =>
        {
            try
            {
                var macroName = context.GetArgument<string>(0);
                // Razor: Client fuehrt CUO-Client-Makros aus — hier spielen wir
                // das gleichnamige RAZOR-Makro (.macro) ab.
                var macro = Assistant.Macros.MacroManager.List.Find(m =>
                    string.Equals(m.ToString(), macroName, StringComparison.OrdinalIgnoreCase));

                if (macro == null)
                {
                    Message.Warning($"Macro '{macroName}' not found");
                    context.Return(false);
                    return new ValueTask<int>(1);
                }

                Assistant.Macros.MacroManager.Play(macro);
                context.Return(true);
            }
            catch (Exception e)
            {
                Message.Warning($"Error executing macro: {e.Message}");
                context.Return(false);
            }

            return new ValueTask<int>(1);
        });

        // Import function (secure)
        state.Environment["Import"] = new LuaFunction("Import", async (context, ct) =>
        {
            try
            {
                var scriptPath = context.GetArgument<string>(0);

                // Security checks
                if (string.IsNullOrEmpty(scriptPath))
                {
                    throw new InvalidOperationException("Import: Script path cannot be empty");
                }

                if (scriptPath.Contains("..") || scriptPath.Contains("/") || scriptPath.Contains("\\") ||
                    Path.IsPathRooted(scriptPath))
                {
                    throw new InvalidOperationException("Import: Security violation - script path must be a filename in the same directory");
                }

                // Add .lua extension if not present
                if (!scriptPath.EndsWith(".lua", StringComparison.OrdinalIgnoreCase))
                {
                    scriptPath += ".lua";
                }

                var fullPath = Path.Combine(_rootPath, scriptPath);

                // Security check: ensure path is within scripts directory
                var normalizedFullPath = Path.GetFullPath(fullPath);
                var normalizedScriptsRoot = Path.GetFullPath(_rootPath);

                if (!normalizedFullPath.StartsWith(normalizedScriptsRoot, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException("Import: Security violation - cannot load scripts outside of scripts directory");
                }

                if (!File.Exists(fullPath))
                {
                    throw new FileNotFoundException($"Import: File not found - {scriptPath}");
                }

                var scriptContent = await File.ReadAllTextAsync(fullPath, ct);

                // Execute the imported script
                var results = await _state.DoStringAsync(scriptContent, scriptPath, ct);

                if (results.Length > 0)
                {
                    context.Return(results[0]);
                    return 1;
                }

                return 0;
            }
            catch (Exception e)
            {
                throw new InvalidOperationException($"Import: Error loading script - {e.Message}", e);
            }
        });

        // Print function for debugging - outputs to both message system and debug console
        state.Environment["print"] = new LuaFunction("print", (context, ct) =>
        {
            var args = new List<string>();
            for (int i = 0; i < context.ArgumentCount; i++)
            {
                args.Add(GetArgumentAsString(context, i));
            }
            var message = string.Join("\t", args);
            Message.Info(message);
            DebugConsole.Lua.Info(message);
            return new ValueTask<int>(0);
        });

        // Console output functions (capital C for consistency)
        var consoleTable = new LuaTable();
        state.Environment["Console"] = consoleTable;

        consoleTable["log"] = new LuaFunction("Console.log", (context, ct) =>
        {
            var message = GetArgumentAsString(context, 0);
            DebugConsole.Lua.Info(message);
            return new ValueTask<int>(0);
        });

        consoleTable["info"] = new LuaFunction("Console.info", (context, ct) =>
        {
            var message = GetArgumentAsString(context, 0);
            DebugConsole.Lua.Info(message);
            return new ValueTask<int>(0);
        });

        consoleTable["warn"] = new LuaFunction("Console.warn", (context, ct) =>
        {
            var message = GetArgumentAsString(context, 0);
            DebugConsole.Lua.Warn(message);
            return new ValueTask<int>(0);
        });

        consoleTable["error"] = new LuaFunction("Console.error", (context, ct) =>
        {
            var message = GetArgumentAsString(context, 0);
            DebugConsole.Lua.Error(message);
            return new ValueTask<int>(0);
        });

        consoleTable["debug"] = new LuaFunction("Console.debug", (context, ct) =>
        {
            var message = GetArgumentAsString(context, 0);
            DebugConsole.Lua.Debug(message);
            return new ValueTask<int>(0);
        });

        consoleTable["clear"] = new LuaFunction("Console.clear", (context, ct) =>
        {
            DebugConsole.Lua.Clear();
            return new ValueTask<int>(0);
        });
    }

    /// <summary>
    /// Register the debug line function that will be called on each line
    /// </summary>
    private static void RegisterDebugLineFunction()
    {
        // Register __debug_line function that scripts will call
        _state.Environment["__debug_line"] = new LuaFunction("__debug_line", (context, ct) =>
        {
            try
            {
                // Get line number from argument
                int lineNumber = 1;
                if (context.ArgumentCount >= 1)
                {
                    try
                    {
                        lineNumber = (int)context.GetArgument<double>(0);
                    }
                    catch { }
                }


                // Update current line
                _currentLine = lineNumber;

                // Fire event to update UI
                CurrentLineChanged?.Invoke(null, _currentLine);

                // Check if we should pause
                bool shouldPause = false;

                // Check for breakpoint (convert to 0-based for comparison with editor breakpoints)
                if (_breakpoints.Contains(lineNumber - 1))
                {
                    shouldPause = true;
                    _isPaused = true;
                }

                // Check for step mode - pause after stepping
                if (_stepMode && !_stepRequested)
                {
                    shouldPause = true;
                    _isPaused = true;
                }
                else if (_stepMode && _stepRequested)
                {
                    // This is the line we're stepping to, allow it to execute
                    _stepRequested = false;
                }

                // Check for pause request
                if (_isPaused)
                {
                    shouldPause = true;
                }

                // If we should pause, block until resumed
                if (shouldPause)
                {
                    _isPaused = true;
                    _pauseEvent.Reset(); // Block the event

                    // Fire paused state change to update UI highlighting
                    CurrentLineChanged?.Invoke(null, _currentLine);

                    DebugConsole.Debug($"[Debug] Paused at line {lineNumber}");

                    // Wait for resume (with cancellation support)
                    try
                    {
                        // Wait indefinitely or until cancelled
                        while (true)
                        {
                            bool signaled = _pauseEvent.Wait(100);

                            if (signaled)
                            {
                                break;
                            }

                            ct.ThrowIfCancellationRequested();
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        throw; // Re-throw to stop script
                    }

                    DebugConsole.Debug($"[Debug] Resumed from line {lineNumber}");
                }
                else
                {
                    // Allow cancellation checks
                    ct.ThrowIfCancellationRequested();
                }
            }
            catch (OperationCanceledException)
            {
                throw; // Re-throw to stop script execution
            }
            catch (Exception ex)
            {
                DebugConsole.Debug($"[Debug] Error in debug line: {ex.Message}");
            }

            return new ValueTask<int>(0);
        });
    }

    /// <summary>
    /// Preprocess script to inject debug line calls at the start of each line
    /// </summary>
    private static string InjectDebugCalls(string script)
    {
        if (string.IsNullOrEmpty(script))
            return script;

        var lines = script.Split('\n');
        var result = new System.Text.StringBuilder();

        // Track brace depth to detect when we're inside a table constructor
        int braceDepth = 0;

        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i].TrimEnd('\r');
            var trimmed = line.TrimStart();

            // Calculate brace depth change from previous lines
            // We need to know the depth BEFORE this line to decide if we should inject
            int depthBeforeLine = braceDepth;

            // Update brace depth based on this line's content (excluding strings and comments)
            braceDepth += CountBraceDepthChange(line);

            // Skip injection if we're inside a table constructor (braceDepth > 0 before this line)
            // or if this line opens a table that continues to the next line
            bool insideTableConstructor = depthBeforeLine > 0;

            // Check if this line starts a multi-line table (ends with { but braceDepth increased)
            bool startsMultiLineTable = !insideTableConstructor &&
                                        braceDepth > depthBeforeLine &&
                                        trimmed.EndsWith("{");

            // Skip empty lines, comments, continuation lines, and certain keywords
            bool shouldInject = !insideTableConstructor &&
                               !string.IsNullOrWhiteSpace(trimmed) &&
                               !trimmed.StartsWith("--") &&
                               !trimmed.StartsWith("end") &&
                               !trimmed.StartsWith("else") &&
                               !trimmed.StartsWith("elseif") &&
                               !trimmed.StartsWith("until") &&
                               !trimmed.StartsWith("(") &&      // Continuation: function args
                               !trimmed.StartsWith(")") &&
                               !trimmed.StartsWith("{") &&      // Table constructor start
                               !trimmed.StartsWith("}") &&
                               !trimmed.StartsWith("[") &&      // Array index
                               !trimmed.StartsWith("]") &&
                               !trimmed.StartsWith(".") &&      // Continuation: method chain
                               !trimmed.StartsWith(":") &&      // Continuation: method call
                               !trimmed.StartsWith(",") &&      // Continuation: list/table
                               !trimmed.StartsWith("+") &&      // Continuation: arithmetic
                               !trimmed.StartsWith("-") &&      // Could be continuation or negative number
                               !trimmed.StartsWith("*") &&      // Continuation: arithmetic
                               !trimmed.StartsWith("/") &&      // Continuation: arithmetic
                               !trimmed.StartsWith("..") &&     // Continuation: string concat
                               !trimmed.StartsWith("and ") &&   // Continuation: logical (with space)
                               !trimmed.StartsWith("or ") &&    // Continuation: logical (with space)
                               !trimmed.StartsWith("then") &&   // Part of if statement
                               !trimmed.StartsWith("do") &&
                               !trimmed.StartsWith("'") &&      // String continuation
                               !trimmed.StartsWith("\"");

            if (shouldInject)
            {
                // Get the leading whitespace
                int indent = line.Length - line.TrimStart().Length;
                string whitespace = line.Substring(0, indent);
                string content = line.TrimStart();

                // Insert debug call at the start of the line with semicolon separator
                result.AppendLine($"{whitespace}__debug_line({i + 1}); {content}");
            }
            else
            {
                result.AppendLine(line);
            }
        }

        return result.ToString();
    }

    /// <summary>
    /// Count the net change in brace depth for a line, ignoring braces in strings and comments
    /// </summary>
    private static int CountBraceDepthChange(string line)
    {
        int depth = 0;
        bool inSingleQuoteString = false;
        bool inDoubleQuoteString = false;
        bool inLongString = false;

        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];

            // Check for comment start (ignore rest of line)
            if (!inSingleQuoteString && !inDoubleQuoteString && !inLongString)
            {
                if (i + 1 < line.Length && c == '-' && line[i + 1] == '-')
                {
                    // Check for long comment --[[
                    if (i + 3 < line.Length && line[i + 2] == '[' && line[i + 3] == '[')
                    {
                        inLongString = true;
                        i += 3;
                        continue;
                    }
                    // Regular comment - ignore rest of line
                    break;
                }

                // Check for long string [[
                if (i + 1 < line.Length && c == '[' && line[i + 1] == '[')
                {
                    inLongString = true;
                    i += 1;
                    continue;
                }
            }

            // Check for long string/comment end ]]
            if (inLongString)
            {
                if (i + 1 < line.Length && c == ']' && line[i + 1] == ']')
                {
                    inLongString = false;
                    i += 1;
                }
                continue;
            }

            // Handle string delimiters
            if (c == '\'' && !inDoubleQuoteString)
            {
                // Check for escape
                if (i > 0 && line[i - 1] == '\\')
                    continue;
                inSingleQuoteString = !inSingleQuoteString;
                continue;
            }

            if (c == '"' && !inSingleQuoteString)
            {
                // Check for escape
                if (i > 0 && line[i - 1] == '\\')
                    continue;
                inDoubleQuoteString = !inDoubleQuoteString;
                continue;
            }

            // Count braces only when not in a string
            if (!inSingleQuoteString && !inDoubleQuoteString)
            {
                if (c == '{')
                    depth++;
                else if (c == '}')
                    depth--;
            }
        }

        return depth;
    }

    /// <summary>
    /// Run a script
    /// </summary>
    public static void RunScript(string content)
    {
        if (_isRunning)
        {
            // Razor: laufendes Script stoppen (wie Client mit AutoTerminateScriptsOnTrigger).
            StopScript();
        }

        ClearErrors();
        _currentScriptContent = content;
        _cancellationTokenSource = new CancellationTokenSource();
        _isRunning = true;
        _isPaused = false;
        _currentLine = 1;
        _stepMode = false;
        _stepRequested = false;
        _pauseEvent.Set(); // Ensure not paused at start

        RunningStateChanged?.Invoke(null, true);

        // Log script start
        DebugConsole.Lua.Info("Script started");

        // Register the debug line function
        RegisterDebugLineFunction();

        // Inject debug calls into the script
        string instrumentedScript = InjectDebugCalls(content);

        // Start the script execution in a task
        _executionTask = Task.Run(async () =>
        {
            try
            {
                // Execute the instrumented script
                await _state.DoStringAsync(instrumentedScript, "script", _cancellationTokenSource.Token);

                // Script completed successfully
                OnScriptCompleted();
            }
            catch (LuaParseException ex)
            {
                HandleScriptError($"Syntax error: {ex.Message}", _currentScriptContent);
                OnScriptCompleted();
            }
            catch (LuaRuntimeException ex)
            {
                HandleScriptError($"Runtime error: {ex.Message}", _currentScriptContent);
                OnScriptCompleted();
            }
            catch (OperationCanceledException)
            {
                // Script was cancelled
                Console.WriteLine("[LuaEngine] Script cancelled");
                DebugConsole.Lua.Warn("Script cancelled");
            }
            catch (Exception ex)
            {
                HandleScriptError($"Unexpected error: {ex.Message}", _currentScriptContent);
                OnScriptCompleted();
            }
        }, _cancellationTokenSource.Token);
    }

    /// <summary>
    /// Called when script execution completes (success or error)
    /// </summary>
    private static void OnScriptCompleted()
    {
        _isRunning = false;
        _isPaused = false;
        _currentLine = -1;
        _stepMode = false;
        _stepRequested = false;
        _callStack.Clear();
        _pauseEvent.Set(); // Ensure not blocked

        RunningStateChanged?.Invoke(null, false);
        CurrentLineChanged?.Invoke(null, -1);
        CallStackChanged?.Invoke(null, new List<LuaCallStackFrame>());

        // Clear the debug line function
        try
        {
            if (_state != null)
                _state.Environment["__debug_line"] = LuaValue.Nil;
        }
        catch { }

        // Clear UI callbacks
        API.LuaUIAPI.ClearAllCallbacks();

        // Destroy UI windows
        UI.ScriptUIManager.DestroyAllWindows();

        Console.WriteLine("[LuaEngine] Script completed");
        DebugConsole.Lua.Info("Script ended");
    }

    /// <summary>
    /// Stop the currently running script
    /// </summary>
    public static void StopScript()
    {
        if (!_isRunning)
            return;

        Message.Warning("Script execution was canceled.");

        _cancellationTokenSource?.Cancel();
        _isPaused = false;
        _pauseEvent.Set(); // Unpause to allow cancellation to proceed

        OnScriptCompleted();
    }

    /// <summary>
    /// Pause the currently running script
    /// </summary>
    public static void PauseScript()
    {
        if (!_isRunning || _isPaused)
            return;

        _isPaused = true;
        _pauseEvent.Reset(); // Will pause at next line hook
    }

    /// <summary>
    /// Resume a paused script
    /// </summary>
    public static void ResumeScript()
    {
        if (!_isRunning || !_isPaused)
            return;

        _stepMode = false;
        _isPaused = false;
        _pauseEvent.Set(); // Signal the pause event to continue execution
        Console.WriteLine("[LuaEngine] Script resumed");
    }

    /// <summary>
    /// Step to the next line (debugger)
    /// </summary>
    public static void StepOver()
    {
        if (!_isRunning)
            return;

        _stepMode = true;
        _stepRequested = true;
        _isPaused = false;
        _pauseEvent.Set(); // Allow one step, then pause again
        Console.WriteLine("[LuaEngine] Step over requested");
    }

    /// <summary>
    /// Step into a function (debugger)
    /// </summary>
    public static void StepInto()
    {
        // Same as StepOver for now - full step into would require more complex tracking
        StepOver();
    }

    /// <summary>
    /// Step out of current function (debugger)
    /// </summary>
    public static void StepOut()
    {
        if (!_isRunning)
            return;

        // For now, just continue execution (step out would require call stack depth tracking)
        _stepMode = false;
        _isPaused = false;
        _pauseEvent.Set();
        Console.WriteLine("[LuaEngine] Step out - continuing execution");
    }

    /// <summary>
    /// Toggle a breakpoint on a line
    /// </summary>
    public static void ToggleBreakpoint(int line)
    {
        if (_breakpoints.Contains(line))
        {
            _breakpoints.Remove(line);
        }
        else
        {
            _breakpoints.Add(line);
        }
    }

    /// <summary>
    /// Clear all breakpoints
    /// </summary>
    public static void ClearBreakpoints()
    {
        _breakpoints.Clear();
    }

    /// <summary>
    /// Set breakpoints from a collection
    /// </summary>
    public static void SetBreakpoints(IEnumerable<int> breakpoints)
    {
        _breakpoints.Clear();
        foreach (var bp in breakpoints)
            _breakpoints.Add(bp);
    }

    /// <summary>
    /// Update the call stack for debugging
    /// </summary>
    private static void UpdateCallStack()
    {
        try
        {
            _callStack.Clear();
            // Note: Getting debug info from CallStackFrame requires using LuaDebug API
            // For now, provide a simple placeholder
            // TODO: Implement proper call stack retrieval with LuaDebug

            _callStack.Add(new LuaCallStackFrame
            {
                FunctionName = "(script)",
                Source = "(script)",
                Line = _currentLine
            });

            CallStackChanged?.Invoke(null, new List<LuaCallStackFrame>(_callStack));
        }
        catch
        {
            // Ignore errors during call stack update
        }
    }

    /// <summary>
    /// Update watched variables for debugging
    /// </summary>
    private static void UpdateVariables()
    {
        try
        {
            var variables = new Dictionary<string, string>();

            // Get global variables from environment
            var env = _state.Environment;
            foreach (var pair in env)
            {
                var key = pair.Key;
                var value = pair.Value;

                // Skip functions and built-in tables
                if (value.Type == LuaValueType.Function ||
                    value.Type == LuaValueType.Table ||
                    key.ToString().StartsWith("_"))
                    continue;

                variables[key.ToString()] = FormatLuaValue(value);
            }

            VariablesChanged?.Invoke(null, variables);
        }
        catch
        {
            // Ignore errors during variable update
        }
    }

    /// <summary>
    /// Format a Lua value for display
    /// </summary>
    private static string FormatLuaValue(LuaValue value)
    {
        switch (value.Type)
        {
            case LuaValueType.Nil:
                return "nil";
            case LuaValueType.Boolean:
                return value.Read<bool>().ToString().ToLower();
            case LuaValueType.Number:
                return value.Read<double>().ToString();
            case LuaValueType.String:
                return $"\"{value.Read<string>()}\"";
            case LuaValueType.Table:
                return "{table}";
            case LuaValueType.Function:
                return "{function}";
            case LuaValueType.UserData:
                return "{userdata}";
            case LuaValueType.Thread:
                return "{thread}";
            default:
                return value.ToString();
        }
    }

    /// <summary>
    /// Get the value of a variable for debugging/tooltips
    /// </summary>
    public static string GetVariableValue(string name)
    {
        try
        {
            var value = _state.Environment[name];
            if (value.Type != LuaValueType.Nil)
            {
                return FormatLuaValue(value);
            }

            return null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Get all current variables
    /// </summary>
    public static Dictionary<string, string> GetAllVariables()
    {
        var variables = new Dictionary<string, string>();

        try
        {
            foreach (var pair in _state.Environment)
            {
                var key = pair.Key.ToString();
                var value = pair.Value;

                // Skip functions and internal variables
                if (value.Type == LuaValueType.Function || key.StartsWith("_"))
                    continue;

                variables[key] = FormatLuaValue(value);
            }
        }
        catch
        {
            // Ignore errors
        }

        return variables;
    }

    // File management methods

    public static void GetFileNamesWithoutExtension()
    {
        Files.Clear();

        var normalizedRootPath = _rootPath.Replace('\\', '/');
        if (!normalizedRootPath.EndsWith(Path.DirectorySeparatorChar))
        {
            normalizedRootPath += Path.DirectorySeparatorChar;
        }

        try
        {
            string[] files = Directory.GetFiles(_rootPath, $"*.{FILETYPE}", SearchOption.TopDirectoryOnly);

            foreach (string filePath in files)
            {
                string relativePath = filePath.Substring(normalizedRootPath.Length);
                string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(relativePath);
                Files[fileNameWithoutExtension] = Path.Combine(_rootPath, relativePath);
            }
        }
        catch (Exception ex)
        {
            Message.Error($"An error occurred: {ex.Message}");
        }
    }

    public static bool CreateEmptyLuaFile(string fileName)
    {
        if (string.IsNullOrEmpty(fileName))
        {
            Message.Error("File path cannot be null or empty.");
            return false;
        }

        if (ContainsInvalidChars(fileName))
        {
            Message.Error("File name cannot contain invalid characters.");
            return false;
        }

        var filePath = Path.Combine(_rootPath, fileName);

        if (!Path.GetExtension(filePath).Equals($".{FILETYPE}", StringComparison.OrdinalIgnoreCase))
        {
            filePath = Path.ChangeExtension(filePath, $".{FILETYPE}");
        }

        using (FileStream fs = File.Create(filePath))
        {
            // Empty file
        }

        GetFileNamesWithoutExtension();
        CurrentFile = new Tuple<string, string, bool>(filePath, null, false);
        return true;
    }

    public static void DeleteFile(string fileName)
    {
        if (string.IsNullOrEmpty(fileName))
        {
            Message.Error("File name cannot be null or empty.");
            return;
        }

        string filePath = Path.Combine(_rootPath, fileName);

        if (!Path.GetExtension(filePath).Equals($".{FILETYPE}", StringComparison.OrdinalIgnoreCase))
        {
            filePath = Path.ChangeExtension(filePath, $".{FILETYPE}");
        }

        File.Delete(filePath);
        Files.Remove(fileName);
        GetFileNamesWithoutExtension();

        if (Files.Count > 0)
        {
            CurrentFile = new Tuple<string, string, bool>(Files.Last().Key, null, false);
        }
        else
        {
            CurrentFile = null;
        }
    }

    public static string LoadFile(string fileName)
    {
        if (string.IsNullOrEmpty(fileName))
        {
            Message.Error("File name cannot be null or empty.");
            return string.Empty;
        }

        string filePath = Path.Combine(_rootPath, fileName);

        if (!Path.GetExtension(filePath).Equals($".{FILETYPE}", StringComparison.OrdinalIgnoreCase))
        {
            filePath = Path.ChangeExtension(filePath, $".{FILETYPE}");
        }

        if (!File.Exists(filePath))
        {
            Message.Error("File does not exist. Refreshing directory!");
            GetFileNamesWithoutExtension();
            return string.Empty;
        }

        string content = File.ReadAllText(filePath);
        CurrentFile = new Tuple<string, string, bool>(fileName, content, false);
        return content;
    }

    public static void SaveFile(string fileName, string content)
    {
        if (string.IsNullOrEmpty(fileName))
        {
            Message.Error("File name cannot be null or empty.");
            return;
        }

        string filePath = Path.Combine(_rootPath, fileName);

        if (!Path.GetExtension(filePath).Equals($".{FILETYPE}", StringComparison.OrdinalIgnoreCase))
        {
            filePath = Path.ChangeExtension(filePath, $".{FILETYPE}");
        }

        File.WriteAllText(filePath, content);
    }

    public static bool ContainsInvalidChars(string fileName)
    {
        char[] invalidChars = Path.GetInvalidFileNameChars();
        return invalidChars.Any(c => fileName.Contains(c));
    }

    // Error handling

    private static void HandleScriptError(string errorMessage, string scriptContent)
    {
        if (string.IsNullOrEmpty(errorMessage))
            return;

        var errors = LuaErrorParser.ParseMultipleErrors(errorMessage, scriptContent);

        _currentErrors.Clear();
        _currentErrors.AddRange(errors);

        Message.Error($"Lua error: {errorMessage}");

        // Also log to debug console for visibility
        foreach (var error in errors)
        {
            DebugConsole.Lua.Error($"Line {error.Line + 1}: {error.Message}");
        }

        ScriptErrorsChanged?.Invoke(null, new List<LuaError>(_currentErrors));

        // Razor-Zusatz: stilles Logfile in CrashLogs (Dedupe 60s), kein Fenster —
        // der Report-Dialog bleibt echten Crashes vorbehalten.
        CrashReporter.ReportScriptError("Lua", CurrentFile?.Item1, scriptContent, errorMessage);
    }

    private static void ClearErrors()
    {
        if (_currentErrors.Count > 0)
        {
            _currentErrors.Clear();
            ScriptErrorsChanged?.Invoke(null, new List<LuaError>());
        }
    }

    public static List<LuaError> GetCurrentErrors() => new List<LuaError>(_currentErrors);
}

/// <summary>
/// Represents a call stack frame for debugging
/// </summary>
public class LuaCallStackFrame
{
    public string FunctionName { get; set; }
    public string Source { get; set; }
    public int Line { get; set; }

    public override string ToString() => $"{FunctionName} at {Source}:{Line}";
}
