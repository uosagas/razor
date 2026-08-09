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

// UOSagas-Razor: Scripts-Tab (Phase 4a) — Razor-Script (1:1 CE) im CE-Look.
//
// Blaupause: Razor CE Razor.Designer.cs, scriptsTab: Script-Liste links,
// Editor rechts, Play/Stop unten. Im fixen 530x372-Fenster ist der Editor
// bewusst klein — der Button "IDE" oeffnet das grosse, frei skalierbare
// ScriptEditorWindow (AvaloniaEdit mit Highlighting/Autocomplete).
//
// Threading: Kern-Zugriffe (ScriptManager) via GameThread.Post; der
// ScriptManager feuert seine Events auf dem Game-Thread -> Dispatcher.UIThread.
// Die Ausfuehrungszeile wird per 200ms-Timer gepollt (int-Read, unkritisch).

using System;
using System.Collections.Generic;
using System.Linq;
using Assistant;
using Assistant.Scripts;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using Razor.UI.Editor;

namespace Razor.UI
{
    public class ScriptsTab : UserControl, ICeTab
    {
        private sealed class ScriptEntry
        {
            public string Display;   // Category\Name
            public string Name;
            public string[] Lines;

            public override string ToString() => Display;
        }

        private readonly MainWindow _owner;

        private readonly TextBox _filter;
        private readonly ListBox _list;
        private readonly ListBox _varList;
        private readonly CodeEditor _editor;
        private readonly Button _playButton;
        private readonly TextBlock _status;
        private readonly DispatcherTimer _lineTimer;

        private ScriptEditorWindow _ide;

        private readonly List<ScriptEntry> _scripts = new List<ScriptEntry>();
        private string _filterText = string.Empty;
        private bool _running;
        private bool _applying;

        /// <summary>Name des aktuell im Editor geladenen Scripts (null = keins).</summary>
        private string _loadedName;

        public ScriptsTab(MainWindow owner)
        {
            _owner = owner;

            var root = Ce.Panel();

            // Linke Spalte als Sub-Tabs wie Razor CE: Scripts-Liste + Variables.
            TabControl sub = Ce.SubTabs(root, 2, 3, 196, 312);

            var pageScripts = Ce.Panel(188, 280);
            Ce.Tab(sub, "Scripts", pageScripts);

            Ce.Label(pageScripts, "Filter:", 3, 8, 36, 15);
            _filter = Ce.Text(pageScripts, 41, 5, 143, 23);
            _filter.TextChanged += (s, e) =>
            {
                if (_applying)
                    return;

                _filterText = _filter.Text?.Trim() ?? string.Empty;
                RebuildList();
            };

            _list = Ce.List(pageScripts, 2, 33, 184, 208);
            _list.SelectionChanged += (s, e) => OnSelectScript();

            Ce.Button(pageScripts, "New...", 2, 248, 56, 26, OnNew);
            Ce.Button(pageScripts, "Delete", 64, 248, 56, 26, OnDelete);
            Ce.Button(pageScripts, "Refresh", 126, 248, 60, 26, OnRefresh);

            // --- Script Variables (CE: !name-Aliase, Profil-persistent) --------
            var pageVars = Ce.Panel(188, 280);
            Ce.Tab(sub, "Variables", pageVars);

            _varList = Ce.List(pageVars, 2, 5, 184, 236);
            Ce.Button(pageVars, "Add...", 2, 248, 88, 26, OnAddVariable);
            Ce.Button(pageVars, "Remove", 96, 248, 88, 26, OnRemoveVariable);

            // Beim Umschalten auf die Variables-Seite frisch einlesen.
            sub.SelectionChanged += (s, e) =>
            {
                if (sub.SelectedIndex == 1)
                    RefreshVariables();
            };

            Canvas grp = Ce.Group(root, "Script", 200, 3, 300, 276);
            _editor = new CodeEditor { LanguageDefinition = RazorScriptLanguage.Instance };
            Ce.At(grp, _editor, 6, 17, 288, 215);

            _playButton = Icons.IconButton("Play (F5)", Icons.PlayPath, Icons.Green, OnPlayStop, 44, 30);
            Ce.At(grp, _playButton, 6, 238);
            Ce.At(grp, Icons.IconButton("Save script", Icons.SavePath, Icons.Dark, OnSave, 44, 30), 56, 238);
            Ce.Button(grp, "Open in IDE", 142, 238, 152, 30, OnOpenIde);

            _status = Ce.Label(root, "Idle", 202, 284, 296, 20);
            _status.Foreground = Ce.GrayText;

            Content = root;

            // Kern-Events (feuern auf dem Game-Thread) -> UI marshallen.
            ScriptManager.OnScriptsChanged += PushScriptsToUi;
            ScriptManager.OnScriptStarted += name => Dispatcher.UIThread.Post(() =>
            {
                _running = true;
                Icons.Swap(_playButton, "Stop", Icons.StopPath, Icons.Red);
                _status.Text = $"Running: {name}";
                _editor.ClearMarkers();
                _ide?.ClearMarkers();
                _ide?.SetStatus($"Running: {name}");
            });
            ScriptManager.OnScriptStopped += () => Dispatcher.UIThread.Post(() =>
            {
                _running = false;
                Icons.Swap(_playButton, "Play (F5)", Icons.PlayPath, Icons.Green);
                _status.Text = "Idle";
                _editor.SetExecutionLine(-1);
                _ide?.SetExecutionLine(-1);
                _ide?.SetStatus("Stopped");
            });
            ScriptManager.OnScriptError += (msg, line) => Dispatcher.UIThread.Post(() =>
            {
                _status.Text = $"Error (line {line}): {msg}";
                _editor.SetErrorLine(line);
                _ide?.SetErrorLine(line);
                _ide?.SetStatus($"Error (line {line}): {msg}");
            });

            // Ausfuehrungszeile poller (nur bei laufendem Script aktiv).
            _lineTimer = new DispatcherTimer(TimeSpan.FromMilliseconds(200), DispatcherPriority.Background,
                (s, e) =>
                {
                    if (!_running)
                        return;

                    int line = ScriptManager.CurrentLine + 1; // 0- -> 1-basiert
                    _editor.SetExecutionLine(line);
                    _ide?.SetExecutionLine(line);
                });
            _lineTimer.Start();

            // Initiale Befuellung (Scripts wurden beim Plugin-Init geladen).
            GameThread.Post(PushScriptsToUi);
        }

        // --- ICeTab (Anzeige laeuft ueber eigene Events, kein Snapshot noetig) --

        public void Contribute(UiRequest req)
        {
        }

        public void Apply(UiSnapshot snap)
        {
        }

        // --- Liste ------------------------------------------------------------

        /// <summary>Kopiert die Script-Liste auf dem Game-Thread und postet sie in die UI.</summary>
        private void PushScriptsToUi()
        {
            List<ScriptEntry> copy = ScriptManager.Scripts
                .Select(s => new ScriptEntry { Display = s.ToString(), Name = s.Name, Lines = (string[]) s.Lines.Clone() })
                .ToList();

            Dispatcher.UIThread.Post(() =>
            {
                _scripts.Clear();
                _scripts.AddRange(copy);
                RebuildList();
            });
        }

        private void RebuildList()
        {
            _applying = true;

            string selected = (_list.SelectedItem as ListBoxItem)?.Tag as string;
            _list.Items.Clear();

            foreach (ScriptEntry entry in _scripts)
            {
                if (_filterText.Length > 0 &&
                    entry.Display.IndexOf(_filterText, StringComparison.OrdinalIgnoreCase) < 0)
                    continue;

                var item = new ListBoxItem { Content = entry.Display, Tag = entry.Name };
                _list.Items.Add(item);

                if (entry.Name == selected)
                    _list.SelectedItem = item;
            }

            _applying = false;
        }

        private ScriptEntry SelectedEntry()
        {
            string name = (_list.SelectedItem as ListBoxItem)?.Tag as string;
            return name == null ? null : _scripts.FirstOrDefault(s => s.Name == name);
        }

        private void OnSelectScript()
        {
            if (_applying)
                return;

            ScriptEntry entry = SelectedEntry();
            if (entry == null)
                return;

            _loadedName = entry.Name;
            _editor.Text = string.Join(Environment.NewLine, entry.Lines);
            _editor.ClearMarkers();

            // Offenes IDE-Fenster mitziehen.
            _ide?.LoadScript(entry.Name, _editor.Text);
        }

        // --- Kommandos ----------------------------------------------------------

        private void OnPlayStop()
        {
            if (_running)
            {
                GameThread.Post(ScriptManager.StopScript);
                return;
            }

            string name = _loadedName ?? "editor";
            string[] lines = SplitEditor(_editor.Text);

            GameThread.Post(() => ScriptManager.PlayScript(lines, name));
        }

        private void OnSave()
        {
            if (_loadedName == null)
                return;

            string name = _loadedName;
            string[] lines = SplitEditor(_editor.Text);

            GameThread.Post(() =>
            {
                RazorScript script = ScriptManager.FindScript(name);
                if (script != null)
                    ScriptManager.SaveScript(script, lines);
            });

            _status.Text = $"Saved: {name}";
        }

        private void OnRefresh()
        {
            GameThread.Post(ScriptManager.LoadScripts);
        }

        private async void OnNew()
        {
            string name = await Dialogs.Prompt(_owner, "New Script", "Script name:");

            if (string.IsNullOrWhiteSpace(name))
                return;

            string clean = name.Trim();
            GameThread.Post(() => ScriptManager.NewScript(clean));
        }

        private async void OnDelete()
        {
            ScriptEntry entry = SelectedEntry();
            if (entry == null)
                return;

            if (!await Dialogs.Confirm(_owner, "Delete Script",
                    $"Delete script '{entry.Display}'? This cannot be undone."))
                return;

            string name = entry.Name;
            GameThread.Post(() => ScriptManager.DeleteScript(name));

            if (_loadedName == name)
            {
                _loadedName = null;
                _editor.Text = string.Empty;
            }
        }

        // --- Script Variables ---------------------------------------------------

        /// <summary>Variablen-Liste vom Game-Thread kopieren und anzeigen.</summary>
        private void RefreshVariables()
        {
            GameThread.Post(() =>
            {
                List<KeyValuePair<string, Serial>> copy = ScriptVariables.Variables.ToList();

                Dispatcher.UIThread.Post(() =>
                {
                    string selected = (_varList.SelectedItem as ListBoxItem)?.Tag as string;
                    _varList.Items.Clear();

                    foreach (KeyValuePair<string, Serial> kv in copy)
                    {
                        var item = new ListBoxItem
                        {
                            Content = $"!{kv.Key} — {kv.Value}",
                            Tag = kv.Key
                        };
                        _varList.Items.Add(item);

                        if (kv.Key == selected)
                            _varList.SelectedItem = item;
                    }
                });
            });
        }

        private async void OnAddVariable()
        {
            string name = await Dialogs.Prompt(_owner, "New Script Variable", "Variable name:");
            if (string.IsNullOrWhiteSpace(name))
                return;

            string clean = name.Trim();

            GameThread.Post(() =>
            {
                if (World.Player == null)
                    return;

                World.Player.SendMessage(MsgLevel.Force, $"Target the object for variable '{clean}'");

                Targeting.OneTimeTarget((ground, serial, pt, gfx) =>
                {
                    ScriptVariables.RegisterVariable(clean, serial);
                    Config.Save();
                    World.Player?.SendMessage(MsgLevel.Force, $"Script variable '{clean}' set to {serial}");
                    Dispatcher.UIThread.Post(RefreshVariables);
                });
            });
        }

        private async void OnRemoveVariable()
        {
            string name = (_varList.SelectedItem as ListBoxItem)?.Tag as string;
            if (name == null)
                return;

            if (!await Dialogs.Confirm(_owner, "Remove Script Variable",
                    $"Remove variable '!{name}'?", "Remove"))
                return;

            GameThread.Post(() =>
            {
                ScriptVariables.UnregisterVariable(name);
                Config.Save();
            });

            RefreshVariables();
        }

        private void OnOpenIde()
        {
            if (_ide == null)
            {
                _ide = new ScriptEditorWindow(RazorScriptLanguage.Instance);

                _ide.PlayRequested += text =>
                {
                    string name = _ide.ScriptName ?? "editor";
                    string[] lines = SplitEditor(text);
                    GameThread.Post(() => ScriptManager.PlayScript(lines, name));
                };
                _ide.StopRequested += () => GameThread.Post(ScriptManager.StopScript);
                _ide.SaveRequested += text =>
                {
                    string name = _ide.ScriptName;
                    if (name == null)
                        return;

                    string[] lines = SplitEditor(text);
                    GameThread.Post(() =>
                    {
                        RazorScript script = ScriptManager.FindScript(name);
                        if (script != null)
                            ScriptManager.SaveScript(script, lines);
                    });
                    _ide.SetStatus($"Saved: {name}");

                    // Mini-Editor synchron halten.
                    if (name == _loadedName)
                        _editor.Text = text;
                };

                // Fenster nur verstecken statt schliessen (gleiches Muster wie MainWindow).
                _ide.Closing += (s, e) =>
                {
                    e.Cancel = true;
                    _ide.Hide();
                };
            }

            _ide.LoadScript(_loadedName ?? "(unsaved)", _editor.Text);
            _ide.Show();
            _ide.Activate();
        }

        private static string[] SplitEditor(string text)
        {
            return (text ?? string.Empty).Replace("\r\n", "\n").Split('\n');
        }
    }
}
