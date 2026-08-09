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

// Branch node - executes different paths based on boolean condition
public class BranchNode : VScriptNode
{
    public BranchNode(string id, string pinIdCounter) : base(id, "Branch", NodeCategory.Logic)
    {
        // Input pins
        InputPins.Add(new NodePin(Guid.NewGuid().ToString(), id, "", PinType.Flow, PinKind.Input));
        InputPins.Add(new NodePin(Guid.NewGuid().ToString(), id, "Condition", PinType.Boolean, PinKind.Input));

        // Output pins
        OutputPins.Add(new NodePin(Guid.NewGuid().ToString(), id, "True", PinType.Flow, PinKind.Output));
        OutputPins.Add(new NodePin(Guid.NewGuid().ToString(), id, "False", PinType.Flow, PinKind.Output));
    }

    public override void Execute(VScriptContext context)
    {
        // Branch nodes are handled specially by the execution engine
        // This is just for validation
        var conditionPin = InputPins.Find(p => p.Name == "Condition");
        if (conditionPin?.Value == null)
        {
            context.ErrorMessage = "Branch: Condition must be specified";
        }
    }

    public bool GetCondition()
    {
        var pin = InputPins.Find(p => p.Name == "Condition");
        return pin?.Value != null && Convert.ToBoolean(pin.Value);
    }

    public NodePin GetTruePin()
    {
        return OutputPins.Find(p => p.Name == "True");
    }

    public NodePin GetFalsePin()
    {
        return OutputPins.Find(p => p.Name == "False");
    }
}

// Compare two numbers
public class CompareNumbersNode : VScriptNode
{
    public CompareNumbersNode(string id, string pinIdCounter) : base(id, "Compare", NodeCategory.Logic)
    {
        // Input pins
        InputPins.Add(new NodePin(Guid.NewGuid().ToString(), id, "A", PinType.Number, PinKind.Input));
        InputPins.Add(new NodePin(Guid.NewGuid().ToString(), id, "B", PinType.Number, PinKind.Input));

        // Output pins
        OutputPins.Add(new NodePin(Guid.NewGuid().ToString(), id, "A > B", PinType.Boolean, PinKind.Output));
        OutputPins.Add(new NodePin(Guid.NewGuid().ToString(), id, "A < B", PinType.Boolean, PinKind.Output));
        OutputPins.Add(new NodePin(Guid.NewGuid().ToString(), id, "A == B", PinType.Boolean, PinKind.Output));
    }

    public override void Execute(VScriptContext context)
    {
        var pinA = InputPins[0];
        var pinB = InputPins[1];

        float valueA = pinA.Value != null ? Convert.ToSingle(pinA.Value) : 0f;
        float valueB = pinB.Value != null ? Convert.ToSingle(pinB.Value) : 0f;

        OutputPins[0].Value = valueA > valueB;  // A > B
        OutputPins[1].Value = valueA < valueB;  // A < B
        OutputPins[2].Value = Math.Abs(valueA - valueB) < 0.0001f;  // A == B (with epsilon)
    }
}

// Boolean NOT
public class NotNode : VScriptNode
{
    public NotNode(string id, string pinIdCounter) : base(id, "NOT", NodeCategory.Logic)
    {
        // Input pin
        InputPins.Add(new NodePin(Guid.NewGuid().ToString(), id, "Value", PinType.Boolean, PinKind.Input));

        // Output pin
        OutputPins.Add(new NodePin(Guid.NewGuid().ToString(), id, "Result", PinType.Boolean, PinKind.Output));
    }

    public override void Execute(VScriptContext context)
    {
        var inputPin = InputPins[0];
        var outputPin = OutputPins[0];

        bool value = inputPin.Value != null && Convert.ToBoolean(inputPin.Value);
        outputPin.Value = !value;
    }
}

// Boolean AND
public class AndNode : VScriptNode
{
    public AndNode(string id, string pinIdCounter) : base(id, "AND", NodeCategory.Logic)
    {
        // Input pins
        InputPins.Add(new NodePin(Guid.NewGuid().ToString(), id, "A", PinType.Boolean, PinKind.Input));
        InputPins.Add(new NodePin(Guid.NewGuid().ToString(), id, "B", PinType.Boolean, PinKind.Input));

        // Output pin
        OutputPins.Add(new NodePin(Guid.NewGuid().ToString(), id, "Result", PinType.Boolean, PinKind.Output));
    }

    public override void Execute(VScriptContext context)
    {
        var pinA = InputPins[0];
        var pinB = InputPins[1];
        var outputPin = OutputPins[0];

        bool valueA = pinA.Value != null && Convert.ToBoolean(pinA.Value);
        bool valueB = pinB.Value != null && Convert.ToBoolean(pinB.Value);

        outputPin.Value = valueA && valueB;
    }
}

// Boolean OR
public class OrNode : VScriptNode
{
    public OrNode(string id, string pinIdCounter) : base(id, "OR", NodeCategory.Logic)
    {
        // Input pins
        InputPins.Add(new NodePin(Guid.NewGuid().ToString(), id, "A", PinType.Boolean, PinKind.Input));
        InputPins.Add(new NodePin(Guid.NewGuid().ToString(), id, "B", PinType.Boolean, PinKind.Input));

        // Output pin
        OutputPins.Add(new NodePin(Guid.NewGuid().ToString(), id, "Result", PinType.Boolean, PinKind.Output));
    }

    public override void Execute(VScriptContext context)
    {
        var pinA = InputPins[0];
        var pinB = InputPins[1];
        var outputPin = OutputPins[0];

        bool valueA = pinA.Value != null && Convert.ToBoolean(pinA.Value);
        bool valueB = pinB.Value != null && Convert.ToBoolean(pinB.Value);

        outputPin.Value = valueA || valueB;
    }
}

// Is Valid - checks if a value is valid (not null, not empty)
public class IsValidNode : VScriptNode
{
    public IsValidNode(string id, string pinIdCounter) : base(id, "Is Valid", NodeCategory.Logic)
    {
        // Input pin - accepts any type
        InputPins.Add(new NodePin(Guid.NewGuid().ToString(), id, "Value", PinType.Any, PinKind.Input));

        // Output pin - boolean result
        OutputPins.Add(new NodePin(Guid.NewGuid().ToString(), id, "Is Valid", PinType.Boolean, PinKind.Output));
    }

    public override void Execute(VScriptContext context)
    {
        var inputPin = InputPins[0];
        var outputPin = OutputPins[0];

        bool isValid = false;

        if (inputPin.Value != null)
        {
            // Check based on value type
            if (inputPin.Value is string str)
            {
                // String is valid if not empty
                isValid = !string.IsNullOrEmpty(str);
            }
            else if (inputPin.Value is int || inputPin.Value is float || inputPin.Value is double ||
                     inputPin.Value is long || inputPin.Value is short || inputPin.Value is byte)
            {
                // Numeric values are always valid if not null
                isValid = true;
            }
            else if (inputPin.Value is bool)
            {
                // Boolean values are always valid
                isValid = true;
            }
            else
            {
                // For objects, just check if not null
                isValid = true;
            }
        }

        outputPin.Value = isValid;
    }
}
