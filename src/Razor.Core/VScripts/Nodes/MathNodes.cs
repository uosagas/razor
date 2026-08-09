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

// Add two numbers
public class AddNumbersNode : VScriptNode
{
    public AddNumbersNode(string id, string pinIdCounter) : base(id, "Add", NodeCategory.Math)
    {
        // Input pins
        InputPins.Add(new NodePin(Guid.NewGuid().ToString(), id, "A", PinType.Number, PinKind.Input));
        InputPins.Add(new NodePin(Guid.NewGuid().ToString(), id, "B", PinType.Number, PinKind.Input));

        // Output pin
        OutputPins.Add(new NodePin(Guid.NewGuid().ToString(), id, "Result", PinType.Number, PinKind.Output));
    }

    public override void Execute(VScriptContext context)
    {
        var pinA = InputPins[0];
        var pinB = InputPins[1];
        var outputPin = OutputPins[0];

        float valueA = pinA.Value != null ? Convert.ToSingle(pinA.Value) : 0f;
        float valueB = pinB.Value != null ? Convert.ToSingle(pinB.Value) : 0f;

        outputPin.Value = valueA + valueB;
    }
}

// Subtract two numbers
public class SubtractNumbersNode : VScriptNode
{
    public SubtractNumbersNode(string id, string pinIdCounter) : base(id, "Subtract", NodeCategory.Math)
    {
        // Input pins
        InputPins.Add(new NodePin(Guid.NewGuid().ToString(), id, "A", PinType.Number, PinKind.Input));
        InputPins.Add(new NodePin(Guid.NewGuid().ToString(), id, "B", PinType.Number, PinKind.Input));

        // Output pin
        OutputPins.Add(new NodePin(Guid.NewGuid().ToString(), id, "Result", PinType.Number, PinKind.Output));
    }

    public override void Execute(VScriptContext context)
    {
        var pinA = InputPins[0];
        var pinB = InputPins[1];
        var outputPin = OutputPins[0];

        float valueA = pinA.Value != null ? Convert.ToSingle(pinA.Value) : 0f;
        float valueB = pinB.Value != null ? Convert.ToSingle(pinB.Value) : 0f;

        outputPin.Value = valueA - valueB;
    }
}

// Multiply two numbers
public class MultiplyNumbersNode : VScriptNode
{
    public MultiplyNumbersNode(string id, string pinIdCounter) : base(id, "Multiply", NodeCategory.Math)
    {
        // Input pins
        InputPins.Add(new NodePin(Guid.NewGuid().ToString(), id, "A", PinType.Number, PinKind.Input));
        InputPins.Add(new NodePin(Guid.NewGuid().ToString(), id, "B", PinType.Number, PinKind.Input));

        // Output pin
        OutputPins.Add(new NodePin(Guid.NewGuid().ToString(), id, "Result", PinType.Number, PinKind.Output));
    }

    public override void Execute(VScriptContext context)
    {
        var pinA = InputPins[0];
        var pinB = InputPins[1];
        var outputPin = OutputPins[0];

        float valueA = pinA.Value != null ? Convert.ToSingle(pinA.Value) : 0f;
        float valueB = pinB.Value != null ? Convert.ToSingle(pinB.Value) : 0f;

        outputPin.Value = valueA * valueB;
    }
}

// Divide two numbers
public class DivideNumbersNode : VScriptNode
{
    public DivideNumbersNode(string id, string pinIdCounter) : base(id, "Divide", NodeCategory.Math)
    {
        // Input pins
        InputPins.Add(new NodePin(Guid.NewGuid().ToString(), id, "A", PinType.Number, PinKind.Input));
        InputPins.Add(new NodePin(Guid.NewGuid().ToString(), id, "B", PinType.Number, PinKind.Input));

        // Output pin
        OutputPins.Add(new NodePin(Guid.NewGuid().ToString(), id, "Result", PinType.Number, PinKind.Output));
    }

    public override void Execute(VScriptContext context)
    {
        var pinA = InputPins[0];
        var pinB = InputPins[1];
        var outputPin = OutputPins[0];

        float valueA = pinA.Value != null ? Convert.ToSingle(pinA.Value) : 0f;
        float valueB = pinB.Value != null ? Convert.ToSingle(pinB.Value) : 1f; // Default to 1 to avoid division by zero

        if (valueB != 0f)
        {
            outputPin.Value = valueA / valueB;
        }
        else
        {
            outputPin.Value = 0f;
            context.ErrorMessage = "Division by zero";
        }
    }
}

// Modulo operation
public class ModuloNode : VScriptNode
{
    public ModuloNode(string id, string pinIdCounter) : base(id, "Modulo", NodeCategory.Math)
    {
        // Input pins
        InputPins.Add(new NodePin(Guid.NewGuid().ToString(), id, "A", PinType.Number, PinKind.Input));
        InputPins.Add(new NodePin(Guid.NewGuid().ToString(), id, "B", PinType.Number, PinKind.Input));

        // Output pin
        OutputPins.Add(new NodePin(Guid.NewGuid().ToString(), id, "Result", PinType.Number, PinKind.Output));
    }

    public override void Execute(VScriptContext context)
    {
        var pinA = InputPins[0];
        var pinB = InputPins[1];
        var outputPin = OutputPins[0];

        float valueA = pinA.Value != null ? Convert.ToSingle(pinA.Value) : 0f;
        float valueB = pinB.Value != null ? Convert.ToSingle(pinB.Value) : 1f;

        if (valueB != 0f)
        {
            outputPin.Value = valueA % valueB;
        }
        else
        {
            outputPin.Value = 0f;
        }
    }
}

// Power (exponentiation)
public class PowerNode : VScriptNode
{
    public PowerNode(string id, string pinIdCounter) : base(id, "Power", NodeCategory.Math)
    {
        // Input pins
        InputPins.Add(new NodePin(Guid.NewGuid().ToString(), id, "Base", PinType.Number, PinKind.Input));
        InputPins.Add(new NodePin(Guid.NewGuid().ToString(), id, "Exponent", PinType.Number, PinKind.Input));

        // Output pin
        OutputPins.Add(new NodePin(Guid.NewGuid().ToString(), id, "Result", PinType.Number, PinKind.Output));
    }

    public override void Execute(VScriptContext context)
    {
        var basePin = InputPins[0];
        var exponentPin = InputPins[1];
        var outputPin = OutputPins[0];

        float baseValue = basePin.Value != null ? Convert.ToSingle(basePin.Value) : 0f;
        float exponentValue = exponentPin.Value != null ? Convert.ToSingle(exponentPin.Value) : 1f;

        outputPin.Value = MathF.Pow(baseValue, exponentValue);
    }
}

// Square root
public class SquareRootNode : VScriptNode
{
    public SquareRootNode(string id, string pinIdCounter) : base(id, "Square Root", NodeCategory.Math)
    {
        // Input pin
        InputPins.Add(new NodePin(Guid.NewGuid().ToString(), id, "Value", PinType.Number, PinKind.Input));

        // Output pin
        OutputPins.Add(new NodePin(Guid.NewGuid().ToString(), id, "Result", PinType.Number, PinKind.Output));
    }

    public override void Execute(VScriptContext context)
    {
        var inputPin = InputPins[0];
        var outputPin = OutputPins[0];

        float value = inputPin.Value != null ? Convert.ToSingle(inputPin.Value) : 0f;

        if (value >= 0f)
        {
            outputPin.Value = MathF.Sqrt(value);
        }
        else
        {
            outputPin.Value = 0f;
            context.ErrorMessage = "Cannot calculate square root of negative number";
        }
    }
}

// Absolute value
public class AbsoluteNode : VScriptNode
{
    public AbsoluteNode(string id, string pinIdCounter) : base(id, "Absolute", NodeCategory.Math)
    {
        // Input pin
        InputPins.Add(new NodePin(Guid.NewGuid().ToString(), id, "Value", PinType.Number, PinKind.Input));

        // Output pin
        OutputPins.Add(new NodePin(Guid.NewGuid().ToString(), id, "Result", PinType.Number, PinKind.Output));
    }

    public override void Execute(VScriptContext context)
    {
        var inputPin = InputPins[0];
        var outputPin = OutputPins[0];

        float value = inputPin.Value != null ? Convert.ToSingle(inputPin.Value) : 0f;
        outputPin.Value = MathF.Abs(value);
    }
}

// Min of two numbers
public class MinNode : VScriptNode
{
    public MinNode(string id, string pinIdCounter) : base(id, "Min", NodeCategory.Math)
    {
        // Input pins
        InputPins.Add(new NodePin(Guid.NewGuid().ToString(), id, "A", PinType.Number, PinKind.Input));
        InputPins.Add(new NodePin(Guid.NewGuid().ToString(), id, "B", PinType.Number, PinKind.Input));

        // Output pin
        OutputPins.Add(new NodePin(Guid.NewGuid().ToString(), id, "Result", PinType.Number, PinKind.Output));
    }

    public override void Execute(VScriptContext context)
    {
        var pinA = InputPins[0];
        var pinB = InputPins[1];
        var outputPin = OutputPins[0];

        float valueA = pinA.Value != null ? Convert.ToSingle(pinA.Value) : 0f;
        float valueB = pinB.Value != null ? Convert.ToSingle(pinB.Value) : 0f;

        outputPin.Value = MathF.Min(valueA, valueB);
    }
}

// Max of two numbers
public class MaxNode : VScriptNode
{
    public MaxNode(string id, string pinIdCounter) : base(id, "Max", NodeCategory.Math)
    {
        // Input pins
        InputPins.Add(new NodePin(Guid.NewGuid().ToString(), id, "A", PinType.Number, PinKind.Input));
        InputPins.Add(new NodePin(Guid.NewGuid().ToString(), id, "B", PinType.Number, PinKind.Input));

        // Output pin
        OutputPins.Add(new NodePin(Guid.NewGuid().ToString(), id, "Result", PinType.Number, PinKind.Output));
    }

    public override void Execute(VScriptContext context)
    {
        var pinA = InputPins[0];
        var pinB = InputPins[1];
        var outputPin = OutputPins[0];

        float valueA = pinA.Value != null ? Convert.ToSingle(pinA.Value) : 0f;
        float valueB = pinB.Value != null ? Convert.ToSingle(pinB.Value) : 0f;

        outputPin.Value = MathF.Max(valueA, valueB);
    }
}

// Clamp value between min and max
public class ClampNode : VScriptNode
{
    public ClampNode(string id, string pinIdCounter) : base(id, "Clamp", NodeCategory.Math)
    {
        // Input pins
        InputPins.Add(new NodePin(Guid.NewGuid().ToString(), id, "Value", PinType.Number, PinKind.Input));
        InputPins.Add(new NodePin(Guid.NewGuid().ToString(), id, "Min", PinType.Number, PinKind.Input));
        InputPins.Add(new NodePin(Guid.NewGuid().ToString(), id, "Max", PinType.Number, PinKind.Input));

        // Output pin
        OutputPins.Add(new NodePin(Guid.NewGuid().ToString(), id, "Result", PinType.Number, PinKind.Output));
    }

    public override void Execute(VScriptContext context)
    {
        var valuePin = InputPins[0];
        var minPin = InputPins[1];
        var maxPin = InputPins[2];
        var outputPin = OutputPins[0];

        float value = valuePin.Value != null ? Convert.ToSingle(valuePin.Value) : 0f;
        float min = minPin.Value != null ? Convert.ToSingle(minPin.Value) : 0f;
        float max = maxPin.Value != null ? Convert.ToSingle(maxPin.Value) : 1f;

        outputPin.Value = Math.Clamp(value, min, max);
    }
}

// Round to nearest integer
public class RoundNode : VScriptNode
{
    public RoundNode(string id, string pinIdCounter) : base(id, "Round", NodeCategory.Math)
    {
        // Input pin
        InputPins.Add(new NodePin(Guid.NewGuid().ToString(), id, "Value", PinType.Number, PinKind.Input));

        // Output pin
        OutputPins.Add(new NodePin(Guid.NewGuid().ToString(), id, "Result", PinType.Number, PinKind.Output));
    }

    public override void Execute(VScriptContext context)
    {
        var inputPin = InputPins[0];
        var outputPin = OutputPins[0];

        float value = inputPin.Value != null ? Convert.ToSingle(inputPin.Value) : 0f;
        outputPin.Value = MathF.Round(value);
    }
}

// Floor (round down)
public class FloorNode : VScriptNode
{
    public FloorNode(string id, string pinIdCounter) : base(id, "Floor", NodeCategory.Math)
    {
        // Input pin
        InputPins.Add(new NodePin(Guid.NewGuid().ToString(), id, "Value", PinType.Number, PinKind.Input));

        // Output pin
        OutputPins.Add(new NodePin(Guid.NewGuid().ToString(), id, "Result", PinType.Number, PinKind.Output));
    }

    public override void Execute(VScriptContext context)
    {
        var inputPin = InputPins[0];
        var outputPin = OutputPins[0];

        float value = inputPin.Value != null ? Convert.ToSingle(inputPin.Value) : 0f;
        outputPin.Value = MathF.Floor(value);
    }
}

// Ceiling (round up)
public class CeilingNode : VScriptNode
{
    public CeilingNode(string id, string pinIdCounter) : base(id, "Ceiling", NodeCategory.Math)
    {
        // Input pin
        InputPins.Add(new NodePin(Guid.NewGuid().ToString(), id, "Value", PinType.Number, PinKind.Input));

        // Output pin
        OutputPins.Add(new NodePin(Guid.NewGuid().ToString(), id, "Result", PinType.Number, PinKind.Output));
    }

    public override void Execute(VScriptContext context)
    {
        var inputPin = InputPins[0];
        var outputPin = OutputPins[0];

        float value = inputPin.Value != null ? Convert.ToSingle(inputPin.Value) : 0f;
        outputPin.Value = MathF.Ceiling(value);
    }
}
