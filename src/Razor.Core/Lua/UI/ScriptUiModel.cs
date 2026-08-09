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

// UOSagas-Razor: Modell der Lua-Script-UI (Phase 4b, eigenes Design).
//
// Bewusst KEINE Client-Kopie mehr (die ImGui-Notation mit x/y-Pixeln und
// manuellem DispatchCallbacks war unergonomisch): Elemente stapeln sich
// automatisch vertikal (Rows horizontal), Callbacks/Bindings verwaltet die
// LuaUIAPI. Dieses Modell ist reine, thread-sichere Daten-Schicht:
// - der Lua-Task mutiert es ueber die API,
// - der Avalonia-Host (Razor.Avalonia/ScriptUi) liest Snapshots und meldet
//   Interaktionen ueber die *FromUi-Methoden -> UIEventQueue,
// - die Engine pumpt die Events waehrend Pause() in die Lua-Callbacks.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace Assistant.LuaEngine.UI
{
    // ---- Events ----------------------------------------------------------

    public enum UIEventType
    {
        Click,
        Change,
        TextChange,
        WindowClose
    }

    public readonly struct UIEvent
    {
        public string ControlId { get; }
        public UIEventType EventType { get; }
        public object Value { get; }

        public UIEvent(string controlId, UIEventType eventType, object value = null)
        {
            ControlId = controlId;
            EventType = eventType;
            Value = value;
        }
    }

    /// <summary>Thread-sichere Event-Queue: UI-Thread schreibt, Lua-Task liest.</summary>
    public static class UIEventQueue
    {
        private static readonly ConcurrentQueue<UIEvent> _events = new();

        public static void Enqueue(string controlId, UIEventType type, object value = null)
        {
            _events.Enqueue(new UIEvent(controlId, type, value));
        }

        public static bool TryDequeue(out UIEvent evt) => _events.TryDequeue(out evt);

        public static bool HasEvents => !_events.IsEmpty;

        public static void Clear()
        {
            while (_events.TryDequeue(out _)) { }
        }
    }

    // ---- Elemente --------------------------------------------------------

    public abstract class UiElement
    {
        private static int _nextId;

        public string Id { get; }
        public bool Visible { get; set; } = true;
        public bool Enabled { get; set; } = true;

        /// <summary>0 = automatische Breite.</summary>
        public float Width { get; set; }

        protected UiElement(string kind)
        {
            Id = $"{kind}_{System.Threading.Interlocked.Increment(ref _nextId)}";
        }
    }

    public sealed class UiLabel : UiElement
    {
        public UiLabel() : base("lbl") { }

        public string Text { get; set; } = "";

        /// <summary>Avalonia-Farbstring ("#RRGGBB"); null = Fenster-Default.</summary>
        public string ColorHex { get; set; }
    }

    public sealed class UiButton : UiElement
    {
        public UiButton() : base("btn") { }

        public string Text { get; set; } = "";

        /// <summary>Klick aus der UI -> Event fuer den Lua-Pump.</summary>
        public void PerformClick()
        {
            if (Enabled)
                UIEventQueue.Enqueue(Id, UIEventType.Click);
        }
    }

    public sealed class UiCheckbox : UiElement
    {
        public UiCheckbox() : base("chk") { }

        public string Text { get; set; } = "";
        public bool Checked { get; set; }

        public void SetCheckedFromUi(bool value)
        {
            if (!Enabled || Checked == value)
                return;

            Checked = value;
            UIEventQueue.Enqueue(Id, UIEventType.Change, value);
        }
    }

    public sealed class UiTextBox : UiElement
    {
        public UiTextBox() : base("txt") { }

        public string Text { get; set; } = "";
        public string Placeholder { get; set; } = "";
        public bool IsPassword { get; set; }

        public void SetTextFromUi(string value)
        {
            value ??= "";
            if (!Enabled || Text == value)
                return;

            Text = value;
            UIEventQueue.Enqueue(Id, UIEventType.TextChange, value);
        }
    }

    public sealed class UiSlider : UiElement
    {
        public UiSlider() : base("sld") { }

        public double Min { get; set; }
        public double Max { get; set; } = 100;
        public double Value { get; set; }

        public void SetValueFromUi(double value)
        {
            value = Math.Clamp(value, Min, Max);
            if (!Enabled || Math.Abs(Value - value) < 0.0001)
                return;

            Value = value;
            UIEventQueue.Enqueue(Id, UIEventType.Change, value);
        }
    }

    public sealed class UiProgressBar : UiElement
    {
        public UiProgressBar() : base("prog") { }

        private double _value;

        /// <summary>0..1.</summary>
        public double Value
        {
            get => _value;
            set => _value = Math.Clamp(value, 0, 1);
        }

        public string ColorHex { get; set; }
    }

    public sealed class UiSeparator : UiElement
    {
        public UiSeparator() : base("sep") { }
    }

    /// <summary>Horizontale Zeile — Kinder stehen nebeneinander.</summary>
    public sealed class UiRow : UiElement
    {
        public UiRow() : base("row") { }

        private readonly List<UiElement> _children = new();
        private readonly object _lock = new();

        public void Add(UiElement element)
        {
            lock (_lock)
            {
                _children.Add(element);
            }
        }

        public UiElement[] GetChildrenSnapshot()
        {
            lock (_lock)
            {
                return _children.ToArray();
            }
        }
    }

    // ---- Fenster ---------------------------------------------------------

    public sealed class UiWindow
    {
        private static int _nextId;

        public string Id { get; }
        public string Title { get; set; }

        /// <summary>-1 = Position dem System ueberlassen.</summary>
        public float X { get; set; } = -1;
        public float Y { get; set; } = -1;

        /// <summary>0 = Groesse aus dem Inhalt (SizeToContent).</summary>
        public float WindowWidth { get; set; }
        public float WindowHeight { get; set; }

        public bool IsOpen { get; private set; } = true;
        public bool IsVisible { get; set; } = true;

        private readonly List<UiElement> _elements = new();
        private readonly object _lock = new();

        /// <summary>Zaehlt Struktur-Aenderungen — der Host baut nur dann neu.</summary>
        public int Version { get; private set; }

        private bool _boundsDirty = true;

        public UiWindow(string title)
        {
            Id = $"win_{System.Threading.Interlocked.Increment(ref _nextId)}";
            Title = title ?? "Script";
        }

        public void AddElement(UiElement element)
        {
            lock (_lock)
            {
                _elements.Add(element);
                Version++;
            }
        }

        /// <summary>Row-Kinder aendern die Struktur ebenfalls.</summary>
        public void BumpVersion()
        {
            lock (_lock)
            {
                Version++;
            }
        }

        public UiElement[] GetElementsSnapshot()
        {
            lock (_lock)
            {
                return _elements.ToArray();
            }
        }

        public void SetPosition(float x, float y)
        {
            X = x;
            Y = y;
            _boundsDirty = true;
        }

        public void SetSize(float width, float height)
        {
            WindowWidth = width;
            WindowHeight = height;
            _boundsDirty = true;
        }

        /// <summary>true genau einmal nach SetPosition/SetSize (Host wendet an).</summary>
        public bool TakeBoundsDirty()
        {
            if (!_boundsDirty)
                return false;

            _boundsDirty = false;
            return true;
        }

        public void Show()
        {
            IsVisible = true;
            IsOpen = true;
        }

        public void Hide()
        {
            IsVisible = false;
        }

        /// <summary>Script-seitiges Schliessen (IsOpen=false beendet win:Run()).</summary>
        public void Close()
        {
            IsOpen = false;
            IsVisible = false;
        }

        /// <summary>X-Button der UI: schliessen + WindowClose-Event fuer OnClose.</summary>
        public void NotifyClosedFromUi()
        {
            if (!IsOpen)
                return;

            IsOpen = false;
            IsVisible = false;
            UIEventQueue.Enqueue(Id, UIEventType.WindowClose);
        }
    }

    // ---- Manager ---------------------------------------------------------

    public static class ScriptUIManager
    {
        private static readonly Dictionary<string, UiWindow> _windows = new();
        private static readonly object _lock = new();

        public static UiWindow CreateWindow(string title)
        {
            var window = new UiWindow(title);
            lock (_lock)
            {
                _windows[window.Id] = window;
            }

            return window;
        }

        public static bool DestroyWindow(UiWindow window)
        {
            if (window == null)
                return false;

            lock (_lock)
            {
                return _windows.Remove(window.Id);
            }
        }

        public static void DestroyAllWindows()
        {
            lock (_lock)
            {
                _windows.Clear();
            }
        }

        public static int GetWindowCount()
        {
            lock (_lock)
            {
                return _windows.Count;
            }
        }

        public static UiWindow[] GetWindowsSnapshot()
        {
            lock (_lock)
            {
                var snapshot = new UiWindow[_windows.Count];
                _windows.Values.CopyTo(snapshot, 0);
                return snapshot;
            }
        }
    }
}
