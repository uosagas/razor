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

// Portiert (getrimmt) aus Razor CE (Razor/Network/Handlers.cs):
// nur die Handler, die das WELTMODELL fuellen. Saemtliche UI-/Filter-
// Seiteneffekte des Originals (Targeting, LT-Hilight-Umfaerbung,
// AutoSearch/AutoOpenCorpses, Overhead-Meldungen, StealthSteps,
// UOAssist, MapWindow, ScriptManager) sind ENTFERNT bzw.
// als TODO markiert. Handler, die in Razor CE als Filter registriert sind
// (weil sie Pakete umfaerben), sind hier reine Viewer.
// Phase 2d NACHGERUESTET (werktreu zu Razor CE): Counter.Count/Uncount,
// ScavengerAgent.Scavenge fuer neue Boden-Items, DragDropManager.EndHolding,
// IgnoreGumps (Gump-Unterdrueckung beim programmatischen Containeroeffnen).

using System;
using System.Collections.Generic;
using System.IO;
using Assistant.Agents;

namespace Assistant
{
    public static class PacketHandlers
    {
        private static bool m_Initialized;

        /// <summary>Razor CE: Handlers.cs, gesetzt vom NewMobileStatus-Handler (0x17).</summary>
        public static bool UseNewStatus = false;

        private static readonly List<Item> _ignoreGumps = new List<Item>();

        /// <summary>Razor CE: Container, deren naechstes Open-Gump (0x24) unterdrueckt wird.</summary>
        public static List<Item> IgnoreGumps => _ignoreGumps;

        public static void Initialize()
        {
            if (m_Initialized)
                return;

            m_Initialized = true;

            // Phase 2c: Recorder-/Wait-Zustands-Viewer + Targeting (0x6C).
            MacroHandlers.Initialize();

            // Gump-Inspector: eigene 0xB0/0xDD/0xB1-Viewer (nur bei Recording aktiv).
            Core.GumpObserver.Initialize();

            // Anzeige-Filter (Speech-/Spell-Hue, Spell-Format, LT-Hilight):
            // Block+Inject statt CE-In-Place-Patch (Mirror ist read-only).
            Core.DisplayFilters.Initialize();

            // Server -> Client
            PacketHandler.RegisterServerToClientViewer(0x11, new PacketViewerCallback(MobileStatus));
            PacketHandler.RegisterServerToClientViewer(0x1A, new PacketViewerCallback(WorldItem));
            PacketHandler.RegisterServerToClientViewer(0x1B, new PacketViewerCallback(LoginConfirm));
            PacketHandler.RegisterServerToClientViewer(0x1D, new PacketViewerCallback(RemoveObject));
            PacketHandler.RegisterServerToClientViewer(0x20, new PacketViewerCallback(MobileUpdate));
            PacketHandler.RegisterServerToClientViewer(0x24, new PacketViewerCallback(BeginContainerContent));
            PacketHandler.RegisterServerToClientViewer(0x25, new PacketViewerCallback(ContainerContentUpdate));
            PacketHandler.RegisterServerToClientViewer(0x2E, new PacketViewerCallback(EquipmentUpdate));
            PacketHandler.RegisterServerToClientViewer(0x3A, new PacketViewerCallback(PlayerSkills));
            PacketHandler.RegisterServerToClientViewer(0x3C, new PacketViewerCallback(ContainerContent));
            PacketHandler.RegisterServerToClientViewer(0x77, new PacketViewerCallback(MobileMoving));
            PacketHandler.RegisterServerToClientViewer(0x78, new PacketViewerCallback(MobileIncoming));
            PacketHandler.RegisterServerToClientViewer(0xA1, new PacketViewerCallback(HitsUpdate));
            PacketHandler.RegisterServerToClientViewer(0xA2, new PacketViewerCallback(ManaUpdate));
            PacketHandler.RegisterServerToClientViewer(0xA3, new PacketViewerCallback(StamUpdate));
            PacketHandler.RegisterServerToClientViewer(0xF3, new PacketViewerCallback(SAWorldItem));
            PacketHandler.RegisterServerToClientViewer(0xF7, new PacketViewerCallback(PacketBatch));
            PacketHandler.RegisterServerToClientViewer(0x0B, new PacketViewerCallback(Damage));
            PacketHandler.RegisterServerToClientViewer(0xD6, new PacketViewerCallback(Core.OplCache.OnMegaCliloc));

            // Client -> Server
            PacketHandler.RegisterClientToServerViewer(0x09, new PacketViewerCallback(ClientSingleClick));

            // Accountname fuer Crash-Reports/Diagnose (wie Razor CE aus den
            // Login-Paketen). Es wird AUSSCHLIESSLICH der Name gelesen — das
            // dahinter liegende Passwort wird nie gelesen oder gespeichert.
            PacketHandler.RegisterClientToServerViewer(0x80, new PacketViewerCallback(AccountLogin));
            PacketHandler.RegisterClientToServerViewer(0x91, new PacketViewerCallback(GameLogin));
        }

        /// <summary>0x80 First Login: [account 30][password 30][0xFF] — nur der Name.</summary>
        private static void AccountLogin(PacketReader p, PacketHandlerEventArgs args)
        {
            string name = p.ReadStringSafe(30);

            if (!string.IsNullOrEmpty(name))
                World.AccountName = name;
        }

        /// <summary>0x91 Game Login: [seed 4][account 30][password 30] — nur der Name.</summary>
        private static void GameLogin(PacketReader p, PacketHandlerEventArgs args)
        {
            p.ReadUInt32(); // seed/auth id

            string name = p.ReadStringSafe(30);

            if (!string.IsNullOrEmpty(name))
                World.AccountName = name;
        }

        /// <summary>Razor CE: ClientSingleClick — Single-Click auf ein Mobile
        /// zeigt die Target-Flags overhead (Option LastTargTextFlags).</summary>
        private static void ClientSingleClick(PacketReader p, PacketHandlerEventArgs args)
        {
            if (World.Player == null)
                return;

            Serial ser = p.ReadUInt32();
            Mobile m = World.FindMobile(ser);

            if (m == null)
                return;

            if (Config.GetBool("LastTargTextFlags"))
                Targeting.CheckTextFlags(m);
        }

        /// <summary>
        /// Razor CE: Damage (0x0B) — erlittener/ausgeteilter Schaden als
        /// Overhead ("[12]") oder Systemmeldung (ShowDamageTaken/-Dealt,
        /// jeweils mit Overhead-Variante). Abweichungen: kein DamageTracker;
        /// eigener Schaden faellt nie in den Dealt-Zweig (CE zeigt ihn bei
        /// ausgeschaltetem ShowDamageTaken faelschlich als "damage on").
        /// </summary>
        private static void Damage(PacketReader p, PacketHandlerEventArgs args)
        {
            if (World.Player == null)
                return;

            uint serial = p.ReadUInt32();
            ushort damage = p.ReadUInt16();

            if (Config.GetBool("ShowDamageTaken") || Config.GetBool("ShowDamageDealt"))
            {
                if (serial == World.Player.Serial && Config.GetBool("ShowDamageTaken"))
                {
                    if (Config.GetBool("ShowDamageTakenOverhead"))
                        World.Player.OverheadMessage(37, $"[{damage}]");
                    else
                        World.Player.SendMessage(MsgLevel.Info, $"{World.Player.Name}: {damage} damage taken");
                }
                else if (serial != World.Player.Serial && Config.GetBool("ShowDamageDealt"))
                {
                    Mobile m = World.FindMobile(serial);

                    if (m == null)
                        return;

                    if (Config.GetBool("ShowDamageDealtOverhead"))
                        m.OverheadMessage(37, $"[{damage}]");
                    else
                        World.Player.SendMessage(MsgLevel.Info, $"{World.Player.Name}: {damage} damage on '{m.Name}'");
                }
            }
        }

        /// <summary>
        /// 0xF7 PacketList — der Shard buendelt mehrere 0xF3-World-Items in EIN
        /// Paket (count(2), dann je id(1)=0xF3 + F3-Rumpf). Ohne Auspacken sieht
        /// der Port die enthaltenen Items nie: kein Weltmodell-Eintrag, kein
        /// AutoOpenCorpses. Der Client behandelt 0xF7 nativ, daher fiel es nur
        /// bei Razor-Features auf. Referenz: Client PacketHandlers.PacketList.
        /// </summary>
        private static void PacketBatch(PacketReader p, PacketHandlerEventArgs args)
        {
            int count = p.ReadUInt16();

            for (int i = 0; i < count && !p.AtEnd; i++)
            {
                byte id = p.ReadByte();
                if (id != 0xF3)
                    break; // unbekannter Sub-Typ — Rest ist nicht sicher lesbar

                // SAWorldItem liest ab dem Kommando-Word (0x01) weiter.
                SAWorldItem(p, args);
            }
        }

        private static void LoginConfirm(PacketReader p, PacketHandlerEventArgs args)
        {
            World.Items.Clear();
            World.Mobiles.Clear();

            UseNewStatus = false;

            Serial serial = p.ReadUInt32();

            PlayerData m = new PlayerData(serial);
            m.Name = World.OrigPlayerName;

            Mobile test = World.FindMobile(serial);
            if (test != null)
                test.Remove();

            World.AddMobile(World.Player = m);

            // Razor CE: Config.LoadProfileFor(World.Player) — hier ueber die
            // Char->Profil-Zuordnung aus Phase 2a.
            if (Config.TryGetProfileForChar(serial, out string profileName) &&
                Config.CurrentProfile?.Name != profileName)
            {
                Config.LoadProfile(profileName);
            }

            p.ReadUInt32(); // always 0?
            m.Body = p.ReadUInt16();
            m.Position = new Point3D(p.ReadUInt16(), p.ReadUInt16(), p.ReadInt16());
            m.Direction = (Direction) p.ReadByte();

            // UOSagas: Razor meldet dem Server seine Version (0xBF sub 0x40) —
            // Grundlage fuer das Server-Versions-Gate (veraltet = Freeze+Kick,
            // kein Razor = keine Meldung = Login laeuft normal durch).
            ClientProxy.SendToServer(new AssistantVersion(AssistantInfo.Name, AssistantInfo.Version));

            // TODO Razor CE: RequestTitlebarUpdate, UOAssist.PostLogin, UpdateTitle,
            // Client.SetPosition, SetSeason, ScriptManager.OnLogin.
        }

        private static void MobileMoving(PacketReader p, PacketHandlerEventArgs args)
        {
            Serial serial = p.ReadUInt32();
            Mobile m = World.FindMobile(serial);

            if (m == null)
            {
                World.AddMobile(m = new Mobile(serial));
                World.RequestMobileStatus(m);
            }

            if (m != null)
            {
                m.Body = p.ReadUInt16();

                m.Position = new Point3D(p.ReadUInt16(), p.ReadUInt16(), p.ReadSByte());

                if (World.Player != null && !Utility.InRange(World.Player.Position, m.Position, World.Player.VisRange))
                {
                    m.Remove();
                    return;
                }

                Targeting.CheckLastTargetRange(m);

                m.Direction = (Direction) p.ReadByte();
                m.Hue = p.ReadUInt16();
                // TODO Razor CE: LT-Hilight (Paket-Umfaerbung) entfernt.

                // UOSagas v2.35+: 2-Byte Extended Flags (wie 0x78/0x20).
                ushort movFlags = p.ReadUInt16();
                m.ProcessPacketFlags((byte) (movFlags & 0xFF));
                m.Notoriety = p.ReadByte();

                // TODO Razor CE: bei m == World.Player Client.SetPosition + Titlebar.
            }
        }

        // Razor CE: Handlers.HealthHues — Dezil-Farbverlauf rot -> gruen.
        private static readonly int[] HealthHues = { 428, 333, 37, 44, 49, 53, 158, 263, 368, 473, 578 };

        private static void HitsUpdate(PacketReader p, PacketHandlerEventArgs args)
        {
            Mobile m = World.FindMobile(p.ReadUInt32());

            if (m != null)
            {
                int oldPercent = m.HitsMax < 1 ? -1 : m.Hits * 100 / m.HitsMax;

                m.HitsMax = p.ReadUInt16();
                m.Hits = p.ReadUInt16();

                // Razor CE: ShowHealth — Prozentwert als Overhead-Meldung, nur
                // bei Aenderung und in Sichtnaehe (kein Spam).
                if (Config.GetBool("ShowHealth") && World.Player != null)
                {
                    int percent = m.Hits * 100 / (m.HitsMax == 0 ? (ushort) 1 : m.HitsMax);

                    if (oldPercent != percent &&
                        Utility.Distance(World.Player.Position, m.Position) <= 12)
                    {
                        try
                        {
                            m.OverheadMessage(HealthHues[((percent + 5) / 10) % HealthHues.Length],
                                string.Format(Config.GetString("HealthFmt"), percent));
                        }
                        catch
                        {
                            // fehlerhaftes HealthFmt darf den Paketfluss nicht stoeren
                        }
                    }
                }
            }
        }

        private static void StamUpdate(PacketReader p, PacketHandlerEventArgs args)
        {
            Mobile m = World.FindMobile(p.ReadUInt32());

            if (m != null)
            {
                m.StamMax = p.ReadUInt16();
                m.Stam = p.ReadUInt16();

                // TODO Razor CE: Titlebar-Update, Party-Stats-Overhead.
            }
        }

        private static void ManaUpdate(PacketReader p, PacketHandlerEventArgs args)
        {
            Mobile m = World.FindMobile(p.ReadUInt32());

            if (m != null)
            {
                m.ManaMax = p.ReadUInt16();
                m.Mana = p.ReadUInt16();

                // TODO Razor CE: Titlebar-Update, Party-Stats-Overhead.
            }
        }

        /// <summary>
        /// Razor CE: Handlers.PlayerSkills (0x3A, Server -> Client). Ohne diesen
        /// Handler bleiben alle Skills 0 und jedes "skill 'Name'" liefert 0.0.
        /// UOSagas/ModernUO sendet die Volliste als Typ 0x02 (0-terminiert) und
        /// Einzelaenderungen als Typ 0xDF (OutgoingPlayerPackets.cs, Standard-UO,
        /// keine Sagas-Abweichung). 0x00/0xFF (alte Clients ohne Cap) bleiben
        /// werktreu erhalten; 0xFE (Skill-Namensliste) braucht der Port nicht,
        /// die 58er-Tabelle steht in Ultima.Skills.
        /// </summary>
        private static void PlayerSkills(PacketReader p, PacketHandlerEventArgs args)
        {
            if (World.Player == null || World.Player.Skills == null)
                return;

            byte type = p.ReadByte();

            switch (type)
            {
                case 0x02: // Liste mit Caps (3.0.8+) — UOSagas-Login-Volliste
                {
                    int i;
                    while ((i = p.ReadUInt16()) > 0)
                    {
                        if (i > 0 && i <= PlayerData.MaxSkills)
                        {
                            Skill skill = World.Player.Skills[i - 1];
                            if (skill == null)
                                continue;

                            skill.FixedValue = p.ReadUInt16();
                            skill.FixedBase = p.ReadUInt16();
                            skill.Lock = (LockType)p.ReadByte();
                            skill.FixedCap = p.ReadUInt16();
                            if (!World.Player.SkillsSent)
                                skill.Delta = 0;
                        }
                        else
                        {
                            p.Seek(7, SeekOrigin.Current);
                        }
                    }

                    World.Player.SkillsSent = true;
                    break;
                }

                case 0x00: // Liste ohne Caps (alte Clients)
                {
                    int i;
                    while ((i = p.ReadUInt16()) > 0)
                    {
                        if (i > 0 && i <= PlayerData.MaxSkills)
                        {
                            Skill skill = World.Player.Skills[i - 1];
                            if (skill == null)
                                continue;

                            skill.FixedValue = p.ReadUInt16();
                            skill.FixedBase = p.ReadUInt16();
                            skill.Lock = (LockType)p.ReadByte();
                            skill.FixedCap = 100;
                            if (!World.Player.SkillsSent)
                                skill.Delta = 0;
                        }
                        else
                        {
                            p.Seek(5, SeekOrigin.Current);
                        }
                    }

                    World.Player.SkillsSent = true;
                    break;
                }

                case 0xDF: // Einzelaenderung mit Cap — UOSagas-Skillgain
                case 0xFF: // Einzelaenderung ohne Cap (alte Clients)
                {
                    int i = p.ReadUInt16();

                    if (i >= 0 && i < PlayerData.MaxSkills)
                    {
                        Skill skill = World.Player.Skills[i];
                        if (skill == null)
                            break;

                        ushort old = skill.FixedBase;
                        skill.FixedValue = p.ReadUInt16();
                        skill.FixedBase = p.ReadUInt16();
                        skill.Lock = (LockType)p.ReadByte();
                        skill.FixedCap = type == 0xDF ? p.ReadUInt16() : (ushort)100;

                        // Razor CE: DisplaySkillChanges (LocString.SkillChanged/-Overhead).
                        if (Config.GetBool("DisplaySkillChanges") && skill.FixedBase != old)
                        {
                            string msg = string.Format(
                                "Your skill in {0} has changed by {1}{2:F1}, it is now {3:F1}.",
                                Ultima.Skills.GetSkillDisplayName(i),
                                skill.FixedBase - old > 0 ? "+" : "",
                                (skill.FixedBase - old) / 10.0,
                                skill.Value);

                            if (Config.GetBool("DisplaySkillChangesOverhead"))
                                World.Player.OverheadMessage(Config.GetInt("SysColor"), msg);
                            else
                                World.Player.SendMessage(MsgLevel.Force, msg);
                        }
                    }

                    break;
                }
            }
        }

        private static void MobileStatus(PacketReader p, PacketHandlerEventArgs args)
        {
            Serial serial = p.ReadUInt32();
            Mobile m = World.FindMobile(serial);
            if (m == null)
                World.AddMobile(m = new Mobile(serial));

            m.Name = p.ReadString(30);

            m.Hits = p.ReadUInt16();
            m.HitsMax = p.ReadUInt16();

            if (p.ReadBoolean())
                m.CanRename = true;

            byte type = p.ReadByte();

            if (m == World.Player && type != 0x00)
            {
                PlayerData player = (PlayerData) m;

                player.Female = p.ReadBoolean();

                player.Str = p.ReadUInt16();
                player.Dex = p.ReadUInt16();
                player.Int = p.ReadUInt16();

                // TODO Razor CE: "Display Stat Changes"-Meldungen entfernt.

                player.Stam = p.ReadUInt16();
                player.StamMax = p.ReadUInt16();
                player.Mana = p.ReadUInt16();
                player.ManaMax = p.ReadUInt16();

                player.Gold = p.ReadUInt32();
                player.AR = p.ReadUInt16(); // ar / physical resist
                player.Weight = p.ReadUInt16();

                if (type >= 0x03)
                {
                    if (type > 0x04)
                    {
                        player.MaxWeight = p.ReadUInt16();

                        p.ReadByte(); // race?
                    }

                    player.StatCap = p.ReadUInt16();

                    player.Followers = p.ReadByte();
                    player.FollowersMax = p.ReadByte();

                    if (type > 0x03)
                    {
                        player.FireResistance = p.ReadInt16();
                        player.ColdResistance = p.ReadInt16();
                        player.PoisonResistance = p.ReadInt16();
                        player.EnergyResistance = p.ReadInt16();

                        player.Luck = p.ReadInt16();

                        // Hinweis: Doppeltes DamageMin-Lesen ist 1:1 aus Razor CE
                        // uebernommen (dort vermutlich ein Bug, aber werktreu).
                        player.DamageMin = p.ReadUInt16();
                        player.DamageMin = p.ReadUInt16();
                        player.DamageMax = p.ReadUInt16();

                        player.Tithe = p.ReadInt32();
                    }
                }

                // TODO Razor CE: Titlebar-/UOAssist-/MainWindow-Updates entfernt.
            }
        }

        private static void MobileUpdate(PacketReader p, PacketHandlerEventArgs args)
        {
            if (World.Player == null)
                return;

            Serial serial = p.ReadUInt32();
            Mobile m = World.FindMobile(serial);
            if (m == null)
                World.AddMobile(m = new Mobile(serial));

            bool wasHidden = !m.Visible;

            m.Body = (ushort) (p.ReadUInt16() + p.ReadSByte());
            m.Hue = p.ReadUInt16();

            // UOSagas v2.35+: 2-Byte Extended Flags (wie 0x78) — der Client liest
            // hier ebenfalls ReadUInt16BE. Standard-Flags im Low-Byte.
            ushort updFlags = p.ReadUInt16();
            m.ProcessPacketFlags((byte) (updFlags & 0xFF));

            ushort x = p.ReadUInt16();
            ushort y = p.ReadUInt16();
            p.ReadUInt16(); //always 0? (serverID)
            m.Direction = (Direction) p.ReadByte();
            m.Position = new Point3D(x, y, p.ReadSByte());

            // Razor CE: Stealth-Zaehler an Sichtbarkeits-Uebergaengen; danach
            // LT-Range-Check (nach der Positions-Zuweisung, sonst alte Position).
            if (m == World.Player)
            {
                if (wasHidden && m.Visible)
                    StealthSteps.Unhide();
                else if (!wasHidden && !m.Visible && Config.GetBool("CountStealthSteps"))
                    StealthSteps.Hide();
            }

            Targeting.CheckLastTargetRange(m);

            Item.UpdateContainers();
        }

        private static void MobileIncoming(PacketReader p, PacketHandlerEventArgs args)
        {
            if (World.Player == null)
                return;

            Serial serial = p.ReadUInt32();
            ushort body = p.ReadUInt16();

            Mobile m = World.FindMobile(serial);

            if (m == null)
            {
                World.AddMobile(m = new Mobile(serial));

                // Razor CE: neue Mobiles automatisch anklicken (der Server
                // antwortet mit dem Namens-Label -> Name erscheint im Spiel).
                if (m != World.Player && Config.GetBool("ShowMobNames"))
                    ClientProxy.SendToServer(new SingleClick(m));

                if (Config.GetBool("LastTargTextFlags"))
                    Targeting.CheckTextFlags(m);
            }

            Point3D position = new Point3D(p.ReadUInt16(), p.ReadUInt16(), p.ReadSByte());

            bool wasHidden = !m.Visible;

            m.Body = body;
            if (m != World.Player)
                m.Position = position;
            m.Direction = (Direction) p.ReadByte();
            m.Hue = p.ReadUInt16();
            // TODO Razor CE: LT-Hilight (Paket-Umfaerbung) entfernt.

            // UOSagas v2.35+: Extended Flags sind 2 Byte (ReadUInt16BE), nicht 1 Byte
            // wie Standard-UO. Der Client liest hier ebenfalls ReadUInt16BE
            // ("Sagas: v2.35+ - Extended flags"). Die Standard-Flags stehen im
            // Low-Byte. Ohne diese 2 Byte verschiebt sich der Rest des Pakets um 1
            // Byte -> die Ausruestungsliste wird nicht gelesen (kein Backpack!).
            ushort mobFlags = p.ReadUInt16();
            m.ProcessPacketFlags((byte) (mobFlags & 0xFF));
            m.Notoriety = p.ReadByte();

            // Razor CE: Stealth-Zaehler an Sichtbarkeits-Uebergaengen + LT-Range.
            if (m == World.Player)
            {
                if (wasHidden && m.Visible)
                    StealthSteps.Unhide();
                else if (!wasHidden && !m.Visible && Config.GetBool("CountStealthSteps"))
                    StealthSteps.Hide();
            }

            Targeting.CheckLastTargetRange(m);

            while (true)
            {
                serial = p.ReadUInt32();
                if (!serial.IsItem)
                    break;

                Item item = World.FindItem(serial);
                if (item == null)
                {
                    World.AddItem(item = new Item(serial));
                }

                item.Container = m;

                ushort id = p.ReadUInt16();

                if (Engine.UseNewMobileIncoming)
                    item.ItemID = (ushort) (id & 0xFFFF);
                else if (Engine.UsePostSAChanges)
                    item.ItemID = (ushort) (id & 0x7FFF);
                else
                    item.ItemID = (ushort) (id & 0x3FFF);

                item.Layer = (Layer) p.ReadByte();

                if (Engine.UseNewMobileIncoming)
                {
                    item.Hue = p.ReadUInt16();
                }
                else
                {
                    if ((id & 0x8000) != 0)
                    {
                        item.Hue = p.ReadUInt16();
                    }
                    else
                    {
                        item.Hue = 0;
                    }
                }

                // Diagnose: Ausruestung des eigenen Chars aus dem 0x78 (Login).
                if (m == World.Player)
                    Diagnostics.NotePlayerEquip();

                // TODO Razor CE: AutoSearch beim eigenen Backpack entfernt.
            }

            Item.UpdateContainers();
        }

        private static void RemoveObject(PacketReader p, PacketHandlerEventArgs args)
        {
            Serial serial = p.ReadUInt32();

            if (serial.IsMobile)
            {
                Mobile m = World.FindMobile(serial);
                if (m != null && m != World.Player)
                    m.Remove();
            }
            else if (serial.IsItem)
            {
                Item i = World.FindItem(serial);
                if (i != null)
                {
                    if (DragDropManager.Holding == i)
                    {
                        Counter.SupressWarnings = true;
                        i.Container = null;
                        Counter.SupressWarnings = false;
                    }
                    else
                    {
                        i.RemoveRequest();
                    }
                }
            }
        }

        /// <summary>
        /// Razor CE: Corpse-Zweig aus WorldItem/SAWorldItem (isNew) —
        /// ShowCorpseNames (SingleClick loest den Namen aus) und
        /// AutoOpenCorpses innerhalb CorpseRange, optional nur einmal pro
        /// Leiche (BlockOpenCorpsesTwice ueber OpenedCorpses).
        /// </summary>
        private static void HandleNewCorpse(Item item)
        {
            if (World.Player == null || item == null || !item.IsCorpse)
                return;

            if (Config.GetBool("ShowCorpseNames"))
                ClientProxy.SendToServer(new SingleClick(item));

            // CE-Semantik "twice erlaubt": kommt dieselbe Leiche erneut als
            // neues Paket (aus der Sicht verschwunden und zurueck), darf sie
            // wieder oeffnen — dazu den Dedup-Eintrag freigeben.
            if (!Config.GetBool("BlockOpenCorpsesTwice"))
                World.Player.OpenedCorpses.Remove(item.Serial);

            CheckAutoOpenCorpses();
        }

        /// <summary>
        /// Wie der integrierte Assistant (PlayerMobile.TryOpenCorpses): laeuft
        /// bei jedem neuen Corpse-Paket UND bei jeder Spielerbewegung (Plugin →
        /// OnPlayerPositionChanged). Der Sweep dedupliziert IMMER ueber
        /// OpenedCorpses — sonst wuerde jeder Schritt neben einer Leiche einen
        /// weiteren Doppelklick ausloesen. Wichtig fuer Fernkampf: die Leiche
        /// faellt ausserhalb der Range und oeffnet erst beim Heranlaufen.
        /// </summary>
        public static void CheckAutoOpenCorpses()
        {
            if (World.Player == null || !Config.GetBool("AutoOpenCorpses") || !World.Player.Visible)
                return;

            int range = Config.GetInt("CorpseRange");
            bool blockTwice = Config.GetBool("BlockOpenCorpsesTwice");

            if (World.Player.OpenedCorpses.Count > 2000)
                World.Player.OpenedCorpses.RemoveRange(0, 500);

            foreach (Item item in World.Items.Values)
            {
                if (!item.IsCorpse)
                    continue;

                if (!Utility.InRange(item.Position, World.Player.Position, range))
                {
                    // "Twice" erlaubt: verlaesst die Leiche die Range, wird der
                    // Dedup-Eintrag freigegeben — der naechste Besuch oeffnet
                    // erneut. Mit Block bleibt es bei einmal pro Sitzung.
                    if (!blockTwice)
                        World.Player.OpenedCorpses.Remove(item.Serial);
                    continue;
                }

                if (World.Player.OpenedCorpses.Contains(item.Serial))
                    continue;

                World.Player.OpenedCorpses.Add(item.Serial);
                PlayerData.DoubleClick(item);
            }
        }

        private static void WorldItem(PacketReader p, PacketHandlerEventArgs args)
        {
            Item item;
            bool isNew = false;
            uint serial = p.ReadUInt32();
            item = World.FindItem(serial & 0x7FFFFFFF);
            if (item == null)
            {
                World.AddItem(item = new Item(serial & 0x7FFFFFFF));
                isNew = true;
            }
            else
            {
                item.CancelRemove();
            }

            if (!DragDropManager.EndHolding(serial))
                return;

            item.Container = null;
            Counter.Uncount(item);

            ushort itemID = p.ReadUInt16();
            item.ItemID = (ushort) (itemID & 0x7FFF);

            if ((serial & 0x80000000) != 0)
                item.Amount = p.ReadUInt16();
            else
                item.Amount = 1;

            if ((itemID & 0x8000) != 0)
                item.ItemID = (ushort) (item.ItemID + p.ReadSByte());

            ushort x = p.ReadUInt16();
            ushort y = p.ReadUInt16();

            if ((x & 0x8000) != 0)
                item.Direction = p.ReadByte();
            else
                item.Direction = 0;

            short z = p.ReadSByte();

            item.Position = new Point3D(x & 0x7FFF, y & 0x3FFF, z);

            if ((y & 0x8000) != 0)
                item.Hue = p.ReadUInt16();
            else
                item.Hue = 0;

            byte flags = 0;
            if ((y & 0x4000) != 0)
                flags = p.ReadByte();

            item.ProcessPacketFlags(flags);

            if (isNew && World.Player != null && item.IsCorpse)
            {
                HandleNewCorpse(item);
            }
            else if (isNew && World.Player != null && !item.IsMulti)
            {
                // Razor CE: Scavenger fuer neue Boden-Items in Reichweite.
                ScavengerAgent s = ScavengerAgent.Instance;
                int dist = Utility.Distance(item.GetWorldPosition(), World.Player.Position);
                if (s != null && !World.Player.IsGhost && World.Player.Visible && dist <= 2 && s.Enabled &&
                    item.Movable)
                    s.Scavenge(item);
            }

            // TODO Razor CE: UOAssist.PostAddMulti entfernt.

            Item.UpdateContainers();

            if (Config.GetBool("ShowStaticWalls"))
                WallStaticFilter.MakeWallStatic(item);
        }

        private static void SAWorldItem(PacketReader p, PacketHandlerEventArgs args)
        {
            /*
            New World Item Packet (0xF3), PacketLen: 24 (Post-7.0.9.0: 26)
            Format:
                 BYTE - 0xF3 packetId
                 WORD - 0x01
                 BYTE - ArtDataID: 0x00 TileData-Art, 0x02 MultiData-Art
                 DWORD - item Serial
                 WORD - item ID
                 BYTE - item direction
                 WORD - amount
                 WORD - amount
                 WORD - X
                 WORD - Y
                 SBYTE - Z
                 BYTE - item light
                 WORD - item Hue
                 BYTE - item flags
                 [WORD ??? nur Post-HS]
            */

            p.ReadUInt16(); // 0x01

            byte artDataID = p.ReadByte();

            Item item;
            bool isNew = false;
            uint serial = p.ReadUInt32();
            item = World.FindItem(serial);
            if (item == null)
            {
                World.AddItem(item = new Item(serial));
                isNew = true;
            }
            else
            {
                item.CancelRemove();
            }

            if (!DragDropManager.EndHolding(serial))
                return;

            item.Container = null;
            Counter.Uncount(item);

            ushort itemID = p.ReadUInt16();
            item.ItemID = (ushort) (artDataID == 0x02 ? itemID | 0x4000 : itemID);

            item.Direction = p.ReadByte();

            p.ReadUInt16(); // amount (doppelt im Paket)
            item.Amount = p.ReadUInt16();

            ushort x = p.ReadUInt16();
            ushort y = p.ReadUInt16();
            short z = p.ReadSByte();

            item.Position = new Point3D(x, y, z);

            p.ReadByte(); // item light

            item.Hue = p.ReadUInt16();

            // UOSagas v2.35+: 2-Byte Extended Flags + unk2 (Client liest ReadUInt16BE
            // fuer Flags, dann ein weiteres UInt16). Standard-Flags im Low-Byte.
            ushort saFlags = p.ReadUInt16();
            p.ReadUInt16(); // unk2 (Sagas corpse-fix)

            item.ProcessPacketFlags((byte) (saFlags & 0xFF));

            // KEIN weiteres Read: CEs Post-HS-"???"-Word existiert im Sagas-
            // Format nicht (der Client liest nach unk2 nichts, D13). Das alte
            // Extra-Read hat jeden 0xF7-Batch ab dem 2. Eintrag desynct.

            if (isNew && World.Player != null && item.IsCorpse)
            {
                HandleNewCorpse(item);
            }
            else if (isNew && World.Player != null && !item.IsMulti)
            {
                // Razor CE: Scavenger fuer neue Boden-Items in Reichweite.
                ScavengerAgent s = ScavengerAgent.Instance;
                int dist = Utility.Distance(item.GetWorldPosition(), World.Player.Position);
                if (s != null && !World.Player.IsGhost && World.Player.Visible && dist <= 2 && s.Enabled &&
                    item.Movable)
                    s.Scavenge(item);
            }

            // TODO Razor CE: UOAssist.PostAddMulti entfernt.

            Item.UpdateContainers();

            if (Config.GetBool("ShowStaticWalls"))
                WallStaticFilter.MakeWallStatic(item);
        }

        private static void ContainerContentUpdate(PacketReader p, PacketHandlerEventArgs args)
        {
            // This function will ignore the item if the container item has not been sent to the client yet.
            // We can do this because we can't really count on getting all of the container info anyway.
            Serial serial = p.ReadUInt32();
            ushort itemid = p.ReadUInt16();
            itemid = (ushort) (itemid + p.ReadSByte()); // signed, itemID offset
            ushort amount = p.ReadUInt16();
            if (amount == 0)
                amount = 1;
            Point3D pos = new Point3D(p.ReadUInt16(), p.ReadUInt16(), 0);
            byte gridPos = 0;
            if (Engine.UsePostKRPackets)
                gridPos = p.ReadByte();
            Serial cser = p.ReadUInt32();
            ushort hue = p.ReadUInt16();
            p.ReadUInt16(); // UOSagas v2.35+: 2-Byte Extended Flags (Client liest sie ebenfalls)

            Item i = World.FindItem(serial);
            if (i == null)
            {
                if (!serial.IsItem)
                    return;

                World.AddItem(i = new Item(serial));
                i.IsNew = i.AutoStack = true;
            }
            else
            {
                i.CancelRemove();
            }

            if (serial != DragDropManager.Pending)
            {
                if (!DragDropManager.EndHolding(serial))
                    return;
            }

            i.ItemID = itemid;
            i.Amount = amount;
            i.Position = pos;
            i.GridNum = gridPos;
            i.Hue = hue;

            // TODO Razor CE: SearchExemptionAgent-Umfaerbung entfernt.

            i.Container = cser;
            if (i.IsNew)
                Item.UpdateContainers();
            if (World.Player != null && !SearchExemptionAgent.IsExempt(i) &&
                (i.IsChildOf(World.Player.Backpack) || i.IsChildOf(World.Player.Quiver)))
                Counter.Count(i);
        }

        private static void BeginContainerContent(PacketReader p, PacketHandlerEventArgs args)
        {
            Serial ser = p.ReadUInt32();
            if (!ser.IsItem)
                return;
            Item item = World.FindItem(ser);
            if (item != null)
            {
                if (_ignoreGumps.Contains(item))
                {
                    _ignoreGumps.Remove(item);
                    args.Block = true;
                }
            }
            else
            {
                World.AddItem(new Item(ser));
                Item.UpdateContainers();
            }
        }

        private static void ContainerContent(PacketReader p, PacketHandlerEventArgs args)
        {
            int count = p.ReadUInt16();

            for (int i = 0; i < count; i++)
            {
                Serial serial = p.ReadUInt32();
                // serial is purposely not checked to be valid, sometimes buy lists dont have "valid" item serials (and we are okay with that).
                Item item = World.FindItem(serial);
                if (item == null)
                {
                    World.AddItem(item = new Item(serial));
                    item.IsNew = true;
                    item.AutoStack = false;
                }
                else
                {
                    item.CancelRemove();
                }

                if (!DragDropManager.EndHolding(serial))
                    continue;

                item.ItemID = p.ReadUInt16();
                item.ItemID = (ushort) (item.ItemID + p.ReadSByte()); // signed, itemID offset
                item.Amount = p.ReadUInt16();
                if (item.Amount == 0)
                    item.Amount = 1;
                item.Position = new Point3D(p.ReadUInt16(), p.ReadUInt16(), 0);
                if (Engine.UsePostKRPackets)
                    item.GridNum = p.ReadByte();
                Serial cont = p.ReadUInt32();
                item.Hue = p.ReadUInt16();
                p.ReadUInt16(); // UOSagas v2.35+: 2-Byte Extended Flags pro Item (Client liest sie ebenfalls) — ohne dies verschiebt sich der Rest der Liste!
                // TODO Razor CE: SearchExemptionAgent-Umfaerbung entfernt.

                item.Container = cont; // must be done after hue is set (for counters)
                if (World.Player != null && !SearchExemptionAgent.IsExempt(item) &&
                    (item.IsChildOf(World.Player.Backpack) || item.IsChildOf(World.Player.Quiver)))
                    Counter.Count(item);

                // Diagnose: Inhalt des eigenen Backpacks via 0x3C.
                if (World.Player != null && World.Player.Backpack != null && cont == World.Player.Backpack.Serial)
                    Diagnostics.NotePlayerBackpackContent(1);
            }

            Item.UpdateContainers();
        }

        private static void EquipmentUpdate(PacketReader p, PacketHandlerEventArgs args)
        {
            Serial serial = p.ReadUInt32();

            Item i = World.FindItem(serial);
            if (i == null)
            {
                World.AddItem(i = new Item(serial));
                Item.UpdateContainers();
            }
            else
            {
                i.CancelRemove();
            }

            if (!DragDropManager.EndHolding(serial))
                return;

            ushort iid = p.ReadUInt16();
            i.ItemID = (ushort) (iid + p.ReadSByte()); // signed, itemID offset
            i.Layer = (Layer) p.ReadByte();
            Serial ser = p.ReadUInt32(); // cont must be set after hue (for counters)
            i.Hue = p.ReadUInt16();

            i.Container = ser;

            // Diagnose: Ausruestung des eigenen Chars via 0x2E (Login/Anlegen).
            if (World.Player != null && ser == World.Player.Serial)
                Diagnostics.NotePlayerEquip();

            // TODO Razor CE: LT-Hilight-Umfaerbung + AutoSearch beim eigenen
            // Backpack entfernt.
        }
    }
}
