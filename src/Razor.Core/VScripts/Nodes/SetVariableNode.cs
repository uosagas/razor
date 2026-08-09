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
using System.Collections;
using System.Collections.Generic;
using Assistant.VScripts.Core;

namespace Assistant.VScripts.Nodes;

public class SetVariableNode : VScriptNode
{
    public string VariableName { get; set; }
    public ObjectSubType ObjectSubType { get; set; }
    public bool IsList { get; set; }
    public List<string> ListInitValues { get; set; } = new(); // Initialization values for list (string/number only)

    public SetVariableNode(string id, string pinIdCounter, string variableName, PinType variableType, ObjectSubType objectSubType = ObjectSubType.Player, bool isList = false)
        : base(id, $"Set {variableName}", NodeCategory.Variable)
    {
        VariableName = variableName;
        ObjectSubType = objectSubType;
        IsList = isList;

        // Add [] indicator to pin name if it's a list
        string pinName = isList ? $"{variableName} []" : variableName;

        InputPins.Add(new NodePin(Guid.NewGuid().ToString(), id, "", PinType.Flow, PinKind.Input));
        InputPins.Add(new NodePin(Guid.NewGuid().ToString(), id, pinName, variableType, PinKind.Input, isList));
        OutputPins.Add(new NodePin(Guid.NewGuid().ToString(), id, "", PinType.Flow, PinKind.Output));
    }

    public override void Execute(VScriptContext context)
    {
        var pinName = IsList ? $"{VariableName} []" : VariableName;
        var valuePin = InputPins.Find(p => p.Name == pinName);

        // Razor-Zusatz: kommaseparierter TEXT am List-Pin ("121020, 0x67B2")
        // wird zur typisierten Liste — sonst landet ein String in der Variablen
        // und ForEach & Co. melden "Input must be a list".
        if (IsList && valuePin?.Value is string text && text.Trim().Length > 0)
        {
            context.SetVariable(VariableName, ParseListText(text, valuePin.Type));
        }
        else if (valuePin?.Value != null)
        {
            context.SetVariable(VariableName, valuePin.Value);
        }
        else if (IsList && ListInitValues.Count > 0)
        {
            // If pin is not connected and we have initialization values, create and populate the list
            var pinType = valuePin?.Type ?? PinType.String;
            IList list = pinType switch
            {
                PinType.Number => new List<float>(),
                PinType.String => new List<string>(),
                _ => new List<string>()
            };

            // Parse and add values to the list
            foreach (var valueStr in ListInitValues)
            {
                if (pinType == PinType.Number)
                {
                    if (float.TryParse(valueStr, out var numValue))
                    {
                        list.Add(numValue);
                    }
                }
                else if (pinType == PinType.String)
                {
                    list.Add(valueStr);
                }
            }

            context.SetVariable(VariableName, list);
        }
    }

    /// <summary>Razor-Zusatz: kommaseparierten Text in eine typisierte Liste
    /// wandeln (Number mit 0x-Hex-Unterstuetzung, sonst Strings).</summary>
    private static IList ParseListText(string text, PinType pinType)
    {
        IList list = pinType == PinType.Number ? new List<float>() : new List<string>();

        foreach (var part in text.Split(','))
        {
            string trimmed = part.Trim();
            if (trimmed.Length == 0)
                continue;

            if (pinType == PinType.Number)
            {
                if (trimmed.StartsWith("0x", StringComparison.OrdinalIgnoreCase) &&
                    uint.TryParse(trimmed.Substring(2), System.Globalization.NumberStyles.HexNumber,
                        System.Globalization.CultureInfo.InvariantCulture, out uint hex))
                {
                    list.Add((float) hex);
                }
                else if (float.TryParse(trimmed, System.Globalization.NumberStyles.Float,
                             System.Globalization.CultureInfo.InvariantCulture, out float number))
                {
                    list.Add(number);
                }
            }
            else
            {
                list.Add(trimmed);
            }
        }

        return list;
    }
}
