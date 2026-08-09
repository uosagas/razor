#region license
// Razor: An Ultima Online Assistant
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

// Portiert aus Razor CE (Razor/Network/Handlers.cs: TryParseGump/
// ParseGumpString + Cliloc-Extraktion aus CompressedGump). Liefert die
// sichtbaren Texte eines Gumps fuer die Script-Expressions
// gumpexists/ingump. Cliloc-Aufloesung via DataService (ClientProxy.GetCliloc)
// statt Language.GetString.

using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Assistant.Core
{
    public static class GumpTextParser
    {
        /// <summary>Extrahiert alle sichtbaren Texte (Clilocs + text/htmlgump-Eintraege) eines Gump-Layouts.</summary>
        public static List<string> ExtractStrings(string layout, string[] textLines)
        {
            List<string> gumpStrings = new List<string>();

            if (string.IsNullOrEmpty(layout))
                return gumpStrings;

            // Razor CE: Cliloc-IDs im Layout aufloesen (bekannte Ranges).
            string[] numbers = Regex.Split(layout, @"\D+");

            foreach (string value in numbers)
            {
                if (!string.IsNullOrEmpty(value) && int.TryParse(value, out int i))
                {
                    if ((i >= 500000 && i <= 503405) || (i >= 1000000 && i <= 1155584) ||
                        (i >= 3000000 && i <= 3011032))
                    {
                        string text = ClientProxy.GetCliloc(i);
                        if (!string.IsNullOrEmpty(text))
                            gumpStrings.Add(text);
                    }
                }
            }

            if (TryParseGump(layout, out string[] gumpPieces))
            {
                gumpStrings.AddRange(ParseGumpString(gumpPieces, textLines ?? new string[0]));
            }

            return gumpStrings;
        }

        private static bool TryParseGump(string gumpData, out string[] pieces)
        {
            List<string> i = new List<string>();
            int dataIndex = 0;
            while (dataIndex < gumpData.Length)
            {
                if (gumpData.Substring(dataIndex) == "\0")
                {
                    break;
                }
                else
                {
                    int begin = gumpData.IndexOf("{", dataIndex);
                    int end = gumpData.IndexOf("}", dataIndex + 1);
                    if ((begin != -1) && (end != -1))
                    {
                        string sub = gumpData.Substring(begin + 1, end - begin - 1).Trim();
                        i.Add(sub);
                        dataIndex = end;
                    }
                    else
                    {
                        break;
                    }
                }
            }

            pieces = i.ToArray();
            return (pieces.Length > 0);
        }

        private static List<string> ParseGumpString(string[] gumpPieces, string[] gumpLines)
        {
            List<string> gumpText = new List<string>();
            for (int i = 0; i < gumpPieces.Length; i++)
            {
                string[] gumpParams = Regex.Split(gumpPieces[i], @"\s+");

                try
                {
                    switch (gumpParams[0].ToLower())
                    {
                        case "croppedtext":
                            // CroppedText [x] [y] [width] [height] [color] [text-id]
                            gumpText.Add(gumpLines[int.Parse(gumpParams[6])]);
                            break;

                        case "htmlgump":
                            // HtmlGump [x] [y] [width] [height] [text-id] [background] [scrollbar]
                            gumpText.Add(gumpLines[int.Parse(gumpParams[5])]);
                            break;

                        case "text":
                            // Text [x] [y] [color] [text-id]
                            gumpText.Add(gumpLines[int.Parse(gumpParams[4])]);
                            break;
                    }
                }
                catch
                {
                    // fehlerhafte/abgeschnittene Eintraege ueberspringen
                }
            }

            return gumpText;
        }
    }
}
