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

// Portiert aus Razor CE (Razor/Core/Mobile.cs). Das Direction-Enum lebt bereits
// in Core/Enums.cs (Phase 2a) und wird hier nicht erneut definiert.
// STUBS/ENTFERNT gegenueber Razor CE (nur Weltzustand, keine Seiteneffekte):
//  * Agent.InvokeMobileCreated, MapWindow-Updates in OnPositionChanging
//  * OverheadMessage*/SetLayerHue/ResetLayerHue/SetMobileHue (Client-Injection)
//  * InParty (PacketHandlers.Party ist nicht portiert, TODO Party-Handling):
//    Remove() entfernt daher immer aus World.Mobiles.

using System;
using System.Collections.Generic;
using System.Text;

namespace Assistant
{
    public partial class Mobile : UOEntity
    {
        private ushort m_Body;
        private Direction m_Direction;
        private string m_Name;

        private byte m_Notoriety;

        private bool m_Visible;
        private bool m_Female;
        private bool m_Poisoned;
        private bool m_Blessed;
        private bool m_Paralyze;
        private bool m_Warmode;

        //new
        private bool m_Unknown;
        private bool m_Unknown2;
        private bool m_Unknown3;

        private bool m_CanRename;
        //end new

        private ushort m_HitsMax, m_Hits;
        protected ushort m_StamMax, m_Stam, m_ManaMax, m_Mana;

        private List<Item> m_Items = new List<Item>();

        private byte m_Map;

        public Mobile(Serial serial) : base(serial)
        {
            m_Map = World.Player == null ? (byte) 0 : World.Player.Map;
            m_Visible = true;

            Agents.Agent.InvokeMobileCreated(this);
        }

        public string Name
        {
            get
            {
                if (m_Name == null)
                    return "";
                else
                    return m_Name;
            }
            set
            {
                if (!string.IsNullOrEmpty(value) && value != m_Name)
                {
                    string trim = ClilocConversion(value);
                    if (trim.Length > 0)
                    {
                        m_Name = trim;
                    }
                }
            }
        }

        private static StringBuilder _InternalSB = new StringBuilder(32);

        private static string ClilocConversion(string old)
        {
            _InternalSB.Clear();
            string[] arr = old.Split(' ');
            for (int i = 0; i < arr.Length; i++)
            {
                string ss = arr[i];
                if (ss.Length > 1 && ss.StartsWith("#"))
                {
                    if (int.TryParse(ss.Substring(1), out int x))
                    {
                        ss = Language.GetCliloc(x);
                        if (string.IsNullOrEmpty(ss))
                        {
                            ss = arr[i];
                        }
                    }
                }

                _InternalSB.Append(ss);
                _InternalSB.Append(' ');
            }

            return _InternalSB.ToString().Trim();
        }

        public ushort Body
        {
            get { return m_Body; }
            set { m_Body = value; }
        }

        public Direction Direction
        {
            get { return m_Direction; }
            set
            {
                if (value != m_Direction)
                {
                    var oldDir = m_Direction;
                    m_Direction = value;
                    OnDirectionChanging(oldDir);
                }
            }
        }

        public bool Visible
        {
            get { return m_Visible; }
            set { m_Visible = value; }
        }

        public bool Poisoned
        {
            get { return m_Poisoned; }
            set { m_Poisoned = value; }
        }

        public bool Blessed
        {
            get { return m_Blessed; }
            set { m_Blessed = value; }
        }

        public bool Paralyzed
        {
            get { return m_Paralyze; }
            set { m_Paralyze = value; }
        }

        public bool IsGhost
        {
            get
            {
                return m_Body == 402
                       || m_Body == 403
                       || m_Body == 607
                       || m_Body == 608
                       || m_Body == 970;
            }
        }

        public bool IsHuman
        {
            get
            {
                return m_Body >= 0
                       && (m_Body == 400
                           || m_Body == 401
                           || m_Body == 402
                           || m_Body == 403
                           || m_Body == 605
                           || m_Body == 606
                           || m_Body == 607
                           || m_Body == 608
                           || m_Body == 970); //player ghost
            }
        }

        public bool IsMonster
        {
            get { return !IsHuman; }
        }

        //new
        public bool Unknown
        {
            get { return m_Unknown; }
            set { m_Unknown = value; }
        }

        public bool Unknown2
        {
            get { return m_Unknown2; }
            set { m_Unknown2 = value; }
        }

        public bool Unknown3
        {
            get { return m_Unknown3; }
            set { m_Unknown3 = value; }
        }

        public bool CanRename //A pet! (where the health bar is open, we can add this to an arraylist of mobiles...
        {
            get { return m_CanRename; }
            set { m_CanRename = value; }
        }
        //end new

        public bool Warmode
        {
            get { return m_Warmode; }
            set { m_Warmode = value; }
        }

        public bool Female
        {
            get { return m_Female; }
            set { m_Female = value; }
        }

        public byte Notoriety
        {
            get { return m_Notoriety; }
            set
            {
                if (value != Notoriety)
                {
                    OnNotoChange(m_Notoriety, value);
                    m_Notoriety = value;
                }
            }
        }

        protected virtual void OnNotoChange(byte old, byte cur)
        {
        }

        // grey, blue, green, 'canbeattacked'
        private static uint[] m_NotoHues = new uint[8]
        {
            // hue color #30
            0x000000, // black		unused 0
            0x30d0e0, // blue		0x0059 1
            0x60e000, // green		0x003F 2
            0x9090b2, // greyish	0x03b2 3
            0x909090, // grey		   "   4
            0xd88038, // orange		0x0090 5
            0xb01000, // red		0x0022 6
            0xe0e000 // yellow		0x0035 7
        };

        private static int[] m_NotoHuesInt = new int[8]
        {
            1, // black		unused 0
            0x059, // blue		0x0059 1
            0x03F, // green		0x003F 2
            0x3B2, // greyish	0x03b2 3
            0x3B2, // grey		   "   4
            0x090, // orange		0x0090 5
            0x022, // red		0x0022 6
            0x035, // yellow		0x0035 7
        };

        public uint GetNotorietyColor()
        {
            if (m_Notoriety < 0 || m_Notoriety >= m_NotoHues.Length)
                return m_NotoHues[0];
            else
                return m_NotoHues[m_Notoriety];
        }

        public int GetNotorietyColorInt()
        {
            if (m_Notoriety < 0 || m_Notoriety >= m_NotoHuesInt.Length)
                return m_NotoHuesInt[0];
            else
                return m_NotoHuesInt[m_Notoriety];
        }

        public byte GetStatusCode()
        {
            if (m_Poisoned)
                return 1;
            else
                return 0;
        }

        public ushort HitsMax
        {
            get { return m_HitsMax; }
            set { m_HitsMax = value; }
        }

        public ushort Hits
        {
            get { return m_Hits; }
            set { m_Hits = value; }
        }

        public ushort Stam
        {
            get { return m_Stam; }
            set { m_Stam = value; }
        }

        public ushort StamMax
        {
            get { return m_StamMax; }
            set { m_StamMax = value; }
        }

        public ushort Mana
        {
            get { return m_Mana; }
            set { m_Mana = value; }
        }

        public ushort ManaMax
        {
            get { return m_ManaMax; }
            set { m_ManaMax = value; }
        }

        public byte Map
        {
            get { return m_Map; }
            set
            {
                if (m_Map != value)
                {
                    OnMapChange(m_Map, value);
                    m_Map = value;
                }
            }
        }

        public virtual void OnMapChange(byte old, byte cur)
        {
        }

        public void AddItem(Item item)
        {
            m_Items.Add(item);
        }

        public void RemoveItem(Item item)
        {
            m_Items.Remove(item);
        }

        public override void Remove()
        {
            List<Item> rem = new List<Item>(m_Items);
            m_Items.Clear();

            for (int i = 0; i < rem.Count; i++)
                rem[i].Remove();

            // TODO Razor CE: Party-Mitglieder werden nur unsichtbar statt entfernt
            // (PacketHandlers.Party) — Party-Handling ist noch nicht portiert.
            base.Remove();
            World.RemoveMobile(this);
        }

        public Item GetItemOnLayer(Layer layer)
        {
            for (int i = 0; i < m_Items.Count; i++)
            {
                Item item = (Item) m_Items[i];
                if (item.Layer == layer)
                    return item;
            }

            return null;
        }

        /// <summary>
        /// Razor CE: Mobile.OverheadMessage — injiziert eine nur lokal sichtbare
        /// Nachricht ueber dem Kopf dieses Mobiles. Von den Script-Kommandos
        /// overhead/headmsg und allen Overhead-Features genutzt. Der Stil folgt
        /// wie CE der Option OverheadStyle (0 = ASCII, sonst Unicode).
        /// </summary>
        public void OverheadMessage(int hue, string text)
        {
            if (Config.GetInt("OverheadStyle") == 0)
                ClientProxy.SendToClient(new AsciiMessage(Serial, Body, MessageType.Regular, hue, 3,
                    Name ?? string.Empty, text));
            else
                ClientProxy.SendToClient(new UnicodeMessage(Serial, Body, MessageType.Regular, hue, 3,
                    Language.CliLocName, Name ?? string.Empty, text));
        }

        public Item Backpack
        {
            get { return GetItemOnLayer(Layer.Backpack); }
        }

        public Item Quiver
        {
            get
            {
                Item item = GetItemOnLayer(Layer.Cloak);

                if (item != null && item.IsContainer)
                    return item;
                else
                    return null;
            }
        }

        public Item FindItemByID(ItemID id)
        {
            for (int i = 0; i < Contains.Count; i++)
            {
                Item item = (Item) Contains[i];
                if (item.ItemID == id)
                    return item;
            }

            return null;
        }

        public virtual void OnDirectionChanging(Direction oldDir)
        {
        }

        public int GetPacketFlags()
        {
            int flags = 0x0;

            if (m_Paralyze)
                flags |= 0x01;

            if (m_Female)
                flags |= 0x02;

            if (m_Poisoned)
                flags |= 0x04;

            if (m_Blessed)
                flags |= 0x08;

            if (m_Warmode)
                flags |= 0x40;

            if (!m_Visible)
                flags |= 0x80;

            if (m_Unknown)
                flags |= 0x01;

            if (m_Unknown2)
                flags |= 0x10;

            if (m_Unknown3)
                flags |= 0x20;

            return flags;
        }

        public void ProcessPacketFlags(byte flags)
        {
            if (!PacketHandlers.UseNewStatus)
                m_Poisoned = (flags & 0x04) != 0;

            m_Paralyze = (flags & 0x01) != 0;
            m_Female = (flags & 0x02) != 0;
            m_Blessed = (flags & 0x08) != 0;
            m_Unknown2 = (flags & 0x10) != 0; //new
            m_Unknown3 = (flags & 0x10) != 0; //new
            m_Warmode = (flags & 0x40) != 0;
            m_Visible = (flags & 0x80) == 0;
        }

        public List<Item> Contains
        {
            get { return m_Items; }
        }

        public override string ToString()
        {
            return $"{this.Name} ({this.Serial})";
        }
    }
}
