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

// UOSagas-Razor: Tests fuer die UO-Titelleiste (Core.TitleBar).
// Token-Ersetzung gegen eine Fake-World; SetWindowTitle wird ueber die
// Fake-Services abgegriffen.

using System;
using System.Globalization;
using System.IO;
using Assistant;
using Assistant.Core;
using Xunit;

namespace Razor.Core.Tests
{
    [Collection("ConfigSequential")]
    public class TitleBarTests : IDisposable
    {
        private const uint PlayerSerial = 0x00000C01;

        private readonly string m_TempDir;
        private readonly CultureInfo m_OldCulture;
        private readonly FakeClientServices m_Fake;

        public TitleBarTests()
        {
            m_OldCulture = CultureInfo.CurrentCulture;
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;

            m_TempDir = Path.Combine(Path.GetTempPath(), "RazorTitleBarTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(m_TempDir);
            Config.Initialize(m_TempDir);
            Config.LoadLastProfile();

            PacketHandlers.Initialize();

            World.Clear();
            World.ShardName = "UOSagas";
            PlayerData player = new PlayerData(PlayerSerial)
            {
                Name = "Tester",
                Hits = 61, HitsMax = 100,
                Mana = 42, ManaMax = 80,
                Stam = 90, StamMax = 95,
                Str = 100, Dex = 90, Int = 45,
                Weight = 250, MaxWeight = 400,
                Gold = 12345,
                Followers = 2, FollowersMax = 5
            };
            World.AddMobile(player);
            World.Player = player;

            m_Fake = new FakeClientServices();
            ClientProxy.Bind(m_Fake);
        }

        public void Dispose()
        {
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

        [Fact]
        public void Ersetzt_Spieler_Tokens()
        {
            Config.SetProperty("TitleBarText", "{char} HP {hp}/{hpmax} M {mana} S {stam} G {gold}");
            Config.SetProperty("TitleBarDisplay", true);

            TitleBar.Update();

            Assert.Equal("Tester HP 61/100 M 42 S 90 G 12345", m_Fake.LastTitle);
        }

        [Fact]
        public void Ersetzt_Shard_Gewicht_Followers()
        {
            Config.SetProperty("TitleBarText", "{shard} {weight}/{maxweight} pets {followers}/{followersmax}");
            TitleBar.Update();

            Assert.Equal("UOSagas 250/400 pets 2/5", m_Fake.LastTitle);
        }

        [Fact]
        public void Unbekannte_OSI_Tokens_werden_neutralisiert()
        {
            Config.SetProperty("TitleBarText", "dps {dps} luck {luck} buffs {buffsdebuffs}");
            TitleBar.Update();

            // Keine rohen {tokens} mehr — durch "-" ersetzt.
            Assert.Equal("dps - luck - buffs -", m_Fake.LastTitle);
            Assert.DoesNotContain("{", m_Fake.LastTitle);
        }

        [Fact]
        public void Counter_Token_wird_durch_die_Anzahl_ersetzt()
        {
            // Ein aktiver Counter mit Format "bandage" -> {bandage} = Amount.
            Counter c = new Counter("Bandages", "bandage", 0x0E21, -1, false);
            c.Enabled = true;
            Counter.List.Add(c);
            try
            {
                Config.SetProperty("TitleBarText", "bandages: {bandage}");
                TitleBar.Update();
                Assert.StartsWith("bandages: ", m_Fake.LastTitle);
                Assert.DoesNotContain("{bandage}", m_Fake.LastTitle);
            }
            finally
            {
                Counter.List.Remove(c);
            }
        }

        [Fact]
        public void Kein_Titel_wenn_Text_leer_und_nur_Tokens()
        {
            Config.SetProperty("TitleBarText", "Health {hp}");
            TitleBar.Update();
            string first = m_Fake.LastTitle;
            Assert.Equal("Health 61", first);

            // Zweiter Aufruf ohne Aenderung -> kein neuer SetWindowTitle-Wert.
            m_Fake.LastTitle = "SENTINEL";
            TitleBar.Update();
            Assert.Equal("SENTINEL", m_Fake.LastTitle);
        }
    }
}
