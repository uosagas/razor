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

// UOSagas-Razor: Headless-UI-Tests fuer den IDE-Editor (Phase 4a).
//
// Laeuft die ECHTE RazorApp (inkl. Style-Pipeline) auf der Avalonia-Headless-
// Platform — reproduziert damit genau die Live-Situation im Spiel:
// Wird das AvaloniaEdit-ControlTemplate angewendet (TextArea im Visual Tree)?
// Kommt Texteingabe im Editor an?

using System.Collections.Generic;
using System.Linq;
using Assistant.Scripts;
using Assistant.Scripts.Engine;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using AvaloniaEdit.Document;
using AvaloniaEdit.Editing;
using AvaloniaEdit.Highlighting;
using Razor.UI;
using Razor.UI.Editor;
using Xunit;

[assembly: AvaloniaTestApplication(typeof(Razor.Core.Tests.HeadlessTestApp))]

namespace Razor.Core.Tests
{
    /// <summary>Headless-Bootstrap mit der ECHTEN RazorApp (unsere Styles!).</summary>
    public class HeadlessTestApp
    {
        public static AppBuilder BuildAvaloniaApp() =>
            AppBuilder.Configure<RazorApp>()
                .UseHeadless(new AvaloniaHeadlessPlatformOptions());
    }

    public class EditorHeadlessTests
    {
        private static Window ShowEditorWindow(out CodeEditor editor)
        {
            editor = new CodeEditor { LanguageDefinition = RazorScriptLanguage.Instance };

            var window = new Window
            {
                Width = 400,
                Height = 300,
                Content = editor
            };
            window.Show();

            return window;
        }

        [AvaloniaFact]
        public void CodeEditor_Template_wird_angewendet()
        {
            Window window = ShowEditorWindow(out CodeEditor editor);

            // Ohne geladenes AvaloniaEdit-Theme hat der TextEditor KEIN
            // ControlTemplate -> keine TextArea im Visual Tree (der Live-Bug:
            // tote schwarze Flaeche ohne Eingabe/Zeilennummern).
            TextArea textArea = editor.GetVisualDescendants().OfType<TextArea>().FirstOrDefault();

            Assert.True(textArea != null,
                "AvaloniaEdit-ControlTemplate nicht angewendet (TextArea fehlt im Visual Tree) — " +
                "Style/Theme fuer AvaloniaEdit wird nicht geladen.");

            window.Close();
        }

        [AvaloniaFact]
        public void CodeEditor_nimmt_Texteingabe_an()
        {
            Window window = ShowEditorWindow(out CodeEditor editor);

            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            editor.TextArea.Focus();
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();

            var focused = Avalonia.Controls.TopLevel.GetTopLevel(editor)?.FocusManager?.GetFocusedElement();

            window.KeyTextInput("say 'hello'");

            Assert.True("say 'hello'" == editor.Text,
                $"Text kam nicht an. Fokus liegt auf: {focused?.GetType().FullName ?? "(nichts)"}; Text='{editor.Text}'");

            window.Close();
        }

        /// <summary>
        /// Registriert die volle Sprachoberflaeche (wie der Plugin-Start), damit
        /// Interpreter.Registered* gefuellt ist — daraus speist sich das
        /// Highlighting jetzt (statt aus einer handgepflegten Liste).
        /// </summary>
        private static void RegisterLanguage()
        {
            Commands.Register();
            AgentCommands.Register();
            SpeechCommands.Register();
            TargetCommands.Register();
            Aliases.Register();
            Expressions.Register();
        }

        private static HashSet<string> WordsWithColor(string line, string colorName)
        {
            return WordsWithColor(RazorScriptLanguage.Instance.Highlighting, line, colorName);
        }

        private static HashSet<string> WordsWithColor(IHighlightingDefinition def, string line, string colorName)
        {
            Assert.NotNull(def);

            var doc = new TextDocument(line);
            var highlighter = new DocumentHighlighter(doc, def);
            HighlightedLine hl = highlighter.HighlightLine(1);

            var words = new HashSet<string>();
            foreach (HighlightedSection sec in hl.Sections)
            {
                if (sec.Color?.Name == colorName)
                    words.Add(line.Substring(sec.Offset, sec.Length));
            }
            return words;
        }

        [AvaloniaFact]
        public void Lua_Highlighting_faerbt_User_Funktionen_und_Aufrufe()
        {
            IHighlightingDefinition lua = LuaLanguage.Instance.Highlighting;

            // Definitionsname gold (function foo / local function foo).
            Assert.Contains("heilen",
                WordsWithColor(lua, "local function heilen() end", "UserFunction"));

            // Zuweisungsform foo = function(...) ebenfalls gold.
            Assert.Contains("myfunc",
                WordsWithColor(lua, "myfunc = function(a) return a end", "UserFunction"));

            // Freier Aufruf gold.
            Assert.Contains("heilen",
                WordsWithColor(lua, "heilen(1, 2)", "UserFunction"));

            // Methoden-Definition (function Obj.foo) gold, nicht als Member-Aufruf.
            Assert.Contains("Heiler.start",
                WordsWithColor(lua, "function Heiler.start() end", "UserFunction"));

            // Member-Aufrufe bleiben in der Modul-Farbe (wie die API selbst).
            HashSet<string> member = WordsWithColor(lua, "Player.Say('hi')", "Module");
            Assert.Contains("Player", member);
            Assert.Contains("Say", member);
            Assert.DoesNotContain("Say",
                WordsWithColor(lua, "Player.Say('hi')", "UserFunction"));

            // Keywords gewinnen: if(x) darf nicht als Funktionsaufruf gelten.
            Assert.DoesNotContain("if",
                WordsWithColor(lua, "if(x) then end", "UserFunction"));
        }

        [AvaloniaFact]
        public void Highlighting_faerbt_registrierte_Commands_und_Expressions()
        {
            RegisterLanguage();

            // Genau die vom User gemeldeten Faelle: setskill (Command),
            // find/gumpexists/counttype (Expressions) waren nicht eingefaerbt.
            var commandWords = WordsWithColor("setskill 'Alchemy' up", "Command");
            Assert.Contains("setskill", commandWords);

            var exprLine = "if find 0x40 and gumpexists 123 and counttype 3617 backpack > 0";
            var exprWords = WordsWithColor(exprLine, "Expression");
            Assert.Contains("find", exprWords);
            Assert.Contains("gumpexists", exprWords);
            Assert.Contains("counttype", exprWords);
        }

        [AvaloniaFact]
        public void Highlighting_faerbt_keine_nicht_vorhandenen_Commands()
        {
            RegisterLanguage();

            // Diese standen in der alten .xshd, sind aber im Port nicht
            // registriert — der Editor darf sie nicht als Command ausgeben.
            foreach (var phantom in new[] { "dressconfig", "dclickvar", "waitforstat" })
            {
                var words = WordsWithColor(phantom + " x", "Command");
                Assert.DoesNotContain(phantom, words);
            }
        }

        [AvaloniaFact]
        public void Autocomplete_deckt_alle_Registrierungen_ab()
        {
            RegisterLanguage();

            var completions = RazorScriptLanguage.Instance.GetCompletions(null)
                .Select(c => c.Text)
                .ToHashSet(System.StringComparer.OrdinalIgnoreCase);

            foreach (var name in Interpreter.RegisteredCommands)
                Assert.Contains(name, completions);
            foreach (var name in Interpreter.RegisteredExpressions)
                Assert.Contains(name, completions);
        }

        [AvaloniaFact]
        public void Deaktivierte_Controls_bekommen_grauen_Text()
        {
            // User-Vorgabe: Ausgegraut heisst auch der TEXT ist grau — das
            // Simple-Theme dimmt sonst nur Glyphe/Rahmen. Der App-Style
            // (RazorApp, :disabled) muss sich gegen das Theme durchsetzen.
            var check = new CheckBox { Content = "Use Pre-AOS status window", IsEnabled = false };
            var button = new Button { Content = "Set Min", IsEnabled = false };

            var window = new Window
            {
                Content = new StackPanel { Children = { check, button } }
            };
            window.Show();
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();

            Assert.Same(Ce.GrayText, check.Foreground);
            Assert.Same(Ce.GrayText, button.Foreground);

            // Gegenprobe: aktivierte Controls bleiben unveraendert.
            check.IsEnabled = true;
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            Assert.NotSame(Ce.GrayText, check.Foreground);

            window.Close();
        }

        [AvaloniaFact]
        public void ScriptEditorWindow_oeffnet_und_laedt_Text()
        {
            var ide = new ScriptEditorWindow(RazorScriptLanguage.Instance);
            ide.Show();

            ide.LoadScript("test", "overhead 'x'");

            Assert.Equal("overhead 'x'", ide.GetText());

            TextArea textArea = ide.GetVisualDescendants().OfType<TextArea>().FirstOrDefault();
            Assert.True(textArea != null, "TextArea fehlt im IDE-Fenster (Template nicht angewendet).");

            ide.Close();
        }
    }
}
