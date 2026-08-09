#region license
// Razor: An Ultima Online Assistant
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

// Portiert aus Razor CE (Razor/Scripts/Expressions.cs) — 1:1 gegen das
// Port-Weltmodell. Abweichungen: FeatureBit/AllowBit gibt es im Port nicht
// (Feature gilt als erlaubt); Skills stammen aus der daten-basierten
// Ultima.Skills-Tabelle.

using System;
using System.Collections.Generic;
using Assistant.Core;
using Assistant.Scripts.Engine;
using Assistant.Scripts.Helpers;
using Ultima;

namespace Assistant.Scripts
{
    public static class Expressions
    {
        public static void Register()
        {
            Interpreter.RegisterExpressionHandler("stam", Stam);
            Interpreter.RegisterExpressionHandler("maxstam", MaxStam);
            Interpreter.RegisterExpressionHandler("hp", Hp);
            Interpreter.RegisterExpressionHandler("hits", Hp);
            Interpreter.RegisterExpressionHandler("maxhp", MaxHp);
            Interpreter.RegisterExpressionHandler("maxhits", MaxHp);
            Interpreter.RegisterExpressionHandler("mana", Mana);
            Interpreter.RegisterExpressionHandler("maxmana", MaxMana);
            Interpreter.RegisterExpressionHandler("poisoned", Poisoned);
            Interpreter.RegisterExpressionHandler("hidden", Hidden);

            Interpreter.RegisterExpressionHandler("mounted", Mounted);
            Interpreter.RegisterExpressionHandler("rhandempty", RHandEmpty);
            Interpreter.RegisterExpressionHandler("lhandempty", LHandEmpty);

            Interpreter.RegisterExpressionHandler("dead", Dead);

            Interpreter.RegisterExpressionHandler("str", Str);
            Interpreter.RegisterExpressionHandler("int", Int);
            Interpreter.RegisterExpressionHandler("dex", Dex);

            Interpreter.RegisterExpressionHandler("weight", Weight);
            Interpreter.RegisterExpressionHandler("maxweight", MaxWeight);

            Interpreter.RegisterExpressionHandler("skill", SkillExpression);
            Interpreter.RegisterExpressionHandler("count", CountExpression);
            Interpreter.RegisterExpressionHandler("counter", CountExpression);

            Interpreter.RegisterExpressionHandler("insysmsg", InSysMessage);
            Interpreter.RegisterExpressionHandler("insysmessage", InSysMessage);

            Interpreter.RegisterExpressionHandler("findtype", FindType);

            Interpreter.RegisterExpressionHandler("findbuff", FindBuffDebuff);
            Interpreter.RegisterExpressionHandler("finddebuff", FindBuffDebuff);

            Interpreter.RegisterExpressionHandler("position", Position);

            Interpreter.RegisterExpressionHandler("queued", Queued);

            Interpreter.RegisterExpressionHandler("varexist", VarExist);
            Interpreter.RegisterExpressionHandler("varexists", VarExist);

            Interpreter.RegisterExpressionHandler("followers", Followers);
            Interpreter.RegisterExpressionHandler("maxfollowers", MaxFollowers);

            Interpreter.RegisterExpressionHandler("targetexists", TargetExists);

            Interpreter.RegisterExpressionHandler("diffweight", DiffWeight);
            Interpreter.RegisterExpressionHandler("diffhits", DiffHits);
            Interpreter.RegisterExpressionHandler("diffhp", DiffHits);
            Interpreter.RegisterExpressionHandler("diffstam", DiffStam);
            Interpreter.RegisterExpressionHandler("diffmana", DiffMana);

            Interpreter.RegisterExpressionHandler("name", Name);
            Interpreter.RegisterExpressionHandler("paralyzed", Paralyzed);
            Interpreter.RegisterExpressionHandler("invuln", Invulnerable);
            Interpreter.RegisterExpressionHandler("invul", Invulnerable);
            Interpreter.RegisterExpressionHandler("blessed", Invulnerable);
            Interpreter.RegisterExpressionHandler("warmode", Warmode);

            Interpreter.RegisterExpressionHandler("itemcount", ItemCount);

            Interpreter.RegisterExpressionHandler("poplist", PopListExp);
            Interpreter.RegisterExpressionHandler("listexists", ListExists);
            Interpreter.RegisterExpressionHandler("list", ListLength);
            Interpreter.RegisterExpressionHandler("inlist", InList);

            Interpreter.RegisterExpressionHandler("timer", TimerValue);
            Interpreter.RegisterExpressionHandler("timerexists", TimerExists);

            // Outlands-Erweiterungen (wiki.uooutlands.com/Razor_Scripting):
            Interpreter.RegisterExpressionHandler("bandaging", Bandaging);
            Interpreter.RegisterExpressionHandler("find", FindBySerial);
            Interpreter.RegisterExpressionHandler("findlayer", FindLayer);
            Interpreter.RegisterExpressionHandler("hue", HueOf);
            Interpreter.RegisterExpressionHandler("noto", Noto);
            Interpreter.RegisterExpressionHandler("counttype", CountType);
            Interpreter.RegisterExpressionHandler("atlist", AtList);
            Interpreter.RegisterExpressionHandler("gumpexists", GumpExists);
            Interpreter.RegisterExpressionHandler("ingump", InGump);
            Interpreter.RegisterExpressionHandler("cooldown", CooldownRemaining);
            Interpreter.RegisterExpressionHandler("pvp", Pvp);
        }

        private static int Bandaging(string expression, Variable[] args, bool quiet, bool force)
        {
            return BandageTimer.RemainingSeconds;
        }

        /// <summary>Outlands: find (serial) [src] [hue] [qty] [range] — prueft ein konkretes Serial.</summary>
        private static uint FindBySerial(string expression, Variable[] vars, bool quiet, bool force)
        {
            if (vars.Length == 0)
                throw new RunTimeError("Usage: find (serial) [src] [hue] [qty] [range]");

            Serial serial = vars[0].AsSerial();
            CommandHelper.FindArgs args = CommandHelper.ParseFindArgs(vars);

            Item item = World.FindItem(serial);

            if (item != null)
            {
                if (args.Hue > -1 && item.Hue != args.Hue)
                    return Serial.Zero;

                if (args.MinQuantity > 0 && item.Amount < args.MinQuantity)
                    return Serial.Zero;

                if (args.Backpack &&
                    !(item.RootContainer is Item root && World.Player?.Backpack != null &&
                      (root == World.Player.Backpack || item.Container == World.Player.Backpack)) &&
                    item.RootContainer != World.Player)
                    return Serial.Zero;

                if (args.Self && item.RootContainer != World.Player)
                    return Serial.Zero;

                if (args.Ground && !item.OnGround)
                    return Serial.Zero;

                if (args.Range > 0 && item.OnGround &&
                    !Utility.InRange(World.Player.Position, item.Position, args.Range))
                    return Serial.Zero;

                return item.Serial;
            }

            Mobile mobile = World.FindMobile(serial);

            if (mobile != null)
            {
                if (args.Range > 0 &&
                    !Utility.InRange(World.Player.Position, mobile.Position, args.Range))
                    return Serial.Zero;

                return mobile.Serial;
            }

            return Serial.Zero;
        }

        /// <summary>Outlands: findlayer self layername — Serial des Items auf dem Layer.</summary>
        private static uint FindLayer(string expression, Variable[] vars, bool quiet, bool force)
        {
            if (vars.Length < 2)
                throw new RunTimeError("Usage: findlayer self (layername)");

            if (World.Player == null)
                return Serial.Zero;

            // vars[0] = 'self' (einzige dokumentierte Quelle).
            Layer layer = CommandHelper.ParseLayer(vars[1].AsString());

            Item item = World.Player.GetItemOnLayer(layer);

            return item?.Serial ?? Serial.Zero;
        }

        /// <summary>Outlands: hue (serial) — Farbton eines Items/Mobiles.</summary>
        private static int HueOf(string expression, Variable[] vars, bool quiet, bool force)
        {
            if (vars.Length == 0)
                throw new RunTimeError("Usage: hue (serial)");

            Serial serial = vars[0].AsSerial();

            Item item = World.FindItem(serial);
            if (item != null)
                return item.Hue;

            Mobile mobile = World.FindMobile(serial);
            if (mobile != null)
                return mobile.Hue;

            return 0;
        }

        /// <summary>
        /// Outlands: noto (mobile) — innocent/friend/hostile/criminal/enemy/
        /// murderer/invulnerable (aus dem Notoriety-Byte).
        /// </summary>
        private static string Noto(string expression, Variable[] vars, bool quiet, bool force)
        {
            if (vars.Length == 0)
                throw new RunTimeError("Usage: noto (serial)");

            Mobile mobile = World.FindMobile(vars[0].AsSerial());

            switch (mobile?.Notoriety ?? 0)
            {
                case 1: return "innocent";
                case 2: return "friend";
                case 3: return "hostile";
                case 4: return "criminal";
                case 5: return "enemy";
                case 6: return "murderer";
                case 7: return "invulnerable";
                default: return "unknown";
            }
        }

        /// <summary>Outlands: counttype (name/gfx) [src] [hue] [range] — Item-Anzahl (Summe der Stacks).</summary>
        private static int CountType(string expression, Variable[] vars, bool quiet, bool force)
        {
            if (vars.Length == 0)
                throw new RunTimeError("Usage: counttype ('name of item'/'graphicID') [src] [hue] [range]");

            if (World.Player == null)
                return 0;

            string gfxStr = vars[0].AsString();
            CommandHelper.FindArgs args = CommandHelper.ParseFindArgs(vars);

            int count = 0;
            foreach (Item item in CommandHelper.GetItems(gfxStr, args))
                count += item.Amount < 1 ? 1 : item.Amount;

            return count;
        }

        /// <summary>Outlands: atlist ('list name') (index) — Element an Position (0-basiert).</summary>
        private static string AtList(string expression, Variable[] vars, bool quiet, bool force)
        {
            if (vars.Length != 2)
                throw new RunTimeError("Usage: atlist ('list name') (index)");

            Variable value = Interpreter.GetListValue(vars[0].AsString(), vars[1].AsInt());

            return value?.AsString() ?? string.Empty;
        }

        /// <summary>Outlands: gumpexists (gumpId/'any').</summary>
        private static bool GumpExists(string expression, Variable[] vars, bool quiet, bool force)
        {
            if (World.Player == null)
                return false;

            if (vars.Length == 0 ||
                vars[0].AsString().IndexOf("any", StringComparison.OrdinalIgnoreCase) != -1)
            {
                return World.Player.HasGump || World.Player.HasCompressedGump ||
                       World.Player.GumpList.Count > 0;
            }

            return World.Player.GumpList.ContainsKey(vars[0].AsUInt());
        }

        /// <summary>Outlands: ingump (text) [gumpId/'any'] — Substring-Suche in den Gump-Texten.</summary>
        private static bool InGump(string expression, Variable[] vars, bool quiet, bool force)
        {
            if (vars.Length == 0)
                throw new RunTimeError("Usage: ingump (text) [gumpId/'any']");

            if (World.Player == null)
                return false;

            string text = vars[0].AsString();

            IEnumerable<string> strings;

            if (vars.Length > 1 &&
                vars[1].AsString().IndexOf("any", StringComparison.OrdinalIgnoreCase) == -1 &&
                World.Player.GumpList.TryGetValue(vars[1].AsUInt(), out PlayerData.GumpInfo info))
            {
                strings = info.Strings;
            }
            else
            {
                strings = World.Player.CurrentGumpStrings;
            }

            foreach (string line in strings)
            {
                if (line != null && line.IndexOf(text, StringComparison.OrdinalIgnoreCase) != -1)
                    return true;
            }

            return false;
        }

        /// <summary>Outlands: cooldown ('name') — verbleibende Millisekunden (0 = abgelaufen/unbekannt).</summary>
        private static int CooldownRemaining(string expression, Variable[] vars, bool quiet, bool force)
        {
            if (vars.Length == 0)
                throw new RunTimeError("Usage: cooldown ('cooldown name')");

            Cooldown cooldown = CooldownManager.Find(vars[0].AsString());

            if (cooldown == null)
                return 0;

            double remaining = (cooldown.EndTime - DateTime.UtcNow).TotalMilliseconds;

            return remaining <= 0 ? 0 : (int) remaining;
        }

        /// <summary>
        /// Outlands: pvp — PvP-Script-Restriktionen aktiv?
        /// TODO(scripting-stub): UOSagas hat (noch) kein entsprechendes
        /// Server-Signal — liefert immer false.
        /// </summary>
        private static bool Pvp(string expression, Variable[] vars, bool quiet, bool force)
        {
            return false;
        }

        private static int TimerValue(string expression, Variable[] args, bool quiet, bool force)
        {
            if (args.Length != 1)
                throw new RunTimeError("Usage: timer ('timer name')");

            var ts = Interpreter.GetTimer(args[0].AsString());

            return (int)ts.TotalMilliseconds;
        }

        private static bool TimerExists(string expression, Variable[] args, bool quiet, bool force)
        {
            if (args.Length != 1)
                throw new RunTimeError("Usage: timerexists ('timer name')");

            return Interpreter.TimerExists(args[0].AsString());
        }

        private static bool ListExists(string expression, Variable[] args, bool quiet, bool force)
        {
            if (args.Length != 1)
                throw new RunTimeError("Usage: listexists ('list name')");

            if (Interpreter.ListExists(args[0].AsString()))
                return true;

            return false;
        }

        private static int ListLength(string expression, Variable[] args, bool quiet, bool force)
        {
            if (args.Length != 1)
                throw new RunTimeError("Usage: list (list name) (operator) (value)");

            return Interpreter.ListLength(args[0].AsString());
        }

        private static bool InList(string expression, Variable[] args, bool quiet, bool force)
        {
            if (args.Length != 2)
                throw new RunTimeError("Usage: inlist (list name) (element)");

            if (Interpreter.ListContains(args[0].AsString(), args[1]))
                return true;

            return false;
        }

        private static uint PopListExp(string command, Variable[] args, bool quiet, bool force)
        {
            if (args.Length != 2)
                throw new RunTimeError("Usage: poplist ('list name') ('element value'/'front'/'back')");

            var listName = args[0].AsString();
            var frontBackOrElementVar = args[1];
            var isFrontOrBack = frontBackOrElementVar.AsString() == "front" || frontBackOrElementVar.AsString() == "back";

            if (isFrontOrBack)
            {
                var isFront = frontBackOrElementVar.AsString() == "front";
                if (force)
                {
                    while (Interpreter.PopList(listName, isFront, out _)) { }
                    return Serial.Zero;
                }

                Interpreter.PopList(listName, isFront, out var popped);
                return popped.AsSerial();
            }

            var evaluatedVar = new Variable(frontBackOrElementVar.AsString());

            if (force)
            {
                while (Interpreter.PopList(listName, evaluatedVar)) { }
                return Serial.Zero;
            }

            Interpreter.PopList(listName, evaluatedVar);
            return evaluatedVar.AsSerial();
        }

        private static int ItemCount(string expression, Variable[] args, bool quiet, bool force)
        {
            return World.Player?.Backpack == null ? 0 : World.Player.Backpack.GetTotalCount();
        }

        private static string Name(string expression, Variable[] args, bool quiet, bool force)
        {
            return World.Player == null ? string.Empty : World.Player.Name;
        }

        private static bool Paralyzed(string expression, Variable[] args, bool quiet, bool force)
        {
            return World.Player != null && World.Player.Paralyzed;
        }

        private static bool Invulnerable(string expression, Variable[] args, bool quiet, bool force)
        {
            return World.Player != null && World.Player.Blessed;
        }

        private static bool Warmode(string expression, Variable[] args, bool quiet, bool force)
        {
            return World.Player != null && World.Player.Warmode;
        }

        private static int MaxFollowers(string expression, Variable[] args, bool quiet, bool force)
        {
            return World.Player == null ? 0 : World.Player.FollowersMax;
        }

        private static int Followers(string expression, Variable[] args, bool quiet, bool force)
        {
            return World.Player == null ? 0 : World.Player.Followers;
        }

        private static bool VarExist(string expression, Variable[] vars, bool quiet, bool force)
        {
            if (vars.Length != 1)
            {
                throw new RunTimeError("Usage: varexist ('name')");
            }

            string varName = vars[0].AsString(false);

            return quiet ? Interpreter.ExistVariable(varName) : Interpreter.ExistAlias(varName);
        }

        private static bool Queued(string expression, Variable[] vars, bool quiet, bool force)
        {
            return !ActionQueue.Empty;
        }

        private static bool FindBuffDebuff(string expression, Variable[] vars, bool quiet, bool force)
        {
            if (vars.Length == 0)
            {
                throw new RunTimeError("Usage: findbuff/finddebuff ('name of buff')");
            }

            string name = vars[0].AsString();

            if (World.Player == null)
                return false;

            foreach (BuffDebuff buff in World.Player.BuffsDebuffs)
            {
                if (buff.ClilocMessage1.IndexOf(name, StringComparison.OrdinalIgnoreCase) != -1)
                {
                    return true;
                }
            }

            return false;
        }

        private static uint FindType(string expression, Variable[] vars, bool quiet, bool force)
        {
            if (vars.Length == 0)
            {
                throw new RunTimeError("Usage: findtype ('name of item'/'graphicID') [source] [hue] [quantity] [range]");
            }

            // Outlands-Grammatik (Obermenge von CE): source = backpack/self/
            // ground/Container-Serial/true/false, 'any' = Platzhalter,
            // Namens-Wildcards (%s%). Siehe CommandHelper.ParseFindArgs.
            string gfxStr = vars[0].AsString();
            CommandHelper.FindArgs args = CommandHelper.ParseFindArgs(vars);

            List<Item> items = CommandHelper.GetItems(gfxStr, args);

            if (items.Count > 0)
            {
                return items[Utility.Random(items.Count)].Serial;
            }

            // Kein Item gefunden -> Mobile-Suche (Name oder Body-ID, wie CE).
            List<Mobile> mobiles = CommandHelper.GetMobiles(gfxStr, args);

            if (mobiles.Count > 0)
            {
                return mobiles[Utility.Random(mobiles.Count)].Serial;
            }

            return Serial.Zero;
        }

        private static bool Mounted(string expression, Variable[] vars, bool quiet, bool force)
        {
            return World.Player != null && World.Player.GetItemOnLayer(Layer.Mount) != null;
        }

        private static bool RHandEmpty(string expression, Variable[] vars, bool quiet, bool force)
        {
            return World.Player != null && World.Player.GetItemOnLayer(Layer.RightHand) == null;
        }

        private static bool LHandEmpty(string expression, Variable[] vars, bool quiet, bool force)
        {
            return World.Player != null && World.Player.GetItemOnLayer(Layer.LeftHand) == null;
        }

        private static bool Dead(string expression, Variable[] vars, bool quiet, bool force)
        {
            // Outlands: dead [serial] — ohne Argument der Spieler, sonst das
            // angegebene Mobile (frueher wurde der Parameter still ignoriert).
            if (vars.Length > 0)
            {
                Mobile mobile = World.FindMobile(vars[0].AsSerial());

                // Unbekanntes Mobile nicht als "tot" werten (ein geloeschtes/
                // fernes Mobile ist schlicht unbekannt, kein Geist).
                return mobile != null && mobile.IsGhost;
            }

            return World.Player != null && World.Player.IsGhost;
        }

        private static bool InSysMessage(string expression, Variable[] vars, bool quiet, bool force)
        {
            if (vars.Length == 0)
            {
                throw new RunTimeError("Usage: insysmsg ('text')");
            }

            string text = vars[0].AsString();

            return SystemMessages.Exists(text);
        }

        private static int Mana(string expression, Variable[] vars, bool quiet, bool force)
        {
            if (World.Player == null)
                return 0;

            return World.Player.Mana;
        }

        private static int MaxMana(string expression, Variable[] vars, bool quiet, bool force)
        {
            if (World.Player == null)
                return 0;

            return World.Player.ManaMax;
        }

        private static bool Poisoned(string expression, Variable[] vars, bool quiet, bool force)
        {
            // Razor CE gated dies zusaetzlich mit AllowBit(BlockHealPoisoned);
            // der Port kennt keine FeatureBits -> reiner Poisoned-Status.
            return World.Player != null && World.Player.Poisoned;
        }

        private static bool Hidden(string expression, Variable[] vars, bool quiet, bool force)
        {
            return World.Player != null && !World.Player.Visible;
        }

        private static int Hp(string expression, Variable[] vars, bool quiet, bool force)
        {
            if (World.Player == null)
                return 0;

            return World.Player.Hits;
        }

        private static int MaxHp(string expression, Variable[] vars, bool quiet, bool force)
        {
            if (World.Player == null)
                return 0;

            return World.Player.HitsMax;
        }

        private static int Stam(string expression, Variable[] vars, bool quiet, bool force)
        {
            if (World.Player == null)
                return 0;

            return World.Player.Stam;
        }

        private static int MaxStam(string expression, Variable[] vars, bool quiet, bool force)
        {
            if (World.Player == null)
                return 0;

            return World.Player.StamMax;
        }

        private static int Str(string expression, Variable[] vars, bool quiet, bool force)
        {
            if (World.Player == null)
                return 0;

            return World.Player.Str;
        }

        private static int Dex(string expression, Variable[] vars, bool quiet, bool force)
        {
            if (World.Player == null)
                return 0;

            return World.Player.Dex;
        }

        private static int Int(string expression, Variable[] vars, bool quiet, bool force)
        {
            if (World.Player == null)
                return 0;

            return World.Player.Int;
        }

        private static int Weight(string expression, Variable[] vars, bool quiet, bool force)
        {
            if (World.Player == null)
                return 0;

            return World.Player.Weight;
        }

        private static int MaxWeight(string expression, Variable[] vars, bool quiet, bool force)
        {
            if (World.Player == null)
                return 0;

            return World.Player.MaxWeight;
        }

        private static double SkillExpression(string expression, Variable[] vars, bool quiet, bool force)
        {
            if (vars.Length < 1)
                throw new RunTimeError("Usage: skill ('name of skill')");

            if (World.Player == null)
                return 0;

            if (Skills.SkillsByName.TryGetValue(vars[0].AsString(), out var skill))
            {
                return force ? World.Player.Skills[skill.Index].Base : World.Player.Skills[skill.Index].Value;
            }

            CommandHelper.SendWarning(expression, $"Skill '{vars[0].AsString()}' not found", quiet);

            return 0;
        }

        private static int CountExpression(string expression, Variable[] vars, bool quiet, bool force)
        {
            if (vars.Length < 1)
                throw new RunTimeError("Usage: count ('name of counter') OR count ('name of item' OR graphicID) [hue]");

            if (World.Player == null)
                return 0;

            var counter = Counter.FindCounter(vars[0].AsString());
            if (counter != null)
            {
                return counter.Amount;
            }

            string gfxStr = vars[0].AsString();
            Serial gfx = Utility.ToUInt16(gfxStr, 0);
            ushort hue = 0xFFFF;

            if (vars.Length == 2)
            {
                hue = Utility.ToUInt16(vars[1].AsString(), 0xFFFF);
            }

            // No graphic id, maybe searching by name?
            if (gfx == 0)
            {
                var items = CommandHelper.GetItemsByName(gfxStr, true, false, -1);

                return items.Count == 0 ? 0 : Counter.GetCount(items[0].ItemID, hue);
            }

            return Counter.GetCount(new ItemID((ushort)gfx.Value), hue);
        }

        private static bool Position(string expression, Variable[] vars, bool quiet, bool force)
        {
            if (World.Player == null)
                return false;

            if (vars.Length < 2)
            {
                throw new RunTimeError("Usage: position (x, y) or position (x, y, z)");
            }

            int x = vars[0].AsInt();
            int y = vars[1].AsInt();
            int z = (vars.Length > 2)
                ? vars[2].AsInt()
                : World.Player.Position.Z;

            return World.Player.Position.X == x
                && World.Player.Position.Y == y
                && World.Player.Position.Z == z;
        }

        private static readonly Dictionary<string, byte> TargetMap = new Dictionary<string, byte>
        {
            {"neutral", 0},
            {"harmful", 1},
            {"beneficial", 2},
            {"any", 3}
        };

        private static bool TargetExists(string expression, Variable[] args, bool quiet, bool force)
        {
            byte type = 3;

            if (args.Length > 0)
            {
                if (!TargetMap.TryGetValue(args[0].AsString().ToLower(), out type))
                {
                    throw new RunTimeError("Invalid target type: 0 = neutral, 1 = harmful, 2 = beneficial, any = 3");
                }
            }

            if (!Targeting.HasTarget)
                return false;

            if (type == 3)
                return true;

            return Targeting.CursorType == type;
        }

        private static int DiffWeight(string expression, Variable[] args, bool quiet, bool force)
        {
            if (World.Player == null)
                return 0;

            return World.Player.MaxWeight - World.Player.Weight;
        }

        private static int DiffHits(string expression, Variable[] args, bool quiet, bool force)
        {
            if (World.Player == null)
                return 0;

            return World.Player.HitsMax - World.Player.Hits;
        }

        private static int DiffStam(string expression, Variable[] args, bool quiet, bool force)
        {
            if (World.Player == null)
                return 0;

            return World.Player.StamMax - World.Player.Stam;
        }

        private static int DiffMana(string expression, Variable[] args, bool quiet, bool force)
        {
            if (World.Player == null)
                return 0;

            return World.Player.ManaMax - World.Player.Mana;
        }
    }
}
