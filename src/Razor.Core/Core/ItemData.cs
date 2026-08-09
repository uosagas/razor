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

// UOSagas-Razor: Tiledata-Zugriff (Ersatz fuer Razor CEs Ultima.ItemData aus
// tiledata.mul). Die Daten liefert der Client ueber den DataService (ABI);
// hier gecacht pro Graphic, damit nicht jeder Zugriff einen Client-Call macht.
//
// Aufrufe erfolgen auf dem Game-Thread (Snapshot-Build, Dress-/Macro-Logik).

using System.Collections.Concurrent;
using UOSagas.AssistantApi;

namespace Assistant
{
    /// <summary>Tiledata-Eintrag fuer ein Item-Graphic.</summary>
    public struct TileItemData
    {
        public string Name;
        public Layer Layer;
        public ulong Flags;
        public byte Weight;
        public byte Height;
        public bool Valid;

        // Tiledata TileFlag-Bits (Werte aus ClassicUO TileDataLoader.TileFlag).
        public bool IsWeapon => (Flags & 0x00000002UL) != 0;   // Weapon
        public bool IsStackable => (Flags & 0x00000800UL) != 0; // Generic
        public bool IsContainer => (Flags & 0x00200000UL) != 0; // Container
        public bool IsWearable => (Flags & 0x00400000UL) != 0; // Wearable
        public bool IsArmor => (Flags & 0x08000000UL) != 0;    // Armor

        /// <summary>Traegt einen Ausruestungs-Layer (Wearable/Armor/Weapon) — wie Razor CE Item.Layer-Fallback.</summary>
        public bool IsEquippable => IsWearable || IsArmor || IsWeapon;
    }

    /// <summary>Gecachter Tiledata-Zugriff (pro Item-Graphic).</summary>
    public static class ItemData
    {
        private static readonly ConcurrentDictionary<ushort, TileItemData> m_Cache = new();

        public static TileItemData Get(ushort graphic)
        {
            if (m_Cache.TryGetValue(graphic, out TileItemData cached))
                return cached;

            TileItemData data = default;

            if (ClientProxy.TryGetStaticTileData(graphic, out StaticTileInfo info))
            {
                data.Name = string.IsNullOrWhiteSpace(info.Name) ? null : info.Name.Trim();
                data.Layer = (Layer) info.Layer;
                data.Flags = info.Flags;
                data.Weight = info.Weight;
                data.Height = info.Height;
                data.Valid = true;

                // Nur cachen, wenn der Client schon gebunden/geladen ist —
                // sonst wuerden wir ein leeres Ergebnis dauerhaft festhalten.
                m_Cache[graphic] = data;
            }

            return data;
        }

        /// <summary>Tiledata-Name oder null.</summary>
        public static string GetName(ushort graphic)
        {
            return Get(graphic).Name;
        }

        /// <summary>Tiledata-Layer (Razor CE: ItemData.Quality) oder Layer.Invalid.</summary>
        public static Layer GetLayer(ushort graphic)
        {
            return Get(graphic).Layer;
        }
    }
}
