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

// UOSagas-Razor: Kompatibilitaetstests fuer das Razor-CE-Profilformat.
// Fixture-Format entspricht Razor CE Config.cs Save():
//   <profile><property name="..." type="...">value</property><agents>...</agents>...</profile>

using System;
using System.Globalization;
using System.IO;
using System.Xml;
using Assistant;
using Xunit;

namespace Razor.Core.Tests
{
    [Collection("ConfigSequential")]
    public class ProfileCompatibilityTests : IDisposable
    {
        // Repraesentatives Razor-CE-Profil: 16 Properties verschiedener Typen,
        // eine -null--Property, plus agents-/hotkeys-/sagascustom-Sektionen.
        private const string ProfileFixture =
            "<?xml version=\"1.0\" encoding=\"utf-8\" standalone=\"yes\"?>\n" +
            "<profile>\n" +
            "\t<property name=\"ShowMobNames\" type=\"System.Boolean\">True</property>\n" +
            "\t<property name=\"ShowCorpseNames\" type=\"System.Boolean\">False</property>\n" +
            "\t<property name=\"TitleBarText\" type=\"System.String\">UO - {char} on Sagas</property>\n" +
            "\t<property name=\"CounterWarnAmount\" type=\"System.Int32\">10</property>\n" +
            "\t<property name=\"ObjectDelay\" type=\"System.Int32\">750</property>\n" +
            "\t<property name=\"SysColor\" type=\"System.Int32\">88</property>\n" +
            "\t<property name=\"FilterDelay\" type=\"System.Double\">7.5</property>\n" +
            "\t<property name=\"LTRange\" type=\"System.Int32\">15</property>\n" +
            "\t<property name=\"HealthFmt\" type=\"System.String\">[{0}hp]</property>\n" +
            "\t<property name=\"SmartLastTarget\" type=\"System.Boolean\">True</property>\n" +
            "\t<property name=\"MessageLevel\" type=\"System.Int32\">1</property>\n" +
            "\t<property name=\"ForceIP\" type=\"System.String\">127.0.0.1</property>\n" +
            "\t<property name=\"ForcePort\" type=\"System.Int32\">2593</property>\n" +
            "\t<property name=\"Opacity\" type=\"System.Int32\">85</property>\n" +
            "\t<property name=\"GrabHotBag\" type=\"System.String\">1074121353</property>\n" +
            "\t<property name=\"Season\" type=\"System.Int32\">2</property>\n" +
            "\t<property name=\"LTHilight\" type=\"-null-\" />\n" +
            "\t<agents>\n" +
            "\t\t<agent type=\"Assistant.Agents.UseOnceAgent\" num=\"1\">\n" +
            "\t\t\t<item serial=\"1074121353\" />\n" +
            "\t\t\t<item serial=\"1080000001\" />\n" +
            "\t\t</agent>\n" +
            "\t\t<agent type=\"Assistant.Agents.SellAgent\" num=\"1\">\n" +
            "\t\t\t<hotbag>1082345678</hotbag>\n" +
            "\t\t\t<item id=\"6584\" />\n" +
            "\t\t</agent>\n" +
            "\t</agents>\n" +
            "\t<hotkeys>\n" +
            "\t\t<key mod=\"0\" key=\"112\" send=\"False\">L:1088</key>\n" +
            "\t\t<key mod=\"2\" key=\"66\" send=\"False\">S:Macro-Bandage</key>\n" +
            "\t</hotkeys>\n" +
            "\t<sagascustom>\n" +
            "\t\t<entry name=\"future\" value=\"42\" />\n" +
            "\t</sagascustom>\n" +
            "</profile>\n";

        private readonly string m_TempDir;
        private readonly CultureInfo m_OldCulture;

        public ProfileCompatibilityTests()
        {
            m_OldCulture = CultureInfo.CurrentCulture;
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;

            m_TempDir = Path.Combine(Path.GetTempPath(), "RazorCoreTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(m_TempDir);
            Config.RootPath = m_TempDir;
            ProfileSections.Clear();
        }

        public void Dispose()
        {
            ProfileSections.Clear();
            CultureInfo.CurrentCulture = m_OldCulture;
            try
            {
                Directory.Delete(m_TempDir, true);
            }
            catch
            {
            }
        }

        private string WriteFixtureProfile(string name)
        {
            string dir = Config.GetUserDirectory("Profiles");
            string file = Path.Combine(dir, name + ".xml");
            File.WriteAllText(file, ProfileFixture);
            return file;
        }

        [Fact]
        public void Load_ReadsAllPropertyTypes()
        {
            WriteFixtureProfile("fixture");

            Profile p = new Profile("fixture");
            Assert.True(p.Load());

            Assert.Equal(true, p.GetProperty("ShowMobNames"));
            Assert.Equal(false, p.GetProperty("ShowCorpseNames"));
            Assert.Equal("UO - {char} on Sagas", p.GetProperty("TitleBarText"));
            Assert.Equal(10, p.GetProperty("CounterWarnAmount"));
            Assert.Equal(750, p.GetProperty("ObjectDelay"));
            Assert.Equal(88, p.GetProperty("SysColor"));
            Assert.Equal(7.5, p.GetProperty("FilterDelay"));
            Assert.Equal(15, p.GetProperty("LTRange"));
            Assert.Equal("[{0}hp]", p.GetProperty("HealthFmt"));
            Assert.Equal(true, p.GetProperty("SmartLastTarget"));
            Assert.Equal(1, p.GetProperty("MessageLevel"));
            Assert.Equal("127.0.0.1", p.GetProperty("ForceIP"));
            Assert.Equal(2593, p.GetProperty("ForcePort"));
            Assert.Equal(85, p.GetProperty("Opacity"));
            Assert.Equal("1074121353", p.GetProperty("GrabHotBag"));
            Assert.Equal(2, p.GetProperty("Season"));

            // "-null-"-Property wird entfernt (Original-Semantik)
            Assert.False(p.HasProperty("LTHilight"));
        }

        [Fact]
        public void LoadSaveLoad_PropertyValuesIdentical()
        {
            WriteFixtureProfile("roundtrip");

            Profile p1 = new Profile("roundtrip");
            Assert.True(p1.Load());
            p1.Save();
            p1.Unload();

            Profile p2 = new Profile("roundtrip");
            Assert.True(p2.Load());

            string[] props =
            {
                "ShowMobNames", "ShowCorpseNames", "TitleBarText", "CounterWarnAmount", "ObjectDelay",
                "SysColor", "FilterDelay", "LTRange", "HealthFmt", "SmartLastTarget", "MessageLevel",
                "ForceIP", "ForcePort", "Opacity", "GrabHotBag", "Season"
            };

            foreach (string prop in props)
            {
                Assert.Equal(p1.GetProperty(prop), p2.GetProperty(prop));
            }

            p2.Unload();
        }

        [Fact]
        public void LoadSave_UnknownSectionsPreserved()
        {
            WriteFixtureProfile("sections");

            Profile p1 = new Profile("sections");
            Assert.True(p1.Load());

            // agents, hotkeys und sagascustom sind unregistriert -> roh konserviert
            Assert.Equal(3, p1.UnknownSections.Count);
            Assert.Equal("agents", p1.UnknownSections[0].Key);
            Assert.Equal("hotkeys", p1.UnknownSections[1].Key);
            Assert.Equal("sagascustom", p1.UnknownSections[2].Key);

            p1.Save();
            p1.Unload();

            // Gespeicherte Datei erneut parsen: Sektionsinhalte muessen erhalten sein
            string file = Path.Combine(Config.GetUserDirectory("Profiles"), "sections.xml");
            XmlDocument doc = new XmlDocument();
            doc.Load(file);

            XmlElement root = doc["profile"];
            Assert.NotNull(root);

            XmlElement agents = root["agents"];
            Assert.NotNull(agents);
            XmlNodeList agentNodes = agents.GetElementsByTagName("agent");
            Assert.Equal(2, agentNodes.Count);
            Assert.Equal("Assistant.Agents.UseOnceAgent", ((XmlElement) agentNodes[0]).GetAttribute("type"));
            Assert.Equal(2, ((XmlElement) agentNodes[0]).GetElementsByTagName("item").Count);
            Assert.Equal("1074121353",
                ((XmlElement) ((XmlElement) agentNodes[0]).GetElementsByTagName("item")[0]).GetAttribute("serial"));

            XmlElement hotkeys = root["hotkeys"];
            Assert.NotNull(hotkeys);
            XmlNodeList keyNodes = hotkeys.GetElementsByTagName("key");
            Assert.Equal(2, keyNodes.Count);
            Assert.Equal("L:1088", keyNodes[0].InnerText);
            Assert.Equal("S:Macro-Bandage", keyNodes[1].InnerText);

            XmlElement custom = root["sagascustom"];
            Assert.NotNull(custom);
            Assert.Equal("42", ((XmlElement) custom.GetElementsByTagName("entry")[0]).GetAttribute("value"));

            // Zweiter Roundtrip: Sektionen bleiben weiterhin erhalten
            Profile p2 = new Profile("sections");
            Assert.True(p2.Load());
            Assert.Equal(3, p2.UnknownSections.Count);
            for (int i = 0; i < p1.UnknownSections.Count; i++)
            {
                Assert.Equal(p1.UnknownSections[i].Key, p2.UnknownSections[i].Key);
                Assert.Equal(p1.UnknownSections[i].Value, p2.UnknownSections[i].Value);
            }

            p2.Unload();
        }

        [Fact]
        public void RegisteredSectionHandler_ReceivesElementAndWritesContent()
        {
            WriteFixtureProfile("handler");

            XmlElement received = null;
            ProfileSections.Register("hotkeys",
                el => received = el,
                xml => xml.WriteElementString("key", "L:9999"));

            try
            {
                Profile p = new Profile("handler");
                Assert.True(p.Load());

                // Handler bekommt die Sektion, sie landet NICHT in UnknownSections
                Assert.NotNull(received);
                Assert.Equal(2, received.GetElementsByTagName("key").Count);
                Assert.Equal(2, p.UnknownSections.Count); // nur agents + sagascustom

                p.Save();
                p.Unload();

                XmlDocument doc = new XmlDocument();
                doc.Load(Path.Combine(Config.GetUserDirectory("Profiles"), "handler.xml"));
                XmlElement hotkeys = doc["profile"]["hotkeys"];
                Assert.NotNull(hotkeys);
                Assert.Equal("L:9999", hotkeys.InnerText);
            }
            finally
            {
                ProfileSections.Unregister("hotkeys");
            }
        }

        [Fact]
        public void MissingProfile_LoadReturnsFalse()
        {
            Profile p = new Profile("does-not-exist");
            Assert.False(p.Load());
        }

        [Fact]
        public void UnknownProperty_IsDropped_LikeRazorCe()
        {
            string dir = Config.GetUserDirectory("Profiles");
            File.WriteAllText(Path.Combine(dir, "unknownprop.xml"),
                "<?xml version=\"1.0\" encoding=\"utf-8\" standalone=\"yes\"?>\n" +
                "<profile>\n" +
                "\t<property name=\"NotARealRazorProperty\" type=\"System.Int32\">123</property>\n" +
                "</profile>\n");

            Profile p = new Profile("unknownprop");
            Assert.True(p.Load());
            Assert.False(p.HasProperty("NotARealRazorProperty"));
        }
    }
}
