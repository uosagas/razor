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

// UOSagas-Razor: Kleine modale Dialoge (Phase 3c, aus 3b offen).
//
//  * HuePicker  — Ersatz fuer Razor CE UI/HueEntry.cs: numerische Eingabe
//    (0-3000, 0 = aus/Standard) + Vorschau-Farbfeld. Die CE-Hue-Palette
//    braucht hues.mul; die Vorschau hier ist eine dokumentierte GROBE
//    Naeherung (Farbton aus dem Palettenindex, kein mul-Zugriff).
//  * AddCounterDialog — Ersatz fuer Razor CE UI/AddCounter.cs: Name, Format,
//    ItemID, Hue (-1 = alle Farben). Die CE-Standard-Counter kommen weiterhin
//    aus counters.xml (Counter.Load) — der Dialog legt eigene an.

using System;
using System.Globalization;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;

namespace Razor.UI
{
    /// <summary>Generische kleine Dialoge (Phase 4a: Text-Prompt fuer "New Script").</summary>
    public static class Dialogs
    {
        /// <summary>Einzeiliger Text-Prompt; Rueckgabe null = Abbruch.</summary>
        public static async Task<string> Prompt(Window owner, string title, string label, string initial = "")
        {
            Window dlg = Ce.Dialog(title, 300, 130);

            var canvas = Ce.Panel(300, 130);
            Ce.Label(canvas, label, 10, 10, 280, 18);

            TextBox box = Ce.Text(canvas, 10, 32, 275, 23, initial);

            Ce.Button(canvas, "OK", 128, 65, 75, 26, () => dlg.Close(box.Text));
            Ce.Button(canvas, "Cancel", 210, 65, 75, 26, () => dlg.Close(null));

            box.KeyDown += (s, e) =>
            {
                if (e.Key == Key.Enter)
                    dlg.Close(box.Text);
                else if (e.Key == Key.Escape)
                    dlg.Close(null);
            };

            dlg.Content = canvas;
            dlg.Opened += (s, e) => box.Focus();

            return await dlg.ShowDialog<string>(owner);
        }

        /// <summary>Ja/Nein-Sicherheitsabfrage (z. B. vor dem Loeschen);
        /// true nur bei explizitem Klick auf den Bestaetigen-Button.</summary>
        public static async Task<bool> Confirm(Window owner, string title, string message,
            string confirmLabel = "Delete")
        {
            Window dlg = Ce.Dialog(title, 320, 130);

            var canvas = Ce.Panel(320, 130);
            TextBlock text = Ce.Label(canvas, message, 10, 12, 300, 46);
            text.TextWrapping = TextWrapping.Wrap;

            Ce.Button(canvas, confirmLabel, 138, 68, 82, 26, () => dlg.Close(true));
            Ce.Button(canvas, "Cancel", 228, 68, 75, 26, () => dlg.Close(false));

            dlg.KeyDown += (s, e) =>
            {
                if (e.Key == Key.Escape)
                    dlg.Close(false);
            };

            dlg.Content = canvas;
            return await dlg.ShowDialog<bool>(owner);
        }
    }

    public static class HuePicker
    {
        /// <summary>Zeigt den Hue-Dialog; Rueckgabe null = Abbruch, sonst der Hue-Wert.</summary>
        public static async Task<int?> Show(Window owner, string title, int currentHue)
        {
            Window dlg = Ce.Dialog(title, 300, 150);

            var canvas = Ce.Panel(300, 150);
            Ce.Label(canvas, "Hue (0 - 3000, 0 = aus):", 10, 10, 280, 18);

            TextBox box = Ce.Text(canvas, 10, 32, 120, 23, currentHue.ToString());

            var preview = new Border
            {
                BorderBrush = Ce.GroupBorder,
                BorderThickness = new Avalonia.Thickness(1),
                Background = new SolidColorBrush(ApproximateHue(currentHue)),
                Width = 146,
                Height = 23
            };
            Ce.At(canvas, preview, 136, 32, 146, 23);

            box.TextChanged += (s, e) =>
            {
                if (int.TryParse(box.Text?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int h))
                    preview.Background = new SolidColorBrush(ApproximateHue(h));
            };

            int? result = null;

            void Commit()
            {
                if (int.TryParse(box.Text?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int h) &&
                    h >= 0 && h <= 3000)
                {
                    result = h;
                    dlg.Close();
                }
            }

            Ce.Button(canvas, "Okay", 118, 90, 75, 26, Commit);
            Ce.Button(canvas, "Cancel", 207, 90, 75, 26, () => dlg.Close());
            box.KeyDown += (s, e) =>
            {
                if (e.Key == Key.Enter)
                    Commit();
                else if (e.Key == Key.Escape)
                    dlg.Close();
            };

            dlg.Content = canvas;
            dlg.Opened += (s, e) =>
            {
                box.Focus();
                box.SelectAll();
            };

            await dlg.ShowDialog(owner);
            return result;
        }

        /// <summary>
        /// Echte UO-Hue-Farbe aus der Client-Hue-Tabelle (DataService, ABI
        /// GetHueColor). Nur wenn der Service (noch) nicht gebunden ist —
        /// Tests / frueher Start — greift die grobe HSV-Naeherung.
        /// </summary>
        internal static Color ApproximateHue(int hue)
        {
            if (hue <= 0 || hue > 3000)
                return Color.FromRgb(190, 190, 190); // aus/ungefaerbt

            uint argb = Assistant.ClientProxy.GetHueColor(hue);
            if (argb != 0)
                return Color.FromArgb(
                    (byte) (argb >> 24), (byte) (argb >> 16), (byte) (argb >> 8), (byte) argb);

            // Fallback ohne DataService: Palettenindex grob auf den Farbkreis.
            int idx = hue - 2;
            if (idx < 0)
                idx = 0;

            double h = (idx % 100) / 100.0 * 360.0;
            double v = 0.45 + 0.5 * (1.0 - ((idx / 100) % 10) / 10.0);

            return FromHsv(h, 0.85, v);
        }

        private static Color FromHsv(double h, double s, double v)
        {
            double c = v * s;
            double x = c * (1 - Math.Abs(h / 60.0 % 2 - 1));
            double m = v - c;

            double r, g, b;
            if (h < 60) (r, g, b) = (c, x, 0);
            else if (h < 120) (r, g, b) = (x, c, 0);
            else if (h < 180) (r, g, b) = (0, c, x);
            else if (h < 240) (r, g, b) = (0, x, c);
            else if (h < 300) (r, g, b) = (x, 0, c);
            else (r, g, b) = (c, 0, x);

            return Color.FromRgb(
                (byte) Math.Round((r + m) * 255),
                (byte) Math.Round((g + m) * 255),
                (byte) Math.Round((b + m) * 255));
        }
    }

    /// <summary>Ergebnis des AddCounter-Dialogs.</summary>
    public sealed class AddCounterResult
    {
        public string Name;
        public string Format;
        public ushort ItemID;
        public int Hue;
    }

    public static class AddCounterDialog
    {
        /// <summary>Zeigt den Dialog; Rueckgabe null = Abbruch.</summary>
        public static async Task<AddCounterResult> Show(Window owner)
        {
            Window dlg = Ce.Dialog("Add Counter", 300, 210);

            var canvas = Ce.Panel(300, 210);

            Ce.Label(canvas, "Name:", 10, 13, 90, 18);
            TextBox name = Ce.Text(canvas, 105, 10, 177, 23);
            Ce.Label(canvas, "Format (kurz):", 10, 42, 90, 18);
            TextBox fmt = Ce.Text(canvas, 105, 39, 177, 23);
            Ce.Label(canvas, "ItemID (0x.. / dez):", 10, 71, 95, 18);
            TextBox itemId = Ce.Text(canvas, 105, 68, 177, 23);
            Ce.Label(canvas, "Hue (-1 = alle):", 10, 100, 90, 18);
            TextBox hue = Ce.Text(canvas, 105, 97, 177, 23, "-1");

            AddCounterResult result = null;

            void Commit()
            {
                string n = name.Text?.Trim();
                string f = fmt.Text?.Trim();
                if (string.IsNullOrEmpty(n) || string.IsNullOrEmpty(f))
                    return;

                string idText = itemId.Text?.Trim() ?? string.Empty;
                ushort id;
                try
                {
                    id = idText.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
                        ? Convert.ToUInt16(idText.Substring(2), 16)
                        : Convert.ToUInt16(idText, CultureInfo.InvariantCulture);
                }
                catch
                {
                    return;
                }

                if (!int.TryParse(hue.Text?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int h) ||
                    h < -1 || h > 3000)
                    return;

                result = new AddCounterResult { Name = n, Format = f, ItemID = id, Hue = h };
                dlg.Close();
            }

            Ce.Button(canvas, "Okay", 118, 140, 75, 26, Commit);
            Ce.Button(canvas, "Cancel", 207, 140, 75, 26, () => dlg.Close());

            dlg.Content = canvas;
            dlg.Opened += (s, e) => name.Focus();

            await dlg.ShowDialog(owner);
            return result;
        }
    }
}
