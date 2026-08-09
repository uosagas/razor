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

// UOSagas-Razor: zentrale Client-Zugriffsschicht (Phase 2c).
//
// Ersetzt Razor CEs Client.Instance (Razor/Client.cs): alle Perform()-
// Implementierungen, Targeting, DragDropManager usw. reden AUSSCHLIESSLICH
// ueber diese statische Klasse mit dem UOSagas-Client. RazorPlugin.Initialize
// befuellt sie mit dem IClientServices des Bootstrap-Hosts.
//
// WICHTIG: Dies ist die EINZIGE Stelle in Razor.Core, die
// UOSagas.AssistantApi referenzieren darf.

using System;
using UOSagas.AssistantApi;

namespace Assistant
{
    public static class ClientProxy
    {
        private static IClientServices m_Services;

        /// <summary>Wird von RazorPlugin.Initialize aufgerufen.</summary>
        public static void Bind(IClientServices services)
        {
            m_Services = services;
        }

        public static void Unbind()
        {
            m_Services = null;
        }

        public static bool IsBound
        {
            get { return m_Services != null; }
        }

        /// <summary>Client bietet den Crash-Report-Relay an (ABI v3 + Capability).</summary>
        public static bool SupportsCrashReport
        {
            get
            {
                try
                {
                    return m_Services?.Has(UOSagas.AssistantApi.AssistantCapabilities.CrashReport) ?? false;
                }
                catch
                {
                    // Alter Host (ABI v2): die AssistantApi.dll des Hosts kennt
                    // die Methode/Capability noch nicht — dann kein Relay.
                    return false;
                }
            }
        }

        /// <summary>Crash-/Error-Report-Envelope an den Client geben (der ruft den
        /// Webhook; blockierend). true = zugestellt.</summary>
        public static bool SubmitCrashReport(string envelopeJson)
        {
            if (m_Services == null || string.IsNullOrEmpty(envelopeJson))
                return false;

            try
            {
                return m_Services.SubmitCrashReport(envelopeJson);
            }
            catch (Exception e)
            {
                Console.WriteLine($"[Razor] SubmitCrashReport failed: {e.Message}");
                return false;
            }
        }

        /// <summary>Razor CE: Client.Instance.SendToServer(Packet).</summary>
        public static bool SendToServer(Packet p)
        {
            if (m_Services == null || p == null)
                return false;

            try
            {
                return m_Services.SendToServer(p.Compile());
            }
            catch (Exception e)
            {
                Console.WriteLine($"[Razor] SendToServer failed (0x{p.PacketID:X2}): {e.Message}");
                return false;
            }
        }

        /// <summary>Razor CE: Client.Instance.SendToClient(Packet).</summary>
        public static bool SendToClient(Packet p)
        {
            if (m_Services == null || p == null)
                return false;

            try
            {
                return m_Services.InjectToClient(p.Compile());
            }
            catch (Exception e)
            {
                Console.WriteLine($"[Razor] InjectToClient failed (0x{p.PacketID:X2}): {e.Message}");
                return false;
            }
        }

        /// <summary>
        /// Injiziert ein ROHES Paket in den Client (fuer Anzeige-Filter, die eine
        /// gepatchte Kopie des Originals senden). Injizierte Pakete laufen im
        /// Client ueber den Plugins-Puffer und NICHT erneut durch den Mirror.
        /// </summary>
        public static bool SendToClient(byte[] raw)
        {
            if (m_Services == null || raw == null || raw.Length == 0)
                return false;

            try
            {
                return m_Services.InjectToClient(raw);
            }
            catch (Exception e)
            {
                Console.WriteLine($"[Razor] InjectToClient failed (0x{raw[0]:X2}): {e.Message}");
                return false;
            }
        }

        /// <summary>Razor CE: Client.Instance.RequestMove(Direction) — laeuft ueber den Client-Walker.</summary>
        public static bool RequestMove(Direction dir, bool run = false)
        {
            if (m_Services == null)
                return false;

            try
            {
                return m_Services.RequestMove((int) (dir & Direction.Mask), run);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>Spell-Cast ueber den Client (Client-eigene Cast-Pipeline).</summary>
        public static void CastSpell(int index)
        {
            try
            {
                m_Services?.CastSpell(index);
            }
            catch
            {
            }
        }

        /// <summary>Autoritative Paketlaenge (-1 = variabel); ohne Bindung -1.</summary>
        public static short GetPacketLength(int id)
        {
            try
            {
                return m_Services?.GetPacketLength(id) ?? (short) -1;
            }
            catch
            {
                return -1;
            }
        }

        public static string GetCliloc(int cliloc, string args = "", bool capitalize = false)
        {
            try
            {
                return m_Services?.GetCliloc(cliloc, args, capitalize) ?? string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        /// <summary>
        /// Echte UO-Hue-Farbe (packed 0xAARRGGBB) aus der entschluesselten
        /// Hue-Tabelle des Clients (ABI GetHueColor, Version 2). 0 = keine Farbe
        /// bzw. Service nicht gebunden (Aufrufer faellt auf eine Naeherung zurueck).
        /// </summary>
        public static uint GetHueColor(int hue)
        {
            try
            {
                return m_Services?.GetHueColor(hue) ?? 0u;
            }
            catch
            {
                return 0u;
            }
        }

        /// <summary>
        /// Statische Tiledata (Name/Layer/Flags/…) fuer ein Item-Graphic aus dem
        /// entschluesselten Client-Loader (DataService). Razor CE liest das aus
        /// tiledata.mul (ItemData); wir bekommen es ueber die ABI.
        /// </summary>
        public static bool TryGetStaticTileData(int graphic, out StaticTileInfo info)
        {
            info = default;
            if (m_Services == null)
                return false;

            try
            {
                return m_Services.TryGetStaticTileData(graphic, out info);
            }
            catch
            {
                return false;
            }
        }

        public static void SetWindowTitle(string title)
        {
            try
            {
                m_Services?.SetWindowTitle(title);
            }
            catch
            {
            }
        }
    }
}
