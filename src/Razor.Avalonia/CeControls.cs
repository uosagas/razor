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

// UOSagas-Razor: WinForms-artige Control-Fabrik fuer den Razor-CE-Look (Phase 3b).
//
// Razor CE (Razor/UI/Razor.Designer.cs) platziert alle Controls mit absoluten
// Koordinaten. Um "genauso wie Razor" auszusehen, bauen wir jede TabPage als
// Canvas nach und uebernehmen Location/Size 1:1 aus der Designer-Datei.
//
// Ausserdem hier: PropBinder (Checkbox/TextBox/Combo/Slider <-> Profile-Property,
// Threading-Regeln: lesen aus UiSnapshot, schreiben via GameThread.Post) und
// eine kleine InputBox (Ersatz fuer Razor CE UI/InputBox.cs).

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Assistant;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;

namespace Razor.UI
{
    /// <summary>Fabrik fuer Controls in Razor-CE-Optik mit Designer-Koordinaten.</summary>
    public static class Ce
    {
        public const double FontSize = 12; // Segoe UI 9pt (WinForms) ~ 12px

        public static readonly IBrush WindowBackground = new SolidColorBrush(Color.Parse("#F0F0F0"));
        public static readonly IBrush GroupBorder = new SolidColorBrush(Color.Parse("#B5B5B5"));
        public static readonly IBrush GrayText = new SolidColorBrush(Color.Parse("#6D6D6D"));

        /// <summary>
        /// Vorkonfiguriertes modales Dialog-Fenster. Topmost ist Pflicht: Owner-
        /// Fenster (MainWindow mit "Always on top", VScript-Editor) koennen sich
        /// bei Aktivierungs-Races VOR den modalen Dialog schieben — der Dialog
        /// blockiert dann unsichtbar alle Eingaben. Topmost + Activate beim
        /// Oeffnen halten ihn garantiert ueber dem Owner.
        /// </summary>
        public static Window Dialog(string title, double w, double h,
            bool canResize = false, IBrush background = null)
        {
            var dlg = new Window
            {
                Title = title,
                Width = w,
                Height = h,
                CanResize = canResize,
                Background = background ?? WindowBackground,
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = FontSize,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                ShowInTaskbar = false,
                Topmost = true
            };
            Branding.ApplyTo(dlg);
            dlg.Opened += (s, e) => dlg.Activate();
            return dlg;
        }

        public static Canvas Panel(double w = double.NaN, double h = double.NaN)
        {
            var c = new Canvas();
            if (!double.IsNaN(w)) c.Width = w;
            if (!double.IsNaN(h)) c.Height = h;
            return c;
        }

        /// <summary>Control mit WinForms-Koordinaten (Location/Size) platzieren.</summary>
        public static T At<T>(Canvas parent, T control, double x, double y,
            double w = double.NaN, double h = double.NaN) where T : Control
        {
            Canvas.SetLeft(control, x);
            Canvas.SetTop(control, y);
            if (!double.IsNaN(w)) control.Width = w;
            if (!double.IsNaN(h)) control.Height = h;
            parent.Children.Add(control);
            return control;
        }

        public static Button Button(Canvas p, string text, double x, double y, double w, double h,
            Action onClick = null)
        {
            var b = new Button { Content = text };
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
                        // Crash-Reporter statt stillem Konsolen-Log (Dedupe 60s).
                        Assistant.CrashReporter.Report(ex, "UI action");
                    }
                };
            }
            else
            {
                // Platzhalter: deaktiviert statt tot wirkend, mit Hinweis-Tooltip,
                // damit keine Schaltflaeche Funktion vortaeuscht, die es nicht gibt.
                b.IsEnabled = false;
                ToolTip.SetTip(b, "noch nicht verfügbar");
            }

            return At(p, b, x, y, w, h);
        }

        // Check/Radio: die WinForms-Designer-Hoehen (oft 16 px) sind knapper als
        // Avalonias Glyph samt Rahmen — bei fester Hoehe wird das Kaestchen oben
        // und unten angeschnitten. Deshalb: mindestens 20 px hoch, dafuer um die
        // halbe Differenz nach oben versetzt, damit die optische Mitte exakt auf
        // der Designer-Position bleibt (Canvas clippt nicht, nichts ueberlappt).
        private const double CheckMinHeight = 20;

        public static CheckBox Check(Canvas p, string text, double x, double y, double w, double h)
        {
            var cb = new CheckBox { Content = text, VerticalAlignment = VerticalAlignment.Center };
            if (h < CheckMinHeight)
            {
                y -= (CheckMinHeight - h) / 2;
                h = CheckMinHeight;
            }

            return At(p, cb, x, y, w, h);
        }

        public static RadioButton Radio(Canvas p, string text, string group, double x, double y, double w, double h)
        {
            var rb = new RadioButton { Content = text, GroupName = group, VerticalAlignment = VerticalAlignment.Center };
            if (h < CheckMinHeight)
            {
                y -= (CheckMinHeight - h) / 2;
                h = CheckMinHeight;
            }

            return At(p, rb, x, y, w, h);
        }

        public static TextBlock Label(Canvas p, string text, double x, double y,
            double w = double.NaN, double h = double.NaN)
        {
            var t = new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap };
            return At(p, t, x, y, w, h);
        }

        public static TextBox Text(Canvas p, double x, double y, double w, double h, string text = "")
        {
            var t = new TextBox { Text = text };
            return At(p, t, x, y, w, h);
        }

        public static ComboBox Combo(Canvas p, double x, double y, double w, double h, params string[] items)
        {
            var c = new ComboBox();
            foreach (string i in items)
                c.Items.Add(i);
            return At(p, c, x, y, w, h);
        }

        public static ListBox List(Canvas p, double x, double y, double w, double h)
        {
            return At(p, new ListBox(), x, y, w, h);
        }

        /// <summary>
        /// WinForms-GroupBox-Nachbau: Rahmen mit Beschriftung, die den Rahmen
        /// oben ueberlappt. Rueckgabe: Canvas, in den die Kinder mit den
        /// ORIGINAL-Designer-Koordinaten (relativ zur GroupBox) gelegt werden.
        /// </summary>
        public static Canvas Group(Canvas p, string header, double x, double y, double w, double h)
        {
            return Group(p, header, x, y, w, h, out _);
        }

        /// <summary>Wie Group, liefert zusaetzlich die Beschriftung (z. B. Agents-GroupBox).</summary>
        public static Canvas Group(Canvas p, string header, double x, double y, double w, double h,
            out TextBlock captionOut)
        {
            var host = Panel(w, h);
            Canvas.SetLeft(host, x);
            Canvas.SetTop(host, y);
            p.Children.Add(host);

            var border = new Border
            {
                BorderBrush = GroupBorder,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(0),
                Width = w,
                Height = h - 7
            };
            Canvas.SetLeft(border, 0);
            Canvas.SetTop(border, 7);
            host.Children.Add(border);

            var caption = new TextBlock
            {
                Text = header,
                Background = WindowBackground,
                Padding = new Thickness(3, 0)
            };
            Canvas.SetLeft(caption, 7);
            Canvas.SetTop(caption, 0);
            host.Children.Add(caption);

            captionOut = caption;
            return host;
        }

        /// <summary>Verschachtelter Sub-TabControl wie Razor CE (z. B. Options/Display).</summary>
        public static TabControl SubTabs(Canvas p, double x, double y, double w, double h)
        {
            var tabs = new TabControl();
            return At(p, tabs, x, y, w, h);
        }

        public static TabItem Tab(TabControl tabs, string header, Control content)
        {
            var item = new TabItem { Header = header, Content = content };
            tabs.Items.Add(item);
            return item;
        }
    }

    /// <summary>
    /// Bindet Controls an Profile-Properties (Namen wie Razor CE).
    /// Schreiben: GameThread.Post -> Config.SetProperty (typerhaltend).
    /// Lesen: Apply(UiSnapshot); fehlt eine Property im Snapshot, wird das
    /// Control deaktiviert (Property existiert im Core-Profil nicht).
    /// </summary>
    public sealed class PropBinder
    {
        private readonly List<(string Prop, CheckBox Box)> _bools = new List<(string, CheckBox)>();
        private readonly List<(string Prop, TextBox Box)> _texts = new List<(string, TextBox)>();
        private readonly List<(string Prop, ComboBox Box)> _combos = new List<(string, ComboBox)>();
        private readonly List<(string Prop, Slider Bar, bool Invert)> _sliders = new List<(string, Slider, bool)>();

        private bool _applying;

        /// <summary>true, waehrend Apply() Werte in Controls schreibt.</summary>
        public bool Applying => _applying;

        public CheckBox Check(CheckBox cb, string prop, Action<bool> uiSideEffect = null)
        {
            _bools.Add((prop, cb));
            cb.IsCheckedChanged += (s, e) =>
            {
                if (_applying)
                    return;

                bool val = cb.IsChecked == true;
                uiSideEffect?.Invoke(val);
                GameThread.Post(() => SetScalar(prop, val));
            };
            return cb;
        }

        public TextBox Text(TextBox tb, string prop)
        {
            _texts.Add((prop, tb));
            tb.LostFocus += (s, e) => CommitText(tb, prop);
            tb.KeyDown += (s, e) =>
            {
                if (e.Key == Key.Enter)
                    CommitText(tb, prop);
            };
            return tb;
        }

        public ComboBox Combo(ComboBox combo, string prop)
        {
            _combos.Add((prop, combo));
            combo.SelectionChanged += (s, e) =>
            {
                if (_applying || combo.SelectedIndex < 0)
                    return;

                int idx = combo.SelectedIndex;
                GameThread.Post(() => SetScalar(prop, idx));
            };
            return combo;
        }

        /// <summary>invert: Wert = Maximum - Property (Razor CE lightLevelBar).</summary>
        public Slider Slider(Slider bar, string prop, bool invert = false, Action<int> uiSideEffect = null)
        {
            _sliders.Add((prop, bar, invert));
            bar.ValueChanged += (s, e) =>
            {
                if (_applying)
                    return;

                int val = (int) Math.Round(bar.Value);
                int stored = invert ? (int) bar.Maximum - val : val;
                uiSideEffect?.Invoke(stored);
                GameThread.Post(() => SetScalar(prop, stored));
            };
            return bar;
        }

        private void CommitText(TextBox tb, string prop)
        {
            if (_applying)
                return;

            string text = tb.Text ?? string.Empty;
            GameThread.Post(() => SetScalarFromString(prop, text));
        }

        /// <summary>NUR Game-Thread. Setzt eine Property typerhaltend aus einem String.</summary>
        public static void SetScalarFromString(string prop, string text)
        {
            object cur;
            try
            {
                cur = Config.GetProperty(prop);
            }
            catch
            {
                return; // Property existiert nicht
            }

            try
            {
                if (cur is int)
                {
                    if (int.TryParse(text.Trim(), out int i))
                        Config.SetProperty(prop, i);
                }
                else if (cur is double)
                {
                    if (double.TryParse(text.Trim(), out double d))
                        Config.SetProperty(prop, d);
                }
                else
                {
                    Config.SetProperty(prop, text);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[UOSagas Razor] SetProperty({prop}) fehlgeschlagen: {ex.Message}");
            }
        }

        /// <summary>NUR Game-Thread.</summary>
        public static void SetScalar(string prop, object val)
        {
            try
            {
                Config.SetProperty(prop, val);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[UOSagas Razor] SetProperty({prop}) fehlgeschlagen: {ex.Message}");
            }
        }

        /// <summary>Angefragte Properties in den Snapshot-Request eintragen.</summary>
        public void Contribute(UiRequest req)
        {
            foreach ((string prop, _) in _bools)
                req.BoolProps.Add(prop);
            foreach ((string prop, _) in _texts)
                req.TextProps.Add(prop);
            foreach ((string prop, _) in _combos)
                req.TextProps.Add(prop);
            foreach ((string prop, _, _) in _sliders)
                req.TextProps.Add(prop);
        }

        /// <summary>Snapshot-Werte in die Controls schreiben (UI-Thread only).</summary>
        public void Apply(UiSnapshot snap)
        {
            _applying = true;
            try
            {
                foreach ((string prop, CheckBox cb) in _bools)
                {
                    if (snap.BoolProps.TryGetValue(prop, out bool val))
                    {
                        if (cb.IsChecked != val)
                            cb.IsChecked = val;
                    }
                    else
                    {
                        cb.IsEnabled = false; // Property im Core nicht vorhanden
                    }
                }

                foreach ((string prop, TextBox tb) in _texts)
                {
                    if (snap.TextProps.TryGetValue(prop, out string val))
                    {
                        // Nicht unter dem Cursor wegtippen: nur setzen, wenn
                        // die Box gerade keinen Fokus hat.
                        if (!tb.IsFocused && !string.Equals(tb.Text, val, StringComparison.Ordinal))
                            tb.Text = val;
                    }
                    else
                    {
                        tb.IsEnabled = false;
                    }
                }

                foreach ((string prop, ComboBox combo) in _combos)
                {
                    if (snap.TextProps.TryGetValue(prop, out string val) &&
                        int.TryParse(val, out int idx))
                    {
                        if (idx >= 0 && idx < combo.Items.Count && combo.SelectedIndex != idx &&
                            !combo.IsDropDownOpen)
                            combo.SelectedIndex = idx;
                    }
                    else
                    {
                        combo.IsEnabled = false;
                    }
                }

                foreach ((string prop, Slider bar, bool invert) in _sliders)
                {
                    if (snap.TextProps.TryGetValue(prop, out string val) &&
                        int.TryParse(val, out int stored))
                    {
                        int shown = invert ? (int) bar.Maximum - stored : stored;
                        if ((int) Math.Round(bar.Value) != shown)
                            bar.Value = shown;
                    }
                    else
                    {
                        bar.IsEnabled = false;
                    }
                }
            }
            finally
            {
                _applying = false;
            }
        }
    }

    /// <summary>Kleiner modaler Namens-Dialog (Ersatz fuer Razor CE UI/InputBox.cs).</summary>
    public static class InputBox
    {
        public static async Task<string> Show(Window owner, string prompt, string title)
        {
            Window dlg = Ce.Dialog(title, 300, 120);

            var canvas = Ce.Panel(300, 120);
            Ce.Label(canvas, prompt, 10, 10, 280, 18);
            TextBox box = Ce.Text(canvas, 10, 32, 272, 23);

            Ce.Button(canvas, "Okay", 118, 62, 75, 26, () => dlg.Close(box.Text?.Trim()));
            Ce.Button(canvas, "Cancel", 207, 62, 75, 26, () => dlg.Close(null));
            box.KeyDown += (s, e) =>
            {
                if (e.Key == Key.Enter)
                    dlg.Close(box.Text?.Trim());
                else if (e.Key == Key.Escape)
                    dlg.Close(null);
            };

            dlg.Content = canvas;
            dlg.Opened += (s, e) => box.Focus();

            string result = await dlg.ShowDialog<string>(owner);
            return string.IsNullOrEmpty(result) ? null : result;
        }
    }
}
