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

// UOSagas-Razor: Autocomplete-Korpus fuer den Lua-Editor (Phase 4b).
//
// 1:1-Uebernahme des Client-Korpus (Assistant/CodeEditor/LuaAutoCompleteManager.cs,
// Initialize*-Methoden) — der Client pflegt seine Vorschlaege/Tooltips dort von
// Hand; diese Datei ist die diff-arme Kopie davon (D25/D27). Beim Nachziehen
// von Client-Updates: nur die Initialize*-Bloecke vergleichen/ersetzen.
// Der ImGui-Rendering-Teil des Originals entfaellt (AvaloniaEdit uebernimmt das);
// AddCompletion baut stattdessen CompletionEntry-Objekte fuer ILanguageDefinition.

using System.Collections.Generic;
using System.Text;

namespace Razor.UI.Editor
{
    internal static class LuaCompletionCorpus
    {
        private enum CompletionType
        {
            Keyword,
            Function,
            Variable,
            Snippet,
            Type
        }

        private static List<CompletionEntry> _entries;

        public static List<CompletionEntry> Entries
        {
            get
            {
                if (_entries == null)
                {
                    _entries = new List<CompletionEntry>();
                    InitializeKeywords();
                    InitializeStandardLibrary();
                    InitializeClassicUOAPI();
                    InitializeScriptUiAPI();
                    InitializeSnippets();
                }

                return _entries;
            }
        }

        // ---- ab hier: Client-Korpus 1:1 (LuaAutoCompleteManager.Initialize*) ----

        private static void InitializeKeywords()
        {
            var keywords = new Dictionary<string, string>
            {
                ["and"] = "Logical AND operator",
                ["break"] = "Break out of a loop",
                ["do"] = "Start of a do...end block",
                ["else"] = "Alternative branch in if statement",
                ["elseif"] = "Additional condition in if statement",
                ["end"] = "End of a block",
                ["false"] = "Boolean false value",
                ["for"] = "Numeric or generic for loop",
                ["function"] = "Define a function",
                ["goto"] = "Jump to a label",
                ["if"] = "Conditional statement",
                ["in"] = "Used in generic for loop",
                ["local"] = "Declare a local variable",
                ["nil"] = "Nil value (absence of value)",
                ["not"] = "Logical NOT operator",
                ["or"] = "Logical OR operator",
                ["repeat"] = "Start of repeat...until loop",
                ["return"] = "Return from a function",
                ["then"] = "Then clause in if statement",
                ["true"] = "Boolean true value",
                ["until"] = "End of repeat...until loop",
                ["while"] = "While loop"
            };

            foreach (var (keyword, desc) in keywords)
            {
                AddCompletion(keyword, CompletionType.Keyword, desc);
            }
        }

        private static void InitializeStandardLibrary()
        {
            // Base functions
            AddCompletion("print", CompletionType.Function, "Print values to console", "print(...)", new List<string> { "print('Hello!')", "print(x, y, z)" });
            AddCompletion("type", CompletionType.Function, "Get the type of a value", "type(v)", new List<string> { "type(42) -- returns 'number'", "type('hi') -- returns 'string'" });
            AddCompletion("tostring", CompletionType.Function, "Convert to string", "tostring(v)", new List<string> { "tostring(42) -- returns '42'" });
            AddCompletion("tonumber", CompletionType.Function, "Convert to number", "tonumber(s [,base])", new List<string> { "tonumber('42') -- returns 42", "tonumber('FF', 16) -- returns 255" });
            AddCompletion("pairs", CompletionType.Function, "Iterate over table pairs", "pairs(t)", new List<string> { "for k, v in pairs(t) do print(k, v) end" });
            AddCompletion("ipairs", CompletionType.Function, "Iterate over array indices", "ipairs(t)", new List<string> { "for i, v in ipairs(t) do print(i, v) end" });
            AddCompletion("error", CompletionType.Function, "Raise an error", "error(message [,level])", new List<string> { "error('Something went wrong!')" });
            AddCompletion("pcall", CompletionType.Function, "Protected call (catch errors)", "pcall(f, ...)", new List<string> { "local ok, err = pcall(function() error('test') end)" });

            // String library
            AddCompletion("string.format", CompletionType.Function, "Format a string", "string.format(format, ...)", new List<string> { "string.format('x=%d, y=%d', 10, 20)", "string.format('%s: %d', name, value)" });
            AddCompletion("string.sub", CompletionType.Function, "Extract substring", "string.sub(s, i [,j])", new List<string> { "string.sub('Hello', 1, 3) -- 'Hel'" });
            AddCompletion("string.find", CompletionType.Function, "Find pattern in string", "string.find(s, pattern [,init [,plain]])", new List<string> { "string.find('Hello', 'l') -- returns 3, 3" });
            AddCompletion("string.match", CompletionType.Function, "Match pattern and return captures", "string.match(s, pattern [,init])", new List<string> { "string.match('key=value', '(%w+)=(%w+)')" });
            AddCompletion("string.gsub", CompletionType.Function, "Global substitution", "string.gsub(s, pattern, repl [,n])", new List<string> { "string.gsub('hello', 'l', 'L') -- 'heLLo'" });
            AddCompletion("string.lower", CompletionType.Function, "Convert to lowercase", "string.lower(s)", new List<string> { "string.lower('HELLO') -- 'hello'" });
            AddCompletion("string.upper", CompletionType.Function, "Convert to uppercase", "string.upper(s)", new List<string> { "string.upper('hello') -- 'HELLO'" });

            // Table library
            AddCompletion("table.insert", CompletionType.Function, "Insert element into table", "table.insert(list, [pos,] value)", new List<string> { "table.insert(t, 'new')", "table.insert(t, 1, 'first')" });
            AddCompletion("table.remove", CompletionType.Function, "Remove element from table", "table.remove(list [,pos])", new List<string> { "table.remove(t)", "table.remove(t, 1)" });
            AddCompletion("table.concat", CompletionType.Function, "Concatenate table elements", "table.concat(list [,sep [,i [,j]]])", new List<string> { "table.concat({'a','b','c'}, ',') -- 'a,b,c'" });
            AddCompletion("table.sort", CompletionType.Function, "Sort table in place", "table.sort(list [,comp])", new List<string> { "table.sort(t)", "table.sort(t, function(a,b) return a > b end)" });

            // Math library
            AddCompletion("math.abs", CompletionType.Function, "Absolute value", "math.abs(x)", new List<string> { "math.abs(-5) -- 5" });
            AddCompletion("math.floor", CompletionType.Function, "Round down", "math.floor(x)", new List<string> { "math.floor(3.7) -- 3" });
            AddCompletion("math.ceil", CompletionType.Function, "Round up", "math.ceil(x)", new List<string> { "math.ceil(3.2) -- 4" });
            AddCompletion("math.min", CompletionType.Function, "Minimum value", "math.min(...)", new List<string> { "math.min(1, 2, 3) -- 1" });
            AddCompletion("math.max", CompletionType.Function, "Maximum value", "math.max(...)", new List<string> { "math.max(1, 2, 3) -- 3" });
            AddCompletion("math.random", CompletionType.Function, "Random number", "math.random([m [,n]])", new List<string> { "math.random() -- 0 to 1", "math.random(10) -- 1 to 10", "math.random(5, 10) -- 5 to 10" });
            AddCompletion("math.sqrt", CompletionType.Function, "Square root", "math.sqrt(x)", new List<string> { "math.sqrt(16) -- 4" });
        }

        private static void InitializeClassicUOAPI()
        {
            // Player API
            AddCompletion("Player", CompletionType.Type, "Player API module - access player information and actions");
            // Player Methods
            AddCompletion("Player.Say", CompletionType.Function, "Say a message", "Player.Say(message [,hue])", new List<string> { "Player.Say('Hello!')", "Player.Say('Hello!', 33)" });
            AddCompletion("Player.SayParty", CompletionType.Function, "Say to party chat", "Player.SayParty(message)", new List<string> { "Player.SayParty('Incoming!')" });
            AddCompletion("Player.SayGuild", CompletionType.Function, "Say to guild chat", "Player.SayGuild(message)", new List<string> { "Player.SayGuild('Hello guild!')" });
            AddCompletion("Player.SayAlliance", CompletionType.Function, "Say to alliance chat", "Player.SayAlliance(message)", new List<string> { "Player.SayAlliance('Hello alliance!')" });
            AddCompletion("Player.SayWhisper", CompletionType.Function, "Whisper a message", "Player.SayWhisper(message)", new List<string> { "Player.SayWhisper('Secret message')" });
            AddCompletion("Player.SayYell", CompletionType.Function, "Yell a message", "Player.SayYell(message)", new List<string> { "Player.SayYell('Help!')" });
            AddCompletion("Player.SayChat", CompletionType.Function, "Say to chat channel", "Player.SayChat(message)", new List<string> { "Player.SayChat('Hello chat!')" });
            AddCompletion("Player.SayEmote", CompletionType.Function, "Perform an emote", "Player.SayEmote(message)", new List<string> { "Player.SayEmote('waves')" });
            AddCompletion("Player.UseObject", CompletionType.Function, "Double-click an object", "Player.UseObject(serial)", new List<string> { "Player.UseObject(item.Serial)" });
            AddCompletion("Player.UseObjectByType", CompletionType.Function, "Use object by graphic type", "Player.UseObjectByType(graphic)", new List<string> { "Player.UseObjectByType(0x0E21)" });
            AddCompletion("Player.ClickObject", CompletionType.Function, "Single-click an object", "Player.ClickObject(serial)", new List<string> { "Player.ClickObject(mob.Serial)" });
            AddCompletion("Player.Attack", CompletionType.Function, "Attack a mobile", "Player.Attack(serial)", new List<string> { "Player.Attack(enemy.Serial)" });
            AddCompletion("Player.ClearHands", CompletionType.Function, "Unequip items from hands", "Player.ClearHands(hand)", new List<string> { "Player.ClearHands('left')", "Player.ClearHands('right')", "Player.ClearHands('both')" });
            AddCompletion("Player.Turn", CompletionType.Function, "Turn player to direction", "Player.Turn(direction)", new List<string> { "Player.Turn('North')", "Player.Turn('South')" });
            AddCompletion("Player.Equip", CompletionType.Function, "Equip an item", "Player.Equip(serial)", new List<string> { "Player.Equip(weapon.Serial)" });
            AddCompletion("Player.PickUp", CompletionType.Function, "Pick up an item", "Player.PickUp(serial [,amount])", new List<string> { "Player.PickUp(item.Serial)", "Player.PickUp(item.Serial, 10)" });
            AddCompletion("Player.DropInBackpack", CompletionType.Function, "Drop held item in backpack", "Player.DropInBackpack()", new List<string> { "Player.DropInBackpack()" });
            AddCompletion("Player.DropInContainer", CompletionType.Function, "Drop held item in container", "Player.DropInContainer(serial)", new List<string> { "Player.DropInContainer(bag.Serial)" });
            AddCompletion("Player.DropOnGround", CompletionType.Function, "Drop held item on ground", "Player.DropOnGround()", new List<string> { "Player.DropOnGround()" });
            AddCompletion("Player.ToggleWarMode", CompletionType.Function, "Toggle war mode", "Player.ToggleWarMode()", new List<string> { "Player.ToggleWarMode()" });
            AddCompletion("Player.PopPouch", CompletionType.Function, "Use trapped pouch", "Player.PopPouch()", new List<string> { "Player.PopPouch()" });
            // Player Properties - Basic
            AddCompletion("Player.Serial", CompletionType.Variable, "Player's serial number", "Player.Serial");
            AddCompletion("Player.Name", CompletionType.Variable, "Player's name", "Player.Name");
            AddCompletion("Player.X", CompletionType.Variable, "Player's X coordinate", "Player.X");
            AddCompletion("Player.Y", CompletionType.Variable, "Player's Y coordinate", "Player.Y");
            AddCompletion("Player.Z", CompletionType.Variable, "Player's Z coordinate", "Player.Z");
            AddCompletion("Player.Direction", CompletionType.Variable, "Player's facing direction", "Player.Direction");
            AddCompletion("Player.Hue", CompletionType.Variable, "Player's hue/color", "Player.Hue");
            AddCompletion("Player.Graphic", CompletionType.Variable, "Player's graphic/body ID", "Player.Graphic");
            AddCompletion("Player.Backpack", CompletionType.Variable, "Backpack item table", "Player.Backpack");
            // Player Properties - Stats
            AddCompletion("Player.Hits", CompletionType.Variable, "Current hit points", "Player.Hits");
            AddCompletion("Player.HitsMax", CompletionType.Variable, "Maximum hit points", "Player.HitsMax");
            AddCompletion("Player.DiffHits", CompletionType.Variable, "Difference between max and current hits", "Player.DiffHits");
            AddCompletion("Player.Mana", CompletionType.Variable, "Current mana", "Player.Mana");
            AddCompletion("Player.MaxMana", CompletionType.Variable, "Maximum mana", "Player.MaxMana");
            AddCompletion("Player.Stam", CompletionType.Variable, "Current stamina", "Player.Stam");
            AddCompletion("Player.MaxStam", CompletionType.Variable, "Maximum stamina", "Player.MaxStam");
            AddCompletion("Player.Str", CompletionType.Variable, "Strength stat", "Player.Str");
            AddCompletion("Player.Dex", CompletionType.Variable, "Dexterity stat", "Player.Dex");
            AddCompletion("Player.Int", CompletionType.Variable, "Intelligence stat", "Player.Int");
            AddCompletion("Player.StatsCap", CompletionType.Variable, "Stats cap", "Player.StatsCap");
            // Player Properties - Combat
            AddCompletion("Player.DamageMin", CompletionType.Variable, "Minimum damage", "Player.DamageMin");
            AddCompletion("Player.DamageMax", CompletionType.Variable, "Maximum damage", "Player.DamageMax");
            AddCompletion("Player.SwingSpeedIncrease", CompletionType.Variable, "Swing speed increase %", "Player.SwingSpeedIncrease");
            AddCompletion("Player.HitChanceIncrease", CompletionType.Variable, "Hit chance increase %", "Player.HitChanceIncrease");
            AddCompletion("Player.DefenseChanceIncrease", CompletionType.Variable, "Defense chance increase %", "Player.DefenseChanceIncrease");
            AddCompletion("Player.SpellDamageIncrease", CompletionType.Variable, "Spell damage increase %", "Player.SpellDamageIncrease");
            // Player Properties - Resistances
            AddCompletion("Player.PhysicalResistance", CompletionType.Variable, "Physical resistance %", "Player.PhysicalResistance");
            AddCompletion("Player.FireResistance", CompletionType.Variable, "Fire resistance %", "Player.FireResistance");
            AddCompletion("Player.ColdResistance", CompletionType.Variable, "Cold resistance %", "Player.ColdResistance");
            AddCompletion("Player.PoisonResistance", CompletionType.Variable, "Poison resistance %", "Player.PoisonResistance");
            AddCompletion("Player.EnergyResistance", CompletionType.Variable, "Energy resistance %", "Player.EnergyResistance");
            AddCompletion("Player.ReflectPhysicalDamage", CompletionType.Variable, "Reflect physical damage %", "Player.ReflectPhysicalDamage");
            // Player Properties - Regeneration
            AddCompletion("Player.HitPointsRegeneration", CompletionType.Variable, "HP regeneration", "Player.HitPointsRegeneration");
            AddCompletion("Player.ManaRegeneration", CompletionType.Variable, "Mana regeneration", "Player.ManaRegeneration");
            AddCompletion("Player.StaminaRegeneration", CompletionType.Variable, "Stamina regeneration", "Player.StaminaRegeneration");
            AddCompletion("Player.LowerManaCost", CompletionType.Variable, "Lower mana cost %", "Player.LowerManaCost");
            AddCompletion("Player.LowerReagentCost", CompletionType.Variable, "Lower reagent cost %", "Player.LowerReagentCost");
            // Player Properties - Misc
            AddCompletion("Player.Gold", CompletionType.Variable, "Gold in backpack", "Player.Gold");
            AddCompletion("Player.Weight", CompletionType.Variable, "Current weight", "Player.Weight");
            AddCompletion("Player.MaxWeight", CompletionType.Variable, "Maximum weight", "Player.MaxWeight");
            AddCompletion("Player.DiffWeight", CompletionType.Variable, "Available weight capacity", "Player.DiffWeight");
            AddCompletion("Player.Luck", CompletionType.Variable, "Luck value", "Player.Luck");
            AddCompletion("Player.TithingPoints", CompletionType.Variable, "Tithing points", "Player.TithingPoints");
            AddCompletion("Player.Followers", CompletionType.Variable, "Current followers", "Player.Followers");
            AddCompletion("Player.MaxFollowers", CompletionType.Variable, "Maximum followers", "Player.MaxFollowers");
            AddCompletion("Player.Distance", CompletionType.Variable, "Distance from player (always 0)", "Player.Distance");
            // Player Properties - Status Flags
            AddCompletion("Player.IsRunning", CompletionType.Variable, "Is player running", "Player.IsRunning");
            AddCompletion("Player.IsParalyzed", CompletionType.Variable, "Is player paralyzed", "Player.IsParalyzed");
            AddCompletion("Player.IsDead", CompletionType.Variable, "Is player dead", "Player.IsDead");
            AddCompletion("Player.IsHidden", CompletionType.Variable, "Is player hidden", "Player.IsHidden");
            AddCompletion("Player.IsPoisoned", CompletionType.Variable, "Is player poisoned", "Player.IsPoisoned");
            AddCompletion("Player.IsMounted", CompletionType.Variable, "Is player mounted", "Player.IsMounted");
            AddCompletion("Player.IsFlying", CompletionType.Variable, "Is player flying (gargoyle)", "Player.IsFlying");
            AddCompletion("Player.IsHuman", CompletionType.Variable, "Is player human race", "Player.IsHuman");
            AddCompletion("Player.IsGargoyle", CompletionType.Variable, "Is player gargoyle race", "Player.IsGargoyle");
            AddCompletion("Player.IsYellowHits", CompletionType.Variable, "Is player yellow hits (mortal strike)", "Player.IsYellowHits");
            AddCompletion("Player.IsRenamable", CompletionType.Variable, "Is player renamable", "Player.IsRenamable");
            AddCompletion("Player.IsFemale", CompletionType.Variable, "Is player female", "Player.IsFemale");
            AddCompletion("Player.IsDestroyed", CompletionType.Variable, "Is player object destroyed", "Player.IsDestroyed");
            // Player Properties - Info
            AddCompletion("Player.NotorietyFlag", CompletionType.Variable, "Player's notoriety (Innocent, Criminal, etc)", "Player.NotorietyFlag");
            AddCompletion("Player.Race", CompletionType.Variable, "Player's race (Human, Elf, Gargoyle)", "Player.Race");
            AddCompletion("Player.Title", CompletionType.Variable, "Player's title", "Player.Title");
            AddCompletion("Player.SpeedMode", CompletionType.Variable, "Player's speed mode", "Player.SpeedMode");

            // Items API
            AddCompletion("Items", CompletionType.Type, "Items API module - find and interact with items");
            AddCompletion("Items.FindBySerial", CompletionType.Function, "Find item by serial", "Items.FindBySerial(serial)", new List<string> { "local item = Items.FindBySerial(0x12345)" });
            AddCompletion("Items.FindByType", CompletionType.Function, "Find item by graphic type", "Items.FindByType(graphic)", new List<string> { "local item = Items.FindByType(0x0E21)" });
            AddCompletion("Items.FindByFilter", CompletionType.Function, "Find items by filter", "Items.FindByFilter(filter)", new List<string> { "local items = Items.FindByFilter({graphic=0x0E21, container=Player.Backpack})" });
            AddCompletion("Items.CountType", CompletionType.Function, "Count items by type in backpack", "Items.CountType(graphic [,hue])", new List<string> { "local count = Items.CountType(0x0EED)" });
            AddCompletion("Items.CountTypeInContainer", CompletionType.Function, "Count items in container", "Items.CountTypeInContainer(container, graphic [,hue])", new List<string> { "local count = Items.CountTypeInContainer(bag, 0x0EED)" });

            // Mobiles API
            AddCompletion("Mobiles", CompletionType.Type, "Mobiles API module - find and interact with mobiles");
            AddCompletion("Mobiles.FindBySerial", CompletionType.Function, "Find mobile by serial", "Mobiles.FindBySerial(serial)", new List<string> { "local mob = Mobiles.FindBySerial(0x12345)" });
            AddCompletion("Mobiles.FindByType", CompletionType.Function, "Find mobile by graphic type", "Mobiles.FindByType(graphic)", new List<string> { "local mob = Mobiles.FindByType(0x190)" });
            AddCompletion("Mobiles.FindByName", CompletionType.Function, "Find mobile by name", "Mobiles.FindByName(name)", new List<string> { "local mob = Mobiles.FindByName('Guard')" });
            AddCompletion("Mobiles.FindByFilter", CompletionType.Function, "Find mobiles by filter", "Mobiles.FindByFilter(filter)", new List<string> { "local mobs = Mobiles.FindByFilter({rangemax=10, human=true})" });

            // Target API
            AddCompletion("Target", CompletionType.Type, "Target API module - targeting operations");
            AddCompletion("Target.WaitForTarget", CompletionType.Function, "Wait for target cursor", "Target.WaitForTarget(timeout)", new List<string> { "if Target.WaitForTarget(5000) then ... end" });
            AddCompletion("Target.GetNewTarget", CompletionType.Function, "Wait for user to select target", "Target.GetNewTarget(timeout)", new List<string> { "local target = Target.GetNewTarget(10000)" });
            AddCompletion("Target.TargetSerial", CompletionType.Function, "Target a specific serial", "Target.TargetSerial(serial)", new List<string> { "Target.TargetSerial(enemy.Serial)" });
            AddCompletion("Target.CancelTarget", CompletionType.Function, "Cancel current target", "Target.CancelTarget()", new List<string> { "Target.CancelTarget()" });
            AddCompletion("Target.Self", CompletionType.Function, "Target self", "Target.Self()", new List<string> { "Target.Self()" });
            AddCompletion("Target.Last", CompletionType.Function, "Target last target", "Target.Last()", new List<string> { "Target.Last()" });
            AddCompletion("Target.IsTargeting", CompletionType.Function, "Check if targeting cursor active", "Target.IsTargeting()", new List<string> { "if Target.IsTargeting() then ... end" });

            // Gumps API
            AddCompletion("Gumps", CompletionType.Type, "Gumps API module - gump interaction");
            AddCompletion("Gumps.HasGump", CompletionType.Function, "Check if gump exists", "Gumps.HasGump([gumpId])", new List<string> { "if Gumps.HasGump() then ... end" });
            AddCompletion("Gumps.IsActive", CompletionType.Function, "Check if gump exists (alias for HasGump)", "Gumps.IsActive([gumpId])", new List<string> { "if Gumps.IsActive(0x12345) then ... end" });
            AddCompletion("Gumps.GetGump", CompletionType.Function, "Get gump info with Serial, X, Y, Width, Height, Texts", "Gumps.GetGump([gumpId])", new List<string> { "local gump = Gumps.GetGump(0x12345)", "for i, text in ipairs(gump.Texts) do print(text) end" });
            AddCompletion("Gumps.WaitForGump", CompletionType.Function, "Wait for gump to appear", "Gumps.WaitForGump(gumpId, timeout)", new List<string> { "if Gumps.WaitForGump(0x12345, 5000) then ... end" });
            AddCompletion("Gumps.Reply", CompletionType.Function, "Click gump button", "Gumps.Reply(gumpId, buttonId)", new List<string> { "Gumps.Reply(0x12345, 1)" });
            AddCompletion("Gumps.PressButton", CompletionType.Function, "Click gump button (alias for Reply)", "Gumps.PressButton(gumpId, buttonId)", new List<string> { "Gumps.PressButton(0x12345, 1)" });
            AddCompletion("Gumps.CloseGump", CompletionType.Function, "Close a gump", "Gumps.CloseGump([gumpId])", new List<string> { "Gumps.CloseGump()" });
            AddCompletion("Gumps.Close", CompletionType.Function, "Close a gump (alias for CloseGump)", "Gumps.Close([gumpId])", new List<string> { "Gumps.Close(0x12345)" });
            AddCompletion("Gumps.Send", CompletionType.Function, "Send gump response with switches/text entries", "Gumps.Send(gumpId, buttonId [,switches] [,textEntries])", new List<string> { "Gumps.Send(0x12345, 1)" });

            // Spells API
            AddCompletion("Spells", CompletionType.Type, "Spells API module - spell casting");
            AddCompletion("Spells.Cast", CompletionType.Function, "Cast a spell by name or ID", "Spells.Cast(spell)", new List<string> { "Spells.Cast('Greater Heal')", "Spells.Cast(29)" });
            AddCompletion("Spells.CastTarget", CompletionType.Function, "Cast spell and target", "Spells.CastTarget(spell, serial)", new List<string> { "Spells.CastTarget('Greater Heal', Player.Serial)" });

            // Skills API
            AddCompletion("Skills", CompletionType.Type, "Skills API module - skill usage");
            AddCompletion("Skills.GetValue", CompletionType.Function, "Get skill value", "Skills.GetValue(skill)", new List<string> { "local val = Skills.GetValue('Magery')" });
            AddCompletion("Skills.Use", CompletionType.Function, "Use a skill", "Skills.Use(skill)", new List<string> { "Skills.Use('Hiding')" });

            // Messages API
            AddCompletion("Messages", CompletionType.Type, "Messages API module - chat and messages");
            AddCompletion("Messages.Print", CompletionType.Function, "Print system message", "Messages.Print(text [,hue])", new List<string> { "Messages.Print('Hello!')", "Messages.Print('Warning!', 37)" });
            AddCompletion("Messages.Info", CompletionType.Function, "Show info message", "Messages.Info(text)", new List<string> { "Messages.Info('Script started!')" });
            AddCompletion("Messages.Warning", CompletionType.Function, "Show warning message", "Messages.Warning(text)", new List<string> { "Messages.Warning('Low health!')" });
            AddCompletion("Messages.Error", CompletionType.Function, "Show error message", "Messages.Error(text)", new List<string> { "Messages.Error('Failed!')" });
            AddCompletion("Messages.Overhead", CompletionType.Function, "Show overhead message on player", "Messages.Overhead(text [,hue])", new List<string> { "Messages.Overhead('Hello!', 68)" });
            AddCompletion("Messages.OverheadMobile", CompletionType.Function, "Show overhead message on mobile", "Messages.OverheadMobile(serial, text [,hue])", new List<string> { "Messages.OverheadMobile(target, 'Tagged!')" });

            // Journal API
            AddCompletion("Journal", CompletionType.Type, "Journal API module - journal searching");
            AddCompletion("Journal.Contains", CompletionType.Function, "Check if journal contains text", "Journal.Contains(text)", new List<string> { "if Journal.Contains('You see') then ... end" });
            AddCompletion("Journal.Clear", CompletionType.Function, "Clear journal for searching", "Journal.Clear()", new List<string> { "Journal.Clear()" });
            AddCompletion("Journal.WaitFor", CompletionType.Function, "Wait for journal entry", "Journal.WaitFor(text, timeout)", new List<string> { "if Journal.WaitFor('You see', 5000) then ... end" });

            // Client API
            AddCompletion("Client", CompletionType.Type, "Client API module - client operations");
            AddCompletion("Client.Pause", CompletionType.Function, "Pause script execution", "Client.Pause(milliseconds)", new List<string> { "Client.Pause(1000)" });
            AddCompletion("Client.HeadMessage", CompletionType.Function, "Show overhead message on entity", "Client.HeadMessage(serial, text [,hue])", new List<string> { "Client.HeadMessage(Player.Serial, 'Hello!')" });

            // Console API - Debug output console
            AddCompletion("Console", CompletionType.Type, "Console API module - debug output console");
            AddCompletion("Console.log", CompletionType.Function, "Log info message to debug console", "Console.log(message)", new List<string> { "Console.log('Processing started')" });
            AddCompletion("Console.info", CompletionType.Function, "Log info message to debug console", "Console.info(message)", new List<string> { "Console.info('Status: OK')" });
            AddCompletion("Console.warn", CompletionType.Function, "Log warning message to debug console", "Console.warn(message)", new List<string> { "Console.warn('Low health!')" });
            AddCompletion("Console.error", CompletionType.Function, "Log error message to debug console", "Console.error(message)", new List<string> { "Console.error('Target not found')" });
            AddCompletion("Console.debug", CompletionType.Function, "Log debug message to debug console", "Console.debug(message)", new List<string> { "Console.debug('Variable x = ' .. tostring(x))" });
            AddCompletion("Console.clear", CompletionType.Function, "Clear the debug console", "Console.clear()", new List<string> { "Console.clear()" });
        }

        /// <summary>UOSagas-Razor-Zusatz (NICHT im Client-Korpus): die eigene
        /// Script-UI-API (Auto-Layout, Callbacks, Variablen-Bindings).</summary>
        private static void InitializeScriptUiAPI()
        {
            AddCompletion("UI", CompletionType.Type, "Script UI module - build your own windows (auto layout, callbacks, bindings)");
            AddCompletion("UI.Window", CompletionType.Function, "Create a script window (elements stack automatically; position/size optional)", "UI.Window(title [, x, y [, width, height]])  or  UI.Window{title=, x=, y=, width=, height=}", new List<string> { "local win = UI.Window('My Helper')", "local win = UI.Window('My Helper', 200, 150, 320, 240)" });
            AddCompletion("UI.Pump", CompletionType.Function, "Process UI callbacks/bindings once (Pause() does this automatically)", "UI.Pump()");
            AddCompletion("UI.DestroyAll", CompletionType.Function, "Destroy all script windows", "UI.DestroyAll()");

            AddCompletion("win:Label", CompletionType.Function, "Add a label; pass a function for a live binding", "win:Label(text)  or  win:Label(fn [, intervalMs])", new List<string> { "win:Label('Hello')", "win:Label(function() return 'HP: ' .. Player.Hits end)" });
            AddCompletion("win:Button", CompletionType.Function, "Add a button with optional click handler", "win:Button(text [, onClick])", new List<string> { "win:Button('Heal', function() Spells.Cast('Greater Heal') end)" });
            AddCompletion("win:Checkbox", CompletionType.Function, "Add a checkbox", "win:Checkbox(text [, checked] [, onChange])", new List<string> { "local auto = win:Checkbox('Auto-Heal', false)" });
            AddCompletion("win:TextBox", CompletionType.Function, "Add a text input", "win:TextBox([text] [, onChange])", new List<string> { "local name = win:TextBox('default')" });
            AddCompletion("win:Slider", CompletionType.Function, "Add a slider", "win:Slider(min, max [, initial] [, onChange])", new List<string> { "win:Slider(0, 100, 50, function(v) Messages.Print(v) end)" });
            AddCompletion("win:ProgressBar", CompletionType.Function, "Add a progress bar (0..1); pass a function for a live binding", "win:ProgressBar([valueOrFn] [, intervalMs])", new List<string> { "win:ProgressBar(function() return Player.Hits / Player.HitsMax end)" });
            AddCompletion("win:Separator", CompletionType.Function, "Add a horizontal separator line", "win:Separator()");
            AddCompletion("win:Row", CompletionType.Function, "Add a horizontal row (same element methods as the window)", "win:Row()", new List<string> { "local row = win:Row()", "row:Button('A') row:Button('B')" });
            AddCompletion("win:Run", CompletionType.Function, "Block until the window is closed; pumps callbacks/bindings", "win:Run([intervalMs])", new List<string> { "win:Run()" });
            AddCompletion("win:Show", CompletionType.Function, "Show the window", "win:Show()");
            AddCompletion("win:Hide", CompletionType.Function, "Hide the window (script can Show() it again)", "win:Hide()");
            AddCompletion("win:Close", CompletionType.Function, "Close the window (ends win:Run())", "win:Close()");
            AddCompletion("win:IsOpen", CompletionType.Function, "Is the window still open?", "win:IsOpen()");
            AddCompletion("win:OnClose", CompletionType.Function, "Callback when the user closes the window", "win:OnClose(fn)");
            AddCompletion("win:SetTitle", CompletionType.Function, "Change the window title", "win:SetTitle(text)");
            AddCompletion("win:SetSize", CompletionType.Function, "Set a fixed window size (default: auto-size)", "win:SetSize(width, height)");
            AddCompletion("win:SetPosition", CompletionType.Function, "Move the window", "win:SetPosition(x, y)");

            // Config-API (ebenfalls Razor-Zusatz): Script-Einstellungen als JSON.
            AddCompletion("Config", CompletionType.Type, "Config module - save/load script settings as JSON (Data/Profiles/Scripts/Config)");
            AddCompletion("Config.Save", CompletionType.Function, "Save a table (strings, numbers, booleans, nested tables/arrays)", "Config.Save(name, table)", new List<string> { "Config.Save('MyScript', { enabled = true, serials = {0x400123} })" });
            AddCompletion("Config.Load", CompletionType.Function, "Load a saved table (nil if it does not exist)", "Config.Load(name)", new List<string> { "local cfg = Config.Load('MyScript') or {}" });
            AddCompletion("Config.Exists", CompletionType.Function, "Does a saved config exist?", "Config.Exists(name)");
            AddCompletion("Config.Delete", CompletionType.Function, "Delete a saved config", "Config.Delete(name)");
        }

        private static void InitializeSnippets()
        {
            AddCompletion("ifblock", CompletionType.Snippet, "If/end block", "if CONDITION then\n    \nend", new List<string> { "if CONDITION then\n    \nend" });
            AddCompletion("ifelseblock", CompletionType.Snippet, "If/else/end block", "if CONDITION then\n    \nelse\n    \nend", new List<string> { "if CONDITION then\n    \nelse\n    \nend" });
            AddCompletion("forblock", CompletionType.Snippet, "For loop block", "for i = 1, COUNT do\n    \nend", new List<string> { "for i = 1, COUNT do\n    \nend" });
            AddCompletion("foreachblock", CompletionType.Snippet, "For each loop block", "for k, v in pairs(TABLE) do\n    \nend", new List<string> { "for k, v in pairs(TABLE) do\n    \nend" });
            AddCompletion("whileblock", CompletionType.Snippet, "While loop block", "while CONDITION do\n    \nend", new List<string> { "while CONDITION do\n    \nend" });
            AddCompletion("funcblock", CompletionType.Snippet, "Function definition", "function NAME()\n    \nend", new List<string> { "function NAME()\n    \nend" });
            AddCompletion("localfunc", CompletionType.Snippet, "Local function", "local function NAME()\n    \nend", new List<string> { "local function NAME()\n    \nend" });
            AddCompletion("healself", CompletionType.Snippet, "Self healing routine", "if Player.Hits < Player.HitsMax - 20 then\n    Spells.Cast('Greater Heal')\n    if Target.WaitForTarget(3000) then\n        Target.Self()\n    end\n    Client.Pause(1500)\nend", null);
        }

        // ---- Ende Client-Korpus -------------------------------------------------

        /// <summary>Adapter: baut aus dem Client-Aufruf einen CompletionEntry.
        /// Tooltip = Syntax + Beschreibung + Beispiele (der Client rendert die
        /// drei Teile getrennt; AvaloniaEdit hat nur einen Tooltip-Text).</summary>
        private static void AddCompletion(string text, CompletionType type, string description,
            string syntax = null, List<string> examples = null)
        {
            var tooltip = new StringBuilder();
            tooltip.Append(syntax ?? text);

            if (!string.IsNullOrEmpty(description))
                tooltip.Append('\n').Append(description);

            if (examples != null && examples.Count > 0)
            {
                tooltip.Append("\n\nExamples:");
                foreach (string example in examples)
                    tooltip.Append('\n').Append(example);
            }

            string category = type switch
            {
                CompletionType.Keyword => "keyword",
                CompletionType.Function => "function",
                CompletionType.Variable => "variable",
                CompletionType.Snippet => "snippet",
                _ => "type"
            };

            _entries.Add(new CompletionEntry(text, category, tooltip.ToString())
            {
                // Snippets fuegen ihren Syntax-Block ein, alle anderen ihren Namen.
                InsertText = type == CompletionType.Snippet ? syntax ?? text : null
            });
        }
    }
}
