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

// UOSagas-Razor: Tests fuer die Pin-Kompatibilitaet des VScript-Editors
// (Drag-off-Palette, UX-Schwung 2026-07). Die Regeln folgen dem In-Client-
// Editor (ArePinsCompatible/CanAutoConvert): Flow nur mit Flow, Any mit allen
// Datentypen, Auto-Konvertierung Number/String/Boolean; die Palette filtert
// generisch ueber Template-Instanzen der NodeFactory.

using System.Linq;
using Assistant.VScripts.Core;
using Assistant.VScripts.Nodes;
using Razor.UI.VScriptEditor;
using Xunit;

namespace Razor.Core.Tests
{
    public class VScriptEditorCompatTests
    {
        private static NodePin Pin(PinType type, PinKind kind, bool isList = false) =>
            new("p1", "n1", "test", type, kind, isList);

        private static NodePin Pin2(PinType type, PinKind kind, bool isList = false) =>
            new("p2", "n2", "test2", type, kind, isList);

        [Fact]
        public void Flow_verbindet_nur_mit_Flow()
        {
            Assert.True(PinCompat.AreTypesCompatible(PinType.Flow, PinType.Flow));
            Assert.False(PinCompat.AreTypesCompatible(PinType.Flow, PinType.Number));
            Assert.False(PinCompat.AreTypesCompatible(PinType.Any, PinType.Flow));
        }

        [Fact]
        public void Any_und_AutoKonvertierung_wie_im_Client()
        {
            Assert.True(PinCompat.AreTypesCompatible(PinType.Any, PinType.Object));
            Assert.True(PinCompat.AreTypesCompatible(PinType.Number, PinType.String));
            Assert.True(PinCompat.AreTypesCompatible(PinType.Boolean, PinType.Number));
            Assert.False(PinCompat.AreTypesCompatible(PinType.Object, PinType.Number));
            Assert.False(PinCompat.AreTypesCompatible(PinType.Variable, PinType.Number));
        }

        [Fact]
        public void CanConnect_verlangt_Output_zu_Input_und_verschiedene_Nodes()
        {
            NodePin output = Pin(PinType.Number, PinKind.Output);
            NodePin input = Pin2(PinType.Number, PinKind.Input);

            Assert.True(PinCompat.CanConnect(output, input));
            Assert.False(PinCompat.CanConnect(input, output));

            // Gleicher Node: verboten.
            NodePin sameNodeInput = new("p3", "n1", "x", PinType.Number, PinKind.Input);
            Assert.False(PinCompat.CanConnect(output, sameNodeInput));
        }

        [Fact]
        public void Templates_existieren_fuer_alle_Palette_Nodes()
        {
            var templates = PinCompat.GetTemplates();

            Assert.True(templates.Count >= 180, $"nur {templates.Count} Definitionen");
            // Fast alle muessen instanziierbar sein (Filter-Grundlage).
            int usable = templates.Count(t => t.Template != null);
            Assert.True(usable >= templates.Count - 5, $"nur {usable} Templates instanziierbar");
        }

        [Fact]
        public void DragOff_von_Flow_Output_findet_Aktions_Nodes_aber_keine_reinen_Daten_Nodes()
        {
            var templates = PinCompat.GetTemplates();
            NodePin flowOut = Pin(PinType.Flow, PinKind.Output);

            var print = templates.First(t => t.Def.TypeName == nameof(PrintMessageNode));
            var add = templates.First(t => t.Def.TypeName == nameof(AddNumbersNode));

            Assert.True(PinCompat.NodeAccepts(print.Template, flowOut));  // hat Flow-Input
            Assert.False(PinCompat.NodeAccepts(add.Template, flowOut));   // reiner Daten-Node
        }

        [Fact]
        public void DragOff_von_Number_Output_findet_Math_Nodes()
        {
            var templates = PinCompat.GetTemplates();
            NodePin numberOut = Pin(PinType.Number, PinKind.Output);

            var add = templates.First(t => t.Def.TypeName == nameof(AddNumbersNode));
            Assert.True(PinCompat.NodeAccepts(add.Template, numberOut));
        }

        [Fact]
        public void DragOff_aus_Input_Pin_findet_Nodes_mit_passendem_Output()
        {
            var templates = PinCompat.GetTemplates();

            // Aus einem Number-INPUT gezogen: Add liefert einen Number-Output.
            NodePin numberIn = Pin(PinType.Number, PinKind.Input);
            var add = templates.First(t => t.Def.TypeName == nameof(AddNumbersNode));
            Assert.True(PinCompat.NodeAccepts(add.Template, numberIn));

            // PrintMessage hat nur einen Flow-Output -> passt nicht an einen Number-Input.
            var print = templates.First(t => t.Def.TypeName == nameof(PrintMessageNode));
            Assert.False(PinCompat.NodeAccepts(print.Template, numberIn));
        }

        [Fact]
        public void DragOff_von_Item_Liste_zeigt_nur_Item_Getter()
        {
            var templates = PinCompat.GetTemplates();

            // Quelle wie der Items-Output von Find Items / das propagierte
            // ForEach-Element: Object mit SubType Item.
            var itemOut = new NodePin("p1", "n1", "Element", PinType.Object, PinKind.Output,
                objectSubType: ObjectSubType.Item);

            var itemGetter = templates.First(t => t.Def.TypeName == nameof(GetItemNameNode));
            var mobileGetter = templates.First(t => t.Def.TypeName == nameof(GetMobileNameNode));

            Assert.True(PinCompat.NodeAccepts(itemGetter.Template, itemOut));
            Assert.False(PinCompat.NodeAccepts(mobileGetter.Template, itemOut));

            // Implizite Player-Pins der GetPlayer*-Getter matchen nie.
            var playerGetter = templates.First(t => t.Def.TypeName == nameof(GetPlayerHitsNode));
            Assert.False(PinCompat.NodeAccepts(playerGetter.Template, itemOut));
        }

        [Fact]
        public void Typ_Suffix_unterscheidet_gleichnamige_Getter()
        {
            var templates = PinCompat.GetTemplates();

            Assert.Equal(" (Item)", PinCompat.SubTypeSuffix(
                templates.First(t => t.Def.TypeName == nameof(GetItemNameNode)).Template));
            Assert.Equal(" (Mobile)", PinCompat.SubTypeSuffix(
                templates.First(t => t.Def.TypeName == nameof(GetMobileNameNode)).Template));
        }

        [Fact]
        public void AutoConnect_bevorzugt_exakten_Typ()
        {
            var graph = new NodeGraph("t");
            var add = new AddNumbersNode(graph.GetNextNodeId(), graph.GetNextPinId());

            NodePin numberOut = Pin(PinType.Number, PinKind.Output);
            NodePin target = PinCompat.FindAutoConnectPin(add, numberOut);

            Assert.NotNull(target);
            Assert.Equal(PinType.Number, target.Type);
            Assert.Equal(PinKind.Input, target.Kind);
        }

        [Fact]
        public void GetVariableNode_passt_an_kompatible_Daten_Pins()
        {
            var get = new GetVariableNode("g1", "gp1", "myNum", PinType.Number);

            Assert.True(PinCompat.NodeAccepts(get, Pin(PinType.Number, PinKind.Input)));
            Assert.True(PinCompat.NodeAccepts(get, Pin(PinType.String, PinKind.Input))); // Auto-Konvertierung
            Assert.False(PinCompat.NodeAccepts(get, Pin(PinType.Flow, PinKind.Input)));
        }

        [Fact]
        public void ListPin_verlangt_listenfaehige_Gegenseite()
        {
            var templates = PinCompat.GetTemplates();
            NodePin listOut = Pin(PinType.Object, PinKind.Output, isList: true);

            // ForEach nimmt eine Liste an.
            var forEach = templates.First(t => t.Def.TypeName == nameof(ForEachNode));
            Assert.True(PinCompat.NodeAccepts(forEach.Template, listOut));

            // Ein reiner Zahlen-Node bietet keinen Auto-Connect fuer eine Objekt-Liste.
            Assert.Null(PinCompat.FindAutoConnectPin(new AddNumbersNode("x", "xp"), listOut));
        }
    }
}
