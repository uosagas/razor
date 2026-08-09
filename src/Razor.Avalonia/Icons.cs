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

// UOSagas-Razor: Icon-Fabrik fuer Toolbar-Buttons (Play/Stop/Save/...).
//
// Pfad-Geometrien statt Font-Icons: funktioniert cross-platform ohne
// Segoe-MDL2-Abhaengigkeit und skaliert sauber. Verwendet vom VScript-
// Editor, VScripts-Tab, Scripts-Tab und der Script-IDE.

using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Layout;
using Avalonia.Media;

namespace Razor.UI
{
    public static class Icons
    {
        // 16x16-ViewBox-Pfade. "F0" = EvenOdd (innere Formen werden Loecher).
        public const string PlayPath = "M4,2 L13.5,8 L4,14 Z";
        public const string StopPath = "M3.5,3.5 H12.5 V12.5 H3.5 Z";
        public const string PausePath = "M4,3 H7 V13 H4 Z M9,3 H12 V13 H9 Z";
        public const string RecordPath = "M8,3 A5,5 0 1 0 8.001,3 Z";
        public const string SavePath = "F0 M2,2 H11 L14,5 V14 H2 Z M4.5,3 H10 V6 H4.5 Z M4,9 H12 V13.5 H4 Z";
        public const string NewPath = "M7,2 H9 V7 H14 V9 H9 V14 H7 V9 H2 V7 H7 Z";
        public const string DeletePath = "M6,2 H10 V3.5 H14 V5 H2 V3.5 H6 Z M3.5,6.5 H12.5 L11.7,14 H4.3 Z";
        public const string RefreshPath = "M8,2.5 A5.5,5.5 0 1 0 13.5,8 H11.5 A3.5,3.5 0 1 1 8,4.5 V7.5 L12.5,4 L8,0.5 Z";
        public const string EditPath = "M3,13 L3.8,10.4 L10.8,3.4 L12.6,5.2 L5.6,12.2 Z M11.5,2.7 L13.3,4.5 L14.2,3.6 A0.9,0.9 0 0 0 14.2,2.3 L13.7,1.8 A0.9,0.9 0 0 0 12.4,1.8 Z";
        public const string VariablesPath = "M2,4 H5 V5.5 H3.5 V10.5 H5 V12 H2 Z M11,4 H14 V12 H11 V10.5 H12.5 V5.5 H11 Z M5.5,5.5 H7 L8,7.3 L9,5.5 H10.5 L8.9,8 L10.5,10.5 H9 L8,8.7 L7,10.5 H5.5 L7.1,8 Z";

        // Center/Fokus (Fadenkreuz), Undo/Redo sind stroke-basiert.
        public const string CenterPath = "M8,1 V4 M8,12 V15 M1,8 H4 M12,8 H15 M5,8 A3,3 0 1 0 11,8 A3,3 0 1 0 5,8";
        /// <summary>Terminal/Konsole (stroked): Rahmen + Prompt-Chevron + Cursorzeile.</summary>
        public const string ConsolePath = "M2,3.5 H14 V12.5 H2 Z M4.5,6 L7,8 L4.5,10 M8.5,10.5 H11.5";
        public const string UndoPath = "M4,6.5 H10.5 A3.5,3.5 0 0 1 10.5,13.5 H6.5 M4,6.5 L7.5,3 M4,6.5 L7.5,10";
        public const string RedoPath = "M12,6.5 H5.5 A3.5,3.5 0 0 0 5.5,13.5 H9.5 M12,6.5 L8.5,3 M12,6.5 L8.5,10";

        public static readonly IBrush Neutral = new SolidColorBrush(Color.Parse("#DADADA"));
        /// <summary>Fuer die hellen CE-Tabs — Neutral ist dort unsichtbar.</summary>
        public static readonly IBrush Dark = new SolidColorBrush(Color.Parse("#1E1E1E"));
        public static readonly IBrush Green = new SolidColorBrush(Color.Parse("#5FBF60"));
        public static readonly IBrush Red = new SolidColorBrush(Color.Parse("#E05050"));
        public static readonly IBrush Yellow = new SolidColorBrush(Color.Parse("#E6C34A"));

        /// <summary>Erzeugt ein 16x16-Icon aus Pfaddaten (fill; stroked = Linien-Icon).</summary>
        public static Path Make(string data, IBrush brush = null, bool stroked = false)
        {
            brush ??= Neutral;
            var p = new Path
            {
                Data = Geometry.Parse(data),
                Width = 16,
                Height = 16,
                Stretch = Stretch.None,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            if (stroked)
            {
                p.Stroke = brush;
                p.StrokeThickness = 1.6;
                p.StrokeLineCap = PenLineCap.Round;
            }
            else
            {
                p.Fill = brush;
            }

            return p;
        }

        /// <summary>Icon-Button mit Tooltip (Standardgroesse 30x26).</summary>
        public static Button IconButton(string tooltip, string data, IBrush brush, Action onClick,
            double w = 30, double h = 26, bool stroked = false)
        {
            var b = new Button
            {
                Content = Make(data, brush, stroked),
                Width = w,
                Height = h,
                Padding = new Thickness(0),
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center
            };
            ToolTip.SetTip(b, tooltip);

            if (onClick != null)
            {
                b.Click += (s, e) =>
                {
                    try
                    {
                        onClick();
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[UOSagas Razor] UI-Aktion fehlgeschlagen: {ex}");
                    }
                };
            }

            return b;
        }

        /// <summary>Tauscht Icon + Tooltip eines bestehenden Buttons (Play/Stop-Toggle).</summary>
        public static void Swap(Button button, string tooltip, string data, IBrush brush, bool stroked = false)
        {
            button.Content = Make(data, brush, stroked);
            ToolTip.SetTip(button, tooltip);
        }
    }
}
