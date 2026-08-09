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
using System.Threading;
using System.Threading.Tasks;
using Assistant.VScripts.Core;
using Lua;

namespace Assistant.LuaEngine.API;

/// <summary>
/// Items API for Lua-CSharp engine
/// </summary>
public static class LuaItemsAPI
{
    public static void Register(LuaState state)
    {
        var items = new LuaTable();

        // Functions
        items["FindBySerial"] = new LuaFunction("FindBySerial", FindBySerial);
        items["FindByType"] = new LuaFunction("FindByType", FindByType);
        items["FindByName"] = new LuaFunction("FindByName", FindByName);
        items["FindByLayer"] = new LuaFunction("FindByLayer", FindByLayer);
        items["FindByFilter"] = new LuaFunction("FindByFilter", FindByFilter);
        items["FindInContainer"] = new LuaFunction("FindInContainer", FindInContainer);
        items["GetContainerItems"] = new LuaFunction("GetContainerItems", GetContainerItems);
        items["CountType"] = new LuaFunction("CountType", CountType);
        items["CountTypeInContainer"] = new LuaFunction("CountTypeInContainer", CountTypeInContainer);

        // Metatable for property access
        var metatable = new LuaTable();
        metatable["__index"] = new LuaFunction("ItemsIndex", ItemsIndex);
        items.Metatable = metatable;

        state.Environment["Items"] = items;
    }

    private static ValueTask<int> ItemsIndex(LuaFunctionExecutionContext context, CancellationToken ct)
    {
        context.Return(LuaValue.Nil);
        return new ValueTask<int>(1);
    }

    private static ValueTask<int> FindBySerial(LuaFunctionExecutionContext context, CancellationToken ct)
    {
        try
        {
            var serial = (uint)context.GetArgument<double>(0);
            if (!World.Items.TryGetValue(serial, out var item) || item == null)
            {
                context.Return(LuaValue.Nil);
                return new ValueTask<int>(1);
            }

            // Check if item type is searchable
            if (!AssistantData.ScriptingRestrictions.IsItemSearchable(item.Graphic))
            {
                context.Return(LuaValue.Nil);
                return new ValueTask<int>(1);
            }

            context.Return(CreateItemTable(item));
        }
        catch (Exception e)
        {
            Message.Warning($"Error in FindBySerial: {e.Message}");
            context.Return(LuaValue.Nil);
        }

        return new ValueTask<int>(1);
    }

    private static ValueTask<int> FindByType(LuaFunctionExecutionContext context, CancellationToken ct)
    {
        try
        {
            var graphic = (ushort)context.GetArgument<double>(0);

            // Check if item type is searchable
            if (!AssistantData.ScriptingRestrictions.IsItemSearchable(graphic))
            {
                context.Return(LuaValue.Nil);
                return new ValueTask<int>(1);
            }

            var player = World.Player;
            Item item = player.FindItemByGraphic(graphic) ??
                        World.Items.Values.FirstOrDefault(m => m.Graphic == graphic && !m.IsDestroyed);

            if (item == null)
            {
                context.Return(LuaValue.Nil);
                return new ValueTask<int>(1);
            }

            context.Return(CreateItemTable(item));
        }
        catch (Exception e)
        {
            Message.Warning($"Error in FindByType: {e.Message}");
            context.Return(LuaValue.Nil);
        }

        return new ValueTask<int>(1);
    }

    private static ValueTask<int> FindByName(LuaFunctionExecutionContext context, CancellationToken ct)
    {
        try
        {
            var name = context.GetArgument<string>(0);

            var item = World.Items.Values
                .FirstOrDefault(m => m.Name == name && !m.IsDestroyed &&
                    AssistantData.ScriptingRestrictions.IsItemSearchable(m.Graphic));

            if (item == null)
            {
                context.Return(LuaValue.Nil);
                return new ValueTask<int>(1);
            }

            context.Return(CreateItemTable(item));
        }
        catch (Exception e)
        {
            Message.Warning($"Error in FindByName: {e.Message}");
            context.Return(LuaValue.Nil);
        }

        return new ValueTask<int>(1);
    }

    private static ValueTask<int> FindByLayer(LuaFunctionExecutionContext context, CancellationToken ct)
    {
        try
        {
            var layer = (Layer)(int)context.GetArgument<double>(0);
            var player = World.Player;
            var item = player.GetItemOnLayer(layer);

            if (item == null || !AssistantData.ScriptingRestrictions.IsItemSearchable(item.Graphic))
            {
                context.Return(LuaValue.Nil);
                return new ValueTask<int>(1);
            }

            context.Return(CreateItemTable(item));
        }
        catch (Exception e)
        {
            Message.Warning($"Error in FindByLayer: {e.Message}");
            context.Return(LuaValue.Nil);
        }

        return new ValueTask<int>(1);
    }

    private static ValueTask<int> FindByFilter(LuaFunctionExecutionContext context, CancellationToken ct)
    {
        try
        {
            var filterTable = context.GetArgument<LuaTable>(0);

            // Extract filter parameters (support both old and new parameter names)
            ushort? graphic = GetTableNumber<ushort>(filterTable, "graphic");
            ushort? hue = GetTableNumber<ushort>(filterTable, "hue");
            int? rangeMin = GetTableNumber<int>(filterTable, "rangemin");
            int? rangeMax = GetTableNumber<int>(filterTable, "rangemax");
            uint? containerSerial = GetTableNumber<uint>(filterTable, "container");
            bool? onGround = GetTableBoolean(filterTable, "onground");

            // Old API parameters
            bool? isCorpse = GetTableBoolean(filterTable, "corpse");
            bool? isContainer = GetTableBoolean(filterTable, "iscontainer");
            bool? isMovable = GetTableBoolean(filterTable, "movable");
            string name = GetTableString(filterTable, "name");

            // graphics/hues/layers can be either a single number or a table of numbers
            List<ushort> graphics = GetTableNumberOrList<ushort>(filterTable, "graphics");
            List<ushort> hues = GetTableNumberOrList<ushort>(filterTable, "hues");
            List<int> layers = GetTableNumberOrList<int>(filterTable, "layers");

            var results = new LuaTable();
            int index = 1;

            foreach (var item in World.Items.Values)
            {
                if (item.IsDestroyed)
                    continue;

                // Check if item type is searchable
                if (!AssistantData.ScriptingRestrictions.IsItemSearchable(item.Graphic))
                    continue;

                // Single graphic filter (using "graphic" key)
                if (graphic.HasValue && item.Graphic != graphic.Value)
                    continue;

                // Multiple graphics filter (using "graphics" key - can be number or table)
                if (graphics != null && graphics.Count > 0 && !graphics.Contains(item.Graphic))
                    continue;

                // Single hue filter (using "hue" key)
                if (hue.HasValue && item.Hue != hue.Value)
                    continue;

                // Multiple hues filter (using "hues" key - can be number or table)
                if (hues != null && hues.Count > 0 && !hues.Contains(item.Hue))
                    continue;

                if (rangeMin.HasValue && item.Distance < rangeMin.Value)
                    continue;
                if (rangeMax.HasValue && item.Distance > rangeMax.Value)
                    continue;
                if (containerSerial.HasValue && item.ContainerSerial != containerSerial.Value)
                    continue;
                if (onGround.HasValue && item.OnGround != onGround.Value)
                    continue;

                // Old API filters
                if (isCorpse.HasValue && item.IsCorpse != isCorpse.Value)
                    continue;
                if (isContainer.HasValue && item.ItemData.IsContainer != isContainer.Value)
                    continue;
                if (isMovable.HasValue && item.IsLocked == isMovable.Value) // IsLocked is opposite of movable
                    continue;
                if (name != null && (item.Name == null || !item.Name.Contains(name, StringComparison.OrdinalIgnoreCase)))
                    continue;
                if (layers != null && layers.Count > 0 && !layers.Contains((int)item.Layer))
                    continue;

                results[index++] = CreateItemTable(item);
            }

            context.Return(results);
        }
        catch (Exception e)
        {
            Message.Warning($"Error in FindByFilter: {e.Message}");
            context.Return(new LuaTable());
        }

        return new ValueTask<int>(1);
    }

    private static ValueTask<int> FindInContainer(LuaFunctionExecutionContext context, CancellationToken ct)
    {
        try
        {
            var containerSerial = (uint)context.GetArgument<double>(0);
            ushort graphic = context.ArgumentCount > 1 ? (ushort)context.GetArgument<double>(1) : (ushort)0;
            ushort hue = context.ArgumentCount > 2 ? (ushort)context.GetArgument<double>(2) : (ushort)0xFFFF;

            // Try to find as item first
            Item container = null;
            World.Items.TryGetValue(containerSerial, out container);

            if (container == null)
            {
                // Try to find as mobile (get backpack)
                if (World.Mobiles.TryGetValue(containerSerial, out var mobile) && mobile != null)
                {
                    container = mobile.GetItemOnLayer(Layer.Backpack);
                    if (container != null)
                        containerSerial = container.Serial;
                }
            }

            if (container == null || !container.ItemData.IsContainer)
            {
                context.Return(LuaValue.Nil);
                return new ValueTask<int>(1);
            }

            var results = new LuaTable();
            int index = 1;

            foreach (var item in World.Items.Values)
            {
                if (item.ContainerSerial != containerSerial || item.IsDestroyed)
                    continue;

                if (!AssistantData.ScriptingRestrictions.IsItemSearchable(item.Graphic))
                    continue;

                if (graphic != 0 && item.Graphic != graphic)
                    continue;

                if (hue != 0xFFFF && item.Hue != hue)
                    continue;

                results[index++] = CreateItemTable(item);
            }

            context.Return(results);
        }
        catch (Exception e)
        {
            Message.Warning($"Error in FindInContainer: {e.Message}");
            context.Return(LuaValue.Nil);
        }

        return new ValueTask<int>(1);
    }

    private static ValueTask<int> GetContainerItems(LuaFunctionExecutionContext context, CancellationToken ct)
    {
        try
        {
            var containerSerial = (uint)context.GetArgument<double>(0);

            // Try to find as item first
            Item container = null;
            World.Items.TryGetValue(containerSerial, out container);

            if (container == null)
            {
                // Try to find as mobile (get backpack)
                if (World.Mobiles.TryGetValue(containerSerial, out var mobile) && mobile != null)
                {
                    container = mobile.GetItemOnLayer(Layer.Backpack);
                    if (container != null)
                        containerSerial = container.Serial;
                }
            }

            if (container == null || !container.ItemData.IsContainer)
            {
                context.Return(LuaValue.Nil);
                return new ValueTask<int>(1);
            }

            var results = new LuaTable();
            int index = 1;

            foreach (var item in World.Items.Values)
            {
                if (item.ContainerSerial != containerSerial || item.IsDestroyed)
                    continue;

                if (!AssistantData.ScriptingRestrictions.IsItemSearchable(item.Graphic))
                    continue;

                results[index++] = CreateItemTable(item);
            }

            context.Return(results);
        }
        catch (Exception e)
        {
            Message.Warning($"Error in GetContainerItems: {e.Message}");
            context.Return(LuaValue.Nil);
        }

        return new ValueTask<int>(1);
    }

    private static ValueTask<int> CountType(LuaFunctionExecutionContext context, CancellationToken ct)
    {
        try
        {
            var graphic = (ushort)context.GetArgument<double>(0);
            ushort? hue = context.ArgumentCount > 1 ? (ushort)context.GetArgument<double>(1) : null;

            // Check if item type is searchable
            if (!AssistantData.ScriptingRestrictions.IsItemSearchable(graphic))
            {
                context.Return(0.0);
                return new ValueTask<int>(1);
            }

            var player = World.Player;
            var backpack = player.GetItemOnLayer(Layer.Backpack);

            if (backpack == null)
            {
                context.Return(0.0);
                return new ValueTask<int>(1);
            }

            int count = CountItemsRecursive(backpack, graphic, hue);
            context.Return((double)count);
        }
        catch (Exception e)
        {
            Message.Warning($"Error in CountType: {e.Message}");
            context.Return(0.0);
        }

        return new ValueTask<int>(1);
    }

    private static ValueTask<int> CountTypeInContainer(LuaFunctionExecutionContext context, CancellationToken ct)
    {
        try
        {
            var containerSerial = (uint)context.GetArgument<double>(0);
            var graphic = (ushort)context.GetArgument<double>(1);
            ushort? hue = context.ArgumentCount > 2 ? (ushort)context.GetArgument<double>(2) : null;

            // Check if item type is searchable
            if (!AssistantData.ScriptingRestrictions.IsItemSearchable(graphic))
            {
                context.Return(0.0);
                return new ValueTask<int>(1);
            }

            if (!World.Items.TryGetValue(containerSerial, out var container) || container == null)
            {
                context.Return(0.0);
                return new ValueTask<int>(1);
            }

            int count = CountItemsRecursive(container, graphic, hue);
            context.Return((double)count);
        }
        catch (Exception e)
        {
            Message.Warning($"Error in CountTypeInContainer: {e.Message}");
            context.Return(0.0);
        }

        return new ValueTask<int>(1);
    }

    private static int CountItemsRecursive(Item container, ushort graphic, ushort? hue)
    {
        int count = 0;

        // Razor: Item.Contains (Liste) statt der Client-LinkedObject-Kette.
        foreach (Item item in container.Contains)
        {
            if (item.Graphic == graphic && (!hue.HasValue || item.Hue == hue.Value))
            {
                count += item.Amount;
            }

            // Recurse into sub-containers
            if (!item.IsEmpty)
            {
                count += CountItemsRecursive(item, graphic, hue);
            }
        }

        return count;
    }

    /// <summary>
    /// Create a Lua table representing an Item
    /// </summary>
    internal static LuaTable CreateItemTable(Item item)
    {
        var table = new LuaTable();

        // Entity properties
        table["Serial"] = (double)(uint)item.Serial;
        table["Name"] = item.Name ?? "";
        table["Distance"] = (double)item.Distance;
        table["Direction"] = ((Direction) item.Direction).ToString() ?? "";
        table["IsDestroyed"] = item.IsDestroyed;

        // GameObject properties
        table["X"] = (double)item.X;
        table["Y"] = (double)item.Y;
        table["Z"] = (double)item.Z;
        table["Hue"] = (double)item.Hue;
        table["Graphic"] = (double)item.Graphic;

        // Item specific properties
        table["Amount"] = (double)item.Amount;
        table["IsDamageable"] = item.IsDamageable;
        table["IsCoin"] = item.IsCoin;
        table["IsCorpse"] = item.IsCorpse;
        table["IsEmpty"] = item.IsEmpty;
        table["IsLocked"] = item.IsLocked;
        table["IsHidden"] = item.IsHidden;
        table["IsMulti"] = item.IsMulti;
        table["IsLootable"] = item.IsLootable;
        table["OnGround"] = item.OnGround;
        table["DisplayedGraphic"] = (double)item.DisplayedGraphic;
        table["MultiGraphic"] = (double)item.MultiGraphic;
        table["Layer"] = Enum.GetName(item.Layer) ?? "";
        table["LightID"] = (double)item.LightID;
        table["Price"] = (double)item.Price;
        table["RootContainer"] = (double)item.RootContainerSerial;
        table["Container"] = (double)item.ContainerSerial;

        // Razor-Zusatz (nicht im Client): Tiledata-Klassifikation ueber den
        // DataService — erspart Loot-Scripts riesige Graphic-Listen
        // (siehe LootOrganizer-Demo). TileLayer = Ausruestungs-Layer laut
        // Tiledata (auch fuer Items, die in einem Container liegen).
        TileItemData tiledata = item.ItemData;
        table["IsWeapon"] = tiledata.IsWeapon;
        table["IsArmor"] = tiledata.IsArmor;
        table["IsWearable"] = tiledata.IsWearable;
        table["IsContainer"] = tiledata.IsContainer;
        table["TileLayer"] = tiledata.Layer.ToString();

        // Properties string
        if (World.OPL.TryGetNameAndData(item.Serial, out string oplName, out string data))
        {
            if (!string.IsNullOrEmpty(oplName) && !string.IsNullOrEmpty(data))
            {
                table["Properties"] = $"{oplName}\n{data}";
            }
            else
            {
                table["Properties"] = LuaValue.Nil;
            }
        }
        else
        {
            table["Properties"] = LuaValue.Nil;
        }

        return table;
    }

    // Helper methods for table access
    private static T? GetTableNumber<T>(LuaTable table, string key) where T : struct
    {
        var value = table[key];
        if (value.Type == LuaValueType.Number)
        {
            var num = value.Read<double>();
            return (T)Convert.ChangeType(num, typeof(T));
        }
        return null;
    }

    private static bool? GetTableBoolean(LuaTable table, string key)
    {
        var value = table[key];
        if (value.Type == LuaValueType.Boolean)
        {
            return value.Read<bool>();
        }
        return null;
    }

    private static string GetTableString(LuaTable table, string key)
    {
        var value = table[key];
        if (value.Type == LuaValueType.String)
        {
            return value.Read<string>();
        }
        return null;
    }

    private static List<T> GetTableList<T>(LuaTable table, string key) where T : struct
    {
        var value = table[key];
        if (value.Type != LuaValueType.Table)
            return null;

        var luaTable = value.Read<LuaTable>();
        var result = new List<T>();

        // Iterate through numeric indices
        int index = 1;
        while (true)
        {
            var item = luaTable[index];
            if (item.Type == LuaValueType.Nil)
                break;

            if (item.Type == LuaValueType.Number)
            {
                var num = item.Read<double>();
                result.Add((T)Convert.ChangeType(num, typeof(T)));
            }
            index++;
        }

        return result.Count > 0 ? result : null;
    }

    /// <summary>
    /// Get a value that can be either a single number or a table of numbers.
    /// Returns a List in either case for uniform handling.
    /// </summary>
    private static List<T> GetTableNumberOrList<T>(LuaTable table, string key) where T : struct
    {
        var value = table[key];

        // Handle single number
        if (value.Type == LuaValueType.Number)
        {
            var num = value.Read<double>();
            return new List<T> { (T)Convert.ChangeType(num, typeof(T)) };
        }

        // Handle table of numbers
        if (value.Type == LuaValueType.Table)
        {
            var luaTable = value.Read<LuaTable>();
            var result = new List<T>();

            // Iterate through numeric indices
            int index = 1;
            while (true)
            {
                var item = luaTable[index];
                if (item.Type == LuaValueType.Nil)
                    break;

                if (item.Type == LuaValueType.Number)
                {
                    var num = item.Read<double>();
                    result.Add((T)Convert.ChangeType(num, typeof(T)));
                }
                index++;
            }

            return result.Count > 0 ? result : null;
        }

        return null;
    }
}
