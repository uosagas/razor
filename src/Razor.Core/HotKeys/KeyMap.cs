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

// UOSagas-Razor: SDL2 <-> WinForms-Keys-Mapping (Phase 3c).
//
// Der Client (GameController) reicht SDL-Keycodes (keysym.sym) und die
// SDL-KMOD-Bitmaske (keysym.mod) an RazorPlugin.OnHotkey durch. Das
// HotKey-System arbeitet intern aber — CE-treu und profilkompatibel — mit
// WinForms-Keys-Werten und der ModKeys-Bitmaske (Alt=1, Ctrl=2, Shift=4).
// Diese Tabelle uebersetzt am Rand: SDL rein, Keys/ModKeys raus.

namespace Assistant
{
    public static class KeyMap
    {
        // SDL2-Keycode-Konstanten (SDL_keycode.h).
        private const int SDLK_SCANCODE_MASK = 1 << 30; // 0x40000000

        // SDL2 KMOD-Bitmaske (SDL_keycode.h).
        private const int KMOD_LSHIFT = 0x0001;
        private const int KMOD_RSHIFT = 0x0002;
        private const int KMOD_LCTRL = 0x0040;
        private const int KMOD_RCTRL = 0x0080;
        private const int KMOD_LALT = 0x0100;
        private const int KMOD_RALT = 0x0200;

        /// <summary>SDL-KMOD-Bitmaske -&gt; Razor-ModKeys (Alt/Control/Shift).</summary>
        public static ModKeys ToModKeys(int sdlMod)
        {
            ModKeys mod = ModKeys.None;

            if ((sdlMod & (KMOD_LCTRL | KMOD_RCTRL)) != 0)
                mod |= ModKeys.Control;
            if ((sdlMod & (KMOD_LALT | KMOD_RALT)) != 0)
                mod |= ModKeys.Alt;
            if ((sdlMod & (KMOD_LSHIFT | KMOD_RSHIFT)) != 0)
                mod |= ModKeys.Shift;

            return mod;
        }

        /// <summary>
        /// SDL-Keycode (keysym.sym) -&gt; WinForms-Keys-Wert.
        /// Liefert Keys.None fuer Tasten ohne WinForms-Entsprechung.
        /// </summary>
        public static Keys ToKeys(int sdlKey)
        {
            // Buchstaben: SDL nutzt Kleinbuchstaben-ASCII (97-122).
            if (sdlKey >= 'a' && sdlKey <= 'z')
                return (Keys) (sdlKey - 'a' + (int) Keys.A);

            // Ziffern der oberen Reihe: identische Werte (48-57).
            if (sdlKey >= '0' && sdlKey <= '9')
                return (Keys) sdlKey;

            // ASCII-Sondertasten.
            switch (sdlKey)
            {
                case 8: return Keys.Back;
                case 9: return Keys.Tab;
                case 13: return Keys.Return;
                case 27: return Keys.Escape;
                case 32: return Keys.Space;
                case 127: return Keys.Delete;

                case '\'': return Keys.OemQuotes;
                case ',': return Keys.Oemcomma;
                case '-': return Keys.OemMinus;
                case '.': return Keys.OemPeriod;
                case '/': return Keys.OemQuestion;
                case ';': return Keys.OemSemicolon;
                case '=': return Keys.Oemplus;
                case '[': return Keys.OemOpenBrackets;
                case '\\': return Keys.OemPipe;
                case ']': return Keys.OemCloseBrackets;
                case '`': return Keys.Oemtilde;
            }

            // Scancode-basierte Keycodes (Bit 30 gesetzt, SDL_SCANCODE_TO_KEYCODE).
            if ((sdlKey & SDLK_SCANCODE_MASK) != 0)
            {
                int sc = sdlKey & ~SDLK_SCANCODE_MASK;

                // F1-F12: Scancodes 58-69.
                if (sc >= 58 && sc <= 69)
                    return (Keys) (sc - 58 + (int) Keys.F1);

                // F13-F24: Scancodes 104-115.
                if (sc >= 104 && sc <= 115)
                    return (Keys) (sc - 104 + (int) Keys.F13);

                // Numpad 1-9: Scancodes 89-97 (Keys.NumPad1-NumPad9).
                if (sc >= 89 && sc <= 97)
                    return (Keys) (sc - 89 + (int) Keys.NumPad1);

                switch (sc)
                {
                    case 57: return Keys.CapsLock;
                    case 70: return Keys.Snapshot; // PrintScreen
                    case 71: return Keys.Scroll;
                    case 72: return Keys.Pause;
                    case 73: return Keys.Insert;
                    case 74: return Keys.Home;
                    case 75: return Keys.PageUp;
                    case 77: return Keys.End;
                    case 78: return Keys.PageDown;
                    case 79: return Keys.Right;
                    case 80: return Keys.Left;
                    case 81: return Keys.Down;
                    case 82: return Keys.Up;
                    case 83: return Keys.NumLock;
                    case 84: return Keys.Divide; // KP_DIVIDE
                    case 85: return Keys.Multiply; // KP_MULTIPLY
                    case 86: return Keys.Subtract; // KP_MINUS
                    case 87: return Keys.Add; // KP_PLUS
                    case 88: return Keys.Return; // KP_ENTER
                    case 98: return Keys.NumPad0; // KP_0
                    case 99: return Keys.Decimal; // KP_PERIOD
                    case 101: return Keys.Apps; // APPLICATION

                    // Reine Modifier-Tasten -> generische WinForms-Codes
                    // (wie WM_KEYDOWN VK_SHIFT/VK_CONTROL/VK_MENU in CE).
                    case 224: return Keys.ControlKey; // LCTRL
                    case 225: return Keys.ShiftKey; // LSHIFT
                    case 226: return Keys.Menu; // LALT
                    case 227: return Keys.LWin; // LGUI
                    case 228: return Keys.ControlKey; // RCTRL
                    case 229: return Keys.ShiftKey; // RSHIFT
                    case 230: return Keys.Menu; // RALT
                    case 231: return Keys.RWin; // RGUI
                }
            }

            return Keys.None;
        }
    }
}
