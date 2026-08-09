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

// UOSagas-Razor: Dialoge des VScript-Editors (Phase 5c, UX-Schwung 2026-07) —
// Node-Palette (Suche + Kategorie-Farben; optional kontextgefiltert auf einen
// gezogenen Pin, wie das Drag-off-Menue des In-Client-/UE-Editors) und ein
// generischer Listen-Picker. Die Palette kennt zusaetzlich pro Graph-Variable
// "Get <name>"/"Set <name>"-Eintraege (UE: Variablen im Kontextmenue).

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Assistant.VScripts.Core;
using Assistant.VScripts.Nodes;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;

namespace Razor.UI.VScriptEditor
{
    /// <summary>Ergebnis der Palette: Node-Definition, Variablen-Get/Set oder
    /// Funktions-Aufruf (vorkonfigurierter ExecuteScriptNode).</summary>
    public sealed class PalettePick
    {
        public NodeDefinition Definition;
        public ScriptVariable Variable;
        public bool IsSetVariable;
        public string CallScriptName;
    }

    /// <summary>Dunkle UI-Bausteine, die das Theme nicht kaputt machen kann.</summary>
    public static class DarkUi
    {
        /// <summary>Dropdown-Items explizit weiss rendern — die Theme-Vorlage
        /// macht sie auf dunklem Popup sonst unlesbar grau.</summary>
        public static ComboBox WhiteItems(ComboBox combo)
        {
            combo.ItemTemplate = new Avalonia.Controls.Templates.FuncDataTemplate<object>((o, _) =>
                new TextBlock
                {
                    Text = o?.ToString(),
                    Foreground = Avalonia.Media.Brushes.White,
                    FontSize = 12
                });
            return combo;
        }
    }

    /// <summary>
    /// Eigener dunkler Dropdown (Button + MenuFlyout) als Ersatz fuer die
    /// Theme-ComboBox: deren Popup ist hell und macht Items in dunklen Dialogen
    /// unlesbar — das Menue-Popup dagegen rendert ueberall lesbar (wie die
    /// Kontextmenues des Editors).
    /// </summary>
    public sealed class DarkDropDown
    {
        private readonly Button _button;
        private readonly TextBlock _label;
        private List<string> _items;

        public Button Control => _button;
        public int SelectedIndex { get; private set; } = -1;

        public string SelectedItem =>
            SelectedIndex >= 0 && SelectedIndex < _items.Count ? _items[SelectedIndex] : null;

        public event Action<string> SelectionChanged;

        public DarkDropDown(IEnumerable<string> items, double width, int initialIndex = -1)
        {
            _items = items?.ToList() ?? new List<string>();

            _label = new TextBlock
            {
                Foreground = new SolidColorBrush(Color.Parse("#DDDDDD")),
                FontSize = 12,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
            };

            var arrow = new TextBlock
            {
                Text = "▾",
                Foreground = new SolidColorBrush(Color.Parse("#9A9A9A")),
                FontSize = 11,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
            };

            var content = new DockPanel();
            DockPanel.SetDock(arrow, Dock.Right);
            content.Children.Add(arrow);
            content.Children.Add(_label);

            _button = new Button
            {
                Width = width,
                Height = 26,
                Padding = new Avalonia.Thickness(8, 2),
                Background = new SolidColorBrush(Color.Parse("#3C3C3C")),
                BorderBrush = new SolidColorBrush(Color.Parse("#555555")),
                HorizontalContentAlignment = Avalonia.Layout.HorizontalAlignment.Stretch,
                Content = content
            };
            _button.Click += (s, e) => ShowMenu();

            if (initialIndex >= 0 && initialIndex < _items.Count)
                SelectIndex(initialIndex, fire: false);
        }

        public bool IsEnabled
        {
            get => _button.IsEnabled;
            set => _button.IsEnabled = value;
        }

        /// <summary>Ersetzt die Items; optional wird ein Eintrag OHNE Event gewaehlt.</summary>
        public void SetItems(IEnumerable<string> items, string select = null)
        {
            _items = items?.ToList() ?? new List<string>();
            int idx = select != null ? _items.IndexOf(select) : -1;
            SelectedIndex = idx;
            _label.Text = idx >= 0 ? _items[idx] : string.Empty;
        }

        /// <summary>Waehlt einen Eintrag (feuert SelectionChanged).</summary>
        public void Select(string item)
        {
            int idx = _items.IndexOf(item);
            if (idx >= 0)
                SelectIndex(idx, fire: true);
        }

        private void SelectIndex(int idx, bool fire)
        {
            SelectedIndex = idx;
            _label.Text = _items[idx];
            if (fire)
                SelectionChanged?.Invoke(_items[idx]);
        }

        private void ShowMenu()
        {
            var flyout = new MenuFlyout();

            for (int i = 0; i < _items.Count; i++)
            {
                int index = i;
                var mi = new MenuItem { Header = _items[i] };
                mi.Click += (s, e) => SelectIndex(index, fire: true);
                flyout.Items.Add(mi);
            }

            flyout.ShowAt(_button);
        }
    }

    /// <summary>Hilfen fuer "Funktionen" = Scripts mit Parameter-/Output-Variablen,
    /// aufgerufen ueber den client-nativen ExecuteScriptNode (dateikompatibel).</summary>
    public static class FunctionHelper
    {
        /// <summary>Signatur eines Scripts: (param: Typ, …) → (output: Typ, …).</summary>
        public static string Signature(NodeGraph graph)
        {
            var ps = graph.Variables.Where(v => v.Scope == VariableScope.Parameter)
                .Select(v => $"{v.Name}: {(v.IsList ? "List of " : "")}{v.Type}");
            var os = graph.Variables.Where(v => v.Scope == VariableScope.Output)
                .Select(v => $"{v.Name}: {(v.IsList ? "List of " : "")}{v.Type}");
            return $"({string.Join(", ", ps)}) → ({string.Join(", ", os)})";
        }

        public static bool IsFunctionLike(NodeGraph graph) =>
            graph.Variables.Any(v => v.Scope is VariableScope.Parameter or VariableScope.Output);

        /// <summary>Vorkonfigurierter Execute-Script-Node (Pins aus dem Ziel-Script).</summary>
        public static Assistant.VScripts.Nodes.ExecuteScriptNode CreateCallNode(
            NodeGraph targetOwner, string scriptName)
        {
            var node = new Assistant.VScripts.Nodes.ExecuteScriptNode(
                targetOwner.GetNextNodeId(), targetOwner.GetNextPinId());
            node.SelectedScriptName = scriptName; // Setter baut die Parameter-/Output-Pins
            node.Name = $"ƒ {scriptName}"; // Funktionsname als Titel (persistiert)
            return node;
        }
    }

    public static class NodePaletteDialog
    {
        /// <summary>Kategorie-Farben des In-Client-Editors (vscript-graph.ts).</summary>
        public static Color CategoryColor(NodeCategory cat) => cat switch
        {
            NodeCategory.Event => Color.Parse("#CC3333"),
            NodeCategory.Action => Color.Parse("#3366CC"),
            NodeCategory.Logic => Color.Parse("#4DB34D"),
            NodeCategory.Variable => Color.Parse("#9933CC"),
            NodeCategory.Math => Color.Parse("#33CC99"),
            NodeCategory.String => Color.Parse("#CC4D99"),
            NodeCategory.UI => Color.Parse("#E6B333"),
            NodeCategory.Flow => Color.Parse("#808080"),
            NodeCategory.Game => Color.Parse("#6699E6"),
            NodeCategory.List => Color.Parse("#E6994D"),
            _ => Color.Parse("#999999")
        };

        private sealed class Entry
        {
            public PalettePick Pick;
            public string Display;
            public string SearchText;
            public NodeCategory Category;
            public string Description;
        }

        /// <summary>
        /// Zeigt die Palette. sourcePin != null filtert auf kompatible Nodes
        /// (Drag-off: aus Output-Pins Nodes mit passendem Input, aus Input-Pins
        /// Nodes mit passendem Output; Variablen-Eintraege werden mitgeprueft).
        /// </summary>
        public static async Task<PalettePick> Show(Window owner, NodeGraph graph, NodePin sourcePin = null)
        {
            List<Entry> all = BuildEntries(graph, sourcePin);

            Window dlg = Ce.Dialog(
                sourcePin == null ? "Add Node" : $"Add Node — compatible with '{sourcePin.Name ?? sourcePin.Type.ToString()}'",
                480, 500, canResize: true, background: new SolidColorBrush(Color.Parse("#252526")));

            var root = new DockPanel { Margin = new Thickness(8) };

            var search = new TextBox
            {
                Watermark = $"Search {all.Count} nodes…",
                Background = new SolidColorBrush(Color.Parse("#3C3C3C")),
                Foreground = new SolidColorBrush(Color.Parse("#DDDDDD")),
                BorderBrush = new SolidColorBrush(Color.Parse("#555555")),
                CaretBrush = new SolidColorBrush(Color.Parse("#DDDDDD"))
            };
            DockPanel.SetDock(search, Dock.Top);
            root.Children.Add(search);

            // Eigene Rows statt Theme-ListBox: Selektion bleibt lesbar
            // (VSCode-Blau statt hellem Theme-Highlight), Klick = Einfuegen.
            var panel = new StackPanel { Spacing = 1 };
            var scroll = new ScrollViewer { Content = panel, Margin = new Thickness(0, 6, 0, 0) };
            root.Children.Add(scroll);

            var rowSelected = new SolidColorBrush(Color.Parse("#094771"));
            var rowHover = new SolidColorBrush(Color.Parse("#2A2D2E"));

            List<Entry> visible = all;
            var rows = new List<Border>();
            int selected = 0;
            PalettePick result = null;

            void Commit(int index)
            {
                if (index >= 0 && index < visible.Count)
                {
                    result = visible[index].Pick;
                    dlg.Close();
                }
            }

            void Highlight(int index)
            {
                if (selected >= 0 && selected < rows.Count)
                    rows[selected].Background = Brushes.Transparent;

                selected = Math.Clamp(index, 0, Math.Max(0, rows.Count - 1));
                if (selected < rows.Count)
                {
                    rows[selected].Background = rowSelected;
                    rows[selected].BringIntoView();
                }
            }

            void Refresh()
            {
                string q = search.Text?.Trim() ?? string.Empty;
                visible = all.Where(en =>
                        q.Length == 0 ||
                        en.SearchText.Contains(q, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                panel.Children.Clear();
                rows.Clear();

                for (int i = 0; i < visible.Count; i++)
                {
                    Entry en = visible[i];
                    int index = i;

                    var row = new Border
                    {
                        Background = Brushes.Transparent,
                        CornerRadius = new CornerRadius(3),
                        Padding = new Thickness(8, 3),
                        Child = new TextBlock
                        {
                            Text = en.Display,
                            Foreground = new SolidColorBrush(CategoryColor(en.Category)),
                            FontSize = 12
                        }
                    };
                    if (!string.IsNullOrEmpty(en.Description))
                        ToolTip.SetTip(row, en.Description);

                    row.PointerEntered += (s, e) =>
                    {
                        if (index != selected)
                            row.Background = rowHover;
                    };
                    row.PointerExited += (s, e) =>
                    {
                        if (index != selected)
                            row.Background = Brushes.Transparent;
                    };
                    row.PointerPressed += (s, e) => Commit(index);

                    rows.Add(row);
                    panel.Children.Add(row);
                }

                selected = 0;
                if (rows.Count > 0)
                    rows[0].Background = rowSelected;
            }

            search.TextChanged += (s, e) => Refresh();
            Refresh();

            dlg.KeyDown += (s, e) =>
            {
                switch (e.Key)
                {
                    case Key.Enter:
                        Commit(selected);
                        e.Handled = true;
                        break;
                    case Key.Escape:
                        dlg.Close();
                        e.Handled = true;
                        break;
                    case Key.Down:
                        Highlight(selected + 1);
                        e.Handled = true;
                        break;
                    case Key.Up:
                        Highlight(selected - 1);
                        e.Handled = true;
                        break;
                }
            };

            dlg.Content = root;
            dlg.Opened += (s, e) => search.Focus();

            await dlg.ShowDialog(owner);
            return result;
        }

        private static List<Entry> BuildEntries(NodeGraph graph, NodePin sourcePin)
        {
            var entries = new List<Entry>();

            foreach (var (def, template) in PinCompat.GetTemplates())
            {
                // Ohne Pin-Kontext: nur sichtbare Palette-Nodes.
                // Mit Pin-Kontext (Drag-off): auch versteckte, aber nur kompatible.
                if (sourcePin == null)
                {
                    if (def.HideInPalette)
                        continue;
                }
                else if (!PinCompat.NodeAccepts(template, sourcePin))
                {
                    continue;
                }

                // Typ-Suffix bei Objekt-Gettern: "Get Name (Item)" vs. "(Mobile)".
                string suffix = PinCompat.SubTypeSuffix(template);

                entries.Add(new Entry
                {
                    Pick = new PalettePick { Definition = def },
                    Display = $"{def.Category}:  {def.Name}{suffix}" + (def.IsExperimental ? "  (beta)" : ""),
                    SearchText = $"{def.Category} {def.Name}{suffix} {def.TypeName} {def.Description}",
                    Category = def.Category,
                    Description = def.Description
                });
            }

            // Variablen-Eintraege (Get/Set) — im Client entstehen sie ueber das
            // Variablen-Panel; hier zusaetzlich direkt in der Palette.
            if (graph != null)
            {
                foreach (var variable in graph.Variables)
                {
                    var get = new GetVariableNode("tpl_get", "tplpin_get",
                        variable.Name, variable.Type, variable.ObjectSubType, variable.IsList);
                    var set = new SetVariableNode("tpl_set", "tplpin_set",
                        variable.Name, variable.Type, variable.ObjectSubType, variable.IsList);

                    if (sourcePin == null || PinCompat.NodeAccepts(get, sourcePin))
                    {
                        entries.Add(new Entry
                        {
                            Pick = new PalettePick { Variable = variable, IsSetVariable = false },
                            Display = $"Variable:  Get {variable.Name}",
                            SearchText = $"Variable Get {variable.Name}",
                            Category = NodeCategory.Variable,
                            Description = $"Read variable '{variable.Name}' ({variable.Type})"
                        });
                    }

                    if (sourcePin == null || PinCompat.NodeAccepts(set, sourcePin))
                    {
                        entries.Add(new Entry
                        {
                            Pick = new PalettePick { Variable = variable, IsSetVariable = true },
                            Display = $"Variable:  Set {variable.Name}",
                            SearchText = $"Variable Set {variable.Name}",
                            Category = NodeCategory.Variable,
                            Description = $"Write variable '{variable.Name}' ({variable.Type})"
                        });
                    }
                }
            }

            // Funktions-Eintraege: andere Scripts als vorkonfigurierter Aufruf
            // (ExecuteScriptNode — client-nativ, Dateiformat bleibt kompatibel).
            if (graph != null)
            {
                foreach (var (name, target) in Assistant.VScripts.Engine.VScriptService.GetAllScripts()
                             .OrderBy(kv => kv.Key))
                {
                    if (string.Equals(name, graph.Name, StringComparison.OrdinalIgnoreCase))
                        continue; // Rekursion ist in der Engine verboten

                    bool fn = FunctionHelper.IsFunctionLike(target);
                    string sig = fn ? FunctionHelper.Signature(target) : "(no parameters)";

                    // Drag-off-Filter: passt der konfigurierte Call-Node an den Pin?
                    if (sourcePin != null)
                    {
                        var template = FunctionHelper.CreateCallNode(graph, name);
                        if (!PinCompat.NodeAccepts(template, sourcePin))
                            continue;
                    }

                    entries.Add(new Entry
                    {
                        Pick = new PalettePick { CallScriptName = name },
                        Display = $"Function:  {(fn ? "ƒ " : "")}Call {name}",
                        SearchText = $"Function Call {name} {sig}",
                        Category = NodeCategory.Flow,
                        Description = $"Run script '{name}' {sig}"
                    });
                }
            }

            return entries
                .OrderBy(e => e.Category.ToString())
                .ThenBy(e => e.Display)
                .ToList();
        }
    }

    /// <summary>
    /// Editor fuer eine Filterzeile der Find-Nodes (Razor-Zusatz, FindFilters.cs):
    /// AND/OR-Verknuepfung, NOT, Filtertyp (Katalog des integrierten Assistants),
    /// Wert, optional "Set via input pin". Mutiert den uebergebenen Filter nur
    /// bei OK (Rueckgabe true).
    /// </summary>
    public static class FilterEditDialog
    {
        public static async System.Threading.Tasks.Task<bool> Show(
            Window owner, Assistant.VScripts.Nodes.FindFilter filter, bool forMobiles, bool isFirst)
        {
            var types = forMobiles
                ? Assistant.VScripts.Nodes.FindFilterCatalog.MobileFilters
                : Assistant.VScripts.Nodes.FindFilterCatalog.ItemFilters;

            Window dlg = Ce.Dialog(isFirst ? "Filter" : "Add / Edit Filter",
                330, 260, background: new SolidColorBrush(Color.Parse("#252526")));

            var lightText = new SolidColorBrush(Color.Parse("#DDDDDD"));
            var inputBg = new SolidColorBrush(Color.Parse("#3C3C3C"));
            var border = new SolidColorBrush(Color.Parse("#555555"));

            var root = new StackPanel { Margin = new Avalonia.Thickness(12), Spacing = 8 };

            // Verknuepfung (erst ab dem zweiten Filter relevant)
            var chainRow = new StackPanel
            {
                Orientation = Avalonia.Layout.Orientation.Horizontal,
                Spacing = 8
            };
            var chainBox = new DarkDropDown(new[] { "AND", "OR" }, 80, filter.Or ? 1 : 0)
            {
                IsEnabled = !isFirst
            };
            var negateCheck = new CheckBox
            {
                Content = "NOT (negate)",
                IsChecked = filter.Negate,
                Foreground = lightText
            };
            chainRow.Children.Add(chainBox.Control);
            chainRow.Children.Add(negateCheck);
            root.Children.Add(chainRow);

            // Typ
            int typeIdx = Array.FindIndex(types, t => t.Name == filter.Type);
            var typeBox = new DarkDropDown(types.Select(t => t.Name), 200, typeIdx >= 0 ? typeIdx : 0);
            root.Children.Add(typeBox.Control);

            // Wert (bool-Filter brauchen keinen)
            var valueBox = new TextBox
            {
                Text = filter.Value ?? string.Empty,
                Watermark = "Value (0x hex ok, comma-separated lists)",
                Background = inputBg,
                Foreground = lightText,
                BorderBrush = border,
                CaretBrush = lightText
            };
            root.Children.Add(valueBox);

            var pinCheck = new CheckBox
            {
                Content = "Set via input pin (pin value wins when connected)",
                IsChecked = filter.UsePin,
                Foreground = lightText,
                FontSize = 11
            };
            root.Children.Add(pinCheck);

            void UpdateValueEnabled()
            {
                var t = types[Math.Max(0, typeBox.SelectedIndex)];
                valueBox.IsEnabled = t.NeedsValue;
                if (!t.NeedsValue)
                    valueBox.Watermark = "(no value — use NOT to invert)";
            }

            // Typwechsel leert den alten Wert (er passt fast nie zum neuen Typ).
            typeBox.SelectionChanged += _ =>
            {
                valueBox.Text = string.Empty;
                UpdateValueEnabled();
            };
            UpdateValueEnabled();

            bool result = false;

            var buttons = new StackPanel
            {
                Orientation = Avalonia.Layout.Orientation.Horizontal,
                Spacing = 8,
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right
            };
            var ok = new Button { Content = "OK", Padding = new Avalonia.Thickness(16, 4) };
            ok.Click += (s, e) =>
            {
                filter.Type = types[Math.Max(0, typeBox.SelectedIndex)].Name;
                filter.Value = valueBox.Text?.Trim();
                filter.Negate = negateCheck.IsChecked == true;
                filter.Or = chainBox.SelectedIndex == 1;
                filter.UsePin = pinCheck.IsChecked == true;
                result = true;
                dlg.Close();
            };
            var cancel = new Button { Content = "Cancel", Padding = new Avalonia.Thickness(12, 4) };
            cancel.Click += (s, e) => dlg.Close();
            buttons.Children.Add(ok);
            buttons.Children.Add(cancel);
            root.Children.Add(buttons);

            dlg.KeyDown += (s, e) =>
            {
                if (e.Key == Key.Escape)
                    dlg.Close();
            };

            dlg.Content = root;
            await dlg.ShowDialog(owner);
            return result;
        }
    }

    /// <summary>Einfache Listen-Auswahl (dunkel, Klick = Auswahl); null = Abbruch.</summary>
    public static class ListPickDialog
    {
        public static async Task<string> Show(Window owner, string title, List<string> items)
        {
            Window dlg = Ce.Dialog(title, 320, 380, canResize: true,
                background: new SolidColorBrush(Color.Parse("#252526")));

            var panel = new StackPanel { Spacing = 1 };
            var scroll = new ScrollViewer { Content = panel, Margin = new Thickness(8) };
            var rowHover = new SolidColorBrush(Color.Parse("#2A2D2E"));

            string result = null;

            foreach (string item in items)
            {
                string value = item;
                var row = new Border
                {
                    Background = Brushes.Transparent,
                    CornerRadius = new CornerRadius(3),
                    Padding = new Thickness(8, 4),
                    Child = new TextBlock
                    {
                        Text = value,
                        Foreground = new SolidColorBrush(Color.Parse("#DDDDDD")),
                        FontSize = 12
                    }
                };

                row.PointerEntered += (s, e) => row.Background = rowHover;
                row.PointerExited += (s, e) => row.Background = Brushes.Transparent;
                row.PointerPressed += (s, e) =>
                {
                    result = value;
                    dlg.Close();
                };

                panel.Children.Add(row);
            }

            dlg.KeyDown += (s, e) =>
            {
                if (e.Key == Key.Escape)
                    dlg.Close();
            };

            dlg.Content = scroll;
            await dlg.ShowDialog(owner);
            return result;
        }
    }
}
