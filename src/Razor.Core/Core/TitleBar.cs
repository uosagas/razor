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

// UOSagas-Razor: UO-Titelleisten-Anzeige (Razor CE: Client.UpdateTitleBar +
// ClassicUO.UpdateTitleBar). Ersetzt Tokens im Format-String (TitleBarText)
// durch Spielerwerte und aktive Counter und schreibt das Ergebnis ueber die
// ABI (ClientProxy.SetWindowTitle) in den Fenstertitel.
//
// Unterschiede zu CE:
//  * Nur EIN Titel-Setter (SetWindowTitle) statt der OSI/CUO-Trennung.
//  * OSI-only-Tokens (Buffs, Damage-/Gold-Tracker, Resistenzen, Luck, AR,
//    Tithe) hat der Port nicht — sie werden zu "-" bzw. leer ersetzt, damit
//    ein Format-String nie rohe {tokens} stehen laesst.
//  * Gedrosselt auf 250 ms (wie CEs TitleBarThrottle), getrieben von OnTick.

using System;
using System.Text;

namespace Assistant.Core
{
    public static class TitleBar
    {
        private static DateTime _lastUpdate = DateTime.MinValue;
        private static string _lastTitle;

        /// <summary>Von RazorPlugin.OnTick aufgerufen; drosselt selbst auf 250 ms.</summary>
        public static void OnTick()
        {
            if (!Config.GetBool("TitleBarDisplay") || World.Player == null)
                return;

            if ((DateTime.UtcNow - _lastUpdate).TotalMilliseconds < 250)
                return;

            _lastUpdate = DateTime.UtcNow;
            Update();
        }

        public static void Update()
        {
            if (World.Player == null)
                return;

            string title = Build(Config.GetString("TitleBarText") ?? string.Empty);

            // Nur bei Aenderung ueber die ABI schreiben (spart Roundtrips).
            if (title == _lastTitle)
                return;

            _lastTitle = title;
            ClientProxy.SetWindowTitle(title);
        }

        private static string Build(string format)
        {
            PlayerData p = World.Player;
            var sb = new StringBuilder(format);

            sb.Replace("{char}", p.Name ?? string.Empty);
            sb.Replace("{shard}", World.ShardName);

            sb.Replace("{hp}", p.Hits.ToString());
            sb.Replace("{hpmax}", p.HitsMax.ToString());
            sb.Replace("{mana}", p.Mana.ToString());
            sb.Replace("{manamax}", p.ManaMax.ToString());
            sb.Replace("{stam}", p.Stam.ToString());
            sb.Replace("{stammax}", p.StamMax.ToString());

            sb.Replace("{str}", p.Str.ToString());
            sb.Replace("{dex}", p.Dex.ToString());
            sb.Replace("{int}", p.Int.ToString());

            sb.Replace("{weight}", p.Weight.ToString());
            sb.Replace("{maxweight}", p.MaxWeight.ToString());
            sb.Replace("{gold}", p.Gold.ToString());
            sb.Replace("{followers}", p.Followers.ToString());
            sb.Replace("{followersmax}", p.FollowersMax.ToString());

            sb.Replace("{bandage}", BandageTimer.Running ? BandageTimer.RemainingSeconds.ToString() : "-");

            // Aktive Counter: Token = ihr Format-Name (z. B. {gold}, {bandage}
            // wenn als Counter angelegt). Nach den Standard-Tokens, damit ein
            // Counter denselben Namen ueberschreiben kann (CE-Reihenfolge).
            foreach (Counter c in Counter.List)
            {
                if (c.Enabled && !string.IsNullOrEmpty(c.Format))
                    sb.Replace("{" + c.Format + "}", c.Amount.ToString());
            }

            // Vom Port (noch) nicht gefuehrte OSI-Tokens neutralisieren.
            foreach (string token in OsiOnlyTokens)
                sb.Replace(token, "-");

            return sb.ToString();
        }

        // OSI-/CE-Tokens ohne Datenquelle im Port — auf "-" statt roh stehen lassen.
        private static readonly string[] OsiOnlyTokens =
        {
            "{ar}", "{physresist}", "{fireresist}", "{coldresist}", "{poisonresist}",
            "{energyresist}", "{luck}", "{tithe}", "{damage}", "{crimtime}",
            "{gps}", "{gpm}", "{gph}", "{goldtotal}", "{goldtotalmin}",
            "{dps}", "{maxdps}", "{maxdamagedealt}", "{maxdamagetaken}",
            "{totaldamagedealt}", "{totaldamagetaken}", "{skill}", "{gate}",
            "{stealthsteps}", "{uptime}", "{buffsdebuffs}",
            "{statbar}", "{mediumstatbar}", "{largestatbar}"
        };
    }
}
