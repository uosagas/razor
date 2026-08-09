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

// UOSagas-Razor: Avalonia.Input.Key -> WinForms-Keys-Mapping (Phase 3c).
//
// Die Key-Erfassung im Hot Keys-Tab laeuft ueber Avalonia-KeyDown-Events;
// das HotKey-System (Razor.Core) arbeitet mit WinForms-Keys-Werten
// (CE-Profilformat). Gegenstueck zum SDL-Mapping in Razor.Core/HotKeys/KeyMap.cs.

using Avalonia.Input;
using WfKeys = Assistant.Keys;

namespace Razor.UI
{
    public static class AvaloniaKeyMap
    {
        /// <summary>Liefert Keys.None fuer Tasten ohne WinForms-Entsprechung.</summary>
        public static WfKeys ToKeys(Key key)
        {
            if (key >= Key.A && key <= Key.Z)
                return (WfKeys) (key - Key.A + WfKeys.A);

            if (key >= Key.D0 && key <= Key.D9)
                return (WfKeys) (key - Key.D0 + WfKeys.D0);

            if (key >= Key.NumPad0 && key <= Key.NumPad9)
                return (WfKeys) (key - Key.NumPad0 + WfKeys.NumPad0);

            if (key >= Key.F1 && key <= Key.F24)
                return (WfKeys) (key - Key.F1 + WfKeys.F1);

            switch (key)
            {
                case Key.Back: return WfKeys.Back;
                case Key.Tab: return WfKeys.Tab;
                case Key.Clear: return WfKeys.Clear;
                case Key.Return: return WfKeys.Return;
                case Key.Pause: return WfKeys.Pause;
                case Key.CapsLock: return WfKeys.CapsLock;
                case Key.Escape: return WfKeys.Escape;
                case Key.Space: return WfKeys.Space;
                case Key.PageUp: return WfKeys.PageUp;
                case Key.PageDown: return WfKeys.PageDown;
                case Key.End: return WfKeys.End;
                case Key.Home: return WfKeys.Home;
                case Key.Left: return WfKeys.Left;
                case Key.Up: return WfKeys.Up;
                case Key.Right: return WfKeys.Right;
                case Key.Down: return WfKeys.Down;
                case Key.Snapshot: return WfKeys.Snapshot;
                case Key.Insert: return WfKeys.Insert;
                case Key.Delete: return WfKeys.Delete;

                case Key.LWin: return WfKeys.LWin;
                case Key.RWin: return WfKeys.RWin;
                case Key.Apps: return WfKeys.Apps;

                case Key.Multiply: return WfKeys.Multiply;
                case Key.Add: return WfKeys.Add;
                case Key.Separator: return WfKeys.Separator;
                case Key.Subtract: return WfKeys.Subtract;
                case Key.Decimal: return WfKeys.Decimal;
                case Key.Divide: return WfKeys.Divide;

                case Key.NumLock: return WfKeys.NumLock;
                case Key.Scroll: return WfKeys.Scroll;

                // Reine Modifier -> generische WinForms-Codes (wie SDL-Mapping).
                case Key.LeftShift:
                case Key.RightShift: return WfKeys.ShiftKey;
                case Key.LeftCtrl:
                case Key.RightCtrl: return WfKeys.ControlKey;
                case Key.LeftAlt:
                case Key.RightAlt: return WfKeys.Menu;

                case Key.OemSemicolon: return WfKeys.OemSemicolon;
                case Key.OemPlus: return WfKeys.Oemplus;
                case Key.OemComma: return WfKeys.Oemcomma;
                case Key.OemMinus: return WfKeys.OemMinus;
                case Key.OemPeriod: return WfKeys.OemPeriod;
                case Key.OemQuestion: return WfKeys.OemQuestion;
                case Key.OemTilde: return WfKeys.Oemtilde;
                case Key.OemOpenBrackets: return WfKeys.OemOpenBrackets;
                case Key.OemPipe: return WfKeys.OemPipe;
                case Key.OemCloseBrackets: return WfKeys.OemCloseBrackets;
                case Key.OemQuotes: return WfKeys.OemQuotes;
                case Key.Oem8: return WfKeys.Oem8;
                case Key.OemBackslash: return WfKeys.OemBackslash;

                default: return WfKeys.None;
            }
        }
    }
}
