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

// Portiert aus Razor CE (Razor/Agents/OrganizerAgent.cs) — Phase 2d.
// ENTFERNT gegenueber Razor CE (dokumentiert):
//  * WinForms-UI/Gumps (OnSelected/OnButtonPress/ListBox/AgentsGump/MessageBox),
//  * HotKey.Add (Phase 3) — Organize()/SetHotBag() sind public,
//  * ObjPropList-HotBag-Labels, Engine.MainWindow/ScriptManager-Fokus.
// Save/Load (hotbag/alias-Attribute, <item id=... />) sind byte-kompatibel.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;

namespace Assistant.Agents
{
    public class OrganizerAgent : Agent
    {
        public static List<OrganizerAgent> Agents { get; set; }

        public static void Initialize()
        {
            int maxAgents = Config.GetAppSetting<int>("MaxOrganizerAgents") == 0
                ? 20
                : Config.GetAppSetting<int>("MaxOrganizerAgents");

            Agents = new List<OrganizerAgent>();

            for (int i = 1; i <= maxAgents; i++)
            {
                OrganizerAgent organizerAgent = new OrganizerAgent(i);

                Agent.Add(organizerAgent);

                Agents.Add(organizerAgent);
            }
        }

        public readonly List<ItemID> Items;
        private uint m_Cont;

        public OrganizerAgent(int num)
        {
            Items = new List<ItemID>();
            Number = num;
            HotKey.Add(HKCategory.Agents, HKSubCat.None,
                $"{Language.GetString(LocString.OrganizerAgent)}-{Number:D2}",
                new HotKeyCallback(Organize));
            HotKey.Add(HKCategory.Agents, HKSubCat.None,
                $"{Language.GetString(LocString.SetOrganizerHB)}-{Number:D2}",
                new HotKeyCallback(SetHotBag));
            PacketHandler.RegisterClientToServerViewer(0x09, new PacketViewerCallback(OnSingleClick));
        }

        public uint HotBag
        {
            get { return m_Cont; }
        }

        private void OnSingleClick(PacketReader pvSrc, PacketHandlerEventArgs args)
        {
            uint serial = pvSrc.ReadUInt32();
            if (m_Cont == serial)
            {
                ushort gfx = 0;
                Item c = World.FindItem(m_Cont);
                if (c != null)
                {
                    gfx = c.ItemID.Value;
                }

                ClientProxy.SendToClient(new UnicodeMessage(m_Cont, gfx, Assistant.MessageType.Label, 0x3B2, 3,
                    Language.CliLocName, "", Language.Format(LocString.OrganizerHBA1, Number)));
            }
        }

        // XML-Elementname im Profil — MUSS dem CE-Language-String (1173) entsprechen.
        public override string Name
        {
            get { return $"Organizer-{Number}"; }
        }

        public override string Alias { get; set; }

        public override int Number { get; }

        /// <summary>Razor CE: SetHotBag — Zielcontainer per Target setzen.</summary>
        public void SetHotBag()
        {
            World.Player.SendMessage(MsgLevel.Force, LocString.TargCont);
            Targeting.OneTimeTarget(new Targeting.TargetResponseCallback(OnTargetBag));
        }

        /// <summary>Zielcontainer direkt setzen (UI/Tests, ohne Target-Cursor).</summary>
        public void SetHotBag(Serial serial)
        {
            m_Cont = serial;
        }

        public void Organize()
        {
            if (m_Cont == 0 || m_Cont > 0x7FFFFF00)
            {
                World.Player.SendMessage(MsgLevel.Force, LocString.ContNotSet);
                return;
            }

            Item pack = World.Player.Backpack;
            if (pack == null)
            {
                World.Player.SendMessage(MsgLevel.Warning, LocString.NoBackpack);
                return;
            }

            int count = OrganizeChildren(pack);

            if (count > 0)
            {
                World.Player.SendMessage(LocString.OrgQueued, count);
            }
            else
            {
                World.Player.SendMessage(LocString.OrgNoItems);
            }
        }

        /// <summary>Razor CE: Button "Stop" — laufende Organize-Queue anhalten.</summary>
        public void StopNow()
        {
            DragDropManager.GracefulStop();
        }

        private int OrganizeChildren(Item container)
        {
            object dest = World.FindItem(m_Cont);
            if (dest == null)
            {
                dest = World.FindMobile(m_Cont);
                if (dest == null)
                {
                    return 0;
                }
            }

            return OrganizeChildren(container, dest);
        }

        private int OrganizeChildren(Item container, object dest)
        {
            int count = 0;
            for (int i = 0; i < container.Contains.Count; i++)
            {
                Item item = (Item) container.Contains[i];
                if (item.Serial != m_Cont && !item.IsChildOf(dest))
                {
                    count += OrganizeChildren(item, dest);
                    if (Items.Contains(item.ItemID.Value))
                    {
                        if (dest is Item)
                        {
                            DragDropManager.DragDrop(item, (Item) dest);
                        }
                        else if (dest is Mobile)
                        {
                            DragDropManager.DragDrop(item, ((Mobile) dest).Serial);
                        }

                        count++;
                    }
                }
            }

            return count;
        }

        private void OnTarget(bool location, Serial serial, Point3D loc, ushort gfx)
        {
            if (!location && serial.IsItem && World.Player != null)
            {
                AddItem(gfx);
            }
        }

        public void AddItemTarget()
        {
            World.Player.SendMessage(MsgLevel.Force, LocString.TargItemAdd);
            Targeting.OneTimeTarget(OnTarget);
        }

        public void AddItem(ushort gfx)
        {
            if (Items != null && Items.Contains(gfx))
            {
                World.Player?.SendMessage(MsgLevel.Force, LocString.ItemExists);
            }
            else
            {
                Items?.Add(gfx);

                World.Player?.SendMessage(MsgLevel.Force, LocString.ItemAdded);
            }
        }

        public void RemoveItem(int itemId)
        {
            ItemID item = Items.FirstOrDefault(a => a == itemId);

            if (item != null)
            {
                Items.Remove(item);

                World.Player?.SendMessage(MsgLevel.Force, LocString.ItemRemoved);
            }
        }

        private void OnTargetBag(bool location, Serial serial, Point3D loc, ushort gfx)
        {
            if (!location && serial > 0 && serial <= 0x7FFFFF00)
            {
                m_Cont = serial;

                if (World.Player != null)
                {
                    World.Player.SendMessage(MsgLevel.Force, LocString.ContSet);
                }
            }
        }

        public override void Clear()
        {
            Items.Clear();
            m_Cont = 0;
        }

        public override void Save(XmlWriter xml)
        {
            xml.WriteAttributeString("hotbag", m_Cont.ToString());
            xml.WriteAttributeString("alias", Alias);

            for (int i = 0; i < Items.Count; i++)
            {
                xml.WriteStartElement("item");
                xml.WriteAttributeString("id", Items[i].Value.ToString());
                xml.WriteEndElement();
            }
        }

        public override void Load(XmlElement node)
        {
            try
            {
                m_Cont = Convert.ToUInt32(node.GetAttribute("hotbag"));
            }
            catch
            {
                // ignored
            }

            try
            {
                Alias = node.GetAttribute("alias");
            }
            catch
            {
                Alias = string.Empty;
            }

            foreach (XmlElement el in node.GetElementsByTagName("item"))
            {
                try
                {
                    string gfx = el.GetAttribute("id");
                    Items.Add(Convert.ToUInt16(gfx));
                }
                catch
                {
                    // ignored
                }
            }
        }
    }
}
