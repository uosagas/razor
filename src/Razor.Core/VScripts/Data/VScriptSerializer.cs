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

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using Assistant.VScripts.Core;
using Assistant.VScripts.Nodes;

namespace Assistant.VScripts.Data;

// JsonSerializerContext for NativeAOT compatibility
[JsonSerializable(typeof(SerializableGraph))]
[JsonSerializable(typeof(SerializableNode))]
[JsonSerializable(typeof(SerializablePin))]
[JsonSerializable(typeof(SerializableLink))]
[JsonSerializable(typeof(SerializableVariable))]
[JsonSerializable(typeof(SerializableCommentBox))]
[JsonSerializable(typeof(List<SerializableNode>))]
[JsonSerializable(typeof(List<SerializablePin>))]
[JsonSerializable(typeof(List<SerializableLink>))]
[JsonSerializable(typeof(List<SerializableVariable>))]
[JsonSerializable(typeof(List<SerializableCommentBox>))]
[JsonSerializable(typeof(SerializableFindFilter))]
[JsonSerializable(typeof(List<SerializableFindFilter>))]
internal partial class VScriptJsonContext : JsonSerializerContext
{
}

/// <summary>
/// RAZOR-ZUSATZ (FindFilters.cs): wird nur geschrieben, wenn Filter existieren
/// (WhenWritingNull) — Dateien ohne Filter bleiben byte-identisch zum Client.
/// </summary>
public class SerializableFindFilter
{
    public string Type { get; set; }
    public string Value { get; set; }
    public bool Negate { get; set; }
    public bool Or { get; set; }
    public bool UsePin { get; set; }
    public string PinId { get; set; }
}

public class SerializableNode
{
    public string Id { get; set; }
    public string TypeName { get; set; }
    public string Name { get; set; }
    public string Category { get; set; }
    public float PositionX { get; set; }
    public float PositionY { get; set; }
    public List<SerializablePin> InputPins { get; set; }
    public List<SerializablePin> OutputPins { get; set; }
    public string VariableName { get; set; } // For Get/SetVariableNode
    public string VariableObjectSubType { get; set; } // For GetVariableNode
    public bool VariableIsList { get; set; } // For Get/SetVariableNode
    public string MessageType { get; set; } // For PrintMessageNode
    public string SayMessageType { get; set; } // For SayNode
    public int SelectedSpellId { get; set; } // For CastSpellNode
    public bool WaitForTarget { get; set; } // For CastSpellNode
    public int Timeout { get; set; } // For CastSpellNode
    public int SelectedSkillIndex { get; set; } // For GetSkillNode and UseSkillNode
    public bool UseBaseValue { get; set; } // For GetSkillNode
    public string TargetEntityType { get; set; } // For ExecuteTargetEntityNode
    public uint SerialOrType { get; set; } // For UseItemNode
    public string UseItemMode { get; set; } // For UseItemNode
    public string ItemLocation { get; set; } // For UseItemNode
    public bool BreakOnFailure { get; set; } // For TargetCursorNode
    public int OutputPinCount { get; set; } // For SequenceNode
    public string HandSelection { get; set; } // For ClearHandsNode
    public uint ClearHandsDelay { get; set; } // For ClearHandsNode
    public string DropLocation { get; set; } // For DropNode
    public List<string> ListInitValues { get; set; } // For SetVariableNode list initialization
    // For FindMobilesNode
    public string FindMobilesMode { get; set; }
    public bool? IsDead { get; set; }
    public bool? IsFemale { get; set; }
    public bool? IsHuman { get; set; }
    public bool? IsPoisoned { get; set; }
    public bool? IsParalyzed { get; set; }
    public double? RangeMin { get; set; }
    public double? RangeMax { get; set; }
    public List<string> FilterNames { get; set; }
    public List<ushort> FilterHues { get; set; }
    public List<ushort> FilterBodies { get; set; }
    public List<byte> FilterNotorieties { get; set; }
    public List<uint> FilterSerials { get; set; }
    // For MoveItemsNode
    public string MoveItemsSelectionMode { get; set; }
    public bool MoveItemsAllItems { get; set; }
    public int MoveItemsAmount { get; set; }
    public int MoveItemsDelayMs { get; set; }
    public string MoveItemsMethod { get; set; }
    // For FindItemsNode
    public string FindItemsMode { get; set; }
    public List<ushort> ExcludedItemIDs { get; set; }
    public bool? IsCorpse { get; set; }
    public bool? IsContainer { get; set; }
    public bool? IsMovable { get; set; }
    public bool? IsOnGround { get; set; }
    public string ItemName { get; set; }
    public List<ushort> ItemGraphics { get; set; }
    public List<byte> ItemLayers { get; set; }
    public List<ushort> ItemHues { get; set; }
    // For MessageOverheadNode
    public string MessageOverheadTarget { get; set; }
    // For ExecuteScriptNode
    public string SelectedScriptName { get; set; }
    public bool ExecuteScriptWait { get; set; }

    // RAZOR-ZUSATZ: Filterkette fuer FindItems-/FindMobilesNode (FindFilters.cs).
    // Nur bei Nutzung geschrieben; der Client ignoriert das unbekannte Feld.
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<SerializableFindFilter> FindFilters { get; set; }
}

public class SerializablePin
{
    public string Id { get; set; }
    public string NodeId { get; set; }
    public string Name { get; set; }
    public string Type { get; set; }
    public string Kind { get; set; }
    public string Value { get; set; }
}

public class SerializableLink
{
    public string Id { get; set; }
    public string StartPinId { get; set; }
    public string EndPinId { get; set; }
}

public class SerializableVariable
{
    public string Name { get; set; }
    public string Type { get; set; }
    public string DefaultValue { get; set; }
    public string ObjectSubType { get; set; }
    public string Scope { get; set; }
    public bool IsList { get; set; }
}

public class SerializableCommentBox
{
    public string Id { get; set; }
    public string Title { get; set; }
    public float PositionX { get; set; }
    public float PositionY { get; set; }
    public float SizeX { get; set; }
    public float SizeY { get; set; }
    public float ColorR { get; set; }
    public float ColorG { get; set; }
    public float ColorB { get; set; }
    public float ColorA { get; set; }
    public string MoveMode { get; set; }
    public int ZOrder { get; set; }
}

public class SerializableGraph
{
    public string Name { get; set; }
    public List<SerializableNode> Nodes { get; set; }
    public List<SerializableLink> Links { get; set; }
    public List<SerializableVariable> Variables { get; set; }
    public List<SerializableCommentBox> CommentBoxes { get; set; }
}

public static class VScriptSerializer
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        TypeInfoResolver = VScriptJsonContext.Default
    };

    public static string Serialize(NodeGraph graph)
    {
        var serializableGraph = new SerializableGraph
        {
            Name = graph.Name,
            Nodes = new List<SerializableNode>(),
            Links = new List<SerializableLink>(),
            Variables = new List<SerializableVariable>(),
            CommentBoxes = new List<SerializableCommentBox>()
        };

        foreach (var node in graph.Nodes)
        {
            var serializableNode = new SerializableNode
            {
                Id = node.Id,
                TypeName = node.GetType().Name,
                Name = node.Name,
                Category = node.Category.ToString(),
                PositionX = node.Position.X,
                PositionY = node.Position.Y,
                InputPins = new List<SerializablePin>(),
                OutputPins = new List<SerializablePin>(),
                VariableName = (node is GetVariableNode getVar) ? getVar.VariableName :
                               (node is SetVariableNode setVar) ? setVar.VariableName : null,
                VariableObjectSubType = (node is GetVariableNode getVarSub) ? getVarSub.ObjectSubType.ToString() :
                                        (node is SetVariableNode setVarSub) ? setVarSub.ObjectSubType.ToString() : null,
                VariableIsList = (node is GetVariableNode getVarList) ? getVarList.IsList :
                                 (node is SetVariableNode setVarList) ? setVarList.IsList : false,
                MessageType = (node is PrintMessageNode printMsg) ? printMsg.MessageType.ToString() : null,
                SayMessageType = (node is SayNode sayNode) ? sayNode.MessageType.ToString() : null,
                SelectedSpellId = (node is CastSpellNode castSpell) ? castSpell.SelectedSpellId : 0,
                WaitForTarget = (node is CastSpellNode castSpellWait) ? castSpellWait.WaitForTarget : false,
                Timeout = (node is CastSpellNode castSpellTimeout) ? castSpellTimeout.Timeout :
                          (node is WaitForTargetNode waitForTarget) ? waitForTarget.Timeout :
                          (node is TargetCursorNode targetCursor) ? targetCursor.Timeout : 10000,
                SelectedSkillIndex = (node is GetSkillNode getSkill) ? getSkill.SelectedSkillIndex :
                                     (node is UseSkillNode useSkill) ? useSkill.SelectedSkillIndex : 0,
                UseBaseValue = (node is GetSkillNode getSkillBase) ? getSkillBase.UseBaseValue : false,
                TargetEntityType = (node is ExecuteTargetEntityNode executeTargetEntity) ? executeTargetEntity.EntityType.ToString() : null,
                SerialOrType = (node is UseItemNode useItem) ? useItem.SerialOrType : 0,
                UseItemMode = (node is UseItemNode useItemMode) ? useItemMode.Mode.ToString() : null,
                ItemLocation = (node is UseItemNode useItemLocation) ? useItemLocation.Location.ToString() : null,
                BreakOnFailure = (node is TargetCursorNode targetCursorBreak) ? targetCursorBreak.BreakOnFailure : false,
                OutputPinCount = (node is SequenceNode sequenceNode) ? sequenceNode.OutputPinCount : 2,
                HandSelection = (node is ClearHandsNode clearHands) ? clearHands.Hand.ToString() : null,
                ClearHandsDelay = (node is ClearHandsNode clearHandsDelay) ? clearHandsDelay.Delay : 500,
                DropLocation = (node is DropNode dropNode) ? dropNode.Location.ToString() : null,
                ListInitValues = (node is SetVariableNode setVarInit) ? setVarInit.ListInitValues : null,
                // FindMobilesNode properties
                FindMobilesMode = (node is FindMobilesNode findMobiles) ? findMobiles.SearchMode.ToString() : null,
                IsDead = (node is FindMobilesNode findMobDead) ? findMobDead.IsDead : null,
                IsFemale = (node is FindMobilesNode findMobFemale) ? findMobFemale.IsFemale : null,
                IsHuman = (node is FindMobilesNode findMobHuman) ? findMobHuman.IsHuman : null,
                IsPoisoned = (node is FindMobilesNode findMobPoisoned) ? findMobPoisoned.IsPoisoned : null,
                IsParalyzed = (node is FindMobilesNode findMobParalyzed) ? findMobParalyzed.IsParalyzed : null,
                RangeMin = (node is FindMobilesNode findMobRangeMin) ? findMobRangeMin.RangeMin : null,
                RangeMax = (node is FindMobilesNode findMobRangeMax) ? findMobRangeMax.RangeMax : null,
                FilterNames = (node is FindMobilesNode findMobNames) ? findMobNames.Names : null,
                FilterHues = (node is FindMobilesNode findMobHues) ? findMobHues.Hues : null,
                FilterBodies = (node is FindMobilesNode findMobBodies) ? findMobBodies.Bodies : null,
                FilterNotorieties = (node is FindMobilesNode findMobNotorieties) ? findMobNotorieties.Notorieties : null,
                FilterSerials = (node is FindMobilesNode findMobSerials) ? findMobSerials.Serials : null,
                // MoveItemsNode properties
                MoveItemsSelectionMode = (node is MoveItemsNode moveItemsMode) ? moveItemsMode.SelectionMode.ToString() : null,
                MoveItemsAllItems = (node is MoveItemsNode moveItems) ? moveItems.AllItems : false,
                MoveItemsAmount = (node is MoveItemsNode moveItemsAmount) ? moveItemsAmount.Amount : 1,
                MoveItemsDelayMs = (node is MoveItemsNode moveItemsDelay) ? moveItemsDelay.DelayMs : 650,
                MoveItemsMethod = (node is MoveItemsNode moveItemsMethod) ? moveItemsMethod.Method.ToString() : null,
                // FindItemsNode properties
                FindItemsMode = (node is FindItemsNode findItems) ? findItems.SearchMode.ToString() : null,
                ExcludedItemIDs = (node is FindItemsNode findItemsExcluded) ? findItemsExcluded.ExcludedItemIDs : null,
                IsCorpse = (node is FindItemsNode findItemsCorpse) ? findItemsCorpse.IsCorpse : null,
                IsContainer = (node is FindItemsNode findItemsContainer) ? findItemsContainer.IsContainer : null,
                IsMovable = (node is FindItemsNode findItemsMovable) ? findItemsMovable.IsMovable : null,
                IsOnGround = (node is FindItemsNode findItemsGround) ? findItemsGround.IsOnGround : null,
                ItemName = (node is FindItemsNode findItemsName) ? findItemsName.Name : null,
                ItemGraphics = (node is FindItemsNode findItemsGraphics) ? findItemsGraphics.Graphics : null,
                ItemLayers = (node is FindItemsNode findItemsLayers) ? findItemsLayers.Layers : null,
                ItemHues = (node is FindItemsNode findItemsHues) ? findItemsHues.Hues : null,
                // MessageOverheadNode properties
                MessageOverheadTarget = (node is MessageOverheadNode msgOverhead) ? msgOverhead.Target.ToString() : null,
                // ExecuteScriptNode properties
                SelectedScriptName = (node is ExecuteScriptNode execScript) ? execScript.SelectedScriptName : null,
                ExecuteScriptWait = (node is ExecuteScriptNode execScriptWait) ? execScriptWait.Wait : true,
                // Razor-Zusatz: Filterkette (null, wenn ungenutzt -> Feld entfaellt)
                FindFilters = SerializeFindFilters(node)
            };

            foreach (var pin in node.InputPins)
            {
                serializableNode.InputPins.Add(new SerializablePin
                {
                    Id = pin.Id,
                    NodeId = pin.NodeId,
                    Name = pin.Name,
                    Type = pin.Type.ToString(),
                    Kind = pin.Kind.ToString(),
                    Value = pin.Value?.ToString()
                });
            }

            foreach (var pin in node.OutputPins)
            {
                serializableNode.OutputPins.Add(new SerializablePin
                {
                    Id = pin.Id,
                    NodeId = pin.NodeId,
                    Name = pin.Name,
                    Type = pin.Type.ToString(),
                    Kind = pin.Kind.ToString(),
                    Value = pin.Value?.ToString()
                });
            }

            serializableGraph.Nodes.Add(serializableNode);
        }

        foreach (var link in graph.Links)
        {
            serializableGraph.Links.Add(new SerializableLink
            {
                Id = link.Id,
                StartPinId = link.StartPinId,
                EndPinId = link.EndPinId
            });
        }

        foreach (var variable in graph.Variables)
        {
            serializableGraph.Variables.Add(new SerializableVariable
            {
                Name = variable.Name,
                Type = variable.Type.ToString(),
                DefaultValue = variable.DefaultValue?.ToString(),
                ObjectSubType = variable.ObjectSubType.ToString(),
                Scope = variable.Scope.ToString(),
                IsList = variable.IsList
            });
        }

        foreach (var commentBox in graph.CommentBoxes)
        {
            serializableGraph.CommentBoxes.Add(new SerializableCommentBox
            {
                Id = commentBox.Id,
                Title = commentBox.Title,
                PositionX = commentBox.Position.X,
                PositionY = commentBox.Position.Y,
                SizeX = commentBox.Size.X,
                SizeY = commentBox.Size.Y,
                ColorR = commentBox.Color.X,
                ColorG = commentBox.Color.Y,
                ColorB = commentBox.Color.Z,
                ColorA = commentBox.Color.W,
                MoveMode = commentBox.MoveMode.ToString(),
                ZOrder = commentBox.ZOrder
            });
        }

        return JsonSerializer.Serialize(serializableGraph, Options);
    }

    /// <summary>
    /// Razor-Zusatz: IsList/ObjectSubType stehen NICHT in der Datei (Format
    /// bleibt client-identisch) — sie werden nach dem Pin-Restore aus einer
    /// frischen Instanz des Node-Typs uebernommen (Match ueber Name+Kind).
    /// Dynamische Pins (Execute-Script-Parameter, Filter-Pins) haben kein
    /// Template-Gegenstueck und bleiben unveraendert.
    /// </summary>
    private static void RepairPinMetadata(VScriptNode node)
    {
        // Variablen-Nodes sind nicht in der Factory — ihre Listen-/Subtyp-Info
        // steht in den eigenen Properties.
        switch (node)
        {
            case GetVariableNode getVar:
            {
                foreach (var pin in node.InputPins.Concat(node.OutputPins).Where(p => p.Type != PinType.Flow))
                {
                    pin.IsList = getVar.IsList;
                    pin.ObjectSubType = getVar.ObjectSubType;
                }

                return;
            }
            case SetVariableNode setVar:
            {
                foreach (var pin in node.InputPins.Concat(node.OutputPins).Where(p => p.Type != PinType.Flow))
                {
                    pin.IsList = setVar.IsList;
                    pin.ObjectSubType = setVar.ObjectSubType;
                }

                return;
            }
        }

        VScriptNode template;
        try
        {
            template = NodeFactory.Create(node.GetType().Name, "tpl_repair", "tplpin_repair");
        }
        catch
        {
            return;
        }

        if (template == null)
            return;

        CopyPinMetadata(node.InputPins, template.InputPins);
        CopyPinMetadata(node.OutputPins, template.OutputPins);
    }

    private static void CopyPinMetadata(List<NodePin> pins, List<NodePin> templatePins)
    {
        foreach (var pin in pins)
        {
            NodePin match = templatePins.FirstOrDefault(t => t.Name == pin.Name && t.Kind == pin.Kind);
            if (match != null)
            {
                pin.IsList = match.IsList;
                pin.ObjectSubType = match.ObjectSubType;
            }
        }
    }

    /// <summary>Razor-Zusatz: Filterkette der Find-Nodes (null = Feld entfaellt im JSON).</summary>
    private static List<SerializableFindFilter> SerializeFindFilters(VScriptNode node)
    {
        List<FindFilter> filters = node switch
        {
            FindItemsNode fi => fi.Filters,
            FindMobilesNode fm => fm.Filters,
            _ => null
        };

        if (filters == null || filters.Count == 0)
            return null;

        return filters.Select(f => new SerializableFindFilter
        {
            Type = f.Type,
            Value = f.Value,
            Negate = f.Negate,
            Or = f.Or,
            UsePin = f.UsePin,
            PinId = f.PinId
        }).ToList();
    }

    public static NodeGraph Deserialize(string json)
    {
        var serializableGraph = JsonSerializer.Deserialize<SerializableGraph>(json, Options);
        if (serializableGraph == null)
        {
            return null;
        }

        var graph = new NodeGraph(serializableGraph.Name);

        var nodeIdMap = new Dictionary<string, VScriptNode>();

        foreach (var serNode in serializableGraph.Nodes)
        {
            VScriptNode node = CreateNodeFromType(serNode);
            if (node != null)
            {
                node.Position = new System.Numerics.Vector2(serNode.PositionX, serNode.PositionY);

                // Razor-Zusatz: gespeicherten Titel wiederherstellen (Funktions-
                // Aufrufe tragen den Funktionsnamen; bei allen anderen Nodes ist
                // der gespeicherte Name ohnehin der Konstruktor-Name).
                if (!string.IsNullOrEmpty(serNode.Name))
                    node.Name = serNode.Name;

                // Restore pins with their original IDs and values
                node.InputPins.Clear();
                foreach (var serPin in serNode.InputPins)
                {
                    if (Enum.TryParse<PinType>(serPin.Type, out var pinType))
                    {
                        var pin = new NodePin(serPin.Id, node.Id, serPin.Name, pinType, PinKind.Input);
                        if (!string.IsNullOrEmpty(serPin.Value))
                        {
                            pin.Value = serPin.Value;
                        }
                        node.InputPins.Add(pin);
                    }
                }

                node.OutputPins.Clear();
                foreach (var serPin in serNode.OutputPins)
                {
                    if (Enum.TryParse<PinType>(serPin.Type, out var pinType))
                    {
                        var pin = new NodePin(serPin.Id, node.Id, serPin.Name, pinType, PinKind.Output);
                        if (!string.IsNullOrEmpty(serPin.Value))
                        {
                            pin.Value = serPin.Value;
                        }
                        node.OutputPins.Add(pin);
                    }
                }

                // Razor-Zusatz: Pin-Metadaten reparieren. Das Dateiformat kennt
                // weder IsList noch ObjectSubType — ohne Reparatur verlieren
                // z. B. Find-Items "Items" (Liste von Items) und ForEach "List"
                // ihre Listen-/Subtyp-Info bei jedem Laden.
                RepairPinMetadata(node);

                // Razor-Zusatz: Filterkette wiederherstellen (PinIds referenzieren
                // die soeben restaurierten Pins).
                if (serNode.FindFilters != null)
                {
                    var restored = serNode.FindFilters.Select(sf => new FindFilter
                    {
                        Type = sf.Type,
                        Value = sf.Value,
                        Negate = sf.Negate,
                        Or = sf.Or,
                        UsePin = sf.UsePin,
                        PinId = sf.PinId
                    }).ToList();

                    if (node is FindItemsNode fiNode)
                        fiNode.Filters = restored;
                    else if (node is FindMobilesNode fmNode)
                        fmNode.Filters = restored;
                }

                graph.AddNode(node);
                nodeIdMap[node.Id] = node;
            }
        }

        foreach (var serLink in serializableGraph.Links)
        {
            graph.Links.Add(new NodeLink(serLink.Id, serLink.StartPinId, serLink.EndPinId));
        }

        if (serializableGraph.Variables != null)
        {
            foreach (var serVar in serializableGraph.Variables)
            {
                if (Enum.TryParse<PinType>(serVar.Type, out var varType))
                {
                    ObjectSubType objectSubType = ObjectSubType.Player;
                    if (!string.IsNullOrEmpty(serVar.ObjectSubType))
                    {
                        // Try to parse, but if it's "Generic" (old format), default to Player
                        if (!Enum.TryParse<ObjectSubType>(serVar.ObjectSubType, out objectSubType))
                        {
                            objectSubType = ObjectSubType.Player;
                        }
                    }

                    VariableScope scope = VariableScope.Local;
                    if (!string.IsNullOrEmpty(serVar.Scope))
                    {
                        Enum.TryParse<VariableScope>(serVar.Scope, out scope);
                    }

                    var variable = new ScriptVariable(serVar.Name, varType, objectSubType, scope, serVar.IsList);
                    graph.Variables.Add(variable);
                }
            }

            // NOTE: Do NOT call UpdateParameterPins/UpdateOutputPins here!
            // The pins have already been restored from serialized data with their original IDs.
            // Calling Update*Pins would regenerate them with new GUIDs, breaking link connections.
        }

        // Rebuild pin mappings for special nodes that have internal dictionaries
        // This restores the name->pinId mappings without regenerating the pin IDs
        foreach (var node in graph.Nodes)
        {
            if (node is StartNode startNode)
            {
                startNode.RebuildParameterPinMapping();
            }
            else if (node is ReturnNode returnNode)
            {
                returnNode.RebuildOutputPinMapping();
            }
            else if (node is ExecuteScriptNode execNode)
            {
                execNode.RebuildPinMappings();
            }
        }

        // Deserialize comment boxes
        if (serializableGraph.CommentBoxes != null)
        {
            foreach (var serCommentBox in serializableGraph.CommentBoxes)
            {
                var moveMode = CommentBoxMoveMode.GroupMovement;
                if (!string.IsNullOrEmpty(serCommentBox.MoveMode))
                {
                    Enum.TryParse<CommentBoxMoveMode>(serCommentBox.MoveMode, out moveMode);
                }

                var commentBox = new CommentBox
                {
                    Id = serCommentBox.Id,
                    Title = serCommentBox.Title,
                    Position = new System.Numerics.Vector2(serCommentBox.PositionX, serCommentBox.PositionY),
                    Size = new System.Numerics.Vector2(serCommentBox.SizeX, serCommentBox.SizeY),
                    Color = new System.Numerics.Vector4(serCommentBox.ColorR, serCommentBox.ColorG, serCommentBox.ColorB, serCommentBox.ColorA),
                    MoveMode = moveMode,
                    ZOrder = serCommentBox.ZOrder
                };
                graph.CommentBoxes.Add(commentBox);
            }
        }

        return graph;
    }

    private static VScriptNode CreateNodeFromType(SerializableNode serNode)
    {
        // For now, we need to manually map type names to constructors
        // In a more advanced system, we could use reflection
        string pinCounter = serNode.InputPins.Count > 0 ? serNode.InputPins[0].Id : Guid.NewGuid().ToString();

        return serNode.TypeName switch
        {
            nameof(StartNode) => new StartNode(serNode.Id, pinCounter),
            nameof(PrintMessageNode) => CreatePrintMessageNode(serNode, pinCounter),
            nameof(SetVariableNode) => CreateSetVariableNode(serNode, pinCounter),
            nameof(GetVariableNode) => CreateGetVariableNode(serNode, pinCounter),
            nameof(DelayNode) => new DelayNode(serNode.Id, pinCounter),
            nameof(SequenceNode) => CreateSequenceNode(serNode, pinCounter),
            nameof(ForLoopNode) => new ForLoopNode(serNode.Id, pinCounter),
            nameof(WhileLoopNode) => new WhileLoopNode(serNode.Id, pinCounter),
            nameof(ForEachNode) => new ForEachNode(serNode.Id, pinCounter),
            nameof(BreakNode) => new BreakNode(serNode.Id, pinCounter),
            // Math nodes
            nameof(AddNumbersNode) => new AddNumbersNode(serNode.Id, pinCounter),
            nameof(SubtractNumbersNode) => new SubtractNumbersNode(serNode.Id, pinCounter),
            nameof(MultiplyNumbersNode) => new MultiplyNumbersNode(serNode.Id, pinCounter),
            nameof(DivideNumbersNode) => new DivideNumbersNode(serNode.Id, pinCounter),
            nameof(ModuloNode) => new ModuloNode(serNode.Id, pinCounter),
            nameof(PowerNode) => new PowerNode(serNode.Id, pinCounter),
            nameof(SquareRootNode) => new SquareRootNode(serNode.Id, pinCounter),
            nameof(AbsoluteNode) => new AbsoluteNode(serNode.Id, pinCounter),
            nameof(MinNode) => new MinNode(serNode.Id, pinCounter),
            nameof(MaxNode) => new MaxNode(serNode.Id, pinCounter),
            nameof(ClampNode) => new ClampNode(serNode.Id, pinCounter),
            nameof(RoundNode) => new RoundNode(serNode.Id, pinCounter),
            nameof(FloorNode) => new FloorNode(serNode.Id, pinCounter),
            nameof(CeilingNode) => new CeilingNode(serNode.Id, pinCounter),
            // Conversion nodes
            nameof(NumberToStringNode) => new NumberToStringNode(serNode.Id, pinCounter),
            nameof(StringToNumberNode) => new StringToNumberNode(serNode.Id, pinCounter),
            nameof(BooleanToStringNode) => new BooleanToStringNode(serNode.Id, pinCounter),
            nameof(StringToBooleanNode) => new StringToBooleanNode(serNode.Id, pinCounter),
            nameof(NumberToBooleanNode) => new NumberToBooleanNode(serNode.Id, pinCounter),
            nameof(BooleanToNumberNode) => new BooleanToNumberNode(serNode.Id, pinCounter),
            nameof(ConcatenateStringsNode) => new ConcatenateStringsNode(serNode.Id, pinCounter),
            nameof(AppendStringNode) => new AppendStringNode(serNode.Id, pinCounter),
            nameof(StringLengthNode) => new StringLengthNode(serNode.Id, pinCounter),
            nameof(StringContainsNode) => new StringContainsNode(serNode.Id, pinCounter),
            nameof(StringToUpperNode) => new StringToUpperNode(serNode.Id, pinCounter),
            nameof(StringToLowerNode) => new StringToLowerNode(serNode.Id, pinCounter),
            // Logic nodes
            nameof(BranchNode) => new BranchNode(serNode.Id, pinCounter),
            nameof(CompareNumbersNode) => new CompareNumbersNode(serNode.Id, pinCounter),
            nameof(NotNode) => new NotNode(serNode.Id, pinCounter),
            nameof(AndNode) => new AndNode(serNode.Id, pinCounter),
            nameof(OrNode) => new OrNode(serNode.Id, pinCounter),
            nameof(IsValidNode) => new IsValidNode(serNode.Id, pinCounter),
            // Game nodes
            nameof(PlayerNode) => new PlayerNode(serNode.Id, pinCounter),
            nameof(GetPlayerHitsNode) => new GetPlayerHitsNode(serNode.Id, pinCounter),
            nameof(GetPlayerHitsMaxNode) => new GetPlayerHitsMaxNode(serNode.Id, pinCounter),
            nameof(GetPlayerStaminaNode) => new GetPlayerStaminaNode(serNode.Id, pinCounter),
            nameof(GetPlayerStaminaMaxNode) => new GetPlayerStaminaMaxNode(serNode.Id, pinCounter),
            nameof(GetPlayerManaNode) => new GetPlayerManaNode(serNode.Id, pinCounter),
            nameof(GetPlayerManaMaxNode) => new GetPlayerManaMaxNode(serNode.Id, pinCounter),
            nameof(GetPlayerStrNode) => new GetPlayerStrNode(serNode.Id, pinCounter),
            nameof(GetPlayerDexNode) => new GetPlayerDexNode(serNode.Id, pinCounter),
            nameof(GetPlayerIntNode) => new GetPlayerIntNode(serNode.Id, pinCounter),
            nameof(GetPlayerHueNode) => new GetPlayerHueNode(serNode.Id, pinCounter),
            nameof(GetPlayerGraphicNode) => new GetPlayerGraphicNode(serNode.Id, pinCounter),
            nameof(GetPlayerSerialNode) => new GetPlayerSerialNode(serNode.Id, pinCounter),
            nameof(GetPlayerDistanceNode) => new GetPlayerDistanceNode(serNode.Id, pinCounter),
            nameof(GetPlayerDirectionNode) => new GetPlayerDirectionNode(serNode.Id, pinCounter),
            nameof(GetPlayerFollowersNode) => new GetPlayerFollowersNode(serNode.Id, pinCounter),
            nameof(GetPlayerMaxFollowersNode) => new GetPlayerMaxFollowersNode(serNode.Id, pinCounter),
            nameof(GetPlayerGoldNode) => new GetPlayerGoldNode(serNode.Id, pinCounter),
            nameof(GetPlayerLuckNode) => new GetPlayerLuckNode(serNode.Id, pinCounter),
            nameof(GetPlayerTithingPointsNode) => new GetPlayerTithingPointsNode(serNode.Id, pinCounter),
            nameof(GetPlayerWeightNode) => new GetPlayerWeightNode(serNode.Id, pinCounter),
            nameof(GetPlayerMaxWeightNode) => new GetPlayerMaxWeightNode(serNode.Id, pinCounter),
            nameof(GetPlayerDiffWeightNode) => new GetPlayerDiffWeightNode(serNode.Id, pinCounter),
            nameof(GetPlayerStatsCapNode) => new GetPlayerStatsCapNode(serNode.Id, pinCounter),
            nameof(GetPlayerPhysicalResistanceNode) => new GetPlayerPhysicalResistanceNode(serNode.Id, pinCounter),
            nameof(GetPlayerDiffHitsNode) => new GetPlayerDiffHitsNode(serNode.Id, pinCounter),
            nameof(GetPlayerIsDestroyedNode) => new GetPlayerIsDestroyedNode(serNode.Id, pinCounter),
            nameof(GetPlayerIsRunningNode) => new GetPlayerIsRunningNode(serNode.Id, pinCounter),
            nameof(GetPlayerIsParalyzedNode) => new GetPlayerIsParalyzedNode(serNode.Id, pinCounter),
            nameof(GetPlayerIsDeadNode) => new GetPlayerIsDeadNode(serNode.Id, pinCounter),
            nameof(GetPlayerIsHiddenNode) => new GetPlayerIsHiddenNode(serNode.Id, pinCounter),
            nameof(GetPlayerIsPoisonedNode) => new GetPlayerIsPoisonedNode(serNode.Id, pinCounter),
            nameof(GetPlayerIsMountedNode) => new GetPlayerIsMountedNode(serNode.Id, pinCounter),
            nameof(GetPlayerIsHumanNode) => new GetPlayerIsHumanNode(serNode.Id, pinCounter),
            nameof(GetPlayerIsYellowHitsNode) => new GetPlayerIsYellowHitsNode(serNode.Id, pinCounter),
            nameof(GetPlayerIsFemaleNode) => new GetPlayerIsFemaleNode(serNode.Id, pinCounter),
            nameof(GetPlayerNameNode) => new GetPlayerNameNode(serNode.Id, pinCounter),
            nameof(GetPlayerTitleNode) => new GetPlayerTitleNode(serNode.Id, pinCounter),
            nameof(GetPlayerNotorietyFlagNode) => new GetPlayerNotorietyFlagNode(serNode.Id, pinCounter),
            nameof(GetPlayerRaceNode) => new GetPlayerRaceNode(serNode.Id, pinCounter),
            // Mobile nodes
            nameof(MobileNode) => new MobileNode(serNode.Id, pinCounter),
            nameof(GetMobileXNode) => new GetMobileXNode(serNode.Id, pinCounter),
            nameof(GetMobileYNode) => new GetMobileYNode(serNode.Id, pinCounter),
            nameof(GetMobileZNode) => new GetMobileZNode(serNode.Id, pinCounter),
            nameof(GetMobileHueNode) => new GetMobileHueNode(serNode.Id, pinCounter),
            nameof(GetMobileGraphicNode) => new GetMobileGraphicNode(serNode.Id, pinCounter),
            nameof(GetMobileNameNode) => new GetMobileNameNode(serNode.Id, pinCounter),
            nameof(GetMobileSerialNode) => new GetMobileSerialNode(serNode.Id, pinCounter),
            nameof(GetMobileDistanceNode) => new GetMobileDistanceNode(serNode.Id, pinCounter),
            nameof(GetMobileDirectionNode) => new GetMobileDirectionNode(serNode.Id, pinCounter),
            nameof(GetMobileIsDestroyedNode) => new GetMobileIsDestroyedNode(serNode.Id, pinCounter),
            nameof(GetMobileIsRunningNode) => new GetMobileIsRunningNode(serNode.Id, pinCounter),
            nameof(GetMobileIsParalyzedNode) => new GetMobileIsParalyzedNode(serNode.Id, pinCounter),
            nameof(GetMobileIsDeadNode) => new GetMobileIsDeadNode(serNode.Id, pinCounter),
            nameof(GetMobileIsHiddenNode) => new GetMobileIsHiddenNode(serNode.Id, pinCounter),
            nameof(GetMobileIsPoisonedNode) => new GetMobileIsPoisonedNode(serNode.Id, pinCounter),
            nameof(GetMobileIsMountedNode) => new GetMobileIsMountedNode(serNode.Id, pinCounter),
            nameof(GetMobileIsHumanNode) => new GetMobileIsHumanNode(serNode.Id, pinCounter),
            nameof(GetMobileIsYellowHitsNode) => new GetMobileIsYellowHitsNode(serNode.Id, pinCounter),
            nameof(GetMobileIsFemaleNode) => new GetMobileIsFemaleNode(serNode.Id, pinCounter),
            nameof(GetMobileTitleNode) => new GetMobileTitleNode(serNode.Id, pinCounter),
            nameof(GetMobileNotorietyFlagNode) => new GetMobileNotorietyFlagNode(serNode.Id, pinCounter),
            nameof(GetMobileRaceNode) => new GetMobileRaceNode(serNode.Id, pinCounter),
            nameof(GetMobileHitsNode) => new GetMobileHitsNode(serNode.Id, pinCounter),
            nameof(GetMobileHitsMaxNode) => new GetMobileHitsMaxNode(serNode.Id, pinCounter),
            nameof(GetMobileDiffHitsNode) => new GetMobileDiffHitsNode(serNode.Id, pinCounter),
            nameof(GetMobileStaminaNode) => new GetMobileStaminaNode(serNode.Id, pinCounter),
            nameof(GetMobileStaminaMaxNode) => new GetMobileStaminaMaxNode(serNode.Id, pinCounter),
            nameof(GetMobileManaNode) => new GetMobileManaNode(serNode.Id, pinCounter),
            nameof(GetMobileManaMaxNode) => new GetMobileManaMaxNode(serNode.Id, pinCounter),
            nameof(FindMobilesNode) => CreateFindMobilesNode(serNode, pinCounter),
            nameof(FindItemsNode) => CreateFindItemsNode(serNode, pinCounter),
            // Journal nodes
            nameof(JournalNode) => new JournalNode(serNode.Id, pinCounter),
            nameof(JournalContainsNode) => new JournalContainsNode(serNode.Id, pinCounter),
            nameof(ClearJournalNode) => new ClearJournalNode(serNode.Id, pinCounter),
            nameof(GetJournalEntryTextNode) => new GetJournalEntryTextNode(serNode.Id, pinCounter),
            nameof(GetJournalEntryHueNode) => new GetJournalEntryHueNode(serNode.Id, pinCounter),
            nameof(GetJournalEntryNameNode) => new GetJournalEntryNameNode(serNode.Id, pinCounter),
            nameof(GetJournalEntryTimeNode) => new GetJournalEntryTimeNode(serNode.Id, pinCounter),
            nameof(GetJournalEntryTextTypeNode) => new GetJournalEntryTextTypeNode(serNode.Id, pinCounter),
            // Item nodes
            nameof(ItemNode) => new ItemNode(serNode.Id, pinCounter),
            nameof(GetItemXNode) => new GetItemXNode(serNode.Id, pinCounter),
            nameof(GetItemYNode) => new GetItemYNode(serNode.Id, pinCounter),
            nameof(GetItemZNode) => new GetItemZNode(serNode.Id, pinCounter),
            nameof(GetItemHueNode) => new GetItemHueNode(serNode.Id, pinCounter),
            nameof(GetItemGraphicNode) => new GetItemGraphicNode(serNode.Id, pinCounter),
            nameof(GetItemNameNode) => new GetItemNameNode(serNode.Id, pinCounter),
            nameof(GetItemSerialNode) => new GetItemSerialNode(serNode.Id, pinCounter),
            nameof(GetItemDistanceNode) => new GetItemDistanceNode(serNode.Id, pinCounter),
            nameof(GetItemDirectionNode) => new GetItemDirectionNode(serNode.Id, pinCounter),
            nameof(GetItemIsDestroyedNode) => new GetItemIsDestroyedNode(serNode.Id, pinCounter),
            nameof(GetItemPropertiesNode) => new GetItemPropertiesNode(serNode.Id, pinCounter),
            nameof(GetItemAmountNode) => new GetItemAmountNode(serNode.Id, pinCounter),
            nameof(GetItemIsDamageableNode) => new GetItemIsDamageableNode(serNode.Id, pinCounter),
            nameof(GetItemIsCoinNode) => new GetItemIsCoinNode(serNode.Id, pinCounter),
            nameof(GetItemIsCorpseNode) => new GetItemIsCorpseNode(serNode.Id, pinCounter),
            nameof(GetItemIsEmptyNode) => new GetItemIsEmptyNode(serNode.Id, pinCounter),
            nameof(GetItemIsLockedNode) => new GetItemIsLockedNode(serNode.Id, pinCounter),
            nameof(GetItemIsHiddenNode) => new GetItemIsHiddenNode(serNode.Id, pinCounter),
            nameof(GetItemIsMultiNode) => new GetItemIsMultiNode(serNode.Id, pinCounter),
            nameof(GetItemIsLootableNode) => new GetItemIsLootableNode(serNode.Id, pinCounter),
            nameof(GetItemOnGroundNode) => new GetItemOnGroundNode(serNode.Id, pinCounter),
            nameof(GetItemDisplayedGraphicNode) => new GetItemDisplayedGraphicNode(serNode.Id, pinCounter),
            nameof(GetItemMultiGraphicNode) => new GetItemMultiGraphicNode(serNode.Id, pinCounter),
            nameof(GetItemLayerNode) => new GetItemLayerNode(serNode.Id, pinCounter),
            nameof(GetItemLightIDNode) => new GetItemLightIDNode(serNode.Id, pinCounter),
            nameof(GetItemRootContainerNode) => new GetItemRootContainerNode(serNode.Id, pinCounter),
            nameof(GetItemContainerNode) => new GetItemContainerNode(serNode.Id, pinCounter),
            nameof(MoveItemsNode) => CreateMoveItemsNode(serNode, pinCounter),
            nameof(BandageSelfNode) => new BandageSelfNode(serNode.Id, pinCounter),
            nameof(AttackNode) => new AttackNode(serNode.Id, pinCounter),
            nameof(ToggleWarModeNode) => new ToggleWarModeNode(serNode.Id, pinCounter),
            nameof(PopPouchNode) => new PopPouchNode(serNode.Id, pinCounter),
            nameof(EquipNode) => new EquipNode(serNode.Id, pinCounter),
            nameof(PickupNode) => new PickupNode(serNode.Id, pinCounter),
            nameof(DropNode) => CreateDropNode(serNode, pinCounter),
            nameof(ClearHandsNode) => CreateClearHandsNode(serNode, pinCounter),
            nameof(SayNode) => CreateSayNode(serNode, pinCounter),
            nameof(MessageOverheadNode) => CreateMessageOverheadNode(serNode, pinCounter),
            nameof(CastSpellNode) => CreateCastSpellNode(serNode, pinCounter),
            nameof(GetSkillNode) => CreateGetSkillNode(serNode, pinCounter),
            nameof(UseSkillNode) => CreateUseSkillNode(serNode, pinCounter),
            nameof(UseItemNode) => CreateUseItemNode(serNode, pinCounter),
            nameof(ClickObjectNode) => new ClickObjectNode(serNode.Id, pinCounter),
            // Targeting nodes
            nameof(LastTargetNode) => new LastTargetNode(serNode.Id, pinCounter),
            nameof(SetLastTargetNode) => new SetLastTargetNode(serNode.Id, pinCounter),
            nameof(GetTargetSerialNode) => new GetTargetSerialNode(serNode.Id, pinCounter),
            nameof(GetTargetGraphicNode) => new GetTargetGraphicNode(serNode.Id, pinCounter),
            nameof(GetTargetXNode) => new GetTargetXNode(serNode.Id, pinCounter),
            nameof(GetTargetYNode) => new GetTargetYNode(serNode.Id, pinCounter),
            nameof(GetTargetZNode) => new GetTargetZNode(serNode.Id, pinCounter),
            nameof(GetTargetIsEntityNode) => new GetTargetIsEntityNode(serNode.Id, pinCounter),
            nameof(GetTargetIsStaticNode) => new GetTargetIsStaticNode(serNode.Id, pinCounter),
            nameof(GetTargetIsLandNode) => new GetTargetIsLandNode(serNode.Id, pinCounter),
            nameof(SelectedTargetNode) => new SelectedTargetNode(serNode.Id, pinCounter),
            nameof(SetSelectedTargetNode) => new SetSelectedTargetNode(serNode.Id, pinCounter),
            nameof(ExecuteTargetNode) => new ExecuteTargetNode(serNode.Id, pinCounter),
            nameof(ExecuteTargetEntityNode) => CreateExecuteTargetEntityNode(serNode, pinCounter),
            nameof(WaitForTargetNode) => CreateWaitForTargetNode(serNode, pinCounter),
            nameof(TargetCursorNode) => CreateTargetCursorNode(serNode, pinCounter),
            // List nodes
            nameof(ListAddNode) => new ListAddNode(serNode.Id, pinCounter),
            nameof(ListInsertNode) => new ListInsertNode(serNode.Id, pinCounter),
            nameof(ListRemoveNode) => new ListRemoveNode(serNode.Id, pinCounter),
            nameof(ListRemoveAtNode) => new ListRemoveAtNode(serNode.Id, pinCounter),
            nameof(ListGetNode) => new ListGetNode(serNode.Id, pinCounter),
            nameof(ListSetNode) => new ListSetNode(serNode.Id, pinCounter),
            nameof(ListCountNode) => new ListCountNode(serNode.Id, pinCounter),
            nameof(ListClearNode) => new ListClearNode(serNode.Id, pinCounter),
            nameof(ListContainsNode) => new ListContainsNode(serNode.Id, pinCounter),
            nameof(ListIndexOfNode) => new ListIndexOfNode(serNode.Id, pinCounter),
            nameof(ExecuteScriptNode) => CreateExecuteScriptNode(serNode, pinCounter),
            nameof(ReturnNode) => new ReturnNode(serNode.Id, pinCounter),
            _ => null
        };
    }

    private static VScriptNode CreateExecuteScriptNode(SerializableNode serNode, string pinCounter)
    {
        var node = new ExecuteScriptNode(serNode.Id, pinCounter);

        // Use SetSelectedScriptNameWithoutUpdate to avoid regenerating pins with new IDs.
        // The pins will be restored from serialized data with their original IDs,
        // preserving link connections.
        if (!string.IsNullOrEmpty(serNode.SelectedScriptName))
        {
            node.SetSelectedScriptNameWithoutUpdate(serNode.SelectedScriptName);
        }

        node.Wait = serNode.ExecuteScriptWait;

        return node;
    }

    private static VScriptNode CreateGetVariableNode(SerializableNode serNode, string pinCounter)
    {
        // For GetVariableNode, the output pin type is the variable type
        var variableName = serNode.VariableName ?? "Variable";
        PinType variableType = PinType.String;

        if (serNode.OutputPins.Count > 0)
        {
            Enum.TryParse(serNode.OutputPins[0].Type, out variableType);
        }

        ObjectSubType objectSubType = ObjectSubType.Player;
        if (!string.IsNullOrEmpty(serNode.VariableObjectSubType))
        {
            // Try to parse, but if it's "Generic" (old format), default to Player
            if (!Enum.TryParse<ObjectSubType>(serNode.VariableObjectSubType, out objectSubType))
            {
                objectSubType = ObjectSubType.Player;
            }
        }

        bool isList = serNode.VariableIsList;

        return new GetVariableNode(serNode.Id, pinCounter, variableName, variableType, objectSubType, isList);
    }

    private static VScriptNode CreateSetVariableNode(SerializableNode serNode, string pinCounter)
    {
        // For SetVariableNode, the second input pin (not flow) is the variable type
        var variableName = serNode.VariableName ?? "Variable";
        PinType variableType = PinType.String;

        if (serNode.InputPins.Count > 1)
        {
            Enum.TryParse(serNode.InputPins[1].Type, out variableType);
        }

        ObjectSubType objectSubType = ObjectSubType.Player;
        if (!string.IsNullOrEmpty(serNode.VariableObjectSubType))
        {
            // Try to parse, but if it's "Generic" (old format), default to Player
            if (!Enum.TryParse<ObjectSubType>(serNode.VariableObjectSubType, out objectSubType))
            {
                objectSubType = ObjectSubType.Player;
            }
        }

        bool isList = serNode.VariableIsList;

        var node = new SetVariableNode(serNode.Id, pinCounter, variableName, variableType, objectSubType, isList);

        // Restore list initialization values if any
        if (serNode.ListInitValues != null && serNode.ListInitValues.Count > 0)
        {
            node.ListInitValues = serNode.ListInitValues;
        }

        return node;
    }

    private static VScriptNode CreatePrintMessageNode(SerializableNode serNode, string pinCounter)
    {
        var node = new PrintMessageNode(serNode.Id, pinCounter);

        // Restore MessageType if available
        if (!string.IsNullOrEmpty(serNode.MessageType))
        {
            if (Enum.TryParse<PrintMessageType>(serNode.MessageType, out var messageType))
            {
                node.MessageType = messageType;
            }
        }

        return node;
    }

    private static VScriptNode CreateSayNode(SerializableNode serNode, string pinCounter)
    {
        var node = new SayNode(serNode.Id, pinCounter);

        // Restore SayMessageType if available
        if (!string.IsNullOrEmpty(serNode.SayMessageType))
        {
            if (Enum.TryParse<SayMessageType>(serNode.SayMessageType, out var messageType))
            {
                node.MessageType = messageType;
            }
        }

        return node;
    }

    private static VScriptNode CreateMessageOverheadNode(SerializableNode serNode, string pinCounter)
    {
        var node = new MessageOverheadNode(serNode.Id, pinCounter);

        // Restore MessageOverheadTarget if available
        if (!string.IsNullOrEmpty(serNode.MessageOverheadTarget))
        {
            if (Enum.TryParse<MessageOverheadTarget>(serNode.MessageOverheadTarget, out var target))
            {
                node.Target = target;
                node.UpdatePinsForTarget();
            }
        }

        return node;
    }

    private static VScriptNode CreateFindMobilesNode(SerializableNode serNode, string pinCounter)
    {
        var node = new FindMobilesNode(serNode.Id, pinCounter);

        // Restore SearchMode
        if (!string.IsNullOrEmpty(serNode.FindMobilesMode))
        {
            if (Enum.TryParse<FindMobilesMode>(serNode.FindMobilesMode, out var searchMode))
            {
                node.SearchMode = searchMode;
                node.UpdatePinsForSearchMode();
            }
        }

        // Restore filter properties
        node.IsDead = serNode.IsDead;
        node.IsFemale = serNode.IsFemale;
        node.IsHuman = serNode.IsHuman;
        node.IsPoisoned = serNode.IsPoisoned;
        node.IsParalyzed = serNode.IsParalyzed;
        node.RangeMin = serNode.RangeMin;
        node.RangeMax = serNode.RangeMax;
        node.Names = serNode.FilterNames;
        node.Hues = serNode.FilterHues;
        node.Bodies = serNode.FilterBodies;
        node.Notorieties = serNode.FilterNotorieties;
        node.Serials = serNode.FilterSerials;

        return node;
    }

    private static VScriptNode CreateClearHandsNode(SerializableNode serNode, string pinCounter)
    {
        var node = new ClearHandsNode(serNode.Id, pinCounter);

        // Restore HandSelection if available
        if (!string.IsNullOrEmpty(serNode.HandSelection))
        {
            if (Enum.TryParse<HandSelection>(serNode.HandSelection, out var handSelection))
            {
                node.Hand = handSelection;
            }
        }

        // Restore Delay
        if (serNode.ClearHandsDelay > 0)
        {
            node.Delay = serNode.ClearHandsDelay;
        }

        return node;
    }

    private static VScriptNode CreateDropNode(SerializableNode serNode, string pinCounter)
    {
        var node = new DropNode(serNode.Id, pinCounter);

        // Restore DropLocation if available
        if (!string.IsNullOrEmpty(serNode.DropLocation))
        {
            if (Enum.TryParse<DropLocation>(serNode.DropLocation, out var dropLocation))
            {
                node.Location = dropLocation;
            }
        }

        return node;
    }

    private static VScriptNode CreateMoveItemsNode(SerializableNode serNode, string pinCounter)
    {
        var node = new MoveItemsNode(serNode.Id, pinCounter);

        // Restore SelectionMode if available
        if (!string.IsNullOrEmpty(serNode.MoveItemsSelectionMode))
        {
            if (Enum.TryParse<MoveItemSelectionMode>(serNode.MoveItemsSelectionMode, out var selectionMode))
            {
                node.SelectionMode = selectionMode;
                node.UpdatePinsForMode();
            }
        }

        // Restore configuration
        node.AllItems = serNode.MoveItemsAllItems;
        node.Amount = serNode.MoveItemsAmount > 0 ? serNode.MoveItemsAmount : 1;
        node.DelayMs = serNode.MoveItemsDelayMs > 0 ? serNode.MoveItemsDelayMs : 650;

        // Restore Method if available
        if (!string.IsNullOrEmpty(serNode.MoveItemsMethod))
        {
            if (Enum.TryParse<MoveItemMethod>(serNode.MoveItemsMethod, out var method))
            {
                node.Method = method;
            }
        }

        return node;
    }

    private static VScriptNode CreateFindItemsNode(SerializableNode serNode, string pinCounter)
    {
        var node = new FindItemsNode(serNode.Id, pinCounter);

        // Restore SearchMode if available
        if (!string.IsNullOrEmpty(serNode.FindItemsMode))
        {
            if (Enum.TryParse<FindItemsMode>(serNode.FindItemsMode, out var searchMode))
            {
                node.SearchMode = searchMode;
                node.UpdatePinsForSearchMode();
            }
        }

        // Restore filter properties
        node.ExcludedItemIDs = serNode.ExcludedItemIDs;
        node.IsCorpse = serNode.IsCorpse;
        node.IsContainer = serNode.IsContainer;
        node.IsMovable = serNode.IsMovable;
        node.IsOnGround = serNode.IsOnGround;
        node.Name = serNode.ItemName;
        node.RangeMin = serNode.RangeMin;
        node.RangeMax = serNode.RangeMax;
        node.Graphics = serNode.ItemGraphics;
        node.Layers = serNode.ItemLayers;
        node.Hues = serNode.ItemHues;

        return node;
    }

    private static VScriptNode CreateCastSpellNode(SerializableNode serNode, string pinCounter)
    {
        var node = new CastSpellNode(serNode.Id, pinCounter);

        // Restore properties if available
        if (serNode.SelectedSpellId > 0)
        {
            node.SelectedSpellId = serNode.SelectedSpellId;
        }

        node.WaitForTarget = serNode.WaitForTarget;

        if (serNode.Timeout > 0)
        {
            node.Timeout = serNode.Timeout;
        }

        return node;
    }

    private static VScriptNode CreateGetSkillNode(SerializableNode serNode, string pinCounter)
    {
        var node = new GetSkillNode(serNode.Id, pinCounter);

        node.SelectedSkillIndex = serNode.SelectedSkillIndex;
        node.UseBaseValue = serNode.UseBaseValue;

        return node;
    }

    private static VScriptNode CreateUseSkillNode(SerializableNode serNode, string pinCounter)
    {
        var node = new UseSkillNode(serNode.Id, pinCounter);

        node.SelectedSkillIndex = serNode.SelectedSkillIndex;

        return node;
    }

    private static VScriptNode CreateUseItemNode(SerializableNode serNode, string pinCounter)
    {
        var node = new UseItemNode(serNode.Id, pinCounter);

        node.SerialOrType = serNode.SerialOrType;

        if (!string.IsNullOrEmpty(serNode.UseItemMode))
        {
            if (Enum.TryParse<UseItemMode>(serNode.UseItemMode, out var mode))
            {
                node.Mode = mode;
            }
        }

        if (!string.IsNullOrEmpty(serNode.ItemLocation))
        {
            if (Enum.TryParse<ItemLocation>(serNode.ItemLocation, out var location))
            {
                node.Location = location;
            }
        }

        return node;
    }

    private static VScriptNode CreateExecuteTargetEntityNode(SerializableNode serNode, string pinCounter)
    {
        var node = new ExecuteTargetEntityNode(serNode.Id, pinCounter);

        // Restore EntityType if available
        if (!string.IsNullOrEmpty(serNode.TargetEntityType))
        {
            if (Enum.TryParse<TargetEntityType>(serNode.TargetEntityType, out var entityType))
            {
                node.EntityType = entityType;
            }
        }

        return node;
    }

    private static VScriptNode CreateWaitForTargetNode(SerializableNode serNode, string pinCounter)
    {
        var node = new WaitForTargetNode(serNode.Id, pinCounter);

        if (serNode.Timeout > 0)
        {
            node.Timeout = serNode.Timeout;
        }

        return node;
    }

    private static VScriptNode CreateTargetCursorNode(SerializableNode serNode, string pinCounter)
    {
        var node = new TargetCursorNode(serNode.Id, pinCounter);

        if (serNode.Timeout > 0)
        {
            node.Timeout = serNode.Timeout;
        }

        node.BreakOnFailure = serNode.BreakOnFailure;

        return node;
    }

    private static VScriptNode CreateSequenceNode(SerializableNode serNode, string pinCounter)
    {
        int outputCount = serNode.OutputPinCount > 0 ? serNode.OutputPinCount : 2;
        var node = new SequenceNode(serNode.Id, pinCounter, outputCount);
        return node;
    }

    public static bool SaveToFile(NodeGraph graph, string filePath)
    {
        try
        {
            var json = Serialize(graph);
            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
            File.WriteAllText(filePath, json);
            return true;
        }
        catch (Exception ex)
        {
            Message.Error($"Failed to save VScript: {ex.Message}");
            Console.WriteLine($"Failed to save VScript: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
            return false;
        }
    }

    public static NodeGraph LoadFromFile(string filePath)
    {
        try
        {
            if (!File.Exists(filePath))
            {
                return null;
            }

            var json = File.ReadAllText(filePath);
            return Deserialize(json);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to load VScript: {ex.Message}");
            return null;
        }
    }

    // Variable-only serialization for global variables
    public static string SerializeVariables(List<ScriptVariable> variables)
    {
        var serializableVars = new List<SerializableVariable>();

        foreach (var variable in variables)
        {
            serializableVars.Add(new SerializableVariable
            {
                Name = variable.Name,
                Type = variable.Type.ToString(),
                ObjectSubType = variable.ObjectSubType.ToString(),
                Scope = variable.Scope.ToString()
            });
        }

        return JsonSerializer.Serialize(serializableVars, Options);
    }

    public static List<ScriptVariable> DeserializeVariables(string json)
    {
        var serializableVars = JsonSerializer.Deserialize<List<SerializableVariable>>(json, Options);
        if (serializableVars == null)
        {
            return new List<ScriptVariable>();
        }

        var variables = new List<ScriptVariable>();
        foreach (var serVar in serializableVars)
        {
            var type = Enum.Parse<PinType>(serVar.Type);
            var objectSubType = string.IsNullOrEmpty(serVar.ObjectSubType) ? ObjectSubType.Player : Enum.Parse<ObjectSubType>(serVar.ObjectSubType);
            var scope = string.IsNullOrEmpty(serVar.Scope) ? VariableScope.Local : Enum.Parse<VariableScope>(serVar.Scope);
            variables.Add(new ScriptVariable(serVar.Name, type, objectSubType, scope, serVar.IsList));
        }

        return variables;
    }
}
