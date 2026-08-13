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

// UOSagas-Razor: Kompatibilitaets-Shims fuer den VScript-Port.
//
// Die Node-Implementierungen stammen 1:1 aus dem integrierten Assistant
// (ClassicUO) und rufen dort Client-APIs (GameActions, NetClient.Socket,
// TargetManager, Message). Damit der Node-Code moeglichst diff-arm zum
// Client bleibt (Updates lassen sich leicht nachziehen), bilden diese Shims
// die Client-Oberflaeche auf die Razor-APIs ab:
//   GameActions.*      -> DragDropManager/PlayerData/ClientProxy/Pakete
//   NetClient.Socket.* -> ClientProxy.SendToServer(Pakete)
//   TargetManager      -> Assistant.Targeting (LastTargetInfo als Adapter)
//   Message.*          -> World.Player.SendMessage
// Enforcement-Anmerkung: ScriptingRestrictions prueft bei uns der CLIENT an
// der ABI-Grenze (D2/D10) — die Client-seitigen Checks entfallen im Port.

using System;

namespace Assistant.VScripts.Core;

/// <summary>Client-kompatible Sicht auf das letzte Target (ClassicUO
/// LastTargetInfo). Wird on-demand aus Assistant.Targeting gebaut.</summary>
public class LastTargetInfo
{
    public bool IsEntity => Serial != 0 && Serial < 0x80000000;
    public bool IsStatic => !IsEntity && Graphic != 0 && Graphic != 0xFFFF;
    public bool IsLand => !IsStatic;
    public ushort Graphic;
    public uint Serial;
    public ushort X, Y;
    public sbyte Z;

    public void SetEntity(uint serial)
    {
        Serial = serial;
        Graphic = 0xFFFF;
        X = Y = 0xFFFF;
        Z = sbyte.MinValue;
    }

    public void SetStatic(ushort graphic, ushort x, ushort y, sbyte z)
    {
        Serial = 0;
        Graphic = graphic;
        X = x;
        Y = y;
        Z = z;
    }

    public void SetLand(ushort x, ushort y, sbyte z)
    {
        Serial = 0;
        Graphic = 0xFFFF;
        X = x;
        Y = y;
        Z = z;
    }

    public void Clear()
    {
        Serial = 0;
        Graphic = 0xFFFF;
        X = Y = 0xFFFF;
        Z = sbyte.MinValue;
    }

    /// <summary>Baut die Client-Sicht aus dem Razor-Targeting.</summary>
    public static LastTargetInfo FromRazor()
    {
        var info = new LastTargetInfo();
        TargetInfo t = Targeting.LastTargetInfo;

        if (t != null)
        {
            info.Serial = t.Serial;
            info.Graphic = t.Gfx;
            info.X = (ushort) t.X;
            info.Y = (ushort) t.Y;
            info.Z = (sbyte) t.Z;
        }
        else
        {
            info.Clear();
        }

        return info;
    }
}

/// <summary>Shim fuer ClassicUO TargetManager (Instanzsicht der Nodes).</summary>
public class VScriptTargetManager
{
    public static readonly VScriptTargetManager Instance = new();

    public bool IsTargeting => Targeting.HasTarget;

    public LastTargetInfo LastTargetInfo => Core.LastTargetInfo.FromRazor();

    /// <summary>Client: TargetManager.SelectedTarget (Get/SetSelectedTarget-Nodes)
    /// — im Port ein reiner VScript-Zustand.</summary>
    public uint SelectedTarget { get; set; }

    public void Target(uint serial)
    {
        Targeting.Target((Serial) serial);
    }

    public void CancelTarget()
    {
        Targeting.CancelTarget();
    }

    // ---- Lua-API-Ergaenzungen (Phase 4b, D27) --------------------------

    private uint _clientPickSerial;

    /// <summary>Lua Targeting.GetNewTarget: client-seitiger Zielcursor ueber
    /// OneTimeTarget; das Ergebnis hat in LastTargetInfo Vorrang, bis der
    /// naechste Pick startet (der Lua-Poll vergleicht LastTargetInfo.Serial).</summary>
    public void BeginClientSidePick()
    {
        _clientPickSerial = 0;
        Targeting.OneTimeTarget((location, serial, pt, gfx) =>
        {
            if (!location)
                _clientPickSerial = serial;
        });
    }

    public LastTargetInfo LastTargetInfoWithPick
    {
        get
        {
            if (_clientPickSerial != 0)
            {
                var info = new LastTargetInfo();
                info.SetEntity(_clientPickSerial);
                return info;
            }

            return LastTargetInfo;
        }
    }

    /// <summary>Roh-Ergebnis des laufenden Client-Picks (0 = noch keiner).
    /// Fuer Lua GetNewTarget — der alte Serial-VERGLEICH gegen den letzten
    /// Pick erkannte einen erneuten Klick auf DASSELBE Objekt nie.</summary>
    public uint ClientPickSerial => _clientPickSerial;

    /// <summary>Lua Target.Last fuer Static/Land: letztes Target wiederholen.</summary>
    public void TargetLast()
    {
        Targeting.LastTarget();
    }
}

/// <summary>Shim fuer ClassicUO GameActions.</summary>
public static class GameActions
{
    // ⚠ Zeit-Semantik-Unterschied zum Client: dessen GameActions.PickUp setzt
    // ItemHold SOFORT (Cursor haelt das Item synchron); unser Lift laeuft ueber
    // die DragDropManager-Queue (Object-Delay-getaktet) — Holding ist erst
    // gesetzt, wenn die Queue den Lift wirklich ausfuehrt. Scripts machen aber
    // PickUp -> Pause -> DropInBackpack und erwarten Client-Verhalten. Deshalb:
    // _lastDragged merkt sich den letzten Script-Lift, HeldOrQueued loest
    // "gehalten ODER noch in der Queue" auf, und DropItem PAART den Drop ueber
    // DragDropManager.Drop mit dem wartenden Lift (CE-Mechanismus), statt ein
    // rohes DropRequest zu senden, das ins Leere geht.
    private static Item _lastDragged;

    /// <summary>Client: GameCursor.ItemHold — Razor: das gehaltene Item oder
    /// das zuletzt gelifte, dessen Lift noch in der Queue wartet.</summary>
    public static Item HeldOrQueued
    {
        get
        {
            Item holding = DragDropManager.Holding;
            if (holding != null)
                return holding;

            Item last = _lastDragged;
            if (last != null &&
                (DragDropManager.HasDragFor(last.Serial) || DragDropManager.Pending == last.Serial))
                return last;

            return null;
        }
    }

    /// <summary>Client: nimmt das Item an den Cursor. Razor: Lift ueber den
    /// DragDropManager (Drops werden ueber HeldOrQueued/DropItem gepaart).</summary>
    public static bool PickUp(uint serial, int x, int y, int amount = -1)
    {
        Item item = World.FindItem(serial);
        return PickUp(item, x, y, amount);
    }

    public static bool PickUp(Item item, int x, int y, int amount = -1)
    {
        if (item == null)
            return false;

        DragDropManager.Drag(item, amount <= 0 ? item.Amount : amount);
        _lastDragged = item;
        return true;
    }

    public static void DropItem(uint serial, int x, int y, int z, uint container)
    {
        // Ground-Drop: Container -1 (Lua uebergibt 0xFFFFFFFF, VScript 0).
        Serial dest = container == 0 || container == 0xFFFFFFFF
            ? Serial.MinusOne
            : (Serial) container;
        var pt = new Point3D(x, y, z);

        // Lift wartet evtl. noch in der Queue: Drop paaren — der Manager
        // sendet ihn dann direkt nach dem Lift (und raeumt Pending/Holding auf).
        Item item = World.FindItem(serial);
        if (item != null && DragDropManager.Drop(item, dest, pt))
        {
            if (_lastDragged != null && _lastDragged.Serial == item.Serial)
                _lastDragged = null;
            return;
        }

        // Kein Lift fuer diese Serial bekannt — roh senden (Client-Verhalten).
        ClientProxy.SendToServer(new DropRequest((Serial) serial, pt, dest));
    }

    public static void Say(string message, ushort hue = 0x3B2, MessageType type = MessageType.Regular, byte font = 3)
    {
        ClientProxy.SendToServer(new ClientUniMessage(type, hue, font, Language.CliLocName, null, message));
    }

    public static void CastSpell(int index)
    {
        ClientProxy.CastSpell(index);
    }

    public static void UseSkill(int index)
    {
        ClientProxy.SendToServer(new UseSkill(index));
    }

    public static void SingleClick(uint serial)
    {
        ClientProxy.SendToServer(new SingleClick((Serial) serial));
    }

    public static void DoubleClick(uint serial)
    {
        PlayerData.DoubleClick((Serial) serial, true);
    }

    public static void MessageOverhead(string message, ushort hue, uint serial)
    {
        Mobile m = World.FindMobile(serial);
        if (m != null)
        {
            m.OverheadMessage(hue, message);
            return;
        }

        Item i = World.FindItem(serial);
        if (i != null)
            ClientProxy.SendToClient(new UnicodeMessage(i.Serial, i.ItemID.Value, MessageType.Regular,
                hue, 3, Language.CliLocName, i.Name ?? string.Empty, message));
        else
            World.Player?.OverheadMessage(hue, message);
    }

    // ---- Lua-API-Ergaenzungen (Phase 4b, D27) --------------------------

    public static void Rename(uint serial, string name)
    {
        ClientProxy.SendToServer(new RenamePacket(serial, name));
    }

    public static void Attack(uint serial)
    {
        ClientProxy.SendToServer(new AttackReq((Serial) serial));
    }

    public static void ToggleWarMode(PlayerData player)
    {
        ClientProxy.SendToServer(new SetWarMode(!(player?.Warmode ?? false)));
    }

    /// <summary>Client: Party-Chat (Send_PartyMessage) — 0xBF sub 0x06,
    /// serial=0 Broadcast, sonst private Nachricht an das Mitglied.</summary>
    public static void SayParty(string message, uint serial = 0)
    {
        ClientProxy.SendToServer(new PartyMessage(message, (Serial) serial));
    }

    public static void ChangeSkillLockStatus(int skillIndex, LockType lockType)
    {
        ClientProxy.SendToServer(new SetSkillLock(skillIndex, lockType));
    }

    /// <summary>Client: equippt das Cursor-Item. Razor: Drop des gehaltenen
    /// (oder noch queued-geliften) Items auf den Spieler mit seiner Layer.</summary>
    public static void Equip()
    {
        Item item = HeldOrQueued;
        if (item != null && World.Player != null)
        {
            if (DragDropManager.Drop(item, World.Player, item.Layer) &&
                _lastDragged != null && _lastDragged.Serial == item.Serial)
                _lastDragged = null;
        }
    }
}

/// <summary>Shim fuer die Client-Klasse Message (Statuszeilen-Ausgabe).</summary>
public static class Message
{
    public static void Error(string msg)
    {
        World.Player?.SendMessage(MsgLevel.Error, msg);
    }

    public static void Info(string msg)
    {
        World.Player?.SendMessage(MsgLevel.Info, msg);
    }

    public static void Success(string msg)
    {
        World.Player?.SendMessage(MsgLevel.Force, msg);
    }

    public static void Warning(string msg)
    {
        World.Player?.SendMessage(MsgLevel.Warning, msg);
    }
}

/// <summary>Shim fuer ClassicUO.Assistant.Data — die Item-Restriktionen prueft
/// bei uns der CLIENT an der ABI-Grenze (0x06/0x07/0x13-Enforcement, D2/D10);
/// der Node-seitige Vorab-Check ist deshalb immer "erlaubt".</summary>
public static class AssistantData
{
    public static class ScriptingRestrictions
    {
        public static bool IsItemTypeAllowed(ushort graphic) => true;
        public static bool IsItemSearchable(ushort graphic) => true;
    }
}

/// <summary>Shim fuer den Client-ItemPickupFilter (BlockLiftList) — das
/// Enforcement sitzt im Client an der ABI (0x07), hier immer frei.</summary>
public static class ItemPickupFilter
{
    public static bool IsItemBlocked(uint serial) => false;
}

/// <summary>
/// Shim fuer ClassicUO UIManager: liefert die offenen Server-Gumps aus dem
/// Razor-Weltmodell (PlayerData.GumpList). Serial-Belegung EXAKT wie der
/// Client (PacketHandlers.CreateGump: new Gump(world, sender, gumpID)):
/// LocalSerial = Absender-Serial, ServerSerial = GumpID (TypeID) — Scripts
/// adressieren Gumps ueber die GumpID (Gumps.WaitForGump(gumpId, ...)).
/// OnButtonClick antwortet wie das gumpresponse-Script-Kommando
/// (Antwort an den Server + CloseGump-Inject).
/// </summary>
public static class UIManager
{
    public static System.Collections.Generic.List<GumpShim> Gumps
    {
        get
        {
            var list = new System.Collections.Generic.List<GumpShim>();
            PlayerData player = World.Player;
            if (player == null)
                return list;

            // GumpList: Key = GumpID, Value.GumpSerial = Absender-Serial.
            foreach (var pair in player.GumpList)
                list.Add(new GumpShim(pair.Key, pair.Value.GumpSerial));

            return list;
        }
    }
}

public class GumpShim
{
    /// <summary>Absender-Serial des Gumps (Client: LocalSerial = sender).</summary>
    public uint LocalSerial { get; }

    /// <summary>GumpID/TypeID (Client: ServerSerial = gumpID) — hierueber
    /// matchen die Script-APIs (WaitForGump/HasGump/Reply).</summary>
    public uint ServerSerial { get; }

    public GumpShim(uint gumpId, uint senderSerial)
    {
        LocalSerial = senderSerial;
        ServerSerial = gumpId;
    }

    // Lua-API-Oberflaeche (LuaGumpsAPI): der Client-Gump hat Position/Groesse
    // und einen Control-Baum — unser Paket-Modell kennt nur Serial + Texte.
    public bool IsDisposed => false;
    public int X => 0;
    public int Y => 0;
    public int Width => 0;
    public int Height => 0;

    /// <summary>Sichtbare Gump-Texte (aus PlayerData.GumpList/GumpInfo.Strings).</summary>
    public System.Collections.Generic.List<string> Texts
    {
        get
        {
            PlayerData player = World.Player;
            if (player != null && player.GumpList.TryGetValue(ServerSerial, out var info))
                return new System.Collections.Generic.List<string>(info.Strings);

            return new System.Collections.Generic.List<string>();
        }
    }

    /// <summary>
    /// Die ROHE Stringtabelle des Gump-Pakets — nur die Texte, die der
    /// Server geschrieben hat, in seiner Reihenfolge. Texts stellt dem
    /// erst alle aufgeloesten Clilocs voran (Razor-CE-Erbe) und taugt
    /// deshalb nicht zum positionsgenauen Auslesen von Listen (z. B. der
    /// 16 Eintraege eines Runenbuchs).
    /// </summary>
    public System.Collections.Generic.List<string> RawTexts
    {
        get
        {
            PlayerData player = World.Player;
            if (player != null && player.GumpList.TryGetValue(ServerSerial, out var info))
                return new System.Collections.Generic.List<string>(info.RawStrings);

            return new System.Collections.Generic.List<string>();
        }
    }

    /// <summary>Gump nur schliessen (Client: Gump.Dispose) — ohne Server-Antwort.</summary>
    public void Dispose()
    {
        ClientProxy.SendToClient(new CloseGump(ServerSerial));

        PlayerData player = World.Player;
        if (player != null)
        {
            player.GumpList.Remove(ServerSerial);
            player.HasGump = false;
            player.HasCompressedGump = false;
        }
    }

    public void OnButtonClick(int buttonId)
    {
        OnButtonClick(buttonId, System.Array.Empty<int>(), System.Array.Empty<GumpTextEntry>());
    }

    /// <summary>Antwort mit Switches (Radio-/Checkbox-Auswahl) und Texteintraegen —
    /// noetig fuer Gumps, deren OK-Button erst mit der Auswahl Sinn ergibt
    /// (z. B. das Moongate-Ziel-Gump).</summary>
    public void OnButtonClick(int buttonId, int[] switches, GumpTextEntry[] entries)
    {
        ClientProxy.SendToClient(new CloseGump(ServerSerial));
        // 0xB1: (Absender-Serial, GumpID, Button, Switches, Texte) — wie gumpresponse.
        ClientProxy.SendToServer(new GumpResponse(LocalSerial, ServerSerial,
            buttonId, switches ?? System.Array.Empty<int>(),
            entries ?? System.Array.Empty<GumpTextEntry>()));

        PlayerData player = World.Player;
        if (player != null)
        {
            player.GumpList.Remove(ServerSerial);
            player.HasGump = false;
            player.HasCompressedGump = false;
        }
    }
}

/// <summary>Shim fuer NetClient.Socket.Send_*-Aufrufe der Nodes.</summary>
public static class NetClient
{
    public static readonly SocketShim Socket = new();

    public class SocketShim
    {
        /// <summary>0xBF sub 0x2C — Targeted Item Use (z. B. Bandage auf Ziel).</summary>
        public void Send_TargetSelectedObject(uint serial, uint targetSerial)
        {
            ClientProxy.SendToServer(new TargetSelectedObject(serial, targetSerial));
        }

        public void Send_AttackRequest(uint serial)
        {
            ClientProxy.SendToServer(new AttackReq((Serial) serial));
        }

        public void Send_ChangeWarMode(bool warMode)
        {
            ClientProxy.SendToServer(new SetWarMode(warMode));
        }

        public void Send_DoubleClick(uint serial)
        {
            PlayerData.DoubleClick((Serial) serial, true);
        }

        public void Send_EquipRequest(uint serial, Layer layer, uint container)
        {
            ClientProxy.SendToServer(new EquipRequest((Serial) serial, (Serial) container, layer));
        }
    }
}
