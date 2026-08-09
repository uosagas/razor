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
/// Mobiles API for Lua-CSharp engine
/// </summary>
public static class LuaMobilesAPI
{
    public static void Register(LuaState state)
    {
        var mobiles = new LuaTable();

        // Functions
        mobiles["FindBySerial"] = new LuaFunction("FindBySerial", FindBySerial);
        mobiles["FindByType"] = new LuaFunction("FindByType", FindByType);
        mobiles["FindByName"] = new LuaFunction("FindByName", FindByName);
        mobiles["FindByFilter"] = new LuaFunction("FindByFilter", FindByFilter);
        mobiles["Rename"] = new LuaFunction("Rename", Rename);

        // Metatable
        var metatable = new LuaTable();
        metatable["__index"] = new LuaFunction("MobilesIndex", MobilesIndex);
        mobiles.Metatable = metatable;

        state.Environment["Mobiles"] = mobiles;
    }

    private static ValueTask<int> MobilesIndex(LuaFunctionExecutionContext context, CancellationToken ct)
    {
        context.Return(LuaValue.Nil);
        return new ValueTask<int>(1);
    }

    private static ValueTask<int> FindBySerial(LuaFunctionExecutionContext context, CancellationToken ct)
    {
        try
        {
            var serial = (uint)context.GetArgument<double>(0);
            if (!World.Mobiles.TryGetValue(serial, out var mobile))
            {
                context.Return(LuaValue.Nil);
                return new ValueTask<int>(1);
            }

            context.Return(CreateMobileTable(mobile));
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
            var mobile = World.Mobiles.Values
                .FirstOrDefault(m => m.Graphic == graphic && !m.IsDestroyed);

            if (mobile == null)
            {
                context.Return(LuaValue.Nil);
                return new ValueTask<int>(1);
            }

            context.Return(CreateMobileTable(mobile));
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

            if (string.IsNullOrEmpty(name))
            {
                context.Return(LuaValue.Nil);
                return new ValueTask<int>(1);
            }

            var mobile = World.Mobiles.Values
                .FirstOrDefault(m => m.Name != null && m.Name.Equals(name, StringComparison.OrdinalIgnoreCase) && !m.IsDestroyed);

            if (mobile == null)
            {
                context.Return(LuaValue.Nil);
                return new ValueTask<int>(1);
            }

            context.Return(CreateMobileTable(mobile));
        }
        catch (Exception e)
        {
            Message.Warning($"Error in FindByName: {e.Message}");
            context.Return(LuaValue.Nil);
        }

        return new ValueTask<int>(1);
    }

    private static ValueTask<int> FindByFilter(LuaFunctionExecutionContext context, CancellationToken ct)
    {
        try
        {
            var filterTable = context.GetArgument<LuaTable>(0);

            // Extract filter parameters
            bool? isDead = GetTableBoolean(filterTable, "dead");
            bool? isFemale = GetTableBoolean(filterTable, "female");
            bool? isHuman = GetTableBoolean(filterTable, "human");
            bool? isPoisoned = GetTableBoolean(filterTable, "poisoned");
            bool? isParalyzed = GetTableBoolean(filterTable, "paralized");
            int? rangeMin = GetTableNumber<int>(filterTable, "rangemin");
            int? rangeMax = GetTableNumber<int>(filterTable, "rangemax");
            var names = GetTableStringList(filterTable, "names");
            var hues = GetTableNumberList<ushort>(filterTable, "hues");
            var bodies = GetTableNumberList<ushort>(filterTable, "bodies");
            var notorieties = GetTableStringList(filterTable, "notorieties");
            var serials = GetTableNumberList<uint>(filterTable, "serials");

            var results = new LuaTable();
            int index = 1;

            foreach (var mobile in World.Mobiles.Values)
            {
                if (mobile.IsDestroyed)
                    continue;

                if (isDead.HasValue && mobile.IsDead != isDead.Value)
                    continue;
                if (isFemale.HasValue && mobile.IsFemale != isFemale.Value)
                    continue;
                if (isHuman.HasValue && mobile.IsHuman != isHuman.Value)
                    continue;
                if (isPoisoned.HasValue && mobile.IsPoisoned != isPoisoned.Value)
                    continue;
                if (isParalyzed.HasValue && mobile.IsParalyzed != isParalyzed.Value)
                    continue;
                if (rangeMin.HasValue && mobile.Distance < rangeMin.Value)
                    continue;
                if (rangeMax.HasValue && mobile.Distance > rangeMax.Value)
                    continue;
                if (names != null && names.Count > 0 && !names.Contains(mobile.Name))
                    continue;
                if (hues != null && hues.Count > 0 && !hues.Contains(mobile.Hue))
                    continue;
                if (bodies != null && bodies.Count > 0 && !bodies.Contains(mobile.Graphic))
                    continue;
                if (notorieties != null && notorieties.Count > 0 && !notorieties.Contains(((Enums.Notoriety)(byte) mobile.NotorietyFlag).ToString()))
                    continue;
                if (serials != null && serials.Count > 0 && !serials.Contains(mobile.Serial))
                    continue;

                results[index++] = CreateMobileTable(mobile);
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

    private static ValueTask<int> Rename(LuaFunctionExecutionContext context, CancellationToken ct)
    {
        try
        {
            var serial = (uint)context.GetArgument<double>(0);
            var name = context.GetArgument<string>(1);

            if (!World.Mobiles.TryGetValue(serial, out var mobile))
            {
                Message.Warning($"Rename: Mobile with serial {serial} not found");
                context.Return(false);
                return new ValueTask<int>(1);
            }

            if (string.IsNullOrEmpty(name))
            {
                Message.Warning("Rename: Name cannot be empty");
                context.Return(false);
                return new ValueTask<int>(1);
            }

            GameActions.Rename(serial, name);
            context.Return(true);
        }
        catch (Exception e)
        {
            Message.Warning($"Error in Rename: {e.Message}");
            context.Return(false);
        }

        return new ValueTask<int>(1);
    }

    /// <summary>
    /// Create a Lua table representing a Mobile
    /// </summary>
    internal static LuaTable CreateMobileTable(Mobile mobile)
    {
        var table = new LuaTable();

        // Entity properties
        table["Serial"] = (double)(uint)mobile.Serial;
        table["Name"] = mobile.Name ?? "";
        table["Distance"] = (double)mobile.Distance;
        table["Direction"] = ((Direction) mobile.Direction).ToString() ?? "";
        table["IsDestroyed"] = mobile.IsDestroyed;

        // GameObject properties
        table["X"] = (double)mobile.X;
        table["Y"] = (double)mobile.Y;
        table["Z"] = (double)mobile.Z;
        table["Hue"] = (double)mobile.Hue;
        table["Graphic"] = (double)mobile.Graphic;

        // Mobile specific properties
        table["Hits"] = (double)mobile.Hits;
        table["HitsMax"] = (double)mobile.HitsMax;
        table["DiffHits"] = (double)(mobile.HitsMax - mobile.Hits);
        table["Stam"] = (double)mobile.Stamina;
        table["MaxStam"] = (double)mobile.StaminaMax;
        table["Mana"] = (double)mobile.Mana;
        table["MaxMana"] = (double)mobile.ManaMax;
        table["IsRunning"] = mobile.IsRunning;
        table["IsParalyzed"] = mobile.IsParalyzed;
        table["IsDead"] = mobile.IsDead;
        table["IsHidden"] = mobile.IsHidden;
        table["IsPoisoned"] = mobile.IsPoisoned;
        table["IsMounted"] = mobile.IsMounted;
        table["IsFlying"] = mobile.IsFlying;
        table["IsHuman"] = mobile.IsHuman;
        table["IsGargoyle"] = mobile.IsGargoyle;
        table["IsYellowHits"] = mobile.IsYellowHits;
        table["IsRenamable"] = mobile.IsRenamable;
        table["IsFemale"] = mobile.IsFemale;
        table["IgnoreCharacters"] = mobile.IgnoreCharacters;
        table["NotorietyFlag"] = ((Enums.Notoriety)(byte) mobile.NotorietyFlag).ToString() ?? "";
        table["Race"] = mobile.Race.ToString() ?? "";
        table["Title"] = mobile.Title ?? "";
        table["SpeedMode"] = "Normal" ?? "";

        return table;
    }

    // Helper methods
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

    private static List<string> GetTableStringList(LuaTable table, string key)
    {
        var value = table[key];
        if (value.Type != LuaValueType.Table)
            return null;

        var list = new List<string>();
        var arrayTable = value.Read<LuaTable>();

        foreach (var pair in arrayTable)
        {
            if (pair.Value.Type == LuaValueType.String)
            {
                list.Add(pair.Value.Read<string>());
            }
        }

        return list;
    }

    private static List<T> GetTableNumberList<T>(LuaTable table, string key) where T : struct
    {
        var value = table[key];
        if (value.Type != LuaValueType.Table)
            return null;

        var list = new List<T>();
        var arrayTable = value.Read<LuaTable>();

        foreach (var pair in arrayTable)
        {
            if (pair.Value.Type == LuaValueType.Number)
            {
                var num = pair.Value.Read<double>();
                list.Add((T)Convert.ChangeType(num, typeof(T)));
            }
        }

        return list;
    }
}
