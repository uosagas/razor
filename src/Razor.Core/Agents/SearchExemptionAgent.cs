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

// Portiert aus Razor CE (Razor/Agents/SearchExemptionAgent.cs) — Phase 2d.
// IsExempt() haengt im Counter-Pfad (Item.UpdateContainer) wie im Original;
// der AutoSearch-Pfad (automatisches Containeroeffnen) ist noch nicht
// portiert (2b-TODO) und nutzt IsExempt spaeter ebenfalls.
// ENTFERNT gegenueber Razor CE (dokumentiert):
//  * WinForms-UI (OnSelected/OnButtonPress/MessageBox),
//  * ContainerItem-Injection zum Umfaerben (SendToClient bleibt beim
//    Add/Remove-Target erhalten, s.u.), Engine.MainWindow-Fokus.
// Save/Load (<item serial=... /> bzw. <item id=... />) byte-kompatibel.

using System;
using System.Collections;
using System.Xml;

namespace Assistant.Agents
{
    public class SearchExemptionAgent : Agent
    {
        public static int Count
        {
            get { return Instance.m_Items.Count; }
        }

        public static SearchExemptionAgent Instance { get; private set; }

        public static void Initialize()
        {
            Agent.Add(Instance = new SearchExemptionAgent());
        }

        public static bool IsExempt(Item item)
        {
            if (item == null || item.IsBagOfSending)
            {
                return true;
            }

            return Instance == null ? false : Instance.CheckExempt(item);
        }

        public static bool Contains(Item item)
        {
            return Instance == null
                ? false
                : Instance.m_Items.Contains(item.Serial) || Instance.m_Items.Contains(item.ItemID);
        }

        private readonly ArrayList m_Items;

        public SearchExemptionAgent()
        {
            m_Items = new ArrayList();
        }

        public override void Clear()
        {
            m_Items.Clear();
        }

        public ArrayList Items
        {
            get { return m_Items; }
        }

        private bool CheckExempt(Item item)
        {
            if (m_Items.Count > 0)
            {
                if (m_Items.Contains(item.Serial))
                {
                    return true;
                }
                else if (m_Items.Contains(item.ItemID))
                {
                    return true;
                }
                else if (item.Container != null && item.Container is Item)
                {
                    return CheckExempt((Item) item.Container);
                }
            }

            return false;
        }

        // XML-Elementname im Profil — MUSS dem CE-Language-String (1180) entsprechen.
        public override string Name
        {
            get { return "AutoSearchExemptions"; }
        }

        public override string Alias { get; set; }

        public override int Number { get; }

        public void AddItemTarget()
        {
            World.Player.SendMessage(MsgLevel.Force, LocString.TargItemAdd);
            Targeting.OneTimeTarget(new Targeting.TargetResponseCallback(OnTarget));
        }

        public void AddTypeTarget()
        {
            World.Player.SendMessage(MsgLevel.Force, LocString.TargItemAdd);
            Targeting.OneTimeTarget(new Targeting.TargetResponseCallback(OnTargetType));
        }

        public void RemoveItemTarget()
        {
            World.Player.SendMessage(MsgLevel.Force, LocString.TargItemRem);
            Targeting.OneTimeTarget(new Targeting.TargetResponseCallback(OnTargetRemove));
        }

        private void OnTarget(bool location, Serial serial, Point3D loc, ushort gfx)
        {
            if (!location && serial.IsItem)
            {
                m_Items.Add(serial);

                Item item = World.FindItem(serial);
                if (item != null)
                {
                    // Razor CE: Refresh im Client (Umfaerbung des Exempt-Items).
                    ClientProxy.SendToClient(new ContainerItem(item));
                }

                World.Player.SendMessage(MsgLevel.Force, LocString.ItemAdded);
            }
        }

        private void OnTargetType(bool location, Serial serial, Point3D loc, ushort gfx)
        {
            if (!serial.IsItem)
            {
                return;
            }

            m_Items.Add((ItemID) gfx);
            World.Player.SendMessage(MsgLevel.Force, LocString.ItemAdded);
        }

        private void OnTargetRemove(bool location, Serial serial, Point3D loc, ushort gfx)
        {
            if (!location && serial.IsItem)
            {
                for (int i = 0; i < m_Items.Count; i++)
                {
                    if (m_Items[i] is Serial && (Serial) m_Items[i] == serial)
                    {
                        m_Items.RemoveAt(i);
                        World.Player.SendMessage(MsgLevel.Force, LocString.ItemRemoved);

                        Item item = World.FindItem(serial);
                        if (item != null)
                        {
                            ClientProxy.SendToClient(new ContainerItem(item));
                        }

                        return;
                    }
                }

                World.Player.SendMessage(MsgLevel.Force, LocString.ItemNotFound);
            }
        }

        public override void Save(XmlWriter xml)
        {
            for (int i = 0; i < m_Items.Count; i++)
            {
                xml.WriteStartElement("item");
                if (m_Items[i] is Serial)
                {
                    xml.WriteAttributeString("serial", ((Serial) m_Items[i]).Value.ToString());
                }
                else
                {
                    xml.WriteAttributeString("id", ((ItemID) m_Items[i]).Value.ToString());
                }

                xml.WriteEndElement();
            }
        }

        public override void Load(XmlElement node)
        {
            foreach (XmlElement el in node.GetElementsByTagName("item"))
            {
                try
                {
                    string ser = el.GetAttribute("serial");
                    string iid = el.GetAttribute("id");

                    if (!string.IsNullOrEmpty(ser))
                    {
                        m_Items.Add((Serial) Convert.ToUInt32(ser));
                    }
                    else if (!string.IsNullOrEmpty(iid))
                    {
                        m_Items.Add((ItemID) Convert.ToUInt16(iid));
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
