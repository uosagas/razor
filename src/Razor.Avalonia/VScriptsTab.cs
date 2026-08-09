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

// UOSagas-Razor: VScripts-Haupttab (Phase 5c) — eigener Tab neben "Scripts".
//
// Liste der .vscript-Graphen aus Data/VScripts (gleicher Ordner wie der
// integrierte Assistant), New/Edit/Run/Stop/Delete; Doppelklick oeffnet den
// Node-Editor (VScriptEditorWindow). Der Running-Status kommt ueber den
// normalen Apply-Puls (500 ms) — kein eigener Timer noetig.

using System;
using System.Collections.Generic;
using System.Linq;
using Assistant.VScripts.Engine;
using Avalonia.Controls;
using Avalonia.Media;
using Razor.UI.VScriptEditor;

namespace Razor.UI
{
    public class VScriptsTab : UserControl, ICeTab
    {
        private readonly ListBox _list;
        private readonly TextBlock _status;
        private List<string> _names = new();

        public VScriptsTab()
        {
            var root = Ce.Panel();

            Ce.Label(root, "Visual Scripts (node graphs, shared with the in-game assistant):", 8, 6, 400, 18);

            _list = Ce.List(root, 8, 26, 380, 300);
            _list.DoubleTapped += (s, e) => OnEdit();

            Ce.Button(root, "New", 396, 26, 120, 28, OnNew);
            Ce.Button(root, "Edit / Open", 396, 60, 120, 28, OnEdit);
            Ce.At(root, Icons.IconButton("Run selected script", Icons.PlayPath, Icons.Green, OnRun, 57, 28), 396, 102);
            Ce.At(root, Icons.IconButton("Stop running script", Icons.StopPath, Icons.Red, OnStop, 57, 28), 459, 102);
            Ce.Button(root, "Delete", 396, 144, 120, 28, OnDelete);
            Ce.At(root, Icons.IconButton("Refresh list", Icons.RefreshPath, Icons.Dark,
                () => RefreshList(force: true), 120, 28), 396, 186);

            _status = new TextBlock
            {
                Foreground = new SolidColorBrush(Color.Parse("#666666"))
            };
            Ce.At(root, _status, 8, 330, 500, 18);

            Content = root;
            RefreshList(force: true);
        }

        private string SelectedName =>
            _list.SelectedIndex >= 0 && _list.SelectedIndex < _names.Count
                ? _names[_list.SelectedIndex]
                : null;

        private void RefreshList(bool force = false)
        {
            List<string> names = VScriptService.GetAllScriptNames().OrderBy(n => n).ToList();

            if (!force && names.SequenceEqual(_names))
                return;

            int sel = _list.SelectedIndex;
            _names = names;
            _list.ItemsSource = names.Cast<object>().ToList();
            if (sel >= 0 && sel < names.Count)
                _list.SelectedIndex = sel;
        }

        private Window Owner => TopLevel.GetTopLevel(this) as Window;

        private async void OnNew()
        {
            Window owner = Owner;
            if (owner == null)
                return;

            string name = await Dialogs.Prompt(owner, "New VScript", "Script name:");
            if (string.IsNullOrWhiteSpace(name))
                return;

            name = name.Trim();

            if (VScriptService.CreateNewScript(name))
            {
                // Wie im Client: neues Script startet mit einem Start-Node.
                var graph = VScriptService.LoadScript(name);
                if (graph != null && !graph.Nodes.Any(n => n is Assistant.VScripts.Nodes.StartNode))
                {
                    var start = new Assistant.VScripts.Nodes.StartNode(
                        graph.GetNextNodeId(), graph.GetNextPinId());
                    start.Position = new System.Numerics.Vector2(80, 120);
                    graph.AddNode(start);
                    VScriptService.SaveScript(name, graph);
                }

                RefreshList(force: true);
                _list.SelectedIndex = _names.IndexOf(name);
                VScriptEditorWindow.Open(owner, name);
            }
        }

        private void OnEdit()
        {
            string name = SelectedName;
            if (name == null)
                return;

            VScriptEditorWindow.Open(Owner, name);
        }

        private void OnRun()
        {
            string name = SelectedName;
            if (name == null)
                return;

            GameThread.Post(() => VScriptService.RunScript(name));
        }

        private void OnStop()
        {
            GameThread.Post(VScriptService.StopScript);
        }

        private async void OnDelete()
        {
            string name = SelectedName;
            Window owner = Owner;
            if (name == null || owner == null)
                return;

            if (!await Dialogs.Confirm(owner, "Delete VScript",
                    $"Delete VScript '{name}'? This cannot be undone."))
                return;

            GameThread.Post(() => VScriptService.DeleteScript(name));
            _names.Remove(name);
            _list.ItemsSource = _names.Cast<object>().ToList();
        }

        public void Contribute(UiRequest req)
        {
        }

        public void Apply(UiSnapshot snap)
        {
            RefreshList();

            var engine = VScriptService.Engine;
            string text = engine.IsRunning
                ? $"Running… ({engine.Context?.CurrentScriptName})"
                : $"{_names.Count} scripts in Data\\VScripts — hotkeys under Hot Keys → Scripts.";

            if (_status.Text != text)
                _status.Text = text;
        }
    }
}
