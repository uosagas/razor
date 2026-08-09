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

// UOSagas-Razor: Tests fuer den Gump-Inspector-Kern (GumpObserver +
// GumpLayoutParser). Der Parser laeuft gegen ein realistisches Layout;
// der Capture-Pfad bekommt synthetische 0xB0/0xB1-Pakete durch den echten
// PacketHandler-Dispatch.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Assistant;
using Assistant.Core;
using Xunit;

namespace Razor.Core.Tests
{
    [Collection("ConfigSequential")]
    public class GumpObserverTests : IDisposable
    {
        public GumpObserverTests()
        {
            PacketHandlers.Initialize(); // registriert auch den GumpObserver
            GumpObserver.Clear();
            GumpObserver.Recording = true;
        }

        public void Dispose()
        {
            GumpObserver.Recording = false;
            GumpObserver.Clear();
        }

        // ------------------------------------------------------------ parser

        private const string Layout =
            "{ resizepic 10 10 5054 400 300 }" +
            "{ page 1 }" +
            "{ xmfhtmlgump 40 40 320 40 1011036 0 0 }" +
            "{ text 40 90 68 0 }" +
            "{ croppedtext 40 120 200 20 0 1 }" +
            "{ htmlgump 40 150 320 60 2 0 1 }" +
            "{ button 40 220 4005 4007 1 0 204 }" +
            "{ checkbox 40 250 210 211 0 42 }" +
            "{ textentry 120 250 150 20 0 7 3 }" +
            "{ gumppic 300 40 1417 }" +
            "{ frobnicate 5 6 7 }";

        private static readonly string[] Lines = { "Title", "Cropped line", "Html body", "prefill" };

        [Fact]
        public void LayoutParser_extrahiert_die_Controls()
        {
            List<GumpControlInfo> controls = GumpLayoutParser.Parse(Layout, Lines);

            Assert.Equal(11, controls.Count);

            var button = controls.Single(c => c.Type == "button");
            Assert.Equal(204, button.ButtonId);
            Assert.Equal(40, button.X);
            Assert.Equal(220, button.Y);

            Assert.Equal(42, controls.Single(c => c.Type == "checkbox").SwitchId);

            var entry = controls.Single(c => c.Type == "textentry");
            Assert.Equal(7, entry.EntryId);
            Assert.Equal("prefill", entry.Text);

            Assert.Equal("Title", controls.Single(c => c.Type == "text").Text);
            Assert.Equal("Cropped line", controls.Single(c => c.Type == "croppedtext").Text);
            Assert.Equal("Html body", controls.Single(c => c.Type == "htmlgump").Text);

            Assert.Equal(1011036, controls.Single(c => c.Type == "xmfhtmlgump").Cliloc);
            Assert.Equal(1417, controls.Single(c => c.Type == "gumppic").Graphic);
            Assert.Equal(1, controls.Single(c => c.Type == "page").Page);

            // Unbekannte Tokens bleiben mit Roh-Args erhalten.
            var unknown = controls.Single(c => c.Type == "frobnicate");
            Assert.Equal(new[] { "5", "6", "7" }, unknown.Args);
            Assert.Equal(5, unknown.X);
        }

        [Fact]
        public void LayoutParser_uebersteht_kaputte_Eintraege()
        {
            var controls = GumpLayoutParser.Parse("{ button 1 }{ text }{ }{ button 5 6 0 0 1 0 9 }", Array.Empty<string>());

            // Der kaputte button behaelt seine Args, der intakte wird erkannt.
            Assert.Equal(9, controls.Last().ButtonId);
            Assert.Null(controls.First().ButtonId);
        }

        // ------------------------------------------------------------ capture

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

        /// <summary>Baut ein 0xB0-Paket wie der Server (Format = ClassicUO OpenGump).</summary>
        private static byte[] BuildOpenGump(uint serial, uint gumpId, string layout, string[] lines)
        {
            var b = new List<byte> { 0xB0, 0, 0 };
            UInt(b, serial);
            UInt(b, gumpId);
            UInt(b, 50);  // x
            UInt(b, 60);  // y
            UShort(b, (ushort)layout.Length);
            b.AddRange(Encoding.ASCII.GetBytes(layout));
            UShort(b, (ushort)lines.Length);
            foreach (string line in lines)
            {
                UShort(b, (ushort)line.Length);
                b.AddRange(Encoding.BigEndianUnicode.GetBytes(line));
            }

            b[1] = (byte)(b.Count >> 8);
            b[2] = (byte)b.Count;
            return b.ToArray();
        }

        /// <summary>Baut ein 0xB1-Response-Paket (Serial, GumpId, Button, keine Switches/Entries).</summary>
        private static byte[] BuildResponse(uint serial, uint gumpId, int buttonId)
        {
            var b = new List<byte> { 0xB1, 0, 0 };
            UInt(b, serial);
            UInt(b, gumpId);
            UInt(b, (uint)buttonId);
            UInt(b, 0); // switches
            UInt(b, 0); // entries
            b[1] = (byte)(b.Count >> 8);
            b[2] = (byte)b.Count;
            return b.ToArray();
        }

        [Fact]
        public void Capture_via_0xB0_und_Antwort_via_0xB1()
        {
            byte[] open = BuildOpenGump(0x40001234, 949095101, Layout, Lines);
            PacketHandler.OnServerPacket(0xB0, new PacketReader(open, true), null);

            List<CapturedGump> captured = GumpObserver.Snapshot();
            CapturedGump g = Assert.Single(captured);
            Assert.Equal(949095101u, g.GumpId);
            Assert.Equal(0x40001234u, g.Serial);
            Assert.Equal(50, g.X);
            Assert.False(g.Compressed);
            Assert.Equal(11, g.Controls.Count);
            Assert.Equal("Title", g.Controls.Single(c => c.Type == "text").Text);
            Assert.Null(g.Response);

            // Der Spieler klickt Button 204 -> Antwort haengt am Capture.
            byte[] resp = BuildResponse(0x40001234, 949095101, 204);
            PacketHandler.OnClientPacket(0xB1, new PacketReader(resp, true), null);

            g = GumpObserver.Snapshot().Single();
            Assert.NotNull(g.Response);
            Assert.Equal(204, g.Response.ButtonId);
        }

        [Fact]
        public void Ohne_Recording_wird_nichts_gefangen()
        {
            GumpObserver.Recording = false;

            byte[] open = BuildOpenGump(0x40001234, 123, "{ button 1 2 3 4 1 0 5 }", Array.Empty<string>());
            PacketHandler.OnServerPacket(0xB0, new PacketReader(open, true), null);

            Assert.Empty(GumpObserver.Snapshot());
        }

        [Fact]
        public void Version_steigt_bei_Capture()
        {
            int before = GumpObserver.Version;

            byte[] open = BuildOpenGump(1, 2, "{ page 0 }", Array.Empty<string>());
            PacketHandler.OnServerPacket(0xB0, new PacketReader(open, true), null);

            Assert.True(GumpObserver.Version > before);
        }
    }
}
