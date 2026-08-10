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

// UOSagas-Razor: Einstiegspunkt der Avalonia-UI (Phase 3a).
//
// Die UI laeuft auf einem EIGENEN Hintergrund-Thread ("RazorUI") im
// Spielprozess — kein eigenes Programm, kein Main(). Der Host-Prozess gehoert
// dem Spiel, deshalb:
//  * ShutdownMode.OnExplicitShutdown (Fenster schliessen beendet NICHTS),
//  * Fenster-Schliessen versteckt nur (MainWindow.Closing -> Hide),
//  * alle Exceptions des UI-Threads werden hier gefangen und geloggt —
//    ein UI-Absturz darf das Spiel nie mitreissen.
//
// Einschraenkung (dokumentiert): Avalonia kann pro Prozess nur EINMAL
// initialisiert werden. Nach Stop() ist in derselben Spielsitzung kein
// erneutes Start() moeglich — Stop() gehoert deshalb in OnShutdown.

using System;
using System.Threading;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;

namespace Razor.UI
{
    public static class RazorUi
    {
        private static Thread _thread;
        private static volatile bool _started;
        private static volatile bool _frameworkReady;

        internal static MainWindow MainWindow;

        public static bool IsRunning => _started;

        /// <summary>Framework initialisiert (Dispatcher nutzbar) — fuer den Crash-Reporter.</summary>
        internal static bool FrameworkReady => _frameworkReady;

        /// <summary>Startet die UI auf einem eigenen Hintergrund-Thread (idempotent).</summary>
        public static void Start()
        {
            if (_started)
            {
                Show();
                return;
            }

            try
            {
                _started = true;
                _thread = new Thread(UiThreadMain)
                {
                    IsBackground = true,
                    Name = "RazorUI"
                };

                // Avalonias [STAThread]-Aequivalent fuer den eingebetteten
                // UI-Thread: ohne STA liefert Avalonia.Win32 keinen OleContext
                // (OleInitialize laeuft nie) — Clipboard-Aufrufe scheitern dann
                // mit CO_E_NOTINITIALIZED (unter Wine/Proton strikt erzwungen)
                // und die OLE-DragSource wird nicht registriert.
                if (OperatingSystem.IsWindows())
                    _thread.SetApartmentState(ApartmentState.STA);

                _thread.Start();
            }
            catch (Exception ex)
            {
                _started = false;
                Console.WriteLine($"[UOSagas Razor] UI-Start fehlgeschlagen: {ex}");
            }
        }

        private static void UiThreadMain()
        {
            try
            {
                AppBuilder.Configure<RazorApp>()
                    .UsePlatformDetect()
                    .StartWithClassicDesktopLifetime(
                        Array.Empty<string>(),
                        lifetime => lifetime.ShutdownMode = ShutdownMode.OnExplicitShutdown);
            }
            catch (Exception ex)
            {
                // Letzte Verteidigungslinie: nichts darf aus dem UI-Thread
                // herausblubbern (eine unbehandelte Exception auf irgendeinem
                // Thread wuerde den Spielprozess beenden). Normalerweise faengt
                // der Dispatcher.UnhandledException-Handler (RazorApp) alles ab;
                // landet doch etwas hier, ist die Haupt-Loop tot — dann laeuft
                // eine eigene Dispatcher-Frame nur fuer das Crash-Fenster.
                Console.WriteLine($"[UOSagas Razor] UI-Thread beendet mit Fehler: {ex}");
                TryShowCrashFrame(ex);
            }
            finally
            {
                _frameworkReady = false;
                _started = false;

                // UI ist tot — Crash-Fenster koennen nicht mehr gezeigt werden
                // (Reports landen weiter in Konsole + Data/CrashLogs).
                Assistant.CrashReporter.UiPresenter = null;

                Console.WriteLine("[UOSagas Razor] UI-Thread beendet.");
            }
        }

        /// <summary>Wird von RazorApp gerufen, sobald das Framework initialisiert ist.</summary>
        internal static void OnFrameworkReady()
        {
            _frameworkReady = true;
        }

        /// <summary>Crash-Fenster nach dem Tod der Haupt-Loop: das Framework ist
        /// noch initialisiert, also traegt eine eigene DispatcherFrame den Dialog
        /// (Razor selbst bleibt bis zum Spielneustart tot — Avalonia kann pro
        /// Prozess nur einmal starten).</summary>
        private static void TryShowCrashFrame(Exception ex)
        {
            if (!_frameworkReady)
                return;

            try
            {
                Assistant.CrashReport report = Assistant.CrashReporter.Capture(
                    ex, "UI thread (main loop terminated)", fatal: true);

                var frame = new DispatcherFrame();
                var window = new CrashReportWindow(report);
                window.Closed += (_, _) => frame.Continue = false;
                window.Show();
                window.Activate();

                Dispatcher.UIThread.PushFrame(frame);
            }
            catch (Exception frameEx)
            {
                Console.WriteLine($"[UOSagas Razor] Crash-Fenster (Fallback) fehlgeschlagen: {frameEx}");
            }
        }

        /// <summary>Fenster (wieder) anzeigen — von jedem Thread aufrufbar.</summary>
        public static void Show()
        {
            if (!_started || !_frameworkReady)
                return;

            try
            {
                Dispatcher.UIThread.Post(() =>
                {
                    MainWindow win = MainWindow;
                    if (win == null)
                        return;

                    win.Show();
                    win.Activate();
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[UOSagas Razor] UI-Show fehlgeschlagen: {ex}");
            }
        }

        /// <summary>Beendet die UI sauber (ohne Prozess-Exit). Fuer OnShutdown.</summary>
        public static void Stop()
        {
            if (!_started)
                return;

            try
            {
                if (_frameworkReady)
                {
                    Dispatcher.UIThread.Post(() =>
                    {
                        try
                        {
                            MainWindow win = MainWindow;
                            if (win != null)
                                win.AllowClose = true;

                            (Application.Current?.ApplicationLifetime as
                                IClassicDesktopStyleApplicationLifetime)?.Shutdown();
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"[UOSagas Razor] UI-Shutdown fehlgeschlagen: {ex}");
                        }
                    });
                }

                // Kurz auf sauberes Ende warten; der Thread ist Background —
                // haengt er, blockiert er den Spiel-Exit trotzdem nicht.
                _thread?.Join(TimeSpan.FromSeconds(3));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[UOSagas Razor] UI-Stop fehlgeschlagen: {ex}");
            }
            finally
            {
                _started = false;
                _frameworkReady = false;
                MainWindow = null;
            }
        }
    }
}
