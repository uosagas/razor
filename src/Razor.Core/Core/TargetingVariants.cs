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

// Portiert aus Razor CE (Razor/Core/TargetingClosest.cs, TargetingRandom.cs,
// TargetingNextPrevious.cs) — Phase 3c2. Alle Closest/Random/Next/Prev-
// Hotkeys nach Notoriety (Murderer/Non-Friendly/Friendly/Innocent/Grey/
// Criminal/Enemy) je als Alle/Humanoid/Monster-Variante.
//
// ABWEICHUNGEN vom Original (dokumentiert):
//  * FriendsManager nicht portiert: die Friend-Listen-Hotkeys (Target Closest/
//    Random Friend, Next/Prev Target Friend) sind BEWUSST NICHT registriert;
//    IsFriend-Filter entfaellt (niemand ist "Friend").
//  * TargetFilterManager (Target-Filter-Liste) nicht portiert — kein
//    IsFilteredTarget-Check.
//  * FeatureBit ClosestTargets/RandomTargets (Server-Negotiation) entfaellt.
//  * Humanoid-Erkennung ueber Mobile.IsHuman (CE prueft in den Closest-
//    Varianten die Bodies 0x190/0x191/0x25D/0x25E direkt, in Next/Prev
//    IsHuman — der Port nutzt durchgehend IsHuman).
//  * Overhead-Meldungen entfallen; ScriptManager.TargetFound wird (wie CE)
//    in den Kernen gesetzt — die Script-Priority-Listen (enemy!criminal)
//    brechen darueber ab.

using System;
using System.Collections.Generic;
using System.Linq;

namespace Assistant
{
    public static partial class Targeting
    {
        public enum TargetType
        {
            Invalid, // invalid/across server line
            Innocent, // blue
            GuildAlly, // green
            Attackable, // attackable but not criminal (gray)
            Criminal, // gray
            Enemy, // orange
            Murderer, // red

            // Razor specific
            NonFriendly, // Attackable, Criminal, Enemy, Murderer
            Friendly, // Innocent, Guild/Ally
            Red, // Murderer
            Blue, // Innocent
            Gray, // Attackable, Criminal
            Grey, // Attackable, Criminal
            Green, // GuildAlly
            Guild // GuildAlly
        }

        private const int KindAny = 0;
        private const int KindHumanoid = 1;
        private const int KindMonster = 2;

        private static readonly int[] NotoAny = new int[0];

        private static readonly int[] NotoNonFriendly =
        {
            (int) TargetType.Attackable, (int) TargetType.Criminal, (int) TargetType.Enemy, (int) TargetType.Murderer
        };

        private static readonly int[] NotoFriendly =
        {
            (int) TargetType.Invalid, (int) TargetType.Innocent, (int) TargetType.GuildAlly
        };

        private static readonly int[] NotoMurderer = {(int) TargetType.Murderer};
        private static readonly int[] NotoInnocent = {(int) TargetType.Innocent};
        private static readonly int[] NotoGrey = {(int) TargetType.Attackable, (int) TargetType.Criminal};
        private static readonly int[] NotoCriminal = {(int) TargetType.Criminal};
        private static readonly int[] NotoEnemy = {(int) TargetType.Enemy};

        /// <summary>Index fuer Next/Prev-Rotation (Razor CE: _nextPrevTargetIndex).</summary>
        private static int _nextPrevTargetIndex;

        // ---- Registrierungen (Reihenfolge wie Razor CE) --------------------------

        private static void InitClosestTargets()
        {
            HotKey.Add(HKCategory.Targets, LocString.TargClosest, new HotKeyCallback(TargetClosest));

            // Razor CE: TargClosestFriend — nicht registriert (kein FriendsManager).

            HotKey.Add(HKCategory.Targets, HKSubCat.SubTargetMurderer, LocString.TargCloseRed,
                new HotKeyCallback(TargetCloseRed));
            HotKey.Add(HKCategory.Targets, HKSubCat.SubTargetMurderer, LocString.TargCloseRedHumanoid,
                new HotKeyCallback(TargetCloseRedHumanoid));
            HotKey.Add(HKCategory.Targets, HKSubCat.SubTargetMurderer, LocString.TargCloseRedMonster,
                new HotKeyCallback(TargetCloseRedMonster));

            HotKey.Add(HKCategory.Targets, HKSubCat.SubTargetNonFriendly, LocString.TargCloseNFriend,
                new HotKeyCallback(TargetCloseNonFriendly));
            HotKey.Add(HKCategory.Targets, HKSubCat.SubTargetNonFriendly, LocString.TargClosestNFriendlyHuman,
                new HotKeyCallback(TargetCloseNonFriendlyHumanoid));
            HotKey.Add(HKCategory.Targets, HKSubCat.SubTargetNonFriendly, LocString.TargClosestNFriendlyMonster,
                new HotKeyCallback(TargetCloseNonFriendlyMonster));

            HotKey.Add(HKCategory.Targets, HKSubCat.SubTargetFriendly, LocString.TargCloseFriend,
                new HotKeyCallback(TargetCloseFriendly));
            HotKey.Add(HKCategory.Targets, HKSubCat.SubTargetFriendly, LocString.TargClosestFriendlyHuman,
                new HotKeyCallback(TargetCloseFriendlyHumanoid));
            HotKey.Add(HKCategory.Targets, HKSubCat.SubTargetFriendly, LocString.TargClosestFriendlyMonster,
                new HotKeyCallback(TargetCloseFriendlyMonster));

            HotKey.Add(HKCategory.Targets, HKSubCat.SubTargetInnocent, LocString.TargCloseBlue,
                new HotKeyCallback(TargetCloseInnocent));
            HotKey.Add(HKCategory.Targets, HKSubCat.SubTargetInnocent, LocString.TargCloseInnocentHuman,
                new HotKeyCallback(TargetCloseInnocentHumanoid));
            HotKey.Add(HKCategory.Targets, HKSubCat.SubTargetInnocent, LocString.TargClosestInnocentMonster,
                new HotKeyCallback(TargetCloseInnocentMonster));

            HotKey.Add(HKCategory.Targets, HKSubCat.SubTargetGrey, LocString.TargCloseGrey,
                new HotKeyCallback(TargetCloseGrey));
            HotKey.Add(HKCategory.Targets, HKSubCat.SubTargetGrey, LocString.TargCloseGreyHuman,
                new HotKeyCallback(TargetCloseGreyHumanoid));
            HotKey.Add(HKCategory.Targets, HKSubCat.SubTargetGrey, LocString.TargCloseGreyMonster,
                new HotKeyCallback(TargetCloseGreyMonster));

            HotKey.Add(HKCategory.Targets, HKSubCat.SubTargetCriminal, LocString.TargCloseCriminal,
                new HotKeyCallback(TargetCloseCriminal));
            HotKey.Add(HKCategory.Targets, HKSubCat.SubTargetCriminal, LocString.TargCloseCriminalHuman,
                new HotKeyCallback(TargetCloseCriminalHumanoid));
            HotKey.Add(HKCategory.Targets, HKSubCat.SubTargetCriminal, LocString.TargClosestCriminalMonster,
                new HotKeyCallback(TargetCloseCriminalMonster));

            HotKey.Add(HKCategory.Targets, HKSubCat.SubTargetEnemy, LocString.TargCloseEnemy,
                new HotKeyCallback(TargetCloseEnemy));
            HotKey.Add(HKCategory.Targets, HKSubCat.SubTargetEnemy, LocString.TargCloseEnemyHuman,
                new HotKeyCallback(TargetCloseEnemyHumanoid));
            HotKey.Add(HKCategory.Targets, HKSubCat.SubTargetEnemy, LocString.TargCloseEnemyMonster,
                new HotKeyCallback(TargetCloseEnemyMonster));
        }

        private static void InitRandomTarget()
        {
            HotKey.Add(HKCategory.Targets, LocString.TargetRandom, new HotKeyCallback(TargetRandAnyone));

            // Razor CE: TargRandomFriend — nicht registriert (kein FriendsManager).

            HotKey.Add(HKCategory.Targets, HKSubCat.SubTargetMurderer, LocString.TargRandRed,
                new HotKeyCallback(TargetRandRed));
            HotKey.Add(HKCategory.Targets, HKSubCat.SubTargetMurderer, LocString.TargRandomRedHuman,
                new HotKeyCallback(TargetRandRedHumanoid));
            HotKey.Add(HKCategory.Targets, HKSubCat.SubTargetMurderer, LocString.TargRandomRedMonster,
                new HotKeyCallback(TargetRandRedMonster));

            HotKey.Add(HKCategory.Targets, HKSubCat.SubTargetNonFriendly, LocString.TargRandNFriend,
                new HotKeyCallback(TargetRandNonFriendly));
            HotKey.Add(HKCategory.Targets, HKSubCat.SubTargetNonFriendly, LocString.TargRandomNFriendlyHuman,
                new HotKeyCallback(TargetRandNonFriendlyHumanoid));
            HotKey.Add(HKCategory.Targets, HKSubCat.SubTargetNonFriendly, LocString.TargRandomNFriendlyMonster,
                new HotKeyCallback(TargetRandNonFriendlyMonster));

            HotKey.Add(HKCategory.Targets, HKSubCat.SubTargetFriendly, LocString.TargRandFriend,
                new HotKeyCallback(TargetRandFriendly));
            HotKey.Add(HKCategory.Targets, HKSubCat.SubTargetFriendly, LocString.TargRandomFriendlyHuman,
                new HotKeyCallback(TargetRandFriendlyHumanoid));
            HotKey.Add(HKCategory.Targets, HKSubCat.SubTargetFriendly, LocString.TargRandomFriendlyMonster,
                new HotKeyCallback(TargetRandFriendlyMonster));

            HotKey.Add(HKCategory.Targets, HKSubCat.SubTargetInnocent, LocString.TargRandBlue,
                new HotKeyCallback(TargetRandInnocent));
            HotKey.Add(HKCategory.Targets, HKSubCat.SubTargetInnocent, LocString.TargRandInnocentHuman,
                new HotKeyCallback(TargetRandInnocentHumanoid));
            HotKey.Add(HKCategory.Targets, HKSubCat.SubTargetInnocent, LocString.TargRandomInnocentMonster,
                new HotKeyCallback(TargetRandInnocentMonster));

            HotKey.Add(HKCategory.Targets, HKSubCat.SubTargetGrey, LocString.TargRandGrey,
                new HotKeyCallback(TargetRandGrey));
            HotKey.Add(HKCategory.Targets, HKSubCat.SubTargetGrey, LocString.TargRandGreyHuman,
                new HotKeyCallback(TargetRandGreyHumanoid));
            HotKey.Add(HKCategory.Targets, HKSubCat.SubTargetGrey, LocString.TargRandGreyMonster,
                new HotKeyCallback(TargetRandGreyMonster));

            HotKey.Add(HKCategory.Targets, HKSubCat.SubTargetEnemy, LocString.TargRandEnemy,
                new HotKeyCallback(TargetRandEnemy));
            HotKey.Add(HKCategory.Targets, HKSubCat.SubTargetEnemy, LocString.TargRandEnemyHuman,
                new HotKeyCallback(TargetRandEnemyHumanoid));
            HotKey.Add(HKCategory.Targets, HKSubCat.SubTargetEnemy, LocString.TargRandEnemyMonster,
                new HotKeyCallback(TargetRandEnemyMonster));

            HotKey.Add(HKCategory.Targets, HKSubCat.SubTargetCriminal, LocString.TargRandCriminal,
                new HotKeyCallback(TargetRandCriminal));
            HotKey.Add(HKCategory.Targets, HKSubCat.SubTargetCriminal, LocString.TargRandCriminalHuman,
                new HotKeyCallback(TargetRandCriminalHumanoid));
            HotKey.Add(HKCategory.Targets, HKSubCat.SubTargetCriminal, LocString.TargRandomCriminalMonster,
                new HotKeyCallback(TargetRandCriminalMonster));
        }

        private static void InitNextPrevTargets()
        {
            // Next targets
            HotKey.Add(HKCategory.Targets, LocString.NextTarget, new HotKeyCallback(NextTarget));
            // Razor CE: NextTargetFriend — nicht registriert (kein FriendsManager).
            HotKey.Add(HKCategory.Targets, LocString.NextTargetNonFriendly, new HotKeyCallback(NextTargetNonFriend));

            // Next humanoids
            HotKey.Add(HKCategory.Targets, LocString.NextTargetHumanoid, new HotKeyCallback(NextTargetHumanoid));

            HotKey.Add(HKCategory.Targets, HKSubCat.SubTargetEnemy, LocString.NextTargetEnemyHumanoid,
                new HotKeyCallback(NextTargetEnemyHumanoid));
            HotKey.Add(HKCategory.Targets, HKSubCat.SubTargetCriminal, LocString.NextTargetCriminalHumanoid,
                new HotKeyCallback(NextTargetCriminalHumanoid));
            HotKey.Add(HKCategory.Targets, HKSubCat.SubTargetMurderer, LocString.NextTargetMurdererHumanoid,
                new HotKeyCallback(NextTargetMurdererHumanoid));
            HotKey.Add(HKCategory.Targets, HKSubCat.SubTargetInnocent, LocString.NextTargetInnocentHumanoid,
                new HotKeyCallback(NextTargetInnocentHumanoid));
            HotKey.Add(HKCategory.Targets, HKSubCat.SubTargetFriendly, LocString.NextTargetFriendlyHumanoid,
                new HotKeyCallback(NextTargetFriendlyHumanoid));
            HotKey.Add(HKCategory.Targets, HKSubCat.SubTargetFriendly, LocString.NextTargetFriendly,
                new HotKeyCallback(NextTargetFriendly));
            HotKey.Add(HKCategory.Targets, HKSubCat.SubTargetGrey, LocString.NextTargetGreyHumanoid,
                new HotKeyCallback(NextTargetGreyHumanoid));
            HotKey.Add(HKCategory.Targets, HKSubCat.SubTargetNonFriendly, LocString.NextTargetNonFriendlyHumanoid,
                new HotKeyCallback(NextTargetNonFriendlyHumanoid));

            // Next monsters
            HotKey.Add(HKCategory.Targets, LocString.NextTargetMonster, new HotKeyCallback(NextTargetMonster));

            HotKey.Add(HKCategory.Targets, HKSubCat.SubTargetEnemy, LocString.NextTargetEnemyMonster,
                new HotKeyCallback(NextTargetEnemyMonster));
            HotKey.Add(HKCategory.Targets, HKSubCat.SubTargetCriminal, LocString.NextTargetCriminalMonster,
                new HotKeyCallback(NextTargetCriminalMonster));
            HotKey.Add(HKCategory.Targets, HKSubCat.SubTargetMurderer, LocString.NextTargetMurdererMonster,
                new HotKeyCallback(NextTargetMurdererMonster));
            HotKey.Add(HKCategory.Targets, HKSubCat.SubTargetInnocent, LocString.NextTargetInnocentMonster,
                new HotKeyCallback(NextTargetInnocentMonster));
            HotKey.Add(HKCategory.Targets, HKSubCat.SubTargetFriendly, LocString.NextTargetFriendlyMonster,
                new HotKeyCallback(NextTargetFriendlyMonster));
            HotKey.Add(HKCategory.Targets, HKSubCat.SubTargetGrey, LocString.NextTargetGreyMonster,
                new HotKeyCallback(NextTargetGreyMonster));
            HotKey.Add(HKCategory.Targets, HKSubCat.SubTargetNonFriendly, LocString.NextTargetNonFriendlyMonster,
                new HotKeyCallback(NextTargetNonFriendlyMonster));

            // Previous targets
            HotKey.Add(HKCategory.Targets, LocString.PrevTarget, new HotKeyCallback(PrevTarget));
            HotKey.Add(HKCategory.Targets, LocString.PrevTargetNonFriendly, new HotKeyCallback(PrevTargetNonFriend));

            HotKey.Add(HKCategory.Targets, LocString.PrevTargetHumanoid, new HotKeyCallback(PrevTargetHumanoid));
            HotKey.Add(HKCategory.Targets, LocString.PrevTargetMonster, new HotKeyCallback(PrevTargetMonster));
            // Razor CE: PrevTargetFriend — nicht registriert (kein FriendsManager).

            // Previous humanoids
            HotKey.Add(HKCategory.Targets, HKSubCat.SubTargetEnemy, LocString.PrevTargetEnemyHumanoid,
                new HotKeyCallback(PrevTargetEnemyHumanoid));
            HotKey.Add(HKCategory.Targets, HKSubCat.SubTargetFriendly, LocString.PrevTargetFriendlyHumanoid,
                new HotKeyCallback(PrevTargetFriendlyHumanoid));
            HotKey.Add(HKCategory.Targets, HKSubCat.SubTargetFriendly, LocString.PrevTargetFriendly,
                new HotKeyCallback(PrevTargetFriendly));
            HotKey.Add(HKCategory.Targets, HKSubCat.SubTargetGrey, LocString.PrevTargetGreyHumanoid,
                new HotKeyCallback(PrevTargetGreyHumanoid));
            HotKey.Add(HKCategory.Targets, HKSubCat.SubTargetNonFriendly, LocString.PrevTargetNonFriendlyHumanoid,
                new HotKeyCallback(PrevTargetNonFriendlyHumanoid));
            HotKey.Add(HKCategory.Targets, HKSubCat.SubTargetCriminal, LocString.PrevTargetCriminalHumanoid,
                new HotKeyCallback(PrevTargetCriminalHumanoid));
            HotKey.Add(HKCategory.Targets, HKSubCat.SubTargetMurderer, LocString.PrevTargetMurdererHumanoid,
                new HotKeyCallback(PrevTargetMurdererHumanoid));
            HotKey.Add(HKCategory.Targets, HKSubCat.SubTargetInnocent, LocString.PrevTargetInnocentHumanoid,
                new HotKeyCallback(PrevTargetInnocentHumanoid));

            // Previous monsters
            HotKey.Add(HKCategory.Targets, HKSubCat.SubTargetEnemy, LocString.PrevTargetEnemyMonster,
                new HotKeyCallback(PrevTargetEnemyMonster));
            HotKey.Add(HKCategory.Targets, HKSubCat.SubTargetFriendly, LocString.PrevTargetFriendlyMonster,
                new HotKeyCallback(PrevTargetFriendlyMonster));
            HotKey.Add(HKCategory.Targets, HKSubCat.SubTargetGrey, LocString.PrevTargetGreyMonster,
                new HotKeyCallback(PrevTargetGreyMonster));
            HotKey.Add(HKCategory.Targets, HKSubCat.SubTargetNonFriendly, LocString.PrevTargetNonFriendlyMonster,
                new HotKeyCallback(PrevTargetNonFriendlyMonster));
            HotKey.Add(HKCategory.Targets, HKSubCat.SubTargetCriminal, LocString.PrevTargetCriminalMonster,
                new HotKeyCallback(PrevTargetCriminalMonster));
            HotKey.Add(HKCategory.Targets, HKSubCat.SubTargetMurderer, LocString.PrevTargetMurdererMonster,
                new HotKeyCallback(PrevTargetMurdererMonster));
            HotKey.Add(HKCategory.Targets, HKSubCat.SubTargetInnocent, LocString.PrevTargetInnocentMonster,
                new HotKeyCallback(PrevTargetInnocentMonster));
        }

        // ---- Filter-Kern ---------------------------------------------------------

        private static bool MatchesKind(Mobile m, int kind)
        {
            switch (kind)
            {
                case KindHumanoid:
                    return m.IsHuman;
                case KindMonster:
                    return m.IsMonster;
                default:
                    return true;
            }
        }

        private static bool MatchesNoto(Mobile m, int[] noto)
        {
            if (noto.Length == 0)
                return true;

            for (int i = 0; i < noto.Length; i++)
            {
                if (noto[i] == m.Notoriety)
                    return true;
            }

            return false;
        }

        private static List<Mobile> FindTargets(int range, int kind, int[] noto, bool checkLtRange)
        {
            List<Mobile> list = new List<Mobile>();

            if (World.Player == null)
                return list;

            int ltRange = Config.GetInt("LTRange");

            foreach (Mobile m in World.MobilesInRange(range))
            {
                if (m.Serial == World.Player.Serial || m.Blessed || m.IsGhost)
                    continue;

                if (!MatchesKind(m, kind) || !MatchesNoto(m, noto))
                    continue;

                if (checkLtRange && !Utility.InRange(World.Player.Position, m.Position, ltRange))
                    continue;

                list.Add(m);
            }

            return list;
        }

        // ---- Closest (Razor CE: ClosestTarget/ClosestHumanoidTarget/ClosestMonsterTarget)

        private static void ClosestTargetKind(int kind, params int[] noto)
        {
            if (World.Player == null)
                return;

            List<Mobile> list = FindTargets(12, kind, noto, true);

            Mobile closest = null;
            double closestDist = double.MaxValue;

            foreach (Mobile m in list)
            {
                double dist = Utility.DistanceSqrt(m.Position, World.Player.Position);

                if (dist < closestDist || closest == null)
                {
                    closestDist = dist;
                    closest = m;
                }
            }

            if (closest != null)
            {
                SetLastTargetTo(closest);
                Scripts.ScriptManager.TargetFound = true; // Razor CE: TargetingClosest.cs
            }
            else
            {
                World.Player.SendMessage(MsgLevel.Warning, LocString.TargNoOne);
            }
        }

        public static void TargetClosest() => ClosestTargetKind(KindAny, NotoAny);
        public static void TargetCloseRed() => ClosestTargetKind(KindAny, NotoMurderer);
        public static void TargetCloseRedHumanoid() => ClosestTargetKind(KindHumanoid, NotoMurderer);
        public static void TargetCloseRedMonster() => ClosestTargetKind(KindMonster, NotoMurderer);
        public static void TargetCloseNonFriendly() => ClosestTargetKind(KindAny, NotoNonFriendly);
        public static void TargetCloseNonFriendlyHumanoid() => ClosestTargetKind(KindHumanoid, NotoNonFriendly);
        public static void TargetCloseNonFriendlyMonster() => ClosestTargetKind(KindMonster, NotoNonFriendly);
        public static void TargetCloseFriendly() => ClosestTargetKind(KindAny, NotoFriendly);
        public static void TargetCloseFriendlyHumanoid() => ClosestTargetKind(KindHumanoid, NotoFriendly);
        public static void TargetCloseFriendlyMonster() => ClosestTargetKind(KindMonster, NotoFriendly);
        public static void TargetCloseInnocent() => ClosestTargetKind(KindAny, NotoInnocent);
        public static void TargetCloseInnocentHumanoid() => ClosestTargetKind(KindHumanoid, NotoInnocent);
        public static void TargetCloseInnocentMonster() => ClosestTargetKind(KindMonster, NotoInnocent);
        public static void TargetCloseGrey() => ClosestTargetKind(KindAny, NotoGrey);
        public static void TargetCloseGreyHumanoid() => ClosestTargetKind(KindHumanoid, NotoGrey);
        public static void TargetCloseGreyMonster() => ClosestTargetKind(KindMonster, NotoGrey);
        public static void TargetCloseCriminal() => ClosestTargetKind(KindAny, NotoCriminal);
        public static void TargetCloseCriminalHumanoid() => ClosestTargetKind(KindHumanoid, NotoCriminal);
        public static void TargetCloseCriminalMonster() => ClosestTargetKind(KindMonster, NotoCriminal);
        public static void TargetCloseEnemy() => ClosestTargetKind(KindAny, NotoEnemy);
        public static void TargetCloseEnemyHumanoid() => ClosestTargetKind(KindHumanoid, NotoEnemy);
        public static void TargetCloseEnemyMonster() => ClosestTargetKind(KindMonster, NotoEnemy);

        // ---- Random (Razor CE: RandomTarget/RandomHumanoidTarget/RandomMonsterTarget)

        private static void RandomTargetKind(int kind, params int[] noto)
        {
            if (World.Player == null)
                return;

            List<Mobile> list = FindTargets(12, kind, noto, true);

            if (list.Count > 0)
            {
                SetLastTargetTo(list[Utility.Random(list.Count)]);
                Scripts.ScriptManager.TargetFound = true; // Razor CE: TargetingRandom.cs
            }
            else
            {
                World.Player.SendMessage(MsgLevel.Warning, LocString.TargNoOne);
            }
        }

        public static void TargetRandAnyone() => RandomTargetKind(KindAny, NotoAny);
        public static void TargetRandRed() => RandomTargetKind(KindAny, NotoMurderer);
        public static void TargetRandRedHumanoid() => RandomTargetKind(KindHumanoid, NotoMurderer);
        public static void TargetRandRedMonster() => RandomTargetKind(KindMonster, NotoMurderer);
        public static void TargetRandNonFriendly() => RandomTargetKind(KindAny, NotoNonFriendly);
        public static void TargetRandNonFriendlyHumanoid() => RandomTargetKind(KindHumanoid, NotoNonFriendly);
        public static void TargetRandNonFriendlyMonster() => RandomTargetKind(KindMonster, NotoNonFriendly);
        public static void TargetRandFriendly() => RandomTargetKind(KindAny, NotoFriendly);
        public static void TargetRandFriendlyHumanoid() => RandomTargetKind(KindHumanoid, NotoFriendly);
        public static void TargetRandFriendlyMonster() => RandomTargetKind(KindMonster, NotoFriendly);
        public static void TargetRandInnocent() => RandomTargetKind(KindAny, NotoInnocent);
        public static void TargetRandInnocentHumanoid() => RandomTargetKind(KindHumanoid, NotoInnocent);
        public static void TargetRandInnocentMonster() => RandomTargetKind(KindMonster, NotoInnocent);
        public static void TargetRandGrey() => RandomTargetKind(KindAny, NotoGrey);
        public static void TargetRandGreyHumanoid() => RandomTargetKind(KindHumanoid, NotoGrey);
        public static void TargetRandGreyMonster() => RandomTargetKind(KindMonster, NotoGrey);
        public static void TargetRandCriminal() => RandomTargetKind(KindAny, NotoCriminal);
        public static void TargetRandCriminalHumanoid() => RandomTargetKind(KindHumanoid, NotoCriminal);
        public static void TargetRandCriminalMonster() => RandomTargetKind(KindMonster, NotoCriminal);
        public static void TargetRandEnemy() => RandomTargetKind(KindAny, NotoEnemy);
        public static void TargetRandEnemyHumanoid() => RandomTargetKind(KindHumanoid, NotoEnemy);
        public static void TargetRandEnemyMonster() => RandomTargetKind(KindMonster, NotoEnemy);

        // ---- Next/Prev (Razor CE: NextPrevTarget) ---------------------------------

        /// <summary>
        /// Razor CE: NextPrevTarget — rotiert durch die (vorgefilterte) Liste.
        /// Abweichungen: kein SmartTarget-Zweig, keine Overhead-Meldung,
        /// ChangeCombatant wird immer injiziert (Friend-Variante entfaellt).
        /// </summary>
        private static void NextPrevTarget(List<Mobile> targets, bool nextTarget,
            bool isFriendly = false, bool isNonFriendly = false)
        {
            if (World.Player == null)
                return;

            if (targets.Count <= 0)
            {
                World.Player.SendMessage(MsgLevel.Warning, LocString.TargNoOne);
                return;
            }

            if (Config.GetBool("NextPrevAlphabetical"))
            {
                targets = targets.OrderBy(m => m.Name).ToList();
            }

            Mobile mobile = null, old = World.FindMobile(_lastTarget?.Serial ?? Serial.Zero);
            TargetInfo target = new TargetInfo();

            // Wie Razor CE: max. 3 Versuche, ein neues Ziel zu finden.
            for (int i = 0; i < 3; i++)
            {
                if (nextTarget)
                {
                    _nextPrevTargetIndex++;

                    if (_nextPrevTargetIndex >= targets.Count)
                        _nextPrevTargetIndex = 0;
                }
                else
                {
                    _nextPrevTargetIndex--;

                    if (_nextPrevTargetIndex < 0)
                        _nextPrevTargetIndex = targets.Count - 1;
                }

                mobile = targets[_nextPrevTargetIndex];

                if (mobile != null && mobile != World.Player && mobile != old)
                    break;

                mobile = null;
            }

            if (mobile == null)
                mobile = old;

            if (mobile == null)
            {
                World.Player.SendMessage(MsgLevel.Warning, LocString.TargNoOne);
                return;
            }

            _lastGroundTarget = _lastTarget = target;

            // Razor CE: SmartLastTarget-Zuweisung — die Next/Prev-Filter
            // entscheiden, ob das Ziel harm, bene oder beides wird.
            // CE prueft zusaetzlich isFriend (Friend-Hotkeys); der Port hat
            // keinen FriendsManager, isFriend ist konstant false — damit
            // vereinfacht sich CEs "OnlyNextPrevBeneficial && !isFriend" zu
            // einem reinen Config-Check.
            if (IsSmartTargetingEnabled())
            {
                if (Config.GetBool("FriendlyBeneficialOnly") && isFriendly)
                    _lastBeneTarget = target;
                else if (Config.GetBool("OnlyNextPrevBeneficial") ||
                         Config.GetBool("NonFriendlyHarmfulOnly") && isNonFriendly)
                    _lastHarmTarget = target;
                else
                    _lastBeneTarget = _lastHarmTarget = target;
            }
            else
            {
                _lastBeneTarget = _lastHarmTarget = target;
            }

            if (_hasTarget)
                target.Flags = _curFlags;
            else
                target.Type = 0;

            target.Gfx = mobile.Body;
            target.Serial = mobile.Serial;
            target.X = mobile.Position.X;
            target.Y = mobile.Position.Y;
            target.Z = mobile.Position.Z;

            ClientProxy.SendToClient(new ChangeCombatant(mobile));
            _lastCombatant = mobile.Serial;

            Scripts.ScriptManager.TargetFound = true; // Razor CE: TargetingNextPrevious.cs

            World.Player.SendMessage(MsgLevel.Force, LocString.NewTargSet);

            OverheadTargetMessage(target);
        }

        private static void NextPrevTargetKind(bool next, int kind, params int[] noto)
        {
            if (World.Player == null)
                return;

            // Razor CE setzt isFriendly bei den Friendly-Hotkeys und
            // isNonFriendly beim NonFriendly-Hotkey (Referenz auf die
            // jeweilige Noto-Menge — gleiche Semantik wie die CE-Call-Sites).
            NextPrevTarget(FindTargets(World.Player.VisRange, kind, noto, false), next,
                isFriendly: ReferenceEquals(noto, NotoFriendly),
                isNonFriendly: ReferenceEquals(noto, NotoNonFriendly));
        }

        public static void NextTarget() => NextPrevTargetKind(true, KindAny, NotoAny);
        public static void NextTargetNonFriend() => NextPrevTargetKind(true, KindAny, NotoNonFriendly);
        public static void NextTargetHumanoid() => NextPrevTargetKind(true, KindHumanoid, NotoAny);
        public static void NextTargetEnemyHumanoid() => NextPrevTargetKind(true, KindHumanoid, NotoEnemy);
        public static void NextTargetCriminalHumanoid() => NextPrevTargetKind(true, KindHumanoid, NotoCriminal);
        public static void NextTargetMurdererHumanoid() => NextPrevTargetKind(true, KindHumanoid, NotoMurderer);
        public static void NextTargetInnocentHumanoid() => NextPrevTargetKind(true, KindHumanoid, NotoInnocent);
        public static void NextTargetFriendlyHumanoid() => NextPrevTargetKind(true, KindHumanoid, NotoFriendly);
        public static void NextTargetFriendly() => NextPrevTargetKind(true, KindAny, NotoFriendly);
        public static void NextTargetGreyHumanoid() => NextPrevTargetKind(true, KindHumanoid, NotoGrey);
        public static void NextTargetNonFriendlyHumanoid() => NextPrevTargetKind(true, KindHumanoid, NotoNonFriendly);
        public static void NextTargetMonster() => NextPrevTargetKind(true, KindMonster, NotoAny);
        public static void NextTargetEnemyMonster() => NextPrevTargetKind(true, KindMonster, NotoEnemy);
        public static void NextTargetCriminalMonster() => NextPrevTargetKind(true, KindMonster, NotoCriminal);
        public static void NextTargetMurdererMonster() => NextPrevTargetKind(true, KindMonster, NotoMurderer);
        public static void NextTargetInnocentMonster() => NextPrevTargetKind(true, KindMonster, NotoInnocent);
        public static void NextTargetFriendlyMonster() => NextPrevTargetKind(true, KindMonster, NotoFriendly);
        public static void NextTargetGreyMonster() => NextPrevTargetKind(true, KindMonster, NotoGrey);
        public static void NextTargetNonFriendlyMonster() => NextPrevTargetKind(true, KindMonster, NotoNonFriendly);

        public static void PrevTarget() => NextPrevTargetKind(false, KindAny, NotoAny);
        public static void PrevTargetNonFriend() => NextPrevTargetKind(false, KindAny, NotoNonFriendly);
        public static void PrevTargetHumanoid() => NextPrevTargetKind(false, KindHumanoid, NotoAny);
        public static void PrevTargetMonster() => NextPrevTargetKind(false, KindMonster, NotoAny);
        public static void PrevTargetEnemyHumanoid() => NextPrevTargetKind(false, KindHumanoid, NotoEnemy);
        public static void PrevTargetFriendlyHumanoid() => NextPrevTargetKind(false, KindHumanoid, NotoFriendly);
        public static void PrevTargetFriendly() => NextPrevTargetKind(false, KindAny, NotoFriendly);
        public static void PrevTargetGreyHumanoid() => NextPrevTargetKind(false, KindHumanoid, NotoGrey);
        public static void PrevTargetNonFriendlyHumanoid() => NextPrevTargetKind(false, KindHumanoid, NotoNonFriendly);
        public static void PrevTargetCriminalHumanoid() => NextPrevTargetKind(false, KindHumanoid, NotoCriminal);
        public static void PrevTargetMurdererHumanoid() => NextPrevTargetKind(false, KindHumanoid, NotoMurderer);
        public static void PrevTargetInnocentHumanoid() => NextPrevTargetKind(false, KindHumanoid, NotoInnocent);
        public static void PrevTargetEnemyMonster() => NextPrevTargetKind(false, KindMonster, NotoEnemy);
        public static void PrevTargetCriminalMonster() => NextPrevTargetKind(false, KindMonster, NotoCriminal);
        public static void PrevTargetMurdererMonster() => NextPrevTargetKind(false, KindMonster, NotoMurderer);
        public static void PrevTargetInnocentMonster() => NextPrevTargetKind(false, KindMonster, NotoInnocent);
        public static void PrevTargetFriendlyMonster() => NextPrevTargetKind(false, KindMonster, NotoFriendly);
        public static void PrevTargetGreyMonster() => NextPrevTargetKind(false, KindMonster, NotoGrey);
        public static void PrevTargetNonFriendlyMonster() => NextPrevTargetKind(false, KindMonster, NotoNonFriendly);

        // ---- Script-API (Razor CE: notoriety-array-basierte Varianten) -----------
        // Von Scripts/Helpers/CommandHelper.FindTargetNotoriety genutzt
        // ("targetclosest 'enemy,criminal'" u. ae.) — 1:1 CE-Signaturen.

        public static void ClosestTarget(params int[] noto) => ClosestTargetKind(KindAny, noto);
        public static void ClosestHumanoidTarget(params int[] noto) => ClosestTargetKind(KindHumanoid, noto);
        public static void ClosestMonsterTarget(params int[] noto) => ClosestTargetKind(KindMonster, noto);

        public static void RandomTarget(params int[] noto) => RandomTargetKind(KindAny, noto);
        public static void RandomHumanoidTarget(params int[] noto) => RandomTargetKind(KindHumanoid, noto);
        public static void RandomMonsterTarget(params int[] noto) => RandomTargetKind(KindMonster, noto);

        public static void NextPrevTargetNotoriety(bool next, params int[] noto) =>
            NextPrevTargetKind(next, KindAny, noto);
        public static void NextPrevTargetNotorietyHumanoid(bool next, params int[] noto) =>
            NextPrevTargetKind(next, KindHumanoid, noto);
        public static void NextPrevTargetNotorietyMonster(bool next, params int[] noto) =>
            NextPrevTargetKind(next, KindMonster, noto);

        // Razor CE: Friend-Listen-Varianten (FriendsManager). Der Port hat keinen
        // FriendsManager (dokumentierte Abweichung) — die Script-Grammatik bleibt
        // 1:1 erhalten, aber ohne Friend-Liste findet sich niemand -> Warnung.
        // TODO(scripting-stub): FriendsManager portieren, dann hier ersetzen.

        public static void TargetClosestFriend() => WarnNoFriends();
        public static void TargetRandFriend() => WarnNoFriends();
        public static void NextTargetFriend() => WarnNoFriends();
        public static void PrevTargetFriend() => WarnNoFriends();

        private static void WarnNoFriends()
        {
            World.Player?.SendMessage(MsgLevel.Warning, "Friends list is not supported yet.");
        }
    }
}
