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

// UOSagas-Razor: NodeExport — generiert vscript-nodes.json fuer die
// Scripting-Site aus den ECHTEN NodeFactory-Registrierungen (D15: die Doku
// wird aus dem Code generiert, nie von Hand gepflegt).
//
// Pro Definition wird eine Instanz erzeugt und die realen Pins (Name/Typ/
// Richtung) mit exportiert. Get-/SetVariableNode sind nicht in der Factory
// (sie entstehen ueber das Variablen-Panel) und werden hier explizit mit
// einer Beispiel-Variable ergaenzt, damit die Referenz vollstaendig ist.
//
// Aufruf:  dotnet run --project tools/NodeExport [-- --out <pfad>]
// Default: ../scripting-site/src/data/vscript-nodes.json  (eigenes Repo uosagas/scripting-site)

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Assistant.VScripts.Core;
using Assistant.VScripts.Nodes;

string outPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,
    "..", "..", "..", "..", "..", "..",
    "scripting-site", "src", "data", "vscript-nodes.json"));

for (int i = 0; i < args.Length - 1; i++)
{
    if (args[i] == "--out")
        outPath = Path.GetFullPath(args[i + 1]);
}

var graph = new NodeGraph("export");

object PinList(IEnumerable<NodePin> pins) => pins.Select(p => new
{
    name = p.Name ?? string.Empty,
    type = p.Type.ToString(),
    list = p.IsList
}).ToList();

object Describe(string name, string typeName, NodeCategory category, string description, VScriptNode node,
    bool experimental = false) => new
{
    name,
    typeName,
    category = category.ToString(),
    description = description ?? string.Empty,
    experimental,
    inputs = PinList(node.InputPins),
    outputs = PinList(node.OutputPins)
};

var nodes = new List<object>();
var failures = new List<string>();

foreach (NodeDefinition def in NodeFactory.GetAllDefinitions())
{
    try
    {
        VScriptNode node = NodeFactory.CreateWithGraph(def.TypeName,
            graph.GetNextNodeId(), graph.GetNextPinId(), graph);

        if (node == null)
        {
            failures.Add($"{def.TypeName}: factory returned null");
            continue;
        }

        nodes.Add(Describe(def.Name, def.TypeName, def.Category, def.Description, node, def.IsExperimental));
    }
    catch (Exception ex)
    {
        failures.Add($"{def.TypeName}: {ex.Message}");
    }
}

// Variablen-Nodes (nicht in der Factory — kommen ueber das Variablen-Panel).
var getVar = new GetVariableNode(graph.GetNextNodeId(), graph.GetNextPinId(),
    "myVariable", PinType.Number, ObjectSubType.Player, false);
nodes.Add(Describe("Get Variable", nameof(GetVariableNode), NodeCategory.Variable,
    "Reads a script variable. Created from the Variables panel; the output pin takes the variable's type.",
    getVar));

var setVar = new SetVariableNode(graph.GetNextNodeId(), graph.GetNextPinId(),
    "myVariable", PinType.Number, ObjectSubType.Player, false);
nodes.Add(Describe("Set Variable", nameof(SetVariableNode), NodeCategory.Variable,
    "Writes a script variable. Created from the Variables panel; the value pin takes the variable's type.",
    setVar));

if (failures.Count > 0)
{
    Console.Error.WriteLine("EXPORT FAILURES:");
    foreach (string f in failures)
        Console.Error.WriteLine("  " + f);
    Environment.Exit(1);
}

var categories = nodes
    .Select(n => (string) n.GetType().GetProperty("category")!.GetValue(n))
    .Distinct()
    .OrderBy(c => c)
    .ToList();

var payload = new
{
    generated = "razor-nodeexport (NodeFactory registrations, do not edit by hand)",
    count = nodes.Count,
    categories,
    nodes = nodes
        .OrderBy(n => (string) n.GetType().GetProperty("category")!.GetValue(n))
        .ThenBy(n => (string) n.GetType().GetProperty("name")!.GetValue(n))
        .ToList()
};

Directory.CreateDirectory(Path.GetDirectoryName(outPath)!);
File.WriteAllText(outPath, JsonSerializer.Serialize(payload, new JsonSerializerOptions
{
    WriteIndented = true
}));

Console.WriteLine($"OK: {nodes.Count} nodes, {categories.Count} categories -> {outPath}");
