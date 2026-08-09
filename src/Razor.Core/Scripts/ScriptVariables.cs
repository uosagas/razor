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

// Portiert aus Razor CE (Razor/Scripts/ScriptVariables.cs) — 1:1.
// Persistente Script-Variablen (Name -> Serial) im Profil-XML; jede Variable
// wird zusaetzlich als Interpreter-Alias registriert.

using System.Collections.Generic;
using System.Xml;
using Assistant.Scripts.Engine;

namespace Assistant.Scripts
{
    public class ScriptVariables
    {
        private static Dictionary<string, Serial> _variables = new Dictionary<string, Serial>();
        public static IEnumerable<KeyValuePair<string, Serial>> Variables => _variables;

        /// <summary>
        /// Bindet die Variablen als "scriptvariables"-Profilsektion an (Format
        /// wie Razor CE -> alte Profile bleiben kompatibel). Von
        /// RazorPlugin.Initialize VOR Config.LoadLastProfile aufzurufen.
        /// </summary>
        public static void Initialize()
        {
            ProfileSections.Register("scriptvariables", Load, Save, ClearAll);
        }

        public static void Save(XmlWriter xml)
        {
            foreach (KeyValuePair<string, Serial> kv in _variables)
            {
                xml.WriteStartElement("scriptvariable");
                xml.WriteAttributeString("serial", kv.Value.ToString());
                xml.WriteAttributeString("name", kv.Key);
                xml.WriteEndElement();
            }
        }

        public static void Load(XmlElement node)
        {
            ClearAll();

            try
            {
                foreach (XmlElement el in node.GetElementsByTagName("scriptvariable"))
                {
                    string name = el.GetAttribute("name");
                    Serial serial = Serial.Parse(el.GetAttribute("serial"));

                    RegisterVariable(name, serial);
                }
            }
            catch
            {
                // ignored
            }
        }

        public static void RegisterVariable(string name, Serial serial)
        {
            name = name.Trim();

            _variables[name] = serial;
            Interpreter.SetAlias(name, serial);
        }

        public static void UnregisterVariable(string name)
        {
            name = name.Trim();
            Interpreter.ClearAlias(name);
            _variables.Remove(name);
        }

        public static void ClearAll()
        {
            foreach (string key in new List<string>(_variables.Keys))
            {
                UnregisterVariable(key);
            }

            _variables.Clear();
        }

        public static Serial GetVariable(string name)
        {
            name = name.Trim();

            if (_variables.TryGetValue(name, out var val))
            {
                return val;
            }

            return Serial.MinusOne;
        }
    }
}
