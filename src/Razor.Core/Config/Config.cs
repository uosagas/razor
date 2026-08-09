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

// Portiert aus Razor CE (Razor/UI/Config.cs, Klasse Config) — OHNE WinForms.
// Abweichungen vom Original (dokumentiert):
//  * Engine.RootPath ersetzt durch konfigurierbares Config.RootPath
//    (Default: Verzeichnis der Assembly; via Initialize(path) setzbar) -> cross-platform.
//  * SetupProfilesList(ComboBox), LoadProfileFor/SetProfileFor(PlayerData) entfernt (UI-/Game-State).
//  * Windows-spezifische App-Setting-Defaults (UODataDir/UOClient) entfernt.
//  * File.Create-Streams werden geschlossen (Leak im Original).

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;

namespace Assistant
{
    public class Config
    {
        private static Profile m_Current;
        private static Dictionary<Serial, string> m_Chars;

        private static string m_RootPath;
        private static Dictionary<string, string> m_AppSettings;

        /// <summary>
        /// Basisverzeichnis fuer settings.csv, Profiles/, Macros/, Scripts/.
        /// Default: Verzeichnis der ausgefuehrten Assembly (AppContext.BaseDirectory).
        /// </summary>
        public static string RootPath
        {
            get
            {
                if (string.IsNullOrEmpty(m_RootPath))
                    m_RootPath = AppContext.BaseDirectory;
                return m_RootPath;
            }
            set
            {
                m_RootPath = value;
                m_AppSettings = null; // settings.csv neu einlesen
                m_Current = null;
                m_Chars = null;
            }
        }

        /// <summary>Setzt das Basisverzeichnis und laedt die App-Settings (settings.csv).</summary>
        public static void Initialize(string rootPath)
        {
            RootPath = rootPath;
            EnsureAppSettings();
        }

        private static Dictionary<string, string> EnsureAppSettings()
        {
            if (m_AppSettings != null)
                return m_AppSettings;

            m_AppSettings = new Dictionary<string, string>();

            // Set defaults (wie Razor CE, ohne Windows-Pfade)
            m_AppSettings["LastPort"] = "2593";
            m_AppSettings["LastProfile"] = "default";
            m_AppSettings["LastServer"] = "127.0.0.1";
            m_AppSettings["LastServerId"] = "0";
            m_AppSettings["ClientEncrypted"] = "0";
            m_AppSettings["ServerEncrypted"] = "0";
            m_AppSettings["ShowWelcome"] = "1";
            m_AppSettings["UId"] = "";
            m_AppSettings["MaxOrganizerAgents"] = "20";
            m_AppSettings["MaxBuyAgents"] = "20";
            m_AppSettings["MaxRestockAgents"] = "20";
            m_AppSettings["ImportProfilesAndMacros"] = "true";
            m_AppSettings["BackupPath"] = Path.Combine(".", "Backup");

            var path = Path.Combine(RootPath, "settings.csv");

            try
            {
                if (File.Exists(path))
                {
                    foreach (var line in File.ReadLines(path))
                    {
                        var strs = line.Split(',');
                        if (strs.Length < 2)
                            continue;

                        m_AppSettings[strs[0].Trim()] = strs[1].Trim();
                    }
                }
            }
            catch (Exception)
            {
            }

            return m_AppSettings;
        }

        public static Profile CurrentProfile
        {
            get
            {
                if (m_Current == null)
                    LoadLastProfile();
                return m_Current;
            }
        }

        public static void Save()
        {
            if (m_Current != null)
                m_Current.Save();
            SaveCharList();
            SaveAppSettings();
        }

        public static bool LoadProfile(string name)
        {
            Profile p = new Profile(name);
            if (p.Load())
            {
                LastProfileName = p.Name;
                if (m_Current != null)
                    m_Current.Unload();
                m_Current = p;
                return true;
            }
            else
            {
                return false;
            }
        }

        public static void NewProfile(string name)
        {
            if (m_Current != null)
                m_Current.Unload();
            m_Current = new Profile(name);
        }

        public static void LoadCharList()
        {
            if (m_Chars == null)
                m_Chars = new Dictionary<Serial, string>();
            else
                m_Chars.Clear();

            string file = Path.Combine(Config.GetUserDirectory("Profiles"), "chars.lst");
            if (!File.Exists(file))
                return;

            using (StreamReader reader = new StreamReader(file))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    if (line.Length <= 0 || line[0] == ';' || line[0] == '#')
                        continue;
                    string[] split = line.Split('=');
                    try
                    {
                        m_Chars.Add(Serial.Parse(split[0]), split[1]);
                    }
                    catch
                    {
                    }
                }
            }
        }

        public static void SaveCharList()
        {
            if (m_Chars == null)
                m_Chars = new Dictionary<Serial, string>();

            try
            {
                using (StreamWriter writer =
                    new StreamWriter(Path.Combine(Config.GetUserDirectory("Profiles"), "chars.lst")))
                {
                    foreach (KeyValuePair<Serial, string> de in m_Chars)
                    {
                        writer.WriteLine("{0}={1}", de.Key, de.Value);
                    }
                }
            }
            catch
            {
            }
        }

        /// <summary>Profil-Zuordnung fuer einen Charakter (ersetzt LoadProfileFor(PlayerData)).</summary>
        public static bool TryGetProfileForChar(Serial serial, out string profileName)
        {
            if (m_Chars == null)
                m_Chars = new Dictionary<Serial, string>();

            return m_Chars.TryGetValue(serial, out profileName) && profileName != null;
        }

        public static void SetProfileForChar(Serial serial)
        {
            if (m_Chars == null)
                m_Chars = new Dictionary<Serial, string>();

            if (m_Current != null)
                m_Chars[serial] = m_Current.Name;
        }

        public static bool LoadLastProfile()
        {
            string name = LastProfileName;
            Profile p;

            if (!string.IsNullOrWhiteSpace(name))
            {
                p = new Profile(name);
                if (p.Load())
                {
                    if (m_Current != null)
                        m_Current.Unload();
                    m_Current = p;
                    return true;
                }
            }

            LastProfileName = "default";
            p = new Profile("default");
            if (!p.Load())
            {
                p.MakeDefault();
                p.Save();
            }

            if (m_Current != null)
                m_Current.Unload();
            m_Current = p;

            return true;
        }

        public static List<string> GetProfileList()
        {
            List<string> list = new List<string>();

            string[] files = Directory.GetFiles(GetUserDirectory("Profiles"), "*.xml");

            foreach (var file in files)
            {
                list.Add(Path.GetFileNameWithoutExtension(file));
            }

            return list;
        }

        private static void SaveAppSettings()
        {
            var settings = EnsureAppSettings();
            var path = Path.Combine(RootPath, "settings.csv");

            EnsureDirectory(RootPath);

            var data = "";
            foreach (var setting in settings)
            {
                data += $"{setting.Key}, {setting.Value}\n";
            }

            File.WriteAllText(path, data);
        }

        public static T GetAppSetting<T>(string key)
        {
            try
            {
                var appSetting = EnsureAppSettings()[key];

                if (!string.IsNullOrWhiteSpace(appSetting))
                {
                    var converter = TypeDescriptor.GetConverter(typeof(T));
                    return (T) (converter.ConvertFromInvariantString(appSetting));
                }

                return default(T);
            }
            catch
            {
                return default(T);
            }
        }

        public static object GetProperty(string name)
        {
            return CurrentProfile.GetProperty(name);
        }

        public static void SetProperty(string name, object val)
        {
            CurrentProfile.SetProperty(name, val);
        }

        public static void AddProperty(string name, object val)
        {
            CurrentProfile.AddProperty(name, val);
        }

        public static bool GetBool(string name)
        {
            return (bool) CurrentProfile.GetProperty(name);
        }

        public static string GetString(string name)
        {
            return (string) CurrentProfile.GetProperty(name);
        }

        public static int GetInt(string name)
        {
            return (int) CurrentProfile.GetProperty(name);
        }

        public static double GetDouble(string name)
        {
            return (double) CurrentProfile.GetProperty(name);
        }

        public static bool SetAppSetting(string key, string value)
        {
            try
            {
                EnsureAppSettings()[key] = value;
                SaveAppSettings();

                return true;
            }
            catch
            {
                return false;
            }
        }

        public static string GetUserDirectory(string name)
        {
            string appDir = GetInstallDirectory();

            EnsureDirectory(Path.Combine(appDir, "Macros"));
            EnsureDirectory(Path.Combine(appDir, "Profiles"));
            EnsureDirectory(Path.Combine(appDir, "Scripts"));

            name = name.Length > 0 ? Path.Combine(appDir, name) : appDir;

            EnsureDirectory(name);

            return name;
        }

        public static string GetUserDirectory()
        {
            return GetUserDirectory("");
        }

        public static string GetInstallDirectory(string name)
        {
            string dir = RootPath;

            if (name.Length > 0)
            {
                dir = Path.Combine(dir, name);
            }

            EnsureDirectory(dir);

            return dir;
        }

        public static string GetInstallDirectory()
        {
            return GetInstallDirectory("");
        }

        public static string LastProfileName
        {
            get => GetAppSetting<string>("LastProfile");
            set => SetAppSetting("LastProfile", value);
        }

        internal static void EnsureDirectory(string dir)
        {
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);
        }
    }
}
