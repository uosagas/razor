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
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Assistant.LuaEngine.Enums;
using Assistant.VScripts.Core;
using Lua;

namespace Assistant.LuaEngine.API;

/// <summary>
/// Player API for Lua-CSharp engine
/// </summary>
public static class LuaPlayerAPI
{
    public static void Register(LuaState state)
    {
        var player = new LuaTable();

        // Functions
        player["Say"] = new LuaFunction("Say", Say);
        player["SayParty"] = new LuaFunction("SayParty", SayParty);
        player["SayGuild"] = new LuaFunction("SayGuild", SayGuild);
        player["SayAlliance"] = new LuaFunction("SayAlliance", SayAlliance);
        player["SayWhisper"] = new LuaFunction("SayWhisper", SayWhisper);
        player["SayYell"] = new LuaFunction("SayYell", SayYell);
        player["SayChat"] = new LuaFunction("SayChat", SayChat);
        player["SayEmote"] = new LuaFunction("SayEmote", SayEmote);
        player["UseObject"] = new LuaFunction("UseObject", UseObject);
        player["UseObjectByType"] = new LuaFunction("UseObjectByType", UseObjectByType);
        player["ClickObject"] = new LuaFunction("ClickObject", ClickObject);
        player["Attack"] = new LuaFunction("Attack", Attack);
        player["ClearHands"] = new LuaFunction("ClearHands", ClearHands);
        player["Turn"] = new LuaFunction("Turn", Turn);
        player["Equip"] = new LuaFunction("Equip", Equip);
        player["PickUp"] = new LuaFunction("PickUp", PickUp);
        player["DropInBackpack"] = new LuaFunction("DropInBackpack", DropInBackpack);
        player["DropInContainer"] = new LuaFunction("DropInContainer", DropInContainer);
        player["DropOnGround"] = new LuaFunction("DropOnGround", DropOnGround);
        player["ToggleWarMode"] = new LuaFunction("ToggleWarMode", ToggleWarMode);
        player["PopPouch"] = new LuaFunction("PopPouch", PopPouch);

        // Create metatable for property access
        var metatable = new LuaTable();
        metatable["__index"] = new LuaFunction("PlayerIndex", PlayerIndex);
        player.Metatable = metatable;

        state.Environment["Player"] = player;
    }

    private static ValueTask<int> PlayerIndex(LuaFunctionExecutionContext context, CancellationToken ct)
    {
        var propertyName = context.GetArgument<string>(1);
        var player = World.Player;

        if (player == null)
        {
            context.Return(LuaValue.Nil);
            return new ValueTask<int>(1);
        }

        switch (propertyName)
        {
            // PlayerMobile specific properties
            case "Backpack":
                var backpack = player.GetItemOnLayer(Layer.Backpack);
                context.Return(backpack != null ? LuaItemsAPI.CreateItemTable(backpack) : LuaValue.Nil);
                return new ValueTask<int>(1);
            case "PhysicalResistance":
                context.Return((double)player.PhysicalResistance);
                return new ValueTask<int>(1);
            case "FireResistance":
                context.Return((double)player.FireResistance);
                return new ValueTask<int>(1);
            case "ColdResistance":
                context.Return((double)player.ColdResistance);
                return new ValueTask<int>(1);
            case "PoisonResistance":
                context.Return((double)player.PoisonResistance);
                return new ValueTask<int>(1);
            case "EnergyResistance":
                context.Return((double)player.EnergyResistance);
                return new ValueTask<int>(1);
            case "Str":
                context.Return((double)player.Strength);
                return new ValueTask<int>(1);
            case "Dex":
                context.Return((double)player.Dexterity);
                return new ValueTask<int>(1);
            case "Int":
                context.Return((double)player.Intelligence);
                return new ValueTask<int>(1);
            case "DamageMin":
                context.Return((double)player.DamageMin);
                return new ValueTask<int>(1);
            case "DamageMax":
                context.Return((double)player.DamageMax);
                return new ValueTask<int>(1);
            case "Followers":
                context.Return((double)player.Followers);
                return new ValueTask<int>(1);
            case "MaxFollowers":
                context.Return((double)player.FollowersMax);
                return new ValueTask<int>(1);
            case "Gold":
                context.Return((double)player.Gold);
                return new ValueTask<int>(1);
            case "Luck":
                context.Return((double)player.Luck);
                return new ValueTask<int>(1);
            case "TithingPoints":
                context.Return((double)player.TithingPoints);
                return new ValueTask<int>(1);
            case "Weight":
                context.Return((double)player.Weight);
                return new ValueTask<int>(1);
            case "MaxWeight":
                context.Return((double)player.WeightMax);
                return new ValueTask<int>(1);
            case "DiffWeight":
                context.Return((double)(player.WeightMax - player.Weight));
                return new ValueTask<int>(1);
            case "StatsCap":
                context.Return((double)player.StatsCap);
                return new ValueTask<int>(1);
            case "SwingSpeedIncrease":
                context.Return((double)player.SwingSpeedIncrease);
                return new ValueTask<int>(1);
            case "HitPointsRegeneration":
                context.Return((double)player.HitPointsRegeneration);
                return new ValueTask<int>(1);
            case "ManaRegeneration":
                context.Return((double)player.ManaRegeneration);
                return new ValueTask<int>(1);
            case "StaminaRegeneration":
                context.Return((double)player.StaminaRegeneration);
                return new ValueTask<int>(1);
            case "LowerManaCost":
                context.Return((double)player.LowerManaCost);
                return new ValueTask<int>(1);
            case "LowerReagentCost":
                context.Return((double)player.LowerReagentCost);
                return new ValueTask<int>(1);
            case "HitChanceIncrease":
                context.Return((double)player.HitChanceIncrease);
                return new ValueTask<int>(1);
            case "DefenseChanceIncrease":
                context.Return((double)player.DefenseChanceIncrease);
                return new ValueTask<int>(1);
            case "SpellDamageIncrease":
                context.Return((double)player.SpellDamageIncrease);
                return new ValueTask<int>(1);
            case "ReflectPhysicalDamage":
                context.Return((double)player.ReflectPhysicalDamage);
                return new ValueTask<int>(1);

            // Mobile properties
            case "Hits":
                context.Return((double)player.Hits);
                return new ValueTask<int>(1);
            case "HitsMax":
                context.Return((double)player.HitsMax);
                return new ValueTask<int>(1);
            case "DiffHits":
                context.Return((double)(player.HitsMax - player.Hits));
                return new ValueTask<int>(1);
            case "Stam":
                context.Return((double)player.Stamina);
                return new ValueTask<int>(1);
            case "MaxStam":
                context.Return((double)player.StaminaMax);
                return new ValueTask<int>(1);
            case "Mana":
                context.Return((double)player.Mana);
                return new ValueTask<int>(1);
            case "MaxMana":
                context.Return((double)player.ManaMax);
                return new ValueTask<int>(1);
            case "IsRunning":
                context.Return(player.IsRunning);
                return new ValueTask<int>(1);
            case "IsParalyzed":
                context.Return(player.IsParalyzed);
                return new ValueTask<int>(1);
            case "IsDead":
                context.Return(player.IsDead);
                return new ValueTask<int>(1);
            case "IsHidden":
                context.Return(player.IsHidden);
                return new ValueTask<int>(1);
            case "IsPoisoned":
                context.Return(player.IsPoisoned);
                return new ValueTask<int>(1);
            case "IsMounted":
                context.Return(player.IsMounted);
                return new ValueTask<int>(1);
            case "IsFlying":
                context.Return(player.IsFlying);
                return new ValueTask<int>(1);
            case "IsHuman":
                context.Return(player.IsHuman);
                return new ValueTask<int>(1);
            case "IsGargoyle":
                context.Return(player.IsGargoyle);
                return new ValueTask<int>(1);
            case "IsYellowHits":
                context.Return(player.IsYellowHits);
                return new ValueTask<int>(1);
            case "IsRenamable":
                context.Return(player.IsRenamable);
                return new ValueTask<int>(1);
            case "IsFemale":
                context.Return(player.IsFemale);
                return new ValueTask<int>(1);
            case "NotorietyFlag":
                context.Return(((Enums.Notoriety)(byte) player.NotorietyFlag).ToString());
                return new ValueTask<int>(1);
            case "Race":
                context.Return(player.Race.ToString());
                return new ValueTask<int>(1);
            case "Title":
                context.Return(player.Title ?? "");
                return new ValueTask<int>(1);
            case "SpeedMode":
                context.Return("Normal");
                return new ValueTask<int>(1);

            // Entity properties
            case "Name":
                context.Return(player.Name ?? "");
                return new ValueTask<int>(1);
            case "Serial":
                context.Return((double)(uint)player.Serial);
                return new ValueTask<int>(1);
            case "Distance":
                context.Return((double)player.Distance);
                return new ValueTask<int>(1);
            case "Direction":
                context.Return(((Direction) player.Direction).ToString());
                return new ValueTask<int>(1);
            case "IsDestroyed":
                context.Return(player.IsDestroyed);
                return new ValueTask<int>(1);

            // GameObject properties
            case "X":
                context.Return((double)player.X);
                return new ValueTask<int>(1);
            case "Y":
                context.Return((double)player.Y);
                return new ValueTask<int>(1);
            case "Z":
                context.Return((double)player.Z);
                return new ValueTask<int>(1);
            case "Hue":
                context.Return((double)player.Hue);
                return new ValueTask<int>(1);
            case "Graphic":
                context.Return((double)player.Graphic);
                return new ValueTask<int>(1);

            default:
                context.Return(LuaValue.Nil);
                return new ValueTask<int>(1);
        }
    }

    private static bool IsAllowedEntity(UOEntity entity)
    {
        if (entity is Item item)
            return AssistantData.ScriptingRestrictions.IsItemTypeAllowed(item.Graphic);
        return true;
    }

    private static ValueTask<int> Say(LuaFunctionExecutionContext context, CancellationToken ct)
    {
        try
        {
            var message = context.GetArgument<string>(0);
            var argCount = context.ArgumentCount;

            switch (argCount)
            {
                case 1:
                    GameActions.Say(message);
                    break;
                case 2:
                    GameActions.Say(message, (ushort)context.GetArgument<double>(1));
                    break;
                case 3:
                    GameActions.Say(message, (ushort)context.GetArgument<double>(1), (MessageType)(int)context.GetArgument<double>(2));
                    break;
                case 4:
                    GameActions.Say(message, (ushort)context.GetArgument<double>(1), (MessageType)(int)context.GetArgument<double>(2), (byte)context.GetArgument<double>(3));
                    break;
                default:
                    context.Return(false);
                    return new ValueTask<int>(1);
            }

            context.Return(true);
        }
        catch (Exception e)
        {
            Message.Warning($"Error in Say: {e.Message}");
            context.Return(false);
        }

        return new ValueTask<int>(1);
    }

    private static ValueTask<int> SayParty(LuaFunctionExecutionContext context, CancellationToken ct)
    {
        try
        {
            var message = context.GetArgument<string>(0);
            GameActions.SayParty(message, 0);
            context.Return(true);
        }
        catch (Exception e)
        {
            Message.Warning($"Error in SayParty: {e.Message}");
            context.Return(false);
        }

        return new ValueTask<int>(1);
    }

    private static ValueTask<int> SayGuild(LuaFunctionExecutionContext context, CancellationToken ct)
    {
        try
        {
            var message = context.GetArgument<string>(0);
            GameActions.Say(message, (ushort)MessageHue.Guild, MessageType.Guild);
            context.Return(true);
        }
        catch (Exception e)
        {
            Message.Warning($"Error in SayGuild: {e.Message}");
            context.Return(false);
        }

        return new ValueTask<int>(1);
    }

    private static ValueTask<int> SayAlliance(LuaFunctionExecutionContext context, CancellationToken ct)
    {
        try
        {
            var message = context.GetArgument<string>(0);
            GameActions.Say(message, (ushort)MessageHue.Ally, MessageType.Alliance);
            context.Return(true);
        }
        catch (Exception e)
        {
            Message.Warning($"Error in SayAlliance: {e.Message}");
            context.Return(false);
        }

        return new ValueTask<int>(1);
    }

    private static ValueTask<int> SayWhisper(LuaFunctionExecutionContext context, CancellationToken ct)
    {
        try
        {
            var message = context.GetArgument<string>(0);
            GameActions.Say(message, (ushort)MessageHue.Whisper, MessageType.Whisper);
            context.Return(true);
        }
        catch (Exception e)
        {
            Message.Warning($"Error in SayWhisper: {e.Message}");
            context.Return(false);
        }

        return new ValueTask<int>(1);
    }

    private static ValueTask<int> SayYell(LuaFunctionExecutionContext context, CancellationToken ct)
    {
        try
        {
            var message = context.GetArgument<string>(0);
            GameActions.Say(message, (ushort)MessageHue.Yell, MessageType.Yell);
            context.Return(true);
        }
        catch (Exception e)
        {
            Message.Warning($"Error in SayYell: {e.Message}");
            context.Return(false);
        }

        return new ValueTask<int>(1);
    }

    private static ValueTask<int> SayChat(LuaFunctionExecutionContext context, CancellationToken ct)
    {
        try
        {
            var message = context.GetArgument<string>(0);
            GameActions.Say(message, (ushort)MessageHue.Chat, MessageType.Regular);
            context.Return(true);
        }
        catch (Exception e)
        {
            Message.Warning($"Error in SayChat: {e.Message}");
            context.Return(false);
        }

        return new ValueTask<int>(1);
    }

    private static ValueTask<int> SayEmote(LuaFunctionExecutionContext context, CancellationToken ct)
    {
        try
        {
            var message = context.GetArgument<string>(0);
            GameActions.Say(message, (ushort)MessageHue.Emote, MessageType.Emote);
            context.Return(true);
        }
        catch (Exception e)
        {
            Message.Warning($"Error in SayEmote: {e.Message}");
            context.Return(false);
        }

        return new ValueTask<int>(1);
    }

    private static ValueTask<int> UseObject(LuaFunctionExecutionContext context, CancellationToken ct)
    {
        try
        {
            var serial = (uint)context.GetArgument<double>(0);
            var entity = World.Get(serial);

            if (entity == null)
            {
                Message.Warning("UseObject: Invalid serial");
                context.Return(false);
                return new ValueTask<int>(1);
            }

            if (!IsAllowedEntity(entity))
            {
                Message.Warning("UseObject: This action is not supported by the script engine");
                context.Return(false);
                return new ValueTask<int>(1);
            }

            GameActions.DoubleClick(serial);
            context.Return(true);
        }
        catch (Exception e)
        {
            Message.Warning($"Error in UseObject: {e.Message}");
            context.Return(false);
        }

        return new ValueTask<int>(1);
    }

    private static ValueTask<int> UseObjectByType(LuaFunctionExecutionContext context, CancellationToken ct)
    {
        try
        {
            var type = (ushort)context.GetArgument<double>(0);

            if (!AssistantData.ScriptingRestrictions.IsItemTypeAllowed(type))
            {
                Message.Warning("UseObjectByType: This action is not supported by the script engine");
                context.Return(false);
                return new ValueTask<int>(1);
            }

            var player = World.Player;
            Item item =
                (player.GetItemOnLayer(Layer.RightHand) is Item oneHanded && oneHanded.Graphic == type) ? oneHanded :
                (player.GetItemOnLayer(Layer.LeftHand) is Item twoHanded && twoHanded.Graphic == type) ? twoHanded :
                player.FindItemByGraphic(type) ??
                World.Items.Values.FirstOrDefault(m => m.Graphic == type && m.Distance <= 2 && !m.IsDestroyed && !m.IsMulti);

            if (item != null)
            {
                GameActions.DoubleClick(item.Serial);
                context.Return(true);
            }
            else
            {
                Message.Warning("UseObjectByType: Item not found");
                context.Return(false);
            }
        }
        catch (Exception e)
        {
            Message.Warning($"Error in UseObjectByType: {e.Message}");
            context.Return(false);
        }

        return new ValueTask<int>(1);
    }

    private static ValueTask<int> ClickObject(LuaFunctionExecutionContext context, CancellationToken ct)
    {
        try
        {
            var serial = (uint)context.GetArgument<double>(0);

            if (World.Get(serial) != null)
            {
                GameActions.SingleClick(serial);
                context.Return(true);
            }
            else
            {
                Message.Warning("ClickObject: Invalid serial");
                context.Return(false);
            }
        }
        catch (Exception e)
        {
            Message.Warning($"Error in ClickObject: {e.Message}");
            context.Return(false);
        }

        return new ValueTask<int>(1);
    }

    private static ValueTask<int> Attack(LuaFunctionExecutionContext context, CancellationToken ct)
    {
        try
        {
            var serial = (uint)context.GetArgument<double>(0);

            if (((Serial) serial).IsMobile)
            {
                GameActions.Attack(serial);
                context.Return(true);
            }
            else
            {
                Message.Warning("Attack: Invalid serial");
                context.Return(false);
            }
        }
        catch (Exception e)
        {
            Message.Warning($"Error in Attack: {e.Message}");
            context.Return(false);
        }

        return new ValueTask<int>(1);
    }

    private static async ValueTask<int> ClearHands(LuaFunctionExecutionContext context, CancellationToken ct)
    {
        try
        {
            var command = context.GetArgument<string>(0);

            if (string.IsNullOrEmpty(command))
            {
                Message.Warning("ClearHands: Invalid argument");
                context.Return(false);
                return 1;
            }

            var player = World.Player;
            Item backpack = player.GetItemOnLayer(Layer.Backpack);

            if (backpack == null)
            {
                context.Return(false);
                return 1;
            }

            Item oneHandedWeapon = player.GetItemOnLayer(Layer.RightHand);
            Item twoHandedWeapon = player.GetItemOnLayer(Layer.LeftHand);
            switch (command)
            {
                case "left":
                    if (oneHandedWeapon != null)
                    {
                        if (ItemPickupFilter.IsItemBlocked(oneHandedWeapon.Serial))
                        {
                            Message.Warning($"Cannot pick up this item type (0x{oneHandedWeapon.Graphic:X4}) - it is blocked from assistant pickup.");
                            context.Return(false);
                            return 1;
                        }
                        GameActions.PickUp(oneHandedWeapon, 0, 0, 1);
                        GameActions.DropItem((uint) oneHandedWeapon.Serial, 0xFFFF, 0xFFFF, 0, (uint) backpack.Serial);
                        StaticValues.LastEquippedOneHanded = oneHandedWeapon;
                    }
                    break;

                case "right":
                    if (twoHandedWeapon != null)
                    {
                        if (ItemPickupFilter.IsItemBlocked(twoHandedWeapon.Serial))
                        {
                            Message.Warning($"Cannot pick up this item type (0x{twoHandedWeapon.Graphic:X4}) - it is blocked from assistant pickup.");
                            context.Return(false);
                            return 1;
                        }
                        GameActions.PickUp(twoHandedWeapon, 0, 0, 1);
                        GameActions.DropItem((uint) twoHandedWeapon.Serial, 0xFFFF, 0xFFFF, 0, (uint) backpack.Serial);
                        StaticValues.LastEquippedTwoHanded = twoHandedWeapon;
                    }
                    break;

                case "both":
                    if (oneHandedWeapon != null)
                    {
                        if (ItemPickupFilter.IsItemBlocked(oneHandedWeapon.Serial))
                        {
                            Message.Warning($"Cannot pick up this item type (0x{oneHandedWeapon.Graphic:X4}) - it is blocked from assistant pickup.");
                            context.Return(false);
                            return 1;
                        }
                        GameActions.PickUp(oneHandedWeapon, 0, 0, 1);
                        GameActions.DropItem((uint) oneHandedWeapon.Serial, 0xFFFF, 0xFFFF, 0, (uint) backpack.Serial);
                        StaticValues.LastEquippedOneHanded = oneHandedWeapon;
                    }

                    if (oneHandedWeapon != null && twoHandedWeapon != null)
                        await Task.Delay(600, ct);

                    if (twoHandedWeapon != null)
                    {
                        if (ItemPickupFilter.IsItemBlocked(twoHandedWeapon.Serial))
                        {
                            Message.Warning($"Cannot pick up this item type (0x{twoHandedWeapon.Graphic:X4}) - it is blocked from assistant pickup.");
                            context.Return(false);
                            return 1;
                        }
                        GameActions.PickUp(twoHandedWeapon, 0, 0, 1);
                        GameActions.DropItem((uint) twoHandedWeapon.Serial, 0xFFFF, 0xFFFF, 0, (uint) backpack.Serial);
                        StaticValues.LastEquippedTwoHanded = twoHandedWeapon;
                    }
                    break;

                default:
                    Message.Warning("ClearHands: Invalid argument");
                    context.Return(false);
                    return 1;
            }

            context.Return(true);
        }
        catch (Exception e)
        {
            Message.Warning($"Error in ClearHands: {e.Message}");
            context.Return(false);
        }

        return 1;
    }

    private static ValueTask<int> Turn(LuaFunctionExecutionContext context, CancellationToken ct)
    {
        try
        {
            var direction = context.GetArgument<string>(0);

            if (string.IsNullOrEmpty(direction))
            {
                Message.Warning("Turn: Invalid argument");
                context.Return(false);
                return new ValueTask<int>(1);
            }

            var player = World.Player;
            int delayInMilliseconds = player.IsMounted ? 200 : 400;

            if (StaticValues.LastWalk + TimeSpan.FromMilliseconds(delayInMilliseconds) >= DateTime.UtcNow)
            {
                context.Return(false);
                return new ValueTask<int>(1);
            }

            StaticValues.LastWalk = DateTime.UtcNow;
            Direction dir = (Direction)Enum.Parse(typeof(Direction), direction, true);

            if (player.Direction != dir)
            {
                player.Walk(dir, false);
            }

            context.Return(true);
        }
        catch (Exception e)
        {
            Message.Warning($"Error in Turn: {e.Message}");
            context.Return(false);
        }

        return new ValueTask<int>(1);
    }

    private static ValueTask<int> Equip(LuaFunctionExecutionContext context, CancellationToken ct)
    {
        try
        {
            var serial = (uint)context.GetArgument<double>(0);
            if (World.Get(serial) != null)
            {
                if (ItemPickupFilter.IsItemBlocked(serial))
                {
                    if (World.Items.TryGetValue(serial, out var item) && item != null)
                    {
                        Message.Warning($"Cannot pick up this item type (0x{item.Graphic:X4}) - it is blocked from assistant pickup.");
                    }
                    context.Return(false);
                    return new ValueTask<int>(1);
                }
                GameActions.PickUp(serial, 0, 0, 1);
                GameActions.Equip();
                context.Return(true);
            }
            else
            {
                Message.Warning("Equip: Invalid serial");
                context.Return(false);
            }
        }
        catch (Exception e)
        {
            Message.Warning($"Error in Equip: {e.Message}");
            context.Return(false);
        }

        return new ValueTask<int>(1);
    }

    private static ValueTask<int> PickUp(LuaFunctionExecutionContext context, CancellationToken ct)
    {
        try
        {
            var serial = (uint)context.GetArgument<double>(0);
            if (World.Get(serial) != null)
            {
                if (ItemPickupFilter.IsItemBlocked(serial))
                {
                    if (World.Items.TryGetValue(serial, out var item) && item != null)
                    {
                        Message.Warning($"Cannot pick up this item type (0x{item.Graphic:X4}) - it is blocked from assistant pickup.");
                    }
                    context.Return(false);
                    return new ValueTask<int>(1);
                }

                var amount = context.ArgumentCount > 1 ? (int)context.GetArgument<double>(1) : 1;
                GameActions.PickUp(serial, 0, 0, amount);
                context.Return(true);
            }
            else
            {
                Message.Warning("PickUp: Invalid serial");
                context.Return(false);
            }
        }
        catch (Exception e)
        {
            Message.Warning($"Error in PickUp: {e.Message}");
            context.Return(false);
        }

        return new ValueTask<int>(1);
    }

    private static ValueTask<int> DropInBackpack(LuaFunctionExecutionContext context, CancellationToken ct)
    {
        try
        {
            Item backpack = World.Player.GetItemOnLayer(Layer.Backpack);

            if (backpack == null)
            {
                Message.Warning("DropInBackpack: Backpack not found");
                context.Return(false);
                return new ValueTask<int>(1);
            }

            // Gehalten ODER noch in der Lift-Queue (der Lift laeuft asynchron
            // ueber den DragDropManager — Client-ItemHold ist synchron).
            Item itemHold = GameActions.HeldOrQueued;

            if (itemHold != null)
            {
                GameActions.DropItem(itemHold.Serial, 0xFFFF, 0xFFFF, 0, backpack.Serial);
                context.Return(true);
            }
            else
            {
                Message.Warning("DropInBackpack: Not holding an item");
                context.Return(false);
            }
        }
        catch (Exception e)
        {
            Message.Warning($"Error in DropInBackpack: {e.Message}");
            context.Return(false);
        }

        return new ValueTask<int>(1);
    }

    private static ValueTask<int> DropInContainer(LuaFunctionExecutionContext context, CancellationToken ct)
    {
        try
        {
            var serial = (uint)context.GetArgument<double>(0);
            Item playerBackpack = World.Player.GetItemOnLayer(Layer.Backpack);
            if (playerBackpack == null)
            {
                Message.Warning("DropInContainer: Player backpack not found");
                context.Return(false);
                return new ValueTask<int>(1);
            }

            var backpackContainer = FindInContainerRecursive(playerBackpack, serial);
            Item container = World.Items.Values.FirstOrDefault(m => m.Serial == serial && m.Distance <= 2 && !m.IsDestroyed && !m.IsMulti);

            Item mobileBackpack = null;
            if (container == null && backpackContainer == null)
            {
                Mobile mobile = World.Mobiles.Values.FirstOrDefault(m => m.Serial == serial && m.Distance <= 2 && !m.IsDestroyed);
                if (mobile != null)
                {
                    mobileBackpack = mobile.GetItemOnLayer(Layer.Backpack);
                }
            }

            uint targetSerial;
            if (backpackContainer != null)
                targetSerial = backpackContainer.Serial;
            else if (container != null)
                targetSerial = container.Serial;
            else if (mobileBackpack != null)
                targetSerial = mobileBackpack.Serial;
            else
            {
                Message.Warning("DropInContainer: Container not found");
                context.Return(false);
                return new ValueTask<int>(1);
            }

            Item itemHold = GameActions.HeldOrQueued;

            if (itemHold != null)
            {
                GameActions.DropItem(itemHold.Serial, 0xFFFF, 0xFFFF, 0, targetSerial);
                context.Return(true);
            }
            else
            {
                Message.Warning("DropInContainer: Not holding an item");
                context.Return(false);
            }
        }
        catch (Exception e)
        {
            Message.Warning($"Error in DropInContainer: {e.Message}");
            context.Return(false);
        }

        return new ValueTask<int>(1);
    }

    private static ValueTask<int> DropOnGround(LuaFunctionExecutionContext context, CancellationToken ct)
    {
        try
        {
            Item item = GameActions.HeldOrQueued;
            if (item != null)
            {
                var player = World.Player;
                GameActions.DropItem(item.Serial, player.X, player.Y, player.Z, 0xFFFF_FFFF);
                context.Return(true);
            }
            else
            {
                Message.Warning("DropOnGround: Not holding an item");
                context.Return(false);
            }
        }
        catch (Exception e)
        {
            Message.Warning($"Error in DropOnGround: {e.Message}");
            context.Return(false);
        }

        return new ValueTask<int>(1);
    }

    private static ValueTask<int> ToggleWarMode(LuaFunctionExecutionContext context, CancellationToken ct)
    {
        try
        {
            GameActions.ToggleWarMode(World.Player);
            context.Return(true);
        }
        catch (Exception e)
        {
            Message.Warning($"Error in ToggleWarMode: {e.Message}");
            context.Return(false);
        }

        return new ValueTask<int>(1);
    }

    private static ValueTask<int> PopPouch(LuaFunctionExecutionContext context, CancellationToken ct)
    {
        try
        {
            if (World.Player == null || World.Player.IsDead)
            {
                Message.Warning("PopPouch: Player is dead or not found");
                context.Return(false);
                return new ValueTask<int>(1);
            }

            Item pouch = World.Player.FindItems(0x0E79, 0x0025).FirstOrDefault().Value;
            if (pouch != null)
            {
                GameActions.DoubleClick(pouch.Serial);
                context.Return(true);
            }
            else
            {
                Message.Warning("PopPouch: No trapped pouch found in backpack");
                context.Return(false);
            }
        }
        catch (Exception e)
        {
            Message.Warning($"Error in PopPouch: {e.Message}");
            context.Return(false);
        }

        return new ValueTask<int>(1);
    }
    /// <summary>Razor-Ersatz fuer Client Item.GetItemBySerial(serial, recurse).</summary>
    private static Item FindInContainerRecursive(Item container, uint serial)
    {
        foreach (Item child in container.Contains)
        {
            if ((uint) child.Serial == serial)
                return child;

            Item nested = FindInContainerRecursive(child, serial);
            if (nested != null)
                return nested;
        }

        return null;
    }
}