#region license
// UOSagas Razor: An Ultima Online Assistant for the UOSagas shard
// Copyright (C) 2026 UOSagas (3HMonkey)
//
// Based on Razor: An Ultima Online Assistant
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

// UOSagas-Razor: Tests fuer Phase 2d — Agents, DressLists, Counters.
// Paket-Fixtures big-endian; Layouts entsprechen Razor CE
// (Razor/Network/Handlers.cs bzw. Razor/Network/Packets.cs).
// Die agents-/counters-/dresslists-Profilsektionen sind im CE-XML-Format
// (Razor/Agents/Agents.cs SaveProfile: pro Agent ein Element mit Items/Hotbags).

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml;
using Assistant;
using Assistant.Agents;
using Assistant.Macros;
using Xunit;

namespace Razor.Core.Tests
{
    [Collection("ConfigSequential")]
    public class AgentTests : IDisposable
    {
        private const uint PlayerSerial = 0x00000701;
        private const uint BackpackSerial = 0x40000702;

        private readonly string m_TempDir;
        private readonly CultureInfo m_OldCulture;
        private readonly FakeClientServices m_Fake;
        private readonly PlayerData m_Player;

        public AgentTests()
        {
            m_OldCulture = CultureInfo.CurrentCulture;
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;

            m_TempDir = Path.Combine(Path.GetTempPath(), "RazorAgentTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(m_TempDir);

            Config.Initialize(m_TempDir);
            Config.LoadLastProfile();

            PacketHandlers.Initialize(); // idempotent

            // Phase-2d-Manager: erzeugen (einmalig) + Profil-Sektionen registrieren.
            Counter.Initialize();
            Agent.Initialize();
            DressList.Initialize();

            Engine.UsePostKRPackets = true;
            Engine.UsePostSAChanges = true;
            Engine.UseNewMobileIncoming = true;
            Engine.UsePostHSChanges = true;

            MacroManager.Stop();
            ActionQueue.Stop();
            Targeting.Reset();

            // Statischen Agent-/Counter-Zustand aus frueheren Tests entfernen.
            Agent.ClearAll();
            DressList.ClearAll();
            Counter.List.Clear();
            Counter.Reset();

            World.Clear();
            m_Player = new PlayerData(PlayerSerial);
            m_Player.Position = new Point3D(1000, 1000, 0);
            World.AddMobile(m_Player);
            World.Player = m_Player;

            ResetAgentToggles();

            m_Fake = new FakeClientServices();
            ClientProxy.Bind(m_Fake);
        }

        public void Dispose()
        {
            MacroManager.Stop();
            ActionQueue.Stop();
            Targeting.Reset();

            ResetAgentToggles();
            Agent.ClearAll();
            DressList.ClearAll();
            Counter.List.Clear();
            Counter.Reset();

            // Sektions-Registrierungen zuruecknehmen, damit die uebrigen
            // Testklassen (Sektions-Erhaltung!) unbeeinflusst bleiben.
            ProfileSections.Unregister("counters");
            ProfileSections.Unregister("agents");
            ProfileSections.Unregister("dresslists");

            ClientProxy.Unbind();
            World.Clear();

            CultureInfo.CurrentCulture = m_OldCulture;
            try
            {
                Directory.Delete(m_TempDir, true);
            }
            catch
            {
            }
        }

        private static void ResetAgentToggles()
        {
            if (BuyAgent.Agents != null)
            {
                foreach (BuyAgent b in BuyAgent.Agents)
                    b.Enabled = false;
            }

            if (SellAgent.Instance != null)
                SellAgent.Instance.Enabled = false;

            // Disable() setzt eine Player-Meldung ab und braucht World.Player.
            if (ScavengerAgent.Instance != null && ScavengerAgent.Instance.Enabled && World.Player != null)
                ScavengerAgent.Instance.Disable();
        }

        // ---- Helpers ---------------------------------------------------------

        private sealed class ByteWriter
        {
            private readonly List<byte> _bytes = new List<byte>();

            public ByteWriter Byte(byte v)
            {
                _bytes.Add(v);
                return this;
            }

            public ByteWriter SByte(sbyte v)
            {
                _bytes.Add((byte) v);
                return this;
            }

            public ByteWriter UShort(ushort v)
            {
                _bytes.Add((byte) (v >> 8));
                _bytes.Add((byte) v);
                return this;
            }

            public ByteWriter UInt(uint v)
            {
                _bytes.Add((byte) (v >> 24));
                _bytes.Add((byte) (v >> 16));
                _bytes.Add((byte) (v >> 8));
                _bytes.Add((byte) v);
                return this;
            }

            public ByteWriter Int(int v)
            {
                return UInt((uint) v);
            }

            public ByteWriter Ascii(string s)
            {
                foreach (char c in s)
                    _bytes.Add((byte) c);
                return this;
            }

            public byte[] ToArray()
            {
                return _bytes.ToArray();
            }

            public byte[] ToDynamicArray()
            {
                int len = _bytes.Count + 2;
                _bytes.Insert(1, (byte) (len >> 8));
                _bytes.Insert(2, (byte) len);
                return _bytes.ToArray();
            }
        }

        private static bool Recv(byte[] data, bool dynamicLength)
        {
            return PacketHandler.OnServerPacket(data[0], new PacketReader(data, dynamicLength), null);
        }

        private static uint ReadUInt(byte[] data, int offset)
        {
            return (uint) ((data[offset] << 24) | (data[offset + 1] << 16) | (data[offset + 2] << 8) |
                           data[offset + 3]);
        }

        private static ushort ReadUShort(byte[] data, int offset)
        {
            return (ushort) ((data[offset] << 8) | data[offset + 1]);
        }

        /// <summary>Backpack-Item am Player anlegen (Layer + Container).</summary>
        private Item CreateBackpack()
        {
            Item pack = new Item(BackpackSerial);
            pack.ItemID = 0x0E75;
            pack.Layer = Layer.Backpack;
            World.AddItem(pack);
            pack.Container = m_Player;
            return pack;
        }

        private Item CreateItemIn(Item container, uint serial, ushort itemId, ushort amount = 1)
        {
            Item item = new Item(serial);
            item.ItemID = itemId;
            item.Amount = amount;
            World.AddItem(item);
            item.Container = container;
            return item;
        }

        private IEnumerable<byte[]> SentWithId(byte id)
        {
            return m_Fake.SentToServer.Where(b => b.Length > 0 && b[0] == id);
        }

        // ---- (a) Profil-Roundtrip: agents-Sektion im CE-Format ----------------

        private const string AgentsProfileFixture =
            "<?xml version=\"1.0\" encoding=\"utf-8\" standalone=\"yes\"?>\n" +
            "<profile>\n" +
            "\t<agents>\n" +
            "\t\t<Organizer-1 hotbag=\"1074000001\" alias=\"Erze\">\n" +
            "\t\t\t<item id=\"6585\" />\n" +
            "\t\t\t<item id=\"3862\" />\n" +
            "\t\t</Organizer-1>\n" +
            "\t\t<Buy-1 enabled=\"True\" alias=\"\">\n" +
            "\t\t\t<item id=\"3842\" amount=\"20\" />\n" +
            "\t\t</Buy-1>\n" +
            "\t\t<Scavenger enabled=\"True\">\n" +
            "\t\t\t<bag serial=\"0x40000123\" />\n" +
            "\t\t\t<item id=\"3821\" />\n" +
            "\t\t</Scavenger>\n" +
            "\t</agents>\n" +
            "</profile>\n";

        [Fact]
        public void AgentsProfile_CeFormat_LoadSaveRoundtrip()
        {
            string dir = Config.GetUserDirectory("Profiles");
            string file = Path.Combine(dir, "agentfixture.xml");
            File.WriteAllText(file, AgentsProfileFixture);

            Profile p = new Profile("agentfixture");
            Assert.True(p.Load());

            // agents ist registriert -> KEINE unbekannte Sektion mehr.
            Assert.DoesNotContain(p.UnknownSections, kv => kv.Key == "agents");

            // Organizer-1: 2 Items + Hotbag + Alias
            OrganizerAgent org = OrganizerAgent.Agents[0];
            Assert.Equal(2, org.Items.Count);
            Assert.Equal((ushort) 6585, org.Items[0].Value);
            Assert.Equal((ushort) 3862, org.Items[1].Value);
            Assert.Equal(1074000001u, org.HotBag);
            Assert.Equal("Erze", org.Alias);

            // Buy-1: 1 Item, enabled
            BuyAgent buy = BuyAgent.Agents[0];
            Assert.True(buy.Enabled);
            Assert.Single(buy.Items);
            Assert.Equal((ushort) 3842, buy.Items[0].Id.Value);
            Assert.Equal((ushort) 20, buy.Items[0].Amount);

            // Scavenger: 1 Item + Bag, enabled
            ScavengerAgent scav = ScavengerAgent.Instance;
            Assert.True(scav.Enabled);
            Assert.Single(scav.Items);
            Assert.Equal((ushort) 3821, scav.Items[0].Value);
            Assert.Equal(0x40000123u, scav.Bag.Value);

            // Save erzeugt wieder das CE-Format (Element je Agent).
            p.SaveToFile(file);
            p.Unload();

            XmlDocument doc = new XmlDocument();
            doc.Load(file);
            XmlElement agents = doc["profile"]["agents"];
            Assert.NotNull(agents);

            XmlElement orgEl = agents["Organizer-1"];
            Assert.NotNull(orgEl);
            Assert.Equal("1074000001", orgEl.GetAttribute("hotbag"));
            Assert.Equal("Erze", orgEl.GetAttribute("alias"));
            XmlNodeList orgItems = orgEl.GetElementsByTagName("item");
            Assert.Equal(2, orgItems.Count);
            Assert.Equal("6585", ((XmlElement) orgItems[0]).GetAttribute("id"));
            Assert.Equal("3862", ((XmlElement) orgItems[1]).GetAttribute("id"));

            XmlElement buyEl = agents["Buy-1"];
            Assert.NotNull(buyEl);
            Assert.Equal("True", buyEl.GetAttribute("enabled"));
            XmlNodeList buyItems = buyEl.GetElementsByTagName("item");
            Assert.Equal(1, buyItems.Count);
            Assert.Equal("3842", ((XmlElement) buyItems[0]).GetAttribute("id"));
            Assert.Equal("20", ((XmlElement) buyItems[0]).GetAttribute("amount"));

            XmlElement scavEl = agents["Scavenger"];
            Assert.NotNull(scavEl);
            Assert.Equal("True", scavEl.GetAttribute("enabled"));
            Assert.Equal("0x40000123", scavEl["bag"].GetAttribute("serial"));
            Assert.Equal("3821", ((XmlElement) scavEl.GetElementsByTagName("item")[0]).GetAttribute("id"));

            // Roundtrip: gespeichertes Profil laedt wieder in die Agents.
            Agent.ClearAll();
            ResetAgentToggles();

            Profile p2 = new Profile("agentfixture");
            Assert.True(p2.Load());
            Assert.Equal(2, OrganizerAgent.Agents[0].Items.Count);
            Assert.Equal(1074000001u, OrganizerAgent.Agents[0].HotBag);
            Assert.Single(BuyAgent.Agents[0].Items);
            Assert.True(BuyAgent.Agents[0].Enabled);
            Assert.Single(ScavengerAgent.Instance.Items);
            Assert.Equal(0x40000123u, ScavengerAgent.Instance.Bag.Value);
            p2.Unload();
        }

        // ---- (b) BuyAgent: 0x74 + 0x24 -> Kauf-Antwort 0x3B --------------------

        [Fact]
        public void BuyAgent_VendorBuyList_SendsVendorBuyResponse()
        {
            const uint vendorSerial = 0x00000AB0;
            const uint shopPackSerial = 0x40000AB1;
            const uint shopItemSerial = 0x40000AB2;
            const ushort gfx = 0x0EED;

            CreateBackpack();
            m_Player.Gold = 5000;

            Mobile vendor = new Mobile(vendorSerial);
            World.AddMobile(vendor);

            Item shopPack = new Item(shopPackSerial);
            shopPack.ItemID = 0x0E75;
            shopPack.Layer = Layer.ShopBuy;
            World.AddItem(shopPack);
            shopPack.Container = vendor;

            Item shopItem = CreateItemIn(shopPack, shopItemSerial, gfx, amount: 50);

            // 0x74 ExtBuyInfo: Preis 15 + Beschreibung fuer das eine Item.
            byte[] extInfo = new ByteWriter()
                .Byte(0x74)
                .UInt(shopPackSerial)
                .Byte(1)
                .Int(15) // Preis
                .Byte(5).Ascii("apple")
                .ToDynamicArray();
            Recv(extInfo, dynamicLength: true);

            Assert.Equal(15, shopItem.Price);

            // Agent scharf schalten: will 10 Stueck der Grafik.
            BuyAgent agent = BuyAgent.Agents[0];
            agent.Enabled = true;
            agent.AddItem(new BuyAgent.BuyEntry(gfx, 10));

            // 0x24 DisplayBuy (gump 0x30) -> Agent antwortet mit 0x3B und blockt.
            byte[] displayBuy = new ByteWriter()
                .Byte(0x24)
                .UInt(vendorSerial)
                .UShort(0x30)
                .ToArray();
            bool blocked = Recv(displayBuy, dynamicLength: false);

            Assert.True(blocked);

            byte[] buyPacket = Assert.Single(SentWithId(0x3B));
            // Layout: id(0), len(1-2), vendor(3-6), flag 0x02(7),
            //         layer 0x1A(8), serial(9-12), amount(13-14)
            Assert.Equal(vendorSerial, ReadUInt(buyPacket, 3));
            Assert.Equal((byte) 0x02, buyPacket[7]);
            Assert.Equal((byte) 0x1A, buyPacket[8]);
            Assert.Equal(shopItemSerial, ReadUInt(buyPacket, 9));
            Assert.Equal((ushort) 10, ReadUShort(buyPacket, 13));
        }

        // ---- (c) ScavengerAgent: neues Bodenitem -> Lift 0x07 -------------------

        [Fact]
        public void ScavengerAgent_NewGroundItem_SendsLiftAndDropToBackpack()
        {
            const uint groundSerial = 0x40000B01;
            const ushort gfx = 0x0EED;

            Item pack = CreateBackpack();

            ScavengerAgent scav = ScavengerAgent.Instance;
            scav.Add(gfx);
            scav.Enable();
            scav.ClearCache();

            // 0x1A WorldItem: neues Bodenitem 1 Tile neben dem Player
            // (y | 0x4000 -> Flags-Byte vorhanden; 0x20 = movable).
            byte[] worldItem = new ByteWriter()
                .Byte(0x1A)
                .UInt(groundSerial) // ohne 0x80000000 -> Amount 1
                .UShort(gfx)
                .UShort(1001) // x
                .UShort(1000 | 0x4000) // y + Flags-Bit
                .SByte(0)
                .Byte(0x20) // flags: movable
                .ToDynamicArray();
            Recv(worldItem, dynamicLength: true);

            // DragDropManager hat sofort gelifted (ObjectDelay-Tick 1 ist synchron).
            byte[] lift = Assert.Single(SentWithId(0x07));
            Assert.Equal(groundSerial, ReadUInt(lift, 1));
            Assert.Equal((ushort) 1, ReadUShort(lift, 5));

            // ... und ins Hotbag/Backpack gedroppt (0x08, Ziel = Backpack).
            byte[] drop = Assert.Single(SentWithId(0x08));
            Assert.Equal(groundSerial, ReadUInt(drop, 1));
            Assert.Equal(pack.Serial.Value, ReadUInt(drop, drop.Length - 4));
        }

        // ---- (d) DressList: Dress() -> Lift + EquipRequest ----------------------

        [Fact]
        public void DressList_Dress_SendsEquipRequestSequence()
        {
            const uint cloakSerial = 0x40000C01;
            const ushort cloakGfx = 0x1515;

            Item pack = CreateBackpack();
            Item cloak = CreateItemIn(pack, cloakSerial, cloakGfx);
            cloak.Layer = Layer.Cloak;

            DressList list = new DressList("Test");
            list.Items.Add((ItemID) cloakGfx);
            DressList.Add(list);

            Assert.Same(list, DressList.Find("Test"));

            list.Dress();

            // Lift 0x07 fuer das Item aus dem Backpack ...
            byte[] lift = Assert.Single(SentWithId(0x07));
            Assert.Equal(cloakSerial, ReadUInt(lift, 1));

            // ... und EquipRequest 0x13 auf den Player-Layer.
            byte[] equip = Assert.Single(SentWithId(0x13));
            Assert.Equal(cloakSerial, ReadUInt(equip, 1));
            Assert.Equal((byte) Layer.Cloak, equip[5]);
            Assert.Equal(PlayerSerial, ReadUInt(equip, 6));
        }

        [Fact]
        public void DressAction_Perform_UsesDressList()
        {
            const uint cloakSerial = 0x40000C11;
            const ushort cloakGfx = 0x1515;

            Item pack = CreateBackpack();
            Item cloak = CreateItemIn(pack, cloakSerial, cloakGfx);
            cloak.Layer = Layer.Cloak;

            DressList list = new DressList("Kampf");
            list.Items.Add((ItemID) cloakGfx);
            DressList.Add(list);

            DressAction action = new DressAction(new[] {"x", "Kampf"});
            // Perform liefert false (MacroWaitAction wartet auf die ActionQueue).
            Assert.False(action.Perform());

            Assert.Single(SentWithId(0x13));
        }

        // ---- (e) Counter: Weltmodell-Zaehlung + If-Bedingung --------------------

        [Fact]
        public void Counter_CountsBackpackItems_AndIfActionEvaluates()
        {
            const ushort gfx = 0x0F06;

            CreateBackpack();

            Counter counter = new Counter("TestZutat", "tz", gfx, -1, true);
            Counter.Register(counter);
            counter.Enabled = true;

            Assert.Equal(0, counter.Amount);

            // 0x25 ContainerContentUpdate: 5 Stueck in den Backpack.
            byte[] update = new ByteWriter()
                .Byte(0x25)
                .UInt(0x40000D01)
                .UShort(gfx)
                .SByte(0) // itemID offset
                .UShort(5) // amount
                .UShort(10).UShort(20) // x/y
                .Byte(0) // grid (UsePostKRPackets)
                .UInt(BackpackSerial)
                .UShort(0) // hue
                .UShort(0) // flags (Sagas: 2 Byte)
                .ToDynamicArray();
            Recv(update, dynamicLength: true);

            Assert.Equal(5, counter.Amount);
            Assert.Equal(5, Counter.GetCount(gfx, -1));

            // If-Bedingungen (Richtung: 0 <=, 1 >=, 2 <, 3 >).
            Assert.True(new IfAction(IfAction.IfVarType.Counter, 3, 4, "TestZutat").Evaluate()); // 5 > 4
            Assert.True(new IfAction(IfAction.IfVarType.Counter, 1, 5, "TestZutat").Evaluate()); // 5 >= 5
            Assert.False(new IfAction(IfAction.IfVarType.Counter, 2, 5, "TestZutat").Evaluate()); // !(5 < 5)

            // While-Bedingung nutzt denselben Zaehler.
            Assert.True(new WhileAction(WhileAction.WhileVarType.Counter, 1, 1, "TestZutat").Evaluate());

            // Item entfernen (0x1D RemoveObject) -> verzoegert (Razor CE 0.25s),
            // Zaehler faellt erst nach Ablauf des Remove-Timers auf 0.
            byte[] remove = new ByteWriter().Byte(0x1D).UInt(0x40000D01).ToArray();
            Recv(remove, dynamicLength: false);

            System.Threading.Thread.Sleep(350);
            Timer.Slice();

            Assert.Equal(0, counter.Amount);
            Assert.False(new IfAction(IfAction.IfVarType.Counter, 3, 0, "TestZutat").Evaluate());
        }
    }
}
