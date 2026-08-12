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

// UOSagas-Razor: VScript-Node-Editor-Fenster (Phase 5c, UX-Schwung 2026-07).
//
// Chrome in der VSCode-Farbwelt der Script-IDE (ScriptEditorWindow):
// dunkle Toolbar (#2D2D30) mit Icon-Buttons, blaue Statusleiste (#007ACC),
// dunkle Sidebar. Palette und Variablen sind eigene Row-Controls (keine
// Theme-ListBox) — Farben unter Kontrolle, Hover/Selektion lesbar, und die
// Rows lassen sich per DRAG & DROP auf den Canvas ziehen (Variablen fragen
// beim Drop Get/Set). Doppelklick fuegt in der Sichtmitte ein.
//
// Variablen-Scopes wie im In-Client-Editor: Local, GLOBAL (geht an den
// VScriptService, nicht in den Graphen), Parameter, Output; nach Parameter/
// Output werden die Start-/Return-Pins aktualisiert (Client-Verhalten).
//
// Links aus Output- UND Input-Pins ziehen; Fallenlassen auf Leerflaeche
// oeffnet die kontextgefilterte Palette; Run/Resume/Stop als Icons; die
// Ausfuehrung laeuft als oranger Trail (Engine-Poll 100 ms), Breakpoints
// pausieren gelb + Resume; Fehler stehen als roter Text direkt am Node.
//
// Graphen liegen im selben Data/VScripts-Ordner wie beim integrierten
// Assistant — Bearbeiten hier, Ausfuehren dort (und umgekehrt) geht immer.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Assistant;
using Assistant.VScripts.Core;
using Assistant.VScripts.Engine;
using Assistant.VScripts.Nodes;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;

namespace Razor.UI.VScriptEditor
{
    public sealed class VScriptEditorWindow : Window
    {
        private static VScriptEditorWindow _open;

        // VSCode-Farbwelt (identisch zur Script-IDE).
        private static readonly SolidColorBrush WindowBg = new(Color.FromRgb(0x25, 0x25, 0x26));
        private static readonly SolidColorBrush BarBg = new(Color.FromRgb(0x2D, 0x2D, 0x30));
        private static readonly SolidColorBrush BarBorder = new(Color.FromRgb(0x1B, 0x1B, 0x1C));
        private static readonly SolidColorBrush StatusBg = new(Color.FromRgb(0x00, 0x7A, 0xCC));
        private static readonly SolidColorBrush ButtonBg = new(Color.FromRgb(0x3C, 0x3C, 0x3C));
        private static readonly SolidColorBrush ButtonBorder = new(Color.FromRgb(0x55, 0x55, 0x55));
        private static readonly SolidColorBrush InputBg = new(Color.FromRgb(0x3C, 0x3C, 0x3C));
        private static readonly SolidColorBrush LightText = new(Color.FromRgb(0xDD, 0xDD, 0xDD));
        private static readonly SolidColorBrush MutedText = new(Color.FromRgb(0x9A, 0x9A, 0x9A));
        private static readonly SolidColorBrush RowHover = new(Color.FromRgb(0x2A, 0x2D, 0x2E));
        private static readonly SolidColorBrush RowSelected = new(Color.FromRgb(0x09, 0x47, 0x71));
        private static readonly IBrush RowTransparent = Brushes.Transparent;

        private readonly DarkDropDown _scriptCombo;
        private readonly NodeCanvas _canvas;
        private readonly TextBlock _status;
        private readonly DispatcherTimer _refreshTimer;

        // Sidebar
        private TextBox _paletteSearch;
        private StackPanel _palettePanel;
        private StackPanel _variablePanel;
        private StackPanel _detailsPanel;

        private ScriptVariable _selectedVariable;
        private bool _selectedVariableIsGlobal;
        private Border _selectedVariableRow;

        // Sidebar-Drag auf den Canvas
        private bool _sidebarDragging;

        // Toolbar-Zustand
        private readonly Button _runButton;
        private bool _runShowsResume;

        private NodeGraph _graph;
        private string _scriptName;
        private bool _dirty;

        public static void Open(Window owner)
        {
            Open(owner, null);
        }

        /// <summary>Oeffnet den Editor und laedt direkt das genannte Script.</summary>
        public static void Open(Window owner, string scriptName)
        {
            if (_open == null)
            {
                _open = new VScriptEditorWindow();
                _open.Closed += (s, e) => _open = null;

                if (owner != null)
                    _open.Show(owner);
                else
                    _open.Show();
            }
            else
            {
                _open.Activate();
            }

            if (scriptName != null)
                _open._scriptCombo.Select(scriptName);
        }

        /// <summary>Sagas-Zusatz (Macro-Konverter): Editor mit einem noch
        /// UNGESPEICHERTEN In-Memory-Graphen oeffnen — der erste Save fragt
        /// nach dem Script-Namen.</summary>
        public static void OpenWithGraph(Window owner, NodeGraph graph, int skippedCount = 0)
        {
            Open(owner, null);

            _open._scriptName = null;
            _open._graph = graph;
            _open._canvas.Graph = graph;
            _open._canvas.RefreshListElementTypes();
            _open._canvas.ClearHistory();
            _open._canvas.CenterOnGraph();
            _open._canvas.SetSelection(null, null);
            _open.SetDirty(true);
            _open.RebuildVariables();
            _open.RebuildPalette();
            _open.RefreshScriptList();
            _open._status.Text = skippedCount > 0
                ? $"Converted macro loaded — {skippedCount} action(s) need manual work (see the comment box). Save will ask for a name."
                : "Converted macro loaded — Save will ask for a name.";
        }

        private VScriptEditorWindow()
        {
            Title = "VScript Editor";
            Width = 1200;
            Height = 720;
            Background = WindowBg;
            FontFamily = new FontFamily("Segoe UI");
            FontSize = Ce.FontSize;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            Branding.ApplyTo(this);

            var root = new DockPanel();

            // ---- Toolbar (dunkle Bar, Icon-Buttons wie die Script-IDE) ----
            var bar = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 6,
                Margin = new Thickness(8, 5)
            };

            _scriptCombo = new DarkDropDown(Array.Empty<string>(), 220);
            _scriptCombo.SelectionChanged += name =>
            {
                if (name != null && name != _scriptName)
                    LoadScript(name);
            };
            bar.Children.Add(_scriptCombo.Control);

            bar.Children.Add(Tool("New script", Icons.NewPath, Icons.Neutral, OnNew));

            var fnButton = new Button
            {
                Content = new TextBlock
                {
                    Text = "ƒ+",
                    FontSize = 13,
                    FontWeight = FontWeight.SemiBold,
                    Foreground = new SolidColorBrush(Color.Parse("#6699E6")),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                },
                Width = 32,
                Height = 26,
                Padding = new Thickness(0),
                Background = ButtonBg,
                BorderBrush = ButtonBorder
            };
            ToolTip.SetTip(fnButton,
                "New function script (Start + Return; add Parameter/Output variables,\nthen call it from other scripts via the Functions palette)");
            fnButton.Click += (s, e) => OnNewFunction();
            bar.Children.Add(fnButton);
            bar.Children.Add(Tool("Save (also saved on Run)", Icons.SavePath, Icons.Neutral, OnSave));
            bar.Children.Add(Tool("Delete script", Icons.DeletePath, Icons.Neutral, OnDelete));
            bar.Children.Add(new Separator { Width = 12 });

            bar.Children.Add(Tool("Undo (Ctrl+Z)", Icons.UndoPath, Icons.Neutral, () => _canvas.Undo(), stroked: true));
            bar.Children.Add(Tool("Redo (Ctrl+Y)", Icons.RedoPath, Icons.Neutral, () => _canvas.Redo(), stroked: true));
            bar.Children.Add(new Separator { Width = 12 });

            _runButton = Tool("Run (breakpoints pause, B toggles)", Icons.PlayPath, Icons.Green, OnRunOrResume);
            bar.Children.Add(_runButton);
            bar.Children.Add(Tool("Stop", Icons.StopPath, Icons.Red, OnStop));
            bar.Children.Add(new Separator { Width = 12 });

            bar.Children.Add(Tool("Center view on graph", Icons.CenterPath, Icons.Neutral,
                () => _canvas.CenterOnGraph(), stroked: true));

            var toolbar = new Border
            {
                Background = BarBg,
                BorderBrush = BarBorder,
                BorderThickness = new Thickness(0, 0, 0, 1),
                Child = bar
            };
            DockPanel.SetDock(toolbar, Dock.Top);
            root.Children.Add(toolbar);

            // ---- Statusleiste (VSCode-Blau) ----
            _status = new TextBlock
            {
                Margin = new Thickness(8, 3),
                Foreground = Brushes.White,
                VerticalAlignment = VerticalAlignment.Center
            };
            var statusBar = new Border { Background = StatusBg, Child = _status };
            DockPanel.SetDock(statusBar, Dock.Bottom);
            root.Children.Add(statusBar);

            // ---- Hauptbereich: Sidebar | Splitter | Canvas ----
            var main = new Grid();
            main.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(252)));
            main.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(4)));
            main.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));

            var sidebar = BuildSidebar();
            Grid.SetColumn(sidebar, 0);
            main.Children.Add(sidebar);

            var splitter = new GridSplitter
            {
                Background = BarBorder,
                ResizeDirection = GridResizeDirection.Columns
            };
            Grid.SetColumn(splitter, 1);
            main.Children.Add(splitter);

            // ---- Canvas ----
            _canvas = new NodeCanvas();
            _canvas.GraphChanged += () => SetDirty(true);
            _canvas.SelectionChanged += RebuildDetails;
            _canvas.PaletteRequested += pos => _ = ShowPalette(pos);
            _canvas.PinDoubleClicked += OnPinDoubleClicked;
            _canvas.NodeDoubleClicked += OnNodeDoubleClicked;
            _canvas.NodeContextRequested += ShowNodeContextMenu;
            _canvas.LinkDropped += (pin, node, world) => _ = OnLinkDropped(pin, world);
            _canvas.CommentRequested += bounds => _ = OnCommentRequested(bounds);
            _canvas.CommentBoxDoubleClicked += box => _ = OnCommentRename(box);
            _canvas.FilterAddRequested += node => _ = OnFilterAdd(node);
            _canvas.FilterEditRequested += (node, idx) => _ = OnFilterEdit(node, idx);
            _canvas.FilterRemoveRequested += OnFilterRemove;
            _canvas.Notify += msg => _status.Text = msg;
            _canvas.GraphReplaced += g =>
            {
                // Undo/Redo hat eine neue Graph-Instanz erzeugt.
                _graph = g;
                SetDirty(true);
                RebuildVariables();
                RebuildDetails();
            };
            Grid.SetColumn(_canvas, 2);
            main.Children.Add(_canvas);

            root.Children.Add(main);
            Content = root;

            // Fenster-weite Shortcuts als Fallback (falls der Canvas nicht den
            // Fokus hat); Texteingaben (TextBox) bleiben unberuehrt.
            AddHandler(KeyDownEvent, (s, e) =>
            {
                if (e.Handled || !e.KeyModifiers.HasFlag(KeyModifiers.Control))
                    return;

                if (TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement() is TextBox)
                    return;

                switch (e.Key)
                {
                    case Key.C:
                        _canvas.CopySelection();
                        e.Handled = true;
                        break;
                    case Key.X:
                        _canvas.CutSelection();
                        e.Handled = true;
                        break;
                    case Key.V:
                        _canvas.PasteClipboard();
                        e.Handled = true;
                        break;
                    case Key.Z when e.KeyModifiers.HasFlag(KeyModifiers.Shift):
                    case Key.Y:
                        _canvas.Redo();
                        e.Handled = true;
                        break;
                    case Key.Z:
                        _canvas.Undo();
                        e.Handled = true;
                        break;
                    case Key.S:
                        OnSave();
                        e.Handled = true;
                        break;
                }
            }, Avalonia.Interactivity.RoutingStrategies.Tunnel);

            // Engine-Poll: Trail/Fehler/Pause-Zustand fuer die Visualisierung.
            _refreshTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
            _refreshTimer.Tick += (s, e) => PollEngine();
            _refreshTimer.Start();
            Closed += (s, e) => _refreshTimer.Stop();

            RefreshScriptList(selectFirst: true);
            RebuildPalette();
            RebuildDetails();
        }

        // ---- Styling-Helfer ------------------------------------------------

        private static Button Tool(string tooltip, string icon, IBrush brush, Action onClick, bool stroked = false)
        {
            Button b = Icons.IconButton(tooltip, icon, brush, onClick, 32, 26, stroked);
            b.Background = ButtonBg;
            b.BorderBrush = ButtonBorder;
            return b;
        }

        private static Button SmallButton(string text, string tooltip, Action onClick)
        {
            var b = new Button
            {
                Content = text,
                Padding = new Thickness(9, 2),
                FontSize = 11,
                Background = ButtonBg,
                Foreground = LightText,
                BorderBrush = ButtonBorder
            };
            ToolTip.SetTip(b, tooltip);
            b.Click += (s, e) => onClick();
            return b;
        }

        private static TextBox DarkTextBox(string watermark = null, double width = double.NaN)
        {
            var t = new TextBox
            {
                Watermark = watermark,
                Background = InputBg,
                Foreground = LightText,
                BorderBrush = ButtonBorder,
                CaretBrush = LightText
            };
            if (!double.IsNaN(width))
                t.Width = width;
            return t;
        }


        private static TextBlock SectionHeader(string text) => new()
        {
            Text = text,
            FontWeight = FontWeight.SemiBold,
            FontSize = 11,
            Foreground = MutedText
        };

        // ---- Sidebar-Aufbau ------------------------------------------------

        private Control BuildSidebar()
        {
            var sidebar = new Grid { Background = WindowBg };
            sidebar.RowDefinitions.Add(new RowDefinition(GridLength.Auto));                       // Palette-Kopf
            sidebar.RowDefinitions.Add(new RowDefinition(new GridLength(3, GridUnitType.Star))); // Palette
            sidebar.RowDefinitions.Add(new RowDefinition(GridLength.Auto));                       // Variablen
            sidebar.RowDefinitions.Add(new RowDefinition(GridLength.Auto));                       // Details-Kopf
            sidebar.RowDefinitions.Add(new RowDefinition(new GridLength(2, GridUnitType.Star))); // Details

            var paletteHead = new StackPanel { Margin = new Thickness(8, 8, 8, 4), Spacing = 4 };
            paletteHead.Children.Add(SectionHeader("PALETTE"));
            _paletteSearch = DarkTextBox("Search nodes…");
            _paletteSearch.TextChanged += (s, e) => RebuildPalette();
            paletteHead.Children.Add(_paletteSearch);
            Grid.SetRow(paletteHead, 0);
            sidebar.Children.Add(paletteHead);

            _palettePanel = new StackPanel { Spacing = 1 };
            var paletteScroll = new ScrollViewer
            {
                Content = _palettePanel,
                Margin = new Thickness(8, 0, 8, 6)
            };
            Grid.SetRow(paletteScroll, 1);
            sidebar.Children.Add(paletteScroll);

            var varSection = BuildVariablesSection();
            Grid.SetRow(varSection, 2);
            sidebar.Children.Add(varSection);

            var detailsHead = SectionHeader("DETAILS");
            detailsHead.Margin = new Thickness(8, 6, 8, 2);
            Grid.SetRow(detailsHead, 3);
            sidebar.Children.Add(detailsHead);

            _detailsPanel = new StackPanel { Spacing = 4, Margin = new Thickness(8, 2, 8, 8) };
            var detailsScroll = new ScrollViewer { Content = _detailsPanel };
            Grid.SetRow(detailsScroll, 4);
            sidebar.Children.Add(detailsScroll);

            return sidebar;
        }

        // ---- Engine-Poll / Toolbar-Zustand ---------------------------------

        private string _lastCenteredPause;
        private readonly HashSet<string> _centeredErrors = new();

        private void PollEngine()
        {
            var engine = VScriptService.Engine;
            bool paused = engine.IsPaused;

            _canvas.RunningNodeId = engine.IsRunning ? engine.CurrentExecutingNodeId : null;
            _canvas.PausedNodeId = paused ? engine.PausedAtNodeId : null;
            _canvas.DelayingNodeId = engine.IsRunning ? engine.Context?.DelayingNodeId : null;
            _canvas.DelayUntilUtc = engine.Context?.DelayUntilUtc ?? default;
            _canvas.NodeErrors = engine.GetNodeErrors();
            _canvas.NodeExecutionTimes = engine.GetNodeExecutionTimes();
            _canvas.LinkActivationTimes = engine.GetLinkActivationTimes();

            // Auto-Zentrierung: neuer Breakpoint-Stopp bzw. neuer Fehler holt
            // den betroffenen Node in die Sichtmitte (einmal pro Ereignis).
            if (paused && engine.PausedAtNodeId != null && engine.PausedAtNodeId != _lastCenteredPause)
            {
                _lastCenteredPause = engine.PausedAtNodeId;
                _canvas.CenterOnNode(engine.PausedAtNodeId);
            }
            else if (!paused)
            {
                _lastCenteredPause = null;
            }

            if (_canvas.NodeErrors.Count == 0)
            {
                _centeredErrors.Clear();
            }
            else
            {
                foreach (string nodeId in _canvas.NodeErrors.Keys)
                {
                    if (_centeredErrors.Add(nodeId))
                        _canvas.CenterOnNode(nodeId);
                }
            }

            if (paused != _runShowsResume)
            {
                _runShowsResume = paused;
                if (paused)
                    Icons.Swap(_runButton, "Resume (paused at breakpoint)", Icons.PlayPath, Icons.Yellow);
                else
                    Icons.Swap(_runButton, "Run (breakpoints pause, B toggles)", Icons.PlayPath, Icons.Green);
            }

            if (_sidebarDragging)
                return; // Drag-Hinweis nicht ueberschreiben

            if (paused)
            {
                string nodeName = _graph?.Nodes.FirstOrDefault(n => n.Id == engine.PausedAtNodeId)?.Name
                                  ?? engine.PausedAtNodeId;
                _status.Text = $"⏸ Paused at breakpoint: {nodeName} — press Resume (▶) to continue.";
                _canvas.InvalidateVisual();
            }
            else if (engine.IsRunning)
            {
                _status.Text = $"Running… ({engine.Context?.CurrentScriptName})";
            }
        }

        private void SetDirty(bool dirty)
        {
            _dirty = dirty;
            UpdateStatus();
        }

        private void UpdateStatus()
        {
            _status.Text = _graph == null
                ? "No script loaded — create one with the + button."
                : $"{_scriptName}{(_dirty ? " *" : "")} — {_graph.Nodes.Count} nodes, " +
                  $"{_graph.Links.Count(l => l.StartPinId != null)} links, {_graph.Variables.Count} variables · " +
                  "Drag pins to connect · Drag empty area to select · C: comment · Ctrl+C/X/V · Del · B: breakpoint";
        }

        private void RefreshScriptList(bool selectFirst = false)
        {
            List<string> names = VScriptService.GetAllScriptNames().OrderBy(n => n).ToList();
            _scriptCombo.SetItems(names, _scriptName);

            if (selectFirst && names.Count > 0)
                _scriptCombo.Select(names[0]);
        }

        private void LoadScript(string name)
        {
            NodeGraph graph = VScriptService.LoadScript(name);
            if (graph == null)
                return;

            _scriptName = name;
            _graph = graph;
            _canvas.Graph = graph;
            _canvas.RefreshListElementTypes(); // Subtypen sind nicht serialisiert
            _canvas.ClearHistory();
            _canvas.CenterOnGraph();
            _canvas.SetSelection(null, null);
            SetDirty(false);
            RebuildVariables();
            RebuildPalette(); // Functions-Sektion haengt vom aktuellen Script ab
        }

        // ---- Toolbar-Aktionen ---------------------------------------------

        private async void OnNew()
        {
            string name = await Dialogs.Prompt(this, "New VScript", "Script name:");
            if (string.IsNullOrWhiteSpace(name))
                return;

            name = name.Trim();

            if (VScriptService.CreateNewScript(name))
            {
                // Neues Script bekommt wie im Client einen Start-Node.
                NodeGraph graph = VScriptService.LoadScript(name);
                if (graph != null && !graph.Nodes.Any(n => n is StartNode))
                {
                    var start = new StartNode(graph.GetNextNodeId(), graph.GetNextPinId());
                    start.Position = new System.Numerics.Vector2(80, 120);
                    graph.AddNode(start);
                    VScriptService.SaveScript(name, graph);
                }

                RefreshScriptList();
                _scriptCombo.Select(name);
            }
        }

        /// <summary>Neues Funktions-Script: Start + Return vorverdrahtet; Parameter/
        /// Output-Variablen machen es per Execute Script/Functions-Palette aufrufbar.</summary>
        private async void OnNewFunction()
        {
            string name = await Dialogs.Prompt(this, "New Function Script", "Function name:");
            if (string.IsNullOrWhiteSpace(name))
                return;

            name = name.Trim();

            if (!VScriptService.CreateNewScript(name))
                return;

            NodeGraph graph = VScriptService.LoadScript(name);
            if (graph != null)
            {
                if (!graph.Nodes.Any(n => n is StartNode))
                {
                    var start = new StartNode(graph.GetNextNodeId(), graph.GetNextPinId());
                    start.Position = new System.Numerics.Vector2(80, 120);
                    graph.AddNode(start);
                }

                if (!graph.Nodes.Any(n => n is ReturnNode))
                {
                    var ret = new ReturnNode(graph.GetNextNodeId(), graph.GetNextPinId());
                    ret.Position = new System.Numerics.Vector2(460, 120);
                    graph.AddNode(ret);
                }

                VScriptService.SaveScript(name, graph);
            }

            RefreshScriptList();
            _scriptCombo.Select(name);
            _status.Text = $"Function '{name}' created — add Parameter/Output variables, " +
                           "then call it from other scripts via the Functions section of the palette.";
        }

        private async void OnSave()
        {
            if (_graph == null)
                return;

            // Sagas-Zusatz: ein konvertiertes Macro kommt ohne Namen an —
            // ERST beim Speichern wird er vergeben (OpenWithGraph).
            if (_scriptName == null)
            {
                string name = await Dialogs.Prompt(this, "Save VScript", "Script name:");
                if (string.IsNullOrWhiteSpace(name))
                    return;

                name = name.Trim();

                if (VScriptService.GetAllScriptNames()
                        .Any(n => string.Equals(n, name, StringComparison.OrdinalIgnoreCase)) &&
                    !await Dialogs.Confirm(this, "Overwrite Script",
                        $"Script '{name}' already exists — overwrite it?", "Overwrite"))
                    return;

                _scriptName = name;
                _graph.Name = name;
            }

            if (VScriptService.SaveScript(_scriptName, _graph))
            {
                SetDirty(false);
                RefreshScriptList();
                _scriptCombo.Select(_scriptName);
                _status.Text = $"Saved {_scriptName}.";
            }
            else
            {
                _status.Text = $"Save FAILED for {_scriptName}!";
            }
        }

        private async void OnDelete()
        {
            if (_scriptName == null)
                return;

            string confirm = await Dialogs.Prompt(this, "Delete VScript",
                $"Type the script name ('{_scriptName}') to delete:");
            if (confirm?.Trim() != _scriptName)
                return;

            VScriptService.DeleteScript(_scriptName);
            _graph = null;
            _scriptName = null;
            _canvas.Graph = null;
            RefreshScriptList(selectFirst: true);
            UpdateStatus();
            RebuildVariables();
        }

        private void OnRunOrResume()
        {
            var engine = VScriptService.Engine;
            if (engine.IsPaused)
            {
                engine.Resume();
                return;
            }

            if (_graph == null)
                return;

            if (_dirty)
                OnSave();

            NodeGraph graph = _graph;
            GameThread.Post(() => VScriptService.RunGraph(graph));
        }

        private void OnStop()
        {
            var engine = VScriptService.Engine;
            if (engine.IsPaused)
                engine.Resume(); // Pause loesen, damit der Stop durchkommt

            GameThread.Post(VScriptService.StopScript);
            _status.Text = "Stopped.";
        }

        // ---- Sidebar: Palette (eigene Rows + Drag&Drop) --------------------

        private void RebuildPalette()
        {
            if (_palettePanel == null)
                return;

            _palettePanel.Children.Clear();
            string q = _paletteSearch?.Text?.Trim() ?? string.Empty;

            NodeCategory? lastCat = null;

            foreach (var (def, template) in PinCompat.GetTemplates()
                         .Where(t => !t.Def.HideInPalette)
                         .OrderBy(t => t.Def.Category.ToString())
                         .ThenBy(t => t.Def.Name))
            {
                if (q.Length > 0 &&
                    !def.Name.Contains(q, StringComparison.OrdinalIgnoreCase) &&
                    !def.TypeName.Contains(q, StringComparison.OrdinalIgnoreCase) &&
                    !def.Category.ToString().Contains(q, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (def.Category != lastCat)
                {
                    lastCat = def.Category;
                    _palettePanel.Children.Add(new TextBlock
                    {
                        Text = def.Category.ToString(),
                        FontWeight = FontWeight.SemiBold,
                        FontSize = 11,
                        Margin = new Thickness(2, _palettePanel.Children.Count == 0 ? 0 : 8, 0, 2),
                        Foreground = new SolidColorBrush(NodePaletteDialog.CategoryColor(def.Category))
                    });
                }

                var row = MakeRow(def.Name + PinCompat.SubTypeSuffix(template) +
                                  (def.IsExperimental ? "  (beta)" : ""), def.Description);
                var pick = new PalettePick { Definition = def };

                row.DoubleTapped += (s, e) => AddPickedNodeAt(pick, _canvas.ViewCenterWorld());
                AttachCanvasDrag(row, worldPos => AddPickedNodeAt(pick, worldPos), def.Name);

                _palettePanel.Children.Add(row);
            }

            // FUNCTIONS: andere Scripts als aufrufbare Funktionen (ExecuteScriptNode).
            var functions = VScriptService.GetAllScripts()
                .Where(kv => !string.Equals(kv.Key, _scriptName, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(kv => FunctionHelper.IsFunctionLike(kv.Value))
                .ThenBy(kv => kv.Key)
                .ToList();

            var matching = functions.Where(kv =>
                q.Length == 0 ||
                kv.Key.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                "function".Contains(q, StringComparison.OrdinalIgnoreCase)).ToList();

            if (matching.Count > 0)
            {
                _palettePanel.Children.Add(new TextBlock
                {
                    Text = "Functions",
                    FontWeight = FontWeight.SemiBold,
                    FontSize = 11,
                    Margin = new Thickness(2, 8, 0, 2),
                    Foreground = new SolidColorBrush(Color.Parse("#6699E6"))
                });

                foreach (var (name, target) in matching)
                {
                    bool fn = FunctionHelper.IsFunctionLike(target);
                    string sig = fn ? FunctionHelper.Signature(target) : "(no parameters)";

                    var row = MakeRow((fn ? "ƒ  " : "") + name, $"Call script '{name}' {sig}");
                    var pick = new PalettePick { CallScriptName = name };

                    row.DoubleTapped += (s, e) => AddPickedNodeAt(pick, _canvas.ViewCenterWorld());
                    AttachCanvasDrag(row, worldPos => AddPickedNodeAt(pick, worldPos), name);

                    _palettePanel.Children.Add(row);
                }
            }
        }

        private Border MakeRow(string text, string tooltip)
        {
            var row = new Border
            {
                Background = RowTransparent,
                CornerRadius = new CornerRadius(3),
                Padding = new Thickness(8, 2),
                Child = new TextBlock { Text = text, Foreground = LightText, FontSize = 12 }
            };

            if (!string.IsNullOrEmpty(tooltip))
                ToolTip.SetTip(row, tooltip);

            row.PointerEntered += (s, e) =>
            {
                if (!ReferenceEquals(row, _selectedVariableRow))
                    row.Background = RowHover;
            };
            row.PointerExited += (s, e) =>
            {
                if (!ReferenceEquals(row, _selectedVariableRow))
                    row.Background = RowTransparent;
            };

            return row;
        }

        /// <summary>
        /// Row per Maus auf den Canvas ziehen: ab 6px Bewegung beginnt der Drag,
        /// Loslassen ueber dem Canvas ruft onDrop mit der Weltposition.
        /// </summary>
        private void AttachCanvasDrag(Border row, Action<Point> onDrop, string dragLabel)
        {
            bool pressed = false;
            bool dragging = false;
            Point pressPos = default;

            row.PointerPressed += (s, e) =>
            {
                if (!e.GetCurrentPoint(row).Properties.IsLeftButtonPressed)
                    return;

                pressed = true;
                dragging = false;
                pressPos = e.GetPosition(row);
                e.Pointer.Capture(row);
            };

            row.PointerMoved += (s, e) =>
            {
                if (!pressed || dragging)
                    return;

                Point p = e.GetPosition(row);
                if (Math.Abs(p.X - pressPos.X) > 6 || Math.Abs(p.Y - pressPos.Y) > 6)
                {
                    dragging = true;
                    _sidebarDragging = true;
                    Cursor = new Cursor(StandardCursorType.DragCopy);
                    _status.Text = $"Drop '{dragLabel}' on the canvas to add it.";
                }
            };

            row.PointerReleased += (s, e) =>
            {
                e.Pointer.Capture(null);

                if (dragging)
                {
                    _sidebarDragging = false;
                    Cursor = Cursor.Default;

                    Point onCanvas = e.GetPosition(_canvas);
                    if (onCanvas.X >= 0 && onCanvas.Y >= 0 &&
                        onCanvas.X <= _canvas.Bounds.Width && onCanvas.Y <= _canvas.Bounds.Height)
                    {
                        onDrop(_canvas.ScreenToWorld(onCanvas));
                    }
                    else
                    {
                        UpdateStatus();
                    }
                }

                pressed = false;
                dragging = false;
            };
        }

        private void AddPickedNodeAt(PalettePick pick, Point worldPos)
        {
            if (_graph == null)
                return;

            VScriptNode node = CreatePickedNode(pick);
            if (node == null)
                return;

            _canvas.PushUndo();
            node.Position = new System.Numerics.Vector2((float) worldPos.X, (float) worldPos.Y);
            _graph.AddNode(node);
            _canvas.SetSelection(node, null);
            SetDirty(true);
            _canvas.InvalidateVisual();
        }

        // ---- Sidebar: Variablen -------------------------------------------

        private Control BuildVariablesSection()
        {
            var panel = new StackPanel { Spacing = 4, Margin = new Thickness(8, 6, 8, 4) };

            panel.Children.Add(SectionHeader("VARIABLES"));

            // Add-Zeile 1: Name + Typ + List
            var row1 = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };
            TextBox nameBox = DarkTextBox("Name", 92);
            var typeBox = new DarkDropDown(new[] { "Number", "String", "Boolean", "Object" }, 84, 0);
            var listCheck = new CheckBox { Content = "List", Foreground = LightText, FontSize = 11 };
            row1.Children.Add(nameBox);
            row1.Children.Add(typeBox.Control);
            row1.Children.Add(listCheck);
            panel.Children.Add(row1);

            // Add-Zeile 2: Scope (inkl. Global wie im Client) + Add
            var row2 = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };
            var scopeBox = new DarkDropDown(new[] { "Local", "Global", "Parameter", "Output" }, 104, 0);
            ToolTip.SetTip(scopeBox.Control,
                "Local: this script only\nGlobal: shared by all scripts (GlobalVariables.json)\n" +
                "Parameter: input pin when called via Execute Script\nOutput: return value");
            row2.Children.Add(scopeBox.Control);

            Button addBtn = Tool("Add variable", Icons.NewPath, Icons.Green, () =>
            {
                string name = nameBox.Text?.Trim();
                if (string.IsNullOrEmpty(name))
                    return;

                PinType type = (typeBox.SelectedItem as string) switch
                {
                    "String" => PinType.String,
                    "Boolean" => PinType.Boolean,
                    "Object" => PinType.Object,
                    _ => PinType.Number
                };
                VariableScope scope = (scopeBox.SelectedItem as string) switch
                {
                    "Global" => VariableScope.Global,
                    "Parameter" => VariableScope.Parameter,
                    "Output" => VariableScope.Output,
                    _ => VariableScope.Local
                };

                var variable = new ScriptVariable(name, type, ObjectSubType.Player, scope,
                    listCheck.IsChecked == true);

                if (scope == VariableScope.Global)
                {
                    // Wie im Client: Global geht an den Service, nicht in den Graphen.
                    if (VScriptService.IsGlobalVariable(name))
                        return;

                    VScriptService.AddGlobalVariable(variable);
                }
                else
                {
                    if (_graph == null || _graph.Variables.Any(v => v.Name == name))
                        return;

                    _canvas.PushUndo();
                    _graph.Variables.Add(variable);

                    // Client-Verhalten: Parameter/Output aktualisieren Start-/Return-Pins.
                    if (scope is VariableScope.Parameter or VariableScope.Output)
                        UpdateStartReturnPins();

                    SetDirty(true);
                }

                nameBox.Text = string.Empty;
                RebuildVariables();
            });
            row2.Children.Add(addBtn);
            panel.Children.Add(row2);

            _variablePanel = new StackPanel { Spacing = 1 };
            panel.Children.Add(new ScrollViewer
            {
                Content = _variablePanel,
                Height = 104,
                Background = new SolidColorBrush(Color.FromRgb(0x1E, 0x1E, 0x1E))
            });

            // Aktionen auf der Selektion: Get-/Set-Node einfuegen, entfernen.
            var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };
            actions.Children.Add(SmallButton("Get", "Insert a Get node for the selected variable",
                () => InsertVariableNode(false, _canvas.ViewCenterWorld())));
            actions.Children.Add(SmallButton("Set", "Insert a Set node for the selected variable",
                () => InsertVariableNode(true, _canvas.ViewCenterWorld())));
            actions.Children.Add(Tool("Remove selected variable", Icons.DeletePath, Icons.Neutral, RemoveSelectedVariable));
            panel.Children.Add(actions);

            return panel;
        }

        private void RebuildVariables()
        {
            if (_variablePanel == null)
                return;

            _variablePanel.Children.Clear();
            _selectedVariable = null;
            _selectedVariableRow = null;

            void AddRow(ScriptVariable v, bool isGlobal)
            {
                var content = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };

                var dotColor = v.Type switch
                {
                    PinType.String => Color.Parse("#CC33CC"),
                    PinType.Boolean => Color.Parse("#B33333"),
                    PinType.Object => Color.Parse("#3380FF"),
                    _ => Color.Parse("#4DCC4D")
                };
                content.Children.Add(new Ellipse
                {
                    Width = 8,
                    Height = 8,
                    Fill = new SolidColorBrush(dotColor),
                    VerticalAlignment = VerticalAlignment.Center
                });

                // Scope-Marker wie im Client: [P] blau, [O] gruen, [G] gold.
                string marker = isGlobal ? "[G]" :
                    v.Scope == VariableScope.Parameter ? "[P]" :
                    v.Scope == VariableScope.Output ? "[O]" : null;
                if (marker != null)
                {
                    content.Children.Add(new TextBlock
                    {
                        Text = marker,
                        FontSize = 11,
                        FontWeight = FontWeight.SemiBold,
                        Foreground = new SolidColorBrush(marker switch
                        {
                            "[G]" => Color.Parse("#E6C34A"),
                            "[P]" => Color.Parse("#80B3FF"),
                            _ => Color.Parse("#80FFB3")
                        }),
                        VerticalAlignment = VerticalAlignment.Center
                    });
                }

                content.Children.Add(new TextBlock
                {
                    Text = $"{v.Name}  ({(v.IsList ? "List of " : "")}{v.Type})",
                    Foreground = LightText,
                    FontSize = 12,
                    VerticalAlignment = VerticalAlignment.Center
                });

                var row = new Border
                {
                    Background = RowTransparent,
                    CornerRadius = new CornerRadius(3),
                    Padding = new Thickness(6, 2),
                    Child = content
                };

                row.PointerEntered += (s, e) =>
                {
                    if (!ReferenceEquals(row, _selectedVariableRow))
                        row.Background = RowHover;
                };
                row.PointerExited += (s, e) =>
                {
                    if (!ReferenceEquals(row, _selectedVariableRow))
                        row.Background = RowTransparent;
                };
                row.PointerPressed += (s, e) =>
                {
                    if (_selectedVariableRow != null)
                        _selectedVariableRow.Background = RowTransparent;

                    _selectedVariable = v;
                    _selectedVariableIsGlobal = isGlobal;
                    _selectedVariableRow = row;
                    row.Background = RowSelected;
                };

                // Drag auf den Canvas: beim Drop Get/Set fragen (UE-Verhalten).
                AttachCanvasDrag(row, worldPos =>
                {
                    _selectedVariable = v;
                    _selectedVariableIsGlobal = isGlobal;
                    ShowVariableDropMenu(v, worldPos);
                }, v.Name);

                _variablePanel.Children.Add(row);
            }

            if (_graph != null)
            {
                foreach (var v in _graph.Variables)
                    AddRow(v, isGlobal: false);
            }

            foreach (var v in VScriptService.GetGlobalVariables())
                AddRow(v, isGlobal: true);
        }

        private void ShowVariableDropMenu(ScriptVariable v, Point worldPos)
        {
            var flyout = new MenuFlyout();

            var get = new MenuItem { Header = $"Get {v.Name}" };
            get.Click += (s, e) => InsertVariableNode(false, worldPos);
            flyout.Items.Add(get);

            var set = new MenuItem { Header = $"Set {v.Name}" };
            set.Click += (s, e) => InsertVariableNode(true, worldPos);
            flyout.Items.Add(set);

            flyout.ShowAt(_canvas, true);
        }

        private void InsertVariableNode(bool set, Point worldPos)
        {
            if (_graph == null || _selectedVariable == null)
                return;

            AddPickedNodeAt(new PalettePick { Variable = _selectedVariable, IsSetVariable = set }, worldPos);
        }

        private void RemoveSelectedVariable()
        {
            if (_selectedVariable == null)
                return;

            if (_selectedVariableIsGlobal)
            {
                var match = VScriptService.GetGlobalVariables()
                    .FirstOrDefault(v => v.Name == _selectedVariable.Name);
                if (match != null)
                    VScriptService.RemoveGlobalVariable(match);
            }
            else if (_graph != null)
            {
                bool wasPinScope = _selectedVariable.Scope is VariableScope.Parameter or VariableScope.Output;
                _canvas.PushUndo();
                _graph.Variables.RemoveAll(v => v.Name == _selectedVariable.Name);
                if (wasPinScope)
                    UpdateStartReturnPins();
                SetDirty(true);
            }

            RebuildVariables();
        }

        /// <summary>Client-Verhalten: Start-/Return-Pins an die Variablen anpassen.</summary>
        private void UpdateStartReturnPins()
        {
            if (_graph == null)
                return;

            foreach (var node in _graph.Nodes)
            {
                switch (node)
                {
                    case StartNode sn:
                        sn.UpdateParameterPins(_graph.Variables);
                        break;
                    case ReturnNode rn:
                        rn.UpdateOutputPins(_graph.Variables);
                        break;
                }
            }

            _canvas.InvalidateVisual();
        }

        // ---- Sidebar: Details ---------------------------------------------

        private void RebuildDetails()
        {
            _detailsPanel.Children.Clear();

            VScriptNode node = _canvas.SelectedNode;

            if (node == null && _canvas.SelectedNodes.Count == 0)
            {
                _detailsPanel.Children.Add(new TextBlock
                {
                    Text = "Nothing selected.\nClick a node to edit its values here.\nDrag on empty canvas to select several.",
                    Foreground = MutedText,
                    TextWrapping = TextWrapping.Wrap,
                    FontSize = 11
                });
                return;
            }

            if (_canvas.SelectedNodes.Count > 1)
            {
                _detailsPanel.Children.Add(new TextBlock
                {
                    Text = $"{_canvas.SelectedNodes.Count} nodes selected.",
                    Foreground = LightText,
                    FontSize = 12
                });
                var comment = SmallButton("Comment around selection (C)",
                    "Create a comment box around the selected nodes",
                    () => _canvas.RequestCommentAroundSelection());
                _detailsPanel.Children.Add(comment);
                _detailsPanel.Children.Add(new TextBlock
                {
                    Text = "Ctrl+C copy · Ctrl+X cut · Ctrl+V paste · Del delete",
                    Foreground = MutedText,
                    FontSize = 11,
                    TextWrapping = TextWrapping.Wrap
                });
                return;
            }

            var v = node.GetTitleBarColor();
            var catColor = Color.FromArgb(255, (byte) (v.X * 255), (byte) (v.Y * 255), (byte) (v.Z * 255));

            _detailsPanel.Children.Add(new TextBlock
            {
                Text = node.Name,
                FontWeight = FontWeight.SemiBold,
                Foreground = new SolidColorBrush(catColor)
            });

            NodeDefinition def = NodeFactory.GetAllDefinitions()
                .FirstOrDefault(d => d.TypeName == node.GetType().Name);
            if (!string.IsNullOrEmpty(def?.Description))
            {
                _detailsPanel.Children.Add(new TextBlock
                {
                    Text = def.Description,
                    Foreground = MutedText,
                    TextWrapping = TextWrapping.Wrap,
                    FontSize = 11
                });
            }

            // Breakpoint
            var bp = new CheckBox
            {
                Content = "Breakpoint (pause here)",
                IsChecked = node.HasBreakpoint,
                Foreground = LightText,
                FontSize = 11
            };
            bp.IsCheckedChanged += (s, e) =>
            {
                node.HasBreakpoint = bp.IsChecked == true;
                SetDirty(true);
                _canvas.InvalidateVisual();
            };
            _detailsPanel.Children.Add(bp);

            // Node-Konfiguration (Spell/Skill/Script/Sequence)
            if (node is CastSpellNode or UseSkillNode or ExecuteScriptNode or SequenceNode)
                _detailsPanel.Children.Add(SmallButton("Configure…", "Edit node settings", () => OnNodeDoubleClicked(node)));

            // Input-Pin-Werte direkt editieren (UE-Details-Panel)
            var dataPins = node.InputPins.Where(p => p.Type != PinType.Flow).ToList();
            if (dataPins.Count > 0)
            {
                var head = SectionHeader("INPUTS");
                head.Margin = new Thickness(0, 4, 0, 0);
                _detailsPanel.Children.Add(head);
            }

            foreach (var pin in dataPins)
            {
                bool connected = _graph != null && _graph.Links.Any(l => l.EndPinId == pin.Id);

                var row = new StackPanel { Spacing = 1 };
                row.Children.Add(new TextBlock
                {
                    Text = $"{pin.Name ?? pin.Type.ToString()} ({pin.Type}{(pin.IsList ? " list" : "")})",
                    FontSize = 10,
                    Foreground = MutedText
                });

                if (connected)
                {
                    row.Children.Add(new TextBlock
                    {
                        Text = "— linked —",
                        FontSize = 11,
                        Foreground = new SolidColorBrush(Color.Parse("#6699E6"))
                    });
                }
                else
                {
                    TextBox box = DarkTextBox(pin.Type.ToString());
                    box.FontSize = 11;
                    box.Text = pin.Value != null
                        ? Convert.ToString(pin.Value, CultureInfo.InvariantCulture)
                        : string.Empty;

                    void Commit()
                    {
                        object newValue = ParsePinValue(pin.Type, box.Text);
                        if (Equals(newValue, pin.Value))
                            return;

                        _canvas.PushUndo();
                        pin.Value = newValue;
                        SetDirty(true);
                        _canvas.InvalidateVisual();
                    }

                    box.LostFocus += (s, e) => Commit();
                    box.KeyDown += (s, e) =>
                    {
                        if (e.Key == Key.Enter)
                        {
                            Commit();
                            e.Handled = true;
                        }
                    };
                    row.Children.Add(box);
                }

                _detailsPanel.Children.Add(row);
            }
        }

        // ---- Palette / Node-Erzeugung -------------------------------------

        private VScriptNode CreatePickedNode(PalettePick pick)
        {
            if (pick == null || _graph == null)
                return null;

            if (pick.CallScriptName != null)
                return FunctionHelper.CreateCallNode(_graph, pick.CallScriptName);

            if (pick.Variable != null)
            {
                return pick.IsSetVariable
                    ? new SetVariableNode(_graph.GetNextNodeId(), _graph.GetNextPinId(),
                        pick.Variable.Name, pick.Variable.Type, pick.Variable.ObjectSubType, pick.Variable.IsList)
                    : new GetVariableNode(_graph.GetNextNodeId(), _graph.GetNextPinId(),
                        pick.Variable.Name, pick.Variable.Type, pick.Variable.ObjectSubType, pick.Variable.IsList);
            }

            VScriptNode created = NodeFactory.CreateWithGraph(pick.Definition.TypeName, _graph.GetNextNodeId(),
                _graph.GetNextPinId(), _graph);

            // Neue Find-Nodes starten im Editor OHNE den Type-Pin: ByFilter ist
            // der Standard, die Filterkette macht die Auswahl (Datei speichert
            // den Modus explizit — client-kompatibel).
            switch (created)
            {
                case Assistant.VScripts.Nodes.FindItemsNode fi:
                    fi.SearchMode = Assistant.VScripts.Nodes.FindItemsMode.ByFilter;
                    fi.UpdatePinsForSearchMode();
                    break;
                case Assistant.VScripts.Nodes.FindMobilesNode fm:
                    fm.SearchMode = Assistant.VScripts.Nodes.FindMobilesMode.ByFilter;
                    fm.UpdatePinsForSearchMode();
                    break;
            }

            return created;
        }

        private async System.Threading.Tasks.Task ShowPalette(Point worldPos)
        {
            if (_graph == null)
                return;

            PalettePick picked = await NodePaletteDialog.Show(this, _graph);
            if (picked == null)
                return;

            AddPickedNodeAt(picked, worldPos);
        }

        /// <summary>Pending-Link auf Leerflaeche: kontextgefilterte Palette + Auto-Connect.</summary>
        private async System.Threading.Tasks.Task OnLinkDropped(NodePin sourcePin, Point worldPos)
        {
            if (_graph == null)
                return;

            PalettePick picked = await NodePaletteDialog.Show(this, _graph, sourcePin);
            if (picked == null)
                return;

            VScriptNode node = CreatePickedNode(picked);
            if (node == null)
                return;

            _canvas.PushUndo();

            // Aus einem Input-Pin gezogen: neuer Node liefert den Wert, liegt links.
            double x = sourcePin.Kind == PinKind.Input ? worldPos.X - 210 : worldPos.X;
            node.Position = new System.Numerics.Vector2((float) x, (float) worldPos.Y);
            _graph.AddNode(node);

            NodePin target = PinCompat.FindAutoConnectPin(node, sourcePin);
            if (target != null)
                _canvas.TryConnect(sourcePin, target, pushUndo: false);

            _canvas.SetSelection(node, null);
            SetDirty(true);
            _canvas.InvalidateVisual();
        }

        // ---- Filter (Find Items / Find Mobiles, Razor-Zusatz) -------------

        /// <summary>Filter-Pins mit der Filterliste abgleichen; Links auf
        /// entfernte Pins mit abraeumen (der Node kennt den Graphen nicht).</summary>
        private void SyncFilters(VScriptNode node)
        {
            var filters = NodeCanvas.GetNodeFilters(node);
            if (filters == null)
                return;

            List<string> removed = Assistant.VScripts.Nodes.FindFilterCatalog.SyncFilterPins(
                node, filters, node is Assistant.VScripts.Nodes.FindMobilesNode);

            if (removed.Count > 0)
            {
                _graph.Links.RemoveAll(l =>
                    (l.StartPinId != null && removed.Contains(l.StartPinId)) ||
                    (l.EndPinId != null && removed.Contains(l.EndPinId)));
            }
        }

        private async System.Threading.Tasks.Task OnFilterAdd(VScriptNode node)
        {
            var filters = NodeCanvas.GetNodeFilters(node);
            if (filters == null)
                return;

            bool forMobiles = node is Assistant.VScripts.Nodes.FindMobilesNode;
            var filter = new Assistant.VScripts.Nodes.FindFilter
            {
                Type = (forMobiles
                    ? Assistant.VScripts.Nodes.FindFilterCatalog.MobileFilters
                    : Assistant.VScripts.Nodes.FindFilterCatalog.ItemFilters)[0].Name
            };

            if (!await FilterEditDialog.Show(this, filter, forMobiles, filters.Count == 0))
                return;

            _canvas.PushUndo();
            filters.Add(filter);
            SyncFilters(node);
            SetDirty(true);
            _canvas.InvalidateVisual();
        }

        private async System.Threading.Tasks.Task OnFilterEdit(VScriptNode node, int index)
        {
            var filters = NodeCanvas.GetNodeFilters(node);
            if (filters == null || index < 0 || index >= filters.Count)
                return;

            var original = filters[index];
            var copy = new Assistant.VScripts.Nodes.FindFilter
            {
                Type = original.Type,
                Value = original.Value,
                Negate = original.Negate,
                Or = original.Or,
                UsePin = original.UsePin,
                PinId = original.PinId
            };

            bool forMobiles = node is Assistant.VScripts.Nodes.FindMobilesNode;
            if (!await FilterEditDialog.Show(this, copy, forMobiles, index == 0))
                return;

            _canvas.PushUndo();
            filters[index] = copy;
            SyncFilters(node);
            SetDirty(true);
            _canvas.InvalidateVisual();
        }

        private void OnFilterRemove(VScriptNode node, int index)
        {
            var filters = NodeCanvas.GetNodeFilters(node);
            if (filters == null || index < 0 || index >= filters.Count)
                return;

            _canvas.PushUndo();
            filters.RemoveAt(index);
            SyncFilters(node);
            SetDirty(true);
            _canvas.InvalidateVisual();
        }

        // ---- Kommentare ---------------------------------------------------

        private async System.Threading.Tasks.Task OnCommentRequested(Rect worldBounds)
        {
            if (_graph == null)
                return;

            string title = await Dialogs.Prompt(this, "Comment", "Comment title:", "Comment");
            if (title == null)
                return;

            title = title.Trim();
            if (title.Length == 0)
                title = "Comment";

            _canvas.PushUndo();
            var box = new CommentBox(title,
                new System.Numerics.Vector2((float) worldBounds.X, (float) worldBounds.Y),
                new System.Numerics.Vector2((float) worldBounds.Width, (float) worldBounds.Height));
            _graph.AddCommentBox(box);
            SetDirty(true);
            _canvas.InvalidateVisual();
        }

        private async System.Threading.Tasks.Task OnCommentRename(CommentBox box)
        {
            string title = await Dialogs.Prompt(this, "Rename Comment", "Comment title:", box.Title);
            if (title == null)
                return;

            _canvas.PushUndo();
            box.Title = title.Trim();
            SetDirty(true);
            _canvas.InvalidateVisual();
        }

        // ---- Node-Kontextmenue (Rechtsklick) ------------------------------

        private void ShowNodeContextMenu(VScriptNode node)
        {
            var flyout = new MenuFlyout();

            var bpItem = new MenuItem
            {
                Header = node.HasBreakpoint ? "Remove Breakpoint" : "Add Breakpoint  (B)"
            };
            bpItem.Click += (s, e) =>
            {
                node.HasBreakpoint = !node.HasBreakpoint;
                SetDirty(true);
                _canvas.InvalidateVisual();
                RebuildDetails();
            };
            flyout.Items.Add(bpItem);

            if (node is CastSpellNode or UseSkillNode or ExecuteScriptNode or SequenceNode)
            {
                var cfgItem = new MenuItem { Header = "Configure…" };
                cfgItem.Click += (s, e) => OnNodeDoubleClicked(node);
                flyout.Items.Add(cfgItem);
            }

            var commentItem = new MenuItem { Header = "Comment around selection  (C)" };
            commentItem.Click += (s, e) => _canvas.RequestCommentAroundSelection();
            flyout.Items.Add(commentItem);

            flyout.Items.Add(new Separator());

            var copyItem = new MenuItem { Header = "Copy  (Ctrl+C)" };
            copyItem.Click += (s, e) => _canvas.CopySelection();
            flyout.Items.Add(copyItem);

            var pasteItem = new MenuItem { Header = "Paste  (Ctrl+V)" };
            pasteItem.Click += (s, e) => _canvas.PasteClipboard();
            flyout.Items.Add(pasteItem);

            if (!(node is StartNode))
            {
                var cutItem = new MenuItem { Header = "Cut  (Ctrl+X)" };
                cutItem.Click += (s, e) => _canvas.CutSelection();
                flyout.Items.Add(cutItem);

                flyout.Items.Add(new Separator());
                var delItem = new MenuItem { Header = "Delete  (Del)" };
                delItem.Click += (s, e) =>
                {
                    _canvas.DeleteNodeAndCleanup(node);
                    _canvas.SetSelection(null, null);
                    SetDirty(true);
                };
                flyout.Items.Add(delItem);
            }

            flyout.ShowAt(_canvas, true);
        }

        // ---- Pin-/Node-Editing --------------------------------------------

        private static object ParsePinValue(PinType type, string text)
        {
            text = text?.Trim();
            if (string.IsNullOrEmpty(text))
                return null;

            return type switch
            {
                PinType.Number when text.StartsWith("0x", StringComparison.OrdinalIgnoreCase) &&
                    uint.TryParse(text.Substring(2), NumberStyles.HexNumber,
                        CultureInfo.InvariantCulture, out uint h) => (float) h,
                PinType.Number when float.TryParse(text, NumberStyles.Float,
                    CultureInfo.InvariantCulture, out float f) => f,
                PinType.Boolean => text.Equals("true", StringComparison.OrdinalIgnoreCase) || text == "1",
                _ => text
            };
        }

        private async void OnPinDoubleClicked(NodePin pin, VScriptNode node)
        {
            if (pin.Kind != PinKind.Input || pin.Type == PinType.Flow)
                return;

            string current = pin.Value != null
                ? Convert.ToString(pin.Value, CultureInfo.InvariantCulture)
                : string.Empty;

            string text = await Dialogs.Prompt(this, $"{node.Name} — {pin.Name}",
                $"Value ({pin.Type}):", current);
            if (text == null)
                return;

            object newValue = ParsePinValue(pin.Type, text);
            if (!Equals(newValue, pin.Value))
            {
                _canvas.PushUndo();
                pin.Value = newValue;
                SetDirty(true);
            }

            _canvas.InvalidateVisual();
            RebuildDetails();
        }

        private async void OnNodeDoubleClicked(VScriptNode node)
        {
            switch (node)
            {
                case CastSpellNode cast:
                {
                    // Sagas: Auswahlliste statt nackter ID — die Bard-Songs
                    // zuerst (auf dem Shard belegen sie 701-706, die die
                    // CE-Tabelle als Masteries fuehrt und die deshalb aus der
                    // Spell-Liste ausgeblendet werden), dann alle Spells,
                    // zuletzt die freie ID-Eingabe. Gespeichert wird weiter
                    // nur SelectedSpellId — Client-dateikompatibel.
                    const string customEntry = "Custom spell ID...";
                    var items = new List<string>();
                    foreach (Assistant.HotKeys.SongHotKeys.Song song in Assistant.HotKeys.SongHotKeys.Songs)
                        items.Add($"{song.Name} ({song.SpellId})");
                    foreach (Assistant.Spell sp in Assistant.Spell.All.OrderBy(x => x.GetID()))
                    {
                        int id = sp.GetID();
                        if (id >= 701 && id <= 706)
                            continue;
                        items.Add($"{sp.PlainName} ({id})");
                    }

                    items.Add(customEntry);

                    string s = await ListPickDialog.Show(this, "Cast Spell", items);
                    if (s == null)
                        break;

                    int spellId;
                    if (s == customEntry)
                    {
                        string raw = await Dialogs.Prompt(this, "Cast Spell", "Spell ID:",
                            cast.SelectedSpellId.ToString());
                        if (!int.TryParse(raw, out spellId))
                            break;
                    }
                    else
                    {
                        var m = System.Text.RegularExpressions.Regex.Match(s, @"\((\d+)\)$");
                        if (!m.Success || !int.TryParse(m.Groups[1].Value, out spellId))
                            break;
                    }

                    _canvas.PushUndo();
                    cast.SelectedSpellId = spellId;
                    SetDirty(true);
                    break;
                }
                case UseSkillNode skill:
                {
                    string s = await Dialogs.Prompt(this, "Use Skill", "Skill index:", skill.SelectedSkillIndex.ToString());
                    if (int.TryParse(s, out int idx))
                    {
                        _canvas.PushUndo();
                        skill.SelectedSkillIndex = idx;
                        SetDirty(true);
                    }

                    break;
                }
                case ExecuteScriptNode exec:
                {
                    string s = await ListPickDialog.Show(this, "Execute Script",
                        VScriptService.GetAllScriptNames().OrderBy(n => n).ToList());
                    if (s != null)
                    {
                        _canvas.PushUndo();
                        exec.SelectedScriptName = s;
                        SetDirty(true);
                    }

                    break;
                }
                case SequenceNode seq:
                {
                    string s = await Dialogs.Prompt(this, "Sequence", "Output pin count:", seq.OutputPinCount.ToString());
                    if (int.TryParse(s, out int cnt) && cnt >= 2 && cnt <= 10)
                    {
                        _canvas.PushUndo();
                        seq.OutputPinCount = cnt;
                        SetDirty(true);
                    }

                    break;
                }
                default:
                    _status.Text = $"{node.Name}: values are edited on the pins or in the Details panel.";
                    break;
            }

            _canvas.InvalidateVisual();
            RebuildDetails();
        }
    }
}
