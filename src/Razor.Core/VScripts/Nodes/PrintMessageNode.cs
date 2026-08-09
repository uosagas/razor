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
using Assistant.VScripts.Core;

namespace Assistant.VScripts.Nodes;

public enum PrintMessageType
{
    Info,
    Success,
    Warning,
    Error
}

public class PrintMessageNode : VScriptNode
{
    public PrintMessageType MessageType { get; set; } = PrintMessageType.Info;

    public PrintMessageNode(string id, string pinIdCounter) : base(id, "Print Message", NodeCategory.UI)
    {
        InputPins.Add(new NodePin(Guid.NewGuid().ToString(), id, "", PinType.Flow, PinKind.Input));
        InputPins.Add(new NodePin(Guid.NewGuid().ToString(), id, "Message", PinType.String, PinKind.Input));
        OutputPins.Add(new NodePin(Guid.NewGuid().ToString(), id, "", PinType.Flow, PinKind.Output));
    }

    public override void Execute(VScriptContext context)
    {
        var messagePin = InputPins.Find(p => p.Name == "Message");
        if (messagePin != null && messagePin.Value != null)
        {
            var message = messagePin.Value.ToString();
            switch (MessageType)
            {
                case PrintMessageType.Info:
                    Message.Info(message);
                    break;
                case PrintMessageType.Success:
                    Message.Success(message);
                    break;
                case PrintMessageType.Warning:
                    Message.Warning(message);
                    break;
                case PrintMessageType.Error:
                    Message.Error(message);
                    break;
            }
        }
    }
}
