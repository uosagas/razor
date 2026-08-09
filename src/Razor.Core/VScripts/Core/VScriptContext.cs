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

using System;
using System.Collections.Generic;

namespace Assistant.VScripts.Core;

public class VScriptContext
{
    public Dictionary<string, object> Variables { get; set; }
    public bool ShouldStop { get; set; }
    public string ErrorMessage { get; set; }
    public bool BreakRequested { get; set; } // For breaking out of loops
    public string CurrentScriptName { get; set; } // Track which script is currently executing

    // Razor-Zusatz: aktiver Delay-Node fuer die Editor-Anzeige (Highlight +
    // ms-Countdown). Gesetzt/geleert von DelayNode.Execute; der Editor liest
    // beide Werte im Render-Tick (Engine-Thread schreibt, UI liest — reine
    // Anzeige, Races sind unkritisch).
    public string DelayingNodeId { get; set; }
    public DateTime DelayUntilUtc { get; set; }

    public VScriptContext()
    {
        Variables = new Dictionary<string, object>();
        ShouldStop = false;
        ErrorMessage = null;
        BreakRequested = false;
        CurrentScriptName = null;
    }

    public void SetVariable(string name, object value)
    {
        // Check if this is a global variable
        if (VScripts.Engine.VScriptService.IsGlobalVariable(name))
        {
            VScripts.Engine.VScriptService.SetGlobalVariableValue(name, value);
        }
        else
        {
            Variables[name] = value;
        }
    }

    public object GetVariable(string name)
    {
        // Check if this is a global variable first
        if (VScripts.Engine.VScriptService.IsGlobalVariable(name))
        {
            return VScripts.Engine.VScriptService.GetGlobalVariableValue(name);
        }

        return Variables.TryGetValue(name, out var value) ? value : null;
    }

    public T GetVariable<T>(string name, T defaultValue = default)
    {
        // Check if this is a global variable first
        if (VScripts.Engine.VScriptService.IsGlobalVariable(name))
        {
            var value = VScripts.Engine.VScriptService.GetGlobalVariableValue(name);
            if (value is T typedValue)
            {
                return typedValue;
            }
            return defaultValue;
        }

        if (Variables.TryGetValue(name, out var localValue) && localValue is T typedLocalValue)
        {
            return typedLocalValue;
        }
        return defaultValue;
    }

    public void Clear()
    {
        Variables.Clear();
        ShouldStop = false;
        ErrorMessage = null;
        BreakRequested = false;
        DelayingNodeId = null;
        DelayUntilUtc = default;
    }
}
