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

// UOSagas-Razor: Tests fuer AutoOpenDoors, ShowHealth und PotionEquip
// (dritter Options-Schwung). Gleiches Muster wie BehaviorFilterTests:
// synthetische Pakete + Fake-Services.

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
    public class QolFeatureTests : IDisposable
    {
        private const uint PlayerSerial = 0x00000D01;
        private const uint DoorSerial = 0x40000D02;
        private const uint PotionSerial = 0x40000D03;
        private const uint ShieldSerial = 0x40000D04;
        private const uint PackSerial = 0x40000D05;
        private const uint OtherMobile = 0x00000D06;

        private readonly string m_TempDir;
        private readonly CultureInfo m_OldCulture;
        private readonly FakeClientServices m_Fake;

        public QolFeatureTests()
        {
            m_OldCulture = CultureInfo.CurrentCulture;
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;

            m_TempDir = Path.Combine(Path.GetTempPath(), "RazorQolTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(m_TempDir);
            Config.Initialize(m_TempDir);
            Config.LoadLastProfile();

            PacketHandlers.Initialize();
            MacroManager.Stop();
            ActionQueue.Stop();
            Config.SetProperty("ObjectDelayEnabled", false);

            World.Clear();
            PlayerData player = new PlayerData(PlayerSerial)
            {
                Position = new Point3D(100, 100, 0),
                Visible = true,
                Direction = Direction.East
            };
            World.AddMobile(player);
            World.Player = player;

            Item pack = new Item(PackSerial) { ItemID = 0x0E75, Layer = Layer.Backpack };
            pack.Container = player.Serial;
            World.AddItem(pack);
            player.AddItem(pack);

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

        // ------------------------------------------------------------ doors

        private void PlaceDoor(int x, int y)
        {
            Item door = new Item(DoorSerial) { ItemID = 0x0675, Position = new Point3D(x, y, 0) };
            World.AddItem(door);
        }

        [Fact]
        public void AutoOpenDoors_oeffnet_Tuer_in_Blickrichtung()
        {
            Config.SetProperty("AutoOpenDoors", true);
            PlaceDoor(101, 100); // ein Feld oestlich, Blick East

            World.Player.AutoOpenDoors();

            byte[] sent = Assert.Single(m_Fake.SentToServer);
            Assert.Equal(0x12, sent[0]);
            Assert.Equal(0x58, sent[3]); // OpenDoor-Macro-Typ
        }

        [Fact]
        public void AutoOpenDoors_ignoriert_Tuer_hinter_dem_Spieler()
        {
            Config.SetProperty("AutoOpenDoors", true);
            PlaceDoor(99, 100); // westlich, Blick East

            World.Player.AutoOpenDoors();

            Assert.Empty(m_Fake.SentToServer);
        }

        [Fact]
        public void AutoOpenDoors_hidden_nur_mit_WhenHidden()
        {
            Config.SetProperty("AutoOpenDoors", true);
            PlaceDoor(101, 100);
            World.Player.Visible = false;

            Config.SetProperty("AutoOpenDoorWhenHidden", false);
            World.Player.AutoOpenDoors();
            Assert.Empty(m_Fake.SentToServer);

            Config.SetProperty("AutoOpenDoorWhenHidden", true);
            World.Player.AutoOpenDoors();
            Assert.Single(m_Fake.SentToServer);
        }

        [Fact]
        public void WalkRequest_aktualisiert_die_Richtung_und_prueft_bei_Drehung()
        {
            Config.SetProperty("AutoOpenDoors", true);
            PlaceDoor(100, 101); // suedlich

            // 0x02: dir(1) seq(1) fastwalk(4) — Drehung nach Sueden.
            byte[] walk = { 0x02, (byte) Direction.South, 0x01, 0, 0, 0, 0 };
            PacketHandler.OnClientPacket(0x02, new PacketReader(walk, false), null);

            Assert.Equal(Direction.South, World.Player.Direction & Direction.Mask);
            Assert.Contains(m_Fake.SentToServer, pkt => pkt[0] == 0x12);
        }

        // ------------------------------------------------------------ health

        private static void UInt(List<byte> b, uint v)
        {
            b.Add((byte)(v >> 24));
            b.Add((byte)(v >> 16));
            b.Add((byte)(v >> 8));
            b.Add((byte)v);
        }

        private static byte[] BuildHits(uint serial, ushort max, ushort cur)
        {
            var b = new List<byte> { 0xA1 };
            UInt(b, serial);
            b.Add((byte)(max >> 8)); b.Add((byte)max);
            b.Add((byte)(cur >> 8)); b.Add((byte)cur);
            return b.ToArray();
        }

        [Fact]
        public void ShowHealth_zeigt_Prozent_Overhead()
        {
            Config.SetProperty("ShowHealth", true);
            Config.SetProperty("HealthFmt", "[{0}%]");

            Mobile m = new Mobile(OtherMobile) { Position = new Point3D(102, 100, 0) };
            World.AddMobile(m);

            PacketHandler.OnServerPacket(0xA1, new PacketReader(BuildHits(OtherMobile, 100, 50), false), null);

            byte[] injected = Assert.Single(m_Fake.InjectedToClient);
            Assert.Equal(0xAE, injected[0]); // UnicodeMessage vom Mobile
            string text = Encoding.BigEndianUnicode.GetString(injected, 48, injected.Length - 50);
            Assert.Contains("[50%]", text);
        }

        [Fact]
        public void ShowHealth_schweigt_ohne_Aenderung_und_ausser_Sichtweite()
        {
            Config.SetProperty("ShowHealth", true);

            Mobile far = new Mobile(OtherMobile) { Position = new Point3D(150, 100, 0) };
            World.AddMobile(far);
            PacketHandler.OnServerPacket(0xA1, new PacketReader(BuildHits(OtherMobile, 100, 50), false), null);
            Assert.Empty(m_Fake.InjectedToClient); // > 12 Felder

            Config.SetProperty("ShowHealth", false);
            far.Position = new Point3D(102, 100, 0);
            PacketHandler.OnServerPacket(0xA1, new PacketReader(BuildHits(OtherMobile, 100, 25), false), null);
            Assert.Empty(m_Fake.InjectedToClient); // Option aus
        }

        // ------------------------------------------------------------ potions

        private void GiveHands(bool leftShield)
        {
            if (leftShield)
            {
                Item shield = new Item(ShieldSerial) { ItemID = 0x1B72, Layer = Layer.LeftHand };
                shield.Container = World.Player.Serial;
                World.AddItem(shield);
                World.Player.AddItem(shield);

                Item weapon = new Item(ShieldSerial + 10) { ItemID = 0x0F5E, Layer = Layer.RightHand };
                weapon.Container = World.Player.Serial;
                World.AddItem(weapon);
                World.Player.AddItem(weapon);
            }
        }

        private void GivePotion()
        {
            Item potion = new Item(PotionSerial) { ItemID = 0x0F0C }; // greater heal
            potion.Container = PackSerial;
            World.AddItem(potion);
        }

        [Fact]
        public void PotionEquip_macht_die_Hand_frei()
        {
            Config.SetProperty("PotionEquip", true);
            Config.SetProperty("PotionReequip", false);
            GiveHands(leftShield: true);
            GivePotion();

            PlayerData.DoubleClick((Serial) PotionSerial, true);

            // Schild-Lift (0x07 auf ShieldSerial) muss VOR dem DoubleClick liegen.
            Assert.Contains(m_Fake.SentToServer, pkt =>
                pkt[0] == 0x07 &&
                ((uint)((pkt[1] << 24) | (pkt[2] << 16) | (pkt[3] << 8) | pkt[4])) == ShieldSerial);
            Assert.Contains(m_Fake.SentToServer, pkt => pkt[0] == 0x06);
        }

        [Fact]
        public void PotionEquip_aus_laesst_die_Hand_in_Ruhe()
        {
            Config.SetProperty("PotionEquip", false);
            GiveHands(leftShield: true);
            GivePotion();

            PlayerData.DoubleClick((Serial) PotionSerial, true);

            Assert.DoesNotContain(m_Fake.SentToServer, pkt => pkt[0] == 0x07);
            Assert.Contains(m_Fake.SentToServer, pkt => pkt[0] == 0x06);
        }

        // ------------------------------------------------------------ emote sound

        /// <summary>0x1C AsciiSpeech mit MessageType.Emote von OtherMobile.</summary>
        private void RecvEmote(string text)
        {
            var b = new List<byte> { 0x1C, 0, 0 }; // Platz fuer Laenge
            UInt(b, OtherMobile);
            b.Add(0x01); b.Add(0x90); // body
            b.Add(0x02); // MessageType.Emote
            b.Add(0); b.Add(0x22); // hue
            b.Add(0); b.Add(3); // font
            for (int i = 0; i < 30; i++) // name(30)
                b.Add(i < 4 ? (byte) "anna"[i] : (byte) 0);
            foreach (char c in text)
                b.Add((byte) c);
            b.Add(0); // null-terminiert

            int len = b.Count;
            b[1] = (byte) (len >> 8);
            b[2] = (byte) len;
            PacketHandler.OnServerPacket(0x1C, new PacketReader(b.ToArray(), true), null);
        }

        private Mobile GiveEmoteMobile(bool female)
        {
            Mobile m = new Mobile(OtherMobile)
            {
                Name = "anna",
                Female = female,
                Position = new Point3D(101, 100, 0)
            };
            World.AddMobile(m);
            return m;
        }

        [Fact]
        public void PlayEmoteSound_spielt_den_passenden_Sound()
        {
            Config.SetProperty("PlayEmoteSound", true);
            GiveEmoteMobile(female: false);

            RecvEmote("*cough*");

            byte[] snd = Assert.Single(m_Fake.InjectedToClient, pkt => pkt[0] == 0x54);
            // 0x54: id(1) flags(1) sound(2) volume(2) x/y/z
            ushort id = (ushort) ((snd[2] << 8) | snd[3]);
            Assert.Equal((ushort) MaleSounds.Cough, id);
        }

        [Fact]
        public void PlayEmoteSound_nutzt_weibliche_Sounds()
        {
            Config.SetProperty("PlayEmoteSound", true);
            GiveEmoteMobile(female: true);

            RecvEmote("*giggle*");

            byte[] snd = Assert.Single(m_Fake.InjectedToClient, pkt => pkt[0] == 0x54);
            ushort id = (ushort) ((snd[2] << 8) | snd[3]);
            Assert.Equal((ushort) FemaleSounds.Giggle, id);
        }

        [Fact]
        public void PlayEmoteSound_ignoriert_unbekannte_Emotes_und_aus()
        {
            GiveEmoteMobile(female: false);

            Config.SetProperty("PlayEmoteSound", true);
            RecvEmote("*does a little dance*");
            Assert.DoesNotContain(m_Fake.InjectedToClient, pkt => pkt[0] == 0x54);

            Config.SetProperty("PlayEmoteSound", false);
            RecvEmote("*cough*");
            Assert.DoesNotContain(m_Fake.InjectedToClient, pkt => pkt[0] == 0x54);
        }
    }
}
