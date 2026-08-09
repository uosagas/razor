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

// Portiert aus Razor CE (Razor/Scripts/Helpers/CommandHelper.cs) — 1:1.
// Abweichung: using Assistant.Filters entfaellt (im Port nicht vorhanden,
// wurde im Original nicht referenziert).

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Assistant.Scripts.Engine;

namespace Assistant.Scripts.Helpers
{
    public static class CommandHelper
    {
        /// <summary>
        /// Common logic for dclicktype and targettype to find items by name
        /// </summary>
        public static List<Item> GetItemsByName(string name, bool backpack, bool inRange, int hue)
        {
            List<Item> items = new List<Item>();

            if (backpack && World.Player.Backpack != null) // search backpack only
            {
                items.AddRange(World.Player.Backpack.FindItemsByName(name, true).Where(item => !Interpreter.CheckIgnored(item.Serial)));
            }
            else if (inRange) // inrange includes both backpack and within 2 tiles
            {
                items.AddRange(World.FindItemsByName(name).Where(item =>
                    !item.IsInBank && !Interpreter.CheckIgnored(item.Serial) && (Utility.InRange(World.Player.Position, item.Position, 2) ||
                                       item.RootContainer == World.Player)));
            }
            else
            {
                items.AddRange(World.FindItemsByName(name).Where(item => !item.IsInBank && !Interpreter.CheckIgnored(item.Serial)));
            }

            if (hue > -1)
            {
                items.RemoveAll(item => item.Hue != hue);
            }

            return items;
        }

        /// <summary>
        /// Common logic for dclicktype and targettype to find items by id
        /// </summary>
        public static List<Item> GetItemsById(ushort id, bool backpack, bool inRange, int hue)
        {
            List<Item> items = new List<Item>();

            if (backpack && World.Player.Backpack != null)
            {
                items.AddRange(World.Player.Backpack.FindItemsById(id, true).Where(item => !Interpreter.CheckIgnored(item.Serial)));
            }
            else if (inRange)
            {
                items.AddRange(World.FindItemsById(id).Where(item =>
                    !item.IsInBank && !Interpreter.CheckIgnored(item.Serial) && (Utility.InRange(World.Player.Position, item.Position, 2) ||
                                                                              item.RootContainer == World.Player)));
            }
            else
            {
                items.AddRange(World.FindItemsById(id).Where(item => !item.IsInBank && !Interpreter.CheckIgnored(item.Serial)));
            }

            if (hue > -1)
            {
                items.RemoveAll(item => item.Hue != hue);
            }

            return items;
        }

        /// <summary>
        /// Common logic for dclicktype and targettype to find mobiles by name
        /// </summary>
        public static List<Mobile> GetMobilesByName(string name, bool inRange)
        {
            List<Mobile> mobiles = new List<Mobile>();

            mobiles.AddRange(inRange
                ? World.FindMobilesByName(name).Where(m => !Interpreter.CheckIgnored(m.Serial) && Utility.InRange(World.Player.Position, m.Position, 2))
                : World.FindMobilesByName(name).Where(m => !Interpreter.CheckIgnored(m.Serial)));

            return mobiles;
        }

        /// <summary>
        /// Common logic for dclicktype and targettype to find mobiles by id
        /// </summary>
        public static List<Mobile> GetMobilesById(ushort id, bool inRange)
        {
            List<Mobile> mobiles = new List<Mobile>();

            mobiles.AddRange(inRange
                ? World.MobilesInRange().Where(m =>
                    Utility.InRange(World.Player.Position, m.Position, 2) && m.Body == id &&
                    !Interpreter.CheckIgnored(m.Serial))
                : World.MobilesInRange().Where(m => m.Body == id && !Interpreter.CheckIgnored(m.Serial)));

            return mobiles;
        }

        // ---- Outlands-Obermenge: toleranter Argument-Parser + Suche ------------
        // Grammatik (wiki.uooutlands.com/Razor_Scripting):
        //   findtype ('name'/'graphic') [source] [hue] [quantity] [range]
        //     source  = 'backpack' | 'self' (am Koerper inkl. Container) |
        //               'ground' | Container-Serial | true/false (CE-inrange)
        //     'any'   = Platzhalter auf jeder Position
        //   Namens-Wildcards wie "clean bandage%s%" (roher Tiledata-Plural-
        //   marker; %...%-Segment ist optional).
        // CE wirft bei 'self' "Cannot convert argument to bool" — wir parsen
        // positionsbasiert und tolerant (unbekannte Tokens werden ignoriert).

        public sealed class FindArgs
        {
            public bool Backpack;
            public bool Self;
            public bool Ground;
            public bool InRange;          // CE-Altform: true/false als 2. Arg
            public Serial Container = Serial.Zero;
            public int Hue = -1;
            public int MinQuantity = -1;
            public int Range = -1;
        }

        /// <summary>Parst findtype/dclicktype/targettype-Argumente ab Index start (tolerant, nie werfend).</summary>
        public static FindArgs ParseFindArgs(Variable[] vars, int start = 1)
        {
            FindArgs result = new FindArgs();
            int position = 0; // 0=source, 1=hue, 2=quantity, 3=range

            for (int i = start; i < vars.Length; i++)
            {
                string tok = vars[i].AsString();
                bool isAny = string.IsNullOrEmpty(tok) || tok.Equals("any", StringComparison.OrdinalIgnoreCase);

                switch (position)
                {
                    case 0: // source
                        if (!isAny)
                        {
                            if (tok.IndexOf("pack", StringComparison.OrdinalIgnoreCase) >= 0)
                                result.Backpack = true;
                            else if (tok.Equals("self", StringComparison.OrdinalIgnoreCase))
                                result.Self = true;
                            else if (tok.Equals("ground", StringComparison.OrdinalIgnoreCase))
                                result.Ground = true;
                            else if (bool.TryParse(tok, out bool inRange))
                                result.InRange = inRange;
                            else if (tok.StartsWith("0x") && Utility.ToUInt32(tok, 0) > 0)
                                result.Container = Utility.ToUInt32(tok, 0);
                            // Unbekanntes Token: ignorieren.
                        }

                        break;
                    case 1: // hue
                        if (!isAny)
                            result.Hue = Utility.ToInt32(tok, -1);
                        break;
                    case 2: // quantity
                        if (!isAny)
                            result.MinQuantity = Utility.ToInt32(tok, -1);
                        break;
                    case 3: // range
                        if (!isAny)
                            result.Range = Utility.ToInt32(tok, -1);
                        break;
                }

                position++;
            }

            return result;
        }

        /// <summary>
        /// Tiledata-Namensvergleich mit Outlands-Wildcard: Segmente zwischen
        /// %...% sind optional ("clean bandage%s%" matcht Singular + Plural).
        /// </summary>
        public static bool NameMatches(string tileName, string pattern)
        {
            if (tileName == null || pattern == null)
                return false;

            if (pattern.IndexOf('%') < 0)
                return string.Equals(tileName, pattern, StringComparison.OrdinalIgnoreCase);

            string[] parts = pattern.Split('%');
            var regex = new System.Text.StringBuilder("^");

            for (int i = 0; i < parts.Length; i++)
            {
                if (parts[i].Length == 0)
                    continue;

                string escaped = Regex.Escape(parts[i]);
                if (i % 2 == 1) // Segment zwischen % ... % -> optional
                    regex.Append("(?:").Append(escaped).Append(")?");
                else
                    regex.Append(escaped);
            }

            regex.Append('$');

            return Regex.IsMatch(tileName, regex.ToString(), RegexOptions.IgnoreCase);
        }

        /// <summary>Items per Name (Wildcard-faehig) oder Graphic-ID gemaess FindArgs suchen.</summary>
        public static List<Item> GetItems(string gfxStr, FindArgs args)
        {
            ushort gfx = Utility.ToUInt16(gfxStr, 0);
            bool byName = gfx == 0;

            bool Matches(Item item)
            {
                if (Interpreter.CheckIgnored(item.Serial))
                    return false;

                if (byName)
                {
                    if (!NameMatches(ItemData.GetName(item.ItemID.Value), gfxStr))
                        return false;
                }
                else if (item.ItemID != gfx)
                {
                    return false;
                }

                if (args.Hue > -1 && item.Hue != args.Hue)
                    return false;

                return args.MinQuantity <= 0 || item.Amount >= args.MinQuantity;
            }

            List<Item> items = new List<Item>();

            if (args.Backpack && World.Player?.Backpack != null)
            {
                CollectRecursive(World.Player.Backpack, Matches, items);
            }
            else if (args.Self && World.Player != null)
            {
                // 'self' (Outlands): alles am Koerper — getragene Items inkl.
                // Container-Inhalt (RootContainer == Player).
                foreach (Item worn in World.Player.Contains)
                {
                    if (Matches(worn))
                        items.Add(worn);

                    CollectRecursive(worn, Matches, items);
                }
            }
            else if (args.Container != Serial.Zero)
            {
                Item container = World.FindItem(args.Container);
                if (container != null)
                    CollectRecursive(container, Matches, items);
            }
            else if (args.Ground)
            {
                int range = args.Range > 0 ? args.Range : 18;
                items.AddRange(World.Items.Values.Where(item =>
                    item.OnGround && Matches(item) &&
                    Utility.InRange(World.Player.Position, item.Position, range)));
            }
            else if (args.InRange || args.Range > 0)
            {
                int range = args.Range > 0 ? args.Range : 2;
                items.AddRange(World.Items.Values.Where(item =>
                    !item.IsInBank && Matches(item) &&
                    (Utility.InRange(World.Player.Position, item.Position, range) ||
                     item.RootContainer == World.Player)));
            }
            else
            {
                items.AddRange(World.Items.Values.Where(item => !item.IsInBank && Matches(item)));
            }

            return items;
        }

        private static void CollectRecursive(Item container, Func<Item, bool> matches, List<Item> result)
        {
            foreach (Item item in container.Contains)
            {
                if (matches(item))
                    result.Add(item);

                CollectRecursive(item, matches, result);
            }
        }

        /// <summary>Mobiles per Name oder Body-ID gemaess FindArgs suchen (Container-Quellen ergeben keine Mobiles).</summary>
        public static List<Mobile> GetMobiles(string gfxStr, FindArgs args)
        {
            if (args.Backpack || args.Self || args.Container != Serial.Zero)
                return new List<Mobile>();

            ushort gfx = Utility.ToUInt16(gfxStr, 0);

            return gfx == 0
                ? GetMobilesByName(gfxStr, args.InRange)
                : GetMobilesById(gfx, args.InRange);
        }

        public static void SendWarning(string command, string message, bool quiet)
        {
            if (!quiet)
            {
                World.Player.SendMessage(MsgLevel.Warning, $"{command} - {message}");
            }
        }

        public static void SendMessage(string message, bool quiet)
        {
            if (!quiet)
            {
                World.Player.SendMessage(MsgLevel.Force, message);
            }
        }

        public static void SendInfo(string message, bool quiet)
        {
            if (!quiet)
            {
                World.Player.SendMessage(MsgLevel.Info, message);
            }
        }

        /// <summary>
        /// Parse the script input to target the correct mobile
        /// </summary>
        public static void FindTarget(Variable[] args, bool closest, bool random = false, bool next = false, bool prev = false)
        {
            ScriptManager.TargetFound = false;

            // Do a basic t
            if (args.Length == 1)
            {
                if (closest)
                {
                    Targeting.TargetClosest();
                }
                else if (random)
                {
                    Targeting.TargetRandAnyone();
                }
                else if (next)
                {
                    Targeting.NextTarget();
                }
                else
                {
                    Targeting.PrevTarget();
                }
            }
            else if ((next || prev) && args.Length == 2)
            {
                switch (args[1].AsString())
                {
                    case "human":
                    case "humanoid":

                        if (next)
                        {
                            Targeting.NextTargetHumanoid();
                        }
                        else
                        {
                            Targeting.PrevTargetHumanoid();
                        }

                        break;
                    case "monster":
                        if (next)
                        {
                            Targeting.NextTargetMonster();
                        }
                        else
                        {
                            Targeting.PrevTargetMonster();
                        }

                        break;
                    case "friend":
                        if (next)
                        {
                            Targeting.NextTargetFriend();
                        }
                        else
                        {
                            Targeting.PrevTargetFriend();
                        }

                        break;
                    case "friendly":
                        if (next)
                        {
                            Targeting.NextTargetFriendly();
                        }
                        else
                        {
                            Targeting.PrevTargetFriendly();
                        }

                        break;
                    case "nonfriendly":
                        if (next)
                        {
                            Targeting.NextTargetNonFriend();
                        }
                        else
                        {
                            Targeting.PrevTargetNonFriend();
                        }

                        break;
                    default:
                        throw new RunTimeError($"Unknown target type: '{args[1].AsString()}' - Missing type? (human/monster)");
                }
            }
            else if (args.Length > 1)
            {
                string list = args[1].AsString();

                if (list.IndexOf('!') != -1)
                {
                    FindTargetPriority(args, closest, random, next);
                }
                else if (list.IndexOf(',') != -1)
                {
                    FindTargetNotoriety(args, closest, random, next);
                }
                else
                {
                    FindTargetPriority(args, closest, random, next);
                }
            }
        }

        /// <summary>
        /// Find targets based on notoriety
        /// </summary>
        private static void FindTargetNotoriety(Variable[] args, bool closest, bool random, bool next)
        {
            string[] notoList = args[1].AsString().Split(',');

            List<int> notoTypes = new List<int>();

            foreach (string notoRaw in notoList)
            {
                // Outlands-Aliase auf CE-TargetTypes mappen.
                string noto = notoRaw.Trim().ToLowerInvariant() switch
                {
                    "hostile" => "Attackable",
                    "invulnerable" => "Invalid",
                    _ => notoRaw
                };

                Targeting.TargetType type = (Targeting.TargetType)Enum.Parse(typeof(Targeting.TargetType), noto, true);

                /*NonFriendly, //Attackable, Criminal, Enemy, Murderer
                Friendly, //Innocent, Guild/Ally
                Red, //Murderer
                Blue, //Innocent
                Gray, //Attackable, Criminal
                Grey, //Attackable, Criminal
                Green, //GuildAlly
                Guild, //GuildAlly*/

                switch (type)
                {
                    case Targeting.TargetType.Friendly:
                        notoTypes.Add((int) Targeting.TargetType.Innocent);
                        notoTypes.Add((int) Targeting.TargetType.GuildAlly);
                        break;
                    case Targeting.TargetType.NonFriendly:
                        notoTypes.Add((int)Targeting.TargetType.Attackable);
                        notoTypes.Add((int)Targeting.TargetType.Criminal);
                        notoTypes.Add((int)Targeting.TargetType.Enemy);
                        notoTypes.Add((int)Targeting.TargetType.Murderer);
                        break;
                    case Targeting.TargetType.Red:
                        notoTypes.Add((int)Targeting.TargetType.Murderer);
                        break;
                    case Targeting.TargetType.Blue:
                        notoTypes.Add((int)Targeting.TargetType.Innocent);
                        break;
                    case Targeting.TargetType.Gray:
                    case Targeting.TargetType.Grey:
                        notoTypes.Add((int)Targeting.TargetType.Attackable);
                        notoTypes.Add((int)Targeting.TargetType.Criminal);
                        break;
                    case Targeting.TargetType.Green:
                    case Targeting.TargetType.Guild:
                        notoTypes.Add((int)Targeting.TargetType.GuildAlly);
                        break;
                    default:
                        notoTypes.Add((int)type);
                        break;
                }
            }

            if (args.Length == 3)
            {
                if (args[2].AsString().IndexOf("human", StringComparison.OrdinalIgnoreCase) != -1)
                {
                    if (closest)
                    {
                        Targeting.ClosestHumanoidTarget(notoTypes.ToArray());
                    }
                    else if (random)
                    {
                        Targeting.RandomHumanoidTarget(notoTypes.ToArray());
                    }
                    else if (next)
                    {
                        Targeting.NextPrevTargetNotorietyHumanoid(true, notoTypes.ToArray());
                    }
                    else
                    {
                        Targeting.NextPrevTargetNotorietyHumanoid(false, notoTypes.ToArray());
                    }
                }
                else if (args[2].AsString().IndexOf("monster", StringComparison.OrdinalIgnoreCase) != -1)
                {
                    if (closest)
                    {
                        Targeting.ClosestMonsterTarget(notoTypes.ToArray());
                    }
                    else if (random)
                    {
                        Targeting.RandomMonsterTarget(notoTypes.ToArray());
                    }
                    else if (next)
                    {
                        Targeting.NextPrevTargetNotorietyMonster(true, notoTypes.ToArray());
                    }
                    else
                    {
                        Targeting.NextPrevTargetNotorietyMonster(false, notoTypes.ToArray());
                    }
                }
            }
            else
            {
                if (closest)
                {
                    Targeting.ClosestTarget(notoTypes.ToArray());
                }
                else if (random)
                {
                    Targeting.RandomTarget(notoTypes.ToArray());
                }
                else if (next)
                {
                    Targeting.NextPrevTargetNotoriety(true, notoTypes.ToArray());
                }
                else
                {
                    Targeting.NextPrevTargetNotoriety(false, notoTypes.ToArray());
                }
            }
        }

        /// <summary>
        /// Find a target based on a priority list of notorieties
        /// </summary>
        private static void FindTargetPriority(Variable[] args, bool closest, bool random, bool next)
        {
            string[] notoList = args[1].AsString().Split('!');

            foreach (string noto in notoList)
            {
                if (ScriptManager.TargetFound)
                {
                    break;
                }

                switch (noto)
                {
                    case "enemy":
                        if (args.Length == 3)
                        {
                            if (args[2].AsString().IndexOf("human", StringComparison.OrdinalIgnoreCase) != -1)
                            {
                                if (closest)
                                {
                                    Targeting.TargetCloseEnemyHumanoid();
                                }
                                else if (random)
                                {
                                    Targeting.TargetRandEnemyHumanoid();
                                }
                                else if (next)
                                {
                                    Targeting.NextTargetEnemyHumanoid();
                                }
                                else
                                {
                                    Targeting.PrevTargetEnemyHumanoid();
                                }
                            }
                            else if (args[2].AsString()
                                .IndexOf("monster", StringComparison.OrdinalIgnoreCase) != -1)
                            {
                                if (closest)
                                {
                                    Targeting.TargetCloseEnemyMonster();
                                }
                                else if (random)
                                {
                                    Targeting.TargetRandEnemyMonster();
                                }
                                else if (next)
                                {
                                    Targeting.NextTargetEnemyMonster();
                                }
                                else
                                {
                                    Targeting.PrevTargetEnemyMonster();
                                }
                            }
                        }
                        else
                        {
                            if (closest)
                            {
                                Targeting.TargetCloseEnemy();
                            }
                            else if (random)
                            {
                                Targeting.TargetRandEnemy();
                            }
                        }

                        break;
                    case "friend":
                        if (closest)
                        {
                            Targeting.TargetClosestFriend();
                        }
                        else if (random)
                        {
                            Targeting.TargetRandFriend();
                        }
                        else if (next)
                        {
                            Targeting.NextTargetFriend();
                        }
                        else
                        {
                            Targeting.PrevTargetFriend();
                        }

                        break;
                    case "friendly":
                        if (args.Length == 3)
                        {
                            if (args[2].AsString().IndexOf("human", StringComparison.OrdinalIgnoreCase) != -1)
                            {
                                if (closest)
                                {
                                    Targeting.TargetCloseFriendlyHumanoid();
                                }
                                else if (random)
                                {
                                    Targeting.TargetRandFriendlyHumanoid();
                                }
                                else if (next)
                                {
                                    Targeting.NextTargetFriendlyHumanoid();
                                }
                                else
                                {
                                    Targeting.PrevTargetFriendlyHumanoid();
                                }
                            }
                            else if (args[2].AsString()
                                .IndexOf("monster", StringComparison.OrdinalIgnoreCase) != -1)
                            {
                                if (closest)
                                {
                                    Targeting.TargetCloseFriendlyMonster();
                                }
                                else if (random)
                                {
                                    Targeting.TargetRandFriendlyMonster();
                                }
                                else if (next)
                                {
                                    Targeting.NextTargetFriendlyMonster();
                                }
                                else
                                {
                                    Targeting.PrevTargetFriendlyMonster();
                                }
                            }
                        }
                        else
                        {
                            if (closest)
                            {
                                Targeting.TargetCloseFriendly();
                            }
                            else if (random)
                            {
                                Targeting.TargetRandFriendly();
                            }
                        }

                        break;
                    case "gray":
                    case "grey":
                        if (args.Length == 3)
                        {
                            if (args[2].AsString().IndexOf("human", StringComparison.OrdinalIgnoreCase) != -1)
                            {
                                if (closest)
                                {
                                    Targeting.TargetCloseGreyHumanoid();
                                }
                                else if (random)
                                {
                                    Targeting.TargetRandGreyHumanoid();
                                }
                                else if (next)
                                {
                                    Targeting.NextTargetGreyHumanoid();
                                }
                                else
                                {
                                    Targeting.PrevTargetGreyHumanoid();
                                }
                            }
                            else if (args[2].AsString()
                                .IndexOf("monster", StringComparison.OrdinalIgnoreCase) != -1)
                            {
                                if (closest)
                                {
                                    Targeting.TargetCloseGreyMonster();
                                }
                                else if (random)
                                {
                                    Targeting.TargetRandGreyMonster();
                                }
                                else if (next)
                                {
                                    Targeting.NextTargetGreyMonster();
                                }
                                else
                                {
                                    Targeting.PrevTargetGreyMonster();
                                }
                            }
                        }
                        else
                        {
                            if (closest)
                            {
                                Targeting.TargetCloseGrey();
                            }
                            else if (random)
                            {
                                Targeting.TargetRandGrey();
                            }
                        }

                        break;
                    case "criminal":
                        if (args.Length == 3)
                        {
                            if (args[2].AsString().IndexOf("human", StringComparison.OrdinalIgnoreCase) != -1)
                            {
                                if (closest)
                                {
                                    Targeting.TargetCloseCriminalHumanoid();
                                }
                                else if (random)
                                {
                                    Targeting.TargetRandCriminalHumanoid();
                                }
                                else if (next)
                                {
                                    Targeting.NextTargetCriminalHumanoid();
                                }
                                else
                                {
                                    Targeting.PrevTargetCriminalHumanoid();
                                }
                            }
                            else if (args[2].AsString()
                                .IndexOf("monster", StringComparison.OrdinalIgnoreCase) != -1)
                            {
                                if (closest)
                                {
                                    Targeting.TargetCloseCriminalMonster();
                                }
                                else if (random)
                                {
                                    Targeting.TargetRandCriminalMonster();
                                }
                                else if (next)
                                {
                                    Targeting.NextTargetCriminalMonster();
                                }
                                else
                                {
                                    Targeting.PrevTargetCriminalMonster();
                                }
                            }
                        }
                        else
                        {
                            if (closest)
                            {
                                Targeting.TargetCloseCriminal();
                            }
                            else if (random)
                            {
                                Targeting.TargetRandCriminal();
                            }
                        }

                        break;
                    case "blue":
                    case "innocent":
                        if (args.Length == 3)
                        {
                            if (args[2].AsString().IndexOf("human", StringComparison.OrdinalIgnoreCase) != -1)
                            {
                                if (closest)
                                {
                                    Targeting.TargetCloseInnocentHumanoid();
                                }
                                else if (random)
                                {
                                    Targeting.TargetRandInnocentHumanoid();
                                }
                                else if (next)
                                {
                                    Targeting.NextTargetInnocentHumanoid();
                                }
                                else
                                {
                                    Targeting.PrevTargetInnocentHumanoid();
                                }
                            }
                            else if (args[2].AsString()
                                .IndexOf("monster", StringComparison.OrdinalIgnoreCase) != -1)
                            {
                                if (closest)
                                {
                                    Targeting.TargetCloseInnocentMonster();
                                }
                                else if (random)
                                {
                                    Targeting.TargetRandInnocentMonster();
                                }
                                else if (next)
                                {
                                    Targeting.NextTargetInnocentMonster();
                                }
                                else
                                {
                                    Targeting.PrevTargetInnocentMonster();
                                }
                            }
                        }
                        else
                        {
                            if (closest)
                            {
                                Targeting.TargetCloseInnocent();
                            }
                            else if (random)
                            {
                                Targeting.TargetRandInnocent();
                            }
                        }

                        break;
                    case "red":
                    case "murderer":
                        if (args.Length == 3)
                        {
                            if (args[2].AsString().IndexOf("human", StringComparison.OrdinalIgnoreCase) != -1)
                            {
                                if (closest)
                                {
                                    Targeting.TargetCloseRedHumanoid();
                                }
                                else if (random)
                                {
                                    Targeting.TargetRandRedHumanoid();
                                }
                                else if (next)
                                {
                                    Targeting.NextTargetMurdererHumanoid();
                                }
                                else
                                {
                                    Targeting.PrevTargetMurdererHumanoid();
                                }
                            }
                            else if (args[2].AsString()
                                .IndexOf("monster", StringComparison.OrdinalIgnoreCase) != -1)
                            {
                                if (closest)
                                {
                                    Targeting.TargetCloseRedMonster();
                                }
                                else if (random)
                                {
                                    Targeting.TargetRandRedMonster();
                                }
                                else if (next)
                                {
                                    Targeting.NextTargetMurdererMonster();
                                }
                                else
                                {
                                    Targeting.PrevTargetMurdererMonster();
                                }
                            }
                        }
                        else
                        {
                            if (closest)
                            {
                                Targeting.TargetCloseRed();
                            }
                            else if (random)
                            {
                                Targeting.TargetRandRed();
                            }
                        }

                        break;
                    case "nonfriendly":
                        if (args.Length == 3)
                        {
                            if (args[2].AsString().IndexOf("human", StringComparison.OrdinalIgnoreCase) != -1)
                            {
                                if (closest)
                                {
                                    Targeting.TargetCloseNonFriendlyHumanoid();
                                }
                                else if (random)
                                {
                                    Targeting.TargetRandNonFriendlyHumanoid();
                                }
                                else if (next)
                                {
                                    Targeting.NextTargetNonFriendlyHumanoid();
                                }
                                else
                                {
                                    Targeting.PrevTargetNonFriendlyHumanoid();
                                }
                            }
                            else if (args[2].AsString()
                                .IndexOf("monster", StringComparison.OrdinalIgnoreCase) != -1)
                            {
                                if (closest)
                                {
                                    Targeting.TargetCloseNonFriendlyMonster();
                                }
                                else if (random)
                                {
                                    Targeting.TargetRandNonFriendlyMonster();
                                }
                                else if (next)
                                {
                                    Targeting.NextTargetNonFriendlyMonster();
                                }
                                else
                                {
                                    Targeting.PrevTargetNonFriendlyMonster();
                                }
                            }
                        }
                        else
                        {
                            if (closest)
                            {
                                Targeting.TargetCloseNonFriendly();
                            }
                            else if (random)
                            {
                                Targeting.TargetRandNonFriendly();
                            }
                        }

                        break;
                    default:
                        throw new RunTimeError($"Unknown target type: '{args[1].AsString()}'");
                }
            }
        }

        public static string ReplaceStringInterpolations(string stringWithPossibleInterpolation)
        {
            Regex regex = new Regex(@"\{{(.*?)\}}");
            return regex.Replace(stringWithPossibleInterpolation, match =>
            {
                string content = match.Groups[1].Value;

                // CE/Outlands-Verhalten zuerst: Variablen/Aliase gewinnen immer,
                // damit bestehende {{myvar}}-Scripts unveraendert laufen.
                Variable varContent = Interpreter.GetVariable(content);
                if (varContent != null)
                    return varContent.AsString();

                // UOSagas-Erweiterung: der Inhalt darf eine Expression sein,
                // z. B. {{skill 'Alchemy'}} oder {{counttype 3821 backpack}}.
                return EvaluateInterpolatedExpression(content) ?? "<not found>";
            });
        }

        // Eigene Instanz statt Lexer._tfp: gleiche Quote-Konfiguration, aber der
        // Lexer-Parser haelt Zustand und koennte mitten im Lexen stehen.
        private static readonly TextParser _interpolationParser =
            new TextParser("", new[] { ' ' }, new char[] { }, new[] { '\'', '\'', '"', '"' });

        /// <summary>
        /// Wertet den Inhalt einer {{...}}-Interpolation als registrierte
        /// Expression aus (erstes Token = Keyword, Rest = Argumente, Quotes wie
        /// im Script). Liefert null, wenn es keine Expression ist oder die
        /// Auswertung fehlschlaegt — der Aufrufer zeigt dann "&lt;not found&gt;".
        /// </summary>
        private static string EvaluateInterpolatedExpression(string content)
        {
            try
            {
                string[] tokens = _interpolationParser.GetTokens(content.Trim());
                if (tokens.Length == 0)
                    return null;

                var handler = Interpreter.GetExpressionHandler(tokens[0]);
                if (handler == null)
                    return null;

                Variable[] args = tokens.Skip(1).Select(t => new Variable(t)).ToArray();

                // quiet, damit eine fehlgeschlagene Auswertung keine Warnungen
                // in die Systemmeldungen spammt (die Ausgabe selbst IST die Meldung).
                IComparable result = handler(tokens[0], args, true, false);
                return FormatExpressionResult(result);
            }
            catch
            {
                // Expressions koennen werfen (Usage-Fehler, fehlende World-Daten).
                // Eine Ausgabe-Interpolation darf das Script nicht abbrechen.
                return null;
            }
        }

        private static string FormatExpressionResult(IComparable result)
        {
            if (result == null)
                return null;

            if (result is bool b)
                return b ? "true" : "false";

            // Skills & Co.: UO-Konvention "eine Nachkommastelle", kulturfest
            // (Punkt statt Komma, egal welche Windows-Sprache laeuft).
            if (result is double d)
                return d.ToString("0.0#", System.Globalization.CultureInfo.InvariantCulture);

            if (result is IFormattable f)
                return f.ToString(null, System.Globalization.CultureInfo.InvariantCulture);

            return result.ToString();
        }

        /// <summary>
        /// Layer-Name -> Layer, inklusive der Outlands-Schreibweisen, die es
        /// im Razor-Layer-Enum nicht gibt: onehanded(secondary), twohanded,
        /// quiver (liegt auf Cloak), outerbody. Wirft bei unbekanntem Namen.
        /// </summary>
        public static Layer ParseLayer(string name)
        {
            switch (name?.Trim().ToLowerInvariant())
            {
                case "onehanded":
                    return Layer.RightHand;
                case "onehandedsecondary":
                case "twohanded":
                    return Layer.LeftHand;
                case "quiver":
                    return Layer.Cloak;
                case "outerbody":
                    return Layer.OuterTorso;
            }

            if (System.Enum.TryParse(name, true, out Layer layer))
                return layer;

            throw new RunTimeError($"Unknown layer '{name}'");
        }
    }
}
