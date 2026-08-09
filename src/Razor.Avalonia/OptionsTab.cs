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

// UOSagas-Razor: Options-Tab im Razor-CE-Layout (Phase 3b).
//
// Blaupause: Razor CE Razor.Designer.cs, moreOptTab -> optionsTabCtrl mit den
// Sub-Tabs "Speech & Messages", "Targeting & Queues", "Additional Options".
// Alle Koordinaten/Texte 1:1 aus der Designer-Datei; JEDE Checkbox ist an die
// gleichnamige Profile-Property gebunden (Property-Namen aus Razor CE
// MainForm/Razor.cs). Hue-Buttons ("Set") sind Platzhalter (Hue-Picker folgt).

using System.Collections.Generic;
using Assistant;
using Avalonia.Controls;
using Avalonia.Layout;

namespace Razor.UI
{
    public class OptionsTab : UserControl, ICeTab
    {
        private readonly PropBinder _binder = new PropBinder();
        private readonly CheckBox _ltHilight;
        private bool _applying;

        /// <summary>Hue-Properties fuer die "Set"-Buttons (Namen wie Razor CE).</summary>
        private readonly List<string> _hueProps = new List<string>();
        private readonly Dictionary<string, int> _hueValues =
            new Dictionary<string, int>(System.StringComparer.OrdinalIgnoreCase);

        public OptionsTab()
        {
            var root = Ce.Panel();
            TabControl sub = Ce.SubTabs(root, 6, 3, 510, 334);

            // ================= Speech & Messages =================
            var speech = Ce.Panel(502, 306);
            Ce.Tab(sub, "Speech & Messages  ", speech);

            Ce.Label(speech, "Search Exemption Hue:", 9, 12, 155, 18);
            HueButton(speech, 168, 12, 47, 20, "ExemptColor", "Search Exemption Hue");
            Ce.Label(speech, "Razor Message Hue:", 9, 38, 155, 18);
            HueButton(speech, 168, 38, 47, 20, "SysColor", "Razor Message Hue");
            Ce.Label(speech, "Warning Message Hue:", 9, 64, 155, 18);
            HueButton(speech, 168, 64, 47, 20, "WarningColor", "Warning Message Hue");

            _binder.Check(Ce.Check(speech, "Override Speech Hue:", 9, 90, 155, 19), "ForceSpeechHue");
            HueButton(speech, 168, 90, 47, 20, "SpeechHue", "Speech Hue");

            _ltHilight = Ce.Check(speech, "Last Target Highlight:", 9, 116, 155, 19);
            _ltHilight.IsCheckedChanged += (s, e) => OnLtHilightToggled();
            HueButton(speech, 168, 116, 47, 20, "LTHilight", "Last Target Highlight Hue");

            _binder.Check(Ce.Check(speech, "Override Spell Hues:", 9, 142, 152, 20), "ForceSpellHue");

            Ce.Label(speech, "Beneficial", 9, 165, 66, 14);
            Ce.Label(speech, "Harmful", 86, 165, 59, 14);
            Ce.Label(speech, "Neutral", 163, 165, 52, 14);
            HueButton(speech, 27, 179, 32, 20, "BeneficialSpellHue", "Beneficial Spell Hue");
            HueButton(speech, 99, 179, 32, 20, "HarmfulSpellHue", "Harmful Spell Hue");
            HueButton(speech, 172, 179, 32, 20, "NeutralSpellHue", "Neutral Spell Hue");

            _binder.Check(Ce.Check(speech, "Override spell format", 9, 205, 152, 20), "OverrideSpellFormat");
            Ce.Label(speech, "Spell Format:", 11, 230, 87, 20);
            _binder.Text(Ce.Text(speech, 89, 228, 126, 23), "SpellFormat");

            _binder.Check(Ce.Check(speech, "Show Names of Incoming People/Creatures", 260, 10, double.NaN, 20),
                "ShowMobNames");
            _binder.Check(Ce.Check(speech, "Show Names of New/Incoming Corpses", 260, 36, double.NaN, 20),
                "ShowCorpseNames");
            _binder.Check(Ce.Check(speech, "Show container labels", 260, 63, 170, 19), "ShowContainerLabels");
            Ce.Button(speech, "...", 438, 63, 33, 19,
                () => ContainerLabelsWindow.Open(TopLevel.GetTopLevel(this) as Window));
            _binder.Check(Ce.Check(speech, "Show health above people/creatures", 260, 88, 231, 20), "ShowHealth");
            Ce.Label(speech, "Health Format:", 283, 112, 115, 18);
            _binder.Text(Ce.Text(speech, 377, 110, 53, 23), "HealthFmt");
            _binder.Check(Ce.Check(speech, "Show mana/stam above party members", 260, 134, 238, 20),
                "ShowPartyStats");
            _binder.Check(Ce.Check(speech, "Show damage dealt", 260, 160, 133, 19), "ShowDamageDealt");
            _binder.Check(Ce.Check(speech, "Overhead", 394, 160, 77, 19), "ShowDamageDealtOverhead");
            _binder.Check(Ce.Check(speech, "Show damage taken", 260, 185, 133, 19), "ShowDamageTaken");
            _binder.Check(Ce.Check(speech, "Overhead", 394, 185, 77, 19), "ShowDamageTakenOverhead");
            _binder.Check(Ce.Check(speech, "Play *emote* sounds", 260, 210, 136, 19), "PlayEmoteSound");

            // ================= Targeting & Queues =================
            var targ = Ce.Panel(502, 308);
            Ce.Tab(sub, "Targeting & Queues  ", targ);

            _binder.Check(Ce.Check(targ, "Queue LastTarget and TargetSelf", 9, 12, 228, 20), "QueueTargets");
            _binder.Check(Ce.Check(targ, "Show Action-Queue status messages", 9, 38, 228, 20), "ActionStatusMsg");
            _binder.Check(Ce.Check(targ, "Auto-Queue Object Delay actions", 9, 64, 202, 20), "QueueActions");
            _binder.Check(Ce.Check(targ, "Object Delay:", 9, 88, 104, 24), "ObjectDelayEnabled");
            _binder.Text(Ce.Text(targ, 109, 89, 32, 23), "ObjectDelay");
            Ce.Label(targ, "ms", 147, 93, 32, 18);
            _binder.Check(Ce.Check(targ, "Show Target Self/Last/Clear Overhead", 9, 118, 232, 19),
                "ShowTargetSelfLastClearOverhead");
            _binder.Check(Ce.Check(targ, "Range check Last Target:", 9, 142, 155, 20), "RangeCheckLT");
            _binder.Text(Ce.Text(targ, 165, 141, 30, 23), "LTRange");
            Ce.Label(targ, "tiles", 201, 144, 30, 18);
            _binder.Check(Ce.Check(targ, "Show target flag on single click", 9, 170, 212, 20), "LastTargTextFlags");
            _binder.Check(Ce.Check(targ, "Attack/target name overhead", 9, 196, 180, 19),
                "ShowAttackTargetOverhead");
            _binder.Check(Ce.Check(targ, "New targets only", 195, 196, 121, 19), "ShowAttackTargetNewOnly");
            _binder.Check(Ce.Check(targ, "Show text target indicator", 9, 221, 232, 19), "ShowTextTargetIndicator");
            Ce.Label(targ, "Format:", 10, 250, 50, 23);
            _binder.Text(Ce.Text(targ, 64, 246, 107, 23), "TargetIndicatorFormat");
            HueButton(targ, 177, 246, 59, 23, "TargetIndicatorHue", "Target Indicator Hue", "Set Hue");

            Canvas smart = Ce.Group(targ, "Smart Targeting:", 243, 11, 253, 153);
            _binder.Check(Ce.Check(smart, "Use Smart Last Target", 6, 22, 212, 20), "SmartLastTarget");
            _binder.Check(Ce.Check(smart, "'Next/Prev Friend' sets beneficial only", 6, 48, 240, 19),
                "OnlyNextPrevBeneficial");
            _binder.Check(Ce.Check(smart, "'Next/Prev Friendly' sets beneficial only", 6, 73, 240, 19),
                "FriendlyBeneficialOnly");
            _binder.Check(Ce.Check(smart, "'Next/Prev Non-Friendly' harmful only", 6, 98, 240, 19),
                "NonFriendlyHarmfulOnly");
            _binder.Check(Ce.Check(smart, "'Next/Prev' by alphabetical order", 6, 123, 240, 19),
                "NextPrevAlphabetical");

            // ================= Additional Options =================
            var misc = Ce.Panel(502, 308);
            Ce.Tab(sub, "Additional Options  ", misc);

            // Auf UOSagas ohne Funktion (kein Client-Pfad ueber die ABI):
            // Pre-AOS-Statusfenster, Season/Light-Level und Force Game Size sind
            // ausgegraut statt entfernt, damit das CE-Layout unveraendert bleibt.
            var oldStatBar = Ce.Check(misc, "Use Pre-AOS status window", 9, 12, 190, 20);
            _binder.Check(oldStatBar, "OldStatBar");
            oldStatBar.IsEnabled = false;
            _binder.Check(Ce.Check(misc, "Auto-Stack Ore/Fish/Logs at Feet", 9, 37, 228, 20), "AutoStack");
            _binder.Check(Ce.Check(misc, "Open new corpses within", 9, 65, 160, 20), "AutoOpenCorpses");
            _binder.Text(Ce.Text(misc, 169, 63, 24, 23), "CorpseRange");
            Ce.Label(misc, "tiles", 201, 67, 36, 16);
            _binder.Check(Ce.Check(misc, "Block opening corpses twice", 9, 91, 209, 20), "BlockOpenCorpsesTwice");
            _binder.Check(Ce.Check(misc, "Block dismount in war mode", 9, 117, 184, 20), "BlockDismount");
            _binder.Check(Ce.Check(misc, "Block trade requests", 9, 143, 184, 20), "BlockTradeRequests");
            _binder.Check(Ce.Check(misc, "Block party invites", 9, 169, 184, 20), "BlockPartyInvites");

            Ce.Label(misc, "Season:", 6, 198, 47, 15).Foreground = Ce.GrayText;
            var season = Ce.Combo(misc, 59, 195, 111, 23,
                "Spring", "Summer", "Fall", "Winter", "Desolation", "Server Default");
            _binder.Combo(season, "Season");
            season.IsEnabled = false;
            Ce.Label(misc, "Light Level:", 6, 226, 70, 15).Foreground = Ce.GrayText;
            var light = new Slider
            {
                Minimum = 0,
                Maximum = 31,
                Value = 15,
                VerticalAlignment = VerticalAlignment.Center
            };
            Ce.At(misc, light, 79, 218, 161, 30);
            _binder.Slider(light, "LightLevel", invert: true);
            light.IsEnabled = false;
            var minMaxLight = Ce.Check(misc, "Enable Min/Max", 9, 250, 114, 20);
            _binder.Check(minMaxLight, "MinMaxLightLevelEnabled");
            minMaxLight.IsEnabled = false;
            Ce.Button(misc, "Set Min", 127, 247, 58, 25, null);
            Ce.Button(misc, "Set Max", 191, 247, 58, 25, null);

            _binder.Check(Ce.Check(misc, "Remember passwords", 260, 12, 148, 20), "RememberPwds");
            _binder.Check(Ce.Check(misc, "Count stealth steps", 260, 37, 130, 20), "CountStealthSteps");
            _binder.Check(Ce.Check(misc, "Overhead", 393, 37, 99, 20), "StealthOverhead");
            Ce.Label(misc, "Format:", 280, 62, 48, 15);
            _binder.Text(Ce.Text(misc, 334, 59, 114, 23), "StealthStepsFormat");
            _binder.Check(Ce.Check(misc, "Auto-open doors", 260, 88, 118, 20), "AutoOpenDoors");
            _binder.Check(Ce.Check(misc, "When hidden", 393, 88, 95, 20), "AutoOpenDoorWhenHidden");
            _binder.Check(Ce.Check(misc, "Auto Unequip hands before casting", 260, 114, 213, 20), "SpellUnequip");
            _binder.Check(Ce.Check(misc, "Auto Unequip for potions", 260, 140, 160, 20), "PotionEquip");
            _binder.Check(Ce.Check(misc, "Re-equip", 423, 140, 69, 20), "PotionReequip");
            _binder.Check(Ce.Check(misc, "Block heal if target is poisoned", 260, 166, 201, 20), "BlockHealPoison");
            var forceSize = Ce.Check(misc, "Force Game Size:", 260, 192, 118, 18);
            _binder.Check(forceSize, "ForceSizeEnabled");
            forceSize.IsEnabled = false;
            var forceSizeX = Ce.Text(misc, 387, 190, 34, 23);
            _binder.Text(forceSizeX, "ForceSizeX");
            forceSizeX.IsEnabled = false;
            Ce.Label(misc, "x", 427, 193, 10, 18).Foreground = Ce.GrayText;
            var forceSizeY = Ce.Text(misc, 443, 191, 33, 23);
            _binder.Text(forceSizeY, "ForceSizeY");
            forceSizeY.IsEnabled = false;
            _binder.Check(Ce.Check(misc, "Static magic fields/walls", 260, 219, 153, 19), "ShowStaticWalls");
            _binder.Check(Ce.Check(misc, "Labels", 421, 220, 60, 17), "ShowStaticWallLabels");
            _binder.Check(Ce.Check(misc, "Buy Agents ignore player gold", 260, 244, 185, 19), "BuyAgentsIgnoreGold");

            Content = root;
        }

        /// <summary>
        /// LTHilight ist ein Int-Hue (0 = aus). Ohne Hue-Picker (spaetere Phase)
        /// schaltet die Checkbox zwischen 0 und dem CE-typischen Hue 0x30 um.
        /// </summary>
        private void OnLtHilightToggled()
        {
            if (_applying || _binder.Applying)
                return;

            bool on = _ltHilight.IsChecked == true;
            GameThread.Post(() =>
            {
                try
                {
                    int cur = Config.GetInt("LTHilight");
                    if (on && cur == 0)
                        Config.SetProperty("LTHilight", 0x0030);
                    else if (!on && cur != 0)
                        Config.SetProperty("LTHilight", 0);
                }
                catch
                {
                }
            });
        }

        /// <summary>
        /// "Set"-Button, der den Hue-Picker fuer eine Int-Hue-Property oeffnet
        /// (Razor CE: HueEntry-Dialog). Schreiben via GameThread.Post.
        /// </summary>
        private Button HueButton(Canvas parent, double x, double y, double w, double h,
            string prop, string title, string caption = "Set")
        {
            if (!_hueProps.Contains(prop))
                _hueProps.Add(prop);

            return Ce.Button(parent, caption, x, y, w, h, async () =>
            {
                var owner = TopLevel.GetTopLevel(this) as Window;
                if (owner == null)
                    return;

                _hueValues.TryGetValue(prop, out int current);

                int? hue = await HuePicker.Show(owner, title, current);
                if (hue.HasValue)
                    GameThread.Post(() => PropBinder.SetScalar(prop, hue.Value));
            });
        }

        public void Contribute(UiRequest req)
        {
            _binder.Contribute(req);
            req.TextProps.Add("LTHilight");
            foreach (string prop in _hueProps)
                req.TextProps.Add(prop);
        }

        public void Apply(UiSnapshot snap)
        {
            _applying = true;
            try
            {
                _binder.Apply(snap);

                if (snap.TextProps.TryGetValue("LTHilight", out string lt) && int.TryParse(lt, out int hue))
                {
                    bool on = hue != 0;
                    if (_ltHilight.IsChecked != on)
                        _ltHilight.IsChecked = on;
                }

                foreach (string prop in _hueProps)
                {
                    if (snap.TextProps.TryGetValue(prop, out string val) && int.TryParse(val, out int v))
                        _hueValues[prop] = v;
                }
            }
            finally
            {
                _applying = false;
            }
        }
    }
}
