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

// UOSagas-Razor: kombinierbare Filterketten fuer FindItems/FindMobiles.
//
// RAZOR-ZUSATZ (nicht im integrierten Assistant): Der Client kennt im
// ByFilter-Modus nur flache, UND-verknuepfte Kriterien. Hier: Liste von
// Filtern mit AND/OR-Verknuepfung, Negation und optionalen Input-Pins
// (Pin verbunden -> Pin-Wert, sonst der konfigurierte Wert).
//
// Die Filtertypen entsprechen den Kriterien des integrierten Assistants
// (FindItemsNode/FindMobilesNode ByFilter). Dateiformat: eigenes Feld
// "findFilters" am SerializableNode, das NUR bei Nutzung geschrieben wird
// (WhenWritingNull) — Dateien ohne Filter bleiben byte-identisch zum
// Client; Dateien MIT Filtern laedt der Client, ignoriert die Filter aber
// still (und verwirft sie beim erneuten Speichern).

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Assistant.VScripts.Core;

namespace Assistant.VScripts.Nodes;

public class FindFilter
{
    public string Type { get; set; }   // Name aus FindFilterCatalog
    public string Value { get; set; }  // konfigurierter Wert (Fallback, wenn Pin leer)
    public bool Negate { get; set; }   // NOT
    public bool Or { get; set; }       // Verknuepfung mit dem VORHERIGEN Filter (false = AND)
    public bool UsePin { get; set; }   // Wert (auch) ueber einen dynamischen Input-Pin
    public string PinId { get; set; }  // Id des dynamischen Pins (wenn UsePin)
}

public class FindFilterType
{
    public string Name { get; init; }
    public PinType PinType { get; init; }
    public bool NeedsValue { get; init; } = true; // bool-Filter brauchen keinen Wert

    public override string ToString() => Name;
}

public static class FindFilterCatalog
{
    // Kriterien des integrierten Assistants (FindItemsNode ByFilter), als
    // Plural: Listen-Werte sind kommasepariert ("0xEED, 0xEEA"); ein einzelner
    // Wert bleibt gueltig. Layers akzeptieren Nummern ODER Namen (onehanded,
    // RightHand, ...). Beim Matching gelten die alten Singular-Namen weiter
    // (bereits gespeicherte Dateien).
    public static readonly FindFilterType[] ItemFilters =
    {
        new() { Name = "Graphics", PinType = PinType.Number },
        new() { Name = "Hues", PinType = PinType.Number },
        new() { Name = "Names", PinType = PinType.String },
        new() { Name = "Serials", PinType = PinType.Number },
        new() { Name = "In Container", PinType = PinType.Number },
        new() { Name = "In Container (recursive)", PinType = PinType.Number },
        new() { Name = "Range Min", PinType = PinType.Number },
        new() { Name = "Range Max", PinType = PinType.Number },
        new() { Name = "Layers", PinType = PinType.String },
        new() { Name = "Amount Min", PinType = PinType.Number },
        new() { Name = "Is Corpse", PinType = PinType.Boolean, NeedsValue = false },
        new() { Name = "Is Container", PinType = PinType.Boolean, NeedsValue = false },
        new() { Name = "Is Movable", PinType = PinType.Boolean, NeedsValue = false },
        new() { Name = "Is On Ground", PinType = PinType.Boolean, NeedsValue = false }
    };

    // Kriterien des integrierten Assistants (FindMobilesNode ByFilter), plural.
    public static readonly FindFilterType[] MobileFilters =
    {
        new() { Name = "Bodies", PinType = PinType.Number },
        new() { Name = "Hues", PinType = PinType.Number },
        new() { Name = "Names", PinType = PinType.String },
        new() { Name = "Serials", PinType = PinType.Number },
        new() { Name = "Range Min", PinType = PinType.Number },
        new() { Name = "Range Max", PinType = PinType.Number },
        new() { Name = "Notorieties", PinType = PinType.Number },
        new() { Name = "Is Dead", PinType = PinType.Boolean, NeedsValue = false },
        new() { Name = "Is Female", PinType = PinType.Boolean, NeedsValue = false },
        new() { Name = "Is Human", PinType = PinType.Boolean, NeedsValue = false },
        new() { Name = "Is Poisoned", PinType = PinType.Boolean, NeedsValue = false },
        new() { Name = "Is Paralyzed", PinType = PinType.Boolean, NeedsValue = false }
    };

    public static FindFilterType Get(bool forMobiles, string name)
    {
        // Alte Singular-Namen (fruehere Dateien) auf die Plural-Katalognamen mappen.
        string canonical = name switch
        {
            "Graphic" => "Graphics",
            "Hue" => "Hues",
            "Name" => "Names",
            "Serial" => "Serials",
            "Layer" => "Layers",
            "Body" => "Bodies",
            "Notoriety" => "Notorieties",
            _ => name
        };

        return (forMobiles ? MobileFilters : ItemFilters).FirstOrDefault(t => t.Name == canonical)
               ?? new FindFilterType { Name = name ?? "?", PinType = PinType.String };
    }

    // ---- Wert-Aufloesung -------------------------------------------------

    /// <summary>Effektiver Rohwert: verbundener Pin gewinnt, sonst der konfigurierte Wert.</summary>
    public static object EffectiveValue(FindFilter filter, NodePin pin) =>
        filter.UsePin && pin?.Value != null ? pin.Value : filter.Value;

    public static double? AsNumber(object raw)
    {
        switch (raw)
        {
            case null:
                return null;
            case double d:
                return d;
            case float f:
                return f;
            case int i:
                return i;
            case uint u:
                return u;
            case string s:
                s = s.Trim();
                if (s.Length == 0)
                    return null;
                if (s.StartsWith("0x", StringComparison.OrdinalIgnoreCase) &&
                    uint.TryParse(s.Substring(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint h))
                    return h;
                return double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out double v)
                    ? v
                    : null;
            default:
                try
                {
                    return Convert.ToDouble(raw, CultureInfo.InvariantCulture);
                }
                catch
                {
                    return null;
                }
        }
    }

    private static string AsString(object raw) =>
        raw == null ? null : Convert.ToString(raw, CultureInfo.InvariantCulture);

    /// <summary>Kommaseparierte Zahlenliste (hex mit 0x ok); Einzelwert = 1 Element.</summary>
    public static List<double> AsNumberList(object raw)
    {
        var result = new List<double>();

        if (raw is string s && s.Contains(','))
        {
            foreach (string part in s.Split(','))
            {
                if (AsNumber(part) is double d)
                    result.Add(d);
            }
        }
        else if (AsNumber(raw) is double single)
        {
            result.Add(single);
        }

        return result;
    }

    /// <summary>Kommaseparierte Stringliste; Einzelwert = 1 Element.</summary>
    public static List<string> AsStringList(object raw)
    {
        string s = AsString(raw);
        if (string.IsNullOrWhiteSpace(s))
            return new List<string>();

        return s.Split(',')
            .Select(p => p.Trim())
            .Where(p => p.Length > 0)
            .ToList();
    }

    /// <summary>Layer als Nummer ODER Name (onehanded/twohanded/quiver/... wie
    /// die Script-Sprache, plus Enum-Namen wie RightHand).</summary>
    public static bool TryParseLayer(string value, out byte layer)
    {
        layer = 0;
        value = value?.Trim();
        if (string.IsNullOrEmpty(value))
            return false;

        if (AsNumber(value) is double n)
        {
            layer = (byte) n;
            return true;
        }

        try
        {
            layer = (byte) Assistant.Scripts.Helpers.CommandHelper.ParseLayer(value);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>Kommaseparierte Layer-Liste (Nummern und Namen mischbar).</summary>
    public static List<byte> AsLayerList(object raw)
    {
        var result = new List<byte>();

        foreach (string part in AsStringList(raw))
        {
            if (TryParseLayer(part, out byte layer))
                result.Add(layer);
        }

        // Zahl-Pin ohne Komma: AsStringList liefert die Rohzahl bereits als Element.
        if (result.Count == 0 && AsNumber(raw) is double n)
            result.Add((byte) n);

        return result;
    }

    // ---- Ketten-Auswertung ----------------------------------------------

    /// <summary>Sequentielle Auswertung links nach rechts: r = f1; r = r AND/OR f2; …</summary>
    public static bool MatchesChain<T>(List<FindFilter> filters, T entity,
        Func<FindFilter, NodePin> pinLookup, Func<FindFilter, object, T, bool> match)
    {
        bool? result = null;

        foreach (var f in filters)
        {
            object raw = EffectiveValue(f, f.UsePin ? pinLookup(f) : null);
            bool m;
            try
            {
                m = match(f, raw, entity);
            }
            catch
            {
                m = false;
            }

            if (f.Negate)
                m = !m;

            result = result == null ? m : f.Or ? result.Value || m : result.Value && m;
        }

        return result ?? true;
    }

    // ---- Praedikate (Property-Oberflaeche wie der Client-ByFilter) -------

    // Listen-Werte matchen als "any of"; Singular-Namen bleiben als Synonyme
    // gueltig (aeltere gespeicherte Dateien).
    public static bool MatchItem(FindFilter f, object raw, Item item)
    {
        switch (f.Type)
        {
            case "Graphic":
            case "Graphics":
                return AsNumberList(raw).Any(g => item.Graphic == (ushort) g);
            case "Hue":
            case "Hues":
                return AsNumberList(raw).Any(h => item.Hue == (ushort) h);
            case "Name":
            case "Names":
                return item.Name != null && AsStringList(raw)
                    .Any(n => item.Name.Contains(n, StringComparison.OrdinalIgnoreCase));
            case "Serial":
            case "Serials":
                return AsNumberList(raw).Any(s => (uint) item.Serial == (uint) s);
            case "In Container":
                // Direkter Container (Serial, kommaseparierte Liste moeglich).
                return AsNumberList(raw).Any(s => item.ContainerSerial == (uint) s);
            case "In Container (recursive)":
                // Auch verschachtelt (Beutel im Rucksack, ...).
                return AsNumberList(raw).Any(s => IsInContainerRecursive(item, (uint) s));
            case "Range Min":
                return AsNumber(raw) is double rmin && item.Distance >= rmin;
            case "Range Max":
                return AsNumber(raw) is double rmax && item.Distance <= rmax;
            case "Layer":
            case "Layers":
                return AsLayerList(raw).Any(l => (byte) item.Layer == l);
            case "Amount Min":
                return AsNumber(raw) is double a && item.Amount >= a;
            case "Is Corpse":
                return item.IsCorpse;
            case "Is Container":
                return item.ItemData.IsContainer;
            case "Is Movable":
                return !item.IsLocked;
            case "Is On Ground":
                return item.OnGround;
            default:
                return false;
        }
    }

    public static bool MatchMobile(FindFilter f, object raw, Mobile mob)
    {
        switch (f.Type)
        {
            case "Body":
            case "Bodies":
                return AsNumberList(raw).Any(b => mob.Graphic == (ushort) b);
            case "Hue":
            case "Hues":
                return AsNumberList(raw).Any(h => mob.Hue == (ushort) h);
            case "Name":
            case "Names":
                return mob.Name != null && AsStringList(raw)
                    .Any(n => mob.Name.Contains(n, StringComparison.OrdinalIgnoreCase));
            case "Serial":
            case "Serials":
                return AsNumberList(raw).Any(s => (uint) mob.Serial == (uint) s);
            case "Range Min":
                return AsNumber(raw) is double rmin && mob.Distance >= rmin;
            case "Range Max":
                return AsNumber(raw) is double rmax && mob.Distance <= rmax;
            case "Notoriety":
            case "Notorieties":
                return AsNumberList(raw).Any(noto => (byte) mob.NotorietyFlag == (byte) noto);
            case "Is Dead":
                return mob.IsDead;
            case "Is Female":
                return mob.IsFemale;
            case "Is Human":
                return mob.IsHuman;
            case "Is Poisoned":
                return mob.IsPoisoned;
            case "Is Paralyzed":
                return mob.IsParalyzed;
            default:
                return false;
        }
    }

    /// <summary>Steckt das Item (auch verschachtelt) im Container mit dieser Serial?
    /// Laeuft die Container-Kette hoch (Item/Mobile/nicht geladene Serial).</summary>
    public static bool IsInContainerRecursive(Item item, uint serial)
    {
        object container = item.Container;

        for (int guard = 0; container != null && guard < 64; guard++)
        {
            switch (container)
            {
                case Item containerItem:
                    if ((uint) containerItem.Serial == serial)
                        return true;
                    container = containerItem.Container;
                    break;
                case Mobile mobile:
                    return (uint) mobile.Serial == serial;
                case Serial rawSerial:
                    if ((uint) rawSerial == serial)
                        return true;

                    // Container noch nicht aufgeloest: ueber die World weiterklettern.
                    Item resolved = rawSerial.IsItem ? World.FindItem(rawSerial) : null;
                    if (resolved == null)
                        return false;

                    if ((uint) resolved.Serial == serial)
                        return true;

                    container = resolved.Container;
                    break;
                default:
                    return false;
            }
        }

        return false;
    }

    // ---- Pin-Verwaltung (dynamische Filter-Pins am Node) -----------------

    private const string PinPrefix = "Filter: ";

    public static bool IsFilterPin(NodePin pin) =>
        pin.Kind == PinKind.Input && pin.Name != null && pin.Name.StartsWith(PinPrefix, StringComparison.Ordinal);

    /// <summary>
    /// Legt fuer UsePin-Filter Input-Pins an bzw. raeumt verwaiste Filter-Pins
    /// ab. Gibt die Ids entfernter Pins zurueck (deren Links muss der Aufrufer
    /// entfernen — der Node kennt den Graphen nicht).
    /// </summary>
    public static List<string> SyncFilterPins(VScriptNode node, List<FindFilter> filters, bool forMobiles)
    {
        var wanted = new HashSet<string>();

        foreach (var f in filters)
        {
            if (!f.UsePin)
            {
                f.PinId = null;
                continue;
            }

            FindFilterType type = Get(forMobiles, f.Type);
            NodePin pin = f.PinId != null ? node.InputPins.FirstOrDefault(p => p.Id == f.PinId) : null;

            if (pin == null)
            {
                pin = new NodePin(Guid.NewGuid().ToString(), node.Id, PinPrefix + f.Type, type.PinType, PinKind.Input);
                node.InputPins.Add(pin);
                f.PinId = pin.Id;
            }
            else
            {
                pin.Name = PinPrefix + f.Type; // Typwechsel: Label nachziehen
                pin.Type = type.PinType;
            }

            wanted.Add(f.PinId);
        }

        var removed = node.InputPins
            .Where(p => IsFilterPin(p) && !wanted.Contains(p.Id))
            .Select(p => p.Id)
            .ToList();

        node.InputPins.RemoveAll(p => removed.Contains(p.Id));
        return removed;
    }

    /// <summary>Kurzbeschreibung einer Filterzeile fuer die Node-Darstellung.</summary>
    public static string Describe(FindFilter f, int index, NodePin pin, bool pinConnected)
    {
        string chain = index == 0 ? "" : f.Or ? "OR " : "AND ";
        string not = f.Negate ? "NOT " : "";
        string value = f.UsePin && pinConnected ? "◈ pin" : f.Value;

        if (string.IsNullOrEmpty(value))
            return $"{chain}{not}{f.Type}";

        if (value.Length > 12)
            value = value.Substring(0, 10) + "…";

        return $"{chain}{not}{f.Type} = {value}";
    }
}
