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

// UOSagas-Razor: Tests fuer den Crash-Reporter (Capture/Logfile, Discord-
// Embed-Payload inkl. Truncation/Kommentar, Envelope fuer den Client-Relay,
// Script-Fehler-Reports, Settings-Roundtrip, Dedupe) und die AccountName-
// Viewer (0x80/0x91 — es darf NUR der Name gelesen werden).

using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using Assistant;
using Xunit;

namespace Razor.Core.Tests
{
    [Collection("ConfigSequential")]
    public class CrashReporterTests : IDisposable
    {
        private readonly string m_TempDir;

        public CrashReporterTests()
        {
            m_TempDir = Path.Combine(Path.GetTempPath(), "RazorCrashTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(m_TempDir);

            CrashReporter.DataRootOverride = m_TempDir;
            CrashReporter.ResetSettingsCache();
            ClientProxy.Unbind();
        }

        public void Dispose()
        {
            CrashReporter.UiPresenter = null;
            CrashReporter.DataRootOverride = null;
            CrashReporter.ResetSettingsCache();
            ClientProxy.Unbind();

            try
            {
                Directory.Delete(m_TempDir, true);
            }
            catch
            {
            }
        }

        private static Exception MakeException(string message = "Kaboom")
        {
            try
            {
                throw new InvalidOperationException(message);
            }
            catch (InvalidOperationException ex)
            {
                return ex;
            }
        }

        [Fact]
        public void Capture_schreibt_Logfile_mit_Kontext()
        {
            World.AccountName = "TestAcct";
            World.ShardName = "UOSagas";

            CrashReport report = CrashReporter.Capture(MakeException("Der Testfehler"), "Unit-Test", fatal: true);

            Assert.NotNull(report.LogFile);
            Assert.True(File.Exists(report.LogFile));

            string text = File.ReadAllText(report.LogFile);
            Assert.Contains("Der Testfehler", text);
            Assert.Contains("TestAcct", text);
            Assert.Contains("UOSagas", text);
            Assert.Contains("Fatal:      yes", text);
            Assert.Contains("InvalidOperationException", text);
            Assert.Contains("MakeException", text); // Stacktrace ist enthalten
        }

        [Fact]
        public void Webhook_Payload_ist_valides_JSON_mit_Embed_Karte()
        {
            World.AccountName = "TestAcct";
            World.ShardName = "UOSagas";

            CrashReport report = CrashReporter.Capture(MakeException(), "Unit-Test", fatal: false);
            string payload = CrashReporter.BuildWebhookPayload(report, "tester#1234", "It happened while looting.");

            using JsonDocument doc = JsonDocument.Parse(payload);
            JsonElement root = doc.RootElement;

            Assert.Equal("Razor Crash Reporter", root.GetProperty("username").GetString());

            JsonElement embed = root.GetProperty("embeds")[0];
            Assert.Equal("Razor Crash Report", embed.GetProperty("title").GetString());
            Assert.Contains("Kaboom", embed.GetProperty("description").GetString());

            var fields = embed.GetProperty("fields").EnumerateArray()
                .ToDictionary(
                    f => f.GetProperty("name").GetString(),
                    f => f.GetProperty("value").GetString());

            Assert.Equal("TestAcct", fields["Account"]);
            Assert.Equal("tester#1234", fields["Discord"]);
            Assert.Equal("UOSagas", fields["Shard"]);
            Assert.Equal("Unit-Test", fields["Source"]);
            Assert.Equal("It happened while looting.", fields["Comment"]);
            Assert.True(fields.ContainsKey("Character"));
            Assert.True(fields.ContainsKey("Razor"));
            Assert.False(fields.ContainsKey("Script")); // kein Script-Fehler
        }

        [Fact]
        public void Webhook_Payload_kuerzt_lange_Stacktraces()
        {
            string huge = new string('x', 10000);
            CrashReport report = CrashReporter.Capture(MakeException(huge), "Unit-Test", fatal: false);

            string payload = CrashReporter.BuildWebhookPayload(report, null, null);

            using JsonDocument doc = JsonDocument.Parse(payload);
            string description = doc.RootElement.GetProperty("embeds")[0]
                .GetProperty("description").GetString();

            Assert.True(description.Length < 4096, $"description ist {description.Length} Zeichen lang");
            Assert.Contains("(truncated)", description);

            // Leerer Discord-Name wird zum Platzhalter; ohne Kommentar kein Feld.
            var fields = doc.RootElement.GetProperty("embeds")[0].GetProperty("fields")
                .EnumerateArray()
                .ToDictionary(
                    f => f.GetProperty("name").GetString(),
                    f => f.GetProperty("value").GetString());
            Assert.Equal("-", fields["Discord"]);
            Assert.False(fields.ContainsKey("Comment"));
        }

        [Fact]
        public void Envelope_enthaelt_Log_und_optional_das_Script()
        {
            CrashReport report = CrashReporter.Capture(MakeException(), "Unit-Test", fatal: false);
            report.ScriptEngine = "Lua";
            report.ScriptName = "My Looter!";
            report.ScriptContent = "print('hi')";

            string withScript = CrashReporter.BuildEnvelope(report, "tester", null, includeScript: true);

            using (JsonDocument doc = JsonDocument.Parse(withScript))
            {
                JsonElement attachments = doc.RootElement.GetProperty("attachments");
                Assert.Equal(2, attachments.GetArrayLength());
                Assert.StartsWith("crash-", attachments[0].GetProperty("name").GetString());

                string scriptName = attachments[1].GetProperty("name").GetString();
                Assert.EndsWith(".lua", scriptName);
                Assert.DoesNotContain("!", scriptName); // Dateiname bereinigt

                string content = Encoding.UTF8.GetString(
                    Convert.FromBase64String(attachments[1].GetProperty("contentBase64").GetString()));
                Assert.Equal("print('hi')", content);

                // payloadJson ist selbst gueltiges JSON.
                using JsonDocument inner = JsonDocument.Parse(doc.RootElement.GetProperty("payloadJson").GetString());
                Assert.Equal("Razor Script Error (Lua)",
                    inner.RootElement.GetProperty("embeds")[0].GetProperty("title").GetString());
            }

            string withoutScript = CrashReporter.BuildEnvelope(report, "tester", null, includeScript: false);

            using (JsonDocument doc = JsonDocument.Parse(withoutScript))
            {
                Assert.Equal(1, doc.RootElement.GetProperty("attachments").GetArrayLength());
            }
        }

        [Fact]
        public async System.Threading.Tasks.Task SendAsync_geht_ueber_den_Client_Relay()
        {
            var fake = new FakeClientServices();
            ClientProxy.Bind(fake);

            CrashReport report = CrashReporter.Capture(MakeException(), "Unit-Test", fatal: false);

            string error = await CrashReporter.SendAsync(report, "tester", "test comment", includeScript: false);

            Assert.Null(error);
            Assert.Single(fake.CrashReports);

            using JsonDocument doc = JsonDocument.Parse(fake.CrashReports[0]);
            Assert.True(doc.RootElement.TryGetProperty("payloadJson", out _));

            // Relay lehnt ab (Rate-Limit) -> Fehlertext statt Erfolg.
            fake.CrashReportResult = false;
            string rejected = await CrashReporter.SendAsync(report, "tester", null, includeScript: false);
            Assert.NotNull(rejected);
        }

        [Fact]
        public async System.Threading.Tasks.Task SendAsync_ohne_Client_und_Webhook_meldet_Fehler()
        {
            ClientProxy.Unbind();
            CrashReport report = CrashReporter.Capture(MakeException(), "Unit-Test", fatal: false);

            string error = await CrashReporter.SendAsync(report, "tester", null, includeScript: false);

            Assert.NotNull(error);
            Assert.Contains("not available", error, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void ReportScriptError_schreibt_Logfile_ohne_Fenster()
        {
            CrashReport shown = null;
            CrashReporter.UiPresenter = r =>
            {
                shown = r;
                return true;
            };

            string logDir = Path.Combine(m_TempDir, "CrashLogs");
            int logsBefore = Directory.Exists(logDir) ? Directory.GetFiles(logDir).Length : 0;

            string error = "Runtime error: " + Guid.NewGuid().ToString("N");
            CrashReporter.ReportScriptError("Lua", "TestScript", "error('x')", error);

            // Der Report-Dialog bleibt echten Crashes vorbehalten — Script-Fehler
            // landen nur still im Logfile.
            Assert.Null(shown);
            Assert.Equal(logsBefore + 1, Directory.GetFiles(logDir).Length);

            // Dedupe: gleicher Fehler direkt nochmal -> kein zweites Logfile.
            CrashReporter.ReportScriptError("Lua", "TestScript", "error('x')", error);
            Assert.Equal(logsBefore + 1, Directory.GetFiles(logDir).Length);
        }

        [Fact]
        public void Settings_Roundtrip_persistiert_DiscordName_und_Webhook()
        {
            CrashReporter.Settings.DiscordName = "tester";
            CrashReporter.Settings.WebhookUrl = "https://discord.com/api/webhooks/1/abc";
            CrashReporter.SaveSettings();

            CrashReporter.ResetSettingsCache();

            Assert.Equal("tester", CrashReporter.Settings.DiscordName);
            Assert.Equal("https://discord.com/api/webhooks/1/abc", CrashReporter.Settings.WebhookUrl);
            Assert.True(File.Exists(Path.Combine(m_TempDir, "crash-reporter.json")));
        }

        [Fact]
        public void Report_dedupliziert_gleiche_Fehler_im_Zeitfenster()
        {
            int shown = 0;
            CrashReporter.UiPresenter = _ =>
            {
                shown++;
                return true;
            };

            // Eindeutige Source, damit fruehere Testlaeufe nicht hineinspielen.
            string source = "Dedupe-Test-" + Guid.NewGuid().ToString("N");
            Exception ex = MakeException();

            CrashReporter.Report(ex, source);
            CrashReporter.Report(ex, source);

            Assert.Equal(1, shown);
        }
    }

    [Collection("ConfigSequential")]
    public class AccountNameViewerTests : IDisposable
    {
        private readonly string m_TempDir;

        public AccountNameViewerTests()
        {
            m_TempDir = Path.Combine(Path.GetTempPath(), "RazorAcctTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(m_TempDir);
            Config.Initialize(m_TempDir);
            Config.LoadLastProfile();

            PacketHandlers.Initialize();
            World.Clear();
            World.AccountName = null;
        }

        public void Dispose()
        {
            World.AccountName = null;

            try
            {
                Directory.Delete(m_TempDir, true);
            }
            catch
            {
            }
        }

        private static byte[] FixedAscii(string s, int len)
        {
            byte[] buf = new byte[len];
            byte[] src = Encoding.ASCII.GetBytes(s);
            Array.Copy(src, buf, Math.Min(src.Length, len));
            return buf;
        }

        [Fact]
        public void FirstLogin_0x80_setzt_den_Accountnamen()
        {
            // Client-Format (OutgoingPackets.Send_FirstLogin):
            // [0x80][account 30][password 30][0xFF]
            using MemoryStream ms = new MemoryStream();
            ms.WriteByte(0x80);
            ms.Write(FixedAscii("TestUser", 30));
            ms.Write(FixedAscii("geheim", 30));
            ms.WriteByte(0xFF);

            PacketHandler.OnClientPacket(0x80, new PacketReader(ms.ToArray(), false), null);

            Assert.Equal("TestUser", World.AccountName);
        }

        [Fact]
        public void GameLogin_0x91_setzt_den_Accountnamen_nach_dem_Seed()
        {
            // Client-Format (OutgoingPackets.Send_SecondLogin):
            // [0x91][seed 4][account 30][password 30]
            using MemoryStream ms = new MemoryStream();
            ms.WriteByte(0x91);
            ms.Write(new byte[] { 0x12, 0x34, 0x56, 0x78 });
            ms.Write(FixedAscii("TestAcct", 30));
            ms.Write(FixedAscii("geheim", 30));

            PacketHandler.OnClientPacket(0x91, new PacketReader(ms.ToArray(), false), null);

            Assert.Equal("TestAcct", World.AccountName);
        }
    }
}
