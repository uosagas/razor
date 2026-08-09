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

// UOSagas-Razor: Hauptfenster im Razor-CE-Look (Phase 3b).
//
// Blaupause: Razor CE Razor/UI/Razor.Designer.cs (MainForm):
//  * ClientSize 530x372, FixedSingle (nicht resizable), Segoe UI 9pt.
//  * TabControl (2,0) 527x370 mit schmalen Tabs oben.
// Tabs wie im Phasenplan: General, Options, Display/Counters, Arm/Dress,
// Agents, Hot Keys, Macros, About (CE-Titel; die uebrigen CE-Tabs wie
// Skills/Filters/Scripts folgen in spaeteren Phasen).
//
// Threading-Modell (WICHTIG, aus 3a beibehalten):
//  * Die UI liest NIE direkt Kernzustand. Ein DispatcherTimer (500 ms) posted
//    eine Snapshot-Anfrage in die GameThread-Queue; RazorPlugin.OnTick baut
//    den UiSnapshot auf dem Game-Thread und posted ihn per Dispatcher zurueck.
//  * Alle Schreibzugriffe gehen ausschliesslich ueber GameThread.Post.

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;

namespace Razor.UI
{
    /// <summary>Gemeinsame Schnittstelle aller CE-Tabs fuer die Snapshot-Pumpe.</summary>
    public interface ICeTab
    {
        /// <summary>Traegt ein, welche Daten der Tab im naechsten Snapshot braucht (UI-Thread).</summary>
        void Contribute(UiRequest req);

        /// <summary>Snapshot in die Controls uebernehmen (UI-Thread).</summary>
        void Apply(UiSnapshot snap);
    }

    public class MainWindow : Window
    {
        /// <summary>Fenstertitel wie Razor CE ("Razor v{0}") mit unserer Version.</summary>
        public static string TitleText
        {
            get
            {
                try
                {
                    Version v = typeof(MainWindow).Assembly.GetName().Version;
                    return $"UOSagas Razor v{v?.Major ?? 0}.{v?.Minor ?? 1}";
                }
                catch
                {
                    return "UOSagas Razor v0.1";
                }
            }
        }

        private readonly List<ICeTab> _tabs = new List<ICeTab>();
        private readonly DispatcherTimer _pump;

        private bool _snapshotPending;
        private DateTime _snapshotRequestedAt;

        /// <summary>true = Fenster darf wirklich zugehen (nur beim UI-Shutdown).</summary>
        internal bool AllowClose;

        public MainWindow()
        {
            // Razor CE MainForm: ClientSize 530x372, FormBorderStyle-Verhalten
            // FixedSingle -> nicht resizable.
            Title = TitleText;
            Width = 530;
            Height = 372;
            CanResize = false;
            ShowInTaskbar = true;

            var icon = Branding.TryLoadWindowIcon();
            if (icon != null)
                Icon = icon;
            Background = Ce.WindowBackground;
            FontFamily = new FontFamily("Segoe UI");
            FontSize = Ce.FontSize;

            var general = new GeneralTab(this);
            var options = new OptionsTab();
            var display = new DisplayTab();
            var dress = new DressTab();
            var agents = new AgentsTab();
            var hotkeys = new HotKeysTab(this);
            var macros = new MacrosTab(this);
            var scripts = new ScriptsTab(this);
            var lua = new LuaTab(this);
            var vscripts = new VScriptsTab();
            var about = new AboutTab();

            _tabs.Add(general);
            _tabs.Add(options);
            _tabs.Add(display);
            _tabs.Add(dress);
            _tabs.Add(agents);
            _tabs.Add(hotkeys);
            _tabs.Add(macros);
            _tabs.Add(scripts);
            _tabs.Add(lua);
            _tabs.Add(vscripts);
            // About treibt die Live-Diagnose (World Tracking) ueber denselben Pump —
            // deshalb hier ebenfalls registrieren, nicht nur als TabItem-Inhalt.
            _tabs.Add(about);

            // Razor CE "tabs": Location (2,0), Size 527x370.
            var tabs = new TabControl
            {
                Margin = new Thickness(2, 0, 1, 2),
                Padding = new Thickness(2)
            };

            Ce.Tab(tabs, "General", general);
            Ce.Tab(tabs, "Options", options);
            Ce.Tab(tabs, "Display/Counters", display);
            Ce.Tab(tabs, "Arm/Dress", dress);
            Ce.Tab(tabs, "Agents", agents);
            Ce.Tab(tabs, "Hot Keys", hotkeys);
            Ce.Tab(tabs, "Macros", macros);
            Ce.Tab(tabs, "Scripts", scripts);
            Ce.Tab(tabs, "Lua", lua);
            Ce.Tab(tabs, "VScripts", vscripts);
            Ce.Tab(tabs, "About", about);

            Content = tabs;

            // Razor ist bewusst NICHT schliessbar: der Prozess gehoert dem Spiel und
            // darf nie beendet werden. Das native X (und Alt+F4) lassen sich unter
            // Avalonia nicht sauber ausblenden — deshalb: jeder Close-Versuch wird
            // abgebrochen (e.Cancel) und das Fenster nur versteckt. Wirklich zugehen
            // darf es einzig beim UI-Shutdown (RazorUi.Stop setzt AllowClose=true).
            // Zusammen mit ShutdownMode.OnExplicitShutdown (RazorApp) kann das
            // Verstecken so nie einen ungewollten Prozess-Exit ausloesen.
            Closing += (s, e) =>
            {
                if (AllowClose)
                    return;

                e.Cancel = true;
                Hide();
            };

            // Unter Windows das native X (und damit Alt+F4) hart ausgrauen, statt es
            // nur abzufangen. Der Closing->Hide-Handler oben bleibt als zusaetzliche
            // Absicherung (und deckt Nicht-Windows-Plattformen ab).
            Opened += (s, e) => DisableNativeCloseButton();

            _pump = new DispatcherTimer(TimeSpan.FromMilliseconds(500), DispatcherPriority.Background, OnPumpTick);
            _pump.Start();

            // Erste Befuellung sofort anfordern.
            RequestSnapshot();
        }

        private void OnPumpTick(object sender, EventArgs e)
        {
            if (!IsVisible)
                return;

            // Verlorene Anfragen (z. B. Game-Thread pausiert) nach 3 s freigeben.
            if (_snapshotPending && (DateTime.UtcNow - _snapshotRequestedAt).TotalSeconds < 3)
                return;

            RequestSnapshot();
        }

        /// <summary>Fordert einen frischen Snapshot vom Game-Thread an (UI-Thread only).</summary>
        internal void RequestSnapshot()
        {
            _snapshotPending = true;
            _snapshotRequestedAt = DateTime.UtcNow;

            var req = new UiRequest();
            foreach (ICeTab tab in _tabs)
            {
                try
                {
                    tab.Contribute(req);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[UOSagas Razor] Snapshot-Anfrage fehlgeschlagen: {ex}");
                }
            }

            GameThread.Post(() =>
            {
                UiSnapshot snap = null;
                try
                {
                    snap = UiSnapshotBuilder.Build(req);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[UOSagas Razor] Snapshot fehlgeschlagen: {ex}");
                }

                Dispatcher.UIThread.Post(() =>
                {
                    _snapshotPending = false;
                    if (snap == null)
                        return;

                    foreach (ICeTab tab in _tabs)
                    {
                        try
                        {
                            tab.Apply(snap);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"[UOSagas Razor] Snapshot-Anzeige fehlgeschlagen: {ex}");
                        }
                    }
                });
            });
        }

        // --- Win32: natives Close-X ausgrauen ---------------------------------
        private const uint SC_CLOSE = 0xF060;
        private const uint MF_BYCOMMAND = 0x0;
        private const uint MF_GRAYED = 0x1;

        [DllImport("user32.dll")]
        private static extern IntPtr GetSystemMenu(IntPtr hWnd, bool bRevert);

        [DllImport("user32.dll")]
        private static extern int EnableMenuItem(IntPtr hMenu, uint uIDEnableItem, uint uEnable);

        /// <summary>
        /// Graut unter Windows das native Close-X (X-Button + Alt+F4) im System-
        /// menue aus. Auf anderen Plattformen passiert nichts — dort greift der
        /// Closing->Hide-Fallback. Ein Fehler darf die UI nie crashen.
        /// </summary>
        private void DisableNativeCloseButton()
        {
            try
            {
                if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    return;

                IntPtr hwnd = TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;
                if (hwnd == IntPtr.Zero)
                    return;

                IntPtr hMenu = GetSystemMenu(hwnd, false);
                if (hMenu == IntPtr.Zero)
                    return;

                EnableMenuItem(hMenu, SC_CLOSE, MF_BYCOMMAND | MF_GRAYED);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[UOSagas Razor] Close-Button ausgrauen fehlgeschlagen: {ex}");
            }
        }
    }
}
