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

// UOSagas-Razor: Tests fuer das HotKey-System (Phase 3c).
//
//  * SDL->WinForms-Keys-Mapping (Stichproben inkl. Modifier)
//  * Avalonia->WinForms-Keys-Mapping (Stichproben)
//  * hotkeys-Profilsektion: CE-Format-Roundtrip (Fixture mit Belegungen,
//    inkl. Erhalt nicht registrierter Eintraege)
//  * Dispatch: Callback + Schluck-Semantik (PassToUO), Enable/Disable
//  * dynamische Macro-Hotkeys (Play: <name>) nach Add/Remove

using System;
using System.Globalization;
using System.IO;
using System.Text;
using System.Xml;
using Assistant;
using Assistant.Macros;
using Xunit;

namespace Razor.Core.Tests
{
    [Collection("ConfigSequential")]
    public class HotKeyTests : IDisposable
    {
        private readonly string m_TempDir;
        private readonly CultureInfo m_OldCulture;

        public HotKeyTests()
        {
            m_OldCulture = CultureInfo.CurrentCulture;
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;

            m_TempDir = Path.Combine(Path.GetTempPath(), "RazorHotKeyTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(m_TempDir);
            Config.Initialize(m_TempDir);

            // Saubere Ausgangslage: alle (aus anderen Tests) registrierten
            // Hotkeys auf unbelegt zuruecksetzen.
            HotKey.ClearAll();
            HotKey.Enabled = true;

            World.Clear();
        }

        public void Dispose()
        {
            // Test-Registrierungen und Belegungen entfernen.
            HotKey.Remove("HKTest: fire");
            HotKey.Remove("HKTest: other");
            HotKey.ClearAll();
            HotKey.Enabled = true;

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

        private static PlayerData MakePlayer()
        {
            var player = new PlayerData(0x00000801);
            player.Position = new Point3D(1000, 1000, 0);
            World.AddMobile(player);
            World.Player = player;
            return player;
        }

        // ---- SDL -> Keys ------------------------------------------------------

        [Theory]
        [InlineData('a', (int) Keys.A)]
        [InlineData('z', (int) Keys.Z)]
        [InlineData('5', (int) Keys.D5)]
        [InlineData(13, (int) Keys.Return)]
        [InlineData(27, (int) Keys.Escape)]
        [InlineData(32, (int) Keys.Space)]
        [InlineData('`', (int) Keys.Oemtilde)]
        [InlineData(0x4000003A, (int) Keys.F1)] // SDLK_F1 (Scancode 58)
        [InlineData(0x4000003E, (int) Keys.F5)] // SDLK_F5 (Scancode 62)
        [InlineData(0x40000045, (int) Keys.F12)] // SDLK_F12 (Scancode 69)
        [InlineData(0x4000005D, (int) Keys.NumPad5)] // SDLK_KP_5 (Scancode 93)
        [InlineData(0x40000062, (int) Keys.NumPad0)] // SDLK_KP_0 (Scancode 98)
        [InlineData(0x40000050, (int) Keys.Left)] // SDLK_LEFT (Scancode 80)
        [InlineData(0x40000052, (int) Keys.Up)] // SDLK_UP (Scancode 82)
        [InlineData(0x40000049, (int) Keys.Insert)] // SDLK_INSERT (Scancode 73)
        [InlineData(0x400000E0, (int) Keys.ControlKey)] // SDLK_LCTRL
        [InlineData(0x400000E5, (int) Keys.ShiftKey)] // SDLK_RSHIFT
        public void SdlKeycodeMapptAufWinFormsKeys(int sdl, int expected)
        {
            Assert.Equal((Keys) expected, KeyMap.ToKeys(sdl));
        }

        [Theory]
        [InlineData(0x0040, (int) ModKeys.Control)] // KMOD_LCTRL
        [InlineData(0x0080, (int) ModKeys.Control)] // KMOD_RCTRL
        [InlineData(0x0001, (int) ModKeys.Shift)] // KMOD_LSHIFT
        [InlineData(0x0200, (int) ModKeys.Alt)] // KMOD_RALT
        [InlineData(0x0041, (int) (ModKeys.Control | ModKeys.Shift))] // LCTRL|LSHIFT
        [InlineData(0x0141, (int) (ModKeys.Control | ModKeys.Shift | ModKeys.Alt))]
        [InlineData(0x1000, (int) ModKeys.None)] // KMOD_NUM wird ignoriert
        [InlineData(0, (int) ModKeys.None)]
        public void SdlKmodMapptAufModKeys(int kmod, int expected)
        {
            Assert.Equal((ModKeys) expected, KeyMap.ToModKeys(kmod));
        }

        // ---- Maus: SDL-Event -> CE-Button + Modifier ---------------------------

        [Theory]
        [InlineData(2, 0, 1)] // SDL Mitte -> CE 1
        [InlineData(4, 0, 2)] // SDL XButton1 -> CE 2
        [InlineData(5, 0, 3)] // SDL XButton2 -> CE 3
        [InlineData(9, 0, 3)] // Viel-Tasten-Maus -> wie XButton2
        [InlineData(0, 1, 0)] // Wheel hoch -> CE 0 (Richtung steckt im wheel)
        [InlineData(0, -1, 0)] // Wheel runter -> CE 0
        public void MausEventMapptAufCeButton(int sdlButton, int wheel, int expected)
        {
            Assert.True(KeyMap.TryTranslateMouse(sdlButton, wheel, out int ceButton, out ModKeys mod));
            Assert.Equal(expected, ceButton);
            Assert.Equal(ModKeys.None, mod);
        }

        [Theory]
        [InlineData(1)] // Links: filtert der Client, zur Sicherheit auch hier
        [InlineData(3)] // Rechts
        [InlineData(0)] // kein Button, kein Wheel
        public void LinksRechtsUndLeereMausEventsWerdenIgnoriert(int sdlButton)
        {
            Assert.False(KeyMap.TryTranslateMouse(sdlButton, 0, out _, out _));
        }

        [Theory]
        [InlineData(0x0040, (int) ModKeys.Control)] // KMOD_LCTRL im High-Word
        [InlineData(0x0201, (int) (ModKeys.Alt | ModKeys.Shift))] // RALT|LSHIFT
        [InlineData(0x1000, (int) ModKeys.None)] // KMOD_NUM wird ignoriert
        public void MausEventEntpacktKmodAusDemHighWord(int kmod, int expected)
        {
            // Sagas-Erweiterung: Client packt SDL_GetModState() ins High-Word.
            Assert.True(KeyMap.TryTranslateMouse(2 | (kmod << 16), 0, out int ceButton, out ModKeys mod));
            Assert.Equal(1, ceButton);
            Assert.Equal((ModKeys) expected, mod);

            Assert.True(KeyMap.TryTranslateMouse(kmod << 16, 1, out int wheelButton, out ModKeys wheelMod));
            Assert.Equal(0, wheelButton);
            Assert.Equal((ModKeys) expected, wheelMod);
        }

        [Fact]
        public void MausDispatchFeuertNurBeiExaktemModifier()
        {
            MakePlayer();

            int fired = 0;
            KeyData kd = HotKey.Add(HKCategory.Misc, HKSubCat.None, "HKTest: fire", () => fired++);
            kd.Key = -3; // Mouse MID Button
            kd.Mod = ModKeys.Control;

            HotKey.OnMouse(1, 0, ModKeys.None); // ohne Strg: nichts
            Assert.Equal(0, fired);

            HotKey.OnMouse(1, 0, ModKeys.Control);
            Assert.Equal(1, fired);
        }

        [Fact]
        public void MausDispatchUnterscheidetWheelRichtungen()
        {
            MakePlayer();

            int up = 0, down = 0;
            KeyData kdUp = HotKey.Add(HKCategory.Misc, HKSubCat.None, "HKTest: fire", () => up++);
            kdUp.Key = -1; // Wheel UP
            KeyData kdDown = HotKey.Add(HKCategory.Misc, HKSubCat.None, "HKTest: other", () => down++);
            kdDown.Key = -2; // Wheel DOWN

            // Je Richtung ein Event (mehr nicht — der 20-ms-Dedupe-Guard in
            // KeyData.Callback wuerde ein zweites Event sofort verschlucken).
            HotKey.OnMouse(0, 1, ModKeys.None);
            HotKey.OnMouse(0, -1, ModKeys.None);

            Assert.Equal(1, up);
            Assert.Equal(1, down);
        }

        // ---- hotkeys-Profilsektion: CE-Format-Roundtrip -------------------------

        [Fact]
        public void HotkeysSektionRoundtripImCeFormat()
        {
            int fired = 0;
            HotKey.Add(HKCategory.Targets, LocString.LastTarget, () => fired++);
            HotKey.Add(HKCategory.Macros, HKSubCat.None, "Play: test", () => fired++);

            // Fixture wie von Razor CE gespeichert: 2 Belegungen + 1 Eintrag
            // eines (hier) nicht registrierten Hotkeys (L:1025 = BandageSelf).
            const string fixture =
                "<hotkeys>" +
                "<key mod=\"6\" key=\"85\" send=\"False\" command=\"\">L:1058</key>" +
                "<key mod=\"2\" key=\"112\" send=\"True\" command=\"\">Play: test</key>" +
                "<key mod=\"1\" key=\"113\" send=\"False\" command=\"\">L:1025</key>" +
                "</hotkeys>";

            var doc = new XmlDocument();
            doc.LoadXml(fixture);
            HotKey.Load(doc.DocumentElement);

            // Belegungen angekommen? (mod 6 = Control|Shift, Keys.U = 85)
            KeyData lastTarget = HotKey.Get((int) LocString.LastTarget);
            Assert.NotNull(lastTarget);
            Assert.Equal((int) Keys.U, lastTarget.Key);
            Assert.Equal(ModKeys.Control | ModKeys.Shift, lastTarget.Mod);
            Assert.False(lastTarget.SendToUO);

            KeyData play = HotKey.Get("Play: test");
            Assert.NotNull(play);
            Assert.Equal((int) Keys.F1, play.Key);
            Assert.Equal(ModKeys.Control, play.Mod);
            Assert.True(play.SendToUO);

            // Speichern: byte-kompatibel zum CE-Format, nicht registrierter
            // Eintrag bleibt erhalten.
            var sb = new StringBuilder();
            using (var xml = XmlWriter.Create(sb, new XmlWriterSettings
                   {
                       OmitXmlDeclaration = true,
                       ConformanceLevel = ConformanceLevel.Fragment
                   }))
            {
                xml.WriteStartElement("hotkeys");
                HotKey.Save(xml);
                xml.WriteEndElement();
            }

            string saved = sb.ToString();

            Assert.Contains("<key mod=\"6\" key=\"85\" send=\"False\" command=\"\">L:1058</key>", saved);
            Assert.Contains("<key mod=\"2\" key=\"112\" send=\"True\" command=\"\">Play: test</key>", saved);
            Assert.Contains("<key mod=\"1\" key=\"113\" send=\"False\" command=\"\">L:1025</key>", saved);

            // Roundtrip Nr. 2: das Gespeicherte laedt identisch wieder.
            var doc2 = new XmlDocument();
            doc2.LoadXml(saved);
            HotKey.ClearAll();
            HotKey.Load(doc2.DocumentElement);

            Assert.Equal((int) Keys.U, HotKey.Get((int) LocString.LastTarget).Key);
            Assert.Equal((int) Keys.F1, HotKey.Get("Play: test").Key);
        }

        [Fact]
        public void UnbelegteHotkeysWerdenNichtGespeichert()
        {
            HotKey.Add(HKCategory.Macros, HKSubCat.None, "HKTest: other", () => { });
            HotKey.ClearAll();

            var sb = new StringBuilder();
            using (var xml = XmlWriter.Create(sb, new XmlWriterSettings
                   {
                       OmitXmlDeclaration = true,
                       ConformanceLevel = ConformanceLevel.Fragment
                   }))
            {
                xml.WriteStartElement("hotkeys");
                HotKey.Save(xml);
                xml.WriteEndElement();
            }

            Assert.DoesNotContain("HKTest: other", sb.ToString());
        }

        // ---- Dispatch -----------------------------------------------------------

        [Fact]
        public void DispatchFeuertCallbackUndVerschlucktOhnePassToUO()
        {
            MakePlayer();

            int fired = 0;
            KeyData kd = HotKey.Add(HKCategory.Misc, HKSubCat.None, "HKTest: fire", () => fired++);
            kd.Key = (int) Keys.F5;
            kd.Mod = ModKeys.Control;
            kd.SendToUO = false;

            // Falscher Modifier: nichts passiert, Taste geht an UO.
            Assert.True(HotKey.OnKeyDown((int) Keys.F5, ModKeys.None));
            Assert.Equal(0, fired);

            // Treffer: Callback feuert, Taste wird verschluckt (PassToUO aus).
            Assert.False(HotKey.OnKeyDown((int) Keys.F5, ModKeys.Control));
            Assert.Equal(1, fired);
        }

        [Fact]
        public void DispatchReichtMitPassToUOAnUODurch()
        {
            MakePlayer();

            int fired = 0;
            KeyData kd = HotKey.Add(HKCategory.Misc, HKSubCat.None, "HKTest: fire", () => fired++);
            kd.Key = (int) Keys.F6;
            kd.Mod = ModKeys.None;
            kd.SendToUO = true;

            Assert.True(HotKey.OnKeyDown((int) Keys.F6, ModKeys.None));
            Assert.Equal(1, fired);
        }

        [Fact]
        public void DispatchOhneSpielerFeuertNurGlobaleHotkeys()
        {
            World.Clear(); // kein Player

            int fired = 0;
            KeyData kd = HotKey.Add(HKCategory.Misc, HKSubCat.None, "HKTest: fire", () => fired++);
            kd.Key = (int) Keys.F7;
            kd.Mod = ModKeys.None;

            // Nicht global: ohne Spieler kein Callback.
            Assert.True(HotKey.OnKeyDown((int) Keys.F7, ModKeys.None));
            Assert.Equal(0, fired);

            // Global (z. B. "Show Razor"): feuert auch ohne Spieler.
            kd.Global = true;
            Assert.False(HotKey.OnKeyDown((int) Keys.F7, ModKeys.None));
            Assert.Equal(1, fired);
        }

        [Fact]
        public void DeaktivierteHotkeysFeuernNicht()
        {
            MakePlayer();

            int fired = 0;
            KeyData kd = HotKey.Add(HKCategory.Misc, HKSubCat.None, "HKTest: fire", () => fired++);
            kd.Key = (int) Keys.F8;
            kd.Mod = ModKeys.None;

            HotKey.Enabled = false;
            Assert.True(HotKey.OnKeyDown((int) Keys.F8, ModKeys.None));
            Assert.Equal(0, fired);

            HotKey.Enabled = true;
            Assert.False(HotKey.OnKeyDown((int) Keys.F8, ModKeys.None));
            Assert.Equal(1, fired);
        }

        // ---- Dynamische Macro-Hotkeys -------------------------------------------

        [Fact]
        public void MacroAddRegistriertPlayHotkeyUndRemoveEntferntIhn()
        {
            string path = Path.Combine(Config.GetUserDirectory("Macros"), "hktest.macro");
            var macro = new Macro(path);

            string hotkeyName = Language.Format(LocString.PlayA1, macro); // "Play: hktest"
            Assert.Equal("Play: hktest", hotkeyName);

            try
            {
                MacroManager.Add(macro);
                Assert.NotNull(HotKey.Get(hotkeyName));

                MacroManager.Remove(macro);
                Assert.Null(HotKey.Get(hotkeyName));
            }
            finally
            {
                if (MacroManager.List.Contains(macro))
                    MacroManager.Remove(macro);
            }
        }
    }
}
