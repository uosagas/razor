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
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using UOSagas.AssistantApi;

namespace Razor.Cli
{
    /// <summary>
    /// razorctl — minimal assistant-host harness for the UOSagas client ABI.
    /// Loads the game client as a native shared library, passes HostBindings,
    /// logs every callback and lets you poke the ClientBindings services from a console.
    ///
    /// Usage: razorctl --lib <path-to-native-client-lib> [client args...]
    /// </summary>
    internal static unsafe class Program
    {
        // Delegates must stay rooted for the lifetime of the process (the client holds raw pointers).
        private static OnBindClientFunctions _bindClient;
        private static OnTick _tick;
        private static OnClosing _closing;
        private static OnConnection _connected, _disconnected;
        private static OnPacketInOut _packetIn, _packetOut;
        private static OnHotkey _hotkey;
        private static OnMouse _mouse;
        private static OnUpdatePlayerPosition _updatePlayerPos;
        private static OnFocusWindow _focusGained, _focusLost;

        private static ClientBindings _client;
        private static bool _clientBound;
        private static long _packetsIn, _packetsOut, _ticks;
        private static volatile bool _watchPackets;

        private static int Main(string[] args)
        {
            string libPath = null;
            var clientArgs = new System.Collections.Generic.List<string>();

            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "--lib" && i + 1 < args.Length)
                    libPath = args[++i];
                else
                    clientArgs.Add(args[i]);
            }

            if (string.IsNullOrEmpty(libPath) || !File.Exists(libPath))
            {
                Console.WriteLine("Usage: razorctl --lib <path-to-native-client-lib> [client args...]");
                Console.WriteLine("The client lib is the NativeAOT build published with /p:NativeLib=Shared /p:OutputType=Library.");
                return 1;
            }

            IntPtr lib;
            try
            {
                lib = NativeLibrary.Load(Path.GetFullPath(libPath));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to load '{libPath}': {ex.Message}");
                return 2;
            }

            if (!NativeLibrary.TryGetExport(lib, Abi.ClientEntryPoint, out IntPtr initPtr))
            {
                Console.WriteLine($"Export '{Abi.ClientEntryPoint}' not found — is this the NativeLib build of the client?");
                return 3;
            }

            Log($"Loaded {libPath}, entry point '{Abi.ClientEntryPoint}' found.");

            _bindClient = BindClientFunctions;
            _tick = Tick;
            _closing = () => Log("Closing()");
            _connected = () => Log("Connected()");
            _disconnected = () => Log("Disconnected()");
            _packetIn = PacketIn;
            _packetOut = PacketOut;
            _hotkey = (key, mod, pressed) => { Log($"Hotkey(key={key}, mod={mod}, pressed={pressed})"); return true; };
            _mouse = (button, wheel) => Log($"Mouse(button={button}, wheel={wheel})");
            _updatePlayerPos = (x, y, z) => { Log($"UpdatePlayerPosition({x},{y},{z})"); return true; };
            _focusGained = () => Log("FocusGained()");
            _focusLost = () => Log("FocusLost()");

            var host = new HostBindings
            {
                HostAbiVersion = Abi.Version,
                InitializeFn = Marshal.GetFunctionPointerForDelegate(_bindClient),
                TickFn = Marshal.GetFunctionPointerForDelegate(_tick),
                ClosingFn = Marshal.GetFunctionPointerForDelegate(_closing),
                ConnectedFn = Marshal.GetFunctionPointerForDelegate(_connected),
                DisconnectedFn = Marshal.GetFunctionPointerForDelegate(_disconnected),
                PacketInFn = Marshal.GetFunctionPointerForDelegate(_packetIn),
                PacketOutFn = Marshal.GetFunctionPointerForDelegate(_packetOut),
                HotkeyFn = Marshal.GetFunctionPointerForDelegate(_hotkey),
                MouseFn = Marshal.GetFunctionPointerForDelegate(_mouse),
                UpdatePlayerPosFn = Marshal.GetFunctionPointerForDelegate(_updatePlayerPos),
                FocusGainedFn = Marshal.GetFunctionPointerForDelegate(_focusGained),
                FocusLostFn = Marshal.GetFunctionPointerForDelegate(_focusLost),
            };

            var console = new Thread(ConsoleLoop) { IsBackground = true, Name = "razorctl-console" };
            console.Start();

            var initialize = Marshal.GetDelegateForFunctionPointer<ClientInitialize>(initPtr);

            var argv = new IntPtr[clientArgs.Count];
            for (int i = 0; i < clientArgs.Count; i++)
                argv[i] = Marshal.StringToHGlobalAnsi(clientArgs[i]);

            Log("Calling Initialize — the game runs on this thread until it exits.");

            fixed (IntPtr* argvPtr = argv)
            {
                // HostBindings must stay pinned/valid during Initialize.
                IntPtr hostMem = Marshal.AllocHGlobal(Marshal.SizeOf<HostBindings>());
                Marshal.StructureToPtr(host, hostMem, false);
                initialize(argvPtr, clientArgs.Count, hostMem);
                Marshal.FreeHGlobal(hostMem);
            }

            Log($"Client exited. Totals: ticks={_ticks}, packetsIn={_packetsIn}, packetsOut={_packetsOut}");
            return 0;
        }

        private static void BindClientFunctions(IntPtr clientBindings)
        {
            _client = Marshal.PtrToStructure<ClientBindings>(clientBindings);
            _clientBound = true;

            Log($"ClientBindings received: abiVersion={_client.ClientAbiVersion}, capabilities=0x{_client.Capabilities:X}");
            Log($"  slots: send={Live(_client.PluginSendFn)} recv={Live(_client.PluginRecvFn)} pktLen={Live(_client.PacketLengthFn)} " +
                $"move={Live(_client.RequestMoveFn)} pos={Live(_client.GetPlayerPositionFn)} cliloc={Live(_client.GetClilocFn)} " +
                $"tiledata={Live(_client.GetTileDataFn)} staticImg={Live(_client.GetStaticImageFn)} filter={Live(_client.SetFilterFn)}");
        }

        private static string Live(IntPtr fn) => fn != IntPtr.Zero ? "yes" : "no";

        private static void Tick()
        {
            if (Interlocked.Increment(ref _ticks) == 1)
                Log("First Tick()");
        }

        private static bool PacketIn(IntPtr data, ref int length)
        {
            Interlocked.Increment(ref _packetsIn);
            if (_watchPackets)
                DumpPacket("IN ", data, length);
            return true;
        }

        private static bool PacketOut(IntPtr data, ref int length)
        {
            Interlocked.Increment(ref _packetsOut);
            if (_watchPackets)
                DumpPacket("OUT", data, length);
            return true;
        }

        private static void DumpPacket(string dir, IntPtr data, int length)
        {
            int count = Math.Min(length, 48);
            var sb = new StringBuilder(count * 3);
            for (int i = 0; i < count; i++)
                sb.Append(Marshal.ReadByte(data, i).ToString("X2", CultureInfo.InvariantCulture)).Append(' ');
            if (length > count)
                sb.Append("...");
            Log($"{dir} {length,4}b {sb}");
        }

        private static void ConsoleLoop()
        {
            while (true)
            {
                string line = Console.ReadLine();
                if (line == null)
                    return;

                try
                {
                    HandleCommand(line.Trim());
                }
                catch (Exception ex)
                {
                    Log($"Error: {ex.Message}");
                }
            }
        }

        private static void HandleCommand(string line)
        {
            if (line.Length == 0)
                return;

            string[] parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            switch (parts[0].ToLowerInvariant())
            {
                case "help":
                    Log("commands: stats | watch on|off | caps | pos | walk <0-7> | cliloc <id> | pktlen <id> | title <text> | tiledata <id> | staticdata <id> | art <graphic>");
                    break;

                case "stats":
                    Log($"ticks={_ticks}, packetsIn={_packetsIn}, packetsOut={_packetsOut}, bound={_clientBound}");
                    break;

                case "watch":
                    _watchPackets = parts.Length < 2 || parts[1] == "on";
                    Log($"packet watch: {(_watchPackets ? "on" : "off")}");
                    break;

                case "caps":
                    RequireBound();
                    Log($"capabilities: {(AssistantCapabilities)_client.Capabilities} (0x{_client.Capabilities:X})");
                    break;

                case "pos":
                {
                    RequireBound();
                    var fn = GetFn<GetPlayerPosition>(_client.GetPlayerPositionFn, "GetPlayerPosition");
                    bool ok = fn(out int x, out int y, out int z);
                    Log(ok ? $"player: ({x},{y},{z})" : "no player (not in game?)");
                    break;
                }

                case "walk":
                {
                    RequireBound();
                    var fn = GetFn<RequestMove>(_client.RequestMoveFn, "RequestMove");
                    int dir = parts.Length > 1 ? int.Parse(parts[1]) : 0;
                    Log($"RequestMove({dir}) → {fn(dir, false)}");
                    break;
                }

                case "cliloc":
                {
                    RequireBound();
                    var fn = GetFn<GetCliloc>(_client.GetClilocFn, "GetCliloc");
                    int id = int.Parse(parts[1]);
                    IntPtr ptr = fn(id, IntPtr.Zero, false);
                    Log($"cliloc {id}: '{(ptr != IntPtr.Zero ? Marshal.PtrToStringAnsi(ptr) : "<null>")}'");
                    FreeClientBuffer(ptr);
                    break;
                }

                case "pktlen":
                {
                    RequireBound();
                    var fn = GetFn<GetPacketLength>(_client.PacketLengthFn, "GetPacketLength");
                    int id = Convert.ToInt32(parts[1], parts[1].StartsWith("0x") ? 16 : 10);
                    Log($"packet 0x{id:X2} length: {fn(id)}");
                    break;
                }

                case "title":
                {
                    RequireBound();
                    var fn = GetFn<SetWindowTitle>(_client.SetWindowTitleFn, "SetWindowTitle");
                    IntPtr text = Marshal.StringToHGlobalAnsi(string.Join(' ', parts, 1, parts.Length - 1));
                    fn(text);
                    Marshal.FreeHGlobal(text);
                    Log("title set");
                    break;
                }

                case "tiledata":
                {
                    RequireBound();
                    var fn = GetFn<GetTileData>(_client.GetTileDataFn, "GetTileData");
                    int id = ParseId(parts[1]);
                    ulong flags = 0;
                    ushort textId = 0;
                    IntPtr name = Marshal.AllocHGlobal(64);
                    bool ok = fn(id, ref flags, ref textId, name, 64);
                    Log(ok ? $"land 0x{id:X4}: name='{Marshal.PtrToStringAnsi(name)}' flags=0x{flags:X} texId={textId}" : $"land 0x{id:X4}: not found");
                    Marshal.FreeHGlobal(name);
                    break;
                }

                case "staticdata":
                {
                    RequireBound();
                    var fn = GetFn<GetStaticData>(_client.GetStaticDataFn, "GetStaticData");
                    int id = ParseId(parts[1]);
                    ulong flags = 0;
                    byte weight = 0, layer = 0, height = 0;
                    int count = 0;
                    ushort animId = 0;
                    IntPtr name = Marshal.AllocHGlobal(64);
                    bool ok = fn(id, ref flags, ref weight, ref layer, ref count, ref animId, ref height, name, 64);
                    Log(ok
                        ? $"static 0x{id:X4}: name='{Marshal.PtrToStringAnsi(name)}' flags=0x{flags:X} weight={weight} layer={layer} height={height} animId={animId}"
                        : $"static 0x{id:X4}: not found");
                    Marshal.FreeHGlobal(name);
                    break;
                }

                case "art":
                {
                    RequireBound();
                    var fn = GetFn<GetStaticImage>(_client.GetStaticImageFn, "GetStaticImage");
                    ushort graphic = (ushort)ParseId(parts[1]);
                    bool ok = fn(graphic, out IntPtr px, out int w, out int h);
                    if (ok)
                    {
                        uint firstPixel = 0;
                        for (int i = 0; i < w * h; i++)
                        {
                            firstPixel = (uint)Marshal.ReadInt32(px, i * 4);
                            if (firstPixel != 0) break;
                        }
                        Log($"art 0x{graphic:X4}: {w}x{h} px, first non-zero pixel 0x{firstPixel:X8}");
                        FreeClientBuffer(px);
                    }
                    else
                    {
                        Log($"art 0x{graphic:X4}: not found");
                    }
                    break;
                }

                default:
                    Log($"unknown command '{parts[0]}' — try 'help'");
                    break;
            }
        }

        private static int ParseId(string s)
            => s.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
                ? Convert.ToInt32(s, 16)
                : int.Parse(s, CultureInfo.InvariantCulture);

        private static void RequireBound()
        {
            if (!_clientBound)
                throw new InvalidOperationException("ClientBindings not received yet.");
        }

        private static T GetFn<T>(IntPtr ptr, string name) where T : Delegate
        {
            if (ptr == IntPtr.Zero)
                throw new InvalidOperationException($"{name} not granted by the client (capability missing).");

            return Marshal.GetDelegateForFunctionPointer<T>(ptr);
        }

        private static void FreeClientBuffer(IntPtr ptr)
        {
            if (ptr == IntPtr.Zero || _client.FreeBufferFn == IntPtr.Zero)
                return;

            Marshal.GetDelegateForFunctionPointer<FreeBuffer>(_client.FreeBufferFn)(ptr);
        }

        private static void Log(string message)
        {
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] {message}");
        }
    }
}
