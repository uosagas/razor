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
using System.Runtime.InteropServices;

namespace UOSagas.AssistantApi
{
    /// <summary>
    /// Function table the client hands back to the assistant host (via HostBindings.InitializeFn).
    /// Field order mirrors the upstream ClassicUO ClientBindings; UOSagas fields are appended.
    /// The client only fills slots covered by the granted <see cref="AssistantCapabilities"/> —
    /// enforcement (packet filters, item restrictions) happens inside these implementations.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct ClientBindings
    {
        // -- upstream-compatible block (never reorder) --
        public IntPtr PluginRecvFn;       // PacketRecvSend — inject into client (filtered)
        public IntPtr PluginSendFn;       // PacketRecvSend — send to server (filtered)
        public IntPtr PacketLengthFn;     // GetPacketLength (authoritative, incl. shard-custom packets)
        public IntPtr CastSpellFn;        // CastSpell
        public IntPtr SetWindowTitleFn;   // SetWindowTitle
        public IntPtr GetClilocFn;        // GetCliloc
        public IntPtr RequestMoveFn;      // RequestMove (runs through the client's walker)
        public IntPtr GetPlayerPositionFn;// GetPlayerPosition
        public IntPtr CommandFn;          // Command (upstream: ReflectionCmdFn)

        // -- UOSagas extension block (append only) --
        /// <summary>Abi.Version the client implements.</summary>
        public int ClientAbiVersion;

        /// <summary>Granted <see cref="AssistantCapabilities"/> — tells the host which slots are live.</summary>
        public ulong Capabilities;

        // Data service (encrypted shard files never leave the client; lookups go through here)
        public IntPtr GetTileDataFn;      // GetTileData
        public IntPtr GetStaticDataFn;    // GetStaticData
        public IntPtr GetStaticImageFn;   // GetStaticImage
        public IntPtr FreeBufferFn;       // FreeBuffer

        public IntPtr SetFilterFn;        // SetFilter (sound/music filter toggles)

        public IntPtr GetHueColorFn;      // GetHueColor (real hue → ARGB, Abi.Version 2)

        public IntPtr SubmitCrashReportFn; // SubmitCrashReport (client-owned webhook relay, Abi.Version 3; was Reserved1)

        // Reserved for future extension (always zero until assigned; bump Abi.Version when used)
        public IntPtr Reserved2;
        public IntPtr Reserved3;
    }
}
