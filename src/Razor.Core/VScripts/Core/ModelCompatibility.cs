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

// UOSagas-Razor: Alias-Properties fuer den VScript-Port.
//
// Die VScript-Nodes stammen aus dem integrierten Assistant und sprechen die
// ClassicUO-Entity-Namen (Graphic, IsDead, X/Y/Z, Stamina, ...). Damit der
// Node-Code diff-arm zum Client bleibt, bekommen die Razor-Modellklassen
// (partial) hier die Client-Namen als reine Aliase — KEINE neue Logik.

namespace Assistant
{
    public partial class UOEntity
    {
        /// <summary>Client: Entity.Distance — Distanz zur Spielerposition.</summary>
        public int Distance => World.Player != null
            ? Utility.Distance(World.Player.Position, Position)
            : int.MaxValue;

        /// <summary>Client: Entity.IsDestroyed.</summary>
        public bool IsDestroyed => Deleted;

        public int X => Position.X;
        public int Y => Position.Y;
        public int Z => Position.Z;
    }

    public partial class Item
    {
        /// <summary>Client: Item.Graphic.</summary>
        public ushort Graphic => ItemID.Value;

        /// <summary>Client: Item.ItemData (Tiledata via DataService).</summary>
        public TileItemData ItemData => Assistant.ItemData.Get(ItemID.Value);

        // OnGround existiert bereits im Razor-Item (CE) — kein Alias noetig.

        /// <summary>Client: IsLocked (nicht bewegbar).</summary>
        public bool IsLocked => !Movable;

        public bool IsHidden => !Visible;

        public bool IsCoin => ItemID.Value >= 0x0EEA && ItemID.Value <= 0x0EF2;

        public bool IsEmpty => Contains.Count == 0;

        // Naeherungen: diese Client-Flags haben im Razor-Weltmodell kein
        // Paket-Gegenstueck (Anzeige-Details des Clients).
        public bool IsDamageable => false;
        public bool IsLootable => IsCorpse;
        public ushort DisplayedGraphic => ItemID.Value;
        public ushort MultiGraphic => ItemID.Value;
        public byte LightID => 0;

        /// <summary>Container als Serial (Client: Item.Container ist uint;
        /// Razor haelt object — Item/Mobile/Serial/null).</summary>
        public uint ContainerSerial => Container switch
        {
            Item i => i.Serial,
            Mobile m => m.Serial,
            Serial s => s,
            _ => 0u
        };

        public uint RootContainerSerial => RootContainer switch
        {
            Item i => i.Serial,
            Mobile m => m.Serial,
            _ => 0u
        };
    }

    public partial class Skill
    {
        /// <summary>Client: Skill.Name (aus der 58er-Skilltabelle).</summary>
        public string Name => Ultima.Skills.SkillsByIndex.TryGetValue(Index, out Ultima.SkillInfo info)
            ? info.Name
            : $"Skill {Index}";

        /// <summary>Client: Skill.IsClickable (benutzbarer Skill).</summary>
        public bool IsClickable => Ultima.Skills.SkillsByIndex.TryGetValue(Index, out Ultima.SkillInfo info) &&
                                   info.IsAction;
    }

    public partial class Mobile
    {
        /// <summary>Client: Mobile.Graphic (Body).</summary>
        public ushort Graphic => Body;

        public bool IsDead => IsGhost;
        public bool IsPoisoned => Poisoned;
        public bool IsParalyzed => Paralyzed;
        public bool IsFemale => Female;
        public bool IsHidden => !Visible;
        public byte NotorietyFlag => Notoriety;
        public bool InWarMode => Warmode;

        // Lua-API (Phase 4b, D27): dokumentierte Naeherungen — das Razor-
        // Weltmodell kennt diese Client-Flags nicht.
        public bool IsFlying => false;
        public bool IsGargoyle => false;
        public bool IsRenamable => false;
        public bool IgnoreCharacters => false;
    }

    public partial class PlayerData
    {
        public ushort WeightMax => MaxWeight;
        public ushort Strength => Str;
        public ushort Dexterity => Dex;
        public ushort Intelligence => Int;
        public int TithingPoints => Tithe;
        public ushort StatsCap => StatCap;
        /// <summary>Client: physische Resistenz — im klassischen Statuspaket ist das die Ruestung (AR).</summary>
        public short PhysicalResistance => (short) AR;

        /// <summary>Client: PlayerMobile.FindBandage — Bandage im Backpack.</summary>
        public Item FindBandage()
        {
            return Backpack?.FindItemById(0x0E21);
        }

        /// <summary>Client: Spieler-Item nach Grafik (Layer + Backpack rekursiv).</summary>
        public Item FindItemByGraphic(ushort graphic)
        {
            for (int i = 0; i < Contains.Count; i++)
            {
                Item item = Contains[i];
                if (item.ItemID.Value == graphic)
                    return item;
            }

            return Backpack?.FindItemById(graphic);
        }

        // ---- Lua-API (Phase 4b, D27) ----------------------------------

        // Char-Sheet-Stats aus den 0x11-Extended-Paketen — das Razor-
        // Weltmodell traegt sie nicht; dokumentierte Naeherung 0.
        public short SwingSpeedIncrease => 0;
        public short HitPointsRegeneration => 0;
        public short ManaRegeneration => 0;
        public short StaminaRegeneration => 0;
        public short LowerManaCost => 0;
        public short LowerReagentCost => 0;
        public short HitChanceIncrease => 0;
        public short DefenseChanceIncrease => 0;
        public short SpellDamageIncrease => 0;
        public short ReflectPhysicalDamage => 0;

        /// <summary>Client: PlayerMobile.Walk — Razor: Bewegung ueber die ABI.</summary>
        public bool Walk(Direction dir, bool run)
        {
            return ClientProxy.RequestMove(dir, run);
        }

        /// <summary>Client: FindItems(graphic, hue) — Suche im Backpack
        /// (rekursiv), Rueckgabe wie das Client-Dictionary (Serial -> Item).</summary>
        public System.Collections.Generic.Dictionary<uint, Item> FindItems(ushort graphic, ushort hue)
        {
            var result = new System.Collections.Generic.Dictionary<uint, Item>();
            Item backpack = Backpack;
            if (backpack != null)
                CollectItems(backpack, graphic, hue, result);

            return result;
        }

        private static void CollectItems(Item container, ushort graphic, ushort hue,
            System.Collections.Generic.Dictionary<uint, Item> result)
        {
            foreach (Item child in container.Contains)
            {
                if (child.ItemID.Value == graphic && child.Hue == hue)
                    result[child.Serial] = child;

                CollectItems(child, graphic, hue, result);
            }
        }
    }

    public partial class Mobile
    {
        public ushort Stamina => Stam;
        public ushort StaminaMax => StamMax;

        /// <summary>Client: Running-Flag im Direction-Byte (0x80).</summary>
        public bool IsRunning => ((byte) Direction & 0x80) != 0;

        /// <summary>Client: sitzt ein Mount auf dem Mount-Layer?</summary>
        public bool IsMounted => GetItemOnLayer(Layer.Mount) != null;

        /// <summary>Client: gelbe Lebensleiste (unverwundbar/blessed).</summary>
        public bool IsYellowHits => Blessed;

        /// <summary>Nicht im Razor-Weltmodell (kein Paket traegt den Titel) — leer.</summary>
        public string Title => string.Empty;

        /// <summary>Nicht im Razor-Weltmodell — 0 (unbekannt).</summary>
        public byte Race => 0;
    }

    public partial class World
    {
        /// <summary>Client: World.TargetManager — VScript-Shim auf Assistant.Targeting.</summary>
        public static VScripts.Core.VScriptTargetManager TargetManager => VScripts.Core.VScriptTargetManager.Instance;

        /// <summary>Client: World.OPL (ObjectPropertyList) — passiver Cache aus
        /// dem 0xD6-Mirror (Core/OplCache); Fallback auf den Weltmodell-Namen.</summary>
        public static OplShim OPL { get; } = new OplShim();

        public class OplShim
        {
            public bool TryGetNameAndData(uint serial, out string name, out string data)
            {
                if (Core.OplCache.TryGet(serial, out name, out data))
                    return true;

                data = string.Empty;
                Serial s = (Serial) serial;
                name = s.IsItem ? FindItem(s)?.Name : FindMobile(s)?.Name;
                return !string.IsNullOrEmpty(name);
            }
        }

        /// <summary>Client: World.Get(serial) — Entity-Lookup (Mobile oder Item).</summary>
        public static UOEntity Get(uint serial)
        {
            Serial s = (Serial) serial;
            if (s.IsMobile)
                return FindMobile(s);
            return FindItem(s);
        }
    }
}
