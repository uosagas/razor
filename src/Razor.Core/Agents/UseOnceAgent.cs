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

// Portiert aus Razor CE (Razor/Agents/UseOnceAgent.cs) — Phase 2d.
// ENTFERNT gegenueber Razor CE (dokumentiert):
//  * WinForms-UI (OnSelected/OnButtonPress/ListBox/MessageBox), HotKey.Add
//    (HotKey-System kommt in Phase 3; Aktionen sind als public-Methoden da),
//  * ObjPropList-Labels ("Use Once" im Tooltip) + FeatureBit-Checks,
//  * Engine.MainWindow/AlwaysOnTop-Fokuswechsel.
// Save/Load (Profil-XML, <item serial=... />) sind byte-kompatibel.

using System;
using System.Collections;
using System.Xml;

namespace Assistant.Agents
{
    public class UseOnceAgent : Agent
    {
        public static UseOnceAgent Instance { get; private set; }

        public static void Initialize()
        {
            Agent.Add(Instance = new UseOnceAgent());
        }

        private readonly ArrayList m_Items;

        public UseOnceAgent()
        {
            m_Items = new ArrayList();
            HotKey.Add(HKCategory.Agents, LocString.UseOnceAgent, new HotKeyCallback(OnHotKey));
            HotKey.Add(HKCategory.Agents, LocString.AddUseOnce, new HotKeyCallback(OnAdd));
            HotKey.Add(HKCategory.Agents, LocString.AddUseOnceContainer, new HotKeyCallback(OnAddContainer));
            PacketHandler.RegisterClientToServerViewer(0x09, new PacketViewerCallback(OnSingleClick));

            Number = 0;

            Agent.OnItemCreated += new ItemCreatedEventHandler(CheckItemOPL);
        }

        public override void Clear()
        {
            m_Items.Clear();
        }

        /// <summary>Liste der Eintraege (Item oder Serial) — fuer UI/Tests.</summary>
        public ArrayList Items
        {
            get { return m_Items; }
        }

        private void CheckItemOPL(Item newItem)
        {
            for (int i = 0; i < m_Items.Count; i++)
            {
                if (m_Items[i] is Serial)
                {
                    if (newItem.Serial == (Serial) m_Items[i])
                    {
                        m_Items[i] = newItem;
                        // TODO Razor CE: newItem.ObjPropList.Add("Use Once") entfernt.
                        break;
                    }
                }
            }
        }

        private void OnSingleClick(PacketReader pvSrc, PacketHandlerEventArgs args)
        {
            Serial serial = pvSrc.ReadUInt32();
            for (int i = 0; i < m_Items.Count; i++)
            {
                Item item;
                if (m_Items[i] is Serial)
                {
                    item = World.FindItem((Serial) m_Items[i]);
                    if (item != null)
                    {
                        m_Items[i] = item;
                    }
                }

                item = m_Items[i] as Item;
                if (item == null)
                {
                    continue;
                }

                if (item.Serial == serial)
                {
                    ClientProxy.SendToClient(new UnicodeMessage(item.Serial, item.ItemID,
                        Assistant.MessageType.Label, 0x3B2, 3, Language.CliLocName, "",
                        Language.Format(LocString.UseOnceHBA1, i + 1)));
                    break;
                }
            }
        }

        // XML-Elementname im Profil — MUSS dem CE-Language-String (1373) entsprechen.
        public override string Name
        {
            get { return "UseOnce"; }
        }

        public override string Alias { get; set; }

        public override int Number { get; }

        /// <summary>Razor CE: Button "Add" — Item per Target hinzufuegen.</summary>
        public void OnAdd()
        {
            World.Player.SendMessage(MsgLevel.Force, LocString.TargItemAdd);
            Targeting.OneTimeTarget(new Targeting.TargetResponseCallback(OnTarget));
        }

        /// <summary>Razor CE: Button "Add Container" — kompletten Containerinhalt hinzufuegen.</summary>
        public void OnAddContainer()
        {
            World.Player.SendMessage(MsgLevel.Force, LocString.TargItemAdd);
            Targeting.OneTimeTarget(new Targeting.TargetResponseCallback(OnTargetBag));
        }

        /// <summary>Razor CE: Button "Remove" — Item per Target entfernen.</summary>
        public void OnRemove()
        {
            World.Player.SendMessage(MsgLevel.Force, LocString.TargItemRem);
            Targeting.OneTimeTarget(new Targeting.TargetResponseCallback(OnTargetRemove));
        }

        public void AddItem(Item item)
        {
            m_Items.Add(item);
            World.Player?.SendMessage(MsgLevel.Force, LocString.ItemAdded);
        }

        private void OnTarget(bool location, Serial serial, Point3D loc, ushort gfx)
        {
            if (!location && serial.IsItem)
            {
                Item item = World.FindItem(serial);
                if (item == null)
                {
                    World.Player.SendMessage(MsgLevel.Force, LocString.ItemNotFound);
                    return;
                }

                AddItem(item);
            }
        }

        private void OnTargetRemove(bool location, Serial serial, Point3D loc, ushort gfx)
        {
            if (!location && serial.IsItem)
            {
                for (int i = 0; i < m_Items.Count; i++)
                {
                    bool rem = false;
                    if (m_Items[i] is Item)
                    {
                        if (((Item) m_Items[i]).Serial == serial)
                        {
                            rem = true;
                        }
                    }
                    else if (m_Items[i] is Serial)
                    {
                        if (((Serial) m_Items[i]) == serial)
                        {
                            rem = true;
                        }
                    }

                    if (rem)
                    {
                        m_Items.RemoveAt(i);
                        World.Player.SendMessage(MsgLevel.Force, LocString.ItemRemoved);
                        return;
                    }
                }

                World.Player.SendMessage(MsgLevel.Force, LocString.ItemNotFound);
            }
        }

        private void OnTargetBag(bool location, Serial serial, Point3D loc, ushort gfx)
        {
            if (!location && serial.IsItem)
            {
                Item i = World.FindItem(serial);
                if (i != null && i.Contains.Count > 0)
                {
                    for (int ci = 0; ci < i.Contains.Count; ci++)
                    {
                        Item toAdd = i.Contains[ci] as Item;

                        if (toAdd != null)
                        {
                            m_Items.Add(toAdd);
                        }
                    }

                    World.Player.SendMessage(MsgLevel.Force, LocString.ItemsAdded, i.Contains.Count);
                }
            }
        }

        public override void Save(XmlWriter xml)
        {
            for (int i = 0; i < m_Items.Count; i++)
            {
                xml.WriteStartElement("item");
                if (m_Items[i] is Item)
                {
                    xml.WriteAttributeString("serial", ((Item) m_Items[i]).Serial.Value.ToString());
                }
                else
                {
                    xml.WriteAttributeString("serial", ((Serial) m_Items[i]).Value.ToString());
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
                    m_Items.Add((Serial) Convert.ToUInt32(ser));
                }
                catch
                {
                    // ignored
                }
            }
        }

        /// <summary>Razor CE: OnHotKey — benutzt (DoubleClick) das erste Item der Liste.</summary>
        public void OnHotKey()
        {
            if (World.Player == null)
            {
                return;
            }

            if (m_Items.Count <= 0)
            {
                World.Player.SendMessage(MsgLevel.Error, LocString.UseOnceEmpty);
            }
            else
            {
                Item item = null;
                if (m_Items[0] is Item)
                {
                    item = (Item) m_Items[0];
                }
                else if (m_Items[0] is Serial)
                {
                    item = World.FindItem((Serial) m_Items[0]);
                }

                try
                {
                    m_Items.RemoveAt(0);
                }
                catch
                {
                }

                if (item != null)
                {
                    World.Player.SendMessage(LocString.UseOnceStatus, item, m_Items.Count);
                    PlayerData.DoubleClick(item);
                }
                else
                {
                    World.Player.SendMessage(LocString.UseOnceError);
                    OnHotKey();
                }
            }
        }
    }
}
