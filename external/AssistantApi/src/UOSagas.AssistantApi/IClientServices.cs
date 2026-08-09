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

namespace UOSagas.AssistantApi
{
    /// <summary>
    /// Managed façade over the client's <see cref="ClientBindings"/> function table.
    /// Plugins call these instead of touching raw pointers. Every call is validated
    /// inside the client (packet filters, item restrictions) — a granted capability is
    /// not a guarantee the action goes through.
    /// </summary>
    public interface IClientServices
    {
        /// <summary>Capabilities the client granted this session.</summary>
        AssistantCapabilities Capabilities { get; }

        /// <summary>True if the capability is granted (and its service slot is live).</summary>
        bool Has(AssistantCapabilities capability);

        /// <summary>Send a raw packet to the game server (filtered client-side). Returns false if blocked.</summary>
        bool SendToServer(byte[] packet);

        /// <summary>Inject a raw packet into the client's incoming stream (filtered client-side). Returns false if blocked.</summary>
        bool InjectToClient(byte[] packet);

        /// <summary>Authoritative packet length for an id (includes shard-custom packets); -1 = variable.</summary>
        short GetPacketLength(int id);

        /// <summary>Cast a spell by index.</summary>
        void CastSpell(int index);

        /// <summary>Request a single sanctioned move; runs through the client's own walker.</summary>
        bool RequestMove(int direction, bool run);

        /// <summary>Current player position; false if not in game.</summary>
        bool TryGetPlayerPosition(out int x, out int y, out int z);

        /// <summary>Set the game window title.</summary>
        void SetWindowTitle(string title);

        /// <summary>Toggle a client-side sound/music filter.</summary>
        void SetFilter(int filterType, bool enabled);

        /// <summary>Real UO hue (1-based) → packed ARGB from the client's hue table; 0 for hue 0.</summary>
        uint GetHueColor(int hue);

        /// <summary>
        /// Submit a crash/error report envelope (UTF-8 JSON: {"payloadJson":..., "attachments":
        /// [{"name":..., "contentBase64":...}]}). The client owns the reporting endpoint and
        /// posts the report (rate-limited). Blocking; true when delivered. Abi.Version 3.
        /// </summary>
        bool SubmitCrashReport(string envelopeJson);

        // --- data service (encrypted shard files never leave the client) ---

        /// <summary>Look up a cliloc string.</summary>
        string GetCliloc(int cliloc, string args = "", bool capitalize = false);

        /// <summary>Land tiledata; false if index out of range.</summary>
        bool TryGetLandTileData(int index, out ulong flags, out ushort textId, out string name);

        /// <summary>Static tiledata; false if index out of range.</summary>
        bool TryGetStaticTileData(int index, out StaticTileInfo info);

        /// <summary>Static art as BGRA32 pixels; false if not found.</summary>
        bool TryGetStaticArt(ushort graphic, out uint[] pixels, out int width, out int height);
    }

    /// <summary>Decoded static tiledata returned by <see cref="IClientServices.TryGetStaticTileData"/>.</summary>
    public struct StaticTileInfo
    {
        public ulong Flags;
        public byte Weight;
        public byte Layer;
        public int Count;
        public ushort AnimId;
        public byte Height;
        public string Name;
    }
}
