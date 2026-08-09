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
using System.Numerics;
using Assistant.VScripts.Core;

namespace Assistant.VScripts.Nodes;

public enum TargetEntityType
{
    Last,
    Self,
    Selected
}

// Last Target reference node - returns the last target object
public class LastTargetNode : VScriptNode
{
    public LastTargetNode(string id, string pinIdCounter) : base(id, "Last Target", NodeCategory.Game)
    {
        OutputPins.Add(new NodePin(Guid.NewGuid().ToString(), id, "Target", PinType.Object, PinKind.Output));
    }

    // Override to use olive-green color like Unreal Engine reference/getter nodes
    public override Vector4 GetTitleBarColor()
    {
        return new Vector4(0.4f, 0.6f, 0.3f, 1.0f); // Olive-green like UE reference nodes
    }

    public override void Execute(VScriptContext context)
    {
        var targetManager = World.TargetManager;
        // Set the target object reference
        OutputPins[0].Value = targetManager.LastTargetInfo;
    }
}

// Set Last Target action node - sets a serial as the last target
public class SetLastTargetNode : VScriptNode
{
    public SetLastTargetNode(string id, string pinIdCounter) : base(id, "Set Last Target", NodeCategory.Game)
    {
        InputPins.Add(new NodePin(Guid.NewGuid().ToString(), id, "", PinType.Flow, PinKind.Input));
        InputPins.Add(new NodePin(Guid.NewGuid().ToString(), id, "Serial", PinType.Number, PinKind.Input));
        OutputPins.Add(new NodePin(Guid.NewGuid().ToString(), id, "", PinType.Flow, PinKind.Output));
    }

    // Override to use darker blue color like Unreal Engine function nodes
    public override Vector4 GetTitleBarColor()
    {
        return new Vector4(0.2f, 0.4f, 0.8f, 1.0f); // Darker blue like UE function nodes
    }

    public override void Execute(VScriptContext context)
    {
        var serialPin = InputPins.Find(p => p.Name == "Serial");
        if (serialPin?.Value != null)
        {
            try
            {
                uint serial = (uint)System.Convert.ToDouble(serialPin.Value);

                if (serial != 0)
                {
                    // Razor: Last Target direkt setzen (Client haelt das im
                    // TargetManager; bei uns pflegt Targeting den Zustand).
                    Targeting.SetLastTarget((Serial) serial);
                }
            }
            catch (System.Exception ex)
            {
                context.ErrorMessage = $"Set Last Target: Invalid serial value - {ex.Message}";
            }
        }
        else
        {
            context.ErrorMessage = "Set Last Target: Serial is required";
        }
    }
}

// Base class for target property accessors
public abstract class TargetPropertyNode : VScriptNode
{
    protected TargetPropertyNode(string id, string name, string pinIdCounter, PinType outputType) : base(id, name, NodeCategory.Game)
    {
        InputPins.Add(new NodePin(Guid.NewGuid().ToString(), id, "Target", PinType.Object, PinKind.Input));
        OutputPins.Add(new NodePin(Guid.NewGuid().ToString(), id, "Value", outputType, PinKind.Output));
    }

    // Override to use olive-green color like Unreal Engine reference/getter nodes
    public override Vector4 GetTitleBarColor()
    {
        return new Vector4(0.4f, 0.6f, 0.3f, 1.0f); // Olive-green like UE reference nodes
    }

    protected object GetTargetFromInput()
    {
        var targetPin = InputPins[0];
        return targetPin.Value;
    }
}

// Target Serial property
public class GetTargetSerialNode : TargetPropertyNode
{
    public GetTargetSerialNode(string id, string pinIdCounter) : base(id, "Get Serial", pinIdCounter, PinType.Number)
    {
    }

    public override void Execute(VScriptContext context)
    {
        var target = GetTargetFromInput() as LastTargetInfo;
        if (target == null)
        {
            context.ErrorMessage = "Get Serial: Target object reference must be connected";
            return;
        }

        OutputPins[0].Value = (double)(uint)target.Serial;
    }
}

// Target Graphic property
public class GetTargetGraphicNode : TargetPropertyNode
{
    public GetTargetGraphicNode(string id, string pinIdCounter) : base(id, "Get Graphic", pinIdCounter, PinType.Number)
    {
    }

    public override void Execute(VScriptContext context)
    {
        var target = GetTargetFromInput() as LastTargetInfo;
        if (target == null)
        {
            context.ErrorMessage = "Get Graphic: Target object reference must be connected";
            return;
        }

        OutputPins[0].Value = (double)target.Graphic;
    }
}

// Target X property
public class GetTargetXNode : TargetPropertyNode
{
    public GetTargetXNode(string id, string pinIdCounter) : base(id, "Get X", pinIdCounter, PinType.Number)
    {
    }

    public override void Execute(VScriptContext context)
    {
        var target = GetTargetFromInput() as LastTargetInfo;
        if (target == null)
        {
            context.ErrorMessage = "Get X: Target object reference must be connected";
            return;
        }

        OutputPins[0].Value = (double)target.X;
    }
}

// Target Y property
public class GetTargetYNode : TargetPropertyNode
{
    public GetTargetYNode(string id, string pinIdCounter) : base(id, "Get Y", pinIdCounter, PinType.Number)
    {
    }

    public override void Execute(VScriptContext context)
    {
        var target = GetTargetFromInput() as LastTargetInfo;
        if (target == null)
        {
            context.ErrorMessage = "Get Y: Target object reference must be connected";
            return;
        }

        OutputPins[0].Value = (double)target.Y;
    }
}

// Target Z property
public class GetTargetZNode : TargetPropertyNode
{
    public GetTargetZNode(string id, string pinIdCounter) : base(id, "Get Z", pinIdCounter, PinType.Number)
    {
    }

    public override void Execute(VScriptContext context)
    {
        var target = GetTargetFromInput() as LastTargetInfo;
        if (target == null)
        {
            context.ErrorMessage = "Get Z: Target object reference must be connected";
            return;
        }

        OutputPins[0].Value = (double)target.Z;
    }
}

// Target IsEntity property
public class GetTargetIsEntityNode : TargetPropertyNode
{
    public GetTargetIsEntityNode(string id, string pinIdCounter) : base(id, "Is Entity", pinIdCounter, PinType.Boolean)
    {
    }

    public override void Execute(VScriptContext context)
    {
        var target = GetTargetFromInput() as LastTargetInfo;
        if (target == null)
        {
            context.ErrorMessage = "Is Entity: Target object reference must be connected";
            return;
        }

        OutputPins[0].Value = target.IsEntity;
    }
}

// Target IsStatic property
public class GetTargetIsStaticNode : TargetPropertyNode
{
    public GetTargetIsStaticNode(string id, string pinIdCounter) : base(id, "Is Static", pinIdCounter, PinType.Boolean)
    {
    }

    public override void Execute(VScriptContext context)
    {
        var target = GetTargetFromInput() as LastTargetInfo;
        if (target == null)
        {
            context.ErrorMessage = "Is Static: Target object reference must be connected";
            return;
        }

        OutputPins[0].Value = target.IsStatic;
    }
}

// Target IsLand property
public class GetTargetIsLandNode : TargetPropertyNode
{
    public GetTargetIsLandNode(string id, string pinIdCounter) : base(id, "Is Land", pinIdCounter, PinType.Boolean)
    {
    }

    public override void Execute(VScriptContext context)
    {
        var target = GetTargetFromInput() as LastTargetInfo;
        if (target == null)
        {
            context.ErrorMessage = "Is Land: Target object reference must be connected";
            return;
        }

        OutputPins[0].Value = target.IsLand;
    }
}

// Selected Target reference node - returns the selected target serial
public class SelectedTargetNode : VScriptNode
{
    public SelectedTargetNode(string id, string pinIdCounter) : base(id, "Selected Target", NodeCategory.Game)
    {
        OutputPins.Add(new NodePin(Guid.NewGuid().ToString(), id, "Serial", PinType.Number, PinKind.Output));
    }

    // Override to use olive-green color like Unreal Engine reference/getter nodes
    public override Vector4 GetTitleBarColor()
    {
        return new Vector4(0.4f, 0.6f, 0.3f, 1.0f); // Olive-green like UE reference nodes
    }

    public override void Execute(VScriptContext context)
    {
        var targetManager = World.TargetManager;
        OutputPins[0].Value = (double)targetManager.SelectedTarget;
    }
}

// Set Selected Target action node - sets a serial as the selected target
public class SetSelectedTargetNode : VScriptNode
{
    public SetSelectedTargetNode(string id, string pinIdCounter) : base(id, "Set Selected Target", NodeCategory.Game)
    {
        InputPins.Add(new NodePin(Guid.NewGuid().ToString(), id, "", PinType.Flow, PinKind.Input));
        InputPins.Add(new NodePin(Guid.NewGuid().ToString(), id, "Serial", PinType.Number, PinKind.Input));
        OutputPins.Add(new NodePin(Guid.NewGuid().ToString(), id, "", PinType.Flow, PinKind.Output));
    }

    // Override to use darker blue color like Unreal Engine function nodes
    public override Vector4 GetTitleBarColor()
    {
        return new Vector4(0.2f, 0.4f, 0.8f, 1.0f); // Darker blue like UE function nodes
    }

    public override void Execute(VScriptContext context)
    {
        var serialPin = InputPins.Find(p => p.Name == "Serial");
        if (serialPin?.Value != null)
        {
            try
            {
                uint serial = (uint)System.Convert.ToDouble(serialPin.Value);
                var targetManager = World.TargetManager;
                targetManager.SelectedTarget = serial;
            }
            catch (System.Exception ex)
            {
                context.ErrorMessage = $"Set Selected Target: Invalid serial value - {ex.Message}";
            }
        }
        else
        {
            context.ErrorMessage = "Set Selected Target: Serial is required";
        }
    }
}

// Execute Target action node - targets a serial
public class ExecuteTargetNode : VScriptNode
{
    public ExecuteTargetNode(string id, string pinIdCounter) : base(id, "Execute Target", NodeCategory.Game)
    {
        InputPins.Add(new NodePin(Guid.NewGuid().ToString(), id, "", PinType.Flow, PinKind.Input));
        InputPins.Add(new NodePin(Guid.NewGuid().ToString(), id, "Serial", PinType.Number, PinKind.Input));
        OutputPins.Add(new NodePin(Guid.NewGuid().ToString(), id, "", PinType.Flow, PinKind.Output));
    }

    // Override to use darker blue color like Unreal Engine function nodes
    public override Vector4 GetTitleBarColor()
    {
        return new Vector4(0.2f, 0.4f, 0.8f, 1.0f); // Darker blue like UE function nodes
    }

    public override void Execute(VScriptContext context)
    {
        var serialPin = InputPins.Find(p => p.Name == "Serial");
        if (serialPin?.Value != null)
        {
            try
            {
                uint serial = (uint)System.Convert.ToDouble(serialPin.Value);
                var targetManager = World.TargetManager;
                targetManager.Target(serial);
            }
            catch (System.Exception ex)
            {
                context.ErrorMessage = $"Execute Target: Invalid serial value - {ex.Message}";
            }
        }
        else
        {
            context.ErrorMessage = "Execute Target: Serial is required";
        }
    }
}

// Execute Target Entity action node - targets last/self/selected
public class ExecuteTargetEntityNode : VScriptNode
{
    public TargetEntityType EntityType { get; set; }

    public ExecuteTargetEntityNode(string id, string pinIdCounter) : base(id, "Execute Target Entity", NodeCategory.Game)
    {
        EntityType = TargetEntityType.Last;
        InputPins.Add(new NodePin(Guid.NewGuid().ToString(), id, "", PinType.Flow, PinKind.Input));
        OutputPins.Add(new NodePin(Guid.NewGuid().ToString(), id, "", PinType.Flow, PinKind.Output));
    }

    // Override to use darker blue color like Unreal Engine function nodes
    public override Vector4 GetTitleBarColor()
    {
        return new Vector4(0.2f, 0.4f, 0.8f, 1.0f); // Darker blue like UE function nodes
    }

    public override void Execute(VScriptContext context)
    {
        try
        {
            var targetManager = World.TargetManager;
            

            switch (EntityType)
            {
                case TargetEntityType.Last:
                    if (targetManager.LastTargetInfo.IsEntity && targetManager.LastTargetInfo.Serial != 0)
                    {
                        targetManager.Target(targetManager.LastTargetInfo.Serial);
                    }
                    else
                    {
                        context.ErrorMessage = "Execute Target Entity: No valid last target";
                    }
                    break;

                case TargetEntityType.Self:
                    if (World.Player != null)
                    {
                        targetManager.Target(World.Player.Serial);
                    }
                    else
                    {
                        context.ErrorMessage = "Execute Target Entity: Player not found";
                    }
                    break;

                case TargetEntityType.Selected:
                    if (targetManager.SelectedTarget != 0)
                    {
                        targetManager.Target(targetManager.SelectedTarget);
                    }
                    else
                    {
                        context.ErrorMessage = "Execute Target Entity: No selected target";
                    }
                    break;
            }
        }
        catch (System.Exception ex)
        {
            context.ErrorMessage = $"Execute Target Entity: {ex.Message}";
        }
    }
}

// Wait For Target action node - waits for targeting cursor with timeout
public class WaitForTargetNode : VScriptNode
{
    public int Timeout { get; set; }

    public WaitForTargetNode(string id, string pinIdCounter) : base(id, "Wait For Target", NodeCategory.Game)
    {
        Timeout = 10000; // Default 10 seconds
        InputPins.Add(new NodePin(Guid.NewGuid().ToString(), id, "", PinType.Flow, PinKind.Input));
        InputPins.Add(new NodePin(Guid.NewGuid().ToString(), id, "Timeout (ms)", PinType.Number, PinKind.Input));
        OutputPins.Add(new NodePin(Guid.NewGuid().ToString(), id, "", PinType.Flow, PinKind.Output));
    }

    // Override to use darker blue color like Unreal Engine function nodes
    public override Vector4 GetTitleBarColor()
    {
        return new Vector4(0.2f, 0.4f, 0.8f, 1.0f); // Darker blue like UE function nodes
    }

    public override void Execute(VScriptContext context)
    {
        try
        {
            // Get timeout from input pin if provided
            var timeoutPin = InputPins.Find(p => p.Name == "Timeout (ms)");
            int timeout = Timeout;

            if (timeoutPin?.Value != null)
            {
                try
                {
                    timeout = System.Convert.ToInt32(timeoutPin.Value);
                }
                catch
                {
                    // Use default timeout if conversion fails
                }
            }

            // Clamp timeout to reasonable values
            timeout = System.Math.Clamp(timeout, 0, 60000); // Max 60 seconds

            var targetManager = World.TargetManager;

            // Wait for targeting cursor to become active
            var startTime = System.DateTime.Now;
            while (!targetManager.IsTargeting && (System.DateTime.Now - startTime).TotalMilliseconds < timeout)
            {
                System.Threading.Tasks.Task.Delay(50).Wait();
            }

            if (!targetManager.IsTargeting)
            {
                context.ErrorMessage = $"Wait For Target: Timeout after {timeout}ms";
            }
        }
        catch (System.Exception ex)
        {
            context.ErrorMessage = $"Wait For Target: {ex.Message}";
        }
    }
}

// Target Cursor action node - brings up targeting cursor and captures the target
public class TargetCursorNode : VScriptNode
{
    public int Timeout { get; set; }
    public bool BreakOnFailure { get; set; }

    public TargetCursorNode(string id, string pinIdCounter) : base(id, "Target Cursor", NodeCategory.Game)
    {
        Timeout = 10000; // Default 10 seconds
        BreakOnFailure = false;
        InputPins.Add(new NodePin(Guid.NewGuid().ToString(), id, "", PinType.Flow, PinKind.Input));
        OutputPins.Add(new NodePin(Guid.NewGuid().ToString(), id, "", PinType.Flow, PinKind.Output));
        OutputPins.Add(new NodePin(Guid.NewGuid().ToString(), id, "Target", PinType.Object, PinKind.Output));
        OutputPins.Add(new NodePin(Guid.NewGuid().ToString(), id, "Success", PinType.Boolean, PinKind.Output));
    }

    // Override to use darker blue color like Unreal Engine function nodes
    public override Vector4 GetTitleBarColor()
    {
        return new Vector4(0.2f, 0.4f, 0.8f, 1.0f); // Darker blue like UE function nodes
    }

    public override void Execute(VScriptContext context)
    {
        try
        {
            var targetManager = World.TargetManager;

            // Cancel any existing targeting
            if (targetManager.IsTargeting)
            {
                targetManager.CancelTarget();
            }

            // Create a new LastTargetInfo to capture the result
            var capturedTarget = new LastTargetInfo();

            // Razor: Client-seitiger Cursor via OneTimeTarget (LocalTargID);
            // der Callback (Game-Thread) fuellt das Ergebnis, wir warten hier.
            var captured = new LastTargetInfo();
            captured.Clear();
            bool done = false;

            Targeting.OneTimeTarget(true,
                (location, serial, pt, gfx) =>
                {
                    if (serial != 0)
                        captured.SetEntity(serial);
                    else if (location && gfx != 0)
                        captured.SetStatic(gfx, (ushort) pt.X, (ushort) pt.Y, (sbyte) pt.Z);
                    else
                        captured.SetLand((ushort) pt.X, (ushort) pt.Y, (sbyte) pt.Z);
                    done = true;
                },
                () => { done = true; });

            // Wait for targeting to complete or timeout
            var startTime = System.DateTime.Now;
            while (!done && (System.DateTime.Now - startTime).TotalMilliseconds < Timeout)
            {
                // Check if script should stop
                if (context.ShouldStop)
                {
                    Targeting.CancelOneTimeTarget();
                    OutputPins[1].Value = null; // Target
                    OutputPins[2].Value = false; // Success
                    return;
                }

                // Non-blocking wait
                System.Threading.Tasks.Task.Delay(50).Wait();
            }

            // Check if targeting completed successfully
            if (done)
            {
                // Targeting was completed - copy the target info
                var lastTarget = captured;

                if (lastTarget.Serial != 0 || lastTarget.Graphic != 0 || lastTarget.Graphic != 0xFFFF)
                {
                    // Valid target was selected - create a copy
                    if (lastTarget.IsEntity)
                    {
                        capturedTarget.SetEntity(lastTarget.Serial);
                    }
                    else if (lastTarget.IsStatic)
                    {
                        capturedTarget.SetStatic(lastTarget.Graphic, lastTarget.X, lastTarget.Y, lastTarget.Z);
                    }
                    else if (lastTarget.IsLand)
                    {
                        capturedTarget.SetLand(lastTarget.X, lastTarget.Y, lastTarget.Z);
                    }

                    OutputPins[1].Value = capturedTarget; // Target
                    OutputPins[2].Value = true; // Success
                }
                else
                {
                    // User cancelled targeting
                    OutputPins[1].Value = null; // Target
                    OutputPins[2].Value = false; // Success

                    if (BreakOnFailure)
                    {
                        context.ShouldStop = true;
                        context.ErrorMessage = "Target Cursor: Targeting was cancelled";
                    }
                }
            }
            else
            {
                // Timeout occurred
                targetManager.CancelTarget();
                OutputPins[1].Value = null; // Target
                OutputPins[2].Value = false; // Success

                if (BreakOnFailure)
                {
                    context.ShouldStop = true;
                    context.ErrorMessage = $"Target Cursor: Timeout after {Timeout}ms";
                }
            }
        }
        catch (System.Exception ex)
        {
            OutputPins[1].Value = null; // Target
            OutputPins[2].Value = false; // Success
            context.ErrorMessage = $"Target Cursor: {ex.Message}";

            if (BreakOnFailure)
            {
                context.ShouldStop = true;
            }
        }
    }
}
