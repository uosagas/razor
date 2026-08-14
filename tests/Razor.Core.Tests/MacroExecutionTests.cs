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

// UOSagas-Razor: Tests fuer Phase 2c — Macro-Ausfuehrung, Waits und Recorder.
// FakeClientServices faengt SendToServer/InjectToClient ab; die Paket-Layouts
// entsprechen Razor CE (Razor/Network/Packets.cs).

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using Assistant;
using Assistant.Macros;
using UOSagas.AssistantApi;
using Xunit;

// Die Macro-Engine haengt an statischem Zustand (World, Timer-Heap,
// MacroManager, PacketHandler-Viewer) — Tests dieser Assembly duerfen
// nicht parallel laufen.
[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace Razor.Core.Tests
{
    /// <summary>Sammelt alle Pakete, die Razor senden/injizieren will.</summary>
    internal sealed class FakeClientServices : IClientServices
    {
        public readonly List<byte[]> SentToServer = new List<byte[]>();
        public readonly List<byte[]> InjectedToClient = new List<byte[]>();
        public readonly List<int> MoveRequests = new List<int>();
        public readonly List<int> CastSpells = new List<int>();

        /// <summary>Testbare Cliloc-Tabelle (OPL-/Journal-Tests).</summary>
        public readonly Dictionary<int, string> Clilocs = new Dictionary<int, string>();

        public AssistantCapabilities Capabilities => (AssistantCapabilities) ulong.MaxValue;

        public bool Has(AssistantCapabilities capability) => true;

        public bool SendToServer(byte[] packet)
        {
            SentToServer.Add(packet);
            return true;
        }

        public bool InjectToClient(byte[] packet)
        {
            InjectedToClient.Add(packet);
            return true;
        }

        public short GetPacketLength(int id) => -1;

        public uint GetHueColor(int hue) => 0u;

        /// <summary>Crash-Reporter-Relay: zeichnet Envelopes auf (Testbarkeit).</summary>
        public readonly List<string> CrashReports = new List<string>();
        public bool CrashReportResult = true;

        public bool SubmitCrashReport(string envelopeJson)
        {
            CrashReports.Add(envelopeJson);
            return CrashReportResult;
        }

        /// <summary>
        /// Called on cast so a test can reproduce that the client then sends
        /// 0xBF/0x1C and that packet lands SYNCHRONOUSLY back in the
        /// client-to-server viewer (that is how it really works: the ABI is a
        /// direct function pointer).
        /// </summary>
        public Action CastSpellHook;

        public void CastSpell(int index)
        {
            CastSpells.Add(index);
            CastSpellHook?.Invoke();
        }

        public bool RequestMove(int direction, bool run)
        {
            MoveRequests.Add(direction);
            return true;
        }

        public bool TryGetPlayerPosition(out int x, out int y, out int z)
        {
            x = y = z = 0;
            return false;
        }

        public string LastTitle;

        public void SetWindowTitle(string title)
        {
            LastTitle = title;
        }

        public void SetFilter(int filterType, bool enabled)
        {
        }

        public string GetCliloc(int cliloc, string args = "", bool capitalize = false)
        {
            if (!Clilocs.TryGetValue(cliloc, out string text))
                return string.Empty;

            // Minimaler ~N-Arg-Ersatz (reicht fuer die Tests).
            if (!string.IsNullOrEmpty(args))
            {
                string[] parts = args.Split('	');
                for (int i = 0; i < parts.Length; i++)
                    text = System.Text.RegularExpressions.Regex.Replace(
                        text, $"~{i + 1}[^~]*~", parts[i].TrimStart('@'));
            }

            return text;
        }

        public bool TryGetLandTileData(int index, out ulong flags, out ushort textId, out string name)
        {
            flags = 0;
            textId = 0;
            name = null;
            return false;
        }

        public bool TryGetStaticTileData(int index, out StaticTileInfo info)
        {
            info = default;
            return false;
        }

        public bool TryGetStaticArt(ushort graphic, out uint[] pixels, out int width, out int height)
        {
            pixels = null;
            width = height = 0;
            return false;
        }
    }

    public class MacroExecutionTests : IDisposable
    {
        private const uint PlayerSerial = 0x00000777;

        private readonly string m_TempDir;
        private readonly CultureInfo m_OldCulture;
        private readonly FakeClientServices m_Fake;

        public MacroExecutionTests()
        {
            m_OldCulture = CultureInfo.CurrentCulture;
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;

            m_TempDir = Path.Combine(Path.GetTempPath(), "RazorMacroExecTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(m_TempDir);

            Config.Initialize(m_TempDir);
            Config.LoadLastProfile();

            PacketHandlers.Initialize(); // idempotent, registriert auch MacroHandlers/Targeting

            MacroManager.Stop();
            ActionQueue.Stop();
            Targeting.Reset();
            Assistant.Core.SystemMessages.Messages.Clear();

            World.Clear();
            PlayerData player = new PlayerData(PlayerSerial);
            player.Position = new Point3D(1000, 1000, 0);
            World.AddMobile(player);
            World.Player = player;

            m_Fake = new FakeClientServices();
            ClientProxy.Bind(m_Fake);
        }

        public void Dispose()
        {
            MacroManager.Stop();
            ActionQueue.Stop();
            Targeting.Reset();
            ClientProxy.Unbind();
            World.Clear();

            CultureInfo.CurrentCulture = m_OldCulture;
            try
            {
                Directory.Delete(m_TempDir, true);
            }
            catch
            {
            }
        }

        // ---- Helpers ---------------------------------------------------------

        private string WriteMacro(string name, params string[] lines)
        {
            string path = Path.Combine(m_TempDir, name + ".macro");
            File.WriteAllLines(path, lines);
            return path;
        }

        /// <summary>Treibt Timer.Slice, bis condition erfuellt ist oder der Timeout ablaeuft.</summary>
        private static bool SliceUntil(Func<bool> condition, int timeoutMs)
        {
            DateTime end = DateTime.UtcNow.AddMilliseconds(timeoutMs);
            while (DateTime.UtcNow < end)
            {
                Assistant.Timer.Slice();
                if (condition())
                    return true;
                Thread.Sleep(5);
            }

            return condition();
        }

        /// <summary>BigEndian-Unicode-Nullterminiert dekodieren (0xAD-Text ohne Keywords).</summary>
        private static string DecodeBigUniNull(byte[] packet, int offset)
        {
            StringBuilder sb = new StringBuilder();
            for (int i = offset; i + 1 < packet.Length; i += 2)
            {
                int c = (packet[i] << 8) | packet[i + 1];
                if (c == 0)
                    break;
                sb.Append((char) c);
            }

            return sb.ToString();
        }

        private IEnumerable<byte[]> SentWithId(byte id)
        {
            return m_Fake.SentToServer.Where(b => b.Length > 0 && b[0] == id);
        }

        private static uint ReadUInt(byte[] data, int offset)
        {
            return (uint) ((data[offset] << 24) | (data[offset + 1] << 16) | (data[offset + 2] << 8) |
                           data[offset + 3]);
        }

        /// <summary>0xAD-Speech-Paket (Client -> Server), nicht encoded.</summary>
        private static byte[] BuildSpeechPacket(string text, MessageType type = MessageType.Regular,
            ushort hue = 52, ushort font = 3)
        {
            List<byte> b = new List<byte> {0xAD, 0, 0};
            b.Add((byte) type);
            b.Add((byte) (hue >> 8));
            b.Add((byte) hue);
            b.Add((byte) (font >> 8));
            b.Add((byte) font);
            b.AddRange(Encoding.ASCII.GetBytes("ENU\0"));
            foreach (char c in text)
            {
                b.Add((byte) (c >> 8));
                b.Add((byte) c);
            }

            b.Add(0);
            b.Add(0);

            int len = b.Count;
            b[1] = (byte) (len >> 8);
            b[2] = (byte) len;
            return b.ToArray();
        }

        /// <summary>0xB0-SendGump-Paket (Server -> Client), nur Header-Felder.</summary>
        private static byte[] BuildGumpPacket(uint serial, uint gumpId)
        {
            List<byte> b = new List<byte> {0xB0, 0, 0};
            void U32(uint v)
            {
                b.Add((byte) (v >> 24));
                b.Add((byte) (v >> 16));
                b.Add((byte) (v >> 8));
                b.Add((byte) v);
            }

            U32(serial);
            U32(gumpId);
            U32(0); // x
            U32(0); // y
            b.Add(0);
            b.Add(0); // layout len 0
            b.Add(0);
            b.Add(0); // text lines 0

            int len = b.Count;
            b[1] = (byte) (len >> 8);
            b[2] = (byte) len;
            return b.ToArray();
        }

        // ---- Tests -----------------------------------------------------------

        [Fact]
        public void SpeechAction_Perform_Sends0xAD_WithText()
        {
            SpeechAction a = new SpeechAction(MessageType.Regular, 52, 3, "ENU", null, "hallo welt");

            Assert.True(a.Perform());

            byte[] pkt = Assert.Single(SentWithId(0xAD));
            // Layout: [0]=0xAD [1,2]=len [3]=type [4,5]=hue [6,7]=font [8..11]=lang [12..]=BigUniNull
            Assert.Equal((byte) MessageType.Regular, pkt[3]);
            Assert.Equal(52, (pkt[4] << 8) | pkt[5]);
            Assert.Equal(3, (pkt[6] << 8) | pkt[7]);
            Assert.Equal("hallo welt", DecodeBigUniNull(pkt, 12));
        }

        [Fact]
        public void DoubleClickAction_Perform_Sends0x06_WithSerial()
        {
            const uint serial = 0x40001234;

            DoubleClickAction a = new DoubleClickAction((Serial) serial, 0x0E75);
            Assert.True(a.Perform());

            // ActionQueue verarbeitet den ersten Eintrag synchron (StartMe -> OnTick).
            byte[] pkt = Assert.Single(SentWithId(0x06));
            Assert.Equal(5, pkt.Length);
            Assert.Equal(serial, ReadUInt(pkt, 1));
        }

        [Fact]
        public void UseSkillAction_Perform_Sends0x12_SkillCommand()
        {
            UseSkillAction a = new UseSkillAction(13);
            Assert.True(a.Perform());

            byte[] pkt = Assert.Single(SentWithId(0x12));
            Assert.Equal(0x24, pkt[3]); // sub: use skill
            Assert.Equal("13 0", Encoding.ASCII.GetString(pkt, 4, pkt.Length - 5)); // ohne Nullterminator
            Assert.Equal(13, World.Player.LastSkill);
        }

        [Fact]
        public void Macro_PauseAction_WaitsBeforeContinuing()
        {
            string path = WriteMacro("pause",
                "Assistant.Macros.SpeechAction|0|52|3|ENU|0|marker1",
                "Assistant.Macros.PauseAction|00:00:00.2500000",
                "Assistant.Macros.SpeechAction|0|52|3|ENU|0|marker2");

            Macro m = new Macro(path);
            MacroManager.Play(m);

            // marker1 kommt sofort, marker2 erst nach Ablauf der Pause.
            Assert.True(SliceUntil(
                () => SentWithId(0xAD).Any(p => DecodeBigUniNull(p, 12) == "marker1"), 1000));

            Assistant.Timer.Slice();
            Assert.DoesNotContain(SentWithId(0xAD), p => DecodeBigUniNull(p, 12) == "marker2");

            Assert.True(SliceUntil(
                () => SentWithId(0xAD).Any(p => DecodeBigUniNull(p, 12) == "marker2"), 2000));
        }

        [Fact]
        public void Macro_IfElseEndIf_TakesCorrectBranch()
        {
            World.Player.HitsMax = 100;
            World.Player.Hits = 100;

            string path = WriteMacro("ifelse",
                "Assistant.Macros.IfAction|0|1|90", // Hits >= 90 -> true
                "Assistant.Macros.SpeechAction|0|52|3|ENU|0|then-branch",
                "Assistant.Macros.ElseAction",
                "Assistant.Macros.SpeechAction|0|52|3|ENU|0|else-branch",
                "Assistant.Macros.EndIfAction");

            Macro m = new Macro(path);
            MacroManager.Play(m);

            Assert.True(SliceUntil(() => !MacroManager.Playing, 2000));

            Assert.Contains(SentWithId(0xAD), p => DecodeBigUniNull(p, 12) == "then-branch");
            Assert.DoesNotContain(SentWithId(0xAD), p => DecodeBigUniNull(p, 12) == "else-branch");
        }

        [Fact]
        public void Macro_IfElseEndIf_FalseCondition_TakesElseBranch()
        {
            World.Player.HitsMax = 100;
            World.Player.Hits = 50;

            string path = WriteMacro("ifelse2",
                "Assistant.Macros.IfAction|0|1|90", // Hits >= 90 -> false
                "Assistant.Macros.SpeechAction|0|52|3|ENU|0|then-branch",
                "Assistant.Macros.ElseAction",
                "Assistant.Macros.SpeechAction|0|52|3|ENU|0|else-branch",
                "Assistant.Macros.EndIfAction");

            Macro m = new Macro(path);
            MacroManager.Play(m);

            Assert.True(SliceUntil(() => !MacroManager.Playing, 2000));

            Assert.DoesNotContain(SentWithId(0xAD), p => DecodeBigUniNull(p, 12) == "then-branch");
            Assert.Contains(SentWithId(0xAD), p => DecodeBigUniNull(p, 12) == "else-branch");
        }

        [Fact]
        public void Macro_ForLoop_RunsBodyNTimes()
        {
            string path = WriteMacro("forloop",
                "Assistant.Macros.ForAction|3",
                "Assistant.Macros.SpeechAction|0|52|3|ENU|0|loop-body",
                "Assistant.Macros.EndForAction");

            Macro m = new Macro(path);
            MacroManager.Play(m);

            Assert.True(SliceUntil(() => !MacroManager.Playing, 3000));

            Assert.Equal(3, SentWithId(0xAD).Count(p => DecodeBigUniNull(p, 12) == "loop-body"));
        }

        [Fact]
        public void Macro_WaitForGump_ResolvesWhenGumpArrives()
        {
            string path = WriteMacro("waitgump",
                "Assistant.Macros.WaitForGumpAction|0|False|10",
                "Assistant.Macros.SpeechAction|0|52|3|ENU|0|after-gump");

            Macro m = new Macro(path);
            MacroManager.Play(m);

            // Wait ist aktiv, Folgeaktion darf noch nicht laufen.
            Assistant.Timer.Slice();
            Thread.Sleep(10);
            Assistant.Timer.Slice();
            Assert.True(MacroManager.Playing);
            Assert.True(MacroManager.Current.Waiting);
            Assert.DoesNotContain(SentWithId(0xAD), p => DecodeBigUniNull(p, 12) == "after-gump");

            // 0xB0 SendGump einspielen -> WaitForGump loest auf (CheckMatch -> ExecNext).
            byte[] gump = BuildGumpPacket(0x4000AAAA, 0x12345678);
            bool blocked = PacketHandler.OnServerPacket(0xB0, new PacketReader(gump, true), null);

            Assert.True(blocked); // Razor CE blockt das Paket, wenn ein Macro darauf wartete
            Assert.True(World.Player.HasGump);
            Assert.Equal(0x12345678u, World.Player.CurrentGumpI);
            Assert.Contains(SentWithId(0xAD), p => DecodeBigUniNull(p, 12) == "after-gump");
        }

        [Fact]
        public void Recorder_ClientSpeechPacket_RecordsSpeechAction()
        {
            Macro m = new Macro(Path.Combine(m_TempDir, "recorded.macro"));
            MacroManager.Record(m);

            byte[] speech = BuildSpeechPacket("hello razor");
            PacketHandler.OnClientPacket(0xAD, new PacketReader(speech, true), null);

            MacroManager.Stop();

            SpeechAction rec = m.Actions.OfType<SpeechAction>().Single();
            Assert.Equal("hello razor", rec.Speech);
            Assert.Equal("Assistant.Macros.SpeechAction|0|52|3|ENU|0|hello razor", rec.Serialize());

            // Stop() speichert das aufgenommene Macro.
            Assert.True(File.Exists(m.Filename));
        }

        [Fact]
        public void Recorder_ClientDoubleClick_RecordsDoubleClickAction()
        {
            Item item = new Item((Serial) 0x40005555u);
            item.ItemID = 0x0E75;
            World.AddItem(item);

            Macro m = new Macro(Path.Combine(m_TempDir, "recorded_dc.macro"));
            MacroManager.Record(m);

            byte[] dclick = {0x06, 0x40, 0x00, 0x55, 0x55};
            PacketHandler.OnClientPacket(0x06, new PacketReader(dclick, false), null);

            MacroManager.Stop();

            DoubleClickAction rec = m.Actions.OfType<DoubleClickAction>().Single();
            Assert.Equal("Assistant.Macros.DoubleClickAction|1073763669|3701", rec.Serialize());
        }

        // WARNING: the UOSagas client casts EVERYTHING through 0xBF/0x1C
        // (spellbook icon, songbook icon, its own macros). Razor CE's 0x12
        // path is dead code in this client, so without that branch the
        // recorder captured no cast at all (Discord report "Macros recording
        // doesn't register bard songs").

        /// <summary>0xBF sub 0x1C: [0x1C][type 2][spell 2] (type 2 = no book).</summary>
        private static byte[] BuildExtCastPacket(ushort spellId)
        {
            return new byte[]
            {
                0xBF, 0x00, 0x09,
                0x00, 0x1C,
                0x00, 0x02,
                (byte) (spellId >> 8), (byte) spellId
            };
        }

        [Fact]
        public void Recorder_ExtendedCastPacket_RecordsExtCastSpellAction()
        {
            Macro m = new Macro(Path.Combine(m_TempDir, "recorded_song.macro"));
            MacroManager.Record(m);

            // 704 = song of healing
            byte[] cast = BuildExtCastPacket(704);
            PacketHandler.OnClientPacket(0xBF, new PacketReader(cast, true), null);

            MacroManager.Stop();

            ExtCastSpellAction rec = m.Actions.OfType<ExtCastSpellAction>().Single();
            Assert.Equal(704, rec.SpellID);
            Assert.Equal(704, World.Player.LastSpell);
        }

        [Fact]
        public void Recorder_IgnoriertEigeneCasts()
        {
            Macro m = new Macro(Path.Combine(m_TempDir, "recorded_self.macro"));
            MacroManager.Record(m);

            // Razor casts itself: the client then sends 0xBF/0x1C, which comes
            // back through the viewer. The triggering action is already in the
            // macro as a hotkey line - nothing may be added here, otherwise
            // playback would cast twice.
            m_Fake.CastSpellHook = () =>
                PacketHandler.OnClientPacket(0xBF, new PacketReader(BuildExtCastPacket(704), true), null);

            ClientProxy.CastSpell(704);

            MacroManager.Stop();

            Assert.Empty(m.Actions.OfType<ExtCastSpellAction>());
        }

        [Fact]
        public void GumpResponseAction_Perform_Sends0xB1_AndClosesClientGump()
        {
            World.Player.CurrentGumpS = 0x4000BBBB;
            World.Player.CurrentGumpI = 0xCAFEBABE;
            World.Player.HasGump = true;

            GumpResponseAction a = new GumpResponseAction(2, new int[0], new GumpTextEntry[0]);
            Assert.True(a.Perform());

            byte[] pkt = Assert.Single(SentWithId(0xB1));
            Assert.Equal(0x4000BBBBu, ReadUInt(pkt, 3));
            Assert.Equal(0xCAFEBABEu, ReadUInt(pkt, 7));
            Assert.Equal(2u, ReadUInt(pkt, 11)); // buttonID
            Assert.False(World.Player.HasGump);

            // CloseGump (0xBF sub 0x04) wird in den Client injiziert.
            Assert.Contains(m_Fake.InjectedToClient, p => p[0] == 0xBF && p[3] == 0x00 && p[4] == 0x04);
        }

        [Fact]
        public void WaitForTarget_ResolvesOnServerTargetRequest()
        {
            string path = WriteMacro("waittarget",
                "Assistant.Macros.WaitForTargetAction|30",
                "Assistant.Macros.SpeechAction|0|52|3|ENU|0|after-target");

            Macro m = new Macro(path);
            MacroManager.Play(m);

            Assistant.Timer.Slice();
            Assert.True(MacroManager.Current.Waiting);

            // Server-Target-Request 0x6C einspielen.
            byte[] targetReq = new byte[19];
            targetReq[0] = 0x6C;
            targetReq[1] = 0; // allowGround
            targetReq[2] = 0xDE;
            targetReq[3] = 0xAD;
            targetReq[4] = 0xBE;
            targetReq[5] = 0xEF; // target id
            targetReq[6] = 0; // flags

            bool blocked = PacketHandler.OnServerPacket(0x6C, new PacketReader(targetReq, false), null);

            Assert.True(blocked);
            Assert.True(Targeting.HasTarget);
            Assert.Contains(SentWithId(0xAD), p => DecodeBigUniNull(p, 12) == "after-target");
        }

        [Fact]
        public void AbsoluteTargetAction_Perform_AnswersPendingTarget()
        {
            // Target-Cursor vom Server simulieren.
            byte[] targetReq = new byte[19];
            targetReq[0] = 0x6C;
            targetReq[2] = 0x00;
            targetReq[3] = 0x00;
            targetReq[4] = 0x00;
            targetReq[5] = 0x42; // target id 0x42
            PacketHandler.OnServerPacket(0x6C, new PacketReader(targetReq, false), null);
            Assert.True(Targeting.HasTarget);

            AbsoluteTargetAction a = new AbsoluteTargetAction(new[]
            {
                "Assistant.Macros.AbsoluteTargetAction", "0", "0", "3735928559", "100", "200", "5", "400"
            });

            Assert.True(a.Perform());

            byte[] pkt = Assert.Single(SentWithId(0x6C));
            Assert.Equal(19, pkt.Length);
            Assert.Equal(0x42u, ReadUInt(pkt, 2)); // beantwortet die AKTUELLE TargID
            Assert.Equal(3735928559u, ReadUInt(pkt, 7)); // Ziel-Serial
            Assert.False(Targeting.HasTarget);
        }
    }
}
