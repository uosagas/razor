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

// UOSagas-Razor: Test fuer die Versionsmeldung an den Server.
// Nach dem Login-Confirm (0x1B) muss Razor 0xBF sub 0x40 mit
// [len]SagasRazor[len]<version> (ASCII) an den Server schicken —
// Grundlage fuer das Server-Versions-Gate (AssistantVerification).

using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Assistant;
using Xunit;

namespace Razor.Core.Tests
{
    [Collection("ConfigSequential")]
    public class AssistantVersionTests : IDisposable
    {
        private readonly string m_TempDir;
        private readonly CultureInfo m_OldCulture;
        private readonly FakeClientServices m_Fake;

        public AssistantVersionTests()
        {
            m_OldCulture = CultureInfo.CurrentCulture;
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;

            m_TempDir = Path.Combine(Path.GetTempPath(), "RazorVersionTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(m_TempDir);
            Config.Initialize(m_TempDir);
            Config.LoadLastProfile();

            PacketHandlers.Initialize();
            World.Clear();

            m_Fake = new FakeClientServices();
            ClientProxy.Bind(m_Fake);
        }

        public void Dispose()
        {
            ClientProxy.Unbind();
            CultureInfo.CurrentCulture = m_OldCulture;

            try
            {
                Directory.Delete(m_TempDir, true);
            }
            catch
            {
            }
        }

        [Fact]
        public void LoginConfirm_sendet_die_Assistant_Version()
        {
            AssistantInfo.Version = "0.1.0";

            // 0x1B Login-Confirm (fixe Laenge): serial(4) unk(4) body(2)
            // x(2) y(2) z(2) dir(1) + Rest-Padding.
            using MemoryStream ms = new MemoryStream();
            ms.WriteByte(0x1B);
            ms.Write(new byte[] { 0x00, 0x00, 0x0A, 0x01 }); // serial
            ms.Write(new byte[4]);                            // unk
            ms.Write(new byte[] { 0x01, 0x90 });              // body
            ms.Write(new byte[] { 0x00, 0x64 });              // x
            ms.Write(new byte[] { 0x00, 0x64 });              // y
            ms.Write(new byte[] { 0x00, 0x00 });              // z
            ms.WriteByte(0x04);                               // direction
            ms.Write(new byte[16]);                           // padding

            PacketHandler.OnServerPacket(0x1B, new PacketReader(ms.ToArray(), false), null);

            byte[] sent = m_Fake.SentToServer.FirstOrDefault(pkt =>
                pkt.Length > 5 && pkt[0] == 0xBF && pkt[3] == 0x00 && pkt[4] == 0x40);

            Assert.NotNull(sent);

            string payload = Encoding.ASCII.GetString(sent);
            Assert.Contains("SagasRazor", payload);
            Assert.Contains("0.1.0", payload);

            // Layout hinter dem Subcommand: [len]Name[len]Version.
            int nameLen = sent[5];
            Assert.Equal("SagasRazor", Encoding.ASCII.GetString(sent, 6, nameLen));
            int verLen = sent[6 + nameLen];
            Assert.Equal("0.1.0", Encoding.ASCII.GetString(sent, 7 + nameLen, verLen));
        }
    }
}
