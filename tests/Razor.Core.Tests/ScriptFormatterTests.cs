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

// UOSagas-Razor: Tests fuer den Razor-Script-Autoformatter (Phase 4a,
// VSCode Shift+Alt+F): Block-Einrueckung nach Kontrollfluss.

using Razor.UI.Editor;
using Xunit;

namespace Razor.Core.Tests
{
    public class ScriptFormatterTests
    {
        [Fact]
        public void Format_IndentsBlocks()
        {
            string input = string.Join("\n",
                "if hp < 50",
                "say 'low'",
                "elseif hp < 80",
                "say 'mid'",
                "else",
                "say 'high'",
                "endif",
                "while not dead",
                "wait 100",
                "endwhile");

            string expected = string.Join(System.Environment.NewLine,
                "if hp < 50",
                "    say 'low'",
                "elseif hp < 80",
                "    say 'mid'",
                "else",
                "    say 'high'",
                "endif",
                "while not dead",
                "    wait 100",
                "endwhile");

            Assert.Equal(expected, RazorScriptLanguage.Instance.Format(input));
        }

        [Fact]
        public void Format_NestedLoops()
        {
            string input = string.Join("\n",
                "for 3",
                "if 1 = 1",
                "sysmsg 'x'",
                "endif",
                "endfor");

            string expected = string.Join(System.Environment.NewLine,
                "for 3",
                "    if 1 = 1",
                "        sysmsg 'x'",
                "    endif",
                "endfor");

            Assert.Equal(expected, RazorScriptLanguage.Instance.Format(input));
        }

        [Fact]
        public void Format_UnbalancedEnd_ClampsAtZero()
        {
            string input = "endif\nsay 'ok'";

            string expected = "endif" + System.Environment.NewLine + "say 'ok'";

            Assert.Equal(expected, RazorScriptLanguage.Instance.Format(input));
        }
    }
}
