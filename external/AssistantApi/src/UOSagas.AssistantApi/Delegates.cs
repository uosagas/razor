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
    // All cross-boundary calls use Cdecl and blittable/IntPtr parameters only.
    // Strings travel as UTF-8/ANSI pointers, buffers as IntPtr + length.
    // Layout and signatures follow the upstream ClassicUO plugin host
    // (PluginHost.cs / ClassicUO.Bootstrap) so proven semantics carry over.

    /// <summary>Entry point exported by the native client library ("Initialize").</summary>
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public unsafe delegate void ClientInitialize(IntPtr* argv, int argc, IntPtr hostBindings);

    // ---- Host callbacks (client → assistant host), slots in HostBindings ----

    /// <summary>Client hands over its ClientBindings struct once during startup.</summary>
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void OnBindClientFunctions(IntPtr clientBindings);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void OnLoadPlugin(IntPtr pluginPathPtr, uint clientVersion, IntPtr assetsPathPtr, IntPtr sdlWindow);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void OnTick();

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void OnClosing();

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void OnFocusWindow();

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void OnConnection();

    /// <summary>Return false to swallow the packet. data points at the packet bytes, length is in/out.</summary>
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate bool OnPacketInOut(IntPtr data, ref int length);

    /// <summary>Return false to swallow the hotkey.</summary>
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate bool OnHotkey(int key, int mod, bool pressed);

    /// <summary>
    /// Mouse event for extra buttons and wheel. Low word of <c>button</c> is the raw SDL
    /// button number (left/right never arrive — the client filters them); the high word
    /// carries the SDL KMOD state of the event (Sagas extension, ABI v3 compatible —
    /// older clients send 0 there). <c>wheel</c> != 0 for scrolling.
    /// </summary>
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void OnMouse(int button, int wheel);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate bool OnUpdatePlayerPosition(int x, int y, int z);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate int OnSdlEvent(IntPtr ev);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void OnCommandList(out IntPtr list, out int len);

    // ---- Client services (assistant host → client), slots in ClientBindings ----

    /// <summary>PluginRecvFn: inject into client. PluginSendFn: send to server. Both filtered client-side.</summary>
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate bool PacketRecvSend(IntPtr data, ref int length);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate short GetPacketLength(int id);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void CastSpell(int index);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void SetWindowTitle(IntPtr textPtr);

    /// <summary>Returns a client-allocated ANSI string pointer (free via FreeBuffer) or IntPtr.Zero.</summary>
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate IntPtr GetCliloc(int cliloc, IntPtr argsPtr, bool capitalize);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate bool RequestMove(int dir, bool run);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate bool GetPlayerPosition(out int x, out int y, out int z);

    /// <summary>Generic extensible command channel (pathfind, abilities, ...); cmdPtr points at a command struct.</summary>
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate IntPtr Command(IntPtr cmdPtr);

    // ---- UOSagas data service (assistant host → client) ----
    // The shard's data files are encrypted; assistants never read mul/uop files.
    // All game data comes from the client's already-decrypted loaders through these calls.

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate bool GetTileData(int index, ref ulong flags, ref ushort textId, IntPtr nameBuffer, int nameBufferLength);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate bool GetStaticData(
        int index,
        ref ulong flags,
        ref byte weight,
        ref byte layer,
        ref int count,
        ref ushort animId,
        ref byte height,
        IntPtr nameBuffer,
        int nameBufferLength);

    /// <summary>Returns BGRA32 pixels for an art graphic. Buffer is client-allocated; free via FreeBuffer.</summary>
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate bool GetStaticImage(ushort graphic, out IntPtr pixels, out int width, out int height);

    /// <summary>Frees a buffer previously returned by a client service (GetCliloc, GetStaticImage, ...).</summary>
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void FreeBuffer(IntPtr ptr);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void SetFilter(int filterType, bool enabled);

    /// <summary>
    /// Resolves a UO hue (1-based) to a representative packed ARGB colour
    /// (0xAARRGGBB) from the client's decrypted hue table. Returns 0 for hue 0
    /// (uncoloured). Lets assistants show true hue swatches without the mul files.
    /// </summary>
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate uint GetHueColor(int hue);

    /// <summary>
    /// Submits a crash/error report envelope (UTF-8 JSON: payloadJson +
    /// base64 attachments, Abi.Version 3). The client owns the reporting
    /// endpoint (Discord webhook URL never leaves the client), validates,
    /// rate-limits and posts the report. Blocking (network); returns true
    /// when the report was delivered.
    /// </summary>
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate bool SubmitCrashReport(IntPtr utf8Json, int length);
}
