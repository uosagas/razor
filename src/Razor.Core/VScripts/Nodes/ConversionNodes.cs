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

// Base class for conversion nodes
public abstract class ConversionNode : VScriptNode
{
    public override float Width => 40.0f; // Very compact - just a circle
    public PinType InputType { get; }
    public PinType OutputType { get; }

    protected ConversionNode(string id, string name, PinType inputType, PinType outputType, string pinIdCounter)
        : base(id, name, NodeCategory.Math)
    {
        InputType = inputType;
        OutputType = outputType;

        // Conversion nodes are pure functions (no flow pins)
        InputPins.Add(new NodePin(Guid.NewGuid().ToString(), id, "", inputType, PinKind.Input));
        OutputPins.Add(new NodePin(Guid.NewGuid().ToString(), id, "", outputType, PinKind.Output));
    }
}

// Number to String
public class NumberToStringNode : ConversionNode
{
    public NumberToStringNode(string id, string pinIdCounter)
        : base(id, "To String", PinType.Number, PinType.String, pinIdCounter)
    {
    }

    public override void Execute(VScriptContext context)
    {
        var inputPin = InputPins[0];
        var outputPin = OutputPins[0];

        if (inputPin.Value != null)
        {
            outputPin.Value = Convert.ToSingle(inputPin.Value).ToString();
        }
        else
        {
            outputPin.Value = "0";
        }
    }
}

// String to Number
public class StringToNumberNode : ConversionNode
{
    public StringToNumberNode(string id, string pinIdCounter)
        : base(id, "To Number", PinType.String, PinType.Number, pinIdCounter)
    {
    }

    public override void Execute(VScriptContext context)
    {
        var inputPin = InputPins[0];
        var outputPin = OutputPins[0];

        if (inputPin.Value != null && float.TryParse(inputPin.Value.ToString(), out float result))
        {
            outputPin.Value = result;
        }
        else
        {
            outputPin.Value = 0f;
        }
    }
}

// Boolean to String
public class BooleanToStringNode : ConversionNode
{
    public BooleanToStringNode(string id, string pinIdCounter)
        : base(id, "To String", PinType.Boolean, PinType.String, pinIdCounter)
    {
    }

    public override void Execute(VScriptContext context)
    {
        var inputPin = InputPins[0];
        var outputPin = OutputPins[0];

        if (inputPin.Value != null)
        {
            outputPin.Value = Convert.ToBoolean(inputPin.Value).ToString();
        }
        else
        {
            outputPin.Value = "False";
        }
    }
}

// String to Boolean
public class StringToBooleanNode : ConversionNode
{
    public StringToBooleanNode(string id, string pinIdCounter)
        : base(id, "To Boolean", PinType.String, PinType.Boolean, pinIdCounter)
    {
    }

    public override void Execute(VScriptContext context)
    {
        var inputPin = InputPins[0];
        var outputPin = OutputPins[0];

        if (inputPin.Value != null && bool.TryParse(inputPin.Value.ToString(), out bool result))
        {
            outputPin.Value = result;
        }
        else
        {
            outputPin.Value = false;
        }
    }
}

// Number to Boolean (0 = false, non-zero = true)
public class NumberToBooleanNode : ConversionNode
{
    public NumberToBooleanNode(string id, string pinIdCounter)
        : base(id, "To Boolean", PinType.Number, PinType.Boolean, pinIdCounter)
    {
    }

    public override void Execute(VScriptContext context)
    {
        var inputPin = InputPins[0];
        var outputPin = OutputPins[0];

        if (inputPin.Value != null)
        {
            outputPin.Value = Convert.ToSingle(inputPin.Value) != 0;
        }
        else
        {
            outputPin.Value = false;
        }
    }
}

// Boolean to Number (false = 0, true = 1)
public class BooleanToNumberNode : ConversionNode
{
    public BooleanToNumberNode(string id, string pinIdCounter)
        : base(id, "To Number", PinType.Boolean, PinType.Number, pinIdCounter)
    {
    }

    public override void Execute(VScriptContext context)
    {
        var inputPin = InputPins[0];
        var outputPin = OutputPins[0];

        if (inputPin.Value != null)
        {
            outputPin.Value = Convert.ToBoolean(inputPin.Value) ? 1f : 0f;
        }
        else
        {
            outputPin.Value = 0f;
        }
    }
}
