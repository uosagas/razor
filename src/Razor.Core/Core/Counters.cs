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

// Portiert aus Razor CE (Razor/Core/Counters.cs) — Phase 2d.
// Die Zaehl-Maschinerie (ItemCountCache/CounterChangeSet/ItemChangeTracker/
// ItemTypeCounter) ist werkgetreu. ENTFERNT gegenueber Razor CE (dokumentiert):
//  * ListViewItem/CounterLVIComparer/Redraw (WinForms-UI),
//  * UOAssist.PostCounterUpdate, Client.RequestTitlebarUpdate, GetTitlebarString,
//  * MessageBox bei counters.xml-Ladefehler (still ignoriert),
//  * Load() laeuft nicht mehr im statischen Konstruktor, sondern in
//    Counter.Initialize() (Config-Pfade muessen initialisiert sein).
// Profil-Sektion "counters" (SaveProfile/LoadProfile) ist byte-kompatibel zu CE.

using System;
using System.IO;
using System.Collections.Generic;
using System.Xml;

namespace Assistant
{
    public class ItemCountCache
    {
        private Dictionary<Item, ulong> _cache = new Dictionary<Item, ulong>();

        public long Add(Item item)
        {
            if (_cache.TryGetValue(item, out var old))
            {
                var diff = item.Amount - old;
                _cache[item] = item.Amount;
                return (long) diff;
            }
            else
            {
                _cache.Add(item, item.Amount);
                return item.Amount;
            }
        }

        public long Remove(Item item)
        {
            if (_cache.TryGetValue(item, out var old))
            {
                var diff = Math.Min(item.Amount, old);
                _cache.Remove(item);
                return -(long) diff;
            }

            // Item was untracked in the first place, hence no change
            return 0;
        }

        public void Reset()
        {
            _cache.Clear();
        }
    }

    public struct CounterID
    {
        public CounterID(ItemID itemID, ushort hue = 0xFFFF)
        {
            ItemID = itemID;
            Hue = hue;
        }

        public ItemID ItemID;
        public ushort Hue;
    }

    public class CounterChangeSet
    {
        public Dictionary<CounterID, long> Changes = new Dictionary<CounterID, long>();

        private void AddValue(CounterID id, long diff)
        {
            if (Changes.ContainsKey(id))
            {
                Changes[id] += diff;
            }
            else
            {
                Changes.Add(id, diff);
            }

            if (Changes[id] == 0)
            {
                Changes.Remove(id);
            }
        }

        public void Add(CounterChangeSet other)
        {
            foreach (var item in other.Changes)
            {
                AddValue(item.Key, item.Value);
            }
        }

        public void Add(Item item, long diff)
        {
            AddValue(new CounterID(item.ItemID, item.Hue), diff);
            AddValue(new CounterID(item.ItemID), diff);
        }

        public void Reset()
        {
            Changes.Clear();
        }
    }

    public class ItemChangeTracker
    {
        private ItemCountCache _cache = new ItemCountCache();

        public void Add(Item item, ref CounterChangeSet changes)
        {
            var diff = _cache.Add(item);
            if (diff != 0)
            {
                changes.Add(item, diff);
            }
        }

        public void Remove(Item item, ref CounterChangeSet changes)
        {
            var diff = _cache.Remove(item);
            if (diff != 0)
            {
                changes.Add(item, diff);
            }
        }

        public void Reset()
        {
            _cache.Reset();
        }
    }

    public class RecursiveItemChangeTracker
    {
        private ItemChangeTracker _tracker = new ItemChangeTracker();

        public void Add(Item item, ref CounterChangeSet changes)
        {
            _tracker.Add(item, ref changes);

            foreach (var child in item.Contains)
            {
                Add(child, ref changes);
            }
        }

        public void Remove(Item item, ref CounterChangeSet changes)
        {
            foreach (var child in item.Contains)
            {
                Remove(child, ref changes);
            }

            _tracker.Remove(item, ref changes);
        }

        public void Reset()
        {
            _tracker.Reset();
        }
    }

    public class ItemTypeCounter
    {
        private CounterChangeSet _counters = new CounterChangeSet();
        private RecursiveItemChangeTracker _tracker = new RecursiveItemChangeTracker();

        public CounterChangeSet Count(Item item)
        {
            var changes = new CounterChangeSet();
            _tracker.Add(item, ref changes);
            _counters.Add(changes);
            return changes;
        }

        public CounterChangeSet Uncount(Item item)
        {
            var changes = new CounterChangeSet();
            _tracker.Remove(item, ref changes);
            _counters.Add(changes);
            return changes;
        }

        public ulong GetValue(ItemID itemID, ushort hue)
        {
            return GetValue(new CounterID(itemID, hue));
        }

        public ulong GetValue(CounterID id)
        {
            if (_counters.Changes.TryGetValue(id, out var value))
            {
                return (value > 0) ? (ulong) value : 0;
            }

            return 0;
        }

        public void Reset()
        {
            _counters.Reset();
            _tracker.Reset();
        }
    }

    public class Counter : IComparable
    {
        private string m_Name;
        private string m_Fmt;
        private ushort m_ItemID;
        private int m_Hue;
        private int m_Count;
        private bool m_Enabled;
        private DateTime m_LastWarning;
        private bool m_Flag;
        private bool m_DispImg;

        public Counter(string name, string fmt, ushort iid, int hue, bool dispImg)
        {
            m_Name = name;
            m_Fmt = fmt;
            m_ItemID = iid;
            m_Hue = hue;
            m_Enabled = false;
            m_Count = 0;
            m_DispImg = dispImg;

            m_NeedXMLSave = true;
        }

        public Counter(XmlElement node)
        {
            m_Name = GetText(node["name"], "");
            m_Fmt = GetText(node["format"], "");
            m_ItemID = (ushort) GetInt(GetText(node["itemid"], "0"), 0);
            m_Hue = GetInt(GetText(node["hue"], "-1"), -1);

            m_Enabled = false;
            m_DispImg = true;
        }

        public void Save(XmlTextWriter xml)
        {
            xml.WriteStartElement("counter");

            xml.WriteStartElement("name");
            xml.WriteString(m_Name);
            xml.WriteEndElement();

            xml.WriteStartElement("format");
            xml.WriteString(m_Fmt);
            xml.WriteEndElement();

            xml.WriteStartElement("itemid");
            xml.WriteString(m_ItemID.ToString());
            xml.WriteEndElement();

            xml.WriteStartElement("hue");
            xml.WriteString(m_Hue.ToString());
            xml.WriteEndElement();

            xml.WriteEndElement();
        }

        public string Name
        {
            get { return m_Name; }
        }

        public string Format
        {
            get { return m_Fmt; }
        }

        public ushort ItemID
        {
            get { return m_ItemID; }
        }

        public int Hue
        {
            get { return m_Hue; }
        }

        public bool Flag
        {
            get { return m_Flag; }
            set { m_Flag = value; }
        }

        public void Set(ushort iid, int hue, string name, string fmt, bool dispImg)
        {
            m_ItemID = iid;
            m_Hue = hue;
            m_Name = name;
            m_Fmt = fmt;
            m_DispImg = dispImg;

            m_NeedXMLSave = true;
        }

        public CounterID CounterID
        {
            get { return new CounterID(ItemID, (ushort) ((Hue == -1) ? 0xFFFF : Hue)); }
        }

        public static int GetCount(ItemID itemID, int hue)
        {
            ushort fixedHue = (ushort) ((hue == -1) ? 0xFFFF : hue);
            return (int) m_PackCounter.GetValue(itemID, fixedHue);
        }

        public static Counter FindCounter(string name)
        {
            foreach (Counter c in List)
            {
                if (c.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
                {
                    return c.Enabled ? c : null;
                }
            }

            return null;
        }

        private void OnUpdate()
        {
            var newValue = (int) m_PackCounter.GetValue(CounterID);
            if (m_Count != newValue)
            {
                if (m_Enabled)
                {
                    if (!SupressWarnings && m_LastWarning + TimeSpan.FromSeconds(1.0) < DateTime.UtcNow &&
                        World.Player != null && newValue < m_Count && Config.GetBool("CounterWarn") &&
                        newValue < Config.GetInt("CounterWarnAmount"))
                    {
                        World.Player.SendMessage(MsgLevel.Warning, LocString.CountLow, Name, newValue);
                        m_LastWarning = DateTime.UtcNow;
                    }

                    // TODO Razor CE: UOAssist.PostCounterUpdate + Titlebar-Update entfernt.

                    m_Count = newValue;
                    if (m_Count < 0)
                        m_Count = 0;
                }
            }
        }

        public int Amount
        {
            get => m_Count;
        }

        public void SetEnabled(bool value)
        {
            m_Enabled = value;
            if (m_Enabled)
            {
                if (!SupressChecks)
                    QuickRecount();
            }
        }

        public bool Enabled
        {
            get { return m_Enabled; }
            set
            {
                if (m_Enabled != value)
                {
                    SetEnabled(value);
                }
            }
        }

        public bool DisplayImage
        {
            get { return m_DispImg; }
            set { m_DispImg = value; }
        }

        public int CompareTo(object comp)
        {
            if (!(comp is Counter))
                return 1;
            else if (this.Enabled && ((Counter) comp).Enabled)
                return this.Name == null ? 1 :
                    ((Counter) comp).Name == null ? -1 : this.Name.CompareTo(((Counter) comp).Name);
            else if (!this.Enabled && ((Counter) comp).Enabled)
                return 1;
            else if (this.Enabled && !((Counter) comp).Enabled)
                return -1;
            else //if ( !this.Enabled && !((Counter)comp).Enabled )
                return this.Name == null ? 1 :
                    ((Counter) comp).Name == null ? -1 : this.Name.CompareTo(((Counter) comp).Name);
        }

        public override string ToString()
        {
            return $"{Name} ({Format})";
        }

        private static bool m_Initialized;
        private static bool m_NeedXMLSave = false;
        private static List<Counter> m_List = new List<Counter>();
        private static bool m_SupressWarn, m_SupressChecks;
        private static ItemTypeCounter m_PackCounter = new ItemTypeCounter();

        public static List<Counter> List
        {
            get { return m_List; }
        }

        /// <summary>
        /// Razor CE: statischer Konstruktor (Load) + Config.cs-Registrierung.
        /// Laedt counters.xml und registriert die "counters"-Profilsektion.
        /// Register() ist idempotent (ersetzt eine bestehende Registrierung).
        /// </summary>
        public static void Initialize()
        {
            if (!m_Initialized)
            {
                m_Initialized = true;
                Load();
            }

            ProfileSections.Register("counters", LoadProfile, SaveProfile, Default);
        }

        public static bool SupressWarnings
        {
            get { return m_SupressWarn; }
            set { m_SupressWarn = value; }
        }

        public static bool SupressChecks
        {
            get { return m_SupressChecks; }
        }

        private static void Load()
        {
            string file = Path.Combine(Config.GetUserDirectory(), "counters.xml");
            if (!File.Exists(file))
                return;

            try
            {
                XmlDocument doc = new XmlDocument();
                doc.Load(file);

                XmlElement root = doc["counters"];
                if (root != null)
                {
                    foreach (XmlElement node in root.GetElementsByTagName("counter"))
                        m_List.Add(new Counter(node));
                }
            }
            catch
            {
                // Original: MessageBox "Counters.xml Load Error"
            }

            m_NeedXMLSave = false;
        }

        public static void Save()
        {
            if (!m_NeedXMLSave)
                return;

            try
            {
                string file = Path.Combine(Config.GetUserDirectory(), "counters.xml");
                using (StreamWriter op = new StreamWriter(file))
                {
                    XmlTextWriter xml = new XmlTextWriter(op);

                    xml.Formatting = Formatting.Indented;
                    xml.IndentChar = '\t';
                    xml.Indentation = 1;

                    xml.WriteStartDocument(true);

                    xml.WriteStartElement("counters");

                    foreach (Counter c in m_List)
                        c.Save(xml);

                    xml.WriteEndElement();
                    xml.Close();
                }

                m_NeedXMLSave = false;
            }
            catch
            {
            }
        }

        public static void SaveProfile(XmlWriter xml)
        {
            for (int i = 0; i < m_List.Count; i++)
            {
                Counter c = m_List[i];
                if (c.Enabled)
                {
                    xml.WriteStartElement("counter");
                    xml.WriteAttributeString("name", c.Name);
                    xml.WriteAttributeString("enabled", c.Enabled.ToString());
                    xml.WriteAttributeString("image", c.DisplayImage.ToString());
                    xml.WriteEndElement();
                }
            }
        }

        public static void Default()
        {
            for (int i = 0; i < m_List.Count; i++)
            {
                Counter c = m_List[i];

                if (c.Format == "bp" || c.Format == "bm" || c.Format == "gl" || c.Format == "gs" ||
                    c.Format == "mr" || c.Format == "ns" || c.Format == "ss" || c.Format == "sa" ||
                    c.Format == "aids")
                {
                    c.Enabled = true;
                }
            }
        }

        public static void DisableAll()
        {
            for (int i = 0; i < m_List.Count; i++)
                m_List[i].Enabled = false;
        }

        public static void LoadProfile(XmlElement xml)
        {
            Reset();
            DisableAll();

            if (xml == null)
                return;

            foreach (XmlElement el in xml.GetElementsByTagName("counter"))
            {
                try
                {
                    string name = el.GetAttribute("name");
                    string en = el.GetAttribute("enabled");
                    string img = el.GetAttribute("image");

                    for (int i = 0; i < m_List.Count; i++)
                    {
                        Counter c = m_List[i];

                        if (c.Name == name)
                        {
                            c.Enabled = Convert.ToBoolean(en);
                            try
                            {
                                c.DisplayImage = Convert.ToBoolean(img);
                            }
                            catch
                            {
                                c.DisplayImage = true;
                            }

                            break;
                        }
                    }
                }
                catch
                {
                }
            }
        }

        private static string GetText(XmlElement node, string defaultValue)
        {
            if (node == null)
                return defaultValue;

            return node.InnerText;
        }

        private static int GetInt(string value, int def)
        {
            try
            {
                return XmlConvert.ToInt32(value);
            }
            catch
            {
                try
                {
                    return Convert.ToInt32(value);
                }
                catch
                {
                    return def;
                }
            }
        }

        public static void Register(Counter c)
        {
            m_List.Add(c);
            m_NeedXMLSave = true;
        }

        private static void OnCountsChanged(CounterChangeSet changes)
        {
            foreach (var c in m_List)
            {
                if (changes.Changes.ContainsKey(c.CounterID))
                {
                    c.OnUpdate();
                }
            }
        }

        public static void Uncount(Item item)
        {
            var changes = m_PackCounter.Uncount(item);
            OnCountsChanged(changes);
        }

        public static void Count(Item item)
        {
            var changes = m_PackCounter.Count(item);
            OnCountsChanged(changes);
        }

        public static void QuickRecount()
        {
            Reset();

            SupressWarnings = true;
            Item pack = World.Player == null ? null : World.Player.Backpack;
            if (pack != null)
                Count(pack);
            pack = World.Player == null ? null : World.Player.Quiver;
            if (pack != null)
                Count(pack);
            SupressWarnings = false;
        }

        public static void FullRecount()
        {
            Reset();

            if (World.Player != null)
            {
                SupressWarnings = true;

                if (World.Player.Backpack != null)
                {
                    while (World.Player.Backpack.Contains.Count > 0)
                        ((Item) World.Player.Backpack.Contains[0]).Remove();

                    PacketHandlers.IgnoreGumps.Add(World.Player.Backpack);
                    PlayerData.DoubleClick(World.Player.Backpack);
                }

                if (World.Player.Quiver != null)
                {
                    while (World.Player.Quiver.Contains.Count > 0)
                        ((Item) World.Player.Quiver.Contains[0]).Remove();

                    PacketHandlers.IgnoreGumps.Add(World.Player.Quiver);
                    PlayerData.DoubleClick(World.Player.Quiver);
                }

                if (!Config.GetBool("AutoSearch"))
                    World.Player.SendMessage(MsgLevel.Info, LocString.NoAutoCount);
                SupressWarnings = false;
            }
        }

        public static void Reset()
        {
            SupressWarnings = true;
            m_PackCounter.Reset();

            foreach (var c in m_List)
            {
                c.OnUpdate();
            }

            SupressWarnings = false;
        }
    }
}
