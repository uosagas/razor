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

// UOSagas-Razor: Macros-Tab im Razor-CE-Layout (Phase 3b; Funktionalitaet aus 3a).
//
// Blaupause: Razor CE Razor.Designer.cs, macrosTab -> tabControl2 mit den
// Sub-Tabs "Macros" und "Options":
//  * Macros: Filter, macroTree (Ordnerstruktur) links, macroActGroup "Actions"
//    rechts (actionList + Play/Record/Set HK + waitDisp + Loop), unten
//    New.../Remove
//  * Options: Macro Variables (Platzhalter) + gebundene Checkboxen
//
// SAEMTLICHE Kern-Zugriffe (MacroManager/Config/Dateien) laufen ueber
// GameThread.Post; die Anzeige kommt ausschliesslich aus UiSnapshots.

using System;
using System.Collections.Generic;
using System.IO;
using Assistant;
using Assistant.LuaEngine;
using Assistant.Macros;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace Razor.UI
{
    public class MacrosTab : UserControl, ICeTab
    {
        private readonly MainWindow _owner;
        private readonly PropBinder _binder = new PropBinder();

        private readonly TextBox _filter;
        private readonly TreeView _tree;
        private readonly ListBox _actionList;
        private readonly ListBox _varList;
        private readonly ComboBox _varTypes;
        private readonly Button _playButton;
        private readonly Button _recordButton;
        private readonly CheckBox _loopBox;
        private readonly TextBlock _waitDisp;

        private bool _applying;
        private UiSnapshot _lastSnapshot;
        private readonly List<MacroInfo> _treeMacros = new List<MacroInfo>();
        private string _treeFilter = string.Empty;

        // Kopierte Action (CE _macroActionToCopy). Die Referenz wird NUR auf dem
        // Game-Thread angefasst; das UI merkt sich lediglich, OB etwas kopiert
        // wurde (fuer das Paste-Enable im Kontextmenue).
        private static MacroAction s_CopiedAction;
        private bool _hasCopiedLine;

        /// <summary>Dateiname des aktuell ausgewaehlten Macros (fuer Snapshot-Anfragen).</summary>
        internal string SelectedMacroFile => (_tree?.SelectedItem as TreeViewItem)?.Tag as string;

        public MacrosTab(MainWindow owner)
        {
            _owner = owner;

            var root = Ce.Panel();
            TabControl sub = Ce.SubTabs(root, 6, 3, 510, 314);

            // ================= Macros =================
            var page = Ce.Panel(502, 286);
            Ce.Tab(sub, "Macros", page);

            Ce.Label(page, "Filter:", 5, 10, 36, 15);
            _filter = Ce.Text(page, 47, 7, 147, 23);
            _filter.TextChanged += (s, e) =>
            {
                if (_applying)
                    return;

                _treeFilter = _filter.Text?.Trim() ?? string.Empty;
                RebuildTree();
            };

            _tree = new TreeView();
            Ce.At(page, _tree, 6, 36, 188, 171);
            _tree.SelectionChanged += (s, e) =>
            {
                if (!_applying)
                    _owner.RequestSnapshot();
            };

            Ce.Button(page, "New...", 8, 213, 74, 30, OnNew);
            Ce.Button(page, "Remove", 120, 213, 74, 30, OnDelete);

            Canvas grp = Ce.Group(page, "Actions", 200, 3, 296, 240);
            _actionList = Ce.List(grp, 6, 17, 218, 217);

            // Kontextmenue + Tastatur + Doppelklick-Edit wie Razor CE
            // (actionList_MouseDown/actionList_KeyDown in Razor.cs).
            _actionList.AddHandler(InputElement.PointerPressedEvent, OnActionListPointerPressed,
                RoutingStrategies.Tunnel);
            _actionList.DoubleTapped += (s, e) => EditSelectedAction();
            _actionList.KeyDown += OnActionListKeyDown;
            _playButton = Ce.Button(grp, "Play", 230, 17, 60, 33, OnPlayStop);
            _recordButton = Ce.Button(grp, "Record", 230, 56, 60, 33, OnRecordStop);
            Ce.Button(grp, "Set HK", 230, 95, 60, 33, null); // Phase 3c
            _waitDisp = Ce.Label(grp, "", 230, 132, 60, 74);
            _waitDisp.TextAlignment = TextAlignment.Center;
            _waitDisp.Foreground = Ce.GrayText;
            _loopBox = Ce.Check(grp, "Loop", 233, 210, 57, 24);
            _loopBox.IsCheckedChanged += (s, e) => OnLoopToggled();

            // ================= Options =================
            var opts = Ce.Panel(502, 288);
            Ce.Tab(sub, "Options", opts);

            Canvas grpVars = Ce.Group(opts, "Macro Variables:", 6, 6, 240, 271);
            Ce.Button(grpVars, "Insert as...", 6, 22, 67, 25, OnInsertVariable);
            _varTypes = Ce.Combo(grpVars, 79, 22, 153, 23,
                "Absolute Target", "DoubleClick Target", "Set Macro Variable Action");
            _varTypes.SelectedIndex = 0;
            Ce.Button(grpVars, "Add", 6, 53, 67, 25, OnAddVariable);
            Ce.Button(grpVars, "Retarget", 6, 84, 67, 25, OnRetargetVariable);
            Ce.Button(grpVars, "Remove", 6, 115, 67, 25, OnRemoveVariable);
            _varList = Ce.List(grpVars, 79, 53, 153, 199);

            // Liste folgt dem Core (Changed feuert auf dem Game-Thread — auch
            // beim Profil-Load und nach Set-Macro-Variable-Actions).
            MacroVariables.Changed += () => Avalonia.Threading.Dispatcher.UIThread.Post(RefreshVariables);
            RefreshVariables();

            _binder.Check(Ce.Check(opts, "Force different 'TargetByType'", 272, 28, 181, 19),
                "DiffTargetByType");
            _binder.Check(Ce.Check(opts, "Range check on 'TargetByType'", 272, 53, 188, 19),
                "RangeCheckTargetByType");
            _binder.Check(Ce.Check(opts, "Range check on 'DoubleClickType'", 272, 78, 207, 19),
                "RangeCheckDoubleClick");
            _binder.Check(Ce.Check(opts, "Step Through Macro", 272, 118, 134, 19), "StepThroughMacro");
            Ce.Button(opts, "Next", 412, 115, 60, 23, null);
            _binder.Check(Ce.Check(opts, "Default macro action delay (50ms)", 272, 158, 207, 19),
                "MacroActionDelay");
            _binder.Check(Ce.Check(opts, "Disable Playing/Finished Message", 272, 183, 204, 19),
                "DisableMacroPlayFinish");

            Content = root;
        }

        // --- Kommandos (alle via GameThread.Post) -----------------------------

        private void OnPlayStop()
        {
            bool playing = _lastSnapshot?.Playing == true;
            string file = SelectedMacroFile;

            if (playing)
            {
                GameThread.Post(() => MacroManager.Stop());
            }
            else if (file != null)
            {
                GameThread.Post(() =>
                {
                    Macro m = UiSnapshotBuilder.FindMacro(file);
                    if (m != null)
                        MacroManager.Play(m);
                });
            }

            _owner.RequestSnapshot();
        }

        private void OnRecordStop()
        {
            bool recording = _lastSnapshot?.Recording == true;
            string file = SelectedMacroFile;

            if (recording)
            {
                GameThread.Post(() =>
                {
                    Macro recorded = MacroManager.Current;
                    MacroManager.Stop();

                    if (recorded != null)
                    {
                        recorded.Save();
                        if (!MacroManager.List.Contains(recorded))
                            MacroManager.Add(recorded);
                    }
                });
            }
            else if (file != null)
            {
                GameThread.Post(() =>
                {
                    Macro m = UiSnapshotBuilder.FindMacro(file);
                    if (m != null)
                        MacroManager.Record(m); // wie Razor CE: Record ersetzt den Inhalt
                });
            }

            _owner.RequestSnapshot();
        }

        private void OnLoopToggled()
        {
            if (_applying)
                return;

            string file = SelectedMacroFile;
            if (file == null)
                return;

            bool loop = _loopBox.IsChecked == true;
            GameThread.Post(() =>
            {
                Macro m = UiSnapshotBuilder.FindMacro(file);
                if (m == null)
                    return;

                m.Loop = loop;

                // Persistieren (Macro.Save schreibt "!Loop" mit; schreibt nur,
                // wenn Aktionen vorhanden sind — leere Macros bleiben leer).
                if (m.Loaded && !m.Recording && !m.Playing)
                    m.Save();
            });
        }

        private async void OnNew()
        {
            string name = await InputBox.Show(_owner, "Enter a name for the new macro:", "New Macro");
            if (string.IsNullOrEmpty(name))
                return;

            foreach (char c in Path.GetInvalidFileNameChars())
                name = name.Replace(c, '_');

            GameThread.Post(() =>
            {
                string path = Path.Combine(Config.GetUserDirectory("Macros"), $"{name}.macro");
                if (UiSnapshotBuilder.FindMacro(path) != null)
                {
                    Console.WriteLine($"[UOSagas Razor] Macro existiert bereits: {name}");
                    return;
                }

                try
                {
                    if (!File.Exists(path))
                        File.WriteAllText(path, string.Empty); // leere Datei (Macro.Save schreibt leere nicht)
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[UOSagas Razor] Macro-Datei konnte nicht angelegt werden: {ex.Message}");
                    return;
                }

                var m = new Macro(path);
                m.Load();
                MacroManager.Add(m);
            });

            _owner.RequestSnapshot();
        }

        private async void OnDelete()
        {
            string file = SelectedMacroFile;
            if (file == null)
                return;

            if (!await Dialogs.Confirm(_owner, "Remove Macro",
                    $"Remove macro '{System.IO.Path.GetFileNameWithoutExtension(file)}'? This cannot be undone."))
                return;

            GameThread.Post(() =>
            {
                Macro m = UiSnapshotBuilder.FindMacro(file);
                if (m == null)
                    return;

                if (MacroManager.Current == m)
                    MacroManager.Stop();

                MacroManager.Remove(m);

                try
                {
                    File.Delete(m.Filename);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[UOSagas Razor] Macro-Datei konnte nicht geloescht werden: {ex.Message}");
                }
            });

            _owner.RequestSnapshot();
        }

        // --- Macro Variables (CE: Macros -> Options) ----------------------------

        /// <summary>Variablen-Liste vom Game-Thread kopieren und anzeigen.</summary>
        private void RefreshVariables()
        {
            GameThread.Post(() =>
            {
                var copy = new List<KeyValuePair<string, string>>();
                foreach (MacroVariables.MacroVariable mV in MacroVariables.MacroVariableList)
                    copy.Add(new KeyValuePair<string, string>(mV.Name, mV.TargetInfo.Serial.ToString()));

                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    string selected = (_varList.SelectedItem as ListBoxItem)?.Tag as string;
                    _varList.Items.Clear();

                    foreach (KeyValuePair<string, string> kv in copy)
                    {
                        var item = new ListBoxItem
                        {
                            Content = $"${kv.Key} — {kv.Value}",
                            Tag = kv.Key
                        };
                        _varList.Items.Add(item);

                        if (kv.Key == selected)
                            _varList.SelectedItem = item;
                    }
                });
            });
        }

        private string SelectedVariable => (_varList.SelectedItem as ListBoxItem)?.Tag as string;

        private async void OnAddVariable()
        {
            string name = await Dialogs.Prompt(_owner, "New Macro Variable", "Variable name:");
            if (string.IsNullOrWhiteSpace(name))
                return;

            string clean = name.Trim();

            GameThread.Post(() =>
            {
                if (World.Player == null)
                    return;

                World.Player.SendMessage(MsgLevel.Force, $"Select target for ${clean}");

                Targeting.OneTimeTarget((ground, serial, pt, gfx) =>
                {
                    MacroVariables.AddOrUpdate(clean, new TargetInfo
                    {
                        Gfx = gfx,
                        Serial = serial,
                        Type = (byte) (ground ? 1 : 0),
                        X = pt.X,
                        Y = pt.Y,
                        Z = pt.Z
                    });

                    World.Player?.SendMessage(MsgLevel.Force, $"Macro variable '{clean}' set to '{serial}'");
                });
            });
        }

        private void OnRetargetVariable()
        {
            string name = SelectedVariable;
            if (name == null)
                return;

            GameThread.Post(() => MacroVariables.Find(name)?.TargetSetMacroVariable());
        }

        private async void OnRemoveVariable()
        {
            string name = SelectedVariable;
            if (name == null)
                return;

            if (!await Dialogs.Confirm(_owner, "Remove Macro Variable",
                    $"Remove variable '${name}'?", "Remove"))
                return;

            GameThread.Post(() => MacroVariables.Remove(name));
        }

        /// <summary>Fuegt die gewaehlte Variable als Action ins ausgewaehlte Macro ein
        /// (hinter der markierten Action, sonst ans Ende) und speichert die Datei.</summary>
        private void OnInsertVariable()
        {
            string name = SelectedVariable;
            string file = SelectedMacroFile;
            int type = _varTypes.SelectedIndex;

            if (name == null || file == null)
                return;

            int insertAt = _actionList.SelectedIndex >= 0 ? _actionList.SelectedIndex + 1 : -1;

            GameThread.Post(() =>
            {
                Macro m = UiSnapshotBuilder.FindMacro(file);
                if (m == null)
                    return;

                MacroAction action = type switch
                {
                    1 => new DoubleClickVariableAction(new[] { name }),
                    2 => new SetMacroVariableTargetAction(name),
                    _ => new AbsoluteTargetVariableAction(new[] { name })
                };

                m.Insert(insertAt, action);
                m.Save();
            });

            _owner.RequestSnapshot();
        }

        // --- Action-Kontextmenue (CE: actionList_MouseDown) ---------------------

        /// <summary>true, solange keine Bearbeitung erlaubt ist (CE sperrt das
        /// Menue waehrend Play/Record komplett).</summary>
        private bool ActionEditLocked =>
            _lastSnapshot?.Playing == true || _lastSnapshot?.Recording == true;

        private void OnActionListPointerPressed(object sender, PointerPressedEventArgs e)
        {
            if (!e.GetCurrentPoint(_actionList).Properties.IsRightButtonPressed)
                return;

            // Item unter dem Cursor selektieren (Avalonia selektiert bei
            // Rechtsklick nicht von selbst; CE arbeitet auf der Selektion).
            if (e.Source is Avalonia.Visual v)
            {
                var container = v.FindAncestorOfType<ListBoxItem>(true);
                if (container != null)
                {
                    int i = _actionList.IndexFromContainer(container);
                    if (i >= 0)
                        _actionList.SelectedIndex = i;
                }
            }

            e.Handled = true;
            OpenActionContextMenu();
        }

        private void OnActionListKeyDown(object sender, KeyEventArgs e)
        {
            if (ActionEditLocked)
                return;

            if (e.Key == Key.Delete)
            {
                RemoveSelectedAction();
                e.Handled = true;
            }
            else if (e.Key == Key.Up && e.KeyModifiers.HasFlag(KeyModifiers.Control))
            {
                MoveSelectedAction(-1);
                e.Handled = true;
            }
            else if (e.Key == Key.Down && e.KeyModifiers.HasFlag(KeyModifiers.Control))
            {
                MoveSelectedAction(1);
                e.Handled = true;
            }
        }

        private void OpenActionContextMenu()
        {
            if (ActionEditLocked)
                return;

            string file = SelectedMacroFile;
            if (file == null)
                return;

            int idx = _actionList.SelectedIndex;
            int count = _actionList.Items.Count;

            if (idx < 0 || idx >= count)
            {
                ShowActionContextMenu(file, idx, count, null);
                return;
            }

            // Die Action-Instanz bestimmt die CE-typischen Zusatz-Menuepunkte
            // (Edit Timeout, Re-Target, Convert To ...). Referenz wie beim
            // Doppelklick-Edit nur zum LESEN an den UI-Thread reichen; jede
            // Mutation laeuft ueber den Game-Thread mit ReferenceEquals-Guard.
            GameThread.Post(() =>
            {
                Macro m = UiSnapshotBuilder.FindMacro(file);
                MacroAction a = m != null && idx < m.Actions.Count ? (MacroAction) m.Actions[idx] : null;
                Avalonia.Threading.Dispatcher.UIThread.Post(() => ShowActionContextMenu(file, idx, count, a));
            });
        }

        private void ShowActionContextMenu(string file, int idx, int count, MacroAction sel)
        {
            bool hasSel = idx >= 0 && idx < count;

            var flyout = new MenuFlyout();

            MenuItem Item(ItemCollection items, string header, Action onClick, bool enabled = true)
            {
                var mi = new MenuItem { Header = header, IsEnabled = enabled };
                mi.Click += (s, e) => onClick();
                items.Add(mi);
                return mi;
            }

            // --- Aktionsspezifische Eintraege ZUERST (CE: GetContextMenuItems
            //     der selektierten Action steht oben im Menue) -----------------
            int before = flyout.Items.Count;
            switch (sel)
            {
                case DoubleClickAction d:
                    Item(flyout.Items, "Re-Target", () => OnReTargetAction(file, idx, sel));
                    Item(flyout.Items, "Convert To DClick By Type",
                        () => ReplaceAt(file, idx, sel, () => new DoubleClickTypeAction(d.Gfx, true)),
                        d.Gfx != 0 && d.Serial.IsItem);
                    break;

                case DoubleClickTypeAction:
                    Item(flyout.Items, "Re-Target", () => OnReTargetAction(file, idx, sel));
                    break;

                case LiftAction l:
                    Item(flyout.Items, "Re-Target", () => OnReTargetAction(file, idx, sel));
                    Item(flyout.Items, "Convert To Lift By Type",
                        () => ReplaceAt(file, idx, sel, () => new LiftTypeAction(l.Gfx, l.Amount)),
                        l.Gfx != 0);
                    Item(flyout.Items, "Edit Amount…", () => OnEditAmount(file, idx, sel, l.Amount));
                    break;

                case LiftTypeAction lt:
                    Item(flyout.Items, "Re-Target", () => OnReTargetAction(file, idx, sel));
                    Item(flyout.Items, "Edit Amount…", () => OnEditAmount(file, idx, sel, lt.Amount));
                    break;

                case DropAction dr:
                    // CE: nur Boden-Drops lassen sich in Relativkoordinaten umwandeln.
                    Item(flyout.Items, "Convert To Relative Location",
                        () => ReplaceAt(file, idx, sel, () => new DropRelLocAction(
                            (sbyte) (dr.At.X - World.Player.Position.X),
                            (sbyte) (dr.At.Y - World.Player.Position.Y),
                            (sbyte) (dr.At.Z - World.Player.Position.Z))),
                        !dr.To.IsValid);
                    break;

                case AbsoluteTargetAction at:
                    Item(flyout.Items, "Re-Target", () => OnReTargetAction(file, idx, sel));
                    Item(flyout.Items, "Convert To Last Target",
                        () => ReplaceAt(file, idx, sel, () => new LastTargetAction()));
                    Item(flyout.Items, "Convert To Target By Type",
                        () => ReplaceAt(file, idx, sel,
                            () => new TargetTypeAction(at.Info.Serial.IsMobile, at.Info.Gfx)));
                    Item(flyout.Items, "Convert To Relative Location",
                        () => ReplaceAt(file, idx, sel, () => new TargetRelLocAction(
                            (sbyte) (at.Info.X - World.Player.Position.X),
                            (sbyte) (at.Info.Y - World.Player.Position.Y))));
                    break;

                case TargetTypeAction:
                    Item(flyout.Items, "Re-Target", () => OnReTargetAction(file, idx, sel));
                    Item(flyout.Items, "Convert To Last Target",
                        () => ReplaceAt(file, idx, sel, () => new LastTargetAction()));
                    break;

                case TargetRelLocAction:
                    Item(flyout.Items, "Re-Target", () => OnReTargetAction(file, idx, sel));
                    break;

                case GumpResponseAction g:
                    Item(flyout.Items, "Use Last Gump Response", () => MutateAt(file, idx, sel, a =>
                    {
                        if (((GumpResponseAction) a).UseLastResponse())
                            World.Player?.SendMessage(MsgLevel.Force, "Set GumpResponse to last response");
                        else
                            World.Player?.SendMessage(MsgLevel.Warning, "No gump response recorded yet");
                    }));
                    Item(flyout.Items, "Edit Button ID…", () => OnEditGumpButton(file, idx, sel, g.ButtonID));
                    break;

                case OverheadMessageAction om:
                    Item(flyout.Items, "Set Hue…", () => OnSetOverheadHue(file, idx, sel, om));
                    break;
            }

            // Edit Timeout — genau die Wait-Actions, denen CE den Menuepunkt gibt
            // (Pause hat stattdessen Edit; Lift/Dress/Walk warten nur intern).
            if (sel is WaitForTargetAction or WaitForGumpAction or WaitForMenuAction
                or WaitForStatAction or WaitForPromptAction)
                Item(flyout.Items, "Edit Timeout…", () => OnEditTimeout(file, idx, (MacroWaitAction) sel));

            if (flyout.Items.Count > before)
                flyout.Items.Add(new Separator());

            Item(flyout.Items, "Reload", () => PostMacro(file, m =>
            {
                m.Load();
            }));
            Item(flyout.Items, "Save", () => PostMacro(file, m => m.Save()));
            flyout.Items.Add(new Separator());

            if (count > 1)
            {
                Item(flyout.Items, "Move Up", () => MoveSelectedAction(-1), hasSel && idx > 0);
                Item(flyout.Items, "Move Down", () => MoveSelectedAction(1), hasSel && idx < count - 1);
                flyout.Items.Add(new Separator());
            }

            Item(flyout.Items, "Copy Line", CopySelectedAction, hasSel);
            Item(flyout.Items, "Paste Line", PasteCopiedAction, hasSel && _hasCopiedLine);
            Item(flyout.Items, "Remove Action", RemoveSelectedAction, hasSel);
            flyout.Items.Add(new Separator());

            Item(flyout.Items, "Begin Recording Here", () => PlayOrRecordFromHere(record: true), hasSel);
            Item(flyout.Items, "Play From Here", () => PlayOrRecordFromHere(record: false), hasSel);
            Item(flyout.Items, "Edit…", EditSelectedAction, hasSel);
            flyout.Items.Add(new Separator());

            // --- Insert Special Construct (CE: "Special Constructs"-Submenue) ---
            var special = new MenuItem { Header = "Insert Special Construct" };

            Item(special.Items, "Wait / Pause…", OnInsertWait);
            Item(special.Items, "Set Last Target", () => InsertAt(file, idx, () => new SetLastTargetAction()));
            Item(special.Items, "Comment…", OnInsertComment);
            Item(special.Items, "Overhead Message…", OnInsertOverhead);
            Item(special.Items, "Wait for Target", () => InsertAt(file, idx, () => new WaitForTargetAction()));
            Item(special.Items, "Clear System Messages", () => InsertAt(file, idx, () => new ClearSysMessages()));
            special.Items.Add(new Separator());
            Item(special.Items, "If…", () => OnInsertCondition(0));
            Item(special.Items, "Else", () => InsertAt(file, idx, () => new ElseAction()));
            Item(special.Items, "End If", () => InsertAt(file, idx, () => new EndIfAction()));
            special.Items.Add(new Separator());
            Item(special.Items, "For…", OnInsertFor);
            Item(special.Items, "End For", () => InsertAt(file, idx, () => new EndForAction()));
            special.Items.Add(new Separator());
            Item(special.Items, "While…", () => OnInsertCondition(1));
            Item(special.Items, "End While", () => InsertAt(file, idx, () => new EndWhileAction()));
            special.Items.Add(new Separator());
            Item(special.Items, "Do", () => InsertAt(file, idx, () => new StartDoWhileAction()));
            Item(special.Items, "Do While…", () => OnInsertCondition(2));

            flyout.Items.Add(special);

            // --- Convert To (Sagas-Zusatz: Macro -> Script; CE kann nur Razor) ---
            var convert = new MenuItem { Header = "Convert To" };
            Item(convert.Items, "Razor Script", () => ConvertMacro(file, ConvertTarget.Razor));
            Item(convert.Items, "Lua Script", () => ConvertMacro(file, ConvertTarget.Lua));
            Item(convert.Items, "VScript", () => ConvertMacro(file, ConvertTarget.VScript));
            flyout.Items.Add(convert);

            flyout.ShowAt(_actionList, true);
        }

        /// <summary>Macro auf dem Game-Thread holen, mutieren, Snapshot anfordern.</summary>
        private void PostMacro(string file, Action<Macro> action)
        {
            GameThread.Post(() =>
            {
                Macro m = UiSnapshotBuilder.FindMacro(file);
                if (m != null)
                    action(m);
            });

            _owner.RequestSnapshot();
        }

        private void MoveSelectedAction(int delta)
        {
            string file = SelectedMacroFile;
            int idx = _actionList.SelectedIndex;
            int target = idx + delta;

            if (file == null || idx < 0 || target < 0 || target >= _actionList.Items.Count)
                return;

            PostMacro(file, m =>
            {
                if (idx >= m.Actions.Count || target >= m.Actions.Count)
                    return;

                object tmp = m.Actions[target];
                m.Actions[target] = m.Actions[idx];
                m.Actions[idx] = tmp;
                m.Save();
            });

            _actionList.SelectedIndex = target; // Selektion folgt der Action (CE)
        }

        private void CopySelectedAction()
        {
            string file = SelectedMacroFile;
            int idx = _actionList.SelectedIndex;
            if (file == null || idx < 0)
                return;

            GameThread.Post(() =>
            {
                Macro m = UiSnapshotBuilder.FindMacro(file);
                if (m != null && idx < m.Actions.Count)
                    s_CopiedAction = (MacroAction) m.Actions[idx];
            });

            _hasCopiedLine = true;
        }

        private void PasteCopiedAction()
        {
            string file = SelectedMacroFile;
            int idx = _actionList.SelectedIndex;
            if (file == null || idx < 0)
                return;

            PostMacro(file, m =>
            {
                if (s_CopiedAction == null || idx >= m.Actions.Count)
                    return;

                // Wie CE (Paste + Save + Reload): der Datei-Roundtrip macht aus
                // der eingefuegten Referenz eine eigenstaendige Instanz.
                m.Insert(idx + 1, s_CopiedAction);
                m.Save();
                m.Load();
            });
        }

        private async void RemoveSelectedAction()
        {
            string file = SelectedMacroFile;
            int idx = _actionList.SelectedIndex;
            if (file == null || idx < 0 || idx >= _actionList.Items.Count)
                return;

            string display = _actionList.Items[idx] as string ?? "this action";
            if (!await Dialogs.Confirm(_owner, "Remove Action", $"Remove '{display}'?", "Remove"))
                return;

            PostMacro(file, m =>
            {
                if (idx >= m.Actions.Count)
                    return;

                m.Actions.RemoveAt(idx);

                if (m.Actions.Count == 0)
                {
                    // Macro.Save() schreibt leere Macros nicht — Datei selbst
                    // leeren, sonst kommt die Action beim Reload wieder.
                    try
                    {
                        File.WriteAllText(m.Filename, m.Loop ? "!Loop" + Environment.NewLine : string.Empty);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[UOSagas Razor] Macro-Datei konnte nicht geleert werden: {ex.Message}");
                    }
                }
                else
                {
                    m.Save();
                }
            });
        }

        /// <summary>CE onMacroBegRecHere/onMacroPlayHere: ab der Zeile NACH der
        /// Selektion aufnehmen bzw. abspielen.</summary>
        private void PlayOrRecordFromHere(bool record)
        {
            string file = SelectedMacroFile;
            int idx = _actionList.SelectedIndex;
            if (file == null)
                return;

            GameThread.Post(() =>
            {
                if (World.Player == null)
                    return;

                Macro m = UiSnapshotBuilder.FindMacro(file);
                if (m == null)
                    return;

                int sel = idx + 1;
                if (sel < 0 || sel > m.Actions.Count)
                    sel = m.Actions.Count;

                if (record)
                    MacroManager.RecordAt(m, sel);
                else
                    MacroManager.PlayAt(m, sel);
            });

            _owner.RequestSnapshot();
        }

        // --- Insert Special Construct -------------------------------------------

        /// <summary>Action hinter der Selektion einfuegen (Selektion -1 = oben,
        /// CE-Verhalten); factory laeuft auf dem Game-Thread.</summary>
        private void InsertAt(string file, int at, Func<MacroAction> factory)
        {
            GameThread.Post(() =>
            {
                Macro m = UiSnapshotBuilder.FindMacro(file);
                if (m == null || at >= m.Actions.Count)
                    return;

                MacroAction a;
                try
                {
                    a = factory();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[UOSagas Razor] Macro-Action konnte nicht erstellt werden: {ex.Message}");
                    return;
                }

                if (a == null)
                    return;

                m.Insert(at + 1, a);
                m.Save();
            });

            _owner.RequestSnapshot();
        }

        private async void OnInsertWait()
        {
            string file = SelectedMacroFile;
            if (file == null)
                return;

            int at = _actionList.SelectedIndex;
            MacroWaitResult res = await MacroWaitDialog.Show(_owner);
            if (res != null)
                InsertAt(file, at, () => BuildWaitAction(res));
        }

        private async void OnInsertComment()
        {
            string file = SelectedMacroFile;
            if (file == null)
                return;

            int at = _actionList.SelectedIndex;
            string text = await Dialogs.Prompt(_owner, "Insert Comment", "Comment:");
            if (text != null)
                InsertAt(file, at, () => new MacroComment(text));
        }

        private async void OnInsertOverhead()
        {
            string file = SelectedMacroFile;
            if (file == null)
                return;

            int at = _actionList.SelectedIndex;
            string text = await Dialogs.Prompt(_owner, "Insert Overhead Message", "Message:");
            if (!string.IsNullOrWhiteSpace(text))
                InsertAt(file, at, () => new OverheadMessageAction((ushort) Config.GetInt("SysColor"), text.Trim()));
        }

        private async void OnInsertFor()
        {
            string file = SelectedMacroFile;
            if (file == null)
                return;

            int at = _actionList.SelectedIndex;
            string text = await Dialogs.Prompt(_owner, "Insert For", "Number of iterations:", "1");
            if (text != null && int.TryParse(text.Trim(), out int n) && n > 0)
                InsertAt(file, at, () => new ForAction(n));
        }

        /// <summary>kind: 0=If, 1=While, 2=DoWhile.</summary>
        private void OnInsertCondition(int kind)
        {
            string file = SelectedMacroFile;
            if (file == null)
                return;

            int at = _actionList.SelectedIndex;
            string title = kind switch { 1 => "Insert While", 2 => "Insert Do While", _ => "Insert If" };

            ShowConditionDialog(title, null, res => InsertAt(file, at, () => BuildConditionAction(kind, res)));
        }

        /// <summary>Counter-/Skill-Listen vom Game-Thread holen, dann den
        /// Bedingungs-Dialog auf dem UI-Thread zeigen.</summary>
        private void ShowConditionDialog(string title, MacroCondResult preset, Action<MacroCondResult> onDone)
        {
            GameThread.Post(() =>
            {
                var counters = new List<string>();
                foreach (Counter c in Counter.List)
                    counters.Add(c.Name);

                var skills = new List<string>();
                int skillCount = Ultima.Skills.SkillsByIndex.Count;
                for (int i = 0; i < skillCount; i++)
                    skills.Add(Ultima.Skills.GetSkillDisplayName(i) ?? $"Skill {i}");

                Avalonia.Threading.Dispatcher.UIThread.Post(async () =>
                {
                    MacroCondResult res = await MacroConditionDialog.Show(_owner, title, counters, skills, preset);
                    if (res != null)
                        onDone(res);
                });
            });
        }

        private static MacroAction BuildWaitAction(MacroWaitResult res)
        {
            switch (res.Kind)
            {
                case MacroWaitResult.KindGump:
                    return new WaitForGumpAction();
                case MacroWaitResult.KindMenu:
                    return new WaitForMenuAction(0);
                case MacroWaitResult.KindTarget:
                    return new WaitForTargetAction();
                case MacroWaitResult.KindStat:
                    return new WaitForStatAction((IfAction.IfVarType) res.Stat,
                        (byte) (res.GreaterEq ? 1 : 0), res.Value);
                default:
                    return new PauseAction(res.PauseMs);
            }
        }

        /// <summary>kind: 0=If, 1=While, 2=DoWhile — gleiche Var-Codes (0-8/50/100).</summary>
        private static MacroAction BuildConditionAction(int kind, MacroCondResult c)
        {
            bool isBool = c.Var is 3 or 6 or 7 or 8;

            switch (kind)
            {
                case 1:
                {
                    var v = (WhileAction.WhileVarType) c.Var;
                    if (c.Var == 4)
                        return new WhileAction(v, c.Text ?? string.Empty);
                    if (c.Var == 50)
                        return new WhileAction(v, c.Op, c.Number, c.Counter);
                    if (c.Var == 100)
                        return new WhileAction(v, c.Op, c.SkillValue, c.SkillId);
                    return new WhileAction(v, c.Op, isBool ? 0 : c.Number);
                }
                case 2:
                {
                    var v = (DoWhileAction.DoWhileVarType) c.Var;
                    if (c.Var == 4)
                        return new DoWhileAction(v, c.Text ?? string.Empty);
                    if (c.Var == 50)
                        return new DoWhileAction(v, c.Op, c.Number, c.Counter);
                    if (c.Var == 100)
                        return new DoWhileAction(v, c.Op, c.SkillValue, c.SkillId);
                    return new DoWhileAction(v, c.Op, isBool ? 0 : c.Number);
                }
                default:
                {
                    var v = (IfAction.IfVarType) c.Var;
                    if (c.Var == 4)
                        return new IfAction(v, c.Text ?? string.Empty);
                    if (c.Var == 50)
                        return new IfAction(v, c.Op, c.Number, c.Counter);
                    if (c.Var == 100)
                        return new IfAction(v, c.Op, c.SkillValue, c.SkillId);
                    return new IfAction(v, c.Op, isBool ? 0 : c.Number);
                }
            }
        }

        // --- Doppelklick-Edit (CE: actionList_MouseDown, e.Clicks == 2) ---------

        private void EditSelectedAction()
        {
            string file = SelectedMacroFile;
            int idx = _actionList.SelectedIndex;
            if (file == null || idx < 0 || ActionEditLocked)
                return;

            GameThread.Post(() =>
            {
                Macro m = UiSnapshotBuilder.FindMacro(file);
                if (m == null || idx >= m.Actions.Count)
                    return;

                var a = (MacroAction) m.Actions[idx];

                // Die Referenz wird nur zum LESEN der (nach Konstruktion
                // unveraenderlichen) Felder an den UI-Thread gereicht; der
                // Commit laeuft wieder ueber den Game-Thread und prueft, dass
                // die Action noch an ihrem Platz ist.
                Avalonia.Threading.Dispatcher.UIThread.Post(() => EditActionOnUi(file, idx, a));
            });
        }

        private async void EditActionOnUi(string file, int idx, MacroAction a)
        {
            switch (a)
            {
                case PauseAction p:
                {
                    MacroWaitResult res = await MacroWaitDialog.Show(_owner, new MacroWaitResult
                    {
                        Kind = MacroWaitResult.KindPause,
                        PauseMs = (int) p.Timeout.TotalMilliseconds
                    });
                    if (res != null)
                        ReplaceAt(file, idx, a, () => BuildWaitAction(res));
                    break;
                }

                case WaitForStatAction ws:
                {
                    MacroWaitResult res = await MacroWaitDialog.Show(_owner, new MacroWaitResult
                    {
                        Kind = MacroWaitResult.KindStat,
                        Stat = (int) ws.Stat,
                        GreaterEq = ws.Op > 0,
                        Value = ws.Amount
                    });
                    if (res != null)
                        ReplaceAt(file, idx, a, () => BuildWaitAction(res));
                    break;
                }

                case WaitForGumpAction:
                case WaitForMenuAction:
                case WaitForTargetAction:
                {
                    int kind = a is WaitForGumpAction ? MacroWaitResult.KindGump
                        : a is WaitForMenuAction ? MacroWaitResult.KindMenu
                        : MacroWaitResult.KindTarget;
                    MacroWaitResult res = await MacroWaitDialog.Show(_owner, new MacroWaitResult { Kind = kind });
                    if (res != null)
                        ReplaceAt(file, idx, a, () => BuildWaitAction(res));
                    break;
                }

                case MacroComment c:
                {
                    string text = await Dialogs.Prompt(_owner, "Edit Comment", "Comment:", c.Comment ?? "");
                    if (text != null)
                        ReplaceAt(file, idx, a, () => new MacroComment(text));
                    break;
                }

                case SpeechAction sp:
                {
                    string text = await Dialogs.Prompt(_owner, "Edit Speech", "Text:", sp.Speech ?? "");
                    if (!string.IsNullOrWhiteSpace(text))
                        ReplaceAt(file, idx, a,
                            () => new SpeechAction(sp.Type, sp.Hue, sp.Font, sp.Lang, sp.Keywords, text.Trim()));
                    break;
                }

                case OverheadMessageAction om:
                {
                    string text = await Dialogs.Prompt(_owner, "Edit Overhead Message", "Message:", om.Message ?? "");
                    if (!string.IsNullOrWhiteSpace(text))
                        ReplaceAt(file, idx, a, () => new OverheadMessageAction(om.Hue, text.Trim()));
                    break;
                }

                case ForAction f:
                {
                    string text = await Dialogs.Prompt(_owner, "Edit For", "Number of iterations:", f.Max.ToString());
                    if (text != null && int.TryParse(text.Trim(), out int n) && n > 0)
                        ReplaceAt(file, idx, a, () => new ForAction(n));
                    break;
                }

                case IfAction ia:
                    ShowConditionDialog("Edit If",
                        CondPreset((int) ia.Variable, ia.Op, ia.Value, ia.Counter, ia.SkillId),
                        res => ReplaceAt(file, idx, a, () => BuildConditionAction(0, res)));
                    break;

                case WhileAction wa:
                    ShowConditionDialog("Edit While",
                        CondPreset((int) wa.Variable, wa.Op, wa.Value, wa.Counter, wa.SkillId),
                        res => ReplaceAt(file, idx, a, () => BuildConditionAction(1, res)));
                    break;

                case DoWhileAction da:
                    ShowConditionDialog("Edit Do While",
                        CondPreset((int) da.Variable, da.Op, da.Value, da.Counter, da.SkillId),
                        res => ReplaceAt(file, idx, a, () => BuildConditionAction(2, res)));
                    break;

                // Alle anderen Typen haben (wie in CE) keinen Edit-Dialog.
            }
        }

        private static MacroCondResult CondPreset(int var, sbyte op, object value, string counter, int skillId)
        {
            var r = new MacroCondResult
            {
                Var = var,
                Op = op >= 0 && op <= 3 ? op : (sbyte) 0,
                Counter = counter,
                SkillId = skillId
            };

            try
            {
                if (var == 4)
                    r.Text = value as string ?? string.Empty;
                else if (var == 100)
                    r.SkillValue = Convert.ToDouble(value, System.Globalization.CultureInfo.InvariantCulture);
                else
                    r.Number = Convert.ToInt32(value, System.Globalization.CultureInfo.InvariantCulture);
            }
            catch
            {
                // Wert bleibt beim Default — der Dialog zeigt dann 0 an.
            }

            return r;
        }

        /// <summary>Action an Position idx ersetzen — nur wenn sie noch dieselbe
        /// Instanz ist (Schutz gegen parallele Aenderungen).</summary>
        private void ReplaceAt(string file, int idx, MacroAction oldAction, Func<MacroAction> factory)
        {
            GameThread.Post(() =>
            {
                Macro m = UiSnapshotBuilder.FindMacro(file);
                if (m == null || idx >= m.Actions.Count || !ReferenceEquals(m.Actions[idx], oldAction))
                    return;

                MacroAction a;
                try
                {
                    a = factory();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[UOSagas Razor] Macro-Action konnte nicht ersetzt werden: {ex.Message}");
                    return;
                }

                if (a == null)
                    return;

                m.Convert(oldAction, a);
                m.Save();
            });
        }

        /// <summary>Action an Position idx IN PLACE mutieren (Edit Timeout /
        /// Edit Amount / Use Last Gump Response) — nur wenn sie noch dieselbe
        /// Instanz ist; danach speichern.</summary>
        private void MutateAt(string file, int idx, MacroAction oldAction, Action<MacroAction> mutate)
        {
            GameThread.Post(() =>
            {
                Macro m = UiSnapshotBuilder.FindMacro(file);
                if (m == null || idx >= m.Actions.Count || !ReferenceEquals(m.Actions[idx], oldAction))
                    return;

                try
                {
                    mutate(oldAction);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[UOSagas Razor] Macro-Action konnte nicht geaendert werden: {ex.Message}");
                    return;
                }

                m.Save();
            });
        }

        // --- CE-Menuepunkte der einzelnen Actions (Edit Timeout / Re-Target /
        //     Edit Amount / Button-ID / Hue) --------------------------------------

        private async void OnEditTimeout(string file, int idx, MacroWaitAction wait)
        {
            string s = await Dialogs.Prompt(_owner, "Change Timeout", "New timeout (in seconds):",
                ((int) wait.Timeout.TotalSeconds).ToString());
            if (s == null || !int.TryParse(s.Trim(), out int secs) || secs <= 0)
                return;

            MutateAt(file, idx, wait, a => ((MacroWaitAction) a).Timeout = TimeSpan.FromSeconds(secs));
        }

        private async void OnEditAmount(string file, int idx, MacroAction action, ushort current)
        {
            string s = await Dialogs.Prompt(_owner, "Edit Amount", "Enter the new amount:", current.ToString());
            if (s == null || !int.TryParse(s.Trim(), out int n) || n < 1 || n > ushort.MaxValue)
                return;

            MutateAt(file, idx, action, a =>
            {
                if (a is LiftAction l)
                    l.Amount = (ushort) n;
                else if (a is LiftTypeAction lt)
                    lt.Amount = (ushort) n;
            });
        }

        private async void OnEditGumpButton(string file, int idx, MacroAction action, int current)
        {
            string s = await Dialogs.Prompt(_owner, "Edit Gump Response", "Button ID:", current.ToString());
            if (s == null || !int.TryParse(s.Trim(), out int n) || n < 0)
                return;

            MutateAt(file, idx, action, a => ((GumpResponseAction) a).ButtonID = n);
        }

        private async void OnSetOverheadHue(string file, int idx, MacroAction action, OverheadMessageAction om)
        {
            // CE oeffnet den HueEntry-Farbwaehler; der Port nimmt die Hue-Nummer
            // direkt (wie die Overhead-Einstellungen an anderer Stelle).
            string s = await Dialogs.Prompt(_owner, "Set Hue", "Hue number:", om.Hue.ToString());
            if (s == null || !int.TryParse(s.Trim(), out int hue) || hue < 0 || hue > 3000)
                return;

            ReplaceAt(file, idx, action, () => new OverheadMessageAction((ushort) hue, om.Message));
        }

        /// <summary>CE Re-Target: Ingame-Cursor, Antwort mutiert die Action —
        /// mit demselben ReferenceEquals-Schutz wie alle anderen Commits.</summary>
        private void OnReTargetAction(string file, int idx, MacroAction action)
        {
            GameThread.Post(() =>
            {
                if (World.Player == null)
                    return;

                Macro m = UiSnapshotBuilder.FindMacro(file);
                if (m == null || idx >= m.Actions.Count || !ReferenceEquals(m.Actions[idx], action))
                    return;

                // Ground-Flag je Action wie CE: Location-Targets erlauben Boden,
                // Objekt-Targets nur, wenn noch kein gueltiges Serial gesetzt ist.
                bool ground = action switch
                {
                    TargetRelLocAction => true,
                    LiftAction l => !l.Serial.IsValid,
                    AbsoluteTargetAction at => !at.Info.Serial.IsValid,
                    _ => false
                };

                World.Player.SendMessage(MsgLevel.Force, "Select the new target for this action");

                Targeting.OneTimeTarget(ground, (g, serial, pt, gfx) =>
                {
                    Macro m2 = UiSnapshotBuilder.FindMacro(file);
                    if (m2 == null || idx >= m2.Actions.Count || !ReferenceEquals(m2.Actions[idx], action))
                        return;

                    switch (action)
                    {
                        case DoubleClickAction d:
                            d.ReTarget(serial, gfx);
                            break;
                        case DoubleClickTypeAction dt:
                            dt.ReTarget(serial, gfx);
                            break;
                        case LiftAction l:
                            l.ReTarget(serial, gfx);
                            break;
                        case LiftTypeAction lt:
                            lt.ReTarget(gfx);
                            break;
                        case AbsoluteTargetAction at:
                            at.ReTarget(g, serial, pt, gfx);
                            break;
                        case TargetTypeAction tt:
                            tt.ReTarget(g, serial, gfx);
                            break;
                        case TargetRelLocAction tr:
                            tr.ReTarget(pt);
                            break;
                    }

                    m2.Save();
                });
            });

            _owner.RequestSnapshot();
        }

        // --- Anzeige ------------------------------------------------------------

        public void Contribute(UiRequest req)
        {
            _binder.Contribute(req);
            req.SelectedMacroFile = SelectedMacroFile;
        }

        /// <summary>Snapshot in die Controls uebernehmen (UI-Thread only).</summary>
        public void Apply(UiSnapshot snap)
        {
            _applying = true;
            try
            {
                _lastSnapshot = snap;
                _binder.Apply(snap);

                // Macro-Baum nur neu aufbauen, wenn sich die Menge geaendert hat
                // (erhaelt Selektion und aufgeklappte Ordner).
                bool changed = _treeMacros.Count != snap.Macros.Count;
                if (!changed)
                {
                    for (int i = 0; i < snap.Macros.Count; i++)
                    {
                        if (!string.Equals(_treeMacros[i].Filename, snap.Macros[i].Filename,
                                StringComparison.OrdinalIgnoreCase) ||
                            !string.Equals(_treeMacros[i].Name, snap.Macros[i].Name, StringComparison.Ordinal))
                        {
                            changed = true;
                            break;
                        }
                    }
                }

                if (changed)
                {
                    _treeMacros.Clear();
                    _treeMacros.AddRange(snap.Macros);
                    RebuildTree();
                }

                // Aktionsliste des ausgewaehlten Macros (read-only Anzeige).
                string curSel = SelectedMacroFile;
                if (curSel != null &&
                    string.Equals(snap.SelectedMacroFile, curSel, StringComparison.OrdinalIgnoreCase))
                {
                    ApplyActionList(snap.SelectedMacroActions);
                    if (_loopBox.IsChecked != snap.SelectedMacroLoop)
                        _loopBox.IsChecked = snap.SelectedMacroLoop;
                    _loopBox.IsEnabled = true;
                }
                else if (curSel == null)
                {
                    _actionList.Items.Clear();
                    _loopBox.IsChecked = false;
                    _loopBox.IsEnabled = false;
                }

                // Buttons/Status wie Razor CE umschalten.
                _playButton.Content = snap.Playing ? "Stop" : "Play";
                _recordButton.Content = snap.Recording ? "Stop Rec." : "Record";
                _playButton.IsEnabled = snap.Playing || curSel != null;
                _recordButton.IsEnabled = snap.Recording || curSel != null;

                string currentName = null;
                if (snap.CurrentMacroFile != null)
                {
                    foreach (MacroInfo info in snap.Macros)
                    {
                        if (string.Equals(info.Filename, snap.CurrentMacroFile, StringComparison.OrdinalIgnoreCase))
                        {
                            currentName = info.Name;
                            break;
                        }
                    }

                    currentName ??= Path.GetFileNameWithoutExtension(snap.CurrentMacroFile);
                }

                if (snap.Recording)
                {
                    _waitDisp.Text = $"Recording{(currentName != null ? ":\n" + currentName : "")}...";
                    _waitDisp.Foreground = Brushes.Red;
                }
                else if (snap.Playing)
                {
                    _waitDisp.Text = $"Playing{(currentName != null ? ":\n" + currentName : "")}...";
                    _waitDisp.Foreground = Brushes.Green;
                }
                else
                {
                    _waitDisp.Text = string.Empty;
                    _waitDisp.Foreground = Ce.GrayText;
                }
            }
            finally
            {
                _applying = false;
            }
        }

        /// <summary>Baum aus den relativen Macro-Pfaden aufbauen (wie Razor CE macroTree).</summary>
        private void RebuildTree()
        {
            bool wasApplying = _applying;
            _applying = true;
            try
            {
                string selected = SelectedMacroFile;
                _tree.Items.Clear();

                var folders = new Dictionary<string, TreeViewItem>(StringComparer.OrdinalIgnoreCase);
                TreeViewItem toSelect = null;

                foreach (MacroInfo info in _treeMacros)
                {
                    if (_treeFilter.Length > 0 &&
                        info.Name.IndexOf(_treeFilter, StringComparison.OrdinalIgnoreCase) < 0)
                        continue;

                    string[] parts = info.Name.Replace('\\', '/').Split('/');

                    // Ordnerkette anlegen/wiederverwenden.
                    TreeViewItem parent = null;
                    string key = string.Empty;
                    for (int i = 0; i < parts.Length - 1; i++)
                    {
                        key = key.Length == 0 ? parts[i] : $"{key}/{parts[i]}";
                        if (!folders.TryGetValue(key, out TreeViewItem folder))
                        {
                            folder = new TreeViewItem { Header = parts[i], IsExpanded = true };
                            folders[key] = folder;
                            if (parent == null)
                                _tree.Items.Add(folder);
                            else
                                parent.Items.Add(folder);
                        }

                        parent = folder;
                    }

                    var leaf = new TreeViewItem
                    {
                        Header = parts[parts.Length - 1],
                        Tag = info.Filename
                    };

                    if (parent == null)
                        _tree.Items.Add(leaf);
                    else
                        parent.Items.Add(leaf);

                    if (selected != null &&
                        string.Equals(info.Filename, selected, StringComparison.OrdinalIgnoreCase))
                        toSelect = leaf;
                }

                if (toSelect != null)
                    _tree.SelectedItem = toSelect;
            }
            finally
            {
                _applying = wasApplying;
            }
        }

        private void ApplyActionList(List<string> actions)
        {
            bool changed = _actionList.Items.Count != actions.Count;
            if (!changed)
            {
                for (int i = 0; i < actions.Count; i++)
                {
                    if (!string.Equals(_actionList.Items[i] as string, actions[i], StringComparison.Ordinal))
                    {
                        changed = true;
                        break;
                    }
                }
            }

            if (!changed)
                return;

            // Selektionsposition ueber den Rebuild retten (CE RedrawActionList).
            int sel = _actionList.SelectedIndex;

            _actionList.Items.Clear();
            foreach (string a in actions)
                _actionList.Items.Add(a);

            if (sel >= 0 && sel < _actionList.Items.Count)
                _actionList.SelectedIndex = sel;
        }
        // --- Convert To: Macro -> Razor-Script/Lua/VScript (Sagas-Zusatz) ------
        //
        // Konvertiert wird auf dem Game-Thread (MacroConverter, Razor.Core);
        // der jeweilige Editor oeffnet mit dem UNGESPEICHERTEN Ergebnis, und
        // erst der erste Save fragt nach dem Script-Namen (kommt ja aus dem
        // Konverter, existiert also noch nicht).

        private enum ConvertTarget
        {
            Razor,
            Lua,
            VScript
        }

        private void ConvertMacro(string file, ConvertTarget target)
        {
            GameThread.Post(() =>
            {
                Macro m = UiSnapshotBuilder.FindMacro(file);
                if (m == null)
                    return;

                try
                {
                    switch (target)
                    {
                        case ConvertTarget.Razor:
                        {
                            string text = MacroConverter.ToRazorScript(m);
                            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                                OpenConvertedEditor(Razor.UI.Editor.RazorScriptLanguage.Instance, text, isLua: false));
                            break;
                        }

                        case ConvertTarget.Lua:
                        {
                            string text = MacroConverter.ToLua(m);
                            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                                OpenConvertedEditor(Razor.UI.Editor.LuaLanguage.Instance, text, isLua: true));
                            break;
                        }

                        case ConvertTarget.VScript:
                        {
                            Assistant.VScripts.Core.NodeGraph graph =
                                MacroConverter.ToVScript(m, out List<string> skipped);
                            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                                Razor.UI.VScriptEditor.VScriptEditorWindow.OpenWithGraph(_owner, graph, skipped.Count));
                            break;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[UOSagas Razor] Macro-Konvertierung fehlgeschlagen: {ex.Message}");
                }
            });
        }

        /// <summary>Eigenstaendiges IDE-Fenster fuer ein konvertiertes (noch
        /// namenloses) Script — der erste Save fragt nach dem Namen.</summary>
        private void OpenConvertedEditor(Razor.UI.Editor.ILanguageDefinition language, string text, bool isLua)
        {
            var win = new Razor.UI.Editor.ScriptEditorWindow(language, debugControls: isLua);

            if (isLua)
            {
                win.PlayRequested += t => GameThread.Post(() => LuaEngineService.RunScript(t));
                win.StopRequested += () => GameThread.Post(LuaEngineService.StopScript);
            }
            else
            {
                win.PlayRequested += t =>
                {
                    string[] lines = SplitScriptText(t);
                    GameThread.Post(() => Assistant.Scripts.ScriptManager.PlayScript(lines, win.ScriptName ?? "converted"));
                };
                win.StopRequested += () => GameThread.Post(Assistant.Scripts.ScriptManager.StopScript);
            }

            win.SaveRequested += async t =>
            {
                string name = win.ScriptName;

                if (name == null)
                {
                    string input = await Dialogs.Prompt(win, isLua ? "Save Lua Script" : "Save Razor Script",
                        "Script name:");
                    if (string.IsNullOrWhiteSpace(input))
                        return;

                    name = input.Trim();

                    LuaEngineService.GetFileNamesWithoutExtension();
                    bool exists = isLua
                        ? LuaEngineService.Files.ContainsKey(name)
                        : Assistant.Scripts.ScriptManager.FindScript(name) != null;
                    if (exists && !await Dialogs.Confirm(win, "Overwrite Script",
                            $"Script '{name}' already exists — overwrite it?", "Overwrite"))
                        return;

                    win.LoadScript(name, t);
                }

                if (isLua)
                {
                    LuaEngineService.SaveFile(name, t);
                    GameThread.Post(LuaHotkeys.Refresh);
                }
                else
                {
                    string[] lines = SplitScriptText(t);
                    GameThread.Post(() =>
                    {
                        Assistant.Scripts.RazorScript script =
                            Assistant.Scripts.ScriptManager.FindScript(name) ??
                            Assistant.Scripts.ScriptManager.NewScript(name);
                        if (script != null)
                            Assistant.Scripts.ScriptManager.SaveScript(script, lines);
                    });
                }

                win.SetStatus($"Saved: {name}");
                _owner.RequestSnapshot();
            };

            win.LoadScript(null, text);
            win.SetStatus("Converted from macro — Save will ask for a name.");
            win.Show(_owner);
            win.Activate();
        }

        private static string[] SplitScriptText(string text)
        {
            return (text ?? string.Empty).Replace("\r\n", "\n").Split('\n');
        }
    }
}
