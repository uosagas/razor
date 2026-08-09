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

// UOSagas-Razor: Registrierungsmechanismus fuer Profil-Sektionen.
//
// Razor CE (Razor/UI/Config.cs) ruft beim Laden/Speichern der Profil-XML fest
// verdrahtete Manager auf (Filter.Load/Save, Agent.LoadProfile/SaveProfile,
// HotKey.Load/Save, ...). Der Port entkoppelt das ueber Delegates: Manager
// registrieren sich hier mit Sektionsnamen + Load/Save(+MakeDefault)-Callbacks.
//
// Sektionen OHNE registrierten Handler werden beim Laden roh konserviert und
// beim Speichern unveraendert zurueckgeschrieben (Format-Fidelity: alte
// Razor-Profile verlieren keine agents/hotkeys/filters/... Daten, auch wenn
// der zugehoerige Manager noch nicht portiert ist).

using System;
using System.Collections.Generic;
using System.Xml;

namespace Assistant
{
    public sealed class ProfileSectionHandler
    {
        public string Name { get; }

        /// <summary>Wird beim Profil-Laden aufgerufen; das Element kann null sein (Sektion fehlt in der Datei).</summary>
        public Action<XmlElement> Load { get; }

        /// <summary>Schreibt den Sektionsinhalt (innerhalb des bereits geoeffneten Sektionselements).</summary>
        public Action<XmlWriter> Save { get; }

        /// <summary>Optional: Zustand zuruecksetzen, wenn Profile.MakeDefault() laeuft (ersetzt die ClearAll()-Aufrufe des Originals).</summary>
        public Action MakeDefault { get; }

        public ProfileSectionHandler(string name, Action<XmlElement> load, Action<XmlWriter> save, Action makeDefault = null)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Load = load;
            Save = save;
            MakeDefault = makeDefault;
        }
    }

    public static class ProfileSections
    {
        private static readonly List<ProfileSectionHandler> m_Handlers = new List<ProfileSectionHandler>();

        public static IReadOnlyList<ProfileSectionHandler> Handlers => m_Handlers;

        /// <summary>
        /// Registriert einen Sektions-Handler. Die Registrierungsreihenfolge bestimmt
        /// die Schreibreihenfolge beim Speichern (wie die feste Reihenfolge im Original).
        /// </summary>
        public static void Register(string name, Action<XmlElement> load, Action<XmlWriter> save, Action makeDefault = null)
        {
            Unregister(name);
            m_Handlers.Add(new ProfileSectionHandler(name, load, save, makeDefault));
        }

        public static void Unregister(string name)
        {
            m_Handlers.RemoveAll(h => string.Equals(h.Name, name, StringComparison.OrdinalIgnoreCase));
        }

        public static ProfileSectionHandler Find(string name)
        {
            foreach (ProfileSectionHandler h in m_Handlers)
            {
                if (string.Equals(h.Name, name, StringComparison.OrdinalIgnoreCase))
                    return h;
            }

            return null;
        }

        public static void MakeDefaultAll()
        {
            foreach (ProfileSectionHandler h in m_Handlers)
            {
                try
                {
                    h.MakeDefault?.Invoke();
                }
                catch
                {
                }
            }
        }

        public static void Clear()
        {
            m_Handlers.Clear();
        }
    }
}
