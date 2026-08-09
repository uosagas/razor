#region license
// Razor: An Ultima Online Assistant
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

// Portiert aus Razor CE (Razor/Scripts/TargetCommands.cs) — 1:1.
// Abweichung: targetrelloc liest in CE die Map-Tile (Ultima.Map.GetTileNear)
// fuer Z/Grafik. Der Port hat KEINEN Map-Zugriff (verschluesselte MULs, D5) —
// stattdessen Ground-Target auf Spieler-Z mit Gfx 0 (ModernUO akzeptiert das).

using System;
using System.Collections.Generic;
using Assistant.Scripts.Engine;
using Assistant.Scripts.Helpers;

namespace Assistant.Scripts
{
    public static class TargetCommands
    {
        public static void Register()
        {
            // Targets
            Interpreter.RegisterCommandHandler("target", Target); //Absolute Target

            Interpreter.RegisterCommandHandler("targettype", TargetType); //TargetTypeAction
            Interpreter.RegisterCommandHandler("targetrelloc", TargetRelLoc); //TargetRelLocAction
            Interpreter.RegisterCommandHandler("targetloc", TargetLocation);

            Interpreter.RegisterCommandHandler("waitfortarget", WaitForTarget); //WaitForTargetAction
            Interpreter.RegisterCommandHandler("wft", WaitForTarget); //WaitForTargetAction
        }

        private static bool Target(string command, Variable[] vars, bool quiet, bool force)
        {
            if (vars.Length < 1)
            {
                throw new RunTimeError("Usage: target (serial) OR target (closest/random/next/prev [noto] [type]");
            }

            switch (vars[0].AsString())
            {
                case "close":
                case "closest":
                    CommandHelper.FindTarget(vars, true);

                    break;

                case "rand":
                case "random":
                    CommandHelper.FindTarget(vars, false, true);

                    break;

                case "next":
                    CommandHelper.FindTarget(vars, false, false, true);

                    break;

                case "prev":
                case "previous":
                    CommandHelper.FindTarget(vars, false, false, false, true);

                    break;

                case "cancel":
                    Targeting.CancelTarget();

                    break;

                case "clear":
                    Targeting.OnClearQueue();

                    break;

                default:
                    Serial serial = vars[0].AsSerial();

                    if (serial != Serial.Zero) // Target a specific item or mobile
                    {
                        Item item = World.FindItem(serial);

                        if (item != null)
                        {
                            Targeting.Target(item);
                            return true;
                        }

                        Mobile mobile = World.FindMobile(serial);

                        if (mobile != null)
                        {
                            Targeting.Target(mobile);
                        }
                    }

                    break;
            }

            return true;
        }

        private static bool TargetType(string command, Variable[] vars, bool quiet, bool force)
        {
            if (Targeting.FromGrabHotKey)
                return false;

            if (vars.Length < 1)
            {
                throw new RunTimeError("Usage: targettype ('name of item or mobile type'/'graphicId') [inrangecheck (true/false)/backpack] [hue]");
            }

            // Outlands-Obermenge: source backpack/self/ground/Serial, 'any',
            // Namens-Wildcards (siehe CommandHelper.ParseFindArgs).
            string gfxStr = vars[0].AsString();
            CommandHelper.FindArgs findArgs = CommandHelper.ParseFindArgs(vars);

            List<Item> items = CommandHelper.GetItems(gfxStr, findArgs);
            List<Mobile> mobiles = items.Count == 0
                ? CommandHelper.GetMobiles(gfxStr, findArgs)
                : new List<Mobile>();

            if (items.Count > 0)
            {
                Targeting.Target(items[Utility.Random(items.Count)]);
            }
            else if (mobiles.Count > 0)
            {
                Targeting.Target(mobiles[Utility.Random(mobiles.Count)]);
            }
            else
            {
                CommandHelper.SendWarning(command, $"Item or mobile type '{gfxStr}' not found", quiet);
            }

            return true;
        }

        private static bool TargetRelLoc(string command, Variable[] vars, bool quiet, bool force)
        {
            if (Targeting.FromGrabHotKey)
                return false;

            if (vars.Length < 2)
            {
                throw new RunTimeError("Usage: targetrelloc (x-offset) (y-offset)");
            }

            int xoffset = Utility.ToInt32(vars[0].AsString(), 0);
            int yoffset = Utility.ToInt32(vars[1].AsString(), 0);

            ushort x = (ushort) (World.Player.Position.X + xoffset);
            ushort y = (ushort) (World.Player.Position.Y + yoffset);
            short z = (short) World.Player.Position.Z;

            try
            {
                // Razor CE: Ultima.Map.GetTileNear(...) liefert Tile-Z + Grafik.
                // Port: kein Map-Zugriff (D5) -> Ground-Target auf Spieler-Z, Gfx 0.
                Targeting.Target(new Point3D(x, y, z));
            }
            catch (Exception e)
            {
                throw new RunTimeError($"{command} - Error Executing: {e.Message}");
            }

            return true;
        }

        private static bool TargetLocation(string command, Variable[] vars, bool quiet, bool force)
        {
            if (vars.Length < 2)
            {
                throw new RunTimeError("Usage: targetloc (x) (y) (z)");
            }

            Targeting.Target(new TargetInfo
            {
                Type = 1,
                Flags = 0,
                Serial = Serial.Zero,
                X = vars[0].AsInt(),
                Y = vars[1].AsInt(),
                Z = vars.Length == 3 ? vars[2].AsInt() : 0,
                Gfx = 0
            });

            return true;
        }

        private static bool WaitForTarget(string command, Variable[] vars, bool quiet, bool force)
        {
            if (Targeting.HasTarget)
            {
                Interpreter.ClearTimeout();
                return true;
            }

            Interpreter.Timeout(vars.Length > 0 ? vars[0].AsUInt() : 30000, () => { return true; });

            return false;
        }
    }
}
