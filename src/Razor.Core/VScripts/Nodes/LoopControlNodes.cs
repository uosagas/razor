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
using System;

namespace Assistant.VScripts.Nodes;

// While Loop node - executes loop body while condition is true
public class WhileLoopNode : VScriptNode
{
    public override float Width => 250.0f; // Wider than default nodes

    public WhileLoopNode(string id, string pinIdCounter) : base(id, "While Loop", NodeCategory.Flow)
    {
        // Input pins
        InputPins.Add(new NodePin(Guid.NewGuid().ToString(), id, "", PinType.Flow, PinKind.Input));
        InputPins.Add(new NodePin(Guid.NewGuid().ToString(), id, "Condition", PinType.Boolean, PinKind.Input));

        // Output pins
        OutputPins.Add(new NodePin(Guid.NewGuid().ToString(), id, "Loop Body", PinType.Flow, PinKind.Output));
        OutputPins.Add(new NodePin(Guid.NewGuid().ToString(), id, "Completed", PinType.Flow, PinKind.Output));
    }

    public override void Execute(VScriptContext context)
    {
        // This node requires special handling in the engine for proper loop execution
        // The Execute method here just validates inputs
        var conditionPin = InputPins.Find(p => p.Name == "Condition");

        if (conditionPin?.Value == null)
        {
            context.ErrorMessage = "WhileLoop: Condition must be specified";
            return;
        }
    }

    public bool GetCondition()
    {
        var pin = InputPins.Find(p => p.Name == "Condition");
        if (pin?.Value == null)
            return false;

        if (pin.Value is bool boolValue)
            return boolValue;

        // Try to convert to bool
        return Convert.ToBoolean(pin.Value);
    }

    public NodePin GetLoopBodyPin()
    {
        return OutputPins.Find(p => p.Name == "Loop Body");
    }

    public NodePin GetCompletedPin()
    {
        return OutputPins.Find(p => p.Name == "Completed");
    }
}

// Break node - exits the current loop (For Loop or While Loop)
public class BreakNode : VScriptNode
{
    public BreakNode(string id, string pinIdCounter) : base(id, "Break", NodeCategory.Flow)
    {
        // Input pin
        InputPins.Add(new NodePin(Guid.NewGuid().ToString(), id, "", PinType.Flow, PinKind.Input));

        // No output pins - breaks the loop execution
    }

    public override void Execute(VScriptContext context)
    {
        // Signal that a break was requested
        context.BreakRequested = true;
    }
}
