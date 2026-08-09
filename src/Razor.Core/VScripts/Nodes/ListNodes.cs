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
using System.Linq;
using Assistant.VScripts.Core;

namespace Assistant.VScripts.Nodes;

// Add Element to List - adds element to end of list
public class ListAddNode : VScriptNode
{
    public ListAddNode(string id, string pinIdCounter) : base(id, "Add", NodeCategory.List)
    {
        InputPins.Add(new NodePin(Guid.NewGuid().ToString(), id, "", PinType.Flow, PinKind.Input));
        InputPins.Add(new NodePin(Guid.NewGuid().ToString(), id, "List", PinType.Any, PinKind.Input, isList: true));
        InputPins.Add(new NodePin(Guid.NewGuid().ToString(), id, "Element", PinType.Any, PinKind.Input));
        OutputPins.Add(new NodePin(Guid.NewGuid().ToString(), id, "", PinType.Flow, PinKind.Output));
    }

    public override void Execute(VScriptContext context)
    {
        var listPin = InputPins[1];
        var elementPin = InputPins[2];

        if (listPin.Value is IList list && elementPin.Value != null)
        {
            list.Add(elementPin.Value);
        }
        else
        {
            context.ErrorMessage = "Add: List is required";
        }
    }
}

// Insert Element at Index
public class ListInsertNode : VScriptNode
{
    public ListInsertNode(string id, string pinIdCounter) : base(id, "Insert", NodeCategory.List)
    {
        InputPins.Add(new NodePin(Guid.NewGuid().ToString(), id, "", PinType.Flow, PinKind.Input));
        InputPins.Add(new NodePin(Guid.NewGuid().ToString(), id, "List", PinType.Any, PinKind.Input, isList: true));
        InputPins.Add(new NodePin(Guid.NewGuid().ToString(), id, "Index", PinType.Number, PinKind.Input));
        InputPins.Add(new NodePin(Guid.NewGuid().ToString(), id, "Element", PinType.Any, PinKind.Input));
        OutputPins.Add(new NodePin(Guid.NewGuid().ToString(), id, "", PinType.Flow, PinKind.Output));
    }

    public override void Execute(VScriptContext context)
    {
        var listPin = InputPins[1];
        var indexPin = InputPins[2];
        var elementPin = InputPins[3];

        if (listPin.Value is IList list && indexPin.Value != null && elementPin.Value != null)
        {
            int index = System.Convert.ToInt32(indexPin.Value);
            if (index >= 0 && index <= list.Count)
            {
                list.Insert(index, elementPin.Value);
            }
            else
            {
                context.ErrorMessage = $"Insert: Index {index} out of range (0-{list.Count})";
            }
        }
        else
        {
            context.ErrorMessage = "Insert: List and Index are required";
        }
    }
}

// Remove Element from List
public class ListRemoveNode : VScriptNode
{
    public ListRemoveNode(string id, string pinIdCounter) : base(id, "Remove", NodeCategory.List)
    {
        InputPins.Add(new NodePin(Guid.NewGuid().ToString(), id, "", PinType.Flow, PinKind.Input));
        InputPins.Add(new NodePin(Guid.NewGuid().ToString(), id, "List", PinType.Any, PinKind.Input, isList: true));
        InputPins.Add(new NodePin(Guid.NewGuid().ToString(), id, "Element", PinType.Any, PinKind.Input));
        OutputPins.Add(new NodePin(Guid.NewGuid().ToString(), id, "", PinType.Flow, PinKind.Output));
        OutputPins.Add(new NodePin(Guid.NewGuid().ToString(), id, "Success", PinType.Boolean, PinKind.Output));
    }

    public override void Execute(VScriptContext context)
    {
        var listPin = InputPins[1];
        var elementPin = InputPins[2];

        if (listPin.Value is IList list && elementPin.Value != null)
        {
            bool removed = list.Contains(elementPin.Value);
            if (removed)
            {
                list.Remove(elementPin.Value);
            }
            OutputPins[1].Value = removed;
        }
        else
        {
            context.ErrorMessage = "Remove: List is required";
            OutputPins[1].Value = false;
        }
    }
}

// Remove Element at Index
public class ListRemoveAtNode : VScriptNode
{
    public ListRemoveAtNode(string id, string pinIdCounter) : base(id, "Remove At", NodeCategory.List)
    {
        InputPins.Add(new NodePin(Guid.NewGuid().ToString(), id, "", PinType.Flow, PinKind.Input));
        InputPins.Add(new NodePin(Guid.NewGuid().ToString(), id, "List", PinType.Any, PinKind.Input, isList: true));
        InputPins.Add(new NodePin(Guid.NewGuid().ToString(), id, "Index", PinType.Number, PinKind.Input));
        OutputPins.Add(new NodePin(Guid.NewGuid().ToString(), id, "", PinType.Flow, PinKind.Output));
    }

    public override void Execute(VScriptContext context)
    {
        var listPin = InputPins[1];
        var indexPin = InputPins[2];

        if (listPin.Value is IList list && indexPin.Value != null)
        {
            int index = System.Convert.ToInt32(indexPin.Value);
            if (index >= 0 && index < list.Count)
            {
                list.RemoveAt(index);
            }
            else
            {
                context.ErrorMessage = $"Remove At: Index {index} out of range (0-{list.Count - 1})";
            }
        }
        else
        {
            context.ErrorMessage = "Remove At: List and Index are required";
        }
    }
}

// Get Element at Index
public class ListGetNode : VScriptNode
{
    public ListGetNode(string id, string pinIdCounter) : base(id, "Get", NodeCategory.List)
    {
        InputPins.Add(new NodePin(Guid.NewGuid().ToString(), id, "List", PinType.Any, PinKind.Input, isList: true));
        InputPins.Add(new NodePin(Guid.NewGuid().ToString(), id, "Index", PinType.Number, PinKind.Input));
        OutputPins.Add(new NodePin(Guid.NewGuid().ToString(), id, "Element", PinType.Any, PinKind.Output));
    }

    public override void Execute(VScriptContext context)
    {
        var listPin = InputPins[0];
        var indexPin = InputPins[1];

        if (listPin.Value is IList list && indexPin.Value != null)
        {
            int index = System.Convert.ToInt32(indexPin.Value);
            if (index >= 0 && index < list.Count)
            {
                OutputPins[0].Value = list[index];
            }
            else
            {
                context.ErrorMessage = $"Get: Index {index} out of range (0-{list.Count - 1})";
                OutputPins[0].Value = null;
            }
        }
        else
        {
            context.ErrorMessage = "Get: List and Index are required";
            OutputPins[0].Value = null;
        }
    }
}

// Set Element at Index
public class ListSetNode : VScriptNode
{
    public ListSetNode(string id, string pinIdCounter) : base(id, "Set", NodeCategory.List)
    {
        InputPins.Add(new NodePin(Guid.NewGuid().ToString(), id, "", PinType.Flow, PinKind.Input));
        InputPins.Add(new NodePin(Guid.NewGuid().ToString(), id, "List", PinType.Any, PinKind.Input, isList: true));
        InputPins.Add(new NodePin(Guid.NewGuid().ToString(), id, "Index", PinType.Number, PinKind.Input));
        InputPins.Add(new NodePin(Guid.NewGuid().ToString(), id, "Element", PinType.Any, PinKind.Input));
        OutputPins.Add(new NodePin(Guid.NewGuid().ToString(), id, "", PinType.Flow, PinKind.Output));
    }

    public override void Execute(VScriptContext context)
    {
        var listPin = InputPins[1];
        var indexPin = InputPins[2];
        var elementPin = InputPins[3];

        if (listPin.Value is IList list && indexPin.Value != null && elementPin.Value != null)
        {
            int index = System.Convert.ToInt32(indexPin.Value);
            if (index >= 0 && index < list.Count)
            {
                list[index] = elementPin.Value;
            }
            else
            {
                context.ErrorMessage = $"Set: Index {index} out of range (0-{list.Count - 1})";
            }
        }
        else
        {
            context.ErrorMessage = "Set: List, Index, and Element are required";
        }
    }
}

// Get List Count/Length
public class ListCountNode : VScriptNode
{
    public ListCountNode(string id, string pinIdCounter) : base(id, "Count", NodeCategory.List)
    {
        InputPins.Add(new NodePin(Guid.NewGuid().ToString(), id, "List", PinType.Any, PinKind.Input, isList: true));
        OutputPins.Add(new NodePin(Guid.NewGuid().ToString(), id, "Count", PinType.Number, PinKind.Output));
    }

    public override void Execute(VScriptContext context)
    {
        var listPin = InputPins[0];

        if (listPin.Value is IList list)
        {
            OutputPins[0].Value = (float)list.Count;
        }
        else
        {
            OutputPins[0].Value = 0f;
        }
    }
}

// Clear all elements from List
public class ListClearNode : VScriptNode
{
    public ListClearNode(string id, string pinIdCounter) : base(id, "Clear", NodeCategory.List)
    {
        InputPins.Add(new NodePin(Guid.NewGuid().ToString(), id, "", PinType.Flow, PinKind.Input));
        InputPins.Add(new NodePin(Guid.NewGuid().ToString(), id, "List", PinType.Any, PinKind.Input, isList: true));
        OutputPins.Add(new NodePin(Guid.NewGuid().ToString(), id, "", PinType.Flow, PinKind.Output));
    }

    public override void Execute(VScriptContext context)
    {
        var listPin = InputPins[1];

        if (listPin.Value is IList list)
        {
            list.Clear();
        }
        else
        {
            context.ErrorMessage = "Clear: List is required";
        }
    }
}

// Check if List contains Element
public class ListContainsNode : VScriptNode
{
    public ListContainsNode(string id, string pinIdCounter) : base(id, "Contains", NodeCategory.List)
    {
        InputPins.Add(new NodePin(Guid.NewGuid().ToString(), id, "List", PinType.Any, PinKind.Input, isList: true));
        InputPins.Add(new NodePin(Guid.NewGuid().ToString(), id, "Element", PinType.Any, PinKind.Input));
        OutputPins.Add(new NodePin(Guid.NewGuid().ToString(), id, "Contains", PinType.Boolean, PinKind.Output));
    }

    public override void Execute(VScriptContext context)
    {
        var listPin = InputPins[0];
        var elementPin = InputPins[1];

        if (listPin.Value is IList list && elementPin.Value != null)
        {
            OutputPins[0].Value = list.Contains(elementPin.Value);
        }
        else
        {
            OutputPins[0].Value = false;
        }
    }
}

// Find Index of Element in List
public class ListIndexOfNode : VScriptNode
{
    public ListIndexOfNode(string id, string pinIdCounter) : base(id, "Index Of", NodeCategory.List)
    {
        InputPins.Add(new NodePin(Guid.NewGuid().ToString(), id, "List", PinType.Any, PinKind.Input, isList: true));
        InputPins.Add(new NodePin(Guid.NewGuid().ToString(), id, "Element", PinType.Any, PinKind.Input));
        OutputPins.Add(new NodePin(Guid.NewGuid().ToString(), id, "Index", PinType.Number, PinKind.Output));
    }

    public override void Execute(VScriptContext context)
    {
        var listPin = InputPins[0];
        var elementPin = InputPins[1];

        if (listPin.Value is IList list && elementPin.Value != null)
        {
            int index = list.IndexOf(elementPin.Value);
            OutputPins[0].Value = (float)index;  // -1 if not found
        }
        else
        {
            OutputPins[0].Value = -1f;
        }
    }
}

// ForEach Loop - Iterate over list elements
public class ForEachNode : VScriptNode
{
    public override float Width => 250.0f; // Wider than default nodes

    public ForEachNode(string id, string pinIdCounter) : base(id, "For Each", NodeCategory.Flow)
    {
        // Input pins
        InputPins.Add(new NodePin(Guid.NewGuid().ToString(), id, "", PinType.Flow, PinKind.Input));
        InputPins.Add(new NodePin(Guid.NewGuid().ToString(), id, "List", PinType.Any, PinKind.Input, isList: true));

        // Output pins
        OutputPins.Add(new NodePin(Guid.NewGuid().ToString(), id, "Loop Body", PinType.Flow, PinKind.Output));
        OutputPins.Add(new NodePin(Guid.NewGuid().ToString(), id, "Completed", PinType.Flow, PinKind.Output));
        OutputPins.Add(new NodePin(Guid.NewGuid().ToString(), id, "Element", PinType.Any, PinKind.Output));
        OutputPins.Add(new NodePin(Guid.NewGuid().ToString(), id, "Index", PinType.Number, PinKind.Output));
    }

    public override void Execute(VScriptContext context)
    {
        // This node requires special handling in the engine for proper loop execution
        // The Execute method here just validates inputs
        var listPin = InputPins.Find(p => p.Name == "List");

        if (listPin?.Value == null)
        {
            context.ErrorMessage = "ForEach: List must be specified";
            return;
        }

        if (listPin.Value is not IList)
        {
            context.ErrorMessage = "ForEach: Input must be a list";
            return;
        }
    }

    public IList GetList()
    {
        var pin = InputPins.Find(p => p.Name == "List");
        return pin?.Value as IList;
    }

    public void SetCurrentElement(object element)
    {
        var elementPin = OutputPins.Find(p => p.Name == "Element");
        if (elementPin != null)
        {
            elementPin.Value = element;
        }
    }

    public void SetCurrentIndex(int index)
    {
        var indexPin = OutputPins.Find(p => p.Name == "Index");
        if (indexPin != null)
        {
            indexPin.Value = (float)index;
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
