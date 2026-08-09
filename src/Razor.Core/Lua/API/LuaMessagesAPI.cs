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
using System.Threading;
using System.Threading.Tasks;
using Assistant.VScripts.Core;
using Lua;

namespace Assistant.LuaEngine.API;

/// <summary>
/// Messages API for Lua-CSharp engine
/// </summary>
public static class LuaMessagesAPI
{
    /// <summary>
    /// Helper to get any Lua argument as a string (handles string, number, boolean, nil, table)
    /// </summary>
    private static string GetArgumentAsString(LuaFunctionExecutionContext context, int index)
    {
        if (context.ArgumentCount <= index)
            return "";

        // Try string first (most common)
        try { return context.GetArgument<string>(index); }
        catch { }

        // Try number
        try { return context.GetArgument<double>(index).ToString(); }
        catch { }

        // Try boolean
        try { return context.GetArgument<bool>(index).ToString().ToLower(); }
        catch { }

        // Try table
        try
        {
            context.GetArgument<LuaTable>(index);
            return "[table]";
        }
        catch { }

        return "nil";
    }

    public static void Register(LuaState state)
    {
        var msgs = new LuaTable();

        // Functions
        msgs["Print"] = new LuaFunction("Print", Print);
        msgs["Info"] = new LuaFunction("Info", Info);
        msgs["Warning"] = new LuaFunction("Warning", Warning);
        msgs["Error"] = new LuaFunction("Error", ErrorMsg);
        msgs["Overhead"] = new LuaFunction("Overhead", Overhead);
        msgs["OverheadMobile"] = new LuaFunction("OverheadMobile", OverheadMobile);

        state.Environment["Messages"] = msgs;
    }

    private static ValueTask<int> Print(LuaFunctionExecutionContext context, CancellationToken ct)
    {
        try
        {
            var text = GetArgumentAsString(context, 0);
            ushort hue = context.ArgumentCount > 1 ? (ushort)context.GetArgument<double>(1) : (ushort)946;
            World.Player?.SendMessage(hue, text); // Razor: Systemmeldung ueber den Kern
            context.Return(true);
        }
        catch
        {
            context.Return(false);
        }
        return new ValueTask<int>(1);
    }

    private static ValueTask<int> Info(LuaFunctionExecutionContext context, CancellationToken ct)
    {
        try
        {
            var text = GetArgumentAsString(context, 0);
            Message.Info(text);
            context.Return(true);
        }
        catch
        {
            context.Return(false);
        }

        return new ValueTask<int>(1);
    }

    private static ValueTask<int> Warning(LuaFunctionExecutionContext context, CancellationToken ct)
    {
        try
        {
            var text = GetArgumentAsString(context, 0);
            Message.Warning(text);
            context.Return(true);
        }
        catch
        {
            context.Return(false);
        }

        return new ValueTask<int>(1);
    }

    private static ValueTask<int> ErrorMsg(LuaFunctionExecutionContext context, CancellationToken ct)
    {
        try
        {
            var text = GetArgumentAsString(context, 0);
            Message.Error(text);
            context.Return(true);
        }
        catch
        {
            context.Return(false);
        }

        return new ValueTask<int>(1);
    }

    private static ValueTask<int> Overhead(LuaFunctionExecutionContext context, CancellationToken ct)
    {
        try
        {
            var text = GetArgumentAsString(context, 0);
            var hue = context.ArgumentCount > 1 ? (ushort)context.GetArgument<double>(1) : (ushort)946;

            World.Player?.OverheadMessage(hue, text); // Razor: OverheadStyle-bewusst

            context.Return(true);
        }
        catch (Exception e)
        {
            Message.Warning($"Error in Overhead: {e.Message}");
            context.Return(false);
        }

        return new ValueTask<int>(1);
    }

    private static ValueTask<int> OverheadMobile(LuaFunctionExecutionContext context, CancellationToken ct)
    {
        try
        {
            var serial = (uint)context.GetArgument<double>(0);
            var text = GetArgumentAsString(context, 1);
            var hue = context.ArgumentCount > 2 ? (ushort)context.GetArgument<double>(2) : (ushort)946;

            if (World.Mobiles.TryGetValue(serial, out var mobile))
            {
                mobile.OverheadMessage(hue, text); // Razor: OverheadStyle-bewusst
                context.Return(true);
            }
            else
            {
                Message.Warning($"OverheadMobile: Mobile {serial} not found");
                context.Return(false);
            }
        }
        catch (Exception e)
        {
            Message.Warning($"Error in OverheadMobile: {e.Message}");
            context.Return(false);
        }

        return new ValueTask<int>(1);
    }
}
