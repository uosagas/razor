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

namespace Assistant.VScripts.Nodes;

public class GetVariableNode : VScriptNode
{
    public string VariableName { get; set; }
    public ObjectSubType ObjectSubType { get; set; }
    public bool IsList { get; set; }

    public GetVariableNode(string id, string pinIdCounter, string variableName, PinType variableType, ObjectSubType objectSubType = ObjectSubType.Player, bool isList = false)
        : base(id, $"Get {variableName}", NodeCategory.Variable)
    {
        VariableName = variableName;
        ObjectSubType = objectSubType;
        IsList = isList;

        // Add [] indicator to pin name if it's a list
        string pinName = isList ? $"{variableName} []" : variableName;
        OutputPins.Add(new NodePin(Guid.NewGuid().ToString(), id, pinName, variableType, PinKind.Output, isList));
    }

    public override void Execute(VScriptContext context)
    {
        var value = context.GetVariable(VariableName);
        var outputPin = OutputPins[0];
        outputPin.Value = value;
    }
}
