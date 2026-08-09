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

// UOSagas-Razor: Tests fuer die Find-Filterketten (Razor-Zusatz, FindFilters.cs):
// AND/OR/NOT-Auswertung, Pin-Wert gewinnt ueber konfigurierten Wert,
// Pin-Synchronisation, Serializer-Roundtrip + Client-Byte-Vertraeglichkeit
// (ohne Filter darf KEIN findFilters-Feld im JSON auftauchen).

using System.Collections.Generic;
using System.Linq;
using Assistant;
using Assistant.VScripts.Core;
using Assistant.VScripts.Data;
using Assistant.VScripts.Nodes;
using Xunit;

namespace Razor.Core.Tests
{
    public class VScriptFindFilterTests
    {
        private static Item MakeItem(ushort graphic, ushort hue = 0)
        {
            return new Item(0x40001234u) { ItemID = graphic, Hue = hue };
        }

        private static bool Chain(List<FindFilter> filters, Item item) =>
            FindFilterCatalog.MatchesChain(filters, item, _ => null, FindFilterCatalog.MatchItem);

        [Fact]
        public void And_Or_und_Negation_werten_links_nach_rechts_aus()
        {
            Item item = MakeItem(0x0EED, hue: 5);

            // Graphic passt UND Hue passt -> true
            var both = new List<FindFilter>
            {
                new() { Type = "Graphic", Value = "0xEED" },
                new() { Type = "Hue", Value = "5" }
            };
            Assert.True(Chain(both, item));

            // Graphic passt UND Hue falsch -> false; mit OR -> true
            var wrongHue = new List<FindFilter>
            {
                new() { Type = "Graphic", Value = "0xEED" },
                new() { Type = "Hue", Value = "99" }
            };
            Assert.False(Chain(wrongHue, item));

            wrongHue[1].Or = true;
            Assert.True(Chain(wrongHue, item));

            // Negation: NOT Graphic(0xEED) -> false
            var negated = new List<FindFilter>
            {
                new() { Type = "Graphic", Value = "0xEED", Negate = true }
            };
            Assert.False(Chain(negated, item));
        }

        [Fact]
        public void Bool_Filter_brauchen_keinen_Wert()
        {
            Item corpse = MakeItem(0x2006);
            Assert.True(corpse.IsCorpse);

            var isCorpse = new List<FindFilter> { new() { Type = "Is Corpse" } };
            Assert.True(Chain(isCorpse, corpse));

            var notCorpse = new List<FindFilter> { new() { Type = "Is Corpse", Negate = true } };
            Assert.False(Chain(notCorpse, corpse));
        }

        [Fact]
        public void PinWert_gewinnt_ueber_konfigurierten_Wert()
        {
            Item item = MakeItem(0x0EED);

            var filter = new FindFilter { Type = "Graphic", Value = "0x9999", UsePin = true, PinId = "fp1" };
            var pin = new NodePin("fp1", "n1", "Filter: Graphic", PinType.Number, PinKind.Input);

            // Pin leer -> konfigurierter (falscher) Wert -> kein Treffer
            Assert.False(FindFilterCatalog.MatchesChain(new List<FindFilter> { filter }, item,
                _ => pin, FindFilterCatalog.MatchItem));

            // Pin liefert den richtigen Wert -> Treffer
            pin.Value = (float) 0x0EED;
            Assert.True(FindFilterCatalog.MatchesChain(new List<FindFilter> { filter }, item,
                _ => pin, FindFilterCatalog.MatchItem));
        }

        [Fact]
        public void SyncFilterPins_legt_und_entfernt_dynamische_Pins()
        {
            var graph = new NodeGraph("filters");
            var node = new FindItemsNode(graph.GetNextNodeId(), graph.GetNextPinId());
            graph.AddNode(node);

            node.Filters.Add(new FindFilter { Type = "Graphics", Value = "0xEED", UsePin = true });
            node.Filters.Add(new FindFilter { Type = "Is Corpse" });

            var removed = FindFilterCatalog.SyncFilterPins(node, node.Filters, forMobiles: false);
            Assert.Empty(removed);

            NodePin pin = node.InputPins.FirstOrDefault(p => p.Id == node.Filters[0].PinId);
            Assert.NotNull(pin);
            Assert.Equal("Filter: Graphics", pin.Name);
            Assert.Equal(PinType.Number, pin.Type);

            // UsePin aus -> Pin wird abgeraeumt und als entfernt gemeldet.
            string oldPinId = node.Filters[0].PinId;
            node.Filters[0].UsePin = false;
            removed = FindFilterCatalog.SyncFilterPins(node, node.Filters, forMobiles: false);

            Assert.Contains(oldPinId, removed);
            Assert.DoesNotContain(node.InputPins, p => p.Id == oldPinId);
        }

        [Fact]
        public void Plural_Werte_matchen_kommasepariert()
        {
            Item item = MakeItem(0x0EED, hue: 5);

            // Graphics-Liste: einer von mehreren passt.
            var graphics = new List<FindFilter> { new() { Type = "Graphics", Value = "0x9999, 0xEED, 42" } };
            Assert.True(Chain(graphics, item));

            var graphicsMiss = new List<FindFilter> { new() { Type = "Graphics", Value = "0x9999, 42" } };
            Assert.False(Chain(graphicsMiss, item));

            // Hues-Liste
            var hues = new List<FindFilter> { new() { Type = "Hues", Value = "3, 5, 7" } };
            Assert.True(Chain(hues, item));

            // Serials-Liste (Item-Serial 0x40001234)
            var serials = new List<FindFilter> { new() { Type = "Serials", Value = "0x40001234, 0x11111111" } };
            Assert.True(Chain(serials, item));

            // Alte Singular-Namen bleiben gueltig (gespeicherte Dateien).
            var legacy = new List<FindFilter> { new() { Type = "Graphic", Value = "0xEED" } };
            Assert.True(Chain(legacy, item));
        }

        [Fact]
        public void Layers_akzeptieren_Namen_und_Nummern()
        {
            Item weapon = MakeItem(0x0F5E);
            weapon.Layer = Layer.RightHand;

            // Enum-Name
            var byName = new List<FindFilter> { new() { Type = "Layers", Value = "RightHand" } };
            Assert.True(Chain(byName, weapon));

            // Script-Alias (onehanded -> RightHand) gemischt mit Nummer
            var byAlias = new List<FindFilter> { new() { Type = "Layers", Value = "onehanded, 22" } };
            Assert.True(Chain(byAlias, weapon));

            // Nummer (RightHand = 1)
            var byNumber = new List<FindFilter> { new() { Type = "Layers", Value = "1" } };
            Assert.True(Chain(byNumber, weapon));

            var miss = new List<FindFilter> { new() { Type = "Layers", Value = "LeftHand, Shoes" } };
            Assert.False(Chain(miss, weapon));
        }

        [Fact]
        public void InContainer_direkt_und_rekursiv()
        {
            // Backpack > Beutel > Bandage (in der World, damit Container aufloesen)
            var backpack = new Item(0x40000B01u) { ItemID = 0x0E75 };
            var pouch = new Item(0x40000B02u) { ItemID = 0x0E79 };
            var bandage = new Item(0x40000B03u) { ItemID = 0x0E21 };
            World.AddItem(backpack);
            World.AddItem(pouch);
            World.AddItem(bandage);
            pouch.Container = backpack;
            bandage.Container = pouch;

            // Direkt: nur der unmittelbare Container zaehlt.
            var direct = new List<FindFilter> { new() { Type = "In Container", Value = "0x40000B02" } };
            Assert.True(Chain(direct, bandage));

            var directBackpack = new List<FindFilter> { new() { Type = "In Container", Value = "0x40000B01" } };
            Assert.False(Chain(directBackpack, bandage));

            // Rekursiv: auch der Backpack weiter oben in der Kette.
            var recursive = new List<FindFilter>
            {
                new() { Type = "In Container (recursive)", Value = "0x40000B01" }
            };
            Assert.True(Chain(recursive, bandage));

            var recursiveMiss = new List<FindFilter>
            {
                new() { Type = "In Container (recursive)", Value = "0x40000BFF" }
            };
            Assert.False(Chain(recursiveMiss, bandage));
        }

        [Fact]
        public void Deserialize_repariert_IsList_und_ObjectSubType_der_Pins()
        {
            // Das Dateiformat kennt IsList/ObjectSubType nicht — ohne Reparatur
            // verlieren Items-/List-Pins ihre Metadaten bei jedem Laden (und die
            // Palette bietet dann Mobile- statt Item-Getter an).
            var graph = new NodeGraph("pin-meta");
            graph.AddNode(new FindItemsNode(graph.GetNextNodeId(), graph.GetNextPinId()));
            graph.AddNode(new ForEachNode(graph.GetNextNodeId(), graph.GetNextPinId()));

            NodeGraph restored = VScriptSerializer.Deserialize(VScriptSerializer.Serialize(graph));

            var find = restored.Nodes.OfType<FindItemsNode>().Single();
            NodePin items = find.OutputPins.Single(p => p.Name == "Items");
            Assert.True(items.IsList);
            Assert.Equal(ObjectSubType.Item, items.ObjectSubType);

            var forEach = restored.Nodes.OfType<ForEachNode>().Single();
            Assert.True(forEach.InputPins.Single(p => p.Name == "List").IsList);
        }

        [Fact]
        public void Serializer_Roundtrip_und_Client_Byte_Vertraeglichkeit()
        {
            var graph = new NodeGraph("filter-roundtrip");
            var find = new FindItemsNode(graph.GetNextNodeId(), graph.GetNextPinId());
            var plain = new FindItemsNode(graph.GetNextNodeId(), graph.GetNextPinId());
            graph.AddNode(find);
            graph.AddNode(plain);

            find.Filters.Add(new FindFilter { Type = "Graphic", Value = "0xEED", UsePin = true });
            find.Filters.Add(new FindFilter { Type = "Is Corpse", Negate = true, Or = true });
            FindFilterCatalog.SyncFilterPins(find, find.Filters, forMobiles: false);

            string json = VScriptSerializer.Serialize(graph);

            // Ohne Filter existiert das Feld gar nicht (Client-Byte-Kompatibilitaet):
            // genau EIN Node traegt findFilters.
            int occurrences = json.Split("findFilters").Length - 1;
            Assert.Equal(1, occurrences);

            NodeGraph restored = VScriptSerializer.Deserialize(json);
            var restoredFind = restored.Nodes.OfType<FindItemsNode>()
                .Single(n => n.Filters.Count > 0);

            Assert.Equal(2, restoredFind.Filters.Count);
            Assert.Equal("Graphic", restoredFind.Filters[0].Type);
            Assert.True(restoredFind.Filters[0].UsePin);
            Assert.True(restoredFind.Filters[1].Negate);
            Assert.True(restoredFind.Filters[1].Or);

            // Der dynamische Filter-Pin ueberlebt den Roundtrip mit derselben Id.
            Assert.Contains(restoredFind.InputPins, p => p.Id == restoredFind.Filters[0].PinId);
        }
    }
}
