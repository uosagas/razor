#region license
// Razor: An Ultima Online Assistant
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

// Portiert aus Razor CE (Razor/Agents/Agents.cs) — Phase 2d.
// ENTFERNT gegenueber Razor CE (dokumentiert):
//  * WinForms-UI (Redraw/Select/OnSelected/OnButtonPress, ListBox/Button) —
//    die Agent-AKTIONEN bleiben als oeffentliche Methoden der Subklassen
//    erhalten (Organize(), Restock(), SetHotBag(), UseNextItem(), ...).
// NEU gegenueber Razor CE:
//  * Initialize() erzeugt alle Agents (Razor CE macht das per Reflection ueber
//    alle statischen Initialize-Methoden der Assembly) und registriert die
//    "agents"-Profilsektion ueber ProfileSections (statt Festverdrahtung in
//    UI/Config.cs). Save/Load sind byte-kompatibel zum CE-Format:
//    <agents><UseOnce>...</UseOnce><Sell>...</Sell>...</agents>
//    (Elementnamen = englische CE-Language-Strings der Agent-Namen).

using System.Collections.Generic;
using System.Xml;

namespace Assistant.Agents
{
    public abstract class Agent
    {
        public static List<Agent> List { get; } = new List<Agent>();

        public delegate void ItemCreatedEventHandler(Item item);

        public delegate void MobileCreatedEventHandler(Mobile m);

        public static event ItemCreatedEventHandler OnItemCreated;
        public static event MobileCreatedEventHandler OnMobileCreated;

        private static bool m_Initialized;

        /// <summary>
        /// Erzeugt alle Agents (einmalig) und registriert die "agents"-
        /// Profilsektion (idempotent; Register ersetzt eine bestehende
        /// Registrierung). Reihenfolge = Schreibreihenfolge im Profil.
        /// </summary>
        public static void Initialize()
        {
            if (!m_Initialized)
            {
                m_Initialized = true;

                UseOnceAgent.Initialize();
                SellAgent.Initialize();
                ScavengerAgent.Initialize();
                OrganizerAgent.Initialize();
                SearchExemptionAgent.Initialize();
                BuyAgent.Initialize();
                RestockAgent.Initialize();
                IgnoreAgent.Initialize();
            }

            ProfileSections.Register("agents", LoadProfile, SaveProfile, ClearAll);
        }

        public static void InvokeMobileCreated(Mobile m)
        {
            if (OnMobileCreated != null)
            {
                OnMobileCreated(m);
            }
        }

        public static void InvokeItemCreated(Item i)
        {
            if (OnItemCreated != null)
            {
                OnItemCreated(i);
            }
        }

        public static void Add(Agent a)
        {
            List.Add(a);
        }

        public static void ClearAll()
        {
            for (int i = 0; i < List.Count; i++)
            {
                ((Agent) List[i]).Clear();
            }
        }

        public static void SaveProfile(XmlWriter xml)
        {
            foreach (Agent a in List)
            {
                xml.WriteStartElement(a.Name);
                a.Save(xml);
                xml.WriteEndElement();
            }
        }

        public static void LoadProfile(XmlElement xml)
        {
            ClearAll();

            if (xml == null)
            {
                return;
            }

            for (int i = 0; i < List.Count; i++)
            {
                try
                {
                    Agent a = (Agent) List[i];
                    XmlElement el = xml[a.Name];
                    if (el != null)
                    {
                        a.Load(el);
                    }
                }
                catch
                {
                }
            }
        }

        public override string ToString()
        {
            return !string.IsNullOrEmpty(Alias) ? $"{Name} ({Alias})" : Name;
        }

        public abstract string Name { get; }
        public abstract string Alias { get; set; }
        public abstract int Number { get; }
        public abstract void Save(XmlWriter xml);
        public abstract void Load(XmlElement node);
        public abstract void Clear();
    }
}
