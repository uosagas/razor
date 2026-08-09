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

// Portiert aus Razor CE (Razor/UltimaSDK/Skills.cs), ABER daten-basiert:
// Razor CE liest Namen + IsAction-Flag aus skills.mul. Der UOSagas-Client
// verschluesselt die MULs (D5) — Razor liest sie NIE. Deshalb hier die
// kanonische Standard-Skilltabelle (Index 0..57, ML/SA-Aera) als statische
// Quelle. DisplayName kommt on-demand ueber den DataService (Skill-Cliloc
// 1044060 + Index), sonst faellt er auf den Namen zurueck.
//
// SkillsByName-Schluessel = Name ohne Leerzeichen (wie CE), plus die CE-Aliase
// (itemid, evalint, forensic, provo, spirit, tasteid).

using System;
using System.Collections.Generic;

namespace Ultima
{
    public sealed class SkillInfo
    {
        public int Index { get; set; }
        public bool IsAction { get; set; }

        public string Name { get; set; }
        public string DisplayName { get; set; }

        public int Extra { get; private set; }

        public SkillInfo(int nr, string name, string displayName, bool action, int extra)
        {
            Index = nr;
            Name = name;
            DisplayName = displayName;
            IsAction = action;
            Extra = extra;
        }
    }

    public sealed class Skills
    {
        // WICHTIG: lazy ueber die Properties initialisieren — die Script-
        // Kommandos (skill/useskill) greifen direkt auf SkillsByName zu;
        // ohne Lazy-Init waere das eine NullReferenceException.
        private static Dictionary<string, SkillInfo> _byName;
        private static Dictionary<int, SkillInfo> _byIndex;

        public static Dictionary<string, SkillInfo> SkillsByName
        {
            get
            {
                Initialize();
                return _byName;
            }
        }

        public static Dictionary<int, SkillInfo> SkillsByIndex
        {
            get
            {
                Initialize();
                return _byIndex;
            }
        }

        public static int StealthIndex { get; private set; }
        public static int MageryIndex { get; private set; }

        // Standard-Skilltabelle: { Name, IsAction }. IsAction = ueber den
        // Skill-"Use"-Button (0x12/0x24) ausloesbar — entspricht dem action-Byte
        // in skills.mul.
        private static readonly (string Name, bool Action)[] Table =
        {
            ("Alchemy", false),                     // 0
            ("Anatomy", true),                      // 1
            ("Animal Lore", true),                  // 2
            ("Item Identification", true),          // 3
            ("Arms Lore", true),                    // 4
            ("Parrying", false),                    // 5
            ("Begging", true),                      // 6
            ("Blacksmithy", false),                 // 7
            ("Bowcraft/Fletching", false),          // 8
            ("Peacemaking", true),                  // 9
            ("Camping", true),                      // 10
            ("Carpentry", false),                   // 11
            ("Cartography", true),                  // 12
            ("Cooking", false),                     // 13
            ("Detecting Hidden", true),             // 14
            ("Discordance", true),                  // 15
            ("Evaluating Intelligence", true),      // 16
            ("Healing", true),                      // 17
            ("Fishing", true),                      // 18
            ("Forensic Evaluation", true),          // 19
            ("Herding", true),                      // 20
            ("Hiding", true),                       // 21
            ("Provocation", true),                  // 22
            ("Inscription", false),                 // 23
            ("Lockpicking", true),                  // 24
            ("Magery", false),                      // 25
            ("Resisting Spells", false),            // 26
            ("Tactics", false),                     // 27
            ("Snooping", false),                    // 28
            ("Musicianship", false),                // 29
            ("Poisoning", true),                    // 30
            ("Archery", false),                     // 31
            ("Spirit Speak", true),                 // 32
            ("Stealing", true),                     // 33
            ("Tailoring", false),                   // 34
            ("Animal Taming", true),                // 35
            ("Taste Identification", true),         // 36
            ("Tinkering", false),                   // 37
            ("Tracking", true),                     // 38
            ("Veterinary", true),                   // 39
            ("Swordsmanship", false),               // 40
            ("Mace Fighting", false),               // 41
            ("Fencing", false),                     // 42
            ("Wrestling", false),                   // 43
            ("Lumberjacking", false),               // 44
            ("Mining", false),                      // 45
            ("Meditation", true),                   // 46
            ("Stealth", true),                      // 47
            ("Remove Trap", true),                  // 48
            ("Necromancy", false),                  // 49
            ("Focus", false),                       // 50
            ("Chivalry", false),                    // 51
            ("Bushido", false),                     // 52
            ("Ninjitsu", false),                    // 53
            ("Spellweaving", false),                // 54
            ("Mysticism", false),                   // 55
            ("Imbuing", false),                     // 56
            ("Throwing", false),                    // 57
        };

        public static void Initialize()
        {
            if (_byIndex != null)
                return;

            Load();
        }

        private static void Load()
        {
            _byIndex = new Dictionary<int, SkillInfo>();
            _byName = new Dictionary<string, SkillInfo>(StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < Table.Length; i++)
            {
                string name = Table[i].Name;
                string displayName = ClientProxyDisplayName(i, name);
                SkillInfo info = new SkillInfo(i, name, displayName, Table[i].Action, 0);

                SkillsByIndex.Add(i, info);
                SkillsByName[name.Replace(" ", string.Empty)] = info;

                switch (name)
                {
                    case "Magery":
                        MageryIndex = i;
                        break;
                    case "Stealth":
                        StealthIndex = i;
                        break;
                    case "Item Identification":
                        SkillsByName["itemid"] = info;
                        break;
                    case "Evaluating Intelligence":
                        SkillsByName["evalint"] = info;
                        break;
                    case "Forensic Evaluation":
                        SkillsByName["forensiceval"] = info;
                        SkillsByName["forensic"] = info;
                        break;
                    case "Provocation":
                        SkillsByName["provo"] = info;
                        break;
                    case "Discordance":
                        SkillsByName["discord"] = info;
                        break;
                    case "Animal Taming":
                        SkillsByName["taming"] = info;
                        break;
                    case "Spirit Speak":
                        SkillsByName["spirit"] = info;
                        break;
                    case "Taste Identification":
                        SkillsByName["tasteid"] = info;
                        break;
                }
            }
        }

        // Skill-Cliloc = 1044060 + Index (Standard-UO). Ueber den DataService,
        // sonst Name als Fallback.
        private static string ClientProxyDisplayName(int index, string fallback)
        {
            try
            {
                string s = Assistant.ClientProxy.GetCliloc(1044060 + index);
                return string.IsNullOrEmpty(s) ? fallback : s;
            }
            catch
            {
                return fallback;
            }
        }

        public static List<int> GetUsableSkillIndexes()
        {
            Initialize();

            List<int> indexes = new List<int>();

            foreach (KeyValuePair<int, SkillInfo> kvp in SkillsByIndex)
            {
                if (kvp.Value.IsAction)
                    indexes.Add(kvp.Key);
            }

            return indexes;
        }

        public static List<string> GetUsableSkillNames()
        {
            Initialize();

            List<string> names = new List<string>();

            foreach (KeyValuePair<string, SkillInfo> kvp in SkillsByName)
            {
                if (kvp.Value.IsAction)
                    names.Add(kvp.Key);
            }

            return names;
        }

        public static string GetSkillDisplayName(int index)
        {
            Initialize();

            if (SkillsByIndex.TryGetValue(index, out SkillInfo skill))
                return skill.DisplayName;

            return string.Empty;
        }

        public static int TotalSkills()
        {
            Initialize();
            return SkillsByIndex.Count;
        }
    }
}
