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

// Portiert aus Razor CE (Razor/Network/Packets.cs) — nur die fuer die
// Macro-Ausfuehrung (Phase 2c) und die Agents (Phase 2d) benoetigten
// Builder. Byte-Layout unveraendert. GumpTextEntry liegt bereits in
// Core/Enums.cs.

using System.Collections;
using System.Collections.Generic;

namespace Assistant
{
    public sealed class DoubleClick : Packet
    {
        public DoubleClick(Serial clicked) : base(0x06, 5)
        {
            Write((uint) clicked.Value);
        }
    }

    /// <summary>Server-Target-Request 0x6C — wird per InjectToClient benutzt (ResendTarget).</summary>
    public sealed class Target : Packet
    {
        public Target(uint tid) : this(tid, false, 0)
        {
        }

        public Target(uint tid, byte flags) : this(tid, false, flags)
        {
        }

        public Target(uint tid, bool ground) : this(tid, ground, 0)
        {
        }

        public Target(uint tid, bool ground, byte flags) : base(0x6C, 19)
        {
            Write(ground);
            Write(tid);
            Write(flags);
            Fill();
        }
    }

    /// <summary>Cursor-Abbruch im Client (0x6C mit MinusOne-TargID), per InjectToClient.</summary>
    public sealed class CancelTarget : Packet
    {
        public CancelTarget(uint tid) : base(0x6C, 19)
        {
            Write((byte) 0);
            Write(tid);
            Write((byte) 3);
            Fill();
        }
    }

    public sealed class TargetResponse : Packet
    {
        public TargetResponse(TargetInfo info) : base(0x6C, 19)
        {
            Write((byte) info.Type);
            Write((uint) info.TargID);
            Write((byte) info.Flags);
            Write((uint) info.Serial);
            Write((ushort) info.X);
            Write((ushort) info.Y);
            Write((short) info.Z);
            Write((ushort) info.Gfx);
        }

        public TargetResponse(uint id, Mobile m) : base(0x6C, 19)
        {
            Write((byte) 0x00); // target object
            Write((uint) id);
            Write((byte) 0); // flags
            Write((uint) m.Serial);
            Write((ushort) m.Position.X);
            Write((ushort) m.Position.Y);
            Write((short) m.Position.Z);
            Write((ushort) m.Body);
        }

        public TargetResponse(uint id, Item item) : base(0x6C, 19)
        {
            Write((byte) 0x00); // target object
            Write((uint) id);
            Write((byte) 0); // flags
            Write((uint) item.Serial);
            Write((ushort) item.Position.X);
            Write((ushort) item.Position.Y);
            Write((short) item.Position.Z);
            Write((ushort) item.ItemID);
        }
    }

    public sealed class TargetCancelResponse : Packet
    {
        public TargetCancelResponse(uint id) : base(0x6C, 19)
        {
            Write((byte) 0);
            Write((uint) id);
            Write((byte) 0);
            Write((uint) 0);
            Write((ushort) 0xFFFF);
            Write((ushort) 0xFFFF);
            Write((short) 0);
            Write((ushort) 0);
        }
    }

    public sealed class ClientUniMessage : Packet
    {
        public ClientUniMessage(MessageType type, int hue, int font, string lang, ArrayList keys, string text) :
            base(0xAD)
        {
            if (lang == null || lang == "") lang = "ENU";
            if (text == null) text = "";

            this.EnsureCapacity(50 + (text.Length * 2) + (keys == null ? 0 : keys.Count + 1));
            if (keys == null || keys.Count <= 1)
                Write((byte) type);
            else
                Write((byte) (type | MessageType.Encoded));
            Write((short) hue);
            Write((short) font);
            WriteAsciiFixed(lang, 4);
            if (keys != null && keys.Count > 1)
            {
                Write((ushort) keys[0]);
                for (int i = 1; i < keys.Count; i++)
                    Write((byte) keys[i]);
                WriteUTF8Null(text);
            }
            else
            {
                WriteBigUniNull(text);
            }
        }
    }

    public sealed class LiftRequest : Packet
    {
        public LiftRequest(Serial ser, int amount) : base(0x07, 7)
        {
            this.Write(ser.Value);
            this.Write((ushort) amount);
        }

        public LiftRequest(Item i, int amount) : this(i.Serial, amount)
        {
        }

        public LiftRequest(Item i) : this(i.Serial, i.Amount)
        {
        }
    }

    /// <summary>Lift-Ablehnung 0x27 — per InjectToClient (DragDropManager bei voller Queue).</summary>
    public sealed class LiftRej : Packet
    {
        public LiftRej() : this(5) // reason = Inspecific
        {
        }

        public LiftRej(byte reason) : base(0x27, 2)
        {
            Write(reason);
        }
    }

    public sealed class EquipRequest : Packet
    {
        public EquipRequest(Serial item, Mobile to, Layer layer) : base(0x13, 10)
        {
            Write(item);
            Write((byte) layer);
            Write(to.Serial);
        }

        public EquipRequest(Serial item, Serial to, Layer layer) : base(0x13, 10)
        {
            Write(item);
            Write((byte) layer);
            Write(to);
        }
    }

    public sealed class DropRequest : Packet
    {
        public DropRequest(Item item, Serial destSer) : base(0x08, 14)
        {
            if (Engine.UsePostKRPackets)
                EnsureCapacity(15);

            Write(item.Serial);
            Write((short) (-1));
            Write((short) (-1));
            Write((sbyte) 0);
            if (Engine.UsePostKRPackets)
                Write((byte) 0);
            Write(destSer);
        }

        public DropRequest(Item item, Item to) : this(item, to.Serial)
        {
        }

        public DropRequest(Serial item, Point3D pt, Serial dest) : base(0x08, 14)
        {
            if (Engine.UsePostKRPackets)
                EnsureCapacity(15);

            Write(item);
            Write((ushort) pt.X);
            Write((ushort) pt.Y);
            Write((sbyte) pt.Z);
            if (Engine.UsePostKRPackets)
                Write((byte) 0);
            Write(dest);
        }

        public DropRequest(Item item, Point3D pt, Serial destSer) : this(item.Serial, pt, destSer)
        {
        }
    }

    public sealed class GumpResponse : Packet
    {
        public GumpResponse(uint serial, uint tid, int bid, int[] switches, GumpTextEntry[] entries) : base(0xB1)
        {
            EnsureCapacity(3 + 4 + 4 + 4 + 4 + switches.Length * 4 + 4 + entries.Length * 4);

            Write((uint) serial);
            Write((uint) tid);

            Write((int) bid);

            Write((int) switches.Length);
            for (int i = 0; i < switches.Length; i++)
                Write((int) switches[i]);
            Write((int) entries.Length);
            for (int i = 0; i < entries.Length; i++)
            {
                GumpTextEntry gte = (GumpTextEntry) entries[i];
                Write((ushort) gte.EntryID);
                Write((ushort) (gte.Text.Length * 2));
                WriteBigUniFixed(gte.Text, gte.Text.Length);
            }
        }
    }

    public sealed class MenuResponse : Packet
    {
        public MenuResponse(uint serial, ushort menuid, ushort index, ushort itemid, ushort hue) : base(0x7D, 13)
        {
            Write((uint) serial);
            Write(menuid);
            Write(index);
            Write(itemid);
            Write(hue);
        }
    }

    public sealed class UseSkill : Packet
    {
        public UseSkill(int sk) : base(0x12)
        {
            string cmd = $"{sk} 0";
            EnsureCapacity(4 + cmd.Length + 1);
            Write((byte) 0x24);
            WriteAsciiNull(cmd);
        }
    }

    public sealed class ExtCastSpell : Packet
    {
        public ExtCastSpell(Serial book, ushort spell) : base(0xBF)
        {
            EnsureCapacity(1 + 2 + 2 + 2 + 4 + 2);
            Write((short) 0x1C);
            Write((short) (book.IsItem ? 1 : 2));
            if (book.IsItem)
                Write((uint) book);
            Write((short) spell);
        }
    }

    public sealed class CastSpellFromBook : Packet
    {
        public CastSpellFromBook(Serial book, ushort spell) : base(0x12)
        {
            string cmd;
            if (book.IsItem)
                cmd = $"{spell} {book.Value}";
            else
                cmd = $"{spell}";
            EnsureCapacity(3 + 1 + cmd.Length + 1);
            Write((byte) 0x27);
            WriteAsciiNull(cmd);
        }
    }

    public sealed class CastSpellFromMacro : Packet
    {
        public CastSpellFromMacro(ushort spell) : base(0x12)
        {
            string cmd = spell.ToString();
            EnsureCapacity(3 + 1 + cmd.Length + 1);
            Write((byte) 0x56);
            WriteAsciiNull(cmd);
        }
    }

    /// <summary>Gump im Client schliessen (0xBF sub 0x04), per InjectToClient.</summary>
    public sealed class CloseGump : Packet
    {
        public CloseGump(uint typeID, uint buttonID) : base(0xBF)
        {
            EnsureCapacity(13);

            Write((short) 0x04);
            Write((int) typeID);
            Write((int) buttonID);
        }

        public CloseGump(uint typeID) : base(0xBF)
        {
            EnsureCapacity(13);

            Write((short) 0x04);
            Write((int) typeID);
            Write((int) 0);
        }
    }

    public sealed class UseAbility : Packet
    {
        // ints are 'encoded' with a leading bool, if true then the number is 0, if flase then followed by all 4 bytes (lame :-)
        public UseAbility(AOSAbility a) : base(0xD7)
        {
            EnsureCapacity(1 + 2 + 4 + 2 + 4);

            Write((uint) World.Player.Serial);
            Write((ushort) 0x19);
            if (a == AOSAbility.Clear)
            {
                Write(true);
            }
            else
            {
                Write(false);
                Write((int) a);
            }
        }
    }

    public sealed class ContextMenuRequest : Packet
    {
        public ContextMenuRequest(Serial entity) : base(0xBF)
        {
            EnsureCapacity(1 + 2 + 2 + 4);
            Write((ushort) 0x13);
            Write((uint) entity);
        }
    }

    public sealed class ContextMenuResponse : Packet
    {
        public ContextMenuResponse(Serial entity, ushort idx) : base(0xBF)
        {
            EnsureCapacity(1 + 2 + 2 + 4 + 2);

            Write((ushort) 0x15);
            Write((uint) entity);
            Write((ushort) idx);
        }
    }

    /// <summary>Unicode-Meldung 0xAE (Server -> Client) — per InjectToClient (Agent-HotBag-Labels u.a.).</summary>
    /// <summary>AsciiMessage 0x1C (Server -> Client) — per InjectToClient (Spell-Format-Ersatztext, Razor CE 1:1).</summary>
    public sealed class AsciiMessage : Packet
    {
        public AsciiMessage(Serial serial, int graphic, MessageType type, int hue, int font, string name, string text)
            : base(0x1C)
        {
            if (name == null) name = "";
            if (text == null) text = "";

            if (hue == 0)
                hue = 0x3B2;

            this.EnsureCapacity(45 + text.Length);

            Write((uint) serial);
            Write((ushort) graphic);
            Write((byte) type);
            Write((ushort) hue);
            Write((ushort) font);
            WriteAsciiFixed(name, 30);
            WriteAsciiNull(text);
        }
    }

    public sealed class UnicodeMessage : Packet
    {
        public UnicodeMessage(Serial serial, int graphic, MessageType type, int hue, int font, string lang, string name,
            string text) : base(0xAE)
        {
            if (lang == null || lang == "") lang = "ENU";
            if (name == null) name = "";
            if (text == null) text = "";

            if (hue == 0)
                hue = 0x3B2;

            this.EnsureCapacity(50 + (text.Length * 2));

            Write((uint) serial);
            Write((ushort) graphic);
            Write((byte) type);
            Write((ushort) hue);
            Write((ushort) font);
            WriteAsciiFixed(lang.ToUpper(), 4);
            WriteAsciiFixed(name, 30);
            WriteBigUniNull(text);
        }
    }

    /// <summary>
    /// SA World Item 0xF3 (Server -> Client) — per InjectToClient
    /// (WallStaticFilter). WICHTIG: im UOSagas-v2.35+-Format (2-Byte-Flags +
    /// unk2, D13) — injizierte Pakete parst der Client mit dem Sagas-Parser.
    /// </summary>
    public sealed class WorldItem : Packet
    {
        public WorldItem(Item item) : base(0xF3, 27)
        {
            Write((ushort) 0x01);
            Write((byte) 0x00); // ArtDataID: TileData
            Write(item.Serial);
            Write((ushort) item.ItemID.Value);
            Write((byte) 0); // graphic increment
            Write((ushort) item.Amount);
            Write((ushort) item.Amount);
            Write((ushort) item.Position.X);
            Write((ushort) item.Position.Y);
            Write((sbyte) item.Position.Z);
            Write((byte) item.Direction);
            Write((ushort) item.Hue);
            Write((ushort) 0); // Sagas: Extended Flags (2 Byte)
            Write((ushort) 0); // Sagas: unk2
        }
    }

    /// <summary>ContainerContentUpdate 0x25 (Server -> Client) — per InjectToClient (SearchExemption-Refresh).</summary>
    public sealed class ContainerItem : Packet
    {
        public ContainerItem(Item item) : this(item, Engine.UsePostKRPackets)
        {
        }

        public ContainerItem(Item item, bool isKR) : base(0x25, 20)
        {
            if (isKR)
                EnsureCapacity(21);

            Write(item.Serial);

            Write(item.ItemID);
            Write((byte) 0);
            Write(item.Amount);
            Write((ushort) item.Position.X);
            Write((ushort) item.Position.Y);

            if (isKR)
                Write(item.GridNum);

            object cont = item.Container;
            if (cont is UOEntity)
                Write((uint) ((UOEntity) item.Container).Serial);
            else if (cont is uint)
                Write((uint) cont);
            else if (cont is Serial)
                Write((Serial) item.Container);
            else
                Write((uint) 0x7FFFFFFF);

            Write(item.Hue);
        }
    }

    public class SellListItem
    {
        public Serial Serial;
        public ushort Amount;

        public SellListItem(Serial s, ushort a)
        {
            Serial = s;
            Amount = a;
        }
    }

    public sealed class VendorSellResponse : Packet
    {
        public VendorSellResponse(Mobile vendor, List<SellListItem> list) : base(0x9F)
        {
            EnsureCapacity(1 + 2 + 4 + 2 + list.Count * 6);

            Write((uint) vendor.Serial);
            Write((ushort) list.Count);

            for (int i = 0; i < list.Count; i++)
            {
                SellListItem sli = list[i];
                Write((uint) sli.Serial);
                Write((ushort) sli.Amount);
            }
        }
    }

    public class VendorBuyItem
    {
        public VendorBuyItem(Serial ser, int amount, int price)
        {
            Serial = ser;
            Amount = amount;
            Price = price;
        }

        public readonly Serial Serial;
        public int Amount;
        public int Price;

        public int TotalCost
        {
            get { return Amount * Price; }
        }
    }

    public sealed class VendorBuyResponse : Packet
    {
        public VendorBuyResponse(Serial vendor, IList<VendorBuyItem> list) : base(0x3B)
        {
            EnsureCapacity(1 + 2 + 4 + 1 + list.Count * 7);

            Write(vendor);
            Write((byte) 0x02); // flag

            for (int i = 0; i < list.Count; i++)
            {
                VendorBuyItem vbi = list[i];
                Write((byte) 0x1A); // layer?
                Write(vbi.Serial);
                Write((ushort) vbi.Amount);
            }
        }
    }

    /// <summary>Einzelklick 0x09 (Client -> Server) — All Names/All Corpses (Razor CE: SingleClick).</summary>
    public sealed class SingleClick : Packet
    {
        public SingleClick(object clicked) : base(0x09, 5)
        {
            if (clicked is Mobile)
                Write(((Mobile) clicked).Serial);
            else if (clicked is Item)
                Write(((Item) clicked).Serial);
            else if (clicked is Serial)
                Write(((Serial) clicked).Value);
            else
                Write((uint) 0);
        }
    }

    /// <summary>Angriffs-Anforderung 0x05 (Razor CE: AttackReq) — Attack Last Target/Combatant.</summary>
    public sealed class AttackReq : Packet
    {
        public AttackReq(Serial serial) : base(0x05, 5)
        {
            Write((uint) serial);
        }
    }

    /// <summary>Resync-Anforderung 0x22 (Razor CE: ResyncReq).</summary>
    public sealed class ResyncReq : Packet
    {
        public ResyncReq() : base(0x22, 3)
        {
            Write((ushort) 0);
        }
    }

    /// <summary>0xBF sub 0x2C — Targeted Item Use ("Use item on target", z. B.
    /// Bandage direkt auf ein Ziel; Layout wie Client Send_TargetSelectedObject).</summary>
    public sealed class TargetSelectedObject : Packet
    {
        public TargetSelectedObject(uint serial, uint targetSerial) : base(0xBF)
        {
            EnsureCapacity(13);
            Write((ushort) 0x2C);
            Write(serial);
            Write(targetSerial);
        }
    }

    /// <summary>War/Peace-Modus 0x72 (Razor CE: SetWarMode) — auch per InjectToClient nutzbar.</summary>
    public sealed class SetWarMode : Packet
    {
        public SetWarMode(bool mode) : base(0x72, 5)
        {
            Write(mode);
            Write((byte) 0x00);
            Write((byte) 0x32);
            Write((byte) 0x00);
        }
    }

    /// <summary>Pre-AOS Disarm (0xBF sub 0x09, Razor CE: DisarmRequest).</summary>
    public sealed class DisarmRequest : Packet
    {
        public DisarmRequest() : base(0xBF)
        {
            EnsureCapacity(3);
            Write((ushort) 0x09);
        }
    }

    /// <summary>Pre-AOS Stun (0xBF sub 0x0A, Razor CE: StunRequest).</summary>
    public sealed class StunRequest : Packet
    {
        public StunRequest() : base(0xBF)
        {
            EnsureCapacity(3);
            Write((ushort) 0x0A);
        }
    }

    /// <summary>AOS-Ability-Anzeige im Client loeschen (0xBF sub 0x21), per InjectToClient.</summary>
    public sealed class ClearAbility : Packet
    {
        public static readonly Packet Instance = new ClearAbility();

        public ClearAbility() : base(0xBF)
        {
            EnsureCapacity(5);

            Write((short) 0x21);
        }
    }

    /// <summary>Party-Einladung annehmen (0xBF sub 0x06/0x08, Razor CE: AcceptParty).</summary>
    public sealed class AcceptParty : Packet
    {
        public AcceptParty(Serial leader) : base(0xBF)
        {
            EnsureCapacity(1 + 2 + 2 + 1 + 4);

            Write((ushort) 0x06); // party command
            Write((byte) 0x08); // accept
            Write((uint) leader);
        }
    }

    /// <summary>Open-Door-Macro (0x12/0x58, Razor CE: OpenDoorMacro) — oeffnet die Tuer vor dem Spieler serverseitig.</summary>
    public sealed class OpenDoorMacro : Packet
    {
        public OpenDoorMacro() : base(0x12)
        {
            EnsureCapacity(5);
            Write((byte) 0x58);
            Write((byte) 0);
        }
    }

    /// <summary>Party-Einladung ablehnen (0xBF sub 0x06/0x09, Razor CE: DeclineParty).</summary>
    public sealed class DeclineParty : Packet
    {
        public DeclineParty(Serial leader) : base(0xBF)
        {
            EnsureCapacity(1 + 2 + 2 + 1 + 4);

            Write((ushort) 0x06); // party command
            Write((byte) 0x09); // decline
            Write((uint) leader);
        }
    }

    /// <summary>Party-Mitglied hinzufuegen (0xBF sub 0x06/0x01, Razor CE: AddParty) — Server antwortet mit Target.</summary>
    public sealed class AddParty : Packet
    {
        public AddParty() : base(0xBF)
        {
            EnsureCapacity(1 + 2 + 2 + 1 + 4);

            Write((ushort) 0x06); // party command
            Write((byte) 0x01); // add party
            Write(0);
        }
    }

    /// <summary>Party-Chat (0xBF sub 0x06) — Format wie Client Send_PartyMessage:
    /// 0x03+Serial = private Nachricht, 0x04 = Broadcast an die Party; Text Unicode-BE nullterminiert.</summary>
    public sealed class PartyMessage : Packet
    {
        public PartyMessage(string text, Serial serial) : base(0xBF)
        {
            EnsureCapacity(1 + 2 + 2 + 1 + 4 + ((text?.Length ?? 0) + 1) * 2);

            Write((ushort) 0x06); // party command

            if (serial.IsValid)
            {
                Write((byte) 0x03); // private message
                Write((uint) serial);
            }
            else
            {
                Write((byte) 0x04); // broadcast
            }

            WriteBigUniNull(text ?? string.Empty);
        }
    }

    /// <summary>Assistant-Versionsmeldung (0xBF sub 0x40, UOSagas-Erweiterung):
    /// [len]Name[len]Version (ASCII). Wird nach dem Login-Confirm gesendet;
    /// das Server-Gate friert veraltete Razor-Versionen ein (Kick nach 30s).</summary>
    public sealed class AssistantVersion : Packet
    {
        public AssistantVersion(string name, string version) : base(0xBF)
        {
            name ??= string.Empty;
            version ??= string.Empty;

            EnsureCapacity(1 + 2 + 2 + 1 + name.Length + 1 + version.Length);

            Write((ushort) 0x40); // UOSagas: assistant version

            Write((byte) name.Length);
            WriteAsciiFixed(name, name.Length);

            Write((byte) version.Length);
            WriteAsciiFixed(version, version.Length);
        }
    }

    /// <summary>Kampfziel-Anzeige 0xAA (Server -> Client) — per InjectToClient (SetLastTargetTo, Razor CE: ChangeCombatant).</summary>
    public sealed class ChangeCombatant : Packet
    {
        public ChangeCombatant(Serial ser) : base(0xAA, 5)
        {
            Write((uint) ser);
        }

        public ChangeCombatant(Mobile m) : this(m.Serial)
        {
        }
    }

    public sealed class PromptResponse : Packet
    {
        public PromptResponse(uint serial, uint promptid, uint operation, string lang, string text)
            : base(0xC2)
        {
            if (text != "")
                EnsureCapacity(2 + 4 + 4 + 4 + 4 + (text.Length * 2));
            else
            {
                EnsureCapacity(18);
            }

            Write((uint) serial);
            Write((uint) promptid);
            Write((uint) operation);

            if (string.IsNullOrEmpty(lang))
                lang = "ENU";

            WriteAsciiFixed(lang.ToUpper(), 4);

            if (text != "")
                WriteLittleUniNull(text);
        }
    }

    // --- Script-Kommando-Pakete (Razor CE Network/Packets.cs, 1:1) -----------

    /// <summary>Razor CE: VirtueRequest — Tugend anrufen (0x12, Subkommando 0xF4).</summary>
    internal sealed class VirtueRequest : Packet
    {
        internal VirtueRequest(byte id) : base(0x12)
        {
            EnsureCapacity(1 + id.ToString().Length + 1);

            Write((byte) 0xF4);
            WriteAsciiNull(id.ToString());
        }
    }

    /// <summary>Razor CE: RenamePacket — Mobile (Pet/Follower) umbenennen (0x75).</summary>
    public sealed class RenamePacket : Packet
    {
        public RenamePacket(uint serial, string newName) : base(0x75, 35)
        {
            Write(serial);
            WriteAsciiFixed(newName, 30);
        }
    }

    /// <summary>Razor CE: SetSkillLock — Skill-Lock aendern (0x3A: up/down/locked).</summary>
    public sealed class SetSkillLock : Packet
    {
        public SetSkillLock(int skill, LockType type) : base(0x3A)
        {
            EnsureCapacity(6);
            Write((short) skill);
            Write((byte) type);
        }
    }

    /// <summary>Razor CE: PlayMusic — Musik im Client abspielen (0x6D, Client-Injektion).</summary>
    public sealed class PlayMusic : Packet
    {
        public PlayMusic(ushort num) : base(0x6D, 3)
        {
            Write(num);
        }
    }

    /// <summary>Razor CE: PlaySound — Sound an der Spielerposition (0x54, Client-Injektion).</summary>
    public sealed class PlaySound : Packet
    {
        public PlaySound(int sound) : base(0x54, 12)
        {
            Write((byte) 0x01); // 0x00=quiet/repeating, 0x01=einmalig
            Write((ushort) sound);
            Write((ushort) 0);
            Write((ushort) World.Player.Position.X);
            Write((ushort) World.Player.Position.Y);
            Write((ushort) World.Player.Position.Z);
        }
    }
}
