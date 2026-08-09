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

// UOSagas-Razor: Tests fuer Phase 3c2 — voller Razor-CE-Hotkey-Umfang.
//
//  * Registrierungs-Inventur: Kategorien-Zaehlung gegen das CE-Soll
//    (Spells 186 = 182 spells.def + 4 Helfer; Skills 22; Targets >= 89;
//    Items 14; Misc >= 33; Dress >= 8; gesamt >= 350).
//  * Stichproben: Spell-Hotkey -> 0x12/0x56 (CastSpellFromMacro),
//    Skill-Hotkey -> 0x12/0x24 (UseSkill), Potion-Hotkey -> 0x06
//    (DoubleClick auf Fixture-Potion im Backpack), Bandage Self ->
//    0x06 + Target-Queue beantwortet den naechsten Server-Cursor (0x6C).

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Assistant;
using Assistant.Agents;
using Assistant.HotKeys;
using Xunit;

namespace Razor.Core.Tests
{
    [Collection("ConfigSequential")]
    public class HotKeyRegistryTests : IDisposable
    {
        private const uint PlayerSerial = 0x00000901;
        private const uint BackpackSerial = 0x40000902;

        private readonly string m_TempDir;
        private readonly CultureInfo m_OldCulture;
        private readonly FakeClientServices m_Fake;
        private readonly PlayerData m_Player;

        public HotKeyRegistryTests()
        {
            m_OldCulture = CultureInfo.CurrentCulture;
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;

            m_TempDir = Path.Combine(Path.GetTempPath(), "RazorHotKeyRegistryTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(m_TempDir);

            Config.Initialize(m_TempDir);
            Config.LoadLastProfile();

            // Alle Registrierungs-Initializer wie RazorPlugin.Initialize
            // (idempotent; Reihenfolge wie im Plugin).
            Counter.Initialize();
            Agent.Initialize();
            DressList.Initialize();
            HotKey.Initialize();
            PacketHandlers.Initialize();
            UndressHotKeys.Initialize();
            Spell.Initialize();
            SkillHotKeys.Initialize();
            UseHotKeys.Initialize();
            SpecialMoves.Initialize();

            Assistant.Macros.MacroManager.Stop();
            ActionQueue.Stop();
            Targeting.Reset();

            HotKey.ClearAll();
            HotKey.Enabled = true;

            World.Clear();
            m_Player = new PlayerData(PlayerSerial);
            m_Player.Position = new Point3D(1000, 1000, 0);
            World.AddMobile(m_Player);
            World.Player = m_Player;

            m_Fake = new FakeClientServices();
            ClientProxy.Bind(m_Fake);
        }

        public void Dispose()
        {
            Assistant.Macros.MacroManager.Stop();
            ActionQueue.Stop();
            Targeting.Reset();

            HotKey.ClearAll();
            HotKey.Enabled = true;

            World.Clear();
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

        // ---- Helpers ---------------------------------------------------------

        private Item CreateBackpack()
        {
            Item pack = new Item(BackpackSerial);
            pack.ItemID = 0x0E75;
            pack.Layer = Layer.Backpack;
            World.AddItem(pack);
            pack.Container = m_Player;
            return pack;
        }

        private Item CreateItemIn(Item container, uint serial, ushort itemId, ushort amount = 1)
        {
            Item item = new Item(serial);
            item.ItemID = itemId;
            item.Amount = amount;
            World.AddItem(item);
            item.Container = container;
            return item;
        }

        private IEnumerable<byte[]> SentWithId(byte id)
        {
            return m_Fake.SentToServer.Where(b => b.Length > 0 && b[0] == id);
        }

        /// <summary>Hotkey belegen und ueber den Dispatch feuern (wie im Spiel).</summary>
        private void Fire(KeyData kd, Keys key, ModKeys mod = ModKeys.None)
        {
            Assert.NotNull(kd);
            kd.Key = (int) key;
            kd.Mod = mod;
            Assert.False(HotKey.OnKeyDown((int) key, mod)); // verschluckt (PassToUO aus)
        }

        private static uint ReadUInt(byte[] data, int offset)
        {
            return (uint) ((data[offset] << 24) | (data[offset + 1] << 16) | (data[offset + 2] << 8) |
                           data[offset + 3]);
        }

        private static string ReadAsciiNull(byte[] data, int offset)
        {
            var sb = new System.Text.StringBuilder();
            for (int i = offset; i < data.Length && data[i] != 0; i++)
                sb.Append((char) data[i]);
            return sb.ToString();
        }

        // ---- Inventur ---------------------------------------------------------

        [Fact]
        public void RegistrierungsUmfangEntsprichtRazorCeSoll()
        {
            List<KeyData> list = HotKey.List;

            // Spells: 182 aus spells.def + HealOrCure/MiniHeal/GHeal/Interrupt.
            Assert.Equal(182, Spell.Count);
            Assert.Equal(186, list.Count(k => k.Category == HKCategory.Spells));

            // Zirkel-Verteilung wie spells.def (8 je Magery-Zirkel usw.).
            Assert.Equal(8, list.Count(k => k.SubCat == HKSubCat.FirstC));
            Assert.Equal(8, list.Count(k => k.SubCat == HKSubCat.EighthC));
            Assert.Equal(17, list.Count(k => k.SubCat == HKSubCat.NecroC));
            Assert.Equal(10, list.Count(k => k.SubCat == HKSubCat.PaladinC));
            Assert.Equal(6, list.Count(k => k.SubCat == HKSubCat.BushidoC));
            Assert.Equal(8, list.Count(k => k.SubCat == HKSubCat.NinjisuC));
            Assert.Equal(16, list.Count(k => k.SubCat == HKSubCat.SpellWeaveC));
            Assert.Equal(16, list.Count(k => k.SubCat == HKSubCat.MystC));
            Assert.Equal(45, list.Count(k => k.SubCat == HKSubCat.MasteriesC));

            // Skills: die 22 Use-Skills.
            Assert.Equal(22, SkillHotKeys.Count);
            Assert.Equal(22, list.Count(k => k.Category == HKCategory.Skills));

            // Items: Bandage Self/LT, Use Hand/Right/Left, Use Bandage,
            // 7 Potions + Enchanted Apple.
            Assert.Equal(14, list.Count(k => k.Category == HKCategory.Items));

            // Targets: 7 Basis + 22 Closest + 22 Random + 38 Next/Prev
            // (+2 Ignore-Agent). Friend-Listen-Varianten fehlen bewusst.
            Assert.True(list.Count(k => k.Category == HKCategory.Targets) >= 89,
                $"Targets: {list.Count(k => k.Category == HKCategory.Targets)}");

            // Misc: 25 aus UseHotKeys (inkl. PetCommands/Counter) + 8 SpecialMoves.
            Assert.True(list.Count(k => k.Category == HKCategory.Misc) >= 33,
                $"Misc: {list.Count(k => k.Category == HKCategory.Misc)}");

            // Dress: Arm/Disarm + Undress-Varianten.
            Assert.True(list.Count(k => k.Category == HKCategory.Dress) >= 8);

            // Gesamtumfang (Razor CE registriert statisch ~390; der Port laesst
            // dokumentiert ~25 UI-/Infrastruktur-Hotkeys aus).
            Assert.True(list.Count >= 350, $"Gesamt: {list.Count}");

            // Stichproben: markante Eintraege existieren.
            Assert.NotNull(HotKey.Get((int) LocString.AttackLastComb));
            Assert.NotNull(HotKey.Get((int) LocString.Resync));
            Assert.NotNull(HotKey.Get((int) LocString.DrinkHeal));
            Assert.NotNull(HotKey.Get((int) LocString.BandageSelf));
            Assert.NotNull(HotKey.Get((int) LocString.TargClosest));
            Assert.NotNull(HotKey.Get((int) LocString.NextTarget));
            Assert.NotNull(HotKey.Get((int) LocString.ToggleStun));
            Assert.NotNull(HotKey.Get((int) LocString.AllCome));
            Assert.NotNull(HotKey.Get(3002014)); // Heal (Magery 1/4, Cliloc)
            Assert.NotNull(HotKey.Get(1044060 + 21)); // Hiding (Skill-Cliloc)

            // Anzeige-Namen kommen aus der eingebetteten Tabelle.
            Assert.Equal("Heal", HotKey.Get(3002014).DisplayName);
            Assert.Equal("Hiding", HotKey.Get(1044060 + 21).DisplayName);
        }

        // ---- Stichproben ---------------------------------------------------------

        [Fact]
        public void SpellHotkeySendetCastSpellFromMacro()
        {
            // Heal = Magery 1. Zirkel Nr. 4 -> Spell-ID 4, Cliloc 3002014.
            Fire(HotKey.Get(3002014), Keys.F2);

            byte[] pkt = Assert.Single(SentWithId(0x12));
            Assert.Equal(0x56, pkt[3]); // Sub-Kommando CastSpellFromMacro
            Assert.Equal("4", ReadAsciiNull(pkt, 4));

            Assert.Equal(4, World.Player.LastSpell);
        }

        [Fact]
        public void SkillHotkeySendetUseSkill()
        {
            // Hiding = Skill-Index 21 (Cliloc 1044081).
            Fire(HotKey.Get(1044060 + 21), Keys.F3);

            byte[] pkt = Assert.Single(SentWithId(0x12));
            Assert.Equal(0x24, pkt[3]); // Sub-Kommando UseSkill
            Assert.Equal("21 0", ReadAsciiNull(pkt, 4));

            Assert.Equal(21, World.Player.LastSkill);
        }

        [Fact]
        public void PotionHotkeyDoppelklicktPotionImBackpack()
        {
            Item pack = CreateBackpack();
            Item potion = CreateItemIn(pack, 0x40000910, 3852); // Heal Potion

            Fire(HotKey.Get((int) LocString.DrinkHeal), Keys.F4);

            byte[] pkt = Assert.Single(SentWithId(0x06));
            Assert.Equal(potion.Serial.Value, ReadUInt(pkt, 1));
        }

        [Fact]
        public void PotionHotkeyOhnePotionSendetNichts()
        {
            CreateBackpack(); // leer

            Fire(HotKey.Get((int) LocString.DrinkCure), Keys.F6);

            Assert.Empty(SentWithId(0x06));
        }

        [Fact]
        public void BandageSelfDoppelklicktBandageUndBeantwortetTargetCursor()
        {
            Item pack = CreateBackpack();
            Item bandage = CreateItemIn(pack, 0x40000911, 3617); // Bandagen

            Fire(HotKey.Get((int) LocString.BandageSelf), Keys.F5);

            // 0x06-DoubleClick auf die Bandagen.
            byte[] dclick = Assert.Single(SentWithId(0x06));
            Assert.Equal(bandage.Serial.Value, ReadUInt(dclick, 1));

            // Der Server schickt den Heil-Cursor (0x6C) — die Target-Queue
            // beantwortet ihn sofort mit Target Self und blockt den Cursor.
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

            byte[] resp = Assert.Single(SentWithId(0x6C));
            Assert.Equal(0, resp[1]); // Objekt-Target
            Assert.Equal(0xDEADBEEFu, ReadUInt(resp, 2)); // Target-ID des Servers
            Assert.Equal(PlayerSerial, ReadUInt(resp, 7)); // Ziel: der Spieler selbst
        }

        [Fact]
        public void SetLastTargetToBedientOffenenCursorUndAttackLastTargGreiftAn()
        {
            // Gegner in Reichweite.
            Mobile enemy = new Mobile(0x00000920);
            enemy.Position = new Point3D(1002, 1002, 0);
            enemy.Notoriety = (byte) Targeting.TargetType.Murderer;
            World.AddMobile(enemy);

            // "Target Closest Murderer" setzt Last Target + Kombattant.
            Fire(HotKey.Get((int) LocString.TargCloseRed), Keys.F7);

            // ChangeCombatant (0xAA) wurde in den Client injiziert.
            Assert.Contains(m_Fake.InjectedToClient, p => p.Length > 0 && p[0] == 0xAA);

            // Attack Last Target -> 0x05 mit der Gegner-Serial.
            Fire(HotKey.Get((int) LocString.AttackLastTarg), Keys.F8);

            byte[] attack = Assert.Single(SentWithId(0x05));
            Assert.Equal(enemy.Serial.Value, ReadUInt(attack, 1));
        }

        [Fact]
        public void PetKommandoSendetEncodedSpeechMitKeyword()
        {
            // "All Kill" -> 0xAD Encoded mit Keyword 0x168 (UOSagas BaseAI).
            Fire(HotKey.Get((int) LocString.AllKill), Keys.F9);

            byte[] pkt = Assert.Single(SentWithId(0xAD));

            // Typ = Regular | Encoded (0xC0).
            Assert.Equal((byte) (MessageType.Regular | MessageType.Encoded), pkt[3]);

            // 12-Bit-Packing: Anzahl 1, Keyword 0x168 -> Bytes 0x00 0x11 0x68
            // hinter dem 4-Byte-Sprachkuerzel (Offset 8).
            int off = 3 + 1 + 2 + 2 + 4; // id+len, typ, hue, font, lang
            Assert.Equal(0x00, pkt[off]);
            Assert.Equal(0x11, pkt[off + 1]);
            Assert.Equal(0x68, pkt[off + 2]);

            // Klartext folgt UTF8-nullterminiert.
            Assert.Equal("All Kill", ReadAsciiNull(pkt, off + 3));
        }

        [Fact]
        public void ResyncHotkeySendet0x22()
        {
            Fire(HotKey.Get((int) LocString.Resync), Keys.F10);

            byte[] pkt = Assert.Single(SentWithId(0x22));
            Assert.Equal(3, pkt.Length);
        }
    }
}
