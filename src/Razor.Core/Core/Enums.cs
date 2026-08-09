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

// Portierte Enums aus Razor CE:
//   Layer       -> Razor/Core/Item.cs
//   Direction   -> Razor/Core/Mobile.cs
//   MessageType -> Razor/Network/Packets.cs
//   AOSAbility  -> Razor/HotKeys/SpecialMoves.cs
// GumpTextEntry -> Razor/Network/Packets.cs

namespace Assistant
{
    public enum Layer : byte
    {
        Invalid = 0x00,

        FirstValid = 0x01,

        RightHand = 0x01,
        LeftHand = 0x02,
        Shoes = 0x03,
        Pants = 0x04,
        Shirt = 0x05,
        Head = 0x06,
        Gloves = 0x07,
        Ring = 0x08,
        Talisman = 0x09,
        Neck = 0x0A,
        Hair = 0x0B,
        Waist = 0x0C,
        InnerTorso = 0x0D,
        Bracelet = 0x0E,
        Face = 0x0F,
        FacialHair = 0x10,
        MiddleTorso = 0x11,
        Earrings = 0x12,
        Arms = 0x13,
        Cloak = 0x14,
        Backpack = 0x15,
        OuterTorso = 0x16,
        OuterLegs = 0x17,
        InnerLegs = 0x18,

        LastUserValid = 0x18,

        Mount = 0x19,
        ShopBuy = 0x1A,
        ShopResale = 0x1B,
        ShopSell = 0x1C,
        Bank = 0x1D,

        LastValid = 0x1D
    }

    public enum Direction : byte
    {
        North = 0x0,
        Right = 0x1,
        East = 0x2,
        Down = 0x3,
        South = 0x4,
        Left = 0x5,
        West = 0x6,
        Up = 0x7,
        Mask = 0x7,
        Running = 0x80,
        ValueMask = 0x87
    }

    public enum MessageType
    {
        Regular = 0x00,
        System = 0x01,
        Emote = 0x02,
        Label = 0x06,
        Focus = 0x07,
        Whisper = 0x08,
        Yell = 0x09,
        Spell = 0x0A,
        Guild = 0x0D,
        Alliance = 0x0E,
        Encoded = 0xC0,

        Special = 0x20
    }

    public enum AOSAbility
    {
        Clear,
        ArmorIgnore,
        BleedAttack,
        ConcussionBlow,
        CrushingBlow,
        Disarm,
        Dismount,
        DoubleStrike,
        InfectiousStrike,
        MortalStrike,
        MovingShot,
        ParalyzingBlow,
        ShadowStrike,
        WhirlwindAttack,
        Invalid
    }

    public sealed class GumpTextEntry
    {
        public GumpTextEntry(ushort id, string s)
        {
            EntryID = id;
            Text = s;
        }

        public ushort EntryID;
        public string Text;
    }
}
