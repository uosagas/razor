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

// Portiert aus Razor CE (Razor/HotKeys/HotKeys.cs) — Phase 3c.
// ABWEICHUNGEN vom Original (dokumentiert):
//  * Kein WinForms: KeyData haelt Category/SubCat statt eines TreeNode;
//    den Baum baut die Avalonia-UI aus HotKey.List (Reihenfolge = CE).
//  * Add() ist idempotent (ersetzt eine bestehende Registrierung mit
//    gleicher Identitaet) — noetig, weil unsere Initialize()-Ketten in
//    Tests mehrfach laufen koennen. CE registriert strikt einmalig.
//  * Kein GetAsyncKeyState: der zweite "any key currently down"-Durchlauf
//    aus CE OnKeyDown entfaellt; die Modifier kommen fertig als ModKeys
//    (vom SDL-KMOD gemappt) herein.
//  * Kein ScriptManager/Command-Validierung gegen Script-Commands.
//  * NEU: Defaults (SetDefault) — z. B. "Show Razor" = Strg+Shift+U; greifen
//    nach ClearAll()/Profil-Load, wenn das Profil keine Belegung enthaelt.
//  * NEU: Global-Flag — solche Hotkeys feuern auch ohne World.Player
//    (z. B. "Show Razor" im Login-Screen). CE bricht ohne Spieler immer ab.
//  * NEU: Nicht zuordenbare <key>-Eintraege alter Profile werden roh
//    konserviert und beim Speichern unveraendert zurueckgeschrieben
//    (CE wuerde Belegungen fuer nicht registrierte Hotkeys verwerfen).
//  * Profil-Sektion "hotkeys" wird ueber ProfileSections.Register angebunden
//    (statt Festverdrahtung in UI/Config.cs); Format ist byte-kompatibel:
//    <key mod="6" key="85" send="False" command="">L:1058</key>

using System;
using System.Collections.Generic;
using System.Text;
using System.Xml;

namespace Assistant
{
    [Flags]
    public enum ModKeys : int
    {
        None = 0,
        Alt = 0x0001,
        Control = 0x0002,
        Shift = 0x0004
    }

    public delegate void HotKeyCallback();

    public delegate void HotKeyCallbackState(ref object state);

    public enum HKCategory
    {
        None = 0,
        Items,
        Targets,
        Agents,
        Dress,
        Macros,
        Spells,
        Skills,
        Misc,
        Friends,
        Scripts
    }

    public enum HKSubCat
    {
        None = 0,

        // spells: (these all are # oriented, so be careful)
        SpellOffset,
        FirstC,
        SecondC,
        ThirdC,
        FourthC,
        FifthC,
        SixthC,
        SeventhC,
        EighthC,
        NecroC = SpellOffset + 10,
        PaladinC = SpellOffset + 20,

        //items
        Potions,

        // misc:
        SpecialMoves,

        // more # oriented, CAUTION!
        BushidoC = SpellOffset + 40,
        NinjisuC = SpellOffset + 50,
        SpellWeaveC = SpellOffset + 60,
        MystC = SpellOffset + 65,
        MasteriesC = SpellOffset + 70,

        // pet commands
        PetCommands = 780,

        SubTargetCriminal = 796,
        SubTargetEnemy,
        SubTargetFriendly,
        SubTargetGrey,
        SubTargetInnocent,
        SubTargetMurderer,
        SubTargetNonFriendly,
    }

    public class KeyData
    {
        private readonly int _name;
        private readonly string _sName;
        private readonly HotKeyCallback _callback;
        private readonly HotKeyCallbackState _callbackState;
        private bool _sendToUO;
        private int _key;
        private ModKeys _mod;
        private object _state;
        private string _command;

        private int _defaultKey;
        private ModKeys _defaultMod;

        public object pState => _state;

        private DateTime _lastTriggerTime = DateTime.MinValue;

        public string DisplayName => _name != 0 ? Language.GetString(_name) : _sName;

        public int LocName => _name;

        public string StrName => _sName;

        public HKCategory Category { get; }

        public HKSubCat SubCat { get; }

        /// <summary>NEU: feuert auch ohne World.Player (z. B. "Show Razor").</summary>
        public bool Global { get; set; }

        public string Command
        {
            get => _command;
            set => _command = value;
        }

        public KeyData(int name, HKCategory cat, HKSubCat sub, HotKeyCallback call)
        {
            _name = name;
            _sName = null;
            _callback = call;
            _callbackState = null;
            Category = cat;
            SubCat = sub;
        }

        public KeyData(int name, HKCategory cat, HKSubCat sub, HotKeyCallbackState call, object state)
        {
            _name = name;
            _sName = null;
            _callback = null;
            _callbackState = call;
            _state = state;
            Category = cat;
            SubCat = sub;
        }

        public KeyData(string name, HKCategory cat, HKSubCat sub, HotKeyCallback call)
        {
            _name = 0;
            _sName = name;
            _callback = call;
            _callbackState = null;
            Category = cat;
            SubCat = sub;
        }

        public KeyData(string name, HKCategory cat, HKSubCat sub, HotKeyCallbackState call, object state)
        {
            _name = 0;
            _sName = name;
            _callback = null;
            _callbackState = call;
            _state = state;
            Category = cat;
            SubCat = sub;
        }

        public void Callback()
        {
            // Schutz gegen "Doppel-Druecken" mancher Tastaturen (wie CE).
            if (_lastTriggerTime + TimeSpan.FromMilliseconds(20) <= DateTime.UtcNow)
            {
                _lastTriggerTime = DateTime.UtcNow;

                if (_callback != null)
                    _callback();
                else if (_callbackState != null)
                    _callbackState(ref _state);
            }
        }

        public bool SendToUO
        {
            get => _sendToUO;
            set => _sendToUO = value;
        }

        public int Key
        {
            get => _key;
            set => _key = value;
        }

        public ModKeys Mod
        {
            get => _mod;
            set => _mod = value;
        }

        /// <summary>
        /// NEU: Standard-Belegung setzen (greift sofort, wenn unbelegt, und
        /// nach jedem ClearAll()/Profil-Load ohne eigene Belegung).
        /// </summary>
        public void SetDefault(int key, ModKeys mod)
        {
            _defaultKey = key;
            _defaultMod = mod;

            if (_key == 0)
            {
                _key = key;
                _mod = mod;
            }
        }

        /// <summary>Auf die Standard-Belegung (oder unbelegt) zuruecksetzen.</summary>
        public void ResetToDefault()
        {
            _key = _defaultKey;
            _mod = _defaultMod;
            _sendToUO = false;
        }

        public string KeyString(bool smallNames = false)
        {
            if (Key != 0)
            {
                StringBuilder sb = new StringBuilder();
                bool np = false;

                if ((Mod & ModKeys.Control) != 0)
                {
                    sb.Append(smallNames ? "Ctrl" : "Control");
                    np = true;
                }

                if ((Mod & ModKeys.Alt) != 0)
                {
                    if (np)
                        sb.Append("+");
                    sb.Append("Alt");
                    np = true;
                }

                if ((Mod & ModKeys.Shift) != 0)
                {
                    if (np)
                        sb.Append("+");
                    sb.Append(smallNames ? "Sft" : "Shift");
                    np = true;
                }

                if (np)
                    sb.Append("+");

                switch (Key)
                {
                    case -1:
                        sb.Append("Wheel UP");
                        break;
                    case -2:
                        sb.Append("Wheel DOWN");
                        break;
                    case -3:
                        sb.Append("Middle Button");
                        break;
                    case -4:
                        sb.Append("XButton 1");
                        break;
                    case -5:
                        sb.Append("XButton 2");
                        break;
                    default:
                        sb.Append(((Keys) Key).ToString());
                        break;
                }

                return sb.ToString();
            }

            return null;
        }

        public override string ToString()
        {
            if (Key != 0)
                return $"{DisplayName} ({KeyString()})";

            return $"{DisplayName} ({Language.GetString(LocString.NotAssigned)})";
        }
    }

    public class HotKey
    {
        private static readonly List<KeyData> m_List = new List<KeyData>();
        private static bool m_Enabled = true;
        private static KeyData m_HK_En;
        private static KeyData _hotKeyEnableToggle;
        private static KeyData _hotKeyDisableToggle;

        // Roh konservierte <key>-Eintraege alter Profile, deren Hotkey (noch)
        // nicht registriert ist — werden beim Speichern zurueckgeschrieben.
        private sealed class PreservedKey
        {
            public string Mod;
            public string Key;
            public string Send;
            public string Command;
            public string Inner;
        }

        private static readonly List<PreservedKey> m_Preserved = new List<PreservedKey>();

        public static List<KeyData> List => m_List;

        /// <summary>Razor CE: m_Enabled (per Hotkey/UI umschaltbar, nicht persistiert).</summary>
        public static bool Enabled
        {
            get => m_Enabled;
            set => m_Enabled = value;
        }

        /// <summary>Letzter Status-Text (Razor CE: hkStatus-Label).</summary>
        public static string StatusText { get; private set; } = string.Empty;

        /// <summary>
        /// Registriert die Basis-Hotkeys (Enable/Disable-Toggles) und die
        /// "hotkeys"-Profilsektion (idempotent). NACH DressList.Initialize()
        /// aufrufen — die Sektions-Reihenfolge entspricht der Schreibreihenfolge
        /// im Profil (CE: counters, agents, dresslists, hotkeys).
        /// </summary>
        public static void Initialize()
        {
            m_HK_En = Add(HKCategory.None, LocString.ToggleHKEnable, new HotKeyCallback(OnToggleEnableDisable));
            _hotKeyEnableToggle = Add(HKCategory.None, LocString.EnableHotkeys, OnToggleEnable);
            _hotKeyDisableToggle = Add(HKCategory.None, LocString.DisableHotkeys, OnToggleDisable);

            ProfileSections.Register("hotkeys", Load, Save, ClearAll);

            UpdateStatus();
        }

        private static void OnToggleEnableDisable()
        {
            m_Enabled = !m_Enabled;

            MsgLevel type = m_Enabled ? MsgLevel.Info : MsgLevel.Warning;

            if (World.Player != null)
                World.Player.SendMessage(type, UpdateStatus());
        }

        private static void OnToggleEnable()
        {
            if (m_Enabled)
            {
                if (World.Player != null)
                    World.Player.SendMessage(MsgLevel.Warning, "HotKeys are already enabled");
            }
            else
            {
                m_Enabled = true;
                if (World.Player != null)
                    World.Player.SendMessage(MsgLevel.Info, UpdateStatus());
            }
        }

        private static void OnToggleDisable()
        {
            if (!m_Enabled)
            {
                if (World.Player != null)
                    World.Player.SendMessage(MsgLevel.Warning, "HotKeys are already disabled");
            }
            else
            {
                m_Enabled = false;
                if (World.Player != null)
                    World.Player.SendMessage(MsgLevel.Warning, UpdateStatus());
            }
        }

        public static string UpdateStatus()
        {
            KeyData kd = Get((int) LocString.ToggleHKEnable);
            string ks = kd?.KeyString();

            string msg;
            if (ks != null)
            {
                msg = m_Enabled
                    ? Language.Format(LocString.HKEnabledPress, ks)
                    : Language.Format(LocString.HKDisabledPress, ks);
            }
            else
            {
                msg = m_Enabled
                    ? Language.GetString(LocString.HKEnabled)
                    : Language.GetString(LocString.HKDisabled);
            }

            StatusText = msg;
            return msg;
        }

        public static bool ValidateCommand(string command)
        {
            if (string.IsNullOrEmpty(command))
                return false;

            foreach (KeyData keyData in m_List)
            {
                if (!string.IsNullOrEmpty(keyData.Command) &&
                    keyData.Command.Equals(command, StringComparison.OrdinalIgnoreCase))
                    return false;
            }

            return true;
        }

        public static bool ExecuteCommand(string command)
        {
            foreach (KeyData keyData in m_List)
            {
                if (!string.IsNullOrEmpty(keyData.Command) &&
                    keyData.Command.Equals(command, StringComparison.OrdinalIgnoreCase))
                {
                    keyData.Callback();
                    return true;
                }
            }

            return false;
        }

        public static void ClearAll()
        {
            for (int i = 0; i < m_List.Count; i++)
                m_List[i].ResetToDefault();

            m_Preserved.Clear();
        }

        public static KeyData GetFromObj(object other)
        {
            for (int i = 0; i < m_List.Count; i++)
            {
                if (m_List[i].pState == other)
                    return m_List[i];
            }

            return null;
        }

        public static KeyData Get(int name)
        {
            for (int i = 0; i < m_List.Count; i++)
            {
                if (m_List[i].LocName == name)
                    return m_List[i];
            }

            return null;
        }

        public static KeyData Get(string name)
        {
            for (int i = 0; i < m_List.Count; i++)
            {
                if (m_List[i].StrName == name)
                    return m_List[i];
            }

            return null;
        }

        /// <summary>
        /// Razor CE: HotKey.GetByNameOrId — Lookup per Anzeigename (case-
        /// insensitive) oder numerischem Listen-Index. Vom Script-Kommando
        /// `hotkey` genutzt.
        /// </summary>
        public static KeyData GetByNameOrId(string query)
        {
            foreach (KeyData key in m_List)
            {
                if (key.DisplayName.ToLower().Equals(query.ToLower()))
                    return key;
            }

            int hkIndex = Utility.ToInt32(query, -1);

            return hkIndex >= 0 && hkIndex < m_List.Count ? m_List[hkIndex] : null;
        }

        public static KeyData Get(int key, ModKeys mod)
        {
            for (int i = 0; i < m_List.Count; i++)
            {
                KeyData hk = m_List[i];
                if (hk.Key == key && hk.Mod == mod && hk.Key != 0)
                    return hk;
            }

            return null;
        }

        /// <summary>
        /// Zentraler Dispatch (Razor CE: OnKeyDown). Eingabe: WinForms-Keys-Wert
        /// + ModKeys (vom SDL-KMOD gemappt, siehe KeyMap).
        /// Rueckgabe: true = Taste an UO durchreichen, false = verschlucken.
        /// </summary>
        public static bool OnKeyDown(int key, ModKeys mod)
        {
            if (key == 0)
                return true;

            // NEU: globale Hotkeys (z. B. "Show Razor") auch ohne Spieler.
            for (int i = 0; i < m_List.Count; i++)
            {
                KeyData hk = m_List[i];
                if (hk.Global && hk.Key == key && hk.Mod == mod && hk.Key != 0)
                {
                    hk.Callback();
                    return hk.SendToUO;
                }
            }

            if (World.Player == null)
                return true;

            // Die Enable/Disable-Toggles feuern auch bei deaktivierten Hotkeys.
            if (m_HK_En != null && m_HK_En.Key > 0 && m_HK_En.Mod == mod && m_HK_En.Key == key)
            {
                m_HK_En.Callback();
                return m_HK_En.SendToUO;
            }

            if (_hotKeyEnableToggle != null && _hotKeyEnableToggle.Key > 0 && _hotKeyEnableToggle.Mod == mod &&
                _hotKeyEnableToggle.Key == key)
            {
                _hotKeyEnableToggle.Callback();
                return _hotKeyEnableToggle.SendToUO;
            }

            if (_hotKeyDisableToggle != null && _hotKeyDisableToggle.Key > 0 && _hotKeyDisableToggle.Mod == mod &&
                _hotKeyDisableToggle.Key == key)
            {
                _hotKeyDisableToggle.Callback();
                return _hotKeyDisableToggle.SendToUO;
            }

            if (m_Enabled)
            {
                for (int i = 0; i < m_List.Count; i++)
                {
                    KeyData hk = m_List[i];
                    if (hk.Mod == mod && hk.Key > 0 && hk.Key == key)
                    {
                        if (Macros.MacroManager.AcceptActions)
                            Macros.MacroManager.Action(new Macros.HotKeyAction(hk.LocName, hk.StrName));

                        hk.Callback();
                        return hk.SendToUO;
                    }
                }
            }

            return true;
        }

        /// <summary>
        /// Maus-Dispatch (Razor CE: OnMouse). button: 0 = Wheel (wheel != 0),
        /// 1 = Mitte, 2 = XButton1, 3 = XButton2.
        /// </summary>
        public static void OnMouse(int button, int wheel, ModKeys mod)
        {
            if (World.Player == null || !m_Enabled)
                return;

            int key = 0;
            switch (button)
            {
                case 0:
                    key = wheel > 0 ? -1 : -2;
                    break;
                case 1:
                    key = -3;
                    break;
                case 2:
                    key = -4;
                    break;
                case 3:
                    key = -5;
                    break;
            }

            KeyData hk = Get(key, mod);
            hk?.Callback();
        }

        private static KeyData Register(KeyData kd)
        {
            // Idempotent: bestehende Registrierung gleicher Identitaet ersetzen
            // (Belegung uebernehmen, damit Re-Initialisierung nichts verliert).
            KeyData old = kd.LocName != 0 ? Get(kd.LocName) : Get(kd.StrName);
            if (old != null)
            {
                kd.Key = old.Key;
                kd.Mod = old.Mod;
                kd.SendToUO = old.SendToUO;
                kd.Command = old.Command;
                m_List.Remove(old);
            }

            m_List.Add(kd);
            return kd;
        }

        public static KeyData Add(HKCategory cat, int name, HotKeyCallback callback)
        {
            return Add(cat, HKSubCat.None, name, callback);
        }

        public static KeyData Add(HKCategory cat, LocString name, HotKeyCallback callback)
        {
            return Add(cat, HKSubCat.None, (int) name, callback);
        }

        public static KeyData Add(HKCategory cat, HKSubCat sub, int name, HotKeyCallback callback)
        {
            return Register(new KeyData(name, cat, sub, callback));
        }

        public static KeyData Add(HKCategory cat, HKSubCat sub, LocString name, HotKeyCallback callback)
        {
            return Register(new KeyData((int) name, cat, sub, callback));
        }

        public static KeyData Add(HKCategory cat, HKSubCat sub, string name, HotKeyCallback callback)
        {
            return Register(new KeyData(name, cat, sub, callback));
        }

        public static KeyData Add(HKCategory cat, int name, HotKeyCallbackState callback, object state)
        {
            return Add(cat, HKSubCat.None, name, callback, state);
        }

        public static KeyData Add(HKCategory cat, LocString name, HotKeyCallbackState callback, object state)
        {
            return Add(cat, HKSubCat.None, (int) name, callback, state);
        }

        public static KeyData Add(HKCategory cat, HKSubCat sub, int name, HotKeyCallbackState callback, object state)
        {
            return Register(new KeyData(name, cat, sub, callback, state));
        }

        public static KeyData Add(HKCategory cat, HKSubCat sub, LocString name, HotKeyCallbackState callback,
            object state)
        {
            return Register(new KeyData((int) name, cat, sub, callback, state));
        }

        public static KeyData Add(HKCategory cat, HKSubCat sub, string name, HotKeyCallbackState callback,
            object state)
        {
            return Register(new KeyData(name, cat, sub, callback, state));
        }

        public static bool Remove(int name)
        {
            if (name == 0)
                return false;

            for (int i = 0; i < m_List.Count; i++)
            {
                if (m_List[i].LocName == name)
                {
                    m_List.RemoveAt(i);
                    return true;
                }
            }

            return false;
        }

        public static bool Remove(string name)
        {
            if (name == null)
                return false;

            for (int i = 0; i < m_List.Count; i++)
            {
                if (m_List[i].StrName == name)
                {
                    m_List.RemoveAt(i);
                    return true;
                }
            }

            return false;
        }

        /// <summary>Laedt die hotkeys-Profilsektion (Format wie Razor CE).</summary>
        public static void Load(XmlElement xml)
        {
            ClearAll();

            if (xml == null)
                return;

            foreach (XmlElement el in xml.GetElementsByTagName("key"))
            {
                try
                {
                    string name = el.GetAttribute("name");
                    string loc = el.GetAttribute("loc");
                    string mod = el.GetAttribute("mod");
                    string key = el.GetAttribute("key");
                    string send = el.GetAttribute("send");
                    string command = el.GetAttribute("command");

                    if (!ValidateCommand(command))
                        command = string.Empty;

                    KeyData k = null;

                    if (loc != null && loc.Length > 0)
                    {
                        k = Get(Convert.ToInt32(loc));
                    }
                    else if (name != null && name.Length > 0)
                    {
                        k = Get(name);
                    }
                    else if (el.InnerText != null && el.InnerText.Length > 1)
                    {
                        if (el.InnerText[0] == 'L' && el.InnerText[1] == ':')
                        {
                            try
                            {
                                k = Get(Convert.ToInt32(el.InnerText.Substring(2)));
                            }
                            catch
                            {
                                // ignored
                            }
                        }
                        else
                        {
                            k = Get(el.InnerText);
                        }
                    }

                    if (k != null)
                    {
                        k.Mod = (ModKeys) Convert.ToInt32(mod);
                        k.Key = Convert.ToInt32(key);
                        k.SendToUO = Convert.ToBoolean(send);
                        k.Command = command;
                    }
                    else
                    {
                        // NEU: Belegung eines (noch) nicht registrierten Hotkeys
                        // konservieren — geht beim Speichern nicht verloren.
                        m_Preserved.Add(new PreservedKey
                        {
                            Mod = mod,
                            Key = key,
                            Send = send,
                            Command = command,
                            Inner = el.InnerText
                        });
                    }
                }
                catch
                {
                    // ignored
                }
            }

            UpdateStatus();
        }

        /// <summary>Schreibt die hotkeys-Profilsektion (Format wie Razor CE).</summary>
        public static void Save(XmlWriter xml)
        {
            for (int i = 0; i < m_List.Count; i++)
            {
                KeyData k = m_List[i];

                if (k.Key != 0)
                {
                    xml.WriteStartElement("key");
                    xml.WriteAttributeString("mod", ((int) k.Mod).ToString());
                    xml.WriteAttributeString("key", k.Key.ToString());
                    xml.WriteAttributeString("send", k.SendToUO.ToString());
                    xml.WriteAttributeString("command", string.IsNullOrEmpty(k.Command) ? string.Empty : k.Command);

                    if (k.LocName != 0)
                        xml.WriteString("L:" + k.LocName.ToString());
                    else
                        xml.WriteString(k.StrName);

                    xml.WriteEndElement();
                }
            }

            foreach (PreservedKey p in m_Preserved)
            {
                xml.WriteStartElement("key");
                xml.WriteAttributeString("mod", p.Mod);
                xml.WriteAttributeString("key", p.Key);
                xml.WriteAttributeString("send", p.Send);
                xml.WriteAttributeString("command", p.Command ?? string.Empty);
                xml.WriteString(p.Inner ?? string.Empty);
                xml.WriteEndElement();
            }
        }
    }
}
