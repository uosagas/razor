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

// UOSagas-Razor: Lua-Script-UI — eigenes, benutzerfreundliches Design.
//
// Kernideen (bewusst KEINE ImGui-Notation des Clients mehr):
// - Auto-Layout: Elemente stapeln vertikal, win:Row() legt sie nebeneinander —
//   keine x/y-Pixel noetig.
// - Funktions-Binding direkt beim Erzeugen: win:Button('Heal', fn),
//   win:Checkbox('Auto', true, fn) — oder nachtraeglich per :OnClick/:OnChange.
// - Variablen-Binding: win:Label(function() return 'HP: ' .. Player.Hits end)
//   und win:ProgressBar(function() return Player.Hits / Player.HitsMax end)
//   aktualisieren sich selbst (Standard alle 200 ms, :Bind(fn, ms) stellt um).
// - Callbacks/Bindings pumpen AUTOMATISCH waehrend Pause() (die Engine ruft
//   PumpAsync in den Wartescheiben) — kein manuelles DispatchCallbacks.
//   win:Run() ist die Komfort-Schleife "warte bis das Fenster zu ist".
//
// Beispiel:
//   local win = UI.Window('Helfer')
//   win:Label(function() return 'HP: ' .. Player.Hits end)
//   local status = win:Label('Bereit')
//   win:Button('Heilen', function()
//       status:SetText('Wirke Heilung...')
//       Spells.Cast('Greater Heal')
//   end)
//   local auto = win:Checkbox('Auto-Heilung', false)
//   win:Run()  -- pumpt Callbacks, endet wenn das Fenster geschlossen wird
//
// Threading: Modell (Lua/UI/ScriptUiModel.cs) ist thread-sicher; der
// Avalonia-Host meldet Klicks/Eingaben in die UIEventQueue, PumpAsync fuehrt
// die Lua-Callbacks auf dem Script-Task aus (DoStringAsync ueber die
// _UIControlTables-Registry — derselbe bewachte Reentry-Pfad, den auch der
// Client fuer Callbacks nutzt).

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Assistant.LuaEngine.UI;
using Assistant.VScripts.Core;
using Lua;

namespace Assistant.LuaEngine.API;

public static class LuaUIAPI
{
    private const int DefaultBindIntervalMs = 200;

    private enum BindTarget
    {
        LabelText,
        ProgressValue
    }

    private sealed class Binding
    {
        public string ControlId;
        public BindTarget Target;
        public UiElement Element;
        public int IntervalMs = DefaultBindIntervalMs;
        public DateTime LastRun = DateTime.MinValue;
    }

    private const int MaxPumpDepth = 3;

    private static LuaState _luaState;
    private static readonly List<Binding> _bindings = new();
    private static readonly object _bindingsLock = new();

    // Pump-Zustand (laeuft komplett auf dem Script-Task, keine Locks noetig):
    // _pumpDepth erlaubt VERSCHACHTELTES Pumpen — waehrend ein langer Callback
    // in Pause() wartet, werden kurze Callbacks (z. B. ein Stop-Button) direkt
    // zugestellt. _runningControls verwirft Events fuer Controls, deren
    // Callback gerade laeuft (Debounce: Doppelklick startet den Job nicht
    // nochmal verschachtelt).
    private static int _pumpDepth;
    private static readonly HashSet<string> _runningControls = new();

    public static void Register(LuaState state)
    {
        _luaState = state;

        // Registry fuer Callback-/Binding-Dispatch (Zugriff aus DoStringAsync).
        state.Environment["_UIControlTables"] = new LuaTable();

        var ui = new LuaTable();
        ui["Window"] = new LuaFunction("Window", CreateWindow);
        ui["Pump"] = new LuaFunction("Pump", async (ctx, ct) =>
        {
            await PumpAsync(ct);
            ctx.Return(true);
            return 1;
        });
        ui["DestroyAll"] = new LuaFunction("DestroyAll", (ctx, ct) =>
        {
            ScriptUIManager.DestroyAllWindows();
            ctx.Return(true);
            return new ValueTask<int>(1);
        });

        state.Environment["UI"] = ui;
    }

    /// <summary>Script-Ende: Events/Bindings/Registry abraeumen (Engine ruft das).</summary>
    public static void ClearAllCallbacks()
    {
        UIEventQueue.Clear();
        _runningControls.Clear();
        _pumpDepth = 0;

        lock (_bindingsLock)
        {
            _bindings.Clear();
        }

        try
        {
            if (_luaState != null)
                _luaState.Environment["_UIControlTables"] = new LuaTable();
        }
        catch
        {
        }
    }

    // ---- Pump: Events -> Lua-Callbacks, Bindings -> Modell ---------------

    /// <summary>
    /// Laeuft auf dem Script-Task (Pause-Scheiben, UI.Pump, win:Run).
    /// Verschachtelt bis MaxPumpDepth: ein langer Callback, der Pause()
    /// aufruft, laesst darin weitere (kurze) Callbacks zu — so wirkt ein
    /// Stop-Button MITTEN in einem laufenden Job, ohne dass das Script eine
    /// eigene Job-Queue bauen muss. Doppel-Events desselben Controls werden
    /// verworfen, solange sein Callback noch laeuft.
    /// </summary>
    public static async ValueTask PumpAsync(CancellationToken ct)
    {
        if (_luaState == null || _pumpDepth >= MaxPumpDepth)
            return;

        _pumpDepth++;
        try
        {
            // 1) Interaktions-Events -> Callbacks
            while (UIEventQueue.TryDequeue(out UIEvent evt))
            {
                // Debounce: Callback dieses Controls laeuft noch (wir stecken
                // gerade in seiner Pause) -> Event verwerfen.
                if (_runningControls.Contains(evt.ControlId))
                    continue;

                string code = evt.EventType switch
                {
                    UIEventType.Click =>
                        $"local t = _UIControlTables[\"{evt.ControlId}\"] if t and t._onClick then t._onClick() end",
                    UIEventType.Change =>
                        $"local t = _UIControlTables[\"{evt.ControlId}\"] if t and t._onChange then t._onChange({FormatLuaValue(evt.Value)}) end",
                    UIEventType.TextChange =>
                        $"local t = _UIControlTables[\"{evt.ControlId}\"] if t and t._onTextChange then t._onTextChange(\"{EscapeLuaString(evt.Value as string ?? "")}\") end",
                    UIEventType.WindowClose =>
                        $"local t = _UIControlTables[\"{evt.ControlId}\"] if t and t._onClose then t._onClose() end",
                    _ => null
                };

                if (code == null)
                    continue;

                _runningControls.Add(evt.ControlId);
                try
                {
                    await _luaState.DoStringAsync(code, "ui_callback", ct);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception e)
                {
                    Message.Warning($"UI callback error ({evt.ControlId}): {e.Message}");
                }
                finally
                {
                    _runningControls.Remove(evt.ControlId);
                }
            }

            // 2) Faellige Variablen-Bindings — nur auf der aeussersten Ebene
            // (verschachtelte Pumps sollen nur kurze Callbacks zustellen).
            if (_pumpDepth > 1)
                return;

            Binding[] due;
            var now = DateTime.UtcNow;
            lock (_bindingsLock)
            {
                due = _bindings.FindAll(b => (now - b.LastRun).TotalMilliseconds >= b.IntervalMs).ToArray();
            }

            foreach (Binding binding in due)
            {
                binding.LastRun = now;

                try
                {
                    LuaValue[] results = await _luaState.DoStringAsync(
                        $"local t = _UIControlTables[\"{binding.ControlId}\"] if t and t._bind then return t._bind() end",
                        "ui_binding", ct);

                    if (results == null || results.Length == 0)
                        continue;

                    ApplyBinding(binding, results[0]);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception e)
                {
                    Message.Warning($"UI binding error ({binding.ControlId}): {e.Message}");
                }
            }
        }
        finally
        {
            _pumpDepth--;
        }
    }

    private static void ApplyBinding(Binding binding, LuaValue value)
    {
        switch (binding.Target)
        {
            case BindTarget.LabelText when binding.Element is UiLabel label:
                label.Text = LuaValueToText(value);
                break;

            case BindTarget.ProgressValue when binding.Element is UiProgressBar bar:
                if (value.TryRead(out double d))
                    bar.Value = d;
                break;
        }
    }

    private static string LuaValueToText(LuaValue value)
    {
        if (value.TryRead(out string s))
            return s;
        if (value.TryRead(out double d))
            return d.ToString("0.##", CultureInfo.InvariantCulture);
        if (value.TryRead(out bool b))
            return b ? "true" : "false";
        if (value.Type == LuaValueType.Nil)
            return "";

        return value.ToString();
    }

    private static string FormatLuaValue(object value)
    {
        return value switch
        {
            bool b => b ? "true" : "false",
            double d => d.ToString(CultureInfo.InvariantCulture),
            float f => f.ToString(CultureInfo.InvariantCulture),
            int i => i.ToString(CultureInfo.InvariantCulture),
            string s => $"\"{EscapeLuaString(s)}\"",
            _ => "nil"
        };
    }

    private static string EscapeLuaString(string input)
    {
        if (string.IsNullOrEmpty(input))
            return "";

        return input
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"")
            .Replace("\n", "\\n")
            .Replace("\r", "\\r")
            .Replace("\t", "\\t");
    }

    // ---- Registry-Helfer -------------------------------------------------

    private static void RegisterTable(string id, LuaTable table)
    {
        var regValue = _luaState?.Environment["_UIControlTables"];
        if (regValue.HasValue && regValue.Value.TryRead(out LuaTable registry))
            registry[id] = table;
    }

    private static void AddBinding(UiElement element, BindTarget target, int intervalMs)
    {
        lock (_bindingsLock)
        {
            _bindings.RemoveAll(b => b.ControlId == element.Id);
            _bindings.Add(new Binding
            {
                ControlId = element.Id,
                Target = target,
                Element = element,
                IntervalMs = Math.Max(50, intervalMs)
            });
        }
    }

    private static string ArgAsString(LuaFunctionExecutionContext ctx, int index)
    {
        if (ctx.ArgumentCount <= index)
            return "";

        try { return ctx.GetArgument<string>(index); }
        catch { }
        try { return ctx.GetArgument<double>(index).ToString("0.##", CultureInfo.InvariantCulture); }
        catch { }
        try { return ctx.GetArgument<bool>(index) ? "true" : "false"; }
        catch { }

        return "";
    }

    private static bool TryArg<T>(LuaFunctionExecutionContext ctx, int index, out T value)
    {
        value = default;
        if (ctx.ArgumentCount <= index)
            return false;

        try
        {
            value = ctx.GetArgument<T>(index);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>Farbe als "#RRGGBB" ODER (r, g, b) mit 0-255 ab Argumentindex.</summary>
    private static string ParseColorArgs(LuaFunctionExecutionContext ctx, int startIndex)
    {
        if (TryArg(ctx, startIndex, out string hex) && !string.IsNullOrEmpty(hex))
            return hex.StartsWith("#") ? hex : "#" + hex;

        if (TryArg(ctx, startIndex, out double r) &&
            TryArg(ctx, startIndex + 1, out double g) &&
            TryArg(ctx, startIndex + 2, out double b))
        {
            return $"#{(byte) Math.Clamp(r, 0, 255):X2}{(byte) Math.Clamp(g, 0, 255):X2}{(byte) Math.Clamp(b, 0, 255):X2}";
        }

        return null;
    }

    // ---- UI.Window -------------------------------------------------------

    private static ValueTask<int> CreateWindow(LuaFunctionExecutionContext context, CancellationToken ct)
    {
        try
        {
            string title = "Script";
            float width = 0, height = 0, x = -1, y = -1;

            // UI.Window('Titel' [, x, y [, breite, hoehe]])
            // oder UI.Window{ title=, x=, y=, width=, height= }
            if (TryArg(context, 0, out LuaTable opts))
            {
                if (opts["title"].TryRead(out string t)) title = t;
                if (opts["width"].TryRead(out double w)) width = (float) w;
                if (opts["height"].TryRead(out double h)) height = (float) h;
                if (opts["x"].TryRead(out double ox)) x = (float) ox;
                if (opts["y"].TryRead(out double oy)) y = (float) oy;
            }
            else if (TryArg(context, 0, out string t))
            {
                title = t;

                if (TryArg(context, 1, out double px) && TryArg(context, 2, out double py))
                {
                    x = (float) px;
                    y = (float) py;
                }

                if (TryArg(context, 3, out double pw) && TryArg(context, 4, out double ph))
                {
                    width = (float) pw;
                    height = (float) ph;
                }
            }

            UiWindow window = ScriptUIManager.CreateWindow(title);
            if (width > 0 || height > 0)
                window.SetSize(width, height);
            if (x >= 0 && y >= 0)
                window.SetPosition(x, y);

            context.Return(CreateWindowTable(window));
        }
        catch (Exception e)
        {
            Message.Warning($"Error in UI.Window: {e.Message}");
            context.Return(LuaValue.Nil);
        }

        return new ValueTask<int>(1);
    }

    // ---- Fenster-Tabelle -------------------------------------------------

    private static LuaTable CreateWindowTable(UiWindow window)
    {
        var table = new LuaTable();
        table["Id"] = window.Id;

        // Elemente direkt am Fenster (vertikales Auto-Layout) ...
        AddElementFactories(table, window, element => window.AddElement(element));

        // ... oder in einer Row (horizontal).
        table["Row"] = new LuaFunction("Row", (ctx, ct) =>
        {
            var row = new UiRow();
            window.AddElement(row);

            var rowTable = new LuaTable();
            rowTable["Id"] = row.Id;
            AddElementFactories(rowTable, window, element =>
            {
                row.Add(element);
                window.BumpVersion();
            });

            ctx.Return(rowTable);
            return new ValueTask<int>(1);
        });

        table["Show"] = new LuaFunction("Show", (ctx, ct) =>
        {
            window.Show();
            ctx.Return(table);
            return new ValueTask<int>(1);
        });

        table["Hide"] = new LuaFunction("Hide", (ctx, ct) =>
        {
            window.Hide();
            ctx.Return(table);
            return new ValueTask<int>(1);
        });

        table["Close"] = new LuaFunction("Close", (ctx, ct) =>
        {
            window.Close();
            ctx.Return(true);
            return new ValueTask<int>(1);
        });

        table["IsOpen"] = new LuaFunction("IsOpen", (ctx, ct) =>
        {
            ctx.Return(window.IsOpen);
            return new ValueTask<int>(1);
        });

        table["SetTitle"] = new LuaFunction("SetTitle", (ctx, ct) =>
        {
            window.Title = ArgAsString(ctx, 1);
            ctx.Return(table);
            return new ValueTask<int>(1);
        });

        table["SetPosition"] = new LuaFunction("SetPosition", (ctx, ct) =>
        {
            if (TryArg(ctx, 1, out double x) && TryArg(ctx, 2, out double y))
                window.SetPosition((float) x, (float) y);
            ctx.Return(table);
            return new ValueTask<int>(1);
        });

        table["SetSize"] = new LuaFunction("SetSize", (ctx, ct) =>
        {
            if (TryArg(ctx, 1, out double w) && TryArg(ctx, 2, out double h))
                window.SetSize((float) w, (float) h);
            ctx.Return(table);
            return new ValueTask<int>(1);
        });

        table["OnClose"] = new LuaFunction("OnClose", (ctx, ct) =>
        {
            if (TryArg(ctx, 1, out LuaFunction fn))
            {
                table["_onClose"] = fn;
                RegisterTable(window.Id, table);
            }

            ctx.Return(table);
            return new ValueTask<int>(1);
        });

        // Komfort-Schleife: pumpt Callbacks/Bindings, bis das Fenster zu ist.
        table["Run"] = new LuaFunction("Run", async (ctx, ct) =>
        {
            int interval = TryArg(ctx, 1, out double ms) ? Math.Max(20, (int) ms) : 50;

            while (window.IsOpen)
            {
                ct.ThrowIfCancellationRequested();
                await PumpAsync(ct);
                await Task.Delay(interval, ct);
            }

            ctx.Return(true);
            return 1;
        });

        RegisterTable(window.Id, table);
        return table;
    }

    // ---- Element-Fabriken (Fenster UND Row) ------------------------------

    private static void AddElementFactories(LuaTable owner, UiWindow window, Action<UiElement> add)
    {
        // win:Label('Text')  |  win:Label(fn [, intervalMs])  -> Label-Tabelle
        owner["Label"] = new LuaFunction("Label", (ctx, ct) =>
        {
            var label = new UiLabel();
            LuaTable table = CreateLabelTable(label);

            if (TryArg(ctx, 1, out LuaFunction bindFn))
            {
                table["_bind"] = bindFn;
                RegisterTable(label.Id, table);
                int interval = TryArg(ctx, 2, out double ms) ? (int) ms : DefaultBindIntervalMs;
                AddBinding(label, BindTarget.LabelText, interval);
            }
            else
            {
                label.Text = ArgAsString(ctx, 1);
            }

            add(label);
            ctx.Return(table);
            return new ValueTask<int>(1);
        });

        // win:Button('Text' [, onClick])
        owner["Button"] = new LuaFunction("Button", (ctx, ct) =>
        {
            var button = new UiButton { Text = ArgAsString(ctx, 1) };
            LuaTable table = CreateButtonTable(button);

            if (TryArg(ctx, 2, out LuaFunction fn))
            {
                table["_onClick"] = fn;
                RegisterTable(button.Id, table);
            }

            add(button);
            ctx.Return(table);
            return new ValueTask<int>(1);
        });

        // win:Checkbox('Text' [, checked] [, onChange])
        owner["Checkbox"] = new LuaFunction("Checkbox", (ctx, ct) =>
        {
            var checkbox = new UiCheckbox { Text = ArgAsString(ctx, 1) };
            if (TryArg(ctx, 2, out bool initial))
                checkbox.Checked = initial;

            LuaTable table = CreateCheckboxTable(checkbox);

            // onChange darf an Position 2 ODER 3 stehen.
            if (TryArg(ctx, 2, out LuaFunction fn2))
            {
                table["_onChange"] = fn2;
                RegisterTable(checkbox.Id, table);
            }
            else if (TryArg(ctx, 3, out LuaFunction fn3))
            {
                table["_onChange"] = fn3;
                RegisterTable(checkbox.Id, table);
            }

            add(checkbox);
            ctx.Return(table);
            return new ValueTask<int>(1);
        });

        // win:TextBox([text] [, onChange])
        owner["TextBox"] = new LuaFunction("TextBox", (ctx, ct) =>
        {
            var textBox = new UiTextBox();
            if (!TryArg(ctx, 1, out LuaFunction _))
                textBox.Text = ArgAsString(ctx, 1);

            LuaTable table = CreateTextBoxTable(textBox);

            if (TryArg(ctx, 1, out LuaFunction fn1))
            {
                table["_onTextChange"] = fn1;
                RegisterTable(textBox.Id, table);
            }
            else if (TryArg(ctx, 2, out LuaFunction fn2))
            {
                table["_onTextChange"] = fn2;
                RegisterTable(textBox.Id, table);
            }

            add(textBox);
            ctx.Return(table);
            return new ValueTask<int>(1);
        });

        // win:Slider(min, max [, initial] [, onChange])
        owner["Slider"] = new LuaFunction("Slider", (ctx, ct) =>
        {
            var slider = new UiSlider();
            if (TryArg(ctx, 1, out double min)) slider.Min = min;
            if (TryArg(ctx, 2, out double max)) slider.Max = max;
            if (TryArg(ctx, 3, out double initial)) slider.Value = initial;

            LuaTable table = CreateSliderTable(slider);

            if (TryArg(ctx, 3, out LuaFunction fn3))
            {
                table["_onChange"] = fn3;
                RegisterTable(slider.Id, table);
            }
            else if (TryArg(ctx, 4, out LuaFunction fn4))
            {
                table["_onChange"] = fn4;
                RegisterTable(slider.Id, table);
            }

            add(slider);
            ctx.Return(table);
            return new ValueTask<int>(1);
        });

        // win:ProgressBar([valueOrFn] [, intervalMs])  — Wert 0..1
        owner["ProgressBar"] = new LuaFunction("ProgressBar", (ctx, ct) =>
        {
            var bar = new UiProgressBar();
            LuaTable table = CreateProgressBarTable(bar);

            if (TryArg(ctx, 1, out LuaFunction bindFn))
            {
                table["_bind"] = bindFn;
                RegisterTable(bar.Id, table);
                int interval = TryArg(ctx, 2, out double ms) ? (int) ms : DefaultBindIntervalMs;
                AddBinding(bar, BindTarget.ProgressValue, interval);
            }
            else if (TryArg(ctx, 1, out double v))
            {
                bar.Value = v;
            }

            add(bar);
            ctx.Return(table);
            return new ValueTask<int>(1);
        });

        owner["Separator"] = new LuaFunction("Separator", (ctx, ct) =>
        {
            add(new UiSeparator());
            ctx.Return(true);
            return new ValueTask<int>(1);
        });
    }

    // ---- Control-Tabellen ------------------------------------------------

    private static void AddCommonMethods(LuaTable table, UiElement element)
    {
        table["Id"] = element.Id;

        table["SetVisible"] = new LuaFunction("SetVisible", (ctx, ct) =>
        {
            if (TryArg(ctx, 1, out bool visible))
                element.Visible = visible;
            ctx.Return(table);
            return new ValueTask<int>(1);
        });

        table["SetEnabled"] = new LuaFunction("SetEnabled", (ctx, ct) =>
        {
            if (TryArg(ctx, 1, out bool enabled))
                element.Enabled = enabled;
            ctx.Return(table);
            return new ValueTask<int>(1);
        });

        table["SetWidth"] = new LuaFunction("SetWidth", (ctx, ct) =>
        {
            if (TryArg(ctx, 1, out double width))
                element.Width = (float) width;
            ctx.Return(table);
            return new ValueTask<int>(1);
        });
    }

    private static LuaTable CreateLabelTable(UiLabel label)
    {
        var table = new LuaTable();
        AddCommonMethods(table, label);

        table["SetText"] = new LuaFunction("SetText", (ctx, ct) =>
        {
            label.Text = ArgAsString(ctx, 1);
            ctx.Return(table);
            return new ValueTask<int>(1);
        });

        table["GetText"] = new LuaFunction("GetText", (ctx, ct) =>
        {
            ctx.Return(label.Text ?? "");
            return new ValueTask<int>(1);
        });

        table["SetColor"] = new LuaFunction("SetColor", (ctx, ct) =>
        {
            label.ColorHex = ParseColorArgs(ctx, 1);
            ctx.Return(table);
            return new ValueTask<int>(1);
        });

        // label:Bind(fn [, intervalMs]) — nachtraegliches Variablen-Binding.
        table["Bind"] = new LuaFunction("Bind", (ctx, ct) =>
        {
            if (TryArg(ctx, 1, out LuaFunction fn))
            {
                table["_bind"] = fn;
                RegisterTable(label.Id, table);
                int interval = TryArg(ctx, 2, out double ms) ? (int) ms : DefaultBindIntervalMs;
                AddBinding(label, BindTarget.LabelText, interval);
            }

            ctx.Return(table);
            return new ValueTask<int>(1);
        });

        return table;
    }

    private static LuaTable CreateButtonTable(UiButton button)
    {
        var table = new LuaTable();
        AddCommonMethods(table, button);

        table["SetText"] = new LuaFunction("SetText", (ctx, ct) =>
        {
            button.Text = ArgAsString(ctx, 1);
            ctx.Return(table);
            return new ValueTask<int>(1);
        });

        table["OnClick"] = new LuaFunction("OnClick", (ctx, ct) =>
        {
            if (TryArg(ctx, 1, out LuaFunction fn))
            {
                table["_onClick"] = fn;
                RegisterTable(button.Id, table);
            }

            ctx.Return(table);
            return new ValueTask<int>(1);
        });

        return table;
    }

    private static LuaTable CreateCheckboxTable(UiCheckbox checkbox)
    {
        var table = new LuaTable();
        AddCommonMethods(table, checkbox);

        table["IsChecked"] = new LuaFunction("IsChecked", (ctx, ct) =>
        {
            ctx.Return(checkbox.Checked);
            return new ValueTask<int>(1);
        });

        table["SetChecked"] = new LuaFunction("SetChecked", (ctx, ct) =>
        {
            if (TryArg(ctx, 1, out bool value))
                checkbox.Checked = value;
            ctx.Return(table);
            return new ValueTask<int>(1);
        });

        table["SetText"] = new LuaFunction("SetText", (ctx, ct) =>
        {
            checkbox.Text = ArgAsString(ctx, 1);
            ctx.Return(table);
            return new ValueTask<int>(1);
        });

        table["OnChange"] = new LuaFunction("OnChange", (ctx, ct) =>
        {
            if (TryArg(ctx, 1, out LuaFunction fn))
            {
                table["_onChange"] = fn;
                RegisterTable(checkbox.Id, table);
            }

            ctx.Return(table);
            return new ValueTask<int>(1);
        });

        return table;
    }

    private static LuaTable CreateTextBoxTable(UiTextBox textBox)
    {
        var table = new LuaTable();
        AddCommonMethods(table, textBox);

        table["GetText"] = new LuaFunction("GetText", (ctx, ct) =>
        {
            ctx.Return(textBox.Text ?? "");
            return new ValueTask<int>(1);
        });

        table["SetText"] = new LuaFunction("SetText", (ctx, ct) =>
        {
            textBox.Text = ArgAsString(ctx, 1);
            ctx.Return(table);
            return new ValueTask<int>(1);
        });

        table["SetPlaceholder"] = new LuaFunction("SetPlaceholder", (ctx, ct) =>
        {
            textBox.Placeholder = ArgAsString(ctx, 1);
            ctx.Return(table);
            return new ValueTask<int>(1);
        });

        table["SetPassword"] = new LuaFunction("SetPassword", (ctx, ct) =>
        {
            textBox.IsPassword = !TryArg(ctx, 1, out bool value) || value;
            ctx.Return(table);
            return new ValueTask<int>(1);
        });

        table["OnChange"] = new LuaFunction("OnChange", (ctx, ct) =>
        {
            if (TryArg(ctx, 1, out LuaFunction fn))
            {
                table["_onTextChange"] = fn;
                RegisterTable(textBox.Id, table);
            }

            ctx.Return(table);
            return new ValueTask<int>(1);
        });

        return table;
    }

    private static LuaTable CreateSliderTable(UiSlider slider)
    {
        var table = new LuaTable();
        AddCommonMethods(table, slider);

        table["GetValue"] = new LuaFunction("GetValue", (ctx, ct) =>
        {
            ctx.Return(slider.Value);
            return new ValueTask<int>(1);
        });

        table["SetValue"] = new LuaFunction("SetValue", (ctx, ct) =>
        {
            if (TryArg(ctx, 1, out double value))
                slider.Value = Math.Clamp(value, slider.Min, slider.Max);
            ctx.Return(table);
            return new ValueTask<int>(1);
        });

        table["OnChange"] = new LuaFunction("OnChange", (ctx, ct) =>
        {
            if (TryArg(ctx, 1, out LuaFunction fn))
            {
                table["_onChange"] = fn;
                RegisterTable(slider.Id, table);
            }

            ctx.Return(table);
            return new ValueTask<int>(1);
        });

        return table;
    }

    private static LuaTable CreateProgressBarTable(UiProgressBar bar)
    {
        var table = new LuaTable();
        AddCommonMethods(table, bar);

        table["SetValue"] = new LuaFunction("SetValue", (ctx, ct) =>
        {
            if (TryArg(ctx, 1, out double value))
                bar.Value = value;
            ctx.Return(table);
            return new ValueTask<int>(1);
        });

        table["GetValue"] = new LuaFunction("GetValue", (ctx, ct) =>
        {
            ctx.Return(bar.Value);
            return new ValueTask<int>(1);
        });

        table["SetColor"] = new LuaFunction("SetColor", (ctx, ct) =>
        {
            bar.ColorHex = ParseColorArgs(ctx, 1);
            ctx.Return(table);
            return new ValueTask<int>(1);
        });

        table["Bind"] = new LuaFunction("Bind", (ctx, ct) =>
        {
            if (TryArg(ctx, 1, out LuaFunction fn))
            {
                table["_bind"] = fn;
                RegisterTable(bar.Id, table);
                int interval = TryArg(ctx, 2, out double ms) ? (int) ms : DefaultBindIntervalMs;
                AddBinding(bar, BindTarget.ProgressValue, interval);
            }

            ctx.Return(table);
            return new ValueTask<int>(1);
        });

        return table;
    }
}
