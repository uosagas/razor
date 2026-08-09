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
using System.Linq;

namespace Assistant.VScripts.Core;

public class NodeGraph
{
    public string Name { get; set; }
    public List<VScriptNode> Nodes { get; set; }
    public List<NodeLink> Links { get; set; }
    public List<ScriptVariable> Variables { get; set; }
    public List<CommentBox> CommentBoxes { get; set; }

    public NodeGraph(string name)
    {
        Name = name;
        Nodes = new List<VScriptNode>();
        Links = new List<NodeLink>();
        Variables = new List<ScriptVariable>();
        CommentBoxes = new List<CommentBox>();
    }

    public string GetNextNodeId() => Guid.NewGuid().ToString();
    public string GetNextPinId() => Guid.NewGuid().ToString();
    public string GetNextLinkId() => Guid.NewGuid().ToString();

    public void AddNode(VScriptNode node)
    {
        Nodes.Add(node);
        // No ID counter maintenance needed with GUIDs
    }

    public void RemoveNode(string nodeId)
    {
        var node = Nodes.FirstOrDefault(n => n.Id == nodeId);
        if (node != null)
        {
            Nodes.Remove(node);

            // Remove all links connected to this node
            Links.RemoveAll(link =>
            {
                var startPin = GetPin(link.StartPinId);
                var endPin = GetPin(link.EndPinId);
                return startPin?.NodeId == nodeId || endPin?.NodeId == nodeId;
            });
        }
    }

    public void AddLink(NodeLink link)
    {
        Links.Add(link);
        // No ID counter maintenance needed with GUIDs
    }

    public void RemoveLink(string linkId)
    {
        Links.RemoveAll(l => l.Id == linkId);
    }

    public NodePin GetPin(string pinId)
    {
        foreach (var node in Nodes)
        {
            var pin = node.InputPins.FirstOrDefault(p => p.Id == pinId);
            if (pin != null) return pin;

            pin = node.OutputPins.FirstOrDefault(p => p.Id == pinId);
            if (pin != null) return pin;
        }
        return null;
    }

    public VScriptNode GetNodeByPinId(string pinId)
    {
        return Nodes.FirstOrDefault(node =>
            node.InputPins.Any(p => p.Id == pinId) ||
            node.OutputPins.Any(p => p.Id == pinId));
    }

    public List<VScriptNode> GetConnectedNodes(string outputPinId)
    {
        var connectedNodes = new List<VScriptNode>();

        foreach (var link in Links.Where(l => l.StartPinId == outputPinId))
        {
            var targetNode = GetNodeByPinId(link.EndPinId);
            if (targetNode != null)
            {
                connectedNodes.Add(targetNode);
            }
        }

        return connectedNodes;
    }

    public void Clear()
    {
        Nodes.Clear();
        Links.Clear();
        CommentBoxes.Clear();
        // No ID counters to reset with GUIDs
    }

    public void AddCommentBox(CommentBox commentBox)
    {
        CommentBoxes.Add(commentBox);
    }

    public void RemoveCommentBox(string commentBoxId)
    {
        CommentBoxes.RemoveAll(c => c.Id == commentBoxId);
    }

    public CommentBox GetCommentBox(string commentBoxId)
    {
        return CommentBoxes.FirstOrDefault(c => c.Id == commentBoxId);
    }
}
