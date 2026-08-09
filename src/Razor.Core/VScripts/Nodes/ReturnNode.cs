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
using System.Linq;
using Assistant.VScripts.Core;

namespace Assistant.VScripts.Nodes;

/// <summary>
/// Return node that ends script execution and optionally returns values.
/// Output variables defined in the script appear as input pins on this node.
/// </summary>
public class ReturnNode : VScriptNode
{
    // Stores the output variable names mapped to their input pin IDs
    private Dictionary<string, string> _outputPinIds = new();

    public ReturnNode(string id, string pinIdCounter) : base(id, "Return", NodeCategory.Flow)
    {
        InputPins.Add(new NodePin(Guid.NewGuid().ToString(), id, "", PinType.Flow, PinKind.Input));
    }

    /// <summary>
    /// Rebuilds the _outputPinIds dictionary from the existing input pins.
    /// Called after deserialization to restore the mapping without regenerating pin IDs.
    /// </summary>
    public void RebuildOutputPinMapping()
    {
        _outputPinIds.Clear();
        foreach (var pin in InputPins.Where(p => p.Type != PinType.Flow))
        {
            if (!string.IsNullOrEmpty(pin.Name))
            {
                _outputPinIds[pin.Name] = pin.Id;
            }
        }
    }

    /// <summary>
    /// Updates the input pins based on the script's output variables.
    /// Called when script variables change.
    /// </summary>
    public void UpdateOutputPins(List<ScriptVariable> variables)
    {
        // Get all output variables
        var outputs = variables.Where(v => v.Scope == VariableScope.Output).ToList();

        // Remove old output pins (keep the flow pin)
        var pinsToRemove = InputPins.Where(p => p.Type != PinType.Flow).ToList();
        foreach (var pin in pinsToRemove)
        {
            InputPins.Remove(pin);
        }
        _outputPinIds.Clear();

        // Add new output pins as input pins on this node
        foreach (var output in outputs)
        {
            var pinId = Guid.NewGuid().ToString();
            var pin = new NodePin(
                pinId,
                Id,
                output.Name,
                output.Type,
                PinKind.Input,
                output.IsList,
                output.ObjectSubType
            );
            InputPins.Add(pin);
            _outputPinIds[output.Name] = pinId;
        }
    }

    /// <summary>
    /// Gets the pin ID for an output by name
    /// </summary>
    public string GetOutputPinId(string outputName)
    {
        return _outputPinIds.TryGetValue(outputName, out var pinId) ? pinId : null;
    }

    /// <summary>
    /// Gets all output pin mappings (name -> pinId)
    /// </summary>
    public Dictionary<string, string> GetOutputPinIds() => new(_outputPinIds);

    public override void Execute(VScriptContext context)
    {
        // Set output values in context from connected input pins
        foreach (var kvp in _outputPinIds)
        {
            var outputName = kvp.Key;
            var pinId = kvp.Value;
            var pin = InputPins.FirstOrDefault(p => p.Id == pinId);
            if (pin != null && pin.Value != null)
            {
                // Store the return value in context with a special prefix for retrieval
                context.SetVariable($"__return_{outputName}", pin.Value);
            }
        }

        // Signal that the script should stop (return)
        context.ShouldStop = true;
    }
}
