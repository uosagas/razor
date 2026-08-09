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
    /// Function table the assistant host passes to the native client's Initialize export.
    /// Field order mirrors the upstream ClassicUO HostBindings; UOSagas fields are appended.
    /// Unused slots stay IntPtr.Zero — the client treats them as "not interested".
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct HostBindings
    {
        // -- upstream-compatible block (never reorder) --
        public IntPtr InitializeFn;      // OnBindClientFunctions
        public IntPtr LoadPluginFn;      // OnLoadPlugin
        public IntPtr TickFn;            // OnTick
        public IntPtr ClosingFn;         // OnClosing
        public IntPtr FocusGainedFn;     // OnFocusWindow
        public IntPtr FocusLostFn;       // OnFocusWindow
        public IntPtr ConnectedFn;       // OnConnection
        public IntPtr DisconnectedFn;    // OnConnection
        public IntPtr HotkeyFn;          // OnHotkey
        public IntPtr MouseFn;           // OnMouse
        public IntPtr CmdListFn;         // OnCommandList
        public IntPtr SdlEventFn;        // OnSdlEvent
        public IntPtr UpdatePlayerPosFn; // OnUpdatePlayerPosition
        public IntPtr PacketInFn;        // OnPacketInOut (server → client mirror)
        public IntPtr PacketOutFn;       // OnPacketInOut (client → server mirror)

        // -- UOSagas extension block (append only) --
        /// <summary>Abi.Version the host was compiled against; the client refuses newer-than-known versions.</summary>
        public int HostAbiVersion;
    }
}
