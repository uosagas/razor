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

// Portiert aus Razor CE (Razor/Core/OverheadManager.cs) — Option
// ShowOverheadMessages: benutzerdefinierte Trigger. Enthaelt eine eingehende
// Systemmeldung einen Suchtext, erscheint die gepflegte Kurzfassung im
// gewaehlten Hue ueber dem Spieler (Format {msg}; {1}..{n} = Woerter der
// Original-Meldung), optional mit Sound. Profilsektion "overheadmessages"
// (byte-kompatibel zu CE — alte Profile bringen ihre Trigger mit).
// UI-Teile (ListView/RedrawList) leben in Razor.Avalonia, nicht hier.

using System;
using System.Collections.Generic;
using System.Xml;

namespace Assistant.Core
{
    public class OverheadMessage
    {
        public string SearchMessage { get; set; }
        public string MessageOverhead { get; set; }
        public int Hue { get; set; }
        public int Sound { get; set; } = -1;
    }

    public static class OverheadManager
    {
        public static List<OverheadMessage> OverheadMessages { get; } = new List<OverheadMessage>();

        private static bool m_Initialized;

        public static void Initialize()
        {
            if (m_Initialized)
                return;

            m_Initialized = true;
            ProfileSections.Register("overheadmessages", Load, Save, ClearAll);
        }

        public static void Save(XmlWriter xml)
        {
            foreach (OverheadMessage message in OverheadMessages)
            {
                xml.WriteStartElement("overheadmessage");
                xml.WriteAttributeString("searchtext", message.SearchMessage);
                xml.WriteAttributeString("message", message.MessageOverhead);
                xml.WriteAttributeString("hue", message.Hue.ToString());
                xml.WriteAttributeString("sound", message.Sound.ToString());
                xml.WriteEndElement();
            }
        }

        public static void Load(XmlElement node)
        {
            ClearAll();

            if (node == null)
                return;

            try
            {
                foreach (XmlElement el in node.GetElementsByTagName("overheadmessage"))
                {
                    OverheadMessage overheadMessage = new OverheadMessage
                    {
                        MessageOverhead = el.GetAttribute("message"),
                        SearchMessage = el.GetAttribute("searchtext"),
                        Hue = string.IsNullOrEmpty(el.GetAttribute("hue"))
                            ? 68
                            : Convert.ToInt32(el.GetAttribute("hue")),
                        Sound = string.IsNullOrEmpty(el.GetAttribute("sound"))
                            ? -1
                            : Convert.ToInt32(el.GetAttribute("sound"))
                    };

                    OverheadMessages.Add(overheadMessage);
                }
            }
            catch
            {
                // ignored (wie CE: defektes Profil bricht das Laden nicht ab)
            }
        }

        public static void ClearAll()
        {
            OverheadMessages.Clear();
        }

        public static void Remove(string searchText)
        {
            foreach (OverheadMessage message in OverheadMessages)
            {
                if (message.SearchMessage.Equals(searchText))
                {
                    OverheadMessages.Remove(message);
                    break;
                }
            }
        }

        /// <summary>CE 1:1: prueft eine eingehende Systemmeldung gegen die
        /// Trigger und zeigt die gepflegte Fassung ueber dem Spieler an.</summary>
        public static void DisplayOverheadMessage(string text)
        {
            if (string.IsNullOrEmpty(text) || World.Player == null)
                return;

            if (!Config.GetBool("ShowOverheadMessages") || OverheadMessages.Count == 0)
                return;

            string overheadFormat = Config.GetString("OverheadFormat");

            foreach (OverheadMessage message in OverheadMessages)
            {
                if (text.IndexOf(message.SearchMessage, StringComparison.OrdinalIgnoreCase) != -1)
                {
                    string ohMessage = overheadFormat.Replace("{msg}", message.MessageOverhead);
                    string[] splitText = text.Split(' ');

                    // CE: {1}..{n} stehen fuer die Woerter der Original-Meldung.
                    for (int wordNum = 1; wordNum < splitText.Length + 1; wordNum++)
                        ohMessage = ohMessage.Replace($"{{{wordNum}}}", splitText[wordNum - 1]);

                    World.Player.OverheadMessage(message.Hue, ohMessage);

                    if (message.Sound > -1)
                        ClientProxy.SendToClient(new PlaySound(message.Sound));

                    break;
                }
            }
        }
    }
}
