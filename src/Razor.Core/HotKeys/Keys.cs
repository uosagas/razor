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

// UOSagas-Razor: Teilmenge des System.Windows.Forms.Keys-Enums (Phase 3c).
//
// Razor CE speichert Hotkey-Belegungen als WinForms-Keys-Integer in der
// hotkeys-Profilsektion (key="112" = F1). Der Port laeuft ohne WinForms,
// braucht aber DIESELBEN numerischen Werte (Profil-Kompatibilitaet) und
// dieselben Namen fuer die Anzeige (((Keys)key).ToString() wie in CE).
//
// Werte 1:1 aus System.Windows.Forms.Keys (ohne Modifier-Flags Shift/Control/
// Alt 0x1xxxx — CE speichert Modifier separat als ModKeys-Attribut "mod").

namespace Assistant
{
    public enum Keys : int
    {
        None = 0,

        Back = 8,
        Tab = 9,
        Clear = 12,
        Return = 13,

        ShiftKey = 16,
        ControlKey = 17,
        Menu = 18, // Alt
        Pause = 19,
        CapsLock = 20,

        Escape = 27,
        Space = 32,
        PageUp = 33,
        PageDown = 34,
        End = 35,
        Home = 36,
        Left = 37,
        Up = 38,
        Right = 39,
        Down = 40,
        Snapshot = 44, // Druck/PrintScreen
        Insert = 45,
        Delete = 46,

        D0 = 48,
        D1 = 49,
        D2 = 50,
        D3 = 51,
        D4 = 52,
        D5 = 53,
        D6 = 54,
        D7 = 55,
        D8 = 56,
        D9 = 57,

        A = 65,
        B = 66,
        C = 67,
        D = 68,
        E = 69,
        F = 70,
        G = 71,
        H = 72,
        I = 73,
        J = 74,
        K = 75,
        L = 76,
        M = 77,
        N = 78,
        O = 79,
        P = 80,
        Q = 81,
        R = 82,
        S = 83,
        T = 84,
        U = 85,
        V = 86,
        W = 87,
        X = 88,
        Y = 89,
        Z = 90,

        LWin = 91,
        RWin = 92,
        Apps = 93,

        NumPad0 = 96,
        NumPad1 = 97,
        NumPad2 = 98,
        NumPad3 = 99,
        NumPad4 = 100,
        NumPad5 = 101,
        NumPad6 = 102,
        NumPad7 = 103,
        NumPad8 = 104,
        NumPad9 = 105,
        Multiply = 106,
        Add = 107,
        Separator = 108,
        Subtract = 109,
        Decimal = 110,
        Divide = 111,

        F1 = 112,
        F2 = 113,
        F3 = 114,
        F4 = 115,
        F5 = 116,
        F6 = 117,
        F7 = 118,
        F8 = 119,
        F9 = 120,
        F10 = 121,
        F11 = 122,
        F12 = 123,
        F13 = 124,
        F14 = 125,
        F15 = 126,
        F16 = 127,
        F17 = 128,
        F18 = 129,
        F19 = 130,
        F20 = 131,
        F21 = 132,
        F22 = 133,
        F23 = 134,
        F24 = 135,

        NumLock = 144,
        Scroll = 145,

        LShiftKey = 160,
        RShiftKey = 161,
        LControlKey = 162,
        RControlKey = 163,
        LMenu = 164,
        RMenu = 165,

        OemSemicolon = 186, // ;
        Oemplus = 187, // =
        Oemcomma = 188, // ,
        OemMinus = 189, // -
        OemPeriod = 190, // .
        OemQuestion = 191, // /
        Oemtilde = 192, // `

        OemOpenBrackets = 219, // [
        OemPipe = 220, // \
        OemCloseBrackets = 221, // ]
        OemQuotes = 222, // '
        Oem8 = 223,
        OemBackslash = 226 // <> auf 102er-Tastaturen
    }
}
