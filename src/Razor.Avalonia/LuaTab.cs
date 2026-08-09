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

// UOSagas-Razor: Lua-Tab (Phase 4b) — Lua-Scripting im CE-Look.
//
// Muster wie ScriptsTab: Script-Liste links (Data/Profiles/Scripts, derselbe
// Ordner wie der integrierte Assistant — .lua-Dateien sind 1:1 teilbar),
// Editor rechts, Play/Save + "Open in IDE" (grosses ScriptEditorWindow mit
// LuaLanguage, Breakpoint-Spalte und Pause/Resume).
//
// Threading: Die LuaEngineService-Events feuern auf dem Script-Task ->
// Dispatcher.UIThread.Post. Run/Stop laufen ueber GameThread.Post (die
// Engine spawnt ihren eigenen Task; der Einstieg bleibt wie bei den anderen
// Engines auf dem Game-Thread). Breakpoints: Editor 1-basiert, Engine
// erwartet die Editor-0-basierte Sicht (_breakpoints.Contains(line - 1)).

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Assistant.LuaEngine;
using Avalonia.Controls;
using Avalonia.Threading;
using Razor.UI.Editor;

namespace Razor.UI
{
    public class LuaTab : UserControl, ICeTab
    {
        private readonly MainWindow _owner;

        private readonly TextBox _filter;
        private readonly ListBox _list;
        private readonly CodeEditor _editor;
        private readonly Button _playButton;
        private readonly TextBlock _status;

        private ScriptEditorWindow _ide;

        private readonly List<string> _scripts = new List<string>();
        private string _filterText = string.Empty;
        private bool _running;
        private bool _applying;

        /// <summary>Name des aktuell im Editor geladenen Scripts (null = keins).</summary>
        private string _loadedName;

        public LuaTab(MainWindow owner)
        {
            _owner = owner;

            var root = Ce.Panel();

            Ce.Label(root, "Filter:", 5, 10, 36, 15);
            _filter = Ce.Text(root, 47, 7, 147, 23);
            _filter.TextChanged += (s, e) =>
            {
                if (_applying)
                    return;

                _filterText = _filter.Text?.Trim() ?? string.Empty;
                RebuildList();
            };

            _list = Ce.List(root, 6, 36, 188, 240);
            _list.SelectionChanged += (s, e) => OnSelectScript();

            Ce.Button(root, "New...", 8, 282, 56, 26, OnNew);
            Ce.Button(root, "Delete", 70, 282, 56, 26, OnDelete);
            Ce.Button(root, "Refresh", 132, 282, 62, 26, OnRefresh);

            Canvas grp = Ce.Group(root, "Script", 200, 3, 300, 276);
            _editor = new CodeEditor { LanguageDefinition = LuaLanguage.Instance };
            _editor.EnableBreakpointMargin();
            _editor.BreakpointsChanged += PushBreakpoints;
            Ce.At(grp, _editor, 6, 17, 288, 215);

            _playButton = Icons.IconButton("Play (F5)", Icons.PlayPath, Icons.Green, OnPlayStop, 44, 30);
            Ce.At(grp, _playButton, 6, 238);
            Ce.At(grp, Icons.IconButton("Save script", Icons.SavePath, Icons.Dark, OnSave, 44, 30), 56, 238);
            Ce.Button(grp, "Open in IDE", 142, 238, 152, 30, OnOpenIde);

            _status = Ce.Label(root, "Idle", 202, 284, 296, 20);
            _status.Foreground = Ce.GrayText;

            Content = root;

            // Engine-Events (feuern auf dem Script-Task) -> UI marshallen.
            LuaEngineService.RunningStateChanged += (s, running) => Dispatcher.UIThread.Post(() =>
            {
                _running = running;

                if (running)
                {
                    Icons.Swap(_playButton, "Stop", Icons.StopPath, Icons.Red);
                    _status.Text = "Running";
                    _editor.ClearMarkers();
                    _ide?.Editor.ClearMarkers();
                    _ide?.SetStatus("Running");
                }
                else
                {
                    Icons.Swap(_playButton, "Play (F5)", Icons.PlayPath, Icons.Green);
                    _status.Text = "Idle";
                    _editor.SetExecutionLine(-1);
                    _ide?.SetExecutionLine(-1);
                    _ide?.SetStatus("Stopped");
                }
            });

            LuaEngineService.CurrentLineChanged += (s, line) => Dispatcher.UIThread.Post(() =>
            {
                _editor.SetExecutionLine(line);
                _ide?.SetExecutionLine(line);

                if (line > 0 && LuaEngineService.IsPaused)
                {
                    _status.Text = $"Paused (line {line})";
                    _ide?.SetStatus($"Paused (line {line}) — F6 resumes");
                }
            });

            LuaEngineService.ScriptErrorsChanged += (s, errors) => Dispatcher.UIThread.Post(() =>
            {
                LuaError first = errors?.FirstOrDefault();
                if (first == null)
                    return;

                // Engine-Fehler sind editor-0-basiert (Client-Konvention).
                int line = first.Line + 1;
                _status.Text = $"Error (line {line}): {first.Message}";
                _editor.SetErrorLine(line);
                _ide?.SetErrorLine(line);
                _ide?.SetStatus($"Error (line {line}): {first.Message}");
            });

            RefreshFromEngine();
        }

        // --- ICeTab (Anzeige laeuft ueber eigene Events, kein Snapshot noetig) --

        public void Contribute(UiRequest req)
        {
        }

        public void Apply(UiSnapshot snap)
        {
        }

        // --- Liste ------------------------------------------------------------

        private void RefreshFromEngine()
        {
            _scripts.Clear();

            if (LuaEngineService.Files != null)
                _scripts.AddRange(LuaEngineService.Files.Keys.OrderBy(n => n, StringComparer.OrdinalIgnoreCase));

            RebuildList();
        }

        private void RebuildList()
        {
            _applying = true;

            string selected = (_list.SelectedItem as ListBoxItem)?.Tag as string;
            _list.Items.Clear();

            foreach (string name in _scripts)
            {
                if (_filterText.Length > 0 &&
                    name.IndexOf(_filterText, StringComparison.OrdinalIgnoreCase) < 0)
                    continue;

                var item = new ListBoxItem { Content = name, Tag = name };
                _list.Items.Add(item);

                if (name == selected)
                    _list.SelectedItem = item;
            }

            _applying = false;
        }

        private void OnSelectScript()
        {
            if (_applying)
                return;

            string name = (_list.SelectedItem as ListBoxItem)?.Tag as string;
            if (name == null)
                return;

            string content;
            try
            {
                content = LuaEngineService.LoadFile(name);
            }
            catch (IOException)
            {
                return;
            }

            _loadedName = name;
            _editor.Text = content ?? string.Empty;
            _editor.ClearMarkers();
            _editor.ClearBreakpoints();

            // Offenes IDE-Fenster mitziehen.
            _ide?.LoadScript(name, _editor.Text);
            _ide?.Editor.ClearBreakpoints();
        }

        // --- Kommandos ----------------------------------------------------------

        private void PushBreakpoints()
        {
            // Editor 1-basiert -> Engine erwartet die 0-basierte Editor-Sicht.
            int[] lines = _editor.BreakpointLines.Select(l => l - 1).ToArray();
            GameThread.Post(() => LuaEngineService.SetBreakpoints(lines));
        }

        private void OnPlayStop()
        {
            if (_running)
            {
                GameThread.Post(LuaEngineService.StopScript);
                return;
            }

            string text = _editor.Text;
            GameThread.Post(() => LuaEngineService.RunScript(text));
        }

        private void OnSave()
        {
            if (_loadedName == null)
                return;

            LuaEngineService.SaveFile(_loadedName, _editor.Text);
            _status.Text = $"Saved: {_loadedName}";
        }

        private void OnRefresh()
        {
            LuaEngineService.GetFileNamesWithoutExtension();
            GameThread.Post(LuaHotkeys.Refresh);
            RefreshFromEngine();
        }

        private async void OnNew()
        {
            string name = await Dialogs.Prompt(_owner, "New Lua Script", "Script name:");

            if (string.IsNullOrWhiteSpace(name))
                return;

            if (LuaEngineService.CreateEmptyLuaFile(name.Trim()))
            {
                GameThread.Post(LuaHotkeys.Refresh);
                RefreshFromEngine();
            }
        }

        private async void OnDelete()
        {
            string name = (_list.SelectedItem as ListBoxItem)?.Tag as string;
            if (name == null)
                return;

            if (!await Dialogs.Confirm(_owner, "Delete Lua Script",
                    $"Delete script '{name}'? This cannot be undone."))
                return;

            LuaEngineService.DeleteFile(name);
            GameThread.Post(LuaHotkeys.Refresh);

            if (_loadedName == name)
            {
                _loadedName = null;
                _editor.Text = string.Empty;
            }

            RefreshFromEngine();
        }

        private void OnOpenIde()
        {
            if (_ide == null)
            {
                _ide = new ScriptEditorWindow(LuaLanguage.Instance, debugControls: true);

                _ide.PlayRequested += text => GameThread.Post(() => LuaEngineService.RunScript(text));
                _ide.StopRequested += () => GameThread.Post(LuaEngineService.StopScript);
                _ide.PauseResumeRequested += () => GameThread.Post(() =>
                {
                    if (!LuaEngineService.IsRunning)
                        return;

                    if (LuaEngineService.IsPaused)
                        LuaEngineService.ResumeScript();
                    else
                        LuaEngineService.PauseScript();
                });
                _ide.SaveRequested += text =>
                {
                    string name = _ide.ScriptName;
                    if (name == null)
                        return;

                    LuaEngineService.SaveFile(name, text);
                    _ide.SetStatus($"Saved: {name}");

                    // Mini-Editor synchron halten.
                    if (name == _loadedName)
                        _editor.Text = text;
                };

                // IDE-Breakpoints treiben die Engine (letzter Setzer gewinnt).
                _ide.Editor.BreakpointsChanged += () =>
                {
                    int[] lines = _ide.Editor.BreakpointLines.Select(l => l - 1).ToArray();
                    GameThread.Post(() => LuaEngineService.SetBreakpoints(lines));
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
    }
}
