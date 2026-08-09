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

// UOSagas-Razor: General-Tab im Razor-CE-Layout (Phase 3b).
//
// Blaupause: Razor CE Razor.Designer.cs, generalTab -> subGeneralTab (TabControl)
// -> subGenTab ("General", 502x286):
//  * groupBox4 "Profiles" @(6,6) 490x95: profiles-Combo + New/Save/Clone/Delete
//  * label11 "Show in:" + taskbar/systray-Radios (Property "Systray")
//  * showWelcome/alwaysTop-Checkboxen, Opacity-Slider, Language-Combo
//  * groupBox15 "Map / Boat" mit UOPS/Boat Control (Platzhalter)
//
// Funktional: Profilverwaltung (New/Save/Clone/Delete + Wechsel), AlwaysOnTop
// (Property + Fenster-Topmost), Opacity (Property + Fenster-Opacity), Systray-
// Radios (nur Property). Welcome/Language/Map/Boat sind Platzhalter.

using System;
using System.IO;
using Assistant;
using Avalonia.Controls;
using Avalonia.Layout;

namespace Razor.UI
{
    public class GeneralTab : UserControl, ICeTab
    {
        private readonly MainWindow _owner;
        private readonly PropBinder _binder = new PropBinder();

        private readonly ComboBox _profiles;
        private readonly TextBlock _opacityLabel;
        private readonly RadioButton _taskbar;
        private readonly RadioButton _systray;

        private bool _applying;

        public GeneralTab(MainWindow owner)
        {
            _owner = owner;

            // Wie Razor CE: eigener Sub-TabControl mit einer "General"-Seite.
            var root = Ce.Panel();
            TabControl sub = Ce.SubTabs(root, 6, 3, 510, 314);
            var page = Ce.Panel(502, 286);
            Ce.Tab(sub, "General", page);

            // --- groupBox4 "Profiles" @(6,6) 490x95 ---------------------------
            Canvas grpProfiles = Ce.Group(page, "Profiles", 6, 6, 490, 95);
            _profiles = Ce.Combo(grpProfiles, 6, 22, 476, 24);
            _profiles.SelectionChanged += (s, e) => OnProfileSelected();
            Ce.Button(grpProfiles, "New", 264, 58, 50, 28, OnNewProfile);
            Ce.Button(grpProfiles, "Save", 320, 58, 50, 28, OnSaveProfile);
            Ce.Button(grpProfiles, "Clone", 376, 58, 50, 28, OnCloneProfile);
            Ce.Button(grpProfiles, "Delete", 432, 58, 50, 28, OnDeleteProfile);

            // --- "Show in:" Taskbar/System Tray -------------------------------
            Ce.Label(page, "Show in:", 11, 122, 54, 16);
            _taskbar = Ce.Radio(page, "Taskbar", "showin", 92, 118, 79, 23);
            _systray = Ce.Radio(page, "System Tray", "showin", 164, 118, 88, 23);
            _taskbar.IsCheckedChanged += (s, e) => OnShowInChanged();
            _systray.IsCheckedChanged += (s, e) => OnShowInChanged();

            // --- Opacity -------------------------------------------------------
            _opacityLabel = Ce.Label(page, "Opacity: 100%", 11, 152, 80, 19);
            var opacity = new Slider
            {
                Minimum = 10,
                Maximum = 100,
                Value = 100,
                VerticalAlignment = VerticalAlignment.Center
            };
            Ce.At(page, opacity, 97, 144, 155, 30);
            _binder.Slider(opacity, "Opacity", invert: false, uiSideEffect: val =>
            {
                _opacityLabel.Text = $"Opacity: {val}%";
                _owner.Opacity = Math.Max(0.1, val / 100.0);
            });

            // --- Rechte Spalte -------------------------------------------------
            CheckBox welcome = Ce.Check(page, "Show Welcome Screen", 270, 107, 152, 23);
            welcome.IsEnabled = false; // App-Setting, kein Profil-Property (spaetere Phase)

            CheckBox alwaysTop = Ce.Check(page, "Use Smart Always on Top", 270, 136, 162, 23);
            _binder.Check(alwaysTop, "AlwaysOnTop", val => _owner.Topmost = val);

            Ce.Label(page, "Language:", 267, 172, 68, 18);
            ComboBox lang = Ce.Combo(page, 341, 169, 136, 23, "English (ENU)");
            lang.SelectedIndex = 0;
            lang.IsEnabled = false;

            // --- groupBox15 "Map / Boat" @(14,232) 223x48 ----------------------
            Canvas grpMap = Ce.Group(page, "Map / Boat", 14, 232, 223, 48);
            Ce.Button(grpMap, "UOPS", 6, 16, 107, 26, null);
            Ce.Button(grpMap, "Boat Control", 119, 16, 96, 26, null);

            // --- "Tools" (UOSagas-Zusatz, kein CE-Pendant) ----------------------
            // VScripts hat einen eigenen Haupttab (neben Scripts).
            Canvas grpTools = Ce.Group(page, "Tools", 245, 232, 240, 48);
            Ce.Button(grpTools, "Gump Inspector", 6, 16, 130, 26, GumpInspectorWindow.Open);

            Content = root;
        }

        // --- Profilverwaltung (alle Schreibzugriffe via GameThread.Post) -------

        private void OnProfileSelected()
        {
            if (_applying)
                return;

            string name = _profiles.SelectedItem as string;
            if (string.IsNullOrEmpty(name))
                return;

            GameThread.Post(() =>
            {
                if (Config.CurrentProfile != null &&
                    string.Equals(Config.CurrentProfile.Name, name, StringComparison.OrdinalIgnoreCase))
                    return;

                // Wie Razor CE beim Profilwechsel: aktuelles Profil sichern,
                // dann neues laden (Sektionen laden ueber ProfileSections).
                try
                {
                    Config.Save();
                }
                catch
                {
                }

                if (Config.LoadProfile(name))
                    Console.WriteLine($"[UOSagas Razor] Profil gewechselt: {name}");
                else
                    Console.WriteLine($"[UOSagas Razor] Profil konnte nicht geladen werden: {name}");
            });
        }

        private async void OnNewProfile()
        {
            string name = await InputBox.Show(_owner, "Enter a name for the new profile:", "New Profile");
            name = Sanitize(name);
            if (name == null)
                return;

            GameThread.Post(() =>
            {
                try
                {
                    Config.Save();
                    Config.NewProfile(name);
                    Config.Save(); // legt die Datei an, damit sie in der Liste erscheint
                    Console.WriteLine($"[UOSagas Razor] Neues Profil: {name}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[UOSagas Razor] Profil anlegen fehlgeschlagen: {ex.Message}");
                }
            });

            _owner.RequestSnapshot();
        }

        private void OnSaveProfile()
        {
            GameThread.Post(() =>
            {
                try
                {
                    Config.Save();
                    Console.WriteLine("[UOSagas Razor] Profil gespeichert.");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[UOSagas Razor] Profil speichern fehlgeschlagen: {ex.Message}");
                }
            });
        }

        private async void OnCloneProfile()
        {
            string name = await InputBox.Show(_owner, "Enter a name for the cloned profile:", "Clone Profile");
            name = Sanitize(name);
            if (name == null)
                return;

            GameThread.Post(() =>
            {
                try
                {
                    if (Config.CurrentProfile == null)
                        return;

                    string path = Path.Combine(Config.GetUserDirectory("Profiles"), $"{name}.xml");
                    Config.CurrentProfile.SaveToFile(path);
                    Config.LoadProfile(name);
                    Console.WriteLine($"[UOSagas Razor] Profil geklont: {name}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[UOSagas Razor] Profil klonen fehlgeschlagen: {ex.Message}");
                }
            });

            _owner.RequestSnapshot();
        }

        private void OnDeleteProfile()
        {
            string name = _profiles.SelectedItem as string;
            if (string.IsNullOrEmpty(name))
                return;

            // Wie Razor CE: "default" ist nicht loeschbar.
            if (string.Equals(name, "default", StringComparison.OrdinalIgnoreCase))
                return;

            GameThread.Post(() =>
            {
                try
                {
                    string path = Path.Combine(Config.GetUserDirectory("Profiles"), $"{name}.xml");

                    bool wasCurrent = Config.CurrentProfile != null &&
                                      string.Equals(Config.CurrentProfile.Name, name,
                                          StringComparison.OrdinalIgnoreCase);

                    if (wasCurrent && !Config.LoadProfile("default"))
                    {
                        Config.NewProfile("default");
                        Config.Save();
                    }

                    if (File.Exists(path))
                        File.Delete(path);

                    Console.WriteLine($"[UOSagas Razor] Profil geloescht: {name}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[UOSagas Razor] Profil loeschen fehlgeschlagen: {ex.Message}");
                }
            });

            _owner.RequestSnapshot();
        }

        private static string Sanitize(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return null;

            name = name.Trim();
            foreach (char c in Path.GetInvalidFileNameChars())
                name = name.Replace(c, '_');
            return name.Length == 0 ? null : name;
        }

        private void OnShowInChanged()
        {
            if (_applying)
                return;

            bool systray = _systray.IsChecked == true;
            GameThread.Post(() => PropBinder.SetScalar("Systray", systray));
        }

        // --- Snapshot -----------------------------------------------------------

        public void Contribute(UiRequest req)
        {
            _binder.Contribute(req);
            req.BoolProps.Add("Systray");
        }

        public void Apply(UiSnapshot snap)
        {
            _applying = true;
            try
            {
                // Profil-Liste nur neu aufbauen, wenn sie sich geaendert hat
                // (sonst klappt die Dropdown-Auswahl unter dem User weg).
                bool changed = _profiles.Items.Count != snap.Profiles.Count;
                if (!changed)
                {
                    for (int i = 0; i < snap.Profiles.Count; i++)
                    {
                        if (!string.Equals(_profiles.Items[i] as string, snap.Profiles[i], StringComparison.Ordinal))
                        {
                            changed = true;
                            break;
                        }
                    }
                }

                if (changed)
                {
                    _profiles.Items.Clear();
                    foreach (string p in snap.Profiles)
                        _profiles.Items.Add(p);
                }

                if (!_profiles.IsDropDownOpen && snap.ProfileName != null)
                {
                    for (int i = 0; i < _profiles.Items.Count; i++)
                    {
                        if (string.Equals(_profiles.Items[i] as string, snap.ProfileName,
                                StringComparison.OrdinalIgnoreCase))
                        {
                            if (_profiles.SelectedIndex != i)
                                _profiles.SelectedIndex = i;
                            break;
                        }
                    }
                }

                if (snap.BoolProps.TryGetValue("Systray", out bool tray))
                {
                    if (_systray.IsChecked != tray)
                        _systray.IsChecked = tray;
                    if (_taskbar.IsChecked != !tray)
                        _taskbar.IsChecked = !tray;
                }

                _binder.Apply(snap);

                if (snap.TextProps.TryGetValue("Opacity", out string op) && int.TryParse(op, out int o))
                    _opacityLabel.Text = $"Opacity: {o}%";
            }
            finally
            {
                _applying = false;
            }
        }
    }
}
