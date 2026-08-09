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
using System.Numerics;
using Assistant.VScripts.Core;

namespace Assistant.VScripts.Nodes;

public enum SayMessageType
{
    Regular,
    Guild,
    Alliance,
    Whisper,
    Yell,
    Emote
}

public enum HandSelection
{
    Left,
    Right,
    Both
}

public enum MessageOverheadTarget
{
    Self,
    Entity
}

public enum UseItemMode
{
    BySerial,
    ByType
}

public enum ItemLocation
{
    Everywhere,
    InHands,
    InBackpack,
    OnGround
}

public enum DropLocation
{
    Backpack,
    Container,
    Ground
}

// Player node - provides access to the player object (reference getter)
public class PlayerNode : VScriptNode
{
    public PlayerNode(string id, string pinIdCounter) : base(id, "Player", NodeCategory.Game)
    {
        OutputPins.Add(new NodePin(Guid.NewGuid().ToString(), id, "Player", PinType.Object, PinKind.Output));
    }

    // Override to use olive-green color like Unreal Engine reference/getter nodes
    public override Vector4 GetTitleBarColor()
    {
        return new Vector4(0.4f, 0.6f, 0.3f, 1.0f); // Olive-green like UE reference nodes
    }

    public override void Execute(VScriptContext context)
    {
        // Set the player object reference
        var outputPin = OutputPins[0];
        outputPin.Value = World.Player;
    }
}

// Base class for player property accessors
public abstract class PlayerPropertyNode : VScriptNode
{
    protected PlayerPropertyNode(string id, string name, string pinIdCounter) : base(id, name, NodeCategory.Game)
    {
        InputPins.Add(new NodePin(Guid.NewGuid().ToString(), id, "Player", PinType.Object, PinKind.Input));
        OutputPins.Add(new NodePin(Guid.NewGuid().ToString(), id, "Value", PinType.Number, PinKind.Output));
    }

    // Override to use olive-green color like Unreal Engine reference/getter nodes
    public override Vector4 GetTitleBarColor()
    {
        return new Vector4(0.4f, 0.6f, 0.3f, 1.0f); // Olive-green like UE reference nodes
    }

    protected object GetPlayerFromInput()
    {
        var playerPin = InputPins[0];
        return playerPin.Value;
    }
}

// Player Hits property
public class GetPlayerHitsNode : PlayerPropertyNode
{
    public GetPlayerHitsNode(string id, string pinIdCounter) : base(id, "Get Hits", pinIdCounter)
    {
    }

    public override void Execute(VScriptContext context)
    {
        // Razor-Zusatz: Player-Pin ist optional — ohne Verbindung gilt implizit World.Player.

        var player = World.Player;
        if (player != null)
        {
            OutputPins[0].Value = (double)player.Hits;
        }
    }
}

// Player HitsMax property
public class GetPlayerHitsMaxNode : PlayerPropertyNode
{
    public GetPlayerHitsMaxNode(string id, string pinIdCounter) : base(id, "Get Hits Max", pinIdCounter)
    {
    }

    public override void Execute(VScriptContext context)
    {
        // Razor-Zusatz: Player-Pin ist optional — ohne Verbindung gilt implizit World.Player.

        var player = World.Player;
        if (player != null)
        {
            OutputPins[0].Value = (double)player.HitsMax;
        }
    }
}

// Player Stamina property
public class GetPlayerStaminaNode : PlayerPropertyNode
{
    public GetPlayerStaminaNode(string id, string pinIdCounter) : base(id, "Get Stamina", pinIdCounter)
    {
    }

    public override void Execute(VScriptContext context)
    {
        // Razor-Zusatz: Player-Pin ist optional — ohne Verbindung gilt implizit World.Player.

        var player = World.Player;
        if (player != null)
        {
            OutputPins[0].Value = (double)player.Stamina;
        }
    }
}

// Player StaminaMax property
public class GetPlayerStaminaMaxNode : PlayerPropertyNode
{
    public GetPlayerStaminaMaxNode(string id, string pinIdCounter) : base(id, "Get Stamina Max", pinIdCounter)
    {
    }

    public override void Execute(VScriptContext context)
    {
        // Razor-Zusatz: Player-Pin ist optional — ohne Verbindung gilt implizit World.Player.

        var player = World.Player;
        if (player != null)
        {
            OutputPins[0].Value = (double)player.StaminaMax;
        }
    }
}

// Player Mana property
public class GetPlayerManaNode : PlayerPropertyNode
{
    public GetPlayerManaNode(string id, string pinIdCounter) : base(id, "Get Mana", pinIdCounter)
    {
    }

    public override void Execute(VScriptContext context)
    {
        // Razor-Zusatz: Player-Pin ist optional — ohne Verbindung gilt implizit World.Player.

        var player = World.Player;
        if (player != null)
        {
            OutputPins[0].Value = (double)player.Mana;
        }
    }
}

// Player ManaMax property
public class GetPlayerManaMaxNode : PlayerPropertyNode
{
    public GetPlayerManaMaxNode(string id, string pinIdCounter) : base(id, "Get Mana Max", pinIdCounter)
    {
    }

    public override void Execute(VScriptContext context)
    {
        // Razor-Zusatz: Player-Pin ist optional — ohne Verbindung gilt implizit World.Player.

        var player = World.Player;
        if (player != null)
        {
            OutputPins[0].Value = (double)player.ManaMax;
        }
    }
}

// Bandage Self action node
public class BandageSelfNode : VScriptNode
{
    public BandageSelfNode(string id, string pinIdCounter) : base(id, "Bandage Self", NodeCategory.Game)
    {
        InputPins.Add(new NodePin(Guid.NewGuid().ToString(), id, "", PinType.Flow, PinKind.Input));
        OutputPins.Add(new NodePin(Guid.NewGuid().ToString(), id, "", PinType.Flow, PinKind.Output));
    }

    // Override to use darker blue color like Unreal Engine function nodes
    public override Vector4 GetTitleBarColor()
    {
        return new Vector4(0.2f, 0.4f, 0.8f, 1.0f); // Darker blue like UE function nodes
    }

    public override void Execute(VScriptContext context)
    {
        

        if (World.Player == null || World.Player.IsDead)
        {
            context.ErrorMessage = "Player is not available or is dead";
            return;
        }

        var bandage = World.Player.FindBandage();

        if (bandage == null)
        {
            Message.Error("No bandages found.");
            return;
        }

        NetClient.Socket.Send_TargetSelectedObject(bandage.Serial, World.Player.Serial);
    }
}

// Attack action node - attacks a target by serial
public class AttackNode : VScriptNode
{
    public AttackNode(string id, string pinIdCounter) : base(id, "Attack", NodeCategory.Game)
    {
        InputPins.Add(new NodePin(Guid.NewGuid().ToString(), id, "", PinType.Flow, PinKind.Input));
        InputPins.Add(new NodePin(Guid.NewGuid().ToString(), id, "Serial", PinType.Number, PinKind.Input));
        OutputPins.Add(new NodePin(Guid.NewGuid().ToString(), id, "", PinType.Flow, PinKind.Output));
    }

    // Override to use darker blue color like Unreal Engine function nodes
    public override Vector4 GetTitleBarColor()
    {
        return new Vector4(0.2f, 0.4f, 0.8f, 1.0f); // Darker blue like UE function nodes
    }

    public override void Execute(VScriptContext context)
    {
        var serialPin = InputPins.Find(p => p.Name == "Serial");

        if (serialPin?.Value == null)
        {
            context.ErrorMessage = "Attack: Serial must be specified";
            return;
        }

        uint serial = 0;

        // Try to convert from pin value - check float first (inline widget type)
        if (serialPin.Value is float f)
        {
            serial = (uint)f;
        }
        else if (serialPin.Value is double d)
        {
            serial = (uint)d;
        }
        else if (serialPin.Value is int i)
        {
            serial = (uint)i;
        }
        else if (serialPin.Value is uint u)
        {
            serial = u;
        }

        if (serial == 0)
        {
            context.ErrorMessage = "Attack: Invalid serial value";
            return;
        }

        NetClient.Socket.Send_AttackRequest(serial);
    }
}

// Toggle War Mode action node - toggles war mode on/off
public class ToggleWarModeNode : VScriptNode
{
    public ToggleWarModeNode(string id, string pinIdCounter) : base(id, "Toggle War Mode", NodeCategory.Game)
    {
        InputPins.Add(new NodePin(Guid.NewGuid().ToString(), id, "", PinType.Flow, PinKind.Input));
        OutputPins.Add(new NodePin(Guid.NewGuid().ToString(), id, "", PinType.Flow, PinKind.Output));
    }

    // Override to use darker blue color like Unreal Engine function nodes
    public override Vector4 GetTitleBarColor()
    {
        return new Vector4(0.2f, 0.4f, 0.8f, 1.0f); // Darker blue like UE function nodes
    }

    public override void Execute(VScriptContext context)
    {
        

        if (World.Player == null)
        {
            context.ErrorMessage = "Toggle War Mode: Player not available";
            return;
        }

        // Toggle war mode - invert the current state
        bool newWarMode = !World.Player.InWarMode;
        NetClient.Socket.Send_ChangeWarMode(newWarMode);
    }
}

// Pop Pouch action node - uses a pouch item
public class PopPouchNode : VScriptNode
{
    public PopPouchNode(string id, string pinIdCounter) : base(id, "Pop Pouch", NodeCategory.Game)
    {
        InputPins.Add(new NodePin(Guid.NewGuid().ToString(), id, "", PinType.Flow, PinKind.Input));
        OutputPins.Add(new NodePin(Guid.NewGuid().ToString(), id, "", PinType.Flow, PinKind.Output));
    }

    // Override to use darker blue color like Unreal Engine function nodes
    public override Vector4 GetTitleBarColor()
    {
        return new Vector4(0.2f, 0.4f, 0.8f, 1.0f); // Darker blue like UE function nodes
    }

    public override void Execute(VScriptContext context)
    {
        

        if (World.Player == null)
        {
            context.ErrorMessage = "Pop Pouch: Player not available";
            return;
        }

        // Find pouch item (graphic 0x0E79, hue 0x0025) — Razor: Spieler-Items
        // rekursiv nach Graphic+Hue durchsuchen.
        var pouch = World.Player.Backpack?.FindItemsById(0x0E79, true)
            .FirstOrDefault(p => p.Hue == 0x0025);
        if (pouch == null)
        {
            context.ErrorMessage = "Pop Pouch: Pouch not found (graphic 0x0E79, hue 0x0025)";
            return;
        }

        // Check if the item type is allowed
        if (!AssistantData.ScriptingRestrictions.IsItemTypeAllowed(pouch.Graphic))
        {
            context.ErrorMessage = "Pop Pouch: This action is not supported by the script engine";
            return;
        }

        NetClient.Socket.Send_DoubleClick(pouch.Serial);
    }
}

// Equip action node - equips an item by serial
public class EquipNode : VScriptNode
{
    public EquipNode(string id, string pinIdCounter) : base(id, "Equip", NodeCategory.Game)
    {
        InputPins.Add(new NodePin(Guid.NewGuid().ToString(), id, "", PinType.Flow, PinKind.Input));
        InputPins.Add(new NodePin(Guid.NewGuid().ToString(), id, "Serial", PinType.Number, PinKind.Input));
        OutputPins.Add(new NodePin(Guid.NewGuid().ToString(), id, "", PinType.Flow, PinKind.Output));
    }

    // Override to use darker blue color like Unreal Engine function nodes
    public override Vector4 GetTitleBarColor()
    {
        return new Vector4(0.2f, 0.4f, 0.8f, 1.0f); // Darker blue like UE function nodes
    }

    public override void Execute(VScriptContext context)
    {
        
        var serialPin = InputPins.Find(p => p.Name == "Serial");

        if (serialPin?.Value == null)
        {
            context.ErrorMessage = "Equip: Serial must be specified";
            return;
        }

        uint serial = 0;

        // Try to convert from pin value - check float first (inline widget type)
        if (serialPin.Value is float f)
        {
            serial = (uint)f;
        }
        else if (serialPin.Value is double d)
        {
            serial = (uint)d;
        }
        else if (serialPin.Value is int i)
        {
            serial = (uint)i;
        }
        else if (serialPin.Value is uint u)
        {
            serial = u;
        }

        if (serial == 0)
        {
            context.ErrorMessage = "Equip: Invalid serial value";
            return;
        }

        // Find the item in the world
        var item = World.FindItem(serial);
        if (item == null)
        {
            context.ErrorMessage = $"Equip: Item with serial 0x{serial:X} not found";
            return;
        }

        // Check if the item type is allowed
        if (!AssistantData.ScriptingRestrictions.IsItemTypeAllowed(item.Graphic))
        {
            context.ErrorMessage = "Equip: This action is not supported by the script engine";
            return;
        }

        // Check if item is wearable
        if (!item.ItemData.IsWearable)
        {
            context.ErrorMessage = $"Equip: Item 0x{item.Graphic:X} is not wearable";
            return;
        }

        // Send equip request
        NetClient.Socket.Send_EquipRequest(serial, (Layer)item.ItemData.Layer, World.Player.Serial);
    }
}

// Pickup action node - picks up an item by serial
public class PickupNode : VScriptNode
{
    public PickupNode(string id, string pinIdCounter) : base(id, "Pickup", NodeCategory.Game)
    {
        InputPins.Add(new NodePin(Guid.NewGuid().ToString(), id, "", PinType.Flow, PinKind.Input));
        InputPins.Add(new NodePin(Guid.NewGuid().ToString(), id, "Serial", PinType.Number, PinKind.Input));
        OutputPins.Add(new NodePin(Guid.NewGuid().ToString(), id, "", PinType.Flow, PinKind.Output));
    }

    // Override to use darker blue color like Unreal Engine function nodes
    public override Vector4 GetTitleBarColor()
    {
        return new Vector4(0.2f, 0.4f, 0.8f, 1.0f); // Darker blue like UE function nodes
    }

    public override void Execute(VScriptContext context)
    {
        
        var serialPin = InputPins.Find(p => p.Name == "Serial");

        if (serialPin?.Value == null)
        {
            context.ErrorMessage = "Pickup: Serial must be specified";
            return;
        }

        uint serial = 0;

        // Try to convert from pin value - check float first (inline widget type)
        if (serialPin.Value is float f)
        {
            serial = (uint)f;
        }
        else if (serialPin.Value is double d)
        {
            serial = (uint)d;
        }
        else if (serialPin.Value is int i)
        {
            serial = (uint)i;
        }
        else if (serialPin.Value is uint u)
        {
            serial = u;
        }

        if (serial == 0)
        {
            context.ErrorMessage = "Pickup: Invalid serial value";
            return;
        }

        // Check if the item is allowed
        var item = World.FindItem(serial);
        if (item != null && !AssistantData.ScriptingRestrictions.IsItemTypeAllowed(item.Graphic))
        {
            context.ErrorMessage = "Pickup: This action is not supported by the script engine";
            return;
        }

        // Attempt to pick up the item
        bool success = GameActions.PickUp(serial, 0, 0);

        if (!success)
        {
            context.ErrorMessage = $"Pickup: Failed to pick up item 0x{serial:X}";
        }
    }
}

// Drop action node - drops an item to backpack, container, or ground
public class DropNode : VScriptNode
{
    private DropLocation _location = DropLocation.Backpack;

    public DropLocation Location
    {
        get => _location;
        set
        {
            if (_location != value)
            {
                _location = value;
                UpdatePins();
            }
        }
    }

    public DropNode(string id, string pinIdCounter) : base(id, "Drop", NodeCategory.Game)
    {
        InputPins.Add(new NodePin(Guid.NewGuid().ToString(), id, "", PinType.Flow, PinKind.Input));
        OutputPins.Add(new NodePin(Guid.NewGuid().ToString(), id, "", PinType.Flow, PinKind.Output));
    }

    // Override to use darker blue color like Unreal Engine function nodes
    public override Vector4 GetTitleBarColor()
    {
        return new Vector4(0.2f, 0.4f, 0.8f, 1.0f); // Darker blue like UE function nodes
    }

    private void UpdatePins()
    {
        // Remove Container Serial pin if it exists
        var containerPin = InputPins.Find(p => p.Name == "Container Serial");
        if (containerPin != null)
        {
            InputPins.Remove(containerPin);
        }

        // Add Container Serial pin if location is Container
        if (Location == DropLocation.Container)
        {
            InputPins.Add(new NodePin(Guid.NewGuid().ToString(), Id, "Container Serial", PinType.Number, PinKind.Input));
        }
    }

    public override void Execute(VScriptContext context)
    {
        

        // Get the item currently held by the cursor.
        // Razor: gehalten ODER noch in der Lift-Queue (Lift ist asynchron).
        var itemHold = GameActions.HeldOrQueued;

        if (itemHold == null)
        {
            context.ErrorMessage = "Drop: No item is currently being held";
            return;
        }

        switch (Location)
        {
            case DropLocation.Backpack:
                var backpack = World.Player.GetItemOnLayer(Layer.Backpack);
                if (backpack == null)
                {
                    context.ErrorMessage = "Drop: Backpack not found";
                    return;
                }
                GameActions.DropItem(itemHold.Serial, 0xFFFF, 0xFFFF, 0, backpack.Serial);
                break;

            case DropLocation.Container:
                var containerPin = InputPins.Find(p => p.Name == "Container Serial");
                if (containerPin?.Value == null)
                {
                    context.ErrorMessage = "Drop: Container Serial must be specified";
                    return;
                }

                uint containerSerial = 0;
                if (containerPin.Value is float cf)
                {
                    containerSerial = (uint)cf;
                }
                else if (containerPin.Value is double cd)
                {
                    containerSerial = (uint)cd;
                }
                else if (containerPin.Value is int ci)
                {
                    containerSerial = (uint)ci;
                }
                else if (containerPin.Value is uint cu)
                {
                    containerSerial = cu;
                }

                if (containerSerial == 0)
                {
                    context.ErrorMessage = "Drop: Invalid container serial";
                    return;
                }

                GameActions.DropItem(itemHold.Serial, 0xFFFF, 0xFFFF, 0, containerSerial);
                break;

            case DropLocation.Ground:
                // Drop at player position
                GameActions.DropItem(itemHold.Serial, World.Player.X, World.Player.Y, World.Player.Z, 0);
                break;
        }
    }
}

// Clear Hands action node - unequips items from hands
public class ClearHandsNode : VScriptNode
{
    public HandSelection Hand { get; set; } = HandSelection.Right;
    public uint Delay { get; set; } = 500; // Default 500ms delay for "Both"

    public ClearHandsNode(string id, string pinIdCounter) : base(id, "Clear Hands", NodeCategory.Game)
    {
        InputPins.Add(new NodePin(Guid.NewGuid().ToString(), id, "", PinType.Flow, PinKind.Input));
        InputPins.Add(new NodePin(Guid.NewGuid().ToString(), id, "Delay (ms)", PinType.Number, PinKind.Input));
        OutputPins.Add(new NodePin(Guid.NewGuid().ToString(), id, "", PinType.Flow, PinKind.Output));
    }

    // Override to use darker blue color like Unreal Engine function nodes
    public override Vector4 GetTitleBarColor()
    {
        return new Vector4(0.2f, 0.4f, 0.8f, 1.0f); // Darker blue like UE function nodes
    }

    public override void Execute(VScriptContext context)
    {
        

        if (World.Player == null || World.Player.IsDead)
        {
            context.ErrorMessage = "Clear Hands: Player is not available or is dead";
            return;
        }

        var backpack = World.Player.GetItemOnLayer(Layer.Backpack);
        if (backpack == null)
        {
            context.ErrorMessage = "Clear Hands: Backpack not found";
            return;
        }

        // Get delay from input pin if provided
        uint delay = Delay;
        var delayPin = InputPins.Find(p => p.Name == "Delay (ms)");
        if (delayPin?.Value != null)
        {
            // Try to convert from pin value - check float first (inline widget type)
            if (delayPin.Value is float f)
            {
                delay = (uint)f;
            }
            else if (delayPin.Value is double d)
            {
                delay = (uint)d;
            }
            else if (delayPin.Value is int i)
            {
                delay = (uint)i;
            }
            else if (delayPin.Value is uint u)
            {
                delay = u;
            }
        }

        switch (Hand)
        {
            case HandSelection.Left:
                UnequipHand(Layer.RightHand, backpack);
                break;

            case HandSelection.Right:
                UnequipHand(Layer.LeftHand, backpack);
                break;

            case HandSelection.Both:
                // Unequip right hand first
                UnequipHand(Layer.LeftHand, backpack);

                // Unequip left hand after delay (non-blocking)
                if (delay > 0)
                {
                    System.Threading.Tasks.Task.Delay((int)delay).ContinueWith(_ =>
                    {
                        UnequipHand(Layer.RightHand, backpack);
                    });
                }
                else
                {
                    UnequipHand(Layer.RightHand, backpack);
                }
                break;
        }
    }

    private void UnequipHand(Layer layer, Assistant.Item backpack)
    {
        var item = World.Player.GetItemOnLayer(layer);
        if (item != null)
        {
            // Pick up the item
            GameActions.PickUp(item.Serial, 0, 0);

            // Drop it into backpack (use a small delay to ensure pickup completes)
            System.Threading.Tasks.Task.Delay(50).ContinueWith(_ =>
            {
                GameActions.DropItem(item.Serial, 0xFFFF, 0xFFFF, 0, backpack.Serial);
            });
        }
    }
}

// Say action node - sends a message in chat
public class SayNode : VScriptNode
{
    public SayMessageType MessageType { get; set; } = SayMessageType.Regular;

    public SayNode(string id, string pinIdCounter) : base(id, "Say", NodeCategory.Game)
    {
        InputPins.Add(new NodePin(Guid.NewGuid().ToString(), id, "", PinType.Flow, PinKind.Input));
        InputPins.Add(new NodePin(Guid.NewGuid().ToString(), id, "Message", PinType.String, PinKind.Input));
        OutputPins.Add(new NodePin(Guid.NewGuid().ToString(), id, "", PinType.Flow, PinKind.Output));
    }

    // Override to use darker blue color like Unreal Engine function nodes
    public override Vector4 GetTitleBarColor()
    {
        return new Vector4(0.2f, 0.4f, 0.8f, 1.0f); // Darker blue like UE function nodes
    }

    public override void Execute(VScriptContext context)
    {
        

        if (World.Player == null || World.Player.IsDead)
        {
            context.ErrorMessage = "Player is not available or is dead";
            return;
        }

        var messagePin = InputPins.Find(p => p.Name == "Message");
        if (messagePin?.Value == null)
        {
            context.ErrorMessage = "Say: Message is required";
            return;
        }

        string message = messagePin.Value.ToString();

        // Map SayMessageType to MessageType
        Assistant.MessageType gameMessageType = this.MessageType switch
        {
            SayMessageType.Regular => Assistant.MessageType.Regular,
            SayMessageType.Guild => Assistant.MessageType.Guild,
            SayMessageType.Alliance => Assistant.MessageType.Alliance,
            SayMessageType.Whisper => Assistant.MessageType.Whisper,
            SayMessageType.Yell => Assistant.MessageType.Yell,
            SayMessageType.Emote => Assistant.MessageType.Emote,
            _ => Assistant.MessageType.Regular
        };

        GameActions.Say(message, type: gameMessageType);
    }
}

// Message Overhead action node
public class MessageOverheadNode : VScriptNode
{
    public MessageOverheadTarget Target { get; set; } = MessageOverheadTarget.Self;

    public MessageOverheadNode(string id, string pinIdCounter) : base(id, "Message Overhead", NodeCategory.Game)
    {
        InputPins.Add(new NodePin(Guid.NewGuid().ToString(), id, "", PinType.Flow, PinKind.Input));
        InputPins.Add(new NodePin(Guid.NewGuid().ToString(), id, "Message", PinType.String, PinKind.Input));
        InputPins.Add(new NodePin(Guid.NewGuid().ToString(), id, "Hue", PinType.Number, PinKind.Input));
        UpdatePinsForTarget();
        OutputPins.Add(new NodePin(Guid.NewGuid().ToString(), id, "", PinType.Flow, PinKind.Output));
    }

    public override Vector4 GetTitleBarColor()
    {
        return new Vector4(0.2f, 0.4f, 0.8f, 1.0f); // Darker blue like UE function nodes
    }

    public void UpdatePinsForTarget()
    {
        // Remove Entity pin if exists
        InputPins.RemoveAll(p => p.Name == "Entity");

        // Add Entity pin for Entity mode
        if (Target == MessageOverheadTarget.Entity)
        {
            // Insert before flow output pin
            var flowIndex = InputPins.FindIndex(p => p.Type == PinType.Flow);
            var insertIndex = flowIndex + 3; // After Flow, Message, Hue
            if (insertIndex <= InputPins.Count)
            {
                InputPins.Insert(insertIndex, new NodePin(Guid.NewGuid().ToString(), Id, "Entity", PinType.Object, PinKind.Input));
            }
            else
            {
                InputPins.Add(new NodePin(Guid.NewGuid().ToString(), Id, "Entity", PinType.Object, PinKind.Input));
            }
        }
    }

    public override void Execute(VScriptContext context)
    {
        

        if (World.Player == null)
        {
            context.ErrorMessage = "Player is not available";
            return;
        }

        var messagePin = InputPins.Find(p => p.Name == "Message");
        if (messagePin?.Value == null)
        {
            context.ErrorMessage = "Message Overhead: Message is required";
            return;
        }

        string message = messagePin.Value.ToString();

        // Get hue (default to 0 if not provided)
        var huePin = InputPins.Find(p => p.Name == "Hue");
        ushort hue = huePin?.Value != null ? Convert.ToUInt16(huePin.Value) : (ushort)0;

        uint serial;

        if (Target == MessageOverheadTarget.Self)
        {
            serial = World.Player.Serial;
        }
        else // Entity
        {
            var entityPin = InputPins.Find(p => p.Name == "Entity");
            if (entityPin?.Value == null)
            {
                context.ErrorMessage = "Message Overhead: Entity is required when Target is set to Entity";
                return;
            }

            // Check if it's a Mobile or Item object
            if (entityPin.Value is Mobile mobile)
            {
                serial = mobile.Serial;
            }
            else if (entityPin.Value is Item item)
            {
                serial = item.Serial;
            }
            else
            {
                context.ErrorMessage = "Message Overhead: Entity must be a Mobile or Item object";
                return;
            }
        }

        GameActions.MessageOverhead(message, hue, serial);
    }
}

// Cast Spell action node
public class CastSpellNode : VScriptNode
{
    public int SelectedSpellId { get; set; } = 1; // Default to Clumsy
    public bool WaitForTarget { get; set; } = false;
    public int Timeout { get; set; } = 10000; // Default 10 seconds

    public CastSpellNode(string id, string pinIdCounter) : base(id, "Cast Spell", NodeCategory.Game)
    {
        InputPins.Add(new NodePin(Guid.NewGuid().ToString(), id, "", PinType.Flow, PinKind.Input));
        OutputPins.Add(new NodePin(Guid.NewGuid().ToString(), id, "", PinType.Flow, PinKind.Output));
        OutputPins.Add(new NodePin(Guid.NewGuid().ToString(), id, "Success", PinType.Boolean, PinKind.Output));
    }

    // Override to use darker blue color like Unreal Engine function nodes
    public override Vector4 GetTitleBarColor()
    {
        return new Vector4(0.2f, 0.4f, 0.8f, 1.0f); // Darker blue like UE function nodes
    }

    public override void Execute(VScriptContext context)
    {
        

        if (World.Player == null || World.Player.IsDead)
        {
            context.ErrorMessage = "Player is not available or is dead";
            OutputPins[1].Value = false; // Success = false
            return;
        }

        // Cast the spell
        GameActions.CastSpell(SelectedSpellId);

        // Wait for target cursor if option is enabled
        if (WaitForTarget)
        {
            var startTime = System.DateTime.Now;
            var timeoutSpan = System.TimeSpan.FromMilliseconds(Timeout);

            while (!Targeting.HasTarget)
            {
                // Check for timeout
                if (System.DateTime.Now - startTime >= timeoutSpan)
                {
                    context.ErrorMessage = $"Cast Spell: Timeout waiting for target cursor ({Timeout}ms)";
                    OutputPins[1].Value = false; // Success = false
                    return;
                }

                // Check if targeting was cancelled
                if (context.ShouldStop)
                {
                    OutputPins[1].Value = false; // Success = false
                    return;
                }

                // Yield execution to prevent blocking - use Task.Delay for proper async behavior
                System.Threading.Tasks.Task.Delay(10).Wait();
            }

            // Target cursor appeared successfully
            OutputPins[1].Value = true; // Success = true
        }
        else
        {
            // No wait required, spell cast successfully
            OutputPins[1].Value = true; // Success = true
        }
    }
}

// Get Skill node - returns skill value (Base or Real)
public class GetSkillNode : VScriptNode
{
    public int SelectedSkillIndex { get; set; } = 0;
    public bool UseBaseValue { get; set; } = false; // false = Real, true = Base

    public GetSkillNode(string id, string pinIdCounter) : base(id, "Get Skill", NodeCategory.Game)
    {
        OutputPins.Add(new NodePin(Guid.NewGuid().ToString(), id, "Value", PinType.Number, PinKind.Output));
    }

    // Override to use olive-green color like Unreal Engine reference/getter nodes
    public override Vector4 GetTitleBarColor()
    {
        return new Vector4(0.4f, 0.6f, 0.3f, 1.0f); // Olive-green like UE reference nodes
    }

    public override void Execute(VScriptContext context)
    {
        var player = World.Player;
        if (player == null)
        {
            context.ErrorMessage = "Player is not available";
            return;
        }

        if (SelectedSkillIndex >= 0 && SelectedSkillIndex < player.Skills.Length)
        {
            var skill = player.Skills[SelectedSkillIndex];
            OutputPins[0].Value = UseBaseValue ? (double)skill.Base : (double)skill.Value;
        }
        else
        {
            OutputPins[0].Value = 0.0;
        }
    }
}

// Use Skill node - uses an actively usable skill
public class UseSkillNode : VScriptNode
{
    public int SelectedSkillIndex { get; set; } = 0;

    public UseSkillNode(string id, string pinIdCounter) : base(id, "Use Skill", NodeCategory.Game)
    {
        InputPins.Add(new NodePin(Guid.NewGuid().ToString(), id, "", PinType.Flow, PinKind.Input));
        OutputPins.Add(new NodePin(Guid.NewGuid().ToString(), id, "", PinType.Flow, PinKind.Output));
    }

    // Override to use darker blue color like Unreal Engine function nodes
    public override Vector4 GetTitleBarColor()
    {
        return new Vector4(0.2f, 0.4f, 0.8f, 1.0f); // Darker blue like UE function nodes
    }

    public override void Execute(VScriptContext context)
    {
        var player = World.Player;
        if (player == null)
        {
            context.ErrorMessage = "Player is not available";
            return;
        }

        if (SelectedSkillIndex >= 0 && SelectedSkillIndex < player.Skills.Length)
        {
            var skill = player.Skills[SelectedSkillIndex];
            if (skill.IsClickable)
            {
                GameActions.UseSkill(skill.Index);
            }
            else
            {
                context.ErrorMessage = $"Skill '{skill.Name}' is not usable";
            }
        }
    }
}

// Click Object node - single-clicks an object by serial
public class ClickObjectNode : VScriptNode
{
    public ClickObjectNode(string id, string pinIdCounter) : base(id, "Click Object", NodeCategory.Game)
    {
        InputPins.Add(new NodePin(Guid.NewGuid().ToString(), id, "", PinType.Flow, PinKind.Input));
        InputPins.Add(new NodePin(Guid.NewGuid().ToString(), id, "Serial", PinType.Number, PinKind.Input));
        OutputPins.Add(new NodePin(Guid.NewGuid().ToString(), id, "", PinType.Flow, PinKind.Output));
    }

    // Override to use darker blue color like Unreal Engine function nodes
    public override Vector4 GetTitleBarColor()
    {
        return new Vector4(0.2f, 0.4f, 0.8f, 1.0f); // Darker blue like UE function nodes
    }

    public override void Execute(VScriptContext context)
    {
        
        var serialPin = InputPins.Find(p => p.Name == "Serial");

        if (serialPin?.Value == null)
        {
            context.ErrorMessage = "Click Object: Serial must be specified";
            return;
        }

        uint serial = 0;

        // Try to convert from pin value - check float first (inline widget type)
        if (serialPin.Value is float f)
        {
            serial = (uint)f;
        }
        else if (serialPin.Value is double d)
        {
            serial = (uint)d;
        }
        else if (serialPin.Value is int i)
        {
            serial = (uint)i;
        }
        else if (serialPin.Value is uint u)
        {
            serial = u;
        }

        if (serial == 0)
        {
            context.ErrorMessage = "Click Object: Invalid serial value";
            return;
        }

        GameActions.SingleClick(serial);
    }
}

// Use Item node - uses an item by serial or type
public class UseItemNode : VScriptNode
{
    public uint SerialOrType { get; set; } = 0;
    public UseItemMode Mode { get; set; } = UseItemMode.BySerial;
    public ItemLocation Location { get; set; } = ItemLocation.Everywhere;

    public UseItemNode(string id, string pinIdCounter) : base(id, "Use", NodeCategory.Game)
    {
        InputPins.Add(new NodePin(Guid.NewGuid().ToString(), id, "", PinType.Flow, PinKind.Input));
        InputPins.Add(new NodePin(Guid.NewGuid().ToString(), id, "Serial/Type", PinType.Number, PinKind.Input));
        OutputPins.Add(new NodePin(Guid.NewGuid().ToString(), id, "", PinType.Flow, PinKind.Output));
    }

    // Override to use darker blue color like Unreal Engine function nodes
    public override Vector4 GetTitleBarColor()
    {
        return new Vector4(0.2f, 0.4f, 0.8f, 1.0f); // Darker blue like UE function nodes
    }

    public override void Execute(VScriptContext context)
    {
        

        if (World.Player == null)
        {
            context.ErrorMessage = "Player is not available";
            return;
        }

        // Get serial/type from input pin or stored value
        var serialOrTypePin = InputPins.Find(p => p.Name == "Serial/Type");
        uint serialOrTypeValue = SerialOrType;

        if (serialOrTypePin?.Value != null)
        {
            // Try to convert from pin value
            if (serialOrTypePin.Value is float f)
            {
                serialOrTypeValue = (uint)f;
            }
            else if (serialOrTypePin.Value is double d)
            {
                serialOrTypeValue = (uint)d;
            }
            else if (serialOrTypePin.Value is int i)
            {
                serialOrTypeValue = (uint)i;
            }
            else if (serialOrTypePin.Value is uint u)
            {
                serialOrTypeValue = u;
            }
        }

        if (serialOrTypeValue == 0)
        {
            context.ErrorMessage = "Use: Serial or Type must be specified";
            return;
        }

        if (Mode == UseItemMode.BySerial)
        {
            // Check if the item is allowed (by looking it up)
            var entity = World.Get(serialOrTypeValue);
            if (entity is Assistant.Item item)
            {
                if (!AssistantData.ScriptingRestrictions.IsItemTypeAllowed(item.Graphic))
                {
                    context.ErrorMessage = "Use: This action is not supported by the script engine";
                    return;
                }
            }

            // Use item by serial
            NetClient.Socket.Send_DoubleClick(serialOrTypeValue);
        }
        else
        {
            // Use item by type
            ushort itemGraphic = (ushort)serialOrTypeValue;

            // Check if the item type is allowed
            if (!AssistantData.ScriptingRestrictions.IsItemTypeAllowed(itemGraphic))
            {
                context.ErrorMessage = "Use: This action is not supported by the script engine";
                return;
            }

            Assistant.Item foundItem = null;

            if (Location == ItemLocation.Everywhere || Location == ItemLocation.InHands)
            {
                // Check left and right hand
                var leftHand = World.Player.GetItemOnLayer(Layer.RightHand);
                var rightHand = World.Player.GetItemOnLayer(Layer.LeftHand);

                if (leftHand != null && leftHand.Graphic == itemGraphic)
                    foundItem = leftHand;
                else if (rightHand != null && rightHand.Graphic == itemGraphic)
                    foundItem = rightHand;
            }

            if (foundItem == null && (Location == ItemLocation.Everywhere || Location == ItemLocation.InBackpack))
            {
                // Search in backpack using FindItemByGraphic
                foundItem = World.Player.FindItemByGraphic(itemGraphic);
            }

            if (foundItem == null && (Location == ItemLocation.Everywhere || Location == ItemLocation.OnGround))
            {
                // Search on ground within 2 tiles
                int maxDistance = 2;
                foundItem = World.Items.Values.FirstOrDefault(item =>
                    item.Graphic == itemGraphic &&
                    !item.IsDestroyed &&
                    !item.IsMulti &&
                    item.Distance <= maxDistance);
            }

            if (foundItem != null)
            {
                NetClient.Socket.Send_DoubleClick(foundItem.Serial);
            }
            else
            {
                context.ErrorMessage = $"Use: Item with type 0x{itemGraphic:X} not found in {Location}";
            }
        }
    }
}

// Player Str property
public class GetPlayerStrNode : PlayerPropertyNode
{
    public GetPlayerStrNode(string id, string pinIdCounter) : base(id, "Get Str", pinIdCounter)
    {
    }

    public override void Execute(VScriptContext context)
    {
        // Razor-Zusatz: Player-Pin ist optional — ohne Verbindung gilt implizit World.Player.

        var player = World.Player;
        if (player != null)
        {
            OutputPins[0].Value = (double)player.Strength;
        }
    }
}

// Player Dex property
public class GetPlayerDexNode : PlayerPropertyNode
{
    public GetPlayerDexNode(string id, string pinIdCounter) : base(id, "Get Dex", pinIdCounter)
    {
    }

    public override void Execute(VScriptContext context)
    {
        // Razor-Zusatz: Player-Pin ist optional — ohne Verbindung gilt implizit World.Player.

        var player = World.Player;
        if (player != null)
        {
            OutputPins[0].Value = (double)player.Dexterity;
        }
    }
}

// Player Int property
public class GetPlayerIntNode : PlayerPropertyNode
{
    public GetPlayerIntNode(string id, string pinIdCounter) : base(id, "Get Int", pinIdCounter)
    {
    }

    public override void Execute(VScriptContext context)
    {
        // Razor-Zusatz: Player-Pin ist optional — ohne Verbindung gilt implizit World.Player.

        var player = World.Player;
        if (player != null)
        {
            OutputPins[0].Value = (double)player.Intelligence;
        }
    }
}

// Player Hue property
public class GetPlayerHueNode : PlayerPropertyNode
{
    public GetPlayerHueNode(string id, string pinIdCounter) : base(id, "Get Hue", pinIdCounter)
    {
    }

    public override void Execute(VScriptContext context)
    {
        // Razor-Zusatz: Player-Pin ist optional — ohne Verbindung gilt implizit World.Player.

        var player = World.Player;
        if (player != null)
        {
            OutputPins[0].Value = (double)player.Hue;
        }
    }
}

// Player Graphic property
public class GetPlayerGraphicNode : PlayerPropertyNode
{
    public GetPlayerGraphicNode(string id, string pinIdCounter) : base(id, "Get Graphic", pinIdCounter)
    {
    }

    public override void Execute(VScriptContext context)
    {
        // Razor-Zusatz: Player-Pin ist optional — ohne Verbindung gilt implizit World.Player.

        var player = World.Player;
        if (player != null)
        {
            OutputPins[0].Value = (double)player.Graphic;
        }
    }
}

// Player Serial property
public class GetPlayerSerialNode : PlayerPropertyNode
{
    public GetPlayerSerialNode(string id, string pinIdCounter) : base(id, "Get Serial", pinIdCounter)
    {
    }

    public override void Execute(VScriptContext context)
    {
        // Razor-Zusatz: Player-Pin ist optional — ohne Verbindung gilt implizit World.Player.

        var player = World.Player;
        if (player != null)
        {
            OutputPins[0].Value = (double)(uint)player.Serial;
        }
    }
}

// Player Distance property
public class GetPlayerDistanceNode : PlayerPropertyNode
{
    public GetPlayerDistanceNode(string id, string pinIdCounter) : base(id, "Get Distance", pinIdCounter)
    {
    }

    public override void Execute(VScriptContext context)
    {
        // Razor-Zusatz: Player-Pin ist optional — ohne Verbindung gilt implizit World.Player.

        var player = World.Player;
        if (player != null)
        {
            OutputPins[0].Value = (double)player.Distance;
        }
    }
}

// Player Direction property
public class GetPlayerDirectionNode : PlayerPropertyNode
{
    public GetPlayerDirectionNode(string id, string pinIdCounter) : base(id, "Get Direction", pinIdCounter)
    {
    }

    public override void Execute(VScriptContext context)
    {
        // Razor-Zusatz: Player-Pin ist optional — ohne Verbindung gilt implizit World.Player.

        var player = World.Player;
        if (player != null)
        {
            OutputPins[0].Value = (double)player.Direction;
        }
    }
}

// Player Followers property
public class GetPlayerFollowersNode : PlayerPropertyNode
{
    public GetPlayerFollowersNode(string id, string pinIdCounter) : base(id, "Get Followers", pinIdCounter)
    {
    }

    public override void Execute(VScriptContext context)
    {
        // Razor-Zusatz: Player-Pin ist optional — ohne Verbindung gilt implizit World.Player.

        var player = World.Player;
        if (player != null)
        {
            OutputPins[0].Value = (double)player.Followers;
        }
    }
}

// Player MaxFollowers property
public class GetPlayerMaxFollowersNode : PlayerPropertyNode
{
    public GetPlayerMaxFollowersNode(string id, string pinIdCounter) : base(id, "Get Max Followers", pinIdCounter)
    {
    }

    public override void Execute(VScriptContext context)
    {
        // Razor-Zusatz: Player-Pin ist optional — ohne Verbindung gilt implizit World.Player.

        var player = World.Player;
        if (player != null)
        {
            OutputPins[0].Value = (double)player.FollowersMax;
        }
    }
}

// Player Gold property
public class GetPlayerGoldNode : PlayerPropertyNode
{
    public GetPlayerGoldNode(string id, string pinIdCounter) : base(id, "Get Gold", pinIdCounter)
    {
    }

    public override void Execute(VScriptContext context)
    {
        // Razor-Zusatz: Player-Pin ist optional — ohne Verbindung gilt implizit World.Player.

        var player = World.Player;
        if (player != null)
        {
            OutputPins[0].Value = (double)player.Gold;
        }
    }
}

// Player Luck property
public class GetPlayerLuckNode : PlayerPropertyNode
{
    public GetPlayerLuckNode(string id, string pinIdCounter) : base(id, "Get Luck", pinIdCounter)
    {
    }

    public override void Execute(VScriptContext context)
    {
        // Razor-Zusatz: Player-Pin ist optional — ohne Verbindung gilt implizit World.Player.

        var player = World.Player;
        if (player != null)
        {
            OutputPins[0].Value = (double)player.Luck;
        }
    }
}

// Player Tithing Points property
public class GetPlayerTithingPointsNode : PlayerPropertyNode
{
    public GetPlayerTithingPointsNode(string id, string pinIdCounter) : base(id, "Get Tithing Points", pinIdCounter)
    {
    }

    public override void Execute(VScriptContext context)
    {
        // Razor-Zusatz: Player-Pin ist optional — ohne Verbindung gilt implizit World.Player.

        var player = World.Player;
        if (player != null)
        {
            OutputPins[0].Value = (double)player.TithingPoints;
        }
    }
}

// Player Weight property
public class GetPlayerWeightNode : PlayerPropertyNode
{
    public GetPlayerWeightNode(string id, string pinIdCounter) : base(id, "Get Weight", pinIdCounter)
    {
    }

    public override void Execute(VScriptContext context)
    {
        // Razor-Zusatz: Player-Pin ist optional — ohne Verbindung gilt implizit World.Player.

        var player = World.Player;
        if (player != null)
        {
            OutputPins[0].Value = (double)player.Weight;
        }
    }
}

// Player MaxWeight property
public class GetPlayerMaxWeightNode : PlayerPropertyNode
{
    public GetPlayerMaxWeightNode(string id, string pinIdCounter) : base(id, "Get Max Weight", pinIdCounter)
    {
    }

    public override void Execute(VScriptContext context)
    {
        // Razor-Zusatz: Player-Pin ist optional — ohne Verbindung gilt implizit World.Player.

        var player = World.Player;
        if (player != null)
        {
            OutputPins[0].Value = (double)player.WeightMax;
        }
    }
}

// Player DiffWeight property
public class GetPlayerDiffWeightNode : PlayerPropertyNode
{
    public GetPlayerDiffWeightNode(string id, string pinIdCounter) : base(id, "Get Diff Weight", pinIdCounter)
    {
    }

    public override void Execute(VScriptContext context)
    {
        // Razor-Zusatz: Player-Pin ist optional — ohne Verbindung gilt implizit World.Player.

        var player = World.Player;
        if (player != null)
        {
            OutputPins[0].Value = (double)(player.Weight - player.WeightMax);
        }
    }
}

// Player StatsCap property
public class GetPlayerStatsCapNode : PlayerPropertyNode
{
    public GetPlayerStatsCapNode(string id, string pinIdCounter) : base(id, "Get Stats Cap", pinIdCounter)
    {
    }

    public override void Execute(VScriptContext context)
    {
        // Razor-Zusatz: Player-Pin ist optional — ohne Verbindung gilt implizit World.Player.

        var player = World.Player;
        if (player != null)
        {
            OutputPins[0].Value = (double)player.StatsCap;
        }
    }
}

// Player Physical Resistance property
public class GetPlayerPhysicalResistanceNode : PlayerPropertyNode
{
    public GetPlayerPhysicalResistanceNode(string id, string pinIdCounter) : base(id, "Get Physical Resistance", pinIdCounter)
    {
    }

    public override void Execute(VScriptContext context)
    {
        // Razor-Zusatz: Player-Pin ist optional — ohne Verbindung gilt implizit World.Player.

        var player = World.Player;
        if (player != null)
        {
            OutputPins[0].Value = (double)player.PhysicalResistance;
        }
    }
}

// Player DiffHits property
public class GetPlayerDiffHitsNode : PlayerPropertyNode
{
    public GetPlayerDiffHitsNode(string id, string pinIdCounter) : base(id, "Get Diff Hits", pinIdCounter)
    {
    }

    public override void Execute(VScriptContext context)
    {
        // Razor-Zusatz: Player-Pin ist optional — ohne Verbindung gilt implizit World.Player.

        var player = World.Player;
        if (player != null)
        {
            OutputPins[0].Value = (double)(player.Hits - player.HitsMax);
        }
    }
}

// Base class for player boolean property accessors
public abstract class PlayerBoolPropertyNode : VScriptNode
{
    protected PlayerBoolPropertyNode(string id, string name, string pinIdCounter) : base(id, name, NodeCategory.Game)
    {
        InputPins.Add(new NodePin(Guid.NewGuid().ToString(), id, "Player", PinType.Object, PinKind.Input));
        OutputPins.Add(new NodePin(Guid.NewGuid().ToString(), id, "Value", PinType.Boolean, PinKind.Output));
    }

    public override Vector4 GetTitleBarColor()
    {
        return new Vector4(0.4f, 0.6f, 0.3f, 1.0f); // Olive-green like UE reference nodes
    }

    protected object GetPlayerFromInput()
    {
        var playerPin = InputPins[0];
        return playerPin.Value;
    }
}

// Base class for player string property accessors
public abstract class PlayerStringPropertyNode : VScriptNode
{
    protected PlayerStringPropertyNode(string id, string name, string pinIdCounter) : base(id, name, NodeCategory.Game)
    {
        InputPins.Add(new NodePin(Guid.NewGuid().ToString(), id, "Player", PinType.Object, PinKind.Input));
        OutputPins.Add(new NodePin(Guid.NewGuid().ToString(), id, "Value", PinType.String, PinKind.Output));
    }

    public override Vector4 GetTitleBarColor()
    {
        return new Vector4(0.4f, 0.6f, 0.3f, 1.0f); // Olive-green like UE reference nodes
    }

    protected object GetPlayerFromInput()
    {
        var playerPin = InputPins[0];
        return playerPin.Value;
    }
}

// Player IsDestroyed property
public class GetPlayerIsDestroyedNode : PlayerBoolPropertyNode
{
    public GetPlayerIsDestroyedNode(string id, string pinIdCounter) : base(id, "Is Destroyed", pinIdCounter)
    {
    }

    public override void Execute(VScriptContext context)
    {
        // Razor-Zusatz: Player-Pin ist optional — ohne Verbindung gilt implizit World.Player.

        var player = World.Player;
        if (player != null)
        {
            OutputPins[0].Value = player.IsDestroyed;
        }
    }
}

// Player IsRunning property
public class GetPlayerIsRunningNode : PlayerBoolPropertyNode
{
    public GetPlayerIsRunningNode(string id, string pinIdCounter) : base(id, "Is Running", pinIdCounter)
    {
    }

    public override void Execute(VScriptContext context)
    {
        // Razor-Zusatz: Player-Pin ist optional — ohne Verbindung gilt implizit World.Player.

        var player = World.Player;
        if (player != null)
        {
            OutputPins[0].Value = player.IsRunning;
        }
    }
}

// Player IsParalyzed property
public class GetPlayerIsParalyzedNode : PlayerBoolPropertyNode
{
    public GetPlayerIsParalyzedNode(string id, string pinIdCounter) : base(id, "Is Paralyzed", pinIdCounter)
    {
    }

    public override void Execute(VScriptContext context)
    {
        // Razor-Zusatz: Player-Pin ist optional — ohne Verbindung gilt implizit World.Player.

        var player = World.Player;
        if (player != null)
        {
            OutputPins[0].Value = player.IsParalyzed;
        }
    }
}

// Player IsDead property
public class GetPlayerIsDeadNode : PlayerBoolPropertyNode
{
    public GetPlayerIsDeadNode(string id, string pinIdCounter) : base(id, "Is Dead", pinIdCounter)
    {
    }

    public override void Execute(VScriptContext context)
    {
        // Razor-Zusatz: Player-Pin ist optional — ohne Verbindung gilt implizit World.Player.

        var player = World.Player;
        if (player != null)
        {
            OutputPins[0].Value = player.IsDead;
        }
    }
}

// Player IsHidden property
public class GetPlayerIsHiddenNode : PlayerBoolPropertyNode
{
    public GetPlayerIsHiddenNode(string id, string pinIdCounter) : base(id, "Is Hidden", pinIdCounter)
    {
    }

    public override void Execute(VScriptContext context)
    {
        // Razor-Zusatz: Player-Pin ist optional — ohne Verbindung gilt implizit World.Player.

        var player = World.Player;
        if (player != null)
        {
            OutputPins[0].Value = player.IsHidden;
        }
    }
}

// Player IsPoisoned property
public class GetPlayerIsPoisonedNode : PlayerBoolPropertyNode
{
    public GetPlayerIsPoisonedNode(string id, string pinIdCounter) : base(id, "Is Poisoned", pinIdCounter)
    {
    }

    public override void Execute(VScriptContext context)
    {
        // Razor-Zusatz: Player-Pin ist optional — ohne Verbindung gilt implizit World.Player.

        var player = World.Player;
        if (player != null)
        {
            OutputPins[0].Value = player.IsPoisoned;
        }
    }
}

// Player IsMounted property
public class GetPlayerIsMountedNode : PlayerBoolPropertyNode
{
    public GetPlayerIsMountedNode(string id, string pinIdCounter) : base(id, "Is Mounted", pinIdCounter)
    {
    }

    public override void Execute(VScriptContext context)
    {
        // Razor-Zusatz: Player-Pin ist optional — ohne Verbindung gilt implizit World.Player.

        var player = World.Player;
        if (player != null)
        {
            OutputPins[0].Value = player.IsMounted;
        }
    }
}

// Player IsHuman property
public class GetPlayerIsHumanNode : PlayerBoolPropertyNode
{
    public GetPlayerIsHumanNode(string id, string pinIdCounter) : base(id, "Is Human", pinIdCounter)
    {
    }

    public override void Execute(VScriptContext context)
    {
        // Razor-Zusatz: Player-Pin ist optional — ohne Verbindung gilt implizit World.Player.

        var player = World.Player;
        if (player != null)
        {
            OutputPins[0].Value = player.IsHuman;
        }
    }
}

// Player IsYellowHits property (already exists, but adding for completeness)
public class GetPlayerIsYellowHitsNode : PlayerBoolPropertyNode
{
    public GetPlayerIsYellowHitsNode(string id, string pinIdCounter) : base(id, "Is Yellow Hits", pinIdCounter)
    {
    }

    public override void Execute(VScriptContext context)
    {
        // Razor-Zusatz: Player-Pin ist optional — ohne Verbindung gilt implizit World.Player.

        var player = World.Player;
        if (player != null)
        {
            OutputPins[0].Value = player.IsYellowHits;
        }
    }
}

// Player IsFemale property
public class GetPlayerIsFemaleNode : PlayerBoolPropertyNode
{
    public GetPlayerIsFemaleNode(string id, string pinIdCounter) : base(id, "Is Female", pinIdCounter)
    {
    }

    public override void Execute(VScriptContext context)
    {
        // Razor-Zusatz: Player-Pin ist optional — ohne Verbindung gilt implizit World.Player.

        var player = World.Player;
        if (player != null)
        {
            OutputPins[0].Value = player.IsFemale;
        }
    }
}

// Player Name property
public class GetPlayerNameNode : PlayerStringPropertyNode
{
    public GetPlayerNameNode(string id, string pinIdCounter) : base(id, "Get Name", pinIdCounter)
    {
    }

    public override void Execute(VScriptContext context)
    {
        // Razor-Zusatz: Player-Pin ist optional — ohne Verbindung gilt implizit World.Player.

        var player = World.Player;
        if (player != null)
        {
            OutputPins[0].Value = player.Name ?? "";
        }
    }
}

// Player Title property
public class GetPlayerTitleNode : PlayerStringPropertyNode
{
    public GetPlayerTitleNode(string id, string pinIdCounter) : base(id, "Get Title", pinIdCounter)
    {
    }

    public override void Execute(VScriptContext context)
    {
        // Razor-Zusatz: Player-Pin ist optional — ohne Verbindung gilt implizit World.Player.

        var player = World.Player;
        if (player != null)
        {
            OutputPins[0].Value = player.Title ?? "";
        }
    }
}

// Player NotorietyFlag property
public class GetPlayerNotorietyFlagNode : PlayerStringPropertyNode
{
    public GetPlayerNotorietyFlagNode(string id, string pinIdCounter) : base(id, "Get Notoriety Flag", pinIdCounter)
    {
    }

    public override void Execute(VScriptContext context)
    {
        // Razor-Zusatz: Player-Pin ist optional — ohne Verbindung gilt implizit World.Player.

        var player = World.Player;
        if (player != null)
        {
            OutputPins[0].Value = player.NotorietyFlag.ToString();
        }
    }
}

// Player Race property
public class GetPlayerRaceNode : PlayerStringPropertyNode
{
    public GetPlayerRaceNode(string id, string pinIdCounter) : base(id, "Get Race", pinIdCounter)
    {
    }

    public override void Execute(VScriptContext context)
    {
        // Razor-Zusatz: Player-Pin ist optional — ohne Verbindung gilt implizit World.Player.

        var player = World.Player;
        if (player != null)
        {
            OutputPins[0].Value = player.Race.ToString();
        }
    }
}

// ============================================================================
// MOBILE NODES
// ============================================================================

// Mobile node - provides access to a Mobile object (reference getter)
public class MobileNode : VScriptNode
{
    public MobileNode(string id, string pinIdCounter) : base(id, "Mobile", NodeCategory.Game)
    {
        OutputPins.Add(new NodePin(Guid.NewGuid().ToString(), id, "Mobile", PinType.Object, PinKind.Output));
    }

    public override Vector4 GetTitleBarColor()
    {
        return new Vector4(0.4f, 0.6f, 0.3f, 1.0f); // Olive-green like UE reference nodes
    }

    public override void Execute(VScriptContext context)
    {
        // Mobile node is typically used as input from other nodes (like Last Target)
        // It doesn't directly set a value in Execute
        var outputPin = OutputPins[0];
        if (outputPin.Value == null)
        {
            outputPin.Value = null; // Will be set by other nodes
        }
    }
}

// Base class for Mobile numeric property accessors
public abstract class MobilePropertyNode : VScriptNode
{
    protected MobilePropertyNode(string id, string name, string pinIdCounter) : base(id, name, NodeCategory.Game)
    {
        InputPins.Add(new NodePin(Guid.NewGuid().ToString(), id, "Mobile", PinType.Object, PinKind.Input));
        OutputPins.Add(new NodePin(Guid.NewGuid().ToString(), id, "Value", PinType.Number, PinKind.Output));
    }

    public override Vector4 GetTitleBarColor()
    {
        return new Vector4(0.4f, 0.6f, 0.3f, 1.0f); // Olive-green like UE reference nodes
    }
}

// Base class for Mobile boolean property accessors
public abstract class MobileBoolPropertyNode : VScriptNode
{
    protected MobileBoolPropertyNode(string id, string name, string pinIdCounter) : base(id, name, NodeCategory.Game)
    {
        InputPins.Add(new NodePin(Guid.NewGuid().ToString(), id, "Mobile", PinType.Object, PinKind.Input));
        OutputPins.Add(new NodePin(Guid.NewGuid().ToString(), id, "Value", PinType.Boolean, PinKind.Output));
    }

    public override Vector4 GetTitleBarColor()
    {
        return new Vector4(0.4f, 0.6f, 0.3f, 1.0f); // Olive-green like UE reference nodes
    }
}

// Base class for Mobile string property accessors
public abstract class MobileStringPropertyNode : VScriptNode
{
    protected MobileStringPropertyNode(string id, string name, string pinIdCounter) : base(id, name, NodeCategory.Game)
    {
        InputPins.Add(new NodePin(Guid.NewGuid().ToString(), id, "Mobile", PinType.Object, PinKind.Input));
        OutputPins.Add(new NodePin(Guid.NewGuid().ToString(), id, "Value", PinType.String, PinKind.Output));
    }

    public override Vector4 GetTitleBarColor()
    {
        return new Vector4(0.4f, 0.6f, 0.3f, 1.0f); // Olive-green like UE reference nodes
    }
}

// Mobile X property
public class GetMobileXNode : MobilePropertyNode
{
    public GetMobileXNode(string id, string pinIdCounter) : base(id, "Get X", pinIdCounter)
    {
    }

    public override void Execute(VScriptContext context)
    {
        var mobilePin = InputPins.Find(p => p.Name == "Mobile");
        if (mobilePin?.Value == null)
        {
            context.ErrorMessage = "Get X: Mobile object reference must be connected";
            return;
        }

        if (mobilePin.Value is Mobile mobile)
        {
            OutputPins[0].Value = (double)mobile.X;
        }
    }
}

// Mobile Y property
public class GetMobileYNode : MobilePropertyNode
{
    public GetMobileYNode(string id, string pinIdCounter) : base(id, "Get Y", pinIdCounter)
    {
    }

    public override void Execute(VScriptContext context)
    {
        var mobilePin = InputPins.Find(p => p.Name == "Mobile");
        if (mobilePin?.Value == null)
        {
            context.ErrorMessage = "Get Y: Mobile object reference must be connected";
            return;
        }

        if (mobilePin.Value is Mobile mobile)
        {
            OutputPins[0].Value = (double)mobile.Y;
        }
    }
}

// Mobile Z property
public class GetMobileZNode : MobilePropertyNode
{
    public GetMobileZNode(string id, string pinIdCounter) : base(id, "Get Z", pinIdCounter)
    {
    }

    public override void Execute(VScriptContext context)
    {
        var mobilePin = InputPins.Find(p => p.Name == "Mobile");
        if (mobilePin?.Value == null)
        {
            context.ErrorMessage = "Get Z: Mobile object reference must be connected";
            return;
        }

        if (mobilePin.Value is Mobile mobile)
        {
            OutputPins[0].Value = (double)mobile.Z;
        }
    }
}

// Mobile Hue property
public class GetMobileHueNode : MobilePropertyNode
{
    public GetMobileHueNode(string id, string pinIdCounter) : base(id, "Get Hue", pinIdCounter)
    {
    }

    public override void Execute(VScriptContext context)
    {
        var mobilePin = InputPins.Find(p => p.Name == "Mobile");
        if (mobilePin?.Value == null)
        {
            context.ErrorMessage = "Get Hue: Mobile object reference must be connected";
            return;
        }

        if (mobilePin.Value is Mobile mobile)
        {
            OutputPins[0].Value = (double)mobile.Hue;
        }
    }
}

// Mobile Graphic property
public class GetMobileGraphicNode : MobilePropertyNode
{
    public GetMobileGraphicNode(string id, string pinIdCounter) : base(id, "Get Graphic", pinIdCounter)
    {
    }

    public override void Execute(VScriptContext context)
    {
        var mobilePin = InputPins.Find(p => p.Name == "Mobile");
        if (mobilePin?.Value == null)
        {
            context.ErrorMessage = "Get Graphic: Mobile object reference must be connected";
            return;
        }

        if (mobilePin.Value is Mobile mobile)
        {
            OutputPins[0].Value = (double)mobile.Graphic;
        }
    }
}

// Mobile Serial property
public class GetMobileSerialNode : MobilePropertyNode
{
    public GetMobileSerialNode(string id, string pinIdCounter) : base(id, "Get Serial", pinIdCounter)
    {
    }

    public override void Execute(VScriptContext context)
    {
        var mobilePin = InputPins.Find(p => p.Name == "Mobile");
        if (mobilePin?.Value == null)
        {
            context.ErrorMessage = "Get Serial: Mobile object reference must be connected";
            return;
        }

        if (mobilePin.Value is Mobile mobile)
        {
            OutputPins[0].Value = (double)(uint)mobile.Serial;
        }
    }
}

// Mobile Distance property
public class GetMobileDistanceNode : MobilePropertyNode
{
    public GetMobileDistanceNode(string id, string pinIdCounter) : base(id, "Get Distance", pinIdCounter)
    {
    }

    public override void Execute(VScriptContext context)
    {
        var mobilePin = InputPins.Find(p => p.Name == "Mobile");
        if (mobilePin?.Value == null)
        {
            context.ErrorMessage = "Get Distance: Mobile object reference must be connected";
            return;
        }

        if (mobilePin.Value is Mobile mobile)
        {
            OutputPins[0].Value = (double)mobile.Distance;
        }
    }
}

// Mobile Direction property
public class GetMobileDirectionNode : MobilePropertyNode
{
    public GetMobileDirectionNode(string id, string pinIdCounter) : base(id, "Get Direction", pinIdCounter)
    {
    }

    public override void Execute(VScriptContext context)
    {
        var mobilePin = InputPins.Find(p => p.Name == "Mobile");
        if (mobilePin?.Value == null)
        {
            context.ErrorMessage = "Get Direction: Mobile object reference must be connected";
            return;
        }

        if (mobilePin.Value is Mobile mobile)
        {
            OutputPins[0].Value = (double)mobile.Direction;
        }
    }
}

// Mobile Hits property
public class GetMobileHitsNode : MobilePropertyNode
{
    public GetMobileHitsNode(string id, string pinIdCounter) : base(id, "Get Hits", pinIdCounter)
    {
    }

    public override void Execute(VScriptContext context)
    {
        var mobilePin = InputPins.Find(p => p.Name == "Mobile");
        if (mobilePin?.Value == null)
        {
            context.ErrorMessage = "Get Hits: Mobile object reference must be connected";
            return;
        }

        if (mobilePin.Value is Mobile mobile)
        {
            OutputPins[0].Value = (double)mobile.Hits;
        }
    }
}

// Mobile HitsMax property
public class GetMobileHitsMaxNode : MobilePropertyNode
{
    public GetMobileHitsMaxNode(string id, string pinIdCounter) : base(id, "Get Hits Max", pinIdCounter)
    {
    }

    public override void Execute(VScriptContext context)
    {
        var mobilePin = InputPins.Find(p => p.Name == "Mobile");
        if (mobilePin?.Value == null)
        {
            context.ErrorMessage = "Get Hits Max: Mobile object reference must be connected";
            return;
        }

        if (mobilePin.Value is Mobile mobile)
        {
            OutputPins[0].Value = (double)mobile.HitsMax;
        }
    }
}

// Mobile DiffHits property
public class GetMobileDiffHitsNode : MobilePropertyNode
{
    public GetMobileDiffHitsNode(string id, string pinIdCounter) : base(id, "Get Diff Hits", pinIdCounter)
    {
    }

    public override void Execute(VScriptContext context)
    {
        var mobilePin = InputPins.Find(p => p.Name == "Mobile");
        if (mobilePin?.Value == null)
        {
            context.ErrorMessage = "Get Diff Hits: Mobile object reference must be connected";
            return;
        }

        if (mobilePin.Value is Mobile mobile)
        {
            OutputPins[0].Value = (double)(mobile.Hits - mobile.HitsMax);
        }
    }
}

// Mobile Stamina property
public class GetMobileStaminaNode : MobilePropertyNode
{
    public GetMobileStaminaNode(string id, string pinIdCounter) : base(id, "Get Stamina", pinIdCounter)
    {
    }

    public override void Execute(VScriptContext context)
    {
        var mobilePin = InputPins.Find(p => p.Name == "Mobile");
        if (mobilePin?.Value == null)
        {
            context.ErrorMessage = "Get Stamina: Mobile object reference must be connected";
            return;
        }

        if (mobilePin.Value is Mobile mobile)
        {
            OutputPins[0].Value = (double)mobile.Stamina;
        }
    }
}

// Mobile StaminaMax property
public class GetMobileStaminaMaxNode : MobilePropertyNode
{
    public GetMobileStaminaMaxNode(string id, string pinIdCounter) : base(id, "Get Stamina Max", pinIdCounter)
    {
    }

    public override void Execute(VScriptContext context)
    {
        var mobilePin = InputPins.Find(p => p.Name == "Mobile");
        if (mobilePin?.Value == null)
        {
            context.ErrorMessage = "Get Stamina Max: Mobile object reference must be connected";
            return;
        }

        if (mobilePin.Value is Mobile mobile)
        {
            OutputPins[0].Value = (double)mobile.StaminaMax;
        }
    }
}

// Mobile Mana property
public class GetMobileManaNode : MobilePropertyNode
{
    public GetMobileManaNode(string id, string pinIdCounter) : base(id, "Get Mana", pinIdCounter)
    {
    }

    public override void Execute(VScriptContext context)
    {
        var mobilePin = InputPins.Find(p => p.Name == "Mobile");
        if (mobilePin?.Value == null)
        {
            context.ErrorMessage = "Get Mana: Mobile object reference must be connected";
            return;
        }

        if (mobilePin.Value is Mobile mobile)
        {
            OutputPins[0].Value = (double)mobile.Mana;
        }
    }
}

// Mobile ManaMax property
public class GetMobileManaMaxNode : MobilePropertyNode
{
    public GetMobileManaMaxNode(string id, string pinIdCounter) : base(id, "Get Mana Max", pinIdCounter)
    {
    }

    public override void Execute(VScriptContext context)
    {
        var mobilePin = InputPins.Find(p => p.Name == "Mobile");
        if (mobilePin?.Value == null)
        {
            context.ErrorMessage = "Get Mana Max: Mobile object reference must be connected";
            return;
        }

        if (mobilePin.Value is Mobile mobile)
        {
            OutputPins[0].Value = (double)mobile.ManaMax;
        }
    }
}

// Mobile IsDestroyed property
public class GetMobileIsDestroyedNode : MobileBoolPropertyNode
{
    public GetMobileIsDestroyedNode(string id, string pinIdCounter) : base(id, "Is Destroyed", pinIdCounter)
    {
    }

    public override void Execute(VScriptContext context)
    {
        var mobilePin = InputPins.Find(p => p.Name == "Mobile");
        if (mobilePin?.Value == null)
        {
            context.ErrorMessage = "Is Destroyed: Mobile object reference must be connected";
            return;
        }

        if (mobilePin.Value is Mobile mobile)
        {
            OutputPins[0].Value = mobile.IsDestroyed;
        }
    }
}

// Mobile IsRunning property
public class GetMobileIsRunningNode : MobileBoolPropertyNode
{
    public GetMobileIsRunningNode(string id, string pinIdCounter) : base(id, "Is Running", pinIdCounter)
    {
    }

    public override void Execute(VScriptContext context)
    {
        var mobilePin = InputPins.Find(p => p.Name == "Mobile");
        if (mobilePin?.Value == null)
        {
            context.ErrorMessage = "Is Running: Mobile object reference must be connected";
            return;
        }

        if (mobilePin.Value is Mobile mobile)
        {
            OutputPins[0].Value = mobile.IsRunning;
        }
    }
}

// Mobile IsParalyzed property
public class GetMobileIsParalyzedNode : MobileBoolPropertyNode
{
    public GetMobileIsParalyzedNode(string id, string pinIdCounter) : base(id, "Is Paralyzed", pinIdCounter)
    {
    }

    public override void Execute(VScriptContext context)
    {
        var mobilePin = InputPins.Find(p => p.Name == "Mobile");
        if (mobilePin?.Value == null)
        {
            context.ErrorMessage = "Is Paralyzed: Mobile object reference must be connected";
            return;
        }

        if (mobilePin.Value is Mobile mobile)
        {
            OutputPins[0].Value = mobile.IsParalyzed;
        }
    }
}

// Mobile IsDead property
public class GetMobileIsDeadNode : MobileBoolPropertyNode
{
    public GetMobileIsDeadNode(string id, string pinIdCounter) : base(id, "Is Dead", pinIdCounter)
    {
    }

    public override void Execute(VScriptContext context)
    {
        var mobilePin = InputPins.Find(p => p.Name == "Mobile");
        if (mobilePin?.Value == null)
        {
            context.ErrorMessage = "Is Dead: Mobile object reference must be connected";
            return;
        }

        if (mobilePin.Value is Mobile mobile)
        {
            OutputPins[0].Value = mobile.IsDead;
        }
    }
}

// Mobile IsHidden property
public class GetMobileIsHiddenNode : MobileBoolPropertyNode
{
    public GetMobileIsHiddenNode(string id, string pinIdCounter) : base(id, "Is Hidden", pinIdCounter)
    {
    }

    public override void Execute(VScriptContext context)
    {
        var mobilePin = InputPins.Find(p => p.Name == "Mobile");
        if (mobilePin?.Value == null)
        {
            context.ErrorMessage = "Is Hidden: Mobile object reference must be connected";
            return;
        }

        if (mobilePin.Value is Mobile mobile)
        {
            OutputPins[0].Value = mobile.IsHidden;
        }
    }
}

// Mobile IsPoisoned property
public class GetMobileIsPoisonedNode : MobileBoolPropertyNode
{
    public GetMobileIsPoisonedNode(string id, string pinIdCounter) : base(id, "Is Poisoned", pinIdCounter)
    {
    }

    public override void Execute(VScriptContext context)
    {
        var mobilePin = InputPins.Find(p => p.Name == "Mobile");
        if (mobilePin?.Value == null)
        {
            context.ErrorMessage = "Is Poisoned: Mobile object reference must be connected";
            return;
        }

        if (mobilePin.Value is Mobile mobile)
        {
            OutputPins[0].Value = mobile.IsPoisoned;
        }
    }
}

// Mobile IsMounted property
public class GetMobileIsMountedNode : MobileBoolPropertyNode
{
    public GetMobileIsMountedNode(string id, string pinIdCounter) : base(id, "Is Mounted", pinIdCounter)
    {
    }

    public override void Execute(VScriptContext context)
    {
        var mobilePin = InputPins.Find(p => p.Name == "Mobile");
        if (mobilePin?.Value == null)
        {
            context.ErrorMessage = "Is Mounted: Mobile object reference must be connected";
            return;
        }

        if (mobilePin.Value is Mobile mobile)
        {
            OutputPins[0].Value = mobile.IsMounted;
        }
    }
}

// Mobile IsHuman property
public class GetMobileIsHumanNode : MobileBoolPropertyNode
{
    public GetMobileIsHumanNode(string id, string pinIdCounter) : base(id, "Is Human", pinIdCounter)
    {
    }

    public override void Execute(VScriptContext context)
    {
        var mobilePin = InputPins.Find(p => p.Name == "Mobile");
        if (mobilePin?.Value == null)
        {
            context.ErrorMessage = "Is Human: Mobile object reference must be connected";
            return;
        }

        if (mobilePin.Value is Mobile mobile)
        {
            OutputPins[0].Value = mobile.IsHuman;
        }
    }
}

// Mobile IsYellowHits property
public class GetMobileIsYellowHitsNode : MobileBoolPropertyNode
{
    public GetMobileIsYellowHitsNode(string id, string pinIdCounter) : base(id, "Is Yellow Hits", pinIdCounter)
    {
    }

    public override void Execute(VScriptContext context)
    {
        var mobilePin = InputPins.Find(p => p.Name == "Mobile");
        if (mobilePin?.Value == null)
        {
            context.ErrorMessage = "Is Yellow Hits: Mobile object reference must be connected";
            return;
        }

        if (mobilePin.Value is Mobile mobile)
        {
            OutputPins[0].Value = mobile.IsYellowHits;
        }
    }
}

// Mobile IsFemale property
public class GetMobileIsFemaleNode : MobileBoolPropertyNode
{
    public GetMobileIsFemaleNode(string id, string pinIdCounter) : base(id, "Is Female", pinIdCounter)
    {
    }

    public override void Execute(VScriptContext context)
    {
        var mobilePin = InputPins.Find(p => p.Name == "Mobile");
        if (mobilePin?.Value == null)
        {
            context.ErrorMessage = "Is Female: Mobile object reference must be connected";
            return;
        }

        if (mobilePin.Value is Mobile mobile)
        {
            OutputPins[0].Value = mobile.IsFemale;
        }
    }
}

// Mobile Name property
public class GetMobileNameNode : MobileStringPropertyNode
{
    public GetMobileNameNode(string id, string pinIdCounter) : base(id, "Get Name", pinIdCounter)
    {
    }

    public override void Execute(VScriptContext context)
    {
        var mobilePin = InputPins.Find(p => p.Name == "Mobile");
        if (mobilePin?.Value == null)
        {
            context.ErrorMessage = "Get Name: Mobile object reference must be connected";
            return;
        }

        if (mobilePin.Value is Mobile mobile)
        {
            OutputPins[0].Value = mobile.Name ?? string.Empty;
        }
    }
}

// Mobile Title property
public class GetMobileTitleNode : MobileStringPropertyNode
{
    public GetMobileTitleNode(string id, string pinIdCounter) : base(id, "Get Title", pinIdCounter)
    {
    }

    public override void Execute(VScriptContext context)
    {
        var mobilePin = InputPins.Find(p => p.Name == "Mobile");
        if (mobilePin?.Value == null)
        {
            context.ErrorMessage = "Get Title: Mobile object reference must be connected";
            return;
        }

        if (mobilePin.Value is Mobile mobile)
        {
            OutputPins[0].Value = mobile.Title ?? string.Empty;
        }
    }
}

// Mobile NotorietyFlag property
public class GetMobileNotorietyFlagNode : MobileStringPropertyNode
{
    public GetMobileNotorietyFlagNode(string id, string pinIdCounter) : base(id, "Get Notoriety Flag", pinIdCounter)
    {
    }

    public override void Execute(VScriptContext context)
    {
        var mobilePin = InputPins.Find(p => p.Name == "Mobile");
        if (mobilePin?.Value == null)
        {
            context.ErrorMessage = "Get Notoriety Flag: Mobile object reference must be connected";
            return;
        }

        if (mobilePin.Value is Mobile mobile)
        {
            OutputPins[0].Value = mobile.NotorietyFlag.ToString();
        }
    }
}

// Mobile Race property
public class GetMobileRaceNode : MobileStringPropertyNode
{
    public GetMobileRaceNode(string id, string pinIdCounter) : base(id, "Get Race", pinIdCounter)
    {
    }

    public override void Execute(VScriptContext context)
    {
        var mobilePin = InputPins.Find(p => p.Name == "Mobile");
        if (mobilePin?.Value == null)
        {
            context.ErrorMessage = "Get Race: Mobile object reference must be connected";
            return;
        }

        if (mobilePin.Value is Mobile mobile)
        {
            OutputPins[0].Value = mobile.Race.ToString();
        }
    }
}

// Find Mobiles search mode enum
public enum FindMobilesMode
{
    BySerial,
    ByType,
    ByName,
    ByFilter
}

// Find Mobiles node - searches for mobiles based on criteria
public class FindMobilesNode : VScriptNode
{
    public FindMobilesMode SearchMode { get; set; } = FindMobilesMode.ByType;

    // Filter criteria
    public bool? IsDead { get; set; }
    public bool? IsFemale { get; set; }
    public bool? IsHuman { get; set; }
    public bool? IsPoisoned { get; set; }
    public bool? IsParalyzed { get; set; }
    public double? RangeMin { get; set; }
    public double? RangeMax { get; set; }
    public List<string> Names { get; set; }
    public List<ushort> Hues { get; set; }
    public List<ushort> Bodies { get; set; }
    public List<byte> Notorieties { get; set; }
    public List<uint> Serials { get; set; }

    // RAZOR-ZUSATZ: kombinierbare Filterkette (AND/OR/NOT, optionale Pin-Werte);
    // Details/Dateiformat siehe FindFilters.cs. Der Client ignoriert das Feld.
    public List<FindFilter> Filters { get; set; } = new();

    public FindMobilesNode(string id, string pinIdCounter) : base(id, "Find Mobiles", NodeCategory.Game)
    {
        InputPins.Add(new NodePin(Guid.NewGuid().ToString(), id, "", PinType.Flow, PinKind.Input));
        UpdatePinsForSearchMode();
        OutputPins.Add(new NodePin(Guid.NewGuid().ToString(), id, "", PinType.Flow, PinKind.Output));
        OutputPins.Add(new NodePin(Guid.NewGuid().ToString(), id, "Mobiles", PinType.Object, PinKind.Output, isList: true));
    }

    public override Vector4 GetTitleBarColor()
    {
        return new Vector4(0.2f, 0.4f, 0.8f, 1.0f); // Darker blue like UE function nodes
    }

    public void UpdatePinsForSearchMode()
    {
        // Remove all data input pins (keep only flow input)
        InputPins.RemoveAll(p => p.Type != PinType.Flow);

        // Add appropriate pins based on search mode
        switch (SearchMode)
        {
            case FindMobilesMode.BySerial:
                InputPins.Add(new NodePin(Guid.NewGuid().ToString(), Id, "Serial", PinType.Number, PinKind.Input));
                break;
            case FindMobilesMode.ByType:
                InputPins.Add(new NodePin(Guid.NewGuid().ToString(), Id, "Type", PinType.Number, PinKind.Input));
                break;
            case FindMobilesMode.ByName:
                InputPins.Add(new NodePin(Guid.NewGuid().ToString(), Id, "Name", PinType.String, PinKind.Input));
                break;
            case FindMobilesMode.ByFilter:
                // No additional input pins needed for filter mode - uses properties
                break;
        }
    }

    public override void Execute(VScriptContext context)
    {
        
        var mobiles = new List<Mobile>();

        if (World.Player == null)
        {
            context.ErrorMessage = "Player is not available";
            return;
        }

        switch (SearchMode)
        {
            case FindMobilesMode.BySerial:
                {
                    var serialPin = InputPins.Find(p => p.Name == "Serial");
                    if (serialPin?.Value != null)
                    {
                        uint serial = Convert.ToUInt32(serialPin.Value);
                        var mobile = World.FindMobile(serial);
                        if (mobile != null && !mobile.IsDestroyed)
                        {
                            mobiles.Add(mobile);
                        }
                    }
                }
                break;

            case FindMobilesMode.ByType:
                {
                    var typePin = InputPins.Find(p => p.Name == "Type");
                    if (typePin?.Value != null)
                    {
                        ushort type = Convert.ToUInt16(typePin.Value);
                        foreach (var mobile in World.Mobiles.Values)
                        {
                            if (!mobile.IsDestroyed && mobile.Graphic == type)
                            {
                                mobiles.Add(mobile);
                            }
                        }
                    }
                }
                break;

            case FindMobilesMode.ByName:
                {
                    var namePin = InputPins.Find(p => p.Name == "Name");
                    if (namePin?.Value != null)
                    {
                        string name = namePin.Value.ToString();
                        foreach (var mobile in World.Mobiles.Values)
                        {
                            if (!mobile.IsDestroyed && mobile.Name != null &&
                                mobile.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
                            {
                                mobiles.Add(mobile);
                            }
                        }
                    }
                }
                break;

            case FindMobilesMode.ByFilter:
                {
                    foreach (var mobile in World.Mobiles.Values)
                    {
                        if (mobile.IsDestroyed)
                            continue;

                        // Apply all filter criteria
                        if (IsDead.HasValue && mobile.IsDead != IsDead.Value)
                            continue;
                        if (IsFemale.HasValue && mobile.IsFemale != IsFemale.Value)
                            continue;
                        if (IsHuman.HasValue && mobile.IsHuman != IsHuman.Value)
                            continue;
                        if (IsPoisoned.HasValue && mobile.IsPoisoned != IsPoisoned.Value)
                            continue;
                        if (IsParalyzed.HasValue && mobile.IsParalyzed != IsParalyzed.Value)
                            continue;
                        if (RangeMin.HasValue && mobile.Distance < RangeMin.Value)
                            continue;
                        if (RangeMax.HasValue && mobile.Distance > RangeMax.Value)
                            continue;
                        if (Names != null && Names.Count > 0 && !Names.Contains(mobile.Name))
                            continue;
                        if (Hues != null && Hues.Count > 0 && !Hues.Contains(mobile.Hue))
                            continue;
                        if (Bodies != null && Bodies.Count > 0 && !Bodies.Contains(mobile.Graphic))
                            continue;
                        if (Notorieties != null && Notorieties.Count > 0 && !Notorieties.Contains((byte)mobile.NotorietyFlag))
                            continue;
                        if (Serials != null && Serials.Count > 0 && !Serials.Contains(mobile.Serial))
                            continue;

                        mobiles.Add(mobile);
                    }
                }
                break;
        }

        // RAZOR-ZUSATZ: Duplikate raus, dann die Filterkette anwenden.
        mobiles = mobiles.Distinct().ToList();

        if (Filters.Count > 0)
        {
            mobiles = mobiles.Where(m => FindFilterCatalog.MatchesChain(Filters, m,
                f => InputPins.Find(p => p.Id == f.PinId), FindFilterCatalog.MatchMobile)).ToList();
        }

        // Set the output pin to the list of mobiles
        var outputPin = OutputPins.Find(p => p.Name == "Mobiles");
        if (outputPin != null)
        {
            outputPin.Value = mobiles;
        }
    }
}

public enum FindItemsMode
{
    ByLayer,
    BySerial,
    ByType,
    ByName,
    ByFilter
}

// Find Items node - searches for items based on criteria
public class FindItemsNode : VScriptNode
{
    public FindItemsMode SearchMode { get; set; } = FindItemsMode.ByType;

    // Filter criteria
    public List<ushort> ExcludedItemIDs { get; set; }
    public bool? IsCorpse { get; set; }
    public bool? IsContainer { get; set; }
    public bool? IsMovable { get; set; }
    public bool? IsOnGround { get; set; }
    public string Name { get; set; }
    public double? RangeMin { get; set; }
    public double? RangeMax { get; set; }
    public List<ushort> Graphics { get; set; }
    public List<byte> Layers { get; set; }
    public List<ushort> Hues { get; set; }

    // RAZOR-ZUSATZ: kombinierbare Filterkette (AND/OR/NOT, optionale Pin-Werte);
    // Details/Dateiformat siehe FindFilters.cs. Der Client ignoriert das Feld.
    public List<FindFilter> Filters { get; set; } = new();

    public FindItemsNode(string id, string pinIdCounter) : base(id, "Find Items", NodeCategory.Game)
    {
        InputPins.Add(new NodePin(Guid.NewGuid().ToString(), id, "", PinType.Flow, PinKind.Input));
        UpdatePinsForSearchMode();
        OutputPins.Add(new NodePin(Guid.NewGuid().ToString(), id, "", PinType.Flow, PinKind.Output));
        OutputPins.Add(new NodePin(Guid.NewGuid().ToString(), id, "Items", PinType.Object, PinKind.Output, isList: true, objectSubType: ObjectSubType.Item));
    }

    public override Vector4 GetTitleBarColor()
    {
        return new Vector4(0.2f, 0.4f, 0.8f, 1.0f); // Darker blue like UE function nodes
    }

    public void UpdatePinsForSearchMode()
    {
        // Remove all data input pins (keep only flow input)
        InputPins.RemoveAll(p => p.Type != PinType.Flow);

        // Add appropriate pins based on search mode
        switch (SearchMode)
        {
            case FindItemsMode.ByLayer:
                InputPins.Add(new NodePin(Guid.NewGuid().ToString(), Id, "Layer", PinType.Number, PinKind.Input));
                break;
            case FindItemsMode.BySerial:
                InputPins.Add(new NodePin(Guid.NewGuid().ToString(), Id, "Serial", PinType.Number, PinKind.Input));
                break;
            case FindItemsMode.ByType:
                InputPins.Add(new NodePin(Guid.NewGuid().ToString(), Id, "Type", PinType.Number, PinKind.Input));
                break;
            case FindItemsMode.ByName:
                InputPins.Add(new NodePin(Guid.NewGuid().ToString(), Id, "Name", PinType.String, PinKind.Input));
                break;
            case FindItemsMode.ByFilter:
                // No additional input pins needed for filter mode - uses properties
                break;
        }
    }

    public override void Execute(VScriptContext context)
    {
        
        var items = new List<Item>();

        if (World.Player == null)
        {
            context.ErrorMessage = "Player is not available";
            return;
        }

        switch (SearchMode)
        {
            case FindItemsMode.ByLayer:
                {
                    var layerPin = InputPins.Find(p => p.Name == "Layer");
                    if (layerPin?.Value != null)
                    {
                        byte layer = Convert.ToByte(layerPin.Value);
                        foreach (var item in World.Items.Values)
                        {
                            if (!item.IsDestroyed && item.Layer == (Layer)layer)
                            {
                                items.Add(item);
                            }
                        }
                    }
                }
                break;

            case FindItemsMode.BySerial:
                {
                    var serialPin = InputPins.Find(p => p.Name == "Serial");
                    if (serialPin?.Value != null)
                    {
                        uint serial = Convert.ToUInt32(serialPin.Value);
                        Item item = null;
                        World.Items.TryGetValue(serial, out item);
                        if (item != null && !item.IsDestroyed)
                        {
                            items.Add(item);
                        }
                    }
                }
                break;

            case FindItemsMode.ByType:
                {
                    var typePin = InputPins.Find(p => p.Name == "Type");
                    if (typePin?.Value != null)
                    {
                        ushort type = Convert.ToUInt16(typePin.Value);
                        foreach (var item in World.Items.Values)
                        {
                            if (!item.IsDestroyed && item.Graphic == type)
                            {
                                items.Add(item);
                            }
                        }
                    }
                }
                break;

            case FindItemsMode.ByName:
                {
                    var namePin = InputPins.Find(p => p.Name == "Name");
                    if (namePin?.Value != null)
                    {
                        string name = namePin.Value.ToString();
                        foreach (var item in World.Items.Values)
                        {
                            if (!item.IsDestroyed && item.Name != null &&
                                item.Name.Contains(name, StringComparison.OrdinalIgnoreCase))
                            {
                                items.Add(item);
                            }
                        }
                    }
                }
                break;

            case FindItemsMode.ByFilter:
                {
                    foreach (var item in World.Items.Values)
                    {
                        if (item.IsDestroyed)
                            continue;

                        // Apply all filter criteria
                        // Check user-defined excluded IDs
                        if (ExcludedItemIDs != null && ExcludedItemIDs.Count > 0 && ExcludedItemIDs.Contains(item.Graphic))
                            continue;
                        // Check centralized search exclusions
                        if (!AssistantData.ScriptingRestrictions.IsItemSearchable(item.Graphic))
                            continue;
                        if (IsCorpse.HasValue && item.IsCorpse != IsCorpse.Value)
                            continue;
                        if (IsContainer.HasValue && item.ItemData.IsContainer != IsContainer.Value)
                            continue;
                        if (IsMovable.HasValue && item.IsLocked != !IsMovable.Value)
                            continue;
                        if (IsOnGround.HasValue && item.OnGround != IsOnGround.Value)
                            continue;
                        if (!string.IsNullOrEmpty(Name) && item.Name != null && !item.Name.Contains(Name, StringComparison.OrdinalIgnoreCase))
                            continue;
                        if (RangeMin.HasValue && item.Distance < RangeMin.Value)
                            continue;
                        if (RangeMax.HasValue && item.Distance > RangeMax.Value)
                            continue;
                        if (Graphics != null && Graphics.Count > 0 && !Graphics.Contains(item.Graphic))
                            continue;
                        if (Layers != null && Layers.Count > 0 && !Layers.Contains((byte)item.Layer))
                            continue;
                        if (Hues != null && Hues.Count > 0 && !Hues.Contains(item.Hue))
                            continue;

                        items.Add(item);
                    }
                }
                break;
        }

        // RAZOR-ZUSATZ: Duplikate raus (ein Item nur einmal ausgeben), dann
        // die Filterkette anwenden.
        items = items.Distinct().ToList();

        if (Filters.Count > 0)
        {
            items = items.Where(it => FindFilterCatalog.MatchesChain(Filters, it,
                f => InputPins.Find(p => p.Id == f.PinId), FindFilterCatalog.MatchItem)).ToList();
        }

        // Set the output pin to the list of items
        var outputPin = OutputPins.Find(p => p.Name == "Items");
        if (outputPin != null)
        {
            outputPin.Value = items;
        }
    }
}

// ===== GUMP NODES =====

// Gump Reference Node
public class GumpNode : VScriptNode
{
    public GumpNode(string id, string pinIdCounter) : base(id, "Gump", NodeCategory.Game)
    {
        InputPins.Add(new NodePin(Guid.NewGuid().ToString(), id, "Serial", PinType.Number, PinKind.Input));
        OutputPins.Add(new NodePin(Guid.NewGuid().ToString(), id, "Gump", PinType.Object, PinKind.Output, objectSubType: ObjectSubType.Gump));
    }

    public override Vector4 GetTitleBarColor()
    {
        return new Vector4(0.4f, 0.6f, 0.3f, 1.0f); // Olive-green like UE reference nodes
    }

    public override void Execute(VScriptContext context)
    {
        var serialPin = InputPins.Find(p => p.Name == "Serial");
        if (serialPin?.Value == null)
        {
            context.ErrorMessage = "Gump: Serial is required";
            return;
        }

        uint serial = Convert.ToUInt32(serialPin.Value);

        // Set the gump serial as output
        var outputPin = OutputPins[0];
        outputPin.Value = serial;
    }
}

// Wait for Gump node
public class WaitForGumpNode : VScriptNode
{
    public WaitForGumpNode(string id, string pinIdCounter) : base(id, "Wait For Gump", NodeCategory.Game)
    {
        InputPins.Add(new NodePin(Guid.NewGuid().ToString(), id, "", PinType.Flow, PinKind.Input));
        InputPins.Add(new NodePin(Guid.NewGuid().ToString(), id, "Gump", PinType.Object, PinKind.Input));
        InputPins.Add(new NodePin(Guid.NewGuid().ToString(), id, "Timeout", PinType.Number, PinKind.Input));
        OutputPins.Add(new NodePin(Guid.NewGuid().ToString(), id, "", PinType.Flow, PinKind.Output));
        OutputPins.Add(new NodePin(Guid.NewGuid().ToString(), id, "Success", PinType.Boolean, PinKind.Output));
    }

    public override Vector4 GetTitleBarColor()
    {
        return new Vector4(0.4f, 0.6f, 0.3f, 1.0f); // Olive-green like UE reference nodes
    }

    public override void Execute(VScriptContext context)
    {
        var gumpPin = InputPins.Find(p => p.Name == "Gump");
        if (gumpPin?.Value == null)
        {
            context.ErrorMessage = "Wait For Gump: Gump serial is required";
            OutputPins[1].Value = false;
            return;
        }

        uint gumpSerial = Convert.ToUInt32(gumpPin.Value);

        var timeoutPin = InputPins.Find(p => p.Name == "Timeout");
        int timeout = timeoutPin?.Value != null ? Convert.ToInt32(timeoutPin.Value) : 5000; // Default 5 seconds

        var startTime = System.DateTime.Now;
        var timeoutSpan = System.TimeSpan.FromMilliseconds(timeout);

        while (System.DateTime.Now - startTime < timeoutSpan)
        {
            foreach (var gump in UIManager.Gumps)
            {
                if (gump.ServerSerial == gumpSerial)
                {
                    OutputPins[1].Value = true;
                    return;
                }
            }

            // Small delay to prevent CPU spinning
            System.Threading.Thread.Sleep(10);
        }

        // Timeout
        OutputPins[1].Value = false;
    }
}

// Is Active Gump node
public class IsActiveGumpNode : VScriptNode
{
    public IsActiveGumpNode(string id, string pinIdCounter) : base(id, "Is Active", NodeCategory.Game)
    {
        InputPins.Add(new NodePin(Guid.NewGuid().ToString(), id, "Gump", PinType.Object, PinKind.Input));
        OutputPins.Add(new NodePin(Guid.NewGuid().ToString(), id, "Is Active", PinType.Boolean, PinKind.Output));
    }

    public override Vector4 GetTitleBarColor()
    {
        return new Vector4(0.4f, 0.6f, 0.3f, 1.0f); // Olive-green like UE reference nodes
    }

    public override void Execute(VScriptContext context)
    {
        var gumpPin = InputPins.Find(p => p.Name == "Gump");
        if (gumpPin?.Value == null)
        {
            context.ErrorMessage = "Is Active: Gump serial is required";
            OutputPins[0].Value = false;
            return;
        }

        uint gumpSerial = Convert.ToUInt32(gumpPin.Value);
        bool isActive = false;

        foreach (var gump in UIManager.Gumps)
        {
            if (gump.ServerSerial == gumpSerial)
            {
                isActive = true;
                break;
            }
        }

        OutputPins[0].Value = isActive;
    }
}

// Press Button Gump node
public class PressButtonGumpNode : VScriptNode
{
    public PressButtonGumpNode(string id, string pinIdCounter) : base(id, "Press Button", NodeCategory.Game)
    {
        InputPins.Add(new NodePin(Guid.NewGuid().ToString(), id, "", PinType.Flow, PinKind.Input));
        InputPins.Add(new NodePin(Guid.NewGuid().ToString(), id, "Gump", PinType.Object, PinKind.Input));
        InputPins.Add(new NodePin(Guid.NewGuid().ToString(), id, "Button ID", PinType.Number, PinKind.Input));
        OutputPins.Add(new NodePin(Guid.NewGuid().ToString(), id, "", PinType.Flow, PinKind.Output));
        OutputPins.Add(new NodePin(Guid.NewGuid().ToString(), id, "Success", PinType.Boolean, PinKind.Output));
    }

    public override Vector4 GetTitleBarColor()
    {
        return new Vector4(0.4f, 0.6f, 0.3f, 1.0f); // Olive-green like UE reference nodes
    }

    public override void Execute(VScriptContext context)
    {
        var gumpPin = InputPins.Find(p => p.Name == "Gump");
        if (gumpPin?.Value == null)
        {
            context.ErrorMessage = "Press Button: Gump serial is required";
            OutputPins[1].Value = false;
            return;
        }

        uint gumpSerial = Convert.ToUInt32(gumpPin.Value);

        var buttonPin = InputPins.Find(p => p.Name == "Button ID");
        if (buttonPin?.Value == null)
        {
            context.ErrorMessage = "Press Button: Button ID is required";
            OutputPins[1].Value = false;
            return;
        }

        int buttonId = Convert.ToInt32(buttonPin.Value);

        GumpShim gump = null;

        foreach (var g in UIManager.Gumps)
        {
            if (g.ServerSerial == gumpSerial)
            {
                gump = g;
                break;
            }
        }

        if (gump == null)
        {
            context.ErrorMessage = $"Press Button: Gump with serial {gumpSerial} not found";
            OutputPins[1].Value = false;
            return;
        }

        gump.OnButtonClick(buttonId);
        OutputPins[1].Value = true;
    }
}

// ===== JOURNAL NODES =====

// Journal Reference Node
public class JournalNode : VScriptNode
{
    public JournalNode(string id, string pinIdCounter) : base(id, "Journal", NodeCategory.Game)
    {
        OutputPins.Add(new NodePin(Guid.NewGuid().ToString(), id, "Journal", PinType.Object, PinKind.Output, objectSubType: ObjectSubType.Journal));
    }

    public override Vector4 GetTitleBarColor()
    {
        return new Vector4(0.4f, 0.6f, 0.3f, 1.0f); // Olive-green like reference nodes
    }

    public override void Execute(VScriptContext context)
    {
        // Journal is a static reference, no execution needed
    }
}

// Journal Contains Node - Check if journal contains text and return the entry
public class JournalContainsNode : VScriptNode
{
    public JournalContainsNode(string id, string pinIdCounter) : base(id, "Contains", NodeCategory.Game)
    {
        InputPins.Add(new NodePin(Guid.NewGuid().ToString(), id, "", PinType.Flow, PinKind.Input));
        InputPins.Add(new NodePin(Guid.NewGuid().ToString(), id, "Journal", PinType.Object, PinKind.Input, objectSubType: ObjectSubType.Journal));
        InputPins.Add(new NodePin(Guid.NewGuid().ToString(), id, "Search", PinType.String, PinKind.Input));

        OutputPins.Add(new NodePin(Guid.NewGuid().ToString(), id, "", PinType.Flow, PinKind.Output));
        OutputPins.Add(new NodePin(Guid.NewGuid().ToString(), id, "Entry", PinType.Object, PinKind.Output) { ObjectSubType = ObjectSubType.JournalEntry });
        OutputPins.Add(new NodePin(Guid.NewGuid().ToString(), id, "Found", PinType.Boolean, PinKind.Output));
    }

    public override Vector4 GetTitleBarColor()
    {
        return new Vector4(0.4f, 0.6f, 0.3f, 1.0f); // Olive-green like reference nodes
    }

    public override void Execute(VScriptContext context)
    {
        var searchPin = InputPins.Find(p => p.Name == "Search");
        var entryOutputPin = OutputPins.Find(p => p.Name == "Entry");
        var foundOutputPin = OutputPins.Find(p => p.Name == "Found");

        if (searchPin?.Value == null || entryOutputPin == null || foundOutputPin == null)
        {
            context.ErrorMessage = "JournalContains: Search text is required";
            return;
        }

        string searchText = searchPin.Value.ToString();
        bool found = false;
        JournalEntry foundEntry = null;

        var journalEntries = Assistant.VScripts.Engine.Journal.Entries;
        if (journalEntries != null)
        {
            // Respect the shared journal clear filter: ignore entries logged before the last Clear.
            DateTime clearTime = Assistant.VScripts.Engine.Journal.StartTime;

            foreach (var entry in journalEntries)
            {
                if (entry.Time < clearTime)
                {
                    continue;
                }

                if (entry.Text != null && entry.Text.Contains(searchText, StringComparison.OrdinalIgnoreCase))
                {
                    found = true;
                    foundEntry = entry;
                    break;
                }
            }
        }

        entryOutputPin.Value = foundEntry;
        foundOutputPin.Value = found;
    }
}

// Clear Journal Node
public class ClearJournalNode : VScriptNode
{
    public ClearJournalNode(string id, string pinIdCounter) : base(id, "Clear Journal", NodeCategory.Game)
    {
        InputPins.Add(new NodePin(Guid.NewGuid().ToString(), id, "", PinType.Flow, PinKind.Input));
        InputPins.Add(new NodePin(Guid.NewGuid().ToString(), id, "Journal", PinType.Object, PinKind.Input, objectSubType: ObjectSubType.Journal));

        OutputPins.Add(new NodePin(Guid.NewGuid().ToString(), id, "", PinType.Flow, PinKind.Output));
    }

    public override Vector4 GetTitleBarColor()
    {
        return new Vector4(0.4f, 0.6f, 0.3f, 1.0f); // Olive-green like reference nodes
    }

    public override void Execute(VScriptContext context)
    {
        // Journal.Clear() does not erase entries - it advances a shared "start time" filter so that
        // searches ignore everything logged before now. Use the same shared filter as Lua/UOR so the
        // clear actually takes effect for VScript searches (JournalContainsNode respects it).
        Assistant.VScripts.Engine.Journal.Clear();
    }
}

// ===== JOURNAL ENTRY PROPERTY NODES =====

// Base class for JournalEntry property nodes
public abstract class JournalEntryPropertyNode : VScriptNode
{
    protected JournalEntryPropertyNode(string id, string name) : base(id, name, NodeCategory.Game)
    {
        InputPins.Add(new NodePin(Guid.NewGuid().ToString(), id, "Entry", PinType.Object, PinKind.Input) { ObjectSubType = ObjectSubType.JournalEntry });
    }

    public override Vector4 GetTitleBarColor()
    {
        return new Vector4(0.4f, 0.6f, 0.3f, 1.0f); // Olive-green
    }
}

public class GetJournalEntryTextNode : JournalEntryPropertyNode
{
    public GetJournalEntryTextNode(string id, string pinIdCounter) : base(id, "Get Text")
    {
        OutputPins.Add(new NodePin(Guid.NewGuid().ToString(), id, "Text", PinType.String, PinKind.Output));
    }

    public override void Execute(VScriptContext context)
    {
        var inputPin = InputPins[0];
        var outputPin = OutputPins[0];

        if (inputPin.Value is JournalEntry entry)
        {
            outputPin.Value = entry.Text ?? "";
        }
        else
        {
            outputPin.Value = "";
        }
    }
}

public class GetJournalEntryHueNode : JournalEntryPropertyNode
{
    public GetJournalEntryHueNode(string id, string pinIdCounter) : base(id, "Get Hue")
    {
        OutputPins.Add(new NodePin(Guid.NewGuid().ToString(), id, "Hue", PinType.Number, PinKind.Output));
    }

    public override void Execute(VScriptContext context)
    {
        var inputPin = InputPins[0];
        var outputPin = OutputPins[0];

        if (inputPin.Value is JournalEntry entry)
        {
            outputPin.Value = (float)entry.Hue;
        }
        else
        {
            outputPin.Value = 0f;
        }
    }
}

public class GetJournalEntryNameNode : JournalEntryPropertyNode
{
    public GetJournalEntryNameNode(string id, string pinIdCounter) : base(id, "Get Name")
    {
        OutputPins.Add(new NodePin(Guid.NewGuid().ToString(), id, "Name", PinType.String, PinKind.Output));
    }

    public override void Execute(VScriptContext context)
    {
        var inputPin = InputPins[0];
        var outputPin = OutputPins[0];

        if (inputPin.Value is JournalEntry entry)
        {
            outputPin.Value = entry.Name ?? "";
        }
        else
        {
            outputPin.Value = "";
        }
    }
}

public class GetJournalEntryTimeNode : JournalEntryPropertyNode
{
    public GetJournalEntryTimeNode(string id, string pinIdCounter) : base(id, "Get Time")
    {
        OutputPins.Add(new NodePin(Guid.NewGuid().ToString(), id, "Time", PinType.String, PinKind.Output));
    }

    public override void Execute(VScriptContext context)
    {
        var inputPin = InputPins[0];
        var outputPin = OutputPins[0];

        if (inputPin.Value is JournalEntry entry)
        {
            outputPin.Value = entry.Time.ToString("HH:mm:ss");
        }
        else
        {
            outputPin.Value = "";
        }
    }
}

public class GetJournalEntryTextTypeNode : JournalEntryPropertyNode
{
    public GetJournalEntryTextTypeNode(string id, string pinIdCounter) : base(id, "Get Text Type")
    {
        OutputPins.Add(new NodePin(Guid.NewGuid().ToString(), id, "Type", PinType.Number, PinKind.Output));
    }

    public override void Execute(VScriptContext context)
    {
        var inputPin = InputPins[0];
        var outputPin = OutputPins[0];

        if (inputPin.Value is JournalEntry entry)
        {
            outputPin.Value = (float)entry.TextType;
        }
        else
        {
            outputPin.Value = 0f;
        }
    }
}

// ===== ITEM NODES =====

// Item Reference Node
public class ItemNode : VScriptNode
{
    public ItemNode(string id, string pinIdCounter) : base(id, "Item", NodeCategory.Game)
    {
        OutputPins.Add(new NodePin(Guid.NewGuid().ToString(), id, "Item", PinType.Object, PinKind.Output) { ObjectSubType = ObjectSubType.Item });
    }

    public override Vector4 GetTitleBarColor()
    {
        return new Vector4(0.4f, 0.6f, 0.3f, 1.0f); // Olive-green like reference nodes
    }

    public override void Execute(VScriptContext context)
    {
        // Item is a static reference, no execution needed
    }
}

// ===== ITEM PROPERTY NODES =====

// Base class for Item property nodes
public abstract class ItemPropertyNode : VScriptNode
{
    protected ItemPropertyNode(string id, string name) : base(id, name, NodeCategory.Game)
    {
        InputPins.Add(new NodePin(Guid.NewGuid().ToString(), id, "Item", PinType.Object, PinKind.Input) { ObjectSubType = ObjectSubType.Item });
    }

    public override Vector4 GetTitleBarColor()
    {
        return new Vector4(0.4f, 0.6f, 0.3f, 1.0f); // Olive-green
    }
}

public class GetItemXNode : ItemPropertyNode
{
    public GetItemXNode(string id, string pinIdCounter) : base(id, "Get X")
    {
        OutputPins.Add(new NodePin(Guid.NewGuid().ToString(), id, "X", PinType.Number, PinKind.Output));
    }

    public override void Execute(VScriptContext context)
    {
        var inputPin = InputPins[0];
        var outputPin = OutputPins[0];

        if (inputPin.Value is Item item)
        {
            outputPin.Value = (float)item.X;
        }
        else
        {
            outputPin.Value = 0f;
        }
    }
}

public class GetItemYNode : ItemPropertyNode
{
    public GetItemYNode(string id, string pinIdCounter) : base(id, "Get Y")
    {
        OutputPins.Add(new NodePin(Guid.NewGuid().ToString(), id, "Y", PinType.Number, PinKind.Output));
    }

    public override void Execute(VScriptContext context)
    {
        var inputPin = InputPins[0];
        var outputPin = OutputPins[0];

        if (inputPin.Value is Item item)
        {
            outputPin.Value = (float)item.Y;
        }
        else
        {
            outputPin.Value = 0f;
        }
    }
}

public class GetItemZNode : ItemPropertyNode
{
    public GetItemZNode(string id, string pinIdCounter) : base(id, "Get Z")
    {
        OutputPins.Add(new NodePin(Guid.NewGuid().ToString(), id, "Z", PinType.Number, PinKind.Output));
    }

    public override void Execute(VScriptContext context)
    {
        var inputPin = InputPins[0];
        var outputPin = OutputPins[0];

        if (inputPin.Value is Item item)
        {
            outputPin.Value = (float)item.Z;
        }
        else
        {
            outputPin.Value = 0f;
        }
    }
}

public class GetItemHueNode : ItemPropertyNode
{
    public GetItemHueNode(string id, string pinIdCounter) : base(id, "Get Hue")
    {
        OutputPins.Add(new NodePin(Guid.NewGuid().ToString(), id, "Hue", PinType.Number, PinKind.Output));
    }

    public override void Execute(VScriptContext context)
    {
        var inputPin = InputPins[0];
        var outputPin = OutputPins[0];

        if (inputPin.Value is Item item)
        {
            outputPin.Value = (float)item.Hue;
        }
        else
        {
            outputPin.Value = 0f;
        }
    }
}

public class GetItemGraphicNode : ItemPropertyNode
{
    public GetItemGraphicNode(string id, string pinIdCounter) : base(id, "Get Graphic")
    {
        OutputPins.Add(new NodePin(Guid.NewGuid().ToString(), id, "Graphic", PinType.Number, PinKind.Output));
    }

    public override void Execute(VScriptContext context)
    {
        var inputPin = InputPins[0];
        var outputPin = OutputPins[0];

        if (inputPin.Value is Item item)
        {
            outputPin.Value = (float)item.Graphic;
        }
        else
        {
            outputPin.Value = 0f;
        }
    }
}

public class GetItemNameNode : ItemPropertyNode
{
    public GetItemNameNode(string id, string pinIdCounter) : base(id, "Get Name")
    {
        OutputPins.Add(new NodePin(Guid.NewGuid().ToString(), id, "Name", PinType.String, PinKind.Output));
    }

    public override void Execute(VScriptContext context)
    {
        var inputPin = InputPins[0];
        var outputPin = OutputPins[0];

        if (inputPin.Value is Item item)
        {
            outputPin.Value = item.Name ?? "";
        }
        else
        {
            outputPin.Value = "";
        }
    }
}

public class GetItemSerialNode : ItemPropertyNode
{
    public GetItemSerialNode(string id, string pinIdCounter) : base(id, "Get Serial")
    {
        OutputPins.Add(new NodePin(Guid.NewGuid().ToString(), id, "Serial", PinType.Number, PinKind.Output));
    }

    public override void Execute(VScriptContext context)
    {
        var inputPin = InputPins[0];
        var outputPin = OutputPins[0];

        if (inputPin.Value is Item item)
        {
            outputPin.Value = (float)(uint)item.Serial;
        }
        else
        {
            outputPin.Value = 0f;
        }
    }
}

public class GetItemDistanceNode : ItemPropertyNode
{
    public GetItemDistanceNode(string id, string pinIdCounter) : base(id, "Get Distance")
    {
        OutputPins.Add(new NodePin(Guid.NewGuid().ToString(), id, "Distance", PinType.Number, PinKind.Output));
    }

    public override void Execute(VScriptContext context)
    {
        var inputPin = InputPins[0];
        var outputPin = OutputPins[0];

        if (inputPin.Value is Item item)
        {
            outputPin.Value = (float)item.Distance;
        }
        else
        {
            outputPin.Value = 0f;
        }
    }
}

public class GetItemDirectionNode : ItemPropertyNode
{
    public GetItemDirectionNode(string id, string pinIdCounter) : base(id, "Get Direction")
    {
        OutputPins.Add(new NodePin(Guid.NewGuid().ToString(), id, "Direction", PinType.String, PinKind.Output));
    }

    public override void Execute(VScriptContext context)
    {
        var inputPin = InputPins[0];
        var outputPin = OutputPins[0];

        if (inputPin.Value is Item item)
        {
            outputPin.Value = item.Direction.ToString();
        }
        else
        {
            outputPin.Value = "";
        }
    }
}

public class GetItemIsDestroyedNode : ItemPropertyNode
{
    public GetItemIsDestroyedNode(string id, string pinIdCounter) : base(id, "Get Is Destroyed")
    {
        OutputPins.Add(new NodePin(Guid.NewGuid().ToString(), id, "Is Destroyed", PinType.Boolean, PinKind.Output));
    }

    public override void Execute(VScriptContext context)
    {
        var inputPin = InputPins[0];
        var outputPin = OutputPins[0];

        if (inputPin.Value is Item item)
        {
            outputPin.Value = item.IsDestroyed;
        }
        else
        {
            outputPin.Value = false;
        }
    }
}

public class GetItemPropertiesNode : ItemPropertyNode
{
    public GetItemPropertiesNode(string id, string pinIdCounter) : base(id, "Get Properties")
    {
        OutputPins.Add(new NodePin(Guid.NewGuid().ToString(), id, "Properties", PinType.String, PinKind.Output));
    }

    public override void Execute(VScriptContext context)
    {
        var inputPin = InputPins[0];
        var outputPin = OutputPins[0];

        if (inputPin.Value is Item item)
        {
            var test = World.OPL.TryGetNameAndData(item.Serial, out string oplName, out string data);
            if (test && !string.IsNullOrEmpty(oplName) && !string.IsNullOrEmpty(data))
            {
                outputPin.Value = $"{oplName}\n{data}";
            }
            else
            {
                outputPin.Value = "";
            }
        }
        else
        {
            outputPin.Value = "";
        }
    }
}

public class GetItemAmountNode : ItemPropertyNode
{
    public GetItemAmountNode(string id, string pinIdCounter) : base(id, "Get Amount")
    {
        OutputPins.Add(new NodePin(Guid.NewGuid().ToString(), id, "Amount", PinType.Number, PinKind.Output));
    }

    public override void Execute(VScriptContext context)
    {
        var inputPin = InputPins[0];
        var outputPin = OutputPins[0];

        if (inputPin.Value is Item item)
        {
            outputPin.Value = (float)item.Amount;
        }
        else
        {
            outputPin.Value = 0f;
        }
    }
}

public class GetItemIsDamageableNode : ItemPropertyNode
{
    public GetItemIsDamageableNode(string id, string pinIdCounter) : base(id, "Get Is Damageable")
    {
        OutputPins.Add(new NodePin(Guid.NewGuid().ToString(), id, "Is Damageable", PinType.Boolean, PinKind.Output));
    }

    public override void Execute(VScriptContext context)
    {
        var inputPin = InputPins[0];
        var outputPin = OutputPins[0];

        if (inputPin.Value is Item item)
        {
            outputPin.Value = item.IsDamageable;
        }
        else
        {
            outputPin.Value = false;
        }
    }
}

public class GetItemIsCoinNode : ItemPropertyNode
{
    public GetItemIsCoinNode(string id, string pinIdCounter) : base(id, "Get Is Coin")
    {
        OutputPins.Add(new NodePin(Guid.NewGuid().ToString(), id, "Is Coin", PinType.Boolean, PinKind.Output));
    }

    public override void Execute(VScriptContext context)
    {
        var inputPin = InputPins[0];
        var outputPin = OutputPins[0];

        if (inputPin.Value is Item item)
        {
            outputPin.Value = item.IsCoin;
        }
        else
        {
            outputPin.Value = false;
        }
    }
}

public class GetItemIsCorpseNode : ItemPropertyNode
{
    public GetItemIsCorpseNode(string id, string pinIdCounter) : base(id, "Get Is Corpse")
    {
        OutputPins.Add(new NodePin(Guid.NewGuid().ToString(), id, "Is Corpse", PinType.Boolean, PinKind.Output));
    }

    public override void Execute(VScriptContext context)
    {
        var inputPin = InputPins[0];
        var outputPin = OutputPins[0];

        if (inputPin.Value is Item item)
        {
            outputPin.Value = item.IsCorpse;
        }
        else
        {
            outputPin.Value = false;
        }
    }
}

public class GetItemIsEmptyNode : ItemPropertyNode
{
    public GetItemIsEmptyNode(string id, string pinIdCounter) : base(id, "Get Is Empty")
    {
        OutputPins.Add(new NodePin(Guid.NewGuid().ToString(), id, "Is Empty", PinType.Boolean, PinKind.Output));
    }

    public override void Execute(VScriptContext context)
    {
        var inputPin = InputPins[0];
        var outputPin = OutputPins[0];

        if (inputPin.Value is Item item)
        {
            outputPin.Value = item.IsEmpty;
        }
        else
        {
            outputPin.Value = false;
        }
    }
}

public class GetItemIsLockedNode : ItemPropertyNode
{
    public GetItemIsLockedNode(string id, string pinIdCounter) : base(id, "Get Is Locked")
    {
        OutputPins.Add(new NodePin(Guid.NewGuid().ToString(), id, "Is Locked", PinType.Boolean, PinKind.Output));
    }

    public override void Execute(VScriptContext context)
    {
        var inputPin = InputPins[0];
        var outputPin = OutputPins[0];

        if (inputPin.Value is Item item)
        {
            outputPin.Value = item.IsLocked;
        }
        else
        {
            outputPin.Value = false;
        }
    }
}

public class GetItemIsHiddenNode : ItemPropertyNode
{
    public GetItemIsHiddenNode(string id, string pinIdCounter) : base(id, "Get Is Hidden")
    {
        OutputPins.Add(new NodePin(Guid.NewGuid().ToString(), id, "Is Hidden", PinType.Boolean, PinKind.Output));
    }

    public override void Execute(VScriptContext context)
    {
        var inputPin = InputPins[0];
        var outputPin = OutputPins[0];

        if (inputPin.Value is Item item)
        {
            outputPin.Value = item.IsHidden;
        }
        else
        {
            outputPin.Value = false;
        }
    }
}

public class GetItemIsMultiNode : ItemPropertyNode
{
    public GetItemIsMultiNode(string id, string pinIdCounter) : base(id, "Get Is Multi")
    {
        OutputPins.Add(new NodePin(Guid.NewGuid().ToString(), id, "Is Multi", PinType.Boolean, PinKind.Output));
    }

    public override void Execute(VScriptContext context)
    {
        var inputPin = InputPins[0];
        var outputPin = OutputPins[0];

        if (inputPin.Value is Item item)
        {
            outputPin.Value = item.IsMulti;
        }
        else
        {
            outputPin.Value = false;
        }
    }
}

public class GetItemIsLootableNode : ItemPropertyNode
{
    public GetItemIsLootableNode(string id, string pinIdCounter) : base(id, "Get Is Lootable")
    {
        OutputPins.Add(new NodePin(Guid.NewGuid().ToString(), id, "Is Lootable", PinType.Boolean, PinKind.Output));
    }

    public override void Execute(VScriptContext context)
    {
        var inputPin = InputPins[0];
        var outputPin = OutputPins[0];

        if (inputPin.Value is Item item)
        {
            outputPin.Value = item.IsLootable;
        }
        else
        {
            outputPin.Value = false;
        }
    }
}

public class GetItemOnGroundNode : ItemPropertyNode
{
    public GetItemOnGroundNode(string id, string pinIdCounter) : base(id, "Get On Ground")
    {
        OutputPins.Add(new NodePin(Guid.NewGuid().ToString(), id, "On Ground", PinType.Boolean, PinKind.Output));
    }

    public override void Execute(VScriptContext context)
    {
        var inputPin = InputPins[0];
        var outputPin = OutputPins[0];

        if (inputPin.Value is Item item)
        {
            outputPin.Value = item.OnGround;
        }
        else
        {
            outputPin.Value = false;
        }
    }
}

public class GetItemDisplayedGraphicNode : ItemPropertyNode
{
    public GetItemDisplayedGraphicNode(string id, string pinIdCounter) : base(id, "Get Displayed Graphic")
    {
        OutputPins.Add(new NodePin(Guid.NewGuid().ToString(), id, "Displayed Graphic", PinType.Number, PinKind.Output));
    }

    public override void Execute(VScriptContext context)
    {
        var inputPin = InputPins[0];
        var outputPin = OutputPins[0];

        if (inputPin.Value is Item item)
        {
            outputPin.Value = (float)item.DisplayedGraphic;
        }
        else
        {
            outputPin.Value = 0f;
        }
    }
}

public class GetItemMultiGraphicNode : ItemPropertyNode
{
    public GetItemMultiGraphicNode(string id, string pinIdCounter) : base(id, "Get Multi Graphic")
    {
        OutputPins.Add(new NodePin(Guid.NewGuid().ToString(), id, "Multi Graphic", PinType.Number, PinKind.Output));
    }

    public override void Execute(VScriptContext context)
    {
        var inputPin = InputPins[0];
        var outputPin = OutputPins[0];

        if (inputPin.Value is Item item)
        {
            outputPin.Value = (float)item.MultiGraphic;
        }
        else
        {
            outputPin.Value = 0f;
        }
    }
}

public class GetItemLayerNode : ItemPropertyNode
{
    public GetItemLayerNode(string id, string pinIdCounter) : base(id, "Get Layer")
    {
        OutputPins.Add(new NodePin(Guid.NewGuid().ToString(), id, "Layer", PinType.String, PinKind.Output));
    }

    public override void Execute(VScriptContext context)
    {
        var inputPin = InputPins[0];
        var outputPin = OutputPins[0];

        if (inputPin.Value is Item item)
        {
            outputPin.Value = item.Layer.ToString();
        }
        else
        {
            outputPin.Value = "";
        }
    }
}

public class GetItemLightIDNode : ItemPropertyNode
{
    public GetItemLightIDNode(string id, string pinIdCounter) : base(id, "Get Light ID")
    {
        OutputPins.Add(new NodePin(Guid.NewGuid().ToString(), id, "Light ID", PinType.Number, PinKind.Output));
    }

    public override void Execute(VScriptContext context)
    {
        var inputPin = InputPins[0];
        var outputPin = OutputPins[0];

        if (inputPin.Value is Item item)
        {
            outputPin.Value = (float)item.LightID;
        }
        else
        {
            outputPin.Value = 0f;
        }
    }
}

public class GetItemRootContainerNode : ItemPropertyNode
{
    public GetItemRootContainerNode(string id, string pinIdCounter) : base(id, "Get Root Container")
    {
        OutputPins.Add(new NodePin(Guid.NewGuid().ToString(), id, "Root Container", PinType.Number, PinKind.Output));
    }

    public override void Execute(VScriptContext context)
    {
        var inputPin = InputPins[0];
        var outputPin = OutputPins[0];

        if (inputPin.Value is Item item)
        {
            outputPin.Value = (float)item.RootContainer;
        }
        else
        {
            outputPin.Value = 0f;
        }
    }
}

public class GetItemContainerNode : ItemPropertyNode
{
    public GetItemContainerNode(string id, string pinIdCounter) : base(id, "Get Container")
    {
        OutputPins.Add(new NodePin(Guid.NewGuid().ToString(), id, "Container", PinType.Number, PinKind.Output));
    }

    public override void Execute(VScriptContext context)
    {
        var inputPin = InputPins[0];
        var outputPin = OutputPins[0];

        if (inputPin.Value is Item item)
        {
            outputPin.Value = (float)item.Container;
        }
        else
        {
            outputPin.Value = 0f;
        }
    }
}

public enum MoveItemMethod
{
    ContainerToContainer,
    GroundToContainer
}

public enum MoveItemSelectionMode
{
    BySerial,
    ByType
}

// Move Items node - iterates through items and moves them with delays
public class MoveItemsNode : VScriptNode
{
    public override float Width => 280.0f; // Wider for controls

    // Configuration fields
    public MoveItemSelectionMode SelectionMode { get; set; } = MoveItemSelectionMode.ByType;
    public bool AllItems { get; set; } = false;
    public int Amount { get; set; } = 1;
    public int DelayMs { get; set; } = 650;
    public MoveItemMethod Method { get; set; } = MoveItemMethod.ContainerToContainer;

    // State for iteration
    private List<Item> _itemsToMove;
    private int _currentIndex;

    public MoveItemsNode(string id, string pinIdCounter) : base(id, "Move Items", NodeCategory.Game)
    {
        // Input pins - start with flow pin
        InputPins.Add(new NodePin(Guid.NewGuid().ToString(), id, "", PinType.Flow, PinKind.Input));

        // Output pins
        OutputPins.Add(new NodePin(Guid.NewGuid().ToString(), id, "Loop Body", PinType.Flow, PinKind.Output));
        OutputPins.Add(new NodePin(Guid.NewGuid().ToString(), id, "Completed", PinType.Flow, PinKind.Output));
        OutputPins.Add(new NodePin(Guid.NewGuid().ToString(), id, "Item", PinType.Object, PinKind.Output) { ObjectSubType = ObjectSubType.Item });
        OutputPins.Add(new NodePin(Guid.NewGuid().ToString(), id, "Success", PinType.Boolean, PinKind.Output));
        OutputPins.Add(new NodePin(Guid.NewGuid().ToString(), id, "Overall Success", PinType.Boolean, PinKind.Output));

        _itemsToMove = new List<Item>();

        // Set up pins based on default mode
        UpdatePinsForMode();
    }

    public void UpdatePinsForMode()
    {
        // Remove all data input pins (keep only flow input)
        InputPins.RemoveAll(p => p.Type != PinType.Flow);

        // Add appropriate pins based on selection mode
        switch (SelectionMode)
        {
            case MoveItemSelectionMode.BySerial:
                InputPins.Add(new NodePin(Guid.NewGuid().ToString(), Id, "Source", PinType.Number, PinKind.Input));
                InputPins.Add(new NodePin(Guid.NewGuid().ToString(), Id, "Target", PinType.Number, PinKind.Input));
                InputPins.Add(new NodePin(Guid.NewGuid().ToString(), Id, "Serial", PinType.Number, PinKind.Input));
                break;
            case MoveItemSelectionMode.ByType:
                InputPins.Add(new NodePin(Guid.NewGuid().ToString(), Id, "Source", PinType.Number, PinKind.Input));
                InputPins.Add(new NodePin(Guid.NewGuid().ToString(), Id, "Target", PinType.Number, PinKind.Input));
                InputPins.Add(new NodePin(Guid.NewGuid().ToString(), Id, "Type", PinType.Number, PinKind.Input));
                break;
        }
    }

    public override void Execute(VScriptContext context)
    {
        // This is called before iteration begins - gather items to move
        var sourcePin = InputPins.Find(p => p.Name == "Source");

        if (sourcePin?.Value == null)
        {
            context.ErrorMessage = "Move Items: Source container must be specified";
            return;
        }

        uint sourceContainerSerial = Convert.ToUInt32(sourcePin.Value);

        _itemsToMove.Clear();
        _currentIndex = 0;

        switch (SelectionMode)
        {
            case MoveItemSelectionMode.BySerial:
                {
                    var serialPin = InputPins.Find(p => p.Name == "Serial");
                    if (serialPin?.Value != null)
                    {
                        uint itemSerial = Convert.ToUInt32(serialPin.Value);
                        Item item = null;
                        World.Items.TryGetValue(itemSerial, out item);

                        if (item != null && !item.IsDestroyed)
                        {
                            // Check if the item type is allowed
                            if (!AssistantData.ScriptingRestrictions.IsItemTypeAllowed(item.Graphic))
                            {
                                context.ErrorMessage = "Move Items: This action is not supported by the script engine";
                                return;
                            }
                            _itemsToMove.Add(item);
                        }
                    }
                }
                break;

            case MoveItemSelectionMode.ByType:
                {
                    var typePin = InputPins.Find(p => p.Name == "Type");
                    if (typePin?.Value != null)
                    {
                        ushort itemType = Convert.ToUInt16(typePin.Value);

                        // Check if the item type is allowed
                        if (!AssistantData.ScriptingRestrictions.IsItemTypeAllowed(itemType))
                        {
                            context.ErrorMessage = "Move Items: This action is not supported by the script engine";
                            return;
                        }

                        if (AllItems)
                        {
                            // Find all items of this type recursively in source container
                            foreach (Item item in World.Items.Values)
                            {
                                if (item.Graphic == itemType &&
                                    (item.ContainerSerial == sourceContainerSerial || item.RootContainerSerial == sourceContainerSerial) &&
                                    !item.IsDestroyed)
                                {
                                    _itemsToMove.Add(item);
                                }
                            }
                        }
                        else
                        {
                            // Find limited number of items
                            int foundCount = 0;
                            foreach (Item item in World.Items.Values)
                            {
                                if (item.Graphic == itemType &&
                                    (item.ContainerSerial == sourceContainerSerial || item.RootContainerSerial == sourceContainerSerial) &&
                                    !item.IsDestroyed)
                                {
                                    _itemsToMove.Add(item);
                                    foundCount++;
                                    if (foundCount >= Amount)
                                        break;
                                }
                            }
                        }
                    }
                }
                break;
        }

        if (_itemsToMove.Count == 0)
        {
            // No items found - set overall success to false
            var overallSuccessPin = OutputPins.Find(p => p.Name == "Overall Success");
            if (overallSuccessPin != null)
            {
                overallSuccessPin.Value = false;
            }
        }
    }

    internal List<Item> GetItemsToMove()
    {
        return _itemsToMove;
    }

    public int GetDelayMs()
    {
        return DelayMs;
    }

    internal bool TryMoveItem(Item item, uint targetContainer, out bool success)
    {
        success = false;

        try
        {
            

            // Check if item is blocked from pickup
            if (ItemPickupFilter.IsItemBlocked(item.Serial))
            {
                Message.Warning($"Cannot pick up this item type (0x{item.Graphic:X4}) - it is blocked from assistant pickup.");
                return false;
            }

            // If method is ContainerToContainer, try to open source container first
            if (Method == MoveItemMethod.ContainerToContainer && item.ContainerSerial != 0)
            {
                Item container = null;
                container = World.FindItem((Serial) item.ContainerSerial);

                if (container != null && !container.IsDestroyed)
                {
                    // Try to open container if not already opened
                    // Note: We can't reliably check if container is open, so we just try to use it
                    GameActions.DoubleClick(container.Serial);
                    // Small delay to allow container to open
                    System.Threading.Thread.Sleep(300);
                }
            }

            // Pick up the item
            int amountToMove = AllItems ? item.Amount : Math.Min(Amount, item.Amount);
            GameActions.PickUp(item, 0, 0, amountToMove);

            // Wait for pickup to be accepted
            System.Threading.Thread.Sleep(300);

            // Drop item to target container (gehalten oder noch queued-geliftet)
            var itemHold = GameActions.HeldOrQueued;
            if (itemHold != null)
            {
                GameActions.DropItem(itemHold.Serial, 0xFFFF, 0xFFFF, 0, targetContainer);
                success = true;

                // Wait for drop to complete
                System.Threading.Thread.Sleep(100);

                return true;
            }

            return false;
        }
        catch (Exception ex)
        {
            Message.Warning($"Error moving item: {ex.Message}");
            return false;
        }
    }

    internal void SetCurrentItem(Item item, bool success)
    {
        var itemPin = OutputPins.Find(p => p.Name == "Item");
        var successPin = OutputPins.Find(p => p.Name == "Success");

        if (itemPin != null)
        {
            itemPin.Value = item;
        }

        if (successPin != null)
        {
            successPin.Value = success;
        }
    }

    public void SetOverallSuccess(bool success)
    {
        var overallSuccessPin = OutputPins.Find(p => p.Name == "Overall Success");
        if (overallSuccessPin != null)
        {
            overallSuccessPin.Value = success;
        }
    }

    public NodePin GetLoopBodyPin()
    {
        return OutputPins.Find(p => p.Name == "Loop Body");
    }

    public NodePin GetCompletedPin()
    {
        return OutputPins.Find(p => p.Name == "Completed");
    }

    public uint GetTargetContainer()
    {
        var targetPin = InputPins.Find(p => p.Name == "Target");
        if (targetPin?.Value != null)
        {
            return Convert.ToUInt32(targetPin.Value);
        }
        return 0;
    }
}
