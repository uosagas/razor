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

// UOSagas-Razor: Kompatibilitaetstests fuer das Razor-CE-.macro-Format.
// Zeilensyntax exakt aus Actions.cs Serialize() abgeleitet:
//   TypeName|arg|arg..., !Loop, '#'/'//'-Kommentare.

using System;
using System.Globalization;
using System.IO;
using System.Linq;
using Assistant.Macros;
using Xunit;

namespace Razor.Core.Tests
{
    public class MacroCompatibilityTests : IDisposable
    {
        // Repraesentatives Razor-CE-Macro: !Loop, Kommentar, If/Else/EndIf,
        // SpeechAction, Waits, For-Schleife, HotKey und ein unbekannter Typ.
        private static readonly string[] MacroFixtureLines =
        {
            "!Loop",
            "// Heal-Makro fuer Sagas",
            "Assistant.Macros.SpeechAction|0|52|3|ENU|0|bank",
            "Assistant.Macros.IfAction|0|1|90",
            "Assistant.Macros.BookCastSpellAction|29|1074121353",
            "Assistant.Macros.WaitForTargetAction|30",
            "Assistant.Macros.LastTargetAction",
            "Assistant.Macros.ElseAction",
            "Assistant.Macros.PauseAction|00:00:01",
            "Assistant.Macros.EndIfAction",
            "Assistant.Macros.DoubleClickAction|1074121353|3702",
            "Assistant.Macros.LiftAction|1080000001|1|3821",
            "Assistant.Macros.DropAction|0x40123456|(1000, 2000, 5)|0",
            "Assistant.Macros.AbsoluteTargetAction|0|0|1074121353|1000|2000|5|3702",
            "Assistant.Macros.WaitForGumpAction|1988087633|False|300",
            "Assistant.Macros.GumpResponseAction|2|0|1|1&text entry",
            "Assistant.Macros.HotKeyAction|1088|",
            "Assistant.Macros.UseSkillAction|13",
            "Assistant.Macros.SetAbilityAction|1",
            "Assistant.Macros.WalkAction|2",
            "Assistant.Macros.ForAction|5",
            "Assistant.Macros.TargetTypeAction|False|3702",
            "Assistant.Macros.EndForAction",
            "Assistant.Macros.WhileAction|4|-1|full backpack",
            "Assistant.Macros.OverheadMessageAction|55|Fertig!",
            "Assistant.Macros.EndWhileAction",
            "Assistant.Macros.ContextMenuAction|0x0|1|0",
            "Assistant.Macros.PromptAction|antwort",
            "Assistant.Macros.WaitForPromptAction|0|False|300",
            "Assistant.Macros.SetMacroVariableTargetAction|mybag",
            "Assistant.Macros.AbsoluteTargetVariableAction|mybag",
            "Assistant.Macros.UnDressAction|winter|255",
            "Assistant.Macros.DressAction|kampf",
            "Assistant.Macros.SomeFutureAction|1|2|drei"
        };

        private readonly string m_TempDir;
        private readonly CultureInfo m_OldCulture;

        public MacroCompatibilityTests()
        {
            m_OldCulture = CultureInfo.CurrentCulture;
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;

            m_TempDir = Path.Combine(Path.GetTempPath(), "RazorMacroTests_" + Guid.NewGuid().ToString("N"));
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

        private string WriteFixture(string name, string[] lines)
        {
            string path = Path.Combine(m_TempDir, name + ".macro");
            File.WriteAllLines(path, lines);
            return path;
        }

        [Fact]
        public void LoadSave_LinesIdentical()
        {
            string inPath = WriteFixture("roundtrip", MacroFixtureLines);
            string outPath = Path.Combine(m_TempDir, "roundtrip_out.macro");

            Macro m = new Macro(inPath);
            m.Load();

            Assert.True(m.Loaded);
            Assert.True(m.Loop);
            // !Loop ist keine Action; alle anderen Zeilen ergeben je eine Action
            Assert.Equal(MacroFixtureLines.Length - 1, m.Actions.Count);

            m.Filename = outPath;
            m.Save();

            string[] saved = File.ReadAllLines(outPath);
            Assert.Equal(MacroFixtureLines, saved);
        }

        [Fact]
        public void LoadSaveLoadSave_Idempotent()
        {
            string inPath = WriteFixture("idem", MacroFixtureLines);
            string out1 = Path.Combine(m_TempDir, "idem1.macro");
            string out2 = Path.Combine(m_TempDir, "idem2.macro");

            Macro m1 = new Macro(inPath);
            m1.Load();
            m1.Filename = out1;
            m1.Save();

            Macro m2 = new Macro(out1);
            m2.Load();
            m2.Filename = out2;
            m2.Save();

            Assert.Equal(File.ReadAllLines(out1), File.ReadAllLines(out2));
        }

        [Fact]
        public void UnknownActionType_IsPreservedRaw()
        {
            string inPath = WriteFixture("unknown", MacroFixtureLines);

            Macro m = new Macro(inPath);
            m.Load();

            UnknownMacroAction unknown = m.Actions.OfType<UnknownMacroAction>().Single();
            Assert.Equal("Assistant.Macros.SomeFutureAction|1|2|drei", unknown.RawLine);
            Assert.Equal("Assistant.Macros.SomeFutureAction", unknown.TypeName);
            Assert.Equal("Assistant.Macros.SomeFutureAction|1|2|drei", unknown.Serialize());
        }

        [Fact]
        public void Comment_RoundTrips()
        {
            string inPath = WriteFixture("comment", new[]
            {
                "// mein kommentar",
                "Assistant.Macros.LastTargetAction"
            });

            Macro m = new Macro(inPath);
            m.Load();

            MacroComment comment = Assert.IsType<MacroComment>(m.Actions[0]);
            Assert.Equal("mein kommentar", comment.Comment);
            Assert.Equal("// mein kommentar", comment.Serialize());
        }

        [Fact]
        public void NoLoop_DoesNotWriteLoopLine()
        {
            string inPath = WriteFixture("noloop", new[]
            {
                "Assistant.Macros.LastTargetAction",
                "Assistant.Macros.WaitForTargetAction|30"
            });
            string outPath = Path.Combine(m_TempDir, "noloop_out.macro");

            Macro m = new Macro(inPath);
            m.Load();
            Assert.False(m.Loop);

            m.Filename = outPath;
            m.Save();

            string[] saved = File.ReadAllLines(outPath);
            Assert.Equal(new[]
            {
                "Assistant.Macros.LastTargetAction",
                "Assistant.Macros.WaitForTargetAction|30"
            }, saved);
        }

        [Fact]
        public void ParameterlessActions_SerializeAsBareTypeName()
        {
            var actions = new MacroAction[]
            {
                new LastTargetAction(),
                new ElseAction(),
                new EndIfAction(),
                new EndForAction(),
                new EndWhileAction(),
                new StartDoWhileAction(),
                new SetLastTargetAction()
            };

            foreach (MacroAction a in actions)
            {
                Assert.Equal(a.GetType().FullName, a.Serialize());
            }
        }
    }
}
