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

// Portiert aus Razor CE (Razor/Core/ContainerLabels.cs) — Option
// ShowContainerLabels: benannte Container zeigen beim Single-Click das
// eigene Label ("[Regs] (metal chest)") statt des Servernamens. Die Labels
// leben in der Profilsektion "containerlabels" (byte-kompatibel zu CE,
// alte CE-Profile bringen ihre Labels also mit).
// Der Ersatz laeuft ueber Block+Inject: das Original-Label wird geblockt
// (args.Block) und die formatierte Fassung injiziert (D23).

using System;
using System.Collections.Generic;
using System.Xml;

namespace Assistant.Core
{
    public static class ContainerLabels
    {
        public class ContainerLabel
        {
            public string Id { get; set; }
            public string Type { get; set; }
            public string Label { get; set; }
            public int Hue { get; set; }
            public string Alias { get; set; } = string.Empty;
        }

        public static Serial LastContainerLabelDisplayed;

        public static List<ContainerLabel> ContainerLabelList { get; } = new List<ContainerLabel>();

        private static bool m_Initialized;

        public static void Initialize()
        {
            if (m_Initialized)
                return;

            m_Initialized = true;
            ProfileSections.Register("containerlabels", Load, Save, ClearAll);
            MessageManager.OnLabelMessage += HandleLabelMessage;
        }

        public static void Save(XmlWriter xml)
        {
            foreach (ContainerLabel label in ContainerLabelList)
            {
                xml.WriteStartElement("containerlabel");
                xml.WriteAttributeString("id", label.Id);
                xml.WriteAttributeString("type", label.Type);
                xml.WriteAttributeString("label", label.Label);
                xml.WriteAttributeString("hue", label.Hue.ToString());
                xml.WriteAttributeString("alias", label.Alias ?? string.Empty);
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
                foreach (XmlElement el in node.GetElementsByTagName("containerlabel"))
                {
                    ContainerLabel label = new ContainerLabel
                    {
                        Id = el.GetAttribute("id"),
                        Type = el.GetAttribute("type"),
                        Label = el.GetAttribute("label"),
                        Hue = Convert.ToInt32(el.GetAttribute("hue")),
                        Alias = el.GetAttribute("alias")
                    };

                    ContainerLabelList.Add(label);
                }
            }
            catch
            {
                // ignored (wie CE: defektes Profil bricht das Laden nicht ab)
            }
        }

        private static void HandleLabelMessage(PacketReader p, PacketHandlerEventArgs args, Serial source,
            ushort graphic, MessageType type, ushort hue, ushort font, string lang, string sourceName, string text)
        {
            if (!Config.GetBool("ShowContainerLabels") || !source.IsItem)
                return;

            Item item = World.FindItem(source);

            if (item == null || !item.IsContainer)
                return;

            string tileName = ItemData.GetName(item.ItemID.Value) ?? string.Empty;

            foreach (ContainerLabel label in ContainerLabelList)
            {
                // Serial muss passen und der Text der Tiledata-Name bzw. der
                // gepflegte Alias sein (CE 1:1 — wir ersetzen nur das Namens-Label).
                if (Serial.Parse(label.Id) == source &&
                    (tileName.Equals(text) ||
                     (!string.IsNullOrEmpty(label.Alias) &&
                      label.Alias.Equals(text, StringComparison.OrdinalIgnoreCase))))
                {
                    string labelDisplay = Config.GetString("ContainerLabelFormat")
                        .Replace("{label}", label.Label)
                        .Replace("{type}", text);

                    if (Config.GetInt("ContainerLabelStyle") == 0)
                    {
                        ClientProxy.SendToClient(new AsciiMessage(source, item.ItemID.Value, MessageType.Label,
                            label.Hue, 3, Language.CliLocName, labelDisplay));
                    }
                    else
                    {
                        ClientProxy.SendToClient(new UnicodeMessage(source, item.ItemID.Value, MessageType.Label,
                            label.Hue, 3, Language.CliLocName, "", labelDisplay));
                    }

                    // Original-Label blocken — die formatierte Fassung ersetzt es.
                    args.Block = true;

                    LastContainerLabelDisplayed = source;
                    break;
                }
            }
        }

        public static void ClearAll()
        {
            ContainerLabelList.Clear();
        }
    }
}
