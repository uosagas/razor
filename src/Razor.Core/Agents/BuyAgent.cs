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

// Portiert aus Razor CE (Razor/Agents/BuyAgent.cs) — Phase 2d.
// Paket-Hooks werktreu: 0x74 (ExtBuyInfo -> Preise/Beschreibungen),
// 0x24 (DisplayBuy gump 0x30 -> automatische Kauf-Antwort 0x3B + Block),
// 0x3B (EndVendorBuy -> Weltmodell-Bestand der Vendor-Liste korrigieren).
// ENTFERNT gegenueber Razor CE (dokumentiert):
//  * WinForms-UI/Gumps (OnSelected/OnButtonPress/InputDialogGump/AgentsGump/
//    MessageBox), FeatureBit-Checks (kein AllowBit im Port),
//  * Enabled ist eine oeffentliche Property statt Button-Toggle.
// Save/Load (enabled/alias-Attribute, <item id=.. amount=.. />) byte-kompatibel.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Xml;

namespace Assistant.Agents
{
    public class BuyAgent : Agent
    {
        public class BuyEntry
        {
            public BuyEntry(ItemID id, ushort amount)
            {
                Id = id;
                Amount = amount;
            }

            public readonly ItemID Id;
            public ushort Amount;

            public override string ToString()
            {
                return $"{Id}\t{Amount}";
            }
        }

        private class ItemXYComparer : IComparer<Item>
        {
            public static readonly ItemXYComparer Instance = new ItemXYComparer();

            private ItemXYComparer()
            {
            }

            public int Compare(Item x, Item y)
            {
                if (!(x is Item))
                {
                    return 1;
                }
                else if (!(y is Item))
                {
                    return -1;
                }

                int xsum = x.Position.X + x.Position.Y * 200;
                int ysum = y.Position.X + y.Position.Y * 200;

                return xsum.CompareTo(ysum);
            }
        }

        private static readonly ArrayList m_Instances = new ArrayList();

        public static List<BuyAgent> Agents { get; set; }

        public static void Initialize()
        {
            PacketHandler.RegisterServerToClientViewer(0x74, new PacketViewerCallback(ExtBuyInfo));
            PacketHandler.RegisterServerToClientViewer(0x24, new PacketViewerCallback(DisplayBuy));
            PacketHandler.RegisterServerToClientViewer(0x3B, new PacketViewerCallback(EndVendorBuy));

            int maxAgents = Config.GetAppSetting<int>("MaxBuyAgents") == 0
                ? 20
                : Config.GetAppSetting<int>("MaxBuyAgents");

            Agents = new List<BuyAgent>();

            for (int i = 1; i <= maxAgents; i++)
            {
                BuyAgent b = new BuyAgent(i);
                m_Instances.Add(b);
                Agent.Add(b);

                Agents.Add(b);
            }
        }

        public readonly List<BuyEntry> Items;
        private bool m_Enabled;

        public BuyAgent(int num)
        {
            Number = num;
            Items = new List<BuyEntry>();
        }

        public bool Enabled
        {
            get { return m_Enabled; }
            set { m_Enabled = value; }
        }

        private static void DisplayBuy(PacketReader p, PacketHandlerEventArgs args)
        {
            Serial serial = p.ReadUInt32();
            ushort gump = p.ReadUInt16();

            if (gump != 0x30 || !serial.IsMobile || World.Player == null)
            {
                return;
            }

            Mobile vendor = World.FindMobile(serial);
            if (vendor == null)
            {
                return;
            }

            Item pack = vendor.GetItemOnLayer(Layer.ShopBuy);
            if (pack == null || pack.Contains == null || pack.Contains.Count <= 0)
            {
                return;
            }

            pack.Contains.Sort(ItemXYComparer.Instance);

            int total = 0;
            int cost = 0;
            List<VendorBuyItem> buyList = new List<VendorBuyItem>();
            Dictionary<ushort, int> found = new Dictionary<ushort, int>();
            bool lowGoldWarn = false;
            for (int i = 0; i < pack.Contains.Count; i++)
            {
                Item item = (Item) pack.Contains[i];
                if (item == null)
                {
                    continue;
                }

                foreach (BuyAgent ba in m_Instances)
                {
                    if (ba == null || ba.Items == null || !ba.m_Enabled)
                    {
                        continue;
                    }

                    for (int a = 0; a < ba.Items.Count; a++)
                    {
                        BuyEntry b = (BuyEntry) ba.Items[a];
                        if (b == null)
                        {
                            continue;
                        }

                        bool dupe = false;
                        foreach (VendorBuyItem vbi in buyList)
                        {
                            if (vbi.Serial == item.Serial)
                            {
                                dupe = true;
                            }
                        }

                        if (dupe)
                        {
                            continue;
                        }

                        // fucking osi and their blank scrolls
                        if (b.Id == item.ItemID.Value || (b.Id == 0x0E34 && item.ItemID.Value == 0x0EF3) ||
                            (b.Id == 0x0EF3 && item.ItemID.Value == 0x0E34))
                        {
                            int count = World.Player.Backpack.GetCount(b.Id);
                            if (found.ContainsKey(b.Id))
                            {
                                count += (int) found[b.Id];
                            }

                            if (count < b.Amount && b.Amount > 0)
                            {
                                count = b.Amount - count;
                                if (count >= item.Amount)
                                {
                                    count = item.Amount;
                                }
                                else if (count <= 0)
                                {
                                    continue;
                                }

                                if (!found.ContainsKey(b.Id))
                                {
                                    found.Add(b.Id, (int) count);
                                }
                                else
                                {
                                    found[b.Id] = (int) found[b.Id] + (int) count;
                                }

                                buyList.Add(new VendorBuyItem(item.Serial, count, item.Price));
                                total += count;
                                cost += item.Price * count;
                            }
                        }
                    }
                }
            }

            if (!Config.GetBool("BuyAgentsIgnoreGold"))
            {
                if (cost > World.Player.Gold && cost < 2000 && buyList.Count > 0)
                {
                    lowGoldWarn = true;
                    do
                    {
                        VendorBuyItem vbi = (VendorBuyItem) buyList[0];
                        if (cost - vbi.TotalCost <= World.Player.Gold)
                        {
                            while (cost > World.Player.Gold && vbi.Amount > 0)
                            {
                                cost -= vbi.Price;
                                --vbi.Amount;
                                --total;
                            }

                            if (vbi.Amount <= 0)
                            {
                                buyList.RemoveAt(0);
                            }
                        }
                        else
                        {
                            cost -= vbi.TotalCost;
                            total -= vbi.Amount;
                            buyList.RemoveAt(0);
                        }
                    } while (cost > World.Player.Gold && buyList.Count > 0);
                }
            }

            if (buyList.Count > 0)
            {
                args.Block = true;
                BuyLists[serial] = buyList;
                ClientProxy.SendToServer(new VendorBuyResponse(serial, buyList));

                if (Config.GetBool("BuyAgentsIgnoreGold"))
                {
                    World.Player.SendMessage(MsgLevel.Force, LocString.BuyAgentAttempt, total, cost);
                }
                else
                {
                    World.Player.SendMessage(MsgLevel.Force, LocString.BuyTotals, total, cost);
                }
            }

            if (lowGoldWarn)
            {
                World.Player.SendMessage(MsgLevel.Force, LocString.BuyLowGold);
            }
        }

        private static readonly Dictionary<uint, List<VendorBuyItem>> BuyLists =
            new Dictionary<uint, List<VendorBuyItem>>();

        private static void ExtBuyInfo(PacketReader p, PacketHandlerEventArgs args)
        {
            Serial ser = p.ReadUInt32();
            Item pack = World.FindItem(ser);
            if (pack == null)
            {
                return;
            }

            byte count = p.ReadByte();
            if (count < pack.Contains.Count)
            {
                World.Player.SendMessage(MsgLevel.Debug,
                    "Buy Agent Warning: Contains Count {0} does not match ExtInfo {1}.", pack.Contains.Count, count);
            }

            pack.Contains.Sort(ItemXYComparer.Instance);

            for (int i = count - 1; i >= 0; i--)
            {
                if (i < pack.Contains.Count)
                {
                    Item item = (Item) pack.Contains[i];
                    item.Price = p.ReadInt32();
                    byte len = p.ReadByte();
                    item.BuyDesc = p.ReadStringSafe(len);
                }
                else
                {
                    p.ReadInt32();
                    p.Position += p.ReadByte() + 1;
                }
            }
        }

        private static void EndVendorBuy(PacketReader p, PacketHandlerEventArgs args)
        {
            if (World.Player == null)
                return;
            uint serial = p.ReadUInt32();
            if (BuyLists.TryGetValue(serial, out var list))
            {
                BuyLists.Remove(serial);
                Mobile vendor = World.FindMobile(serial);
                if (vendor == null)
                    return;

                Item pack = vendor.GetItemOnLayer(Layer.ShopBuy);
                if (pack == null || pack.Contains == null || pack.Contains.Count <= 0)
                    return;

                for (int i = list.Count - 1; i >= 0; --i)
                {
                    VendorBuyItem vbi = list[i];
                    Item item = World.FindItem(vbi.Serial);
                    if (item == null || !pack.Contains.Contains(item))
                        continue;
                    item.Amount -= (ushort) vbi.Amount;
                    if (item.Amount <= 0)
                        item.Remove();
                }
            }
        }

        public override void Clear()
        {
            Items.Clear();
        }

        public static void OnDisconnected()
        {
            BuyLists.Clear();
        }

        // XML-Elementname im Profil — MUSS dem CE-Language-String (1302) entsprechen.
        public override string Name => $"Buy-{Number}";

        public override string Alias { get; set; }

        public override int Number { get; }

        public void AddItemTarget()
        {
            World.Player.SendMessage(MsgLevel.Force, LocString.TargItemAdd);
            Targeting.OneTimeTarget(OnTarget);
        }

        private ushort m_PendingGfx;
        private bool m_HasPendingTarget;

        private void OnTarget(bool location, Serial serial, Point3D loc, ushort gfx)
        {
            if (!location && !serial.IsMobile)
            {
                // Razor CE fragt hier per InputDialogGump nach der Menge —
                // ohne UI merken wir uns die Grafik; die UI (Phase 3) ruft
                // danach AddItem(new BuyEntry(gfx, amount)) auf.
                m_PendingGfx = gfx;
                m_HasPendingTarget = true;
            }
        }

        /// <summary>Grafik des letzten AddItemTarget-Treffers (fuer UI/Phase 3).</summary>
        public bool TryGetPendingTarget(out ushort gfx)
        {
            gfx = m_PendingGfx;
            bool has = m_HasPendingTarget;
            m_HasPendingTarget = false;
            return has;
        }

        public void AddItem(BuyEntry entry)
        {
            Items?.Add(entry);

            World.Player?.SendMessage(MsgLevel.Force, LocString.ItemAdded);
        }

        public void RemoveItem(int itemId)
        {
            BuyEntry item = Items.FirstOrDefault(a => a.Id == itemId);

            if (item != null)
            {
                Items.Remove(item);

                World.Player?.SendMessage(MsgLevel.Force, LocString.ItemRemoved);
            }
        }

        public void SetItemAmount(int itemId, ushort amount)
        {
            int itemIndex = Items.FindIndex(a => a.Id == itemId);
            if (itemIndex < 0)
            {
                return;
            }

            Items[itemIndex].Amount = amount;
        }

        public override void Save(XmlWriter xml)
        {
            if (Items == null)
            {
                return;
            }

            xml.WriteAttributeString("enabled", m_Enabled.ToString());
            xml.WriteAttributeString("alias", Alias);

            foreach (BuyEntry b in Items)
            {
                xml.WriteStartElement("item");
                xml.WriteAttributeString("id", b.Id.Value.ToString());
                xml.WriteAttributeString("amount", b.Amount.ToString());
                xml.WriteEndElement();
            }
        }

        public override void Load(XmlElement node)
        {
            try
            {
                m_Enabled = bool.Parse(node.GetAttribute("enabled"));
            }
            catch
            {
                m_Enabled = false;
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
                    ushort id = Convert.ToUInt16(el.GetAttribute("id"));
                    ushort amount = Convert.ToUInt16(el.GetAttribute("amount"));

                    Items.Add(new BuyEntry(id, amount));
                }
                catch
                {
                    // ignored
                }
            }
        }
    }
}
