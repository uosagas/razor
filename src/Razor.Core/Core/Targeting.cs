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

// Portiert (getrimmt) aus Razor CE (Razor/Core/Targeting.cs) — Phase 2c.
// Enthalten: Target-Zustand (HasTarget/CurrentID/Flags/AllowGround),
// NewTarget-Viewer (Server 0x6C), TargetResponse-Viewer (Client 0x6C,
// LastTarget-Pflege + Macro-Recording), Target(...)-Antworten, DoLastTarget/
// DoTargetSelf, ResendTarget, CancelClientTarget/CancelTarget.
// Phase 2d: Intercept/OneTimeTarget aus Razor CE nachgeruestet (Agents:
// SetHotBag/AddItem-Targets). Abweichung: OneTimeResponse laeuft synchron
// statt ueber Timer.DelayedCallbackState (kein UI-Thread im Port).
// Phase 3c2: Target-Queue (QueueTargets/forceQ wie Razor CE — Basis fuer
// Bandage Self/LT und Heal-or-Cure), Attack Last Combatant/Target (0x05,
// Kombattant via 0xAA-Viewer), SetLastTargetTo + die kompletten Closest/
// Random/Next/Prev-Hotkeys (TargetingVariants.cs).
// Feature-Paritaet (2026-07-17): SmartLastTarget (getrennte Harm-/Bene-Ziele
// nach Cursor-Flags) inkl. Set Last Beneficial/Harmful-Hotkeys und der
// Next/Prev-Zuweisungsfilter (OnlyNextPrevBeneficial/FriendlyBeneficialOnly/
// NonFriendlyHarmfulOnly, TargetingVariants.cs). Abweichung zu CE:
// IsSmartTargetingEnabled prueft nur das Profil — AllowBit/FeatureBits
// entfallen (Features gelten als erlaubt, siehe STATE.md).
// ENTFERNT gegenueber Razor CE (dokumentiert):
//  * HealPoison-Block, OPL-TextFlags (LastTargTextFlags — Anzeige-Batch),
//    ScriptManager, GateTimer/SpellTargetID, FriendsManager-Checks.

using System.Collections.Generic;
using Assistant.Macros;

namespace Assistant
{
    public static partial class Targeting
    {
        public const uint LocalTargID = 0x7FFFFFFF; // uid for Razor-initiated targets

        public delegate void TargetResponseCallback(bool location, Serial serial, Point3D p, ushort gfxid);

        public delegate void CancelTargetCallback();

        private static bool m_Initialized;

        private static bool _hasTarget;
        private static bool _clientTarget;
        private static TargetInfo _lastTarget;
        private static TargetInfo _lastGroundTarget;
        private static TargetInfo _lastBeneTarget;
        private static TargetInfo _lastHarmTarget;

        private static bool _allowGround;
        private static uint _currentID;
        private static byte _curFlags;

        private static bool _intercept;
        private static TargetResponseCallback _onTarget;
        private static CancelTargetCallback _onCancel;
        private static uint _previousID;
        private static bool _previousGround;
        private static byte _prevFlags;

        private static readonly List<uint> _filterCancel = new List<uint>();

        // Phase 3c2: Target-Queue (Razor CE: QueueTarget/TargetSelfAction/LastTargetAction).
        private delegate bool QueueTarget();

        private static readonly QueueTarget TargetSelfAction = new QueueTarget(DoTargetSelf);
        private static readonly QueueTarget LastTargetAction = new QueueTarget(DoLastTarget);
        private static QueueTarget _queueTarget;

        // Phase 3c2: letzter Kombattant (Razor CE: _lastCombatant via 0xAA).
        private static Serial _lastCombatant = Serial.Zero;

        public static bool HasTarget
        {
            get { return _hasTarget; }
        }

        public static short CursorType
        {
            get { return _curFlags; }
        }

        public static uint CurrentTargetID
        {
            get { return _currentID; }
        }

        public static TargetInfo LastTargetInfo
        {
            get { return _lastTarget; }
        }

        /// <summary>Razor CE: Grab-HotKey-Sonderfall — im Port immer false.</summary>
        public static bool FromGrabHotKey
        {
            get { return false; }
        }

        public static void Initialize()
        {
            // Phase 3c2: Hotkeys wie Razor CE Targeting.Initialize.
            HotKey.Add(HKCategory.Targets, LocString.LastTarget, new HotKeyCallback(LastTarget));
            HotKey.Add(HKCategory.Targets, LocString.TargetSelf, new HotKeyCallback(TargetSelf));
            HotKey.Add(HKCategory.Targets, LocString.ClearTargQueue, new HotKeyCallback(OnClearQueue));

            HotKey.Add(HKCategory.Targets, LocString.SetLT, new HotKeyCallback(TargetSetLastTarget));
            HotKey.Add(HKCategory.Targets, LocString.SetLastBeneficial, new HotKeyCallback(SetLastTargetBeneficial));
            HotKey.Add(HKCategory.Targets, LocString.SetLastHarmful, new HotKeyCallback(SetLastTargetHarmful));

            HotKey.Add(HKCategory.Targets, LocString.AttackLastComb, new HotKeyCallback(AttackLastComb));
            HotKey.Add(HKCategory.Targets, LocString.AttackLastTarg, new HotKeyCallback(AttackLastTarg));
            HotKey.Add(HKCategory.Targets, LocString.CancelTarget, new HotKeyCallback(CancelTarget));

            InitRandomTarget();
            InitNextPrevTargets();
            InitClosestTargets();

            if (m_Initialized)
                return;

            m_Initialized = true;

            PacketHandler.RegisterServerToClientViewer(0x6C, new PacketViewerCallback(NewTarget));
            PacketHandler.RegisterClientToServerViewer(0x6C, new PacketViewerCallback(TargetResponse));
            PacketHandler.RegisterServerToClientViewer(0xAA, new PacketViewerCallback(CombatantChange));
            PacketHandler.RegisterClientToServerViewer(0x05, new PacketViewerCallback(AttackRequest));
        }

        /// <summary>Razor CE: CombatantChange — Server meldet aktuelles Kampfziel (0xAA).</summary>
        private static void CombatantChange(PacketReader p, PacketHandlerEventArgs e)
        {
            Serial ser = p.ReadUInt32();
            if (ser.IsMobile && World.Player != null && ser != World.Player.Serial && ser != Serial.Zero &&
                ser != Serial.MinusOne)
                _lastCombatant = ser;
        }

        /// <summary>0x05 Attack-Request des Clients — fuer die Attack-Overhead-Anzeige.</summary>
        private static void AttackRequest(PacketReader p, PacketHandlerEventArgs e)
        {
            Serial ser = p.ReadUInt32();
            if (ser.IsMobile)
                ShowAttackOverhead(ser);
        }

        private static Serial _lastOverheadMessageAttack = Serial.Zero;

        /// <summary>
        /// Razor CE: Targeting.ShowAttackOverhead — "Attack: Name" ueber dem
        /// eigenen Kopf im Noto-Hue des Ziels (FriendsManager nicht portiert,
        /// daher ohne Friend-Sonderfarbe).
        /// </summary>
        public static void ShowAttackOverhead(Serial serial)
        {
            if (!Config.GetBool("ShowAttackTargetOverhead") || World.Player == null)
                return;

            if (Config.GetBool("ShowAttackTargetNewOnly") && serial == _lastOverheadMessageAttack)
                return;

            Mobile m = World.FindMobile(serial);
            if (m == null)
                return;

            World.Player.OverheadMessage(m.GetNotorietyColorInt(), $"Attack: {m.Name}");
            _lastOverheadMessageAttack = serial;
        }

        /// <summary>
        /// Razor CE: Targeting.CheckLastTargetRange — wartet ein Last-Target in
        /// der Queue und kommt das Ziel in LTRange, wird es sofort ausgeloest.
        /// Aufgerufen aus MobileMoving/MobileIncoming/MobileUpdate.
        /// </summary>
        public static void CheckLastTargetRange(Mobile m)
        {
            if (World.Player == null)
                return;

            if (_hasTarget && m != null && _lastTarget != null && m.Serial == _lastTarget.Serial &&
                _queueTarget == LastTargetAction)
            {
                if (Config.GetBool("RangeCheckLT") &&
                    Utility.InRange(World.Player.Position, m.Position, Config.GetInt("LTRange")))
                {
                    if (_queueTarget())
                        ClearQueue();
                }
            }
        }

        /// <summary>Razor CE: AttackLastComb — letzten Kombattanten angreifen (0x05).</summary>
        public static void AttackLastComb()
        {
            if (_lastCombatant.IsMobile)
            {
                ClientProxy.SendToServer(new AttackReq(_lastCombatant));
                ShowAttackOverhead(_lastCombatant);
            }
        }

        /// <summary>Razor CE: AttackLastTarg — Last Target angreifen; bei
        /// SmartLastTarget wird das harmful Ziel bevorzugt.</summary>
        public static void AttackLastTarg()
        {
            TargetInfo targ;
            if (IsSmartTargetingEnabled())
            {
                // Razor CE: bei Smart Targeting greift der Hotkey das harmful Ziel an.
                targ = _lastHarmTarget ?? _lastTarget;
            }
            else
            {
                targ = _lastTarget;
            }

            if (targ != null && targ.Serial.IsMobile)
            {
                ClientProxy.SendToServer(new AttackReq(targ.Serial));
                ShowAttackOverhead(targ.Serial);
            }
        }

        /// <summary>Razor CE: IsSmartTargetingEnabled — im Port ohne AllowBit
        /// (FeatureBits entfallen, Features gelten als erlaubt).</summary>
        private static bool IsSmartTargetingEnabled()
        {
            return Config.GetBool("SmartLastTarget");
        }

        // ---- Overhead-Anzeigen (Razor CE: CheckTextFlags/OverheadTargetMessage) ----

        private static System.DateTime _lastFlagCheck = System.DateTime.UtcNow;
        private static Serial _lastFlagCheckSerial;

        /// <summary>
        /// Razor CE: CheckTextFlags — zeigt beim Single-Click (und bei neuen
        /// Mobiles) "[Last Target]"/"[Harmful Target]"/"[Beneficial Target]"
        /// bzw. "[Ignored]" overhead an (Option LastTargTextFlags).
        /// </summary>
        public static void CheckTextFlags(Mobile m)
        {
            if (m == null || World.Player == null)
                return;

            // 250-ms-Drossel pro Mobile (CE 1:1) — Single-Click loest sonst
            // mehrfach aus (Label-Anfrage + Antwort).
            if (System.DateTime.UtcNow - _lastFlagCheck < System.TimeSpan.FromMilliseconds(250) &&
                m.Serial == _lastFlagCheckSerial)
                return;

            if (Agents.IgnoreAgent.IsIgnored(m.Serial))
            {
                m.OverheadMessage(Config.GetInt("SysColor"), "[Ignored]");
            }

            if (IsSmartTargetingEnabled())
            {
                bool harm = _lastHarmTarget != null && _lastHarmTarget.Serial == m.Serial;
                bool bene = _lastBeneTarget != null && _lastBeneTarget.Serial == m.Serial;

                if (harm)
                    m.OverheadMessage(0x90, $"[{Language.GetString(LocString.HarmfulTarget)}]");
                if (bene)
                    m.OverheadMessage(0x3F, $"[{Language.GetString(LocString.BeneficialTarget)}]");
            }

            if (_lastTarget != null && _lastTarget.Serial == m.Serial)
                m.OverheadMessage(0x3B2, $"[{Language.GetString(LocString.LastTarget)}]");

            _lastFlagCheck = System.DateTime.UtcNow;
            _lastFlagCheckSerial = m.Serial;
        }

        private static TargetInfo _lastOverheadMessageTarget = new TargetInfo();

        /// <summary>
        /// Razor CE: OverheadTargetMessage — beim Setzen/Beantworten eines
        /// Targets "Target: Name" ueber dem Spieler (ShowAttackTargetOverhead)
        /// und/oder den Indikator ueber dem Ziel (ShowTextTargetIndicator,
        /// Format {name}, Hue TargetIndicatorHue). Abweichung: kein
        /// FriendsManager-Hue (63) — es gilt immer der Noto-Hue.
        /// </summary>
        public static void OverheadTargetMessage(TargetInfo info)
        {
            if (info == null || World.Player == null)
                return;

            if (Config.GetBool("ShowAttackTargetNewOnly") && info.Serial == _lastOverheadMessageTarget.Serial)
                return;

            Mobile m = null;

            if (Config.GetBool("ShowAttackTargetOverhead") && info.Serial != 0 && info.Serial.IsMobile)
            {
                m = World.FindMobile(info.Serial);

                if (m == null)
                    return;

                World.Player.OverheadMessage(m.GetNotorietyColorInt(), $"Target: {m.Name}");
            }

            if (Config.GetBool("ShowTextTargetIndicator") && info.Serial != 0 && info.Serial.IsMobile)
            {
                if (m == null)
                    m = World.FindMobile(info.Serial);

                if (m == null)
                    return;

                m.OverheadMessage(Config.GetInt("TargetIndicatorHue"),
                    Config.GetString("TargetIndicatorFormat").Replace("{name}", m.Name));
            }

            _lastOverheadMessageTarget = info;
        }

        public static void ClearQueue()
        {
            _queueTarget = null;
        }

        /// <summary>Razor CE: OnClearQueue — Meldung optional overhead.</summary>
        public static void OnClearQueue()
        {
            ClearQueue();

            if (Config.GetBool("ShowTargetSelfLastClearOverhead"))
                World.Player?.OverheadMessage(Config.GetInt("SysColor"), Language.GetString(LocString.TQCleared));
            else
                World.Player?.SendMessage(MsgLevel.Force, LocString.TQCleared);
        }

        /// <summary>Nur fuer Tests: Zustand zuruecksetzen.</summary>
        public static void Reset()
        {
            _hasTarget = false;
            _clientTarget = false;
            _lastTarget = null;
            _lastGroundTarget = null;
            _lastBeneTarget = null;
            _lastHarmTarget = null;
            _allowGround = false;
            _currentID = 0;
            _curFlags = 0;
            _filterCancel.Clear();
            EndIntercept();
            _previousID = 0;
            _previousGround = false;
            _prevFlags = 0;
            _queueTarget = null;
            _lastCombatant = Serial.Zero;
        }

        // ---- OneTimeTarget/Intercept (Razor CE: Targeting.OneTimeTarget) ----

        public static void OneTimeTarget(TargetResponseCallback onTarget)
        {
            OneTimeTarget(false, onTarget, null);
        }

        public static void OneTimeTarget(bool ground, TargetResponseCallback onTarget)
        {
            OneTimeTarget(ground, onTarget, null);
        }

        public static void OneTimeTarget(TargetResponseCallback onTarget, CancelTargetCallback onCancel)
        {
            OneTimeTarget(false, onTarget, onCancel);
        }

        public static void OneTimeTarget(bool ground, TargetResponseCallback onTarget, CancelTargetCallback onCancel)
        {
            if (_intercept && _onCancel != null)
            {
                _onCancel();
                CancelOneTimeTarget();
            }

            if (_hasTarget && _currentID != 0 && _currentID != LocalTargID)
            {
                _previousID = _currentID;
                _previousGround = _allowGround;
                _prevFlags = _curFlags;

                _filterCancel.Add(_previousID);
            }

            _intercept = true;
            _currentID = LocalTargID;
            _onTarget = onTarget;
            _onCancel = onCancel;

            _clientTarget = _hasTarget = true;
            ClientProxy.SendToClient(new Target(LocalTargID, ground));
        }

        public static void CancelOneTimeTarget()
        {
            _clientTarget = _hasTarget = false;

            ClientProxy.SendToClient(new CancelTarget(LocalTargID));
            EndIntercept();
        }

        private static void EndIntercept()
        {
            _intercept = false;
            _onTarget = null;
            _onCancel = null;
        }

        /// <summary>
        /// Razor CE: Targeting.OneTimeResponse — beantwortet einen Intercept-Target.
        /// Abweichung: synchron statt Timer.DelayedCallbackState.
        /// </summary>
        private static void OneTimeResponse(TargetInfo info)
        {
            TargetResponseCallback onTarget = _onTarget;
            CancelTargetCallback onCancel = _onCancel;

            EndIntercept();

            if (info != null)
            {
                if ((info.X == 0xFFFF && info.Y == 0xFFFF) && (info.Serial == 0 || info.Serial >= 0x80000000))
                {
                    onCancel?.Invoke();
                }
                else
                {
                    if (MacroManager.AcceptActions)
                        MacroManager.Action(new AbsoluteTargetAction(info));

                    onTarget?.Invoke(info.Type == 1 ? true : false, info.Serial,
                        new Point3D(info.X, info.Y, info.Z), info.Gfx);
                }
            }
        }

        /// <summary>Razor CE: Targeting.IsLastTarget — bei SmartLastTarget zaehlt
        /// das harmful Ziel (LTHilight faerbt dann den Gegner, nicht den Heiler).</summary>
        public static bool IsLastTarget(Mobile m)
        {
            if (m == null)
                return false;

            if (IsSmartTargetingEnabled())
                return _lastHarmTarget != null && _lastHarmTarget.Serial == m.Serial;

            return _lastTarget != null && _lastTarget.Serial == m.Serial;
        }

        /// <summary>Razor CE: IsBeneficialTarget.</summary>
        public static bool IsBeneficialTarget(Mobile m)
        {
            if (m == null)
                return false;

            if (IsSmartTargetingEnabled())
                return _lastBeneTarget != null && _lastBeneTarget.Serial == m.Serial;

            return _lastTarget != null && _lastTarget.Serial == m.Serial;
        }

        /// <summary>Razor CE: IsHarmfulTarget.</summary>
        public static bool IsHarmfulTarget(Mobile m)
        {
            if (m == null)
                return false;

            if (IsSmartTargetingEnabled())
                return _lastHarmTarget != null && _lastHarmTarget.Serial == m.Serial;

            return _lastTarget != null && _lastTarget.Serial == m.Serial;
        }

        public static void SetLastTarget(Serial serial)
        {
            TargetInfo targ = new TargetInfo();
            targ.Type = 0;
            targ.Flags = 0;
            targ.Serial = serial;

            if (serial.IsMobile)
            {
                Mobile m = World.FindMobile(serial);
                if (m != null)
                {
                    targ.X = m.Position.X;
                    targ.Y = m.Position.Y;
                    targ.Z = m.Position.Z;
                    targ.Gfx = m.Body;
                }
            }
            else if (serial.IsItem)
            {
                Item i = World.FindItem(serial);
                if (i != null)
                {
                    targ.X = i.Position.X;
                    targ.Y = i.Position.Y;
                    targ.Z = i.Position.Z;
                    targ.Gfx = i.ItemID;
                }
            }

            _lastGroundTarget = _lastTarget = targ;
        }

        // ---- Hotkey-Wrapper (Razor CE: TargetSelf/LastTarget/TargetSetLastTarget) ----

        /// <summary>Razor CE: Targeting.TargetSelf — mit Queue-Zweig (QueueTargets/forceQ).</summary>
        public static void TargetSelf()
        {
            TargetSelf(false);
        }

        public static void TargetSelf(bool forceQ)
        {
            if (World.Player == null)
                return;

            if (_hasTarget)
            {
                if (!DoTargetSelf())
                    ResendTarget();
            }
            else if (forceQ || Config.GetBool("QueueTargets"))
            {
                if (!forceQ)
                    World.Player.SendMessage(MsgLevel.Force, LocString.QueuedTS);

                _queueTarget = TargetSelfAction;
            }
        }

        /// <summary>Razor CE: Targeting.LastTarget — mit Queue-Zweig (QueueTargets/forceQ).</summary>
        public static void LastTarget()
        {
            LastTarget(false);
        }

        public static void LastTarget(bool forceQ)
        {
            if (World.Player == null)
                return;

            if (_hasTarget)
            {
                if (!DoLastTarget())
                    ResendTarget();
            }
            else if (forceQ || Config.GetBool("QueueTargets"))
            {
                if (!forceQ)
                    World.Player.SendMessage(MsgLevel.Force, LocString.QueuedLT);

                _queueTarget = LastTargetAction;
            }
        }

        /// <summary>
        /// Razor CE: SetLastTargetTo — Last Target direkt auf ein Mobile setzen
        /// (Basis der Closest/Random/Next/Prev-Hotkeys). Abweichung: keine
        /// Overhead-Meldung (OverheadTargetMessage — Anzeige-Batch).
        /// </summary>
        public static void SetLastTargetTo(Mobile m)
        {
            SetLastTargetTo(m, 0);
        }

        public static void SetLastTargetTo(Mobile m, byte flagType)
        {
            TargetInfo targ = new TargetInfo();
            _lastGroundTarget = _lastTarget = targ;

            // Razor CE: harm/bene folgt dem offenen Cursor bzw. dem flagType;
            // flagType 0 setzt beide (das Ziel gilt fuer alles).
            if ((_hasTarget && _curFlags == 1) || flagType == 1)
                _lastHarmTarget = targ;
            else if ((_hasTarget && _curFlags == 2) || flagType == 2)
                _lastBeneTarget = targ;
            else if (flagType == 0)
                _lastHarmTarget = _lastBeneTarget = targ;

            targ.Type = 0;
            targ.Flags = _hasTarget ? _curFlags : flagType;

            targ.Gfx = m.Body;
            targ.Serial = m.Serial;
            targ.X = m.Position.X;
            targ.Y = m.Position.Y;
            targ.Z = m.Position.Z;

            ClientProxy.SendToClient(new ChangeCombatant(m));
            _lastCombatant = m.Serial;
            World.Player.SendMessage(MsgLevel.Force, LocString.NewTargSet);

            OverheadTargetMessage(targ);

            // Offener Cursor wird sofort bedient. Razor CE schaltet Smart
            // Targeting dafuer kurz ab: das FRISCH gesetzte _lastTarget soll
            // antworten, nicht das alte harm/bene-Ziel.
            bool wasSmart = Config.GetBool("SmartLastTarget");
            if (wasSmart)
                Config.SetProperty("SmartLastTarget", false);
            LastTarget();
            if (wasSmart)
                Config.SetProperty("SmartLastTarget", true);
        }

        /// <summary>Razor CE: TargetSetLastTarget — naechstes Target wird 'Last Target'.</summary>
        public static void TargetSetLastTarget()
        {
            if (World.Player == null)
                return;

            World.Player.SendMessage(MsgLevel.Force, LocString.TargSetLT);
            OneTimeTarget(false, new TargetResponseCallback(OnSetLastTarget));
        }

        private static void OnSetLastTarget(bool location, Serial serial, Point3D p, ushort gfxid)
        {
            if (serial == World.Player?.Serial)
                return;

            TargetInfo targ = new TargetInfo
            {
                Type = (byte) (location ? 1 : 0),
                Flags = 0,
                Serial = serial,
                X = p.X,
                Y = p.Y,
                Z = p.Z,
                Gfx = gfxid
            };

            _lastGroundTarget = _lastTarget = targ;

            World.Player?.SendMessage(MsgLevel.Force, LocString.LTSet);
        }

        // ---- SmartLastTarget: Set Last Beneficial/Harmful (Razor CE 1:1) ----

        /// <summary>Razor CE: SetLastTargetBeneficial — naechstes Target wird
        /// das beneficial Last Target (nur mit SmartLastTarget).</summary>
        public static void SetLastTargetBeneficial()
        {
            if (!IsSmartTargetingEnabled())
            {
                World.Player?.SendMessage(MsgLevel.Error, "Smart Targeting is disabled");
                return;
            }

            if (World.Player != null)
            {
                OneTimeTarget(false, new TargetResponseCallback(OnSetLastTargetBeneficial));
                World.Player.SendMessage(MsgLevel.Force, LocString.NewBeneficialTarget);
            }
        }

        private static void OnSetLastTargetBeneficial(bool location, Serial serial, Point3D p, ushort gfxid)
        {
            if (serial == World.Player?.Serial)
                return;

            _lastBeneTarget = new TargetInfo
            {
                Flags = 0,
                Gfx = gfxid,
                Serial = serial,
                Type = (byte) (location ? 1 : 0),
                X = p.X,
                Y = p.Y,
                Z = p.Z
            };

            World.Player?.SendMessage(MsgLevel.Force, LocString.SetLTBene);
        }

        /// <summary>Razor CE: SetLastTargetHarmful — naechstes Target wird
        /// das harmful Last Target (nur mit SmartLastTarget).</summary>
        public static void SetLastTargetHarmful()
        {
            if (!IsSmartTargetingEnabled())
            {
                World.Player?.SendMessage(MsgLevel.Error, "Smart Targeting is disabled");
                return;
            }

            if (World.Player != null)
            {
                OneTimeTarget(false, new TargetResponseCallback(OnSetLastTargetHarmful));
                World.Player.SendMessage(MsgLevel.Force, LocString.NewHarmfulTarget);
            }
        }

        private static void OnSetLastTargetHarmful(bool location, Serial serial, Point3D p, ushort gfxid)
        {
            if (serial == World.Player?.Serial)
                return;

            _lastHarmTarget = new TargetInfo
            {
                Flags = 0,
                Gfx = gfxid,
                Serial = serial,
                Type = (byte) (location ? 1 : 0),
                X = p.X,
                Y = p.Y,
                Z = p.Z
            };

            World.Player?.SendMessage(MsgLevel.Force, LocString.SetLTHarm);
        }

        public static bool DoTargetSelf()
        {
            if (World.Player == null)
                return false;

            CancelClientTarget();
            _hasTarget = false;

            ClientProxy.SendToServer(new TargetResponse(_currentID, World.Player));

            return true;
        }

        public static bool DoLastTarget()
        {
            TargetInfo targ;
            if (IsSmartTargetingEnabled())
            {
                // Razor CE: der Cursor-Typ waehlt das Ziel — harmful (Flags 1)
                // nimmt das harmful Ziel, beneficial (Flags 2) das beneficial.
                if (_allowGround && _lastGroundTarget != null)
                    targ = _lastGroundTarget;
                else if (_curFlags == 1)
                    targ = _lastHarmTarget;
                else if (_curFlags == 2)
                    targ = _lastBeneTarget;
                else
                    targ = _lastTarget;

                if (targ == null)
                    targ = _lastTarget;
            }
            else
            {
                if (_allowGround && _lastGroundTarget != null)
                    targ = _lastGroundTarget;
                else
                    targ = _lastTarget;
            }

            if (targ == null)
                return false;

            Point3D pos = Point3D.Zero;
            if (targ.Serial.IsMobile)
            {
                Mobile m = World.FindMobile(targ.Serial);
                if (m != null)
                {
                    pos = m.Position;

                    targ.X = pos.X;
                    targ.Y = pos.Y;
                    targ.Z = pos.Z;
                }
            }
            else if (targ.Serial.IsItem)
            {
                Item i = World.FindItem(targ.Serial);
                if (i != null)
                {
                    pos = i.GetWorldPosition();

                    targ.X = i.Position.X;
                    targ.Y = i.Position.Y;
                    targ.Z = i.Position.Z;
                }
                else
                {
                    targ.X = targ.Y = targ.Z = 0;
                }
            }
            else
            {
                if (!_allowGround && (targ.Serial == Serial.Zero || targ.Serial >= 0x80000000))
                {
                    World.Player?.SendMessage(MsgLevel.Warning, "Last target was ground; current target does not allow ground.");
                    return false;
                }
            }

            // Razor CE: RangeCheckLT — Ziel ausserhalb LTRange wird NICHT
            // getargetet, sondern (mit QueueTargets) gemerkt; kommt es in
            // Reichweite, feuert CheckLastTargetRange die wartende Aktion.
            if (Config.GetBool("RangeCheckLT") &&
                (pos == Point3D.Zero || !Utility.InRange(World.Player.Position, pos, Config.GetInt("LTRange"))))
            {
                if (Config.GetBool("QueueTargets"))
                    _queueTarget = LastTargetAction;
                World.Player.SendMessage(MsgLevel.Warning, LocString.LTOutOfRange);
                return false;
            }

            CancelClientTarget();
            _hasTarget = false;

            targ.TargID = _currentID;

            ClientProxy.SendToServer(new TargetResponse(targ));
            return true;
        }

        public static void Target(TargetInfo info)
        {
            if (_intercept)
            {
                OneTimeResponse(info);
            }
            else if (_hasTarget)
            {
                info.TargID = _currentID;
                _lastGroundTarget = _lastTarget = info;
                ClientProxy.SendToServer(new TargetResponse(info));
            }

            CancelClientTarget();
            _hasTarget = false;
        }

        public static void Target(Point3D pt)
        {
            TargetInfo info = new TargetInfo();
            info.Type = 1;
            info.Flags = 0;
            info.Serial = 0;
            info.X = pt.X;
            info.Y = pt.Y;
            info.Z = pt.Z;
            info.Gfx = 0;

            Target(info);
        }

        public static void Target(Point3D pt, int gfx)
        {
            TargetInfo info = new TargetInfo();
            info.Type = 1;
            info.Flags = 0;
            info.Serial = 0;
            info.X = pt.X;
            info.Y = pt.Y;
            info.Z = pt.Z;
            info.Gfx = (ushort) (gfx & 0x3FFF);

            Target(info);
        }

        public static void Target(Serial s)
        {
            TargetInfo info = new TargetInfo();
            info.Type = 0;
            info.Flags = 0;
            info.Serial = s;

            if (s.IsItem)
            {
                Item item = World.FindItem(s);
                if (item != null)
                {
                    info.X = item.Position.X;
                    info.Y = item.Position.Y;
                    info.Z = item.Position.Z;
                    info.Gfx = item.ItemID;
                }
            }
            else if (s.IsMobile)
            {
                Mobile m = World.FindMobile(s);
                if (m != null)
                {
                    info.X = m.Position.X;
                    info.Y = m.Position.Y;
                    info.Z = m.Position.Z;
                    info.Gfx = m.Body;
                }
            }

            Target(info);
        }

        public static void Target(object o)
        {
            if (o is Item)
            {
                Item item = (Item) o;
                TargetInfo info = new TargetInfo();
                info.Type = 0;
                info.Flags = 0;
                info.Serial = item.Serial;
                info.X = item.Position.X;
                info.Y = item.Position.Y;
                info.Z = item.Position.Z;
                info.Gfx = item.ItemID;
                Target(info);
            }
            else if (o is Mobile)
            {
                Mobile m = (Mobile) o;
                TargetInfo info = new TargetInfo();
                info.Type = 0;
                info.Flags = 0;
                info.Serial = m.Serial;
                info.X = m.Position.X;
                info.Y = m.Position.Y;
                info.Z = m.Position.Z;
                info.Gfx = m.Body;
                Target(info);
            }
            else if (o is Serial)
            {
                Target((Serial) o);
            }
            else if (o is TargetInfo)
            {
                Target((TargetInfo) o);
            }
        }

        public static void CancelTarget()
        {
            CancelClientTarget();

            if (_hasTarget)
            {
                ClientProxy.SendToServer(new TargetCancelResponse(_currentID));
                _hasTarget = false;
            }
        }

        private static void CancelClientTarget()
        {
            if (_clientTarget)
            {
                _filterCancel.Add((uint) _currentID);
                ClientProxy.SendToClient(new CancelTarget(_currentID));
                _clientTarget = false;
            }
        }

        public static void ResendTarget()
        {
            if (!_clientTarget || !_hasTarget)
            {
                CancelClientTarget();
                _clientTarget = _hasTarget = true;
                ClientProxy.SendToClient(new Target(_currentID, _allowGround, _curFlags));
            }
        }

        /// <summary>Client -> Server 0x6C: Spieler hat geantwortet (oder abgebrochen).</summary>
        private static void TargetResponse(PacketReader p, PacketHandlerEventArgs args)
        {
            if (World.Player == null)
                return;

            TargetInfo info = new TargetInfo
            {
                Type = p.ReadByte(),
                TargID = p.ReadUInt32(),
                Flags = p.ReadByte(),
                Serial = p.ReadUInt32(),
                X = p.ReadUInt16(),
                Y = p.ReadUInt16(),
                Z = p.ReadInt16(),
                Gfx = p.ReadUInt16()
            };

            _clientTarget = false;

            OverheadTargetMessage(info);

            // check for cancel
            if (info.X == 0xFFFF && info.Y == 0xFFFF && (info.Serial <= 0 || info.Serial >= 0x80000000))
            {
                _hasTarget = false;

                if (_intercept)
                {
                    args.Block = true;
                    OneTimeResponse(info);

                    if (_previousID != 0)
                    {
                        _currentID = _previousID;
                        _allowGround = _previousGround;
                        _curFlags = _prevFlags;

                        _previousID = 0;

                        ResendTarget();
                    }
                }
                else if (_filterCancel.Contains((uint) info.TargID) || info.TargID == LocalTargID)
                {
                    args.Block = true;
                }

                _filterCancel.Clear();
                return;
            }

            if (_intercept)
            {
                if (info.TargID == LocalTargID)
                {
                    OneTimeResponse(info);

                    _hasTarget = false;
                    args.Block = true;

                    if (_previousID != 0)
                    {
                        _currentID = _previousID;
                        _allowGround = _previousGround;
                        _curFlags = _prevFlags;

                        _previousID = 0;

                        ResendTarget();
                    }

                    _filterCancel.Clear();

                    return;
                }
                else
                {
                    EndIntercept();
                }
            }

            _hasTarget = false;

            if (info.Serial != World.Player.Serial)
            {
                if (info.Serial.IsValid)
                {
                    // only let lasttarget be a non-ground target
                    _lastTarget = info;

                    // Razor CE: SmartLastTarget merkt sich harm/bene getrennt
                    // (Cursor-Flags des beantworteten Targets).
                    if (info.Flags == 1)
                        _lastHarmTarget = info;
                    else if (info.Flags == 2)
                        _lastBeneTarget = info;
                }

                _lastGroundTarget = info; // ground target is the true last target
            }

            if (MacroManager.AcceptActions)
                MacroManager.Action(new AbsoluteTargetAction(info));

            _filterCancel.Clear();
        }

        /// <summary>Server -> Client 0x6C: Target-Cursor angefordert.</summary>
        private static void NewTarget(PacketReader p, PacketHandlerEventArgs args)
        {
            _allowGround = p.ReadBoolean(); // allow ground
            _currentID = p.ReadUInt32(); // target uid
            _curFlags = p.ReadByte(); // flags
            // the rest of the packet is 0s

            // check for a server cancel command
            if (!_allowGround && _currentID == 0 && _curFlags == 3)
            {
                _hasTarget = false;
                _clientTarget = false;
                return;
            }

            _hasTarget = true;
            _clientTarget = false;

            if (_queueTarget == null && MacroManager.AcceptActions &&
                MacroManager.Action(new WaitForTargetAction()))
            {
                // Ein wartendes Macro hat den Cursor konsumiert — nicht an den
                // Client durchreichen (Razor CE blockt hier ebenfalls).
                args.Block = true;
            }
            else if (_queueTarget != null && _queueTarget())
            {
                // Vorgemerktes Ziel (Bandage Self/LT, QueueTargets) bedient den
                // Cursor sofort — nicht an den Client durchreichen (wie CE).
                ClearQueue();
                args.Block = true;
            }
            else
            {
                _clientTarget = true;
            }
        }
    }
}
