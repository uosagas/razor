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
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Assistant.VScripts.Core;
using Lua;

namespace Assistant.LuaEngine.API;

/// <summary>
/// Targeting API for Lua-CSharp engine with native async support
/// </summary>
public static class LuaTargetingAPI
{
    public static void Register(LuaState state)
    {
        var target = new LuaTable();

        // Functions
        target["WaitForTarget"] = new LuaFunction("WaitForTarget", WaitForTarget);
        target["GetNewTarget"] = new LuaFunction("GetNewTarget", GetNewTarget);
        target["TargetSerial"] = new LuaFunction("TargetSerial", TargetSerial);
        target["CancelTarget"] = new LuaFunction("CancelTarget", CancelTarget);
        target["GetLastTarget"] = new LuaFunction("GetLastTarget", GetLastTarget);
        target["IsTargeting"] = new LuaFunction("IsTargeting", IsTargeting);
        target["SetLast"] = new LuaFunction("SetLast", SetLast);
        target["Self"] = new LuaFunction("Self", TargetSelf);
        target["Last"] = new LuaFunction("Last", TargetLast);

        state.Environment["Target"] = target;
        // Backward compatibility alias
        state.Environment["Targeting"] = target;
    }

    /// <summary>
    /// Native async wait for target cursor - no coroutines needed!
    /// </summary>
    private static async ValueTask<int> WaitForTarget(LuaFunctionExecutionContext context, CancellationToken ct)
    {
        try
        {
            var milliseconds = (int)context.GetArgument<double>(0);
            var stopwatch = Stopwatch.StartNew();

            while (stopwatch.ElapsedMilliseconds < milliseconds)
            {
                ct.ThrowIfCancellationRequested();

                if (World.TargetManager.IsTargeting)
                {
                    context.Return(true);
                    return 1;
                }

                await Task.Delay(50, ct);
            }

            // Timeout
            context.Return(false);
        }
        catch (OperationCanceledException)
        {
            context.Return(false);
        }
        catch (Exception e)
        {
            Message.Warning($"Error in WaitForTarget: {e.Message}");
            context.Return(false);
        }

        return 1;
    }

    /// <summary>
    /// Native async get new target - waits for user to select target
    /// Returns the serial directly for backward compatibility
    /// </summary>
    private static async ValueTask<int> GetNewTarget(LuaFunctionExecutionContext context, CancellationToken ct)
    {
        try
        {
            var milliseconds = context.ArgumentCount > 0 ? (int)context.GetArgument<double>(0) : 10000;
            var stopwatch = Stopwatch.StartNew();

            // Store initial target state
            var targetManager = World.TargetManager;
            var initialSerial = targetManager.LastTargetInfoWithPick.Serial;

            // Enable targeting cursor for the user
            SetGetNewTargetActive(true);
            targetManager.BeginClientSidePick(); // Razor: OneTimeTarget-Cursor

            while (stopwatch.ElapsedMilliseconds < milliseconds)
            {
                ct.ThrowIfCancellationRequested();

                // Check if a new target was selected
                if (targetManager.LastTargetInfoWithPick.Serial != initialSerial && targetManager.LastTargetInfoWithPick.Serial != 0)
                {
                    SetGetNewTargetActive(false);
                    // Return just the serial for backward compatibility
                    context.Return((double)targetManager.LastTargetInfoWithPick.Serial);
                    return 1;
                }

                await Task.Delay(50, ct);
            }

            // Timeout
            SetGetNewTargetActive(false);
            context.Return(LuaValue.Nil);
        }
        catch (OperationCanceledException)
        {
            SetGetNewTargetActive(false);
            context.Return(LuaValue.Nil);
        }
        catch (Exception e)
        {
            SetGetNewTargetActive(false);
            Message.Warning($"Error in GetNewTarget: {e.Message}");
            context.Return(LuaValue.Nil);
        }

        return 1;
    }

    private static ValueTask<int> TargetSerial(LuaFunctionExecutionContext context, CancellationToken ct)
    {
        try
        {
            var serial = (uint)context.GetArgument<double>(0);

            var targetManager = World.TargetManager;

            // Find the entity to target
            UOEntity entity = null;
            if (World.Mobiles.TryGetValue(serial, out var mobile))
            {
                entity = mobile;
            }
            else if (World.Items.TryGetValue(serial, out var item))
            {
                entity = item;
            }

            if (entity != null)
            {
                targetManager.Target((uint) entity.Serial);
                context.Return(true);
            }
            else
            {
                Message.Warning($"TargetSerial: Entity {serial} not found");
                context.Return(false);
            }
        }
        catch (Exception e)
        {
            Message.Warning($"Error in TargetSerial: {e.Message}");
            context.Return(false);
        }

        return new ValueTask<int>(1);
    }

    private static ValueTask<int> CancelTarget(LuaFunctionExecutionContext context, CancellationToken ct)
    {
        try
        {
            var targetManager = World.TargetManager;
            targetManager.CancelTarget();
            context.Return(true);
        }
        catch (Exception e)
        {
            Message.Warning($"Error in CancelTarget: {e.Message}");
            context.Return(false);
        }

        return new ValueTask<int>(1);
    }

    private static ValueTask<int> GetLastTarget(LuaFunctionExecutionContext context, CancellationToken ct)
    {
        try
        {
            var targetManager = World.TargetManager;
            var serial = targetManager.LastTargetInfoWithPick.Serial;

            if (serial == 0)
            {
                context.Return(LuaValue.Nil);
                return new ValueTask<int>(1);
            }

            var resultTable = new LuaTable();
            resultTable["Serial"] = (double)serial;

            // Try to get more info
            if (World.Mobiles.TryGetValue(serial, out var mobile) && mobile != null)
            {
                resultTable["X"] = (double)mobile.X;
                resultTable["Y"] = (double)mobile.Y;
                resultTable["Z"] = (double)mobile.Z;
                resultTable["Name"] = mobile.Name ?? "";
            }

            context.Return(resultTable);
        }
        catch (Exception e)
        {
            Message.Warning($"Error in GetLastTarget: {e.Message}");
            context.Return(LuaValue.Nil);
        }

        return new ValueTask<int>(1);
    }

    private static ValueTask<int> IsTargeting(LuaFunctionExecutionContext context, CancellationToken ct)
    {
        try
        {
            var targetManager = World.TargetManager;
            context.Return(targetManager.IsTargeting);
        }
        catch (Exception e)
        {
            Message.Warning($"Error in IsTargeting: {e.Message}");
            context.Return(false);
        }

        return new ValueTask<int>(1);
    }

    private static ValueTask<int> SetLast(LuaFunctionExecutionContext context, CancellationToken ct)
    {
        try
        {
            var serial = (uint)context.GetArgument<double>(0);

            var targetManager = World.TargetManager;
            targetManager.LastTargetInfoWithPick.Serial = serial;

            context.Return(true);
        }
        catch (Exception e)
        {
            Message.Warning($"Error in SetLast: {e.Message}");
            context.Return(false);
        }

        return new ValueTask<int>(1);
    }

    private static ValueTask<int> TargetSelf(LuaFunctionExecutionContext context, CancellationToken ct)
    {
        try
        {
            var player = World.Player;
            var targetManager = World.TargetManager;
            targetManager.Target((uint) player.Serial);
            context.Return(true);
        }
        catch (Exception e)
        {
            Message.Warning($"Error in TargetSelf: {e.Message}");
            context.Return(false);
        }

        return new ValueTask<int>(1);
    }

    private static ValueTask<int> TargetLast(LuaFunctionExecutionContext context, CancellationToken ct)
    {
        try
        {
            var targetManager = World.TargetManager;
            var lastInfo = targetManager.LastTargetInfoWithPick;
            var serial = lastInfo.Serial;

            // Entity target (mobile/item) - target by serial
            if (serial != 0)
            {
                UOEntity entity = null;
                if (World.Mobiles.TryGetValue(serial, out var mobile))
                    entity = mobile;
                else if (World.Items.TryGetValue(serial, out var item))
                    entity = item;

                if (entity != null)
                {
                    targetManager.Target((uint) entity.Serial);
                    context.Return(true);
                }
                else
                {
                    Message.Warning($"TargetLast: Last target entity {serial} not found");
                    context.Return(false);
                }
                return new ValueTask<int>(1);
            }

            // Static/Land target (e.g. tree, mining tile) - replay last target packet
            // Supports the use case: user manually clicks static with axe/pickaxe,
            // then script uses Target.Last() to repeat the same target
            if (lastInfo.IsStatic || lastInfo.IsLand)
            {
                targetManager.TargetLast();
                context.Return(true);
                return new ValueTask<int>(1);
            }

            Message.Warning("TargetLast: No last target set");
            context.Return(false);
        }
        catch (Exception e)
        {
            Message.Warning($"Error in TargetLast: {e.Message}");
            context.Return(false);
        }

        return new ValueTask<int>(1);
    }

    // Flag for tracking if GetNewTarget is active (used by other parts of the client)
    private static bool _isGetNewTargetActive = false;

    /// <summary>
    /// Check if GetNewTarget mode is currently active
    /// Used by GameSceneInputHandler to avoid showing InspectorGump during target selection
    /// </summary>
    public static bool IsGetNewTargetActive() => _isGetNewTargetActive;

    /// <summary>
    /// Set the GetNewTarget active state
    /// </summary>
    internal static void SetGetNewTargetActive(bool active) => _isGetNewTargetActive = active;
}
