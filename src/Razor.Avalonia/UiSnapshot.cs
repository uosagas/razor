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

// UOSagas-Razor: Unveraenderliche Momentaufnahme des Kernzustands fuer die UI
// (Phase 3a, erweitert in 3b um Properties/Agents/Counters/DressLists).
// Build() laeuft auf dem GAME-Thread (via GameThread.Post), das Ergebnis wird
// per Dispatcher.UIThread.Post an die UI uebergeben. Die UI liest danach nur
// noch diese Kopie — nie den lebenden Kernzustand.

using System;
using System.Collections.Generic;
using System.Linq;
using Assistant;
using Assistant.Agents;
using Assistant.Macros;

namespace Razor.UI
{
    /// <summary>Was die UI fuer den naechsten Snapshot braucht (UI-Thread baut, Game-Thread liest).</summary>
    public sealed class UiRequest
    {
        public string SelectedMacroFile;
        public HashSet<string> BoolProps = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        public HashSet<string> TextProps = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        public int AgentIndex = -1;
        public string DressListName;

        /// <summary>Agent-Index, dessen schwebendes Add-Target (Buy/Restock) abgeholt werden soll.</summary>
        public int PendingTargetAgentIndex = -1;
    }

    public sealed class MacroInfo
    {
        public string Name;
        public string Filename;
        public bool Loop;
    }

    public sealed class AgentInfo
    {
        public string Display; // Agent.ToString(): "Name (Alias)"
        public string Kind;    // Typname, z. B. "OrganizerAgent"
    }

    public sealed class CounterInfo
    {
        public string Name;
        public string Format;
        public bool Enabled;
        public int Amount;
    }

    /// <summary>Ein registrierter Hotkey (Phase 3c) — Identitaet: LocName != 0 ? LocName : StrName.</summary>
    public sealed class HotKeyInfo
    {
        public int LocName;
        public string StrName;
        public string DisplayName;
        public HKCategory Category;
        public HKSubCat SubCat;
        public int Key;
        public ModKeys Mod;
        public bool SendToUO;
        public string Command;

        /// <summary>Stabile Identitaet fuer Tree-Selektion/GameThread-Kommandos.</summary>
        public string Identity => LocName != 0 ? $"L:{LocName}" : $"S:{StrName}";
    }

    public sealed class UiSnapshot
    {
        public string ProfileName;
        public List<string> Profiles = new List<string>();

        public bool Recording;
        public bool Playing;
        public string CurrentMacroFile;

        public List<MacroInfo> Macros = new List<MacroInfo>();

        /// <summary>Aktionsliste (action.ToString()) des angefragten Macros, sonst leer.</summary>
        public string SelectedMacroFile;
        public List<string> SelectedMacroActions = new List<string>();
        public bool SelectedMacroLoop;

        /// <summary>Angefragte Bool-Profil-Properties.</summary>
        public Dictionary<string, bool> BoolProps =
            new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);

        /// <summary>Angefragte String/Int/Double-Properties (als String).</summary>
        public Dictionary<string, string> TextProps =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        // --- Agents-Tab ---------------------------------------------------
        public List<AgentInfo> Agents = new List<AgentInfo>();
        public int AgentIndex = -1;
        public string AgentKind;
        public List<string> AgentItems = new List<string>();
        public bool? AgentEnabled; // null = Agent hat keinen Enable-Zustand
        public bool AgentHasHotBag; // Sell: HotBag gesetzt (Button-Beschriftung Set/Clear)

        /// <summary>Abgeholtes Add-Target von Buy/Restock (Grafik-ID), sonst -1.</summary>
        public int PendingTargetAgentIndex = -1;
        public int PendingTargetGfx = -1;

        // --- Display/Counters-Tab ------------------------------------------
        public List<CounterInfo> Counters = new List<CounterInfo>();

        // --- Arm/Dress-Tab --------------------------------------------------
        public List<string> DressLists = new List<string>();
        public string DressListName;
        public List<string> DressItems = new List<string>();

        // --- Hot Keys-Tab ----------------------------------------------------
        public List<HotKeyInfo> HotKeys = new List<HotKeyInfo>();
        public bool HotKeysEnabled = true;
        public string HotKeyStatus = string.Empty;

        // --- About/Diagnose-Tab ----------------------------------------------
        /// <summary>Live-Weltzustand (Diagnostics.Snapshot), Game-Thread befuellt.</summary>
        public WorldStatus Status;

        /// <summary>Haeufigste empfangene Paket-IDs als "0xXX: N" (Diagnostics.TopRecv).</summary>
        public List<string> TopPackets = new List<string>();
    }

    public static class UiSnapshotBuilder
    {
        /// <summary>NUR auf dem Game-Thread aufrufen.</summary>
        public static UiSnapshot Build(UiRequest req)
        {
            var snap = new UiSnapshot();
            req = req ?? new UiRequest();

            try
            {
                snap.ProfileName = Config.CurrentProfile?.Name;
                snap.Profiles = Config.GetProfileList();
            }
            catch
            {
            }

            BuildMacros(snap, req.SelectedMacroFile);
            BuildProps(snap, req);
            BuildAgents(snap, req.AgentIndex);
            BuildCounters(snap);
            BuildDressLists(snap, req.DressListName);
            BuildHotKeys(snap);
            BuildDiagnostics(snap);
            CollectPendingTarget(snap, req.PendingTargetAgentIndex);

            return snap;
        }

        /// <summary>
        /// Live-Diagnose fuer den About-Tab (World Tracking + gesehene Pakete).
        /// Laeuft mit — auch ohne ausgewaehlten Tab —, solange das Fenster den
        /// Pump treibt, damit der User den Weltzustand jederzeit sieht.
        /// </summary>
        private static void BuildDiagnostics(UiSnapshot snap)
        {
            try
            {
                snap.Status = Diagnostics.Snapshot();
                snap.TopPackets = Diagnostics.TopRecv(15)
                    .Select(kv => $"0x{kv.Key:X2}: {kv.Value}")
                    .ToList();
            }
            catch
            {
            }
        }

        private static void BuildMacros(UiSnapshot snap, string selectedMacroFile)
        {
            try
            {
                snap.Recording = MacroManager.Recording;
                snap.Playing = MacroManager.Playing;
                snap.CurrentMacroFile = MacroManager.Current?.Filename;
            }
            catch
            {
            }

            try
            {
                foreach (Macro m in MacroManager.List)
                {
                    snap.Macros.Add(new MacroInfo
                    {
                        Name = m.ToString(), // relativer Pfad ohne .macro (wie Razor CE Tree)
                        Filename = m.Filename,
                        Loop = m.Loop
                    });
                }

                if (!string.IsNullOrEmpty(selectedMacroFile))
                {
                    Macro sel = FindMacro(selectedMacroFile);
                    if (sel != null)
                    {
                        // Wie Razor CE: Macro beim Anwaehlen aus der Datei laden
                        // (nicht waehrend Aufnahme/Wiedergabe dazwischenfunken).
                        if (!sel.Loaded && !sel.Recording && !sel.Playing)
                        {
                            try
                            {
                                sel.Load();
                            }
                            catch
                            {
                            }
                        }

                        snap.SelectedMacroFile = sel.Filename;
                        snap.SelectedMacroLoop = sel.Loop;

                        foreach (object action in sel.Actions)
                            snap.SelectedMacroActions.Add(action?.ToString() ?? string.Empty);
                    }
                }
            }
            catch
            {
            }
        }

        private static void BuildProps(UiSnapshot snap, UiRequest req)
        {
            foreach (string prop in req.BoolProps)
            {
                try
                {
                    if (Config.CurrentProfile != null && Config.CurrentProfile.HasProperty(prop))
                        snap.BoolProps[prop] = Config.GetBool(prop);
                }
                catch
                {
                }
            }

            foreach (string prop in req.TextProps)
            {
                try
                {
                    if (Config.CurrentProfile != null && Config.CurrentProfile.HasProperty(prop))
                        snap.TextProps[prop] = Config.GetProperty(prop)?.ToString() ?? string.Empty;
                }
                catch
                {
                }
            }
        }

        private static void BuildAgents(UiSnapshot snap, int agentIndex)
        {
            try
            {
                foreach (Agent a in Agent.List)
                {
                    snap.Agents.Add(new AgentInfo
                    {
                        Display = a.ToString(),
                        Kind = a.GetType().Name
                    });
                }

                if (agentIndex >= 0 && agentIndex < Agent.List.Count)
                {
                    Agent a = Agent.List[agentIndex];
                    snap.AgentIndex = agentIndex;
                    snap.AgentKind = a.GetType().Name;
                    FillAgentItems(snap, a);
                }
            }
            catch
            {
            }
        }

        /// <summary>Item-Liste + Enabled-Zustand des Agents (Anzeige wie Razor CE OnSelected).</summary>
        private static void FillAgentItems(UiSnapshot snap, Agent a)
        {
            switch (a)
            {
                case UseOnceAgent useOnce:
                    foreach (object o in useOnce.Items)
                    {
                        if (o is Serial s)
                        {
                            Item it = World.FindItem(s);
                            snap.AgentItems.Add(it?.ToString() ?? $"0x{s.Value:X8}");
                        }
                        else
                        {
                            snap.AgentItems.Add(o?.ToString() ?? string.Empty);
                        }
                    }

                    break;

                case SellAgent sell:
                    foreach (ushort gfx in sell.Items)
                        snap.AgentItems.Add(((ItemID) gfx).ToString());
                    snap.AgentEnabled = sell.Enabled;
                    snap.AgentHasHotBag = sell.HotBag != 0;
                    break;

                case OrganizerAgent org:
                    foreach (ItemID id in org.Items)
                        snap.AgentItems.Add(id.ToString());
                    break;

                case BuyAgent buy:
                    foreach (BuyAgent.BuyEntry entry in buy.Items)
                        snap.AgentItems.Add(entry.ToString());
                    snap.AgentEnabled = buy.Enabled;
                    break;

                case RestockAgent restock:
                    foreach (RestockAgent.RestockItem item in restock.Items)
                        snap.AgentItems.Add(item.ToString());
                    break;

                case ScavengerAgent scav:
                    foreach (ItemID id in scav.Items)
                        snap.AgentItems.Add(id.ToString());
                    snap.AgentEnabled = scav.Enabled;
                    break;

                case SearchExemptionAgent exempt:
                    foreach (object o in exempt.Items)
                        snap.AgentItems.Add(o?.ToString() ?? string.Empty);
                    break;

                case IgnoreAgent ignore:
                    foreach (Serial s in ignore.Chars)
                    {
                        Mobile m = World.FindMobile(s);
                        snap.AgentItems.Add(m?.Name != null && m.Name.Length > 0
                            ? $"{m.Name} (0x{s.Value:X8})"
                            : $"0x{s.Value:X8}");
                    }

                    snap.AgentEnabled = ignore.Enabled;
                    break;
            }
        }

        /// <summary>
        /// Holt das schwebende Add-Target eines Buy/Restock-Agents ab (die UI
        /// fragt danach die Menge ab und ruft AddItem). Konsumierend!
        /// </summary>
        private static void CollectPendingTarget(UiSnapshot snap, int agentIndex)
        {
            if (agentIndex < 0 || agentIndex >= Agent.List.Count)
                return;

            try
            {
                Agent a = Agent.List[agentIndex];
                ushort gfx = 0;
                bool has = false;

                if (a is BuyAgent buy)
                    has = buy.TryGetPendingTarget(out gfx);
                else if (a is RestockAgent restock)
                    has = restock.TryGetPendingTarget(out gfx);

                if (has)
                {
                    snap.PendingTargetAgentIndex = agentIndex;
                    snap.PendingTargetGfx = gfx;
                }
            }
            catch
            {
            }
        }

        private static void BuildCounters(UiSnapshot snap)
        {
            try
            {
                foreach (Counter c in Counter.List)
                {
                    snap.Counters.Add(new CounterInfo
                    {
                        Name = c.Name,
                        Format = c.Format,
                        Enabled = c.Enabled,
                        Amount = c.Enabled ? c.Amount : 0
                    });
                }
            }
            catch
            {
            }
        }

        private static void BuildDressLists(UiSnapshot snap, string dressListName)
        {
            try
            {
                foreach (DressList dl in DressList.List)
                    snap.DressLists.Add(dl.Name);

                if (!string.IsNullOrEmpty(dressListName))
                {
                    DressList sel = DressList.Find(dressListName);
                    if (sel != null)
                    {
                        snap.DressListName = sel.Name;
                        foreach (object o in sel.Items)
                        {
                            if (o is Serial s)
                            {
                                Item it = World.FindItem(s);
                                snap.DressItems.Add(it?.ToString() ?? $"0x{s.Value:X8} (out of range)");
                            }
                            else if (o is ItemID id)
                            {
                                snap.DressItems.Add(id.ToString());
                            }
                            else
                            {
                                snap.DressItems.Add(o?.ToString() ?? string.Empty);
                            }
                        }
                    }
                }
            }
            catch
            {
            }
        }

        private static void BuildHotKeys(UiSnapshot snap)
        {
            try
            {
                snap.HotKeysEnabled = HotKey.Enabled;
                snap.HotKeyStatus = HotKey.StatusText;

                foreach (KeyData kd in HotKey.List)
                {
                    snap.HotKeys.Add(new HotKeyInfo
                    {
                        LocName = kd.LocName,
                        StrName = kd.StrName,
                        DisplayName = kd.DisplayName,
                        Category = kd.Category,
                        SubCat = kd.SubCat,
                        Key = kd.Key,
                        Mod = kd.Mod,
                        SendToUO = kd.SendToUO,
                        Command = kd.Command
                    });
                }
            }
            catch
            {
            }
        }

        /// <summary>NUR auf dem Game-Thread aufrufen. Identitaet wie HotKeyInfo.Identity.</summary>
        public static KeyData FindHotKey(string identity)
        {
            if (string.IsNullOrEmpty(identity) || identity.Length < 3)
                return null;

            if (identity.StartsWith("L:", StringComparison.Ordinal) &&
                int.TryParse(identity.Substring(2), out int loc))
                return HotKey.Get(loc);

            if (identity.StartsWith("S:", StringComparison.Ordinal))
                return HotKey.Get(identity.Substring(2));

            return null;
        }

        /// <summary>NUR auf dem Game-Thread aufrufen.</summary>
        public static Macro FindMacro(string filename)
        {
            if (string.IsNullOrEmpty(filename))
                return null;

            foreach (Macro m in MacroManager.List)
            {
                if (string.Equals(m.Filename, filename, StringComparison.OrdinalIgnoreCase))
                    return m;
            }

            return null;
        }
    }
}
