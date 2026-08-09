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

// UOSagas-Razor: Laufzeit-Diagnose (fuer den About-Tab).
// Zeigt, ob/was das Weltmodell vom Client bekommt — hilft beim Nachweis, dass
// die Initialisierung/Paket-Weiterleitung korrekt ist (Backpack, Item-/Mobile-
// Zaehler, gesehene Paket-Typen). Vorbild: Razor Enhanced „Info"-Anzeigen.

using System.Collections.Generic;
using System.Linq;

namespace Assistant
{
    public static class Diagnostics
    {
        private static readonly object m_Lock = new object();

        // Pro Paket-ID: Anzahl + Richtung (server->client / client->server).
        private static readonly Dictionary<int, long> m_RecvCounts = new();
        private static readonly Dictionary<int, long> m_SendCounts = new();

        public static long TotalRecv { get; private set; }
        public static long TotalSend { get; private set; }
        public static bool Connected { get; set; }

        // "Jemals gesehen" fuer Schluesselpakete (ueberlebt Reset Stats), damit die
        // einmaligen Login-Pakete nicht aus der laufenden Statistik verschwinden.
        private static readonly SortedSet<int> m_EverSeen = new();

        // Login-Equipment-Diagnose: wurde je Ausruestung/Backpack fuer den Player
        // verarbeitet? (Nachweis, dass die Equip-/Container-Pakete ankommen.)
        public static int PlayerEquipEventsSeen { get; private set; }
        public static int PlayerBackpackContentSeen { get; private set; }

        public static void NotePlayerEquip() { PlayerEquipEventsSeen++; }
        public static void NotePlayerBackpackContent(int count) { PlayerBackpackContentSeen += count; }

        public static string EverSeen()
        {
            lock (m_Lock)
                return string.Join(" ", m_EverSeen.Select(id => $"0x{id:X2}"));
        }

        public static void NotePacket(bool fromServer, int id)
        {
            lock (m_Lock)
            {
                if (fromServer)
                {
                    TotalRecv++;
                    m_EverSeen.Add(id);
                    m_RecvCounts[id] = m_RecvCounts.TryGetValue(id, out long c) ? c + 1 : 1;
                }
                else
                {
                    TotalSend++;
                    m_SendCounts[id] = m_SendCounts.TryGetValue(id, out long c) ? c + 1 : 1;
                }
            }
        }

        public static void Reset()
        {
            lock (m_Lock)
            {
                m_RecvCounts.Clear();
                m_SendCounts.Clear();
                TotalRecv = 0;
                TotalSend = 0;
            }
        }

        /// <summary>Wurde ein bestimmter Paket-Typ jemals empfangen?</summary>
        public static long RecvCount(int id)
        {
            lock (m_Lock)
                return m_RecvCounts.TryGetValue(id, out long c) ? c : 0;
        }

        /// <summary>Die N haeufigsten empfangenen Paket-Typen (id, count), absteigend.</summary>
        public static List<KeyValuePair<int, long>> TopRecv(int max)
        {
            lock (m_Lock)
                return m_RecvCounts.OrderByDescending(kv => kv.Value).Take(max).ToList();
        }

        /// <summary>Momentaufnahme des Weltzustands fuer die Anzeige.</summary>
        public static WorldStatus Snapshot()
        {
            var s = new WorldStatus();

            PlayerData p = World.Player;
            s.HasPlayer = p != null;
            s.Connected = Connected;

            if (p != null)
            {
                s.PlayerName = string.IsNullOrEmpty(p.Name) ? "(unbekannt)" : p.Name;
                s.PlayerSerial = p.Serial.Value;
                s.Position = $"{p.Position.X}, {p.Position.Y}, {p.Position.Z}";
                s.Body = p.Body;

                Item bp = p.Backpack;
                s.HasBackpack = bp != null;
                if (bp != null)
                {
                    s.BackpackSerial = bp.Serial.Value;
                    s.BackpackItemCount = bp.Contains.Count;
                }

                // Getragene Ausruestung (direkte Kinder des Player-Mobiles).
                s.EquippedCount = p.Contains.Count;
            }

            s.WorldItems = World.Items.Count;
            s.WorldMobiles = World.Mobiles.Count;
            s.TotalRecv = TotalRecv;
            s.TotalSend = TotalSend;
            s.ShardName = World.ShardName;
            s.EverSeen = EverSeen();
            s.PlayerEquipEvents = PlayerEquipEventsSeen;
            s.PlayerBackpackContent = PlayerBackpackContentSeen;

            return s;
        }
    }

    public struct WorldStatus
    {
        public bool Connected;
        public bool HasPlayer;
        public string PlayerName;
        public uint PlayerSerial;
        public string Position;
        public ushort Body;
        public bool HasBackpack;
        public uint BackpackSerial;
        public int BackpackItemCount;
        public int EquippedCount;
        public int WorldItems;
        public int WorldMobiles;
        public long TotalRecv;
        public long TotalSend;
        public string ShardName;
        public string EverSeen;
        public int PlayerEquipEvents;
        public int PlayerBackpackContent;
    }
}
