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

// Portiert aus Razor CE (Razor/HotKeys/SkillHotKeys.cs) — Phase 3c2.
// ABWEICHUNGEN vom Original (dokumentiert):
//  * Razor CE liest die nutzbaren Skills (Action-Flag) aus skills.idx/mul
//    (Ultima.Skills.GetUsableSkillIndexes); der Port hat keinen Mul-Zugriff
//    und bettet die 22 Standard-"Use"-Skills mit ihren Indizes ein.
//  * Registriert wird wie CE mit der Skill-Namens-Cliloc (1044060 + Index) —
//    Profil-kompatibel zu CE ("L:1044061" = Anatomy usw.). Der Anzeigename
//    kommt aus der eingebetteten Tabelle (Language.RegisterRuntime).
//  * ENTFERNT: SkillTimer.Start (kein Skill-Timer-Fenster im Port) und
//    StealthSteps.Hide (StealthSteps nicht portiert).

namespace Assistant.HotKeys
{
    public class SkillHotKeys
    {
        private static HotKeyCallbackState _callback;

        private sealed class UsableSkill
        {
            public readonly int Index;
            public readonly string Name;

            public UsableSkill(int index, string name)
            {
                Index = index;
                Name = name;
            }
        }

        // Standard-Skilltabelle (Indizes wie skills.mul; Teilmenge mit
        // "Use"-Button). Entspricht den per 0x12/0x24 nutzbaren Skills.
        private static readonly UsableSkill[] m_UsableSkills =
        {
            new UsableSkill(1, "Anatomy"),
            new UsableSkill(2, "Animal Lore"),
            new UsableSkill(3, "Item Identification"),
            new UsableSkill(4, "Arms Lore"),
            new UsableSkill(6, "Begging"),
            new UsableSkill(9, "Peacemaking"),
            new UsableSkill(14, "Detecting Hidden"),
            new UsableSkill(15, "Discordance"),
            new UsableSkill(16, "Evaluating Intelligence"),
            new UsableSkill(19, "Forensic Evaluation"),
            new UsableSkill(21, "Hiding"),
            new UsableSkill(22, "Provocation"),
            new UsableSkill(23, "Inscription"),
            new UsableSkill(30, "Poisoning"),
            new UsableSkill(32, "Spirit Speak"),
            new UsableSkill(33, "Stealing"),
            new UsableSkill(35, "Animal Taming"),
            new UsableSkill(36, "Taste Identification"),
            new UsableSkill(38, "Tracking"),
            new UsableSkill(46, "Meditation"),
            new UsableSkill(47, "Stealth"),
            new UsableSkill(48, "Remove Trap")
        };

        /// <summary>Anzahl der registrierten Skill-Hotkeys (Testverankerung).</summary>
        public static int Count
        {
            get { return m_UsableSkills.Length; }
        }

        public static void Initialize()
        {
            _callback = new HotKeyCallbackState(OnHotKey);

            // 1044060 = Alchemy in UO cliloc (wie Razor CE)
            foreach (UsableSkill skill in m_UsableSkills)
            {
                Language.RegisterRuntime(1044060 + skill.Index, skill.Name);
                HotKey.Add(HKCategory.Skills, HKSubCat.None, 1044060 + skill.Index, _callback, skill.Index);
            }
        }

        private static void OnHotKey(ref object state)
        {
            int sk = (int) state;
            ClientProxy.SendToServer(new UseSkill(sk));

            if (World.Player != null)
            {
                World.Player.LastSkill = sk;
            }
        }
    }
}
