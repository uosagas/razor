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

// Portiert (getrimmt) aus Razor CE (Razor/Network/Handlers.cs +
// Razor/Core/Commands.cs OnSpeech): die Viewer, die
//  a) den RECORDER fuettern (Client->Server: 0xAD Speech, 0x06 DoubleClick,
//     0x07 Lift, 0x08 Drop, 0x13 Equip, 0xB1 GumpResponse, 0x7D MenuResponse,
//     0x12 UseSkill/CastSpell, 0xBF/0x15 ContextMenu-Response) und
//  b) die WAIT-ZUSTAENDE pflegen (Server->Client: 0xB0/0xDD Gump, 0x7C Menu,
//     0xC2 Prompt, 0xBF/0x04 CloseGump, 0x27 LiftReject; 0x6C laeuft ueber
//     Targeting.Initialize) sowie SystemMessages (0x1C/0xAE) fuer
//     If/While(SysMessage).
// ENTFERNT gegenueber Razor CE (dokumentiert): QueueActions-Blocking,
// BlockDismount, ScriptManager-Recording, Razor-Kommandos ("-cmd" in 0xAD),
// Gump-Layout-Dekompression (0xDD: nur Serial/ID + Flags, keine Strings),
// Spell.OnCast-Seiteneffekte, StealthSteps, ScreenCap/MIB/Overhead.

using System;
using System.Collections;
using Assistant.Core;
using Assistant.Macros;

namespace Assistant
{
    public static class MacroHandlers
    {
        private static bool m_Initialized;

        public static void Initialize()
        {
            if (m_Initialized)
                return;

            m_Initialized = true;

            // Client -> Server (Recorder)
            PacketHandler.RegisterClientToServerViewer(0x06, new PacketViewerCallback(ClientDoubleClick));
            PacketHandler.RegisterClientToServerViewer(0x07, new PacketViewerCallback(LiftRequest));
            PacketHandler.RegisterClientToServerViewer(0x08, new PacketViewerCallback(DropRequest));
            PacketHandler.RegisterClientToServerViewer(0x12, new PacketViewerCallback(ClientTextCommand));
            PacketHandler.RegisterClientToServerViewer(0x13, new PacketViewerCallback(EquipRequest));
            PacketHandler.RegisterClientToServerViewer(0x7D, new PacketViewerCallback(MenuResponse));
            PacketHandler.RegisterClientToServerViewer(0xAD, new PacketViewerCallback(SpeechRequest));
            PacketHandler.RegisterClientToServerViewer(0xB1, new PacketViewerCallback(ClientGumpResponse));
            PacketHandler.RegisterClientToServerViewer(0xBF, new PacketViewerCallback(ExtendedClientCommand));

            // Server -> Client (Wait-Zustaende + SysMessages)
            PacketHandler.RegisterServerToClientViewer(0x27, new PacketViewerCallback(LiftReject));
            PacketHandler.RegisterServerToClientViewer(0x7C, new PacketViewerCallback(SendMenu));
            PacketHandler.RegisterServerToClientViewer(0xB0, new PacketViewerCallback(SendGump));
            PacketHandler.RegisterServerToClientViewer(0xDD, new PacketViewerCallback(CompressedGump));
            PacketHandler.RegisterServerToClientViewer(0xC2, new PacketViewerCallback(UnicodePromptReceived));
            PacketHandler.RegisterServerToClientViewer(0xBF, new PacketViewerCallback(ExtendedPacket));
            PacketHandler.RegisterServerToClientViewer(0x6F, new PacketViewerCallback(TradeRequest));
            PacketHandler.RegisterClientToServerViewer(0x02, new PacketViewerCallback(WalkRequest));
            PacketHandler.RegisterServerToClientViewer(0x1C, new PacketViewerCallback(AsciiSpeech));
            PacketHandler.RegisterServerToClientViewer(0xAE, new PacketViewerCallback(UnicodeSpeech));
            // ModernUO-Shards (UOSagas!) senden Systemmeldungen ueberwiegend als
            // Cliloc (0xC1) — ohne diesen Handler sehen insysmsg/waitforsysmsg/
            // getlabel/BandageTimer die Meldungen nicht.
            PacketHandler.RegisterServerToClientViewer(0xC1, new PacketViewerCallback(LocalizedMessage));

            // Target-Zustand (0x6C beide Richtungen)
            Targeting.Initialize();
        }

        // --- Client -> Server -------------------------------------------------

        /// <summary>Razor CE: Handlers.ClientDoubleClick (0x06).</summary>
        private static void ClientDoubleClick(PacketReader p, PacketHandlerEventArgs args)
        {
            Serial ser = p.ReadUInt32();

            // Razor CE: BlockDismount — Doppelklick auf sich selbst sitzt ab;
            // im Warmode mit Mount wird genau das geblockt (Anti-Miss-Click).
            if (Config.GetBool("BlockDismount") && World.Player != null && ser == World.Player.Serial &&
                World.Player.Warmode && World.Player.GetItemOnLayer(Layer.Mount) != null)
            {
                World.Player.SendMessage(MsgLevel.Force, "Dismount blocked (war mode).");
                args.Block = true;
                return;
            }

            // Razor CE: QueueActions — der direkte Client-Doppelklick wird
            // geblockt und laeuft stattdessen ueber die ActionQueue (Object-
            // Delay-Pacing). CE-DoubleClick "returnt" immer false -> Block.
            if (Config.GetBool("QueueActions"))
            {
                PlayerData.DoubleClick((Serial) ser, false);
                args.Block = true;
            }

            if (ser.IsItem && World.Player != null)
                World.Player.LastObject = ser;

            if (MacroManager.AcceptActions)
            {
                ushort gfx = 0;
                if (ser.IsItem)
                {
                    Item i = World.FindItem(ser);
                    if (i != null)
                        gfx = i.ItemID;
                }
                else
                {
                    Mobile m = World.FindMobile(ser);
                    if (m != null)
                        gfx = m.Body;
                }

                if (gfx != 0)
                {
                    MacroManager.Action(new DoubleClickAction(ser, gfx));
                }
            }
        }

        /// <summary>Razor CE: Handlers.LiftRequest (0x07).</summary>
        private static void LiftRequest(PacketReader p, PacketHandlerEventArgs args)
        {
            Serial serial = p.ReadUInt32();
            ushort amount = p.ReadUInt16();

            Item item = World.FindItem(serial);
            ushort iid = 0;

            if (item != null)
                iid = item.ItemID;

            if (MacroManager.AcceptActions)
            {
                MacroManager.Action(new LiftAction(serial, amount, iid));
            }
        }

        /// <summary>Razor CE: Handlers.DropRequest (0x08).</summary>
        private static void DropRequest(PacketReader p, PacketHandlerEventArgs args)
        {
            Serial iser = p.ReadUInt32();
            int x = p.ReadInt16();
            int y = p.ReadInt16();
            int z = p.ReadSByte();
            if (Engine.UsePostKRPackets)
                /* grid num */
                p.ReadByte();
            Point3D newPos = new Point3D(x, y, z);
            Serial dser = p.ReadUInt32();

            if (MacroManager.AcceptActions)
                MacroManager.Action(new DropAction(dser, newPos));
        }

        /// <summary>Razor CE: Handlers.EquipRequest (0x13) — als DropAction mit Layer.</summary>
        private static void EquipRequest(PacketReader p, PacketHandlerEventArgs args)
        {
            Serial iser = p.ReadUInt32(); // item being dropped serial
            Layer layer = (Layer) p.ReadByte();
            Serial mser = p.ReadUInt32();

            Item item = World.FindItem(iser);

            if (MacroManager.AcceptActions)
            {
                if (layer == Layer.Invalid || layer > Layer.LastValid)
                {
                    // Razor CE faellt zusaetzlich auf ItemID.ItemData.Quality
                    // (Tiledata) zurueck — ohne Tiledata nur item.Layer.
                    if (item != null)
                        layer = item.Layer;
                }

                if (layer > Layer.Invalid && layer <= Layer.LastUserValid)
                    MacroManager.Action(new DropAction(mser, Point3D.Zero, layer));
            }
        }

        /// <summary>Razor CE: Handlers.ClientTextCommand (0x12) — UseSkill/CastSpell-Recording.</summary>
        private static void ClientTextCommand(PacketReader p, PacketHandlerEventArgs args)
        {
            int type = p.ReadByte();
            string command = p.ReadString();

            switch (type)
            {
                case 0x24: // Use skill
                {
                    int skillIndex;

                    try
                    {
                        skillIndex = Convert.ToInt32(command.Split(' ')[0]);
                    }
                    catch
                    {
                        break;
                    }

                    if (World.Player != null)
                        World.Player.LastSkill = skillIndex;

                    if (MacroManager.AcceptActions)
                        MacroManager.Action(new UseSkillAction(skillIndex));

                    break;
                }

                case 0x27: // Cast spell from book
                {
                    try
                    {
                        string[] split = command.Split(' ');

                        if (split.Length > 0)
                        {
                            ushort spellID = Convert.ToUInt16(split[0]);
                            uint serial = split.Length > 1 ? Convert.ToUInt32(split[1]) : 0xFFFFFFFF;

                            if (World.Player != null)
                                World.Player.LastSpell = spellID;

                            if (MacroManager.AcceptActions)
                                MacroManager.Action(new BookCastSpellAction(spellID, serial));
                        }
                    }
                    catch
                    {
                    }

                    break;
                }

                case 0x56: // Cast spell from macro
                {
                    try
                    {
                        ushort spellID = Convert.ToUInt16(command);

                        if (World.Player != null)
                            World.Player.LastSpell = spellID;

                        if (MacroManager.AcceptActions)
                            MacroManager.Action(new MacroCastSpellAction(spellID));
                    }
                    catch
                    {
                    }

                    break;
                }
            }
        }

        /// <summary>Razor CE: Handlers.MenuResponse (0x7D, Client -> Server).</summary>
        private static void MenuResponse(PacketReader p, PacketHandlerEventArgs args)
        {
            if (World.Player == null)
                return;

            uint serial = p.ReadUInt32();
            ushort menuID = p.ReadUInt16();
            ushort index = p.ReadUInt16();
            ushort itemID = p.ReadUInt16();
            ushort hue = p.ReadUInt16();

            World.Player.HasMenu = false;
            if (MacroManager.AcceptActions)
                MacroManager.Action(new MenuResponseAction(index, itemID, hue));
        }

        /// <summary>
        /// Razor CE: Command.OnSpeech (0xAD-Filter in Commands.cs). Der Port
        /// zeichnet nur auf; das "-command"-Handling entfaellt (noch keine
        /// Razor-Kommandos).
        /// </summary>
        private static void SpeechRequest(PacketReader p, PacketHandlerEventArgs args)
        {
            if (World.Player == null)
                return;

            MessageType type = (MessageType) p.ReadByte();
            ushort hue = p.ReadUInt16();
            ushort font = p.ReadUInt16();
            string lang = p.ReadString(4);
            string text;
            ArrayList keys = null;

            World.Player.SpeechHue = hue;

            if ((type & MessageType.Encoded) != 0)
            {
                int value = p.ReadInt16();
                int count = (value & 0xFFF0) >> 4;
                keys = new ArrayList();
                keys.Add((ushort) value);

                for (int i = 0; i < count; ++i)
                {
                    if ((i & 1) == 0)
                    {
                        keys.Add(p.ReadByte());
                    }
                    else
                    {
                        keys.Add(p.ReadByte());
                        keys.Add(p.ReadByte());
                    }
                }

                text = p.ReadUTF8StringSafe();
                type &= ~MessageType.Encoded;
            }
            else
            {
                text = p.ReadUnicodeStringSafe();
            }

            text = text.Trim();

            if (text.Length > 0)
            {
                MacroManager.Action(new SpeechAction(type, hue, font, lang, keys, text));
            }
        }

        /// <summary>Razor CE: Handlers.ClientGumpResponse (0xB1).</summary>
        private static void ClientGumpResponse(PacketReader p, PacketHandlerEventArgs args)
        {
            if (World.Player == null)
                return;

            Serial ser = p.ReadUInt32();
            uint gumpId = p.ReadUInt32();
            int buttonId = p.ReadInt32();

            World.Player.GumpList.Remove(gumpId); // CE: beantworteter Gump ist zu

            World.Player.HasGump = false;
            World.Player.HasCompressedGump = false;

            int switchCount = p.ReadInt32();
            if (switchCount < 0 || switchCount > 2000)
                return;

            int[] switches = new int[switchCount];
            for (int i = 0; i < switchCount; i++)
                switches[i] = p.ReadInt32();

            int ec = p.ReadInt32();
            if (ec < 0 || ec > 2000)
                return;

            GumpTextEntry[] entries = new GumpTextEntry[ec];
            for (int i = 0; i < ec; i++)
            {
                ushort id = p.ReadUInt16();
                ushort len = p.ReadUInt16();
                if (len >= 240)
                    return;
                string text = p.ReadUnicodeStringSafe(len);
                entries[i] = new GumpTextEntry(id, text);
            }

            if (MacroManager.AcceptActions)
                MacroManager.Action(new GumpResponseAction(buttonId, switches, entries));
        }

        /// <summary>Razor CE: Handlers.ExtendedClientCommand (0xBF, Client -> Server) — nur 0x15 ContextMenu-Response.</summary>
        private static void ExtendedClientCommand(PacketReader p, PacketHandlerEventArgs args)
        {
            ushort ext = p.ReadUInt16();
            switch (ext)
            {
                case 0x15: // context menu response
                {
                    Serial ser = p.ReadUInt32();
                    ushort idx = p.ReadUInt16();

                    if (MacroManager.AcceptActions)
                    {
                        // Razor CE liest den Eintragsnamen aus ent.ContextMenu
                        // (Cliloc) — ContextMenu-Cache kommt spaeter, CtxName = 0.
                        Serial entity = ser;
                        if (World.Player != null && World.Player.Serial == entity)
                            entity = Serial.Zero;

                        MacroManager.Action(new ContextMenuAction(entity, idx, 0));
                    }

                    break;
                }
            }
        }

        // --- Server -> Client -------------------------------------------------

        /// <summary>Razor CE: Handlers.LiftReject (0x27).</summary>
        private static void LiftReject(PacketReader p, PacketHandlerEventArgs args)
        {
            p.ReadByte(); // reason

            if (!DragDropManager.LiftReject())
                args.Block = true;
        }

        /// <summary>Razor CE: Handlers.SendMenu (0x7C).</summary>
        private static void SendMenu(PacketReader p, PacketHandlerEventArgs args)
        {
            if (World.Player == null)
                return;

            World.Player.CurrentMenuS = p.ReadUInt32();
            World.Player.CurrentMenuI = p.ReadUInt16();
            World.Player.HasMenu = true;

            if (MacroManager.AcceptActions && MacroManager.Action(new WaitForMenuAction(World.Player.CurrentMenuI)))
                args.Block = true;
        }

        /// <summary>Razor CE: Handlers.SendGump (0xB0).</summary>
        private static void SendGump(PacketReader p, PacketHandlerEventArgs args)
        {
            if (World.Player == null)
                return;

            World.Player.CurrentGumpS = p.ReadUInt32();
            World.Player.CurrentGumpI = p.ReadUInt32();
            World.Player.HasGump = true;
            World.Player.GumpList[World.Player.CurrentGumpI] =
                new PlayerData.GumpInfo(World.Player.CurrentGumpS);

            if (MacroManager.AcceptActions &&
                MacroManager.Action(new WaitForGumpAction(World.Player.CurrentGumpI)))
                args.Block = true;
        }

        /// <summary>
        /// Razor CE: Handlers.CompressedGump (0xDD) — inkl. ZLib-Dekompression
        /// von Layout + Text-Zeilen (PacketReader.GetCompressedReader). Die
        /// extrahierten Texte speisen gumpexists/ingump (CurrentGumpStrings +
        /// GumpList[gumpId].Strings).
        /// </summary>
        private static void CompressedGump(PacketReader p, PacketHandlerEventArgs args)
        {
            if (World.Player == null)
                return;

            World.Player.CurrentGumpS = p.ReadUInt32();
            World.Player.CurrentGumpI = p.ReadUInt32();
            World.Player.HasCompressedGump = true;

            var info = new PlayerData.GumpInfo(World.Player.CurrentGumpS);
            World.Player.GumpList[World.Player.CurrentGumpI] = info;

            if (MacroManager.AcceptActions &&
                MacroManager.Action(new WaitForGumpAction(World.Player.CurrentGumpI)))
                args.Block = true;

            // Razor CE: Layout + Text-Tabelle dekomprimieren und Texte parsen.
            try
            {
                p.ReadInt32(); // x
                p.ReadInt32(); // y

                string layout = p.GetCompressedReader().ReadString();

                int numStrings = p.ReadInt32();
                if (numStrings < 0 || numStrings > 256)
                    numStrings = 0;

                PacketReader pComp = p.GetCompressedReader();
                var textLines = new System.Collections.Generic.List<string>();
                int len;

                while (!pComp.AtEnd && textLines.Count < numStrings && (len = pComp.ReadInt16()) > 0)
                    textLines.Add(pComp.ReadUnicodeString(len));

                var strings = GumpTextParser.ExtractStrings(layout, textLines.ToArray());

                info.Strings.AddRange(strings);

                World.Player.CurrentGumpStrings.Clear();
                World.Player.CurrentGumpStrings.AddRange(strings);
                World.Player.CurrentGumpRawData = layout;
            }
            catch
            {
                // Gump-Parsing darf den Paketfluss nie stoeren.
            }
        }

        /// <summary>Razor CE: Handlers.UnicodePromptReceived (0xC2, Server -> Client).</summary>
        private static void UnicodePromptReceived(PacketReader p, PacketHandlerEventArgs args)
        {
            if (World.Player == null)
                return;

            World.Player.PromptSenderSerial = p.ReadUInt32();
            World.Player.PromptID = p.ReadUInt32();
            World.Player.PromptType = p.ReadUInt32();
            World.Player.HasPrompt = true;
            World.Player.PromptInputText = string.Empty;
        }

        /// <summary>Razor CE: Handlers.ExtendedPacket (0xBF, Server -> Client) — 0x04 CloseGump, 0x06 Party.</summary>
        private static void ExtendedPacket(PacketReader p, PacketHandlerEventArgs args)
        {
            ushort type = p.ReadUInt16();

            switch (type)
            {
                case 0x04: // close gump
                {
                    if (World.Player != null)
                    {
                        uint gumpId = p.ReadUInt32(); // gump type id
                        World.Player.HasGump = false;
                        World.Player.HasCompressedGump = false;
                        World.Player.GumpList.Remove(gumpId);
                    }

                    break;
                }

                case 0x06: // party command
                {
                    byte partyType = p.ReadByte();

                    // Razor CE: 0x07 = Einladung. BlockPartyInvites lehnt sofort
                    // ab, damit gar kein Annehmen-Cursor/Fenster aufgeht.
                    if (partyType == 0x07)
                    {
                        Serial leader = p.ReadUInt32();

                        if (Config.GetBool("BlockPartyInvites") && leader != Serial.Zero)
                        {
                            ClientProxy.SendToServer(new DeclineParty(leader));
                            World.Player?.SendMessage(MsgLevel.Info, "Party invite declined (blocked).");
                        }
                    }

                    break;
                }
            }
        }

        /// <summary>
        /// 0x02 Walk-Request (Client -> Server): haelt die Blickrichtung des
        /// Spielers im Weltmodell aktuell (kommt sonst nur sporadisch per 0x20).
        /// Bei reiner DREHUNG (Richtung wechselt, Position nicht) laeuft der
        /// Tuer-Check hier; beim Schritt uebernimmt der Positions-Callback —
        /// nie beide fuer denselben Schritt (Doppel-Toggle wuerde die Tuer
        /// wieder schliessen).
        /// </summary>
        private static void WalkRequest(PacketReader p, PacketHandlerEventArgs args)
        {
            if (World.Player == null)
                return;

            byte dir = p.ReadByte();
            Direction newDir = (Direction) (dir & 0x87); // Mask + Running-Bit
            bool turnOnly = (World.Player.Direction & Direction.Mask) != (newDir & Direction.Mask);

            World.Player.Direction = newDir;

            if (turnOnly)
                World.Player.AutoOpenDoors();
        }

        /// <summary>Razor CE: Handlers.TradeRequest (0x6F) — BlockTradeRequests verwirft das Handelsfenster.</summary>
        private static void TradeRequest(PacketReader p, PacketHandlerEventArgs args)
        {
            if (Config.GetBool("BlockTradeRequests"))
                args.Block = true;
        }

        /// <summary>
        /// 0x1C AsciiSpeech: Systemmeldungen fuer If/While(SysMessage) puffern.
        /// Razor CE laesst das ueber den MessageManager laufen (inkl. Filter).
        /// </summary>
        private static void AsciiSpeech(PacketReader p, PacketHandlerEventArgs args)
        {
            if (World.Player == null)
                return;

            Serial serial = p.ReadUInt32();
            ushort body = p.ReadUInt16(); // body/graphic
            MessageType type = (MessageType) p.ReadByte();
            ushort hue = p.ReadUInt16();
            ushort font = p.ReadUInt16();
            string name = p.ReadStringSafe(30);
            string msg = p.ReadStringSafe();

            if (!serial.IsValid || type == MessageType.System || serial == World.Player.Serial)
                SystemMessages.Add(msg);

            // Razor CE: Overhead-Trigger nur fuer echte Systemmeldungen.
            if (!serial.IsValid || type == MessageType.System)
                OverheadManager.DisplayOverheadMessage(msg);

            BandageTimer.OnSystemMessage(msg);

            // Label-Messages an den MessageManager (Script-Kommando getlabel).
            MessageManager.HandleSpeech(p, args, serial, body, type, hue, font, "A", name, msg);
        }

        /// <summary>0xAE UnicodeSpeech: Systemmeldungen fuer If/While(SysMessage) puffern.</summary>
        private static void UnicodeSpeech(PacketReader p, PacketHandlerEventArgs args)
        {
            if (World.Player == null)
                return;

            Serial serial = p.ReadUInt32();
            ushort body = p.ReadUInt16(); // body/graphic
            MessageType type = (MessageType) p.ReadByte();
            ushort hue = p.ReadUInt16();
            ushort font = p.ReadUInt16();
            string lang = p.ReadString(4);
            string name = p.ReadStringSafe(30);
            string msg = p.ReadUnicodeStringSafe();

            if (!serial.IsValid || type == MessageType.System || serial == World.Player.Serial)
                SystemMessages.Add(msg);

            // Razor CE: Overhead-Trigger nur fuer echte Systemmeldungen.
            if (!serial.IsValid || type == MessageType.System)
                OverheadManager.DisplayOverheadMessage(msg);

            BandageTimer.OnSystemMessage(msg);

            // Label-Messages an den MessageManager (Script-Kommando getlabel).
            MessageManager.HandleSpeech(p, args, serial, body, type, hue, font, lang, name, msg);
        }

        /// <summary>
        /// 0xC1 Cliloc-Message (Layout wie der authoritative Client-Parser,
        /// DisplayClilocString — keine Sagas-Abweichung). Der Text wird ueber
        /// den DataService aufgeloest (GetCliloc mit Argumenten).
        /// </summary>
        private static void LocalizedMessage(PacketReader p, PacketHandlerEventArgs args)
        {
            if (World.Player == null)
                return;

            Serial serial = p.ReadUInt32();
            ushort body = p.ReadUInt16();
            MessageType type = (MessageType) p.ReadByte();
            ushort hue = p.ReadUInt16();
            ushort font = p.ReadUInt16();
            int cliloc = (int) p.ReadUInt32();
            string name = p.ReadStringSafe(30);
            string arguments = p.ReadUnicodeStringLESafe();

            string text = ClientProxy.GetCliloc(cliloc, arguments);

            if (string.IsNullOrEmpty(text))
                text = $"[cliloc {cliloc}]";

            if (!serial.IsValid || type == MessageType.System || serial == World.Player.Serial)
                SystemMessages.Add(text);

            // Razor CE: Overhead-Trigger nur fuer echte Systemmeldungen.
            if (!serial.IsValid || type == MessageType.System)
                OverheadManager.DisplayOverheadMessage(text);

            BandageTimer.OnLocalizedMessage(cliloc);

            // Cliloc-Labels (z.B. Item-Namen beim Single-Click) an getlabel.
            MessageManager.HandleSpeech(p, args, serial, body, type, hue, font, "ENU", name, text);
        }
    }
}
