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
using System.Text;

namespace UOSagas.AssistantApi
{
    /// <summary>
    /// Default <see cref="IClientServices"/> backed by a <see cref="ClientBindings"/> table.
    /// Marshals managed calls to the client's function pointers and frees any client-allocated
    /// buffers via FreeBuffer. Used by the bootstrap host and the razorctl test harness.
    /// </summary>
    public sealed class ClientServices : IClientServices
    {
        private readonly ClientBindings _b;

        private readonly PacketRecvSend _send;
        private readonly PacketRecvSend _inject;
        private readonly GetPacketLength _packetLength;
        private readonly CastSpell _castSpell;
        private readonly RequestMove _requestMove;
        private readonly GetPlayerPosition _getPos;
        private readonly SetWindowTitle _setTitle;
        private readonly SetFilter _setFilter;
        private readonly GetHueColor _getHueColor;
        private readonly GetCliloc _getCliloc;
        private readonly SubmitCrashReport _submitCrashReport;
        private readonly GetTileData _getTileData;
        private readonly GetStaticData _getStaticData;
        private readonly GetStaticImage _getStaticImage;
        private readonly FreeBuffer _freeBuffer;

        public ClientServices(ClientBindings bindings)
        {
            _b = bindings;

            _send = Bind<PacketRecvSend>(bindings.PluginSendFn);
            _inject = Bind<PacketRecvSend>(bindings.PluginRecvFn);
            _packetLength = Bind<GetPacketLength>(bindings.PacketLengthFn);
            _castSpell = Bind<CastSpell>(bindings.CastSpellFn);
            _requestMove = Bind<RequestMove>(bindings.RequestMoveFn);
            _getPos = Bind<GetPlayerPosition>(bindings.GetPlayerPositionFn);
            _setTitle = Bind<SetWindowTitle>(bindings.SetWindowTitleFn);
            _setFilter = Bind<SetFilter>(bindings.SetFilterFn);
            _getHueColor = Bind<GetHueColor>(bindings.GetHueColorFn);
            _getCliloc = Bind<GetCliloc>(bindings.GetClilocFn);
            _submitCrashReport = Bind<SubmitCrashReport>(bindings.SubmitCrashReportFn);
            _getTileData = Bind<GetTileData>(bindings.GetTileDataFn);
            _getStaticData = Bind<GetStaticData>(bindings.GetStaticDataFn);
            _getStaticImage = Bind<GetStaticImage>(bindings.GetStaticImageFn);
            _freeBuffer = Bind<FreeBuffer>(bindings.FreeBufferFn);
        }

        private static T Bind<T>(IntPtr ptr) where T : Delegate
            => ptr != IntPtr.Zero ? Marshal.GetDelegateForFunctionPointer<T>(ptr) : null;

        public AssistantCapabilities Capabilities => (AssistantCapabilities)_b.Capabilities;

        public bool Has(AssistantCapabilities capability) => (Capabilities & capability) == capability;

        public bool SendToServer(byte[] packet) => ForwardPacket(_send, packet);

        public bool InjectToClient(byte[] packet) => ForwardPacket(_inject, packet);

        private static bool ForwardPacket(PacketRecvSend fn, byte[] packet)
        {
            if (fn == null || packet == null || packet.Length == 0)
                return false;

            int length = packet.Length;
            var handle = GCHandle.Alloc(packet, GCHandleType.Pinned);
            try
            {
                return fn(handle.AddrOfPinnedObject(), ref length);
            }
            finally
            {
                handle.Free();
            }
        }

        public short GetPacketLength(int id) => _packetLength?.Invoke(id) ?? -1;

        public void CastSpell(int index) => _castSpell?.Invoke(index);

        public bool RequestMove(int direction, bool run) => _requestMove?.Invoke(direction, run) ?? false;

        public bool TryGetPlayerPosition(out int x, out int y, out int z)
        {
            x = y = z = 0;
            return _getPos != null && _getPos(out x, out y, out z);
        }

        public void SetWindowTitle(string title)
        {
            if (_setTitle == null)
                return;

            IntPtr ptr = Marshal.StringToHGlobalAnsi(title ?? string.Empty);
            try
            {
                _setTitle(ptr);
            }
            finally
            {
                Marshal.FreeHGlobal(ptr);
            }
        }

        public void SetFilter(int filterType, bool enabled) => _setFilter?.Invoke(filterType, enabled);

        public uint GetHueColor(int hue) => _getHueColor?.Invoke(hue) ?? 0u;

        public bool SubmitCrashReport(string envelopeJson)
        {
            if (_submitCrashReport == null || string.IsNullOrEmpty(envelopeJson))
                return false;

            byte[] utf8 = Encoding.UTF8.GetBytes(envelopeJson);
            var handle = GCHandle.Alloc(utf8, GCHandleType.Pinned);
            try
            {
                return _submitCrashReport(handle.AddrOfPinnedObject(), utf8.Length);
            }
            finally
            {
                handle.Free();
            }
        }

        public string GetCliloc(int cliloc, string args = "", bool capitalize = false)
        {
            if (_getCliloc == null)
                return string.Empty;

            IntPtr argsPtr = Marshal.StringToHGlobalAnsi(args ?? string.Empty);
            IntPtr result = IntPtr.Zero;
            try
            {
                result = _getCliloc(cliloc, argsPtr, capitalize);
                return result != IntPtr.Zero ? Marshal.PtrToStringAnsi(result) : string.Empty;
            }
            finally
            {
                Marshal.FreeHGlobal(argsPtr);
                Free(result);
            }
        }

        public bool TryGetLandTileData(int index, out ulong flags, out ushort textId, out string name)
        {
            flags = 0;
            textId = 0;
            name = string.Empty;

            if (_getTileData == null)
                return false;

            var buffer = Marshal.AllocHGlobal(NameBufferLength);
            try
            {
                if (!_getTileData(index, ref flags, ref textId, buffer, NameBufferLength))
                    return false;

                name = Marshal.PtrToStringAnsi(buffer) ?? string.Empty;
                return true;
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }

        public bool TryGetStaticTileData(int index, out StaticTileInfo info)
        {
            info = default;

            if (_getStaticData == null)
                return false;

            ulong flags = 0;
            byte weight = 0, layer = 0, height = 0;
            int count = 0;
            ushort animId = 0;

            var buffer = Marshal.AllocHGlobal(NameBufferLength);
            try
            {
                if (!_getStaticData(index, ref flags, ref weight, ref layer, ref count, ref animId, ref height, buffer, NameBufferLength))
                    return false;

                info = new StaticTileInfo
                {
                    Flags = flags,
                    Weight = weight,
                    Layer = layer,
                    Count = count,
                    AnimId = animId,
                    Height = height,
                    Name = Marshal.PtrToStringAnsi(buffer) ?? string.Empty
                };
                return true;
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }

        public bool TryGetStaticArt(ushort graphic, out uint[] pixels, out int width, out int height)
        {
            pixels = Array.Empty<uint>();
            width = 0;
            height = 0;

            if (_getStaticImage == null)
                return false;

            if (!_getStaticImage(graphic, out IntPtr ptr, out width, out height) || ptr == IntPtr.Zero || width <= 0 || height <= 0)
            {
                Free(ptr);
                return false;
            }

            try
            {
                pixels = new uint[width * height];
                Marshal.Copy(ptr, (int[])(object)pixels, 0, pixels.Length);
                return true;
            }
            finally
            {
                Free(ptr);
            }
        }

        private void Free(IntPtr clientBuffer)
        {
            if (clientBuffer != IntPtr.Zero)
                _freeBuffer?.Invoke(clientBuffer);
        }

        private const int NameBufferLength = 64;
    }
}
