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

// UOSagas-Razor: Tests fuer die Anzeige-Filter (DisplayFilters).
//
// Die Filter arbeiteten per Block+Inject: das Original wird geblockt
// (args.Block -> OnServerPacket true) und eine gepatchte Kopie landet via
// FakeClientServices.InjectedToClient. Genau das pruefen die Tests, mit
// synthetischen Paketen durch den echten PacketHandler-Dispatch.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Assistant;
using Assistant.Core;
using Xunit;

namespace Razor.Core.Tests
{
    [Collection("ConfigSequential")]
    public class DisplayFilterTests : IDisposable
    {
        private const uint PlayerSerial = 0x00000901;
        private const uint OtherMobile = 0x00000902;

        private readonly string m_TempDir;
        private readonly CultureInfo m_OldCulture;
        private readonly FakeClientServices m_Fake;

        public DisplayFilterTests()
        {
            m_OldCulture = CultureInfo.CurrentCulture;
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;

            m_TempDir = Path.Combine(Path.GetTempPath(), "RazorDisplayFilterTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(m_TempDir);
            Config.Initialize(m_TempDir);
            Config.LoadLastProfile();

            PacketHandlers.Initialize();
            Spell.Initialize(); // fuellt m_SpellsByPower (wie der Plugin-Start)
            Targeting.Reset();

            World.Clear();
            PlayerData player = new PlayerData(PlayerSerial) { Position = new Point3D(100, 100, 0) };
            World.AddMobile(player);
            World.Player = player;

            Mobile other = new Mobile(OtherMobile) { Position = new Point3D(101, 100, 0) };
            World.AddMobile(other);

            m_Fake = new FakeClientServices();
            ClientProxy.Bind(m_Fake);
        }

        public void Dispose()
        {
            ClientProxy.Unbind();
            World.Clear();
            Targeting.Reset();
            CultureInfo.CurrentCulture = m_OldCulture;

            try
            {
                Directory.Delete(m_TempDir, true);
            }
            catch
            {
            }
        }

        // ------------------------------------------------------------ builders

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

        /// <summary>0x1C AsciiSpeech: serial body type hue font name(30) text\0.</summary>
        private static byte[] BuildAsciiSpeech(uint serial, byte type, ushort hue, string text)
        {
            var b = new List<byte> { 0x1C, 0, 0 };
            UInt(b, serial);
            UShort(b, 0x0190); // body
            b.Add(type);
            UShort(b, hue);
            UShort(b, 3); // font
            var name = new byte[30];
            Encoding.ASCII.GetBytes("Someone", 0, 7, name, 0);
            b.AddRange(name);
            b.AddRange(Encoding.ASCII.GetBytes(text));
            b.Add(0);
            b[1] = (byte)(b.Count >> 8);
            b[2] = (byte)b.Count;
            return b.ToArray();
        }

        /// <summary>0x77 MobileMoving (fix 17): serial body x y z dir hue flags noto.</summary>
        private static byte[] BuildMobileMoving(uint serial, ushort hue)
        {
            var b = new List<byte> { 0x77 };
            UInt(b, serial);
            UShort(b, 0x0190);
            UShort(b, 101);
            UShort(b, 100);
            b.Add(0);           // z
            b.Add(0);           // dir
            UShort(b, hue);
            UShort(b, 0);       // Sagas v2.35: 2-Byte-Flags NACH dem Hue
            b.Add(1);           // noto
            return b.ToArray();
        }

        private static bool Recv(byte[] data, bool dynamicLength)
        {
            return PacketHandler.OnServerPacket(data[0], new PacketReader(data, dynamicLength), null);
        }

        private static ushort HueAt(byte[] packet, int offset)
        {
            return (ushort)((packet[offset] << 8) | packet[offset + 1]);
        }

        // ------------------------------------------------------------ speech hue

        [Fact]
        public void ForceSpeechHue_patcht_fremde_Sprache()
        {
            Config.SetProperty("ForceSpeechHue", true);
            Config.SetProperty("SpeechHue", 0x0123);

            bool blocked = Recv(BuildAsciiSpeech(OtherMobile, 0x00, 0x0044, "hello there"), true);

            Assert.True(blocked, "Original muss geblockt werden");
            byte[] injected = Assert.Single(m_Fake.InjectedToClient);
            Assert.Equal(0x1C, injected[0]);
            Assert.Equal(0x0123, HueAt(injected, 10));
            // Nur der Hue aendert sich, der Rest bleibt byte-identisch.
            Assert.Equal("hello there", Encoding.ASCII.GetString(injected, 44, 11));
        }

        [Fact]
        public void ForceSpeechHue_laesst_eigene_Sprache_in_Ruhe()
        {
            Config.SetProperty("ForceSpeechHue", true);

            bool blocked = Recv(BuildAsciiSpeech(PlayerSerial, 0x00, 0x0044, "me talking"), true);

            Assert.False(blocked);
            Assert.Empty(m_Fake.InjectedToClient);
        }

        [Fact]
        public void ForceSpeechHue_aus_patcht_nichts()
        {
            Config.SetProperty("ForceSpeechHue", false);

            bool blocked = Recv(BuildAsciiSpeech(OtherMobile, 0x00, 0x0044, "hello"), true);

            Assert.False(blocked);
            Assert.Empty(m_Fake.InjectedToClient);
        }

        // ------------------------------------------------------------ spells

        [Fact]
        public void OverrideSpellFormat_ersetzt_Powerwords()
        {
            Config.SetProperty("OverrideSpellFormat", true);
            Config.SetProperty("ForceSpellHue", true);
            Config.SetProperty("SpellFormat", "{power} [{spell}]");
            Config.SetProperty("HarmfulSpellHue", 0x0058);

            // "Corp Por" = Energy Bolt (harmful)
            bool blocked = Recv(BuildAsciiSpeech(OtherMobile, 0x0A, 0x03B1, "Corp Por"), true);

            Assert.True(blocked);
            byte[] injected = Assert.Single(m_Fake.InjectedToClient);
            Assert.Equal(0x1C, injected[0]);
            Assert.Equal((byte)MessageType.Spell, injected[9]);
            Assert.Equal(0x0058, HueAt(injected, 10));

            string text = Encoding.ASCII.GetString(injected, 44, injected.Length - 45);
            Assert.Contains("Corp Por", text);
            Assert.Contains("[Energy Bolt]", text);
        }

        [Fact]
        public void ForceSpellHue_ohne_Format_patcht_nur_den_Hue()
        {
            Config.SetProperty("OverrideSpellFormat", false);
            Config.SetProperty("ForceSpellHue", true);
            Config.SetProperty("BeneficialSpellHue", 0x0005);

            // "In Vas Mani" = Greater Heal (beneficial)
            bool blocked = Recv(BuildAsciiSpeech(OtherMobile, 0x0A, 0x03B1, "In Vas Mani"), true);

            Assert.True(blocked);
            byte[] injected = Assert.Single(m_Fake.InjectedToClient);
            Assert.Equal(0x0005, HueAt(injected, 10));
            // Text bleibt unveraendert (nur Hue-Patch, kein Neuaufbau).
            Assert.Contains("In Vas Mani", Encoding.ASCII.GetString(injected));
        }

        [Fact]
        public void Spells_ohne_Optionen_bleiben_unangetastet()
        {
            Config.SetProperty("OverrideSpellFormat", false);
            Config.SetProperty("ForceSpellHue", false);

            bool blocked = Recv(BuildAsciiSpeech(OtherMobile, 0x0A, 0x03B1, "Corp Por"), true);

            Assert.False(blocked);
            Assert.Empty(m_Fake.InjectedToClient);
        }

        // ------------------------------------------------------------ LT hilight

        [Fact]
        public void LTHilight_faerbt_das_LastTarget()
        {
            Config.SetProperty("LTHilight", 0x0030);
            Targeting.SetLastTarget((Serial)OtherMobile);

            bool blocked = Recv(BuildMobileMoving(OtherMobile, 0x0044), false);

            Assert.True(blocked);
            byte[] injected = Assert.Single(m_Fake.InjectedToClient);
            Assert.Equal(0x77, injected[0]);
            Assert.Equal((ushort)(0x0030 | 0x8000), HueAt(injected, 13));
            // Sagas-Flags hinter dem Hue bleiben unveraendert.
            Assert.Equal(0, HueAt(injected, 15));
        }

        [Fact]
        public void LTHilight_ignoriert_andere_Mobiles()
        {
            Config.SetProperty("LTHilight", 0x0030);
            Targeting.SetLastTarget((Serial)OtherMobile);

            Mobile third = new Mobile(0x00000903u) { Position = new Point3D(102, 100, 0) };
            World.AddMobile(third);

            bool blocked = Recv(BuildMobileMoving(0x00000903u, 0x0044), false);

            Assert.False(blocked);
            Assert.Empty(m_Fake.InjectedToClient);
        }

        [Fact]
        public void LTHilight_aus_patcht_nichts()
        {
            Config.SetProperty("LTHilight", 0);
            Targeting.SetLastTarget((Serial)OtherMobile);

            bool blocked = Recv(BuildMobileMoving(OtherMobile, 0x0044), false);

            Assert.False(blocked);
            Assert.Empty(m_Fake.InjectedToClient);
        }
    }
}
