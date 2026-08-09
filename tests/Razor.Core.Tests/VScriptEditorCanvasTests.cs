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

// UOSagas-Razor: Headless-Tests fuer die NodeCanvas-Logik des VScript-Editors
// (Feedback-Runde 3): Auto-Konvertierungs-Nodes beim Verbinden inkompatibler
// Datentypen (UE-/Client-Verhalten), Undo/Redo ueber Serializer-Snapshots,
// Copy/Paste als Teilgraph-Klon, Value-Reset beim Trennen.

using System.Linq;
using Assistant.VScripts.Core;
using Assistant.VScripts.Nodes;
using Razor.UI.VScriptEditor;
using Xunit;

namespace Razor.Core.Tests
{
    public class VScriptEditorCanvasTests
    {
        private static (NodeCanvas canvas, NodeGraph graph) MakeCanvas()
        {
            var graph = new NodeGraph("canvas-test");
            var canvas = new NodeCanvas { Graph = graph };
            return (canvas, graph);
        }

        [Fact]
        public void TryConnect_fuegt_Konvertierungs_Node_bei_Number_zu_String_ein()
        {
            var (canvas, graph) = MakeCanvas();

            var add = new AddNumbersNode(graph.GetNextNodeId(), graph.GetNextPinId());
            var concat = new ConcatenateStringsNode(graph.GetNextNodeId(), graph.GetNextPinId());
            graph.AddNode(add);
            graph.AddNode(concat);

            NodePin numberOut = add.OutputPins.First(p => p.Type == PinType.Number);
            NodePin stringIn = concat.InputPins.First(p => p.Type == PinType.String);

            Assert.True(canvas.TryConnect(numberOut, stringIn));

            // Konverter sitzt dazwischen: 3 Nodes, 2 Links, kein Direkt-Link.
            var conv = graph.Nodes.OfType<NumberToStringNode>().Single();
            Assert.Equal(2, graph.Links.Count);
            Assert.Contains(graph.Links, l => l.StartPinId == numberOut.Id && l.EndPinId == conv.InputPins[0].Id);
            Assert.Contains(graph.Links, l => l.StartPinId == conv.OutputPins[0].Id && l.EndPinId == stringIn.Id);
            Assert.DoesNotContain(graph.Links, l => l.StartPinId == numberOut.Id && l.EndPinId == stringIn.Id);
        }

        [Fact]
        public void TryConnect_gleicher_Typ_verbindet_direkt_ohne_Konverter()
        {
            var (canvas, graph) = MakeCanvas();

            var a = new AddNumbersNode(graph.GetNextNodeId(), graph.GetNextPinId());
            var b = new AbsoluteNode(graph.GetNextNodeId(), graph.GetNextPinId());
            graph.AddNode(a);
            graph.AddNode(b);

            NodePin output = a.OutputPins.First(p => p.Type == PinType.Number);
            NodePin input = b.InputPins.First(p => p.Type == PinType.Number);

            Assert.True(canvas.TryConnect(output, input));
            Assert.Single(graph.Links);
            Assert.Equal(2, graph.Nodes.Count);
        }

        [Fact]
        public void Undo_und_Redo_stellen_den_Graphen_wieder_her()
        {
            var (canvas, graph) = MakeCanvas();

            var start = new StartNode(graph.GetNextNodeId(), graph.GetNextPinId());
            graph.AddNode(start);

            NodeGraph replaced = null;
            canvas.GraphReplaced += g => replaced = g;

            // Mutation mit Snapshot davor (wie AddPickedNodeAt im Fenster).
            canvas.PushUndo();
            graph.AddNode(new PrintMessageNode(graph.GetNextNodeId(), graph.GetNextPinId()));
            Assert.Equal(2, graph.Nodes.Count);

            canvas.Undo();
            Assert.NotNull(replaced);
            Assert.Single(replaced.Nodes);
            Assert.IsType<StartNode>(replaced.Nodes[0]);

            canvas.Redo();
            Assert.Equal(2, canvas.Graph.Nodes.Count);
        }

        [Fact]
        public void CopyPaste_klont_Auswahl_mit_Links_und_neuen_Ids()
        {
            var (canvas, graph) = MakeCanvas();

            var add = new AddNumbersNode(graph.GetNextNodeId(), graph.GetNextPinId());
            var abs = new AbsoluteNode(graph.GetNextNodeId(), graph.GetNextPinId());
            graph.AddNode(add);
            graph.AddNode(abs);
            graph.AddLink(new NodeLink(graph.GetNextLinkId(),
                add.OutputPins.First(p => p.Type == PinType.Number).Id,
                abs.InputPins.First(p => p.Type == PinType.Number).Id));

            canvas.SelectedNodes.Add(add);
            canvas.SelectedNodes.Add(abs);
            canvas.CopySelection();
            canvas.PasteClipboard();

            Assert.Equal(4, graph.Nodes.Count);
            Assert.Equal(2, graph.Links.Count);

            // Neue Ids, keine Kollisionen.
            Assert.Equal(4, graph.Nodes.Select(n => n.Id).Distinct().Count());

            // Der geklonte Link verbindet die geklonten Nodes.
            var pasted = canvas.SelectedNodes; // Paste selektiert die Kopien
            Assert.Equal(2, pasted.Count);
            var pastedPinIds = pasted.SelectMany(n => n.InputPins.Concat(n.OutputPins)).Select(p => p.Id).ToHashSet();
            Assert.Contains(graph.Links, l =>
                pastedPinIds.Contains(l.StartPinId) && pastedPinIds.Contains(l.EndPinId));
        }

        [Fact]
        public void DeleteNodeAndCleanup_leert_die_Werte_nachgelagerter_Inputs()
        {
            var (canvas, graph) = MakeCanvas();

            var add = new AddNumbersNode(graph.GetNextNodeId(), graph.GetNextPinId());
            var abs = new AbsoluteNode(graph.GetNextNodeId(), graph.GetNextPinId());
            graph.AddNode(add);
            graph.AddNode(abs);

            NodePin input = abs.InputPins.First(p => p.Type == PinType.Number);
            input.Value = 42f;
            graph.AddLink(new NodeLink(graph.GetNextLinkId(),
                add.OutputPins.First(p => p.Type == PinType.Number).Id, input.Id));

            canvas.DeleteNodeAndCleanup(add);

            Assert.Single(graph.Nodes);
            Assert.Null(input.Value); // getrennter Input verliert seinen Wert
        }
    }
}
