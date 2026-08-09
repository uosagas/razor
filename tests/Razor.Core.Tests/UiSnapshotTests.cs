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

// UOSagas-Razor: Smoke-Tests fuer den erweiterten UiSnapshot (Phase 3b).
// KEIN Display noetig: UiSnapshotBuilder.Build laeuft ohne Avalonia-Init —
// genau so, wie ihn RazorPlugin.OnTick auf dem Game-Thread aufruft.

using System;
using System.Globalization;
using System.IO;
using Assistant;
using Assistant.Agents;
using Razor.UI;
using Xunit;

namespace Razor.Core.Tests
{
    public class UiSnapshotTests : IDisposable
    {
        private readonly string m_TempDir;
        private readonly CultureInfo m_OldCulture;

        public UiSnapshotTests()
        {
            m_OldCulture = CultureInfo.CurrentCulture;
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;

            m_TempDir = Path.Combine(Path.GetTempPath(), "RazorUiSnapTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(m_TempDir);

            Config.Initialize(m_TempDir);
            Config.LoadLastProfile();

            Counter.Initialize();
            Agent.Initialize();
            DressList.Initialize();

            Agent.ClearAll();
            DressList.ClearAll();
        }

        public void Dispose()
        {
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
        public void Snapshot_liefert_Profile_Properties_und_ignoriert_unbekannte()
        {
            var req = new UiRequest();
            req.BoolProps.Add("AutoOpenDoors");
            req.BoolProps.Add("GibtEsNicht");
            req.TextProps.Add("SpellFormat");
            req.TextProps.Add("ObjectDelay");
            req.TextProps.Add("AuchNicht");

            UiSnapshot snap = UiSnapshotBuilder.Build(req);

            Assert.True(snap.BoolProps.ContainsKey("AutoOpenDoors"));
            Assert.False(snap.BoolProps.ContainsKey("GibtEsNicht"));
            Assert.Equal("{power} [{spell}]", snap.TextProps["SpellFormat"]);
            Assert.Equal("600", snap.TextProps["ObjectDelay"]);
            Assert.False(snap.TextProps.ContainsKey("AuchNicht"));
        }

        [Fact]
        public void Snapshot_liefert_Agents_mit_Items_und_Enabled()
        {
            UiSnapshot snap = UiSnapshotBuilder.Build(new UiRequest());
            Assert.Equal(Agent.List.Count, snap.Agents.Count);
            Assert.True(snap.Agents.Count > 0);

            // Organizer-1 suchen, ein Item einfuegen und die Sub-Liste anfragen.
            int orgIdx = snap.Agents.FindIndex(a => a.Kind == "OrganizerAgent");
            Assert.True(orgIdx >= 0);

            var org = (OrganizerAgent) Agent.List[orgIdx];
            org.AddItem(0x0EED);

            var req = new UiRequest { AgentIndex = orgIdx };
            snap = UiSnapshotBuilder.Build(req);

            Assert.Equal(orgIdx, snap.AgentIndex);
            Assert.Equal("OrganizerAgent", snap.AgentKind);
            Assert.Single(snap.AgentItems);
            Assert.Null(snap.AgentEnabled); // Organizer hat keinen Enable-Zustand

            // Sell-Agent hat einen Enable-Zustand.
            int sellIdx = snap.Agents.FindIndex(a => a.Kind == "SellAgent");
            Assert.True(sellIdx >= 0);
            snap = UiSnapshotBuilder.Build(new UiRequest { AgentIndex = sellIdx });
            Assert.NotNull(snap.AgentEnabled);
        }

        [Fact]
        public void Snapshot_liefert_Counter_und_DressLists()
        {
            DressList.Add(new DressList("Suit"));

            // Counter stammen aus counters.xml (wie Razor CE) — hier explizit registrieren.
            if (Counter.FindCounter("Bandage") == null)
                Counter.Register(new Counter("Bandage", "aids", 0x0E21, 0, true));

            UiSnapshot snap = UiSnapshotBuilder.Build(new UiRequest { DressListName = "Suit" });

            Assert.Contains(snap.Counters, c => c.Name == "Bandage" && c.Format == "aids");
            Assert.Contains(snap.DressLists, n => n == "Suit");
            Assert.Equal("Suit", snap.DressListName);
            Assert.Empty(snap.DressItems);
        }

        [Fact]
        public void Diagnostics_ohne_Player_wirft_nicht_und_zaehlt_Pakete()
        {
            World.Clear();
            Diagnostics.Reset();

            // Ohne eingeloggten Player darf Snapshot() nicht werfen.
            WorldStatus s = Diagnostics.Snapshot();
            Assert.False(s.HasPlayer);
            Assert.False(s.HasBackpack);
            Assert.Equal(0, s.WorldMobiles);

            // NotePacket zaehlt empfangene IDs; TopRecv liefert sie absteigend.
            Diagnostics.NotePacket(fromServer: true, id: 0x78);
            Diagnostics.NotePacket(fromServer: true, id: 0x78);
            Diagnostics.NotePacket(fromServer: true, id: 0x1B);

            Assert.Equal(2, Diagnostics.RecvCount(0x78));
            Assert.Equal(3, Diagnostics.Snapshot().TotalRecv);

            var top = Diagnostics.TopRecv(15);
            Assert.Equal(0x78, top[0].Key);
            Assert.Equal(2, top[0].Value);

            // Der UiSnapshot traegt Status + TopPackets ("0xXX: N") mit.
            UiSnapshot snap = UiSnapshotBuilder.Build(new UiRequest());
            Assert.Equal(3, snap.Status.TotalRecv);
            Assert.Contains(snap.TopPackets, p => p == "0x78: 2");
        }

        [Fact]
        public void SetScalarFromString_erhaelt_den_Property_Typ()
        {
            PropBinder.SetScalarFromString("ObjectDelay", "750");
            Assert.IsType<int>(Config.GetProperty("ObjectDelay"));
            Assert.Equal(750, Config.GetInt("ObjectDelay"));

            PropBinder.SetScalarFromString("FilterDelay", "4.5");
            Assert.IsType<double>(Config.GetProperty("FilterDelay"));

            PropBinder.SetScalarFromString("SpellFormat", "{spell}!");
            Assert.Equal("{spell}!", Config.GetString("SpellFormat"));

            // Unsinnige Eingaben lassen den Wert unveraendert.
            PropBinder.SetScalarFromString("ObjectDelay", "abc");
            Assert.Equal(750, Config.GetInt("ObjectDelay"));
        }
    }
}
