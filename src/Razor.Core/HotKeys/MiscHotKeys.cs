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

// Portiert aus Razor CE (Razor/HotKeys/Misc.cs "UseHotKeys" + Razor/HotKeys/
// Counters.cs + ActionQueue.Initialize-Hotkey) — Phase 3c2.
//
// BEWUSST NICHT REGISTRIERT (Razor CE hat sie; Infrastruktur fehlt im Port —
// hier dokumentiert, damit der Hotkey-Baum absichtlich schlanker ist):
//  * GoldPerHotkey        (GoldPerHourTimer nicht portiert)
//  * DamageTracker        (UI-Fenster, nicht portiert)
//  * CaptureBod           (BOD-Gump-Capture nicht portiert)
//  * GumpInfo/GumpSysMsg/GumpHotKeys/GumpBoatControl (interne Razor-Gumps)
//  * ToggleMap            (Kartenfenster nicht portiert)
//  * TakeSS               (ScreenCapture nicht portiert)
//  * NextWaypoint/PrevWaypoint/HideWaypoint (WaypointManager nicht portiert)
//  * Play/Stop/Pause Script, ScriptDClickType/ScriptTargetType (Phase 4)
//  * Friends-Gruppen-Hotkeys (FriendsManager nicht portiert)
//
// WEITERE ABWEICHUNGEN:
//  * AllNames/AllMobiles ohne Targeting.CheckTextFlags/FriendsManager-Overhead
//    (Text-Flags/Friend-Overlay nicht portiert) — die 0x09-Klicks sind identisch.
//  * GrabHotBag wird bei jedem Zugriff aus dem Profil gelesen (CE cached beim
//    Initialize — dort ist das Profil aber noch nicht geladen).
//  * Party-Einladungen: der Leader wird ueber einen 0xBF-Sub-0x06-Viewer
//    verfolgt; CE-Zusatzlogik (BlockPartyInvites/AutoAccept/10s-Auto-Decline)
//    folgt mit den Options-Features.
//  * OnUseItem prueft BlockHealPoison ohne FeatureBit-Negotiation.

using System;
using Assistant.Agents;

namespace Assistant.HotKeys
{
    public class UseHotKeys
    {
        private static bool m_Initialized;

        /// <summary>Razor CE: PacketHandlers.PartyLeader — letzter Party-Einlader.</summary>
        public static Serial PartyLeader = Serial.Zero;

        private static Serial GrabHotBag
        {
            get
            {
                try
                {
                    return Convert.ToUInt32(Config.GetString("GrabHotBag"));
                }
                catch
                {
                    return Serial.Zero;
                }
            }
        }

        public static void Initialize()
        {
            HotKey.Add(HKCategory.Misc, LocString.Resync, new HotKeyCallback(Resync));

            HotKey.Add(HKCategory.Misc, LocString.ClearDragDropQueue, new HotKeyCallback(DragDropManager.GracefulStop));
            HotKey.Add(HKCategory.Misc, LocString.DropCur, new HotKeyCallback(DragDropManager.DropCurrent));

            HotKey.Add(HKCategory.Misc, LocString.LastSpell, new HotKeyCallback(LastSpell));
            HotKey.Add(HKCategory.Misc, LocString.LastSkill, new HotKeyCallback(LastSkill));
            HotKey.Add(HKCategory.Misc, LocString.LastObj, new HotKeyCallback(LastObj));
            HotKey.Add(HKCategory.Misc, LocString.AllNames, new HotKeyCallback(AllNames));
            HotKey.Add(HKCategory.Misc, LocString.AllCorpses, new HotKeyCallback(AllCorpses));
            HotKey.Add(HKCategory.Misc, LocString.AllMobiles, new HotKeyCallback(AllMobiles));
            HotKey.Add(HKCategory.Misc, LocString.Dismount, new HotKeyCallback(Dismount));

            HotKey.Add(HKCategory.Items, LocString.BandageSelf, new HotKeyCallback(BandageSelf));
            HotKey.Add(HKCategory.Items, LocString.BandageLT, new HotKeyCallback(BandageLastTarg));
            HotKey.Add(HKCategory.Items, LocString.UseHand, new HotKeyCallback(UseItemInHand));
            HotKey.Add(HKCategory.Items, LocString.UseRightHand, new HotKeyCallback(UseItemInRightHand));
            HotKey.Add(HKCategory.Items, LocString.UseLeftHand, new HotKeyCallback(UseItemInLeftHand));

            HotKey.Add(HKCategory.Misc, LocString.PartyAccept, new HotKeyCallback(PartyAccept));
            HotKey.Add(HKCategory.Misc, LocString.PartyDecline, new HotKeyCallback(PartyDecline));
            HotKey.Add(HKCategory.Misc, LocString.PartyAdd, new HotKeyCallback(PartyAdd));

            HotKey.Add(HKCategory.Misc, HKSubCat.PetCommands, LocString.AllCome, new HotKeyCallback(PetAllCome));
            HotKey.Add(HKCategory.Misc, HKSubCat.PetCommands, LocString.AllFollowMe,
                new HotKeyCallback(PetAllFollowMe));
            HotKey.Add(HKCategory.Misc, HKSubCat.PetCommands, LocString.AllFollow, new HotKeyCallback(PetAllFollow));
            HotKey.Add(HKCategory.Misc, HKSubCat.PetCommands, LocString.AllGuardMe, new HotKeyCallback(PetAllGuardMe));
            HotKey.Add(HKCategory.Misc, HKSubCat.PetCommands, LocString.AllGuard, new HotKeyCallback(PetAllGuard));
            HotKey.Add(HKCategory.Misc, HKSubCat.PetCommands, LocString.AllKill, new HotKeyCallback(PetAllKill));
            HotKey.Add(HKCategory.Misc, HKSubCat.PetCommands, LocString.AllStay, new HotKeyCallback(PetAllStay));
            HotKey.Add(HKCategory.Misc, HKSubCat.PetCommands, LocString.AllStop, new HotKeyCallback(PetAllStop));

            HotKeyCallbackState call = new HotKeyCallbackState(OnUseItem);
            HotKey.Add(HKCategory.Items, LocString.UseBandage, call, (ushort) 3617);
            HotKey.Add(HKCategory.Items, HKSubCat.Potions, LocString.DrinkHeal, call, (ushort) 3852);
            HotKey.Add(HKCategory.Items, HKSubCat.Potions, LocString.DrinkCure, call, (ushort) 3847);
            HotKey.Add(HKCategory.Items, HKSubCat.Potions, LocString.DrinkRef, call, (ushort) 3851);
            HotKey.Add(HKCategory.Items, HKSubCat.Potions, LocString.DrinkNS, call, (ushort) 3846);
            HotKey.Add(HKCategory.Items, HKSubCat.Potions, LocString.DrinkExp, call, (ushort) 3853);
            HotKey.Add(HKCategory.Items, HKSubCat.Potions, LocString.DrinkStr, call, (ushort) 3849);
            HotKey.Add(HKCategory.Items, HKSubCat.Potions, LocString.DrinkAg, call, (ushort) 3848);
            HotKey.Add(HKCategory.Items, HKSubCat.Potions, LocString.DrinkApple, new HotKeyCallback(OnDrinkApple));

            HotKey.Add(HKCategory.Misc, LocString.GrabItem, new HotKeyCallback(GrabItem));
            HotKey.Add(HKCategory.Misc, LocString.SetGrabItemHotBag, new HotKeyCallback(SetGrabItemHotBag));

            // Razor CE: HotKeys/Counters.cs
            HotKey.Add(HKCategory.Misc, LocString.DispCounters, new HotKeyCallback(DispCounters));
            HotKey.Add(HKCategory.Misc, LocString.RecountCounters, new HotKeyCallback(Counter.FullRecount));

            if (!m_Initialized)
            {
                m_Initialized = true;

                // GrabItem-HotBag-Label bei Einzelklick (Razor CE: 0x09-Viewer).
                PacketHandler.RegisterClientToServerViewer(0x09, new PacketViewerCallback(OnGrabItemSingleClick));

                // Party-Leader-Verfolgung (Razor CE: Handlers.OnExtendedPacket 0x06/0x07).
                PacketHandler.RegisterServerToClientViewer(0xBF, new PacketViewerCallback(OnExtendedPacket));
            }
        }

        // ---- Party ----------------------------------------------------------

        private static void OnExtendedPacket(PacketReader p, PacketHandlerEventArgs args)
        {
            ushort ext = p.ReadUInt16();

            if (ext == 0x06) // party command
            {
                byte cmd = p.ReadByte();

                if (cmd == 0x07) // party invite
                    PartyLeader = p.ReadUInt32();
                else if (cmd == 0x02 && PartyLeader != Serial.Zero) // remove member/disband
                    PartyLeader = Serial.Zero;
            }
        }

        private static void PartyAccept()
        {
            if (PartyLeader != Serial.Zero)
            {
                ClientProxy.SendToServer(new AcceptParty(PartyLeader));
                PartyLeader = Serial.Zero;
            }
        }

        private static void PartyDecline()
        {
            if (PartyLeader != Serial.Zero)
            {
                ClientProxy.SendToServer(new DeclineParty(PartyLeader));
                PartyLeader = Serial.Zero;
            }
        }

        private static void PartyAdd()
        {
            ClientProxy.SendToServer(new AddParty());
        }

        // ---- Pet-Kommandos (Speech mit Keyword-Encoding) ---------------------

        private static void PetAllCome()
        {
            World.Player.Say(Language.GetString(LocString.AllCome));
        }

        private static void PetAllFollowMe()
        {
            World.Player.Say(Language.GetString(LocString.AllFollowMe));
        }

        private static void PetAllFollow()
        {
            World.Player.Say(Language.GetString(LocString.AllFollow));
        }

        private static void PetAllGuardMe()
        {
            World.Player.Say(Language.GetString(LocString.AllGuardMe));
        }

        private static void PetAllGuard()
        {
            World.Player.Say(Language.GetString(LocString.AllGuard));
        }

        private static void PetAllKill()
        {
            World.Player.Say(Language.GetString(LocString.AllKill));
        }

        private static void PetAllStay()
        {
            World.Player.Say(Language.GetString(LocString.AllStay));
        }

        private static void PetAllStop()
        {
            World.Player.Say(Language.GetString(LocString.AllStop));
        }

        // ---- Misc -------------------------------------------------------------

        private static void Dismount()
        {
            if (World.Player.GetItemOnLayer(Layer.Mount) != null)
                ActionQueue.DoubleClick(true, World.Player.Serial);
            else
                World.Player.SendMessage("You are not mounted.");
        }

        private static void AllNames()
        {
            foreach (Mobile m in World.MobilesInRange())
            {
                if (m != World.Player)
                    ClientProxy.SendToServer(new SingleClick(m));
            }

            foreach (Item i in World.Items.Values)
            {
                if (i.IsCorpse)
                    ClientProxy.SendToServer(new SingleClick(i));
            }
        }

        private static void AllCorpses()
        {
            foreach (Item i in World.Items.Values)
            {
                if (i.IsCorpse)
                    ClientProxy.SendToServer(new SingleClick(i));
            }
        }

        private static void AllMobiles()
        {
            foreach (Mobile m in World.MobilesInRange())
            {
                if (m != World.Player)
                    ClientProxy.SendToServer(new SingleClick(m));
            }
        }

        private static void LastSkill()
        {
            if (World.Player != null && World.Player.LastSkill != -1)
                ClientProxy.SendToServer(new UseSkill(World.Player.LastSkill));
        }

        private static void LastObj()
        {
            if (World.Player != null && World.Player.LastObject != Serial.Zero)
                PlayerData.DoubleClick(World.Player.LastObject);
        }

        private static void LastSpell()
        {
            if (World.Player != null && World.Player.LastSpell != -1)
            {
                ushort id = (ushort) World.Player.LastSpell;
                object o = id;
                Spell.OnHotKey(ref o);
            }
        }

        private static DateTime m_LastSync;

        private static void Resync()
        {
            if (DateTime.UtcNow - m_LastSync > TimeSpan.FromSeconds(1.0))
            {
                m_LastSync = DateTime.UtcNow;

                ClientProxy.SendToServer(new ResyncReq());
            }
        }

        // ---- Bandagen/Haende ---------------------------------------------------

        public static void BandageLastTarg()
        {
            Item pack = World.Player.Backpack;
            if (pack != null)
            {
                if (!World.Player.UseItem(pack, 3617))
                {
                    World.Player.SendMessage(MsgLevel.Warning, LocString.NoBandages);
                }
                else
                {
                    // Ziel-Cursor kommt vom Server — Last Target vormerken.
                    Targeting.LastTarget(true);
                }
            }
        }

        public static void BandageSelf()
        {
            Item pack = World.Player.Backpack;
            if (pack != null)
            {
                if (!World.Player.UseItem(pack, 3617))
                {
                    World.Player.SendMessage(MsgLevel.Warning, LocString.NoBandages);
                }
                else
                {
                    Targeting.ClearQueue();
                    Targeting.TargetSelf(true);
                }
            }
        }

        private static void UseItemInHand()
        {
            Item item = World.Player.GetItemOnLayer(Layer.RightHand);
            if (item == null)
                item = World.Player.GetItemOnLayer(Layer.LeftHand);

            if (item != null)
                PlayerData.DoubleClick(item);
        }

        private static void UseItemInRightHand()
        {
            Item item = World.Player.GetItemOnLayer(Layer.RightHand);

            if (item != null)
                PlayerData.DoubleClick(item);
        }

        private static void UseItemInLeftHand()
        {
            Item item = World.Player.GetItemOnLayer(Layer.LeftHand);

            if (item != null)
                PlayerData.DoubleClick(item);
        }

        // ---- Potions/Apple -------------------------------------------------------

        private static void OnUseItem(ref object state)
        {
            Item pack = World.Player.Backpack;
            if (pack == null)
                return;

            ushort id = (ushort) state;
            if (id == 3852 && World.Player.Poisoned && Config.GetBool("BlockHealPoison"))
            {
                World.Player.SendMessage(MsgLevel.Force, LocString.HealPoisonBlocked);
                return;
            }

            if (!World.Player.UseItem(pack, id))
                World.Player.SendMessage(LocString.NoItemOfType, (ItemID) id);
        }

        private static bool DrinkApple(Item cont)
        {
            for (int i = 0; i < cont.Contains.Count; i++)
            {
                Item item = (Item) cont.Contains[i];

                if (item.ItemID == 12248 && item.Hue == 1160)
                {
                    PlayerData.DoubleClick(item);
                    return true;
                }
                else if (item.Contains != null && item.Contains.Count > 0)
                {
                    if (DrinkApple(item))
                        return true;
                }
            }

            return false;
        }

        private static void OnDrinkApple()
        {
            if (World.Player.Backpack == null)
                return;

            if (!DrinkApple(World.Player.Backpack))
                World.Player.SendMessage(LocString.NoItemOfType, (ItemID) 12248);
        }

        // ---- Grab Item -------------------------------------------------------------

        private static void GrabItem()
        {
            World.Player.SendMessage(MsgLevel.Force, LocString.GrabItemTarget);
            Targeting.OneTimeTarget(OnGrabItem);
        }

        private static void OnGrabItem(bool loc, Serial serial, Point3D pt, ushort itemId)
        {
            Item item = World.FindItem(serial);

            if (item != null && item.Serial.IsItem && item.Movable && item.Visible)
            {
                Item hotbag = World.FindItem(GrabHotBag) ?? World.Player.Backpack;

                DragDropManager.DragDrop(item, item.Amount, hotbag);
            }
            else
            {
                World.Player.SendMessage(MsgLevel.Error, "Invalid or inaccessible item.");
            }
        }

        private static void SetGrabItemHotBag()
        {
            World.Player.SendMessage(MsgLevel.Force, LocString.SetGrabItemHotBag);
            Targeting.OneTimeTarget(OnSetGrabItemHotBag);
        }

        private static void OnSetGrabItemHotBag(bool loc, Serial serial, Point3D pt, ushort itemId)
        {
            if (!loc && serial.IsItem)
            {
                Item hb = World.FindItem(serial);

                if (hb != null)
                {
                    Config.SetProperty("GrabHotBag", serial.Value.ToString());

                    World.Player.SendMessage(MsgLevel.Force, "Grab Item HotBag Set");
                }
                else
                {
                    Config.SetProperty("GrabHotBag", "0");
                }
            }
        }

        private static void OnGrabItemSingleClick(PacketReader pvSrc, PacketHandlerEventArgs args)
        {
            Serial serial = pvSrc.ReadUInt32();
            Serial hotBag = GrabHotBag;

            if (hotBag != Serial.Zero && hotBag == serial)
            {
                ushort gfx = 0;
                Item c = World.FindItem(hotBag);
                if (c != null)
                {
                    gfx = c.ItemID.Value;
                }

                ClientProxy.SendToClient(new UnicodeMessage(hotBag, gfx, MessageType.Label, 0x3B2, 3,
                    Language.CliLocName, "", Language.GetString(LocString.GrabHB)));
            }
        }

        // ---- Counter (Razor CE: HotKeys/Counters.cs) --------------------------------

        private static void DispCounters()
        {
            for (int i = 0; i < Counter.List.Count; i++)
            {
                Counter c = Counter.List[i];

                if (c.Enabled)
                    World.Player.SendMessage(MsgLevel.Force, "{0}: {1}", c.Name, c.Amount);
            }
        }
    }
}
