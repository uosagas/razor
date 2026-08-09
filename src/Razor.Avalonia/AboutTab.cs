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

// UOSagas-Razor: About-Tab mit Logo + Live-Diagnose (Phase 3b).
//
// Aufbau (CE-schlicht, passt in die 530x372-Tabflaeche):
//  * Oben: PNG-Logo links, rechts Titel/Version/GPL-Hinweis + "Reset Stats".
//  * Mitte "World Tracking": Live-Anzeige aus Diagnostics.Snapshot() — der
//    About-Tab liest NUR den UiSnapshot (Status), den der Game-Thread im
//    500ms-Pump befuellt (nie Diagnostics direkt aus dem UI-Thread).
// Vorbild fuer die Info-Zeilen: Razor Enhanced "Info"-Anzeigen (Player, Backpack,
// Item-/Mobile-Counts, Verbindungsstatus).

using System;
using System.Reflection;
using Assistant;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;

namespace Razor.UI
{
    public class AboutTab : UserControl, ICeTab
    {
        private static readonly IBrush RedText = new SolidColorBrush(Color.Parse("#C00000"));
        private static readonly IBrush NormalText = Brushes.Black;

        private const string Dash = "—";

        // Werte-Labels der World-Tracking-Zeilen (in Apply befuellt).
        private readonly TextBlock _connection;
        private readonly TextBlock _player;
        private readonly TextBlock _position;
        private readonly TextBlock _body;
        private readonly TextBlock _backpack;
        private readonly TextBlock _equipped;
        private readonly TextBlock _worldItems;
        private readonly TextBlock _worldMobiles;
        private readonly TextBlock _packets;
        private readonly TextBlock _equipEvents;

        public AboutTab()
        {
            string version = "0.1";
            try
            {
                Version v = Assembly.GetExecutingAssembly().GetName().Version;
                if (v != null)
                    version = $"{v.Major}.{v.Minor}.{v.Build}";
            }
            catch
            {
            }

            var root = Ce.Panel();

            // --- Kopf: Logo + Titel + Reset ------------------------------------
            Bitmap logo = Branding.TryLoadLogo();
            if (logo != null)
            {
                var img = new Image
                {
                    Source = logo,
                    Width = 56,
                    Height = 56,
                    Stretch = Stretch.Uniform
                };
                Ce.At(root, img, 10, 6, 56, 56);
            }

            TextBlock title = Ce.Label(root, "UOSagas Razor", 78, 8, 320, 26);
            title.FontSize = 19;
            title.FontWeight = FontWeight.Bold;

            TextBlock ver = Ce.Label(root, $"Version {version}", 80, 37, 320, 18);
            ver.Foreground = Ce.GrayText;

            TextBlock sub = Ce.Label(root, "GNU GPL v3 — based on Razor Community Edition", 80, 55, 320, 18);
            sub.FontSize = 11;
            sub.Foreground = Ce.GrayText;

            Ce.Button(root, "Reset Stats", 416, 10, 100, 24, OnResetStats);

            // --- "World Tracking" (Live-Diagnose) ------------------------------
            Canvas grp = Ce.Group(root, "World Tracking", 8, 74, 508, 175);
            _connection = Row(grp, "Connection:", 18);
            _player = Row(grp, "Player:", 33);
            _position = Row(grp, "Position:", 48);
            _body = Row(grp, "Body:", 63);
            _backpack = Row(grp, "Backpack:", 78);
            _equipped = Row(grp, "Equipped items:", 93);
            _worldItems = Row(grp, "World items:", 108);
            _worldMobiles = Row(grp, "World mobiles:", 123);
            _packets = Row(grp, "Packets:", 138);
            _equipEvents = Row(grp, "Player equip events:", 153);

            Content = root;
        }

        /// <summary>Eine Label/Wert-Zeile in der World-Tracking-GroupBox.</summary>
        private static TextBlock Row(Canvas grp, string caption, double y)
        {
            TextBlock cap = Ce.Label(grp, caption, 10, y, 150, 15);
            cap.FontSize = 11;
            cap.Foreground = Ce.GrayText;

            TextBlock val = Ce.Label(grp, Dash, 160, y, 340, 15);
            val.FontSize = 11;
            return val;
        }

        // ICeTab: der About-Tab braucht keine Request-Beitraege — Diagnostics
        // wird immer mitgebaut (BuildDiagnostics), solange der Pump laeuft.
        public void Contribute(UiRequest req)
        {
        }

        public void Apply(UiSnapshot snap)
        {
            if (snap == null)
                return;

            WorldStatus s = snap.Status;

            _connection.Text = s.Connected
                ? $"connected{(string.IsNullOrEmpty(s.ShardName) ? "" : $" ({s.ShardName})")}"
                : "disconnected";
            _connection.Foreground = s.Connected ? NormalText : Ce.GrayText;

            if (!s.HasPlayer)
            {
                // Not logged in: Felder auf "—".
                _player.Text = "Not logged in";
                _player.Foreground = Ce.GrayText;
                _position.Text = Dash;
                _body.Text = Dash;
                _backpack.Text = Dash;
                _backpack.Foreground = NormalText;
                _equipped.Text = Dash;
            }
            else
            {
                _player.Text = $"{s.PlayerName} (0x{s.PlayerSerial:X8})";
                _player.Foreground = NormalText;
                _position.Text = string.IsNullOrEmpty(s.Position) ? Dash : s.Position;
                _body.Text = $"0x{s.Body:X4} ({s.Body})";

                if (s.HasBackpack)
                {
                    _backpack.Text = $"0x{s.BackpackSerial:X8} — {s.BackpackItemCount} items";
                    _backpack.Foreground = NormalText;
                }
                else
                {
                    _backpack.Text = "NOT initialized";
                    _backpack.Foreground = RedText;
                }

                _equipped.Text = s.EquippedCount.ToString();
            }

            _worldItems.Text = s.WorldItems.ToString();
            _worldMobiles.Text = s.WorldMobiles.ToString();
            _packets.Text = $"received {s.TotalRecv} / sent {s.TotalSend}";
            _equipEvents.Text = s.PlayerEquipEvents.ToString();
        }

        private static void OnResetStats()
        {
            GameThread.Post(() => Diagnostics.Reset());
        }
    }
}
