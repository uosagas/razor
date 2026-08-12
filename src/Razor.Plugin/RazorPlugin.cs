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

// UOSagas-Razor: Plugin-Skeleton (Phase 2a/2b/2c).
// Wird vom Bootstrap-Host des UOSagas-Clients ueber IAssistantPlugin geladen.
// Phase 2a: Config/Profile laden + Verzeichnisstruktur anlegen, Pakete zaehlen.
// Phase 2b: Weltmodell (World/Item/Mobile/PlayerData) + Paket-Dispatch.
// Phase 2c: ClientProxy-Bindung, Timer.Slice pro Frame, Macro-Engine/Recorder.
// Phase 2d: Counters, Agents (UseOnce/Sell/Scavenger/Organizer/AutoSearch-
//           Exemptions/Buy/Restock/Ignore) und DressLists inkl. Profil-Sektionen.
// Spaeter: Hotkeys, Scripts.

using System;
using System.IO;
using System.Reflection;
using Assistant;
using Assistant.Agents;
using Assistant.Macros;
using Razor.UI;
using UOSagas.AssistantApi;

namespace Razor.Plugin
{
    public class RazorPlugin : IAssistantPlugin
    {
        private IClientServices _client;

        private long _packetsFromServer;
        private long _packetsToServer;

        public string Name => "UOSagas Razor";

        public IClientServices Client => _client;

        public long PacketsFromServer => _packetsFromServer;
        public long PacketsToServer => _packetsToServer;

        public void Initialize(IClientServices client)
        {
            _client = client;

            // Phase 2c: einzige AssistantApi-Bruecke fuer Razor.Core.
            ClientProxy.Bind(client);

            // Crash-Reporter so frueh wie moeglich: AppDomain-/Task-Auffangnetze
            // + Logfiles nach Data/CrashLogs; das Fenster haengt sich spaeter
            // ueber RazorApp (UiPresenter) ein.
            CrashReporter.Initialize();

            // Versionsmeldung fuer das Server-Gate: dieselbe Assembly-Version,
            // die auch der Launcher anzeigt (UOSagas.Razor.dll, z. B. 0.1.0).
            AssistantInfo.Version = typeof(RazorPlugin).Assembly.GetName().Version?.ToString(3) ?? "0.0.0";

            try
            {
                InitializeCore();
            }
            catch (Exception ex)
            {
                // Sichtbar melden (Crash-Fenster, sobald die UI steht sonst
                // Konsole + Logfile) und wie bisher an den Bootstrap-Host
                // weiterreichen (der loggt und laesst das Plugin deaktiviert).
                CrashReporter.Report(ex, "Initialize");
                throw;
            }
        }

        private void InitializeCore()
        {

            // Datenverzeichnis: ./Data neben der Plugin-Assembly
            // (Config legt darunter Profiles/, Macros/, Scripts/ an).
            string baseDir;
            try
            {
                baseDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            }
            catch
            {
                baseDir = AppContext.BaseDirectory;
            }

            if (string.IsNullOrEmpty(baseDir))
                baseDir = AppContext.BaseDirectory;

            string dataDir = Path.Combine(baseDir, "Data");
            Config.Initialize(dataDir);

            // Phase 2d/3c: Counters/Agents/DressLists/HotKeys VOR dem
            // Profil-Laden initialisieren (registrieren ihre Profil-Sektionen;
            // idempotent). Reihenfolge = Schreibreihenfolge im Profil (wie
            // Razor CE: counters, agents, dresslists, hotkeys) — und die
            // hotkeys-Sektion muss NACH dresslists laden, damit die
            // dynamischen Dress-Hotkeys beim Belegen schon existieren.
            Counter.Initialize();
            Agent.Initialize();
            DressList.Initialize();
            HotKey.Initialize();

            // Phase 4a: Script-Variablen als Profil-Sektion ("scriptvariables",
            // CE-kompatibel) — VOR Config.LoadLastProfile.
            Assistant.Scripts.ScriptVariables.Initialize();

            // Macro-Variablen als Profil-Sektion ("macrovariables", CE-kompatibel)
            // — VOR Config.LoadLastProfile.
            Assistant.Macros.MacroVariables.Initialize();

            // Feature-Paritaet: Container-Labels + Overhead-Trigger als
            // Profil-Sektionen ("containerlabels"/"overheadmessages",
            // CE-kompatibel) — VOR Config.LoadLastProfile.
            Assistant.Core.ContainerLabels.Initialize();
            Assistant.Core.OverheadManager.Initialize();

            // Phase 5: VScript-Service — laedt Data/VScripts (gleicher Ordner
            // wie der integrierte Assistant, Graphen 1:1 austauschbar) und
            // registriert pro Script einen "VScript: <name>"-Hotkey. VOR
            // LoadLastProfile, damit gespeicherte Belegungen greifen.
            Assistant.VScripts.Engine.VScriptService.Initialize();

            // Phase 4b: Lua-Engine (Client-Dialekt 1:1, LuaCSharp) — laedt
            // Data/Profiles/Scripts (gleicher Ordner wie der integrierte
            // Assistant, .lua-Dateien austauschbar) und registriert pro Datei
            // einen "Lua: <name>"-Hotkey. VOR LoadLastProfile (Belegungen!).
            Assistant.LuaEngine.LuaEngineService.Initialize();
            Assistant.LuaEngine.LuaHotkeys.Initialize();

            // Phase 3c: alle Hotkey-Registrierungen muessen vor dem
            // Profil-Laden stehen, sonst gehen gespeicherte Belegungen ins Leere:
            // Weltmodell-/Macro-Handler (registriert Targeting-Hotkeys), ...
            PacketHandlers.Initialize();

            // ... Macro-Liste (registriert "Play: <name>"-Hotkeys je Macro), ...
            MacroManager.Initialize();

            // ... Dress/Undress-Hotkeys (Arm/Disarm, Undress All/Hands/...), ...
            Assistant.HotKeys.UndressHotKeys.Initialize();

            // Phase 3c2: voller Razor-CE-Hotkey-Umfang — Spells (alle Schulen),
            // Use-Skills, Items/Potions/Bandagen/Misc und Special Moves.
            // Ebenfalls VOR Config.LoadLastProfile (Belegungen!).
            Spell.Initialize();
            Assistant.HotKeys.SkillHotKeys.Initialize();
            Assistant.HotKeys.UseHotKeys.Initialize();
            SpecialMoves.Initialize();

            // Sagas-Zusatz: die sechs Bard-Songs (Spell-IDs 701-706) als
            // eigene Hotkey-Kategorie, wie im eingebauten Assistant.
            Assistant.HotKeys.SongHotKeys.Initialize();

            // Phase 4a: Razor-Scripting (1:1 CE) — registriert alle Script-
            // Commands/Expressions/Aliases und laedt die *.razor-Dateien.
            // Nach den Hotkey-Systemen (das `hotkey`-Kommando sucht per Name).
            Assistant.Scripts.ScriptManager.OnLogin();

            // ... und "Show Razor" (UI-Bruecke; Razor.Core kennt kein Avalonia).
            KeyData showRazor = HotKey.Add(HKCategory.Misc, HKSubCat.None, "Show Razor",
                new HotKeyCallback(RazorUi.Show));
            showRazor.Global = true; // funktioniert auch im Login-Screen
            showRazor.SetDefault((int) Assistant.Keys.U, ModKeys.Control | ModKeys.Shift);

            // Legt Profiles/ + Macros/ an und laedt das zuletzt genutzte Profil
            // (bzw. erzeugt/speichert "default", wenn keines existiert).
            Config.LoadLastProfile();
            Config.LoadCharList();

            // Phase 3a: Avalonia-UI auf eigenem Hintergrund-Thread starten
            // (nach Config-Load; ein UI-Fehler darf das Spiel nie mitreissen).
            try
            {
                RazorUi.Start();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[{Name}] UI konnte nicht gestartet werden: {ex}");
            }

            Console.WriteLine($"[{Name}] initialisiert.");
            Console.WriteLine($"[{Name}] Datenverzeichnis: {dataDir}");
            Console.WriteLine($"[{Name}] Profil: {Config.CurrentProfile?.Name ?? "(none)"}");
            Console.WriteLine($"[{Name}] Capabilities: {_client?.Capabilities}");
        }

        public void OnConnected()
        {
            Diagnostics.Connected = true;
            Console.WriteLine($"[{Name}] verbunden.");
        }

        public void OnDisconnected()
        {
            try
            {
                OnDisconnectedCore();
            }
            catch (Exception ex)
            {
                CrashReporter.Report(ex, "Game thread: OnDisconnected");
            }
        }

        private void OnDisconnectedCore()
        {
            Diagnostics.Connected = false;
            Console.WriteLine($"[{Name}] getrennt.");

            // Phase 2c: laufende Macros/Queues anhalten.
            MacroManager.Stop();
            ActionQueue.Stop();

            // Phase 4a: laufendes Razor-Script stoppen.
            Assistant.Scripts.ScriptManager.StopScript();

            // Phase 2d: offene Vendor-Kauflisten verwerfen (wie Razor CE).
            BuyAgent.OnDisconnected();

            // Wie Razor CE beim Disconnect (Client/ClassicUO.cs):
            // Player = null, Items/Mobiles leeren.
            World.Clear();

            try
            {
                Config.Save();
            }
            catch
            {
            }
        }

        public void OnTick()
        {
            // Crash-Reporter statt stillem Konsolen-Log: der Bootstrap faengt
            // Exceptions zwar ab, aber der User sah nie etwas davon.
            try
            {
                OnTickCore();
            }
            catch (Exception ex)
            {
                CrashReporter.Report(ex, "Game thread: OnTick");
            }
        }

        private void OnTickCore()
        {
            // Phase 3a: Kommandos der Avalonia-UI auf dem Game-Thread ausfuehren.
            GameThread.DrainOnGameThread();

            // Phase 2c: treibt MacroTimer/ActionQueue (Razor-CE-Timer-Pump).
            Timer.Slice();

            // Phase 4a: Razor-Script-Pump (CE: 25ms-ScriptTimer; hier pro Tick).
            Assistant.Scripts.ScriptManager.OnTick();

            // Phase 4b: Lua-Gump-Aktionen (Reply/Close/Send) laufen ueber eine
            // Main-Thread-Queue — der Client pumpt sie im GameController, wir
            // hier. Ohne den Pump kommt kein Button-Klick je beim Server an.
            Assistant.LuaEngine.API.LuaGumpsAPI.ProcessMainThreadQueue();

            // UO-Titelleiste (TitleBarDisplay), selbst auf 250 ms gedrosselt.
            Assistant.Core.TitleBar.OnTick();
        }

        public bool OnPacket(bool fromServer, byte[] data)
        {
            if (data == null || data.Length == 0)
                return true;

            if (fromServer)
                _packetsFromServer++;
            else
                _packetsToServer++;

            int id = data[0];
            Diagnostics.NotePacket(fromServer, id);

            // Der Mirror liefert genau EIN vollstaendiges Paket pro Aufruf —
            // kein Framing noetig. GetPacketLength liefert -1 fuer Pakete mit
            // variabler Laenge (2-Byte-Laengenfeld nach der Paket-ID).
            short fixedLen = _client?.GetPacketLength(id) ?? -1;
            bool dynLength = fixedLen < 0;

            try
            {
                PacketReader reader = new PacketReader(data, dynLength);

                bool blocked = fromServer
                    ? PacketHandler.OnServerPacket(id, reader, null)
                    : PacketHandler.OnClientPacket(id, reader, null);

                return !blocked;
            }
            catch (Exception ex)
            {
                // Paket unveraendert durchreichen — ein Handler-Fehler darf den
                // Datenstrom nicht anhalten (Dedupe verhindert Fenster-Stuerme).
                CrashReporter.Report(ex, $"Game thread: OnPacket 0x{id:X2}");
                return true;
            }
        }

        public bool OnHotkey(int key, int modifier, bool pressed)
        {
            // Phase 3c: SDL-Keycode/KMOD -> WinForms-Keys/ModKeys mappen und
            // an das HotKey-System dispatchen. Rueckgabe true = an UO
            // durchreichen (CE-Semantik: hk.SendToUO), false = verschlucken.
            if (!pressed)
                return true;

            Assistant.Keys keys = KeyMap.ToKeys(key);
            if (keys == Assistant.Keys.None)
                return true;

            try
            {
                return HotKey.OnKeyDown((int) keys, KeyMap.ToModKeys(modifier));
            }
            catch (Exception ex)
            {
                CrashReporter.Report(ex, "Game thread: OnHotkey");
                return true;
            }
        }

        public void OnMouse(int button, int wheel)
        {
            // Maus-Hotkeys (Wheel/Mitte/XButton). button traegt im Low-Word die
            // rohe SDL-Buttonnummer (2/4/5 — Links/Rechts filtert der Client)
            // und im High-Word den SDL-KMOD-Status (Sagas-Erweiterung; alte
            // Clients senden dort 0 -> ModKeys.None). Mapping in KeyMap.
            try
            {
                if (KeyMap.TryTranslateMouse(button, wheel, out int ceButton, out ModKeys mod))
                    HotKey.OnMouse(ceButton, wheel, mod);
            }
            catch (Exception ex)
            {
                CrashReporter.Report(ex, "Game thread: OnMouse");
            }
        }

        public void OnPlayerPositionChanged(int x, int y, int z)
        {
            try
            {
                if (World.Player != null)
                {
                    World.Player.Position = new Point3D(x, y, z);

                    // Wie der integrierte Assistant (TryOpenCorpses bei Bewegung):
                    // Fernkampf-Leichen fallen ausserhalb der CorpseRange und
                    // oeffnen erst, wenn man hinlaeuft.
                    PacketHandlers.CheckAutoOpenCorpses();

                    // Razor CE: PlayerData.OnPositionChanging — Stealth-Schritte
                    // zaehlen und Tueren in Blickrichtung oeffnen.
                    StealthSteps.OnMove();
                    World.Player.AutoOpenDoors();
                }
            }
            catch (Exception ex)
            {
                CrashReporter.Report(ex, "Game thread: OnPlayerPositionChanged");
            }
        }

        public void OnShutdown()
        {
            // Phase 3a: UI sauber beenden (blockiert max. kurz; Prozess-Exit
            // macht ausschliesslich das Spiel).
            try
            {
                RazorUi.Stop();
            }
            catch
            {
            }

            try
            {
                Config.Save();
            }
            catch
            {
            }

            Console.WriteLine(
                $"[{Name}] beendet. Pakete: {_packetsFromServer} vom Server, {_packetsToServer} zum Server.");
        }
    }
}
