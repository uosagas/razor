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

public class ForLoopNode : VScriptNode
{
    public override float Width => 250.0f; // Wider than default nodes

    public ForLoopNode(string id, string pinIdCounter) : base(id, "For Loop", NodeCategory.Flow)
    {
        // Input pins
        InputPins.Add(new NodePin(Guid.NewGuid().ToString(), id, "", PinType.Flow, PinKind.Input));
        InputPins.Add(new NodePin(Guid.NewGuid().ToString(), id, "First Index", PinType.Number, PinKind.Input));
        InputPins.Add(new NodePin(Guid.NewGuid().ToString(), id, "Last Index", PinType.Number, PinKind.Input));

        // Output pins
        OutputPins.Add(new NodePin(Guid.NewGuid().ToString(), id, "Loop Body", PinType.Flow, PinKind.Output));
        OutputPins.Add(new NodePin(Guid.NewGuid().ToString(), id, "Completed", PinType.Flow, PinKind.Output));
        OutputPins.Add(new NodePin(Guid.NewGuid().ToString(), id, "Index", PinType.Number, PinKind.Output));
    }

    public override void Execute(VScriptContext context)
    {
        // This node requires special handling in the engine for proper loop execution
        // The Execute method here just validates inputs
        // Note: First Index defaults to 0 if not specified, so null values are acceptable
        // GetFirstIndex() and GetLastIndex() provide default values
    }

    public int GetFirstIndex()
    {
        var pin = InputPins.Find(p => p.Name == "First Index");
        return pin?.Value != null ? Convert.ToInt32(pin.Value) : 0;
    }

    public int GetLastIndex()
    {
        var pin = InputPins.Find(p => p.Name == "Last Index");
        return pin?.Value != null ? Convert.ToInt32(pin.Value) : 0;
    }

    public void SetCurrentIndex(int index)
    {
        var indexPin = OutputPins.Find(p => p.Name == "Index");
        if (indexPin != null)
        {
            indexPin.Value = index;
        }
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
