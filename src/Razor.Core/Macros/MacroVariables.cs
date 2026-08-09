#region license
// Razor: An Ultima Online Assistant
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

// Portiert aus Razor CE (Razor/Macros/MacroVariables.cs).
// Macro-Variablen: benannte Ziele (TargetInfo) fuer AbsoluteTargetVariable-/
// DoubleClickVariable-/SetMacroVariableTarget-Actions, profil-persistent als
// "macrovariables"-Sektion (CE-Dateiformat -> alte Profile bleiben kompatibel).
// Abweichungen vom Original: Save schreibt Z korrekt (CE-Bug: schrieb X als z);
// statt Engine.MainWindow.SaveMacroVariables laeuft Persistenz ueber
// Config.Save() und die UI haengt sich an das Changed-Event.

using System;
using System.Collections.Generic;
using System.Xml;

namespace Assistant.Macros
{
    public class MacroVariables
    {
        public class MacroVariable
        {
            public TargetInfo TargetInfo { get; set; }
            public string Name { get; set; }

            public bool TargetWasSet { get; set; }

            public MacroVariable(string targetVarName, TargetInfo t)
            {
                TargetInfo = t;
                Name = targetVarName;
                TargetWasSet = true;
            }

            /// <summary>Neues Ziel fuer diese Variable anfordern (Ingame-Cursor).
            /// Wird auch von SetMacroVariableTargetAction.Perform benutzt.</summary>
            public void TargetSetMacroVariable()
            {
                if (World.Player != null)
                {
                    TargetWasSet = false;

                    Targeting.OneTimeTarget(OnMacroVariableTarget);
                    World.Player.SendMessage(MsgLevel.Force, $"Select target for ${Name}");
                }
            }

            private void OnMacroVariableTarget(bool ground, Serial serial, Point3D pt, ushort gfx)
            {
                TargetInfo = new TargetInfo
                {
                    Gfx = gfx,
                    Serial = serial,
                    Type = (byte) (ground ? 1 : 0),
                    X = pt.X,
                    Y = pt.Y,
                    Z = pt.Z
                };

                World.Player?.SendMessage(MsgLevel.Force,
                    $"'{Name}' macro variable updated to '{TargetInfo.Serial}'");

                Config.Save();
                TargetWasSet = true;
                RaiseChanged();
            }
        }

        public static List<MacroVariable> MacroVariableList = new List<MacroVariable>();

        /// <summary>Feuert nach jeder Aenderung der Liste (Game-Thread) — die UI
        /// marshallt selbst auf den UI-Thread.</summary>
        public static event Action Changed;

        internal static void RaiseChanged()
        {
            Changed?.Invoke();
        }

        /// <summary>Bindet die "macrovariables"-Profilsektion an (CE-Format).
        /// Von RazorPlugin.Initialize VOR Config.LoadLastProfile aufzurufen.</summary>
        public static void Initialize()
        {
            ProfileSections.Register("macrovariables", Load, Save, ClearAll);
        }

        public static MacroVariable Find(string name)
        {
            foreach (MacroVariable mV in MacroVariableList)
            {
                if (mV.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
                    return mV;
            }

            return null;
        }

        /// <summary>Variable anlegen bzw. bei gleichem Namen das Ziel ersetzen
        /// (UI-Add-Flow); speichert das Profil.</summary>
        public static void AddOrUpdate(string name, TargetInfo t)
        {
            MacroVariable existing = Find(name);

            if (existing != null)
                existing.TargetInfo = t;
            else
                MacroVariableList.Add(new MacroVariable(name, t));

            Config.Save();
            RaiseChanged();
        }

        /// <summary>Variable entfernen (UI-Remove-Flow); speichert das Profil.</summary>
        public static void Remove(string name)
        {
            MacroVariable existing = Find(name);
            if (existing == null)
                return;

            MacroVariableList.Remove(existing);
            Config.Save();
            RaiseChanged();
        }

        public static void Save(XmlWriter xml)
        {
            foreach (MacroVariable target in MacroVariableList)
            {
                xml.WriteStartElement("macrovariable");
                xml.WriteAttributeString("type", target.TargetInfo.Type.ToString());
                xml.WriteAttributeString("flags", target.TargetInfo.Flags.ToString());
                xml.WriteAttributeString("serial", target.TargetInfo.Serial.ToString());
                xml.WriteAttributeString("x", target.TargetInfo.X.ToString());
                xml.WriteAttributeString("y", target.TargetInfo.Y.ToString());
                xml.WriteAttributeString("z", target.TargetInfo.Z.ToString());
                xml.WriteAttributeString("gfx", target.TargetInfo.Gfx.ToString());
                xml.WriteAttributeString("name", target.Name);
                xml.WriteEndElement();
            }
        }

        public static void Load(XmlElement node)
        {
            ClearAll();

            try
            {
                foreach (XmlElement el in node.GetElementsByTagName("macrovariable"))
                {
                    TargetInfo target = new TargetInfo
                    {
                        Type = Convert.ToByte(el.GetAttribute("type")),
                        Flags = Convert.ToByte(el.GetAttribute("flags")),
                        Serial = Serial.Parse(el.GetAttribute("serial")),
                        X = Convert.ToInt32(el.GetAttribute("x")),
                        Y = Convert.ToInt32(el.GetAttribute("y")),
                        Z = Convert.ToInt32(el.GetAttribute("z")),
                        Gfx = Convert.ToUInt16(el.GetAttribute("gfx"))
                    };

                    MacroVariableList.Add(new MacroVariable(el.GetAttribute("name"), target));
                }
            }
            catch
            {
                // ignored
            }

            RaiseChanged();
        }

        public static void ClearAll()
        {
            MacroVariableList.Clear();
            RaiseChanged();
        }
    }
}
