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

// Concatenate two strings together
public class ConcatenateStringsNode : VScriptNode
{
    public ConcatenateStringsNode(string id, string pinIdCounter) : base(id, "Concat Strings", NodeCategory.String)
    {
        // Input pins
        InputPins.Add(new NodePin(Guid.NewGuid().ToString(), id, "A", PinType.String, PinKind.Input));
        InputPins.Add(new NodePin(Guid.NewGuid().ToString(), id, "B", PinType.String, PinKind.Input));

        // Output pin
        OutputPins.Add(new NodePin(Guid.NewGuid().ToString(), id, "Result", PinType.String, PinKind.Output));
    }

    public override void Execute(VScriptContext context)
    {
        var pinA = InputPins[0];
        var pinB = InputPins[1];
        var outputPin = OutputPins[0];

        string valueA = pinA.Value?.ToString() ?? "";
        string valueB = pinB.Value?.ToString() ?? "";

        outputPin.Value = valueA + valueB;
    }
}

// Append string with separator
public class AppendStringNode : VScriptNode
{
    public AppendStringNode(string id, string pinIdCounter) : base(id, "Append String", NodeCategory.String)
    {
        // Input pins
        InputPins.Add(new NodePin(Guid.NewGuid().ToString(), id, "A", PinType.String, PinKind.Input));
        InputPins.Add(new NodePin(Guid.NewGuid().ToString(), id, "B", PinType.String, PinKind.Input));
        InputPins.Add(new NodePin(Guid.NewGuid().ToString(), id, "Separator", PinType.String, PinKind.Input));

        // Output pin
        OutputPins.Add(new NodePin(Guid.NewGuid().ToString(), id, "Result", PinType.String, PinKind.Output));
    }

    public override void Execute(VScriptContext context)
    {
        var pinA = InputPins[0];
        var pinB = InputPins[1];
        var separatorPin = InputPins[2];
        var outputPin = OutputPins[0];

        string valueA = pinA.Value?.ToString() ?? "";
        string valueB = pinB.Value?.ToString() ?? "";
        string separator = separatorPin.Value?.ToString() ?? "";

        outputPin.Value = valueA + separator + valueB;
    }
}

// String length
public class StringLengthNode : VScriptNode
{
    public StringLengthNode(string id, string pinIdCounter) : base(id, "String Length", NodeCategory.String)
    {
        // Input pin
        InputPins.Add(new NodePin(Guid.NewGuid().ToString(), id, "String", PinType.String, PinKind.Input));

        // Output pin
        OutputPins.Add(new NodePin(Guid.NewGuid().ToString(), id, "Length", PinType.Number, PinKind.Output));
    }

    public override void Execute(VScriptContext context)
    {
        var inputPin = InputPins[0];
        var outputPin = OutputPins[0];

        string value = inputPin.Value?.ToString() ?? "";
        outputPin.Value = (float)value.Length;
    }
}

// String contains check
public class StringContainsNode : VScriptNode
{
    public StringContainsNode(string id, string pinIdCounter) : base(id, "String Contains", NodeCategory.String)
    {
        // Input pins
        InputPins.Add(new NodePin(Guid.NewGuid().ToString(), id, "String", PinType.String, PinKind.Input));
        InputPins.Add(new NodePin(Guid.NewGuid().ToString(), id, "Search", PinType.String, PinKind.Input));

        // Output pin
        OutputPins.Add(new NodePin(Guid.NewGuid().ToString(), id, "Contains", PinType.Boolean, PinKind.Output));
    }

    public override void Execute(VScriptContext context)
    {
        var stringPin = InputPins[0];
        var searchPin = InputPins[1];
        var outputPin = OutputPins[0];

        string stringValue = stringPin.Value?.ToString() ?? "";
        string searchValue = searchPin.Value?.ToString() ?? "";

        outputPin.Value = stringValue.Contains(searchValue, StringComparison.OrdinalIgnoreCase);
    }
}

// String to upper case
public class StringToUpperNode : VScriptNode
{
    public StringToUpperNode(string id, string pinIdCounter) : base(id, "To Upper Case", NodeCategory.String)
    {
        // Input pin
        InputPins.Add(new NodePin(Guid.NewGuid().ToString(), id, "String", PinType.String, PinKind.Input));

        // Output pin
        OutputPins.Add(new NodePin(Guid.NewGuid().ToString(), id, "Result", PinType.String, PinKind.Output));
    }

    public override void Execute(VScriptContext context)
    {
        var inputPin = InputPins[0];
        var outputPin = OutputPins[0];

        string value = inputPin.Value?.ToString() ?? "";
        outputPin.Value = value.ToUpper();
    }
}

// String to lower case
public class StringToLowerNode : VScriptNode
{
    public StringToLowerNode(string id, string pinIdCounter) : base(id, "To Lower Case", NodeCategory.String)
    {
        // Input pin
        InputPins.Add(new NodePin(Guid.NewGuid().ToString(), id, "String", PinType.String, PinKind.Input));

        // Output pin
        OutputPins.Add(new NodePin(Guid.NewGuid().ToString(), id, "Result", PinType.String, PinKind.Output));
    }

    public override void Execute(VScriptContext context)
    {
        var inputPin = InputPins[0];
        var outputPin = OutputPins[0];

        string value = inputPin.Value?.ToString() ?? "";
        outputPin.Value = value.ToLower();
    }
}
