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

// UOSagas-Razor: Protokoll-Flags, die Razor CE aus der Client-Version ableitet
// (Razor/Core/Main.cs bzw. Engine). Der UOSagas-Client ist ein moderner
// ClassicUO-Fork (>= 7.0.9.0), daher sind alle Flags per Default aktiv.
// Tests/Sonderfaelle koennen sie umschalten.

namespace Assistant
{
    public static class Engine
    {
        /// <summary>Client >= 6.0.1.7: Grid-Byte in Container-Paketen (0x25/0x3C).</summary>
        public static bool UsePostKRPackets = true;

        /// <summary>Client >= 7.0.0.0 (Stygian Abyss).</summary>
        public static bool UsePostSAChanges = true;

        /// <summary>Client >= 7.0.33.1: neues 0x78-Format (Hue immer vorhanden).</summary>
        public static bool UseNewMobileIncoming = true;

        /// <summary>Client >= 7.0.9.0 (High Seas): 0xF3 hat ein Trailing-WORD.</summary>
        public static bool UsePostHSChanges = true;
    }
}
