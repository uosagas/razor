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

// UOSagas-Razor: AvaloniaEdit-Completion-Eintrag fuer den IDE-Editor.
//
// Bruecke von CompletionEntry (sprachneutral) auf AvaloniaEdits ICompletionData.
// Der Tooltip (Description) erscheint neben dem Vorschlag.

using System;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using AvaloniaEdit.CodeCompletion;
using AvaloniaEdit.Document;
using AvaloniaEdit.Editing;

namespace Razor.UI.Editor
{
    public sealed class CompletionData : ICompletionData
    {
        private readonly CompletionEntry _entry;

        public CompletionData(CompletionEntry entry)
        {
            _entry = entry;
        }

        public IImage Image => null;

        public string Text => _entry.Text;

        // Im Popup angezeigter Inhalt.
        public object Content => _entry.Text;

        // Tooltip rechts neben dem Popup (Usage/Beschreibung/Beispiel).
        public object Description => _entry.Description;

        public double Priority => _entry.Category == "keyword" ? 2 : _entry.Category == "command" ? 1 : 0;

        public void Complete(TextArea textArea, ISegment completionSegment, EventArgs insertionRequestEventArgs)
        {
            textArea.Document.Replace(completionSegment, _entry.InsertText ?? _entry.Text);
        }
    }
}
