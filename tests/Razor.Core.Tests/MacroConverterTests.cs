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

// UOSagas-Razor: Tests fuer den Macro-zu-Script-Konverter (Sagas-Zusatz).

using System;
using System.Globalization;
using System.IO;
using System.Linq;
using Assistant.Macros;
using Assistant.VScripts.Core;
using Assistant.VScripts.Nodes;
using Xunit;

namespace Razor.Core.Tests
{
    public class MacroConverterTests : IDisposable
    {
        private readonly string m_TempDir;
        private readonly CultureInfo m_OldCulture;

        public MacroConverterTests()
        {
            m_OldCulture = CultureInfo.CurrentCulture;
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;

            m_TempDir = Path.Combine(Path.GetTempPath(), "RazorConvTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(m_TempDir);
        }

        public void Dispose()
        {
            CultureInfo.CurrentCulture = m_OldCulture;
            try
            {
                Directory.Delete(m_TempDir, true);
            }
            catch
            {
            }
        }

        private Macro LoadMacro(params string[] macroLines)
        {
            string path = Path.Combine(m_TempDir, Guid.NewGuid().ToString("N") + ".macro");
            File.WriteAllLines(path, macroLines);
            Macro m = new Macro(path);
            m.Load();
            return m;
        }

        [Fact]
        public void ToRazorScript_NutztToScriptMitEinrueckung()
        {
            Macro m = LoadMacro(
                "Assistant.Macros.IfAction|0|1|90",
                "Assistant.Macros.DoubleClickAction|1074121353|3702",
                "Assistant.Macros.EndIfAction",
                "Assistant.Macros.WaitForTargetAction|30");

            string script = MacroConverter.ToRazorScript(m);

            Assert.Contains("if hits >=", script);
            Assert.Contains("    dclick ", script);
            Assert.Contains("endif", script);
            Assert.Contains("waitfortarget", script);
        }

        [Fact]
        public void ToLua_MapptKernaktionen()
        {
            Macro m = LoadMacro(
                "!Loop",
                "Assistant.Macros.DoubleClickAction|1074121353|3702",
                "Assistant.Macros.PauseAction|00:00:01",
                "Assistant.Macros.WaitForGumpAction|1988087633|False|300",
                "Assistant.Macros.GumpResponseAction|2|1|3|0",
                "Assistant.Macros.WaitForTargetAction|30",
                "Assistant.Macros.LastTargetAction",
                "Assistant.Macros.LiftAction|1080000001|5|3821",
                "Assistant.Macros.DropAction|1079000001|(117, 65, 0)|0",
                "Assistant.Macros.UseSkillAction|21",
                "Assistant.Macros.BookCastSpellAction|29|1074121353",
                "Assistant.Macros.SpeechAction|0|52|3|ENU|0|bank");

            string lua = MacroConverter.ToLua(m);

            Assert.Contains("while true do", lua);                 // !Loop
            Assert.Contains("Player.UseObject(0x4005CA89)", lua);
            Assert.Contains("Pause(1000)", lua);
            Assert.Contains("Gumps.WaitForGump(0x767FCF51, 300000)", lua);
            Assert.Contains("Gumps.Reply(0x767FCF51, 2, { 3 })", lua); // Id aus WaitForGump uebernommen
            Assert.Contains("Targeting.WaitForTarget(30000)", lua);
            Assert.Contains("Targeting.Last()", lua);
            Assert.Contains("Player.PickUp(0x405F7E01, 5)", lua);
            Assert.Contains("Player.DropInContainer(0x40503BC1)", lua);
            Assert.Contains("Skills.Use('Hiding')", lua);
            Assert.Contains("Spells.CastById(29)", lua);
            Assert.Contains("Player.Say('bank')", lua);
        }

        [Fact]
        public void ToLua_KontrollflussWirdUebersetzt()
        {
            Macro m = LoadMacro(
                "Assistant.Macros.IfAction|1|0|20",
                "Assistant.Macros.PauseAction|00:00:01",
                "Assistant.Macros.ElseAction",
                "Assistant.Macros.ForAction|5",
                "Assistant.Macros.EndForAction",
                "Assistant.Macros.EndIfAction");

            string lua = MacroConverter.ToLua(m);

            Assert.Contains("if Player.Mana <= 20 then", lua);
            Assert.Contains("else", lua);
            Assert.Contains("for i = 1, 5 do", lua);
            Assert.Contains("end", lua);
        }

        [Fact]
        public void ToVScript_BautLineareKetteMitFlowLinks()
        {
            Macro m = LoadMacro(
                "Assistant.Macros.DoubleClickAction|1074121353|3702",
                "Assistant.Macros.PauseAction|00:00:01",
                "Assistant.Macros.BookCastSpellAction|29|1074121353",
                "Assistant.Macros.HotKeyAction|1088|"); // nicht abbildbar

            NodeGraph graph = MacroConverter.ToVScript(m, out var skipped);

            Assert.Single(graph.Nodes.OfType<StartNode>());
            Assert.Single(graph.Nodes.OfType<UseItemNode>());
            Assert.Single(graph.Nodes.OfType<DelayNode>());
            CastSpellNode cast = Assert.Single(graph.Nodes.OfType<CastSpellNode>());
            Assert.Equal(29, cast.SelectedSpellId);

            // Kette: Start -> Use -> Delay -> Cast = 3 Flow-Links.
            Assert.Equal(3, graph.Links.Count);

            // Nicht abbildbare Action landet in skipped + CommentBox.
            Assert.Single(skipped);
            CommentBox box = Assert.Single(graph.CommentBoxes);
            Assert.Contains("NOT converted", box.Title);
        }
    }
}
