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

// UOSagas-Razor: Lua-API-Export fuer die Doku-Website (share.uosagas.com).
//
// Gleiches Prinzip wie tools/LanguageExport: die EINZIGE Quelle der Wahrheit
// sind die echten Registrierungen (LuaEngineService.CreateStateForExport
// registriert Sandbox + alle Module headless). Das Inventar wird mit den
// kuratierten Texten aus lua-api-docs.json verheiratet:
//   * registriert, aber undokumentiert   -> Warnung (Doku-Luecke)
//   * dokumentiert, aber nicht registriert -> FEHLER (Doku luegt)
// Mit --strict wird jede Luecke zum Fehler (fuer CI).
//
// UI-Methoden (window:/control:) und die Objekt-Feldlisten haengen an
// Laufzeit-Instanzen und sind nicht statisch enumerierbar — sie sind
// kuratiert und werden 1:1 uebernommen (im Output entsprechend markiert).
//
// Ergebnis: lua-api.json (gleiches Entry-Schema wie razor-language.json,
// damit die Site Reference.astro unveraendert wiederverwendet).
//
// Aufruf:
//   dotnet run --project tools/LuaExport -- [--out <pfad>] [--strict]

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using Assistant.LuaEngine;
using Lua;

namespace Razor.LuaExport
{
    internal static class Program
    {
        /// <summary>Module, deren Funktionen statisch am State haengen.</summary>
        private static readonly string[] Modules =
        {
            "Player", "Items", "Mobiles", "Gumps", "Targeting", "Spells",
            "Skills", "Journal", "Messages", "Config", "UI", "Console",
        };

        /// <summary>Globale Funktionen der Engine (keine Standard-Lib).</summary>
        private static readonly string[] Globals = { "Pause", "StopScript", "ExecuteMacro", "Import", "print" };

        private static int Main(string[] args)
        {
            string outPath = GetArg(args, "--out")
                             ?? Path.Combine("..", "..", "..", "..", "..",
                                             "ModernUO-Client", "tools", "scripting-site",
                                             "src", "data", "lua-api.json");
            bool strict = args.Contains("--strict");

            Console.WriteLine("Lua API export");
            Console.WriteLine("==============");

            // 1) Inventar: echte Registrierungen auf einem frischen State.
            LuaState state = LuaEngineService.CreateStateForExport();
            var registered = new List<string>();

            foreach (string module in Modules)
            {
                LuaValue value = state.Environment[module];
                if (value.Type != LuaValueType.Table)
                {
                    Console.Error.WriteLine($"FEHLER: Modul '{module}' ist nicht registriert.");
                    return 1;
                }

                var table = value.Read<LuaTable>();
                foreach (var pair in table)
                {
                    string key = pair.Key.ToString();
                    if (key.StartsWith("__") || pair.Value.Type != LuaValueType.Function)
                        continue;

                    registered.Add($"{module}.{key}");
                }
            }

            foreach (string g in Globals)
            {
                if (state.Environment[g].Type == LuaValueType.Function)
                    registered.Add(g);
                else
                    Console.Error.WriteLine($"WARNUNG: Global '{g}' ist nicht registriert.");
            }

            registered.Sort(StringComparer.Ordinal);
            Console.WriteLine($"  registriert: {registered.Count} Funktionen in {Modules.Length} Modulen + Globals");

            // 2) Kuratierte Doku laden.
            string docsPath = FindDocsFile();
            if (docsPath == null)
            {
                Console.Error.WriteLine("FEHLER: lua-api-docs.json nicht gefunden.");
                return 1;
            }

            DocsFile docs;
            try
            {
                docs = JsonSerializer.Deserialize<DocsFile>(File.ReadAllText(docsPath), ReadOptions);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"FEHLER: lua-api-docs.json ist ungueltig: {ex.Message}");
                return 1;
            }

            Console.WriteLine($"  dokumentiert: {docs.Functions.Count} Funktionen, {docs.UiMethods.Count} UI-Methoden " +
                              $"({Path.GetFileName(docsPath)})");
            Console.WriteLine();

            // 3) Abgleich (nur die statisch enumerierbaren Funktionen).
            var problems = new List<string>();
            var gaps = new List<string>();
            var reg = new HashSet<string>(registered, StringComparer.Ordinal);

            foreach (string name in docs.Functions.Keys.OrderBy(x => x, StringComparer.Ordinal))
            {
                if (!reg.Contains(name))
                    problems.Add($"Funktion '{name}' ist dokumentiert, aber nicht registriert.");
            }

            foreach (string name in registered)
            {
                if (!docs.Functions.ContainsKey(name))
                    gaps.Add($"Funktion '{name}' hat noch keine Doku.");
            }

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
                Console.WriteLine($"{gaps.Count} registrierte Funktionen haben noch keine Doku (erscheinen als 'undocumented').");

                if (strict)
                {
                    Console.Error.WriteLine("--strict: Abbruch wegen Doku-Luecken.");
                    return 1;
                }
            }

            // 4) Ausgabe bauen.
            var output = new ApiFile
            {
                Generated = "razor-luaexport (tools/LuaExport) — do not edit by hand",
                Note = "Function inventory comes from the real LuaState registrations; prose from lua-api-docs.json. " +
                       "UI methods and object fields are curated (attached to runtime instances, not statically enumerable).",
                Counts = new Counts
                {
                    Functions = registered.Count,
                    Modules = Modules.Length,
                    UiMethods = docs.UiMethods.Count,
                },
                Categories = docs.Categories,
                Functions = registered.Select(name => ToEntry(name, docs.Functions.GetValueOrDefault(name))).ToList(),
                Ui = docs.UiMethods
                    .OrderBy(kv => kv.Key, StringComparer.Ordinal)
                    .Select(kv => ToEntry(kv.Key, kv.Value))
                    .ToList(),
                Objects = docs.Objects,
            };

            string json = JsonSerializer.Serialize(output, WriteOptions);

            string full = Path.GetFullPath(outPath);
            Directory.CreateDirectory(Path.GetDirectoryName(full));
            File.WriteAllText(full, json + Environment.NewLine);

            Console.WriteLine();
            Console.WriteLine($"geschrieben: {full}");
            Console.WriteLine($"             {output.Functions.Count} Funktionen + {output.Ui.Count} UI-Methoden, {json.Length / 1024} KB");
            return 0;
        }

        private static Entry ToEntry(string name, DocEntry d)
        {
            string description = d?.Description;
            if (d?.Returns != null)
                description = string.IsNullOrEmpty(description)
                    ? $"Returns: {d.Returns}"
                    : $"{description} Returns: {d.Returns}";

            return new Entry
            {
                Name = name,
                Category = d?.Category ?? "core",
                Source = new List<string> { "uosagas" },
                Supported = true,
                Signature = d?.Signature,
                Summary = d?.Summary,
                Description = description,
                Params = d?.Params,
                Example = d?.Example,
                SagasNote = d?.SagasNote,
                SeeAlso = d?.SeeAlso,
                Documented = d != null,
            };
        }

        private static string FindDocsFile()
        {
            foreach (string candidate in new[]
                     {
                         Path.Combine(AppContext.BaseDirectory, "lua-api-docs.json"),
                         Path.Combine("tools", "LuaExport", "lua-api-docs.json"),
                         "lua-api-docs.json",
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
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        };
    }

    // ---- kuratierte Eingabe (lua-api-docs.json) -----------------------------

    internal sealed class DocsFile
    {
        public Dictionary<string, string> Categories { get; set; } = new();
        public Dictionary<string, DocEntry> Functions { get; set; } = new(StringComparer.Ordinal);
        public Dictionary<string, DocEntry> UiMethods { get; set; } = new(StringComparer.Ordinal);
        public Dictionary<string, List<FieldEntry>> Objects { get; set; } = new();
    }

    internal sealed class DocEntry
    {
        public string Category { get; set; }
        public string Signature { get; set; }
        public string Summary { get; set; }
        public string Description { get; set; }
        public List<ParamEntry> Params { get; set; }
        public string Returns { get; set; }
        public string Example { get; set; }
        public string SagasNote { get; set; }
        public List<string> SeeAlso { get; set; }
    }

    internal sealed class ParamEntry
    {
        public string Name { get; set; }
        public string Desc { get; set; }
        public bool Optional { get; set; }
    }

    internal sealed class FieldEntry
    {
        public string Name { get; set; }
        public string Type { get; set; }
        public string Desc { get; set; }
    }

    // ---- Ausgabe (lua-api.json) ---------------------------------------------

    internal sealed class ApiFile
    {
        [JsonPropertyName("$generated")] public string Generated { get; set; }
        [JsonPropertyName("$note")] public string Note { get; set; }
        public Counts Counts { get; set; }
        public Dictionary<string, string> Categories { get; set; }
        public List<Entry> Functions { get; set; }
        public List<Entry> Ui { get; set; }
        public Dictionary<string, List<FieldEntry>> Objects { get; set; }
    }

    internal sealed class Counts
    {
        public int Functions { get; set; }
        public int Modules { get; set; }
        public int UiMethods { get; set; }
    }

    internal sealed class Entry
    {
        public string Name { get; set; }
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
        public bool Documented { get; set; }
    }
}
