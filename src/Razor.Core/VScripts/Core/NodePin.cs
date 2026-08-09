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

using System.Numerics;

namespace Assistant.VScripts.Core;

public enum PinType
{
    Flow,      // Execution flow (white)
    Boolean,   // Boolean data (red)
    Number,    // Numeric data (green)
    String,    // String data (purple)
    Object,    // Object/Entity reference (blue)
    Variable,  // Variable reference (orange) - connection only, no inline input
    Any        // Any type (gray)
}

public enum PinKind
{
    Input,
    Output
}

public class NodePin
{
    public string Id { get; set; }
    public string NodeId { get; set; }
    public string Name { get; set; }
    public PinType Type { get; set; }
    public PinKind Kind { get; set; }
    public bool IsList { get; set; }
    public ObjectSubType ObjectSubType { get; set; } = ObjectSubType.Player; // For Object type pins
    public object Value { get; set; }

    public NodePin(string id, string nodeId, string name, PinType type, PinKind kind, bool isList = false, ObjectSubType objectSubType = ObjectSubType.Player)
    {
        Id = id;
        NodeId = nodeId;
        Name = name;
        Type = type;
        Kind = kind;
        IsList = isList;
        ObjectSubType = objectSubType;
        Value = null;
    }

    public Vector4 GetColor()
    {
        return Type switch
        {
            PinType.Flow => new Vector4(1.0f, 1.0f, 1.0f, 1.0f),        // White
            PinType.Boolean => new Vector4(0.8f, 0.2f, 0.2f, 1.0f),     // Red
            PinType.Number => new Vector4(0.2f, 0.8f, 0.2f, 1.0f),      // Green
            PinType.String => new Vector4(0.8f, 0.2f, 0.8f, 1.0f),      // Purple/Magenta
            PinType.Object => new Vector4(0.2f, 0.5f, 1.0f, 1.0f),      // Blue
            PinType.Variable => new Vector4(1.0f, 0.6f, 0.2f, 1.0f),    // Orange
            PinType.Any => new Vector4(0.7f, 0.7f, 0.7f, 1.0f),         // Gray
            _ => new Vector4(0.5f, 0.5f, 0.5f, 1.0f)
        };
    }
}
