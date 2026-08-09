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

public class StartNode : VScriptNode
{
    // Stores the parameter variable names mapped to their output pin IDs
    private Dictionary<string, string> _parameterPinIds = new();

    public StartNode(string id, string pinIdCounter) : base(id, "Start", NodeCategory.Event)
    {
        OutputPins.Add(new NodePin(Guid.NewGuid().ToString(), id, "", PinType.Flow, PinKind.Output));
    }

    /// <summary>
    /// Rebuilds the _parameterPinIds dictionary from the existing output pins.
    /// Called after deserialization to restore the mapping without regenerating pin IDs.
    /// </summary>
    public void RebuildParameterPinMapping()
    {
        _parameterPinIds.Clear();
        foreach (var pin in OutputPins.Where(p => p.Type != PinType.Flow))
        {
            if (!string.IsNullOrEmpty(pin.Name))
            {
                _parameterPinIds[pin.Name] = pin.Id;
            }
        }
    }

    /// <summary>
    /// Updates the output pins based on the script's parameter variables.
    /// Called when script variables change.
    /// </summary>
    public void UpdateParameterPins(List<ScriptVariable> variables)
    {
        // Get all parameter variables
        var parameters = variables.Where(v => v.Scope == VariableScope.Parameter).ToList();

        // Remove old parameter pins (keep the flow pin)
        var pinsToRemove = OutputPins.Where(p => p.Type != PinType.Flow).ToList();
        foreach (var pin in pinsToRemove)
        {
            OutputPins.Remove(pin);
        }
        _parameterPinIds.Clear();

        // Add new parameter pins
        foreach (var param in parameters)
        {
            var pinId = Guid.NewGuid().ToString();
            var pin = new NodePin(
                pinId,
                Id,
                param.Name,
                param.Type,
                PinKind.Output,
                param.IsList,
                param.ObjectSubType
            );
            OutputPins.Add(pin);
            _parameterPinIds[param.Name] = pinId;
        }
    }

    /// <summary>
    /// Gets the pin ID for a parameter by name
    /// </summary>
    public string GetParameterPinId(string parameterName)
    {
        return _parameterPinIds.TryGetValue(parameterName, out var pinId) ? pinId : null;
    }

    /// <summary>
    /// Gets all parameter pin mappings (name -> pinId)
    /// </summary>
    public Dictionary<string, string> GetParameterPinIds() => new(_parameterPinIds);

    public override void Execute(VScriptContext context)
    {
        // Start node outputs parameter values from context to connected nodes
        // The parameter values are set in the context before execution by ExecuteScriptNode
        foreach (var kvp in _parameterPinIds)
        {
            var paramName = kvp.Key;
            var pinId = kvp.Value;
            var pin = OutputPins.FirstOrDefault(p => p.Id == pinId);
            if (pin != null)
            {
                // Get the parameter value from context and set it on the pin
                pin.Value = context.GetVariable(paramName);
            }
        }
    }
}
