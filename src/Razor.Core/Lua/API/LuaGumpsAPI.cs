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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Assistant.VScripts.Core; // GumpShim/UIManager/Message (Paket-basierte Gump-Sicht)
using Lua;

namespace Assistant.LuaEngine.API;

/// <summary>
/// Gumps API for Lua-CSharp engine
/// </summary>
public static class LuaGumpsAPI
{
    // Queue for gump operations that must run on main thread
    private static readonly ConcurrentQueue<Action> _mainThreadQueue = new();

    public static void Register(LuaState state)
    {
        var gumps = new LuaTable();

        // Functions
        gumps["HasGump"] = new LuaFunction("HasGump", HasGump);
        gumps["IsActive"] = new LuaFunction("IsActive", HasGump); // Alias for HasGump
        gumps["CloseGump"] = new LuaFunction("CloseGump", CloseGump);
        gumps["GetGump"] = new LuaFunction("GetGump", GetGump);
        gumps["WaitForGump"] = new LuaFunction("WaitForGump", WaitForGump);
        gumps["Reply"] = new LuaFunction("Reply", Reply);
        gumps["PressButton"] = new LuaFunction("PressButton", Reply); // Alias for Reply
        gumps["Close"] = new LuaFunction("Close", CloseGump);
        gumps["Send"] = new LuaFunction("Send", Send);

        state.Environment["Gumps"] = gumps;
    }

    /// <summary>
    /// Process queued gump operations on main thread - call from game loop
    /// </summary>
    public static void ProcessMainThreadQueue()
    {
        while (_mainThreadQueue.TryDequeue(out var action))
        {
            try
            {
                action();
            }
            catch (Exception e)
            {
                Message.Warning($"Error processing gump action: {e.Message}");
            }
        }
    }

    /// <summary>
    /// Helper to find a gump by server serial (thread-safe using snapshot)
    /// </summary>
    private static GumpShim FindGumpByServerSerial(uint serverSerial)
    {
        try
        {
            // Create a snapshot to avoid collection modification during enumeration
            var gumpsSnapshot = UIManager.Gumps.ToArray();

            foreach (var gump in gumpsSnapshot)
            {
                if (gump == null || gump.IsDisposed)
                    continue;

                if (serverSerial == 0 || gump.ServerSerial == serverSerial)
                {
                    return gump;
                }
            }
        }
        catch (Exception)
        {
            // Collection was modified during ToArray, return null
        }
        return null;
    }

    private static ValueTask<int> HasGump(LuaFunctionExecutionContext context, CancellationToken ct)
    {
        try
        {
            var gumpId = context.ArgumentCount > 0 ? (uint)context.GetArgument<double>(0) : 0;
            var gump = FindGumpByServerSerial(gumpId);
            context.Return(gump != null);
        }
        catch (Exception e)
        {
            Message.Warning($"Error in HasGump: {e.Message}");
            context.Return(false);
        }

        return new ValueTask<int>(1);
    }

    private static ValueTask<int> CloseGump(LuaFunctionExecutionContext context, CancellationToken ct)
    {
        try
        {
            var gumpId = context.ArgumentCount > 0 ? (uint)context.GetArgument<double>(0) : 0;
            var gump = FindGumpByServerSerial(gumpId);

            if (gump != null)
            {
                // Queue dispose on main thread to avoid collection modification during enumeration
                _mainThreadQueue.Enqueue(() =>
                {
                    if (!gump.IsDisposed)
                        gump.Dispose();
                });
                context.Return(true);
            }
            else
            {
                context.Return(false);
            }
        }
        catch (Exception e)
        {
            Message.Warning($"Error in CloseGump: {e.Message}");
            context.Return(false);
        }

        return new ValueTask<int>(1);
    }

    private static ValueTask<int> GetGump(LuaFunctionExecutionContext context, CancellationToken ct)
    {
        try
        {
            var gumpId = context.ArgumentCount > 0 ? (uint)context.GetArgument<double>(0) : 0;
            var gump = FindGumpByServerSerial(gumpId);

            if (gump == null)
            {
                context.Return(LuaValue.Nil);
                return new ValueTask<int>(1);
            }

            var table = new LuaTable();
            table["Serial"] = (double)gump.ServerSerial;
            table["X"] = (double)gump.X;
            table["Y"] = (double)gump.Y;
            table["Width"] = (double)gump.Width;
            table["Height"] = (double)gump.Height;

            // Razor: Texte kommen aus dem Paket-Modell (GumpInfo.Strings) statt
            // aus dem Client-Control-Baum.
            List<string> texts = gump.Texts;

            // Create Lua array of texts
            var textsTable = new LuaTable();
            for (int i = 0; i < texts.Count; i++)
            {
                textsTable[i + 1] = texts[i]; // Lua arrays are 1-indexed
            }
            table["Texts"] = textsTable;

            // RawTexts: nur die Server-Strings in Server-Reihenfolge (Texts
            // hat alle aufgeloesten Clilocs davor stehen) — noetig, wenn ein
            // Script Listeneintraege positionsgenau lesen will.
            List<string> rawTexts = gump.RawTexts;
            var rawTable = new LuaTable();
            for (int i = 0; i < rawTexts.Count; i++)
            {
                rawTable[i + 1] = rawTexts[i];
            }
            table["RawTexts"] = rawTable;

            context.Return(table);
        }
        catch (Exception e)
        {
            Message.Warning($"Error in GetGump: {e.Message}");
            context.Return(LuaValue.Nil);
        }

        return new ValueTask<int>(1);
    }

    /// <summary>
    /// Native async wait for gump
    /// </summary>
    private static async ValueTask<int> WaitForGump(LuaFunctionExecutionContext context, CancellationToken ct)
    {
        try
        {
            var gumpId = (uint)context.GetArgument<double>(0);
            var milliseconds = (int)context.GetArgument<double>(1);
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            while (stopwatch.ElapsedMilliseconds < milliseconds)
            {
                ct.ThrowIfCancellationRequested();

                var gump = FindGumpByServerSerial(gumpId);
                if (gump != null)
                {
                    context.Return(true);
                    return 1;
                }

                await Task.Delay(100, ct);
            }

            context.Return(false);
        }
        catch (OperationCanceledException)
        {
            context.Return(false);
        }
        catch (Exception e)
        {
            Message.Warning($"Error in WaitForGump: {e.Message}");
            context.Return(false);
        }

        return 1;
    }

    /// <summary>Switches-Array aus einer Lua-Tabelle lesen ({0, 3, ...}).</summary>
    private static int[] ReadSwitches(LuaTable table)
    {
        if (table == null)
            return Array.Empty<int>();

        var switches = new List<int>();
        for (int i = 1; ; i++)
        {
            LuaValue value = table[i];
            if (value.Type == LuaValueType.Nil)
                break;
            if (value.TryRead(out double d))
                switches.Add((int)d);
        }

        return switches.ToArray();
    }

    /// <summary>Texteintraege aus einer Lua-Tabelle lesen ({{id, text}, ...}).</summary>
    private static GumpTextEntry[] ReadTextEntries(LuaTable table)
    {
        if (table == null)
            return Array.Empty<GumpTextEntry>();

        var entries = new List<GumpTextEntry>();
        for (int i = 1; ; i++)
        {
            LuaValue value = table[i];
            if (value.Type == LuaValueType.Nil)
                break;
            if (value.TryRead(out LuaTable pair)
                && pair[1].TryRead(out double id)
                && pair[2].TryRead(out string text))
            {
                entries.Add(new GumpTextEntry((ushort)id, text));
            }
        }

        return entries.ToArray();
    }

    /// <summary>
    /// Gumps.Reply(gumpId, button [, switches] [, textEntries]) — Antwort mit
    /// Radio-/Checkbox-Auswahl und Texteintraegen. Ein Ziel-Gump (z. B. das
    /// Moongate) braucht die Switch-Liste, der Button allein sagt dem Server
    /// nicht, WOHIN.
    /// </summary>
    private static ValueTask<int> Reply(LuaFunctionExecutionContext context, CancellationToken ct)
    {
        try
        {
            var gumpId = (uint)context.GetArgument<double>(0);
            var buttonId = (int)context.GetArgument<double>(1);
            var switchesTable = context.ArgumentCount > 2 ? context.GetArgument<LuaTable>(2) : null;
            var textEntriesTable = context.ArgumentCount > 3 ? context.GetArgument<LuaTable>(3) : null;

            // Auf dem Script-Task lesen — der Main-Thread-Callback fasst
            // keine LuaTable mehr an.
            int[] switches = ReadSwitches(switchesTable);
            GumpTextEntry[] entries = ReadTextEntries(textEntriesTable);

            var gump = FindGumpByServerSerial(gumpId);

            if (gump != null)
            {
                // Queue button click on main thread
                _mainThreadQueue.Enqueue(() =>
                {
                    if (!gump.IsDisposed)
                        gump.OnButtonClick(buttonId, switches, entries);
                });
                context.Return(true);
            }
            else
            {
                Message.Warning($"Reply: Gump {gumpId} not found");
                context.Return(false);
            }
        }
        catch (Exception e)
        {
            Message.Warning($"Error in Reply: {e.Message}");
            context.Return(false);
        }

        return new ValueTask<int>(1);
    }

    /// <summary>Alias von Reply mit identischer Signatur (historisch).</summary>
    private static ValueTask<int> Send(LuaFunctionExecutionContext context, CancellationToken ct)
    {
        return Reply(context, ct);
    }
}
