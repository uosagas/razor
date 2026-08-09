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

// Portiert aus Razor CE (Razor/Agents/IgnoreAgent.cs) — Phase 2d.
// ENTFERNT gegenueber Razor CE (dokumentiert):
//  * WinForms-UI (OnSelected/OnButtonPress/MessageBox), HotKey.Add (Phase 3),
//  * MessageManager-Hook (Mobile-Sprachnachrichten blocken) — der
//    MessageManager ist noch nicht portiert; IsIgnored() ist public und wird
//    dort spaeter angebunden (TODO Phase 3),
//  * ObjPropList-"Razor Ignored"-Labels.
// Save/Load (enabled-Attribut, <ignore serial=.. name=.. />) byte-kompatibel.

using System;
using System.Collections.Generic;
using System.Xml;

namespace Assistant.Agents
{
    public class IgnoreAgent : Agent
    {
        public static IgnoreAgent Instance { get; private set; }

        public static void Initialize()
        {
            Agent.Add(Instance = new IgnoreAgent());
        }

        public static bool IsIgnored(Serial ser)
        {
            return Instance?.IsSerialIgnored(ser) ?? false;
        }

        private readonly List<Serial> m_Chars;
        private readonly Dictionary<Serial, string> m_Names;
        private static bool m_Enabled;

        public IgnoreAgent()
        {
            m_Chars = new List<Serial>();
            m_Names = new Dictionary<Serial, string>();

            HotKey.Add(HKCategory.Targets, LocString.AddToIgnore, new HotKeyCallback(AddToIgnoreList));
            HotKey.Add(HKCategory.Targets, LocString.RemoveFromIgnore, new HotKeyCallback(RemoveFromIgnoreList));

            Number = 0;
        }

        public override void Clear()
        {
            m_Chars.Clear();
            m_Names.Clear();
        }

        public static bool IsEnabled()
        {
            return m_Enabled;
        }

        public bool Enabled
        {
            get { return m_Enabled; }
            set { m_Enabled = value; }
        }

        public List<Serial> Chars
        {
            get { return m_Chars; }
        }

        public bool IsSerialIgnored(Serial ser)
        {
            if (m_Enabled)
            {
                return m_Chars.Contains(ser);
            }
            else
            {
                return false;
            }
        }

        // XML-Elementname im Profil — MUSS dem CE-Language-String (1988) entsprechen.
        public override string Name
        {
            get { return "IgnoreList"; }
        }

        public override string Alias { get; set; }

        public override int Number { get; }

        public void AddToIgnoreList()
        {
            World.Player.SendMessage(MsgLevel.Force, LocString.AddToIgnore);
            Targeting.OneTimeTarget(new Targeting.TargetResponseCallback(OnAddTarget));
        }

        public void RemoveFromIgnoreList()
        {
            World.Player.SendMessage(MsgLevel.Force, LocString.RemoveFromIgnore);
            Targeting.OneTimeTarget(new Targeting.TargetResponseCallback(OnRemoveTarget));
        }

        private void OnAddTarget(bool location, Serial serial, Point3D loc, ushort gfx)
        {
            if (!location && serial.IsMobile && serial != World.Player.Serial)
            {
                World.Player.SendMessage(MsgLevel.Force, LocString.AddToIgnore);
                if (!m_Chars.Contains(serial))
                {
                    m_Chars.Add(serial);

                    Mobile m = World.FindMobile(serial);
                    if (m != null && !string.IsNullOrEmpty(m.Name))
                    {
                        m_Names[serial] = m.Name;
                    }
                }
            }
        }

        private void OnRemoveTarget(bool location, Serial serial, Point3D loc, ushort gfx)
        {
            if (!location && serial.IsMobile && serial != World.Player.Serial)
            {
                m_Chars.Remove(serial);
                m_Names.Remove(serial);

                World.Player.SendMessage(MsgLevel.Force, LocString.RemoveFromIgnore);
            }
        }

        public override void Save(XmlWriter xml)
        {
            xml.WriteAttributeString("enabled", m_Enabled.ToString());
            for (int i = 0; i < m_Chars.Count; i++)
            {
                xml.WriteStartElement("ignore");
                xml.WriteAttributeString("serial", m_Chars[i].ToString());
                try
                {
                    if (m_Names.ContainsKey((Serial) m_Chars[i]))
                    {
                        xml.WriteAttributeString("name", m_Names[(Serial) m_Chars[i]].ToString());
                    }
                }
                catch
                {
                }

                xml.WriteEndElement();
            }
        }

        public override void Load(XmlElement node)
        {
            try
            {
                m_Enabled = Convert.ToBoolean(node.GetAttribute("enabled"));
            }
            catch
            {
                // ignored
            }

            foreach (XmlElement el in node.GetElementsByTagName("ignore"))
            {
                try
                {
                    Serial toAdd = Serial.Parse(el.GetAttribute("serial"));

                    if (!m_Chars.Contains(toAdd))
                    {
                        m_Chars.Add(toAdd);
                    }

                    string name = el.GetAttribute("name");
                    if (!string.IsNullOrEmpty(name))
                    {
                        m_Names.Add(toAdd, name.Trim());
                    }
                }
                catch
                {
                    // ignored
                }
            }
        }
    }
}
