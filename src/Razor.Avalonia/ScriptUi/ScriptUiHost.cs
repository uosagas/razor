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

// UOSagas-Razor: Avalonia-Darstellung der Lua-Script-UI (Phase 4b).
//
// Zeichnet die UiWindow-Modelle (Razor.Core/Lua/UI/ScriptUiModel.cs) als
// echte Fenster: vertikales Auto-Layout (StackPanel), Rows horizontal,
// SizeToContent wenn das Script keine Groesse setzt. 100ms-Poll-Sync
// (Muster UiSnapshot-Pump): Modell -> Ansicht; Interaktionen laufen ueber
// die *FromUi-Methoden in die UIEventQueue, die der Script-Task waehrend
// Pause()/win:Run() in die Lua-Callbacks pumpt.

using System;
using System.Collections.Generic;
using System.Linq;
using Assistant.LuaEngine.UI;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;

namespace Razor.UI.ScriptUi
{
    public static class ScriptUiHost
    {
        private static readonly Dictionary<UiWindow, ScriptUiWindow> _views = new();
        private static bool _started;

        /// <summary>Auf dem UI-Thread aufrufen (RazorApp nach Framework-Init).</summary>
        public static void Start()
        {
            if (_started)
                return;

            _started = true;
            var timer = new DispatcherTimer(TimeSpan.FromMilliseconds(100),
                DispatcherPriority.Background, (s, e) => Tick());
            timer.Start();
        }

        private static void Tick()
        {
            try
            {
                UiWindow[] models = ScriptUIManager.GetWindowsSnapshot();

                // Zerstoerte Fenster (Script-Ende/UI.DestroyAll) wirklich schliessen.
                foreach (var pair in _views.Where(p => !models.Contains(p.Key)).ToList())
                {
                    _views.Remove(pair.Key);
                    pair.Value.ForceClose();
                }

                foreach (UiWindow model in models)
                {
                    if (!_views.TryGetValue(model, out ScriptUiWindow view))
                    {
                        view = new ScriptUiWindow(model);
                        _views[model] = view;
                    }

                    view.Sync();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[UOSagas Razor] Script-UI-Sync fehlgeschlagen: {ex}");
            }
        }
    }

    /// <summary>Avalonia-Fenster fuer ein UiWindow-Modell.</summary>
    internal sealed class ScriptUiWindow : Window
    {
        private static readonly IBrush DarkBackground = new SolidColorBrush(Color.FromRgb(0x25, 0x25, 0x26));
        private static readonly IBrush LightText = new SolidColorBrush(Color.FromRgb(0xDC, 0xDC, 0xDC));
        private static readonly IBrush SeparatorBrush = new SolidColorBrush(Color.FromRgb(0x4A, 0x4A, 0x4A));

        private readonly UiWindow _model;
        private readonly StackPanel _stack;
        private readonly Dictionary<string, Control> _views = new();
        private int _lastVersion = -1;
        private bool _allowClose;
        private bool _syncing;

        public ScriptUiWindow(UiWindow model)
        {
            _model = model;

            Title = model.Title;
            CanResize = true;
            ShowInTaskbar = false;
            Branding.ApplyTo(this);
            Background = DarkBackground;
            Foreground = LightText;

            ApplyBounds();

            _stack = new StackPanel
            {
                Orientation = Orientation.Vertical,
                Spacing = 6,
                Margin = new Thickness(10)
            };

            Content = new ScrollViewer { Content = _stack };

            // X-Button: Modell schliessen (beendet win:Run, feuert OnClose) —
            // das Fenster selbst raeumt der Host-Tick ab bzw. versteckt es.
            Closing += (s, e) =>
            {
                if (_allowClose)
                    return;

                e.Cancel = true;
                _model.NotifyClosedFromUi();
                Hide();
            };
        }

        public void ForceClose()
        {
            _allowClose = true;
            Close();
        }

        private void ApplyBounds()
        {
            if (_model.WindowWidth > 0)
                Width = _model.WindowWidth;
            if (_model.WindowHeight > 0)
                Height = _model.WindowHeight;

            if (_model.WindowWidth <= 0 && _model.WindowHeight <= 0)
                SizeToContent = SizeToContent.WidthAndHeight;
            else if (_model.WindowWidth <= 0)
                SizeToContent = SizeToContent.Width;
            else if (_model.WindowHeight <= 0)
                SizeToContent = SizeToContent.Height;
            else
                SizeToContent = SizeToContent.Manual;

            if (_model.X >= 0 && _model.Y >= 0)
                Position = new PixelPoint((int) _model.X, (int) _model.Y);
        }

        public void Sync()
        {
            _syncing = true;
            try
            {
                if (_model.IsVisible && !IsVisible)
                    Show();
                else if (!_model.IsVisible && IsVisible)
                    Hide();

                if (Title != _model.Title)
                    Title = _model.Title;

                if (_model.TakeBoundsDirty())
                    ApplyBounds();

                if (_model.Version != _lastVersion)
                    Rebuild();
                else
                    SyncElements(_model.GetElementsSnapshot());
            }
            finally
            {
                _syncing = false;
            }
        }

        // ---- Aufbau ------------------------------------------------------

        private void Rebuild()
        {
            _lastVersion = _model.Version;
            _stack.Children.Clear();
            _views.Clear();

            foreach (UiElement element in _model.GetElementsSnapshot())
            {
                Control view = CreateView(element);
                if (view != null)
                    _stack.Children.Add(view);
            }

            SyncElements(_model.GetElementsSnapshot());
        }

        private Control CreateView(UiElement element)
        {
            Control view;

            switch (element)
            {
                case UiRow row:
                {
                    var panel = new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        Spacing = 6
                    };

                    foreach (UiElement child in row.GetChildrenSnapshot())
                    {
                        Control childView = CreateView(child);
                        if (childView != null)
                            panel.Children.Add(childView);
                    }

                    view = panel;
                    break;
                }

                case UiLabel:
                    view = new TextBlock
                    {
                        Foreground = LightText,
                        VerticalAlignment = VerticalAlignment.Center
                    };
                    break;

                case UiButton button:
                {
                    var btn = new Button
                    {
                        HorizontalAlignment = HorizontalAlignment.Left,
                        Padding = new Thickness(10, 4)
                    };
                    btn.Click += (s, e) => button.PerformClick();
                    view = btn;
                    break;
                }

                case UiCheckbox checkbox:
                {
                    var chk = new CheckBox { Foreground = LightText };
                    chk.IsCheckedChanged += (s, e) =>
                    {
                        if (!_syncing)
                            checkbox.SetCheckedFromUi(chk.IsChecked == true);
                    };
                    view = chk;
                    break;
                }

                case UiTextBox textBox:
                {
                    var txt = new TextBox { MinWidth = 160 };
                    txt.TextChanged += (s, e) =>
                    {
                        if (!_syncing)
                            textBox.SetTextFromUi(txt.Text ?? "");
                    };
                    view = txt;
                    break;
                }

                case UiSlider slider:
                {
                    var sld = new Slider { MinWidth = 160 };
                    sld.ValueChanged += (s, e) =>
                    {
                        if (!_syncing)
                            slider.SetValueFromUi(sld.Value);
                    };
                    view = sld;
                    break;
                }

                case UiProgressBar:
                    view = new ProgressBar
                    {
                        Minimum = 0,
                        Maximum = 1,
                        MinWidth = 160,
                        Height = 18
                    };
                    break;

                case UiSeparator:
                    view = new Border
                    {
                        Height = 1,
                        Background = SeparatorBrush,
                        Margin = new Thickness(0, 2)
                    };
                    break;

                default:
                    return null;
            }

            _views[element.Id] = view;
            return view;
        }

        // ---- Property-Sync ----------------------------------------------

        private void SyncElements(IEnumerable<UiElement> elements)
        {
            foreach (UiElement element in elements)
            {
                if (element is UiRow row)
                {
                    if (_views.TryGetValue(row.Id, out Control rowView))
                        rowView.IsVisible = row.Visible;

                    SyncElements(row.GetChildrenSnapshot());
                    continue;
                }

                if (!_views.TryGetValue(element.Id, out Control view))
                    continue;

                view.IsVisible = element.Visible;
                view.IsEnabled = element.Enabled;
                if (element.Width > 0)
                {
                    view.Width = element.Width;

                    // Eine explizit gesetzte Script-Breite gewinnt gegen die
                    // Default-MinWidth der Views (TextBox/Slider: 160) —
                    // sonst laesst sich z. B. eine Anzahl-Box nie schmal machen.
                    if (view.MinWidth > element.Width)
                        view.MinWidth = element.Width;
                }

                switch (element)
                {
                    case UiLabel label when view is TextBlock text:
                        if (text.Text != label.Text)
                            text.Text = label.Text;
                        text.Foreground = BrushFrom(label.ColorHex) ?? LightText;
                        break;

                    case UiButton button when view is Button btn:
                        if (!Equals(btn.Content, button.Text))
                            btn.Content = button.Text;
                        break;

                    case UiCheckbox checkbox when view is CheckBox chk:
                        if (!Equals(chk.Content, checkbox.Text))
                            chk.Content = checkbox.Text;
                        if ((chk.IsChecked == true) != checkbox.Checked)
                            chk.IsChecked = checkbox.Checked;
                        break;

                    case UiTextBox textBox when view is TextBox txt:
                        // Script-SetText nur uebernehmen, wenn der User nicht
                        // gerade tippt (sonst Cursorsprung).
                        if (!txt.IsFocused && (txt.Text ?? "") != textBox.Text)
                            txt.Text = textBox.Text;
                        if (txt.Watermark != textBox.Placeholder)
                            txt.Watermark = textBox.Placeholder;
                        txt.PasswordChar = textBox.IsPassword ? '●' : '\0';
                        break;

                    case UiSlider slider when view is Slider sld:
                        if (Math.Abs(sld.Minimum - slider.Min) > 0.0001) sld.Minimum = slider.Min;
                        if (Math.Abs(sld.Maximum - slider.Max) > 0.0001) sld.Maximum = slider.Max;
                        if (!sld.IsFocused && Math.Abs(sld.Value - slider.Value) > 0.0001)
                            sld.Value = slider.Value;
                        break;

                    case UiProgressBar bar when view is ProgressBar prog:
                        prog.Value = bar.Value;
                        IBrush brush = BrushFrom(bar.ColorHex);
                        if (brush != null)
                            prog.Foreground = brush;
                        break;
                }
            }
        }

        private static IBrush BrushFrom(string colorHex)
        {
            if (string.IsNullOrEmpty(colorHex))
                return null;

            try
            {
                return new SolidColorBrush(Color.Parse(colorHex));
            }
            catch
            {
                return null;
            }
        }
    }
}
