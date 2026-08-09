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

// UOSagas-Razor: Bandage-Tracking + Anzeige (Razor CE Core/BandageTimer.cs).
//
// Start bei "You begin applying the bandages." (Text 0x1C/0xAE oder Cliloc
// 500956 via 0xC1), Stop bei den CE-End-Meldungen (Cliloc-Nummern) bzw. den
// bekannten End-Texten; der Server (SagasGameServer Bandage.cs) sendet genau
// diese Clilocs. Sicherheitsnetz wie CE: nach 30s bzw. als Geist stoppt der
// Timer ohne End-Meldung.
//
// Anzeige (Feature-Paritaet 2026-07-17, CE 1:1): ShowBandageTimer zeigt pro
// Sekunde das Format ("Bandage: {count}s") als Overhead (Location 0) oder
// Systemmeldung (Location 1) im ShowBandageTimerHue; OnlyShowBandageTimerEvery
// drosselt auf jede X. Sekunde. ShowBandageStart/-End melden Beginn/Ende.
//
// Speist ausserdem die Outlands-Script-Expression `bandaging`.

using System;

namespace Assistant.Core
{
    public static class BandageTimer
    {
        // Geschaetzte Verbandsdauer fuer die Outlands-Expression `bandaging`
        // (Sekunden bis fertig): Standard-UO self-bandage liegt je nach Dex bei
        // ~4-8s; wir nehmen 8s und stoppen frueher, sobald die End-Meldung kommt.
        private const int EstimatedDurationSeconds = 8;

        private static int _count;
        private static readonly Timer m_Timer = new InternalTimer();

        public static int Count => _count;

        public static bool Running => m_Timer.Running;

        /// <summary>
        /// Outlands-Expression `bandaging`: Sekunden bis der Verband fertig ist
        /// (0 = kein Verband aktiv/bereit). Naeherung ueber EstimatedDuration.
        /// </summary>
        public static int RemainingSeconds
        {
            get
            {
                if (!Running)
                    return 0;

                int remaining = EstimatedDurationSeconds - _count;
                return remaining <= 0 ? 0 : remaining;
            }
        }

        public static void Start()
        {
            _count = 0;

            if (m_Timer.Running)
                m_Timer.Stop();

            m_Timer.Start();
        }

        public static void Stop()
        {
            m_Timer.Stop();
        }

        private static void OnBandageStarted()
        {
            Start();

            if (Config.GetBool("ShowBandageTimer") && Config.GetBool("ShowBandageStart"))
                ShowBandagingStatusMessage(Config.GetString("BandageStartMessage"));
        }

        private static void OnBandageEnded()
        {
            Stop();

            if (Config.GetBool("ShowBandageTimer") && Config.GetBool("ShowBandageEnd"))
                ShowBandagingStatusMessage(Config.GetString("BandageEndMessage"));
        }

        /// <summary>Text-Meldungen (0x1C/0xAE) — CE: OnSystemMessage.</summary>
        public static void OnSystemMessage(string msg)
        {
            if (string.IsNullOrEmpty(msg))
                return;

            if (msg == "You begin applying the bandages.")
            {
                OnBandageStarted();
                return;
            }

            if (Running &&
                (msg == "You heal what little damage you had." ||
                 msg == "You heal what little damage the patient had." ||
                 msg == "You did not stay close enough to heal your target."))
            {
                OnBandageEnded();
            }
        }

        /// <summary>Cliloc-Meldungen (0xC1) — CE: OnLocalizedMessage.</summary>
        public static void OnLocalizedMessage(int num)
        {
            if (num == 500956) // "You begin applying the bandages."
            {
                OnBandageStarted();
                return;
            }

            if (!Running)
                return;

            // CE ClilocNums: Heil-Ergebnisse/Abbrueche beenden den Timer.
            if (num == 500955 || (num >= 500962 && num <= 500969) ||
                (num >= 503252 && num <= 503261) ||
                num == 1010058 || num == 1010648 || num == 1010650 ||
                num == 1060088 || num == 1060167)
            {
                OnBandageEnded();
            }
        }

        private static void ShowBandagingStatusMessage(string msg)
        {
            if (World.Player == null)
                return;

            if (Config.GetInt("ShowBandageTimerLocation") == 0)
                World.Player.OverheadMessage(Config.GetInt("ShowBandageTimerHue"), msg);
            else
                World.Player.SendMessage(Config.GetInt("ShowBandageTimerHue"), msg);
        }

        private class InternalTimer : Timer
        {
            public InternalTimer() : base(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1))
            {
            }

            protected override void OnTick()
            {
                // CE: als Geist sofort stoppen (ohne End-Meldung).
                if (World.Player != null && World.Player.IsGhost)
                {
                    BandageTimer.Stop();
                    return;
                }

                _count++;

                if (Config.GetBool("ShowBandageTimer"))
                {
                    bool showMessage = !(Config.GetBool("OnlyShowBandageTimerEvery") &&
                                         Config.GetInt("OnlyShowBandageTimerSeconds") > 0 &&
                                         _count % Config.GetInt("OnlyShowBandageTimerSeconds") != 0);

                    if (showMessage)
                        ShowBandagingStatusMessage(Config.GetString("ShowBandageTimerFormat")
                            .Replace("{count}", _count.ToString()));
                }

                // CE: Sicherheitsnetz — nach 30s ohne End-Meldung aufgeben.
                if (_count > 30)
                    Stop();
            }
        }
    }
}
