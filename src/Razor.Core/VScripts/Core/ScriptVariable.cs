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

// Portiert aus dem integrierten Assistant (VScripts/Core/ScriptVariable.cs).
// Client-Typen (ClassicUO Mobile/Item/LastTargetInfo/JournalEntry) sind durch
// die Razor-Aequivalente ersetzt (Assistant.Mobile/Item/TargetInfo,
// VScripts.Core.JournalEntry) — reine Laufzeit-Typen, kein Dateiformat.

namespace Assistant.VScripts.Core;

public enum ObjectSubType
{
    Player,
    Target,
    Mobile,
    Journal,
    JournalEntry,
    Item,
    Gump
}

public enum VariableScope
{
    Local,
    Global,
    Parameter,  // Input parameter - shows as output pin on StartNode, input pin on ExecuteScriptNode
    Output      // Output value - shows as input pin on ReturnNode, output pin on ExecuteScriptNode
}

public class ScriptVariable
{
    public string Name { get; set; }
    public PinType Type { get; set; }
    public ObjectSubType ObjectSubType { get; set; }
    public VariableScope Scope { get; set; }
    public bool IsList { get; set; }
    public object DefaultValue { get; set; }

    public ScriptVariable(string name, PinType type, ObjectSubType objectSubType = ObjectSubType.Player, VariableScope scope = VariableScope.Local, bool isList = false)
    {
        Name = name;
        Type = type;
        ObjectSubType = objectSubType;
        Scope = scope;
        IsList = isList;
        DefaultValue = GetDefaultValueForType(type, objectSubType, isList);
    }

    private object GetDefaultValueForType(PinType type, ObjectSubType objectSubType, bool isList)
    {
        if (isList)
        {
            // Initialize empty typed list immediately
            if (type == PinType.Object)
            {
                // For Object types, use the appropriate object subtype
                return objectSubType switch
                {
                    ObjectSubType.Player => new System.Collections.Generic.List<Assistant.Mobile>(),
                    ObjectSubType.Target => new System.Collections.Generic.List<Assistant.TargetInfo>(),
                    ObjectSubType.Mobile => new System.Collections.Generic.List<Assistant.Mobile>(),
                    ObjectSubType.JournalEntry => new System.Collections.Generic.List<JournalEntry>(),
                    ObjectSubType.Item => new System.Collections.Generic.List<Assistant.Item>(),
                    _ => new System.Collections.Generic.List<object>()
                };
            }

            return type switch
            {
                PinType.Number => new System.Collections.Generic.List<float>(),
                PinType.String => new System.Collections.Generic.List<string>(),
                PinType.Boolean => new System.Collections.Generic.List<bool>(),
                _ => new System.Collections.Generic.List<object>()
            };
        }

        return type switch
        {
            PinType.Boolean => false,
            PinType.Number => 0f,
            PinType.String => "",
            PinType.Object => null,
            _ => null
        };
    }
}
