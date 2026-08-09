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

// UOSagas-Razor: Config-API fuer Lua-Scripts (Razor-Zusatz, nicht im Client).
//
// Scripts koennen ihre Einstellungen in benannte JSON-Dateien speichern und
// wieder laden — mit ALLEN Lua-Datentypen: string, number, boolean,
// verschachtelte Tabellen und Arrays. Ablage: Data/Profiles/Scripts/Config/
// <name>.json (neben den Scripts, von Hand editierbar).
//
//   Config.Save('MyScript', { containers = {...}, enabled = true })
//   local cfg = Config.Load('MyScript')      -- Tabelle oder nil
//   Config.Exists('MyScript')                -- bool
//   Config.Delete('MyScript')                -- bool
//
// Technik: Die Sandbox hat kein io — die Datei-Ein-/Ausgabe macht C#.
// Serialisiert wird LUA-seitig (konstanter Encoder-Chunk, Tabelle wird ueber
// das Global _ConfigTemp uebergeben), weil LuaTable aus C# nicht enumerierbar
// ist; deserialisiert wird C#-seitig (System.Text.Json -> LuaTable per
// Indexer). Keine Ausfuehrung von Dateiinhalt als Code.

using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Assistant.VScripts.Core;
using Lua;

namespace Assistant.LuaEngine.API;

public static class LuaConfigAPI
{
    private static LuaState _luaState;

    // JSON-Encoder in Lua: laeuft in der Script-Sandbox, liest _ConfigTemp.
    private const string EncoderChunk = @"
local function esc(s)
    s = string.gsub(s, '\\', '\\\\')
    s = string.gsub(s, '""', '\\""')
    s = string.gsub(s, '\n', '\\n')
    s = string.gsub(s, '\r', '\\r')
    s = string.gsub(s, '\t', '\\t')
    return s
end
local function isarray(t)
    local n = 0
    for k in pairs(t) do
        if type(k) ~= 'number' then return false end
        n = n + 1
    end
    return n == #t
end
local function enc(v)
    local tv = type(v)
    if tv == 'nil' then return 'null' end
    if tv == 'boolean' then return v and 'true' or 'false' end
    if tv == 'number' then
        if v == math.floor(v) and math.abs(v) < 2^52 then
            return string.format('%.0f', v)
        end
        return tostring(v)
    end
    if tv == 'string' then return '""' .. esc(v) .. '""' end
    if tv == 'table' then
        local parts = {}
        if isarray(v) then
            for i = 1, #v do parts[#parts + 1] = enc(v[i]) end
            return '[' .. table.concat(parts, ',') .. ']'
        end
        for k, val in pairs(v) do
            parts[#parts + 1] = '""' .. esc(tostring(k)) .. '"":' .. enc(val)
        end
        return '{' .. table.concat(parts, ',') .. '}'
    end
    return 'null'
end
return enc(_ConfigTemp)
";

    public static void Register(LuaState state)
    {
        _luaState = state;

        var config = new LuaTable();
        config["Save"] = new LuaFunction("Save", Save);
        config["Load"] = new LuaFunction("Load", Load);
        config["Exists"] = new LuaFunction("Exists", Exists);
        config["Delete"] = new LuaFunction("Delete", Delete);

        state.Environment["Config"] = config;
    }

    // ---- Pfad-Handling ---------------------------------------------------

    private static string ConfigDirectory =>
        Path.Combine(LuaEngineService.GetScriptRootPath() ?? "Data", "Config");

    /// <summary>Dateipfad zu einem Config-Namen; null bei ungueltigem Namen.
    /// Kein Pfad-Traversal: nur einfache Dateinamen sind erlaubt.</summary>
    private static string ResolvePath(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return null;

        name = name.Trim();
        if (name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
            name != Path.GetFileName(name))
            return null;

        return Path.Combine(ConfigDirectory, name + ".json");
    }

    // ---- Save ------------------------------------------------------------

    private static async ValueTask<int> Save(LuaFunctionExecutionContext context, CancellationToken ct)
    {
        try
        {
            var name = context.GetArgument<string>(0);
            var table = context.GetArgument<LuaTable>(1);

            string path = ResolvePath(name);
            if (path == null)
            {
                Message.Warning($"Config.Save: invalid config name '{name}'.");
                context.Return(false);
                return 1;
            }

            // Tabelle ueber das Global an den Lua-Encoder reichen.
            _luaState.Environment["_ConfigTemp"] = table;
            string json;
            try
            {
                LuaValue[] results = await _luaState.DoStringAsync(EncoderChunk, "config_encode", ct);
                if (results == null || results.Length == 0 || !results[0].TryRead(out json))
                {
                    Message.Warning("Config.Save: could not serialize the table.");
                    context.Return(false);
                    return 1;
                }
            }
            finally
            {
                _luaState.Environment["_ConfigTemp"] = LuaValue.Nil;
            }

            Directory.CreateDirectory(ConfigDirectory);
            File.WriteAllText(path, json);
            context.Return(true);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception e)
        {
            Message.Warning($"Config.Save failed: {e.Message}");
            context.Return(false);
        }

        return 1;
    }

    // ---- Load ------------------------------------------------------------

    private static ValueTask<int> Load(LuaFunctionExecutionContext context, CancellationToken ct)
    {
        try
        {
            var name = context.GetArgument<string>(0);
            string path = ResolvePath(name);

            if (path == null || !File.Exists(path))
            {
                context.Return(LuaValue.Nil);
                return new ValueTask<int>(1);
            }

            using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(path));
            context.Return(ToLuaValue(doc.RootElement));
        }
        catch (Exception e)
        {
            Message.Warning($"Config.Load failed: {e.Message}");
            context.Return(LuaValue.Nil);
        }

        return new ValueTask<int>(1);
    }

    /// <summary>JSON -> Lua: Objekte/Arrays werden zu Tabellen, Zahlen zu
    /// number, null zu nil.</summary>
    private static LuaValue ToLuaValue(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
            {
                var table = new LuaTable();
                foreach (JsonProperty prop in element.EnumerateObject())
                    table[prop.Name] = ToLuaValue(prop.Value);
                return table;
            }

            case JsonValueKind.Array:
            {
                var table = new LuaTable();
                int index = 1;
                foreach (JsonElement item in element.EnumerateArray())
                    table[index++] = ToLuaValue(item);
                return table;
            }

            case JsonValueKind.String:
                return element.GetString() ?? "";

            case JsonValueKind.Number:
                return element.GetDouble();

            case JsonValueKind.True:
                return true;

            case JsonValueKind.False:
                return false;

            default:
                return LuaValue.Nil;
        }
    }

    // ---- Exists / Delete -------------------------------------------------

    private static ValueTask<int> Exists(LuaFunctionExecutionContext context, CancellationToken ct)
    {
        try
        {
            string path = ResolvePath(context.GetArgument<string>(0));
            context.Return(path != null && File.Exists(path));
        }
        catch
        {
            context.Return(false);
        }

        return new ValueTask<int>(1);
    }

    private static ValueTask<int> Delete(LuaFunctionExecutionContext context, CancellationToken ct)
    {
        try
        {
            string path = ResolvePath(context.GetArgument<string>(0));
            if (path != null && File.Exists(path))
            {
                File.Delete(path);
                context.Return(true);
            }
            else
            {
                context.Return(false);
            }
        }
        catch (Exception e)
        {
            Message.Warning($"Config.Delete failed: {e.Message}");
            context.Return(false);
        }

        return new ValueTask<int>(1);
    }
}
