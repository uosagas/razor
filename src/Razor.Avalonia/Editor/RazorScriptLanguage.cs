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

// UOSagas-Razor: Sprachdefinition fuer die Razor-Script-Sprache.
//
// Liefert das AvaloniaEdit-Highlighting (Farben/Regeln aus der eingebetteten
// RazorScript.xshd) und die Autocomplete-Vorschlaege, mit Tooltips aus
// ScriptCommandHelp.
//
// WICHTIG — Command-/Expression-/Alias-Namen werden NICHT hier gepflegt:
// sie kommen aus den echten Registrierungen (Interpreter.Registered*), genau
// wie die Doku-Website (D15). Vorher standen sie doppelt hartkodiert (einmal
// in der .xshd, einmal als Array hier) und beide Kopien sind gedriftet:
// 14 registrierte Eintraege fehlten (setskill, find, gumpexists, counttype,
// bandaging, ...), und drei Commands wurden eingefaerbt, die es im Port gar
// nicht gibt (dressconfig, dclickvar, waitforstat). Der Editor darf weder
// Vorhandenes verschweigen noch Nichtvorhandenes versprechen.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;
using Assistant.Scripts.Engine;
using AvaloniaEdit.Highlighting;
using AvaloniaEdit.Highlighting.Xshd;

namespace Razor.UI.Editor
{
    public sealed class RazorScriptLanguage : ILanguageDefinition
    {
        public static readonly RazorScriptLanguage Instance = new RazorScriptLanguage();

        public string Name => "Razor Script";
        public string LineCommentPrefix => "//";

        private IHighlightingDefinition _highlighting;
        public IHighlightingDefinition Highlighting => _highlighting ??= LoadHighlighting();

        // Grammatik, nicht registrierbar — bleibt hier (Lexer kennt sie fest).
        private static readonly string[] Keywords =
        {
            "if", "elseif", "else", "endif", "while", "endwhile", "for", "foreach",
            "endfor", "break", "continue", "stop", "replay", "loop", "not", "and", "or", "as", "in"
        };

        private static string[] Commands => Interpreter.RegisteredCommands.OrderBy(s => s).ToArray();
        private static string[] Expressions => Interpreter.RegisteredExpressions.OrderBy(s => s).ToArray();
        private static string[] AliasNames => Interpreter.RegisteredAliases.OrderBy(s => s).ToArray();

        private List<CompletionEntry> _all;

        private List<CompletionEntry> BuildAll()
        {
            var list = new List<CompletionEntry>();

            foreach (var k in Keywords)
                list.Add(new CompletionEntry(k, "keyword", "Control-flow / operator keyword"));

            foreach (var c in Commands)
            {
                string desc = ScriptCommandHelp.Commands.TryGetValue(c, out var help)
                    ? help.ToTooltip()
                    : "Razor script command";
                list.Add(new CompletionEntry(c, "command", desc));
            }

            foreach (var e in Expressions)
            {
                string desc = ScriptCommandHelp.Commands.TryGetValue(e, out var help)
                    ? help.ToTooltip()
                    : "Razor script expression (usable in if/while/for)";
                list.Add(new CompletionEntry(e, "expression", desc));
            }

            foreach (var a in AliasNames)
                list.Add(new CompletionEntry(a, "alias", "Built-in alias (a serial you can pass to commands)"));

            return list;
        }

        public IEnumerable<CompletionEntry> GetCompletions(string prefix)
        {
            _all ??= BuildAll();

            if (string.IsNullOrEmpty(prefix))
                return _all;

            return _all.Where(e => e.Text.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                       .OrderBy(e => e.Text, StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Autoformat: rueckt Bloecke nach Kontrollfluss ein (4 Spaces) —
        /// if/while/for/foreach erhoehen, elseif/else/end* senken.
        /// </summary>
        public string Format(string text)
        {
            string[] lines = (text ?? string.Empty).Replace("\r\n", "\n").Split('\n');
            var result = new System.Text.StringBuilder();
            int indent = 0;

            for (int n = 0; n < lines.Length; n++)
            {
                string trimmed = lines[n].Trim();

                if (trimmed.Length == 0)
                {
                    result.AppendLine();
                    continue;
                }

                int space = trimmed.IndexOf(' ');
                string word = (space < 0 ? trimmed : trimmed.Substring(0, space)).ToLowerInvariant();

                bool dedentThis = word is "endif" or "endwhile" or "endfor" or "elseif" or "else";
                bool indentNext = word is "if" or "while" or "for" or "foreach" or "elseif" or "else";

                int level = indent - (dedentThis ? 1 : 0);
                if (level < 0)
                    level = 0;

                result.Append(new string(' ', level * 4)).Append(trimmed);
                if (n < lines.Length - 1)
                    result.AppendLine();

                indent = indentNext ? level + 1 : level;
            }

            return result.ToString();
        }

        private static IHighlightingDefinition LoadHighlighting()
        {
            try
            {
                var asm = typeof(RazorScriptLanguage).Assembly;
                // RootNamespace = Razor.UI -> Ressourcenname = Razor.UI.Editor.RazorScript.xshd
                using var stream = asm.GetManifestResourceStream("Razor.UI.Editor.RazorScript.xshd");
                if (stream == null)
                {
                    Console.WriteLine("[UOSagas Razor] RazorScript.xshd nicht gefunden (EmbeddedResource).");
                    return null;
                }

                // Die .xshd liefert Farben, Kommentar-/String-Spans, Grammatik-
                // Keywords, Wertwoerter (Layer/Noto/Richtungen) und die Zahl-Regeln.
                // Die Command-/Expression-/Alias-Woerter setzen wir hier ein, damit
                // sie nicht von den Registrierungen abweichen koennen.
                var doc = new XmlDocument();
                doc.Load(stream);

                FillWords(doc, "Command", Commands);
                FillWords(doc, "Expression", Expressions);
                // Aliase sind Serials, keine Aufrufe -> Wert-Farbe, wie die
                // Layer-Woerter (und wie tok-n auf der Website).
                FillWords(doc, "Layer", AliasNames, append: true);

                using var reader = new XmlNodeReader(doc);
                return HighlightingLoader.Load(reader, HighlightingManager.Instance);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[UOSagas Razor] Highlighting laden fehlgeschlagen: {ex}");
                return null;
            }
        }

        /// <summary>
        /// Ersetzt (oder ergaenzt) die &lt;Word&gt;-Liste der &lt;Keywords color="..."&gt;-Gruppe.
        /// Leere Registrierungen lassen die Datei unangetastet: dann ist der
        /// Interpreter noch nicht initialisiert und die Vorlage ist besser als nichts.
        /// </summary>
        private static void FillWords(XmlDocument doc, string color, string[] words, bool append = false)
        {
            if (words == null || words.Length == 0)
                return;

            var group = doc.DocumentElement?.SelectSingleNode(
                $"//*[local-name()='Keywords'][@color='{color}']") as XmlElement;

            if (group == null)
                return;

            var existing = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (append)
            {
                foreach (XmlNode child in group.ChildNodes)
                    existing.Add(child.InnerText);
            }
            else
            {
                group.RemoveAll();
                // RemoveAll() loescht auch das color-Attribut.
                group.SetAttribute("color", color);
            }

            string ns = doc.DocumentElement.NamespaceURI;

            foreach (string word in words)
            {
                if (!existing.Add(word))
                    continue;

                var element = doc.CreateElement("Word", ns);
                element.InnerText = word;
                group.AppendChild(element);
            }
        }
    }
}
