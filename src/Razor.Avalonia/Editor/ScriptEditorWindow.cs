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

// UOSagas-Razor: frei skalierbares IDE-Editor-Fenster (Pop-out).
//
// Das Razor-Hauptfenster ist fix 530x372 — zu klein fuer einen echten
// IDE-Editor. Dieses separate, resizable Fenster hostet den CodeEditor mit
// voller Flaeche, VSCode-artig: Menueleiste (File/Edit/Script), Toolbar,
// Statuszeile, Shortcuts (Ctrl+S Save, F5 Play, Shift+F5 Stop,
// Ctrl+/ Kommentar, Shift+Alt+F Format), dunkles Theme.
//
// Bewusst ueber Events (Play/Stop/Save) entkoppelt von ScriptManager -> der
// spaetere Lua-Editor kann dasselbe Fenster mit einer LuaLanguage nutzen.
//
// Konsole: unten ein-/ausblendbares Panel (Ctrl+J / View-Menue / Toolbar) auf
// der GETEILTEN DebugConsole — Lua-print/Console.* und die Razor-Script-
// Engine (Start/Stop/Fehler) schreiben in denselben Puffer, wie die Konsole
// des integrierten Assistants.

using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using DebugConsole = Assistant.LuaEngine.DebugConsole;

namespace Razor.UI.Editor
{
    public class ScriptEditorWindow : Window
    {
        private readonly CodeEditor _editor;
        private readonly TextBlock _status;

        private readonly RowDefinition _consoleRow;
        private readonly Border _consolePanel;
        private readonly StackPanel _consoleLines;
        private readonly ScrollViewer _consoleScroll;
        private bool _consoleVisible = true;
        private volatile bool _consoleDirty = true;

        public event Action<string> PlayRequested;   // arg = aktueller Editor-Text
        public event Action StopRequested;
        public event Action<string> SaveRequested;    // arg = aktueller Editor-Text

        /// <summary>Nur mit Debug-Controls (Lua): Pause laufend / Resume pausiert.</summary>
        public event Action PauseResumeRequested;

        /// <summary>Aktuell bearbeiteter Script-Name (fuer Titel/Save).</summary>
        public string ScriptName { get; private set; }

        /// <summary>Direkter Editor-Zugriff (Lua-Tab: Breakpoints/Marker verdrahten).</summary>
        public CodeEditor Editor => _editor;

        public ScriptEditorWindow(ILanguageDefinition language, bool debugControls = false)
        {
            Width = 900;
            Height = 640;
            CanResize = true;
            Title = "UOSagas Razor — Script Editor";
            Branding.ApplyTo(this);
            Background = new SolidColorBrush(Color.FromRgb(0x25, 0x25, 0x26));

            _editor = new CodeEditor { LanguageDefinition = language };

            // VSCode-Farbwelt fuer Chrome (Bar/Toolbar/Status).
            var barBackground = new SolidColorBrush(Color.FromRgb(0x2D, 0x2D, 0x30));
            var barForeground = new SolidColorBrush(Color.FromRgb(0xCC, 0xCC, 0xCC));
            var popupForeground = new SolidColorBrush(Color.FromRgb(0x1E, 0x1E, 0x1E));
            var statusBackground = new SolidColorBrush(Color.FromRgb(0x00, 0x7A, 0xCC));

            // ---- Menueleiste (VSCode-artig: dunkle Bar, helle Top-Items) -------
            var menu = new Menu
            {
                Background = barBackground,
                Padding = new Thickness(4, 2)
            };

            MenuItem Item(string header, string gesture, Action action)
            {
                var item = new MenuItem
                {
                    Header = header,
                    InputGesture = CodeEditor.TryParseGesture(gesture),
                    // Popups rendern hell (SimpleTheme) -> dunkle Schrift, sonst
                    // erben die Sub-Items die helle Bar-Schrift (unlesbar).
                    Foreground = popupForeground
                };
                item.Click += (s, e) => action();
                return item;
            }

            MenuItem Top(string header)
            {
                var item = new MenuItem { Header = header, Foreground = barForeground };
                menu.Items.Add(item);
                return item;
            }

            var file = Top("_File");
            file.Items.Add(Item("Save", "Ctrl+S", DoSave));

            var edit = Top("_Edit");
            edit.Items.Add(Item("Undo", "Ctrl+Z", () => _editor.Undo()));
            edit.Items.Add(Item("Redo", "Ctrl+Y", () => _editor.Redo()));
            edit.Items.Add(new Separator());
            edit.Items.Add(Item("Cut", "Ctrl+X", _editor.Cut));
            edit.Items.Add(Item("Copy", "Ctrl+C", _editor.Copy));
            edit.Items.Add(Item("Paste", "Ctrl+V", _editor.Paste));
            edit.Items.Add(Item("Select All", "Ctrl+A", _editor.SelectAll));
            edit.Items.Add(new Separator());
            edit.Items.Add(Item("Toggle Comment", "Ctrl+/", _editor.ToggleComment));
            edit.Items.Add(Item("Format Document", "Shift+Alt+F", _editor.FormatDocument));

            var script = Top("_Script");
            script.Items.Add(Item("Play", "F5", DoPlay));
            script.Items.Add(Item("Stop", "Shift+F5", () => StopRequested?.Invoke()));

            var view = Top("_View");
            view.Items.Add(Item("Toggle Console", "Ctrl+J", ToggleConsole));

            // ---- Toolbar (dunkle Buttons auf dunkler Leiste) --------------------
            var play = MakeIconButton("Play (F5)", Icons.PlayPath, Icons.Green, DoPlay);
            var stop = MakeIconButton("Stop (Shift+F5)", Icons.StopPath, Icons.Red, () => StopRequested?.Invoke());
            var save = MakeIconButton("Save (Ctrl+S)", Icons.SavePath, Icons.Neutral, DoSave);
            var format = MakeIconButton("Format Document (Shift+Alt+F)", Icons.EditPath, Icons.Neutral,
                _editor.FormatDocument);

            var toolbarPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 6,
                Margin = new Thickness(6, 4, 6, 4)
            };
            toolbarPanel.Children.Add(play);
            if (debugControls)
            {
                // Lua-Debugger: Pause/Resume (Breakpoints setzt die Spalte links).
                _editor.EnableBreakpointMargin();
                toolbarPanel.Children.Add(MakeIconButton("Pause/Resume (F6)", Icons.PausePath,
                    Icons.Neutral, () => PauseResumeRequested?.Invoke()));
            }
            toolbarPanel.Children.Add(stop);
            toolbarPanel.Children.Add(save);
            toolbarPanel.Children.Add(format);
            toolbarPanel.Children.Add(MakeIconButton("Toggle Console (Ctrl+J)", Icons.ConsolePath,
                Icons.Neutral, ToggleConsole));

            var toolbar = new Border
            {
                Background = barBackground,
                BorderBrush = new SolidColorBrush(Color.FromRgb(0x1B, 0x1B, 0x1C)),
                BorderThickness = new Thickness(0, 0, 0, 1),
                Child = toolbarPanel
            };

            // ---- Statusleiste (VSCode-Blau) -------------------------------------
            _status = new TextBlock
            {
                Foreground = Brushes.White,
                Margin = new Thickness(8, 3, 8, 3),
                VerticalAlignment = VerticalAlignment.Center,
                Text = "Ready"
            };

            var statusBar = new Border
            {
                Background = statusBackground,
                Child = _status
            };

            // ---- Konsole (geteilte DebugConsole, ein-/ausblendbar) --------------
            var consoleHeader = new DockPanel { Margin = new Thickness(8, 3, 4, 3) };
            var consoleTitle = new TextBlock
            {
                Text = "CONSOLE",
                FontSize = 11,
                Foreground = barForeground,
                VerticalAlignment = VerticalAlignment.Center
            };
            Button clear = MakeIconButton("Clear console", Icons.DeletePath, Icons.Neutral, () =>
            {
                DebugConsole.Lua.Clear(); // leert den geteilten Puffer
                _consoleDirty = true;
            });
            clear.Width = 26;
            clear.Height = 20;
            Button hide = MakeIconButton("Hide console", Icons.ConsolePath, Icons.Neutral, ToggleConsole);
            hide.Width = 26;
            hide.Height = 20;

            var headerButtons = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 4,
                HorizontalAlignment = HorizontalAlignment.Right
            };
            headerButtons.Children.Add(clear);
            headerButtons.Children.Add(hide);
            DockPanel.SetDock(headerButtons, Dock.Right);
            consoleHeader.Children.Add(headerButtons);
            consoleHeader.Children.Add(consoleTitle);

            _consoleLines = new StackPanel { Margin = new Thickness(8, 2, 8, 4) };
            _consoleScroll = new ScrollViewer
            {
                Content = _consoleLines,
                HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto
            };

            var consoleGrid = new Grid();
            consoleGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            consoleGrid.RowDefinitions.Add(new RowDefinition(GridLength.Star));
            Grid.SetRow(consoleHeader, 0);
            Grid.SetRow(_consoleScroll, 1);
            consoleGrid.Children.Add(consoleHeader);
            consoleGrid.Children.Add(_consoleScroll);

            _consolePanel = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(0x1E, 0x1E, 0x1E)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(0x3C, 0x3C, 0x3C)),
                BorderThickness = new Thickness(0, 1, 0, 0),
                Child = consoleGrid
            };

            var grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            grid.RowDefinitions.Add(new RowDefinition(GridLength.Star));
            _consoleRow = new RowDefinition(new GridLength(150));
            grid.RowDefinitions.Add(_consoleRow);
            grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));

            Grid.SetRow(menu, 0);
            Grid.SetRow(toolbar, 1);
            Grid.SetRow(_editor, 2);
            Grid.SetRow(_consolePanel, 3);
            Grid.SetRow(statusBar, 4);
            grid.Children.Add(menu);
            grid.Children.Add(toolbar);
            grid.Children.Add(_editor);
            grid.Children.Add(_consolePanel);
            grid.Children.Add(statusBar);

            Content = grid;

            // Konsole treiben: Changed setzt nur ein Flag (feuert auf Script-
            // Threads), der 250ms-UI-Timer baut die Ansicht bei Bedarf neu.
            DebugConsole.Changed += () => _consoleDirty = true;
            var consoleTimer = new DispatcherTimer(TimeSpan.FromMilliseconds(250),
                DispatcherPriority.Background, (s, e) => RefreshConsole());
            consoleTimer.Start();

            // ---- Fenster-Shortcuts ----------------------------------------------
            AddHandler(KeyDownEvent, (s, e) =>
            {
                if (e.Key == Key.S && e.KeyModifiers.HasFlag(KeyModifiers.Control))
                {
                    DoSave();
                    e.Handled = true;
                }
                else if (e.Key == Key.F5 && e.KeyModifiers.HasFlag(KeyModifiers.Shift))
                {
                    StopRequested?.Invoke();
                    e.Handled = true;
                }
                else if (e.Key == Key.F5)
                {
                    DoPlay();
                    e.Handled = true;
                }
                else if (e.Key == Key.F6)
                {
                    PauseResumeRequested?.Invoke();
                    e.Handled = true;
                }
                else if (e.Key == Key.J && e.KeyModifiers.HasFlag(KeyModifiers.Control))
                {
                    ToggleConsole();
                    e.Handled = true;
                }
            }, Avalonia.Interactivity.RoutingStrategies.Tunnel);

            Opened += (s, e) => _editor.Focus();
        }

        private void DoPlay() => PlayRequested?.Invoke(_editor.Text);
        private void DoSave() => SaveRequested?.Invoke(_editor.Text);

        // ---- Konsole --------------------------------------------------------

        private void ToggleConsole()
        {
            _consoleVisible = !_consoleVisible;
            _consolePanel.IsVisible = _consoleVisible;
            _consoleRow.Height = _consoleVisible ? new GridLength(150) : new GridLength(0);

            if (_consoleVisible)
            {
                _consoleDirty = true;
                RefreshConsole();
            }
        }

        /// <summary>Baut die Konsolen-Ansicht aus dem geteilten Puffer neu
        /// (nur wenn sichtbar und veraendert; Level faerbt die Zeile).</summary>
        private void RefreshConsole()
        {
            if (!_consoleVisible || !_consoleDirty || !IsVisible)
                return;

            _consoleDirty = false;

            List<string> lines = DebugConsole.Snapshot();
            _consoleLines.Children.Clear();

            foreach (string line in lines)
            {
                IBrush brush;
                if (line.Contains("/error]"))
                    brush = new SolidColorBrush(Color.FromRgb(0xF4, 0x47, 0x47));
                else if (line.Contains("/warn]"))
                    brush = new SolidColorBrush(Color.FromRgb(0xCC, 0xA7, 0x00));
                else if (line.Contains("/debug]"))
                    brush = new SolidColorBrush(Color.FromRgb(0x85, 0x85, 0x85));
                else
                    brush = new SolidColorBrush(Color.FromRgb(0xCC, 0xCC, 0xCC));

                _consoleLines.Children.Add(new TextBlock
                {
                    Text = line,
                    Foreground = brush,
                    FontFamily = new FontFamily("Consolas,Menlo,DejaVu Sans Mono,monospace"),
                    FontSize = 12
                });
            }

            _consoleScroll.ScrollToEnd();
        }

        private static Button MakeIconButton(string tooltip, string iconPath, IBrush brush, Action onClick)
        {
            Button b = Icons.IconButton(tooltip, iconPath, brush, onClick, 32, 26);
            b.Background = new SolidColorBrush(Color.FromRgb(0x3C, 0x3C, 0x3C));
            b.BorderBrush = new SolidColorBrush(Color.FromRgb(0x55, 0x55, 0x55));
            return b;
        }

        public void LoadScript(string name, string text)
        {
            ScriptName = name;
            Title = $"UOSagas Razor — {name}";
            _editor.Text = text ?? string.Empty;
            _editor.ClearMarkers();
            SetStatus("Loaded " + name);
        }

        public string GetText() => _editor.Text;

        public void SetExecutionLine(int line) => _editor.SetExecutionLine(line);
        public void SetErrorLine(int line) => _editor.SetErrorLine(line);
        public void ClearMarkers() => _editor.ClearMarkers();

        public void SetStatus(string text)
        {
            if (_status != null)
                _status.Text = text;
        }
    }
}
