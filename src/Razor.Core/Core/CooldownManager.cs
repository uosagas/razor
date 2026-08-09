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

// UOSagas-Razor: Cooldown-Datenmodell (Razor CE Core/CooldownManager.cs).
//
// Razor CE zeichnet Cooldown-Balken als Overlay ueber dem Spiel (WinForms/
// GDI). Der Port haelt die Cooldowns vorerst NUR als Daten (Script-Kommando
// `cooldown` funktioniert, abgelaufene Eintraege werden entfernt); der
// Sound wird als PlaySound in den Client injiziert.
// TODO(scripting-stub): Overlay-Anzeige (Avalonia) fuer aktive Cooldowns.
// Abweichung: Fore-/BackgroundColor als String statt System.Drawing.Color
// (kein System.Drawing im Port).

using System;
using System.Collections.Generic;
using System.Linq;

namespace Assistant
{
    public sealed class Cooldown
    {
        public string Name;
        public DateTime EndTime;
        public int Hue;
        public ushort Icon;
        public int Seconds;
        public int SoundId;
        public bool StayVisible;
        public string ForegroundColor;
        public string BackgroundColor;
    }

    public static class CooldownManager
    {
        private static readonly List<Cooldown> _cooldowns = new List<Cooldown>();

        public static IReadOnlyList<Cooldown> Cooldowns
        {
            get
            {
                _cooldowns.RemoveAll(c => !c.StayVisible && c.EndTime < DateTime.UtcNow);
                return _cooldowns;
            }
        }

        public static void AddCooldown(Cooldown cooldown)
        {
            _cooldowns.RemoveAll(c => c.Name.Equals(cooldown.Name, StringComparison.OrdinalIgnoreCase));
            _cooldowns.Add(cooldown);

            if (cooldown.SoundId > 0)
                ClientProxy.SendToClient(new PlaySound(cooldown.SoundId));
        }

        public static Cooldown Find(string name)
        {
            return Cooldowns.FirstOrDefault(c => c.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        }

        public static void Clear()
        {
            _cooldowns.Clear();
        }
    }
}
