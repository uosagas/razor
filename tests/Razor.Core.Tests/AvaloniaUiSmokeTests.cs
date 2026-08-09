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

// UOSagas-Razor: Smoke-Tests fuer Razor.Avalonia (Phase 3a).
// KEIN Display noetig: Es wird nur geprueft, dass die Assembly samt
// Avalonia-Abhaengigkeiten laedt (Typen aufloesbar sind) und dass die
// GameThread-Queue das erwartete Verhalten hat. Die echte UI testet der
// User im Spiel.

using System;
using System.Threading;
using Razor.UI;
using Xunit;

namespace Razor.Core.Tests
{
    public class AvaloniaUiSmokeTests
    {
        [Fact]
        public void RazorUi_Typ_und_Avalonia_Assemblies_laden()
        {
            // Typ-Zugriff erzwingt das Laden von Razor.Avalonia + Avalonia-Kern.
            Type t = typeof(RazorUi);

            Assert.Equal("Razor.Avalonia", t.Assembly.GetName().Name);
            Assert.NotNull(t.GetMethod("Start"));
            Assert.NotNull(t.GetMethod("Stop"));

            // Die UI-Typen muessen aufloesbar sein, ohne Avalonia zu initialisieren.
            Assert.NotNull(typeof(RazorApp));
            Assert.NotNull(t.Assembly.GetType("Razor.UI.MainWindow", throwOnError: true));
        }

        [Fact]
        public void VScriptEditor_Typen_laden_und_Canvas_haelt_einen_Graphen()
        {
            // Phase 5c: Editor-Typen aufloesbar; NodeCanvas nimmt einen Graphen an.
            var canvas = new Razor.UI.VScriptEditor.NodeCanvas();
            var graph = new Assistant.VScripts.Core.NodeGraph("smoke");
            var start = new Assistant.VScripts.Nodes.StartNode(
                graph.GetNextNodeId(), graph.GetNextPinId());
            graph.AddNode(start);

            canvas.Graph = graph;
            canvas.CenterOnGraph();

            Assert.Same(graph, canvas.Graph);
            Assert.NotNull(typeof(Razor.UI.VScriptEditor.VScriptEditorWindow));
        }

        [Fact]
        public void GameThread_Queue_fuehrt_Aktionen_aus_und_schluckt_Fehler()
        {
            // Queue leeren (andere Tests/Reihenfolge egal machen).
            while (GameThread.Queue.TryDequeue(out _))
            {
            }

            int executed = 0;
            GameThread.Post(() => Interlocked.Increment(ref executed));
            GameThread.Post(() => throw new InvalidOperationException("darf den Tick nicht mitreissen"));
            GameThread.Post(() => Interlocked.Increment(ref executed));
            GameThread.Post(null); // null wird ignoriert

            GameThread.DrainOnGameThread();

            Assert.Equal(2, executed);
            Assert.True(GameThread.Queue.IsEmpty);
        }

        [Fact]
        public void GameThread_Drain_respektiert_Limit()
        {
            while (GameThread.Queue.TryDequeue(out _))
            {
            }

            int executed = 0;
            for (int i = 0; i < 10; i++)
                GameThread.Post(() => executed++);

            GameThread.DrainOnGameThread(maxActions: 4);
            Assert.Equal(4, executed);

            GameThread.DrainOnGameThread();
            Assert.Equal(10, executed);
        }
    }
}
