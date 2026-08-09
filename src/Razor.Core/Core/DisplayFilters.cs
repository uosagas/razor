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

// UOSagas-Razor: Anzeige-Filter (Razor CE: Paket-Umfaerbung in Handlers.cs /
// MessageManager / Spells).
//
// CE patcht die Bytes IN-PLACE (p.Seek + p.Write), weil es im Paketstrom
// sitzt. Unser Mirror ist read-only: der Host kopiert den Puffer und schreibt
// nichts zurueck. Der Weg hier: Original BLOCKEN (args.Block -> OnPacket false
// -> Client verwirft) und eine GEPATCHTE KOPIE injizieren. Injizierte Pakete
// laufen im Client ueber den Plugins-Puffer (allowPlugins=false) und damit
// NICHT erneut durch den Mirror — keine Schleife. Das Weltmodell des Ports
// hat das Original zu diesem Zeitpunkt schon verarbeitet (Viewer laufen vor
// dem Block), es behaelt also die ECHTEN Hues; nur die Anzeige aendert sich.
//
// Abgedeckt (alles CE-werkgetreu):
//  * ForceSpeechHue  — Sprech-Hue fremder Mobiles vereinheitlichen (0x1C/0xAE)
//  * ForceSpellHue   — Spell-Powerwords nach Schule einfaerben (0x1C Typ Spell)
//  * OverrideSpellFormat — Powerwords-Text ersetzen ("{power} [{spell}]")
//  * LTHilight       — Last Target einfaerben (0x77/0x78/0x20/0x2E)

using System;
using System.Text;

namespace Assistant.Core
{
    public static class DisplayFilters
    {
        public static void Initialize()
        {
            PacketHandler.RegisterServerToClientViewer(0x1C, AsciiSpeech);
            PacketHandler.RegisterServerToClientViewer(0xAE, UnicodeSpeech);
            PacketHandler.RegisterServerToClientViewer(0x77, MobileMoving);
            PacketHandler.RegisterServerToClientViewer(0x78, MobileIncoming);
            PacketHandler.RegisterServerToClientViewer(0x20, MobileUpdate);
            PacketHandler.RegisterServerToClientViewer(0x2E, EquipmentUpdate);
        }

        // ------------------------------------------------------------------ helpers

        /// <summary>
        /// Blockt das Original und injiziert eine Kopie mit gepatchtem Hue
        /// (2 Bytes big-endian an absoluter Paketposition).
        /// </summary>
        private static void PatchHue(PacketReader p, PacketHandlerEventArgs args, int absOffset, ushort hue)
        {
            byte[] copy = p.CopyBytes(0, p.Length);
            if (copy == null || copy.Length < absOffset + 2)
                return;

            copy[absOffset] = (byte)(hue >> 8);
            copy[absOffset + 1] = (byte)hue;

            if (ClientProxy.SendToClient(copy))
                args.Block = true;
        }

        // ------------------------------------------------------------------ speech

        // 0x1C: id(1) len(2) serial(4) body(2) type(1) hue(2) font(2) name(30) text(asciiz)
        private const int SpeechHueOffset = 10;

        private static void AsciiSpeech(PacketReader p, PacketHandlerEventArgs args)
        {
            if (World.Player == null)
                return;

            Serial serial = p.ReadUInt32();
            p.ReadUInt16(); // body
            MessageType type = (MessageType)p.ReadByte();
            ushort hue = p.ReadUInt16();

            if (type == MessageType.Spell)
            {
                p.ReadUInt16(); // font
                p.ReadStringSafe(30); // name
                string text = p.ReadStringSafe().Trim();
                HandleSpellSpeech(p, args, serial, hue, text);
                return;
            }

            HandleSpeechHue(p, args, serial, type);
        }

        // 0xAE: id(1) len(2) serial(4) body(2) type(1) hue(2) font(2) lang(4) name(30) text(unicode)
        private static void UnicodeSpeech(PacketReader p, PacketHandlerEventArgs args)
        {
            if (World.Player == null)
                return;

            Serial serial = p.ReadUInt32();
            p.ReadUInt16(); // body
            MessageType type = (MessageType)p.ReadByte();
            ushort hue = p.ReadUInt16();

            if (type == MessageType.Spell)
            {
                p.ReadUInt16(); // font
                p.ReadStringSafe(4); // lang
                p.ReadStringSafe(30); // name
                string text = p.ReadUnicodeStringSafe().Trim();
                HandleSpellSpeech(p, args, serial, hue, text);
                return;
            }

            HandleSpeechHue(p, args, serial, type);
        }

        /// <summary>Razor CE: MessageManager.HandleMobileMessage — ForceSpeechHue.</summary>
        private static void HandleSpeechHue(PacketReader p, PacketHandlerEventArgs args, Serial source, MessageType type)
        {
            if (type != MessageType.Regular && type != MessageType.Emote &&
                type != MessageType.Whisper && type != MessageType.Yell)
                return;

            if (!source.IsMobile || source == World.Player.Serial)
                return;

            if (!Config.GetBool("ForceSpeechHue"))
                return;

            PatchHue(p, args, SpeechHueOffset, (ushort)Config.GetInt("SpeechHue"));
        }

        /// <summary>Razor CE: Spells.HandleSpellMessage — OverrideSpellFormat + ForceSpellHue.</summary>
        private static void HandleSpellSpeech(PacketReader p, PacketHandlerEventArgs args,
            Serial source, ushort hue, string text)
        {
            Spell spell = Spell.Get(text);

            if (Config.GetBool("OverrideSpellFormat") && spell != null)
            {
                var sb = new StringBuilder(Config.GetString("SpellFormat"));
                sb.Replace(@"{power}", spell.WordsOfPower);
                sb.Replace(@"{spell}", spell.PlainName);
                sb.Replace(@"{name}", spell.PlainName);
                sb.Replace(@"{circle}", spell.Circle.ToString());

                string newText = sb.ToString();

                if (!string.IsNullOrEmpty(newText) && newText != text)
                {
                    Mobile m = World.FindMobile(source);
                    if (ClientProxy.SendToClient(new AsciiMessage(source, m?.Body ?? 0,
                            MessageType.Spell, spell.GetHue(hue), 3, m?.Name ?? "", newText)))
                        args.Block = true;
                    return;
                }
            }

            if (Config.GetBool("ForceSpellHue"))
            {
                int newHue = spell != null ? spell.GetHue(hue) : Config.GetInt("NeutralSpellHue");
                if (newHue != hue)
                    PatchHue(p, args, SpeechHueOffset, (ushort)newHue);
            }
        }

        // ------------------------------------------------------------------ LT-Hilight
        //
        // Hue-Offsets sind fix, weil die Sagas-Extended-Flags (v2.35+) NACH dem
        // Hue liegen. CE setzt bei Mobiles 0x8000 (Partial-Hue-Flag), bei
        // Ausruestung 0x3FFF-Maske.

        private static ushort LtHue()
        {
            int hue = Config.GetInt("LTHilight");
            return hue != 0 ? (ushort)hue : (ushort)0;
        }

        private static bool IsLt(Serial serial)
        {
            Mobile m = World.FindMobile(serial);
            return m != null && Targeting.IsLastTarget(m);
        }

        // 0x77: id(1) serial(4) body(2) x(2) y(2) z(1) dir(1) hue(2) ...
        private static void MobileMoving(PacketReader p, PacketHandlerEventArgs args)
        {
            ushort hue = LtHue();
            if (hue == 0 || World.Player == null)
                return;

            Serial serial = p.ReadUInt32();
            if (IsLt(serial))
                PatchHue(p, args, 13, (ushort)(hue | 0x8000));
        }

        // 0x78: id(1) len(2) serial(4) body(2) x(2) y(2) z(1) dir(1) hue(2) ...
        private static void MobileIncoming(PacketReader p, PacketHandlerEventArgs args)
        {
            ushort hue = LtHue();
            if (hue == 0 || World.Player == null)
                return;

            Serial serial = p.ReadUInt32();
            if (IsLt(serial))
                PatchHue(p, args, 15, (ushort)(hue | 0x8000));
        }

        // 0x20: id(1) serial(4) body(2) bodyofs(1) hue(2) ...
        private static void MobileUpdate(PacketReader p, PacketHandlerEventArgs args)
        {
            ushort hue = LtHue();
            if (hue == 0 || World.Player == null)
                return;

            Serial serial = p.ReadUInt32();
            if (IsLt(serial))
                PatchHue(p, args, 8, (ushort)(hue | 0x8000));
        }

        // 0x2E: id(1) serial(4) itemid(2) ofs(1) layer(1) container(4) hue(2)
        private static void EquipmentUpdate(PacketReader p, PacketHandlerEventArgs args)
        {
            ushort hue = LtHue();
            if (hue == 0 || World.Player == null)
                return;

            p.ReadUInt32(); // item serial
            p.ReadUInt16(); // itemid
            p.ReadSByte();  // offset
            p.ReadByte();   // layer
            Serial container = p.ReadUInt32();

            if (container.IsMobile && IsLt(container))
                PatchHue(p, args, 13, (ushort)(hue & 0x3FFF));
        }
    }
}
