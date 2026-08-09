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

// Portiert aus Razor CE (Razor/Scripts/Commands.cs) — 1:1-Sprachoberflaeche
// (alle Registrierungen identisch). Dokumentierte Abweichungen:
//  * Client.Instance.* -> ClientProxy.* (in-process ABI statt CUO-Plugin-API).
//  * AllowBit(FeatureBit...) entfaellt (Port kennt keine FeatureBits).
//  * cooldown: Farben als String statt System.Drawing.Color; Overlay-UI TODO.
//  * cuo/classicuo: TODO(scripting-stub) — kein ClassicUOManager (AOT-Client,
//    spaeter ggf. ueber die ABI-CommandFn).
//  * skill: StealthSteps-Zaehler nicht portiert (nur der Hide-Overlay-Teil).
//  * interrupt (layer): Port-Spell.Interrupt kennt keine Layer-Auswahl ->
//    generisches Interrupt, TODO.
//  * setvar: MainWindow.SaveScriptVariables -> Config.Save() (Profil-Sektion
//    "scriptvariables").

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Assistant.Core;
using Assistant.HotKeys;
using Assistant.Scripts.Engine;
using Assistant.Scripts.Helpers;
using Ultima;

namespace Assistant.Scripts
{
    public static class Commands
    {
        public static void Register()
        {
            // Commands based on Actions.cs
            Interpreter.RegisterCommandHandler("attack", Attack); //Attack by serial
            Interpreter.RegisterCommandHandler("cast", Cast); //BookcastAction, etc

            // Dress
            Interpreter.RegisterCommandHandler("dress", DressCommand); //DressAction
            Interpreter.RegisterCommandHandler("undress", UnDressCommand); //UndressAction

            // Using stuff
            Interpreter.RegisterCommandHandler("dclicktype", DClickType); // DoubleClickTypeAction
            Interpreter.RegisterCommandHandler("dclick", DClick); //DoubleClickAction

            Interpreter.RegisterCommandHandler("usetype", DClickType); // DoubleClickTypeAction
            Interpreter.RegisterCommandHandler("useobject", DClick); //DoubleClickAction

            // Moving stuff
            Interpreter.RegisterCommandHandler("drop", DropItem); //DropAction
            Interpreter.RegisterCommandHandler("droprelloc", DropRelLoc); //DropAction
            Interpreter.RegisterCommandHandler("lift", LiftItem); //LiftAction
            Interpreter.RegisterCommandHandler("lifttype", LiftType); //LiftTypeAction

            // Gump
            Interpreter.RegisterCommandHandler("waitforgump", WaitForGump); // WaitForGumpAction
            Interpreter.RegisterCommandHandler("gumpresponse", GumpResponse); // GumpResponseAction
            Interpreter.RegisterCommandHandler("gumpclose", GumpClose); // GumpResponseAction

            // Menu
            Interpreter.RegisterCommandHandler("menu", ContextMenu); //ContextMenuAction
            Interpreter.RegisterCommandHandler("menuresponse", MenuResponse); //MenuResponseAction
            Interpreter.RegisterCommandHandler("waitformenu", WaitForMenu); //WaitForMenuAction

            // Prompt
            Interpreter.RegisterCommandHandler("promptresponse", PromptResponse); //PromptAction
            Interpreter.RegisterCommandHandler("waitforprompt", WaitForPrompt); //WaitForPromptAction

            // Hotkey execution
            Interpreter.RegisterCommandHandler("hotkey", Hotkey); //HotKeyAction

            Interpreter.RegisterCommandHandler("overhead", OverheadMessage); //OverheadMessageAction
            Interpreter.RegisterCommandHandler("headmsg", OverheadMessage); //OverheadMessageAction
            Interpreter.RegisterCommandHandler("sysmsg", SystemMessage); //SystemMessageAction
            Interpreter.RegisterCommandHandler("sysmessage", SystemMessage); // Outlands-Alias
            Interpreter.RegisterCommandHandler("clearsysmsg", ClearSysMsg); //SystemMessageAction
            Interpreter.RegisterCommandHandler("clearjournal", ClearSysMsg); //SystemMessageAction

            // General Waits/Pauses
            Interpreter.RegisterCommandHandler("wait", Pause); //PauseAction
            Interpreter.RegisterCommandHandler("pause", Pause); //PauseAction
            Interpreter.RegisterCommandHandler("waitforsysmsg", WaitForSysMsg);
            Interpreter.RegisterCommandHandler("wfsysmsg", WaitForSysMsg);

            // Misc
            Interpreter.RegisterCommandHandler("setability", SetAbility); //SetAbilityAction
            Interpreter.RegisterCommandHandler("setlasttarget", SetLastTarget); //SetLastTargetAction
            Interpreter.RegisterCommandHandler("lasttarget", LastTarget); //LastTargetAction
            Interpreter.RegisterCommandHandler("skill", UseSkillCommand); //SkillAction
            Interpreter.RegisterCommandHandler("useskill", UseSkillCommand); //SkillAction
            Interpreter.RegisterCommandHandler("walk", Walk); //Move/WalkAction
            Interpreter.RegisterCommandHandler("potion", Potion);

            // Script related
            Interpreter.RegisterCommandHandler("script", PlayScript);
            Interpreter.RegisterCommandHandler("setvar", SetVar);
            Interpreter.RegisterCommandHandler("setvariable", SetVar);
            Interpreter.RegisterCommandHandler("unsetvar", UnsetVar);
            Interpreter.RegisterCommandHandler("unsetvariable", UnsetVar);

            Interpreter.RegisterCommandHandler("stop", Stop);

            Interpreter.RegisterCommandHandler("clearall", ClearAll);

            Interpreter.RegisterCommandHandler("clearhands", ClearHands);

            Interpreter.RegisterCommandHandler("virtue", Virtue);

            Interpreter.RegisterCommandHandler("random", Random);

            Interpreter.RegisterCommandHandler("cleardragdrop", ClearDragDrop);
            Interpreter.RegisterCommandHandler("interrupt", Interrupt);

            Interpreter.RegisterCommandHandler("sound", Sound);
            Interpreter.RegisterCommandHandler("music", Music);

            Interpreter.RegisterCommandHandler("classicuo", ClassicUOProfile);
            Interpreter.RegisterCommandHandler("cuo", ClassicUOProfile);

            Interpreter.RegisterCommandHandler("rename", Rename);

            Interpreter.RegisterCommandHandler("getlabel", GetLabel);

            Interpreter.RegisterCommandHandler("ignore", AddIgnore);
            Interpreter.RegisterCommandHandler("unignore", RemoveIgnore);
            Interpreter.RegisterCommandHandler("clearignore", ClearIgnore);

            Interpreter.RegisterCommandHandler("cooldown", Cooldown);

            Interpreter.RegisterCommandHandler("poplist", PopList);
            Interpreter.RegisterCommandHandler("pushlist", PushList);
            Interpreter.RegisterCommandHandler("removelist", RemoveList);
            Interpreter.RegisterCommandHandler("createlist", CreateList);
            Interpreter.RegisterCommandHandler("clearlist", ClearList);

            Interpreter.RegisterCommandHandler("settimer", SetTimer);
            Interpreter.RegisterCommandHandler("removetimer", RemoveTimer);
            Interpreter.RegisterCommandHandler("createtimer", CreateTimer);

            // Outlands-Erweiterungen (wiki.uooutlands.com/Razor_Scripting)
            Interpreter.RegisterCommandHandler("warmode", WarModeCommand);
            Interpreter.RegisterCommandHandler("setskill", SetSkillCommand);
            Interpreter.RegisterCommandHandler("findtypelist", FindTypeList);
        }

        /// <summary>Outlands: warmode ('on'/'off') — Kriegsmodus setzen (0x72).</summary>
        private static bool WarModeCommand(string command, Variable[] vars, bool quiet, bool force)
        {
            if (vars.Length < 1)
                throw new RunTimeError("Usage: warmode ('on'/'off')");

            bool on = vars[0].AsString().Equals("on", StringComparison.OrdinalIgnoreCase);

            ClientProxy.SendToServer(new SetWarMode(on));

            return true;
        }

        /// <summary>Outlands: setskill SkillName (up/down/lock) — Skill-Lock aendern (0x3A).</summary>
        private static bool SetSkillCommand(string command, Variable[] vars, bool quiet, bool force)
        {
            if (vars.Length < 2)
                throw new RunTimeError("Usage: setskill ('skill name') ('up'/'down'/'lock')");

            if (!Skills.SkillsByName.TryGetValue(vars[0].AsString(), out SkillInfo skill))
            {
                CommandHelper.SendWarning(command, $"Skill '{vars[0].AsString()}' not found", quiet);
                return true;
            }

            LockType type;
            switch (vars[1].AsString().ToLowerInvariant())
            {
                case "up":
                    type = LockType.Up;
                    break;
                case "down":
                    type = LockType.Down;
                    break;
                case "lock":
                case "locked":
                    type = LockType.Locked;
                    break;
                default:
                    throw new RunTimeError("Usage: setskill ('skill name') ('up'/'down'/'lock')");
            }

            ClientProxy.SendToServer(new SetSkillLock(skill.Index, type));

            if (World.Player != null && skill.Index < World.Player.Skills.Length)
                World.Player.Skills[skill.Index].Lock = type;

            return true;
        }

        /// <summary>
        /// Outlands: findtypelist ('listname') ('name'/'graphic') [src] [hue]
        /// [qty] [range] — wie findtype, aber ALLE Treffer-Serials landen in
        /// der (existierenden) Liste.
        /// </summary>
        private static bool FindTypeList(string command, Variable[] vars, bool quiet, bool force)
        {
            if (vars.Length < 2)
                throw new RunTimeError("Usage: findtypelist ('list name') ('name of item'/'graphicID') [src] [hue] [qty] [range]");

            string listName = vars[0].AsString();

            if (!Interpreter.ListExists(listName))
            {
                CommandHelper.SendWarning(command, $"List '{listName}' does not exist", quiet);
                return true;
            }

            string gfxStr = vars[1].AsString();
            CommandHelper.FindArgs findArgs = CommandHelper.ParseFindArgs(vars, 2);

            foreach (Item item in CommandHelper.GetItems(gfxStr, findArgs))
                Interpreter.PushList(listName, new Variable("0x" + item.Serial.Value.ToString("X")), false, true);

            return true;
        }

        private static bool PopList(string command, Variable[] args, bool quiet, bool force)
        {
            if (args.Length != 2)
                throw new RunTimeError("Usage: poplist ('list name') ('element value'/'front'/'back')");

            if (args[1].AsString() == "front")
            {
                if (force)
                    while (Interpreter.PopList(args[0].AsString(), true, out _)) { }
                else
                    Interpreter.PopList(args[0].AsString(), true, out _);
            }
            else if (args[1].AsString() == "back")
            {
                if (force)
                    while (Interpreter.PopList(args[0].AsString(), false, out _)) { }
                else
                    Interpreter.PopList(args[0].AsString(), false, out _);
            }
            else
            {
                var evaluatedVar = new Variable(args[1].AsString());
                if (force)
                {
                    while (Interpreter.PopList(args[0].AsString(), evaluatedVar)) { }
                }
                else
                    Interpreter.PopList(args[0].AsString(), evaluatedVar);
            }

            return true;
        }

        private static bool PushList(string command, Variable[] args, bool quiet, bool force)
        {
            if (args.Length < 2 || args.Length > 3)
                throw new RunTimeError("Usage: pushlist ('list name') ('element value') ['front'/'back']");

            bool front = false;
            if (args.Length == 3)
            {
                if (args[2].AsString() == "front")
                    front = true;
            }

            Interpreter.PushList(args[0].AsString(), new Variable(args[1].AsString()), front, force);

            return true;
        }

        private static bool RemoveList(string command, Variable[] args, bool quiet, bool force)
        {
            if (args.Length != 1)
                throw new RunTimeError("Usage: removelist ('list name')");

            Interpreter.DestroyList(args[0].AsString());

            return true;
        }

        private static bool CreateList(string command, Variable[] args, bool quiet, bool force)
        {
            if (args.Length != 1)
                throw new RunTimeError("Usage: createlist ('list name')");

            Interpreter.CreateList(args[0].AsString());

            return true;
        }

        private static bool ClearList(string command, Variable[] args, bool quiet, bool force)
        {
            if (args.Length != 1)
                throw new RunTimeError("Usage: clearlist ('list name')");

            Interpreter.ClearList(args[0].AsString());

            return true;
        }

        private static bool SetTimer(string command, Variable[] args, bool quiet, bool force)
        {
            if (args.Length != 2)
                throw new RunTimeError("Usage: settimer (timer name) (value)");


            Interpreter.SetTimer(args[0].AsString(), args[1].AsInt());
            return true;
        }

        private static bool RemoveTimer(string command, Variable[] args, bool quiet, bool force)
        {
            if (args.Length != 1)
                throw new RunTimeError("Usage: removetimer (timer name)");

            Interpreter.RemoveTimer(args[0].AsString());
            return true;
        }

        private static bool CreateTimer(string command, Variable[] args, bool quiet, bool force)
        {
            if (args.Length != 1)
                throw new RunTimeError("Usage: createtimer (timer name)");

            Interpreter.CreateTimer(args[0].AsString());
            return true;
        }

        private static bool Cooldown(string command, Variable[] vars, bool quiet, bool force)
        {
            if (vars.Length < 2)
            {
                throw new RunTimeError("Usage: cooldown ('name') ('seconds') ['hue'] ['icon'] ['sound'] ['stay visible'] ['foreground color'] ['background color']");
            }

            string name = vars[0].AsString();

            // CE nimmt Sekunden, Outlands Millisekunden (cooldown 'x' 30000).
            // Heuristik: Werte >= 100 sind Millisekunden.
            int rawDuration = vars[1].AsInt();
            int seconds = rawDuration >= 100 ? Math.Max(1, rawDuration / 1000) : rawDuration;

            int hue = 0, sound = 0;
            string icon = "none";
            bool stay = false;

            // Abweichung: Farben als Namens-String (kein System.Drawing im Port).
            string foreColor = string.Empty;
            string backColor = string.Empty;

            switch (vars.Length)
            {
                case 3:
                    hue = vars[2].AsInt();

                    break;
                case 4:
                    hue = vars[2].AsInt();
                    icon = vars[3].AsString();

                    break;
                case 5:
                    hue = vars[2].AsInt();
                    icon = vars[3].AsString();
                    sound = vars[4].AsInt();

                    break;
                case 6:
                    hue = vars[2].AsInt();
                    icon = vars[3].AsString();
                    sound = vars[4].AsInt();
                    stay = vars[5].AsBool();

                    break;
                case 7:
                    hue = vars[2].AsInt();
                    icon = vars[3].AsString();
                    sound = vars[4].AsInt();
                    stay = vars[5].AsBool();

                    foreColor = vars[6].AsString();

                    break;
                case 8:
                    hue = vars[2].AsInt();
                    icon = vars[3].AsString();
                    sound = vars[4].AsInt();
                    stay = vars[5].AsBool();

                    foreColor = vars[6].AsString();
                    backColor = vars[7].AsString();

                    break;
            }

            CooldownManager.AddCooldown(new Cooldown
            {
                Name = name,
                EndTime = DateTime.UtcNow.AddSeconds(seconds),
                Hue = hue,
                Icon = icon.Equals("0") ? (ushort) 0 : BuffDebuffManager.GetGraphicId(icon),
                Seconds = seconds,
                SoundId = sound,
                StayVisible = stay,
                ForegroundColor = foreColor,
                BackgroundColor = backColor
            });

            return true;
        }

        private enum GetLabelState
        {
            None,
            WaitingForFirstLabel,
            WaitingForRemainingLabels
        };

        private static GetLabelState _getLabelState = GetLabelState.None;
        private static MessageManager.LabelMessageHandler _onLabelMessage;
        private static Action _onStop;

        private static bool GetLabel(string command, Variable[] args, bool quiet, bool force)
        {
            if (args.Length != 2)
                throw new RunTimeError("Usage: getlabel (serial) (name)");

            var serial = args[0].AsSerial();
            var name = args[1].AsString(false);

            var mobile = World.FindMobile(serial);
            if (mobile != null)
            {
                if (mobile.IsHuman)
                {
                    return false;
                }
            }

            switch (_getLabelState)
            {
                case GetLabelState.None:
                    _getLabelState = GetLabelState.WaitingForFirstLabel;
                    Interpreter.Timeout(2000, () =>
                    {
                        MessageManager.OnLabelMessage -= _onLabelMessage;
                        _onLabelMessage = null;
                        Interpreter.OnStop -= _onStop;
                        _getLabelState = GetLabelState.None;
                        MessageManager.GetLabelCommand = false;
                        return true;
                    });

                    // Single click the object
                    ClientProxy.SendToServer(new SingleClick((Serial) args[0].AsSerial()));

                    // Capture all message responses
                    StringBuilder label = new StringBuilder();

                    // Some messages from Outlands server are send in sequence of LabelType and RegularType
                    // so we want to invoke that _onLabelMessage in both cases with delays
                    MessageManager.GetLabelCommand = true;

                    // Reset the state when script is stopped
                    _onStop = () =>
                    {
                        if (_onLabelMessage != null)
                        {
                            MessageManager.OnLabelMessage -= _onLabelMessage;
                            _onLabelMessage = null;
                        }
                        _getLabelState = GetLabelState.None;

                        Interpreter.OnStop -= _onStop;
                        MessageManager.GetLabelCommand = false;
                    };

                    _onLabelMessage = (p, a, source, graphic, type, hue, font, lang, sourceName, text) =>
                    {
                        if (source != serial)
                            return;

                        a.Block = true;

                        if (_getLabelState == GetLabelState.WaitingForFirstLabel)
                        {
                            // After the first message, switch to a pause instead of a timeout.
                            _getLabelState = GetLabelState.WaitingForRemainingLabels;
                            Interpreter.Pause(500);
                        }

                        label.Append(" " + text);

                        Interpreter.SetVariable(name, label.ToString().Trim());
                    };

                    Interpreter.OnStop += _onStop;
                    MessageManager.OnLabelMessage += _onLabelMessage;

                    break;
                case GetLabelState.WaitingForFirstLabel:
                    break;
                case GetLabelState.WaitingForRemainingLabels:
                    // We get here after the pause has expired.
                    Interpreter.OnStop -= _onStop;
                    MessageManager.OnLabelMessage -= _onLabelMessage;

                    _onLabelMessage = null;
                    _getLabelState = GetLabelState.None;

                    MessageManager.GetLabelCommand = false;

                    return true;
            }

            return false;
        }

        private static bool Rename(string command, Variable[] vars, bool quiet, bool force)
        {
            if (vars.Length < 2)
            {
                throw new RunTimeError("Usage: rename (serial) (new_name)");
            }

            string newName = vars[1].AsString();

            if (newName.Length < 1)
            {
                throw new RunTimeError("Mobile name must be longer than one character");
            }

            if (World.Mobiles.TryGetValue(vars[0].AsSerial(), out var follower))
            {
                if (follower.CanRename)
                {
                    PlayerData.RenameMobile(follower.Serial, newName);
                }
                else
                {
                    CommandHelper.SendMessage("Unable to rename mobile", quiet);
                }
            }

            return true;
        }

        private static bool ClassicUOProfile(string commands, Variable[] vars, bool quiet, bool force)
        {
            if (vars.Length != 2)
            {
                throw new RunTimeError("Usage: cuo (setting) (value)");
            }

            // TODO(scripting-stub): Razor CE setzt hier ClassicUO-Profil-
            // Properties per Reflection (ClassicUOManager). Der UOSagas-Client
            // ist NativeAOT — kein Reflection-Zugriff; spaeter ggf. ueber die
            // ABI-CommandFn nachruesten.
            CommandHelper.SendWarning(commands, "cuo/classicuo is not supported (yet) on UOSagas", quiet);

            return true;
        }

        private static bool Sound(string commands, Variable[] vars, bool quiet, bool force)
        {
            if (vars.Length != 1)
            {
                throw new RunTimeError("Usage: sound (serial)");
            }

            ClientProxy.SendToClient(new PlaySound(vars[0].AsInt()));

            return true;
        }

        private static bool Music(string commands, Variable[] vars, bool quiet, bool force)
        {
            if (vars.Length != 1)
            {
                throw new RunTimeError("Usage: music (id)");
            }

            ClientProxy.SendToClient(new PlayMusic(vars[0].AsUShort()));

            return true;
        }

        private static bool AddIgnore(string commands, Variable[] vars, bool quiet, bool force)
        {
            if (vars.Length != 1)
                throw new RunTimeError("Usage: ignore (serial)");

            Variable toIgnore = vars[0];
            string ignoreListName = vars[0].AsString();

            if (Interpreter.ListExists(ignoreListName))
            {
                List<Serial> list = Interpreter.GetList(ignoreListName).Select(v => (Serial) v.AsSerial()).ToList();
                Interpreter.AddIgnoreRange(list);
                CommandHelper.SendMessage($"Added {list.Count} entries to ignore list", quiet);
            }
            else
            {
                uint serial = toIgnore.AsSerial();
                Interpreter.AddIgnore(serial);
                CommandHelper.SendMessage($"Added {serial} to ignore list", quiet);
            }

            return true;
        }

        private static bool RemoveIgnore(string commands, Variable[] vars, bool quiet, bool force)
        {
            if (vars.Length != 1)
                throw new RunTimeError("Usage: unignore (serial or list)");

            Variable toIgnore = vars[0];
            string ignoreListName = toIgnore.AsString();

            if (Interpreter.ListExists(ignoreListName))
            {
                List<Serial> list = Interpreter.GetList(ignoreListName).Select(v => (Serial) v.AsSerial()).ToList();
                Interpreter.RemoveIgnoreRange(list);
                CommandHelper.SendMessage($"Removed {list.Count} entries from ignore list", quiet);
            }
            else
            {
                uint serial = toIgnore.AsSerial();
                Interpreter.RemoveIgnore(serial);
                CommandHelper.SendMessage($"Removed {serial} from ignore list", quiet);
            }

            return true;
        }

        private static bool ClearIgnore(string commands, Variable[] vars, bool quiet, bool force)
        {
            Interpreter.ClearIgnore();

            CommandHelper.SendMessage("Ignore List cleared", quiet);

            return true;
        }

        private static readonly string[] Virtues = { "honor", "sacrifice", "valor" };

        private static bool Virtue(string command, Variable[] vars, bool quiet, bool force)
        {
            if (vars.Length == 0 || !Virtues.Contains(vars[0].AsString()))
            {
                throw new RunTimeError("Usage: virtue ('honor'/'sacrifice'/'valor')");
            }

            switch (vars[0].AsString())
            {
                case "honor":
                    PlayerData.InvokeVirtue(PlayerData.InvokeVirtues.Honor);
                    break;
                case "sacrifice":
                    PlayerData.InvokeVirtue(PlayerData.InvokeVirtues.Sacrifice);
                    break;
                case "valor":
                    PlayerData.InvokeVirtue(PlayerData.InvokeVirtues.Valor);
                    break;
            }

            return true;
        }

        private static bool ClearAll(string command, Variable[] vars, bool quiet, bool force)
        {

            DragDropManager.GracefulStop(); // clear drag/drop queue
            Targeting.CancelTarget(); // clear target queue & cancel current target
            DragDropManager.DropCurrent(); // drop what you are currently holding

            return true;
        }

        private static bool SetLastTarget(string command, Variable[] vars, bool quiet, bool force)
        {
            if (vars.Length == 0)
            {
                throw new RunTimeError("Usage: setlasttarget ('serial')");
            }

            Serial serial = vars[0].AsSerial();

            if (serial != Serial.Zero)
            {
                Mobile mobile = World.FindMobile(serial);

                if (mobile != null)
                {
                    Targeting.SetLastTargetTo(mobile);
                    return true;
                }

                Item item = World.FindItem(serial);

                if (item != null)
                {
                    Targeting.SetLastTarget(item.Serial);
                    return true;
                }

                Targeting.SetLastTarget(serial);
            }

            return true;
        }

        private enum SetVarState
        {
            INITIAL_PROMPT,
            WAIT_FOR_TARGET,
            COMPLETE,
        };

        private static SetVarState _setVarState = SetVarState.INITIAL_PROMPT;

        private static bool SetVar(string command, Variable[] vars, bool quiet, bool force)
        {
            if (vars.Length < 1 || vars.Length > 2)
            {
                throw new RunTimeError("Usage: setvar ('variable') [serial] [timeout]");
            }

            string name = vars[0].AsString(false);

            if (vars.Length == 2)
            {
                // No need to target anything. We have the serial.
                var serial = vars[1].AsSerial();

                if (force)
                {
                    Interpreter.SetVariable(name, serial.ToString(), true);
                    return true;
                }

                if (ScriptVariables.GetVariable(name) == Serial.MinusOne && !quiet)
                {
                    CommandHelper.SendMessage($"'{name}' not found, creating new variable", quiet);
                }

                ScriptVariables.RegisterVariable(name, serial);
                CommandHelper.SendMessage($"'{name}' script variable updated to '{serial}'", quiet);

                Config.Save(); // CE: MainWindow.SaveScriptVariables()

                return true;
            }

            Interpreter.Timeout(vars.Length == 2 ? vars[1].AsUInt() : 30000, () => { _setVarState = SetVarState.INITIAL_PROMPT; return true; } );

            switch (_setVarState)
            {
                case SetVarState.INITIAL_PROMPT:
                    if (ScriptVariables.GetVariable(name) == Serial.MinusOne)
                    {
                        CommandHelper.SendMessage($"'{name}' not found, creating new variable", quiet);
                    }
                    World.Player.SendMessage(MsgLevel.Force, $"Select target for variable '{name}'");

                    _setVarState = SetVarState.WAIT_FOR_TARGET;

                    Targeting.OneTimeTarget((ground, serial, pt, gfx) =>
                    {
                        ScriptVariables.RegisterVariable(name, serial);
                        CommandHelper.SendMessage($"'{name}' script variable updated to '{serial}'", quiet);

                        Config.Save(); // CE: MainWindow.SaveScriptVariables()
                        _setVarState = SetVarState.COMPLETE;
                    },
                    () =>
                    {
                        _setVarState = SetVarState.COMPLETE;
                    });
                    break;
                case SetVarState.WAIT_FOR_TARGET:
                    break;
                case SetVarState.COMPLETE:
                    _setVarState = SetVarState.INITIAL_PROMPT;
                    return true;
            }

            return false;
        }

        private static bool UnsetVar(string expression, Variable[] args, bool quiet, bool force)
        {
            if (args.Length != 1)
                throw new RunTimeError("Usage: unsetvar ('name')");

            var name = args[0].AsString(false);

            if (force)
            {
                if (quiet)
                {
                    Interpreter.ClearVariable(name);
                }
                else
                {
                    Interpreter.ClearAlias(name);
                }
            }
            else
            {
                ScriptVariables.UnregisterVariable(name);
                ScriptManager.RedrawScripts();
            }

            return true;
        }


        private static bool Stop(string command, Variable[] vars, bool quiet, bool force)
        {
            ScriptManager.StopScript();

            return true;
        }

        private static bool Hotkey(string command, Variable[] vars, bool quiet, bool force)
        {
            if (vars.Length < 1)
            {
                throw new RunTimeError("Usage: hotkey ('name of hotkey') OR (hotkeyId)");
            }

            string query = vars[0].AsString();

            KeyData hk = HotKey.GetByNameOrId(query);

            if (hk == null)
            {
                throw new RunTimeError($"{command} - Hotkey '{query}' not found");
            }

            hk.Callback();

            return true;
        }

        private static bool WaitForGump(string command, Variable[] vars, bool quiet, bool force)
        {
            if (vars.Length < 1)
            {
                throw new RunTimeError("Usage: waitforgump (gumpId/'any') [timeout]");
            }

            uint gumpId = 0;
            bool strict = false;

            if (vars[0].AsString().IndexOf("any", StringComparison.OrdinalIgnoreCase) != -1)
            {
                strict = false;
            }
            else
            {
                gumpId = Utility.ToUInt32(vars[0].AsString(), 0);

                if (gumpId > 0)
                {
                    strict = true;
                }
            }

            Interpreter.Timeout(vars.Length == 2 ? vars[1].AsUInt() : 30000, () => { return true; });

            if ((World.Player.HasGump || World.Player.HasCompressedGump) &&
                (World.Player.CurrentGumpI == gumpId || !strict || gumpId == 0))
            {
                Interpreter.ClearTimeout();
                return true;
            }

            return false;
        }

        private static bool WaitForMenu(string command, Variable[] vars, bool quiet, bool force)
        {
            if (vars.Length < 1)
            {
                throw new RunTimeError("Usage: waitformenu (menuId/'any') [timeout]");
            }

            uint menuId = 0;

            // Look for a specific menu
            menuId = vars[0].AsString().IndexOf("any", StringComparison.OrdinalIgnoreCase) != -1
                ? 0
                : Utility.ToUInt32(vars[0].AsString(), 0);

            Interpreter.Timeout(vars.Length == 2 ? vars[1].AsUInt() : 30000, () => { return true; });

            if (World.Player.HasMenu && (World.Player.CurrentGumpI == menuId || menuId == 0))
            {
                Interpreter.ClearTimeout();
                return true;
            }

            return false;
        }

        private static bool WaitForPrompt(string command, Variable[] vars, bool quiet, bool force)
        {
            if (vars.Length < 1)
            {
                throw new RunTimeError("Usage: waitforprompt (promptId/'any') [timeout]");
            }

            uint promptId = 0;
            bool strict = false;

            // Look for a specific prompt
            if (vars[0].AsString().IndexOf("any", StringComparison.OrdinalIgnoreCase) != -1)
            {
                strict = false;
            }
            else
            {
                promptId = Utility.ToUInt32(vars[0].AsString(), 0);

                if (promptId > 0)
                {
                    strict = true;
                }
            }

            Interpreter.Timeout(vars.Length == 2 ? vars[1].AsUInt() : 30000, () => { return true; });

            if (World.Player.HasPrompt && (World.Player.PromptID == promptId || !strict || promptId == 0))
            {
                Interpreter.ClearTimeout();
                return true;
            }

            return false;
        }

        private static readonly string[] Abilities = {"primary", "secondary", "stun", "disarm"};

        private static bool SetAbility(string command, Variable[] vars, bool quiet, bool force)
        {
            if (vars.Length < 1 || !Abilities.Contains(vars[0].AsString()))
            {
                throw new RunTimeError("Usage: setability ('primary'/'secondary'/'stun'/'disarm') ['on'/'off']");
            }

            if (vars.Length == 2 && vars[1].AsString() == "on" || vars.Length == 1)
            {
                switch (vars[0].AsString())
                {
                    case "primary":
                        SpecialMoves.SetPrimaryAbility();
                        break;
                    case "secondary":
                        SpecialMoves.SetSecondaryAbility();
                        break;
                    case "stun":
                        ClientProxy.SendToServer(new StunRequest());
                        break;
                    case "disarm":
                        ClientProxy.SendToServer(new DisarmRequest());
                        break;
                }
            }
            else if (vars.Length == 2 && vars[1].AsString() == "off")
            {
                ClientProxy.SendToServer(new UseAbility(AOSAbility.Clear));
                ClientProxy.SendToClient(ClearAbility.Instance);
            }

            return true;
        }

        private static readonly string[] Hands = {"left", "right", "both", "hands"};

        private static bool ClearHands(string command, Variable[] vars, bool quiet, bool force)
        {
            if (vars.Length == 0 || !Hands.Contains(vars[0].AsString()))
            {
                throw new RunTimeError("Usage: clearhands ('left'/'right'/'both')");
            }

            switch (vars[0].AsString())
            {
                case "left":
                    Dress.Unequip(Layer.LeftHand);
                    break;
                case "right":
                    Dress.Unequip(Layer.RightHand);
                    break;
                default:
                    Dress.Unequip(Layer.LeftHand);
                    Dress.Unequip(Layer.RightHand);
                    break;
            }

            return true;
        }

        private static bool DClickType(string command, Variable[] vars, bool quiet, bool force)
        {
            if (vars.Length == 0)
            {
                throw new RunTimeError("Usage: dclicktype ('name of item'/'graphicID') [inrangecheck (true/false)/backpack] [hue]");
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
                PlayerData.DoubleClick(items[Utility.Random(items.Count)].Serial);
            }
            else if (mobiles.Count > 0)
            {
                PlayerData.DoubleClick(mobiles[Utility.Random(mobiles.Count)].Serial);
            }
            else
            {
                CommandHelper.SendWarning(command, $"Item or mobile type '{gfxStr}' not found", quiet);
            }

            return true;
        }

        private static bool DClick(string command, Variable[] vars, bool quiet, bool force)
        {
            if (vars.Length == 0)
            {
                throw new RunTimeError("Usage: dclick (serial) or dclick ('left'/'right'/'hands')");
            }

            if (Hands.Contains(vars[0].AsString()))
            {
                Item item;

                switch (vars[0].AsString())
                {
                    case "left":
                        item = World.Player.GetItemOnLayer(Layer.LeftHand);
                        break;
                    case "right":
                        item = World.Player.GetItemOnLayer(Layer.RightHand);
                        break;
                    default:
                        item = World.Player.GetItemOnLayer(Layer.RightHand) ?? World.Player.GetItemOnLayer(Layer.LeftHand);
                        break;
                }

                if (item != null)
                {
                    PlayerData.DoubleClick(item);
                }
                else
                {
                    CommandHelper.SendWarning(command, $"Item not found in '{vars[0].AsString()}'", quiet);
                }
            }
            else
            {
                Serial serial = vars[0].AsSerial();

                if (!serial.IsValid)
                {
                    throw new RunTimeError("dclick - invalid serial");
                }

                PlayerData.DoubleClick(serial);
            }

            return true;
        }

        private static bool DropItem(string command, Variable[] vars, bool quiet, bool force)
        {
            if (vars.Length < 1)
            {
                throw new RunTimeError("Usage: drop (serial) (x y z/layername)");
            }

            Serial serial = vars[0].AsString().IndexOf("ground", StringComparison.OrdinalIgnoreCase) > 0
                ? uint.MaxValue
                : vars[0].AsSerial();

            Point3D to = new Point3D(0, 0, 0);
            Layer layer = Layer.Invalid;

            switch (vars.Length)
            {
                case 1: // drop at feet if only serial is provided
                    to = new Point3D(World.Player.Position.X, World.Player.Position.Y, World.Player.Position.Z);
                    break;
                case 2: // dropping on a layer
                    layer = CommandHelper.ParseLayer(vars[1].AsString());
                    break;
                case 3: // x y
                    to = new Point3D(Utility.ToInt32(vars[1].AsString(), 0), Utility.ToInt32(vars[2].AsString(), 0), 0);
                    break;
                case 4: // x y z
                    to = new Point3D(Utility.ToInt32(vars[1].AsString(), 0), Utility.ToInt32(vars[2].AsString(), 0),
                        Utility.ToInt32(vars[3].AsString(), 0));
                    break;
            }

            if (DragDropManager.Holding != null)
            {
                if (layer > Layer.Invalid && layer <= Layer.LastUserValid)
                {
                    Mobile m = World.FindMobile(serial);
                    if (m != null)
                        DragDropManager.Drop(DragDropManager.Holding, m, layer);
                }
                else
                {
                    DragDropManager.Drop(DragDropManager.Holding, serial, to);
                }
            }
            else
            {
                CommandHelper.SendWarning(command, "Not holding anything", quiet);
            }

            return true;
        }

        private static bool DropRelLoc(string command, Variable[] vars, bool quiet, bool force)
        {
            if (vars.Length < 2)
            {
                throw new RunTimeError("Usage: droprelloc (x) (y)");
            }

            int x = vars[0].AsInt();
            int y = vars[1].AsInt();

            if (DragDropManager.Holding != null)
            {
                DragDropManager.Drop(DragDropManager.Holding, null,
                    new Point3D((ushort) (World.Player.Position.X + x),
                        (ushort) (World.Player.Position.Y + y), World.Player.Position.Z));
            }
            else
            {
                CommandHelper.SendWarning(command, "Not holding anything", quiet);
            }

            return true;
        }

        private static int _lastLiftId;

        private static bool LiftItem(string command, Variable[] vars, bool quiet, bool force)
        {
            if (vars.Length < 1)
            {
                throw new RunTimeError("Usage: lift (serial) [amount] [timeout]");
            }

            Serial serial = vars[0].AsSerial();

            if (!serial.IsValid)
            {
                throw new RunTimeError($"{command} - Invalid serial");
            }

            ushort amount = 1;

            if (vars.Length == 2)
            {
                amount = Utility.ToUInt16(vars[1].AsString(), 1);
            }

            long timeout = 30000;

            if (vars.Length == 3)
            {
                timeout = Utility.ToLong(vars[2].AsString(), 30000);
            }

            if (_lastLiftId > 0)
            {
                if (DragDropManager.LastIDLifted == _lastLiftId)
                {
                    _lastLiftId = 0;
                    Interpreter.ClearTimeout();
                    return true;
                }

                Interpreter.Timeout(timeout, () =>
                {
                    _lastLiftId = 0;
                    return true;
                });
            }
            else
            {
                Item item = World.FindItem(serial);

                if (item != null)
                {
                    _lastLiftId = DragDropManager.Drag(item, amount <= item.Amount ? amount : item.Amount);
                }
                else
                {
                    CommandHelper.SendWarning(command, "Item not found or out of range", quiet);
                    return true;
                }
            }

            return false;
        }

        private static int _lastLiftTypeId;

        private static bool LiftType(string command, Variable[] vars, bool quiet, bool force)
        {
            if (vars.Length < 1)
            {
                throw new RunTimeError("Usage: lifttype (gfx/'name of item') [amount] [hue]");
            }

            string gfxStr = vars[0].AsString();
            ushort gfx = Utility.ToUInt16(gfxStr, 0);
            ushort amount = 1;
            int hue = -1;

            if (vars.Length > 1)
            {
                if (vars.Length >= 2)
                {
                    amount = Utility.ToUInt16(vars[1].AsString(), 1);
                }

                if (vars.Length == 3)
                {
                    hue = Utility.ToUInt16(vars[2].AsString(), 0);
                }
            }

            if (_lastLiftTypeId > 0)
            {
                if (DragDropManager.LastIDLifted == _lastLiftTypeId)
                {
                    _lastLiftTypeId = 0;
                    Interpreter.ClearTimeout();
                    return true;
                }

                Interpreter.Timeout(30000, () =>
                {
                    _lastLiftTypeId = 0;
                    return true;
                });
            }
            else
            {
                // Outlands: lifttype (name/gfx) [amount] [src] [hue] — src/hue
                // ueber den toleranten Parser (Default: Backpack wie CE).
                CommandHelper.FindArgs findArgs = CommandHelper.ParseFindArgs(vars, 2);

                if (!findArgs.Self && !findArgs.Ground && findArgs.Container == Serial.Zero && !findArgs.InRange)
                    findArgs.Backpack = true;

                if (findArgs.Hue < 0 && hue > -1)
                    findArgs.Hue = hue; // CE-Altform: hue als 3. Arg ohne src

                List<Item> items = CommandHelper.GetItems(gfxStr, findArgs);

                if (items.Count > 0)
                {
                    Item item = items[Utility.Random(items.Count)];

                    if (item.Amount < amount)
                        amount = item.Amount;

                    _lastLiftTypeId = DragDropManager.Drag(item, amount);
                }
                else
                {
                    if (gfx == 0)
                        CommandHelper.SendWarning(command, $"Item '{gfxStr}' not found", quiet);
                    else
                        CommandHelper.SendWarning(command, Language.Format(LocString.NoItemOfType, (ItemID) gfx), quiet);

                    return true;
                }
            }

            return false;
        }

        private static bool Walk(string command, Variable[] vars, bool quiet, bool force)
        {
            if (vars.Length < 1)
            {
                throw new RunTimeError("Usage: walk ('direction')");
            }

            if (ScriptManager.LastWalk + TimeSpan.FromSeconds(0.4) >= DateTime.UtcNow)
            {
                return false;
            }

            ScriptManager.LastWalk = DateTime.UtcNow;

            Direction dir = (Direction) Enum.Parse(typeof(Direction), vars[0].AsString(), true);
            ClientProxy.RequestMove(dir);

            return true;
        }

        private static bool UseSkillCommand(string command, Variable[] vars, bool quiet, bool force)
        {
            if (vars.Length == 0)
            {
                throw new RunTimeError("Usage: skill ('skill name'/'last')");
            }

            if (vars[0].AsString() == "last")
            {
                ClientProxy.SendToServer(new UseSkill(World.Player.LastSkill));
            }
            else if (Skills.SkillsByName.TryGetValue(vars[0].AsString(), out SkillInfo skill))
            {
                if (skill.IsAction)
                {
                    ClientProxy.SendToServer(new UseSkill(skill.Index));

                    World.Player.LastSkill = skill.Index;
                }
                else
                {
                    CommandHelper.SendWarning(command, $"Skill '{vars[0].AsString()}' is not usable. Available usable skills: {string.Join(", ", Skills.GetUsableSkillNames())}", quiet);
                }
            }
            else
            {
                CommandHelper.SendWarning(command, $"Skill '{vars[0].AsString()}' not found. Available usable skills: {string.Join(", ", Skills.GetUsableSkillNames())}", quiet);
            }

            // Razor CE: StealthSteps.Hide() bei Stealth — der Schritt-Zaehler
            // (Overlay) ist im Port nicht vorhanden.

            return true;
        }

        private static bool Pause(string command, Variable[] vars, bool quiet, bool force)
        {
            if (vars.Length == 0)
                throw new RunTimeError("Usage: wait (timeout) [shorthand]");

            uint timeout = vars[0].AsUInt();

            if (vars.Length == 2)
            {
                switch (vars[1].AsString())
                {
                    case "s":
                    case "sec":
                    case "secs":
                    case "second":
                    case "seconds":
                        timeout *= 1000;
                        break;
                    case "m":
                    case "min":
                    case "mins":
                    case "minute":
                    case "minutes":
                        timeout *= 60000;
                        break;
                }
            }

            Interpreter.Pause(timeout);

            return true;
        }

        private static bool Attack(string command, Variable[] vars, bool quiet, bool force)
        {
            if (vars.Length == 0)
            {
                throw new RunTimeError("Usage: attack (serial)");
            }

            Serial serial = vars[0].AsSerial();

            if (!serial.IsValid)
            {
                throw new RunTimeError($"{command} - Invalid serial");
            }

            if (Targeting.LastTargetInfo != null && serial == Targeting.LastTargetInfo.Serial)
            {
                Targeting.AttackLastTarg();
            }
            else
            {
                if (serial.IsMobile)
                    ClientProxy.SendToServer(new AttackReq(serial));
            }

            return true;
        }

        private static bool Cast(string command, Variable[] vars, bool quiet, bool force)
        {
            if (vars.Length < 1)
            {
                throw new RunTimeError("Usage: cast 'name of spell'");
            }

            Spell spell = int.TryParse(vars[0].AsString(), out int spellnum)
                ? Spell.Get(spellnum)
                : Spell.GetByName(vars[0].AsString());

            if (spell != null)
            {
                spell.OnCast(new CastSpellFromMacro((ushort) spell.GetID()));
            }
            else
            {
                throw new RunTimeError($"{command} - Spell name or number not valid");
            }

            return true;
        }

        private static bool OverheadMessage(string command, Variable[] vars, bool quiet, bool force)
        {
            if (vars.Length == 0)
            {
                throw new RunTimeError("Usage: overhead ('text') [color] [serial]");
            }

            string overheadMessage = vars[0].AsString();
            overheadMessage = CommandHelper.ReplaceStringInterpolations(overheadMessage);

            if (vars.Length == 1)
            {
                World.Player.OverheadMessage(Config.GetInt("SysColor"), overheadMessage);
            }
            else if (vars.Length >= 2)
            {
                int hue = Utility.ToInt32(vars[1].AsString(), 0);

                if (vars.Length == 3)
                {
                    uint serial = vars[2].AsSerial();

                    Mobile m = World.FindMobile(serial);
                    m?.OverheadMessage(hue, overheadMessage);
                }
                else
                {
                    World.Player.OverheadMessage(hue, overheadMessage);
                }
            }

            return true;
        }

        private static bool SystemMessage(string command, Variable[] vars, bool quiet, bool force)
        {
            if (vars.Length == 0)
            {
                throw new RunTimeError("Usage: sysmsg ('text') [color]");
            }

            var sysMessage = vars[0].AsString();
            sysMessage = CommandHelper.ReplaceStringInterpolations(sysMessage);

            if (vars.Length == 1)
            {
                World.Player.SendMessage(Config.GetInt("SysColor"), sysMessage);
            }
            else if (vars.Length == 2)
            {
                World.Player.SendMessage(Utility.ToInt32(vars[1].AsString(), 0), sysMessage);
            }

            return true;
        }

        private static bool ClearSysMsg(string command, Variable[] vars, bool quiet, bool force)
        {
            SystemMessages.Messages.Clear();

            return true;
        }

        private static DressList _lastDressList;

        private static bool DressCommand(string command, Variable[] vars, bool quiet, bool force)
        {
            if (vars.Length == 0)
            {
                throw new RunTimeError("Usage: dress ('name of dress list')");
            }

            if (_lastDressList == null)
            {
                _lastDressList = DressList.Find(vars[0].AsString());

                if (_lastDressList != null)
                {
                    _lastDressList.Dress();
                }
                else
                {
                    Serial serial = vars[0].AsSerial();
                    Item item = World.FindItem(serial);

                    if (item != null)
                    {
                        DressList dressList = new DressList("temp");
                        dressList.Items.Add(serial);
                        dressList.Dress();

                        _lastDressList = dressList;
                    }
                    else
                    {
                        CommandHelper.SendWarning(command, $"'{vars[0].AsString()}' not found", quiet);
                        return true;
                    }
                }
            }
            else if (ActionQueue.Empty)
            {
                _lastDressList = null;
                return true;
            }

            return false;
        }

        private static DressList _lastUndressList;
        private static bool _undressAll;
        private static bool _undressLayer;

        private static bool UnDressCommand(string command, Variable[] vars, bool quiet, bool force)
        {

            if (vars.Length == 0 && !_undressAll) // full naked!
            {
                _undressAll = true;
                Dress.UndressAll(); // CE: UndressHotKeys.OnUndressAll (Wrapper)
            }
            else if (vars.Length == 1 && _lastUndressList == null && !_undressLayer) // either a dress list item or a layer
            {
                _lastUndressList = DressList.Find(vars[0].AsString());

                if (_lastUndressList != null)
                {
                    _lastUndressList.Undress();
                }
                else // lets find the layer
                {
                    if (Enum.TryParse(vars[0].AsString(), true, out Layer layer))
                    {
                        Dress.Unequip(layer);
                        _undressLayer = true;
                    }
                    else
                    {
                        Serial serial = vars[0].AsSerial();
                        Item item = World.FindItem(serial);

                        if (item != null)
                        {
                            DressList undressList = new DressList("temp");
                            undressList.Items.Add(serial);
                            undressList.Undress();

                            _lastUndressList = undressList;
                        }
                        else
                        {
                            CommandHelper.SendWarning(command, $"'{vars[0].AsString()}' not found", quiet);
                            return true;
                        }
                    }
                }
            }
            else if (ActionQueue.Empty)
            {
                _undressAll = false;
                _undressLayer = false;
                _lastUndressList = null;
                return true;
            }

            return false;
        }

        private static bool GumpResponse(string command, Variable[] vars, bool quiet, bool force)
        {
            if (vars.Length < 1)
            {
                throw new RunTimeError("Usage: gumpresponse (buttondId)");
                //throw new RunTimeError("Usage: gumpresponse (buttondId) [option] ['text1'|fieldId] ['text2'|fieldId]");
            }

            int buttonId = vars[0].AsInt();

            // Outlands: optionales [gumpId] — antwortet gezielt auf diesen Gump
            // statt auf den zuletzt geoeffneten.
            uint gumpI = World.Player.CurrentGumpI;
            uint gumpS = World.Player.CurrentGumpS;

            if (vars.Length > 1)
            {
                uint requested = vars[1].AsUInt();

                if (World.Player.GumpList.TryGetValue(requested, out PlayerData.GumpInfo info))
                {
                    gumpI = requested;
                    gumpS = info.GumpSerial;
                }
            }

            ClientProxy.SendToClient(new CloseGump(gumpI));
            ClientProxy.SendToServer(new GumpResponse(gumpS, gumpI,
                buttonId, new int[] { }, new GumpTextEntry[] { }));

            World.Player.GumpList.Remove(gumpI);
            World.Player.HasGump = false;
            World.Player.HasCompressedGump = false;

            return true;
        }

        private static bool GumpClose(string command, Variable[] vars, bool quiet, bool force)
        {
            uint gumpI = World.Player.CurrentGumpI;

            if (vars.Length > 0)
            {
                gumpI = vars[0].AsUInt();
            }

            if (!World.Player.GumpList.ContainsKey(gumpI))
            {
                CommandHelper.SendWarning(command, $"'{gumpI}' unknown gump id", quiet);
                return true;
            }

            uint gumpS = World.Player.GumpList[gumpI].GumpSerial;

            ClientProxy.SendToClient(new CloseGump(gumpI));
            ClientProxy.SendToServer(new GumpResponse(gumpS, gumpI, 0, new int[] { }, new GumpTextEntry[] { }));

            World.Player.HasGump = false;
            World.Player.HasCompressedGump = false;

            return true;
        }

        private static bool ContextMenu(string command, Variable[] vars, bool quiet, bool force)
        {
            if (vars.Length < 2)
            {
                throw new RunTimeError("Usage: menu (serial) (index)");
            }

            Serial s = vars[0].AsSerial();
            ushort index = vars[1].AsUShort();
            bool blockPopup = true;

            if (vars.Length > 2)
            {
                blockPopup = vars[2].AsBool();
            }

            if (s == Serial.Zero && World.Player != null)
                s = World.Player.Serial;

            ScriptManager.BlockPopupMenu = blockPopup;

            ClientProxy.SendToServer(new ContextMenuRequest(s));
            ClientProxy.SendToServer(new ContextMenuResponse(s, index));
            return true;
        }

        private static bool MenuResponse(string command, Variable[] vars, bool quiet, bool force)
        {
            if (vars.Length < 2)
            {
                throw new RunTimeError("Usage: menuresponse (index) (menuId) [hue]");
            }

            ushort index = vars[0].AsUShort();
            ushort menuId = vars[1].AsUShort();
            ushort hue = 0;

            if (vars.Length == 3)
                hue = vars[2].AsUShort();

            ClientProxy.SendToServer(new MenuResponse(World.Player.CurrentMenuS, World.Player.CurrentMenuI, index,
                menuId, hue));
            World.Player.HasMenu = false;
            return true;
        }

        private static bool PromptResponse(string command, Variable[] vars, bool quiet, bool force)
        {
            if (vars.Length < 1)
            {
                throw new RunTimeError("Usage: promptresponse ('response to the prompt')");
            }

            World.Player.ResponsePrompt(vars[0].AsString());
            return true;
        }

        private static bool LastTarget(string command, Variable[] vars, bool quiet, bool force)
        {
            if (!Targeting.DoLastTarget())
                Targeting.ResendTarget();

            return true;
        }

        private static bool PlayScript(string command, Variable[] vars, bool quiet, bool force)
        {
            if (vars.Length < 1)
            {
                throw new RunTimeError("Usage: script 'name of script'");
            }

            ScriptManager.PlayScript(vars[0].AsString());

            return true;
        }

        private static readonly Dictionary<string, ushort> PotionList = new Dictionary<string, ushort>()
        {
            {"heal", 3852},
            {"cure", 3847},
            {"refresh", 3851},
            {"nightsight", 3846},
            {"ns", 3846},
            {"explosion", 3853},
            {"strength", 3849},
            {"str", 3849},
            {"agility", 3848}
        };

        private static bool Potion(string command, Variable[] vars, bool quiet, bool force)
        {
            if (vars.Length == 0)
            {
                throw new RunTimeError("Usage: potion ('type')");
            }

            Item pack = World.Player.Backpack;
            if (pack == null)
                return true;

            if (PotionList.TryGetValue(vars[0].AsString().ToLower(), out ushort potionId))
            {
                // Razor CE: zusaetzlich AllowBit(FeatureBit.BlockHealPoisoned) —
                // der Port kennt keine FeatureBits.
                if (potionId == 3852 && World.Player.Poisoned && Config.GetBool("BlockHealPoison"))
                {
                    World.Player.SendMessage(MsgLevel.Force, LocString.HealPoisonBlocked);
                    return true;
                }

                if (!World.Player.UseItem(pack, potionId))
                {
                    CommandHelper.SendWarning(command, Language.Format(LocString.NoItemOfType, (ItemID) potionId), quiet);
                }
            }
            else
            {
                throw new RunTimeError($"{command} - Unknown potion type");
            }

            return true;
        }

        private static bool WaitForSysMsg(string command, Variable[] vars, bool quiet, bool force)
        {
            if (vars.Length < 1)
            {
                throw new RunTimeError("Usage: waitforsysmsg 'message to wait for' [timeout]");
            }

            if (SystemMessages.Exists(vars[0].AsString()))
            {
                Interpreter.ClearTimeout();
                return true;
            }

            Interpreter.Timeout(vars.Length > 1 ? vars[1].AsUInt() : 30000, () => { return true; });

            return false;
        }

        private static bool Random(string command, Variable[] vars, bool quiet, bool force)
        {
            if (vars.Length < 1)
            {
                throw new RunTimeError("Usage: random 'max value'");
            }

            int max = vars[0].AsInt();

            World.Player.SendMessage(MsgLevel.Info, $"Random: {Utility.Random(1, max)}");

            return true;
        }

        private static bool ClearDragDrop(string command, Variable[] vars, bool quiet, bool force)
        {
            DragDropManager.GracefulStop();

            return true;
        }

        private static bool Interrupt(string command, Variable[] vars, bool quiet, bool force)
        {
            if (vars.Length == 1)
            {
                Layer layer = CommandHelper.ParseLayer(vars[0].AsString());

                if (layer > Layer.Invalid && layer <= Layer.LastUserValid)
                {
                    // Razor CE: Spell.Interrupt(layer) — gezieltes Layer.
                    // TODO(scripting-stub): Port-Interrupt kennt keine Layer-
                    // Auswahl; generisches Interrupt.
                    Spell.Interrupt();
                }
                else
                {
                    throw new RunTimeError($"{command} - Invalid layer");
                }
            }
            else
            {
                Spell.Interrupt();
            }

            return true;
        }
    }
}
