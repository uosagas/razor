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

// UOSagas-Razor: Verwaltung der Container-Labels (Ersatz fuer Razor CE
// UI/ContainerLabels.cs). Geoeffnet ueber den "..."-Button neben
// "Show container labels" im Options-Tab.
//
// Add laeuft ueber Targeting.OneTimeTarget (Game-Thread): Container im Spiel
// anklicken -> Label-Text erfragen -> Eintrag in ContainerLabelList (Profil-
// Sektion "containerlabels", CE-kompatibel). Mutationen laufen ueber
// GameThread.Post, die Anzeige refresht via Avalonia-Dispatcher.

using System;
using System.Collections.Generic;
using Assistant;
using Assistant.Core;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;

namespace Razor.UI
{
    public sealed class ContainerLabelsWindow : Window
    {
        private static ContainerLabelsWindow _open;

        private readonly ListBox _list;

        public static void Open(Window owner)
        {
            if (_open != null)
            {
                _open.Activate();
                return;
            }

            _open = new ContainerLabelsWindow();
            _open.Closed += (s, e) => _open = null;

            if (owner != null)
                _open.Show(owner);
            else
                _open.Show();
        }

        private ContainerLabelsWindow()
        {
            Title = "Container Labels";
            Width = 420;
            Height = 320;
            CanResize = false;
            Background = Ce.WindowBackground;
            FontFamily = new FontFamily("Segoe UI");
            FontSize = Ce.FontSize;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            ShowInTaskbar = false;
            Branding.ApplyTo(this);

            var canvas = Ce.Panel(420, 320);

            Ce.Label(canvas, "Labeled containers (shown on single click):", 10, 8, 390, 18);

            _list = new ListBox();
            Ce.At(canvas, _list, 10, 30, 400, 210);

            Ce.Button(canvas, "Add (Target)", 10, 250, 95, 26, OnAdd);
            Ce.Button(canvas, "Edit Label", 111, 250, 80, 26, OnEditLabel);
            Ce.Button(canvas, "Set Hue", 197, 250, 70, 26, OnSetHue);
            Ce.Button(canvas, "Remove", 273, 250, 70, 26, OnRemove);
            Ce.Button(canvas, "Close", 349, 250, 61, 26, Close);

            Content = canvas;
            Refresh();
        }

        private void Refresh()
        {
            var items = new List<string>();
            foreach (ContainerLabels.ContainerLabel label in ContainerLabels.ContainerLabelList)
                items.Add($"{label.Id} — [{label.Label}] ({label.Type}) hue {label.Hue}");

            int sel = _list.SelectedIndex;
            _list.ItemsSource = items;
            if (sel >= 0 && sel < items.Count)
                _list.SelectedIndex = sel;
        }

        private async void OnAdd()
        {
            string labelText = await Dialogs.Prompt(this, "Container Label", "Label (e.g. Regs):");
            if (string.IsNullOrWhiteSpace(labelText))
                return;

            World.Player?.SendMessage(MsgLevel.Force, "Target the container to label");

            GameThread.Post(() =>
            {
                Targeting.OneTimeTarget((loc, serial, pt, gfx) =>
                {
                    if (!serial.IsItem)
                        return;

                    Item item = World.FindItem(serial);
                    string typeName = item != null
                        ? (ItemData.GetName(item.ItemID.Value) ?? item.Name ?? string.Empty)
                        : string.Empty;

                    // Bestehendes Label fuer dieselbe Serial ersetzen.
                    ContainerLabels.ContainerLabelList.RemoveAll(
                        l => Serial.Parse(l.Id) == serial);

                    ContainerLabels.ContainerLabelList.Add(new ContainerLabels.ContainerLabel
                    {
                        Id = serial.ToString(),
                        Type = typeName,
                        Label = labelText.Trim(),
                        Hue = Config.GetInt("SysColor"),
                        Alias = typeName
                    });

                    World.Player?.SendMessage(MsgLevel.Force, $"Container labeled '{labelText.Trim()}'");
                    Dispatcher.UIThread.Post(Refresh);
                });
            });
        }

        private async void OnEditLabel()
        {
            ContainerLabels.ContainerLabel label = SelectedLabel();
            if (label == null)
                return;

            string text = await Dialogs.Prompt(this, "Container Label", "Label:", label.Label);
            if (string.IsNullOrWhiteSpace(text))
                return;

            GameThread.Post(() =>
            {
                label.Label = text.Trim();
                Dispatcher.UIThread.Post(Refresh);
            });
        }

        private async void OnSetHue()
        {
            ContainerLabels.ContainerLabel label = SelectedLabel();
            if (label == null)
                return;

            int? hue = await HuePicker.Show(this, "Label Hue", label.Hue);
            if (!hue.HasValue)
                return;

            GameThread.Post(() =>
            {
                label.Hue = hue.Value;
                Dispatcher.UIThread.Post(Refresh);
            });
        }

        private void OnRemove()
        {
            ContainerLabels.ContainerLabel label = SelectedLabel();
            if (label == null)
                return;

            GameThread.Post(() =>
            {
                ContainerLabels.ContainerLabelList.Remove(label);
                Dispatcher.UIThread.Post(Refresh);
            });
        }

        private ContainerLabels.ContainerLabel SelectedLabel()
        {
            int idx = _list.SelectedIndex;
            if (idx < 0 || idx >= ContainerLabels.ContainerLabelList.Count)
                return null;

            return ContainerLabels.ContainerLabelList[idx];
        }
    }
}
