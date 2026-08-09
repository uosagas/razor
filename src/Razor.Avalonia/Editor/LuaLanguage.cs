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

// UOSagas-Razor: Sprachdefinition fuer Lua (Phase 4b).
//
// Highlighting aus der eingebetteten Lua.xshd, Autocomplete aus dem 1:1
// uebernommenen Client-Korpus (LuaCompletionCorpus), Autoformat = Port des
// Client-LuaFormatters (Assistant/LuaCodeEditor/LuaFormatter.cs) — gleiche
// Einrueckungslogik wie der In-Client-Editor.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using AvaloniaEdit.Highlighting;
using AvaloniaEdit.Highlighting.Xshd;

namespace Razor.UI.Editor
{
    public sealed class LuaLanguage : ILanguageDefinition
    {
        public static readonly LuaLanguage Instance = new LuaLanguage();

        public string Name => "Lua";
        public string LineCommentPrefix => "--";

        // "Player.Say" soll als EIN Wort vervollstaendigt werden.
        public bool CompletionIncludesDots => true;

        private IHighlightingDefinition _highlighting;
        public IHighlightingDefinition Highlighting => _highlighting ??= LoadHighlighting();

        public IEnumerable<CompletionEntry> GetCompletions(string prefix)
        {
            List<CompletionEntry> all = LuaCompletionCorpus.Entries;

            if (string.IsNullOrEmpty(prefix))
                return all;

            // Nach einem Punkt ("Player.") auch die Member-Eintraege anbieten.
            return all.Where(e => e.Text.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                      .OrderBy(e => e.Text, StringComparer.OrdinalIgnoreCase);
        }

        public string Format(string text)
        {
            return LuaFormatter.Format(text ?? string.Empty);
        }

        private static IHighlightingDefinition LoadHighlighting()
        {
            try
            {
                var asm = typeof(LuaLanguage).Assembly;
                using var stream = asm.GetManifestResourceStream("Razor.UI.Editor.Lua.xshd");
                if (stream == null)
                {
                    Console.WriteLine("[UOSagas Razor] Lua.xshd nicht gefunden (EmbeddedResource).");
                    return null;
                }

                using var reader = System.Xml.XmlReader.Create(stream);
                return HighlightingLoader.Load(reader, HighlightingManager.Instance);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[UOSagas Razor] Lua-Highlighting laden fehlgeschlagen: {ex}");
                return null;
            }
        }
    }

    /// <summary>
    /// Port des Client-LuaFormatters (Assistant/LuaCodeEditor/LuaFormatter.cs)
    /// — Logik 1:1, nur statisch statt Instanz. Beim Nachziehen von
    /// Client-Updates diese Klasse gegen das Original diffen.
    /// </summary>
    internal static class LuaFormatter
    {
        private static readonly HashSet<string> IndentIncreaseKeywords = new()
        {
            "function", "if", "for", "while", "repeat", "do", "then", "else", "elseif"
        };

        private static readonly HashSet<string> IndentDecreaseKeywords = new()
        {
            "end", "until", "else", "elseif"
        };

        public static string Format(string code)
        {
            try
            {
                var lines = code.Split('\n');
                return FormatLines(lines);
            }
            catch (Exception)
            {
                // If formatting fails, return original code
                return code;
            }
        }

        private static string FormatLines(string[] lines)
        {
            var result = new StringBuilder();
            int indentLevel = 0;
            bool inMultiLineString = false;
            bool inMultiLineComment = false;

            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i].Trim();

                // Handle empty lines
                if (string.IsNullOrWhiteSpace(line))
                {
                    result.AppendLine();
                    continue;
                }

                // Check for multi-line strings and comments
                if (!inMultiLineString && !inMultiLineComment)
                {
                    if (line.Contains("[["))
                    {
                        if (line.StartsWith("--[["))
                        {
                            inMultiLineComment = true;
                        }
                        else
                        {
                            inMultiLineString = true;
                        }
                    }
                }
                else if (inMultiLineString && line.Contains("]]"))
                {
                    inMultiLineString = false;
                }
                else if (inMultiLineComment && line.Contains("]]"))
                {
                    inMultiLineComment = false;
                }

                // Don't format inside multi-line strings or comments
                if (inMultiLineString || inMultiLineComment)
                {
                    result.AppendLine(new string(' ', indentLevel * 4) + line);
                    continue;
                }

                // Handle single-line comments
                if (line.StartsWith("--"))
                {
                    result.AppendLine(new string(' ', indentLevel * 4) + line);
                    continue;
                }

                // Adjust indent level for current line
                var currentIndent = indentLevel;

                // Check if this line should decrease indent
                if (ShouldDecreaseIndent(line))
                {
                    currentIndent = Math.Max(0, indentLevel - 1);
                }

                // Format the line
                var formattedLine = FormatLine(line);

                // Add the formatted line with proper indentation
                result.AppendLine(new string(' ', currentIndent * 4) + formattedLine);

                // Adjust indent level for next line
                if (ShouldIncreaseIndent(line))
                {
                    indentLevel++;
                }
                else if (ShouldDecreaseIndent(line))
                {
                    indentLevel = Math.Max(0, indentLevel - 1);
                }
            }

            return result.ToString().TrimEnd();
        }

        private static string FormatLine(string line)
        {
            // Remove extra whitespace
            line = Regex.Replace(line, @"\s+", " ").Trim();

            // Add spaces around operators
            line = Regex.Replace(line, @"([^<>=!~])([<>=!~]=?)([^<>=!~])", "$1 $2 $3");
            line = Regex.Replace(line, @"([^+\-*/])([+\-*/])([^+\-*/])", "$1 $2 $3");
            line = Regex.Replace(line, @"([^,])([,])([^,])", "$1$2 $3");

            // Fix spacing around parentheses and brackets
            line = Regex.Replace(line, @"\s*\(\s*", "(");
            line = Regex.Replace(line, @"\s*\)\s*", ")");
            line = Regex.Replace(line, @"\s*\[\s*", "[");
            line = Regex.Replace(line, @"\s*\]\s*", "]");
            line = Regex.Replace(line, @"\s*\{\s*", "{");
            line = Regex.Replace(line, @"\s*\}\s*", "}");

            // Add space after keywords
            var keywords = new[] { "if", "then", "else", "elseif", "while", "for", "do", "function", "local", "return" };
            foreach (var keyword in keywords)
            {
                line = Regex.Replace(line, $@"\b{keyword}\b(?!\s)", $"{keyword} ");
            }

            return line;
        }

        private static bool ShouldIncreaseIndent(string line)
        {
            var tokens = TokenizeLine(line);

            foreach (var token in tokens)
            {
                if (IndentIncreaseKeywords.Contains(token.ToLower()))
                {
                    return true;
                }
            }

            // Special case for table literals
            if (line.Contains("{") && !line.Contains("}"))
            {
                return true;
            }

            return false;
        }

        private static bool ShouldDecreaseIndent(string line)
        {
            var tokens = TokenizeLine(line);

            if (tokens.Count > 0)
            {
                var firstToken = tokens[0].ToLower();
                if (IndentDecreaseKeywords.Contains(firstToken))
                {
                    return true;
                }
            }

            // Special case for closing braces
            if (line.TrimStart().StartsWith("}"))
            {
                return true;
            }

            return false;
        }

        private static List<string> TokenizeLine(string line)
        {
            var tokens = new List<string>();
            var currentToken = new StringBuilder();
            bool inString = false;
            char stringChar = '\0';

            for (int i = 0; i < line.Length; i++)
            {
                var ch = line[i];

                if (inString)
                {
                    currentToken.Append(ch);
                    if (ch == stringChar && (i == 0 || line[i - 1] != '\\'))
                    {
                        inString = false;
                        tokens.Add(currentToken.ToString());
                        currentToken.Clear();
                    }
                }
                else if (ch == '"' || ch == '\'')
                {
                    if (currentToken.Length > 0)
                    {
                        tokens.Add(currentToken.ToString());
                        currentToken.Clear();
                    }
                    inString = true;
                    stringChar = ch;
                    currentToken.Append(ch);
                }
                else if (char.IsWhiteSpace(ch))
                {
                    if (currentToken.Length > 0)
                    {
                        tokens.Add(currentToken.ToString());
                        currentToken.Clear();
                    }
                }
                else if (IsOperatorChar(ch))
                {
                    if (currentToken.Length > 0)
                    {
                        tokens.Add(currentToken.ToString());
                        currentToken.Clear();
                    }

                    // Handle multi-character operators
                    if (i + 1 < line.Length)
                    {
                        var twoChar = line.Substring(i, 2);
                        if (twoChar == "==" || twoChar == "~=" || twoChar == "<=" || twoChar == ">=" || twoChar == "..")
                        {
                            tokens.Add(twoChar);
                            i++; // Skip next character
                            continue;
                        }
                    }

                    tokens.Add(ch.ToString());
                }
                else
                {
                    currentToken.Append(ch);
                }
            }

            if (currentToken.Length > 0)
            {
                tokens.Add(currentToken.ToString());
            }

            return tokens;
        }

        private static bool IsOperatorChar(char ch)
        {
            return ch == '+' || ch == '-' || ch == '*' || ch == '/' || ch == '%' ||
                   ch == '^' || ch == '#' || ch == '=' || ch == '<' || ch == '>' ||
                   ch == '(' || ch == ')' || ch == '{' || ch == '}' || ch == '[' ||
                   ch == ']' || ch == ';' || ch == ':' || ch == ',' || ch == '.';
        }
    }
}
