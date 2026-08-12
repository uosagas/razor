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

// Portiert aus Razor CE (Razor/Core/Player.cs).
// STUBS/ENTFERNT gegenueber Razor CE (nur Weltzustand, keine Seiteneffekte):
//  * SendMessage/Say/Whisper/Yell/Emote/Guild/Alliance (Client-/Server-Injection)
//  * AutoOpenDoors, StealthSteps, Scavenger/DragDrop in OnPositionChanging
//  * CriminalTimer, SeasonTimer, DoubleClick/UseItem (ActionQueue), Virtue/Rename
//  * Gump-/Prompt-/Menu-Zustand bleibt als reine Daten erhalten (ohne GumpCollection)
// Feature-Paritaet (2026-07-17): MaleSounds/FemaleSounds-Enums nachgeruestet
// (PlayEmoteSound im MessageManager).
//  * Skill-Anzahl: Razor CE nutzt Ultima.Skills.TotalSkills() aus den MUL-Dateien;
//    hier fix 58 (Standard-Skilltabelle, TODO: via IClientServices ermitteln)

using System;
using System.Collections.Generic;

namespace Assistant
{
    public enum LockType : byte
    {
        Up = 0,
        Down = 1,
        Locked = 2
    }

    public enum MsgLevel
    {
        Debug = 0,
        Info = 0,
        Warning = 1,
        Error = 2,
        Force = 3,
        Friend = 4
    }

    public partial class Skill
    {
        private LockType _lock;
        private ushort _value;
        private ushort _base;
        private ushort _cap;
        private short _delta;
        private int _index;

        public Skill(int idx)
        {
            _index = idx;
        }

        public int Index
        {
            get { return _index; }
        }

        public LockType Lock
        {
            get { return _lock; }
            set { _lock = value; }
        }

        public ushort FixedValue
        {
            get { return _value; }
            set { _value = value; }
        }

        public ushort FixedBase
        {
            get { return _base; }
            set
            {
                _delta += (short) (value - _base);
                _base = value;
            }
        }

        public ushort FixedCap
        {
            get { return _cap; }
            set { _cap = value; }
        }

        public double Value
        {
            get { return _value / 10.0; }
            set { _value = (ushort) (value * 10.0); }
        }

        public double Base
        {
            get { return _base / 10.0; }
            set { _base = (ushort) (value * 10.0); }
        }

        public double Cap
        {
            get { return _cap / 10.0; }
            set { _cap = (ushort) (value * 10.0); }
        }

        public double Delta
        {
            get { return _delta / 10.0; }
            set { _delta = (short) (value * 10); }
        }
    }

    // Razor CE 1:1: Sound-IDs der klassischen Emote-Sounds (PlayEmoteSound —
    // ein *cough* etc. eines Mobiles spielt den passenden Sound lokal ab).
    public enum MaleSounds
    {
        Ah = 0x419,
        Ahha = 0x41A,
        Applaud = 0x41B,
        Blownose = 0x41C,
        Burp = 0x41D,
        Cheer = 0x41E,
        Clear_Throat = 0x41F,
        Cough = 0x420,
        Coughbs = 0x421,
        Cry = 0x422,
        Death_01 = 0x423,
        Death_02 = 0x424,
        Death_03 = 0x425,
        Death_04 = 0x426,
        Death_05 = 0x427,
        Fart = 0x428,
        Gasp = 0x429,
        Giggle = 0x42A,
        Groan = 0x42B,
        Growl = 0x42C,
        Hey = 0x42D,
        Hiccup = 0x42E,
        Huh = 0x42F,
        Kiss = 0x430,
        Laugh = 0x431,
        No = 0x432,
        Oh = 0x433,
        Oomph_01 = 0x434,
        Oomph_02 = 0x435,
        Oomph_03 = 0x436,
        Oomph_04 = 0x437,
        Oomph_05 = 0x438,
        Oomph_06 = 0x439,
        Oomph_07 = 0x43A,
        Oomph_08 = 0x43B,
        Oomph_09 = 0x43C,
        Oooh = 0x43D,
        Oops = 0x43E,
        Puke = 0x43F,
        Scream = 0x440,
        Shush = 0x441,
        Sigh = 0x442,
        Sneeze = 0x443,
        Sniff = 0x444,
        Snore = 0x445,
        Spit = 0x446,
        Whistle = 0x447,
        Yawn = 0x448,
        Yea = 0x449,
        Yell = 0x44A
    }

    public enum FemaleSounds
    {
        Ah = 0x30A,
        Ahha = 0x30B,
        Applaud = 0x30C,
        Blownose = 0x30D,
        Burp = 0x30E,
        Cheer = 0x30F,
        Clear_Throat = 0x310,
        Cough = 0x311,
        Coughbs = 0x312,
        Cry = 0x313,
        Death_01 = 0x314,
        Death_02 = 0x315,
        Death_03 = 0x316,
        Death_04 = 0x317,
        Fart = 0x318,
        Gasp = 0x319,
        Giggle = 0x31A,
        Groan = 0x31B,
        Growl = 0x31C,
        Hey = 0x31D,
        Hiccup = 0x31E,
        Huh = 0x31F,
        Kiss = 0x320,
        Laugh = 0x321,
        No = 0x322,
        Oh = 0x323,
        Oomph_01 = 0x324,
        Oomph_02 = 0x325,
        Oomph_03 = 0x326,
        Oomph_04 = 0x327,
        Oomph_05 = 0x328,
        Oomph_06 = 0x329,
        Oomph_07 = 0x32A,
        Oooh = 0x32B,
        Oops = 0x32C,
        Puke = 0x32D,
        Scream = 0x32E,
        Shush = 0x32F,
        Sigh = 0x330,
        Sneeze = 0x331,
        Sniff = 0x332,
        Snore = 0x333,
        Spit = 0x334,
        Whistle = 0x335,
        Yawn = 0x336,
        Yea = 0x337,
        Yell = 0x338
    }

    public partial class PlayerData : Mobile
    {
        /// <summary>
        /// TODO: Razor CE ermittelt die Skill-Anzahl aus skills.idx (Ultima.Skills.TotalSkills()).
        /// </summary>
        public const int MaxSkills = 58;

        public int VisRange = 18;

        public int MultiVisRange
        {
            get { return VisRange + 5; }
        }

        private int _maxWeight = -1;

        private short _fireResist, _coldResist, _poisonResist, _energyResist, _luck;
        private ushort _damageMin, _damageMax;

        private ushort _str, _dex, _int;
        private LockType _strLock, _dexLock, _intLock;
        private uint _gold;
        private ushort _weight;
        private Skill[] _skills;
        private ushort _ar;
        private ushort _statCap;
        private byte _followers;
        private byte _followersMax;
        private int _tithe;
        private sbyte _localLight;
        private byte _globalLight;
        private uint _features;
        private byte _season;
        private byte _defaultSeason;
        private int[] _mapPatches = new int[10];

        private bool _skillsSent;

        private List<uint> _openedCorpses = new List<uint>();

        public List<uint> OpenedCorpses
        {
            get { return _openedCorpses; }
        }

        public PlayerData(Serial serial) : base(serial)
        {
            _skills = new Skill[MaxSkills];
            for (int i = 0; i < _skills.Length; i++)
                _skills[i] = new Skill(i);
        }

        public ushort Str
        {
            get { return _str; }
            set { _str = value; }
        }

        public ushort Dex
        {
            get { return _dex; }
            set { _dex = value; }
        }

        public ushort Int
        {
            get { return _int; }
            set { _int = value; }
        }

        public uint Gold
        {
            get { return _gold; }
            set { _gold = value; }
        }

        public ushort Weight
        {
            get { return _weight; }
            set { _weight = value; }
        }

        public ushort MaxWeight
        {
            get
            {
                if (_maxWeight == -1)
                    return (ushort) ((_str * 3.5) + 40);
                else
                    return (ushort) _maxWeight;
            }
            set { _maxWeight = value; }
        }

        public short FireResistance
        {
            get { return _fireResist; }
            set { _fireResist = value; }
        }

        public short ColdResistance
        {
            get { return _coldResist; }
            set { _coldResist = value; }
        }

        public short PoisonResistance
        {
            get { return _poisonResist; }
            set { _poisonResist = value; }
        }

        public short EnergyResistance
        {
            get { return _energyResist; }
            set { _energyResist = value; }
        }

        public short Luck
        {
            get { return _luck; }
            set { _luck = value; }
        }

        public ushort DamageMin
        {
            get { return _damageMin; }
            set { _damageMin = value; }
        }

        public ushort DamageMax
        {
            get { return _damageMax; }
            set { _damageMax = value; }
        }

        public LockType StrLock
        {
            get { return _strLock; }
            set { _strLock = value; }
        }

        public LockType DexLock
        {
            get { return _dexLock; }
            set { _dexLock = value; }
        }

        public LockType IntLock
        {
            get { return _intLock; }
            set { _intLock = value; }
        }

        public ushort StatCap
        {
            get { return _statCap; }
            set { _statCap = value; }
        }

        public ushort AR
        {
            get { return _ar; }
            set { _ar = value; }
        }

        public byte Followers
        {
            get { return _followers; }
            set { _followers = value; }
        }

        public byte FollowersMax
        {
            get { return _followersMax; }
            set { _followersMax = value; }
        }

        public int Tithe
        {
            get { return _tithe; }
            set { _tithe = value; }
        }

        public Skill[] Skills
        {
            get { return _skills; }
        }

        public bool SkillsSent
        {
            get { return _skillsSent; }
            set { _skillsSent = value; }
        }

        public override void OnPositionChanging(Point3D oldPos)
        {
            // Werktreu zu Razor CE: Mobiles ausserhalb der Sichtweite verschwinden,
            // Boden-Items ausserhalb der (Multi-)Sichtweite ebenfalls.
            // ENTFERNT: StealthSteps, AutoOpenDoors, Targeting-Checks, Scavenger, MapWindow.
            List<Mobile> mlist = new List<Mobile>(World.Mobiles.Values);
            for (int i = 0; i < mlist.Count; i++)
            {
                Mobile m = mlist[i];
                if (m != this)
                {
                    if (!Utility.InRange(m.Position, Position, VisRange))
                        m.Remove();
                }
            }

            List<Item> ilist = new List<Item>(World.Items.Values);
            for (int i = 0; i < ilist.Count; i++)
            {
                Item item = ilist[i];
                if (item.Deleted || item.Container != null)
                    continue;

                int dist = Utility.Distance(item.GetWorldPosition(), Position);
                if (dist > MultiVisRange || (!item.IsMulti && dist > VisRange))
                    item.Remove();
            }

            base.OnPositionChanging(oldPos);
        }

        public override void OnMapChange(byte old, byte cur)
        {
            // Werktreu zu Razor CE: Mobiles anderer Maps entfernen, Boden-Items
            // verwerfen, eigene getragenen Items wieder registrieren.
            // ENTFERNT: Counter.Reset, AutoSearch, UOAssist/MapWindow-Benachrichtigung.
            List<Mobile> list = new List<Mobile>(World.Mobiles.Values);
            for (int i = 0; i < list.Count; i++)
            {
                Mobile m = list[i];
                if (m != this && m.Map != cur)
                    m.Remove();
            }

            World.Items.Clear();
            for (int i = 0; i < Contains.Count; i++)
            {
                Item item = (Item) Contains[i];
                World.AddItem(item);
                item.Contains.Clear();
            }
        }

        // Aktueller Gump-/Menu-/Prompt-Zustand (reine Daten, keine UI).
        public uint CurrentGumpS, CurrentGumpI;
        public bool HasGump;
        public bool HasCompressedGump;

        /// <summary>Razor CE (Handlers.cs): letzte echte Gump-Antwort des
        /// Spielers — Quelle fuer "Use Last Gump Response" im Macro-Menue.
        /// Gesetzt in MacroHandlers.GumpResponse (auch ohne Recording).</summary>
        public Assistant.Macros.GumpResponseAction LastGumpResponseAction;

        /// <summary>Razor CE: PlayerData.GumpInfo — Serial + geparste Texte eines offenen Gumps.</summary>
        public sealed class GumpInfo
        {
            public uint GumpSerial;
            public System.Collections.Generic.List<string> Strings =
                new System.Collections.Generic.List<string>();

            public GumpInfo(uint serial)
            {
                GumpSerial = serial;
            }
        }

        /// <summary>
        /// Razor CE: PlayerData.GumpList — alle offenen Gumps (GumpID ->
        /// GumpInfo), gepflegt von MacroHandlers.SendGump/CompressedGump.
        /// Von gumpclose/gumpexists/ingump genutzt.
        /// </summary>
        public System.Collections.Generic.Dictionary<uint, GumpInfo> GumpList { get; } =
            new System.Collections.Generic.Dictionary<uint, GumpInfo>();
        public List<string> CurrentGumpStrings = new List<string>();
        public string CurrentGumpRawData;
        public uint CurrentMenuS;
        public ushort CurrentMenuI;
        public bool HasMenu;

        public bool HasPrompt;
        public uint PromptSenderSerial;
        public uint PromptID;
        public uint PromptType;
        public string PromptInputText;

        private ushort _speechHue;

        public ushort SpeechHue
        {
            get { return _speechHue; }
            set { _speechHue = value; }
        }

        // ⚠️ STUB (siehe BuffDebuff.cs): wird erst vom 0xDF-Handler befuellt.
        // Bis dahin leer -> Script-Ausdruecke findbuff/finddebuff liefern false.
        public System.Collections.Generic.List<BuffDebuff> BuffsDebuffs { get; } =
            new System.Collections.Generic.List<BuffDebuff>();

        public sbyte LocalLightLevel
        {
            get { return _localLight; }
            set { _localLight = value; }
        }

        public byte GlobalLightLevel
        {
            get { return _globalLight; }
            set { _globalLight = value; }
        }

        public enum SeasonFlag
        {
            Spring,
            Summer,
            Fall,
            Winter,
            Desolation
        }

        public byte Season
        {
            get { return _season; }
            set { _season = value; }
        }

        public byte DefaultSeason
        {
            get { return _defaultSeason; }
            set { _defaultSeason = value; }
        }

        public uint Features
        {
            get { return _features; }
            set { _features = value; }
        }

        public int[] MapPatches
        {
            get { return _mapPatches; }
            set { _mapPatches = value; }
        }

        private int _lstSkill = -1;

        public int LastSkill
        {
            get { return _lstSkill; }
            set { _lstSkill = value; }
        }

        private Serial _lastObj = Serial.Zero;

        public Serial LastObject
        {
            get { return _lastObj; }
            set { _lastObj = value; }
        }

        private int _lastSpell = -1;

        public int LastSpell
        {
            get { return _lastSpell; }
            set { _lastSpell = value; }
        }

        // --- Phase 2c: Aktions-Pfade (portiert aus Razor CE Core/Player.cs) ---

        /// <summary>
        /// Razor CE: PlayerData.SendMessage — injiziert eine Systemmeldung in den
        /// Client (Unicode 0xAE). Port-Vereinfachung: zusaetzlich Konsole; ohne
        /// gebundenen Client nur Konsole.
        /// TODO Razor CE: Hue je MsgLevel + "FilterRazorMessages".
        /// </summary>
        public void SendMessage(MsgLevel lvl, string format, params object[] args)
        {
            if (args != null && args.Length > 0)
                format = string.Format(format, args);

            Console.WriteLine($"[Razor] {lvl}: {format}");

            // Razor CE: Meldung als System-Message in den Client injizieren, damit
            // sie im Spiel sichtbar ist (UOAssist.cs: UnicodeMessage 0xFFFFFFFF).
            if (ClientProxy.IsBound)
            {
                ushort hue = HueForLevel(lvl);
                ClientProxy.SendToClient(new UnicodeMessage(
                    0xFFFFFFFF, -1, MessageType.Regular, hue, 3, "ENU", "System", format));
            }
        }

        private static ushort HueForLevel(MsgLevel lvl)
        {
            // Farbgebung wie Razor CE (SystemMessages/Config): Fehler rot, Warnung
            // gelb, sonst der Standard-Grauton.
            switch (lvl)
            {
                case MsgLevel.Error: return 0x21;   // rot
                case MsgLevel.Warning: return 0x35;  // orange/gelb
                case MsgLevel.Force: return 0x90;    // hervorgehoben
                default: return 0x3B2;               // Standard-System-Grau
            }
        }

        public void SendMessage(string format, params object[] args)
        {
            SendMessage(MsgLevel.Info, format, args);
        }

        /// <summary>Razor CE: SendMessage(int hue, string) — Systemmeldung mit explizitem Farbton (sysmsg-Kommando).</summary>
        public void SendMessage(int hue, string text)
        {
            Console.WriteLine($"[Razor] {text}");

            if (ClientProxy.IsBound)
            {
                ClientProxy.SendToClient(new UnicodeMessage(
                    0xFFFFFFFF, -1, MessageType.Regular, hue, 3, Language.CliLocName, "System", text));
            }

            Assistant.Core.SystemMessages.Add(text);
        }

        /// <summary>Razor CE: SendMessage(MsgLevel, LocString, args) — hier ueber den Language-Stub.</summary>
        public void SendMessage(MsgLevel lvl, LocString loc, params object[] args)
        {
            SendMessage(lvl, Language.Format(loc, args));
        }

        public void SendMessage(LocString loc, params object[] args)
        {
            SendMessage(MsgLevel.Info, Language.Format(loc, args));
        }

        /// <summary>
        /// Razor CE: PlayerData.Say — Sprech-Paket 0xAD mit Keyword-Encoding
        /// (Pet-Kommandos u. ae.). ENTFERNT: nichts; die Keyword-Tabelle ist
        /// eine eingebettete Teilmenge (siehe EncodedSpeech).
        /// </summary>
        public void Say(int hue, string msg)
        {
            List<ushort> keywords = Assistant.Core.EncodedSpeech.GetKeywords(msg);

            System.Collections.ArrayList keys = new System.Collections.ArrayList(keywords.Count);
            for (int i = 0; i < keywords.Count; i++)
            {
                if (i == 0)
                    keys.Add(keywords[i]); // erstes Element als ushort (12-Bit-Header)
                else
                    keys.Add((byte) keywords[i]);
            }

            // ClientUniMessage setzt bei keys.Count > 1 selbst MessageType.Encoded.
            ClientProxy.SendToServer(new ClientUniMessage(MessageType.Regular, hue, 3, Language.CliLocName, keys, msg));
        }

        public void Say(string msg)
        {
            Say(Config.GetInt("SpeechHue"), msg);
        }

        // Razor CE: PlayerData.Whisper/Yell/Emote/Guild/Alliance — reine
        // Sprechpaket-Varianten (0xAD) mit anderem MessageType, 1:1 uebernommen
        // (fuer die Script-Kommandos whisper/yell/emote/guild/alliance).

        public void Whisper(string msg, int hue)
        {
            ClientProxy.SendToServer(new ClientUniMessage(MessageType.Whisper, hue, 3,
                Language.CliLocName, new System.Collections.ArrayList(), msg));
        }

        public void Yell(string msg, int hue)
        {
            ClientProxy.SendToServer(new ClientUniMessage(MessageType.Yell, hue, 3,
                Language.CliLocName, new System.Collections.ArrayList(), msg));
        }

        public void Emote(string msg, int hue)
        {
            msg = $"*{msg}*";

            ClientProxy.SendToServer(new ClientUniMessage(MessageType.Emote, hue, 3,
                Language.CliLocName, new System.Collections.ArrayList(), msg));
        }

        public void Guild(string msg, int hue)
        {
            ClientProxy.SendToServer(new ClientUniMessage(MessageType.Guild, hue, 3,
                Language.CliLocName, new System.Collections.ArrayList(), msg));
        }

        public void Alliance(string msg, int hue)
        {
            ClientProxy.SendToServer(new ClientUniMessage(MessageType.Alliance, hue, 3,
                Language.CliLocName, new System.Collections.ArrayList(), msg));
        }

        /// <summary>
        /// Razor CE: PlayerData.UseItem — sucht rekursiv (Container in
        /// Container) ein Item mit der ItemID und doppelklickt es.
        /// ENTFERNT: FeatureBit.PotionHotkeys-Check (keine Server-Negotiation im Port).
        /// </summary>
        public bool UseItem(Item cont, ushort find)
        {
            if (cont == null)
                return false;

            for (int i = 0; i < cont.Contains.Count; i++)
            {
                Item item = (Item) cont.Contains[i];

                if (item.ItemID == find)
                {
                    DoubleClick(item);
                    return true;
                }

                if (item.Contains != null && item.Contains.Count > 0)
                {
                    if (UseItem(item, find))
                        return true;
                }
            }

            return false;
        }

        /// <summary>Razor CE: PlayerData.ResponsePrompt — beantwortet den offenen Server-Prompt (0xC2).</summary>
        public void ResponsePrompt(string text)
        {
            ClientProxy.SendToServer(new PromptResponse(PromptSenderSerial, PromptID, 1, "ENU", text));

            PromptInputText = text;
            HasPrompt = false;
        }

        /// <summary>
        /// Razor CE: PlayerData.DoubleClick(object) — laeuft ueber die ActionQueue
        /// (ObjectDelay). ENTFERNT: PotionEquip-Sonderfall (FeatureBits).
        /// </summary>
        /// <summary>
        /// Razor CE: PlayerData.AutoOpenDoors — steht direkt vor der Tuer eine
        /// Kachel in Blickrichtung, geht das Open-Door-Macro (0x12/0x58) raus.
        /// Aufgerufen bei Positionswechsel (Plugin) und bei Drehungen (0x02-
        /// Viewer) — NICHT bei beidem fuer denselben Schritt, sonst wuerde die
        /// Tuer doppelt getoggelt (auf und wieder zu).
        /// </summary>
        public void AutoOpenDoors()
        {
            if (!Config.GetBool("AutoOpenDoors"))
                return;

            if (!Visible && !Config.GetBool("AutoOpenDoorWhenHidden"))
                return;

            // Nur Kardinalrichtungen (CE) und keine Geister.
            if (IsGhost || ((int) (Direction & Direction.Mask)) % 2 != 0)
                return;

            int x = Position.X, y = Position.Y, z = Position.Z;
            Utility.Offset(Direction, ref x, ref y);

            foreach (Item s in World.Items.Values)
            {
                if (s.IsDoor && s.Position.X == x && s.Position.Y == y &&
                    s.Position.Z - 15 <= z && s.Position.Z + 15 >= z)
                {
                    ClientProxy.SendToServer(new OpenDoorMacro());
                    return;
                }
            }
        }

        public static bool DoubleClick(object clicked)
        {
            return DoubleClick(clicked, true);
        }

        // Razor CE: PlayerData.InvokeVirtues / InvokeVirtue / RenameMobile — 1:1
        // (Virtue-Anrufung 0x12/0xF4, Mobile-Rename 0x75; Script-Kommandos
        // virtue/rename).

        public enum InvokeVirtues
        {
            Honor = 0x01,
            Sacrifice = 0x02,
            Valor = 0x03
        }

        public static void InvokeVirtue(InvokeVirtues virtue)
        {
            ClientProxy.SendToServer(new VirtueRequest((byte) virtue));
        }

        public static void RenameMobile(Serial serial, string newName)
        {
            ClientProxy.SendToServer(new RenamePacket(serial.Value, newName));
        }

        public static bool DoubleClick(object clicked, bool silent)
        {
            Serial s;
            if (clicked is Mobile)
                s = ((Mobile) clicked).Serial.Value;
            else if (clicked is Item)
                s = ((Item) clicked).Serial.Value;
            else if (clicked is Serial)
                s = ((Serial) clicked).Value;
            else
                s = Serial.Zero;

            if (s != Serial.Zero)
            {
                // Razor CE: PotionEquip — vor dem Trinken eine Hand freimachen
                // (nicht fuer Explosionstraenke), PotionReequip zieht danach
                // wieder an. Läuft ueber den DragDrop-Queue-Mechanismus.
                Item freed = null;
                Item pack = World.Player?.Backpack;
                if (s.IsItem && pack != null && Config.GetBool("PotionEquip"))
                {
                    Item potion = World.FindItem(s);
                    if (potion != null && potion.IsPotion && potion.ItemID != 3853)
                    {
                        Item left = World.Player.GetItemOnLayer(Layer.LeftHand);
                        Item right = World.Player.GetItemOnLayer(Layer.RightHand);

                        if (left != null && (right != null || left.IsTwoHanded))
                            freed = left;
                        else if (right != null && right.IsTwoHanded)
                            freed = right;

                        if (freed != null)
                        {
                            if (DragDropManager.HasDragFor(freed.Serial))
                                freed = null;
                            else
                                DragDropManager.DragDrop(freed, pack);
                        }
                    }
                }

                if (s.IsItem && World.Player != null)
                    World.Player.LastObject = s;

                ActionQueue.DoubleClick(silent, s);

                if (freed != null && Config.GetBool("PotionReequip"))
                    DragDropManager.DragDrop(freed, World.Player, freed.Layer, true);

                return true;
            }

            return false;
        }
    }
}
