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

// UOSagas-Razor: Display/Counters-Tab im Razor-CE-Layout (Phase 3b).
//
// Blaupause: Razor CE Razor.Designer.cs, displayTab -> displayCountersTabCtrl
// mit den Sub-Tabs Display, Counters, Bandage Timer, Overhead Messages,
// Waypoints, Buffs/Cooldowns. Alle Koordinaten/Texte aus der Designer-Datei.
//
// Funktional: alle property-gebundenen Checkboxen/Textfelder/Combos sowie die
// Counter-Liste (Enable/Disable je Counter, Recount). Overhead-/Waypoint-
// Listen und Hue-/Sound-Buttons sind Layout-Platzhalter (Feature folgt).

using System;
using System.Collections.Generic;
using Assistant;
using Avalonia.Controls;
using Avalonia.Media;

namespace Razor.UI
{
    public class DisplayTab : UserControl, ICeTab
    {
        private static readonly string[] TitleParams =
        {
            "{char}", "{shard}", "{profile}", "{account}", "{hp}", "{mana}", "{stam}",
            "{ar}", "{gold}", "{weight}", "{bandage}", "{crimtime}", "{stealthsteps}", "{version}"
        };

        private readonly PropBinder _binder = new PropBinder();
        private readonly ListBox _counterList;
        private readonly TextBox _titleStr;
        private bool _applying;
        private int _bandageHue = 88;

        private TextBox _ohSearchBox;
        private ListBox _ohSearchList;
        private ListBox _ohList;

        private sealed class CounterEntry
        {
            public string Name;
            public CheckBox Box;
        }

        private readonly List<CounterEntry> _counterEntries = new List<CounterEntry>();

        public DisplayTab()
        {
            var root = Ce.Panel();
            TabControl sub = Ce.SubTabs(root, 6, 3, 510, 314);

            // ================= Display =================
            var disp = Ce.Panel(502, 286);
            Ce.Tab(sub, "Display", disp);

            Canvas grpTitle = Ce.Group(disp, "Title Bar Display", 6, 6, 490, 107);
            _binder.Check(Ce.Check(grpTitle, "Show in UO title bar:", 11, 22, 141, 20), "TitleBarDisplay");
            ComboBox titleParams = Ce.Combo(grpTitle, 380, 19, 104, 23, TitleParams);
            titleParams.SelectedIndex = 0; // wie Razor CE (vor dem Event-Anschluss!)
            _titleStr = Ce.Text(grpTitle, 6, 48, 478, 53);
            _titleStr.AcceptsReturn = true;
            _titleStr.TextWrapping = TextWrapping.Wrap;
            _binder.Text(_titleStr, "TitleBarText");
            titleParams.SelectionChanged += (s, e) =>
            {
                if (_applying || titleParams.SelectedIndex < 0)
                    return;

                // Wie Razor CE: gewaehlten Parameter in den Titeltext einfuegen.
                string param = titleParams.SelectedItem as string;
                if (!string.IsNullOrEmpty(param))
                {
                    _titleStr.Text = $"{_titleStr.Text} {param}".TrimStart();
                    string text = _titleStr.Text;
                    GameThread.Post(() => PropBinder.SetScalarFromString("TitleBarText", text));
                }
            };

            _binder.Check(Ce.Check(disp, "Highlight Spell Reagents on Cast", 6, 119, 205, 20), "HighlightReagents");
            _binder.Check(Ce.Check(disp, "Show noto hue on {char} in TitleBar", 6, 145, 221, 20), "ShowNotoHue");

            // Session-Features ohne Profil-Property in Razor CE — Platzhalter.
            Ce.Check(disp, "Enable gold per sec/min/hour tracker", 273, 120, 223, 19).IsEnabled = false;
            Ce.Check(disp, "Enable damage tracker", 273, 146, 146, 19).IsEnabled = false;

            Canvas grpRazorTitle = Ce.Group(disp, "Razor Title Bar", 6, 175, 490, 71);
            _binder.Check(Ce.Check(grpRazorTitle, "Show in Razor title bar:", 6, 17, 146, 19),
                "ShowInRazorTitleBar");
            Ce.Button(grpRazorTitle, "?", 468, 17, 16, 20, null);
            _binder.Text(Ce.Text(grpRazorTitle, 6, 42, 478, 22), "RazorTitleBarText");

            // ================= Counters =================
            var counters = Ce.Panel(502, 288);
            Ce.Tab(sub, "Counters", counters);

            Canvas grpCounters = Ce.Group(counters, "Counters", 6, 6, 284, 254);
            _counterList = Ce.List(grpCounters, 6, 18, 272, 187);
            Ce.Button(grpCounters, "Add...", 6, 211, 70, 37, async () =>
            {
                // Razor CE: AddCounter-Dialog -> Counter.Register (Phase 3c).
                var owner = TopLevel.GetTopLevel(this) as Window;
                if (owner == null)
                    return;

                AddCounterResult res = await AddCounterDialog.Show(owner);
                if (res == null)
                    return;

                GameThread.Post(() =>
                {
                    try
                    {
                        Counter.Register(new Counter(res.Name, res.Format, res.ItemID, res.Hue, true));
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[UOSagas Razor] Counter anlegen fehlgeschlagen: {ex.Message}");
                    }
                });
            });
            Ce.Button(grpCounters, "Del/Edit", 108, 211, 71, 37, null);
            Ce.Button(grpCounters, "Recount", 208, 211, 70, 37, () =>
            {
                GameThread.Post(() =>
                {
                    try
                    {
                        Counter.FullRecount();
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[UOSagas Razor] Recount fehlgeschlagen: {ex.Message}");
                    }
                });
            });

            _binder.Check(Ce.Check(counters, "Auto search new containers", 299, 24, 200, 20), "AutoSearch");
            _binder.Check(Ce.Check(counters, "Never auto-search pouches", 299, 48, 192, 21), "NoSearchPouches");
            _binder.Check(Ce.Check(counters, "Show Images with Counters", 299, 75, 180, 20), "TitlebarImages");
            _binder.Check(Ce.Check(counters, "Warn when below:", 299, 101, 128, 19), "CounterWarn");
            _binder.Text(Ce.Text(counters, 430, 99, 30, 23), "CounterWarnAmount");

            // ================= Bandage Timer =================
            var bandage = Ce.Panel(502, 288);
            Ce.Tab(sub, "Bandage Timer", bandage);

            _binder.Check(Ce.Check(bandage, "Show bandage timer", 9, 11, 132, 20), "ShowBandageTimer");
            _binder.Combo(Ce.Combo(bandage, 145, 10, 118, 23, "Overhead", "System Messages"),
                "ShowBandageTimerLocation");
            Ce.Label(bandage, "Format && Hue:", 21, 44, 90, 23);
            _binder.Text(Ce.Text(bandage, 113, 41, 150, 23), "ShowBandageTimerFormat");
            Ce.Button(bandage, "Set Hue", 269, 41, 67, 23, async () =>
            {
                var owner = TopLevel.GetTopLevel(this) as Window;
                if (owner == null)
                    return;

                int? hue = await HuePicker.Show(owner, "Bandage Timer Hue", _bandageHue);
                if (hue.HasValue)
                {
                    _bandageHue = hue.Value;
                    GameThread.Post(() => PropBinder.SetScalar("ShowBandageTimerHue", hue.Value));
                }
            });
            _binder.Check(Ce.Check(bandage, "Only show every X seconds:", 9, 72, 168, 20),
                "OnlyShowBandageTimerEvery");
            _binder.Text(Ce.Text(bandage, 180, 71, 48, 23), "OnlyShowBandageTimerSeconds");
            _binder.Check(Ce.Check(bandage, "Show bandaging start message", 9, 100, 190, 19), "ShowBandageStart");
            _binder.Text(Ce.Text(bandage, 205, 98, 146, 23), "BandageStartMessage");
            _binder.Check(Ce.Check(bandage, "Show bandaging end message", 9, 125, 190, 19), "ShowBandageEnd");
            _binder.Text(Ce.Text(bandage, 205, 123, 146, 23), "BandageEndMessage");

            // ================= Overhead Messages =================
            var overhead = Ce.Panel(502, 288);
            Ce.Tab(sub, "Overhead Messages", overhead);

            _binder.Check(Ce.Check(overhead, "Enabled", 8, 9, 71, 24), "ShowOverheadMessages");
            Ce.Label(overhead, "Format:", 5, 39, 48, 15);
            _binder.Text(Ce.Text(overhead, 8, 57, 84, 23), "OverheadFormat");
            Ce.Label(overhead, "Message Style:", 5, 83, 90, 15);
            RadioButton unicode = Ce.Radio(overhead, "Unicode", "ohstyle", 8, 101, 69, 19);
            RadioButton ascii = Ce.Radio(overhead, "ASCII", "ohstyle", 8, 126, 60, 19);
            unicode.IsCheckedChanged += (s, e) => OnOverheadStyleChanged(unicode);
            ascii.IsCheckedChanged += (s, e) => OnOverheadStyleChanged(unicode);
            _overheadUnicode = unicode;
            _overheadAscii = ascii;

            Ce.Button(overhead, "Edit", 8, 135, 84, 28, OnOverheadEdit);
            Ce.Button(overhead, "Remove", 8, 169, 84, 28, OnOverheadRemove);
            Ce.Button(overhead, "Set Hue", 8, 203, 84, 28, OnOverheadSetHue);
            Ce.Button(overhead, "Set Sound", 8, 237, 84, 28, OnOverheadSetSound);

            Ce.Label(overhead, "Search:", 95, 12, 45, 15);
            _ohSearchBox = Ce.Text(overhead, 146, 9, 225, 23);
            Ce.Button(overhead, "Search", 377, 9, 60, 24, OnOverheadSearch);
            Ce.Button(overhead, "Add", 443, 9, 56, 24, OnOverheadAdd);
            // Abweichung zu CE (dokumentiert): CE durchsucht die komplette
            // Cliloc-Tabelle; die haben wir nicht (D5, keine Enumeration ueber
            // die ABI). Wir durchsuchen die zuletzt empfangenen Systemmeldungen
            // — der Trigger laesst sich auch direkt als Suchtext anlegen.
            _ohSearchList = Ce.List(overhead, 98, 39, 401, 111);
            _ohList = Ce.List(overhead, 98, 156, 401, 109);
            RefreshOverheadList();

            // ================= Waypoints =================
            var waypoints = Ce.Panel(502, 288);
            Ce.Tab(sub, "Waypoints", waypoints);

            Ce.List(waypoints, 11, 12, 164, 154).IsEnabled = false;
            _binder.Check(Ce.Check(waypoints, "Show waypoint messages overhead", 184, 12, 213, 19),
                "ShowWaypointOverhead");
            _binder.Check(Ce.Check(waypoints, "Show tile distance every", 184, 37, 148, 19),
                "ShowWaypointDistance");
            _binder.Text(Ce.Text(waypoints, 336, 35, 32, 23), "ShowWaypointSeconds");
            Ce.Label(waypoints, "secs", 374, 37, 58, 18);
            _binder.Check(Ce.Check(waypoints, "Hide waypoint within", 184, 62, 139, 19), "ClearWaypoint");
            _binder.Text(Ce.Text(waypoints, 336, 60, 32, 23), "HideWaypointDistance");
            Ce.Label(waypoints, "tiles", 374, 63, 58, 18);
            _binder.Check(Ce.Check(waypoints, "Create waypoint on death", 184, 87, 162, 19),
                "CreateWaypointOnDeath");

            Ce.Label(waypoints, "Name:", 181, 124, 42, 15);
            Ce.Text(waypoints, 229, 121, 129, 23).IsEnabled = false;
            Ce.Button(waypoints, "Add Waypoint", 364, 121, 129, 24, null);
            Ce.Label(waypoints, "X:", 206, 154, 17, 15);
            Ce.Text(waypoints, 229, 151, 50, 23).IsEnabled = false;
            Ce.Label(waypoints, "Y:", 285, 154, 17, 15);
            Ce.Text(waypoints, 308, 151, 50, 23).IsEnabled = false;
            Ce.Button(waypoints, "Use Current Position", 364, 151, 129, 23, null);
            Ce.Button(waypoints, "Remove Selected", 184, 242, 135, 29, null);
            Ce.Button(waypoints, "Clear Waypoint", 364, 242, 129, 29, null);

            // ================= Buffs / Cooldowns =================
            var buffs = Ce.Panel(502, 286);
            Ce.Tab(sub, "Buffs / Cooldowns", buffs);

            _binder.Check(Ce.Check(buffs, "Show buff/debuff overhead", 14, 12, 172, 19), "ShowBuffDebuffOverhead");
            Ce.Button(buffs, "Buff / Debuff Options", 26, 37, 140, 30, null);

            Canvas grpCooldown = Ce.Group(buffs, "Cooldowns:", 14, 124, 232, 151);
            Ce.Label(grpCooldown, "Bar height:", 9, 29, 64, 15);
            _binder.Text(Ce.Text(grpCooldown, 75, 26, 36, 23), "CooldownHeight");
            Ce.Label(grpCooldown, "Bar width:", 9, 57, 60, 15);
            _binder.Text(Ce.Text(grpCooldown, 75, 54, 36, 23), "CooldownWidth");

            Canvas grpBuff = Ce.Group(buffs, "Buff/Debuff Gump:", 252, 12, 235, 263);
            _binder.Check(Ce.Check(grpBuff, "Show buff/debuff gump", 15, 22, 155, 19), "ShowBuffDebuffGump");
            _binder.Check(Ce.Check(grpBuff, "Show buff/debuff icons", 15, 62, 151, 19), "ShowBuffDebuffIcons");
            _binder.Check(Ce.Check(grpBuff, "Use black background", 15, 87, 143, 19), "UseBlackBuffDebuffBg");
            Ce.Label(grpBuff, "Show time:", 12, 115, 66, 15);
            _binder.Combo(Ce.Combo(grpBuff, 84, 112, 126, 23,
                    "Next to name", "Outside of bar", "Right side of bar", "Under Icon", "None"),
                "ShowBuffDebuffTimeType");
            Ce.Label(grpBuff, "Bar height:", 12, 162, 64, 15);
            _binder.Text(Ce.Text(grpBuff, 78, 159, 36, 23), "ShowBuffDebuffHeight");
            Ce.Label(grpBuff, "Bar width:", 12, 190, 60, 15);
            _binder.Text(Ce.Text(grpBuff, 78, 187, 36, 23), "ShowBuffDebuffWidth");
            Ce.Label(grpBuff, "Sort by:", 12, 226, 47, 15);
            _binder.Combo(Ce.Combo(grpBuff, 65, 223, 145, 23,
                "Alphabetical (A-Z)", "Ascending by time", "Descending by time"), "ShowBuffDebuffSort");

            Content = root;
        }

        private RadioButton _overheadUnicode;
        private RadioButton _overheadAscii;

        private void OnOverheadStyleChanged(RadioButton unicode)
        {
            if (_applying || _binder.Applying || _overheadUnicode == null)
                return;

            int style = unicode.IsChecked == true ? 1 : 0;
            GameThread.Post(() => PropBinder.SetScalar("OverheadStyle", style));
        }

        public void Contribute(UiRequest req)
        {
            _binder.Contribute(req);
            req.TextProps.Add("OverheadStyle");
            req.TextProps.Add("ShowBandageTimerHue");
        }

        public void Apply(UiSnapshot snap)
        {
            _applying = true;
            try
            {
                _binder.Apply(snap);

                if (snap.TextProps.TryGetValue("OverheadStyle", out string st) && int.TryParse(st, out int style))
                {
                    bool uni = style != 0;
                    if (_overheadUnicode.IsChecked != uni)
                        _overheadUnicode.IsChecked = uni;
                    if (_overheadAscii.IsChecked != !uni)
                        _overheadAscii.IsChecked = !uni;
                }

                if (snap.TextProps.TryGetValue("ShowBandageTimerHue", out string bh) && int.TryParse(bh, out int bhue))
                    _bandageHue = bhue;

                // Overhead-Trigger-Liste nach Profilwechsel nachziehen
                // (Count-Vergleich, damit der 500-ms-Pump nicht staendig
                // die Selektion zerstoert).
                if (_ohList != null &&
                    (_ohList.ItemCount != Assistant.Core.OverheadManager.OverheadMessages.Count))
                    RefreshOverheadList();

                ApplyCounters(snap);
            }
            finally
            {
                _applying = false;
            }
        }

        /// <summary>Counter-Liste wie Razor CE: Checkbox = Enabled, Text = "Name (Format): Count".</summary>
        private void ApplyCounters(UiSnapshot snap)
        {
            bool changed = _counterEntries.Count != snap.Counters.Count;
            if (!changed)
            {
                for (int i = 0; i < snap.Counters.Count; i++)
                {
                    if (!string.Equals(_counterEntries[i].Name, snap.Counters[i].Name, StringComparison.Ordinal))
                    {
                        changed = true;
                        break;
                    }
                }
            }

            if (changed)
            {
                _counterList.Items.Clear();
                _counterEntries.Clear();

                foreach (CounterInfo info in snap.Counters)
                {
                    string name = info.Name;
                    var cb = new CheckBox();
                    cb.IsCheckedChanged += (s, e) =>
                    {
                        if (_applying)
                            return;

                        bool val = cb.IsChecked == true;
                        GameThread.Post(() =>
                        {
                            try
                            {
                                Counter.FindCounter(name)?.SetEnabled(val);
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"[UOSagas Razor] Counter umschalten fehlgeschlagen: {ex.Message}");
                            }
                        });
                    };

                    _counterEntries.Add(new CounterEntry { Name = name, Box = cb });
                    _counterList.Items.Add(cb);
                }
            }

            for (int i = 0; i < _counterEntries.Count && i < snap.Counters.Count; i++)
            {
                CounterInfo info = snap.Counters[i];
                CounterEntry entry = _counterEntries[i];

                string text = info.Enabled
                    ? $"{info.Name} ({info.Format}): {info.Amount}"
                    : $"{info.Name} ({info.Format})";

                if (!Equals(entry.Box.Content, text))
                    entry.Box.Content = text;
                if (entry.Box.IsChecked != info.Enabled)
                    entry.Box.IsChecked = info.Enabled;
            }
        }

        // ---- Overhead Messages (Razor CE: OverheadManager + Overhead-Tab) ----

        private void RefreshOverheadList()
        {
            var items = new List<string>();
            foreach (Assistant.Core.OverheadMessage m in Assistant.Core.OverheadManager.OverheadMessages)
            {
                string sound = m.Sound > -1 ? $", sound {m.Sound}" : string.Empty;
                items.Add($"{m.SearchMessage} → {m.MessageOverhead} (hue {m.Hue}{sound})");
            }

            int sel = _ohList.SelectedIndex;
            _ohList.ItemsSource = items;
            if (sel >= 0 && sel < items.Count)
                _ohList.SelectedIndex = sel;
        }

        private Assistant.Core.OverheadMessage SelectedOverheadMessage()
        {
            int idx = _ohList.SelectedIndex;
            if (idx < 0 || idx >= Assistant.Core.OverheadManager.OverheadMessages.Count)
                return null;

            return Assistant.Core.OverheadManager.OverheadMessages[idx];
        }

        private void OnOverheadSearch()
        {
            string search = _ohSearchBox.Text?.Trim();
            var found = new List<string>();

            if (!string.IsNullOrEmpty(search))
            {
                // Suche ueber die zuletzt empfangenen Systemmeldungen (statt
                // CEs Cliloc-Volltextsuche, siehe Kommentar am Control).
                foreach (string msg in Assistant.Core.SystemMessages.Messages)
                {
                    if (msg.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0 && !found.Contains(msg))
                        found.Add(msg);
                }
            }

            _ohSearchList.ItemsSource = found;
        }

        private async void OnOverheadAdd()
        {
            // Trigger = markierte Meldung aus der Suche, sonst der Suchtext.
            string trigger = _ohSearchList.SelectedItem as string;
            if (string.IsNullOrEmpty(trigger))
                trigger = _ohSearchBox.Text?.Trim();

            if (string.IsNullOrEmpty(trigger))
                return;

            var owner = TopLevel.GetTopLevel(this) as Window;
            if (owner == null)
                return;

            string message = await Dialogs.Prompt(owner, "Overhead Message", "Text to display overhead:", trigger);
            if (string.IsNullOrWhiteSpace(message))
                return;

            GameThread.Post(() =>
            {
                Assistant.Core.OverheadManager.OverheadMessages.Add(new Assistant.Core.OverheadMessage
                {
                    SearchMessage = trigger,
                    MessageOverhead = message.Trim(),
                    Hue = Config.GetInt("SysColor"),
                    Sound = -1
                });
                Avalonia.Threading.Dispatcher.UIThread.Post(RefreshOverheadList);
            });
        }

        private async void OnOverheadEdit()
        {
            Assistant.Core.OverheadMessage m = SelectedOverheadMessage();
            if (m == null)
                return;

            var owner = TopLevel.GetTopLevel(this) as Window;
            if (owner == null)
                return;

            string message = await Dialogs.Prompt(owner, "Overhead Message", "Text to display overhead:", m.MessageOverhead);
            if (string.IsNullOrWhiteSpace(message))
                return;

            GameThread.Post(() =>
            {
                m.MessageOverhead = message.Trim();
                Avalonia.Threading.Dispatcher.UIThread.Post(RefreshOverheadList);
            });
        }

        private void OnOverheadRemove()
        {
            Assistant.Core.OverheadMessage m = SelectedOverheadMessage();
            if (m == null)
                return;

            GameThread.Post(() =>
            {
                Assistant.Core.OverheadManager.OverheadMessages.Remove(m);
                Avalonia.Threading.Dispatcher.UIThread.Post(RefreshOverheadList);
            });
        }

        private async void OnOverheadSetHue()
        {
            Assistant.Core.OverheadMessage m = SelectedOverheadMessage();
            if (m == null)
                return;

            var owner = TopLevel.GetTopLevel(this) as Window;
            if (owner == null)
                return;

            int? hue = await HuePicker.Show(owner, "Overhead Message Hue", m.Hue);
            if (!hue.HasValue)
                return;

            GameThread.Post(() =>
            {
                m.Hue = hue.Value;
                Avalonia.Threading.Dispatcher.UIThread.Post(RefreshOverheadList);
            });
        }

        private async void OnOverheadSetSound()
        {
            Assistant.Core.OverheadMessage m = SelectedOverheadMessage();
            if (m == null)
                return;

            var owner = TopLevel.GetTopLevel(this) as Window;
            if (owner == null)
                return;

            // Abweichung zu CE: kein Sound-Browser (SoundMusicManager braucht
            // die MUL-Namen) — numerische Sound-ID, -1 = kein Sound.
            string text = await Dialogs.Prompt(owner, "Overhead Message Sound",
                "Sound ID (-1 = none):", m.Sound.ToString());
            if (text == null || !int.TryParse(text.Trim(), out int sound))
                return;

            GameThread.Post(() =>
            {
                m.Sound = sound;
                Avalonia.Threading.Dispatcher.UIThread.Post(RefreshOverheadList);
            });
        }
    }
}
