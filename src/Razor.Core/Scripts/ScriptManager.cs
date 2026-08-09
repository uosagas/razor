#region license
// Razor: An Ultima Online Assistant
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

// Portiert aus Razor CE (Razor/Scripts/ScriptManager.cs) — NUR der Nicht-UI-
// Kern. ENTFERNT gegenueber CE: FastColoredTextBox/TreeView/Autocomplete-
// Verkabelung, Highlight-Zeilen (macht der Avalonia-Editor ueber die Events/
// CurrentLine), Clipboard-Hotkeys (ScriptDClickType/ScriptTargetType),
// Gump-Info-Fenster.
//
// Pump-Modell: Razor CE nutzt einen 25ms-ScriptTimer. Der Port pumpt vom
// Game-Thread: RazorPlugin.OnTick ruft ScriptManager.OnTick() — gleiche
// Semantik (StartScript beim ersten Tick, danach ExecuteScript pro Tick).

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Assistant.Macros;
using Assistant.Scripts.Engine;
// Geteilte Script-Konsole der IDE (Razor-Zusatz): Start/Stop/Fehler landen
// im selben Puffer wie die Lua-Ausgaben (DebugConsole.Script-Kanal).
using DebugConsole = Assistant.LuaEngine.DebugConsole;

namespace Assistant.Scripts
{
    public static class ScriptManager
    {
        /// <summary>Laeuft gerade ein Script (inkl. queued)?</summary>
        public static bool Running => ScriptRunning;

        private static bool ScriptRunning { get; set; }

        /// <summary>Von den Targeting-Varianten gesetzt (Priority-Listen-Abbruch).</summary>
        public static bool TargetFound { get; set; }

        /// <summary>Drossel fuer das walk-Kommando (Razor CE: LastWalk).</summary>
        public static DateTime LastWalk { get; set; }

        /// <summary>menu-Kommando: naechstes Kontextmenue-Popup im Client unterdruecken.</summary>
        public static bool BlockPopupMenu { get; set; }

        /// <summary>Script-Ordner (CE: Config.GetUserDirectory("Scripts")).</summary>
        public static string ScriptPath => Config.GetUserDirectory("Scripts");

        private static readonly List<RazorScript> _scriptList = new List<RazorScript>();

        /// <summary>Alle geladenen Scripts (Name/Category/Lines) — Quelle fuer die UI.</summary>
        public static IReadOnlyList<RazorScript> Scripts => _scriptList;

        private static Script _queuedScript;
        private static string _queuedScriptName;

        // Zeilen des aktiven Scripts — fuer aussagekraeftige Fehlermeldungen
        // ("Zeile N: <text>") statt nur einer nackten Exception.
        private static string[] _activeLines;
        private static string _activeName;

        private static readonly Stopwatch _watch = new Stopwatch();

        /// <summary>UI-Refresh-Hook (CE: RedrawScripts). Die Avalonia-UI haengt sich hier ein.</summary>
        public static event Action OnScriptsChanged;

        /// <summary>Feuert bei Start (Name) / Stop eines Scripts — fuer UI-Status + Zeilen-Marker.</summary>
        public static event Action<string> OnScriptStarted;
        public static event Action OnScriptStopped;

        /// <summary>Feuert bei einem Script-Fehler (Meldung, Zeile 1-basiert; 0 = unbekannt).</summary>
        public static event Action<string, int> OnScriptError;

        // ---- Registrierung / Lifecycle ------------------------------------------

        /// <summary>
        /// Razor CE: ScriptManager.OnLogin — registriert alle Commands/
        /// Expressions/Aliases genau einmal und laedt die Script-Dateien.
        /// </summary>
        public static void OnLogin()
        {
            Commands.Register();
            AgentCommands.Register();
            SpeechCommands.Register();
            TargetCommands.Register();
            Aliases.Register();
            Expressions.Register();

            // Razor CE: AllowLoop haengt an FeatureBit.LoopingMacros — der Port
            // kennt keine FeatureBits, Loops sind erlaubt.
            Lexer.AllowLoop = true;

            LoadScripts();
        }

        public static void OnLogout()
        {
            StopScript();
        }

        // ---- Script-Dateien (CE: Recurse ueber *.razor) --------------------------

        /// <summary>Laedt alle *.razor-Dateien (rekursiv; Unterordner = Category).</summary>
        public static void LoadScripts()
        {
            _scriptList.Clear();

            try
            {
                Recurse(ScriptPath, string.Empty);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Razor] Scripts laden fehlgeschlagen: {ex.Message}");
            }

            _scriptList.Sort((a, b) => string.Compare(a.ToString(), b.ToString(), StringComparison.OrdinalIgnoreCase));

            OnScriptsChanged?.Invoke();
        }

        private static void Recurse(string path, string category)
        {
            if (!Directory.Exists(path))
                return;

            foreach (string file in Directory.GetFiles(path, "*.razor"))
            {
                _scriptList.Add(new RazorScript
                {
                    Path = file,
                    Name = Path.GetFileNameWithoutExtension(file),
                    Category = category,
                    Lines = File.ReadAllLines(file)
                });
            }

            foreach (string dir in Directory.GetDirectories(path))
            {
                string name = Path.GetFileName(dir);
                Recurse(dir, string.IsNullOrEmpty(category) ? name : $"{category}\\{name}");
            }
        }

        public static RazorScript FindScript(string name)
        {
            foreach (RazorScript script in _scriptList)
            {
                if (script.Name.IndexOf(name, StringComparison.OrdinalIgnoreCase) != -1 ||
                    script.ToString().IndexOf(name, StringComparison.OrdinalIgnoreCase) != -1)
                    return script;
            }

            return null;
        }

        /// <summary>Speichert Zeilen in die Script-Datei (Editor-Save) und laedt die Liste neu.</summary>
        public static void SaveScript(RazorScript script, string[] lines)
        {
            if (script == null)
                return;

            File.WriteAllLines(script.Path, lines);
            script.Lines = lines;
            OnScriptsChanged?.Invoke();
        }

        /// <summary>Legt ein neues leeres Script an (Name ohne Endung) und laedt die Liste neu.</summary>
        public static RazorScript NewScript(string name)
        {
            Directory.CreateDirectory(ScriptPath);
            string file = Path.Combine(ScriptPath, name + ".razor");

            if (!File.Exists(file))
                File.WriteAllText(file, string.Empty);

            LoadScripts();
            return FindScript(name);
        }

        /// <summary>Loescht die Script-Datei (exakter Name) und laedt die Liste neu.
        /// Die Sicherheitsabfrage macht die UI VOR dem Aufruf.</summary>
        public static void DeleteScript(string name)
        {
            RazorScript script = _scriptList.Find(
                s => string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase));
            if (script == null)
                return;

            try
            {
                File.Delete(script.Path);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Razor] Script-Datei konnte nicht geloescht werden: {ex.Message}");
            }

            LoadScripts();
        }

        /// <summary>CE: RedrawScripts — UI-Refresh anstossen.</summary>
        public static void RedrawScripts()
        {
            OnScriptsChanged?.Invoke();
        }

        // ---- Play / Stop / Pause --------------------------------------------------

        /// <summary>Script per Name abspielen (CE: PlayScript(name); Kommando `script`).</summary>
        public static void PlayScript(string name)
        {
            RazorScript script = FindScript(name);

            if (script == null)
            {
                World.Player?.SendMessage(MsgLevel.Warning, $"Script '{name}' not found");
                return;
            }

            PlayScript(script.Lines, script.Name);
        }

        /// <summary>Script aus Zeilen abspielen (CE: PlayScript(lines)).</summary>
        public static void PlayScript(string[] lines, string name)
        {
            if (World.Player == null || lines == null)
                return;

            StopScript(); // CE: implizit — nur ein aktives Script

            if (MacroManager.Playing)
                MacroManager.Stop();

            try
            {
                Script script = new Script(Lexer.Lex(lines));

                _queuedScript = script;
                _queuedScriptName = name;
                _activeLines = lines;
                _activeName = name;
            }
            catch (SyntaxError e)
            {
                World.Player?.SendMessage(MsgLevel.Error, $"Script compile error on line {e.LineNumber + 1}: {e.Message}");
                Console.WriteLine($"[Razor/script] Compile-Fehler '{name}' Zeile {e.LineNumber + 1}: {e.Message}");
                Console.WriteLine($"[Razor/script]   > {e.Line?.Trim()}");
                DebugConsole.Script.Error($"Compile error '{name}' line {e.LineNumber + 1}: {e.Message}");
                OnScriptError?.Invoke(e.Message, e.LineNumber + 1);

                // Razor-Zusatz: stilles Logfile in CrashLogs (Dedupe 60s), kein Fenster.
                CrashReporter.ReportScriptError("Razor Script", name,
                    string.Join(Environment.NewLine, lines),
                    $"Compile error on line {e.LineNumber + 1}: {e.Message}\n> {e.Line?.Trim()}");
            }
        }

        public static void StopScript()
        {
            _queuedScript = null;
            _queuedScriptName = null;

            Interpreter.StopScript();

            if (ScriptRunning)
            {
                ScriptRunning = false;
                DebugConsole.Script.Info("Script stopped");
                OnScriptStopped?.Invoke();
            }
        }

        public static void PauseScript()
        {
            Interpreter.PauseScript();
        }

        public static void ResumeScript()
        {
            Interpreter.ResumeScript();
        }

        public static bool Paused { get; private set; }

        // ---- Pump (CE: ScriptTimer.OnTick, 25ms) ----------------------------------

        /// <summary>
        /// Vom Game-Thread pro Tick aufzurufen (RazorPlugin.OnTick). Startet
        /// das queued Script bzw. fuehrt das aktive weiter; Fehler stoppen das
        /// Script mit Meldung (CE-Verhalten).
        /// </summary>
        public static void OnTick()
        {
            try
            {
                if (!ClientProxy.IsBound)
                    return;

                if (_queuedScript != null)
                {
                    if (World.Player == null)
                        return;

                    Script script = _queuedScript;
                    string name = _queuedScriptName;
                    _queuedScript = null;
                    _queuedScriptName = null;

                    Interpreter.StartScript(script);

                    ScriptRunning = true;
                    _watch.Restart();
                    DebugConsole.Script.Info($"Script started: {name}");
                    OnScriptStarted?.Invoke(name);
                }
                else if (ScriptRunning)
                {
                    if (!Interpreter.ExecuteScript())
                    {
                        ScriptRunning = false;
                        _watch.Stop();
                        DebugConsole.Script.Info("Script ended");
                        OnScriptStopped?.Invoke();
                    }
                }
            }
            catch (RunTimeError ex)
            {
                // Erwarteter Script-Fehler (falsche Argumente etc.) — Zeile +
                // Zeilentext reichen, kein Stacktrace.
                ReportScriptError(ex, verbose: false);
            }
            catch (Exception ex)
            {
                // Unerwarteter Fehler (Bug im Port) — voller Kontext in die
                // Konsole: Zeile, Zeilentext, Exception-Typ und Stacktrace.
                ReportScriptError(ex, verbose: true);
            }
        }

        private static void ReportScriptError(Exception ex, bool verbose)
        {
            int line = Interpreter.CurrentLine;           // 0-basiert
            string name = _activeName ?? "(unnamed)";
            string text = (_activeLines != null && line >= 0 && line < _activeLines.Length)
                ? _activeLines[line].Trim()
                : "(unbekannt)";

            string kind = verbose ? ex.GetType().Name : "Script error";

            World.Player?.SendMessage(MsgLevel.Error,
                $"{kind} in '{name}' line {line + 1}: {ex.Message}  >>  {text}");

            Console.WriteLine($"[Razor/script] {kind} in '{name}' Zeile {line + 1}: {ex.Message}");
            Console.WriteLine($"[Razor/script]   > {text}");
            if (verbose)
                Console.WriteLine($"[Razor/script]   {ex}");

            DebugConsole.Script.Error($"{kind} in '{name}' line {line + 1}: {ex.Message}  >>  {text}");
            OnScriptError?.Invoke($"{ex.Message}  >>  {text}", line + 1);

            // Razor-Zusatz: stilles Logfile in CrashLogs (Dedupe 60s), kein Fenster.
            // Bei unerwarteten Fehlern (Bug im Port) wandert der Stacktrace mit.
            CrashReporter.ReportScriptError("Razor Script", name,
                _activeLines != null ? string.Join(Environment.NewLine, _activeLines) : null,
                verbose
                    ? $"{kind} on line {line + 1}: {ex.Message}\n> {text}\n\n{ex}"
                    : $"Script error on line {line + 1}: {ex.Message}\n> {text}");

            StopScript();
        }

        /// <summary>Aktuelle Ausfuehrungszeile (0-basiert) des aktiven Scripts.</summary>
        public static int CurrentLine => Interpreter.CurrentLine;
    }
}
