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

// UOSagas-Razor: Pin-Kompatibilitaet fuer den VScript-Editor.
//
// Regeln nach dem In-Client-Editor (VScriptEditorComponent.ArePinsCompatible/
// CanAutoConvert): Flow nur mit Flow; Any mit allen Datentypen; gleiche Typen;
// Auto-Konvertierung Number/String/Boolean. Object-Pins verbinden wir tolerant
// untereinander (der Client prueft Subtypen ueber eine hartkodierte Node-Liste;
// pin.ObjectSubType ist dafuer nicht verlaesslich gepflegt — lieber erlauben
// als faelschlich blocken, die Engine behandelt Fehlbelegung zur Laufzeit).
//
// Die Palette-Filterung ist generisch: pro NodeDefinition wird einmalig eine
// Template-Instanz gebaut und gegen die echten Pins geprueft — funktioniert
// damit automatisch fuer alle registrierten Nodes (statt der Client-Liste).

using System;
using System.Collections.Generic;
using System.Linq;
using Assistant.VScripts.Core;

namespace Razor.UI.VScriptEditor
{
    public static class PinCompat
    {
        private static List<(NodeDefinition Def, VScriptNode Template)> _templates;

        /// <summary>Kann ein Link von Output-Pin zu Input-Pin gelegt werden?</summary>
        public static bool CanConnect(NodePin output, NodePin input)
        {
            if (output == null || input == null)
                return false;

            if (output.Kind != PinKind.Output || input.Kind != PinKind.Input)
                return false;

            if (output.NodeId == input.NodeId)
                return false;

            return AreTypesCompatible(output.Type, input.Type);
        }

        /// <summary>Typ-Kompatibilitaet unabhaengig von der Richtung.</summary>
        public static bool AreTypesCompatible(PinType from, PinType to)
        {
            // Flow nur mit Flow.
            if (from == PinType.Flow || to == PinType.Flow)
                return from == PinType.Flow && to == PinType.Flow;

            // Any verbindet sich mit allem (ausser Flow, oben behandelt).
            if (from == PinType.Any || to == PinType.Any)
                return true;

            if (from == to)
                return true;

            // Auto-Konvertierung wie im Client (Conversion-Nodes existieren dafuer).
            return (from, to) switch
            {
                (PinType.Number, PinType.String) => true,
                (PinType.String, PinType.Number) => true,
                (PinType.Boolean, PinType.String) => true,
                (PinType.String, PinType.Boolean) => true,
                (PinType.Number, PinType.Boolean) => true,
                (PinType.Boolean, PinType.Number) => true,
                _ => false
            };
        }

        /// <summary>
        /// Template-Instanzen aller Palette-Nodes (einmalig gebaut, gecacht) —
        /// die Pins der Templates sind die Wahrheit fuer die Filterung.
        /// </summary>
        public static IReadOnlyList<(NodeDefinition Def, VScriptNode Template)> GetTemplates()
        {
            if (_templates != null)
                return _templates;

            var list = new List<(NodeDefinition, VScriptNode)>();
            foreach (var def in NodeFactory.GetAllDefinitions())
            {
                VScriptNode template = null;
                try
                {
                    template = def.Factory?.Invoke($"tpl_{def.TypeName}", $"tplpin_{def.TypeName}");
                }
                catch
                {
                    // Ein Node, der ohne Graph-Kontext nicht instanziierbar ist,
                    // faellt aus der Drag-off-Filterung heraus (Palette zeigt ihn weiter).
                }

                list.Add((def, template));
            }

            _templates = list;
            return _templates;
        }

        /// <summary>
        /// Passt der Node zu einem gezogenen Pin? source.Kind == Output verlangt
        /// einen kompatiblen Input am Node, source.Kind == Input einen Output.
        /// List-Pins verlangen eine List-faehige Gegenseite (oder Any).
        /// </summary>
        public static bool NodeAccepts(VScriptNode template, NodePin source)
        {
            if (template == null || source == null)
                return false;

            IEnumerable<NodePin> candidates = source.Kind == PinKind.Output
                ? template.InputPins.Where(p => !IsImplicitPlayerPin(template, p))
                : template.OutputPins;

            return candidates.Any(p => PinMatches(source, p));
        }

        /// <summary>Erster kompatibler Gegen-Pin am neuen Node (fuer Auto-Connect).</summary>
        public static NodePin FindAutoConnectPin(VScriptNode newNode, NodePin source)
        {
            IEnumerable<NodePin> candidates = (source.Kind == PinKind.Output
                    ? newNode.InputPins.Where(p => !IsImplicitPlayerPin(newNode, p))
                    : newNode.OutputPins.AsEnumerable())
                .ToList();

            // Exakter Typ-Treffer zuerst, dann kompatible (Any/Konvertierung).
            return candidates.FirstOrDefault(p => p.Type == source.Type && PinMatches(source, p))
                   ?? candidates.FirstOrDefault(p => PinMatches(source, p));
        }

        /// <summary>
        /// Erwarteter Objekt-Subtyp eines Pins. Die Client-Nodes kodieren ihn
        /// zuverlaessiger im PIN-NAMEN ("Player"/"Mobile"/"Item"/"Gump") als in
        /// ObjectSubType (dessen Default Player ist).
        /// </summary>
        public static ObjectSubType ExpectedSubType(NodePin pin) => pin.Name switch
        {
            "Player" => ObjectSubType.Player,
            "Mobile" or "Mobiles" => ObjectSubType.Mobile,
            "Item" or "Items" => ObjectSubType.Item,
            "Gump" => ObjectSubType.Gump,
            "Target" => ObjectSubType.Target,
            _ => pin.ObjectSubType
        };

        /// <summary>Object-Subtypen kompatibel? (Player ist ein Mobile — wie der Client.)</summary>
        public static bool SubTypesCompatible(NodePin output, NodePin input)
        {
            if (output.Type != PinType.Object || input.Type != PinType.Object)
                return true;

            ObjectSubType from = ExpectedSubType(output);
            ObjectSubType to = ExpectedSubType(input);

            if (from == to)
                return true;

            return from == ObjectSubType.Player && to == ObjectSubType.Mobile;
        }

        /// <summary>
        /// Impliziter Player-Pin der GetPlayer*-Getter: die Engine nimmt ohne
        /// Verbindung World.Player — der Editor blendet diese Pins aus und die
        /// Palette matcht nicht auf sie.
        /// </summary>
        public static bool IsImplicitPlayerPin(VScriptNode node, NodePin pin) =>
            pin.Kind == PinKind.Input && pin.Type == PinType.Object && pin.Name == "Player" &&
            node != null && node.GetType().Name.StartsWith("GetPlayer", StringComparison.Ordinal);

        /// <summary>Typ-Suffix fuer Palette-Anzeigen: " (Item)"/" (Mobile)"/…,
        /// abgeleitet vom ersten Objekt-Input des Templates.</summary>
        public static string SubTypeSuffix(VScriptNode template)
        {
            if (template == null)
                return string.Empty;

            NodePin objectInput = template.InputPins
                .FirstOrDefault(p => p.Type == PinType.Object && p.Kind == PinKind.Input);
            if (objectInput == null)
                return string.Empty;

            return $" ({ExpectedSubType(objectInput)})";
        }

        private static bool PinMatches(NodePin source, NodePin candidate)
        {
            NodePin output = source.Kind == PinKind.Output ? source : candidate;
            NodePin input = source.Kind == PinKind.Output ? candidate : source;

            if (!AreTypesCompatible(output.Type, input.Type))
                return false;

            // Palette-Filterung ist subtyp-genau: aus einem Item-Element werden
            // nur Item-Getter angeboten (manuelles Verbinden bleibt tolerant).
            if (!SubTypesCompatible(output, input))
                return false;

            return ListMatches(source, candidate);
        }

        private static bool ListMatches(NodePin source, NodePin candidate)
        {
            // Flow-Pins kennen keine Listen.
            if (source.Type == PinType.Flow)
                return true;

            // List-Pin verlangt eine List-faehige Gegenseite; Any schluckt beides.
            if (source.IsList)
                return candidate.IsList || candidate.Type == PinType.Any;

            return !candidate.IsList || candidate.Type == PinType.Any || source.Type == PinType.Any;
        }
    }
}
