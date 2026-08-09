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

// UOSagas-Razor: Tests fuer Feedback-Runde 4 des VScript-Editors —
// (1) Fehler-Attribution an den VERURSACHER-Node (pure Daten-Nodes, die beim
//     Pin-Pull scheitern, werden selbst markiert, nicht nur der Flow-Node),
// (2) Funktionen: Scripts mit Parameter-/Output-Variablen als aufrufbare
//     Einheiten ueber den client-nativen ExecuteScriptNode (FunctionHelper).

using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using Assistant.VScripts.Core;
using Assistant.VScripts.Engine;
using Assistant.VScripts.Nodes;
using Razor.UI.VScriptEditor;
using Xunit;

namespace Razor.Core.Tests
{
    public class VScriptFunctionAndErrorTests
    {
        /// <summary>Purer Daten-Node (keine Flow-Pins), dessen Execute wirft.</summary>
        private sealed class ThrowingPureNode : VScriptNode
        {
            public ThrowingPureNode(string id, string pinId)
                : base(id, "Get Diff Hits (test)", NodeCategory.Game)
            {
                OutputPins.Add(new NodePin(pinId, id, "Value", PinType.Number, PinKind.Output));
            }

            public override void Execute(VScriptContext context)
            {
                throw new NullReferenceException("no object connected");
            }
        }

        private static void RunToCompletion(VScriptEngine engine, NodeGraph graph)
        {
            engine.LoadGraph(graph);
            engine.Start();

            var sw = Stopwatch.StartNew();
            while (engine.IsRunning && sw.ElapsedMilliseconds < 3000)
                Thread.Sleep(10);

            Assert.False(engine.IsRunning, "Engine haengt");
        }

        [Fact]
        public void Fehler_in_purem_Daten_Node_wird_am_Verursacher_attribuiert()
        {
            var graph = new NodeGraph("error-attribution");
            var start = new StartNode(graph.GetNextNodeId(), graph.GetNextPinId());
            var print = new PrintMessageNode(graph.GetNextNodeId(), graph.GetNextPinId());
            var source = new ThrowingPureNode(graph.GetNextNodeId(), graph.GetNextPinId());
            graph.AddNode(start);
            graph.AddNode(print);
            graph.AddNode(source);

            // Start -> Print (Flow), Print.Message <- ThrowingPureNode.Value (Daten)
            graph.AddLink(new NodeLink(graph.GetNextLinkId(),
                start.OutputPins.First(p => p.Type == PinType.Flow).Id,
                print.InputPins.First(p => p.Type == PinType.Flow).Id));
            graph.AddLink(new NodeLink(graph.GetNextLinkId(),
                source.OutputPins[0].Id,
                print.InputPins.First(p => p.Type != PinType.Flow).Id));

            var engine = new VScriptEngine();
            RunToCompletion(engine, graph);

            var errors = engine.GetNodeErrors();

            // Der VERURSACHER (purer Node) ist markiert — mit seinem Namen in
            // der Meldung — und der anfragende Flow-Node ebenfalls (Kontext).
            Assert.True(errors.ContainsKey(source.Id), "Verursacher-Node nicht markiert");
            Assert.Contains("Get Diff Hits (test)", errors[source.Id]);
            Assert.True(errors.ContainsKey(print.Id), "Flow-Node nicht markiert");
        }

        [Fact]
        public void SetVariable_parst_kommaseparierten_Text_am_ListPin()
        {
            var context = new VScriptContext();
            var setVar = new SetVariableNode("n1", "p1", "Swords", PinType.Number, isList: true);

            NodePin valuePin = setVar.InputPins.First(p => p.Name == "Swords []");
            valuePin.Value = "121020, 0x67B2, 26546";

            setVar.Execute(context);

            var list = Assert.IsType<System.Collections.Generic.List<float>>(context.GetVariable("Swords"));
            Assert.Equal(new[] { 121020f, 0x67B2, 26546f }, list);

            // ForEach akzeptiert das Ergebnis als Liste.
            Assert.True(context.GetVariable("Swords") is System.Collections.IList);
        }

        [Fact]
        public void FunctionHelper_Signatur_und_Erkennung()
        {
            var graph = new NodeGraph("fn");
            Assert.False(FunctionHelper.IsFunctionLike(graph));

            graph.Variables.Add(new ScriptVariable("amount", PinType.Number,
                scope: VariableScope.Parameter));
            graph.Variables.Add(new ScriptVariable("result", PinType.String,
                scope: VariableScope.Output));

            Assert.True(FunctionHelper.IsFunctionLike(graph));
            Assert.Equal("(amount: Number) → (result: String)", FunctionHelper.Signature(graph));
        }

        [Fact]
        public void CreateCallNode_baut_Parameter_und_Output_Pins_aus_dem_Zielscript()
        {
            const string targetName = "fn_test_target_delete_me";

            var target = new NodeGraph(targetName);
            target.Variables.Add(new ScriptVariable("amount", PinType.Number,
                scope: VariableScope.Parameter));
            target.Variables.Add(new ScriptVariable("result", PinType.String,
                scope: VariableScope.Output));

            Assert.True(VScriptService.SaveScript(targetName, target));

            try
            {
                var owner = new NodeGraph("caller");
                ExecuteScriptNode call = FunctionHelper.CreateCallNode(owner, targetName);

                Assert.Equal(targetName, call.SelectedScriptName);
                Assert.Contains(call.InputPins, p => p.Name == "amount" && p.Type == PinType.Number);
                Assert.Contains(call.OutputPins, p => p.Name == "result" && p.Type == PinType.String);

                // Drag-off-Kompatibilitaet: passt an einen Number-Output und an
                // einen String-Input.
                Assert.True(PinCompat.NodeAccepts(call,
                    new NodePin("px", "nx", "x", PinType.Number, PinKind.Output)));
                Assert.True(PinCompat.NodeAccepts(call,
                    new NodePin("py", "ny", "y", PinType.String, PinKind.Input)));
            }
            finally
            {
                VScriptService.DeleteScript(targetName);
            }
        }
    }
}
