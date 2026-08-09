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

// UOSagas-Razor: Tests fuer den fuenften Options-Schwung, Teil 2 —
// Anzeige-Batch: ShowDamageTaken/-Dealt, ShowMobNames, LastTargTextFlags,
// ShowTextTargetIndicator, ShowStaticWalls/-Labels.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Assistant;
using Assistant.Macros;
using Xunit;

namespace Razor.Core.Tests
{
    [Collection("ConfigSequential")]
    public class DisplayBatchTests : IDisposable
    {
        private const uint PlayerSerial = 0x00001001;
        private const uint EnemySerial = 0x00001002;
        private const uint NewMobSerial = 0x00001003;
        private const uint FieldSerial = 0x40001004;

        private readonly string m_TempDir;
        private readonly CultureInfo m_OldCulture;
        private readonly FakeClientServices m_Fake;

        public DisplayBatchTests()
        {
            m_OldCulture = CultureInfo.CurrentCulture;
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;

            m_TempDir = Path.Combine(Path.GetTempPath(), "RazorDisplayBatchTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(m_TempDir);
            Config.Initialize(m_TempDir);
            Config.LoadLastProfile();

            PacketHandlers.Initialize();
            MacroManager.Stop();
            ActionQueue.Stop();
            Targeting.Reset();

            World.Clear();
            PlayerData player = new PlayerData(PlayerSerial)
            {
                Name = "Tester",
                Position = new Point3D(100, 100, 0),
                Visible = true
            };
            World.AddMobile(player);
            World.Player = player;

            Mobile enemy = new Mobile(EnemySerial)
            {
                Name = "an orc",
                Position = new Point3D(103, 100, 0),
                Notoriety = 6
            };
            World.AddMobile(enemy);

            m_Fake = new FakeClientServices();
            ClientProxy.Bind(m_Fake);
        }

        public void Dispose()
        {
            Assistant.Core.BandageTimer.Stop();
            ClientProxy.Unbind();
            Targeting.Reset();
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

        // ------------------------------------------------------------ helpers

        private static void UInt(List<byte> b, uint v)
        {
            b.Add((byte) (v >> 24));
            b.Add((byte) (v >> 16));
            b.Add((byte) (v >> 8));
            b.Add((byte) v);
        }

        private static void UShort(List<byte> b, ushort v)
        {
            b.Add((byte) (v >> 8));
            b.Add((byte) v);
        }

        private static void RecvDamage(uint serial, ushort damage)
        {
            var b = new List<byte> { 0x0B };
            UInt(b, serial);
            UShort(b, damage);
            PacketHandler.OnServerPacket(0x0B, new PacketReader(b.ToArray(), false), null);
        }

        /// <summary>Extrahiert den Unicode-Text aus einem injizierten 0xAE.</summary>
        private static string UnicodeText(byte[] pkt)
        {
            // 0xAE: id(1) len(2) serial(4) body(2) type(1) hue(2) font(2) lang(4) name(30) text...
            return Encoding.BigEndianUnicode.GetString(pkt, 48, pkt.Length - 50);
        }

        private static uint PacketSerial(byte[] pkt, int offset)
        {
            return (uint) ((pkt[offset] << 24) | (pkt[offset + 1] << 16) | (pkt[offset + 2] << 8) | pkt[offset + 3]);
        }

        // ------------------------------------------------------------ damage

        [Fact]
        public void ShowDamageTaken_zeigt_eigenen_Schaden_overhead()
        {
            Config.SetProperty("ShowDamageTaken", true);
            Config.SetProperty("ShowDamageTakenOverhead", true);

            RecvDamage(PlayerSerial, 15);

            byte[] msg = Assert.Single(m_Fake.InjectedToClient, pkt => pkt[0] == 0xAE);
            Assert.Equal(PlayerSerial, PacketSerial(msg, 3));
            Assert.Contains("[15]", UnicodeText(msg));
        }

        [Fact]
        public void ShowDamageDealt_zeigt_Schaden_ueber_dem_Ziel()
        {
            Config.SetProperty("ShowDamageDealt", true);
            Config.SetProperty("ShowDamageDealtOverhead", true);

            RecvDamage(EnemySerial, 22);

            byte[] msg = Assert.Single(m_Fake.InjectedToClient, pkt => pkt[0] == 0xAE);
            Assert.Equal(EnemySerial, PacketSerial(msg, 3));
            Assert.Contains("[22]", UnicodeText(msg));
        }

        [Fact]
        public void ShowDamage_aus_zeigt_nichts()
        {
            Config.SetProperty("ShowDamageTaken", false);
            Config.SetProperty("ShowDamageDealt", false);

            RecvDamage(PlayerSerial, 15);
            RecvDamage(EnemySerial, 22);

            Assert.Empty(m_Fake.InjectedToClient);
        }

        // ------------------------------------------------------------ mob names

        private static void RecvMobileIncoming(uint serial)
        {
            var b = new List<byte> { 0x78, 0, 0 };
            UInt(b, serial);
            UShort(b, 0x0190); // body
            UShort(b, 105); // x
            UShort(b, 100); // y
            b.Add(0); // z
            b.Add(2); // dir
            UShort(b, 0); // hue
            UShort(b, 0); // Sagas: 2-Byte-Flags
            b.Add(1); // noto
            UInt(b, 0); // Equip-Terminator

            int len = b.Count;
            b[1] = (byte) (len >> 8);
            b[2] = (byte) len;
            PacketHandler.OnServerPacket(0x78, new PacketReader(b.ToArray(), true), null);
        }

        [Fact]
        public void ShowMobNames_klickt_neue_Mobiles_an()
        {
            Config.SetProperty("ShowMobNames", true);
            Config.SetProperty("LastTargTextFlags", false);

            RecvMobileIncoming(NewMobSerial);

            byte[] click = Assert.Single(m_Fake.SentToServer, pkt => pkt[0] == 0x09);
            Assert.Equal(NewMobSerial, PacketSerial(click, 1));
        }

        [Fact]
        public void ShowMobNames_aus_und_bekannte_Mobiles_kein_Klick()
        {
            Config.SetProperty("ShowMobNames", false);
            Config.SetProperty("LastTargTextFlags", false);

            RecvMobileIncoming(NewMobSerial);
            Assert.DoesNotContain(m_Fake.SentToServer, pkt => pkt[0] == 0x09);

            Config.SetProperty("ShowMobNames", true);
            RecvMobileIncoming(EnemySerial); // bekannt -> kein Klick
            Assert.DoesNotContain(m_Fake.SentToServer, pkt => pkt[0] == 0x09);
        }

        // ------------------------------------------------------------ text flags

        [Fact]
        public void LastTargTextFlags_zeigt_Flag_beim_SingleClick()
        {
            Config.SetProperty("LastTargTextFlags", true);
            Config.SetProperty("SmartLastTarget", false);
            Targeting.SetLastTarget((Serial) EnemySerial);

            // Client single-clickt den Gegner (0x09).
            var b = new List<byte> { 0x09 };
            UInt(b, EnemySerial);
            PacketHandler.OnClientPacket(0x09, new PacketReader(b.ToArray(), false), null);

            byte[] msg = Assert.Single(m_Fake.InjectedToClient, pkt => pkt[0] == 0xAE);
            Assert.Equal(EnemySerial, PacketSerial(msg, 3));
            Assert.Contains("[Last Target]", UnicodeText(msg));
        }

        // ------------------------------------------------------------ target indicator

        [Fact]
        public void TargetIndicator_erscheint_beim_Beantworten_des_Cursors()
        {
            Config.SetProperty("ShowTextTargetIndicator", true);
            Config.SetProperty("ShowAttackTargetOverhead", false);
            Config.SetProperty("ShowAttackTargetNewOnly", false);
            Config.SetProperty("TargetIndicatorFormat", "-> {name}");
            Config.SetProperty("TargetIndicatorHue", 10);

            // Server oeffnet den Cursor, Spieler beantwortet ihn auf den Ork.
            var t = new List<byte> { 0x6C, 0x00 };
            UInt(t, 0xB1);
            t.Add(0);
            for (int i = 0; i < 12; i++) t.Add(0);
            PacketHandler.OnServerPacket(0x6C, new PacketReader(t.ToArray(), false), null);

            Mobile enemy = World.FindMobile(EnemySerial);
            var r = new List<byte> { 0x6C, 0x00 };
            UInt(r, 0xB1);
            r.Add(0);
            UInt(r, EnemySerial);
            UShort(r, (ushort) enemy.Position.X);
            UShort(r, (ushort) enemy.Position.Y);
            UShort(r, (ushort) enemy.Position.Z);
            UShort(r, enemy.Body);
            PacketHandler.OnClientPacket(0x6C, new PacketReader(r.ToArray(), false), null);

            byte[] msg = Assert.Single(m_Fake.InjectedToClient, pkt => pkt[0] == 0xAE);
            Assert.Equal(EnemySerial, PacketSerial(msg, 3));
            Assert.Contains("-> an orc", UnicodeText(msg));
        }

        // ------------------------------------------------------------ static walls

        private static void RecvF3(uint serial, ushort itemId, ushort x, ushort y)
        {
            var b = new List<byte> { 0xF3 };
            UShort(b, 0x0001);
            b.Add(0x00); // art data
            UInt(b, serial);
            UShort(b, itemId);
            b.Add(0); // graphic inc
            UShort(b, 1);
            UShort(b, 1);
            UShort(b, x);
            UShort(b, y);
            b.Add(0); // z
            b.Add(0); // dir/light
            UShort(b, 0); // hue
            UShort(b, 0); // sagas flags
            UShort(b, 0); // unk2
            PacketHandler.OnServerPacket(0xF3, new PacketReader(b.ToArray(), false), null);
        }

        [Fact]
        public void ShowStaticWalls_ersetzt_ein_FireField()
        {
            Config.SetProperty("ShowStaticWalls", true);
            Config.SetProperty("ShowStaticWallLabels", false);

            RecvF3(FieldSerial, 0x3996, 105, 100); // Fire Field

            // Weltmodell traegt die Ersatzgrafik (CE-Verhalten).
            Item field = World.FindItem(FieldSerial);
            Assert.Equal((ushort) 0x28A8, (ushort) field.ItemID);
            Assert.Equal((ushort) 0x0845, field.Hue);

            // Injiziertes 0xF3 mit der Wand-Grafik.
            byte[] inj = Assert.Single(m_Fake.InjectedToClient, pkt => pkt[0] == 0xF3);
            Assert.Equal(FieldSerial, PacketSerial(inj, 4));
            ushort graphic = (ushort) ((inj[8] << 8) | inj[9]);
            Assert.Equal((ushort) 0x28A8, graphic);
        }

        [Fact]
        public void ShowStaticWallLabels_haengt_das_Label_an()
        {
            Config.SetProperty("ShowStaticWalls", true);
            Config.SetProperty("ShowStaticWallLabels", true);

            RecvF3(FieldSerial, 0x3915, 105, 100); // Poison Field

            byte[] label = Assert.Single(m_Fake.InjectedToClient, pkt => pkt[0] == 0xAE);
            Assert.Contains("[Poison Field]", UnicodeText(label));
        }

        [Fact]
        public void ShowStaticWalls_aus_laesst_Felder_unangetastet()
        {
            Config.SetProperty("ShowStaticWalls", false);

            RecvF3(FieldSerial, 0x3996, 105, 100);

            Item field = World.FindItem(FieldSerial);
            Assert.Equal((ushort) 0x3996, (ushort) field.ItemID);
            Assert.Empty(m_Fake.InjectedToClient);
        }

        // ------------------------------------------------------------ container labels

        private const uint ChestSerial = 0x40001005;

        /// <summary>0x1C Ascii-Label vom Server (Antwort auf Single-Click).</summary>
        private static bool RecvAsciiLabel(uint serial, ushort body, string text)
        {
            var b = new List<byte> { 0x1C, 0, 0 };
            UInt(b, serial);
            UShort(b, body);
            b.Add(0x06); // MessageType.Label
            UShort(b, 0x3B2); // hue
            UShort(b, 3); // font
            for (int i = 0; i < 30; i++) // name(30)
                b.Add(0);
            foreach (char c in text)
                b.Add((byte) c);
            b.Add(0);

            int len = b.Count;
            b[1] = (byte) (len >> 8);
            b[2] = (byte) len;
            return PacketHandler.OnServerPacket(0x1C, new PacketReader(b.ToArray(), true), null);
        }

        [Fact]
        public void ContainerLabels_ersetzen_das_Servernamen_Label()
        {
            Assistant.Core.ContainerLabels.Initialize();
            Assistant.Core.ContainerLabels.ClearAll();
            Config.SetProperty("ShowContainerLabels", true);
            Config.SetProperty("ContainerLabelFormat", "[{label}] ({type})");
            Config.SetProperty("ContainerLabelStyle", 1);

            Item chest = new Item(ChestSerial) { ItemID = 0x0E43, Position = new Point3D(101, 100, 0) };
            World.AddItem(chest);

            Assistant.Core.ContainerLabels.ContainerLabelList.Add(
                new Assistant.Core.ContainerLabels.ContainerLabel
                {
                    Id = "0x" + ChestSerial.ToString("X8"),
                    Type = "wooden chest",
                    Label = "Regs",
                    Hue = 88,
                    Alias = "wooden chest"
                });

            bool blocked = RecvAsciiLabel(ChestSerial, 0x0E43, "wooden chest");

            Assert.True(blocked, "Original-Label muss geblockt werden");
            byte[] msg = Assert.Single(m_Fake.InjectedToClient, pkt => pkt[0] == 0xAE);
            Assert.Equal(ChestSerial, PacketSerial(msg, 3));
            Assert.Contains("[Regs] (wooden chest)", UnicodeText(msg));

            Assistant.Core.ContainerLabels.ClearAll();
        }

        [Fact]
        public void ContainerLabels_aus_oder_fremde_Serial_bleiben_unangetastet()
        {
            Assistant.Core.ContainerLabels.Initialize();
            Assistant.Core.ContainerLabels.ClearAll();
            Config.SetProperty("ShowContainerLabels", true);

            Item chest = new Item(ChestSerial) { ItemID = 0x0E43, Position = new Point3D(101, 100, 0) };
            World.AddItem(chest);

            // Kein Label gepflegt -> nichts passiert.
            bool blocked = RecvAsciiLabel(ChestSerial, 0x0E43, "wooden chest");
            Assert.False(blocked);
            Assert.Empty(m_Fake.InjectedToClient);

            // Option aus -> ebenfalls nichts, selbst mit gepflegtem Label.
            Assistant.Core.ContainerLabels.ContainerLabelList.Add(
                new Assistant.Core.ContainerLabels.ContainerLabel
                {
                    Id = "0x" + ChestSerial.ToString("X8"),
                    Type = "wooden chest",
                    Label = "Regs",
                    Hue = 88,
                    Alias = "wooden chest"
                });
            Config.SetProperty("ShowContainerLabels", false);
            blocked = RecvAsciiLabel(ChestSerial, 0x0E43, "wooden chest");
            Assert.False(blocked);
            Assert.Empty(m_Fake.InjectedToClient);

            Assistant.Core.ContainerLabels.ClearAll();
        }

        // ------------------------------------------------------------ bandage timer

        [Fact]
        public void BandageTimer_zeigt_Start_und_Endmeldung()
        {
            Config.SetProperty("ShowBandageTimer", true);
            Config.SetProperty("ShowBandageTimerLocation", 0); // Overhead
            Config.SetProperty("ShowBandageStart", true);
            Config.SetProperty("BandageStartMessage", "Bandage: Starting");
            Config.SetProperty("ShowBandageEnd", true);
            Config.SetProperty("BandageEndMessage", "Bandage: Ending");
            Config.SetProperty("ShowBandageTimerHue", 88);

            Assistant.Core.BandageTimer.OnLocalizedMessage(500956); // start
            Assert.True(Assistant.Core.BandageTimer.Running);
            Assert.Contains(m_Fake.InjectedToClient,
                pkt => pkt[0] == 0xAE && UnicodeText(pkt).Contains("Bandage: Starting"));

            Assistant.Core.BandageTimer.OnLocalizedMessage(500969); // finish
            Assert.False(Assistant.Core.BandageTimer.Running);
            Assert.Contains(m_Fake.InjectedToClient,
                pkt => pkt[0] == 0xAE && UnicodeText(pkt).Contains("Bandage: Ending"));
        }

        [Fact]
        public void BandageTimer_tickt_die_Sekundenanzeige()
        {
            Config.SetProperty("ShowBandageTimer", true);
            Config.SetProperty("ShowBandageTimerLocation", 0);
            Config.SetProperty("ShowBandageStart", false);
            Config.SetProperty("ShowBandageEnd", false);
            Config.SetProperty("OnlyShowBandageTimerEvery", false);
            Config.SetProperty("ShowBandageTimerFormat", "Bandage: {count}s");

            Assistant.Core.BandageTimer.OnLocalizedMessage(500956);

            System.Threading.Thread.Sleep(1100);
            Timer.Slice();

            Assert.Contains(m_Fake.InjectedToClient,
                pkt => pkt[0] == 0xAE && UnicodeText(pkt).Contains("Bandage: 1s"));

            Assistant.Core.BandageTimer.Stop();
        }

        [Fact]
        public void BandageTimer_Anzeige_aus_bleibt_stumm()
        {
            Config.SetProperty("ShowBandageTimer", false);
            Config.SetProperty("ShowBandageStart", true);

            Assistant.Core.BandageTimer.OnLocalizedMessage(500956);
            Assert.True(Assistant.Core.BandageTimer.Running); // Kern laeuft (bandaging)
            Assert.Empty(m_Fake.InjectedToClient); // aber keine Anzeige

            Assistant.Core.BandageTimer.Stop();
        }

        // ------------------------------------------------------------ overhead manager

        [Fact]
        public void OverheadManager_zeigt_Trigger_Meldung_mit_Sound()
        {
            Assistant.Core.OverheadManager.Initialize();
            Assistant.Core.OverheadManager.ClearAll();
            Config.SetProperty("ShowOverheadMessages", true);
            Config.SetProperty("OverheadFormat", "[{msg}]");

            Assistant.Core.OverheadManager.OverheadMessages.Add(new Assistant.Core.OverheadMessage
            {
                SearchMessage = "committed a criminal act",
                MessageOverhead = "Criminal!",
                Hue = 38,
                Sound = 0x2A
            });

            // 0x1C-Systemmeldung (System-Typ) mit dem Trigger-Text.
            RecvSystemMessage("You've committed a criminal act!!");

            Assert.Contains(m_Fake.InjectedToClient,
                pkt => pkt[0] == 0xAE && UnicodeText(pkt).Contains("[Criminal!]"));
            Assert.Contains(m_Fake.InjectedToClient, pkt => pkt[0] == 0x54); // sound

            Assistant.Core.OverheadManager.ClearAll();
        }

        [Fact]
        public void OverheadManager_aus_oder_ohne_Treffer_bleibt_stumm()
        {
            Assistant.Core.OverheadManager.Initialize();
            Assistant.Core.OverheadManager.ClearAll();

            Assistant.Core.OverheadManager.OverheadMessages.Add(new Assistant.Core.OverheadMessage
            {
                SearchMessage = "poisoned",
                MessageOverhead = "Poison!",
                Hue = 38,
                Sound = -1
            });

            Config.SetProperty("ShowOverheadMessages", false);
            RecvSystemMessage("You have been poisoned!");
            Assert.Empty(m_Fake.InjectedToClient);

            Config.SetProperty("ShowOverheadMessages", true);
            RecvSystemMessage("The weather is nice today.");
            Assert.Empty(m_Fake.InjectedToClient);

            Assistant.Core.OverheadManager.ClearAll();
        }

        [Fact]
        public void OverheadManager_Profilsektion_roundtrip()
        {
            Assistant.Core.OverheadManager.Initialize();
            Assistant.Core.OverheadManager.ClearAll();

            Assistant.Core.OverheadManager.OverheadMessages.Add(new Assistant.Core.OverheadMessage
            {
                SearchMessage = "is attacking you",
                MessageOverhead = "Attacked!",
                Hue = 138,
                Sound = 0x1F
            });

            var sb = new StringBuilder();
            using (var writer = System.Xml.XmlWriter.Create(sb,
                new System.Xml.XmlWriterSettings { OmitXmlDeclaration = true, ConformanceLevel = System.Xml.ConformanceLevel.Fragment }))
            {
                writer.WriteStartElement("overheadmessages");
                Assistant.Core.OverheadManager.Save(writer);
                writer.WriteEndElement();
            }

            var doc = new System.Xml.XmlDocument();
            doc.LoadXml(sb.ToString());
            Assistant.Core.OverheadManager.Load(doc.DocumentElement);

            var m = Assert.Single(Assistant.Core.OverheadManager.OverheadMessages);
            Assert.Equal("is attacking you", m.SearchMessage);
            Assert.Equal("Attacked!", m.MessageOverhead);
            Assert.Equal(138, m.Hue);
            Assert.Equal(0x1F, m.Sound);

            Assistant.Core.OverheadManager.ClearAll();
        }

        /// <summary>0x1C mit MessageType.System (Serial -1, Name "System").</summary>
        private static void RecvSystemMessage(string text)
        {
            var b = new List<byte> { 0x1C, 0, 0 };
            UInt(b, 0xFFFFFFFF);
            UShort(b, 0xFFFF);
            b.Add(0x01); // MessageType.System
            UShort(b, 0x3B2);
            UShort(b, 3);
            for (int i = 0; i < 30; i++)
                b.Add(i < 6 ? (byte) "System"[i] : (byte) 0);
            foreach (char c in text)
                b.Add((byte) c);
            b.Add(0);

            int len = b.Count;
            b[1] = (byte) (len >> 8);
            b[2] = (byte) len;
            PacketHandler.OnServerPacket(0x1C, new PacketReader(b.ToArray(), true), null);
        }

        [Fact]
        public void ContainerLabels_Profilsektion_roundtrip()
        {
            Assistant.Core.ContainerLabels.Initialize();
            Assistant.Core.ContainerLabels.ClearAll();

            Assistant.Core.ContainerLabels.ContainerLabelList.Add(
                new Assistant.Core.ContainerLabels.ContainerLabel
                {
                    Id = "0x40001005",
                    Type = "metal chest",
                    Label = "Gems",
                    Hue = 55,
                    Alias = "shiny"
                });

            var sb = new StringBuilder();
            using (var writer = System.Xml.XmlWriter.Create(sb,
                new System.Xml.XmlWriterSettings { OmitXmlDeclaration = true, ConformanceLevel = System.Xml.ConformanceLevel.Fragment }))
            {
                writer.WriteStartElement("containerlabels");
                Assistant.Core.ContainerLabels.Save(writer);
                writer.WriteEndElement();
            }

            var doc = new System.Xml.XmlDocument();
            doc.LoadXml(sb.ToString());
            Assistant.Core.ContainerLabels.Load(doc.DocumentElement);

            var label = Assert.Single(Assistant.Core.ContainerLabels.ContainerLabelList);
            Assert.Equal("0x40001005", label.Id);
            Assert.Equal("Gems", label.Label);
            Assert.Equal(55, label.Hue);
            Assert.Equal("shiny", label.Alias);

            Assistant.Core.ContainerLabels.ClearAll();
        }
    }
}
