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

// Portiert aus Razor CE (Razor/Core/World.cs).
// Abweichungen:
//  * Member sind public statt internal (Testbarkeit/Plugin-Zugriff).
//  * FindItemsByName entfaellt (braucht Tiledata, TODO).
//  * RequestMobileStatus ist ein Stub (TODO: StatusQuery via IClientServices.SendToServer).

using System;
using System.Collections.Generic;

namespace Assistant
{
    public partial class World
    {
        private static Dictionary<Serial, Item> m_Items;
        private static Dictionary<Serial, Mobile> m_Mobiles;
        private static PlayerData m_Player;
        private static string m_ShardName, m_PlayerName, m_AccountName;
        private static Dictionary<ushort, string> m_Servers;

        static World()
        {
            m_Servers = new Dictionary<ushort, string>();
            m_Items = new Dictionary<Serial, Item>();
            m_Mobiles = new Dictionary<Serial, Mobile>();
            m_ShardName = "[None]";
        }

        public static Dictionary<ushort, string> Servers
        {
            get { return m_Servers; }
        }

        public static Dictionary<Serial, Item> Items
        {
            get { return m_Items; }
        }

        public static Dictionary<Serial, Mobile> Mobiles
        {
            get { return m_Mobiles; }
        }

        public static Item FindItem(Serial serial)
        {
            m_Items.TryGetValue(serial, out Item it);
            return it;
        }

        public static Item FindItemByType(int itemId)
        {
            foreach (KeyValuePair<Serial, Item> item in m_Items)
            {
                if (item.Value.ItemID.Value == itemId)
                    return item.Value;
            }

            return null;
        }

        public static List<Item> FindItemsById(ushort id)
        {
            List<Item> items = new List<Item>();

            foreach (KeyValuePair<Serial, Item> item in m_Items)
            {
                if (item.Value.ItemID == id)
                    items.Add(item.Value);
            }

            return items;
        }

        /// <summary>
        /// Razor CE: World.FindItemsByName — Tiledata-Name exakt (case-
        /// insensitive). Namen kommen im Port ueber den DataService (ItemData).
        /// </summary>
        public static List<Item> FindItemsByName(string name)
        {
            List<Item> items = new List<Item>();

            foreach (KeyValuePair<Serial, Item> item in m_Items)
            {
                // GetName liefert null, wenn Tiledata (noch) fehlt -> null-sicher.
                if (string.Equals(ItemData.GetName(item.Value.ItemID.Value), name, StringComparison.OrdinalIgnoreCase))
                    items.Add(item.Value);
            }

            return items;
        }

        public static Mobile FindMobile(Serial serial)
        {
            m_Mobiles.TryGetValue(serial, out Mobile m);
            return m;
        }

        public static List<Mobile> FindMobilesByName(string name)
        {
            List<Mobile> mobiles = new List<Mobile>();
            foreach (KeyValuePair<Serial, Mobile> item in m_Mobiles)
            {
                if (item.Value.Name.IndexOf(name, StringComparison.OrdinalIgnoreCase) != -1)
                    mobiles.Add(item.Value);
            }

            return mobiles;
        }

        public static List<Mobile> MobilesInRange(int range)
        {
            List<Mobile> list = new List<Mobile>();

            if (World.Player == null)
                return list;

            foreach (Mobile m in World.Mobiles.Values)
            {
                if (Utility.InRange(World.Player.Position, m.Position, World.Player.VisRange))
                    list.Add(m);
            }

            return list;
        }

        public static List<Mobile> MobilesInRange()
        {
            if (Player == null)
                return MobilesInRange(18);
            else
                return MobilesInRange(Player.VisRange);
        }

        public static void AddItem(Item item)
        {
            m_Items[item.Serial] = item;
        }

        public static void AddMobile(Mobile mob)
        {
            m_Mobiles[mob.Serial] = mob;
        }

        public static void RequestMobileStatus(Mobile m)
        {
            // TODO Razor CE: Client.Instance.SendToServer(new StatusQuery(m));
            // Phase 3: ueber IClientServices.SendToServer anbinden.
        }

        public static void RemoveMobile(Mobile mob)
        {
            m_Mobiles.Remove(mob.Serial);
        }

        public static void RemoveItem(Item item)
        {
            m_Items.Remove(item.Serial);
        }

        /// <summary>
        /// Setzt das komplette Weltmodell zurueck (wie Razor CE beim Disconnect:
        /// Player = null, Items/Mobiles leeren).
        /// </summary>
        public static void Clear()
        {
            m_Player = null;
            m_Items.Clear();
            m_Mobiles.Clear();
            Item.ClearAutoStackCache(); // neue Session, neue Serials
        }

        public static PlayerData Player
        {
            get { return m_Player; }
            set { m_Player = value; }
        }

        public static string ShardName
        {
            get { return m_ShardName; }
            set { m_ShardName = value; }
        }

        public static string OrigPlayerName
        {
            get { return m_PlayerName; }
            set { m_PlayerName = value; }
        }

        public static string AccountName
        {
            get { return m_AccountName; }
            set { m_AccountName = value; }
        }
    }
}
