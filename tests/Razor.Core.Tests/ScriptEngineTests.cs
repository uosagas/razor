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

// UOSagas-Razor: Tests fuer Phase 4a — Razor-Script-Engine (Lexer/Interpreter).
// Verifiziert die 1:1-Portierung des CE-Engine-Kerns: Grammatik (if/while/
// for/foreach), Modifier (@/!), Variablen/Listen/Timer, Pause/Timeout-
// Scheduler, Alias-Aufloesung sowie die Registrierung der vollen
// Sprachoberflaeche (88 Commands / 52 Expressions / 8 Aliases).

using System;
using System.Collections.Generic;
using Assistant.Scripts;
using Assistant.Scripts.Engine;
using Xunit;

namespace Razor.Core.Tests
{
    [Collection("ConfigSequential")]
    public class ScriptEngineTests
    {
        /// <summary>Fuehrt ein Script synchron bis zum Ende aus (max. Schritte als Schutz).</summary>
        private static void Run(string[] lines, int maxSteps = 10000)
        {
            Interpreter.StopScript();

            Script script = new Script(Lexer.Lex(lines));
            Interpreter.StartScript(script);

            int steps = 0;
            while (Interpreter.ExecuteScript())
            {
                if (++steps > maxSteps)
                {
                    Interpreter.StopScript();
                    throw new InvalidOperationException("Script terminiert nicht");
                }
            }
        }

        [Fact]
        public void Lexer_ParsesControlFlow()
        {
            // Repraesentatives CE-Script: if/elseif/else, while, for, Modifier.
            ASTNode root = Lexer.Lex(new[]
            {
                "// Kommentar",
                "# auch Kommentar",
                "if hp < 50",
                "    say 'low'",
                "elseif hp < 80 and stam > 10",
                "    say 'mid'",
                "else",
                "    say 'high'",
                "endif",
                "while not dead",
                "    wait 100",
                "endwhile",
                "for 5",
                "    @attack! 0x1234",
                "endfor",
                "stop"
            });

            Assert.NotNull(root);
            Assert.Equal(ASTNodeType.SCRIPT, root.Type);
            Assert.NotNull(root.FirstChild());
        }

        [Fact]
        public void Lexer_SyntaxError_ReportsLine()
        {
            SyntaxError err = Assert.Throws<SyntaxError>(() => Lexer.Lex(new[]
            {
                "say 'ok'",
                "if" // Ausdruck fehlt
            }));

            Assert.Equal(1, err.LineNumber);
        }

        [Fact]
        public void Interpreter_ExecutesCommands_InOrder()
        {
            var calls = new List<string>();
            Interpreter.RegisterCommandHandler("t_first", (c, a, q, f) => { calls.Add("first"); return true; });
            Interpreter.RegisterCommandHandler("t_second", (c, a, q, f) => { calls.Add("second"); return true; });

            Run(new[] { "t_first", "t_second" });

            Assert.Equal(new[] { "first", "second" }, calls);
        }

        [Fact]
        public void Interpreter_QuietAndForce_ModifiersArriveAtHandler()
        {
            bool quiet = false, force = false;
            Interpreter.RegisterCommandHandler("t_mod", (c, a, q, f) =>
            {
                quiet = q;
                force = f;
                return true;
            });

            Run(new[] { "@t_mod!" });

            Assert.True(quiet);
            Assert.True(force);
        }

        [Fact]
        public void Interpreter_IfElse_TakesCorrectBranch()
        {
            var calls = new List<string>();
            Interpreter.RegisterCommandHandler("t_branch", (c, a, q, f) => { calls.Add(a[0].AsString()); return true; });
            Interpreter.RegisterExpressionHandler<int>("t_five", (e, a, q, f) => 5);

            Run(new[]
            {
                "if t_five > 10",
                "    t_branch 'big'",
                "elseif t_five > 3",
                "    t_branch 'mid'",
                "else",
                "    t_branch 'small'",
                "endif"
            });

            Assert.Equal(new[] { "mid" }, calls);
        }

        [Fact]
        public void Interpreter_ForLoop_RunsExactly()
        {
            int count = 0;
            Interpreter.RegisterCommandHandler("t_count", (c, a, q, f) => { count++; return true; });

            Run(new[] { "for 3", "    t_count", "endfor" });

            Assert.Equal(3, count);
        }

        [Fact]
        public void Interpreter_WhileLoop_RunsUntilConditionFalse()
        {
            int count = 0;
            Interpreter.RegisterCommandHandler("t_loop2", (c, a, q, f) => { count++; return true; });
            Interpreter.RegisterExpressionHandler<int>("t_count2", (e, a, q, f) => count);

            Run(new[] { "while t_count2 < 4", "    t_loop2", "endwhile" });

            Assert.Equal(4, count);
        }

        [Fact]
        public void Interpreter_Break_LeavesLoop()
        {
            int count = 0;
            Interpreter.RegisterCommandHandler("t_loop3", (c, a, q, f) => { count++; return true; });
            Interpreter.RegisterExpressionHandler<int>("t_count3", (e, a, q, f) => count);

            Run(new[]
            {
                "while 1 = 1",
                "    t_loop3",
                "    if t_count3 >= 2",
                "        break",
                "    endif",
                "endwhile"
            });

            Assert.Equal(2, count);
        }

        [Fact]
        public void Interpreter_Foreach_IteratesList()
        {
            var seen = new List<string>();
            Interpreter.RegisterCommandHandler("t_item", (c, a, q, f) => { seen.Add(a[0].AsString()); return true; });

            Interpreter.DestroyList("fruits");
            Interpreter.CreateList("fruits");
            Interpreter.PushList("fruits", new Variable("apple"), false, false);
            Interpreter.PushList("fruits", new Variable("pear"), false, false);

            Run(new[] { "foreach x in fruits", "    t_item x", "endfor" });

            Assert.Equal(new[] { "apple", "pear" }, seen);

            Interpreter.DestroyList("fruits");
        }

        [Fact]
        public void Interpreter_Variables_ScopeAndResolution()
        {
            Interpreter.StopScript();
            Interpreter.SetVariable("t_var", "42", true);

            Assert.Equal(42, new Variable("t_var").AsInt());
            Assert.Equal("42", new Variable("t_var").AsString());

            Interpreter.ClearAlias("t_var");
        }

        [Fact]
        public void Interpreter_Alias_ResolvesToSerial()
        {
            Interpreter.RegisterAliasHandler("t_alias", a => 0x40001234);

            Assert.Equal(0x40001234u, new Variable("t_alias").AsSerial());

            Interpreter.UnregisterAliasHandler("t_alias");
        }

        [Fact]
        public void Interpreter_Timers_CreateAndExpire()
        {
            Interpreter.CreateTimer("t_timer");
            Assert.True(Interpreter.TimerExists("t_timer"));

            Interpreter.SetTimer("t_timer", 5000); // startete "vor 5s"
            Assert.True(Interpreter.GetTimer("t_timer").TotalMilliseconds >= 5000);

            Interpreter.RemoveTimer("t_timer");
            Assert.False(Interpreter.TimerExists("t_timer"));
        }

        [Fact]
        public void Interpreter_Pause_BlocksUntilElapsed()
        {
            Interpreter.StopScript();

            bool after = false;
            Interpreter.RegisterCommandHandler("t_pause", (c, a, q, f) =>
            {
                Interpreter.Pause(50);
                return true;
            });
            Interpreter.RegisterCommandHandler("t_after", (c, a, q, f) => { after = true; return true; });

            Script script = new Script(Lexer.Lex(new[] { "t_pause", "t_after" }));
            Interpreter.StartScript(script);

            // Direkt nach Start: Pause aktiv, t_after noch nicht gelaufen.
            Interpreter.ExecuteScript();
            Assert.False(after);

            // Nach Ablauf der Pause laeuft das Script weiter.
            System.Threading.Thread.Sleep(80);
            while (Interpreter.ExecuteScript())
            {
            }

            Assert.True(after);
        }

        [Fact]
        public void Interpreter_UnknownCommand_Throws()
        {
            Interpreter.StopScript();

            Script script = new Script(Lexer.Lex(new[] { "definitely_not_a_command_xyz" }));

            Assert.Throws<RunTimeError>(() =>
            {
                Interpreter.StartScript(script);
                while (Interpreter.ExecuteScript())
                {
                }
            });

            Interpreter.StopScript();
        }

        [Fact]
        public void Language_Surface_IsComplete()
        {
            // Volle 1:1-Sprachoberflaeche: alle CE-Registrierungen vorhanden.
            Commands.Register();
            AgentCommands.Register();
            SpeechCommands.Register();
            TargetCommands.Register();
            Aliases.Register();
            Expressions.Register();

            string[] commands =
            {
                "attack", "cast", "dress", "undress", "dclicktype", "dclick", "usetype", "useobject",
                "drop", "droprelloc", "lift", "lifttype", "waitforgump", "gumpresponse", "gumpclose",
                "menu", "menuresponse", "waitformenu", "promptresponse", "waitforprompt", "hotkey",
                "overhead", "headmsg", "sysmsg", "clearsysmsg", "clearjournal", "wait", "pause",
                "waitforsysmsg", "wfsysmsg", "setability", "setlasttarget", "lasttarget", "skill",
                "useskill", "walk", "potion", "script", "setvar", "setvariable", "unsetvar",
                "unsetvariable", "stop", "clearall", "clearhands", "virtue", "random", "cleardragdrop",
                "interrupt", "sound", "music", "classicuo", "cuo", "rename", "getlabel", "ignore",
                "unignore", "clearignore", "cooldown", "poplist", "pushlist", "removelist",
                "createlist", "clearlist", "settimer", "removetimer", "createtimer",
                "useonce", "organizer", "organize", "org", "restock", "scav", "scavenger", "sell",
                "say", "msg", "yell", "whisper", "emote", "guild", "alliance",
                "target", "targettype", "targetrelloc", "targetloc", "waitfortarget", "wft"
            };

            foreach (string cmd in commands)
                Assert.True(Interpreter.GetCommandHandler(cmd) != null, $"Command '{cmd}' fehlt");

            string[] expressions =
            {
                "stam", "maxstam", "hp", "hits", "maxhp", "maxhits", "mana", "maxmana", "poisoned",
                "hidden", "mounted", "rhandempty", "lhandempty", "dead", "str", "int", "dex",
                "weight", "maxweight", "skill", "count", "counter", "insysmsg", "insysmessage",
                "findtype", "findbuff", "finddebuff", "position", "queued", "varexist", "varexists",
                "followers", "maxfollowers", "targetexists", "diffweight", "diffhits", "diffhp",
                "diffstam", "diffmana", "name", "paralyzed", "invuln", "invul", "blessed", "warmode",
                "itemcount", "poplist", "listexists", "list", "inlist", "timer", "timerexists"
            };

            foreach (string expr in expressions)
                Assert.True(Interpreter.GetExpressionHandler(expr) != null, $"Expression '{expr}' fehlt");

            string[] aliases = { "backpack", "last", "lasttarget", "lastobject", "self", "righthand", "lefthand", "hand" };

            foreach (string alias in aliases)
                Assert.True(Interpreter.AliasHandlerExist(alias), $"Alias '{alias}' fehlt");
        }
    }
}
