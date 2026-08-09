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

// UOSagas-Razor: wiederverwendbare Sprachdefinition fuer den IDE-Editor.
//
// Der CodeEditor (AvaloniaEdit-Wrapper) ist sprachagnostisch; alles
// Sprachspezifische (Highlighting, Autocomplete, Kommentar-Syntax) kommt aus
// einer ILanguageDefinition. Fuer Razor Script gibt es RazorScriptLanguage;
// der spaetere Lua-Editor implementiert dieselbe Schnittstelle (LuaLanguage).

using System.Collections.Generic;
using AvaloniaEdit.Highlighting;

namespace Razor.UI.Editor
{
    /// <summary>Ein Autocomplete-Vorschlag: einzufuegender Text + Tooltip.</summary>
    public sealed class CompletionEntry
    {
        public string Text;
        public string Category;      // z. B. "keyword" / "command" / "expression"
        public string Description;   // Tooltip-Text (kann leer sein)

        /// <summary>Abweichender Einfuege-Text (Snippets: Text = Name im Popup,
        /// InsertText = der Code-Block). null = Text einfuegen.</summary>
        public string InsertText;

        public CompletionEntry(string text, string category, string description)
        {
            Text = text;
            Category = category;
            Description = description;
        }
    }

    public interface ILanguageDefinition
    {
        /// <summary>Anzeigename der Sprache (z. B. "Razor Script").</summary>
        string Name { get; }

        /// <summary>AvaloniaEdit-Highlighting (aus .xshd geladen); null = keins.</summary>
        IHighlightingDefinition Highlighting { get; }

        /// <summary>Kommentar-Praefix fuer Zeilenkommentar-Umschalten (z. B. "//").</summary>
        string LineCommentPrefix { get; }

        /// <summary>true: '.' zaehlt beim Autocomplete zum Wort (Lua: "Player.Say").</summary>
        bool CompletionIncludesDots => false;

        /// <summary>Alle Vorschlaege, gefiltert auf das aktuelle Wort-Praefix.</summary>
        IEnumerable<CompletionEntry> GetCompletions(string prefix);

        /// <summary>Autoformat (VSCode: Shift+Alt+F) — formatiert den kompletten Text.</summary>
        string Format(string text);
    }
}
