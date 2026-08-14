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

// UOSagas-Razor: Tests fuer Phase 2b — Weltmodell + Paket-Parsing.
// Synthetische Paket-Fixtures, Byte-Layout aus den portierten Handlern
// (Razor CE, Razor/Network/Handlers.cs) abgeleitet. Alle Werte big-endian.

using System;
using System.Collections.Generic;
using Assistant;
using Xunit;

namespace Razor.Core.Tests
{
    public class WorldPacketTests
    {
        private const uint PlayerSerial = 0x00000101;
        private const uint MobSerial = 0x00000202;

        public WorldPacketTests()
        {
            PacketHandlers.Initialize(); // idempotent
            World.Clear();
            Engine.UsePostKRPackets = true;
            Engine.UsePostSAChanges = true;
            Engine.UseNewMobileIncoming = true;
            Engine.UsePostHSChanges = true;
        }

        // ---- Helpers -------------------------------------------------------

        /// <summary>Big-endian-Bytewriter fuer Paket-Fixtures.</summary>
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

            public ByteWriter Short(short v)
            {
                return UShort((ushort) v);
            }

            public ByteWriter UInt(uint v)
            {
                _bytes.Add((byte) (v >> 24));
                _bytes.Add((byte) (v >> 16));
                _bytes.Add((byte) (v >> 8));
                _bytes.Add((byte) v);
                return this;
            }

            public ByteWriter Pad(int count)
            {
                for (int i = 0; i < count; i++)
                    _bytes.Add(0);
                return this;
            }

            public byte[] ToArray()
            {
                return _bytes.ToArray();
            }

            /// <summary>Fuegt bei dynamischen Paketen das 2-Byte-Laengenfeld an Position 1 ein.</summary>
            public byte[] ToDynamicArray()
            {
                int len = _bytes.Count + 2;
                _bytes.Insert(1, (byte) (len >> 8));
                _bytes.Insert(2, (byte) len);
                return _bytes.ToArray();
            }
        }

        private static void Recv(byte[] data, bool dynamicLength)
        {
            PacketHandler.OnServerPacket(data[0], new PacketReader(data, dynamicLength), null);
        }

        /// <summary>Wartet real und feuert dann die Timer (Item-Remove-Delay etc.).</summary>
        private static void SliceAfter(TimeSpan delay)
        {
            System.Threading.Thread.Sleep(delay);
            Timer.Slice();
        }

        /// <summary>0x1B LoginConfirm (fixe Laenge 37).</summary>
        private static void SendLoginConfirm(
            uint serial = PlayerSerial, ushort body = 0x0190,
            ushort x = 1000, ushort y = 1200, short z = 5, byte dir = 2)
        {
            byte[] packet = new ByteWriter()
                .Byte(0x1B)
                .UInt(serial)
                .UInt(0) // always 0
                .UShort(body)
                .UShort(x)
                .UShort(y)
                .Short(z)
                .Byte(dir)
                .Pad(37 - 18) // Rest des 37-Byte-Pakets
                .ToArray();

            Recv(packet, dynamicLength: false);
        }

        /// <summary>0x1A WorldItem (dynamische Laenge, kompakteste Variante ohne Flag-Bits).</summary>
        private static void SendWorldItem(uint serial, ushort itemId, ushort x, ushort y, sbyte z)
        {
            byte[] packet = new ByteWriter()
                .Byte(0x1A)
                .UInt(serial) // ohne 0x80000000 -> Amount = 1
                .UShort(itemId) // ohne 0x8000 -> kein ItemID-Offset
                .UShort(x) // ohne 0x8000 -> keine Direction
                .UShort(y) // ohne 0x8000/0x4000 -> keine Hue/Flags
                .SByte(z)
                .ToDynamicArray();

            Recv(packet, dynamicLength: true);
        }

        // ---- 0x1B LoginConfirm ---------------------------------------------

        [Fact]
        public void LoginConfirm_erzeugt_Player_mit_Serial_und_Position()
        {
            SendLoginConfirm();

            Assert.NotNull(World.Player);
            Assert.Equal(PlayerSerial, World.Player.Serial.Value);
            Assert.Equal(new Point3D(1000, 1200, 5), World.Player.Position);
            Assert.Equal(0x0190, World.Player.Body);
            Assert.Equal(Direction.East, World.Player.Direction);

            // Der Player steht auch im Mobile-Verzeichnis.
            Assert.Same(World.Player, World.FindMobile(PlayerSerial));
        }

        // ---- 0x78 MobileIncoming --------------------------------------------

        [Fact]
        public void MobileIncoming_mit_zwei_EquipItems_fuellt_Layer()
        {
            SendLoginConfirm();

            const uint weaponSerial = 0x40000001;
            const uint packSerial = 0x40000002;

            byte[] packet = new ByteWriter()
                .Byte(0x78)
                .UInt(MobSerial)
                .UShort(0x0190) // body
                .UShort(1001).UShort(1201).SByte(5) // Position (in Sichtweite)
                .Byte(1) // direction
                .UShort(0) // hue
                .UShort(0) // flags (Sagas: 2 Byte Extended)
                .Byte(1) // notoriety
                // Equip-Liste (UseNewMobileIncoming: Hue immer vorhanden)
                .UInt(weaponSerial).UShort(0x13B9).Byte((byte) Layer.RightHand).UShort(0)
                .UInt(packSerial).UShort(0x0E75).Byte((byte) Layer.Backpack).UShort(0x0021)
                .UInt(0) // Terminator
                .ToDynamicArray();

            Recv(packet, dynamicLength: true);

            Mobile m = World.FindMobile(MobSerial);
            Assert.NotNull(m);
            Assert.Equal(new Point3D(1001, 1201, 5), m.Position);
            Assert.Equal(0x0190, m.Body);
            Assert.Equal(1, m.Notoriety);

            Item weapon = m.GetItemOnLayer(Layer.RightHand);
            Assert.NotNull(weapon);
            Assert.Equal(weaponSerial, weapon.Serial.Value);
            Assert.Equal((ushort) 0x13B9, weapon.ItemID.Value);

            Item pack = m.GetItemOnLayer(Layer.Backpack);
            Assert.NotNull(pack);
            Assert.Equal(packSerial, pack.Serial.Value);
            Assert.Equal((ushort) 0x0021, pack.Hue);
            Assert.Same(pack, m.Backpack);

            // Container-Hierarchie: Equip haengt am Mobile.
            Assert.Same(m, weapon.Container);
            Assert.Same(m, weapon.RootContainer);
        }

        // ---- 0x78 fuer den EIGENEN Spieler (Grundlage von "Add Current") -------

        [Fact]
        public void MobileIncoming_fuer_Player_fuellt_getragene_Ausruestung()
        {
            SendLoginConfirm();

            const uint weaponSerial = 0x40000101;
            const uint ringSerial = 0x40000102;
            const uint packSerial = 0x40000103;

            // 0x78 mit der Player-Serial: Waffe (RightHand), Ring, Backpack.
            byte[] packet = new ByteWriter()
                .Byte(0x78)
                .UInt(PlayerSerial)
                .UShort(0x0190) // body
                .UShort(1000).UShort(1200).SByte(5) // Position (fuer Player ignoriert)
                .Byte(2) // direction
                .UShort(0) // hue
                .UShort(0) // flags (Sagas: 2 Byte Extended)
                .Byte(1) // notoriety
                .UInt(weaponSerial).UShort(0x13B9).Byte((byte) Layer.RightHand).UShort(0)
                .UInt(ringSerial).UShort(0x108A).Byte((byte) Layer.Ring).UShort(0)
                .UInt(packSerial).UShort(0x0E75).Byte((byte) Layer.Backpack).UShort(0)
                .UInt(0) // Terminator
                .ToDynamicArray();

            Recv(packet, dynamicLength: true);

            // GetItemOnLayer liefert die getragenen Items (Kern von "Add Current").
            Item weapon = World.Player.GetItemOnLayer(Layer.RightHand);
            Item ring = World.Player.GetItemOnLayer(Layer.Ring);
            Assert.NotNull(weapon);
            Assert.Equal(weaponSerial, weapon.Serial.Value);
            Assert.NotNull(ring);
            Assert.Equal(ringSerial, ring.Serial.Value);
            Assert.Same(World.Player.Backpack, World.Player.GetItemOnLayer(Layer.Backpack));

            // "Add Current"-Filter (werktreu zu DressTab.OnAddCurrent / CE dressUseCur):
            // ueber Contains, Ausruestungs-Layer, ohne Backpack/Hair/FacialHair.
            var collected = new List<uint>();
            foreach (Item item in new List<Item>(World.Player.Contains))
            {
                Layer layer = item.Layer;
                if (layer <= Layer.Invalid || layer > Layer.LastUserValid ||
                    layer == Layer.Backpack || layer == Layer.Hair || layer == Layer.FacialHair)
                    continue;
                collected.Add(item.Serial.Value);
            }

            Assert.Contains(weaponSerial, collected);
            Assert.Contains(ringSerial, collected);
            Assert.DoesNotContain(packSerial, collected); // Backpack wird ausgeschlossen
            Assert.Equal(2, collected.Count);
        }

        // ---- 0x1A WorldItem --------------------------------------------------

        [Fact]
        public void WorldItem_erzeugt_Item_mit_Position()
        {
            SendLoginConfirm();

            const uint itemSerial = 0x40000010;
            SendWorldItem(itemSerial, 0x0EED, 1005, 1210, -5);

            Item item = World.FindItem(itemSerial);
            Assert.NotNull(item);
            Assert.Equal(new Point3D(1005, 1210, -5), item.Position);
            Assert.Equal((ushort) 0x0EED, item.ItemID.Value);
            Assert.Equal(1, item.Amount);
            Assert.True(item.OnGround);
            Assert.True(item.Visible);
        }

        // ---- 0x3C ContainerContent -------------------------------------------

        [Fact]
        public void ContainerContent_mit_drei_Items_setzt_Contains_und_RootContainer()
        {
            SendLoginConfirm();

            // Container als Backpack am Player anlegen (0x2E), damit die
            // Hierarchie Kind -> Backpack -> Player komplett ist.
            const uint contSerial = 0x40000020;
            byte[] equip = new ByteWriter()
                .Byte(0x2E)
                .UInt(contSerial)
                .UShort(0x0E75) // itemID (Backpack)
                .SByte(0)
                .Byte((byte) Layer.Backpack)
                .UInt(PlayerSerial)
                .UShort(0)
                .ToArray();
            Recv(equip, dynamicLength: false);

            uint[] childSerials = {0x40000021, 0x40000022, 0x40000023};

            ByteWriter w = new ByteWriter()
                .Byte(0x3C)
                .UShort((ushort) childSerials.Length);

            for (int i = 0; i < childSerials.Length; i++)
            {
                w.UInt(childSerials[i])
                    .UShort(0x0EED) // itemID
                    .SByte(0) // itemID offset
                    .UShort((ushort) (5 + i)) // amount
                    .UShort((ushort) (10 + i)) // x (Containerkoordinate)
                    .UShort((ushort) (20 + i)) // y
                    .Byte((byte) i) // grid (UsePostKRPackets)
                    .UInt(contSerial)
                    .UShort(0) // hue
                    .UShort(0); // flags (Sagas: 2 Byte pro Item)
            }

            Recv(w.ToDynamicArray(), dynamicLength: true);

            Item cont = World.FindItem(contSerial);
            Assert.NotNull(cont);
            Assert.Equal(3, cont.Contains.Count);
            Assert.Same(cont, World.Player.Backpack);

            for (int i = 0; i < childSerials.Length; i++)
            {
                Item child = World.FindItem(childSerials[i]);
                Assert.NotNull(child);
                Assert.Equal(5 + i, child.Amount);
                Assert.Same(cont, child.Container);
                // RootContainer laeuft die Item-Kette hoch: Kind -> Backpack -> Player.
                Assert.Same(World.Player, child.RootContainer);
                Assert.True(child.IsChildOf(cont));
                Assert.True(child.IsChildOf(World.Player.Backpack));
                Assert.False(child.OnGround);
            }
        }

        // ---- 0x25 ContainerContentUpdate --------------------------------------

        [Fact]
        public void ContainerContentUpdate_haengt_Item_in_Container()
        {
            SendLoginConfirm();

            const uint contSerial = 0x40000040;
            const uint childSerial = 0x40000041;
            SendWorldItem(contSerial, 0x0E75, 1002, 1202, 0);

            byte[] packet = new ByteWriter()
                .Byte(0x25)
                .UInt(childSerial)
                .UShort(0x0F06) // itemID
                .SByte(0) // itemID offset
                .UShort(0) // amount 0 -> 1
                .UShort(30).UShort(40) // x/y im Container
                .Byte(0) // grid (UsePostKRPackets)
                .UInt(contSerial)
                .UShort(0x0044) // hue
                .UShort(0) // flags (Sagas: 2 Byte pro Item)
                .ToDynamicArray();

            Recv(packet, dynamicLength: true);

            Item child = World.FindItem(childSerial);
            Assert.NotNull(child);
            Assert.Equal(1, child.Amount);
            Assert.Equal((ushort) 0x0044, child.Hue);

            Item cont = World.FindItem(contSerial);
            Assert.Same(cont, child.Container);
            Assert.Contains(child, cont.Contains);
        }

        // ---- 0x2E EquipmentUpdate ----------------------------------------------

        [Fact]
        public void EquipmentUpdate_legt_Item_auf_Layer_des_Players()
        {
            SendLoginConfirm();

            const uint cloakSerial = 0x40000030;

            byte[] packet = new ByteWriter()
                .Byte(0x2E)
                .UInt(cloakSerial)
                .UShort(0x1515) // itemID (Cloak)
                .SByte(0) // itemID offset
                .Byte((byte) Layer.Cloak)
                .UInt(PlayerSerial)
                .UShort(0x0005) // hue
                .ToArray(); // 0x2E ist ein Fixed-Length-Paket (15 Bytes)

            Recv(packet, dynamicLength: false);

            Item cloak = World.Player.GetItemOnLayer(Layer.Cloak);
            Assert.NotNull(cloak);
            Assert.Equal(cloakSerial, cloak.Serial.Value);
            Assert.Equal((ushort) 0x0005, cloak.Hue);
            Assert.Same(World.Player, cloak.RootContainer);
        }

        // ---- 0xA1/0xA2/0xA3 Stats ------------------------------------------------

        [Fact]
        public void HitsManaStam_Updates_setzen_Werte()
        {
            SendLoginConfirm();

            Recv(new ByteWriter().Byte(0xA1).UInt(PlayerSerial).UShort(100).UShort(87).ToArray(), false);
            Recv(new ByteWriter().Byte(0xA2).UInt(PlayerSerial).UShort(90).UShort(45).ToArray(), false);
            Recv(new ByteWriter().Byte(0xA3).UInt(PlayerSerial).UShort(80).UShort(33).ToArray(), false);

            Assert.Equal(100, World.Player.HitsMax);
            Assert.Equal(87, World.Player.Hits);
            Assert.Equal(90, World.Player.ManaMax);
            Assert.Equal(45, World.Player.Mana);
            Assert.Equal(80, World.Player.StamMax);
            Assert.Equal(33, World.Player.Stam);
        }

        // ---- 0x77 MobileMoving -----------------------------------------------------

        [Fact]
        public void MobileMoving_aktualisiert_Position_und_Notoriety()
        {
            SendLoginConfirm();

            byte[] packet = new ByteWriter()
                .Byte(0x77)
                .UInt(MobSerial)
                .UShort(0x0190) // body
                .UShort(1003).UShort(1203).SByte(7)
                .Byte(4) // direction
                .UShort(0x0021) // hue
                .UShort(0x40) // flags: warmode (Sagas 2 Byte)
                .Byte(6) // notoriety: murderer
                .ToArray(); // fixe Laenge (17)

            Recv(packet, dynamicLength: false);

            Mobile m = World.FindMobile(MobSerial);
            Assert.NotNull(m);
            Assert.Equal(new Point3D(1003, 1203, 7), m.Position);
            Assert.Equal(Direction.South, m.Direction);
            Assert.Equal((ushort) 0x0021, m.Hue);
            Assert.True(m.Warmode);
            Assert.Equal(6, m.Notoriety);
        }

        // ---- 0x1D RemoveObject --------------------------------------------------------

        [Fact]
        public void RemoveObject_entfernt_Item_und_Mobile()
        {
            SendLoginConfirm();

            const uint itemSerial = 0x40000050;
            SendWorldItem(itemSerial, 0x0EED, 1005, 1210, 0);
            Assert.NotNull(World.FindItem(itemSerial));

            // 0x1D fuer Items entfernt VERZOEGERT (Razor CE: 0.25s), damit ein
            // sofortiges Re-Add das Item rettet. Direkt nach 0x1D ist es noch da.
            Recv(new ByteWriter().Byte(0x1D).UInt(itemSerial).ToArray(), false);
            Assert.NotNull(World.FindItem(itemSerial));

            // Nach Ablauf des Remove-Timers (0.25s) ist es weg.
            SliceAfter(TimeSpan.FromMilliseconds(350));
            Assert.Null(World.FindItem(itemSerial));

            // Rettungs-Verhalten: 0x1D, dann sofortiges Re-Add -> Item bleibt.
            SendWorldItem(itemSerial, 0x0EED, 1005, 1210, 0);
            Recv(new ByteWriter().Byte(0x1D).UInt(itemSerial).ToArray(), false);
            SendWorldItem(itemSerial, 0x0EED, 1006, 1210, 0); // Re-Add -> CancelRemove
            SliceAfter(TimeSpan.FromMilliseconds(350));
            Assert.NotNull(World.FindItem(itemSerial));

            // Mobile anlegen (0x77) und wieder entfernen.
            byte[] moving = new ByteWriter()
                .Byte(0x77)
                .UInt(MobSerial)
                .UShort(0x0190)
                .UShort(1001).UShort(1201).SByte(5)
                .Byte(0).UShort(0).UShort(0).Byte(1)
                .ToArray();
            Recv(moving, dynamicLength: false);
            Assert.NotNull(World.FindMobile(MobSerial));

            Recv(new ByteWriter().Byte(0x1D).UInt(MobSerial).ToArray(), false);
            Assert.Null(World.FindMobile(MobSerial));

            // Der Player selbst wird durch 0x1D nie entfernt.
            Recv(new ByteWriter().Byte(0x1D).UInt(PlayerSerial).ToArray(), false);
            Assert.NotNull(World.FindMobile(PlayerSerial));
        }

        // ---- 0x20 MobileUpdate -------------------------------------------------------------

        [Fact]
        public void MobileUpdate_setzt_PlayerPosition()
        {
            SendLoginConfirm();

            byte[] packet = new ByteWriter()
                .Byte(0x20)
                .UInt(PlayerSerial)
                .UShort(0x0190) // body
                .SByte(0) // body offset
                .UShort(0x0000) // hue
                .UShort(0) // flags (Sagas: 2 Byte Extended)
                .UShort(1010).UShort(1190)
                .UShort(0) // always 0
                .Byte(3) // direction
                .SByte(12)
                .ToArray(); // fixe Laenge (19)

            Recv(packet, dynamicLength: false);

            Assert.Equal(new Point3D(1010, 1190, 12), World.Player.Position);
            Assert.Equal(Direction.Down, World.Player.Direction);
        }

        // ---- 0x17 NewMobileStatus (poison / yellow healthbar) ----------------------------
        //
        // On this shard 0x17 is the ONLY source of poison state: bit 0x04 of
        // the flags byte carries flying, not poison. Without this handler
        // Poisoned stayed false forever (Smart Heal always healed).

        /// <summary>0x17: [serial][count] then per block: [type][level] (dynamic length).</summary>
        private static void SendHealthbar(uint serial, ushort type, byte level)
        {
            byte[] packet = new ByteWriter()
                .Byte(0x17)
                .UInt(serial)
                .UShort(1) // one block
                .UShort(type)
                .Byte(level)
                .ToDynamicArray();

            Recv(packet, dynamicLength: true);
        }

        [Fact]
        public void Healthbar_setzt_und_loescht_Gift_beim_Spieler()
        {
            SendLoginConfirm();
            Assert.False(World.Player.Poisoned);

            // Level = Poison.Level + 1, so 1 already means poisoned.
            SendHealthbar(PlayerSerial, 1, 1);
            Assert.True(World.Player.Poisoned);

            // 0 = poison gone (cured or expired).
            SendHealthbar(PlayerSerial, 1, 0);
            Assert.False(World.Player.Poisoned);
        }

        [Fact]
        public void Healthbar_setzt_Gift_auch_bei_fremden_Mobiles()
        {
            SendLoginConfirm();

            byte[] moving = new ByteWriter()
                .Byte(0x77).UInt(MobSerial).UShort(0x0190)
                .UShort(1000).UShort(1200).SByte(5)
                .Byte(0).UShort(0).UShort(0).Byte(1)
                .ToArray();
            Recv(moving, dynamicLength: false);

            SendHealthbar(MobSerial, 1, 4); // deadly poison
            Assert.True(World.FindMobile(MobSerial).Poisoned);
        }

        [Fact]
        public void Healthbar_Typ2_setzt_gelbe_Leiste_und_nicht_Gift()
        {
            SendLoginConfirm();

            SendHealthbar(PlayerSerial, 2, 1);

            Assert.True(World.Player.Blessed);
            Assert.False(World.Player.Poisoned);
        }

        [Fact]
        public void Nach_Healthbar_darf_das_FlagsByte_Gift_nicht_mehr_ueberschreiben()
        {
            SendLoginConfirm();
            SendHealthbar(PlayerSerial, 1, 1);
            Assert.True(World.Player.Poisoned);

            // 0x20 MobileUpdate with flags = 0: ProcessPacketFlags must NOT
            // clear the poison, otherwise every position change would "cure"
            // the player (bit 0x04 is flying on this server).
            byte[] packet = new ByteWriter()
                .Byte(0x20)
                .UInt(PlayerSerial)
                .UShort(0x0190)
                .SByte(0)
                .UShort(0x0000)
                .UShort(0) // flags = 0
                .UShort(1010).UShort(1190)
                .UShort(0)
                .Byte(3)
                .SByte(12)
                .ToArray();

            Recv(packet, dynamicLength: false);

            Assert.True(World.Player.Poisoned);
        }
    }
}
