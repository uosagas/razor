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

// UOSagas-Razor: Phase 5b — die VScript-Engine fuehrt Graphen gegen das
// Razor-Weltmodell aus. Smoke-Tests: Flow + Datenpin-Pull + Variablen +
// Branch + Game-Node (Say) + OPL-Cache (0xD6).

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Assistant;
using Assistant.Macros;
using Assistant.VScripts.Core;
using Assistant.VScripts.Engine;
using Assistant.VScripts.Nodes;
using Xunit;

namespace Razor.Core.Tests
{
    [Collection("ConfigSequential")]
    public class VScriptEngineTests : IDisposable
    {
        private const uint PlayerSerial = 0x00001101;
        private const uint ChestSerial = 0x40001102;

        private readonly string m_TempDir;
        private readonly CultureInfo m_OldCulture;
        private readonly FakeClientServices m_Fake;

        public VScriptEngineTests()
        {
            m_OldCulture = CultureInfo.CurrentCulture;
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;

            m_TempDir = Path.Combine(Path.GetTempPath(), "RazorVScriptTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(m_TempDir);
            Config.Initialize(m_TempDir);
            Config.LoadLastProfile();

            PacketHandlers.Initialize();
            MacroManager.Stop();
            ActionQueue.Stop();
            Assistant.Core.OplCache.Clear();

            World.Clear();
            PlayerData player = new PlayerData(PlayerSerial)
            {
                Name = "Tester",
                Position = new Point3D(100, 100, 0),
                Visible = true
            };
            World.AddMobile(player);
            World.Player = player;

            m_Fake = new FakeClientServices();
            ClientProxy.Bind(m_Fake);
        }

        public void Dispose()
        {
            ClientProxy.Unbind();
            Assistant.Core.OplCache.Clear();
            World.Clear();
            CultureInfo.CurrentCulture = m_OldCulture;

            try
            {
                Directory.Delete(m_TempDir, true);
            }
            catch
            {
            }
        }

        // ------------------------------------------------------------ helpers

        private static VScriptNode Create(NodeGraph graph, string typeName, float x = 0, float y = 0)
        {
            VScriptNode node = NodeFactory.CreateWithGraph(typeName, graph.GetNextNodeId(),
                graph.GetNextPinId(), graph);
            node.Position = new System.Numerics.Vector2(x, y);
            graph.AddNode(node);
            return node;
        }

        private static void Link(NodeGraph graph, NodePin from, NodePin to)
        {
            graph.AddLink(new NodeLink(graph.GetNextLinkId(), from.Id, to.Id));
        }

        private static NodePin FlowOut(VScriptNode node) =>
            node.OutputPins.First(p => p.Type == PinType.Flow);

        private static NodePin FlowIn(VScriptNode node) =>
            node.InputPins.First(p => p.Type == PinType.Flow);

        // ------------------------------------------------------------ engine

        [Fact]
        public void Engine_fuehrt_Flow_und_Datenpins_aus()
        {
            // Start -> SetVariable("sum" = Add(2, 3)) -> Ende.
            var graph = new NodeGraph("smoke");
            graph.Variables.Add(new ScriptVariable("sum", PinType.Number));

            var start = Create(graph, nameof(StartNode));
            var add = Create(graph, nameof(AddNumbersNode));
            // Get/SetVariableNode entstehen im Client ueber das Variablen-Panel,
            // nicht die Palette — direkt konstruieren (wie der Serializer).
            var setVar = new SetVariableNode(graph.GetNextNodeId(), graph.GetNextPinId(),
                "sum", PinType.Number, ObjectSubType.Player, false);
            graph.AddNode(setVar);

            add.InputPins[0].Value = 2f;
            add.InputPins[1].Value = 3f;

            Link(graph, FlowOut(start), FlowIn(setVar));
            // Datenpin: Add.Result -> SetVariable.Value
            NodePin valuePin = setVar.InputPins.First(p => p.Type != PinType.Flow);
            Link(graph, add.OutputPins[0], valuePin);

            var context = new VScriptContext();
            var engine = new VScriptEngine();
            engine.ExecuteGraphSynchronously(graph, context);

            Assert.True(string.IsNullOrEmpty(context.ErrorMessage), context.ErrorMessage);
            // ExecuteGraphSynchronously kopiert die Variablen des Parent-Contexts —
            // Ergebnis liegt im Kind-Kontext; wir pruefen ueber einen zweiten Weg:
            // GetVariable im selben Graphlauf via PrintMessage waere UI. Stattdessen
            // direkt: der SetVariable-Node hat den Add-Wert gesehen.
            Assert.Equal(5f, Convert.ToSingle(valuePin.Value));
        }

        [Fact]
        public void Engine_Branch_folgt_der_Bedingung()
        {
            // Start -> Branch(cond) -> True: Say("yes") / False: Say("no")
            var graph = new NodeGraph("branch");

            var start = Create(graph, nameof(StartNode));
            var branch = (BranchNode) Create(graph, nameof(BranchNode));
            var sayYes = Create(graph, nameof(SayNode));
            var sayNo = Create(graph, nameof(SayNode));

            sayYes.InputPins.First(p => p.Name == "Message").Value = "yes";
            sayNo.InputPins.First(p => p.Name == "Message").Value = "no";

            Link(graph, FlowOut(start), FlowIn(branch));
            branch.InputPins.First(p => p.Type == PinType.Boolean).Value = true;

            NodePin truePin = branch.OutputPins.First(p => p.Name == "True");
            NodePin falsePin = branch.OutputPins.First(p => p.Name == "False");
            Link(graph, truePin, FlowIn(sayYes));
            Link(graph, falsePin, FlowIn(sayNo));

            var context = new VScriptContext();
            var engine = new VScriptEngine();
            engine.ExecuteGraphSynchronously(graph, context);

            Assert.True(string.IsNullOrEmpty(context.ErrorMessage), context.ErrorMessage);

            // Say sendet 0xAD an den Server — nur "yes" darf rausgegangen sein.
            byte[] speech = Assert.Single(m_Fake.SentToServer, p => p[0] == 0xAD);
            string text = Encoding.BigEndianUnicode.GetString(speech); // Text ist BE-Unicode
            Assert.Contains("yes", text);
            Assert.Single(m_Fake.SentToServer); // kein zweites Say
        }

        // ------------------------------------------------------------ OPL / 0xD6

        [Fact]
        public void OplCache_liest_MegaCliloc_und_fuellt_GetItemProperties()
        {
            m_Fake.Clilocs[1050039] = "~1_NUMBER~ ~2_ITEMNAME~"; // "a wooden chest"
            m_Fake.Clilocs[1072789] = "Weight: ~1_WEIGHT~ stones";

            Item chest = new Item(ChestSerial) { ItemID = 0x0E43 };
            World.AddItem(chest);

            // 0xD6: unknown(2)=1 serial(4) 0(2) revision(4) [cliloc(4) len(2) textLE]* 0-Terminator
            var b = new List<byte> { 0xD6, 0, 0 };
            void UShort(ushort v) { b.Add((byte)(v >> 8)); b.Add((byte)v); }
            void UInt(uint v)
            {
                b.Add((byte)(v >> 24)); b.Add((byte)(v >> 16)); b.Add((byte)(v >> 8)); b.Add((byte)v);
            }
            void ArgsLE(string s)
            {
                byte[] bytes = Encoding.Unicode.GetBytes(s);
                UShort((ushort) bytes.Length);
                b.AddRange(bytes);
            }

            UShort(1);
            UInt(ChestSerial);
            UShort(0);
            UInt(42); // revision
            UInt(1050039);
            ArgsLE("1\ta wooden chest");
            UInt(1072789);
            ArgsLE("4");
            UInt(0); // terminator

            int len = b.Count;
            b[1] = (byte)(len >> 8);
            b[2] = (byte)len;
            PacketHandler.OnServerPacket(0xD6, new PacketReader(b.ToArray(), true), null);

            Assert.True(Assistant.Core.OplCache.TryGet(ChestSerial, out string name, out string data));
            Assert.Equal("1 a wooden chest", name);
            Assert.Contains("Weight: 4 stones", data);

            // World.OPL-Shim (GetItemPropertiesNode-Pfad) liefert dieselben Daten.
            Assert.True(World.OPL.TryGetNameAndData(ChestSerial, out string oplName, out string oplData));
            Assert.Equal("1 a wooden chest", oplName);
            Assert.Contains("Weight", oplData);

            // Und der Item-Name im Weltmodell wurde uebernommen.
            Assert.Equal("1 a wooden chest", World.FindItem(ChestSerial).Name);
        }
    }
}
