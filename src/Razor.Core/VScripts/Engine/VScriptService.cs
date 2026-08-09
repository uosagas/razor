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

// Portiert aus dem integrierten Assistant (VScripts/Engine/VScriptService.cs).
// Abweichungen: Hotkeys laufen ueber das Razor-HotKey-System (HKCategory.
// Scripts, wie DressList-Dynamik); Meldungen ueber World.Player.SendMessage.
// VScripts leben in der RAZOR-Ordnerstruktur (<Plugin>/Data/VScripts) — das
// DATEIFORMAT bleibt 1:1 client-kompatibel, Austausch = Datei kopieren.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Assistant.VScripts.Core;
using Assistant.VScripts.Data;

namespace Assistant.VScripts.Engine;

public static class VScriptService
{
    private static readonly string _vscriptsDirectory = ResolveVScriptsDirectory();
    private static readonly string _globalVariablesFile = Path.Combine(ResolveVScriptsDirectory(), "GlobalVariables.json");

    /// <summary>Razor-Ordnerstruktur (&lt;Plugin&gt;/Data/VScripts) statt CWD-relativ;
    /// relativer Fallback, wenn Config noch nicht initialisiert ist (Tests).</summary>
    private static string ResolveVScriptsDirectory()
    {
        try
        {
            return Config.GetInstallDirectory("VScripts");
        }
        catch
        {
            return Path.Combine("Data", "VScripts");
        }
    }
    private static readonly VScriptEngine _engine = new();
    private static Dictionary<string, NodeGraph> _loadedScripts = new();
    private static List<ScriptVariable> _globalVariables = new();
    private static Dictionary<string, object> _globalVariableValues = new(); // Runtime values for global variables

    public static VScriptEngine Engine => _engine;
    private static bool _initialized = false;

    static VScriptService()
    {
        // Static constructor - will be called on first access
        Initialize();
    }

    public static void Initialize()
    {
        if (_initialized)
        {
            Console.WriteLine("VScriptService.Initialize() - Already initialized, skipping");
            return;
        }

        Console.WriteLine("VScriptService.Initialize() - Starting initialization...");
        _initialized = true;
        EnsureDirectoryExists();
        LoadGlobalVariables();
        LoadAllScripts();
        Console.WriteLine($"VScriptService.Initialize() - Completed. Loaded {_loadedScripts.Count} scripts");
    }

    private static void EnsureDirectoryExists()
    {
        try
        {
            if (!Directory.Exists(_vscriptsDirectory))
            {
                Directory.CreateDirectory(_vscriptsDirectory);
                Console.WriteLine($"Created VScripts directory: {Path.GetFullPath(_vscriptsDirectory)}");
            }
            else
            {
                Console.WriteLine($"VScripts directory exists: {Path.GetFullPath(_vscriptsDirectory)}");
            }
        }
        catch (Exception ex)
        {
            Error($"Failed to create VScripts directory: {ex.Message}");
            Console.WriteLine($"Failed to create VScripts directory: {ex.Message}");
        }
    }

    public static void LoadAllScripts()
    {
        _loadedScripts.Clear();

        if (!Directory.Exists(_vscriptsDirectory))
        {
            return;
        }

        var files = Directory.GetFiles(_vscriptsDirectory, "*.vscript");
        foreach (var file in files)
        {
            var graph = VScriptSerializer.LoadFromFile(file);
            if (graph != null)
            {
                var name = Path.GetFileNameWithoutExtension(file);
                _loadedScripts[name] = graph;
                RegisterVScriptHotkey(name);
            }
        }
    }

    public static Dictionary<string, NodeGraph> GetAllScripts()
    {
        return new Dictionary<string, NodeGraph>(_loadedScripts);
    }

    public static List<string> GetAllScriptNames()
    {
        return new List<string>(_loadedScripts.Keys);
    }

    public static NodeGraph LoadScript(string name)
    {
        if (_loadedScripts.TryGetValue(name, out var graph))
        {
            return graph;
        }

        var filePath = Path.Combine(_vscriptsDirectory, $"{name}.vscript");
        graph = VScriptSerializer.LoadFromFile(filePath);
        if (graph != null)
        {
            _loadedScripts[name] = graph;
        }

        return graph;
    }

    public static bool SaveScript(string name, NodeGraph graph)
    {
        var filePath = Path.Combine(_vscriptsDirectory, $"{name}.vscript");
        if (VScriptSerializer.SaveToFile(graph, filePath))
        {
            _loadedScripts[name] = graph;
            return true;
        }
        return false;
    }

    public static bool CreateNewScript(string name)
    {
        if (_loadedScripts.ContainsKey(name))
        {
            Error($"VScript '{name}' already exists!");
            return false;
        }

        var graph = new NodeGraph(name);
        var filePath = Path.Combine(_vscriptsDirectory, $"{name}.vscript");

        Console.WriteLine($"Creating new VScript: {name}");
        Console.WriteLine($"Full path: {Path.GetFullPath(filePath)}");

        if (VScriptSerializer.SaveToFile(graph, filePath))
        {
            _loadedScripts[name] = graph;
            RegisterVScriptHotkey(name);
            Console.WriteLine($"Successfully created VScript: {name}");
            return true;
        }

        Console.WriteLine($"Failed to save VScript: {name}");
        return false;
    }

    public static bool DeleteScript(string name)
    {
        var filePath = Path.Combine(_vscriptsDirectory, $"{name}.vscript");
        if (File.Exists(filePath))
        {
            try
            {
                File.Delete(filePath);
                _loadedScripts.Remove(name);
                UnregisterVScriptHotkey(name);
                return true;
            }
            catch
            {
                return false;
            }
        }
        return false;
    }

    public static void RunScript(string name)
    {
        var graph = LoadScript(name);
        if (graph == null)
        {
            Error($"VScript '{name}' not found!");
            return;
        }

        if (_engine.IsRunning)
        {
            Error("A VScript is already running!");
            return;
        }

        _engine.LoadGraph(graph);
        _engine.Start();
    }

    public static void RunNestedScript(string name, VScriptContext parentContext)
    {
        var graph = LoadScript(name);
        if (graph == null)
        {
            parentContext.ErrorMessage = $"VScript '{name}' not found!";
            return;
        }

        // Execute the nested script synchronously in the current context
        _engine.ExecuteGraphSynchronously(graph, parentContext);
    }

    /// <summary>
    /// Runs a nested script with explicit parameters and returns output values
    /// </summary>
    /// <param name="name">Script name to execute</param>
    /// <param name="parentContext">Parent execution context</param>
    /// <param name="parameters">Dictionary of parameter names to values</param>
    /// <returns>Dictionary of output names to values, or null if execution failed</returns>
    public static Dictionary<string, object> RunNestedScriptWithParameters(
        string name,
        VScriptContext parentContext,
        Dictionary<string, object> parameters)
    {
        var graph = LoadScript(name);
        if (graph == null)
        {
            parentContext.ErrorMessage = $"VScript '{name}' not found!";
            return null;
        }

        // Execute the nested script with parameters and get return values
        return _engine.ExecuteGraphWithParameters(graph, parentContext, parameters);
    }

    public static void RunGraph(NodeGraph graph)
    {
        if (_engine.IsRunning)
        {
            Error("A VScript is already running!");
            return;
        }

        _engine.LoadGraph(graph);
        _engine.Start();
    }

    public static void StopScript()
    {
        _engine.Stop();
    }

    public static bool IsRunning()
    {
        return _engine.IsRunning;
    }

    public static VScriptEngine GetEngine()
    {
        return _engine;
    }

    // Global Variables Management
    public static List<ScriptVariable> GetGlobalVariables()
    {
        return new List<ScriptVariable>(_globalVariables);
    }

    public static void AddGlobalVariable(ScriptVariable variable)
    {
        _globalVariables.Add(variable);
        SaveGlobalVariables();
        // Initialize the runtime value
        _globalVariableValues[variable.Name] = variable.DefaultValue;
    }

    public static void RemoveGlobalVariable(ScriptVariable variable)
    {
        _globalVariables.Remove(variable);
        _globalVariableValues.Remove(variable.Name);
        SaveGlobalVariables();
    }

    public static bool IsGlobalVariable(string name)
    {
        return _globalVariables.Any(v => v.Name == name);
    }

    public static object GetGlobalVariableValue(string name)
    {
        return _globalVariableValues.TryGetValue(name, out var value) ? value : null;
    }

    public static void SetGlobalVariableValue(string name, object value)
    {
        if (IsGlobalVariable(name))
        {
            _globalVariableValues[name] = value;
        }
    }

    private static void LoadGlobalVariables()
    {
        try
        {
            if (File.Exists(_globalVariablesFile))
            {
                var json = File.ReadAllText(_globalVariablesFile);
                var variables = VScriptSerializer.DeserializeVariables(json);
                if (variables != null)
                {
                    _globalVariables = variables;
                    // Initialize runtime values for all global variables
                    _globalVariableValues.Clear();
                    foreach (var variable in _globalVariables)
                    {
                        _globalVariableValues[variable.Name] = variable.DefaultValue;
                    }
                    Console.WriteLine($"Loaded {_globalVariables.Count} global variables");
                }
            }
            else
            {
                Console.WriteLine("No global variables file found, starting with empty list");
            }
        }
        catch (Exception ex)
        {
            Error($"Failed to load global variables: {ex.Message}");
            Console.WriteLine($"Failed to load global variables: {ex.Message}");
        }
    }

    private static void SaveGlobalVariables()
    {
        try
        {
            var json = VScriptSerializer.SerializeVariables(_globalVariables);
            File.WriteAllText(_globalVariablesFile, json);
            Console.WriteLine($"Saved {_globalVariables.Count} global variables");
        }
        catch (Exception ex)
        {
            Error($"Failed to save global variables: {ex.Message}");
            Console.WriteLine($"Failed to save global variables: {ex.Message}");
        }
    }

    // Hotkey-Integration: Razor-HotKey-System (dynamisch wie die DressLists).
    // Wichtig: Registrierung MUSS vor Config.LoadLastProfile stehen, damit
    // gespeicherte Belegungen greifen (RazorPlugin ruft Initialize frueh).
    private static void RegisterVScriptHotkey(string scriptName)
    {
        HotKey.Add(HKCategory.Scripts, HKSubCat.None, $"VScript: {scriptName}",
            new HotKeyCallbackState(OnHotKey), scriptName);
    }

    private static void UnregisterVScriptHotkey(string scriptName)
    {
        HotKey.Remove($"VScript: {scriptName}");
    }

    private static void OnHotKey(ref object state)
    {
        RunScript((string) state);
    }

    private static void Error(string msg)
    {
        World.Player?.SendMessage(MsgLevel.Error, msg);
        Console.WriteLine($"[Razor/vscript] {msg}");
    }
}
