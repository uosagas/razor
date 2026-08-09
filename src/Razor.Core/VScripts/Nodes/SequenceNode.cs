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
using Assistant.VScripts.Core;
using System.Numerics;

namespace Assistant.VScripts.Nodes;

public class SequenceNode : VScriptNode
{
    public int OutputPinCount { get; set; }

    public SequenceNode(string id, string pinIdCounter, int outputCount = 2) : base(id, "Sequence", NodeCategory.Flow)
    {
        OutputPinCount = outputCount;

        // Add flow input
        InputPins.Add(new NodePin(Guid.NewGuid().ToString(), id, "", PinType.Flow, PinKind.Input));

        // Add output pins
        for (int i = 0; i < outputCount; i++)
        {
            OutputPins.Add(new NodePin(Guid.NewGuid().ToString() + i, id, $"Then {i}", PinType.Flow, PinKind.Output));
        }
    }

    // Override to use darker blue color like Unreal Engine function nodes
    public override Vector4 GetTitleBarColor()
    {
        return new Vector4(0.2f, 0.4f, 0.8f, 1.0f); // Darker blue like UE function nodes
    }

    public override void Execute(VScriptContext context)
    {
        // Sequence node executes all output pins in order
        // The execution engine will handle following the flow pins
        // This node just passes through execution
    }

    public void AddOutputPin(string pinId)
    {
        int index = OutputPins.Count;
        OutputPins.Add(new NodePin(pinId, Id, $"Then {index}", PinType.Flow, PinKind.Output));
        OutputPinCount = OutputPins.Count;
    }
}
