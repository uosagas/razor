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

// UOSagas-Razor: Sprach-Export fuer die Doku-Website (razor.uosagas.com).
//
// WARUM: Doku, die von Hand gepflegt wird, driftet garantiert vom Code weg.
// Dieses Tool nimmt die EINZIGE Quelle der Wahrheit — die tatsaechlichen
// Registrierungen in Razor.Core — und verheiratet sie mit den kuratierten
// Doku-Texten (language-docs.json, Inhalte aus razorce.com/guide und
// wiki.uooutlands.com/Razor_Scripting).
//
// Ergebnis: razor-language.json  (Inventar + Texte + Support-Status)
//
// Der Abgleich ist der eigentliche Wert:
//   * registriert, aber undokumentiert  -> Warnung (Doku-Luecke)
//   * dokumentiert, aber nicht registriert -> FEHLER (Doku luegt)
// Mit --strict wird jede Luecke zum Fehler (fuer CI).
//
// Aufruf:
//   dotnet run --project tools/LanguageExport -- [--out <pfad>] [--strict]

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using Assistant.Scripts;
using Assistant.Scripts.Engine;

namespace Razor.LanguageExport
{
    internal static class Program
    {
        private static int Main(string[] args)
        {
            string outPath = GetArg(args, "--out")
                             ?? Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,
                                 "..", "..", "..", "..", "..", "..",
                                 "scripting-site", "src", "data", "razor-language.json"));
            bool strict = args.Contains("--strict");

            Console.WriteLine("Razor language export");
            Console.WriteLine("=====================");

            // 1) Sprache registrieren — exakt wie ScriptManager.OnLogin es tut.
            Commands.Register();
            AgentCommands.Register();
            SpeechCommands.Register();
            TargetCommands.Register();
            Aliases.Register();
            Expressions.Register();

            var registeredCommands = Interpreter.RegisteredCommands.OrderBy(x => x, StringComparer.Ordinal).ToList();
            var registeredExpressions = Interpreter.RegisteredExpressions.OrderBy(x => x, StringComparer.Ordinal).ToList();
            var registeredAliases = Interpreter.RegisteredAliases.OrderBy(x => x, StringComparer.Ordinal).ToList();

            Console.WriteLine($"  registriert: {registeredCommands.Count} Commands, " +
                              $"{registeredExpressions.Count} Expressions, {registeredAliases.Count} Aliases");

            // 2) Kuratierte Doku laden.
            string docsPath = FindDocsFile();
            if (docsPath == null)
            {
                Console.Error.WriteLine("FEHLER: language-docs.json nicht gefunden.");
                return 1;
            }

            DocsFile docs;
            try
            {
                docs = JsonSerializer.Deserialize<DocsFile>(File.ReadAllText(docsPath), ReadOptions);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"FEHLER: language-docs.json ist ungueltig: {ex.Message}");
                return 1;
            }

            Console.WriteLine($"  dokumentiert: {docs.Commands.Count} Commands, {docs.Expressions.Count} Expressions, " +
                              $"{docs.Aliases.Count} Aliases  ({Path.GetFileName(docsPath)})");
            Console.WriteLine();

            // 3) Abgleich. Aliase (msg -> say, wft -> waitfortarget, ...) sind
            //    eigene Registrierungen, aber am kanonischen Eintrag dokumentiert —
            //    sie zaehlen als abgedeckt und verweisen im Output dorthin.
            var cmdAliases = BuildAliasMap(docs.Commands);
            var exprAliases = BuildAliasMap(docs.Expressions);
            var aliasAliases = BuildAliasMap(docs.Aliases);

            var problems = new List<string>();
            var gaps = new List<string>();

            Reconcile("Command", registeredCommands, docs.Commands, cmdAliases, problems, gaps);
            Reconcile("Expression", registeredExpressions, docs.Expressions, exprAliases, problems, gaps);
            Reconcile("Alias", registeredAliases, docs.Aliases, aliasAliases, problems, gaps);

            foreach (string p in problems)
                Console.Error.WriteLine($"  FEHLER: {p}");

            foreach (string g in gaps)
                Console.WriteLine($"  offen:  {g}");

            if (problems.Count > 0)
            {
                Console.Error.WriteLine();
                Console.Error.WriteLine($"{problems.Count} Eintrag/Eintraege dokumentiert, aber NICHT registriert — die Doku wuerde luegen.");
                return 1;
            }

            if (gaps.Count > 0)
            {
                Console.WriteLine();
                Console.WriteLine($"{gaps.Count} registrierte Eintraege haben noch keine Doku (erscheinen als 'undocumented').");

                if (strict)
                {
                    Console.Error.WriteLine("--strict: Abbruch wegen Doku-Luecken.");
                    return 1;
                }
            }

            // 4) Ausgabe bauen.
            var output = new LanguageFile
            {
                Generated = "razor-langexport (tools/LanguageExport) — do not edit by hand",
                AbiNote = "Inventory comes from Razor.Core registrations; prose from language-docs.json.",
                Counts = new Counts
                {
                    Commands = registeredCommands.Count,
                    Expressions = registeredExpressions.Count,
                    Aliases = registeredAliases.Count,
                },
                Categories = docs.Categories,
                Commands = Merge(registeredCommands, docs.Commands, cmdAliases),
                Expressions = Merge(registeredExpressions, docs.Expressions, exprAliases),
                Aliases = Merge(registeredAliases, docs.Aliases, aliasAliases),
                Keywords = docs.Keywords,
                Layers = docs.Layers,
                // Skills kommen NICHT aus der Doku, sondern aus der echten
                // Skill-Tabelle des Ports (Ultima.Skills) — inkl. IsAction,
                // das entscheidet, ob `skill 'x'` ueberhaupt geht.
                Skills = ExportSkills(),
            };

            string json = JsonSerializer.Serialize(output, WriteOptions);

            string full = Path.GetFullPath(outPath);
            Directory.CreateDirectory(Path.GetDirectoryName(full));
            File.WriteAllText(full, json + Environment.NewLine);

            Console.WriteLine();
            Console.WriteLine($"geschrieben: {full}");
            Console.WriteLine($"             {registeredCommands.Count + registeredExpressions.Count + registeredAliases.Count} Eintraege, " +
                              $"{json.Length / 1024} KB");
            return 0;
        }

        /// <summary>Die echte Skill-Tabelle des Ports (Namen + Index + usable).</summary>
        private static List<SkillEntry> ExportSkills()
        {
            Ultima.Skills.Initialize();

            return Ultima.Skills.SkillsByIndex
                .OrderBy(kv => kv.Key)
                .Select(kv => new SkillEntry
                {
                    Name = kv.Value.Name,
                    Index = kv.Key,
                    Usable = kv.Value.IsAction,
                })
                .ToList();
        }

        /// <summary>alias-Name -> kanonischer Name, aus den "aliases"-Listen der Doku.</summary>
        private static Dictionary<string, string> BuildAliasMap(Dictionary<string, DocEntry> documented)
        {
            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (var kv in documented)
            {
                if (kv.Value.Aliases == null)
                    continue;

                foreach (string alias in kv.Value.Aliases)
                    map[alias] = kv.Key;
            }

            return map;
        }

        /// <summary>
        /// registriert ∖ (dokumentiert ∪ aliase) -> Luecke (Warnung).
        /// dokumentiert ∖ registriert            -> Fehler (die Doku wuerde etwas versprechen, das es nicht gibt).
        /// alias ∖ registriert                   -> Fehler (Alias existiert nicht).
        /// </summary>
        private static void Reconcile(string kind, List<string> registered, Dictionary<string, DocEntry> documented,
            Dictionary<string, string> aliasMap, List<string> problems, List<string> gaps)
        {
            var reg = new HashSet<string>(registered, StringComparer.OrdinalIgnoreCase);

            foreach (string name in documented.Keys.OrderBy(x => x, StringComparer.Ordinal))
            {
                if (!reg.Contains(name))
                    problems.Add($"{kind} '{name}' ist dokumentiert, aber nicht registriert.");
            }

            foreach (var kv in aliasMap.OrderBy(x => x.Key, StringComparer.Ordinal))
            {
                if (!reg.Contains(kv.Key))
                    problems.Add($"{kind} '{kv.Key}' ist als Alias von '{kv.Value}' dokumentiert, aber nicht registriert.");
            }

            foreach (string name in registered)
            {
                if (!documented.ContainsKey(name) && !aliasMap.ContainsKey(name))
                    gaps.Add($"{kind} '{name}' hat noch keine Doku.");
            }
        }

        private static List<Entry> Merge(List<string> registered, Dictionary<string, DocEntry> documented,
            Dictionary<string, string> aliasMap)
        {
            var list = new List<Entry>();

            foreach (string name in registered)
            {
                // Alias? -> Text vom kanonischen Eintrag erben und dorthin verweisen.
                string aliasOf = null;
                if (!documented.TryGetValue(name, out DocEntry d) &&
                    aliasMap.TryGetValue(name, out string canonical))
                {
                    aliasOf = canonical;
                    documented.TryGetValue(canonical, out d);
                }

                list.Add(new Entry
                {
                    Name = name,
                    AliasOf = aliasOf,
                    Category = d?.Category ?? "misc",
                    Source = d?.Source ?? new List<string> { "ce" },
                    Supported = d?.Supported ?? true,
                    Signature = d?.Signature,
                    Summary = d?.Summary,
                    Description = d?.Description,
                    Params = d?.Params,
                    Example = d?.Example,
                    SagasNote = d?.SagasNote,
                    Aliases = aliasOf == null ? d?.Aliases : null,
                    SeeAlso = d?.SeeAlso,
                    Documented = d != null,
                });
            }

            return list;
        }

        private static string FindDocsFile()
        {
            foreach (string candidate in new[]
                     {
                         Path.Combine(AppContext.BaseDirectory, "language-docs.json"),
                         Path.Combine("tools", "LanguageExport", "language-docs.json"),
                         "language-docs.json",
                     })
            {
                if (File.Exists(candidate))
                    return candidate;
            }

            return null;
        }

        private static string GetArg(string[] args, string name)
        {
            int i = Array.IndexOf(args, name);
            return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
        }

        private static readonly JsonSerializerOptions ReadOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
        };

        private static readonly JsonSerializerOptions WriteOptions = new()
        {
            WriteIndented = true,
            // camelCase: die Datei wird von der Astro-Seite (TypeScript) gelesen.
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        };
    }

    // ---- kuratierte Eingabe (language-docs.json) ----------------------------

    internal sealed class DocsFile
    {
        public Dictionary<string, string> Categories { get; set; } = new();
        public Dictionary<string, DocEntry> Commands { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, DocEntry> Expressions { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, DocEntry> Aliases { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public List<string> Keywords { get; set; } = new();
        public List<NameValue> Layers { get; set; } = new();
        public List<SkillEntry> Skills { get; set; } = new();
    }

    internal sealed class DocEntry
    {
        public string Category { get; set; }
        /// <summary>"ce" | "outlands" | "uosagas" — kann mehrere sein.</summary>
        public List<string> Source { get; set; }
        /// <summary>false = auf UOSagas (noch) nicht funktionsfaehig; SagasNote erklaert warum.</summary>
        public bool? Supported { get; set; }
        public string Signature { get; set; }
        public string Summary { get; set; }
        public string Description { get; set; }
        public List<ParamEntry> Params { get; set; }
        public string Example { get; set; }
        public string SagasNote { get; set; }
        public List<string> Aliases { get; set; }
        public List<string> SeeAlso { get; set; }
    }

    internal sealed class ParamEntry
    {
        public string Name { get; set; }
        public string Desc { get; set; }
        public bool Optional { get; set; }
    }

    internal sealed class NameValue
    {
        public string Name { get; set; }
        public string Note { get; set; }
    }

    internal sealed class SkillEntry
    {
        public string Name { get; set; }
        public int Index { get; set; }
        public bool Usable { get; set; }
    }

    // ---- Ausgabe (razor-language.json) -------------------------------------

    internal sealed class LanguageFile
    {
        [JsonPropertyName("$generated")] public string Generated { get; set; }
        [JsonPropertyName("$note")] public string AbiNote { get; set; }
        public Counts Counts { get; set; }
        public Dictionary<string, string> Categories { get; set; }
        public List<Entry> Commands { get; set; }
        public List<Entry> Expressions { get; set; }
        public List<Entry> Aliases { get; set; }
        public List<string> Keywords { get; set; }
        public List<NameValue> Layers { get; set; }
        public List<SkillEntry> Skills { get; set; }
    }

    internal sealed class Counts
    {
        public int Commands { get; set; }
        public int Expressions { get; set; }
        public int Aliases { get; set; }
    }

    internal sealed class Entry
    {
        public string Name { get; set; }
        /// <summary>Gesetzt, wenn dieser Name nur ein Alias eines anderen Eintrags ist.</summary>
        public string AliasOf { get; set; }
        public string Category { get; set; }
        public List<string> Source { get; set; }
        public bool Supported { get; set; }
        public string Signature { get; set; }
        public string Summary { get; set; }
        public string Description { get; set; }
        public List<ParamEntry> Params { get; set; }
        public string Example { get; set; }
        public string SagasNote { get; set; }
        public List<string> Aliases { get; set; }
        public List<string> SeeAlso { get; set; }
        /// <summary>false = registriert, aber noch ohne kuratierten Text.</summary>
        public bool Documented { get; set; }
    }
}
