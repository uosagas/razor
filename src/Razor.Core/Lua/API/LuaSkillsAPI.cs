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
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Assistant.VScripts.Core;
using Lua;

namespace Assistant.LuaEngine.API;

/// <summary>
/// Skills API for Lua-CSharp engine
/// </summary>
public static class LuaSkillsAPI
{
    public static void Register(LuaState state)
    {
        var skills = new LuaTable();

        // Functions
        skills["Use"] = new LuaFunction("Use", Use);
        skills["GetValue"] = new LuaFunction("GetValue", GetValue);
        skills["GetBase"] = new LuaFunction("GetBase", GetBase);
        skills["GetCap"] = new LuaFunction("GetCap", GetCap);
        skills["GetLock"] = new LuaFunction("GetLock", GetLock);
        skills["SetLock"] = new LuaFunction("SetLock", SetLock);
        skills["GetAll"] = new LuaFunction("GetAll", GetAll);

        state.Environment["Skills"] = skills;
    }

    private static ValueTask<int> Use(LuaFunctionExecutionContext context, CancellationToken ct)
    {
        try
        {
            var skillName = context.GetArgument<string>(0);

            var player = World.Player;
            var skill = player.Skills.SingleOrDefault(x => x.Name.Equals(skillName, StringComparison.OrdinalIgnoreCase));

            if (skill == null)
            {
                Message.Warning($"Skills.Use: Skill '{skillName}' not found");
                context.Return(false);
                return new ValueTask<int>(1);
            }

            GameActions.UseSkill(skill.Index);
            context.Return(true);
        }
        catch (Exception e)
        {
            Message.Warning($"Error in Skills.Use: {e.Message}");
            context.Return(false);
        }

        return new ValueTask<int>(1);
    }

    private static ValueTask<int> GetValue(LuaFunctionExecutionContext context, CancellationToken ct)
    {
        try
        {
            var skillName = context.GetArgument<string>(0);

            var player = World.Player;
            var skill = player.Skills.SingleOrDefault(x => x.Name.Equals(skillName, StringComparison.OrdinalIgnoreCase));

            if (skill == null)
            {
                Message.Warning($"Skills.GetValue: Skill '{skillName}' not found");
                context.Return(0.0);
                return new ValueTask<int>(1);
            }

            context.Return(skill.Value);
        }
        catch (Exception e)
        {
            Message.Warning($"Error in Skills.GetValue: {e.Message}");
            context.Return(0.0);
        }

        return new ValueTask<int>(1);
    }

    private static ValueTask<int> GetBase(LuaFunctionExecutionContext context, CancellationToken ct)
    {
        try
        {
            var skillName = context.GetArgument<string>(0);

            var player = World.Player;
            var skill = player.Skills.SingleOrDefault(x => x.Name.Equals(skillName, StringComparison.OrdinalIgnoreCase));

            if (skill == null)
            {
                Message.Warning($"Skills.GetBase: Skill '{skillName}' not found");
                context.Return(0.0);
                return new ValueTask<int>(1);
            }

            context.Return(skill.Base);
        }
        catch (Exception e)
        {
            Message.Warning($"Error in Skills.GetBase: {e.Message}");
            context.Return(0.0);
        }

        return new ValueTask<int>(1);
    }

    private static ValueTask<int> GetCap(LuaFunctionExecutionContext context, CancellationToken ct)
    {
        try
        {
            var skillName = context.GetArgument<string>(0);

            var player = World.Player;
            var skill = player.Skills.SingleOrDefault(x => x.Name.Equals(skillName, StringComparison.OrdinalIgnoreCase));

            if (skill == null)
            {
                Message.Warning($"Skills.GetCap: Skill '{skillName}' not found");
                context.Return(0.0);
                return new ValueTask<int>(1);
            }

            context.Return(skill.Cap);
        }
        catch (Exception e)
        {
            Message.Warning($"Error in Skills.GetCap: {e.Message}");
            context.Return(0.0);
        }

        return new ValueTask<int>(1);
    }

    private static ValueTask<int> GetLock(LuaFunctionExecutionContext context, CancellationToken ct)
    {
        try
        {
            var skillName = context.GetArgument<string>(0);

            var player = World.Player;
            var skill = player.Skills.SingleOrDefault(x => x.Name.Equals(skillName, StringComparison.OrdinalIgnoreCase));

            if (skill == null)
            {
                Message.Warning($"Skills.GetLock: Skill '{skillName}' not found");
                context.Return(LuaValue.Nil);
                return new ValueTask<int>(1);
            }

            string lockStatus = skill.Lock switch
            {
                LockType.Up => "Up",
                LockType.Down => "Down",
                LockType.Locked => "Locked",
                _ => "Unknown"
            };

            context.Return(lockStatus);
        }
        catch (Exception e)
        {
            Message.Warning($"Error in Skills.GetLock: {e.Message}");
            context.Return(LuaValue.Nil);
        }

        return new ValueTask<int>(1);
    }

    private static ValueTask<int> SetLock(LuaFunctionExecutionContext context, CancellationToken ct)
    {
        try
        {
            var skillName = context.GetArgument<string>(0);
            var lockStatus = context.GetArgument<string>(1);

            var player = World.Player;
            var skill = player.Skills.SingleOrDefault(x => x.Name.Equals(skillName, StringComparison.OrdinalIgnoreCase));

            if (skill == null)
            {
                Message.Warning($"Skills.SetLock: Skill '{skillName}' not found");
                context.Return(false);
                return new ValueTask<int>(1);
            }

            byte lockValue = lockStatus.ToLowerInvariant() switch
            {
                "up" => 0,
                "down" => 1,
                "locked" or "lock" => 2,
                _ => 2
            };

            GameActions.ChangeSkillLockStatus(skill.Index, (LockType) lockValue);
            context.Return(true);
        }
        catch (Exception e)
        {
            Message.Warning($"Error in Skills.SetLock: {e.Message}");
            context.Return(false);
        }

        return new ValueTask<int>(1);
    }

    private static ValueTask<int> GetAll(LuaFunctionExecutionContext context, CancellationToken ct)
    {
        try
        {
            var player = World.Player;
            var result = new LuaTable();

            foreach (var skill in player.Skills)
            {
                var skillTable = new LuaTable();
                skillTable["Name"] = skill.Name ?? "";
                skillTable["Index"] = (double)skill.Index;
                skillTable["Value"] = skill.Value;
                skillTable["Base"] = skill.Base;
                skillTable["Cap"] = skill.Cap;
                skillTable["Lock"] = skill.Lock switch
                {
                    LockType.Up => "Up",
                    LockType.Down => "Down",
                    LockType.Locked => "Locked",
                    _ => "Unknown"
                };

                result[skill.Name ?? skill.Index.ToString()] = skillTable;
            }

            context.Return(result);
        }
        catch (Exception e)
        {
            Message.Warning($"Error in Skills.GetAll: {e.Message}");
            context.Return(new LuaTable());
        }

        return new ValueTask<int>(1);
    }
}
