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

using System;

namespace UOSagas.AssistantApi
{
    /// <summary>
    /// Capability flags the client grants to the attached assistant host.
    /// The client only fills the ClientBindings function slots covered by these flags;
    /// everything else stays IntPtr.Zero. The shard keeps full control.
    /// </summary>
    [Flags]
    public enum AssistantCapabilities : ulong
    {
        None = 0,

        /// <summary>Mirror of incoming/outgoing packets (PacketIn/PacketOut host callbacks).</summary>
        PacketMirror = 1 << 0,

        /// <summary>Send packets to the server (PluginSendFn) — subject to client-side filters.</summary>
        PacketSend = 1 << 1,

        /// <summary>Inject packets into the client (PluginRecvFn) — subject to client-side filters.</summary>
        PacketInject = 1 << 2,

        /// <summary>Sanctioned movement (RequestMoveFn).</summary>
        Movement = 1 << 3,

        /// <summary>Cast spells (CastSpellFn).</summary>
        CastSpell = 1 << 4,

        /// <summary>Game data lookups: cliloc, tiledata, static data, art (data service slots).</summary>
        DataService = 1 << 5,

        /// <summary>Client-side sound/music filter toggles (SetFilterFn).</summary>
        Filters = 1 << 6,

        /// <summary>Window title (SetWindowTitleFn).</summary>
        SetTitle = 1 << 7,

        /// <summary>Extended commands via CommandFn (pathfinding, abilities, ...).</summary>
        Commands = 1 << 8,

        /// <summary>Crash/error report relay (SubmitCrashReportFn) — the client owns the
        /// reporting endpoint (Discord webhook) and posts on the assistant's behalf.</summary>
        CrashReport = 1 << 9,
    }
}
