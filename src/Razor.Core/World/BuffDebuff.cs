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

// Portiert aus Razor CE (Razor/Core/BuffDebuffManager.cs), STARK reduziert.
//
// ⚠️ STUB / TODO: Das eigentliche Buff-/Debuff-Tracking speist sich im Original
// aus dem 0xDF "Buff/Debuff"-Paket. Dieser Handler ist im Port noch NICHT
// verdrahtet, daher bleibt PlayerData.BuffsDebuffs vorerst leer und die
// Script-Ausdruecke `findbuff`/`finddebuff` liefern `false`. Sobald der 0xDF-
// Handler in Razor.Core/Network/PacketHandlers.cs ergaenzt ist, befuellt er
// diese Liste — die Script-API bleibt unveraendert (1:1).

namespace Assistant
{
    public sealed class BuffDebuff
    {
        public int Duration { get; set; }
        public string ClilocMessage1 { get; set; } = string.Empty;
        public string ClilocMessage2 { get; set; } = string.Empty;
        public ushort IconNumber { get; set; }
        public string Title { get; set; } = string.Empty;
    }

    public static class BuffDebuffManager
    {
        /// <summary>
        /// Razor CE bildet Buff-Namen auf Grafik-IDs ab (grosse Enum-Tabelle).
        /// Im Port noch nicht portiert — 0 bis das Buff-Tracking steht.
        /// TODO: Name-&gt;BuffIconType-Tabelle aus CE uebernehmen.
        /// </summary>
        public static ushort GetGraphicId(string name)
        {
            return 0;
        }
    }
}
