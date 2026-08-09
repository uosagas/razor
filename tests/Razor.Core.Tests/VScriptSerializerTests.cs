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

// UOSagas-Razor: Phase 5a — Dateikompatibilitaet des VScript-Ports.
//
// Die Fixtures sind ECHTE .vscript-Graphen aus dem integrierten Assistant
// (bin/Data/VScripts des Clients). Der Port muss sie verlustfrei lesen und
// wieder schreiben — Graphen sind zwischen integriertem Assistant und Razor
// 1:1 austauschbar (Rueckwaerts-Kompatibilitaet).

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Assistant.VScripts.Core;
using Assistant.VScripts.Data;
using Xunit;

namespace Razor.Core.Tests
{
    public class VScriptSerializerTests
    {
        private static string FixturePath(string name)
        {
            return Path.Combine(AppContext.BaseDirectory, "Fixtures", "VScripts", name);
        }

        public static IEnumerable<object[]> AllFixtures()
        {
            foreach (string file in Directory.GetFiles(
                         Path.Combine(AppContext.BaseDirectory, "Fixtures", "VScripts"), "*.vscript"))
                yield return new object[] { Path.GetFileName(file) };
        }

        [Theory]
        [MemberData(nameof(AllFixtures))]
        public void Fixture_laedt_ohne_Verluste(string fileName)
        {
            string json = File.ReadAllText(FixturePath(fileName));
            NodeGraph graph = VScriptSerializer.Deserialize(json);

            Assert.NotNull(graph);

            // Referenzzahlen direkt aus dem JSON (ohne den Serializer).
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            var root = doc.RootElement;
            int jsonNodes = root.GetProperty("nodes").GetArrayLength();
            int jsonLinks = root.GetProperty("links").GetArrayLength();
            int jsonVars = root.GetProperty("variables").GetArrayLength();

            Assert.Equal(jsonNodes, graph.Nodes.Count);
            Assert.Equal(jsonLinks, graph.Links.Count);
            Assert.Equal(jsonVars, graph.Variables.Count);

            // Jeder Node muss als sein ECHTER Typ ankommen (TypeName == Klassenname);
            // ein Fallback-/Unknown-Typ waere ein Kompatibilitaetsbruch.
            foreach (var node in graph.Nodes)
            {
                System.Text.Json.JsonElement jsonNode = root.GetProperty("nodes").EnumerateArray()
                    .First(n => n.GetProperty("id").GetString() == node.Id);
                string expectedType = jsonNode.GetProperty("typeName").GetString();

                Assert.Equal(expectedType, node.GetType().Name);

                // Pin-Anzahl muss der Datei entsprechen (IDs kommen aus der Datei,
                // damit die Links weiter passen).
                Assert.Equal(jsonNode.GetProperty("inputPins").GetArrayLength(), node.InputPins.Count);
                Assert.Equal(jsonNode.GetProperty("outputPins").GetArrayLength(), node.OutputPins.Count);
            }

            // Alle ECHTEN Links muessen aufloesbar sein (beide Pins existieren).
            // Hinweis: alte Client-Dateien enthalten teils Null-Links (Save-Bug
            // des integrierten Assistants); der Client ignoriert sie still und
            // wir behalten sie werktreu 1:1 bei.
            foreach (var link in graph.Links.Where(l => l.StartPinId != null && l.EndPinId != null))
            {
                Assert.NotNull(graph.GetPin(link.StartPinId));
                Assert.NotNull(graph.GetPin(link.EndPinId));
            }
        }

        [Theory]
        [MemberData(nameof(AllFixtures))]
        public void Roundtrip_ist_stabil(string fileName)
        {
            string json = File.ReadAllText(FixturePath(fileName));

            NodeGraph graph1 = VScriptSerializer.Deserialize(json);
            string out1 = VScriptSerializer.Serialize(graph1);

            NodeGraph graph2 = VScriptSerializer.Deserialize(out1);
            string out2 = VScriptSerializer.Serialize(graph2);

            // Unser eigenes Roundtrip muss byte-stabil sein.
            Assert.Equal(out1, out2);

            // Und strukturgleich zur Originaldatei bleiben.
            Assert.Equal(graph1.Nodes.Count, graph2.Nodes.Count);
            Assert.Equal(graph1.Links.Count, graph2.Links.Count);
            Assert.Equal(graph1.Variables.Count, graph2.Variables.Count);

            for (int i = 0; i < graph1.Nodes.Count; i++)
            {
                Assert.Equal(graph1.Nodes[i].GetType(), graph2.Nodes[i].GetType());
                Assert.Equal(graph1.Nodes[i].Id, graph2.Nodes[i].Id);
                Assert.Equal(graph1.Nodes[i].Position, graph2.Nodes[i].Position);

                for (int p = 0; p < graph1.Nodes[i].InputPins.Count; p++)
                {
                    Assert.Equal(graph1.Nodes[i].InputPins[p].Id, graph2.Nodes[i].InputPins[p].Id);
                    Assert.Equal(graph1.Nodes[i].InputPins[p].Value?.ToString(),
                        graph2.Nodes[i].InputPins[p].Value?.ToString());
                }
            }
        }

        [Fact]
        public void NodeFactory_erzeugt_alle_registrierten_Typen()
        {
            var graph = new NodeGraph("factory-test");
            List<string> failures = new();
            int count = 0;

            foreach (NodeDefinition def in NodeFactory.GetAllDefinitions())
            {
                count++;
                try
                {
                    VScriptNode node = NodeFactory.CreateWithGraph(def.TypeName,
                        graph.GetNextNodeId(), graph.GetNextPinId(), graph);
                    if (node == null)
                        failures.Add($"{def.TypeName}: null");
                    else if (node.GetType().Name != def.TypeName)
                        failures.Add($"{def.TypeName}: erzeugt {node.GetType().Name}");
                }
                catch (Exception ex)
                {
                    failures.Add($"{def.TypeName}: {ex.Message}");
                }
            }

            Assert.True(failures.Count == 0, string.Join("\n", failures));
            Assert.True(count >= 180, $"Nur {count} Node-Typen registriert (Client: ~190)");
        }
    }
}
