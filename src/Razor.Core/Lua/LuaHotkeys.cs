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

// UOSagas-Razor: "Lua: <name>"-Hotkeys (Phase 4b) — Razor-Zusatz.
//
// Bewusst NICHT in LuaEngineService (die Datei ist eine diff-arme Client-
// Kopie, D25/D27); die Hotkey-Anbindung ist Razor-Infrastruktur wie
// VScriptService.RegisterVScriptHotkey: pro .lua-Datei ein dynamischer
// Hotkey in HKCategory.Scripts. Registrierung MUSS vor Config.LoadLastProfile
// laufen, damit gespeicherte Belegungen greifen (RazorPlugin ruft Initialize
// direkt nach LuaEngineService.Initialize).

using System;
using System.Collections.Generic;
using System.Linq;
using Assistant.LuaEngine;

namespace Assistant.LuaEngine
{
    public static class LuaHotkeys
    {
        private static readonly HashSet<string> _registered = new(StringComparer.OrdinalIgnoreCase);

        public static void Initialize()
        {
            Refresh();
        }

        /// <summary>Gleicht die Hotkey-Liste mit LuaEngineService.Files ab
        /// (nach New/Delete/Refresh aufrufen).</summary>
        public static void Refresh()
        {
            if (LuaEngineService.Files == null)
                return;

            List<string> current = LuaEngineService.Files.Keys.ToList();

            foreach (string name in current)
            {
                if (_registered.Add(name))
                {
                    HotKey.Add(HKCategory.Scripts, HKSubCat.None, $"Lua: {name}",
                        new HotKeyCallbackState(OnHotKey), name);
                }
            }

            foreach (string name in _registered.Except(current, StringComparer.OrdinalIgnoreCase).ToList())
            {
                HotKey.Remove($"Lua: {name}");
                _registered.Remove(name);
            }
        }

        private static void OnHotKey(ref object state)
        {
            string name = (string) state;
            string content = LuaEngineService.LoadFile(name);

            if (!string.IsNullOrEmpty(content))
                LuaEngineService.RunScript(content);
        }
    }
}
