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
using System.Numerics;

namespace Assistant.VScripts.Core;

/// <summary>
/// Represents a comment box in the VScript editor for organizing and documenting node graphs.
/// Similar to Unreal Engine's Blueprint comment boxes.
/// </summary>
public class CommentBox
{
    /// <summary>
    /// Unique identifier for this comment box
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    /// The title/text displayed in the comment box header
    /// </summary>
    public string Title { get; set; }

    /// <summary>
    /// Position of the top-left corner in graph space
    /// </summary>
    public Vector2 Position { get; set; }

    /// <summary>
    /// Size of the comment box (width, height)
    /// </summary>
    public Vector2 Size { get; set; }

    /// <summary>
    /// Background color of the comment box (RGBA, 0-1 range)
    /// </summary>
    public Vector4 Color { get; set; }

    /// <summary>
    /// Movement mode for the comment box
    /// </summary>
    public CommentBoxMoveMode MoveMode { get; set; }

    /// <summary>
    /// Z-order for rendering (lower values are rendered first/behind)
    /// </summary>
    public int ZOrder { get; set; }

    public CommentBox()
    {
        Id = Guid.NewGuid().ToString();
        Title = "Comment";
        Position = Vector2.Zero;
        Size = new Vector2(200, 150);
        Color = new Vector4(0.2f, 0.4f, 0.6f, 0.5f); // Default blue-ish color with transparency
        MoveMode = CommentBoxMoveMode.GroupMovement;
        ZOrder = 0;
    }

    public CommentBox(string title, Vector2 position, Vector2 size) : this()
    {
        Title = title;
        Position = position;
        Size = size;
    }

    public CommentBox(string title, Vector2 position, Vector2 size, Vector4 color) : this(title, position, size)
    {
        Color = color;
    }

    /// <summary>
    /// Checks if a point is inside this comment box
    /// </summary>
    public bool Contains(Vector2 point)
    {
        return point.X >= Position.X && point.X <= Position.X + Size.X &&
               point.Y >= Position.Y && point.Y <= Position.Y + Size.Y;
    }

    /// <summary>
    /// Checks if a point is in the header area (for dragging)
    /// </summary>
    public bool IsInHeader(Vector2 point, float headerHeight = 25f)
    {
        return point.X >= Position.X && point.X <= Position.X + Size.X &&
               point.Y >= Position.Y && point.Y <= Position.Y + headerHeight;
    }

    /// <summary>
    /// Checks if a point is in the resize handle area (bottom-right corner)
    /// </summary>
    public bool IsInResizeHandle(Vector2 point, float handleSize = 15f)
    {
        var handlePos = Position + Size - new Vector2(handleSize, handleSize);
        return point.X >= handlePos.X && point.X <= Position.X + Size.X &&
               point.Y >= handlePos.Y && point.Y <= Position.Y + Size.Y;
    }

    /// <summary>
    /// Checks if a node (by its bounding box) is fully contained within this comment box
    /// </summary>
    public bool ContainsNode(Vector2 nodePosition, Vector2 nodeSize)
    {
        return nodePosition.X >= Position.X &&
               nodePosition.Y >= Position.Y &&
               nodePosition.X + nodeSize.X <= Position.X + Size.X &&
               nodePosition.Y + nodeSize.Y <= Position.Y + Size.Y;
    }
}

/// <summary>
/// Defines how the comment box behaves when moved
/// </summary>
public enum CommentBoxMoveMode
{
    /// <summary>
    /// All nodes fully inside the box move with it
    /// </summary>
    GroupMovement,

    /// <summary>
    /// Only the comment box moves, nodes stay in place
    /// </summary>
    CommentOnly
}
