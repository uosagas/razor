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
using System.Threading;
using System.Threading.Tasks;
using Assistant.LuaEngine.Enums;
using Assistant.VScripts.Core;
using Lua;

namespace Assistant.LuaEngine.API;

/// <summary>
/// Spells API for Lua-CSharp engine
/// </summary>
public static class LuaSpellsAPI
{
    public static void Register(LuaState state)
    {
        var spells = new LuaTable();

        // Functions
        spells["Cast"] = new LuaFunction("Cast", Cast);
        spells["CastById"] = new LuaFunction("CastById", CastById);

        state.Environment["Spells"] = spells;
    }

    private static ValueTask<int> Cast(LuaFunctionExecutionContext context, CancellationToken ct)
    {
        try
        {
            // Razor-Fix: Scripts uebergeben den ANZEIGE-Namen mit Leerzeichen
            // ('Greater Heal'), der Enum kennt nur 'GreaterHeal' — der
            // urspruengliche Enum.TryParse auf dem Rohstring fand deshalb NIE
            // einen Spell. Zusaetzlich darf auch direkt eine Spell-ID kommen.
            // String ZUERST lesen (etabliertes ArgAsString-Muster) — ein
            // fehlgeschlagener GetArgument<double> macht Folge-Reads kaputt.
            string spellName = null;
            try
            {
                spellName = context.GetArgument<string>(0);
            }
            catch
            {
            }

            if (!string.IsNullOrEmpty(spellName))
            {
                string key = NormalizeSpellName(spellName);

                // Enum.TryParse akzeptiert auch numerische Strings ('29').
                // ⚠ VOLL qualifizieren: der Namespace-Walk loest 'Spell' sonst
                // zu Razors KLASSE Assistant.Spell auf (die usings verlieren
                // gegen die Namespace-Hierarchie) — Enum.TryParse wirft dann
                // zur Laufzeit 'Type provided must be an Enum' und Cast gab
                // fuer JEDEN Namen false zurueck.
                if (Enum.TryParse(typeof(Enums.Spell), key, true, out var result))
                {
                    GameActions.CastSpell((int)result);
                    context.Return(true);
                }
                else
                {
                    Message.Warning($"Cast: Unknown spell '{spellName}'");
                    context.Return(false);
                }
            }
            else if (TryGetNumber(context, out int numericId))
            {
                GameActions.CastSpell(numericId);
                context.Return(true);
            }
            else
            {
                Message.Warning("Cast: expected a spell name or id");
                context.Return(false);
            }
        }
        catch (Exception e)
        {
            Message.Warning($"Error in Cast: {e.Message}");
            context.Return(false);
        }

        return new ValueTask<int>(1);
    }

    /// <summary>'Greater Heal' / 'greater-heal' / "Mage's ..." -> 'GreaterHeal'.</summary>
    private static string NormalizeSpellName(string name)
    {
        if (string.IsNullOrEmpty(name))
            return "";

        return name.Replace(" ", "").Replace("'", "").Replace("-", "").Replace("_", "");
    }

    private static bool TryGetNumber(LuaFunctionExecutionContext context, out int value)
    {
        value = 0;
        if (context.ArgumentCount < 1)
            return false;

        try
        {
            value = (int)context.GetArgument<double>(0);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static ValueTask<int> CastById(LuaFunctionExecutionContext context, CancellationToken ct)
    {
        try
        {
            var spellId = (int)context.GetArgument<double>(0);
            GameActions.CastSpell(spellId);
            context.Return(true);
        }
        catch (Exception e)
        {
            Message.Warning($"Error in CastById: {e.Message}");
            context.Return(false);
        }

        return new ValueTask<int>(1);
    }
}
