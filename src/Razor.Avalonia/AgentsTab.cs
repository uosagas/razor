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

// UOSagas-Razor: Agents-Tab im Razor-CE-Layout (Phase 3b).
//
// Blaupause: Razor CE Razor.Designer.cs, agentsTab (519x342):
//  * agentList (ComboBox) @(8,14) 130x23 — alle Agents (Agent.List)
//  * agentB1..agentB6 @(8,41/79/117/155/193/231) je 130x32 — Beschriftung und
//    Aktion je Agent wie Razor CE Agent.OnSelected/OnButtonPress
//  * agentSetHotKey @(8,274) 130x32 (Platzhalter, Phase 3c)
//  * agentGroup (GroupBox, Titel = Agent-Name) @(144,3) 368x329 mit
//    agentSubList @(6,22) 356x301
//
// Buy/Restock "Add (Target)": Der Agent merkt sich das Target (TryGetPending-
// Target); die Snapshot-Pumpe holt es ab, die UI fragt die Menge per InputBox
// und ruft AddItem — wie der Mengen-Dialog in Razor CE.

using System;
using System.Collections.Generic;
using Assistant;
using Assistant.Agents;
using Avalonia.Controls;

namespace Razor.UI
{
    public class AgentsTab : UserControl, ICeTab
    {
        private const string PushDisable = "(Push to Disable)";
        private const string PushEnable = "(Push to Enable)";

        /// <summary>Ein Agent-Button (Beschriftung ggf. vom Snapshot abhaengig).</summary>
        private sealed class BtnSpec
        {
            public Func<UiSnapshot, string> Caption;

            /// <summary>Aktion auf dem GAME-Thread: (Agent, Index des gewaehlten Listeneintrags).</summary>
            public Action<Agent, int> GameAction;

            /// <summary>Alternative UI-Aktion (fuer InputBox-Dialoge). Bekommt (tab, agentIndex, selIdx).</summary>
            public Action<AgentsTab, int, int> UiAction;
        }

        private static readonly Dictionary<string, BtnSpec[]> ButtonSets = BuildButtonSets();

        private readonly ComboBox _agentList;
        private readonly ListBox _subList;
        private readonly Button[] _buttons = new Button[6];
        private readonly TextBlock _groupCaption;

        private bool _applying;
        private UiSnapshot _last;

        /// <summary>Agent-Index, fuer den auf ein Buy/Restock-Add-Target gewartet wird.</summary>
        private int _awaitAmountAgent = -1;

        private MainWindow OwnerWindow => VisualRoot as MainWindow;

        public AgentsTab()
        {
            var root = Ce.Panel();

            _agentList = Ce.Combo(root, 8, 14, 130, 23);
            _agentList.SelectionChanged += (s, e) =>
            {
                if (!_applying)
                {
                    _awaitAmountAgent = -1;
                    UpdateButtons(_last);
                    OwnerWindow?.RequestSnapshot();
                }
            };

            double[] ys = { 41, 79, 117, 155, 193, 231 };
            for (int i = 0; i < 6; i++)
            {
                int btnIndex = i;
                _buttons[i] = Ce.Button(root, "", 8, ys[i], 130, 32, () => OnAgentButton(btnIndex));
                _buttons[i].IsVisible = false;
            }

            Ce.Button(root, "Set Hot Key", 8, 274, 130, 32, null); // Phase 3c

            Canvas grp = Ce.Group(root, "", 144, 3, 368, 329, out _groupCaption);
            _subList = Ce.List(grp, 6, 22, 356, 301);

            Content = root;
        }

        // --- Button-Definitionen je Agent-Typ (wie Razor CE OnSelected) ---------

        private static Dictionary<string, BtnSpec[]> BuildButtonSets()
        {
            BtnSpec Fix(string caption, Action<Agent, int> action) =>
                new BtnSpec { Caption = s => caption, GameAction = action };

            BtnSpec EnableToggle(Action<Agent> toggle) => new BtnSpec
            {
                Caption = s => s?.AgentEnabled == true ? PushDisable : PushEnable,
                GameAction = (a, i) => toggle(a)
            };

            var sets = new Dictionary<string, BtnSpec[]>(StringComparer.Ordinal);

            sets["UseOnceAgent"] = new[]
            {
                Fix("Add (Target)...", (a, i) => ((UseOnceAgent) a).OnAdd()),
                Fix("Add Container (Target)...", (a, i) => ((UseOnceAgent) a).OnAddContainer()),
                Fix("Remove (Target)...", (a, i) => ((UseOnceAgent) a).OnRemove()),
                Fix("Clear List", (a, i) => a.Clear()),
                null,
                null
            };

            sets["SellAgent"] = new[]
            {
                Fix("Add (Target)...", (a, i) => ((SellAgent) a).AddItemTarget()),
                Fix("Remove", (a, i) =>
                {
                    var sell = (SellAgent) a;
                    if (i >= 0 && i < sell.Items.Count)
                        sell.Items.RemoveAt(i);
                }),
                new BtnSpec
                {
                    Caption = s => s?.AgentHasHotBag == true ? "Clear Hot Bag" : "Set Hot Bag",
                    GameAction = (a, i) =>
                    {
                        var sell = (SellAgent) a;
                        if (sell.HotBag != 0)
                            sell.ClearHotBag();
                        else
                            sell.SetHotBag();
                    }
                },
                Fix("Clear", (a, i) => a.Clear()),
                new BtnSpec
                {
                    Caption = s =>
                    {
                        string max = null;
                        s?.TextProps.TryGetValue("SellAgentMax", out max);
                        return $"Max Sell: {max ?? "?"}";
                    },
                    UiAction = (tab, agentIdx, selIdx) => tab.OnSellAmount()
                },
                EnableToggle(a => ((SellAgent) a).Enabled = !((SellAgent) a).Enabled)
            };

            sets["OrganizerAgent"] = new[]
            {
                Fix("Add (Target)", (a, i) => ((OrganizerAgent) a).AddItemTarget()),
                Fix("Set Hot Bag", (a, i) => ((OrganizerAgent) a).SetHotBag()),
                Fix("Organize Now", (a, i) => ((OrganizerAgent) a).Organize()),
                Fix("Remove", (a, i) =>
                {
                    var org = (OrganizerAgent) a;
                    if (i >= 0 && i < org.Items.Count)
                        org.RemoveItem(org.Items[i].Value);
                }),
                Fix("Clear", (a, i) => a.Clear()),
                Fix("Stop Now", (a, i) => ((OrganizerAgent) a).StopNow())
            };

            sets["BuyAgent"] = new[]
            {
                new BtnSpec
                {
                    Caption = s => "Add (Target)...",
                    UiAction = (tab, agentIdx, selIdx) => tab.BeginAddWithAmount(agentIdx,
                        a => ((BuyAgent) a).AddItemTarget())
                },
                new BtnSpec
                {
                    Caption = s => "Edit...",
                    UiAction = (tab, agentIdx, selIdx) => tab.OnBuyEdit(agentIdx, selIdx)
                },
                Fix("Remove", (a, i) =>
                {
                    var buy = (BuyAgent) a;
                    if (i >= 0 && i < buy.Items.Count)
                        buy.RemoveItem(buy.Items[i].Id.Value);
                }),
                Fix("Clear List", (a, i) => a.Clear()),
                EnableToggle(a => ((BuyAgent) a).Enabled = !((BuyAgent) a).Enabled),
                null
            };

            sets["RestockAgent"] = new[]
            {
                new BtnSpec
                {
                    Caption = s => "Add (Target)...",
                    UiAction = (tab, agentIdx, selIdx) => tab.BeginAddWithAmount(agentIdx,
                        a => ((RestockAgent) a).AddItemTarget())
                },
                Fix("Remove", (a, i) =>
                {
                    var restock = (RestockAgent) a;
                    if (i >= 0 && i < restock.Items.Count)
                        restock.RemoveItem(restock.Items[i].ItemID.Value);
                }),
                new BtnSpec
                {
                    Caption = s => "Set Amount",
                    UiAction = (tab, agentIdx, selIdx) => tab.OnRestockAmount(agentIdx, selIdx)
                },
                Fix("Clear List", (a, i) => a.Clear()),
                Fix("Set Hot Bag", (a, i) => ((RestockAgent) a).SetHB()),
                Fix("Restock Now", (a, i) => ((RestockAgent) a).Restock())
            };

            sets["ScavengerAgent"] = new[]
            {
                Fix("Add (Target)...", (a, i) => ((ScavengerAgent) a).OnAddToHotBag()),
                Fix("Remove", (a, i) =>
                {
                    var scav = (ScavengerAgent) a;
                    if (i >= 0 && i < scav.Items.Count)
                        scav.Items.RemoveAt(i);
                }),
                Fix("Set Hot Bag", (a, i) => ((ScavengerAgent) a).OnSetHotBag()),
                Fix("Clear List", (a, i) => a.Clear()),
                Fix("Clear Scavenger Cache", (a, i) => ((ScavengerAgent) a).ClearCache()),
                EnableToggle(a => ((ScavengerAgent) a).ToggleEnabled())
            };

            sets["SearchExemptionAgent"] = new[]
            {
                Fix("Add (Target)...", (a, i) => ((SearchExemptionAgent) a).AddItemTarget()),
                Fix("Add Type (Target)...", (a, i) => ((SearchExemptionAgent) a).AddTypeTarget()),
                Fix("Remove", (a, i) =>
                {
                    var ex = (SearchExemptionAgent) a;
                    if (i >= 0 && i < ex.Items.Count)
                        ex.Items.RemoveAt(i);
                }),
                Fix("Remove (Target)...", (a, i) => ((SearchExemptionAgent) a).RemoveItemTarget()),
                Fix("Clear List", (a, i) => a.Clear()),
                null
            };

            sets["IgnoreAgent"] = new[]
            {
                Fix("Add (Target)...", (a, i) => ((IgnoreAgent) a).AddToIgnoreList()),
                Fix("Remove", (a, i) =>
                {
                    var ig = (IgnoreAgent) a;
                    if (i >= 0 && i < ig.Chars.Count)
                        ig.Chars.RemoveAt(i);
                }),
                Fix("Remove (Target)...", (a, i) => ((IgnoreAgent) a).RemoveFromIgnoreList()),
                Fix("Clear List", (a, i) => a.Clear()),
                EnableToggle(a => ((IgnoreAgent) a).Enabled = !((IgnoreAgent) a).Enabled),
                null
            };

            return sets;
        }

        // --- Button-Klicks ------------------------------------------------------

        private void OnAgentButton(int btnIndex)
        {
            int agentIdx = _agentList.SelectedIndex;
            string kind = _last?.AgentKind;
            if (agentIdx < 0 || kind == null || !ButtonSets.TryGetValue(kind, out BtnSpec[] specs))
                return;

            BtnSpec spec = specs[btnIndex];
            if (spec == null)
                return;

            int selIdx = _subList.SelectedIndex;

            if (spec.UiAction != null)
            {
                spec.UiAction(this, agentIdx, selIdx);
            }
            else if (spec.GameAction != null)
            {
                GameThread.Post(() =>
                {
                    if (agentIdx >= Agent.List.Count)
                        return;

                    spec.GameAction(Agent.List[agentIdx], selIdx);
                });
            }

            OwnerWindow?.RequestSnapshot();
        }

        /// <summary>Buy/Restock: Target ausloesen und auf Grafik+Menge warten.</summary>
        private void BeginAddWithAmount(int agentIdx, Action<Agent> beginTarget)
        {
            _awaitAmountAgent = agentIdx;
            GameThread.Post(() =>
            {
                if (agentIdx < Agent.List.Count)
                    beginTarget(Agent.List[agentIdx]);
            });
        }

        private async void OnSellAmount()
        {
            var owner = OwnerWindow;
            if (owner == null)
                return;

            string input = await InputBox.Show(owner, "Enter the maximum amount to sell:", "Max Sell Amount");
            if (input != null && int.TryParse(input, out int max) && max > 0)
                GameThread.Post(() => PropBinder.SetScalar("SellAgentMax", max));

            owner.RequestSnapshot();
        }

        private async void OnBuyEdit(int agentIdx, int selIdx)
        {
            var owner = OwnerWindow;
            if (owner == null || selIdx < 0)
                return;

            string input = await InputBox.Show(owner, "Enter Amount:", "Edit Buy Amount");
            if (input != null && ushort.TryParse(input, out ushort amount))
            {
                GameThread.Post(() =>
                {
                    if (agentIdx < Agent.List.Count && Agent.List[agentIdx] is BuyAgent buy &&
                        selIdx < buy.Items.Count)
                        buy.SetItemAmount(buy.Items[selIdx].Id.Value, amount);
                });
            }

            owner.RequestSnapshot();
        }

        private async void OnRestockAmount(int agentIdx, int selIdx)
        {
            var owner = OwnerWindow;
            if (owner == null || selIdx < 0)
                return;

            string input = await InputBox.Show(owner, "Enter Amount:", "Restock Amount");
            if (input != null && int.TryParse(input, out int amount) && amount > 0)
            {
                GameThread.Post(() =>
                {
                    if (agentIdx < Agent.List.Count && Agent.List[agentIdx] is RestockAgent restock &&
                        selIdx < restock.Items.Count)
                        restock.SetItemAmount(restock.Items[selIdx].ItemID.Value, amount);
                });
            }

            owner.RequestSnapshot();
        }

        /// <summary>Abgeholtes Buy/Restock-Target: Menge abfragen und AddItem rufen.</summary>
        private async void CompletePendingAdd(int agentIdx, int gfx)
        {
            var owner = OwnerWindow;
            if (owner == null)
                return;

            string input = await InputBox.Show(owner, "Enter Amount:", "Add Item");
            if (input != null && int.TryParse(input, out int amount) && amount > 0)
            {
                GameThread.Post(() =>
                {
                    if (agentIdx >= Agent.List.Count)
                        return;

                    Agent a = Agent.List[agentIdx];
                    if (a is BuyAgent buy)
                        buy.AddItem(new BuyAgent.BuyEntry((ushort) gfx, (ushort) amount));
                    else if (a is RestockAgent restock)
                        restock.AddItem(new RestockAgent.RestockItem((ushort) gfx, amount));
                });
            }

            owner.RequestSnapshot();
        }

        // --- Snapshot -----------------------------------------------------------

        public void Contribute(UiRequest req)
        {
            req.AgentIndex = _agentList.SelectedIndex;
            req.PendingTargetAgentIndex = _awaitAmountAgent;
            req.TextProps.Add("SellAgentMax");
        }

        public void Apply(UiSnapshot snap)
        {
            _applying = true;
            try
            {
                _last = snap;

                // Agent-Liste nur bei Aenderung neu aufbauen.
                bool changed = _agentList.Items.Count != snap.Agents.Count;
                if (!changed)
                {
                    for (int i = 0; i < snap.Agents.Count; i++)
                    {
                        if (!string.Equals(_agentList.Items[i] as string, snap.Agents[i].Display,
                                StringComparison.Ordinal))
                        {
                            changed = true;
                            break;
                        }
                    }
                }

                if (changed)
                {
                    int sel = _agentList.SelectedIndex;
                    _agentList.Items.Clear();
                    foreach (AgentInfo a in snap.Agents)
                        _agentList.Items.Add(a.Display);
                    if (sel >= 0 && sel < _agentList.Items.Count)
                        _agentList.SelectedIndex = sel;
                }

                // Sub-Liste des gewaehlten Agents.
                if (snap.AgentIndex == _agentList.SelectedIndex && snap.AgentIndex >= 0)
                {
                    _groupCaption.Text = snap.AgentIndex < snap.Agents.Count
                        ? snap.Agents[snap.AgentIndex].Display
                        : string.Empty;

                    bool itemsChanged = _subList.Items.Count != snap.AgentItems.Count;
                    if (!itemsChanged)
                    {
                        for (int i = 0; i < snap.AgentItems.Count; i++)
                        {
                            if (!string.Equals(_subList.Items[i] as string, snap.AgentItems[i],
                                    StringComparison.Ordinal))
                            {
                                itemsChanged = true;
                                break;
                            }
                        }
                    }

                    if (itemsChanged)
                    {
                        int sel = _subList.SelectedIndex;
                        _subList.Items.Clear();
                        foreach (string s in snap.AgentItems)
                            _subList.Items.Add(s);
                        if (sel >= 0 && sel < _subList.Items.Count)
                            _subList.SelectedIndex = sel;
                    }
                }
                else if (_agentList.SelectedIndex < 0)
                {
                    _groupCaption.Text = string.Empty;
                    _subList.Items.Clear();
                }

                UpdateButtons(snap);

                // Buy/Restock: abgeholtes Target -> Mengen-Dialog.
                if (snap.PendingTargetGfx >= 0 && snap.PendingTargetAgentIndex == _awaitAmountAgent &&
                    _awaitAmountAgent >= 0)
                {
                    int agentIdx = _awaitAmountAgent;
                    int gfx = snap.PendingTargetGfx;
                    _awaitAmountAgent = -1;
                    CompletePendingAdd(agentIdx, gfx);
                }
            }
            finally
            {
                _applying = false;
            }
        }

        /// <summary>Beschriftung/Sichtbarkeit der 6 Buttons wie Razor CE OnSelected.</summary>
        private void UpdateButtons(UiSnapshot snap)
        {
            string kind = snap?.AgentIndex == _agentList.SelectedIndex ? snap?.AgentKind : null;

            if (kind == null || !ButtonSets.TryGetValue(kind, out BtnSpec[] specs))
            {
                foreach (Button b in _buttons)
                    b.IsVisible = false;
                return;
            }

            for (int i = 0; i < 6; i++)
            {
                BtnSpec spec = specs[i];
                if (spec == null)
                {
                    _buttons[i].IsVisible = false;
                    continue;
                }

                string caption = spec.Caption(snap);
                if (!Equals(_buttons[i].Content, caption))
                    _buttons[i].Content = caption;
                _buttons[i].IsVisible = true;
            }
        }
    }
}
