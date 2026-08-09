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

// UOSagas-Razor: Support-Typen fuer die portierte Lua-Engine (Phase 4b).
//
// Ersetzt die Client-Abhaengigkeiten der Kopie (CodeEditor.DebugConsole,
// ErrorMarkerManager.LuaError) durch Razor-Gegenstuecke:
// - DebugConsole: Ring-Puffer + Event, gespeist von print/Console.* der
//   Scripts und der Engine; die IDE zeigt ihn als Konsole an.
// - LuaError/ErrorSeverity: 1:1 wie der Client (ErrorMarkerManager.cs).
// Die Script-UI (UI.CreateWindow ...) lebt in Lua/API/LuaUIAPI.cs +
// Lua/UI/* (Client-Port) mit Avalonia-Darstellung in Razor.Avalonia/ScriptUi.

using System;
using System.Collections.Generic;

namespace Assistant.LuaEngine
{
    public class LuaError
    {
        public int Line { get; set; }
        public int StartColumn { get; set; }
        public int EndColumn { get; set; }
        public string Message { get; set; }
        public ErrorSeverity Severity { get; set; }

        public LuaError(int line, int startColumn, int endColumn, string message,
            ErrorSeverity severity = ErrorSeverity.Error)
        {
            Line = line;
            StartColumn = startColumn;
            EndColumn = endColumn;
            Message = message;
            Severity = severity;
        }
    }

    public enum ErrorSeverity
    {
        Info,
        Warning,
        Error
    }

    /// <summary>Minimal-Port des Client-LuaErrorParsers: extrahiert Zeilen-
    /// nummern aus Lua-CSharp-Fehlermeldungen ([string "…"]:N: / line N / N:).</summary>
    public static class LuaErrorParser
    {
        private static readonly System.Text.RegularExpressions.Regex LinePattern =
            new(@"(?:\[string ""[^""]*""\]:|line\s+|:)?(\d+):\s*(.*)",
                System.Text.RegularExpressions.RegexOptions.Compiled);

        public static List<LuaError> ParseMultipleErrors(string errorMessage, string scriptContent)
        {
            var errors = new List<LuaError>();
            if (string.IsNullOrEmpty(errorMessage))
                return errors;

            foreach (string line in errorMessage.Split('\n'))
            {
                var match = LinePattern.Match(line.Trim());
                if (match.Success && int.TryParse(match.Groups[1].Value, out int lineNumber))
                {
                    // Editor-Konvention: 0-basiert (wie der Client).
                    errors.Add(new LuaError(Math.Max(0, lineNumber - 1), 0, 0,
                        match.Groups[2].Value.Trim().Length > 0 ? match.Groups[2].Value.Trim() : line.Trim()));
                }
            }

            if (errors.Count == 0)
                errors.Add(new LuaError(0, 0, 0, errorMessage.Trim()));

            return errors;
        }
    }

    /// <summary>
    /// Script-Konsole: Ring-Puffer (500 Zeilen) + Changed-Event fuer die IDE.
    /// Statische Fassade wie im Client (DebugConsole.Lua.Info / DebugConsole.Debug).
    /// Der Puffer ist GETEILT — Lua- und Razor-Script-Kanal schreiben in
    /// dieselbe Konsole (wie die Client-DebugConsole beider Editoren);
    /// der Kanal-Tag in der Zeile unterscheidet die Quelle.
    /// </summary>
    public static class DebugConsole
    {
        public sealed class Channel
        {
            private readonly string _prefix;
            public Channel(string prefix) => _prefix = prefix;

            public void Info(string msg) => Append("info", msg);
            public void Warn(string msg) => Append("warn", msg);
            public void Error(string msg) => Append("error", msg);
            public void Debug(string msg) => Append("debug", msg);

            public void Clear()
            {
                lock (_lock)
                {
                    _lines.Clear();
                }

                Changed?.Invoke();
            }

            private void Append(string level, string msg)
            {
                string line = $"[{DateTime.Now:HH:mm:ss}] [{_prefix}/{level}] {msg}";
                lock (_lock)
                {
                    _lines.Add(line);
                    if (_lines.Count > 500)
                        _lines.RemoveAt(0);
                }

                Console.WriteLine($"[Razor/{_prefix}] {msg}");
                Changed?.Invoke();
            }
        }

        private static readonly object _lock = new();
        private static readonly List<string> _lines = new();

        public static readonly Channel Lua = new("lua");

        /// <summary>Kanal der Razor-Script-Engine (Start/Stop/Fehler).</summary>
        public static readonly Channel Script = new("script");

        /// <summary>Engine-interne Debug-Meldungen (Pause/Resume etc.).</summary>
        public static void Debug(string msg) => Lua.Debug(msg);

        public static event Action Changed;

        public static List<string> Snapshot()
        {
            lock (_lock)
            {
                return new List<string>(_lines);
            }
        }
    }
}

// Die frueheren D19-Stubs fuer LuaUIAPI/ScriptUIManager sind durch den echten
// Port ersetzt: Lua/API/LuaUIAPI.cs (Client-Kopie) + Lua/UI/* (Modell) +
// Razor.Avalonia/ScriptUi (Avalonia-Darstellung statt ImGui).
