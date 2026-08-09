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
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Assistant.VScripts.Core;
using RazorJournal = Assistant.VScripts.Engine.Journal;
using Assistant.VScripts.Core;
using Lua;

namespace Assistant.LuaEngine.API;

/// <summary>
/// Journal API for Lua-CSharp engine
/// </summary>
public static class LuaJournalAPI
{
    // Razor: UtcNow statt Client-Now — das geteilte Journal (VScripts.Engine.Journal)
    // stempelt Eintraege mit DateTime.UtcNow; ein lokaler Vergleichszeitpunkt
    // wuerde je nach Zeitzone ALLE neuen Eintraege ausfiltern.
    private static DateTime _startTime;

    // Expliziter statischer Ctor: ohne ihn ist die Klasse beforefieldinit und
    // die CLR darf _startTime erst beim ERSTEN Contains/GetAll stempeln —
    // dann fielen alle Eintraege zwischen Engine-Init und erstem Aufruf raus.
    static LuaJournalAPI()
    {
        _startTime = DateTime.UtcNow;
    }

    /// <summary>
    /// Start time used as the journal "clear" filter. Entries older than this are ignored by the
    /// Contains/search functions. Shared across all scripting engines (Lua, UOR, VScript) so a
    /// Journal.Clear() in any of them takes effect everywhere.
    /// </summary>
    public static DateTime StartTime => _startTime;

    public static void Register(LuaState state)
    {
        var journal = new LuaTable();

        // Functions
        journal["Contains"] = new LuaFunction("Contains", Contains);
        journal["ContainsFrom"] = new LuaFunction("ContainsFrom", ContainsFrom);
        journal["Clear"] = new LuaFunction("Clear", Clear);
        journal["WaitForText"] = new LuaFunction("WaitForText", WaitForText);
        journal["GetLast"] = new LuaFunction("GetLast", GetLast);
        journal["GetAll"] = new LuaFunction("GetAll", GetAll);

        state.Environment["Journal"] = journal;
    }

    private static ValueTask<int> Contains(LuaFunctionExecutionContext context, CancellationToken ct)
    {
        try
        {
            var text = context.GetArgument<string>(0);

            List<JournalEntry> journalEntries = RazorJournal.Entries;

            foreach (JournalEntry entry in journalEntries)
            {
                if (entry.Time >= _startTime)
                {
                    if (entry.Text.IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        context.Return(true);
                        return new ValueTask<int>(1);
                    }
                }
            }

            context.Return(false);
        }
        catch (Exception e)
        {
            Message.Warning($"Error in Journal.Contains: {e.Message}");
            context.Return(false);
        }

        return new ValueTask<int>(1);
    }

    private static ValueTask<int> ContainsFrom(LuaFunctionExecutionContext context, CancellationToken ct)
    {
        try
        {
            var text = context.GetArgument<string>(0);
            var source = context.GetArgument<string>(1);

            List<JournalEntry> journalEntries = RazorJournal.Entries;

            foreach (JournalEntry entry in journalEntries)
            {
                if (entry.Time >= _startTime)
                {
                    if (entry.Name != null &&
                        entry.Name.Equals(source, StringComparison.OrdinalIgnoreCase) &&
                        entry.Text.IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        context.Return(true);
                        return new ValueTask<int>(1);
                    }
                }
            }

            context.Return(false);
        }
        catch (Exception e)
        {
            Message.Warning($"Error in Journal.ContainsFrom: {e.Message}");
            context.Return(false);
        }

        return new ValueTask<int>(1);
    }

    private static ValueTask<int> Clear(LuaFunctionExecutionContext context, CancellationToken ct)
    {
        try
        {
            _startTime = DateTime.UtcNow;
            context.Return(true);
        }
        catch (Exception e)
        {
            Message.Warning($"Error in Journal.Clear: {e.Message}");
            context.Return(false);
        }

        return new ValueTask<int>(1);
    }

    /// <summary>
    /// Wait for specific text to appear in journal - native async
    /// </summary>
    private static async ValueTask<int> WaitForText(LuaFunctionExecutionContext context, CancellationToken ct)
    {
        try
        {
            var text = context.GetArgument<string>(0);
            var milliseconds = (int)context.GetArgument<double>(1);
            var stopwatch = Stopwatch.StartNew();
            var searchStartTime = DateTime.UtcNow;

            while (stopwatch.ElapsedMilliseconds < milliseconds)
            {
                ct.ThrowIfCancellationRequested();

                List<JournalEntry> journalEntries = RazorJournal.Entries;

                foreach (JournalEntry entry in journalEntries)
                {
                    if (entry.Time >= searchStartTime)
                    {
                        if (entry.Text.IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            context.Return(true);
                            return 1;
                        }
                    }
                }

                await Task.Delay(100, ct);
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
            Message.Warning($"Error in Journal.WaitForText: {e.Message}");
            context.Return(false);
        }

        return 1;
    }

    private static ValueTask<int> GetLast(LuaFunctionExecutionContext context, CancellationToken ct)
    {
        try
        {
            int count = context.ArgumentCount > 0 ? (int)context.GetArgument<double>(0) : 1;

            List<JournalEntry> journalEntries = RazorJournal.Entries;
            var result = new LuaTable();

            int index = 1;
            for (int i = journalEntries.Count - 1; i >= 0 && index <= count; i--)
            {
                var entry = journalEntries[i];
                var entryTable = new LuaTable();
                entryTable["Text"] = entry.Text ?? "";
                entryTable["Name"] = entry.Name ?? "";
                entryTable["Hue"] = (double)entry.Hue;
                entryTable["Time"] = entry.Time.ToString("HH:mm:ss");

                result[index++] = entryTable;
            }

            context.Return(result);
        }
        catch (Exception e)
        {
            Message.Warning($"Error in Journal.GetLast: {e.Message}");
            context.Return(new LuaTable());
        }

        return new ValueTask<int>(1);
    }

    private static ValueTask<int> GetAll(LuaFunctionExecutionContext context, CancellationToken ct)
    {
        try
        {
            List<JournalEntry> journalEntries = RazorJournal.Entries;
            var result = new LuaTable();

            int index = 1;
            foreach (var entry in journalEntries)
            {
                if (entry.Time >= _startTime)
                {
                    var entryTable = new LuaTable();
                    entryTable["Text"] = entry.Text ?? "";
                    entryTable["Name"] = entry.Name ?? "";
                    entryTable["Hue"] = (double)entry.Hue;
                    entryTable["Time"] = entry.Time.ToString("HH:mm:ss");

                    result[index++] = entryTable;
                }
            }

            context.Return(result);
        }
        catch (Exception e)
        {
            Message.Warning($"Error in Journal.GetAll: {e.Message}");
            context.Return(new LuaTable());
        }

        return new ValueTask<int>(1);
    }

    /// <summary>
    /// Clear internal journal timer (called from UOR scripts)
    /// </summary>
    public static void ClearInternal()
    {
        _startTime = DateTime.UtcNow;
    }

    /// <summary>
    /// Check if journal contains text (C# API for UOR scripts)
    /// </summary>
    public static bool ContainsText(string text)
    {
        try
        {
            List<JournalEntry> journalEntries = RazorJournal.Entries;

            foreach (JournalEntry entry in journalEntries)
            {
                if (entry.Time >= _startTime)
                {
                    if (entry.Text.IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        return true;
                    }
                }
            }
            return false;
        }
        catch
        {
            return false;
        }
    }
}
