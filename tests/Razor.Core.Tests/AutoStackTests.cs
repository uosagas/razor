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

// UOSagas-Razor: Tests fuer den fuenften Options-Schwung, Teil 1 —
// AutoStack (Ressourcen am Boden stapeln) + AutoSearch (Container
// automatisch oeffnen) im Item.UpdateContainer-Pfad (CE 1:1).

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
    public class AutoStackTests : IDisposable
    {
        private const uint PlayerSerial = 0x00000F01;
        private const uint PackSerial = 0x40000F02;
        private const uint OreSerial = 0x40000F03;
        private const uint GroundStackSerial = 0x40000F04;
        private const uint BoxSerial = 0x40000F05;
        private const uint PouchSerial = 0x40000F06;

        private const ushort OreId = 0x19B7;

        private readonly string m_TempDir;
        private readonly CultureInfo m_OldCulture;
        private readonly FakeClientServices m_Fake;

        public AutoStackTests()
        {
            m_OldCulture = CultureInfo.CurrentCulture;
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;

            m_TempDir = Path.Combine(Path.GetTempPath(), "RazorAutoStackTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(m_TempDir);
            Config.Initialize(m_TempDir);
            Config.LoadLastProfile();

            PacketHandlers.Initialize();
            MacroManager.Stop();
            ActionQueue.Stop();
            PacketHandlers.IgnoreGumps.Clear();
            Config.SetProperty("ObjectDelayEnabled", false);
            Config.SetProperty("AutoSearch", false);

            World.Clear();
            PlayerData player = new PlayerData(PlayerSerial)
            {
                Position = new Point3D(100, 100, 0),
                Visible = true
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
            ActionQueue.Stop();
            PacketHandlers.IgnoreGumps.Clear();
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

        /// <summary>0x25 ContainerContentUpdate: legt ein NEUES Item in den Backpack
        /// (setzt IsNew + AutoStack wie der echte Handler).</summary>
        private static void RecvContainerUpdate(uint serial, ushort itemId, ushort amount, ushort hue = 0)
        {
            var b = new List<byte> { 0x25, 0, 0 }; // Platz fuer Laenge
            void UShort(ushort v)
            {
                b.Add((byte) (v >> 8));
                b.Add((byte) v);
            }

            void UInt(uint v)
            {
                b.Add((byte) (v >> 24));
                b.Add((byte) (v >> 16));
                b.Add((byte) (v >> 8));
                b.Add((byte) v);
            }

            UInt(serial);
            UShort(itemId);
            b.Add(0); // itemID offset (sbyte)
            UShort(amount);
            UShort(30); // x im Container
            UShort(40); // y im Container
            b.Add(0); // grid (UsePostKRPackets)
            UInt(PackSerial);
            UShort(hue);
            UShort(0); // Sagas v2.35+: 2-Byte Extended Flags

            int len = b.Count;
            b[1] = (byte) (len >> 8);
            b[2] = (byte) len;

            PacketHandler.OnServerPacket(0x25, new PacketReader(b.ToArray(), true), null);
        }

        private static uint ReadDropDest(byte[] drop)
        {
            // 0x08 PostKR: id(1) serial(4) x(2) y(2) z(1) grid(1) dest(4)
            return (uint) ((drop[11] << 24) | (drop[12] << 16) | (drop[13] << 8) | drop[14]);
        }

        // ------------------------------------------------------------ autostack

        [Fact]
        public void AutoStack_droppt_neue_Ressource_auf_die_Spielerposition()
        {
            Config.SetProperty("AutoStack", true);

            RecvContainerUpdate(OreSerial, OreId, 5);

            byte[] lift = m_Fake.SentToServer.FirstOrDefault(p => p[0] == 0x07);
            byte[] drop = m_Fake.SentToServer.FirstOrDefault(p => p[0] == 0x08);
            Assert.NotNull(lift);
            Assert.NotNull(drop);
            Assert.Equal(0xFFFFFFFF, ReadDropDest(drop)); // ground
        }

        [Fact]
        public void AutoStack_stapelt_auf_gleichen_Bodenstapel_in_Reichweite()
        {
            Config.SetProperty("AutoStack", true);

            Item stack = new Item(GroundStackSerial)
            {
                ItemID = OreId,
                Amount = 20,
                Position = new Point3D(101, 100, 0) // 1 Feld entfernt
            };
            World.AddItem(stack);

            RecvContainerUpdate(OreSerial, OreId, 5);

            byte[] drop = m_Fake.SentToServer.FirstOrDefault(p => p[0] == 0x08);
            Assert.NotNull(drop);
            Assert.Equal(GroundStackSerial, ReadDropDest(drop));
        }

        [Fact]
        public void AutoStack_ignoriert_Bodenstapel_mit_anderem_Hue()
        {
            Config.SetProperty("AutoStack", true);

            Item stack = new Item(GroundStackSerial)
            {
                ItemID = OreId,
                Amount = 20,
                Hue = 0x044E, // andersfarbiges Erz
                Position = new Point3D(101, 100, 0)
            };
            World.AddItem(stack);

            RecvContainerUpdate(OreSerial, OreId, 5); // hue 0

            byte[] drop = m_Fake.SentToServer.FirstOrDefault(p => p[0] == 0x08);
            Assert.NotNull(drop);
            Assert.Equal(0xFFFFFFFF, ReadDropDest(drop)); // ground statt Stapel
        }

        [Fact]
        public void AutoStack_aus_tut_nichts()
        {
            Config.SetProperty("AutoStack", false);

            RecvContainerUpdate(OreSerial, OreId, 5);

            Assert.Empty(m_Fake.SentToServer);
        }

        [Fact]
        public void AutoStack_greift_nur_bei_Ressourcen()
        {
            Config.SetProperty("AutoStack", true);

            RecvContainerUpdate(OreSerial, 0x0F06, 1); // Potion, keine Ressource

            Assert.Empty(m_Fake.SentToServer);
        }

        // ------------------------------------------------------------ autosearch

        [Fact]
        public void AutoSearch_oeffnet_neuen_Container_im_Backpack()
        {
            Config.SetProperty("AutoSearch", true);
            Config.SetProperty("ObjectDelayEnabled", false);

            RecvContainerUpdate(BoxSerial, 0x0E75, 1); // Kiste

            Assert.Contains(m_Fake.SentToServer, p => p[0] == 0x06); // DoubleClick
            Assert.Contains(PacketHandlers.IgnoreGumps, i => i.Serial == (Serial) BoxSerial);
        }

        [Fact]
        public void AutoSearch_laesst_Pouches_zu_wenn_NoSearchPouches_aus()
        {
            Config.SetProperty("AutoSearch", true);

            Config.SetProperty("NoSearchPouches", true);
            RecvContainerUpdate(PouchSerial, 0x0E79, 1); // Pouch
            Assert.DoesNotContain(m_Fake.SentToServer, p => p[0] == 0x06);

            Config.SetProperty("NoSearchPouches", false);
            RecvContainerUpdate(0x40000F07, 0x0E79, 1); // zweiter Pouch
            Assert.Contains(m_Fake.SentToServer, p => p[0] == 0x06);
        }

        [Fact]
        public void AutoSearch_aus_oeffnet_nichts()
        {
            Config.SetProperty("AutoSearch", false);

            RecvContainerUpdate(BoxSerial, 0x0E75, 1);

            Assert.DoesNotContain(m_Fake.SentToServer, p => p[0] == 0x06);
        }
    }
}
