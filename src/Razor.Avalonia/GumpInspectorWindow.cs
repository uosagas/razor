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

// UOSagas-Razor: Gump-Inspector (Gegenstueck zum "Gump Observer" des
// integrierten Assistants, aber paket-basiert).
//
// Zeichnet auf, was der Server oeffnet (0xB0/0xDD) und was der Spieler
// antwortet (0xB1). Fuer Script-Autoren beantwortet das die zwei Fragen,
// fuer die man den Inspector aufmacht:
//   1. Welche Gump-ID nehme ich fuer waitforgump?
//   2. Welche Button-ID nehme ich fuer gumpresponse?
// Frage 2 loest man am schnellsten, indem man den Button im Spiel einmal
// selbst klickt — der Inspector zeigt die Antwort am Capture an.
//
// Datenfluss wie der Rest der UI: der Kern (GumpObserver) sammelt auf dem
// Game-Thread, dieses Fenster pollt Version alle 500 ms und liest Snapshots.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Assistant.Core;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;

namespace Razor.UI
{
    public class GumpInspectorWindow : Window
    {
        private static GumpInspectorWindow _instance;

        /// <summary>Oeffnet den Inspector (ein Fenster fuer die ganze App) bzw. holt ihn nach vorn.</summary>
        public static void Open()
        {
            if (_instance == null)
            {
                _instance = new GumpInspectorWindow();
                _instance.Closed += (s, e) => _instance = null;
                _instance.Show();
            }
            else
            {
                _instance.Activate();
            }
        }

        private readonly ListBox _gumpList;
        private readonly ListBox _controlList;
        private readonly TextBlock _header;
        private readonly TextBox _detail;
        private readonly TextBlock _countLabel;
        private readonly Button _recordButton;
        private readonly DispatcherTimer _timer;

        private List<CapturedGump> _snapshot = new List<CapturedGump>();
        private int _lastVersion = -1;
        private bool _rebuilding;

        public GumpInspectorWindow()
        {
            Width = 860;
            Height = 560;
            CanResize = true;
            Title = "UOSagas Razor — Gump Inspector";
            Branding.ApplyTo(this);
            Background = Ce.WindowBackground;
            FontFamily = new FontFamily("Segoe UI");
            FontSize = 12;

            // ---- Toolbar ------------------------------------------------------
            _recordButton = new Button { Content = "Start Recording", Padding = new Thickness(12, 5) };
            _recordButton.Click += (s, e) =>
            {
                GumpObserver.Recording = !GumpObserver.Recording;
                UpdateToolbar();
            };

            var clearButton = new Button { Content = "Clear", Padding = new Thickness(12, 5) };
            clearButton.Click += (s, e) =>
            {
                GumpObserver.Clear();
                _detail.Text = string.Empty;
                _header.Text = string.Empty;
                _controlList.ItemsSource = null;
            };

            _countLabel = new TextBlock
            {
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = Ce.GrayText
            };

            var toolbar = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 8,
                Margin = new Thickness(10, 8)
            };
            toolbar.Children.Add(_recordButton);
            toolbar.Children.Add(clearButton);
            toolbar.Children.Add(_countLabel);

            // ---- Linke Liste: gefangene Gumps ----------------------------------
            _gumpList = new ListBox { FontFamily = new FontFamily("Consolas,monospace") };
            _gumpList.SelectionChanged += (s, e) =>
            {
                if (!_rebuilding)
                    ShowDetails(SelectedGump());
            };

            // ---- Rechte Seite: Kopf + Snippets + Controls + Detail -------------
            _header = new TextBlock
            {
                FontFamily = new FontFamily("Consolas,monospace"),
                Margin = new Thickness(10, 8),
                TextWrapping = TextWrapping.Wrap
            };

            var copyPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 6,
                Margin = new Thickness(10, 0, 10, 8)
            };
            copyPanel.Children.Add(CopyButton("Copy Gump ID", () => SelectedGump()?.GumpId.ToString()));
            copyPanel.Children.Add(CopyButton("Copy Serial", () => Hex(SelectedGump()?.Serial)));
            copyPanel.Children.Add(CopyButton("Copy waitforgump", () =>
                SelectedGump() is { } g ? $"waitforgump {g.GumpId} 5000" : null));
            copyPanel.Children.Add(CopyButton("Copy gumpresponse", () =>
            {
                CapturedGump g = SelectedGump();
                if (g == null)
                    return null;

                // Am nuetzlichsten ist der Button, den der Spieler wirklich
                // geklickt hat; sonst der erste Button aus dem Layout.
                int? button = g.Response?.ButtonId ?? g.Controls.FirstOrDefault(c => c.ButtonId.HasValue)?.ButtonId;
                return button.HasValue ? $"gumpresponse {button.Value} {g.GumpId}" : null;
            }));

            _controlList = new ListBox { FontFamily = new FontFamily("Consolas,monospace") };
            _controlList.SelectionChanged += (s, e) => ShowControlDetail();

            _detail = new TextBox
            {
                IsReadOnly = true,
                AcceptsReturn = true,
                TextWrapping = TextWrapping.Wrap,
                FontFamily = new FontFamily("Consolas,monospace"),
                Height = 110,
                Margin = new Thickness(10, 6, 10, 10)
            };

            var right = new DockPanel();
            DockPanel.SetDock(_header, Dock.Top);
            DockPanel.SetDock(copyPanel, Dock.Top);
            DockPanel.SetDock(_detail, Dock.Bottom);
            right.Children.Add(_header);
            right.Children.Add(copyPanel);
            right.Children.Add(_detail);
            right.Children.Add(new Border
            {
                Child = _controlList,
                BorderBrush = Ce.GroupBorder,
                BorderThickness = new Thickness(1),
                Margin = new Thickness(10, 0)
            });

            // ---- Gesamtlayout ---------------------------------------------------
            var main = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("280,4,*"),
                Margin = new Thickness(10, 0, 0, 10)
            };
            var listBorder = new Border
            {
                Child = _gumpList,
                BorderBrush = Ce.GroupBorder,
                BorderThickness = new Thickness(1)
            };
            Grid.SetColumn(listBorder, 0);
            var splitter = new GridSplitter { Background = Brushes.Transparent };
            Grid.SetColumn(splitter, 1);
            Grid.SetColumn(right, 2);
            main.Children.Add(listBorder);
            main.Children.Add(splitter);
            main.Children.Add(right);

            var root = new DockPanel();
            DockPanel.SetDock(toolbar, Dock.Top);
            root.Children.Add(toolbar);
            root.Children.Add(main);
            Content = root;

            UpdateToolbar();

            // Gleicher Rhythmus wie der UiSnapshot-Pump des Hauptfensters.
            _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
            _timer.Tick += (s, e) => Poll();
            _timer.Start();

            Closed += (s, e) => _timer.Stop();
        }

        // ------------------------------------------------------------------ helpers

        private static string Hex(uint? value) => value.HasValue ? $"0x{value.Value:X8}" : null;

        private Button CopyButton(string label, Func<string> value)
        {
            var button = new Button { Content = label, Padding = new Thickness(9, 4), FontSize = 11 };
            button.Click += async (s, e) =>
            {
                string text = value();
                if (string.IsNullOrEmpty(text))
                    return;
                var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
                if (clipboard != null)
                    await clipboard.SetTextAsync(text);
            };
            return button;
        }

        private CapturedGump SelectedGump()
        {
            int i = _gumpList.SelectedIndex;
            return i >= 0 && i < _snapshot.Count ? _snapshot[i] : null;
        }

        private void UpdateToolbar()
        {
            _recordButton.Content = GumpObserver.Recording ? "Stop Recording" : "Start Recording";
            _countLabel.Text = GumpObserver.Recording
                ? $"RECORDING — {_snapshot.Count} captured"
                : $"not recording — {_snapshot.Count} captured";
            _countLabel.Foreground = GumpObserver.Recording ? Brushes.Firebrick : Ce.GrayText;
        }

        // ------------------------------------------------------------------ refresh

        private void Poll()
        {
            int version = GumpObserver.Version;
            if (version == _lastVersion)
                return;
            _lastVersion = version;

            CapturedGump selected = SelectedGump();
            _snapshot = GumpObserver.Snapshot();

            _rebuilding = true;
            try
            {
                _gumpList.ItemsSource = _snapshot
                    .Select((g, i) =>
                        $"{g.Timestamp:HH:mm:ss}  ID {g.GumpId}{(g.Response != null ? $"  → btn {g.Response.ButtonId}" : "")}")
                    .ToList();

                int index = selected != null ? _snapshot.IndexOf(selected) : -1;
                if (index < 0 && _snapshot.Count > 0)
                    index = _snapshot.Count - 1; // neuester
                _gumpList.SelectedIndex = index;
            }
            finally
            {
                _rebuilding = false;
            }

            UpdateToolbar();
            ShowDetails(SelectedGump());
        }

        private void ShowDetails(CapturedGump gump)
        {
            if (gump == null)
            {
                _header.Text = _snapshot.Count == 0
                    ? "No gumps captured yet. Start recording, then open the gump in game."
                    : "Select a captured gump.";
                _controlList.ItemsSource = null;
                _detail.Text = string.Empty;
                return;
            }

            var sb = new StringBuilder();
            sb.AppendLine($"Gump ID : {gump.GumpId}  (0x{gump.GumpId:X})   ← waitforgump/gumpresponse");
            sb.AppendLine($"Serial  : 0x{gump.Serial:X8}");
            sb.AppendLine($"Source  : {(gump.Compressed ? "0xDD compressed" : "0xB0 uncompressed")}   Position: ({gump.X}, {gump.Y})   Captured: {gump.Timestamp:HH:mm:ss}");

            if (gump.Response != null)
            {
                sb.Append($"ANSWERED: button {gump.Response.ButtonId}");
                if (gump.Response.Switches.Length > 0)
                    sb.Append($"   switches: {string.Join(", ", gump.Response.Switches)}");
                if (gump.Response.TextEntries.Length > 0)
                    sb.Append($"   entries: {string.Join(", ", gump.Response.TextEntries.Select(t => $"{t.Key}='{t.Value}'"))}");
                sb.AppendLine();
            }

            _header.Text = sb.ToString().TrimEnd();

            _controlList.ItemsSource = gump.Controls.Select((c, i) => ControlLine(i, c)).ToList();
            _detail.Text = string.Empty;
        }

        private static string ControlLine(int index, GumpControlInfo c)
        {
            var sb = new StringBuilder($"[{index}] {c.Type}");

            if (c.Page.HasValue)
                sb.Append($" {c.Page.Value}");
            else if (c.X >= 0)
                sb.Append($" ({c.X},{c.Y})");

            if (c.ButtonId.HasValue)
                sb.Append($"  id={c.ButtonId.Value}");
            if (c.SwitchId.HasValue)
                sb.Append($"  switch={c.SwitchId.Value}");
            if (c.EntryId.HasValue)
                sb.Append($"  entry={c.EntryId.Value}");
            if (c.Cliloc.HasValue)
                sb.Append($"  cliloc={c.Cliloc.Value}");
            if (c.Graphic.HasValue)
                sb.Append($"  gfx=0x{c.Graphic.Value:X}");
            if (!string.IsNullOrEmpty(c.Text))
                sb.Append($"  \"{Shorten(c.Text)}\"");

            return sb.ToString();
        }

        private static string Shorten(string text) =>
            text.Length > 40 ? text.Substring(0, 38) + ".." : text;

        private void ShowControlDetail()
        {
            CapturedGump gump = SelectedGump();
            int i = _controlList.SelectedIndex;
            if (gump == null || i < 0 || i >= gump.Controls.Count)
                return;

            GumpControlInfo c = gump.Controls[i];
            var sb = new StringBuilder();
            sb.AppendLine($"{{ {c.Raw} }}");

            if (c.ButtonId.HasValue)
                sb.AppendLine($"gumpresponse {c.ButtonId.Value} {gump.GumpId}");
            if (!string.IsNullOrEmpty(c.Text))
                sb.AppendLine($"Text: {c.Text}");
            if (c.Cliloc.HasValue)
                sb.AppendLine($"Cliloc: {c.Cliloc.Value}");

            _detail.Text = sb.ToString().TrimEnd();
        }
    }
}
