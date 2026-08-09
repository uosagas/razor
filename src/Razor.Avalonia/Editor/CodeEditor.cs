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

// UOSagas-Razor: wiederverwendbarer IDE-Code-Editor (AvaloniaEdit-Wrapper).
//
// Sprachagnostisch — Highlighting/Autocomplete kommen aus einer
// ILanguageDefinition (RazorScriptLanguage jetzt; LuaLanguage spaeter).
// Features: Syntax-Highlighting, Zeilennummern, Autocomplete-Popup
// (Ctrl+Space + beim Tippen), Zeilen-Marker (Ausfuehrung=blau / Fehler=rot),
// Kommentar umschalten (Ctrl+/), Monospace, dunkles Editor-Theme.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Avalonia;
using Avalonia.Input;
using Avalonia.Media;
using AvaloniaEdit;
using AvaloniaEdit.CodeCompletion;
using AvaloniaEdit.Rendering;

namespace Razor.UI.Editor
{
    public class CodeEditor : TextEditor
    {
        // KRITISCH: Avalonia-11-Styles matchen ueber den StyleKey, und der ist
        // bei abgeleiteten Klassen standardmaessig die EIGENE Klasse. Ohne
        // dieses Override bekommt CodeEditor die TextEditor-Styles (und damit
        // das ControlTemplate aus dem AvaloniaEdit-Theme) NIE -> tote Flaeche
        // ohne TextArea/Eingabe/Zeilennummern.
        protected override Type StyleKeyOverride => typeof(TextEditor);

        private ILanguageDefinition _language;
        private CompletionWindow _completionWindow;
        private readonly LineMarkerRenderer _markers = new LineMarkerRenderer();

        public CodeEditor()
        {
            FontFamily = new FontFamily("Consolas,Menlo,DejaVu Sans Mono,monospace");
            FontSize = 13;
            ShowLineNumbers = true;
            WordWrap = false;
            Options.ConvertTabsToSpaces = true;
            Options.IndentationSize = 4;
            Options.HighlightCurrentLine = true;
            Options.EnableHyperlinks = false;

            // Dunkles Editor-Theme (die CE-Highlighting-Farben sind dafuer ausgelegt).
            Background = new SolidColorBrush(Color.FromRgb(0x1E, 0x1E, 0x1E));
            Foreground = new SolidColorBrush(Color.FromRgb(0xDC, 0xDC, 0xDC));
            LineNumbersForeground = new SolidColorBrush(Color.FromRgb(0x85, 0x85, 0x85));

            // Caret/Selektion auf dunklem Grund sichtbar machen (VSCode-Optik).
            TextArea.Caret.CaretBrush = Brushes.White;
            TextArea.SelectionBrush = new SolidColorBrush(Color.FromArgb(0x60, 0x26, 0x4F, 0x78));
            TextArea.SelectionForeground = null;
            TextArea.SelectionCornerRadius = 0;

            TextArea.TextView.BackgroundRenderers.Add(_markers);
            TextArea.TextEntered += OnTextEntered;
            AddHandler(KeyDownEvent, OnPreviewKeyDown, Avalonia.Interactivity.RoutingStrategies.Tunnel);

            // Fokus auf dem Editor (Tab/Focus()) in die TextArea weiterleiten —
            // Eingaben verarbeitet ausschliesslich die TextArea.
            GotFocus += (s, e) =>
            {
                if (e.Source == this)
                    TextArea.Focus();
            };

            BuildContextMenu();
        }

        /// <summary>Rechtsklick-Menue (Cut/Copy/Paste/Select All + Kommentar/Format).</summary>
        private void BuildContextMenu()
        {
            var menu = new Avalonia.Controls.ContextMenu();

            void Add(string header, string gesture, Action action)
            {
                var item = new Avalonia.Controls.MenuItem { Header = header, InputGesture = TryParseGesture(gesture) };
                item.Click += (s, e) => action();
                menu.Items.Add(item);
            }

            Add("Undo", "Ctrl+Z", () => Undo());
            Add("Redo", "Ctrl+Y", () => Redo());
            menu.Items.Add(new Avalonia.Controls.Separator());
            Add("Cut", "Ctrl+X", Cut);
            Add("Copy", "Ctrl+C", Copy);
            Add("Paste", "Ctrl+V", Paste);
            Add("Select All", "Ctrl+A", SelectAll);
            menu.Items.Add(new Avalonia.Controls.Separator());
            Add("Toggle Comment", "Ctrl+/", ToggleComment);
            Add("Format Document", "Shift+Alt+F", FormatDocument);

            ContextMenu = menu;
        }

        /// <summary>
        /// KeyGesture.Parse crasht bei Sondertasten wie "/" (kein Key-Name) —
        /// die Gesture am MenuItem ist rein kosmetisch, der Shortcut selbst
        /// laeuft ueber KeyDown. Nicht parsebare Gesten -> keine Anzeige.
        /// </summary>
        internal static KeyGesture TryParseGesture(string gesture)
        {
            if (string.IsNullOrEmpty(gesture))
                return null;

            try
            {
                return KeyGesture.Parse(gesture);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>Autoformat ueber die Sprachdefinition (VSCode: Shift+Alt+F).</summary>
        public void FormatDocument()
        {
            if (_language == null)
                return;

            int caret = CaretOffset;
            string formatted = _language.Format(Text);

            if (formatted == Text)
                return;

            Document.Text = formatted;
            CaretOffset = Math.Min(caret, Document.TextLength);
        }

        /// <summary>Zeilenkommentar der Auswahl/Zeile umschalten (Ctrl+/).</summary>
        public void ToggleComment()
        {
            ToggleLineComment();
        }

        public ILanguageDefinition LanguageDefinition
        {
            get => _language;
            set
            {
                _language = value;
                SyntaxHighlighting = value?.Highlighting;
            }
        }

        /// <summary>Markiert die aktuell ausgefuehrte Zeile (1-basiert; -1 = keine).</summary>
        public void SetExecutionLine(int line)
        {
            _markers.ExecLine = line;
            TextArea.TextView.InvalidateLayer(KnownLayer.Background);
            if (line >= 1 && line <= Document.LineCount)
                ScrollToLine(line);
        }

        /// <summary>Markiert eine Fehlerzeile (1-basiert; -1 = keine).</summary>
        public void SetErrorLine(int line)
        {
            _markers.ErrLine = line;
            TextArea.TextView.InvalidateLayer(KnownLayer.Background);
            if (line >= 1 && line <= Document.LineCount)
                ScrollToLine(line);
        }

        public void ClearMarkers()
        {
            _markers.ExecLine = -1;
            _markers.ErrLine = -1;
            TextArea.TextView.InvalidateLayer(KnownLayer.Background);
        }

        // ---- Breakpoints (Lua-Debugger) ------------------------------------

        private BreakpointMargin _breakpointMargin;

        /// <summary>Gesetzte Breakpoint-Zeilen (1-basiert, Editor-Sicht).</summary>
        public IReadOnlyCollection<int> BreakpointLines =>
            _breakpointMargin?.Lines ?? (IReadOnlyCollection<int>) Array.Empty<int>();

        /// <summary>Feuert nach jedem Toggle (Klick in die Breakpoint-Spalte).</summary>
        public event Action BreakpointsChanged;

        /// <summary>Blendet links eine klickbare Breakpoint-Spalte ein (rote
        /// Punkte, Klick = Toggle). Nur fuer Sprachen mit Debugger (Lua).</summary>
        public void EnableBreakpointMargin()
        {
            if (_breakpointMargin != null)
                return;

            _breakpointMargin = new BreakpointMargin(this);
            TextArea.LeftMargins.Insert(0, _breakpointMargin);
        }

        public void ClearBreakpoints()
        {
            if (_breakpointMargin == null || _breakpointMargin.Lines.Count == 0)
                return;

            _breakpointMargin.Lines.Clear();
            _breakpointMargin.InvalidateVisual();
            BreakpointsChanged?.Invoke();
        }

        /// <summary>Schmale klickbare Spalte links der Zeilennummern; zeichnet
        /// pro Breakpoint-Zeile einen roten Punkt (VSCode-Optik).</summary>
        private sealed class BreakpointMargin : AvaloniaEdit.Editing.AbstractMargin
        {
            private const double MarginWidth = 16;

            private readonly CodeEditor _owner;
            private readonly IBrush _background = new SolidColorBrush(Color.FromRgb(0x1E, 0x1E, 0x1E));
            private readonly IBrush _dot = new SolidColorBrush(Color.FromRgb(0xE5, 0x14, 0x00));

            public readonly HashSet<int> Lines = new HashSet<int>();

            public BreakpointMargin(CodeEditor owner)
            {
                _owner = owner;
                Cursor = new Cursor(StandardCursorType.Hand);
            }

            protected override Size MeasureOverride(Size availableSize)
            {
                return new Size(MarginWidth, 0);
            }

            protected override void OnTextViewChanged(TextView oldTextView, TextView newTextView)
            {
                if (oldTextView != null)
                    oldTextView.VisualLinesChanged -= OnVisualLinesChanged;
                if (newTextView != null)
                    newTextView.VisualLinesChanged += OnVisualLinesChanged;

                base.OnTextViewChanged(oldTextView, newTextView);
                InvalidateVisual();
            }

            private void OnVisualLinesChanged(object sender, EventArgs e) => InvalidateVisual();

            public override void Render(DrawingContext context)
            {
                // Hintergrund immer fuellen, sonst ist die Spalte nicht klickbar.
                context.FillRectangle(_background, new Rect(Bounds.Size));

                TextView textView = TextView;
                if (textView == null || !textView.VisualLinesValid || Lines.Count == 0)
                    return;

                foreach (var visualLine in textView.VisualLines)
                {
                    int line = visualLine.FirstDocumentLine.LineNumber;
                    if (!Lines.Contains(line))
                        continue;

                    double y = visualLine.GetTextLineVisualYPosition(visualLine.TextLines[0],
                        VisualYPosition.TextTop) - textView.VerticalOffset;
                    double height = visualLine.TextLines[0].Height;
                    double radius = 4.5;

                    context.DrawEllipse(_dot, null,
                        new Point(MarginWidth / 2, y + height / 2), radius, radius);
                }
            }

            protected override void OnPointerPressed(PointerPressedEventArgs e)
            {
                base.OnPointerPressed(e);

                TextView textView = TextView;
                if (textView == null || !textView.VisualLinesValid)
                    return;

                double y = e.GetPosition(this).Y + textView.VerticalOffset;

                foreach (var visualLine in textView.VisualLines)
                {
                    double top = visualLine.GetTextLineVisualYPosition(visualLine.TextLines[0],
                        VisualYPosition.TextTop);

                    if (y >= top && y < top + visualLine.Height)
                    {
                        int line = visualLine.FirstDocumentLine.LineNumber;
                        if (!Lines.Add(line))
                            Lines.Remove(line);

                        InvalidateVisual();
                        _owner.BreakpointsChanged?.Invoke();
                        e.Handled = true;
                        return;
                    }
                }
            }
        }

        private void OnPreviewKeyDown(object sender, KeyEventArgs e)
        {
            // Ctrl+Space: Autocomplete manuell oeffnen.
            if (e.Key == Key.Space && e.KeyModifiers.HasFlag(KeyModifiers.Control))
            {
                ShowCompletion();
                e.Handled = true;
                return;
            }

            // Ctrl+/ : Zeilenkommentar umschalten.
            if (e.Key == Key.OemQuestion && e.KeyModifiers.HasFlag(KeyModifiers.Control))
            {
                ToggleLineComment();
                e.Handled = true;
                return;
            }

            // Shift+Alt+F : Autoformat (VSCode).
            if (e.Key == Key.F && e.KeyModifiers.HasFlag(KeyModifiers.Shift) &&
                e.KeyModifiers.HasFlag(KeyModifiers.Alt))
            {
                FormatDocument();
                e.Handled = true;
            }
        }

        private void OnTextEntered(object sender, TextInputEventArgs e)
        {
            if (string.IsNullOrEmpty(e.Text))
                return;

            char c = e.Text[0];

            // Beim Tippen von Wortzeichen die Vervollstaendigung anbieten/aktualisieren.
            // Lua: '.' oeffnet die Member-Liste ("Player." -> Player.*).
            if (char.IsLetter(c) || c == '_' ||
                (c == '.' && _language?.CompletionIncludesDots == true))
            {
                if (_completionWindow == null)
                    ShowCompletion();
            }
        }

        private void ShowCompletion()
        {
            if (_language == null)
                return;

            string prefix = GetCurrentWordPrefix(out int prefixStart);
            var matches = _language.GetCompletions(prefix).Take(400).ToList();
            if (matches.Count == 0)
                return;

            _completionWindow = new CompletionWindow(TextArea);
            // Vervollstaendigung ersetzt das bereits getippte Wort-Praefix.
            _completionWindow.StartOffset = prefixStart;

            var data = _completionWindow.CompletionList.CompletionData;
            foreach (var m in matches)
                data.Add(new CompletionData(m));

            _completionWindow.Closed += (s, ev) => _completionWindow = null;
            _completionWindow.Show();
        }

        private string GetCurrentWordPrefix(out int start)
        {
            int caret = CaretOffset;
            int i = caret;
            var doc = Document;
            bool dots = _language?.CompletionIncludesDots == true;

            while (i > 0)
            {
                char ch = doc.GetCharAt(i - 1);
                if (char.IsLetterOrDigit(ch) || ch == '_' || (dots && ch == '.'))
                    i--;
                else
                    break;
            }

            start = i;
            return doc.GetText(i, caret - i);
        }

        private void ToggleLineComment()
        {
            string prefix = _language?.LineCommentPrefix ?? "//";
            var doc = Document;

            int startLine = doc.GetLineByOffset(SelectionStart).LineNumber;
            int endLine = doc.GetLineByOffset(SelectionStart + SelectionLength).LineNumber;

            // Wenn alle betroffenen Zeilen kommentiert sind -> auskommentieren aufheben.
            bool allCommented = true;
            for (int ln = startLine; ln <= endLine; ln++)
            {
                var line = doc.GetLineByNumber(ln);
                string text = doc.GetText(line.Offset, line.Length).TrimStart();
                if (text.Length > 0 && !text.StartsWith(prefix))
                {
                    allCommented = false;
                    break;
                }
            }

            BeginChange();
            for (int ln = endLine; ln >= startLine; ln--)
            {
                var line = doc.GetLineByNumber(ln);
                string text = doc.GetText(line.Offset, line.Length);
                if (text.Trim().Length == 0)
                    continue;

                if (allCommented)
                {
                    int idx = text.IndexOf(prefix, StringComparison.Ordinal);
                    if (idx >= 0)
                        doc.Remove(line.Offset + idx, prefix.Length);
                }
                else
                {
                    int indent = 0;
                    while (indent < text.Length && (text[indent] == ' ' || text[indent] == '\t'))
                        indent++;
                    doc.Insert(line.Offset + indent, prefix);
                }
            }
            EndChange();
        }

        /// <summary>Hintergrund-Renderer: hebt Ausfuehrungs- (blau) und Fehlerzeile (rot) hervor.</summary>
        private sealed class LineMarkerRenderer : IBackgroundRenderer
        {
            public int ExecLine = -1;
            public int ErrLine = -1;

            private readonly IBrush _execBrush = new SolidColorBrush(Color.FromArgb(0x55, 0x2B, 0x5C, 0xA8));
            private readonly IBrush _errBrush = new SolidColorBrush(Color.FromArgb(0x55, 0xC0, 0x24, 0x30));

            public KnownLayer Layer => KnownLayer.Background;

            public void Draw(TextView textView, DrawingContext drawingContext)
            {
                if (textView?.Document == null)
                    return;

                DrawLine(textView, drawingContext, ErrLine, _errBrush);
                DrawLine(textView, drawingContext, ExecLine, _execBrush);
            }

            private static void DrawLine(TextView textView, DrawingContext dc, int line, IBrush brush)
            {
                if (line < 1 || line > textView.Document.LineCount)
                    return;

                textView.EnsureVisualLines();
                var docLine = textView.Document.GetLineByNumber(line);

                foreach (var rect in BackgroundGeometryBuilder.GetRectsForSegment(textView, docLine))
                {
                    dc.FillRectangle(brush, new Rect(0, rect.Y, textView.Bounds.Width, rect.Height));
                }
            }
        }
    }
}
