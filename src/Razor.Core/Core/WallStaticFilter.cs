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

// Portiert aus Razor CE (Razor/Filters/WallStaticFilter.cs) — Option
// ShowStaticWalls (+ ShowStaticWallLabels): Feldzauber (Fire/Poison/Paralyze/
// Energy/Wall of Stone) werden als eingefaerbte Wand-Statics dargestellt.
//
// Abweichung zum CE-Mechanismus (dokumentiert): CE BLOCKT das Original-Paket
// und schickt ein eigenes; unser Mirror ist read-only und beim 0xF7-Batch
// wuerde ein Block das GANZE Buendel treffen. Deshalb Update-Inject: das
// Original erreicht den Client, direkt danach injizieren wir ein 0xF3 mit
// derselben Serial und neuer Grafik/Hue — der Client aktualisiert das Item.

namespace Assistant
{
    public static class WallStaticFilter
    {
        private const ushort WallStaticID = 0x28A8;
        private const ushort WallStaticIDStone = 0x0750;

        internal enum WallColor : ushort
        {
            Stone = 0x3B1,
            Fire = 0x0845,
            Poison = 0x016A,
            Paralyze = 0x00DA,
            Energy = 0x0125
        }

        /// <summary>Ersetzt Feldzauber-Grafiken (CE 1:1, IDs identisch);
        /// true = Item war ein Feld und wurde ersetzt.</summary>
        public static bool MakeWallStatic(Item wall)
        {
            switch (wall.ItemID.Value)
            {
                case 0x0080:
                case 0x0082:
                    return Replace(wall, WallStaticIDStone, WallColor.Stone, "[Wall Of Stone]");
                case 0x3996:
                case 0x398C:
                    return Replace(wall, WallStaticID, WallColor.Fire, "[Fire Field]");
                case 0x3915:
                case 0x3920:
                case 0x3922:
                    return Replace(wall, WallStaticID, WallColor.Poison, "[Poison Field]");
                case 0x3967:
                case 0x3979:
                    return Replace(wall, WallStaticID, WallColor.Paralyze, "[Paralyze Field]");
                case 0x3946:
                case 0x3956:
                    return Replace(wall, WallStaticID, WallColor.Energy, "[Energy Field]");
                default:
                    return false;
            }
        }

        private static bool Replace(Item wall, ushort staticId, WallColor color, string label)
        {
            // Weltmodell wie CE mitziehen (Folge-Logik sieht die Ersatzgrafik).
            wall.ItemID = staticId;
            wall.Hue = (ushort) color;

            ClientProxy.SendToClient(new WorldItem(wall));

            if (Config.GetBool("ShowStaticWallLabels"))
                ClientProxy.SendToClient(new UnicodeMessage(wall.Serial, wall.ItemID.Value, MessageType.Regular,
                    (ushort) color, 3, Language.CliLocName, wall.Name, label));

            return true;
        }
    }
}
