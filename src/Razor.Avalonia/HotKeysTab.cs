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

// UOSagas-Razor: Hot Keys-Tab im Razor-CE-Layout (Phase 3c, FUNKTIONAL).
//
// Blaupause: Razor CE Razor.Designer.cs, hotkeysTab (519x342) + die Handler
// aus Razor/UI/Razor.cs (hotkeyTree_AfterSelect, setHK_Click, unsetHK_Click,
// key_KeyUp, dohotkey_Click, filterHotkeys_TextChanged):
//  * Filter-Label + Textbox @(8,11)/(50,8)
//  * hotkeyTree (TreeView) @(8,37) 323x295 — Kategorien/Unterkategorien wie
//    der statische Konstruktor von CE HotKey (Items/Potions, Targets/...,
//    Agents, Dress, Macros, Spells/Zirkel, Skills, Misc/..., Friends, Scripts)
//  * groupBox8 "Hot Key" @(337,8) 175x156: Ctrl/Alt/Shift, Key:, Command,
//    Pass to UO, Unset/Set
//  * "Execute Selected Hot Key" @(337,170) 175x29, Status-Label darunter
//  * NEU (nicht in CE): "Disable all hotkeys"-Checkbox unter dem Status —
//    schaltet HotKey.Enabled (CE macht das nur ueber die Toggle-Hotkeys).
//
// Threading: Anzeige ausschliesslich aus UiSnapshot.HotKeys; alle Aenderungen
// (Set/Unset/Execute/Enable) via GameThread.Post.

using System;
using System.Collections.Generic;
using Assistant;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;

namespace Razor.UI
{
    public class HotKeysTab : UserControl, ICeTab
    {
        private readonly MainWindow _owner;

        private readonly TextBox _filter;
        private readonly TreeView _tree;
        private readonly CheckBox _ctrl;
        private readonly CheckBox _alt;
        private readonly CheckBox _shift;
        private readonly TextBox _keyBox;
        private readonly TextBox _command;
        private readonly CheckBox _pass;
        private readonly Button _unset;
        private readonly Button _set;
        private readonly Button _exec;
        private readonly TextBlock _status;
        private readonly CheckBox _disableAll;

        private bool _applying;
        private int _lastKV; // zuletzt erfasster WinForms-Keys-Wert (CE: m_LastKV)
        private string _treeFilter = string.Empty;

        private readonly List<HotKeyInfo> _hotkeys = new List<HotKeyInfo>();
        private readonly Dictionary<string, TreeViewItem> _leaves =
            new Dictionary<string, TreeViewItem>(StringComparer.Ordinal);

        /// <summary>Identitaet des ausgewaehlten Hotkeys ("L:1058"/"S:...").</summary>
        private string SelectedIdentity => (_tree?.SelectedItem as TreeViewItem)?.Tag as string;

        public HotKeysTab(MainWindow owner)
        {
            _owner = owner;

            var root = Ce.Panel();

            Ce.Label(root, "Filter:", 8, 11, 36, 15);
            _filter = Ce.Text(root, 50, 8, 281, 23);
            _filter.TextChanged += (s, e) =>
            {
                if (_applying)
                    return;

                _treeFilter = _filter.Text?.Trim() ?? string.Empty;
                RebuildTree();
            };

            _tree = new TreeView();
            Ce.At(root, _tree, 8, 37, 323, 295);
            _tree.SelectionChanged += (s, e) =>
            {
                if (!_applying)
                    ApplySelection();
            };

            Canvas grp = Ce.Group(root, "Hot Key", 337, 8, 175, 156);
            _ctrl = Ce.Check(grp, "Ctrl", 8, 20, 48, 16);
            _alt = Ce.Check(grp, "Alt", 58, 20, 46, 16);
            _shift = Ce.Check(grp, "Shift", 113, 20, 56, 16);
            Ce.Label(grp, "Key:", 8, 44, 28, 20);
            _keyBox = Ce.Text(grp, 36, 40, 133, 23);
            _keyBox.IsReadOnly = true; // Erfassung ueber KeyDown, kein Tippen
            _keyBox.KeyDown += OnKeyCapture;
            Ce.Label(grp, "Command", 8, 76, 62, 20);
            _command = Ce.Text(grp, 73, 73, 96, 23);
            _pass = Ce.Check(grp, "Pass to UO", 11, 102, 113, 16);
            _unset = Ce.Button(grp, "Unset", 11, 124, 56, 26, OnUnset);
            _set = Ce.Button(grp, "Set", 113, 124, 56, 26, OnSet);

            _exec = Ce.Button(root, "Execute Selected Hot Key", 337, 170, 175, 29, OnExecute);

            _status = Ce.Label(root, "", 337, 226, 175, 64);
            _status.Foreground = Ce.GrayText;
            _status.TextAlignment = TextAlignment.Center;

            // NEU gegenueber CE: kompletter Aus-Schalter als Checkbox.
            _disableAll = Ce.Check(root, "Disable all hotkeys", 337, 300, 175, 20);
            _disableAll.IsCheckedChanged += (s, e) =>
            {
                if (_applying)
                    return;

                bool disabled = _disableAll.IsChecked == true;
                GameThread.Post(() =>
                {
                    HotKey.Enabled = !disabled;
                    HotKey.UpdateStatus();
                });
                _owner.RequestSnapshot();
            };

            SetEditorEnabled(false);

            Content = root;
        }

        // --- Key-Erfassung (CE: key_KeyUp) ------------------------------------

        private void OnKeyCapture(object sender, KeyEventArgs e)
        {
            Assistant.Keys k = AvaloniaKeyMap.ToKeys(e.Key);
            if (k != Assistant.Keys.None)
            {
                _lastKV = (int) k;
                _keyBox.Text = k.ToString();
            }

            e.Handled = true;
        }

        // --- Kommandos ---------------------------------------------------------

        /// <summary>CE setHK_Click: Belegung setzen, Konflikt raeumt die alte Belegung.</summary>
        private void OnSet()
        {
            string identity = SelectedIdentity;
            if (identity == null || _lastKV == 0)
                return;

            ModKeys mod = ModKeys.None;
            if (_ctrl.IsChecked == true)
                mod |= ModKeys.Control;
            if (_alt.IsChecked == true)
                mod |= ModKeys.Alt;
            if (_shift.IsChecked == true)
                mod |= ModKeys.Shift;

            int key = _lastKV;
            bool pass = _pass.IsChecked == true;
            string command = _command.Text?.Trim() ?? string.Empty;

            GameThread.Post(() =>
            {
                KeyData hk = UiSnapshotBuilder.FindHotKey(identity);
                if (hk == null)
                    return;

                // Konflikt: CE fragt per MessageBox; headless raeumen wir die
                // alte Belegung (CE-"Yes"-Zweig) und loggen das.
                KeyData g = HotKey.Get(key, mod);
                if (g != null && g != hk)
                {
                    Console.WriteLine($"[UOSagas Razor] Hotkey-Konflikt: '{g.DisplayName}' verliert die Belegung an '{hk.DisplayName}'.");
                    g.Key = 0;
                    g.Mod = ModKeys.None;
                    g.SendToUO = false;
                }

                hk.Key = key;
                hk.Mod = mod;
                hk.SendToUO = pass;

                if (!string.IsNullOrEmpty(command))
                {
                    if (HotKey.ValidateCommand(command))
                        hk.Command = command;
                    else if (!command.Equals(hk.Command, StringComparison.OrdinalIgnoreCase))
                        Console.WriteLine("[UOSagas Razor] Hotkey-Command bereits vergeben.");
                }
                else
                {
                    hk.Command = string.Empty;
                }

                HotKey.UpdateStatus();
            });

            _owner.RequestSnapshot();
        }

        /// <summary>CE unsetHK_Click.</summary>
        private void OnUnset()
        {
            string identity = SelectedIdentity;
            if (identity == null)
                return;

            GameThread.Post(() =>
            {
                KeyData hk = UiSnapshotBuilder.FindHotKey(identity);
                if (hk == null)
                    return;

                hk.Key = 0;
                hk.Mod = ModKeys.None;
                hk.SendToUO = false;
                hk.Command = string.Empty;

                HotKey.UpdateStatus();
            });

            ClearEditor();
            _owner.RequestSnapshot();
        }

        /// <summary>CE dohotkey_Click.</summary>
        private void OnExecute()
        {
            string identity = SelectedIdentity;
            if (identity == null)
                return;

            GameThread.Post(() =>
            {
                KeyData hk = UiSnapshotBuilder.FindHotKey(identity);
                if (hk == null)
                    return;

                if (World.Player == null && !hk.Global)
                    return;

                if (Assistant.Macros.MacroManager.AcceptActions)
                    Assistant.Macros.MacroManager.Action(
                        new Assistant.Macros.HotKeyAction(hk.LocName, hk.StrName));

                hk.Callback();
            });
        }

        // --- Anzeige ------------------------------------------------------------

        public void Contribute(UiRequest req)
        {
            // Hotkey-Liste ist immer im Snapshot enthalten.
        }

        public void Apply(UiSnapshot snap)
        {
            _applying = true;
            try
            {
                // Baum nur neu bauen, wenn sich Menge/Belegungen geaendert haben.
                bool changed = _hotkeys.Count != snap.HotKeys.Count;
                if (!changed)
                {
                    for (int i = 0; i < snap.HotKeys.Count; i++)
                    {
                        HotKeyInfo a = _hotkeys[i];
                        HotKeyInfo b = snap.HotKeys[i];
                        if (!string.Equals(a.Identity, b.Identity, StringComparison.Ordinal) ||
                            a.Key != b.Key || a.Mod != b.Mod ||
                            !string.Equals(a.DisplayName, b.DisplayName, StringComparison.Ordinal))
                        {
                            changed = true;
                            break;
                        }
                    }
                }

                if (changed)
                {
                    _hotkeys.Clear();
                    _hotkeys.AddRange(snap.HotKeys);
                    RebuildTree();
                }
                else
                {
                    // SendToUO/Command koennen sich ohne Baum-Relevanz aendern.
                    _hotkeys.Clear();
                    _hotkeys.AddRange(snap.HotKeys);
                }

                if (_disableAll.IsChecked != !snap.HotKeysEnabled)
                    _disableAll.IsChecked = !snap.HotKeysEnabled;

                _status.Text = snap.HotKeyStatus ?? string.Empty;
            }
            finally
            {
                _applying = false;
            }
        }

        private HotKeyInfo FindInfo(string identity)
        {
            if (identity == null)
                return null;

            foreach (HotKeyInfo info in _hotkeys)
            {
                if (string.Equals(info.Identity, identity, StringComparison.Ordinal))
                    return info;
            }

            return null;
        }

        /// <summary>CE hotkeyTree_AfterSelect: Editor-Controls befuellen.</summary>
        private void ApplySelection()
        {
            HotKeyInfo info = FindInfo(SelectedIdentity);
            if (info == null)
            {
                ClearEditor();
                return;
            }

            bool wasApplying = _applying;
            _applying = true;
            try
            {
                SetEditorEnabled(true);

                _lastKV = info.Key;
                _keyBox.Text = KeyText(info.Key);
                _ctrl.IsChecked = (info.Mod & ModKeys.Control) != 0;
                _alt.IsChecked = (info.Mod & ModKeys.Alt) != 0;
                _shift.IsChecked = (info.Mod & ModKeys.Shift) != 0;
                _pass.IsChecked = info.SendToUO;
                _command.Text = info.Command ?? string.Empty;
            }
            finally
            {
                _applying = wasApplying;
            }
        }

        private static string KeyText(int key)
        {
            switch (key)
            {
                case 0: return "";
                case -1: return "MouseWheel UP";
                case -2: return "MouseWheel DOWN";
                case -3: return "Mouse MID Button";
                case -4: return "Mouse XButton 1";
                case -5: return "Mouse XButton 2";
                default:
                    return key > 0 && key < 256 ? ((Assistant.Keys) key).ToString() : "";
            }
        }

        private void ClearEditor()
        {
            bool wasApplying = _applying;
            _applying = true;
            try
            {
                _lastKV = 0;
                _keyBox.Text = "";
                _command.Text = "";
                _ctrl.IsChecked = false;
                _alt.IsChecked = false;
                _shift.IsChecked = false;
                _pass.IsChecked = false;

                SetEditorEnabled(false);
            }
            finally
            {
                _applying = wasApplying;
            }
        }

        private void SetEditorEnabled(bool enabled)
        {
            _ctrl.IsEnabled = enabled;
            _alt.IsEnabled = enabled;
            _shift.IsEnabled = enabled;
            _keyBox.IsEnabled = enabled;
            _command.IsEnabled = enabled;
            _pass.IsEnabled = enabled;
            _unset.IsEnabled = enabled;
            _set.IsEnabled = enabled;
            _exec.IsEnabled = enabled;
        }

        // --- Baum (CE: HotKey statischer Konstruktor + RebuildList) -------------

        private void RebuildTree()
        {
            bool wasApplying = _applying;
            _applying = true;
            try
            {
                string selected = SelectedIdentity;

                _tree.Items.Clear();
                _leaves.Clear();

                var rootItem = new TreeViewItem { Header = "Hot Keys", IsExpanded = true };
                _tree.Items.Add(rootItem);

                bool filtering = _treeFilter.Length > 0;

                // Skelett wie Razor CE (statischer Konstruktor von HotKey).
                var catNodes = new Dictionary<HKCategory, TreeViewItem>();
                var subNodes = new Dictionary<HKSubCat, TreeViewItem>();

                TreeViewItem Cat(HKCategory cat, string name)
                {
                    var item = new TreeViewItem { Header = name, IsExpanded = filtering };
                    catNodes[cat] = item;
                    rootItem.Items.Add(item);
                    return item;
                }

                void Sub(TreeViewItem parent, HKSubCat sub, string name)
                {
                    var item = new TreeViewItem { Header = name, IsExpanded = filtering };
                    subNodes[sub] = item;
                    parent.Items.Add(item);
                }

                TreeViewItem items = Cat(HKCategory.Items, "Items");
                Sub(items, HKSubCat.Potions, "Potions");

                TreeViewItem targets = Cat(HKCategory.Targets, "Targets");
                Sub(targets, HKSubCat.SubTargetCriminal, "Criminal Targets");
                Sub(targets, HKSubCat.SubTargetEnemy, "Enemy Targets");
                Sub(targets, HKSubCat.SubTargetFriendly, "Friendly Targets");
                Sub(targets, HKSubCat.SubTargetGrey, "Grey Targets");
                Sub(targets, HKSubCat.SubTargetInnocent, "Innocent Targets");
                Sub(targets, HKSubCat.SubTargetMurderer, "Murderer Targets");
                Sub(targets, HKSubCat.SubTargetNonFriendly, "Non-Friendly Targets");

                Cat(HKCategory.Agents, "Agents");
                Cat(HKCategory.Dress, "Dress");
                Cat(HKCategory.Macros, "Macros");

                TreeViewItem spells = Cat(HKCategory.Spells, "Spells");
                Sub(spells, HKSubCat.FirstC, "1st");
                Sub(spells, HKSubCat.SecondC, "2nd");
                Sub(spells, HKSubCat.ThirdC, "3rd");
                Sub(spells, HKSubCat.FourthC, "4th");
                Sub(spells, HKSubCat.FifthC, "5th");
                Sub(spells, HKSubCat.SixthC, "6th");
                Sub(spells, HKSubCat.SeventhC, "7th");
                Sub(spells, HKSubCat.EighthC, "8th");
                Sub(spells, HKSubCat.NecroC, "Necro");
                Sub(spells, HKSubCat.PaladinC, "Paladin");
                Sub(spells, HKSubCat.BushidoC, "Bushido");
                Sub(spells, HKSubCat.NinjisuC, "Ninjisu");
                Sub(spells, HKSubCat.SpellWeaveC, "Spellweaving");
                Sub(spells, HKSubCat.MystC, "Mysticism");
                Sub(spells, HKSubCat.MasteriesC, "Masteries");

                Cat(HKCategory.Skills, "Skills");

                TreeViewItem misc = Cat(HKCategory.Misc, "Misc");
                Sub(misc, HKSubCat.SpecialMoves, "Special Moves");
                Sub(misc, HKSubCat.PetCommands, "Pet Commands");

                Cat(HKCategory.Friends, "Friends");
                Cat(HKCategory.Scripts, "Scripts");

                // Hotkeys einhaengen (Reihenfolge = Registrierungsreihenfolge, wie CE).
                TreeViewItem toSelect = null;
                foreach (HotKeyInfo info in _hotkeys)
                {
                    string text = LeafText(info);

                    if (filtering &&
                        text.IndexOf(_treeFilter, StringComparison.OrdinalIgnoreCase) < 0)
                        continue;

                    TreeViewItem parent = rootItem;
                    if (info.Category != HKCategory.None && catNodes.TryGetValue(info.Category, out TreeViewItem cn))
                        parent = cn;
                    if (info.SubCat != HKSubCat.None && subNodes.TryGetValue(info.SubCat, out TreeViewItem sn))
                        parent = sn;

                    var leaf = new TreeViewItem
                    {
                        Header = text,
                        Tag = info.Identity
                    };

                    parent.Items.Add(leaf);
                    _leaves[info.Identity] = leaf;

                    if (selected != null && string.Equals(info.Identity, selected, StringComparison.Ordinal))
                        toSelect = leaf;
                }

                if (toSelect != null)
                {
                    // Elternkette aufklappen, damit die Selektion sichtbar bleibt.
                    for (TreeViewItem p = toSelect.Parent as TreeViewItem; p != null; p = p.Parent as TreeViewItem)
                        p.IsExpanded = true;

                    _tree.SelectedItem = toSelect;
                }
                else if (selected != null)
                {
                    ClearEditor();
                }
            }
            finally
            {
                _applying = wasApplying;
            }
        }

        /// <summary>CE KeyData.ToString: "Name (Control+U)" bzw. "Name (not assigned)".</summary>
        private static string LeafText(HotKeyInfo info)
        {
            if (info.Key == 0)
                return $"{info.DisplayName} (not assigned)";

            var sb = new System.Text.StringBuilder();
            if ((info.Mod & ModKeys.Control) != 0)
                sb.Append("Control+");
            if ((info.Mod & ModKeys.Alt) != 0)
                sb.Append("Alt+");
            if ((info.Mod & ModKeys.Shift) != 0)
                sb.Append("Shift+");
            sb.Append(KeyText(info.Key));

            return $"{info.DisplayName} ({sb})";
        }
    }
}
