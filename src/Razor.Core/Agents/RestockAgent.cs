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

// Portiert aus Razor CE (Razor/Agents/RestockAgent.cs) — Phase 2d.
// ENTFERNT gegenueber Razor CE (dokumentiert):
//  * WinForms-UI/Gumps (OnSelected/OnButtonPress/InputDialogGump/AgentsGump/
//    MessageBox), HotKey.Add (Phase 3), FeatureBit-Checks,
//  * ObjPropList-HotBag-Labels, Engine.MainWindow/ScriptManager-Fokus.
// Restock() (Razor CE: OnHotKey) und SetHB() sind public; der Restock-Ablauf
// (Ziel-Target -> ggf. Container oeffnen -> DoRestock via Timer) ist werktreu.
// Save/Load (hotbag/alias-Attribute, <item id=.. amount=.. />) byte-kompatibel.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;

namespace Assistant.Agents
{
    public class RestockAgent : Agent
    {
        public static List<RestockAgent> Agents { get; set; }

        public static void Initialize()
        {
            int maxAgents = Config.GetAppSetting<int>("MaxRestockAgents") == 0
                ? 20
                : Config.GetAppSetting<int>("MaxRestockAgents");

            Agents = new List<RestockAgent>();

            for (int i = 1; i <= maxAgents; i++)
            {
                RestockAgent restockAgent = new RestockAgent(i);

                Agent.Add(restockAgent);
                Agents.Add(restockAgent);
            }
        }

        public readonly List<RestockItem> Items;
        private Serial m_HotBag;

        public RestockAgent(int num)
        {
            Number = num;

            Items = new List<RestockItem>();

            HotKey.Add(HKCategory.Agents, HKSubCat.None,
                $"{Language.GetString(LocString.RestockAgent)}-{Number:D2}",
                new HotKeyCallback(OnHotKey));
            HotKey.Add(HKCategory.Agents, HKSubCat.None,
                $"{Language.GetString(LocString.SetRestockHB)}-{Number:D2}",
                new HotKeyCallback(SetHB));
            PacketHandler.RegisterClientToServerViewer(0x09, new PacketViewerCallback(OnSingleClick));
        }

        public Serial HotBag
        {
            get { return m_HotBag; }
        }

        private void OnSingleClick(PacketReader pvSrc, PacketHandlerEventArgs args)
        {
            Serial serial = pvSrc.ReadUInt32();
            if (m_HotBag == serial)
            {
                ushort gfx = 0;
                Item c = World.FindItem(m_HotBag);
                if (c != null)
                {
                    gfx = c.ItemID.Value;
                }

                ClientProxy.SendToClient(new UnicodeMessage(m_HotBag, gfx, Assistant.MessageType.Label, 0x3B2, 3,
                    Language.CliLocName, "", Language.Format(LocString.RestockHBA1, Number)));
            }
        }

        public override void Clear()
        {
            Items.Clear();
        }

        // XML-Elementname im Profil — MUSS dem CE-Language-String (1326) entsprechen.
        public override string Name
        {
            get { return $"Restock-{Number}"; }
        }

        public override string Alias { get; set; }

        public override int Number { get; }

        /// <summary>Razor CE: SetHB — HotBag per Target setzen/entfernen.</summary>
        public void SetHB()
        {
            World.Player.SendMessage(MsgLevel.Force, LocString.TargCont);
            Targeting.OneTimeTarget(new Targeting.TargetResponseCallback(OnHBTarget));
        }

        /// <summary>HotBag direkt setzen (UI/Tests, ohne Target-Cursor).</summary>
        public void SetHotBag(Serial serial)
        {
            m_HotBag = serial;
        }

        private void OnHBTarget(bool location, Serial serial, Point3D loc, ushort gfx)
        {
            if (!location && serial.IsItem)
            {
                m_HotBag = serial;
            }
            else
            {
                m_HotBag = Serial.Zero;
            }
        }

        /// <summary>Razor CE: OnHotKey — Quelle per Target waehlen, dann auffuellen.</summary>
        public void Restock()
        {
            World.Player.SendMessage(MsgLevel.Force, LocString.RestockTarget);
            Targeting.OneTimeTarget(new Targeting.TargetResponseCallback(OnRestockTarget));
        }

        /// <summary>Razor CE: OnHotKey (Alias fuer die Namensgleichheit mit CE).</summary>
        public void OnHotKey()
        {
            Restock();
        }

        Item m_Cont = null;

        private void OnRestockTarget(bool location, Serial serial, Point3D loc, ushort gfx)
        {
            if (serial == World.Player.Serial)
            {
                m_Cont = World.Player.GetItemOnLayer(Layer.Bank);
            }
            else if (serial.IsItem)
            {
                m_Cont = World.FindItem(serial);
                if (m_Cont != null)
                {
                    object root = m_Cont.RootContainer;
                    if (root is Mobile && root != World.Player)
                    {
                        m_Cont = null;
                    }
                }
            }

            if (m_Cont == null || m_Cont.IsCorpse)
            {
                World.Player.SendMessage(MsgLevel.Force, LocString.InvalidCont);
                return;
            }

            if (Utility.Distance(World.Player.Position, m_Cont.GetWorldPosition()) > 3)
            {
                World.Player.SendMessage(MsgLevel.Error, LocString.TooFar);
            }
            else
            {
                if (m_Cont.IsContainer && m_Cont.Layer != Layer.Bank)
                {
                    PlayerData.DoubleClick(m_Cont);

                    if (Config.GetBool("ObjectDelayEnabled"))
                    {
                        Timer.DelayedCallback(TimeSpan.FromMilliseconds(Config.GetInt("ObjectDelay") + 200),
                            new TimerCallback(DoRestock)).Start();
                    }
                    else
                    {
                        Timer.DelayedCallback(TimeSpan.FromMilliseconds(200), new TimerCallback(DoRestock)).Start();
                    }

                    World.Player.SendMessage(LocString.RestockQueued);
                }
                else
                {
                    DoRestock();
                }
            }
        }

        private void DoRestock()
        {
            Item bag = null;
            if (m_HotBag != Serial.Zero)
            {
                bag = World.FindItem(m_HotBag);
                if (bag != null && bag.RootContainer != World.Player)
                {
                    bag = null;
                }
            }

            if (bag == null)
            {
                bag = World.Player.Backpack;
                if (bag == null)
                {
                    World.Player.SendMessage(MsgLevel.Force, LocString.NoBackpack);
                    return;
                }
            }

            int num = 0;
            for (int i = 0; i < Items.Count; i++)
            {
                RestockItem ri = Items[i];
                int count = World.Player.Backpack.GetCount(ri.ItemID);

                num += Recurse(bag, m_Cont.Contains, ri, ref count);
            }

            World.Player.SendMessage(MsgLevel.Force, LocString.RestockDone, num, num != 1 ? "s" : "");
        }

        private int Recurse(Item pack, List<Item> items, RestockItem ri, ref int count)
        {
            int num = 0;
            for (int i = 0; count < ri.Amount && i < items.Count; i++)
            {
                Item item = (Item) items[i];

                if (item.ItemID == ri.ItemID)
                {
                    int amt = ri.Amount - count;
                    if (amt > item.Amount)
                    {
                        amt = item.Amount;
                    }

                    DragDropManager.DragDrop(item, amt, pack);
                    count += amt;
                    num++;
                }
                else if (item.Contains.Count > 0)
                {
                    num += Recurse(pack, item.Contains, ri, ref count);
                }
            }

            return num;
        }

        public void AddItemTarget()
        {
            Targeting.OneTimeTarget(OnItemTarget);
            World.Player.SendMessage(MsgLevel.Force, LocString.TargItemAdd);
        }

        private ushort m_PendingGfx;
        private bool m_HasPendingTarget;

        private void OnItemTarget(bool location, Serial serial, Point3D loc, ushort gfx)
        {
            if (location || serial.IsMobile)
            {
                return;
            }

            Item item = World.FindItem(serial);
            if (item != null)
            {
                gfx = item.ItemID;
            }

            if (gfx == 0 || gfx >= 0x4000)
            {
                return;
            }

            // Razor CE fragt hier per InputDialogGump nach der Menge — ohne UI
            // merken wir uns die Grafik; die UI (Phase 3) ruft danach
            // AddItem(new RestockItem(gfx, amount)) auf.
            m_PendingGfx = gfx;
            m_HasPendingTarget = true;
        }

        /// <summary>Grafik des letzten AddItemTarget-Treffers (fuer UI/Phase 3).</summary>
        public bool TryGetPendingTarget(out ushort gfx)
        {
            gfx = m_PendingGfx;
            bool has = m_HasPendingTarget;
            m_HasPendingTarget = false;
            return has;
        }

        public void AddItem(RestockItem item)
        {
            foreach (RestockItem restockItem in Items)
            {
                if (restockItem.ItemID.Value == item.ItemID.Value)
                {
                    World.Player?.SendMessage(MsgLevel.Force, LocString.ItemExists);
                    return;
                }
            }

            Items.Add(item);

            World.Player?.SendMessage(MsgLevel.Force, LocString.ItemAdded);
        }

        public void RemoveItem(int itemId)
        {
            RestockItem item = Items.FirstOrDefault(a => a.ItemID == itemId);

            if (item != null)
            {
                Items.Remove(item);

                World.Player?.SendMessage(MsgLevel.Force, LocString.ItemRemoved);
            }
        }

        public void SetItemAmount(int itemId, int amount)
        {
            int itemIndex = Items.FindIndex(a => a.ItemID == itemId);
            if (itemIndex < 0)
            {
                return;
            }

            Items[itemIndex].Amount = amount;
        }

        public override void Save(XmlWriter xml)
        {
            xml.WriteAttributeString("hotbag", m_HotBag.Value.ToString());
            xml.WriteAttributeString("alias", Alias);

            for (int i = 0; i < Items.Count; i++)
            {
                xml.WriteStartElement("item");
                RestockItem ri = (RestockItem) Items[i];
                xml.WriteAttributeString("id", ri.ItemID.Value.ToString());
                xml.WriteAttributeString("amount", ri.Amount.ToString());
                xml.WriteEndElement();
            }
        }

        public override void Load(XmlElement node)
        {
            try
            {
                m_HotBag = Convert.ToUInt32(node.GetAttribute("hotbag"));
            }
            catch
            {
                m_HotBag = Serial.Zero;
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
                    string iid = el.GetAttribute("id");
                    string amt = el.GetAttribute("amount");
                    Items.Add(new RestockItem((ItemID) Convert.ToInt32(iid), Convert.ToInt32(amt)));
                }
                catch
                {
                    // ignored
                }
            }
        }

        public class RestockItem
        {
            public ItemID ItemID;
            public int Amount;

            public RestockItem(ItemID id, int amount)
            {
                ItemID = id;
                Amount = amount;
            }

            public override string ToString()
            {
                return $"{ItemID}\t\t{Amount}";
            }
        }
    }
}
