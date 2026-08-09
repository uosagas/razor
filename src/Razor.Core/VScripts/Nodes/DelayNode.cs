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

using Assistant.VScripts.Core;
using System;
using System.Threading.Tasks;

namespace Assistant.VScripts.Nodes;

public class DelayNode : VScriptNode
{
    public DelayNode(string id, string pinIdCounter) : base(id, "Delay", NodeCategory.Flow)
    {
        InputPins.Add(new NodePin(Guid.NewGuid().ToString(), id, "", PinType.Flow, PinKind.Input));
        InputPins.Add(new NodePin(Guid.NewGuid().ToString(), id, "Milliseconds", PinType.Number, PinKind.Input));
        OutputPins.Add(new NodePin(Guid.NewGuid().ToString(), id, "", PinType.Flow, PinKind.Output));
    }

    public override void Execute(VScriptContext context)
    {
        var msPin = InputPins.Find(p => p.Name == "Milliseconds");
        if (msPin?.Value != null)
        {
            try
            {
                int ms = Convert.ToInt32(msPin.Value);
                if (ms > 0 && ms <= 60000) // Max 60 seconds
                {
                    // Razor-Zusatz: in Scheiben warten statt einem Block —
                    // (a) der Editor kann den Node solange highlighten und die
                    // Restzeit herunterzaehlen (Context.Delaying*), (b) Stop
                    // greift sofort statt erst nach Ablauf des Delays.
                    context.DelayingNodeId = Id;
                    context.DelayUntilUtc = DateTime.UtcNow.AddMilliseconds(ms);

                    try
                    {
                        while (DateTime.UtcNow < context.DelayUntilUtc && !context.ShouldStop)
                        {
                            Task.Delay(25).Wait();
                        }
                    }
                    finally
                    {
                        context.DelayingNodeId = null;
                    }
                }
            }
            catch
            {
                // Ignore invalid delay values
            }
        }
    }
}
