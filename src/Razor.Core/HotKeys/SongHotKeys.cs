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

// UOSagas-Razor: Bard-Song-Hotkeys (Sagas-Zusatz, kein CE-Bestand).
//
// Die zehn Bard-Songs des Shards sind serverseitig normale Spells mit den
// IDs 701-710 (Cast-Paket wie jeder andere Zauber); der eingebaute Assistant
// fuehrt sie als eigene Hotkey-Kategorie "Songs". Razor CEs Spell-Tabelle
// belegt 701-710 mit den ersten zehn Masteries ("Inspire" usw.) — die
// Tabelle bleibt deshalb unangetastet, die Songs registrieren sich hier als
// eigene Kategorie unter ihren Sagas-Namen. String-benannte Hotkeys
// serialisieren im Profil ueber den englischen Namen, nicht ueber die
// Kategorie — die neue Kategorie ist damit profil-kompatibel.

namespace Assistant.HotKeys
{
    public class SongHotKeys
    {
        public sealed class Song
        {
            public readonly int SpellId;
            public readonly string Name;

            public Song(int spellId, string name)
            {
                SpellId = spellId;
                Name = name;
            }
        }

        /// <summary>Die zehn Sagas-Bard-Songs (Spell-IDs 701-710 in Server-Reihenfolge; das Songbook zeigt sie umsortiert).</summary>
        public static readonly Song[] Songs =
        {
            new Song(701, "Song of Provocation"),
            new Song(702, "Song of Peacemaking"),
            new Song(703, "Song of Discordance"),
            new Song(704, "Song of Healing"),
            new Song(705, "Song of Fortune"),
            new Song(706, "Song of Light"),
            new Song(707, "Song of Vigor"),
            new Song(708, "Song of Clarity"),
            new Song(709, "Song of Warfare"),
            new Song(710, "Song of Sorcery")
        };

        /// <summary>True, wenn die Spell-ID auf dem Shard ein Bard-Song ist (die CE-Tabelle fuehrt sie als Masteries).</summary>
        public static bool IsSong(int spellId)
        {
            foreach (Song song in Songs)
            {
                if (song.SpellId == spellId)
                    return true;
            }

            return false;
        }

        /// <summary>Anzahl der registrierten Song-Hotkeys (Testverankerung).</summary>
        public static int Count
        {
            get { return Songs.Length; }
        }

        private static HotKeyCallbackState _callback;

        public static void Initialize()
        {
            _callback = new HotKeyCallbackState(OnHotKey);

            foreach (Song song in Songs)
                HotKey.Add(HKCategory.Songs, HKSubCat.None, song.Name, _callback, song.SpellId);
        }

        private static void OnHotKey(ref object state)
        {
            // Wie der eingebaute Assistant: direkt GameActions.CastSpell(id)
            // ueber die ABI (kein Spell.OnCast — die Tabelle kennt 701-710
            // nur als Masteries).
            ClientProxy.CastSpell((int) state);
        }
    }
}
