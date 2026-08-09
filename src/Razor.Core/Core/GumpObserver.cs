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

// UOSagas-Razor: Gump-Inspector-Kern (Gegenstueck zum "Gump Observer" des
// integrierten Assistants).
//
// Der integrierte Observer liest den UI-Baum des Clients; das kann der Port
// nicht (die ABI exponiert keine Client-UI). Unsere Quelle ist der Paket-
// Mirror, und fuer Script-Autoren ist das die relevantere Sicht: 0xB0/0xDD
// liefern genau die Gump-ID fuer waitforgump, das Layout mit den Button-IDs
// fuer gumpresponse und die sichtbaren Texte fuer ingump — und 0xB1 zeigt,
// welchen Button man gerade selbst geklickt hat.
//
// Kein UI-Code hier: die Avalonia-Seite (GumpInspectorWindow) liest per
// Snapshot() und pollt Version (gleiche Architektur wie der UiSnapshot-Pump).

using System;
using System.Collections.Generic;
using System.Threading;

namespace Assistant.Core
{
    /// <summary>Ein Eintrag aus dem Gump-Layout ("{ button 10 10 ... }").</summary>
    public sealed class GumpControlInfo
    {
        public string Type;
        /// <summary>Roh-Argumente ohne den Typ — nichts geht verloren, auch bei unbekannten Tokens.</summary>
        public string[] Args = Array.Empty<string>();

        public int X = -1;
        public int Y = -1;
        public int? ButtonId;   // button/buttontileart: der Wert fuer gumpresponse
        public int? SwitchId;   // checkbox/radio
        public int? EntryId;    // textentry(limited)
        public int? Cliloc;     // xmfhtml*/tooltip
        public int? Graphic;    // gumppic/tilepic/resizepic
        public int? Page;       // page
        public string Text;     // aufgeloester Text (Textzeile bzw. Cliloc)

        public string Raw => Args.Length == 0 ? Type : Type + " " + string.Join(" ", Args);
    }

    /// <summary>Die 0xB1-Antwort des Spielers auf einen gefangenen Gump.</summary>
    public sealed class CapturedGumpResponse
    {
        public DateTime Timestamp;
        public int ButtonId;
        public int[] Switches = Array.Empty<int>();
        public KeyValuePair<ushort, string>[] TextEntries = Array.Empty<KeyValuePair<ushort, string>>();
    }

    public sealed class CapturedGump
    {
        public DateTime Timestamp;
        public uint Serial;
        public uint GumpId;
        public int X;
        public int Y;
        /// <summary>true = 0xDD (komprimiert), false = 0xB0.</summary>
        public bool Compressed;
        public string Layout = string.Empty;
        public string[] TextLines = Array.Empty<string>();
        public List<GumpControlInfo> Controls = new List<GumpControlInfo>();
        public CapturedGumpResponse Response;
    }

    public static class GumpObserver
    {
        private const int MaxCaptured = 100;

        private static readonly object _sync = new object();
        private static readonly List<CapturedGump> _captured = new List<CapturedGump>();
        private static int _version;

        /// <summary>Aufzeichnung an/aus. Standard aus — der Mirror laeuft immer, gespeichert wird nur hier.</summary>
        public static bool Recording { get; set; }

        /// <summary>Zaehlt bei jeder Aenderung hoch; die UI pollt darauf.</summary>
        public static int Version => Volatile.Read(ref _version);

        public static void Initialize()
        {
            // Eigene Viewer NEBEN den MacroHandlers-Viewern — ProcessViewers
            // ruft MoveToData() vor jedem Viewer, die Leseposition ist frisch.
            PacketHandler.RegisterServerToClientViewer(0xB0, OnUncompressedGump);
            PacketHandler.RegisterServerToClientViewer(0xDD, OnCompressedGump);
            PacketHandler.RegisterClientToServerViewer(0xB1, OnGumpResponse);
        }

        public static List<CapturedGump> Snapshot()
        {
            lock (_sync)
                return new List<CapturedGump>(_captured);
        }

        public static void Clear()
        {
            lock (_sync)
                _captured.Clear();
            Interlocked.Increment(ref _version);
        }

        // ---------------------------------------------------------------- capture

        /// <summary>0xB0 — Layout und Textzeilen liegen unkomprimiert im Paket (Format wie ClassicUO OpenGump).</summary>
        private static void OnUncompressedGump(PacketReader p, PacketHandlerEventArgs args)
        {
            if (!Recording)
                return;

            try
            {
                var capture = new CapturedGump
                {
                    Timestamp = DateTime.Now,
                    Serial = p.ReadUInt32(),
                    GumpId = p.ReadUInt32(),
                    X = (int)p.ReadUInt32(),
                    Y = (int)p.ReadUInt32(),
                    Compressed = false
                };

                ushort layoutLen = p.ReadUInt16();
                capture.Layout = p.ReadString(layoutLen);

                ushort lineCount = p.ReadUInt16();
                var lines = new List<string>();
                for (int i = 0; i < lineCount && !p.AtEnd; i++)
                {
                    int len = p.ReadUInt16();
                    // Textzeilen sind UTF-16 big-endian. ReadUnicodeString liest
                    // ueber ReadUInt16 (BE); die Methode ReadUnicodeStringBE ist
                    // trotz Namens little-endian (CE-Erbe) — nicht verwenden.
                    lines.Add(len > 0 ? p.ReadUnicodeString(len) : string.Empty);
                }

                capture.TextLines = lines.ToArray();
                capture.Controls = GumpLayoutParser.Parse(capture.Layout, capture.TextLines);
                Add(capture);
            }
            catch
            {
                // Der Inspector darf den Paketfluss nie stoeren.
            }
        }

        /// <summary>0xDD — Layout + Textzeilen ZLib-komprimiert (gleiche Dekompression wie MacroHandlers.CompressedGump).</summary>
        private static void OnCompressedGump(PacketReader p, PacketHandlerEventArgs args)
        {
            if (!Recording)
                return;

            try
            {
                var capture = new CapturedGump
                {
                    Timestamp = DateTime.Now,
                    Serial = p.ReadUInt32(),
                    GumpId = p.ReadUInt32(),
                    X = p.ReadInt32(),
                    Y = p.ReadInt32(),
                    Compressed = true
                };

                capture.Layout = p.GetCompressedReader().ReadString();

                int numStrings = p.ReadInt32();
                if (numStrings < 0 || numStrings > 256)
                    numStrings = 0;

                PacketReader pComp = p.GetCompressedReader();
                var lines = new List<string>();
                int len;
                while (!pComp.AtEnd && lines.Count < numStrings && (len = pComp.ReadInt16()) > 0)
                    lines.Add(pComp.ReadUnicodeString(len));

                capture.TextLines = lines.ToArray();
                capture.Controls = GumpLayoutParser.Parse(capture.Layout, capture.TextLines);
                Add(capture);
            }
            catch
            {
            }
        }

        /// <summary>0xB1 — die Antwort haengt sich an den juengsten offenen Capture desselben Gumps.</summary>
        private static void OnGumpResponse(PacketReader p, PacketHandlerEventArgs args)
        {
            try
            {
                uint serial = p.ReadUInt32();
                uint gumpId = p.ReadUInt32();
                int buttonId = p.ReadInt32();

                int switchCount = p.ReadInt32();
                if (switchCount < 0 || switchCount > 2000)
                    return;

                int[] switches = new int[switchCount];
                for (int i = 0; i < switchCount; i++)
                    switches[i] = p.ReadInt32();

                int entryCount = p.ReadInt32();
                if (entryCount < 0 || entryCount > 2000)
                    return;

                var entries = new KeyValuePair<ushort, string>[entryCount];
                for (int i = 0; i < entryCount; i++)
                {
                    ushort id = p.ReadUInt16();
                    ushort textLen = p.ReadUInt16();
                    if (textLen >= 240)
                        return;
                    entries[i] = new KeyValuePair<ushort, string>(id, p.ReadUnicodeStringSafe(textLen));
                }

                lock (_sync)
                {
                    for (int i = _captured.Count - 1; i >= 0; i--)
                    {
                        CapturedGump g = _captured[i];
                        if (g.GumpId == gumpId && g.Serial == serial && g.Response == null)
                        {
                            g.Response = new CapturedGumpResponse
                            {
                                Timestamp = DateTime.Now,
                                ButtonId = buttonId,
                                Switches = switches,
                                TextEntries = entries
                            };
                            break;
                        }
                    }
                }

                Interlocked.Increment(ref _version);
            }
            catch
            {
            }
        }

        private static void Add(CapturedGump capture)
        {
            lock (_sync)
            {
                _captured.Add(capture);
                if (_captured.Count > MaxCaptured)
                    _captured.RemoveAt(0);
            }

            Interlocked.Increment(ref _version);
        }
    }

    /// <summary>
    /// Zerlegt ein Gump-Layout in seine Controls. Bewusst tolerant: unbekannte
    /// Tokens bleiben mit Roh-Argumenten erhalten, fehlerhafte werden uebersprungen.
    /// Token-Formate wie im klassischen UO-Gump-Layout (Referenz: ClassicUO
    /// GumpsLoader/CE TryParseGump).
    /// </summary>
    public static class GumpLayoutParser
    {
        public static List<GumpControlInfo> Parse(string layout, string[] textLines)
        {
            var controls = new List<GumpControlInfo>();
            if (string.IsNullOrEmpty(layout))
                return controls;

            textLines ??= Array.Empty<string>();

            int index = 0;
            while (index < layout.Length)
            {
                int begin = layout.IndexOf('{', index);
                if (begin < 0)
                    break;
                int end = layout.IndexOf('}', begin + 1);
                if (end < 0)
                    break;

                string token = layout.Substring(begin + 1, end - begin - 1).Trim();
                index = end + 1;

                if (token.Length == 0)
                    continue;

                string[] parts = token.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                var control = new GumpControlInfo
                {
                    Type = parts[0].ToLowerInvariant(),
                    Args = parts.Length > 1 ? parts[1..] : Array.Empty<string>()
                };

                try
                {
                    Enrich(control, textLines);
                }
                catch
                {
                    // Roh-Args reichen; ein kaputter Eintrag darf den Rest nicht kosten.
                }

                controls.Add(control);
            }

            return controls;
        }

        private static int Arg(GumpControlInfo c, int i) => int.Parse(c.Args[i]);

        private static string Line(string[] lines, int i) =>
            i >= 0 && i < lines.Length ? lines[i] : null;

        private static void Enrich(GumpControlInfo c, string[] lines)
        {
            switch (c.Type)
            {
                // { button x y released pressed quit page id }
                case "button":
                // { buttontileart x y released pressed quit page id tileid hue tilex tiley }
                case "buttontileart":
                    ReadXy(c);
                    c.ButtonId = Arg(c, 6);
                    break;

                // { checkbox x y inactive active state serial } / radio dito
                case "checkbox":
                case "radio":
                    ReadXy(c);
                    c.SwitchId = Arg(c, 5);
                    break;

                // { textentry x y w h hue id textindex } (+ limited: maxlen)
                case "textentry":
                case "textentrylimited":
                    ReadXy(c);
                    c.EntryId = Arg(c, 5);
                    c.Text = Line(lines, Arg(c, 6));
                    break;

                // { text x y hue textindex }
                case "text":
                    ReadXy(c);
                    c.Text = Line(lines, Arg(c, 3));
                    break;

                // { croppedtext x y w h hue textindex }
                case "croppedtext":
                    ReadXy(c);
                    c.Text = Line(lines, Arg(c, 5));
                    break;

                // { htmlgump x y w h textindex background scrollbar }
                case "htmlgump":
                    ReadXy(c);
                    c.Text = Line(lines, Arg(c, 4));
                    break;

                // { xmfhtmlgump x y w h cliloc background scrollbar } (+color: ... hue)
                case "xmfhtmlgump":
                case "xmfhtmlgumpcolor":
                    ReadXy(c);
                    c.Cliloc = Arg(c, 4);
                    c.Text = ResolveCliloc(c.Cliloc.Value);
                    break;

                // { xmfhtmltok x y w h background scrollbar color cliloc @args@ }
                case "xmfhtmltok":
                    ReadXy(c);
                    c.Cliloc = Arg(c, 7);
                    c.Text = ResolveCliloc(c.Cliloc.Value);
                    break;

                // { gumppic x y graphic [hue=...] } / tilepic(hue) / resizepic x y graphic w h
                case "gumppic":
                case "tilepic":
                case "tilepichue":
                case "resizepic":
                    ReadXy(c);
                    c.Graphic = Arg(c, 2);
                    break;

                // { gumppictiled x y w h graphic }
                case "gumppictiled":
                    ReadXy(c);
                    c.Graphic = Arg(c, 4);
                    break;

                // { page n }
                case "page":
                    c.Page = Arg(c, 0);
                    break;

                // { tooltip cliloc [@args@] }
                case "tooltip":
                    c.Cliloc = Arg(c, 0);
                    c.Text = ResolveCliloc(c.Cliloc.Value);
                    break;

                default:
                    // Unbekannt/rein visuell: wenn die ersten zwei Args Zahlen
                    // sind, sind es fast immer Koordinaten.
                    if (c.Args.Length >= 2 &&
                        int.TryParse(c.Args[0], out int x) && int.TryParse(c.Args[1], out int y))
                    {
                        c.X = x;
                        c.Y = y;
                    }
                    break;
            }
        }

        private static void ReadXy(GumpControlInfo c)
        {
            c.X = Arg(c, 0);
            c.Y = Arg(c, 1);
        }

        private static string ResolveCliloc(int cliloc)
        {
            try
            {
                return ClientProxy.GetCliloc(cliloc);
            }
            catch
            {
                // Ohne gebundenen DataService (Tests, frueher Start) bleibt nur die ID.
                return null;
            }
        }
    }
}
