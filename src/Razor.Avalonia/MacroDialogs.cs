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

// UOSagas-Razor: Dialoge fuer das Macro-Action-Kontextmenue.
//
// Blaupausen: Razor CE UI/MacroInsertWait.cs (Radio Pause/Gump/Menu/Target/
// Stat) und UI/MacroInsertIf.cs (Variable/Operator/Wert + Counter/Skill).
// Die Dialoge sind Core-frei: sie liefern nur ein Ergebnis-Objekt zurueck;
// die eigentliche MacroAction baut der Aufrufer auf dem Game-Thread.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;

namespace Razor.UI
{
    /// <summary>Ergebnis des Wait/Pause-Dialogs.</summary>
    public sealed class MacroWaitResult
    {
        public const int KindPause = 0;
        public const int KindGump = 1;
        public const int KindMenu = 2;
        public const int KindTarget = 3;
        public const int KindStat = 4;

        public int Kind = KindPause;
        public int PauseMs = 1000;
        public int Stat;             // 0=Hits, 1=Mana, 2=Stamina (== IfVarType)
        public bool GreaterEq = true;
        public int Value = 100;
    }

    /// <summary>Wait/Pause einfuegen bzw. bearbeiten (CE: MacroInsertWait).</summary>
    public static class MacroWaitDialog
    {
        public static async Task<MacroWaitResult> Show(Window owner, MacroWaitResult preset = null)
        {
            Window dlg = Ce.Dialog("Wait / Pause", 300, 235);
            var canvas = Ce.Panel(300, 235);

            RadioButton pause = Ce.Radio(canvas, "Pause for (ms):", "wait", 10, 10, 120, 20);
            TextBox ms = Ce.Text(canvas, 140, 8, 90, 23, (preset?.PauseMs ?? 1000).ToString());

            RadioButton gump = Ce.Radio(canvas, "Wait for Gump", "wait", 10, 38, 160, 20);
            RadioButton menu = Ce.Radio(canvas, "Wait for Menu", "wait", 10, 64, 160, 20);
            RadioButton targ = Ce.Radio(canvas, "Wait for Target", "wait", 10, 90, 160, 20);
            RadioButton stat = Ce.Radio(canvas, "Wait for Stat:", "wait", 10, 116, 110, 20);

            ComboBox statSel = Ce.Combo(canvas, 28, 142, 92, 23, "Hits", "Mana", "Stamina");
            ComboBox opSel = Ce.Combo(canvas, 126, 142, 58, 23, ">=", "<=");
            TextBox statVal = Ce.Text(canvas, 190, 142, 60, 23, (preset?.Value ?? 100).ToString());

            statSel.SelectedIndex = preset?.Stat is >= 0 and <= 2 ? preset.Stat : 0;
            opSel.SelectedIndex = preset == null || preset.GreaterEq ? 0 : 1;

            switch (preset?.Kind ?? MacroWaitResult.KindPause)
            {
                case MacroWaitResult.KindGump: gump.IsChecked = true; break;
                case MacroWaitResult.KindMenu: menu.IsChecked = true; break;
                case MacroWaitResult.KindTarget: targ.IsChecked = true; break;
                case MacroWaitResult.KindStat: stat.IsChecked = true; break;
                default: pause.IsChecked = true; break;
            }

            void UpdateEnabled()
            {
                ms.IsEnabled = pause.IsChecked == true;
                bool st = stat.IsChecked == true;
                statSel.IsEnabled = opSel.IsEnabled = statVal.IsEnabled = st;
            }

            foreach (RadioButton rb in new[] { pause, gump, menu, targ, stat })
                rb.IsCheckedChanged += (s, e) => UpdateEnabled();
            UpdateEnabled();

            MacroWaitResult result = null;

            void Commit()
            {
                var r = new MacroWaitResult();
                if (pause.IsChecked == true)
                {
                    if (!int.TryParse(ms.Text?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture,
                            out int v) || v <= 0)
                        return;

                    r.Kind = MacroWaitResult.KindPause;
                    r.PauseMs = v;
                }
                else if (gump.IsChecked == true)
                {
                    r.Kind = MacroWaitResult.KindGump;
                }
                else if (menu.IsChecked == true)
                {
                    r.Kind = MacroWaitResult.KindMenu;
                }
                else if (targ.IsChecked == true)
                {
                    r.Kind = MacroWaitResult.KindTarget;
                }
                else
                {
                    if (!int.TryParse(statVal.Text?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture,
                            out int v))
                        return;

                    r.Kind = MacroWaitResult.KindStat;
                    r.Stat = Math.Max(0, statSel.SelectedIndex);
                    r.GreaterEq = opSel.SelectedIndex == 0;
                    r.Value = v;
                }

                result = r;
                dlg.Close();
            }

            Ce.Button(canvas, "Okay", 118, 178, 75, 26, Commit);
            Ce.Button(canvas, "Cancel", 207, 178, 75, 26, () => dlg.Close());

            dlg.KeyDown += (s, e) =>
            {
                if (e.Key == Key.Enter)
                    Commit();
                else if (e.Key == Key.Escape)
                    dlg.Close();
            };

            dlg.Content = canvas;
            await dlg.ShowDialog(owner);
            return result;
        }
    }

    /// <summary>Ergebnis des Bedingungs-Dialogs (If/While/DoWhile).
    /// Var traegt den rohen IfVarType-Wert (0-8, 50 Counter, 100 Skill).</summary>
    public sealed class MacroCondResult
    {
        public int Var;
        public sbyte Op;          // 0 kleiner-gleich, 1 groesser-gleich, 2 kleiner, 3 groesser
        public int Number;
        public string Text;
        public double SkillValue;
        public int SkillId = -1;
        public string Counter;
    }

    /// <summary>Bedingung fuer If/While/DoWhile (CE: MacroInsertIf).</summary>
    public static class MacroConditionDialog
    {
        private static readonly int[] VarIds = { 0, 1, 2, 3, 4, 5, 6, 7, 8, 50, 100 };

        private static readonly string[] VarNames =
        {
            "Hits", "Mana", "Stamina", "Poisoned", "Sys Message", "Weight",
            "Mounted", "R-Hand Empty", "L-Hand Empty", "Counter", "Skill"
        };

        /// <summary>counters/skills kommen vom Game-Thread (Counter.List bzw.
        /// Ultima.Skills, Index = SkillId); preset != null = Bearbeiten.</summary>
        public static async Task<MacroCondResult> Show(Window owner, string title,
            List<string> counters, List<string> skills, MacroCondResult preset = null)
        {
            Window dlg = Ce.Dialog(title, 320, 240);
            var canvas = Ce.Panel(320, 240);

            Ce.Label(canvas, "Variable:", 10, 13, 84, 18);
            ComboBox varSel = Ce.Combo(canvas, 100, 10, 200, 23, VarNames);

            Ce.Label(canvas, "Operator:", 10, 42, 84, 18);
            ComboBox opSel = Ce.Combo(canvas, 100, 39, 80, 23, "<=", ">=", "<", ">");

            Ce.Label(canvas, "Value:", 10, 71, 84, 18);
            TextBox value = Ce.Text(canvas, 100, 68, 200, 23);

            Ce.Label(canvas, "Counter:", 10, 100, 84, 18);
            ComboBox counterSel = Ce.Combo(canvas, 100, 97, 200, 23,
                counters?.ToArray() ?? Array.Empty<string>());

            Ce.Label(canvas, "Skill:", 10, 129, 84, 18);
            ComboBox skillSel = Ce.Combo(canvas, 100, 126, 200, 23,
                skills?.ToArray() ?? Array.Empty<string>());

            int VarId() => VarIds[Math.Max(0, varSel.SelectedIndex)];

            void UpdateEnabled()
            {
                int id = VarId();
                bool isBool = id is 3 or 6 or 7 or 8; // Poisoned/Mounted/R-Hand/L-Hand
                bool isText = id == 4;                // Sys Message

                opSel.IsEnabled = !isBool && !isText;
                value.IsEnabled = !isBool;
                counterSel.IsEnabled = id == 50;
                skillSel.IsEnabled = id == 100;
            }

            varSel.SelectionChanged += (s, e) => UpdateEnabled();

            // Preset (Bearbeiten) anwenden
            varSel.SelectedIndex = Math.Max(0, Array.IndexOf(VarIds, preset?.Var ?? 0));
            opSel.SelectedIndex = preset != null && preset.Op is >= 0 and <= 3 ? preset.Op : 0;
            if (preset != null)
            {
                value.Text = preset.Var switch
                {
                    4 => preset.Text ?? string.Empty,
                    100 => preset.SkillValue.ToString(CultureInfo.InvariantCulture),
                    _ => preset.Number.ToString()
                };

                if (preset.Counter != null && counters != null)
                    counterSel.SelectedIndex = counters.IndexOf(preset.Counter);
                if (preset.SkillId >= 0 && skills != null && preset.SkillId < skills.Count)
                    skillSel.SelectedIndex = preset.SkillId;
            }

            UpdateEnabled();

            MacroCondResult result = null;

            void Commit()
            {
                var r = new MacroCondResult
                {
                    Var = VarId(),
                    Op = (sbyte) Math.Max(0, opSel.SelectedIndex)
                };

                switch (r.Var)
                {
                    case 4: // Sys Message: Text zwingend
                        r.Text = value.Text?.Trim();
                        if (string.IsNullOrEmpty(r.Text))
                            return;
                        break;

                    case 3:
                    case 6:
                    case 7:
                    case 8: // bool-Variablen: kein Wert
                        break;

                    case 50: // Counter: Name + Zahl
                        r.Counter = counterSel.SelectedItem as string;
                        if (r.Counter == null || !int.TryParse(value.Text?.Trim(), NumberStyles.Integer,
                                CultureInfo.InvariantCulture, out r.Number))
                            return;
                        break;

                    case 100: // Skill: Id + double
                        r.SkillId = skillSel.SelectedIndex;
                        if (r.SkillId < 0 || !double.TryParse(value.Text?.Trim(), NumberStyles.Float,
                                CultureInfo.InvariantCulture, out r.SkillValue))
                            return;
                        break;

                    default: // Hits/Mana/Stamina/Weight
                        if (!int.TryParse(value.Text?.Trim(), NumberStyles.Integer,
                                CultureInfo.InvariantCulture, out r.Number))
                            return;
                        break;
                }

                result = r;
                dlg.Close();
            }

            Ce.Button(canvas, "Okay", 138, 172, 75, 26, Commit);
            Ce.Button(canvas, "Cancel", 227, 172, 75, 26, () => dlg.Close());

            dlg.KeyDown += (s, e) =>
            {
                if (e.Key == Key.Enter)
                    Commit();
                else if (e.Key == Key.Escape)
                    dlg.Close();
            };

            dlg.Content = canvas;
            await dlg.ShowDialog(owner);
            return result;
        }
    }
}
