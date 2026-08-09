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

// UOSagas-Razor: Hilfe-/Tooltip-Korpus fuer die Razor-Script-Sprache.
//
// Portiert aus Razor CE (ScriptManager.cs, #region CommandToolTips): pro Command
// Titel, Parameter(-Signaturen), Beschreibung und Beispiel. Speist die
// Autocomplete-Tooltips im IDE-Editor. Bewusst UI-unabhaengige Datenklasse,
// damit ein spaeterer Lua-Editor dasselbe Muster nutzen kann.

using System.Collections.Generic;
using System.Text;

namespace Razor.UI.Editor
{
    /// <summary>Ein Hilfe-Eintrag fuer ein Script-Kommando (Tooltip-Inhalt).</summary>
    public sealed class CommandHelp
    {
        public string Title;
        public string[] Parameters;
        public string Description;
        public string Example;

        public CommandHelp(string title, string[] parameters, string description, string example)
        {
            Title = title;
            Parameters = parameters;
            Description = description;
            Example = example;
        }

        /// <summary>Formatierter Tooltip-Text (Parameter / Beschreibung / Beispiel).</summary>
        public string ToTooltip()
        {
            var sb = new StringBuilder();
            sb.Append("Parameter(s):");
            foreach (var p in Parameters)
                sb.Append("\n  ").Append(p);
            sb.Append("\n\nDescription:\n  ").Append(Description);
            if (!string.IsNullOrEmpty(Example))
                sb.Append("\n\nExample:\n  ").Append(Example.Replace("\t", "  "));
            return sb.ToString();
        }
    }

    public static class ScriptCommandHelp
    {
        public static readonly Dictionary<string, CommandHelp> Commands =
            new Dictionary<string, CommandHelp>();

        static ScriptCommandHelp()
        {
            void Add(string name, string[] pars, string desc, string ex) =>
                Commands[name] = new CommandHelp(name, pars, desc, ex);

            Add("attack", new[] { "attack (serial) or attack ('variablename')" },
                "Attack a specific serial or variable tied to a serial.", "attack 0x2AB4\nattack 'attackdummy'");
            Add("clearall", new[] { "clearall" },
                "Clear target, clear queues, drop anything you're holding", "clearall");
            Add("clearhands", new[] { "clearhands ('right'/'left'/'hands')" },
                "Use the item in your hands", "clearhands");
            Add("virtue", new[] { "virtue ('honor'/'sacrifice'/'valor')" },
                "Invoke a specific virtue", "virtue 'honor'");
            Add("cast", new[] { "cast ('name of spell')" },
                "Cast a spell by name", "cast 'blade spirits'");
            Add("dclick", new[] { "dclick (serial) or useobject (serial)" },
                "This command will use (double-click) a specific item or mobile.", "dclick 0x34AB");
            Add("dclicktype", new[] { "dclicktype ('name of item') OR (graphicID) [inrange/backpack] [hue]" },
                "Use (double-click) an item type by name or graphic ID. Optional true = only items within 2 tiles.",
                "dclicktype 'dagger'\nwaitfortarget\ntargettype 'robe'");
            Add("dress", new[] { "dress ('name of dress list')" },
                "Execute a dress list you have defined in Razor.", "dress 'My Sunday Best'");
            Add("drop", new[] { "drop (serial) (x/y/z/layername)" },
                "Drop the item you are holding at your feet, on a layer, or at a specific X/Y/Z location.",
                "lift 0x400D54A7 1\ndrop 0x6311 InnerTorso");
            Add("droprelloc", new[] { "droprelloc (x) (y)" },
                "Drop the held item to a location relative to your position.",
                "lift 0x400EED2A 1\nwait 1000\ndroprelloc 1 1");
            Add("gumpresponse", new[] { "gumpresponse (buttonID)" },
                "Responds to a specific gump button", "gumpresponse 4");
            Add("gumpclose", new[] { "gumpclose" },
                "Close the last gump that opened.", "gumpclose");
            Add("hotkey", new[] { "hotkey ('name of hotkey')" },
                "Execute any Razor hotkey by name.",
                "skill 'detect hidden'\nwaitfortarget\nhotkey 'target self'");
            Add("lasttarget", new[] { "lasttarget" },
                "Target your last target set in Razor.",
                "cast 'magic arrow'\nwaitfortarget\nlasttarget");
            Add("lift", new[] { "lift (serial) [amount]" },
                "Lift a specific item and amount (default 1).",
                "lift 0x400EED2A 1\nwait 1000\ndroprelloc 1 1 0");
            Add("lifttype", new[] { "lifttype (gfx) [amount] or lifttype ('name of item') [amount] [hue]" },
                "Lift an item by type (graphic id or name). Default amount 1.",
                "lifttype 'robe'\nwait 1000\ndroprelloc 1 1 0");
            Add("menu", new[] { "menu (serial) (index) [false]" },
                "Selects a specific index within a context menu", "menu 0 1");
            Add("menuresponse", new[] { "menuresponse (index) (menuId) [hue]" },
                "Responds to a specific menu and menu ID (not a context menu)", "menuresponse 3 4");
            Add("organizer", new[] { "organizer (number) ['set']" },
                "Execute a specific organizer agent. 'set' prompts for the hotbag.",
                "organizer 1\norganizer 4 'set'");
            Add("overhead", new[] { "overhead ('text') [color] [serial]" },
                "Display a message over your head (only you can see it).",
                "if stam = 100\n  overhead 'ready to go!'\nendif");
            Add("potion", new[] { "potion ('potion type')" },
                "Use a specific potion based on the type.", "potion 'agility'\npotion 'heal'");
            Add("promptresponse", new[] { "promptresponse ('prompt response')" },
                "Respond to a prompt (e.g. renaming runes, guild title).",
                "dclicktype 'rune'\nwaitforprompt\npromptresponse 'to home'");
            Add("restock", new[] { "restock (number) ['set']" },
                "Execute a specific restock agent. 'set' prompts for the hotbag.",
                "restock 1\nrestock 4 'set'");
            Add("say", new[] { "say ('message') [hue] or msg ('message') [hue]" },
                "Force your character to say the message.", "say 'Hello world!'\nsay 'Hello world!' 454");
            Add("whisper", new[] { "whisper ('message') [hue]" },
                "Force your character to whisper the message.", "whisper 'Hello world!' 454");
            Add("yell", new[] { "yell ('message') [hue]" },
                "Force your character to yell the message.", "yell 'Hello world!' 454");
            Add("emote", new[] { "emote ('message') [hue]" },
                "Force your character to emote the message.", "emote 'Hello world!' 454");
            Add("script", new[] { "script 'name'" },
                "Call another script.", "if hp = 40\n  script 'healself'\nendif");
            Add("scavenger", new[] { "scavenger ['clear'/'add'/'on'/'off'/'set']" },
                "Control the scavenger agent.", "scavenger 'off'");
            Add("sell", new[] { "sell" },
                "Set the Sell agent's hotbag.", "sell");
            Add("setability", new[] { "setability ('primary'/'secondary'/'stun'/'disarm') ['on'/'off']" },
                "Set a specific ability on or off (default on).", "setability stun");
            Add("setlasttarget", new[] { "setlasttarget" },
                "Pause the script until you select a target to be set as Last Target.",
                "setlasttarget\ncast 'magic arrow'\nwaitfortarget\ntarget 'last'");
            Add("setvar", new[] { "setvar ('variable') or setvariable ('variable')" },
                "Pause the script until you select a target to assign to a variable (must exist first).",
                "setvar 'dummy'\ncast 'magic arrow'\nwaitfortarget\ntarget 'dummy'");
            Add("skill", new[] { "skill 'name of skill' or skill last" },
                "Use a specific (usable) skill.",
                "while mana < maxmana\n  skill 'meditation'\n  wait 11000\nendwhile");
            Add("sysmsg", new[] { "sysmsg ('message')" },
                "Display a message in the lower-left of the client.",
                "if stam = 100\n  sysmsg 'ready to go!'\nendif");
            Add("target", new[] { "target (serial) or target (x) (y) (z)" },
                "Target a specific mobile/item or a location by X/Y/Z.",
                "cast 'lightning'\nwaitfortarget\ntarget 0xBB3");
            Add("targettype", new[] { "targettype (graphic) or ('name') [inrangecheck/backpack] [hue]" },
                "Target a type of mobile/item by graphic id or name. Optional true = within 2 tiles.",
                "usetype 'dagger'\nwaitfortarget\ntargettype 'robe'");
            Add("targetrelloc", new[] { "targetrelloc (x-offset) (y-offset)" },
                "Target a map location relative to your position.",
                "cast 'fire field'\nwaitfortarget\ntargetrelloc 1 1");
            Add("undress", new[] { "undress ['name of dress list'] or undress 'LayerName'" },
                "Undress completely, or a dress list, or a specific layer.",
                "undress\nundress 'My Sunday Best'\nundress 'Shirt'");
            Add("useonce", new[] { "useonce ['add'/'addcontainer']" },
                "Execute the UseOnce agent; 'add'/'addcontainer' add items to the list.",
                "useonce\nuseonce 'add'");
            Add("walk", new[] { "walk ('direction')" },
                "Turn and/or walk your player in a direction.",
                "walk 'North'\nwalk 'West'\nwalk 'South'\nwalk 'East'");
            Add("wait", new[] { "wait [milliseconds]" },
                "Pause the execution of a script for a given time.",
                "while stam < 100\n  wait 5000\nendwhile");
            Add("pause", new[] { "pause [milliseconds]" },
                "Pause the execution of a script for a given time.",
                "while stam < 100\n  pause 5000\nendwhile");
            Add("waitforgump", new[] { "waitforgump [gump id]" },
                "Wait for a gump (any gump if no id).", "waitforgump\nwaitforgump 4");
            Add("waitformenu", new[] { "waitformenu [menu id]" },
                "Wait for a menu (not a context menu; any if no id).", "waitformenu\nwaitformenu 4");
            Add("waitforprompt", new[] { "waitforprompt" },
                "Wait for a prompt before continuing.",
                "dclicktype 'rune'\nwaitforprompt\npromptresponse 'to home'");
            Add("waitfortarget", new[] { "waitfortarget [ms] or wft [ms]" },
                "Pause until you have a target cursor (default 30s).",
                "cast 'energy bolt'\nwaitfortarget\nhotkey 'Target Closest Enemy'");
            Add("clearsysmsg", new[] { "clearsysmsg" },
                "Clear the internal system message queue used with insysmsg.", "clearsysmsg");
            Add("clearjournal", new[] { "clearjournal" },
                "Clear the internal system message queue used with insysmsg.", "clearjournal");
            Add("waitforsysmsg", new[] { "waitforsysmsg ('text')" },
                "Pause the script until the given message is in the system message queue.",
                "waitforsysmsg 'message here'");
            Add("random", new[] { "random [max number]" },
                "Output a random number between 1 and the max number provided.", "random 15");
        }
    }
}
