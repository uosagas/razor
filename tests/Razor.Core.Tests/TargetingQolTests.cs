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

// UOSagas-Razor: Tests fuer den vierten Options-Schwung —
// QueueActions, ShowAttackTarget-Overhead, RangeCheckLT, StealthSteps.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Assistant;
using Assistant.Macros;
using Xunit;

namespace Razor.Core.Tests
{
    [Collection("ConfigSequential")]
    public class TargetingQolTests : IDisposable
    {
        private const uint PlayerSerial = 0x00000E01;
        private const uint EnemySerial = 0x00000E02;
        private const uint ItemSerial = 0x40000E03;

        private readonly string m_TempDir;
        private readonly CultureInfo m_OldCulture;
        private readonly FakeClientServices m_Fake;

        public TargetingQolTests()
        {
            m_OldCulture = CultureInfo.CurrentCulture;
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;

            m_TempDir = Path.Combine(Path.GetTempPath(), "RazorTargetQolTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(m_TempDir);
            Config.Initialize(m_TempDir);
            Config.LoadLastProfile();

            PacketHandlers.Initialize();
            MacroManager.Stop();
            ActionQueue.Stop();
            Targeting.Reset();
            Config.SetProperty("ObjectDelayEnabled", false);

            World.Clear();
            PlayerData player = new PlayerData(PlayerSerial)
            {
                Position = new Point3D(100, 100, 0),
                Visible = true
            };
            World.AddMobile(player);
            World.Player = player;

            Mobile enemy = new Mobile(EnemySerial)
            {
                Name = "an orc",
                Position = new Point3D(110, 100, 0),
                Notoriety = 6 // murderer -> rot
            };
            World.AddMobile(enemy);

            m_Fake = new FakeClientServices();
            ClientProxy.Bind(m_Fake);
        }

        public void Dispose()
        {
            ClientProxy.Unbind();
            Targeting.Reset();
            StealthSteps.Unhide();
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

        private static void UInt(List<byte> b, uint v)
        {
            b.Add((byte)(v >> 24));
            b.Add((byte)(v >> 16));
            b.Add((byte)(v >> 8));
            b.Add((byte)v);
        }

        // ------------------------------------------------------------ attack overhead

        [Fact]
        public void ShowAttackTarget_zeigt_Overhead_beim_Angriff()
        {
            Config.SetProperty("ShowAttackTargetOverhead", true);
            Config.SetProperty("ShowAttackTargetNewOnly", false);

            // Client sendet 0x05 Attack-Request.
            var b = new List<byte> { 0x05 };
            UInt(b, EnemySerial);
            PacketHandler.OnClientPacket(0x05, new PacketReader(b.ToArray(), false), null);

            byte[] injected = Assert.Single(m_Fake.InjectedToClient);
            Assert.Equal(0xAE, injected[0]);
            string text = Encoding.BigEndianUnicode.GetString(injected, 48, injected.Length - 50);
            Assert.Contains("Attack: an orc", text);
        }

        [Fact]
        public void ShowAttackTarget_NewOnly_meldet_dasselbe_Ziel_nur_einmal()
        {
            Config.SetProperty("ShowAttackTargetOverhead", true);
            Config.SetProperty("ShowAttackTargetNewOnly", true);

            var b = new List<byte> { 0x05 };
            UInt(b, EnemySerial);
            byte[] attack = b.ToArray();

            PacketHandler.OnClientPacket(0x05, new PacketReader(attack, false), null);
            PacketHandler.OnClientPacket(0x05, new PacketReader(attack, false), null);

            Assert.Single(m_Fake.InjectedToClient);
        }

        [Fact]
        public void ShowAttackTarget_aus_zeigt_nichts()
        {
            Config.SetProperty("ShowAttackTargetOverhead", false);

            var b = new List<byte> { 0x05 };
            UInt(b, EnemySerial);
            PacketHandler.OnClientPacket(0x05, new PacketReader(b.ToArray(), false), null);

            Assert.Empty(m_Fake.InjectedToClient);
        }

        // ------------------------------------------------------------ queue actions

        [Fact]
        public void QueueActions_blockt_den_direkten_Doppelklick()
        {
            Item item = new Item(ItemSerial) { ItemID = 0x0E21 };
            World.AddItem(item);

            var b = new List<byte> { 0x06 };
            UInt(b, ItemSerial);

            Config.SetProperty("QueueActions", true);
            bool blocked = PacketHandler.OnClientPacket(0x06, new PacketReader(b.ToArray(), false), null);
            Assert.True(blocked, "Direkter Klick muss geblockt werden");
            // ... aber die Aktion geht ueber die Queue raus (0x06 via SendToServer).
            Assert.Contains(m_Fake.SentToServer, pkt => pkt[0] == 0x06);

            Config.SetProperty("QueueActions", false);
            blocked = PacketHandler.OnClientPacket(0x06, new PacketReader(b.ToArray(), false), null);
            Assert.False(blocked);
        }

        // ------------------------------------------------------------ stealth

        [Fact]
        public void StealthSteps_zaehlt_beim_Gehen_im_Verborgenen()
        {
            Config.SetProperty("CountStealthSteps", true);
            Config.SetProperty("StealthOverhead", false);
            Config.SetProperty("StealthStepsFormat", "Steps: {step}");

            Assistant.Core.SystemMessages.Messages.Clear();
            StealthSteps.Hide();

            StealthSteps.OnMove();
            StealthSteps.OnMove();

            Assert.Equal(2, StealthSteps.Count);
            Assert.True(StealthSteps.Counting);
        }

        [Fact]
        public void StealthSteps_endet_beim_Sichtbarwerden()
        {
            Config.SetProperty("CountStealthSteps", true);
            StealthSteps.Hide();
            StealthSteps.OnMove();
            Assert.True(StealthSteps.Counting);

            StealthSteps.Unhide();
            Assert.False(StealthSteps.Counting);
            Assert.Equal(0, StealthSteps.Count);
        }

        // ------------------------------------------------------------ smart last target

        private const uint HealerSerial = 0x00000E05;

        private Mobile GiveHealer()
        {
            Mobile healer = new Mobile(HealerSerial)
            {
                Name = "a healer",
                Position = new Point3D(105, 100, 0),
                Notoriety = 1 // innocent -> blau
            };
            World.AddMobile(healer);
            return healer;
        }

        /// <summary>Server oeffnet einen Target-Cursor (0x6C) mit Flags.</summary>
        private static void OpenCursor(uint targId, byte flags)
        {
            var t = new List<byte> { 0x6C, 0x00 };
            UInt(t, targId);
            t.Add(flags);
            for (int i = 0; i < 12; i++) t.Add(0);
            PacketHandler.OnServerPacket(0x6C, new PacketReader(t.ToArray(), false), null);
        }

        /// <summary>Spieler beantwortet den Cursor (Client-0x6C) auf ein Mobile.</summary>
        private static void AnswerCursor(uint targId, byte flags, uint serial)
        {
            Mobile m = World.FindMobile(serial);
            var t = new List<byte> { 0x6C, 0x00 }; // type 0 = object
            UInt(t, targId);
            t.Add(flags);
            UInt(t, serial);
            void UShort(ushort v)
            {
                t.Add((byte) (v >> 8));
                t.Add((byte) v);
            }

            UShort((ushort) m.Position.X);
            UShort((ushort) m.Position.Y);
            UShort((ushort) m.Position.Z);
            UShort(m.Body);
            PacketHandler.OnClientPacket(0x6C, new PacketReader(t.ToArray(), false), null);
        }

        private static uint ReadTargetSerial(byte[] pkt)
        {
            // 0x6C: id(1) type(1) targID(4) flags(1) serial(4)
            return (uint) ((pkt[7] << 24) | (pkt[8] << 16) | (pkt[9] << 8) | pkt[10]);
        }

        [Fact]
        public void SmartLastTarget_trennt_harm_und_bene_Ziele()
        {
            Config.SetProperty("SmartLastTarget", true);
            GiveHealer();

            // Harmful-Cursor auf den Ork beantworten, Bene-Cursor auf den Heiler.
            OpenCursor(0xA1, 1);
            AnswerCursor(0xA1, 1, EnemySerial);
            OpenCursor(0xA2, 2);
            AnswerCursor(0xA2, 2, HealerSerial);

            // Neuer harmful Cursor + LastTarget -> der Ork antwortet.
            OpenCursor(0xA3, 1);
            Targeting.LastTarget();
            byte[] harm = m_Fake.SentToServer.FindLast(p => p[0] == 0x6C);
            Assert.Equal(EnemySerial, ReadTargetSerial(harm));

            // Neuer beneficial Cursor + LastTarget -> der Heiler antwortet.
            OpenCursor(0xA4, 2);
            Targeting.LastTarget();
            byte[] bene = m_Fake.SentToServer.FindLast(p => p[0] == 0x6C);
            Assert.Equal(HealerSerial, ReadTargetSerial(bene));
        }

        [Fact]
        public void SmartLastTarget_aus_nimmt_immer_das_letzte_Ziel()
        {
            Config.SetProperty("SmartLastTarget", false);
            GiveHealer();

            OpenCursor(0xA1, 1);
            AnswerCursor(0xA1, 1, EnemySerial);
            OpenCursor(0xA2, 2);
            AnswerCursor(0xA2, 2, HealerSerial); // zuletzt: Heiler

            OpenCursor(0xA3, 1); // harmful Cursor
            Targeting.LastTarget();
            byte[] sent = m_Fake.SentToServer.FindLast(p => p[0] == 0x6C);
            Assert.Equal(HealerSerial, ReadTargetSerial(sent)); // trotzdem der Heiler
        }

        [Fact]
        public void AttackLastTarget_bevorzugt_das_harmful_Ziel()
        {
            Config.SetProperty("SmartLastTarget", true);
            Config.SetProperty("ShowAttackTargetOverhead", false);
            GiveHealer();

            OpenCursor(0xA1, 1);
            AnswerCursor(0xA1, 1, EnemySerial); // harm = Ork
            OpenCursor(0xA2, 2);
            AnswerCursor(0xA2, 2, HealerSerial); // last = Heiler

            Targeting.AttackLastTarg();

            byte[] attack = m_Fake.SentToServer.FindLast(p => p[0] == 0x05);
            Assert.NotNull(attack);
            uint serial = (uint) ((attack[1] << 24) | (attack[2] << 16) | (attack[3] << 8) | attack[4]);
            Assert.Equal(EnemySerial, serial);
        }

        [Fact]
        public void SetLastBeneficial_setzt_nur_das_bene_Ziel()
        {
            Config.SetProperty("SmartLastTarget", true);
            Mobile healer = GiveHealer();
            Mobile enemy = World.FindMobile(EnemySerial);

            Targeting.SetLastTargetBeneficial();
            // OneTimeTarget oeffnet den lokalen Cursor; Antwort auf den Heiler.
            AnswerCursor(Targeting.LocalTargID, 0, HealerSerial);

            Assert.True(Targeting.IsBeneficialTarget(healer));
            Assert.False(Targeting.IsHarmfulTarget(healer));
            Assert.False(Targeting.IsBeneficialTarget(enemy));
        }

        [Fact]
        public void NextTarget_Zuweisung_folgt_OnlyNextPrevBeneficial()
        {
            Config.SetProperty("SmartLastTarget", true);
            Mobile enemy = World.FindMobile(EnemySerial);

            // Ohne Filter: Ziel wird harm UND bene.
            Config.SetProperty("OnlyNextPrevBeneficial", false);
            Config.SetProperty("FriendlyBeneficialOnly", false);
            Config.SetProperty("NonFriendlyHarmfulOnly", false);
            Targeting.NextTarget();
            Assert.True(Targeting.IsHarmfulTarget(enemy));
            Assert.True(Targeting.IsBeneficialTarget(enemy));

            Targeting.Reset();

            // Mit OnlyNextPrevBeneficial: Next/Prev setzt nur noch harm.
            Config.SetProperty("OnlyNextPrevBeneficial", true);
            Targeting.NextTarget();
            Assert.True(Targeting.IsHarmfulTarget(enemy));
            Assert.False(Targeting.IsBeneficialTarget(enemy));
        }

        // ------------------------------------------------------------ range check LT

        [Fact]
        public void RangeCheckLT_feuert_das_wartende_LastTarget_bei_Annaeherung()
        {
            Config.SetProperty("RangeCheckLT", true);
            Config.SetProperty("LTRange", 5);
            Config.SetProperty("QueueTargets", true);

            // Last Target = der Gegner, 10 Felder entfernt (LTRange 5).
            Targeting.SetLastTarget((Serial) EnemySerial);

            // Server oeffnet einen Target-Cursor (0x6C).
            var t = new List<byte> { 0x6C, 0x00 };
            UInt(t, 0x000000AA); // TargetID
            t.Add(0x00);
            for (int i = 0; i < 12; i++) t.Add(0);
            PacketHandler.OnServerPacket(0x6C, new PacketReader(t.ToArray(), false), null);

            // LastTarget-Hotkey: Ziel ausserhalb LTRange -> DoLastTarget lehnt
            // ab und merkt die Aktion (QueueTargets) statt zu senden.
            Targeting.LastTarget();
            Assert.DoesNotContain(m_Fake.SentToServer, pkt => pkt[0] == 0x6C);

            // Der Gegner laeuft heran -> CheckLastTargetRange feuert die
            // wartende Aktion, die Target-Antwort geht raus.
            Mobile enemy = World.FindMobile(EnemySerial);
            enemy.Position = new Point3D(102, 100, 0);
            Targeting.CheckLastTargetRange(enemy);

            Assert.Contains(m_Fake.SentToServer, pkt => pkt[0] == 0x6C);
        }
    }
}
