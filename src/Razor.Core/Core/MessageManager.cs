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

// UOSagas-Razor: minimaler MessageManager-Port (Razor CE Core/Messages.cs).
//
// Razor CE leitet alle Sprach-/Label-Pakete durch den MessageManager (inkl.
// Message-Filter). Der Port braucht davon nur den Label-Event-Teil fuer das
// Script-Kommando `getlabel`: Single-Click auf ein Objekt -> Server antwortet
// mit Label-Messages -> getlabel sammelt sie in eine Script-Variable.
//
// Gespeist von MacroHandlers.AsciiSpeech/UnicodeSpeech (0x1C/0xAE).
// Feature-Paritaet (2026-07-17): PlayEmoteSound (CE MessageManager 1:1) —
// ein *cough* etc. eines Mobiles spielt den passenden Sound lokal ab.
// TODO Razor CE: Message-Filter/MessageQueue sind nicht portiert.

using System;

namespace Assistant
{
    public static class MessageManager
    {
        public delegate void LabelMessageHandler(PacketReader p, PacketHandlerEventArgs args, Serial source,
            ushort graphic, MessageType type, ushort hue, ushort font, string lang, string sourceName, string text);

        /// <summary>Feuert fuer Label-Messages (und waehrend GetLabelCommand auch fuer Regular).</summary>
        public static event LabelMessageHandler OnLabelMessage;

        /// <summary>
        /// Razor CE: waehrend `getlabel` aktiv ist, werden auch Regular-Messages
        /// als Label behandelt (manche Server senden Label+Regular-Sequenzen).
        /// </summary>
        public static bool GetLabelCommand { get; set; }

        internal static void HandleSpeech(PacketReader p, PacketHandlerEventArgs args, Serial source,
            ushort graphic, MessageType type, ushort hue, ushort font, string lang, string sourceName, string text)
        {
            if (type == MessageType.Label || (GetLabelCommand && type == MessageType.Regular))
                OnLabelMessage?.Invoke(p, args, source, graphic, type, hue, font, lang, sourceName, text);

            if (type == MessageType.Emote)
                PlayEmoteSound(source, text);

            // VScript-Journal (JournalContains-Nodes u. a.) mitschreiben.
            VScripts.Engine.Journal.Add(sourceName, text, hue, type, lang != "A");
        }

        // Razor CE 1:1 (Core/MessageManager.cs, case MessageType.Emote): der
        // Emote-Text ohne Sterne wird gegen die klassischen Sound-Namen
        // (MaleSounds/FemaleSounds) geparst und lokal abgespielt.
        private static void PlayEmoteSound(Serial source, string text)
        {
            if (!Config.GetBool("PlayEmoteSound") || !source.IsMobile || string.IsNullOrEmpty(text))
                return;

            Mobile m = World.FindMobile(source);
            if (m == null)
                return;

            text = text.Trim('*');

            if (m.Female)
            {
                if (Enum.TryParse(text, true, out FemaleSounds sound) && sound != 0)
                    ClientProxy.SendToClient(new PlaySound((int) sound));
            }
            else
            {
                if (Enum.TryParse(text, true, out MaleSounds sound) && sound != 0)
                    ClientProxy.SendToClient(new PlaySound((int) sound));
            }
        }
    }
}
