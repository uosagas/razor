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
    /// Managed plugin contract. Plugins (e.g. UOSagas-Razor) live in the same process
    /// as the bootstrap host, so they talk to the client through a normal .NET interface
    /// rather than function pointers — the pointer boundary is only bootstrap ↔ native client.
    ///
    /// A plugin assembly exposes exactly one public type implementing this interface;
    /// the bootstrap instantiates it via its parameterless constructor.
    /// </summary>
    public interface IAssistantPlugin
    {
        /// <summary>Human-readable plugin name (shown in logs / UI).</summary>
        string Name { get; }

        /// <summary>
        /// Called once after the client handed over its services. Use it to cache
        /// <paramref name="client"/> and wire up event handlers.
        /// </summary>
        void Initialize(IClientServices client);

        /// <summary>Client connected to a game server.</summary>
        void OnConnected();

        /// <summary>Client disconnected from the game server.</summary>
        void OnDisconnected();

        /// <summary>Per-frame tick on the game thread. Keep it short.</summary>
        void OnTick();

        /// <summary>
        /// A packet was mirrored. <paramref name="fromServer"/> = incoming (server→client),
        /// otherwise outgoing (client→server). Return false to swallow it.
        /// </summary>
        bool OnPacket(bool fromServer, byte[] data);

        /// <summary>A hotkey fired. Return false to swallow it.</summary>
        bool OnHotkey(int key, int modifier, bool pressed);

        /// <summary>
        /// Mouse event: low word of button = raw SDL button number (> 0 for extra buttons,
        /// left/right are filtered by the client), high word = SDL KMOD state of the event
        /// (Sagas extension, 0 on older clients); wheel != 0 for scrolling.
        /// </summary>
        void OnMouse(int button, int wheel);

        /// <summary>The player moved (server-confirmed position).</summary>
        void OnPlayerPositionChanged(int x, int y, int z);

        /// <summary>Client is shutting down. Flush/save here.</summary>
        void OnShutdown();
    }
}
