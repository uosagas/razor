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

// UOSagas-Razor: passiver OPL-Cache (Object Property List) aus dem 0xD6-
// MegaCliloc-Mirror. Der Client fordert die Properties ohnehin fuer seine
// Tooltips an; wir lesen die Antworten mit und halten pro Serial den Namen
// (erste Zeile) und die restlichen Property-Zeilen. Genutzt von den VScript-
// Nodes (GetItemProperties via World.OPL-Shim). Cliloc-Aufloesung ueber den
// DataService (D5 — keine mul-Dateien).

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;

namespace Assistant.Core
{
    public static class OplCache
    {
        public sealed class OplEntry
        {
            public string Name;
            public string Data;
            public uint Revision;
        }

        private static readonly ConcurrentDictionary<uint, OplEntry> m_Cache = new();

        public static bool TryGet(uint serial, out string name, out string data)
        {
            if (m_Cache.TryGetValue(serial, out OplEntry entry))
            {
                name = entry.Name;
                data = entry.Data;
                return !string.IsNullOrEmpty(name);
            }

            name = null;
            data = null;
            return false;
        }

        public static void Clear()
        {
            m_Cache.Clear();
        }

        /// <summary>0xD6 MegaCliloc (Layout wie der authoritative Client-Parser):
        /// unknown(2) serial(4) skip(2) revision(4), dann [cliloc(4) len(2)
        /// unicodeLE(len)]* bis cliloc == 0.</summary>
        public static void OnMegaCliloc(PacketReader p, PacketHandlerEventArgs args)
        {
            ushort unknown = p.ReadUInt16();
            if (unknown > 1)
                return;

            uint serial = p.ReadUInt32();
            p.ReadUInt16(); // 0
            uint revision = p.ReadUInt32();

            string name = null;
            var lines = new List<string>();

            while (!p.AtEnd)
            {
                int cliloc = (int) p.ReadUInt32();
                if (cliloc == 0)
                    break;

                ushort length = p.ReadUInt16();
                // Achtung CE-Erbe: ReadUnicodeStringBE liest trotz Namens
                // little-endian (siehe STATE.md, Gump-Inspector-Falle).
                string argument = length > 0 ? p.ReadUnicodeStringBE(length / 2) : string.Empty;

                string text = ClientProxy.GetCliloc(cliloc, argument);
                if (string.IsNullOrEmpty(text))
                    text = $"[cliloc {cliloc}]";

                if (name == null)
                    name = text;
                else
                    lines.Add(text);
            }

            if (name == null)
                return;

            m_Cache[serial] = new OplEntry
            {
                Name = name,
                Data = string.Join("\n", lines),
                Revision = revision
            };

            // Namen ins Weltmodell uebernehmen (OPL-Name ist der echte Name).
            Serial s = (Serial) serial;
            if (s.IsMobile)
            {
                Mobile m = World.FindMobile(s);
                if (m != null && string.IsNullOrEmpty(m.Name))
                    m.Name = StripHtml(name);
            }
            else if (s.IsItem)
            {
                Item i = World.FindItem(s);
                if (i != null)
                    i.Name = StripHtml(name);
            }
        }

        /// <summary>OPL-Zeilen tragen teils BASEFONT-/HTML-Tags — fuer Namen entfernen.</summary>
        private static string StripHtml(string text)
        {
            if (string.IsNullOrEmpty(text) || text.IndexOf('<') < 0)
                return text;

            var sb = new StringBuilder(text.Length);
            bool inTag = false;

            foreach (char c in text)
            {
                if (c == '<')
                    inTag = true;
                else if (c == '>')
                    inTag = false;
                else if (!inTag)
                    sb.Append(c);
            }

            return sb.ToString().Trim();
        }
    }
}
