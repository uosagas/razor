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

// Portiert aus Razor CE (Razor/HotKeys/Undress.cs) — Phase 3c.
// OnUndressAll ist bereits in Assistant.Core.Dress.UndressAll (Phase 2d).

using Assistant.Core;

namespace Assistant.HotKeys
{
    public class UndressHotKeys
    {
        public static void Initialize()
        {
            HotKey.Add(HKCategory.Dress, LocString.ArmDisarmRight, new HotKeyCallback(Dress.ToggleRight));
            HotKey.Add(HKCategory.Dress, LocString.ArmDisarmLeft, new HotKeyCallback(Dress.ToggleLeft));

            HotKey.Add(HKCategory.Dress, LocString.UndressAll, new HotKeyCallback(Dress.UndressAll));
            HotKey.Add(HKCategory.Dress, LocString.UndressHands, new HotKeyCallback(OnUnequipBothHands));
            HotKey.Add(HKCategory.Dress, LocString.UndressLeft, new HotKeyCallback(OnUnequipLeft));
            HotKey.Add(HKCategory.Dress, LocString.UndressRight, new HotKeyCallback(OnUnequipRight));
            HotKey.Add(HKCategory.Dress, LocString.UndressHat, new HotKeyCallback(OnUnequipHat));
            HotKey.Add(HKCategory.Dress, LocString.UndressJewels, new HotKeyCallback(OnUnequipJewelry));
        }

        public static void OnUnequipJewelry()
        {
            Dress.Unequip(Layer.Ring);
            Dress.Unequip(Layer.Bracelet);
            Dress.Unequip(Layer.Earrings);
        }

        public static void OnUnequipHat()
        {
            Dress.Unequip(Layer.Head);
        }

        public static void OnUnequipBothHands()
        {
            Dress.Unequip(Layer.RightHand);
            Dress.Unequip(Layer.LeftHand);
        }

        public static void OnUnequipRight()
        {
            Dress.Unequip(Layer.RightHand);
        }

        public static void OnUnequipLeft()
        {
            Dress.Unequip(Layer.LeftHand);
        }
    }
}
