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

// Portiert aus Razor CE (Razor/UI/Config.cs, Klasse Profile) — OHNE WinForms.
// Abweichungen vom Original (dokumentiert):
//  * MessageBox-Fehlerdialoge entfernt (Fehler -> return false bzw. still).
//  * Die fest verdrahteten Manager-Aufrufe (Filter/Counter/Agent/DressList/...)
//    sind durch den ProfileSections-Registrierungsmechanismus ersetzt.
//  * Sektionen ohne registrierten Handler werden roh konserviert und beim
//    Speichern unveraendert zurueckgeschrieben (Fidelity fuer alte Profile).
//  * Typaufloesung der Properties zusaetzlich ueber die Razor.Core-Assembly.

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Xml;

namespace Assistant
{
    public class Profile
    {
        private string m_Name;
        private Dictionary<string, object> m_Props;
        private System.Threading.Mutex m_Mutex;

        // Roh konservierte Sektionen (Name -> OuterXml) in Dateireihenfolge,
        // fuer die kein ProfileSections-Handler registriert ist.
        private readonly List<KeyValuePair<string, string>> m_UnknownSections =
            new List<KeyValuePair<string, string>>();

        public Profile(string name)
        {
            Name = name;
            m_Props = new Dictionary<string, object>(16, StringComparer.OrdinalIgnoreCase);

            MakeDefault();
        }

        public IReadOnlyList<KeyValuePair<string, string>> UnknownSections => m_UnknownSections;

        public void MakeDefault()
        {
            m_Props.Clear();
            m_UnknownSections.Clear();

            AddProperty("ShowMobNames", false);
            AddProperty("ShowCorpseNames", false);
            AddProperty("DisplaySkillChanges", false);

            if (Client.IsOSI)
                AddProperty("TitleBarText", @"UO - {char} {crimtime}- {mediumstatbar} {bp} {bm} {gl} {gs} {mr} {ns} {ss} {sa} {aids}");
            else
                AddProperty("TitleBarText", @"UO - {char}");

            AddProperty("TitleBarDisplay", true);
            AddProperty("AutoSearch", true);
            AddProperty("NoSearchPouches", true);
            AddProperty("CounterWarnAmount", (int) 5);
            AddProperty("CounterWarn", true);
            AddProperty("ObjectDelay", (int) 600);
            AddProperty("ObjectDelayEnabled", true);
            AddProperty("AlwaysOnTop", false);
            AddProperty("SortCounters", true);
            AddProperty("QueueActions", false);
            AddProperty("QueueTargets", false);
            AddProperty("WindowX", (int) 400);
            AddProperty("WindowY", (int) 400);
            AddProperty("CountStealthSteps", true);

            AddProperty("SysColor", (int) 0x0044);
            AddProperty("WarningColor", (int) 0x0025);
            AddProperty("ExemptColor", (int) 0x0480);
            AddProperty("SpeechHue", (int) 0x03B1);
            AddProperty("BeneficialSpellHue", (int) 0x0005);
            AddProperty("HarmfulSpellHue", (int) 0x0058);
            AddProperty("NeutralSpellHue", (int) 0x03B1);
            AddProperty("ForceSpeechHue", false);
            AddProperty("ForceSpellHue", true);
            AddProperty("SpellFormat", @"{power} [{spell}]");

            AddProperty("ShowNotoHue", true);
            AddProperty("Opacity", (int) 100);

            AddProperty("AutoOpenCorpses", false);
            AddProperty("CorpseRange", (int) 2);

            AddProperty("BlockDismount", false);

            AddProperty("CapFullScreen", false);
            AddProperty("CapPath",
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "RazorScreenShots"));
            AddProperty("CapTimeStamp", true);
            AddProperty("ImageFormat", "jpg");

            AddProperty("UndressConflicts", true);
            AddProperty("HighlightReagents", true);
            AddProperty("Systray", false);
            AddProperty("TitlebarImages", true);

            AddProperty("SellAgentMax", (int) 99);
            AddProperty("SkillListCol", (int) -1);
            AddProperty("SkillListAsc", false);

            AddProperty("AutoStack", false);
            AddProperty("ActionStatusMsg", true);
            AddProperty("RememberPwds", false);

            AddProperty("SpellUnequip", true);
            AddProperty("RangeCheckLT", true);
            AddProperty("LTRange", (int) 12);

            AddProperty("FilterSnoopMsg", true);
            AddProperty("OldStatBar", false);

            AddProperty("SmartLastTarget", false);
            AddProperty("LastTargTextFlags", true);
            AddProperty("LTHilight", (int) 0);

            AddProperty("AutoFriend", false);

            AddProperty("AutoOpenDoors", true);

            AddProperty("MessageLevel", 0);

            AddProperty("ForceIP", "");
            AddProperty("ForcePort", 0);

            AddProperty("ForceSizeEnabled", false);
            AddProperty("ForceSizeX", 1000);
            AddProperty("ForceSizeY", 800);

            AddProperty("PotionEquip", false);
            AddProperty("BlockHealPoison", true);

            AddProperty("SmoothWalk", false);

            AddProperty("Negotiate", true);

            AddProperty("MapX", 200);
            AddProperty("MapY", 200);
            AddProperty("MapW", 200);
            AddProperty("MapH", 200);

            AddProperty("LogPacketsByDefault", false);

            AddProperty("ShowHealth", true);
            AddProperty("HealthFmt", "[{0}%]");
            AddProperty("ShowPartyStats", true);
            AddProperty("PartyStatFmt", "[{0}% / {1}%]");

            AddProperty("HotKeyStop", false);
            AddProperty("DiffTargetByType", false);
            AddProperty("StepThroughMacro", false);

            AddProperty("ShowTargetSelfLastClearOverhead", true);
            AddProperty("ShowOverheadMessages", false);
            AddProperty("CaptureMibs", false);

            AddProperty("OverheadFormat", "[{msg}]");
            AddProperty("OverheadStyle", 1);

            AddProperty("GoldPerDisplay", false);

            AddProperty("LightLevel", 31);
            AddProperty("LogSkillChanges", false);
            AddProperty("StealthOverhead", false);

            AddProperty("ShowBuffDebuffOverhead", true);
            AddProperty("BuffDebuffFormat", "[{action}{name} {duration}]");

            AddProperty("BlockOpenCorpsesTwice", false);

            AddProperty("ShowAttackTargetOverhead", true);

            AddProperty("RangeCheckTargetByType", false);
            AddProperty("RangeCheckDoubleClick", false);

            AddProperty("ShowContainerLabels", false);
            AddProperty("ContainerLabelFormat", "[{label}] ({type})");
            AddProperty("ContainerLabelColor", 88);
            AddProperty("ContainerLabelStyle", 1);

            AddProperty("Season", 5);

            AddProperty("BlockTradeRequests", false);
            AddProperty("BlockPartyInvites", false);
            AddProperty("AutoAcceptParty", false);

            AddProperty("MaxLightLevel", 31);
            AddProperty("MinLightLevel", 0);
            AddProperty("MinMaxLightLevelEnabled", false);
            AddProperty("ShowStaticWalls", false);
            AddProperty("ShowStaticWallLabels", false);

            AddProperty("ShowTextTargetIndicator", false);
            AddProperty("ShowAttackTargetNewOnly", true);

            AddProperty("FilterDragonGraphics", false);
            AddProperty("FilterDrakeGraphics", false);
            AddProperty("DragonGraphic", 0);
            AddProperty("DrakeGraphic", 0);

            AddProperty("ShowDamageDealt", false);
            AddProperty("ShowDamageDealtOverhead", false);
            AddProperty("ShowDamageTaken", false);
            AddProperty("ShowDamageTakenOverhead", false);

            AddProperty("ShowInRazorTitleBar", false);
            AddProperty("RazorTitleBarText", "{name} on {account} ({profile} - {shard}) - Razor v{version}");

            AddProperty("EnableUOAAPI", true);

            AddProperty("TargetIndicatorFormat", "* Target *");

            AddProperty("NextPrevTargetIgnoresFriends", false);

            AddProperty("StealthStepsFormat", "Steps: {step}");

            AddProperty("ShowFriendOverhead", false);

            AddProperty("DisplaySkillChangesOverhead", false);

            AddProperty("GrabHotBag", "0");

            // Enable it for OSI client by default, CUO turn it off
            AddProperty("MacroActionDelay", Client.IsOSI);

            AddProperty("AutoOpenDoorWhenHidden", false);

            AddProperty("DisableMacroPlayFinish", false);

            AddProperty("ShowBandageTimer", false);
            AddProperty("ShowBandageTimerFormat", "Bandage: {count}s");
            AddProperty("ShowBandageTimerLocation", 0);
            AddProperty("OnlyShowBandageTimerEvery", false);
            AddProperty("OnlyShowBandageTimerSeconds", 1);
            AddProperty("ShowBandageTimerHue", 88);

            AddProperty("TargetIndicatorHue", 10);

            AddProperty("FilterSystemMessages", false);
            AddProperty("FilterRazorMessages", false);
            AddProperty("FilterDelay", 3.5);
            AddProperty("FilterOverheadMessages", false);

            AddProperty("OnlyNextPrevBeneficial", false);
            AddProperty("FriendlyBeneficialOnly", false);
            AddProperty("NonFriendlyHarmfulOnly", false);

            AddProperty("ShowBandageStart", false);
            AddProperty("BandageStartMessage", "Bandage: Starting");
            AddProperty("ShowBandageEnd", false);
            AddProperty("BandageEndMessage", "Bandage: Ending");

            AddProperty("BuffDebuffSeconds", 20);
            AddProperty("BuffHue", 88);
            AddProperty("DebuffHue", 338);
            AddProperty("DisplayBuffDebuffEvery", false);
            AddProperty("BuffDebuffFilter", string.Empty);
            AddProperty("BuffDebuffEveryXSeconds", false);

            AddProperty("CaptureOthersDeathDelay", 0.5);
            AddProperty("CaptureOwnDeathDelay", 0.5);
            AddProperty("CaptureOthersDeath", false);
            AddProperty("CaptureOwnDeath", false);

            AddProperty("TargetFilterEnabled", false);

            AddProperty("FilterDaemonGraphics", false);
            AddProperty("DaemonGraphic", 0);

            AddProperty("SoundFilterEnabled", false);
            AddProperty("ShowFilteredSound", false);
            AddProperty("ShowPlayingSoundInfo", false);
            AddProperty("ShowMusicInfo", false);

            AddProperty("AutoSaveScript", false);
            AddProperty("AutoSaveScriptPlay", false);

            AddProperty("HighlightFriend", false);

            AddProperty("ScriptDisablePlayFinish", false);

            AddProperty("ShowWaypointOverhead", true);
            AddProperty("ShowWaypointDistance", true);
            AddProperty("ShowWaypointSeconds", 10);

            AddProperty("ClearWaypoint", false);
            AddProperty("HideWaypointDistance", 4);

            AddProperty("CreateWaypointOnDeath", false);

            AddProperty("ShowPartyFriendOverhead", false);

            AddProperty("OverrideSpellFormat", true);

            AddProperty("PotionReequip", true);

            AddProperty("EnableTextFilter", false);

            AddProperty("DisableScriptTooltips", false);

            AddProperty("BuyAgentsIgnoreGold", false);

            AddProperty("OverrideBuffDebuffFormat", false);

            AddProperty("NextPrevAlphabetical", false);

            AddProperty("FilterWyrmGraphics", false);
            AddProperty("WyrmGraphic", 0);

            AddProperty("WindowSizeX", 546);
            AddProperty("WindowSizeY", 411);

            AddProperty("PlayEmoteSound", false);

            AddProperty("ShowBuffDebuffGump", false);
            AddProperty("ShowBuffDebuffIcons", true);
            AddProperty("ShowBuffDebuffWidth", 100);
            AddProperty("ShowBuffDebuffHeight", 18);
            AddProperty("ShowBuffDebuffSort", 2);
            AddProperty("UseBlackBuffDebuffBg", false);
            AddProperty("ShowBuffDebuffTimeType", 0);

            AddProperty("DefaultScriptDelay", true);
            AddProperty("EnableHighlight", false);
            AddProperty("DisableScriptStopwatch", false);

            AddProperty("CooldownHeight", 28);
            AddProperty("CooldownWidth", 110);

            // Original: Counter.Default(); Filter.DisableAll(); DressList.ClearAll(); HotKey.ClearAll(); ...
            // Ersetzt durch den Registrierungsmechanismus:
            ProfileSections.MakeDefaultAll();
        }

        public string Name
        {
            get { return m_Name; }
            set
            {
                if (value != null && value.Trim() != "")
                {
                    StringBuilder sb = new StringBuilder(value);
                    sb.Replace('\\', '_');
                    sb.Replace('/', '_');
                    sb.Replace('\"', '\'');
                    sb.Replace(':', '_');
                    sb.Replace('?', '_');
                    sb.Replace('*', '_');
                    sb.Replace('<', '(');
                    sb.Replace('>', ')');
                    sb.Replace('|', '_');
                    m_Name = sb.ToString();
                }
                else
                {
                    m_Name = "[No Name]";
                }
            }
        }

        private static bool m_Warned = false;

        public bool Load()
        {
            if (m_Name == null || m_Name.Trim() == "")
                return false;

            string path = Config.GetUserDirectory("Profiles");
            string file = Path.Combine(path, $"{m_Name}.xml");

            return LoadFromFile(file);
        }

        /// <summary>
        /// Laedt ein Profil aus einer konkreten XML-Datei (Format wie Razor CE).
        /// </summary>
        public bool LoadFromFile(string file)
        {
            if (!File.Exists(file))
                return false;

            XmlDocument doc = new XmlDocument();
            try
            {
                doc.Load(file);
            }
            catch
            {
                // Original: MessageBox "Profile Load Error"
                return false;
            }

            XmlElement root = doc["profile"];
            if (root == null)
                return false;

            Assembly exe = Assembly.GetExecutingAssembly();

            m_UnknownSections.Clear();
            var sections = new Dictionary<string, XmlElement>(StringComparer.OrdinalIgnoreCase);

            foreach (XmlNode node in root.ChildNodes)
            {
                if (!(node is XmlElement el))
                    continue;

                if (el.Name == "property")
                {
                    try
                    {
                        string name = el.GetAttribute("name");
                        string typeStr = el.GetAttribute("type");
                        string val = el.InnerText;

                        if (typeStr == "-null-" || name == "LimitSize" || name == "VisRange")
                        {
                            if (m_Props.ContainsKey(name))
                                m_Props.Remove(name);
                        }
                        else
                        {
                            Type type = Type.GetType(typeStr);
                            if (type == null)
                                type = exe.GetType(typeStr);

                            if (m_Props.ContainsKey(name) || name == "ForceSize")
                            {
                                if (type == null)
                                    m_Props.Remove(name);
                                else
                                    m_Props[name] = GetObjectFromString(val, type);
                            }
                        }
                    }
                    catch
                    {
                        // Original: MessageBox "Profile Load Exception"
                    }
                }
                else if (ProfileSections.Find(el.Name) != null)
                {
                    sections[el.Name] = el;
                }
                else
                {
                    // Unbekannte Sektion: roh konservieren (agents/hotkeys/filters/...,
                    // solange kein Manager dafuer registriert ist).
                    m_UnknownSections.Add(new KeyValuePair<string, string>(el.Name, el.OuterXml));
                }
            }

            // Registrierte Handler in Registrierungsreihenfolge aufrufen
            // (wie die feste Aufruf-Reihenfolge im Original; Element kann null sein).
            foreach (ProfileSectionHandler handler in ProfileSections.Handlers)
            {
                sections.TryGetValue(handler.Name, out XmlElement el);
                try
                {
                    handler.Load?.Invoke(el);
                }
                catch
                {
                }
            }

            if (m_Props.ContainsKey("ForceSize"))
            {
                try
                {
                    int x, y;
                    switch ((int) m_Props["ForceSize"])
                    {
                        case 1:
                            x = 960;
                            y = 600;
                            break;
                        case 2:
                            x = 1024;
                            y = 768;
                            break;
                        case 3:
                            x = 1152;
                            y = 864;
                            break;
                        case 4:
                            x = 1280;
                            y = 720;
                            break;
                        case 5:
                            x = 1280;
                            y = 768;
                            break;
                        case 6:
                            x = 1280;
                            y = 800;
                            break;
                        case 7:
                            x = 1280;
                            y = 960;
                            break;
                        case 8:
                            x = 1280;
                            y = 1024;
                            break;

                        case 0:
                        default:
                            x = 800;
                            y = 600;
                            break;
                    }

                    SetProperty("ForceSizeX", x);
                    SetProperty("ForceSizeY", y);

                    if (x != 800 || y != 600)
                        SetProperty("ForceSizeEnabled", true);

                    m_Props.Remove("ForceSize");
                }
                catch
                {
                }
            }

            return true;
        }

        public void Unload()
        {
            try
            {
                if (m_Mutex != null)
                {
                    m_Mutex.ReleaseMutex();
                    m_Mutex.Close();
                    m_Mutex = null;
                }
            }
            catch
            {
            }
        }

        public void Save()
        {
            string profileDir = Config.GetUserDirectory("Profiles");
            string file = Path.Combine(profileDir, $"{m_Name}.xml");

            SaveToFile(file, acquireMutex: true);
        }

        /// <summary>
        /// Speichert das Profil in eine konkrete XML-Datei (Format wie Razor CE).
        /// </summary>
        public void SaveToFile(string file, bool acquireMutex = false)
        {
            if (acquireMutex && m_Name != "default" && !m_Warned)
            {
                try
                {
                    m_Mutex = new System.Threading.Mutex(true, $"Razor_Profile_{m_Name}");

                    if (!m_Mutex.WaitOne(10, false))
                        throw new Exception("Can't grab profile mutex, must be in use!");
                }
                catch
                {
                    return; // refuse to overwrite profiles that we don't own.
                }
            }

            XmlTextWriter xml;
            try
            {
                xml = new XmlTextWriter(file, Encoding.UTF8);
            }
            catch
            {
                return;
            }

            xml.Formatting = Formatting.Indented;
            xml.IndentChar = '\t';
            xml.Indentation = 1;

            xml.WriteStartDocument(true);
            xml.WriteStartElement("profile");

            foreach (KeyValuePair<string, object> de in m_Props)
            {
                xml.WriteStartElement("property");
                xml.WriteAttributeString("name", de.Key);
                if (de.Value == null)
                {
                    xml.WriteAttributeString("type", "-null-");
                }
                else
                {
                    xml.WriteAttributeString("type", de.Value.GetType().FullName);
                    xml.WriteString(de.Value.ToString());
                }

                xml.WriteEndElement();
            }

            // Registrierte Sektionen (im Original: filters, counters, agents, dresslists,
            // hotkeys, passwords, overheadmessages, containerlabels, macrovariables,
            // scriptvariables, friends, textfilters, targetfilters, soundfilters, waypoints).
            foreach (ProfileSectionHandler handler in ProfileSections.Handlers)
            {
                xml.WriteStartElement(handler.Name);
                try
                {
                    handler.Save?.Invoke(xml);
                }
                catch
                {
                }

                xml.WriteEndElement();
            }

            // Nicht registrierte Sektionen unveraendert zurueckschreiben.
            foreach (KeyValuePair<string, string> raw in m_UnknownSections)
            {
                if (ProfileSections.Find(raw.Key) != null)
                    continue; // inzwischen registriert -> Handler hat bereits geschrieben

                xml.WriteWhitespace("\n\t");
                xml.WriteRaw(raw.Value);
            }

            xml.WriteEndElement(); // end profile section

            xml.Close();
        }

        private static Type[] ctorTypes = new Type[] {typeof(string)};

        private object GetObjectFromString(string val, Type type)
        {
            if (type == typeof(string))
            {
                return val;
            }
            else
            {
                try
                {
                    ConstructorInfo ctor = type.GetConstructor(ctorTypes);
                    if (ctor != null)
                        return ctor.Invoke(new object[] {val});
                }
                catch
                {
                }

                return Convert.ChangeType(val, type);
            }
        }

        public bool HasProperty(string name)
        {
            return m_Props.ContainsKey(name);
        }

        public object GetProperty(string name)
        {
            if (!m_Props.ContainsKey(name))
                throw new Exception(Language.Format(LocString.NoProp, name));
            return m_Props[name];
        }

        public void SetProperty(string name, object val)
        {
            if (!m_Props.ContainsKey(name))
                throw new Exception(Language.Format(LocString.NoProp, name));
            m_Props[name] = val;
        }

        public void AddProperty(string name, object val)
        {
            m_Props[name] = val;
        }
    }
}
