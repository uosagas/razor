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

// UOSagas-Razor: Phase 4b — die portierte Lua-Engine (Client-Dialekt 1:1)
// laeuft gegen das Razor-Weltmodell. Tests: Sandbox, Say->0xAD,
// Items.FindBySerial, Journal.Contains, Gumps ueber GumpInfo und die
// __debug_line-Injektion (CurrentLineChanged/Breakpoint-Konvention).

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using Assistant;
using Assistant.LuaEngine;
using Assistant.Macros;
using Xunit;

namespace Razor.Core.Tests
{
    [Collection("ConfigSequential")]
    public class LuaEngineTests : IDisposable
    {
        private const uint PlayerSerial = 0x00001101;
        private const uint ChestSerial = 0x40001102;

        private readonly string m_TempDir;
        private readonly CultureInfo m_OldCulture;
        private readonly FakeClientServices m_Fake;

        public LuaEngineTests()
        {
            m_OldCulture = CultureInfo.CurrentCulture;
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;

            m_TempDir = Path.Combine(Path.GetTempPath(), "RazorLuaTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(m_TempDir);
            Config.Initialize(m_TempDir);
            Config.LoadLastProfile();

            PacketHandlers.Initialize();
            MacroManager.Stop();
            ActionQueue.Stop();

            World.Clear();
            PlayerData player = new PlayerData(PlayerSerial)
            {
                Name = "Tester",
                Position = new Point3D(100, 100, 0),
                Visible = true
            };
            World.AddMobile(player);
            World.Player = player;

            m_Fake = new FakeClientServices();
            ClientProxy.Bind(m_Fake);

            // Frischer LuaState pro Test (statisch — Initialize setzt alles neu auf).
            LuaEngineService.Initialize();
            LuaEngineService.ClearBreakpoints();
        }

        public void Dispose()
        {
            LuaEngineService.StopScript();
            LuaEngineService.ClearBreakpoints();
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

        // ------------------------------------------------------------ helpers

        /// <summary>Fuehrt das Script aus und wartet auf das Laufende-Ende
        /// (RunningStateChanged false); die Engine laeuft auf einem Task.</summary>
        private void RunAndWait(string script, int timeoutMs = 10000)
        {
            using var done = new ManualResetEventSlim(false);
            EventHandler<bool> handler = (s, running) =>
            {
                if (!running)
                    done.Set();
            };

            LuaEngineService.RunningStateChanged += handler;
            try
            {
                LuaEngineService.RunScript(script);
                Assert.True(done.Wait(timeoutMs), "Lua-Script nicht beendet (Timeout)");
            }
            finally
            {
                LuaEngineService.RunningStateChanged -= handler;
            }
        }

        private void AssertNoErrors()
        {
            Assert.True(LuaEngineService.Errors.Count == 0,
                "Lua-Fehler: " + string.Join("; ", LuaEngineService.Errors.Select(e => $"{e.Line}: {e.Message}")));
        }

        private string LastSpeechText()
        {
            byte[] speech = m_Fake.SentToServer.LastOrDefault(p => p[0] == 0xAD);
            Assert.NotNull(speech);
            return Encoding.BigEndianUnicode.GetString(speech);
        }

        // ------------------------------------------------------------ tests

        [Fact]
        public void Sandbox_blockt_dofile_load_io_und_os_execute()
        {
            RunAndWait(@"
if dofile == nil and load == nil and require == nil and io == nil and os.execute == nil then
    Player.Say('sandbox-ok')
end");

            AssertNoErrors();
            Assert.Contains("sandbox-ok", LastSpeechText());
        }

        [Fact]
        public void PlayerSay_sendet_0xAD_an_den_Server()
        {
            RunAndWait("Player.Say('hello-lua')");

            AssertNoErrors();
            Assert.Contains("hello-lua", LastSpeechText());
        }

        [Fact]
        public void Items_FindBySerial_liest_das_Weltmodell()
        {
            Item chest = new Item(ChestSerial) { ItemID = 0x0E43, Name = "chest", Hue = 5 };
            World.AddItem(chest);

            RunAndWait(@"
local it = Items.FindBySerial(0x40001102)
if it ~= nil and it.Name == 'chest' and it.Hue == 5 then
    Player.Say('item-ok')
end");

            AssertNoErrors();
            Assert.Contains("item-ok", LastSpeechText());
        }

        [Fact]
        public void Journal_Contains_findet_neue_Eintraege()
        {
            Assistant.VScripts.Engine.Journal.Add("Gandalf", "You shall not pass", 0x3B2,
                MessageType.Regular, true);

            RunAndWait(@"
if Journal.Contains('shall not pass') and not Journal.Contains('flying purple monkey') then
    Player.Say('journal-ok')
end");

            AssertNoErrors();
            Assert.Contains("journal-ok", LastSpeechText());
        }

        [Fact]
        public void Gumps_laufen_ueber_die_GumpInfo_Paketsicht()
        {
            // GumpID 0x1234 vom Absender 0x40009999 mit einer Textzeile.
            // Client-Semantik (PacketHandlers.CreateGump): Scripts adressieren
            // Gumps ueber die GUMP-ID (Gump.ServerSerial = gumpID), NICHT ueber
            // die Absender-Serial — genau das war der Live-Bug (WaitForGump
            // triggerte nie, weil der Shim beide vertauscht hatte).
            var info = new PlayerData.GumpInfo(0x40009999);
            info.Strings.Add("Hello Gump");
            World.Player.GumpList[0x1234] = info;

            RunAndWait(@"
if Gumps.HasGump(0x1234) and not Gumps.HasGump(0x40009999) then
    if Gumps.WaitForGump(0x1234, 1000) then
        local g = Gumps.GetGump(0x1234)
        if g ~= nil and g.Serial == 0x1234 and g.Texts[1] == 'Hello Gump' then
            Player.Say('gump-ok')
        end
    end
end");

            AssertNoErrors();
            Assert.Contains("gump-ok", LastSpeechText());
        }

        [Fact]
        public void Gumps_Reply_sendet_0xB1_erst_nach_dem_Queue_Pump()
        {
            World.Player.GumpList[0x1234] = new PlayerData.GumpInfo(0x40009999);

            RunAndWait("Gumps.Reply(0x1234, 3)");
            AssertNoErrors();

            // Reply legt den Klick nur in die Main-Thread-Queue — im Client
            // pumpt der GameController, in Razor RazorPlugin.OnTick. Ohne den
            // Pump ging live NIE ein Button-Klick raus (User-Bugreport).
            Assert.DoesNotContain(m_Fake.SentToServer, p => p[0] == 0xB1);

            Assistant.LuaEngine.API.LuaGumpsAPI.ProcessMainThreadQueue();

            byte[] resp = Assert.Single(m_Fake.SentToServer, p => p[0] == 0xB1);
            // 0xB1: id(1) len(2) absender(4) gumpId(4) button(4) ...
            uint sender = (uint) ((resp[3] << 24) | (resp[4] << 16) | (resp[5] << 8) | resp[6]);
            uint gumpId = (uint) ((resp[7] << 24) | (resp[8] << 16) | (resp[9] << 8) | resp[10]);
            int button = (resp[11] << 24) | (resp[12] << 16) | (resp[13] << 8) | resp[14];

            Assert.Equal(0x40009999u, sender);
            Assert.Equal(0x1234u, gumpId);
            Assert.Equal(3, button);
            Assert.False(World.Player.GumpList.ContainsKey(0x1234));
        }

        [Fact]
        public void PickUp_und_Drop_paaren_sich_ueber_die_Lift_Queue()
        {
            // Client-Semantik: PickUp haelt das Item SOFORT am Cursor; unser
            // Lift laeuft asynchron ueber die DragDropManager-Queue. Drop darf
            // deshalb nicht an Holding==null scheitern, sondern paart sich mit
            // dem wartenden Lift (Live-Bug: Pickup ging "manchmal" nicht).
            Item backpack = new Item(0x40002200) { ItemID = 0x0E75, Layer = Layer.Backpack };
            backpack.Container = World.Player.Serial;
            World.AddItem(backpack);

            // Boden-Item NEBEN dem Spieler — ProcessNext verwirft Ground-Lifts
            // ueber 3 Felder Distanz still (CE-Verhalten).
            Item board = new Item(ChestSerial)
            {
                ItemID = 0x1BD7, Amount = 10, Name = "boards",
                Position = new Point3D(101, 100, 0)
            };
            World.AddItem(board);
            Item.UpdateContainers();

            // Kein Pause dazwischen — haerter als das reale Script (Pause 600).
            RunAndWait(@"
Player.PickUp(0x40001102, 1)
if Player.DropInBackpack() then
    Player.Say('paired-ok')
end");

            AssertNoErrors();
            Assert.Contains("paired-ok", LastSpeechText());
        }

        [Fact]
        public void UI_API_baut_Fenster_mit_AutoLayout_und_Methoden()
        {
            RunAndWait(@"
local win = UI.Window('Test Window')
local lbl = win:Label('Hello')
lbl:SetText('Hello World')
lbl:SetColor('#FF8800')
local chk = win:Checkbox('Check me', true)
local txt = win:TextBox('vorbelegt')
win:Separator()
local row = win:Row()
row:Button('A')
row:Button('B')
if win:IsOpen() and chk:IsChecked() and txt:GetText() == 'vorbelegt'
   and lbl:GetText() == 'Hello World' then
    Player.Say('ui-ok')
end");

            AssertNoErrors();
            Assert.Contains("ui-ok", LastSpeechText());

            // Script-Ende zerstoert die Script-Fenster.
            Assert.Equal(0, Assistant.LuaEngine.UI.ScriptUIManager.GetWindowCount());
        }

        [Fact]
        public void UI_Button_Callback_feuert_automatisch_in_win_Run()
        {
            // Kein manuelles Dispatch: der Klick kommt vom UI-Thread in die
            // Queue, win:Run() pumpt ihn in den OnClick-Callback; win:Close()
            // im Callback beendet die Run-Schleife und damit das Script.
            const string script = @"
local win = UI.Window('cbwin')
win:Button('go', function()
    Player.Say('clicked-ok')
    win:Close()
end)
win:Run()";

            using var done = new ManualResetEventSlim(false);
            EventHandler<bool> handler = (s, running) =>
            {
                if (!running)
                    done.Set();
            };

            LuaEngineService.RunningStateChanged += handler;
            try
            {
                LuaEngineService.RunScript(script);

                // Warten bis das Script Fenster+Button angelegt hat.
                Assistant.LuaEngine.UI.UiButton button = null;
                for (int i = 0; i < 100 && button == null; i++)
                {
                    Thread.Sleep(20);
                    button = Assistant.LuaEngine.UI.ScriptUIManager.GetWindowsSnapshot()
                        .SelectMany(w => w.GetElementsSnapshot())
                        .OfType<Assistant.LuaEngine.UI.UiButton>()
                        .FirstOrDefault();
                }

                Assert.NotNull(button);
                button.PerformClick(); // "UI-Thread": Event in die Queue

                Assert.True(done.Wait(10000), "Lua-Script nicht beendet (Timeout)");
            }
            finally
            {
                LuaEngineService.RunningStateChanged -= handler;
            }

            AssertNoErrors();
            Assert.Contains("clicked-ok", LastSpeechText());
        }

        [Fact]
        public void SpellsCast_akzeptiert_Anzeigenamen_und_IDs()
        {
            // Der Rohstring 'Greater Heal' matcht den Enum-Wert GreaterHeal
            // nicht — Cast normalisiert jetzt (Leerzeichen/'/-/_ raus) und
            // akzeptiert zusaetzlich direkt eine Spell-ID.
            RunAndWait(@"
local a = Spells.Cast('Greater Heal')
local b = Spells.Cast('night sight')
local c = Spells.Cast(29)
local d = Spells.CastById(42)
Player.Say('a=' .. tostring(a) .. ' b=' .. tostring(b) .. ' c=' .. tostring(c) .. ' d=' .. tostring(d))");

            AssertNoErrors();
            Assert.Contains("a=true b=true c=true d=true", LastSpeechText());
            Assert.Equal(new[] { 29, 6, 29, 42 }, m_Fake.CastSpells);
        }

        [Fact]
        public void UI_Window_nimmt_Position_und_Groesse_optional()
        {
            // Script pausiert nach dem Aufbau — die Modelle werden WAEHREND
            // des Laufs geprueft (Script-Ende zerstoert sie).
            const string script = @"
UI.Window('positioned', 50, 60, 300, 200)
UI.Window{ title = 'via-table', x = 10, y = 20, width = 111, height = 222 }
UI.Window('auto')
Pause(1000)";

            using var done = new ManualResetEventSlim(false);
            EventHandler<bool> handler = (s, running) =>
            {
                if (!running)
                    done.Set();
            };

            LuaEngineService.RunningStateChanged += handler;
            try
            {
                LuaEngineService.RunScript(script);

                Assistant.LuaEngine.UI.UiWindow[] windows = null;
                for (int i = 0; i < 100 && (windows == null || windows.Length < 3); i++)
                {
                    Thread.Sleep(20);
                    windows = Assistant.LuaEngine.UI.ScriptUIManager.GetWindowsSnapshot();
                }

                Assert.NotNull(windows);
                Assert.Equal(3, windows.Length);

                var positioned = windows.Single(w => w.Title == "positioned");
                Assert.Equal(50, positioned.X);
                Assert.Equal(60, positioned.Y);
                Assert.Equal(300, positioned.WindowWidth);
                Assert.Equal(200, positioned.WindowHeight);

                var viaTable = windows.Single(w => w.Title == "via-table");
                Assert.Equal(10, viaTable.X);
                Assert.Equal(20, viaTable.Y);
                Assert.Equal(111, viaTable.WindowWidth);
                Assert.Equal(222, viaTable.WindowHeight);

                var auto = windows.Single(w => w.Title == "auto");
                Assert.Equal(-1, auto.X);        // Position: System
                Assert.Equal(0, auto.WindowWidth); // Groesse: SizeToContent

                Assert.True(done.Wait(10000), "Lua-Script nicht beendet (Timeout)");
            }
            finally
            {
                LuaEngineService.RunningStateChanged -= handler;
            }

            AssertNoErrors();
        }

        [Fact]
        public void UI_kurzer_Callback_feuert_waehrend_ein_langer_Callback_laeuft()
        {
            // Backend-Job-Handling: der lange Callback wartet in Pause(),
            // waehrenddessen stellt der verschachtelte Pump den Stop-Klick zu —
            // Scripts brauchen KEINE eigene Job-Queue mehr.
            const string script = @"
local win = UI.Window('nestwin')
local stop = false
win:Button('long', function()
    while not stop do Pause(50) end
    Player.Say('long-done')
    win:Close()
end)
win:Button('stop', function() stop = true end)
win:Run()";

            using var done = new ManualResetEventSlim(false);
            EventHandler<bool> handler = (s, running) =>
            {
                if (!running)
                    done.Set();
            };

            LuaEngineService.RunningStateChanged += handler;
            try
            {
                LuaEngineService.RunScript(script);

                Assistant.LuaEngine.UI.UiButton longButton = null, stopButton = null;
                for (int i = 0; i < 100 && (longButton == null || stopButton == null); i++)
                {
                    Thread.Sleep(20);
                    var buttons = Assistant.LuaEngine.UI.ScriptUIManager.GetWindowsSnapshot()
                        .SelectMany(w => w.GetElementsSnapshot())
                        .OfType<Assistant.LuaEngine.UI.UiButton>()
                        .ToArray();
                    longButton = buttons.FirstOrDefault(b => b.Text == "long");
                    stopButton = buttons.FirstOrDefault(b => b.Text == "stop");
                }

                Assert.NotNull(longButton);
                Assert.NotNull(stopButton);

                longButton.PerformClick();   // startet die Endlos-Warteschleife
                Thread.Sleep(300);           // Callback haengt jetzt in Pause()
                stopButton.PerformClick();   // muss VERSCHACHTELT zugestellt werden

                Assert.True(done.Wait(10000), "Lua-Script nicht beendet (Timeout) — Stop kam nicht durch");
            }
            finally
            {
                LuaEngineService.RunningStateChanged -= handler;
            }

            AssertNoErrors();
            Assert.Contains("long-done", LastSpeechText());
        }

        [Fact]
        public void Config_speichert_und_laedt_alle_Datentypen()
        {
            RunAndWait(@"
local cfg = {
    name = 'Loot ""Master""',
    multiline = 'a\nb',
    count = 42,
    ratio = 1.5,
    negative = -7,
    serial = 0x40001234,
    enabled = true,
    disabled = false,
    nested = { a = 1, b = 'two', deep = { ok = true } },
    list = { 10, 20, 30 },
}
if not Config.Save('lua_test_cfg', cfg) then
    Player.Say('save-failed')
    return
end
local back = Config.Load('lua_test_cfg')
if back ~= nil
   and back.name == 'Loot ""Master""'
   and back.multiline == 'a\nb'
   and back.count == 42
   and back.ratio == 1.5
   and back.negative == -7
   and back.serial == 0x40001234
   and back.enabled == true
   and back.disabled == false
   and back.nested.a == 1 and back.nested.b == 'two' and back.nested.deep.ok == true
   and #back.list == 3 and back.list[2] == 20
   and Config.Exists('lua_test_cfg')
   and Config.Delete('lua_test_cfg')
   and not Config.Exists('lua_test_cfg') then
    Player.Say('config-ok')
end");

            AssertNoErrors();
            Assert.Contains("config-ok", LastSpeechText());
        }

        [Fact]
        public void UI_Label_Binding_aktualisiert_sich_waehrend_Pause()
        {
            // Variablen-Binding: Label mit Funktion — Pause() pumpt die
            // Auswertung automatisch (Standard-Intervall 200ms).
            RunAndWait(@"
local win = UI.Window('bindwin')
local lbl = win:Label(function() return 'HP: ' .. Player.Hits end)
Pause(600)
Player.Say('bind=' .. lbl:GetText())");

            AssertNoErrors();
            Assert.Contains("bind=HP: 0", LastSpeechText());
        }

        [Fact]
        public void DebugInjektion_meldet_jede_Quellzeile_1basiert()
        {
            var lines = new List<int>();
            EventHandler<int> handler = (s, line) =>
            {
                lock (lines)
                {
                    lines.Add(line);
                }
            };

            LuaEngineService.CurrentLineChanged += handler;
            try
            {
                RunAndWait("local a = 1\nlocal b = 2\nPlayer.Say('lines-ok')");
            }
            finally
            {
                LuaEngineService.CurrentLineChanged -= handler;
            }

            AssertNoErrors();

            lock (lines)
            {
                // __debug_line(n) wird pro Statement-Zeile injiziert (1-basiert);
                // das abschliessende -1 kommt vom Script-Ende.
                Assert.Contains(1, lines);
                Assert.Contains(2, lines);
                Assert.Contains(3, lines);
                Assert.Equal(-1, lines.Last());
            }
        }

        [Fact]
        public void Syntaxfehler_liefert_ScriptErrorsChanged_mit_0basierter_Zeile()
        {
            List<LuaError> reported = null;
            EventHandler<List<LuaError>> handler = (s, errors) => reported = errors;

            LuaEngineService.ScriptErrorsChanged += handler;
            try
            {
                RunAndWait("local a = 1\nthis is not lua at all(");
            }
            finally
            {
                LuaEngineService.ScriptErrorsChanged -= handler;
            }

            Assert.NotNull(reported);
            Assert.NotEmpty(reported);
            // Editor-Konvention: 0-basiert (Client ErrorMarkerManager).
            Assert.All(reported, e => Assert.True(e.Line >= 0));
        }
    }
}
