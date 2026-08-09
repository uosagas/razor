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

// UOSagas-Razor: Tests fuer die Verhaltens-Filter (BlockDismount,
// BlockTradeRequests, BlockPartyInvites, AutoOpenCorpses/-Twice).
// Synthetische Pakete durch den echten PacketHandler-Dispatch; die
// Fake-Services zeichnen SendToServer/InjectToClient auf.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Assistant;
using Assistant.Macros;
using Xunit;

namespace Razor.Core.Tests
{
    [Collection("ConfigSequential")]
    public class BehaviorFilterTests : IDisposable
    {
        private const uint PlayerSerial = 0x00000A01;
        private const uint MountSerial = 0x40000A02;
        private const uint CorpseSerial = 0x40000A03;

        private readonly string m_TempDir;
        private readonly CultureInfo m_OldCulture;
        private readonly FakeClientServices m_Fake;

        public BehaviorFilterTests()
        {
            m_OldCulture = CultureInfo.CurrentCulture;
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;

            m_TempDir = Path.Combine(Path.GetTempPath(), "RazorBehaviorTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(m_TempDir);
            Config.Initialize(m_TempDir);
            Config.LoadLastProfile();

            PacketHandlers.Initialize();
            MacroManager.Stop();
            ActionQueue.Stop();

            // ObjectDelay aus: der Delay-Zeitstempel ist statisch — mit Delay
            // wuerde ein DoubleClick je nach Testreihenfolge nur gequeued
            // statt gesendet (Pollution durch vorherige Tests).
            Config.SetProperty("ObjectDelayEnabled", false);

            World.Clear();
            PlayerData player = new PlayerData(PlayerSerial)
            {
                Position = new Point3D(100, 100, 0),
                Visible = true
            };
            World.AddMobile(player);
            World.Player = player;

            m_Fake = new FakeClientServices();
            ClientProxy.Bind(m_Fake);
        }

        public void Dispose()
        {
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

        private static void UShort(List<byte> b, ushort v)
        {
            b.Add((byte)(v >> 8));
            b.Add((byte)v);
        }

        private static void UInt(List<byte> b, uint v)
        {
            b.Add((byte)(v >> 24));
            b.Add((byte)(v >> 16));
            b.Add((byte)(v >> 8));
            b.Add((byte)v);
        }

        // ------------------------------------------------------------ dismount

        private void GiveMount()
        {
            Item mount = new Item(MountSerial) { ItemID = 0x3E9F, Layer = Layer.Mount };
            mount.Container = World.Player.Serial;
            World.AddItem(mount);
            World.Player.AddItem(mount);
        }

        private static bool SendDoubleClick(uint serial)
        {
            var b = new List<byte> { 0x06 };
            UInt(b, serial);
            return PacketHandler.OnClientPacket(0x06, new PacketReader(b.ToArray(), false), null);
        }

        [Fact]
        public void BlockDismount_blockt_im_Warmode_mit_Mount()
        {
            Config.SetProperty("BlockDismount", true);
            GiveMount();
            World.Player.Warmode = true;

            Assert.True(SendDoubleClick(PlayerSerial), "Dismount muss geblockt werden");
        }

        [Fact]
        public void BlockDismount_erlaubt_ohne_Warmode_oder_Mount()
        {
            Config.SetProperty("BlockDismount", true);
            GiveMount();
            World.Player.Warmode = false;

            Assert.False(SendDoubleClick(PlayerSerial), "Ohne Warmode kein Block");

            World.Player.Warmode = true;
            Item mount = World.FindItem(MountSerial);
            World.Player.RemoveItem(mount);
            mount.Remove();

            Assert.False(SendDoubleClick(PlayerSerial), "Ohne Mount kein Block");
        }

        [Fact]
        public void BlockDismount_aus_blockt_nie()
        {
            Config.SetProperty("BlockDismount", false);
            GiveMount();
            World.Player.Warmode = true;

            Assert.False(SendDoubleClick(PlayerSerial));
        }

        // ------------------------------------------------------------ trade

        [Fact]
        public void BlockTradeRequests_verwirft_das_Handelsfenster()
        {
            // 0x6F: action(1) serial1(4) serial2(4) serial3(4) nameLen... (Inhalt egal, Viewer liest nichts)
            var b = new List<byte> { 0x6F, 0, 17, 0x00 };
            UInt(b, 0x40001111);
            UInt(b, 0x40002222);
            UInt(b, 0x40003333);
            byte[] packet = b.ToArray();

            Config.SetProperty("BlockTradeRequests", true);
            Assert.True(PacketHandler.OnServerPacket(0x6F, new PacketReader(packet, true), null));

            Config.SetProperty("BlockTradeRequests", false);
            Assert.False(PacketHandler.OnServerPacket(0x6F, new PacketReader(packet, true), null));
        }

        // ------------------------------------------------------------ party

        private static byte[] BuildPartyInvite(uint leader)
        {
            var b = new List<byte> { 0xBF, 0, 0 };
            UShort(b, 0x0006); // party command
            b.Add(0x07);       // invite
            UInt(b, leader);
            b[1] = (byte)(b.Count >> 8);
            b[2] = (byte)b.Count;
            return b.ToArray();
        }

        [Fact]
        public void BlockPartyInvites_lehnt_sofort_ab()
        {
            Config.SetProperty("BlockPartyInvites", true);

            PacketHandler.OnServerPacket(0xBF, new PacketReader(BuildPartyInvite(0x00000B01), true), null);

            byte[] sent = Assert.Single(m_Fake.SentToServer);
            Assert.Equal(0xBF, sent[0]);
            // 0xBF len(2) sub(2)=0x0006 partyCmd(1)=0x09 decline leader(4)
            Assert.Equal(0x06, sent[4]);
            Assert.Equal(0x09, sent[5]);
            Assert.Equal(0x0B, sent[8]);
            Assert.Equal(0x01, sent[9]);
        }

        [Fact]
        public void Ohne_BlockPartyInvites_keine_Antwort()
        {
            Config.SetProperty("BlockPartyInvites", false);

            PacketHandler.OnServerPacket(0xBF, new PacketReader(BuildPartyInvite(0x00000B01), true), null);

            Assert.Empty(m_Fake.SentToServer);
        }

        // ------------------------------------------------------------ corpses

        /// <summary>0x1A WorldItem (dynamisch): serial|0x80000000 fuer Amount, itemid, amount, x, y, z.</summary>
        private static byte[] BuildWorldItem(uint serial, ushort itemId, ushort x, ushort y)
        {
            var b = new List<byte> { 0x1A, 0, 0 };
            UInt(b, serial | 0x80000000); // Amount folgt
            UShort(b, itemId);
            UShort(b, 1); // amount
            UShort(b, x);
            UShort(b, y);
            b.Add(0); // z
            b[1] = (byte)(b.Count >> 8);
            b[2] = (byte)b.Count;
            return b.ToArray();
        }

        private void RecvCorpse(uint serial)
        {
            // 0x2006 = Corpse-ItemID; Position neben dem Spieler.
            PacketHandler.OnServerPacket(0x1A,
                new PacketReader(BuildWorldItem(serial, 0x2006, 101, 100), true), null);
        }

        [Fact]
        public void AutoOpenCorpses_oeffnet_neue_Leiche_in_Reichweite()
        {
            Config.SetProperty("AutoOpenCorpses", true);
            Config.SetProperty("BlockOpenCorpsesTwice", false);
            Config.SetProperty("CorpseRange", 3);

            RecvCorpse(CorpseSerial);

            // PlayerData.DoubleClick laeuft ueber die ActionQueue/direkt -> am
            // Ende geht ein 0x06 DoubleClick an den Server.
            Assert.Contains(m_Fake.SentToServer, pkt => pkt[0] == 0x06);
        }

        [Fact]
        public void AutoOpenCorpses_respektiert_die_Reichweite()
        {
            Config.SetProperty("AutoOpenCorpses", true);
            Config.SetProperty("CorpseRange", 3);

            PacketHandler.OnServerPacket(0x1A,
                new PacketReader(BuildWorldItem(CorpseSerial, 0x2006, 150, 100), true), null);

            Assert.DoesNotContain(m_Fake.SentToServer, pkt => pkt[0] == 0x06);
        }

        [Fact]
        public void AutoOpenCorpses_oeffnet_beim_Heranlaufen()
        {
            // Der Live-Fall (Fernkampf): Leiche faellt AUSSERHALB der Range —
            // beim Paket-Eintreffen passiert nichts. Erst der Bewegungs-Sweep
            // (OnPlayerPositionChanged -> CheckAutoOpenCorpses) oeffnet sie.
            Config.SetProperty("AutoOpenCorpses", true);
            Config.SetProperty("CorpseRange", 2);

            PacketHandler.OnServerPacket(0x1A,
                new PacketReader(BuildWorldItem(CorpseSerial, 0x2006, 101, 104), true), null); // 4 Felder weg

            Assert.DoesNotContain(m_Fake.SentToServer, pkt => pkt[0] == 0x06);

            // Spieler laeuft zur Leiche.
            World.Player.Position = new Point3D(101, 103, 0);
            PacketHandlers.CheckAutoOpenCorpses();

            Assert.Contains(m_Fake.SentToServer, pkt => pkt[0] == 0x06);

            // Weiterer Schritt daneben darf NICHT erneut oeffnen (Sweep-Dedup).
            int opens = m_Fake.SentToServer.Count(pkt => pkt[0] == 0x06);
            World.Player.Position = new Point3D(101, 104, 0);
            PacketHandlers.CheckAutoOpenCorpses();
            Assert.Equal(opens, m_Fake.SentToServer.Count(pkt => pkt[0] == 0x06));
        }

        // Diese zwei Tests pruefen den Dedup-Satz OpenedCorpses direkt (das ist
        // genau, was BlockOpenCorpsesTwice steuert). Das erneute 0x06-Senden
        // haengt zusaetzlich am ActionQueue-m_Last-Guard, der erst beim Timer-
        // Leerlauf faellt — das ist ein separater, schon getesteter Belang und
        // wuerde den Test nur zeitabhaengig machen.

        [Fact]
        public void OhneTwiceBlock_gibt_Leiche_ausser_Reichweite_frei()
        {
            Config.SetProperty("AutoOpenCorpses", true);
            Config.SetProperty("BlockOpenCorpsesTwice", false);
            Config.SetProperty("CorpseRange", 2);

            RecvCorpse(CorpseSerial); // neben dem Spieler -> geoeffnet + gemerkt
            Assert.Contains(CorpseSerial, World.Player.OpenedCorpses);

            // Weggehen: Leiche verlaesst die Range -> Merker wird freigegeben.
            // Nur wenige Felder weg (ausserhalb CorpseRange 2, aber INNERHALB
            // der Sichtweite ~18) — sonst pruned OnPositionChanging die Leiche
            // ganz aus dem Weltmodell (wie live beim Weit-Weggehen).
            World.Player.Position = new Point3D(106, 100, 0);
            PacketHandlers.CheckAutoOpenCorpses();
            Assert.DoesNotContain(CorpseSerial, World.Player.OpenedCorpses);

            // Zurueckkommen: wieder in Range, nicht mehr gemerkt -> neu gemerkt
            // (und ein Oeffnen wird angestossen).
            World.Player.Position = new Point3D(101, 100, 0);
            PacketHandlers.CheckAutoOpenCorpses();
            Assert.Contains(CorpseSerial, World.Player.OpenedCorpses);
        }

        [Fact]
        public void MitTwiceBlock_behaelt_den_Merker()
        {
            Config.SetProperty("AutoOpenCorpses", true);
            Config.SetProperty("BlockOpenCorpsesTwice", true);
            Config.SetProperty("CorpseRange", 2);

            RecvCorpse(CorpseSerial);
            Assert.Contains(CorpseSerial, World.Player.OpenedCorpses);

            // Weggehen (in Sichtweite) darf den Merker NICHT freigeben.
            World.Player.Position = new Point3D(106, 100, 0);
            PacketHandlers.CheckAutoOpenCorpses();
            Assert.Contains(CorpseSerial, World.Player.OpenedCorpses);
        }

        [Fact]
        public void BlockOpenCorpsesTwice_oeffnet_nur_einmal()
        {
            Config.SetProperty("AutoOpenCorpses", true);
            Config.SetProperty("BlockOpenCorpsesTwice", true);
            Config.SetProperty("CorpseRange", 3);

            RecvCorpse(CorpseSerial);
            int afterFirst = m_Fake.SentToServer.Count(pkt => pkt[0] == 0x06);

            // Leiche verschwindet aus der Welt (z. B. ausser Sicht) und kommt wieder.
            World.FindItem(CorpseSerial)?.Remove();
            RecvCorpse(CorpseSerial);

            Assert.Equal(1, afterFirst);
            Assert.Equal(1, m_Fake.SentToServer.Count(pkt => pkt[0] == 0x06));
        }

        [Fact]
        public void PacketBatch_0xF7_packt_Corpse_aus()
        {
            // Der Shard buendelt World-Items in 0xF7 (count + je id(1)=0xF3 + Rumpf).
            // Ohne Auspacken sieht der Port die Leiche nie -> kein AutoOpen.
            Config.SetProperty("AutoOpenCorpses", true);
            Config.SetProperty("BlockOpenCorpsesTwice", false);
            Config.SetProperty("CorpseRange", 3);

            // Ein 0xF3-Rumpf (OHNE fuehrende Paket-ID/Laenge) wie im Batch:
            // cmd(2)=1 artData(1) serial(4) itemid(2) dir(1) amount(2) amount(2)
            // x(2) y(2) z(1) light(1) hue(2) flags(2) unk2(2)
            var body = new List<byte>();
            UShort(body, 0x0001);
            body.Add(0x00);
            UInt(body, CorpseSerial);
            UShort(body, 0x2006); // corpse
            body.Add(0);          // dir
            UShort(body, 1);
            UShort(body, 1);
            UShort(body, 101);    // x neben dem Spieler
            UShort(body, 100);    // y
            body.Add(0);          // z
            body.Add(0);          // light
            UShort(body, 0);      // hue
            UShort(body, 0);      // sagas flags
            UShort(body, 0);      // unk2

            var batch = new List<byte> { 0xF7, 0, 0 };
            UShort(batch, 1);     // count
            batch.Add(0xF3);      // sub-id
            batch.AddRange(body);
            batch[1] = (byte)(batch.Count >> 8);
            batch[2] = (byte)batch.Count;

            PacketHandler.OnServerPacket(0xF7, new PacketReader(batch.ToArray(), true), null);

            Assert.NotNull(World.FindItem(CorpseSerial));
            Assert.Contains(m_Fake.SentToServer, pkt => pkt[0] == 0x06);
        }

        [Fact]
        public void PacketBatch_0xF7_liest_mehrere_Eintraege_ohne_Desync()
        {
            // Regression: ein CE-Erbe (Post-HS-Extra-Word) las pro F3-Rumpf
            // 2 Bytes zu viel — der zweite Batch-Eintrag desyncte. Der Client
            // liest nach unk2 nichts (D13).
            Config.SetProperty("AutoOpenCorpses", false);
            Config.SetProperty("ShowCorpseNames", false);

            const uint ItemA = 0x40000C31;
            const uint ItemB = 0x40000C32;

            List<byte> F3Body(uint serial, ushort itemId, ushort x)
            {
                var body = new List<byte>();
                UShort(body, 0x0001);
                body.Add(0x00);
                UInt(body, serial);
                UShort(body, itemId);
                body.Add(0); // dir
                UShort(body, 1);
                UShort(body, 1);
                UShort(body, x);
                UShort(body, 100);
                body.Add(0); // z
                body.Add(0); // light
                UShort(body, 0x0123); // hue
                UShort(body, 0); // sagas flags
                UShort(body, 0); // unk2
                return body;
            }

            var batch = new List<byte> { 0xF7, 0, 0 };
            UShort(batch, 2); // count
            batch.Add(0xF3);
            batch.AddRange(F3Body(ItemA, 0x0EED, 101));
            batch.Add(0xF3);
            batch.AddRange(F3Body(ItemB, 0x0F3F, 102));
            batch[1] = (byte) (batch.Count >> 8);
            batch[2] = (byte) batch.Count;

            PacketHandler.OnServerPacket(0xF7, new PacketReader(batch.ToArray(), true), null);

            Item a = World.FindItem(ItemA);
            Item b = World.FindItem(ItemB);
            Assert.NotNull(a);
            Assert.NotNull(b);
            Assert.Equal((ushort) 0x0F3F, (ushort) b.ItemID); // 2. Eintrag korrekt gelesen
            Assert.Equal(102, b.Position.X);
        }

        [Fact]
        public void ShowCorpseNames_single_clickt_die_Leiche()
        {
            Config.SetProperty("ShowCorpseNames", true);
            Config.SetProperty("AutoOpenCorpses", false);

            RecvCorpse(CorpseSerial);

            // SingleClick = 0x09
            Assert.Contains(m_Fake.SentToServer, pkt => pkt[0] == 0x09);
        }
    }
}
