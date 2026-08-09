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

// UOSagas-Razor: Kommando-Queue UI-Thread -> Game-Thread (Phase 3a).
//
// Der Razor-Kern (Config/MacroManager/Agents/World) ist NICHT threadsicher und
// gehoert dem Game-Thread (RazorPlugin.OnTick). Die Avalonia-UI laeuft auf
// einem eigenen Thread und darf deshalb NIE direkt in den Kern greifen.
// Stattdessen enqueued die UI hier Aktionen; RazorPlugin.OnTick ruft
// DrainOnGameThread() auf und fuehrt sie auf dem Game-Thread aus.
//
// Ergebnisse zurueck zur UI gehen ueber Avalonia's Dispatcher.UIThread.Post
// (siehe MainWindow: Snapshot-Pump).

using System;
using System.Collections.Concurrent;

namespace Razor.UI
{
    public static class GameThread
    {
        /// <summary>Aktionen, die auf dem Game-Thread ausgefuehrt werden sollen.</summary>
        public static readonly ConcurrentQueue<Action> Queue = new ConcurrentQueue<Action>();

        /// <summary>Enqueued eine Aktion fuer den Game-Thread (threadsicher, von der UI aufrufbar).</summary>
        public static void Post(Action action)
        {
            if (action != null)
                Queue.Enqueue(action);
        }

        /// <summary>
        /// Arbeitet die Queue ab — NUR vom Game-Thread aufrufen (RazorPlugin.OnTick).
        /// Fehler einzelner Aktionen werden geloggt und reissen weder Tick noch Spiel mit.
        /// </summary>
        public static void DrainOnGameThread(int maxActions = 64)
        {
            int n = 0;
            while (n++ < maxActions && Queue.TryDequeue(out Action action))
            {
                try
                {
                    action();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[UOSagas Razor] UI-Kommando fehlgeschlagen: {ex}");
                }
            }
        }
    }
}
