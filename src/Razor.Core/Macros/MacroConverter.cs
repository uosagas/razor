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

// UOSagas-Razor: Macro-zu-Script-Konverter (Sagas-Zusatz).
//
// Uebersetzt ein aufgenommenes Macro in die drei Script-Sprachen:
//  * Razor-Script — CE-getreu ueber MacroAction.ToScript() (CE: "Convert to
//    Script" im Macro-Kontextmenue), zusaetzlich mit Einrueckung.
//  * Lua — Mapping auf die Lua-API; nicht abbildbare Actions landen als
//    "-- TODO:"-Kommentar an Ort und Stelle (nichts geht still verloren).
//  * VScript — linearer Node-Graph (Start -> Aktionskette ueber Flow-Links);
//    Kontrollfluss (If/For/While) und Spezial-Actions haben keine 1:1-Nodes
//    und werden in einer CommentBox am Graphen aufgelistet.
//
// Der Konverter erzeugt nur Text/Graphen — Datei anlegen + Editor oeffnen
// macht die UI (MacrosTab), Name wird erst beim ersten Speichern vergeben.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using System.Text;
using Assistant.VScripts.Core;
using Assistant.VScripts.Nodes;

namespace Assistant.Macros
{
    public static class MacroConverter
    {
        // ---- Razor-Script ---------------------------------------------------

        public static string ToRazorScript(Macro m)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"// Converted from macro '{m.GetName()}'");
            if (m.Loop)
                sb.AppendLine("// This macro loops — wrap the body in 'while'/'endwhile' if you want that here.");
            sb.AppendLine();

            int depth = 0;
            foreach (MacroAction a in m.Actions)
            {
                string line;
                try
                {
                    line = a.ToScript();
                }
                catch (Exception e)
                {
                    line = $"// {a} — could not be converted ({e.Message})";
                }

                bool closes = a is ElseAction || a is EndIfAction || a is EndForAction ||
                              a is EndWhileAction || a is DoWhileAction;
                bool opens = a is IfAction || a is ElseAction || a is ForAction ||
                             a is WhileAction || a is StartDoWhileAction;

                if (closes && depth > 0)
                    depth--;

                sb.AppendLine(new string(' ', depth * 4) + line);

                if (opens)
                    depth++;
            }

            return sb.ToString();
        }

        // ---- Lua ------------------------------------------------------------

        public static string ToLua(Macro m)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"-- Converted from macro '{m.GetName()}'");
            sb.AppendLine();

            int depth = 0;
            uint lastGumpId = 0; // aus einem vorangehenden WaitForGump uebernommen

            void Line(string text)
            {
                sb.AppendLine(new string(' ', depth * 4) + text);
            }

            if (m.Loop)
            {
                Line("while true do");
                depth++;
            }

            foreach (MacroAction a in m.Actions)
            {
                switch (a)
                {
                    case MacroComment c:
                        Line($"-- {c.Comment}");
                        break;

                    case SpeechAction sp:
                        Line($"Player.Say('{LuaStr(sp.Speech)}')");
                        break;

                    case DoubleClickAction d:
                        Line($"Player.UseObject(0x{d.Serial.Value:X})");
                        break;

                    case DoubleClickTypeAction dt:
                        Line($"Player.UseObjectByType(0x{dt.Gfx:X})");
                        break;

                    case LiftAction l:
                        Line($"Player.PickUp(0x{l.Serial.Value:X}, {Math.Max((ushort) 1, l.Amount)})");
                        break;

                    case LiftTypeAction lt:
                        Line($"local liftItem = Items.FindByType(0x{lt.Gfx:X})");
                        Line("if liftItem ~= nil then");
                        Line($"    Player.PickUp(liftItem.Serial, {Math.Max((ushort) 1, lt.Amount)})");
                        Line("end");
                        break;

                    case DropRelLocAction:
                        Line("Player.DropOnGround() -- macro dropped at an offset from the player");
                        break;

                    case DropAction dr when dr.Layer > Layer.Invalid && dr.Layer <= Layer.LastUserValid:
                        Line($"-- TODO: macro equipped the held item to layer {dr.Layer} — use Player.Equip(serial) on the lifted item");
                        break;

                    case DropAction dr when !dr.To.IsValid:
                        Line("Player.DropOnGround()");
                        break;

                    case DropAction dr:
                        Line($"Player.DropInContainer(0x{dr.To.Value:X})");
                        break;

                    case GumpResponseAction g:
                    {
                        string sw = g.Switches != null && g.Switches.Length > 0
                            ? ", { " + string.Join(", ", g.Switches) + " }"
                            : "";
                        if (lastGumpId != 0)
                            Line($"Gumps.Reply(0x{lastGumpId:X}, {g.ButtonID}{sw})");
                        else
                            Line($"-- TODO: fill in the gump id: Gumps.Reply(0xGUMPID, {g.ButtonID}{sw})");
                        break;
                    }

                    case MenuResponseAction:
                        Line($"-- TODO: {a} — menus have no Lua API yet");
                        break;

                    case PauseAction p:
                        Line($"Pause({(int) p.Timeout.TotalMilliseconds})");
                        break;

                    case WaitForTargetAction wt:
                        Line($"Targeting.WaitForTarget({(int) wt.Timeout.TotalMilliseconds})");
                        break;

                    case LastTargetAction:
                        Line("Targeting.Last()");
                        break;

                    case AbsoluteTargetAction at:
                        Line($"Targeting.TargetSerial(0x{at.Info.Serial.Value:X})");
                        break;

                    case TargetTypeAction tt:
                        Line($"local targ = {(TargetTypeIsMobile(tt) ? "Mobiles" : "Items")}.FindByType(0x{TargetTypeGfx(tt):X})");
                        Line("if targ ~= nil then");
                        Line("    Targeting.TargetSerial(targ.Serial)");
                        Line("end");
                        break;

                    case WaitForGumpAction wg:
                        lastGumpId = wg.GumpID;
                        Line($"Gumps.WaitForGump(0x{wg.GumpID:X}, {(int) wg.Timeout.TotalMilliseconds})");
                        break;

                    case UseSkillAction us:
                        Line($"Skills.Use('{Ultima.Skills.GetSkillDisplayName(us.Skill)}')");
                        break;

                    case BookCastSpellAction bc:
                        Line($"Spells.CastById({bc.SpellID}){SpellComment(bc.SpellID)}");
                        break;

                    case ExtCastSpellAction ec:
                        Line($"Spells.CastById({ec.SpellID}){SpellComment(ec.SpellID)}");
                        break;

                    case MacroCastSpellAction mc:
                        Line($"Spells.CastById({mc.SpellID}){SpellComment(mc.SpellID)}");
                        break;

                    case OverheadMessageAction om:
                        Line($"Messages.Overhead('{LuaStr(om.Message)}', {om.Hue}, Player.Serial)");
                        break;

                    case ClearSysMessages:
                        Line("Journal.Clear()");
                        break;

                    case WaitForStatAction ws:
                    {
                        // Warten = Gegenteil der Zielbedingung pollen.
                        string expr = StatExpr(ws.Stat, (sbyte) ws.Op, ws.Amount);
                        Line($"while not ({expr}) do -- wait for stat, macro timeout {(int) ws.Timeout.TotalSeconds}s");
                        Line("    Pause(500)");
                        Line("end");
                        break;
                    }

                    case IfAction ia:
                        Line($"if {CondExpr(ia.Variable, ia.Op, ia.Value, ia.Counter, ia.SkillId)} then");
                        depth++;
                        break;

                    case ElseAction:
                        if (depth > 0) depth--;
                        Line("else");
                        depth++;
                        break;

                    case EndIfAction:
                        if (depth > 0) depth--;
                        Line("end");
                        break;

                    case ForAction f:
                        Line($"for i = 1, {f.Max} do");
                        depth++;
                        break;

                    case EndForAction:
                        if (depth > 0) depth--;
                        Line("end");
                        break;

                    case WhileAction wa:
                        Line($"while {CondExpr((IfAction.IfVarType) (int) wa.Variable, wa.Op, wa.Value, wa.Counter, wa.SkillId)} do");
                        depth++;
                        break;

                    case EndWhileAction:
                        if (depth > 0) depth--;
                        Line("end");
                        break;

                    case StartDoWhileAction:
                        Line("repeat");
                        depth++;
                        break;

                    case DoWhileAction dw:
                        if (depth > 0) depth--;
                        Line($"until not ({CondExpr((IfAction.IfVarType) (int) dw.Variable, dw.Op, dw.Value, dw.Counter, dw.SkillId)})");
                        break;

                    default:
                        Line($"-- TODO: {SafeToString(a)} — no direct Lua equivalent");
                        break;
                }
            }

            if (m.Loop)
            {
                depth = 0;
                sb.AppendLine("end");
            }

            return sb.ToString();
        }

        private static string LuaStr(string s)
        {
            return (s ?? "").Replace("\\", "\\\\").Replace("'", "\\'").Replace("\r", "").Replace("\n", "\\n");
        }

        private static string SafeToString(MacroAction a)
        {
            try
            {
                return a.ToString();
            }
            catch
            {
                return a.GetType().Name;
            }
        }

        private static string SpellComment(int id)
        {
            foreach (HotKeys.SongHotKeys.Song song in HotKeys.SongHotKeys.Songs)
            {
                if (song.SpellId == id)
                    return $" -- {song.Name}";
            }

            string name = Spell.GetName(id);
            return string.IsNullOrEmpty(name) ? "" : $" -- {Language.GetString(Spell.Get(id).Name)}";
        }

        // 0 <=, 1 >=, 2 <, 3 > (Kommentar an IfAction.m_Direction)
        private static string OpText(sbyte op)
        {
            switch (op)
            {
                case 0: return "<=";
                case 1: return ">=";
                case 2: return "<";
                case 3: return ">";
                default: return "<=";
            }
        }

        private static string StatExpr(IfAction.IfVarType stat, sbyte op, int amount)
        {
            string field = stat == IfAction.IfVarType.Mana ? "Player.Mana"
                : stat == IfAction.IfVarType.Stamina ? "Player.Stam"
                : "Player.Hits";
            return $"{field} {OpText(op)} {amount}";
        }

        private static string CondExpr(IfAction.IfVarType var, sbyte op, object value, string counter, int skillId)
        {
            switch (var)
            {
                case IfAction.IfVarType.Hits:
                    return $"Player.Hits {OpText(op)} {value}";
                case IfAction.IfVarType.Mana:
                    return $"Player.Mana {OpText(op)} {value}";
                case IfAction.IfVarType.Stamina:
                    return $"Player.Stam {OpText(op)} {value}";
                case IfAction.IfVarType.Weight:
                    return $"Player.Weight {OpText(op)} {value}";
                case IfAction.IfVarType.Poisoned:
                    return "Player.IsPoisoned";
                case IfAction.IfVarType.Mounted:
                    return "Player.IsMounted";
                case IfAction.IfVarType.RHandEmpty:
                    return "Items.FindByLayer(1) == nil";
                case IfAction.IfVarType.LHandEmpty:
                    return "Items.FindByLayer(2) == nil";
                case IfAction.IfVarType.SysMessage:
                    return $"Journal.Contains('{LuaStr(value as string)}')";
                case IfAction.IfVarType.Skill:
                {
                    string skill = Ultima.Skills.GetSkillDisplayName(skillId);
                    string v = Convert.ToString(value, CultureInfo.InvariantCulture);
                    return $"Skills.GetValue('{skill}') {OpText(op)} {v}";
                }
                case IfAction.IfVarType.Counter:
                    return $"false --[[ TODO: counter '{counter}' has no Lua API ]]";
                default:
                    return "false --[[ TODO: unsupported condition ]]";
            }
        }

        // TargetTypeAction haelt Mobile-Flag/Gfx privat — nur fuer den
        // Konverter ueber die Serialisierung lesen (Format: Type|mobile|gfx).
        private static bool TargetTypeIsMobile(TargetTypeAction tt)
        {
            string[] parts = tt.Serialize().Split('|');
            return parts.Length > 1 && bool.TryParse(parts[1], out bool b) && b;
        }

        private static ushort TargetTypeGfx(TargetTypeAction tt)
        {
            string[] parts = tt.Serialize().Split('|');
            return parts.Length > 2 && ushort.TryParse(parts[2], out ushort g) ? g : (ushort) 0;
        }

        // ---- VScript --------------------------------------------------------

        /// <summary>Baut einen linearen Graphen (Start -> Kette). Nicht
        /// abbildbare Actions kommen nach <paramref name="skipped"/> und in
        /// eine CommentBox am Graphen.</summary>
        public static NodeGraph ToVScript(Macro m, out List<string> skipped)
        {
            skipped = new List<string>();
            var graph = new NodeGraph(m.GetName());

            var start = new StartNode(graph.GetNextNodeId(), graph.GetNextPinId())
            {
                Position = new Vector2(80, 160)
            };
            graph.AddNode(start);

            VScriptNode prev = start;
            int index = 0;
            uint lastGumpId = 0;

            void Chain(VScriptNode node)
            {
                index++;
                node.Position = new Vector2(80 + (index % 6) * 250, 160 + (index / 6) * 220);
                graph.AddNode(node);

                NodePin from = prev.OutputPins.Find(p => p.Type == PinType.Flow);
                NodePin to = node.InputPins.Find(p => p.Type == PinType.Flow);
                if (from != null && to != null)
                    graph.AddLink(new NodeLink(graph.GetNextLinkId(), from.Id, to.Id));

                prev = node;
            }

            void SetPin(VScriptNode node, string pin, string value)
            {
                NodePin p = node.InputPins.Find(x => x.Name == pin);
                if (p != null)
                    p.Value = value;
            }

            foreach (MacroAction a in m.Actions)
            {
                switch (a)
                {
                    case MacroComment:
                        break; // Kommentare tragen im Graphen nichts

                    case SpeechAction sp:
                    {
                        var n = new SayNode(graph.GetNextNodeId(), graph.GetNextPinId());
                        SetPin(n, "Message", sp.Speech ?? "");
                        Chain(n);
                        break;
                    }

                    case DoubleClickAction d:
                    {
                        var n = new UseItemNode(graph.GetNextNodeId(), graph.GetNextPinId());
                        SetPin(n, "Serial/Type", $"0x{d.Serial.Value:X}");
                        Chain(n);
                        break;
                    }

                    case DoubleClickTypeAction dt:
                    {
                        var n = new UseItemNode(graph.GetNextNodeId(), graph.GetNextPinId());
                        SetPin(n, "Serial/Type", $"0x{dt.Gfx:X}");
                        Chain(n);
                        break;
                    }

                    case LiftAction l:
                    {
                        var n = new PickupNode(graph.GetNextNodeId(), graph.GetNextPinId());
                        SetPin(n, "Serial", $"0x{l.Serial.Value:X}");
                        Chain(n);
                        break;
                    }

                    case DropAction dr when dr.To.IsValid && dr.Layer == Layer.Invalid:
                    {
                        var n = new DropNode(graph.GetNextNodeId(), graph.GetNextPinId())
                        {
                            Location = DropLocation.Container
                        };
                        SetPin(n, "Container Serial", $"0x{dr.To.Value:X}");
                        Chain(n);
                        break;
                    }

                    case PauseAction p:
                    {
                        var n = new DelayNode(graph.GetNextNodeId(), graph.GetNextPinId());
                        SetPin(n, "Milliseconds", ((int) p.Timeout.TotalMilliseconds).ToString());
                        Chain(n);
                        break;
                    }

                    case WaitForTargetAction wt:
                    {
                        var n = new WaitForTargetNode(graph.GetNextNodeId(), graph.GetNextPinId());
                        SetPin(n, "Timeout (ms)", ((int) wt.Timeout.TotalMilliseconds).ToString());
                        Chain(n);
                        break;
                    }

                    case AbsoluteTargetAction at:
                    {
                        var n = new ExecuteTargetNode(graph.GetNextNodeId(), graph.GetNextPinId());
                        SetPin(n, "Serial", $"0x{at.Info.Serial.Value:X}");
                        Chain(n);
                        break;
                    }

                    case WaitForGumpAction wg:
                    {
                        lastGumpId = wg.GumpID;
                        var n = new WaitForGumpNode(graph.GetNextNodeId(), graph.GetNextPinId());
                        SetPin(n, "Gump", $"0x{wg.GumpID:X}");
                        SetPin(n, "Timeout", ((int) wg.Timeout.TotalMilliseconds).ToString());
                        Chain(n);
                        break;
                    }

                    case GumpResponseAction g:
                    {
                        var n = new PressButtonGumpNode(graph.GetNextNodeId(), graph.GetNextPinId());
                        if (lastGumpId != 0)
                            SetPin(n, "Gump", $"0x{lastGumpId:X}");
                        SetPin(n, "Button ID", g.ButtonID.ToString());
                        if (g.Switches != null && g.Switches.Length > 0)
                            SetPin(n, "Switches", string.Join(",", g.Switches));
                        Chain(n);
                        break;
                    }

                    case UseSkillAction us:
                    {
                        var n = new UseSkillNode(graph.GetNextNodeId(), graph.GetNextPinId())
                        {
                            SelectedSkillIndex = us.Skill
                        };
                        Chain(n);
                        break;
                    }

                    case BookCastSpellAction bc:
                        ChainCast(bc.SpellID);
                        break;
                    case ExtCastSpellAction ec:
                        ChainCast(ec.SpellID);
                        break;
                    case MacroCastSpellAction mc:
                        ChainCast(mc.SpellID);
                        break;

                    case OverheadMessageAction om:
                    {
                        var n = new MessageOverheadNode(graph.GetNextNodeId(), graph.GetNextPinId());
                        SetPin(n, "Message", om.Message ?? "");
                        SetPin(n, "Hue", om.Hue.ToString());
                        Chain(n);
                        break;
                    }

                    case ClearSysMessages:
                        Chain(new ClearJournalNode(graph.GetNextNodeId(), graph.GetNextPinId()));
                        break;

                    default:
                        skipped.Add(SafeToString(a));
                        break;
                }
            }

            void ChainCast(int spellId)
            {
                var n = new CastSpellNode(graph.GetNextNodeId(), graph.GetNextPinId())
                {
                    SelectedSpellId = spellId
                };
                Chain(n);
            }

            string note = $"Converted from macro '{m.GetName()}'";
            if (m.Loop)
                note += " (macro loops — add a While Loop yourself)";
            if (skipped.Count > 0)
                note += $" — NOT converted: {string.Join("; ", skipped)}";

            graph.AddCommentBox(new CommentBox
            {
                Id = Guid.NewGuid().ToString(),
                Title = note,
                Position = new Vector2(60, 40),
                Size = new Vector2(Math.Max(600, 80 + Math.Min(index, 6) * 250), 70)
            });

            return graph;
        }
    }
}
