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

// UOSagas-Razor: Arm/Dress-Tab im Razor-CE-Layout (Phase 3b).
//
// Blaupause: Razor CE Razor.Designer.cs, dressTab (519x342):
//  * groupBox5 "Arm/Dress Selection" @(8,3) 187x329: dressList + Add.../Remove
//    + "Auto-move conflicts"
//  * groupBox6 "Arm/Dress Items" @(201,3) 311x329: dressItems + Dress/Undress/
//    Add (Target)/Add Current/Remove/Change Undress Bag/Clear List
//
// Funktional ueber Razor.Core DressList: alle Buttons; Target-Buttons nutzen
// Targeting.OneTimeTarget. Anzeige aus UiSnapshot, Schreiben via GameThread.

using System;
using Assistant;
using Avalonia.Controls;

namespace Razor.UI
{
    public class DressTab : UserControl, ICeTab
    {
        private readonly PropBinder _binder = new PropBinder();
        private readonly ListBox _lists;
        private readonly ListBox _items;
        private bool _applying;

        private MainWindow OwnerWindow => VisualRoot as MainWindow;

        /// <summary>Name der aktuell gewaehlten Dress-Liste (fuer Snapshot-Anfragen).</summary>
        internal string SelectedListName => _lists?.SelectedItem as string;

        public DressTab()
        {
            var root = Ce.Panel();

            // --- groupBox5 "Arm/Dress Selection" -------------------------------
            // Liste gegenueber CE (250) gekuerzt, damit die Checkbox unten im
            // zweizeiligen Tab-Layout nicht vom Fensterrand abgeschnitten wird.
            Canvas grpSel = Ce.Group(root, "Arm/Dress Selection", 8, 3, 187, 313);
            _lists = Ce.List(grpSel, 6, 18, 175, 234);
            _lists.SelectionChanged += (s, e) =>
            {
                if (!_applying)
                    OwnerWindow?.RequestSnapshot();
            };
            Ce.Button(grpSel, "Add...", 6, 258, 60, 25, OnAddList);
            Ce.Button(grpSel, "Remove", 121, 258, 60, 25, OnRemoveList);
            _binder.Check(Ce.Check(grpSel, "Auto-move conflicts", 6, 287, 137, 18), "UndressConflicts");

            // --- groupBox6 "Arm/Dress Items" -----------------------------------
            Canvas grpItems = Ce.Group(root, "Arm/Dress Items", 201, 3, 311, 329);
            _items = Ce.List(grpItems, 6, 18, 197, 305);
            Ce.Button(grpItems, "Dress", 209, 18, 96, 32, () => WithList(dl => dl.Dress()));
            Ce.Button(grpItems, "Undress", 209, 56, 96, 32, () => WithList(dl => dl.Undress()));
            Ce.Button(grpItems, "Add (Target)", 209, 94, 96, 32, OnAddTarget);
            Ce.Button(grpItems, "Add Current", 209, 132, 96, 32, OnAddCurrent);
            Ce.Button(grpItems, "Remove", 209, 170, 96, 32, OnRemoveItem);
            Button undressBag = Ce.Button(grpItems, "Change Undress Bag", 209, 208, 96, 40, OnChangeUndressBag);
            undressBag.Content = new TextBlock
            {
                Text = "Change Undress Bag",
                TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                TextAlignment = Avalonia.Media.TextAlignment.Center
            };
            Ce.Button(grpItems, "Clear List", 209, 257, 96, 32, () => WithList(dl => dl.Items.Clear()));

            Content = root;
        }

        /// <summary>Aktion auf der gewaehlten DressList (Game-Thread).</summary>
        private void WithList(Action<DressList> action)
        {
            string name = SelectedListName;
            if (name == null)
                return;

            GameThread.Post(() =>
            {
                DressList dl = DressList.Find(name);
                if (dl != null)
                    action(dl);
            });

            OwnerWindow?.RequestSnapshot();
        }

        private async void OnAddList()
        {
            var owner = OwnerWindow;
            if (owner == null)
                return;

            string name = await InputBox.Show(owner, "Enter a name for the new dress list:", "Dress List");
            if (string.IsNullOrEmpty(name))
                return;

            GameThread.Post(() =>
            {
                if (DressList.Find(name) != null)
                {
                    Console.WriteLine($"[UOSagas Razor] Dress-Liste existiert bereits: {name}");
                    return;
                }

                DressList.Add(new DressList(name));
            });

            owner.RequestSnapshot();
        }

        private void OnRemoveList()
        {
            string name = SelectedListName;
            if (name == null)
                return;

            GameThread.Post(() =>
            {
                DressList dl = DressList.Find(name);
                if (dl != null)
                    DressList.Remove(dl);
            });

            OwnerWindow?.RequestSnapshot();
        }

        /// <summary>Wie Razor CE targItem: Item anvisieren und als Serial aufnehmen.</summary>
        private void OnAddTarget()
        {
            string name = SelectedListName;
            if (name == null)
                return;

            GameThread.Post(() =>
            {
                DressList dl = DressList.Find(name);
                if (dl == null || World.Player == null)
                    return;

                World.Player.SendMessage(MsgLevel.Force, LocString.TargItemAdd);
                Targeting.OneTimeTarget((ground, serial, pt, gfx) =>
                {
                    if (!ground && serial.IsItem)
                    {
                        dl.Items.Add(serial);
                        World.Player?.SendMessage(MsgLevel.Force, LocString.ItemAdded);
                    }
                });
            });
        }

        /// <summary>
        /// Wie Razor CE dressUseCur_Click: alle aktuell getragenen Items der
        /// gewaehlten Liste hinzufuegen. Werktreu iterieren wir World.Player.Contains
        /// (nicht Layer-fuer-Layer per GetItemOnLayer, das pro Layer nur das erste
        /// Item liefert) und filtern auf Ausruestungs-Layer wie CE.
        /// Diagnose: warnt auf der Konsole, wenn keine getragene Ausruestung im
        /// Weltmodell vorhanden ist (dann liefert der Client die Equip-Pakete
        /// 0x78/0x2E offenbar nicht an das Plugin) — damit der User-Test aussagt.
        /// </summary>
        private void OnAddCurrent()
        {
            WithList(dl =>
            {
                if (World.Player == null)
                {
                    Console.WriteLine("[UOSagas Razor] Add Current: kein Spieler im Weltmodell (nicht eingeloggt?).");
                    return;
                }

                int worn = 0;
                int added = 0;

                // Schnappschuss der Liste, da Contains die lebende Ausruestung ist.
                var contains = new System.Collections.Generic.List<Item>(World.Player.Contains);
                foreach (Item item in contains)
                {
                    if (item == null)
                        continue;

                    Layer layer = item.Layer;
                    if (layer <= Layer.Invalid || layer > Layer.LastUserValid ||
                        layer == Layer.Backpack || layer == Layer.Hair || layer == Layer.FacialHair)
                        continue;

                    worn++;
                    if (!dl.Items.Contains(item.Serial))
                    {
                        dl.Items.Add(item.Serial);
                        added++;
                    }
                }

                if (worn == 0)
                    Console.WriteLine(
                        "[UOSagas Razor] Add Current: keine getragene Ausruestung gefunden " +
                        "(World.Player.Contains ist leer — Equip-Pakete 0x78/0x2E kamen nicht an).");
                else
                    Console.WriteLine(
                        $"[UOSagas Razor] Add Current: {added} von {worn} getragenen Items zur Liste '{dl.Name}' hinzugefuegt.");
            });
        }

        private void OnRemoveItem()
        {
            string name = SelectedListName;
            int idx = _items.SelectedIndex;
            if (name == null || idx < 0)
                return;

            GameThread.Post(() =>
            {
                DressList dl = DressList.Find(name);
                if (dl != null && idx < dl.Items.Count)
                    dl.Items.RemoveAt(idx);
            });

            OwnerWindow?.RequestSnapshot();
        }

        private void OnChangeUndressBag()
        {
            string name = SelectedListName;
            if (name == null)
                return;

            GameThread.Post(() =>
            {
                DressList dl = DressList.Find(name);
                if (dl == null || World.Player == null)
                    return;

                World.Player.SendMessage(MsgLevel.Force, LocString.TargUndressBag);
                Targeting.OneTimeTarget((ground, serial, pt, gfx) =>
                {
                    if (!ground && serial.IsItem)
                    {
                        dl.SetUndressBag(serial);
                        World.Player?.SendMessage(MsgLevel.Force, LocString.UB_Set);
                    }
                });
            });
        }

        // --- Snapshot -----------------------------------------------------------

        public void Contribute(UiRequest req)
        {
            _binder.Contribute(req);
            req.DressListName = SelectedListName;
        }

        public void Apply(UiSnapshot snap)
        {
            _applying = true;
            try
            {
                _binder.Apply(snap);

                // Listen-Namen nur bei Aenderung neu aufbauen (Selektion erhalten).
                bool changed = _lists.Items.Count != snap.DressLists.Count;
                if (!changed)
                {
                    for (int i = 0; i < snap.DressLists.Count; i++)
                    {
                        if (!string.Equals(_lists.Items[i] as string, snap.DressLists[i], StringComparison.Ordinal))
                        {
                            changed = true;
                            break;
                        }
                    }
                }

                if (changed)
                {
                    string selected = SelectedListName;
                    _lists.Items.Clear();

                    int selIdx = -1;
                    for (int i = 0; i < snap.DressLists.Count; i++)
                    {
                        _lists.Items.Add(snap.DressLists[i]);
                        if (selected != null &&
                            string.Equals(snap.DressLists[i], selected, StringComparison.OrdinalIgnoreCase))
                            selIdx = i;
                    }

                    if (selIdx >= 0)
                        _lists.SelectedIndex = selIdx;
                }

                // Item-Liste der gewaehlten Dress-Liste.
                string cur = SelectedListName;
                if (cur != null && string.Equals(snap.DressListName, cur, StringComparison.OrdinalIgnoreCase))
                {
                    bool itemsChanged = _items.Items.Count != snap.DressItems.Count;
                    if (!itemsChanged)
                    {
                        for (int i = 0; i < snap.DressItems.Count; i++)
                        {
                            if (!string.Equals(_items.Items[i] as string, snap.DressItems[i],
                                    StringComparison.Ordinal))
                            {
                                itemsChanged = true;
                                break;
                            }
                        }
                    }

                    if (itemsChanged)
                    {
                        _items.Items.Clear();
                        foreach (string s in snap.DressItems)
                            _items.Items.Add(s);
                    }
                }
                else if (cur == null && _items.Items.Count > 0)
                {
                    _items.Items.Clear();
                }
            }
            finally
            {
                _applying = false;
            }
        }
    }
}
