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

// UOSagas-Razor: Praxis-Tests (Phase 4a) — repraesentative Outlands-Style
// Healing-Scripts laufen gegen eine Fake-World (Player + Backpack + Items).
// Ziel: NullReference-/RunTime-Fehler in Command-/Expression-Pfaden finden,
// BEVOR der User sie live trifft. Exceptions laufen hier ungefangen in den
// Test (Interpreter wird direkt gepumpt, nicht ueber ScriptManager.OnTick).

using System;
using System.Globalization;
using System.IO;
using System.Threading;
using Assistant;
using Assistant.HotKeys;
using Assistant.Scripts;
using Assistant.Scripts.Engine;
using Assistant.Scripts.Helpers;
using Xunit;

namespace Razor.Core.Tests
{
    [Collection("ConfigSequential")]
    public class ScriptRealWorldTests : IDisposable
    {
        private const uint PlayerSerial = 0x00000801;
        private const uint BackpackSerial = 0x40000802;
        private const uint BandageSerial = 0x40000803;

        private readonly string m_TempDir;
        private readonly CultureInfo m_OldCulture;
        private readonly FakeClientServices m_Fake;

        public ScriptRealWorldTests()
        {
            m_OldCulture = CultureInfo.CurrentCulture;
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;

            m_TempDir = Path.Combine(Path.GetTempPath(), "RazorScriptRealTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(m_TempDir);

            Config.Initialize(m_TempDir);
            Config.LoadLastProfile();

            PacketHandlers.Initialize();
            Assistant.Agents.Agent.Initialize();
            Assistant.Macros.MacroManager.Stop();
            ActionQueue.Stop();
            Targeting.Reset();
            Assistant.Core.SystemMessages.Messages.Clear();

            // Volle Sprachoberflaeche registrieren (idempotent).
            Commands.Register();
            AgentCommands.Register();
            SpeechCommands.Register();
            TargetCommands.Register();
            Aliases.Register();
            Expressions.Register();

            // Fake-World: Player mit Stats/Skills, Backpack, Bandagen.
            World.Clear();
            PlayerData player = new PlayerData(PlayerSerial)
            {
                Position = new Point3D(1000, 1000, 0),
                Hits = 60,
                HitsMax = 100,
                Mana = 50,
                ManaMax = 80,
                Stam = 90,
                StamMax = 100,
                Str = 90,
                Dex = 90,
                Int = 90,
                Weight = 100,
                MaxWeight = 300
            };
            World.AddMobile(player);
            World.Player = player;

            Item backpack = new Item(BackpackSerial) { ItemID = 0x0E75, Layer = Layer.Backpack };
            backpack.Container = player.Serial;
            World.AddItem(backpack);

            Item bandage = new Item(BandageSerial) { ItemID = 0x0E21, Amount = 25 };
            bandage.Container = backpack.Serial;
            World.AddItem(bandage);

            Item.UpdateContainers();

            m_Fake = new FakeClientServices();
            ClientProxy.Bind(m_Fake);
        }

        public void Dispose()
        {
            Interpreter.StopScript();
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

        /// <summary>
        /// Pumpt das Script bis zum Ende (mit kurzen Sleeps, damit kleine
        /// Timeouts/Pausen ablaufen). Wirft jede Command-Exception in den Test.
        /// </summary>
        private static void RunToEnd(string[] lines, int maxTicks = 4000)
        {
            Interpreter.StopScript();
            Script script = new Script(Lexer.Lex(lines));
            Interpreter.StartScript(script);

            int ticks = 0;
            while (Interpreter.ExecuteScript())
            {
                if (++ticks > maxTicks)
                {
                    Interpreter.StopScript();
                    throw new InvalidOperationException("Script haengt (Tick-Limit erreicht)");
                }

                Thread.Sleep(1);
            }
        }

        [Fact]
        public void Outlands_HealingScript_laeuft_ohne_Exceptions()
        {
            // Repraesentativer Querschnitt der Konstrukte aus den Outlands-
            // Healing-Scripts (uorazorscripts.com/tags/healing).
            RunToEnd(new[]
            {
                "// SUPER-Auto healing (repraesentativ)",
                "@setvar! healtarget 0x00000801",
                "if not dead and diffhits > 5",
                "    overhead 'healing!' 38",
                "    if skill 'magery' > 50 and mana >= 11",
                "        cast 'greater heal'",
                "        wft 5",
                "        target 'self'",
                "    else",
                "        dclicktype 0x0E21 backpack",
                "        wft 5",
                "        target 'self'",
                "        cooldown 'Bandage' 10",
                "    endif",
                "endif",
                "if poisoned",
                "    potion 'cure'",
                "endif",
                "if findbuff 'magic reflection'",
                "    sysmsg 'reflect up'",
                "endif",
                "if insysmsg 'You are frozen'",
                "    dclicktype 0x0E79 backpack",
                "endif",
                "if weight > maxweight",
                "    lifttype 0x0EED 100",
                "    droprelloc 0 0",
                "endif",
                "if hits < maxhits and targetexists",
                "    lasttarget",
                "endif",
                "wait 10"
            });
        }

        [Fact]
        public void SkillExpression_und_UseSkill_ohne_NRE()
        {
            // Regressionstest: Skills.SkillsByName war nicht lazy-initialisiert
            // -> NullReferenceException bei jedem skill-Zugriff.
            RunToEnd(new[]
            {
                "if skill 'healing' < 90",
                "    sysmsg 'skilling'",
                "endif",
                "useskill 'hiding'",
                "skill 'meditation'"
            });
        }

        [Fact]
        public void Targeting_Kommandos_ohne_LastTarget_ohne_NRE()
        {
            // lasttarget/attack ohne gesetztes Last Target darf nicht crashen.
            RunToEnd(new[]
            {
                "lasttarget",
                "attack 0x00000801",
                "target 'closest'",
                "target 'random' 'enemy'",
                "target 'next' 'monster'",
                "target 'cancel'",
                "target 'clear'"
            });
        }

        [Fact]
        public void Listen_Timer_Variablen_Kombination()
        {
            RunToEnd(new[]
            {
                "createlist 'potions'",
                "pushlist 'potions' 'heal'",
                "pushlist 'potions' 'cure'",
                "foreach pot in potions",
                "    sysmsg pot",
                "endfor",
                "removelist 'potions'",
                "createtimer 'bandage'",
                "settimer 'bandage' 5000",
                "if timer 'bandage' >= 5000",
                "    sysmsg 'ready'",
                "endif",
                "removetimer 'bandage'"
            });
        }

        [Fact]
        public void DclickType_nach_Name_warnt_statt_crasht()
        {
            // Ohne Tiledata (Fake liefert keine Namen) darf die Namenssuche
            // nur warnen — niemals crashen.
            RunToEnd(new[]
            {
                "dclicktype 'clean bandage' backpack",
                "targettype 'robe'",
                "lifttype 'apple' 3"
            });
        }

        /// <summary>Pumpt ein Endlos-Script (loop) fuer eine feste Dauer; Exceptions fliegen in den Test.</summary>
        private static void RunForDuration(string[] lines, int durationMs)
        {
            Interpreter.StopScript();
            Script script = new Script(Lexer.Lex(lines));
            Interpreter.StartScript(script);

            DateTime end = DateTime.UtcNow.AddMilliseconds(durationMs);
            while (DateTime.UtcNow < end)
            {
                if (!Interpreter.ExecuteScript())
                    break;

                Assistant.Timer.Slice(); // ActionQueue/DragDrop pumpen
                Thread.Sleep(1);
            }

            Interpreter.StopScript();
        }

        [Fact]
        public void Outlands_AutoHeal_Script_des_Users_laeuft()
        {
            // Exakt das vom User getestete Outlands-Script (uorazorscripts.com):
            // Outlands-Syntax findtype [source self/backpack] [hue] [any] [any],
            // %s%-Wildcard, bandaging, hotkey, getlabel, as-Variable, loop.
            UseHotKeys.Initialize(); // "Bandage Self"-Hotkey (idempotent)

            // Heal-Potion (3852 = 0x0F0C) in den Backpack -> findtype ... self trifft.
            Item healPot = new Item(0x40000905) { ItemID = 3852, Amount = 3 };
            healPot.Container = World.Player.Backpack.Serial;
            World.AddItem(healPot);
            Item.UpdateContainers();

            World.Player.Hits = 20; // hp < 25 -> Heal-Potion-Zweig aktiv

            RunForDuration(new[]
            {
                "@setvar! globalTimeout 650",
                "if not dead",
                "    if findtype \"clean bandage%s%\" backpack",
                "        if bandaging = 0",
                "            if hp != maxhp",
                "                hotkey 'Bandage self'",
                "                wait globalTimeout",
                "            endif",
                "        endif",
                "    endif",
                "    if paralyzed",
                "        overhead 'Paralyzed!' 38",
                "        if findtype \"pouch\" backpack 38 any any",
                "            say '[pouch'",
                "            wait globalTimeout",
                "        endif",
                "    endif",
                "    if poisoned",
                "        if findtype \"Orange Potion\" self",
                "            wait globalTimeout",
                "            overhead 'Drinking cure!' 48",
                "            potion 'cure'",
                "            wait globalTimeout",
                "        endif",
                "    endif",
                "    if hp < 25",
                "        if findtype 3852 self as PotsHeal",
                "            getlabel PotsHeal LabelPotsHeal",
                "            if 'next usable' in LabelPotsHeal",
                "                sysmsg 'Unable to use Heal Potion Yet!' 33",
                "            else",
                "                overhead 'Drinking heal!' 68",
                "                dclick PotsHeal",
                "                wait globalTimeout",
                "            endif",
                "        endif",
                "    endif",
                "else",
                "    pause 1000",
                "endif",
                "loop"
            }, 4000);
        }

        [Fact]
        public void FindType_OutlandsGrammatik()
        {
            // Heal-Potion in den Backpack — findtype 3852 self muss sie finden
            // (self = am Koerper inkl. Container-Inhalt).
            Item healPot = new Item(0x40000906) { ItemID = 3852, Amount = 2 };
            healPot.Container = World.Player.Backpack.Serial;
            World.AddItem(healPot);
            Item.UpdateContainers();

            Assistant.Core.SystemMessages.Messages.Clear();

            RunToEnd(new[]
            {
                "if findtype 3852 self as Pots",
                "    sysmsg 'found'",
                "endif",
                "if findtype 3852 backpack 0 any any",
                "    sysmsg 'found-any'",
                "endif",
                "if findtype 9999 self",
                "    sysmsg 'ghost'",
                "endif"
            });

            Assert.Contains(Assistant.Core.SystemMessages.Messages, m => m.Contains("found"));
            Assert.Contains(Assistant.Core.SystemMessages.Messages, m => m.Contains("found-any"));
            Assert.DoesNotContain(Assistant.Core.SystemMessages.Messages, m => m.Contains("ghost"));
        }

        [Fact]
        public void NameMatches_Wildcard()
        {
            Assert.True(CommandHelper.NameMatches("clean bandage", "clean bandage%s%"));
            Assert.True(CommandHelper.NameMatches("clean bandages", "clean bandage%s%"));
            Assert.False(CommandHelper.NameMatches("dirty bandage", "clean bandage%s%"));
            Assert.True(CommandHelper.NameMatches("pouch", "pouch"));
            Assert.False(CommandHelper.NameMatches(null, "pouch"));
        }

        [Fact]
        public void Outlands_Erweiterungen_registriert_und_funktional()
        {
            // Registrierung der kompletten Outlands-Oberflaeche
            // (wiki.uooutlands.com/Razor_Scripting).
            foreach (string cmd in new[] { "sysmessage", "warmode", "setskill", "findtypelist" })
                Assert.True(Interpreter.GetCommandHandler(cmd) != null, $"Command '{cmd}' fehlt");

            foreach (string expr in new[]
                     {
                         "bandaging", "find", "findlayer", "hue", "noto", "counttype",
                         "atlist", "gumpexists", "ingump", "cooldown", "pvp"
                     })
                Assert.True(Interpreter.GetExpressionHandler(expr) != null, $"Expression '{expr}' fehlt");

            // Funktional: Heal-Potion mit Hue in den Backpack.
            Item healPot = new Item(0x40000907) { ItemID = 3852, Amount = 4, Hue = 38 };
            healPot.Container = World.Player.Backpack.Serial;
            World.AddItem(healPot);
            Item.UpdateContainers();

            World.Player.Notoriety = 1; // innocent

            Assistant.Core.SystemMessages.Messages.Clear();
            m_Fake.SentToServer.Clear();

            RunToEnd(new[]
            {
                "warmode 'on'",
                "setskill 'hiding' 'lock'",
                "createlist 'loot'",
                "findtypelist 'loot' 3852 self",
                "if list 'loot' > 0",
                "    sysmsg 'listed'",
                "endif",
                "if atlist 'loot' 0 as firstpot",
                "    sysmsg 'aliased'",
                "endif",
                "if find 0x40000907 self",
                "    sysmsg 'found-serial'",
                "endif",
                "if findlayer self backpack",
                "    sysmsg 'has-backpack'",
                "endif",
                "if hue 0x40000907 = 38",
                "    sysmsg 'hue-ok'",
                "endif",
                "if noto self = 'innocent'",
                "    sysmsg 'noto-ok'",
                "endif",
                "if counttype 3852 self >= 4",
                "    sysmsg 'count-ok'",
                "endif",
                "cooldown 'testcd' 30000",
                "if cooldown 'testcd' > 2000",
                "    sysmsg 'cd-ok'",
                "endif",
                "if pvp",
                "    sysmsg 'pvp-on'",
                "endif",
                "removelist 'loot'"
            });

            foreach (string expected in new[]
                     {
                         "listed", "aliased", "found-serial", "has-backpack",
                         "hue-ok", "noto-ok", "count-ok", "cd-ok"
                     })
                Assert.Contains(Assistant.Core.SystemMessages.Messages, m => m.Contains(expected));

            Assert.DoesNotContain(Assistant.Core.SystemMessages.Messages, m => m.Contains("pvp-on"));

            // warmode (0x72) + setskill (0x3A) wurden gesendet.
            Assert.Contains(m_Fake.SentToServer, pkt => pkt.Length > 0 && pkt[0] == 0x72);
            Assert.Contains(m_Fake.SentToServer, pkt => pkt.Length > 0 && pkt[0] == 0x3A);
        }

        [Fact]
        public void GumpExists_und_InGump()
        {
            World.Player.GumpList.Clear();
            World.Player.CurrentGumpStrings.Clear();

            var info = new PlayerData.GumpInfo(0x11223344);
            info.Strings.Add("Would you like to teleport home?");
            World.Player.GumpList[42] = info;
            World.Player.CurrentGumpStrings.AddRange(info.Strings);
            World.Player.HasGump = true;

            Assistant.Core.SystemMessages.Messages.Clear();

            RunToEnd(new[]
            {
                "if gumpexists 42",
                "    sysmsg 'gump-42'",
                "endif",
                "if gumpexists 'any'",
                "    sysmsg 'gump-any'",
                "endif",
                "if ingump 'teleport home'",
                "    sysmsg 'text-hit'",
                "endif",
                "if ingump 'teleport home' 42",
                "    sysmsg 'text-hit-42'",
                "endif",
                "if ingump 'does not exist'",
                "    sysmsg 'text-miss'",
                "endif"
            });

            Assert.Contains(Assistant.Core.SystemMessages.Messages, m => m.Contains("gump-42"));
            Assert.Contains(Assistant.Core.SystemMessages.Messages, m => m.Contains("gump-any"));
            Assert.Contains(Assistant.Core.SystemMessages.Messages, m => m.Contains("text-hit"));
            Assert.Contains(Assistant.Core.SystemMessages.Messages, m => m.Contains("text-hit-42"));
            Assert.DoesNotContain(Assistant.Core.SystemMessages.Messages, m => m.Contains("text-miss"));

            World.Player.GumpList.Clear();
            World.Player.HasGump = false;
        }

        [Fact]
        public void Bandaging_Expression_folgt_den_Meldungen()
        {
            Assistant.Core.BandageTimer.Stop();
            Assert.Equal(0, Assistant.Core.BandageTimer.RemainingSeconds);

            Assistant.Core.BandageTimer.OnSystemMessage("You begin applying the bandages.");
            Assert.True(Assistant.Core.BandageTimer.RemainingSeconds > 0);

            Assistant.Core.BandageTimer.OnLocalizedMessage(500966); // Heil-Ergebnis
            Assert.Equal(0, Assistant.Core.BandageTimer.RemainingSeconds);

            // Script-Sicht: bandaging = 0 -> Zweig aktiv.
            Assistant.Core.SystemMessages.Messages.Clear();
            RunToEnd(new[]
            {
                "if bandaging = 0",
                "    sysmsg 'ready'",
                "endif"
            });
            Assert.Contains(Assistant.Core.SystemMessages.Messages, m => m.Contains("ready"));
        }

        // ============================================================
        // String-Interpolation: {{variable}} (CE/Outlands) und
        // {{expression args}} (UOSagas-Erweiterung).
        // ============================================================

        [Fact]
        public void Interpolation_Variable_verhaelt_sich_wie_CE()
        {
            // CE speichert Variablen als Dezimalstring (SetAlias -> uint.ToString()),
            // 0x12345 = 74565. Genau diese Ausgabe muss stabil bleiben.
            ScriptVariables.RegisterVariable("ipol_testvar", (Serial)0x12345);
            try
            {
                Assert.Equal("var: 74565",
                    CommandHelper.ReplaceStringInterpolations("var: {{ipol_testvar}}"));
            }
            finally
            {
                ScriptVariables.UnregisterVariable("ipol_testvar");
            }
        }

        [Fact]
        public void Interpolation_Expression_Skillwert()
        {
            int idx = Ultima.Skills.SkillsByName["Alchemy"].Index;
            World.Player.Skills[idx].FixedValue = 1013; // 101.3

            Assert.Equal("Alchemy: 101.3",
                CommandHelper.ReplaceStringInterpolations("Alchemy: {{skill 'Alchemy'}}"));

            // Skill-Namen funktionieren mit beiden Quote-Arten und ganze
            // Werte behalten die UO-uebliche eine Nachkommastelle.
            World.Player.Skills[idx].FixedValue = 1200;
            Assert.Equal("Alchemy: 120.0",
                CommandHelper.ReplaceStringInterpolations("Alchemy: {{skill \"Alchemy\"}}"));
        }

        [Fact]
        public void Interpolation_Expression_Stats_und_Bool()
        {
            // Int-Expression (Fake-Player: Hits = 60).
            Assert.Equal("hp 60/100",
                CommandHelper.ReplaceStringInterpolations("hp {{hits}}/{{maxhits}}"));

            // Bool-Expression wird klein geschrieben ausgegeben.
            Assert.Equal("dead: false",
                CommandHelper.ReplaceStringInterpolations("dead: {{dead}}"));
        }

        [Fact]
        public void Interpolation_Variable_hat_Vorrang_vor_Expression()
        {
            // "stam" ist eine registrierte Expression; eine gleichnamige
            // Variable muss trotzdem gewinnen (CE-Verhalten bleibt stabil).
            ScriptVariables.RegisterVariable("stam", (Serial)42);
            try
            {
                Assert.Equal("42",
                    CommandHelper.ReplaceStringInterpolations("{{stam}}"));
            }
            finally
            {
                ScriptVariables.UnregisterVariable("stam");
            }

            // Ohne die Variable liefert dieselbe Interpolation die Expression.
            Assert.Equal("90",
                CommandHelper.ReplaceStringInterpolations("{{stam}}"));
        }

        [Fact]
        public void Interpolation_unbekannter_Inhalt_bleibt_not_found()
        {
            Assert.Equal("<not found>",
                CommandHelper.ReplaceStringInterpolations("{{definitely no such thing}}"));

            // Eine werfende Expression (Usage-Fehler) bricht die Ausgabe nicht ab.
            Assert.Equal("x <not found> y",
                CommandHelper.ReplaceStringInterpolations("x {{skill}} y"));
        }

        [Fact]
        public void Interpolation_ist_kulturfest()
        {
            int idx = Ultima.Skills.SkillsByName["Alchemy"].Index;
            World.Player.Skills[idx].FixedValue = 1013;

            CultureInfo old = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = new CultureInfo("de-DE");
                Assert.Equal("101.3",
                    CommandHelper.ReplaceStringInterpolations("{{skill 'Alchemy'}}"));
            }
            finally
            {
                CultureInfo.CurrentCulture = old;
            }
        }

        [Fact]
        public void PlayerSkills_Paket_fuellt_Skillwerte()
        {
            // Regressionstest: der 0x3A-Handler fehlte komplett -> jede
            // skill-Expression lieferte 0.0, weil nie jemand FixedValue schrieb.
            int idx = Ultima.Skills.SkillsByName["Alchemy"].Index;
            Assert.Equal(0.0, World.Player.Skills[idx].Value, 3);

            // Volliste Typ 0x02, exakt wie OutgoingPlayerPackets.SendSkillsUpdate:
            // pro Skill (id+1, value, base, lock, cap), 0-Terminator am Ende.
            var full = new System.Collections.Generic.List<byte> { 0x3A, 0, 0, 0x02 };
            void W(System.Collections.Generic.List<byte> l, ushort v)
            {
                l.Add((byte)(v >> 8));
                l.Add((byte)v);
            }
            W(full, (ushort)(idx + 1));
            W(full, 1013); // 101.3
            W(full, 1000);
            full.Add(0);   // Lock
            W(full, 1200);
            W(full, 0);    // Terminator
            full[1] = (byte)(full.Count >> 8);
            full[2] = (byte)full.Count;

            PacketHandler.OnServerPacket(0x3A, new PacketReader(full.ToArray(), true), null);

            Assert.Equal(101.3, World.Player.Skills[idx].Value, 3);
            Assert.Equal("101.3", CommandHelper.ReplaceStringInterpolations("{{skill 'Alchemy'}}"));

            // Einzelaenderung Typ 0xDF (SendSkillChange): rohe SkillID ohne +1.
            var change = new System.Collections.Generic.List<byte> { 0x3A, 0, 13, 0xDF };
            W(change, (ushort)idx);
            W(change, 1020); // 102.0
            W(change, 1010);
            change.Add(0);
            W(change, 1200);

            PacketHandler.OnServerPacket(0x3A, new PacketReader(change.ToArray(), true), null);

            Assert.Equal(102.0, World.Player.Skills[idx].Value, 3);
        }

        [Fact]
        public void Interpolation_ueber_overhead_im_Script()
        {
            int idx = Ultima.Skills.SkillsByName["Alchemy"].Index;
            World.Player.Skills[idx].FixedValue = 987; // 98.7

            Assistant.Core.SystemMessages.Messages.Clear();
            RunToEnd(new[]
            {
                "sysmsg 'Alchemy: {{skill Alchemy}}'"
            });
            Assert.Contains(Assistant.Core.SystemMessages.Messages, m => m.Contains("Alchemy: 98.7"));
        }

        [Fact]
        public void Dead_prueft_das_angegebene_Mobile()
        {
            // Fix des einzigen "still falschen" Stubs (D19): dead <serial>
            // ignorierte den Parameter und meldete den Spieler.
            // IsGhost haengt am Body (402/403/607/608/970).
            Mobile ghost = new Mobile(0x00000901) { Name = "a ghost", Body = 402 };
            World.AddMobile(ghost);

            Mobile alive = new Mobile(0x00000902) { Name = "alive", Body = 0x0190 };
            World.AddMobile(alive);

            Assistant.Core.SystemMessages.Messages.Clear();
            RunToEnd(new[]
            {
                "if dead 0x00000901",
                "    sysmsg 'ghost-dead'",
                "endif",
                "if not dead 0x00000902",
                "    sysmsg 'alive-ok'",
                "endif",
                "if not dead 0x00000999", // unbekannt -> nicht tot
                "    sysmsg 'unknown-ok'",
                "endif",
                "if not dead", // ohne Argument: der (lebende) Spieler
                "    sysmsg 'player-ok'",
                "endif"
            });

            Assert.Contains(Assistant.Core.SystemMessages.Messages, m => m == "ghost-dead");
            Assert.Contains(Assistant.Core.SystemMessages.Messages, m => m == "alive-ok");
            Assert.Contains(Assistant.Core.SystemMessages.Messages, m => m == "unknown-ok");
            Assert.Contains(Assistant.Core.SystemMessages.Messages, m => m == "player-ok");
        }

        [Fact]
        public void Outlands_Layernamen_und_Skill_Spellings()
        {
            // findlayer kennt jetzt die Outlands-Schreibweisen; discord/taming
            // loesen auf Discordance/Animal Taming auf.
            Assert.Equal(Layer.LeftHand, Assistant.Scripts.Helpers.CommandHelper.ParseLayer("onehandedsecondary"));
            Assert.Equal(Layer.RightHand, Assistant.Scripts.Helpers.CommandHelper.ParseLayer("onehanded"));
            Assert.Equal(Layer.Cloak, Assistant.Scripts.Helpers.CommandHelper.ParseLayer("quiver"));
            Assert.Equal(Layer.OuterTorso, Assistant.Scripts.Helpers.CommandHelper.ParseLayer("outerbody"));
            Assert.Equal(Layer.Gloves, Assistant.Scripts.Helpers.CommandHelper.ParseLayer("gloves"));

            Assert.Equal("Discordance", Ultima.Skills.SkillsByName["discord"].Name);
            Assert.Equal("Animal Taming", Ultima.Skills.SkillsByName["taming"].Name);

            // Und ueber die Sprache selbst (wirft nicht mehr):
            RunToEnd(new[]
            {
                "if findlayer self quiver = 0",
                "    sysmsg 'no quiver'",
                "endif",
                "sysmsg 'discord: {{skill discord}}'"
            });
        }
    }
}
