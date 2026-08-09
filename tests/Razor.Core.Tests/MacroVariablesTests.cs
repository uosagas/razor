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

// UOSagas-Razor: Tests fuer MacroVariables (CE-Profilsektion "macrovariables").
// Das XML-Format ist exakt Razor CE (Attribute type/flags/serial/x/y/z/gfx/name),
// damit alte CE-Profile kompatibel bleiben. Einzige bewusste Abweichung:
// Save schreibt Z korrekt (CE-Bug schrieb X als z) — der Load liest bei alten
// CE-Dateien denselben Attributnamen, bleibt also kompatibel.

using System;
using System.IO;
using System.Text;
using System.Xml;
using Assistant;
using Assistant.Macros;
using Xunit;

namespace Razor.Core.Tests
{
    public class MacroVariablesTests : IDisposable
    {
        public MacroVariablesTests()
        {
            MacroVariables.MacroVariableList.Clear();
        }

        public void Dispose()
        {
            MacroVariables.MacroVariableList.Clear();
        }

        private static TargetInfo MakeTarget(uint serial)
        {
            return new TargetInfo
            {
                Type = 0,
                Flags = 0,
                Serial = (Serial) serial,
                X = 1000,
                Y = 2000,
                Z = 5,
                Gfx = 3702
            };
        }

        [Fact]
        public void AddOrUpdate_Find_Remove()
        {
            MacroVariables.AddOrUpdate("mybag", MakeTarget(0x40123456));

            MacroVariables.MacroVariable found = MacroVariables.Find("MYBAG"); // case-insensitive
            Assert.NotNull(found);
            Assert.Equal((Serial) 0x40123456u, found.TargetInfo.Serial);

            // Gleicher Name -> Ziel ersetzen, kein Duplikat.
            MacroVariables.AddOrUpdate("mybag", MakeTarget(0x40AABBCC));
            Assert.Single(MacroVariables.MacroVariableList);
            Assert.Equal((Serial) 0x40AABBCCu, MacroVariables.Find("mybag").TargetInfo.Serial);

            MacroVariables.Remove("mybag");
            Assert.Empty(MacroVariables.MacroVariableList);
        }

        [Fact]
        public void SaveLoad_Roundtrip_im_CE_Format()
        {
            MacroVariables.AddOrUpdate("mybag", MakeTarget(0x40123456));
            MacroVariables.AddOrUpdate("healer", MakeTarget(0x00001234));

            // Save wie Profile.Save: Sektion-Element mit macrovariable-Kindern.
            var sb = new StringBuilder();
            using (XmlWriter xml = XmlWriter.Create(sb, new XmlWriterSettings { OmitXmlDeclaration = true }))
            {
                xml.WriteStartElement("macrovariables");
                MacroVariables.Save(xml);
                xml.WriteEndElement();
            }

            string savedXml = sb.ToString();
            Assert.Contains("macrovariable", savedXml);
            Assert.Contains("name=\"mybag\"", savedXml);
            Assert.Contains("serial=\"0x40123456\"", savedXml);
            Assert.Contains("z=\"5\"", savedXml); // CE-Bug (X als z) ist hier gefixt

            // Load aus dem gespeicherten XML.
            var doc = new XmlDocument();
            doc.LoadXml(savedXml);
            MacroVariables.Load(doc.DocumentElement);

            Assert.Equal(2, MacroVariables.MacroVariableList.Count);

            MacroVariables.MacroVariable mybag = MacroVariables.Find("mybag");
            Assert.NotNull(mybag);
            Assert.Equal((Serial) 0x40123456u, mybag.TargetInfo.Serial);
            Assert.Equal(1000, mybag.TargetInfo.X);
            Assert.Equal(2000, mybag.TargetInfo.Y);
            Assert.Equal(5, mybag.TargetInfo.Z);
            Assert.Equal(3702, mybag.TargetInfo.Gfx);
        }

        [Fact]
        public void Actions_loesen_Variable_lazy_auf_und_serialisieren_roh()
        {
            // Variable existiert beim Konstruieren NICHT — der Name darf trotzdem
            // nie gemangelt werden (CE machte "?name?" daraus und zerstoerte beim
            // Save die Macro-Datei, wenn das Profil spaeter lud).
            var setVar = new SetMacroVariableTargetAction(new[] { "Assistant.Macros.SetMacroVariableTargetAction", "later" });
            var absTarget = new AbsoluteTargetVariableAction(new[] { "Assistant.Macros.AbsoluteTargetVariableAction", "later" });
            var dclick = new DoubleClickVariableAction(new[] { "Assistant.Macros.DoubleClickVariableAction", "later" });

            Assert.Equal("later", setVar.VariableName);
            Assert.EndsWith("|later", setVar.Serialize());
            Assert.EndsWith("|later", absTarget.Serialize());
            Assert.EndsWith("|later", dclick.Serialize());
        }
    }
}
