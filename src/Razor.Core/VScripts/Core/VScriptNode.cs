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

using System.Collections.Generic;
using System.Numerics;

namespace Assistant.VScripts.Core;

public enum NodeCategory
{
    Event,
    Action,
    Logic,
    Variable,
    Math,
    String,
    UI,
    Flow,
    Game,
    List
}

public abstract class VScriptNode
{
    public string Id { get; set; }
    public string Name { get; set; }
    public NodeCategory Category { get; set; }
    public Vector2 Position { get; set; }
    public List<NodePin> InputPins { get; set; }
    public List<NodePin> OutputPins { get; set; }
    public virtual float Width => 200.0f; // Default width, can be overridden
    public bool HasBreakpoint { get; set; } = false; // Breakpoint flag

    protected VScriptNode(string id, string name, NodeCategory category)
    {
        Id = id;
        Name = name;
        Category = category;
        Position = Vector2.Zero;
        InputPins = new List<NodePin>();
        OutputPins = new List<NodePin>();
    }

    public virtual Vector4 GetTitleBarColor()
    {
        return Category switch
        {
            NodeCategory.Event => new Vector4(0.8f, 0.2f, 0.2f, 1.0f),      // Red
            NodeCategory.Action => new Vector4(0.2f, 0.4f, 0.8f, 1.0f),     // Blue
            NodeCategory.Logic => new Vector4(0.3f, 0.7f, 0.3f, 1.0f),      // Green
            NodeCategory.Variable => new Vector4(0.6f, 0.3f, 0.8f, 1.0f),   // Purple
            NodeCategory.Math => new Vector4(0.2f, 0.8f, 0.6f, 1.0f),       // Cyan/Teal
            NodeCategory.String => new Vector4(0.8f, 0.3f, 0.6f, 1.0f),     // Pink
            NodeCategory.UI => new Vector4(0.9f, 0.7f, 0.2f, 1.0f),         // Orange/Yellow
            NodeCategory.Flow => new Vector4(0.5f, 0.5f, 0.5f, 1.0f),       // Gray
            NodeCategory.Game => new Vector4(0.4f, 0.6f, 0.9f, 1.0f),       // Light Blue
            NodeCategory.List => new Vector4(0.9f, 0.6f, 0.2f, 1.0f),       // Orange/Amber
            _ => new Vector4(0.4f, 0.4f, 0.4f, 1.0f)
        };
    }

    public abstract void Execute(VScriptContext context);
}
