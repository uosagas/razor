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

// Portiert aus Razor CE (Razor/Core/EncodedSpeech.cs) — Phase 3c2.
// ABWEICHUNG: Razor CE laedt die komplette Keyword-Tabelle aus Speech.mul.
// Der Port hat keinen Mul-Zugriff; stattdessen ist die Teilmenge eingebettet,
// die die Speech-Hotkeys brauchen (Pet-Kommandos). Die IDs sind die
// OSI-Standardwerte und gegen den UOSagas-Server (BaseAI.cs) verifiziert:
//   0x164 all come, 0x165 all follow, 0x166 all guard, 0x16B all guard me,
//   0x167 all stop, 0x168 all kill, 0x169 all attack, 0x16C all follow me,
//   0x170 all stay.
// Das 12-Bit-Packing (Anzahl + Keyword-IDs) entspricht exakt Razor CE.

using System.Collections.Generic;

namespace Assistant.Core
{
    public class EncodedSpeech
    {
        internal class SpeechEntry : System.IComparable<SpeechEntry>
        {
            internal short m_KeywordID;
            internal string[] m_Keywords;

            internal SpeechEntry(int idKeyword, string keyword)
            {
                m_KeywordID = (short) idKeyword;
                m_Keywords = keyword.Split(new char[] {'*'});
            }

            public int CompareTo(SpeechEntry entry)
            {
                if (entry == null)
                    return -1;

                if (entry != this)
                {
                    if (m_KeywordID < entry.m_KeywordID)
                        return -1;

                    if (m_KeywordID > entry.m_KeywordID)
                        return 1;
                }

                return 0;
            }
        }

        // Eingebettete Teilmenge der Speech.mul-Keywords (siehe Kopfkommentar).
        private static readonly List<SpeechEntry> m_Speech = new List<SpeechEntry>
        {
            new SpeechEntry(0x164, "all come"),
            new SpeechEntry(0x165, "all follow"),
            new SpeechEntry(0x166, "all guard"),
            new SpeechEntry(0x167, "all stop"),
            new SpeechEntry(0x168, "all kill"),
            new SpeechEntry(0x169, "all attack"),
            new SpeechEntry(0x16B, "all guard me"),
            new SpeechEntry(0x16C, "all follow me"),
            new SpeechEntry(0x170, "all stay")
        };

        internal static List<ushort> GetKeywords(string text)
        {
            List<ushort> keynumber = new List<ushort>();

            text = text.ToLower();

            List<SpeechEntry> keywords = new List<SpeechEntry>();
            foreach (SpeechEntry entry in m_Speech)
            {
                if (IsMatch(text, entry.m_Keywords))
                    keywords.Add(entry);
            }

            keywords.Sort();

            bool flag = false;

            int numk = keywords.Count & 15;
            int index = 0;
            while (index < keywords.Count)
            {
                SpeechEntry entry = keywords[index];
                int keywordID = entry.m_KeywordID;

                if (flag)
                {
                    keynumber.Add((byte) (keywordID >> 4));
                    numk = keywordID & 15;
                }
                else
                {
                    keynumber.Add((byte) ((numk << 4) | ((keywordID >> 8) & 15)));
                    keynumber.Add((byte) keywordID);
                }

                index++;
                flag = !flag;
            }

            if (!flag)
            {
                keynumber.Add((byte) (numk << 4));
            }

            return keynumber;
        }

        private static bool IsMatch(string input, string[] split)
        {
            int startIndex = 0;

            for (int i = 0; i < split.Length; i++)
            {
                if (split[i].Length > 0)
                {
                    int index = input.IndexOf(split[i], startIndex);
                    if ((index > 0) && (i == 0))
                        return false;

                    if (index < 0)
                        return false;

                    startIndex = index + split[i].Length;
                }
            }

            return ((split[split.Length - 1].Length <= 0) || (startIndex == input.Length));
        }
    }
}
