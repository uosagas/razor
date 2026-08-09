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

// UOSagas-Razor: Journal-Puffer fuer die VScript-Journal-Nodes.
//
// Gegenstueck zu JournalComponent/LuaJournalAPI des integrierten Assistants:
// ein Ringpuffer aller Sprach-/Systemmeldungen (aus MessageManager.HandleSpeech,
// also 0x1C/0xAE/0xC1) plus der geteilte "Clear"-Filter: Clear() loescht keine
// Eintraege, sondern setzt StartTime — Suchen ignorieren alles davor
// (JournalContainsNode prueft entry.Time < StartTime, 1:1 wie im Client).

using System;
using System.Collections.Generic;
using Assistant.VScripts.Core;

namespace Assistant.VScripts.Engine;

public static class Journal
{
    private const int MaxEntries = 500;

    private static readonly object m_Lock = new();
    private static readonly List<JournalEntry> m_Entries = new();

    /// <summary>Geteilter Clear-Filter (wie LuaJournalAPI.StartTime).</summary>
    public static DateTime StartTime { get; private set; } = DateTime.MinValue;

    /// <summary>Snapshot der Eintraege (aeltester zuerst).</summary>
    public static List<JournalEntry> Entries
    {
        get
        {
            lock (m_Lock)
            {
                return new List<JournalEntry>(m_Entries);
            }
        }
    }

    public static void Add(string name, string text, ushort hue, MessageType type, bool unicode)
    {
        var entry = new JournalEntry
        {
            Name = name ?? string.Empty,
            Text = text ?? string.Empty,
            Hue = hue,
            IsUnicode = unicode,
            Time = DateTime.UtcNow,
            TextType = type switch
            {
                MessageType.System => JournalTextType.System,
                MessageType.Emote => JournalTextType.Emote,
                MessageType.Label => JournalTextType.Label,
                MessageType.Spell => JournalTextType.Spell,
                MessageType.Guild => JournalTextType.Guild,
                MessageType.Alliance => JournalTextType.Alliance,
                _ => JournalTextType.Regular
            }
        };

        lock (m_Lock)
        {
            m_Entries.Add(entry);
            if (m_Entries.Count > MaxEntries)
                m_Entries.RemoveRange(0, m_Entries.Count - MaxEntries);
        }
    }

    /// <summary>Setzt den Clear-Filter auf jetzt (loescht nichts).</summary>
    public static void Clear()
    {
        StartTime = DateTime.UtcNow;
    }

    /// <summary>Nur fuer Tests: Puffer + Filter komplett zuruecksetzen.</summary>
    public static void Reset()
    {
        lock (m_Lock)
        {
            m_Entries.Clear();
        }

        StartTime = DateTime.MinValue;
    }
}
