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

// Portiert aus Razor CE (Razor/Core/Spells.cs) — Phase 3c2.
// ABWEICHUNGEN vom Original (dokumentiert):
//  * Razor CE laedt die Spell-Tabelle aus etc/spells.def; der Port bettet
//    dieselben 182 Eintraege (alle Schulen: Magery 1.-8. Zirkel, Necromancy,
//    Chivalry, Bushido, Ninjitsu, Spellweaving, Mysticism, Masteries) als
//    statische Tabelle ein (Quelle: Razor CE etc/spells.def, unveraendert).
//  * Der Klartext-Name aus spells.def wird zusaetzlich gehalten und beim
//    Registrieren als Anzeige-Text fuer die Name-Cliloc hinterlegt
//    (Language.RegisterRuntime) — CE loest die Cliloc ueber Language auf.
//  * ENTFERNT: MessageManager/HandleSpellMessage (Powerword-Umformatierung,
//    ForceSpellHue — Filter-Thema), UOAssist.PostSpellCast,
//    RequestTitlebarUpdate, Targeting.SpellTargetID, FeatureBit-Checks.
//  * Initialize() registriert explizit (statt static ctor) und ist idempotent
//    (HotKey.Add ersetzt Registrierungen gleicher Identitaet).

using System;
using System.Collections.Generic;

namespace Assistant
{
    public class Spell
    {
        public enum SpellFlag
        {
            None = '?',
            Beneficial = 'B',
            Harmful = 'H',
            Neutral = 'N'
        }

        public enum SpellKind
        {
            Magery,
            Necromancy,
            Chivalry,
            Bushido,
            Ninjutsu,
            Spellweaving,
            Mysticism,
            Masteries,
            Unknown
        }

        public readonly SpellFlag Flag;
        public readonly int Circle;
        public readonly int Number;
        public readonly string PlainName;
        public readonly string WordsOfPower;
        public readonly string[] Reagents;

        private static Timer m_UnflagTimer;

        public Spell(char flag, int n, int c, string name, string power, string[] reags)
        {
            Flag = (SpellFlag) flag;
            Number = n;
            Circle = c;
            PlainName = name;
            WordsOfPower = power;
            Reagents = reags;
        }

        /// <summary>Razor CE: Spell.GetHue — Anzeige-Hue nach Schule (ForceSpellHue).</summary>
        public int GetHue(int def)
        {
            if (!Config.GetBool("ForceSpellHue"))
                return def;

            switch (Flag)
            {
                case SpellFlag.Beneficial:
                    return Config.GetInt("BeneficialSpellHue");
                case SpellFlag.Harmful:
                    return Config.GetInt("HarmfulSpellHue");
                case SpellFlag.Neutral:
                    return Config.GetInt("NeutralSpellHue");
                default:
                    return def;
            }
        }

        public SpellKind Kind
        {
            get
            {
                switch (Circle)
                {
                    case int circle when circle <= 8:
                        return SpellKind.Magery;
                    case 10:
                        return SpellKind.Necromancy;
                    case 20:
                        return SpellKind.Chivalry;
                    case 40:
                        return SpellKind.Bushido;
                    case 50:
                        return SpellKind.Ninjutsu;
                    case 60:
                        return SpellKind.Spellweaving;
                    case 65:
                        return SpellKind.Mysticism;
                    case 70:
                        return SpellKind.Masteries;
                    default:
                        return SpellKind.Unknown;
                }
            }
        }

        public bool NeedsChanneling
        {
            get { return (Kind == SpellKind.Magery || Kind == SpellKind.Mysticism); }
        }

        /// <summary>Razor CE: Spell.Name — Cliloc-Nummer des Spell-Namens.</summary>
        public int Name
        {
            get
            {
                switch (Kind)
                {
                    case SpellKind.Magery:
                        return 3002011 + ((Circle - 1) * 8) + (Number - 1);

                    case SpellKind.Necromancy:
                        return 1060509 + Number - 1;

                    case SpellKind.Chivalry:
                        return 1060585 + Number - 1;

                    case SpellKind.Bushido:
                        return 1060595 + Number - 1;

                    case SpellKind.Ninjutsu:
                        return 1060610 + Number - 1;

                    case SpellKind.Spellweaving:
                        return 1071026 + Number - 1;

                    case SpellKind.Mysticism:
                        return 1031678 + this.Number - 28;

                    case SpellKind.Masteries:
                    {
                        if (Number <= 6)
                            return 1115683 + Number - 1;

                        return 1155896 + Number - 7;
                    }

                    default:
                        return -1;
                }
            }
        }

        public override string ToString()
        {
            return $"{Language.GetString(this.Name)} (#{GetID()})";
        }

        public string GetName()
        {
            return $"{Language.GetString(Name)}";
        }

        public int GetID()
        {
            return ToID(Circle, Number);
        }

        public void OnCast(Packet p)
        {
            Cast();
            ClientProxy.SendToServer(p);
        }

        /// <summary>
        /// Razor CE: Spell.Cast — SpellUnequip (Haende freimachen bei
        /// Channeling-Schulen), Reagenzien-Counter markieren, LastSpell/
        /// LastCastTime pflegen.
        /// </summary>
        private void Cast()
        {
            if (Config.GetBool("SpellUnequip") && NeedsChanneling)
            {
                Item pack = World.Player?.Backpack;
                if (pack != null)
                {
                    // Runebooks/Spellbooks nicht ablegen (wie Razor CE).
                    Item item = World.Player.GetItemOnLayer(Layer.RightHand);
                    if (item != null && item.ItemID != 0x22C5 && item.ItemID != 0xE3B && item.ItemID != 0xEFA)
                    {
                        DragDropManager.Drag(item, item.Amount);
                        DragDropManager.Drop(item, pack);
                    }

                    item = World.Player.GetItemOnLayer(Layer.LeftHand);
                    if (item != null && item.ItemID != 0x22C5 && item.ItemID != 0xE3B && item.ItemID != 0xEFA)
                    {
                        DragDropManager.Drag(item, item.Amount);
                        DragDropManager.Drop(item, pack);
                    }
                }
            }

            for (int i = 0; i < Counter.List.Count; i++)
                Counter.List[i].Flag = false;

            if (Config.GetBool("HighlightReagents"))
            {
                for (int r = 0; r < Reagents.Length; r++)
                {
                    for (int i = 0; i < Counter.List.Count; i++)
                    {
                        Counter c = Counter.List[i];
                        if (c.Enabled && c.Format.ToLower() == Reagents[r])
                        {
                            c.Flag = true;
                            break;
                        }
                    }
                }

                if (m_UnflagTimer != null)
                    m_UnflagTimer.Stop();
                else
                    m_UnflagTimer = new UnflagTimer();
                m_UnflagTimer.Start();
            }

            if (World.Player != null)
            {
                World.Player.LastSpell = GetID();
                LastCastTime = DateTime.UtcNow;
            }
        }

        public static DateTime LastCastTime = DateTime.MinValue;

        private class UnflagTimer : Timer
        {
            public UnflagTimer() : base(TimeSpan.FromSeconds(30.0))
            {
            }

            protected override void OnTick()
            {
                for (int i = 0; i < Counter.List.Count; i++)
                    Counter.List[i].Flag = false;
            }
        }

        private static readonly Dictionary<string, Spell> m_SpellsByPower = new Dictionary<string, Spell>(64 + 10 + 16);
        private static readonly Dictionary<int, Spell> m_SpellsByID = new Dictionary<int, Spell>(64 + 10 + 16);
        private static readonly Dictionary<string, Spell> m_SpellsByName = new Dictionary<string, Spell>(64 + 10 + 16);
        private static HotKeyCallbackState HotKeyCallback;

        // Spell-Tabelle aus Razor CE etc/spells.def (Flag | Nummer | Zirkel |
        // Name | Words of Power | Reagenzien). 182 Eintraege.
        private static readonly Spell[] m_SpellDef =
        {
            new Spell('H', 1, 1, "Clumsy", "Uus Jux", new[] { "bm", "ns" }),
            new Spell('N', 2, 1, "Create Food", "In Mani Ylem", new[] { "gl", "gs", "mr" }),
            new Spell('H', 3, 1, "Feeblemind", "Rel Wis", new[] { "gs", "ns" }),
            new Spell('B', 4, 1, "Heal", "In Mani", new[] { "gl", "gs", "ss" }),
            new Spell('H', 5, 1, "Magic Arrow", "In Por Ylem", new[] { "sa" }),
            new Spell('N', 6, 1, "Night Sight", "In Lor", new[] { "ss", "sa" }),
            new Spell('B', 7, 1, "Reactive Armor", "Flam Sanct", new[] { "gl", "sa", "ss" }),
            new Spell('H', 8, 1, "Weaken", "Des Mani", new[] { "gl", "ns" }),
            new Spell('B', 1, 2, "Agility", "Ex Uus", new[] { "bm", "mr" }),
            new Spell('B', 2, 2, "Cunning", "Uus Wis", new[] { "mr", "ns" }),
            new Spell('B', 3, 2, "Cure", "An Nox", new[] { "gl", "gs" }),
            new Spell('H', 4, 2, "Harm", "An Mani", new[] { "ns", "ss" }),
            new Spell('N', 5, 2, "Magic Trap", "In Jux", new[] { "gl", "sa", "ss" }),
            new Spell('N', 6, 2, "Magic Untrap", "An Jux", new[] { "bm", "sa" }),
            new Spell('B', 7, 2, "Protection", "Uus Sanct", new[] { "gl", "gs", "sa" }),
            new Spell('B', 8, 2, "Strength", "Uus Mani", new[] { "mr", "ns" }),
            new Spell('B', 1, 3, "Bless", "Rel Sanct", new[] { "gl", "mr" }),
            new Spell('H', 2, 3, "Fireball", "Vas Flam", new[] { "bp" }),
            new Spell('N', 3, 3, "Magic Lock", "An Por", new[] { "sa", "bm", "gl" }),
            new Spell('H', 4, 3, "Poison", "In Nox", new[] { "ns" }),
            new Spell('N', 5, 3, "Telekinesis", "Ort Por Ylem", new[] { "bm", "mr" }),
            new Spell('N', 6, 3, "Teleport", "Rel Por", new[] { "bm", "mr" }),
            new Spell('N', 7, 3, "Unlock", "Ex Por", new[] { "bm", "sa" }),
            new Spell('N', 8, 3, "Wall of Stone", "In Sanct Ylem", new[] { "bm", "gl" }),
            new Spell('B', 1, 4, "Arch Cure", "Vas An Nox", new[] { "gl", "gs", "mr" }),
            new Spell('B', 2, 4, "Arch Protection", "Vas Uus Sanct", new[] { "gl", "gs", "mr", "sa" }),
            new Spell('H', 3, 4, "Curse", "Des Sanct", new[] { "gl", "ns", "sa" }),
            new Spell('N', 4, 4, "Fire Field", "In Flam Grav", new[] { "bp", "ss", "sa" }),
            new Spell('B', 5, 4, "Greater Heal", "In Vas Mani", new[] { "gl", "gs", "mr", "ss" }),
            new Spell('H', 6, 4, "Lightning", "Por Ort Grav", new[] { "mr", "sa" }),
            new Spell('H', 7, 4, "Mana Drain", "Ort Rel", new[] { "bp", "mr", "ss" }),
            new Spell('N', 8, 4, "Recall", "Kal Ort Por", new[] { "bp", "bm", "mr" }),
            new Spell('H', 1, 5, "Blade Spirits", "In Jux Hur Ylem", new[] { "bp", "mr", "ns" }),
            new Spell('N', 2, 5, "Dispel Field", "An Grav", new[] { "gl", "bp", "ss", "sa" }),
            new Spell('N', 3, 5, "Incognito", "Kal In Ex", new[] { "bm", "gl", "ns" }),
            new Spell('B', 4, 5, "Magic Reflection", "In Jux Sanct", new[] { "gl", "mr", "ss" }),
            new Spell('H', 5, 5, "Mind Blast", "Por Corp Wis", new[] { "bp", "mr", "ns", "sa" }),
            new Spell('H', 6, 5, "Paralyze", "An Ex Por", new[] { "gl", "mr", "ss" }),
            new Spell('N', 7, 5, "Poison Field", "In Nox Grav", new[] { "bp", "ns", "ss" }),
            new Spell('N', 8, 5, "Summon Creature", "Kal Xen", new[] { "bm", "mr", "ss" }),
            new Spell('N', 1, 6, "Dispel", "An Ort", new[] { "gl", "mr", "sa" }),
            new Spell('H', 2, 6, "Energy Bolt", "Corp Por", new[] { "bp", "ns" }),
            new Spell('H', 3, 6, "Explosion", "Vas Ort Flam", new[] { "bm", "mr" }),
            new Spell('N', 4, 6, "Invisibility", "An Lor Xen", new[] { "bm", "ns" }),
            new Spell('N', 5, 6, "Mark", "Kal Por Ylem", new[] { "bp", "bm", "mr" }),
            new Spell('N', 6, 6, "Mass Curse", "Vas Des Sanct", new[] { "gl", "mr", "ns", "sa" }),
            new Spell('N', 7, 6, "Paralyze Field", "In Ex Grav", new[] { "bp", "gs", "ss" }),
            new Spell('N', 8, 6, "Reveal", "Wis Quas", new[] { "bm", "sa" }),
            new Spell('H', 1, 7, "Chain Lightning", "Vas Ort Grav", new[] { "bp", "mr", "bm", "sa" }),
            new Spell('N', 2, 7, "Energy Field", "In Sanct Grav", new[] { "bp", "mr", "ss", "sa" }),
            new Spell('H', 3, 7, "Flamestrike", "Kal Vas Flam", new[] { "ss", "sa" }),
            new Spell('N', 4, 7, "Gate Travel", "Vas Rel Por", new[] { "bp", "mr", "sa" }),
            new Spell('H', 5, 7, "Mana Vampire", "Ort Sanct", new[] { "bp", "bm", "mr", "ss" }),
            new Spell('N', 6, 7, "Mass Dispel", "Vas An Ort", new[] { "bp", "gl", "mr", "sa" }),
            new Spell('H', 7, 7, "Meteor Swarm", "Flam Kal Des Ylem", new[] { "bm", "ss", "mr", "sa" }),
            new Spell('N', 8, 7, "Polymorph", "Vas Ylem Rel", new[] { "bm", "mr", "ss" }),
            new Spell('H', 1, 8, "Earthquake", "In Vas Por", new[] { "bm", "gs", "mr", "sa" }),
            new Spell('H', 2, 8, "Energy Vortex", "Vas Corp Por", new[] { "bp", "bm", "mr", "ns" }),
            new Spell('B', 3, 8, "Resurrection", "An Corp", new[] { "bm", "gl", "gs" }),
            new Spell('N', 4, 8, "Summon Air Elemental", "Kal Vas Xen Hur", new[] { "bm", "mr", "ss" }),
            new Spell('N', 5, 8, "Summon Daemon", "Kal Vas Xen Corp", new[] { "bm", "mr", "ss", "sa" }),
            new Spell('N', 6, 8, "Summon Earth Elemental", "Kal Vas Xen Ylem", new[] { "bm", "mr", "ss" }),
            new Spell('N', 7, 8, "Summon Fire Elemental", "Kal Vas Xen Flam", new[] { "bm", "mr", "ss", "sa" }),
            new Spell('N', 8, 8, "Summon Water Elemental", "Kal Vas Xen An Flam", new[] { "bm", "mr", "ss" }),
            new Spell('N', 1, 10, "Animate Dead", "Uus Corp", new[] { "db", "gd" }),
            new Spell('H', 2, 10, "Blood Oath", "In Jux Mani Xen", new[] { "db" }),
            new Spell('N', 3, 10, "Corpse Skin", "In Agle Corp Ylem", new[] { "bw", "gd" }),
            new Spell('B', 4, 10, "Curse Weapon", "An Sanct Gra Char", new[] { "pi" }),
            new Spell('H', 5, 10, "Evil Omen", "Pas Tym An Sanct", new[] { "bw", "nc" }),
            new Spell('N', 6, 10, "Horrific Beast", "Rel Xen Vas Bal", new[] { "bw", "db" }),
            new Spell('N', 7, 10, "Lich Form", "Rel Xen Corp Ort", new[] { "nc", "db", "gd" }),
            new Spell('H', 8, 10, "Mind Rot", "Wis An Ben", new[] { "bw", "db", "pi" }),
            new Spell('H', 9, 10, "Pain Spike", "In Sar", new[] { "gd", "pi" }),
            new Spell('H', 10, 10, "Poison Strike", "In Vas Nox", new[] { "nc" }),
            new Spell('H', 11, 10, "Strangle", "In Bal Nox", new[] { "nc", "db" }),
            new Spell('N', 12, 10, "Summon Familiar", "Kal Xen Bal", new[] { "bw", "gd", "db" }),
            new Spell('N', 13, 10, "Vampiric Embrace", "Rel Xen An Sanct", new[] { "bw", "nc", "pi" }),
            new Spell('H', 14, 10, "Vengeful Spirit", "Kal Xen Bal Beh", new[] { "bw", "gd", "pi" }),
            new Spell('H', 15, 10, "Wither", "Kal Vas An Flam", new[] { "nc", "gd", "pi" }),
            new Spell('N', 16, 10, "Wraith Form", "Rel Xen Um", new[] { "pi", "nc" }),
            new Spell('N', 17, 10, "Exorcism", "Ort Corp Grav", new[] { "nc", "gd" }),
            new Spell('B', 1, 20, "Cleanse by Fire", "Expor Flamus", new string[0]),
            new Spell('B', 2, 20, "Close Wounds", "Obsu Vulni", new string[0]),
            new Spell('B', 3, 20, "Consecrate Weapon", "Consecrus Arma", new string[0]),
            new Spell('N', 4, 20, "Dispel Evil", "Dispiro Malas", new string[0]),
            new Spell('B', 5, 20, "Divine Fury", "Divinum Furis", new string[0]),
            new Spell('H', 6, 20, "Enemy of One", "Forul Solum", new string[0]),
            new Spell('H', 7, 20, "Holy Light", "Augus Luminos", new string[0]),
            new Spell('B', 8, 20, "Noble Sacrifice", "Dium Prostra", new string[0]),
            new Spell('B', 9, 20, "Remove Curse", "Extermo Vomica", new string[0]),
            new Spell('N', 10, 20, "Sacred Journey", "Sanctum Viatas", new string[0]),
            new Spell('H', 1, 40, "Honorable Execution", "", new string[0]),
            new Spell('B', 2, 40, "Confidence", "", new string[0]),
            new Spell('B', 3, 40, "Evasion", "", new string[0]),
            new Spell('H', 4, 40, "Counter Attack", "", new string[0]),
            new Spell('H', 5, 40, "Lightning Strike", "", new string[0]),
            new Spell('H', 6, 40, "Momentum Strike", "", new string[0]),
            new Spell('H', 1, 50, "Focus Attack", "", new string[0]),
            new Spell('H', 2, 50, "Death Strike", "", new string[0]),
            new Spell('B', 3, 50, "Animal Form", "", new string[0]),
            new Spell('H', 4, 50, "Ki Attack", "", new string[0]),
            new Spell('H', 5, 50, "Surprise Attack", "", new string[0]),
            new Spell('H', 6, 50, "Backstab", "", new string[0]),
            new Spell('N', 7, 50, "Shadowjump", "", new string[0]),
            new Spell('N', 8, 50, "Mirror Image", "", new string[0]),
            new Spell('N', 1, 60, "Arcane Circle", "Myrshalee", new string[0]),
            new Spell('B', 2, 60, "Gift of Renewal", "Olorisstra", new string[0]),
            new Spell('N', 3, 60, "Immolating Weapon", "Thalshara", new string[0]),
            new Spell('H', 4, 60, "Attune Weapon", "Haeldril", new string[0]),
            new Spell('H', 5, 60, "Thunderstorm", "Erelonia", new string[0]),
            new Spell('H', 6, 60, "Nature's Fury", "Rauvvrae", new string[0]),
            new Spell('N', 7, 60, "Summon Fey", "Alalithra", new string[0]),
            new Spell('N', 8, 60, "Summon Fiend", "Nylisstra", new string[0]),
            new Spell('N', 9, 60, "Reaper Form", "Tarisstree", new string[0]),
            new Spell('H', 10, 60, "Wildfire", "Haelyn", new string[0]),
            new Spell('H', 11, 60, "Essence of Wind", "Anathrae", new string[0]),
            new Spell('N', 12, 60, "Dryad Allure", "Rathril", new string[0]),
            new Spell('N', 13, 60, "Ethereal Voyage", "Orlavdra", new string[0]),
            new Spell('H', 14, 60, "Word of Death", "Nyraxle", new string[0]),
            new Spell('B', 15, 60, "Gift of Life", "Illorae", new string[0]),
            new Spell('B', 16, 60, "Arcane Empowerment", "Aslavdra", new string[0]),
            new Spell('H', 28, 65, "Nether Bolt", "In Corp Ylem", new string[0]),
            new Spell('N', 29, 65, "Healing Stone", "Kal In Mani", new string[0]),
            new Spell('H', 30, 65, "Purge Magic", "An Ort Sanct", new string[0]),
            new Spell('N', 31, 65, "Enchant", "In Ort Ylem", new string[0]),
            new Spell('H', 32, 65, "Sleep", "In Zu", new string[0]),
            new Spell('H', 33, 65, "Eagle Strike", "Kal Por Xen", new string[0]),
            new Spell('H', 34, 65, "Animated Weapon", "In Jux Por Ylem", new string[0]),
            new Spell('N', 35, 65, "Stone Form", "In Rel Ylem", new string[0]),
            new Spell('N', 36, 65, "Spell Trigger", "In Vas Ort Ex", new string[0]),
            new Spell('H', 37, 65, "Mass Sleep", "Vas Zu", new string[0]),
            new Spell('B', 38, 65, "Cleansing Winds", "In Vas Mani Hur", new string[0]),
            new Spell('H', 39, 65, "Bombard", "Corp Por Ylem", new string[0]),
            new Spell('H', 40, 65, "Spell Plague", "Vas Rel Jux Ort", new string[0]),
            new Spell('H', 41, 65, "Hail Storm", "Kal Des Ylem", new string[0]),
            new Spell('H', 42, 65, "Nether Cyclone", "Grav Hur", new string[0]),
            new Spell('H', 43, 65, "Rising Colossus", "Kal Vas Xen Corp Ylem", new string[0]),
            new Spell('N', 1, 70, "Inspire", "Uus Por", new string[0]),
            new Spell('N', 2, 70, "Invigorate", "An Zu", new string[0]),
            new Spell('N', 3, 70, "Resilience", "Kal Mani Tym", new string[0]),
            new Spell('N', 4, 70, "Perserverance", "Uus Jux Sanct", new string[0]),
            new Spell('N', 5, 70, "Tribulation", "In Jux Hur Rel", new string[0]),
            new Spell('N', 6, 70, "Despair", "Kal Des Mani Tym", new string[0]),
            new Spell('N', 7, 70, "Death Ray", "In Grav Corp", new string[0]),
            new Spell('N', 8, 70, "Ethereal Burst", "Uus Ort Grav", new string[0]),
            new Spell('N', 9, 70, "Nether Blast", "In Vas Xen Por", new string[0]),
            new Spell('N', 10, 70, "Mystic Weapon", "Vas Ylem Wis", new string[0]),
            new Spell('N', 11, 70, "Command Undead", "In Corp Xen Por", new string[0]),
            new Spell('N', 12, 70, "Conduit", "Uus Corp Grav", new string[0]),
            new Spell('N', 13, 70, "Mana Shield", "Faerkulggen", new string[0]),
            new Spell('N', 14, 70, "Summon Reaper", "Lartarisstree", new string[0]),
            new Spell('N', 15, 70, "Enchanted Summoning", "NA", new string[0]),
            new Spell('N', 16, 70, "Anticipate Hit", "NA", new string[0]),
            new Spell('N', 17, 70, "Warcry", "NA", new string[0]),
            new Spell('N', 18, 70, "Intuition", "NA", new string[0]),
            new Spell('N', 19, 70, "Rejuvenate", "NA", new string[0]),
            new Spell('N', 20, 70, "Holy Fist", "NA", new string[0]),
            new Spell('N', 21, 70, "Shadow", "NA", new string[0]),
            new Spell('N', 22, 70, "White Tiger Form", "NA", new string[0]),
            new Spell('N', 23, 70, "Flaming Shot", "NA", new string[0]),
            new Spell('N', 24, 70, "Playing The Odds", "NA", new string[0]),
            new Spell('N', 25, 70, "Thrust", "NA", new string[0]),
            new Spell('N', 26, 70, "Pierce", "NA", new string[0]),
            new Spell('N', 27, 70, "Stagger", "NA", new string[0]),
            new Spell('N', 28, 70, "Toughness", "NA", new string[0]),
            new Spell('N', 29, 70, "Onslaught", "NA", new string[0]),
            new Spell('N', 30, 70, "Focused Eye", "NA", new string[0]),
            new Spell('N', 31, 70, "Elemental Fury", "NA", new string[0]),
            new Spell('N', 32, 70, "Called Shot", "NA", new string[0]),
            new Spell('N', 33, 70, "Saving Throw", "NA", new string[0]),
            new Spell('N', 34, 70, "Shield Bash", "NA", new string[0]),
            new Spell('N', 35, 70, "Bodyguard", "NA", new string[0]),
            new Spell('N', 36, 70, "Heighten Senses", "NA", new string[0]),
            new Spell('N', 37, 70, "Tolerance", "NA", new string[0]),
            new Spell('N', 38, 70, "Injected Strike", "NA", new string[0]),
            new Spell('N', 39, 70, "Potency", "NA", new string[0]),
            new Spell('N', 40, 70, "Rampage", "NA", new string[0]),
            new Spell('N', 41, 70, "Fists of Fury", "NA", new string[0]),
            new Spell('N', 42, 70, "Knockout", "NA", new string[0]),
            new Spell('N', 43, 70, "Whispering", "NA", new string[0]),
            new Spell('N', 44, 70, "Combat Training", "NA", new string[0]),
            new Spell('N', 45, 70, "Boarding", "NA", new string[0]),
        };

        /// <summary>
        /// Baut die Lookup-Tabellen und registriert alle Spell-Hotkeys
        /// (Razor CE: static ctor). Idempotent; VOR Config.LoadLastProfile
        /// aufrufen, damit gespeicherte Belegungen greifen.
        /// </summary>
        public static void Initialize()
        {
            m_SpellsByID.Clear();
            m_SpellsByName.Clear();
            m_SpellsByPower.Clear();

            foreach (Spell s in m_SpellDef)
            {
                m_SpellsByID[s.GetID()] = s;

                string name = s.PlainName;
                if (!string.IsNullOrEmpty(name))
                {
                    m_SpellsByName[name.ToLower()] = s;

                    // Anzeige-Text der Name-Cliloc (CE: Language-Pack/Cliloc).
                    Language.RegisterRuntime(s.Name, name);
                }

                if (s.WordsOfPower != null && s.WordsOfPower.Trim().Length > 0)
                    m_SpellsByPower[s.WordsOfPower] = s;
            }

            HotKeyCallback = new HotKeyCallbackState(OnHotKey);
            foreach (Spell s in m_SpellsByID.Values)
                HotKey.Add(HKCategory.Spells, HKSubCat.SpellOffset + s.Circle, s.Name, HotKeyCallback,
                    (ushort) s.GetID());

            HotKey.Add(HKCategory.Spells, LocString.HealOrCureSelf, new HotKeyCallback(HealOrCureSelf));
            HotKey.Add(HKCategory.Spells, LocString.MiniHealOrCureSelf, new HotKeyCallback(MiniHealOrCureSelf));
            HotKey.Add(HKCategory.Spells, LocString.GHealOrCureSelf, new HotKeyCallback(GHealOrCureSelf));
            HotKey.Add(HKCategory.Spells, LocString.Interrupt, new HotKeyCallback(Interrupt));
        }

        /// <summary>Razor CE: Smart Heal/Cure Self (Greater Heal/Mini Heal/Cure nach Zustand).</summary>
        public static void HealOrCureSelf()
        {
            if (World.Player == null)
                return;

            Spell s;

            if (World.Player.Poisoned)
            {
                s = Get(2, 3); // cure
            }
            else if (World.Player.Hits + 2 < World.Player.HitsMax)
            {
                if (World.Player.Hits + 30 < World.Player.HitsMax && World.Player.Mana >= 12)
                    s = Get(4, 5); // greater heal
                else
                    s = Get(1, 4); // mini heal
            }
            else
            {
                if (World.Player.Mana >= 12)
                    s = Get(4, 5); // greater heal
                else
                    s = Get(1, 4); // mini heal
            }

            if (s != null)
            {
                if (World.Player.Poisoned || World.Player.Hits < World.Player.HitsMax)
                    Targeting.TargetSelf(true);
                ClientProxy.SendToServer(new CastSpellFromMacro((ushort) s.GetID()));
                s.Cast();
            }
        }

        public static void MiniHealOrCureSelf()
        {
            if (World.Player == null)
                return;

            Spell s;
            if (World.Player.Poisoned)
                s = Get(2, 3); // cure
            else
                s = Get(1, 4); // mini heal

            if (s != null)
            {
                if (World.Player.Poisoned || World.Player.Hits < World.Player.HitsMax)
                    Targeting.TargetSelf(true);
                ClientProxy.SendToServer(new CastSpellFromMacro((ushort) s.GetID()));
                s.Cast();
            }
        }

        public static void GHealOrCureSelf()
        {
            if (World.Player == null)
                return;

            Spell s;
            if (World.Player.Poisoned)
                s = Get(2, 3); // cure
            else
                s = Get(4, 5); // gheal

            if (s != null)
            {
                if (World.Player.Poisoned || World.Player.Hits < World.Player.HitsMax)
                    Targeting.TargetSelf(true);
                ClientProxy.SendToServer(new CastSpellFromMacro((ushort) s.GetID()));
                s.Cast();
            }
        }

        /// <summary>Razor CE: "> Interrupt" — angelegtes Item kurz ab- und wieder anlegen.</summary>
        public static void Interrupt()
        {
            Item item = FindUsedLayer();

            if (item != null)
            {
                ClientProxy.SendToServer(new LiftRequest(item, 1)); // unequip
                ClientProxy.SendToServer(new EquipRequest(item.Serial, World.Player, item.Layer)); // equip
            }
        }

        private static readonly Layer[] m_InterruptLayers =
        {
            Layer.Shirt, Layer.Shoes, Layer.Pants, Layer.Head, Layer.Gloves, Layer.Ring, Layer.Neck, Layer.Waist,
            Layer.InnerTorso, Layer.Bracelet, Layer.MiddleTorso, Layer.Earrings, Layer.Arms, Layer.Cloak,
            Layer.OuterTorso, Layer.OuterLegs, Layer.InnerLegs, Layer.RightHand, Layer.LeftHand
        };

        private static Item FindUsedLayer()
        {
            if (World.Player == null)
                return null;

            foreach (Layer layer in m_InterruptLayers)
            {
                Item layeredItem = World.Player.GetItemOnLayer(layer);
                if (layeredItem != null)
                    return layeredItem;
            }

            return null;
        }

        public static void OnHotKey(ref object state)
        {
            ushort id = (ushort) state;
            Spell s = Spell.Get(id);
            if (s != null)
            {
                s.OnCast(new CastSpellFromMacro(id));
            }
        }

        public static int ToID(int circle, int num)
        {
            if (circle < 10)
                return ((circle - 1) * 8) + num;
            else
                return (circle * 10) + num;
        }

        /// <summary>Anzahl der eingebetteten Spell-Definitionen (Testverankerung).</summary>
        public static int Count
        {
            get { return m_SpellDef.Length; }
        }

        public static Spell Get(string power)
        {
            m_SpellsByPower.TryGetValue(power, out Spell s);
            return s;
        }

        public static Spell Get(int num)
        {
            m_SpellsByID.TryGetValue(num, out Spell s);
            return s;
        }

        public static Spell GetByName(string name)
        {
            m_SpellsByName.TryGetValue(name.ToLower(), out Spell s);
            return s;
        }

        public static string GetName(int num)
        {
            if (m_SpellsByID.TryGetValue(num, out Spell spell))
                return spell.GetName();

            return string.Empty;
        }

        public static Spell Get(int circle, int num)
        {
            return Get(Spell.ToID(circle, num));
        }
    }
}
