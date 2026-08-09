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

// Portiert (getrimmt) aus Razor CE (Razor/Core/Utility.cs):
// Distanz-/Range-Helfer fuer das Weltmodell + Random (Phase 2c,
// DoubleClickType/TargetType-Zufallsauswahl).

using System;

namespace Assistant
{
    public static class Utility
    {
        private static readonly Random m_Random = new Random();

        public static int Random(int min, int max)
        {
            return m_Random.Next(max - min + 1) + min;
        }

        public static int Random(int num)
        {
            return m_Random.Next(num);
        }

        /// <summary>Razor CE: Utility.Offset — ein Feld in Blickrichtung.</summary>
        public static void Offset(Direction d, ref int x, ref int y)
        {
            switch (d & Direction.Mask)
            {
                case Direction.North: --y; break;
                case Direction.South: ++y; break;
                case Direction.West:  --x; break;
                case Direction.East:  ++x; break;
                case Direction.Right: ++x; --y; break;
                case Direction.Left:  --x; ++y; break;
                case Direction.Down:  ++x; ++y; break;
                case Direction.Up:    --x; --y; break;
            }
        }

        public static bool InRange(IPoint2D from, IPoint2D to, int range)
        {
            return (to.X >= (from.X - range))
                   && (to.X <= (from.X + range))
                   && (to.Y >= (from.Y - range))
                   && (to.Y <= (from.Y + range));
        }

        public static int Distance(int fx, int fy, int tx, int ty)
        {
            int xDelta = Math.Abs(fx - tx);
            int yDelta = Math.Abs(fy - ty);

            return (xDelta > yDelta ? xDelta : yDelta);
        }

        public static int Distance(IPoint2D from, IPoint2D to)
        {
            int xDelta = Math.Abs(from.X - to.X);
            int yDelta = Math.Abs(from.Y - to.Y);

            return (xDelta > yDelta ? xDelta : yDelta);
        }

        public static double DistanceSqrt(IPoint2D from, IPoint2D to)
        {
            float xDelta = Math.Abs(from.X - to.X);
            float yDelta = Math.Abs(from.Y - to.Y);

            return Math.Sqrt(xDelta * xDelta + yDelta * yDelta);
        }

        // --- Parse-Helfer (Razor CE Core/Utility.cs): dezimal ODER "0x"-Hex,
        //     bei Fehler der Default. Von den Script-Kommandos genutzt. ---

        public static int ToInt32(string str, int def)
        {
            if (string.IsNullOrEmpty(str))
                return def;

            int val;

            if (str.StartsWith("0x"))
            {
                if (int.TryParse(str.Substring(2), System.Globalization.NumberStyles.HexNumber, null, out val))
                    return val;
            }
            else if (int.TryParse(str, out val))
                return val;

            return def;
        }

        public static uint ToUInt32(string str, uint def)
        {
            if (string.IsNullOrEmpty(str))
                return def;

            uint val;

            if (str.StartsWith("0x"))
            {
                if (uint.TryParse(str.Substring(2), System.Globalization.NumberStyles.HexNumber, null, out val))
                    return val;
            }
            else if (uint.TryParse(str, out val))
                return val;

            return def;
        }

        public static ushort ToUInt16(string str, ushort def)
        {
            if (string.IsNullOrEmpty(str))
                return def;

            ushort val;

            if (str.StartsWith("0x"))
            {
                if (ushort.TryParse(str.Substring(2), System.Globalization.NumberStyles.HexNumber, null, out val))
                    return val;
            }
            else if (ushort.TryParse(str, out val))
                return val;

            return def;
        }

        public static long ToLong(string str, long def)
        {
            if (string.IsNullOrEmpty(str))
                return def;

            long val;

            if (str.StartsWith("0x"))
            {
                if (long.TryParse(str.Substring(2), System.Globalization.NumberStyles.HexNumber, null, out val))
                    return val;
            }
            else if (long.TryParse(str, out val))
                return val;

            return def;
        }
    }
}
