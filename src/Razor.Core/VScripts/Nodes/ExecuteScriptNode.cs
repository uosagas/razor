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

using Assistant.VScripts.Core;
using Assistant.VScripts.Engine;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Assistant.VScripts.Nodes;

/// <summary>
/// Flow node that executes another VScript with parameter support
/// </summary>
public class ExecuteScriptNode : VScriptNode
{
    private string _selectedScriptName = "";

    public string SelectedScriptName
    {
        get => _selectedScriptName;
        set
        {
            if (_selectedScriptName != value)
            {
                _selectedScriptName = value;
                UpdateDynamicPins();
            }
        }
    }

    /// <summary>
    /// Sets the script name without triggering UpdateDynamicPins.
    /// Used during deserialization when pins are restored from saved data.
    /// </summary>
    public void SetSelectedScriptNameWithoutUpdate(string scriptName)
    {
        _selectedScriptName = scriptName;
    }

    public bool Wait { get; set; } = true; // Wait for script to finish before continuing

    // Maps parameter names to their input pin IDs
    private Dictionary<string, string> _parameterPinIds = new();

    // Maps output names to their output pin IDs
    private Dictionary<string, string> _outputPinIds = new();

    public ExecuteScriptNode(string id, string pinIdCounter) : base(id, "Execute Script", NodeCategory.Flow)
    {
        InputPins.Add(new NodePin(Guid.NewGuid().ToString(), id, "", PinType.Flow, PinKind.Input));
        OutputPins.Add(new NodePin(Guid.NewGuid().ToString(), id, "", PinType.Flow, PinKind.Output));
    }

    // Razor-Zusatz: Funktions-Aufrufe sind rot wie Events (UE-Optik) — der Node
    // traegt zudem den Funktionsnamen als Titel (setzt der Editor/Serializer).
    public override System.Numerics.Vector4 GetTitleBarColor()
    {
        return new System.Numerics.Vector4(0.8f, 0.2f, 0.2f, 1.0f);
    }

    /// <summary>
    /// Rebuilds the pin mapping dictionaries from the existing pins.
    /// Called after deserialization to restore the mapping without regenerating pin IDs.
    /// </summary>
    public void RebuildPinMappings()
    {
        _parameterPinIds.Clear();
        foreach (var pin in InputPins.Where(p => p.Type != PinType.Flow))
        {
            if (!string.IsNullOrEmpty(pin.Name))
            {
                _parameterPinIds[pin.Name] = pin.Id;
            }
        }

        _outputPinIds.Clear();
        foreach (var pin in OutputPins.Where(p => p.Type != PinType.Flow))
        {
            if (!string.IsNullOrEmpty(pin.Name))
            {
                _outputPinIds[pin.Name] = pin.Id;
            }
        }
    }

    /// <summary>
    /// Updates the dynamic pins based on the selected script's parameters and outputs
    /// </summary>
    public void UpdateDynamicPins()
    {
        // Remove old dynamic pins (keep only flow pins)
        var inputPinsToRemove = InputPins.Where(p => p.Type != PinType.Flow).ToList();
        foreach (var pin in inputPinsToRemove)
        {
            InputPins.Remove(pin);
        }

        var outputPinsToRemove = OutputPins.Where(p => p.Type != PinType.Flow).ToList();
        foreach (var pin in outputPinsToRemove)
        {
            OutputPins.Remove(pin);
        }

        _parameterPinIds.Clear();
        _outputPinIds.Clear();

        if (string.IsNullOrEmpty(_selectedScriptName))
            return;

        // Load the target script to get its variables
        var targetGraph = VScriptService.LoadScript(_selectedScriptName);
        if (targetGraph == null)
            return;

        // Add input pins for parameters (variables with Scope = Parameter)
        var parameters = targetGraph.Variables.Where(v => v.Scope == VariableScope.Parameter).ToList();
        foreach (var param in parameters)
        {
            var pinId = Guid.NewGuid().ToString();
            var pin = new NodePin(
                pinId,
                Id,
                param.Name,
                param.Type,
                PinKind.Input,
                param.IsList,
                param.ObjectSubType
            );
            InputPins.Add(pin);
            _parameterPinIds[param.Name] = pinId;
        }

        // Add output pins for outputs (variables with Scope = Output)
        var outputs = targetGraph.Variables.Where(v => v.Scope == VariableScope.Output).ToList();
        foreach (var output in outputs)
        {
            var pinId = Guid.NewGuid().ToString();
            var pin = new NodePin(
                pinId,
                Id,
                output.Name,
                output.Type,
                PinKind.Output,
                output.IsList,
                output.ObjectSubType
            );
            OutputPins.Add(pin);
            _outputPinIds[output.Name] = pinId;
        }
    }

    /// <summary>
    /// Gets parameter pin mappings (name -> pinId)
    /// </summary>
    public Dictionary<string, string> GetParameterPinIds() => new(_parameterPinIds);

    /// <summary>
    /// Gets output pin mappings (name -> pinId)
    /// </summary>
    public Dictionary<string, string> GetOutputPinIds() => new(_outputPinIds);

    public override void Execute(VScriptContext context)
    {
        if (string.IsNullOrEmpty(SelectedScriptName))
        {
            context.ErrorMessage = "No script selected in Execute Script node";
            return;
        }

        // Prevent script from calling itself (infinite recursion)
        if (!string.IsNullOrEmpty(context.CurrentScriptName) &&
            context.CurrentScriptName.Equals(SelectedScriptName, StringComparison.OrdinalIgnoreCase))
        {
            context.ErrorMessage = $"Script '{SelectedScriptName}' cannot call itself (recursion not allowed)";
            return;
        }

        // Collect parameter values from input pins
        var parameters = new Dictionary<string, object>();
        foreach (var kvp in _parameterPinIds)
        {
            var paramName = kvp.Key;
            var pinId = kvp.Value;
            var pin = InputPins.FirstOrDefault(p => p.Id == pinId);
            if (pin != null)
            {
                parameters[paramName] = pin.Value;
            }
        }

        if (Wait)
        {
            // Execute the script synchronously with parameters
            var returnValues = VScriptService.RunNestedScriptWithParameters(SelectedScriptName, context, parameters);

            // Set return values on output pins
            if (returnValues != null)
            {
                foreach (var kvp in _outputPinIds)
                {
                    var outputName = kvp.Key;
                    var pinId = kvp.Value;
                    var pin = OutputPins.FirstOrDefault(p => p.Id == pinId);
                    if (pin != null && returnValues.TryGetValue(outputName, out var value))
                    {
                        pin.Value = value;
                    }
                }
            }
        }
        else
        {
            // Execute the script asynchronously (fire and forget, no return values)
            System.Threading.Tasks.Task.Run(() => VScriptService.RunScript(SelectedScriptName));
        }
    }
}
