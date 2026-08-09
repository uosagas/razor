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

// Portiert aus Razor CE (Razor/Macros/Actions.cs):
// Felder, ctor(string[]), Serialize()/ToString()/ToScript() sind werkgetreu,
// damit .macro-Dateien verlustfrei geladen und byte-gleich gespeichert werden.
// Die Typ-FullNames (Assistant.Macros.*) MUESSEN mit Razor CE uebereinstimmen,
// da sie 1:1 in den .macro-Dateien stehen — Namespace daher nicht umbenennen!
//
// Phase 2c: Perform()/PerformWait() sind aus Razor CE portiert.
// PRIORITAET A (funktional, werktreu bis auf dokumentierte Trimmung):
//   Speech, DoubleClick(Type), Lift(Type), Drop, DropRelLoc, GumpResponse,
//   MenuResponse, Pause, WaitForTarget/Gump/Menu/Stat/Prompt, If/Else/EndIf,
//   For/EndFor, While/EndWhile, DoWhile, ContextMenu, UseSkill, CastSpell
//   (Ext/Book/Macro), SetAbility, AbsoluteTarget, TargetType, TargetRelLoc,
//   LastTarget, Prompt, Walk (ueber IClientServices.RequestMove).
// PRIORITAET B (NotImplemented-Log, s. MacroAction.Perform-Basis):
//   OverheadMessage (Client-Injection), HotKey (HotKey-System),
//   SetLastTarget/SetMacroVariableTarget/AbsoluteTargetVariable/
//   DoubleClickVariable (MacroVariables), ClearSysMessages ist funktional.
// PHASE 2d NACHGERUESTET: Dress/UnDress (DressList/Dress-Port) und die
//   Counter-Bedingungen in If/While/DoWhile (Counter-Port) sind funktional.
// WEITER NICHT PORTIERT: WinForms-Kontextmenues (GetContextMenuItems/Edit/
// ReTarget), Command-Praefix "-" in SpeechAction (Razor-Kommandos kommen spaeter).

using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using Assistant.Core;

namespace Assistant.Macros
{
    public abstract class MacroAction
    {
        protected Macro m_Parent;

        public MacroAction()
        {
        }

        public override string ToString()
        {
            return $"?{GetType().Name}?";
        }

        public abstract string ToScript();

        public virtual string Serialize()
        {
            return GetType().FullName;
        }

        protected string DoSerialize(params object[] args)
        {
            StringBuilder sb = new StringBuilder(GetType().FullName);
            for (int i = 0; i < args.Length; i++)
                sb.AppendFormat("|{0}", args[i]);
            return sb.ToString();
        }

        public Macro Parent
        {
            get { return m_Parent; }
            set { m_Parent = value; }
        }

        /// <summary>
        /// Fuehrt die Aktion aus. true = fertig, weiter mit der naechsten Aktion;
        /// false bei MacroWaitActions = Warten beginnt (PerformWait).
        /// Basis-Implementierung: Prioritaet-B-Log (Aktion wird uebersprungen).
        /// </summary>
        public virtual bool Perform()
        {
            Console.WriteLine(
                $"[Razor] {GetType().Name}.Perform() ist in Phase 2c nicht implementiert (Prioritaet B) — uebersprungen.");
            return true;
        }
    }

    public abstract class MacroWaitAction : MacroAction
    {
        protected TimeSpan m_Timeout = TimeSpan.FromMinutes(5);
        private DateTime m_Start;

        public MacroWaitAction()
        {
        }

        /// <summary>true = weiter warten, false = Wartebedingung erfuellt.</summary>
        public virtual bool PerformWait()
        {
            return false;
        }

        public TimeSpan Timeout
        {
            get { return m_Timeout; }
        }

        public DateTime StartTime
        {
            get { return m_Start; }
            set { m_Start = value; }
        }

        public virtual bool CheckMatch(MacroAction a)
        {
            return false;
        }
    }

    public class MacroComment : MacroAction
    {
        private string m_Comment;

        public MacroComment(string comment)
        {
            if (comment == null)
                comment = "";

            m_Comment = comment.Trim();
        }

        public override bool Perform()
        {
            return true;
        }

        public override string ToScript()
        {
            return $"# {m_Comment}";
        }

        public override string Serialize()
        {
            return ToString();
        }

        public string Comment
        {
            get { return m_Comment; }
            set { m_Comment = value; }
        }

        public override string ToString()
        {
            if (m_Comment == null)
                m_Comment = "";

            return $"// {m_Comment}";
        }
    }

    /// <summary>
    /// UOSagas-Razor-Erweiterung (nicht in Razor CE): konserviert Zeilen mit
    /// unbekanntem Aktionstyp roh, damit Laden+Speichern verlustfrei bleibt.
    /// Razor CE verwirft solche Zeilen stillschweigend.
    /// </summary>
    public class UnknownMacroAction : MacroAction
    {
        private readonly string m_RawLine;

        public UnknownMacroAction(string rawLine)
        {
            m_RawLine = rawLine ?? "";
        }

        public string RawLine
        {
            get { return m_RawLine; }
        }

        public string TypeName
        {
            get
            {
                int idx = m_RawLine.IndexOf('|');
                return idx < 0 ? m_RawLine : m_RawLine.Substring(0, idx);
            }
        }

        public override bool Perform()
        {
            Console.WriteLine($"[Razor] Unbekannte Macro-Aktion '{TypeName}' uebersprungen.");
            return true;
        }

        public override string Serialize()
        {
            return m_RawLine;
        }

        public override string ToScript()
        {
            return $"# unknown action: {TypeName}";
        }

        public override string ToString()
        {
            return $"?{TypeName}?";
        }
    }

    public class ClearSysMessages : MacroAction
    {
        public ClearSysMessages()
        {
        }

        public override bool Perform()
        {
            SystemMessages.Messages.Clear();

            return true;
        }

        public override string ToScript()
        {
            return "clearsysmsg";
        }

        public override string Serialize()
        {
            return DoSerialize();
        }

        public override string ToString()
        {
            return Language.GetString(LocString.ClearSysMsg);
        }
    }

    public class DoubleClickAction : MacroAction
    {
        private Serial m_Serial;
        private ushort m_Gfx;

        public DoubleClickAction(Serial obj, ushort gfx)
        {
            m_Serial = obj;
            m_Gfx = gfx;
        }

        public DoubleClickAction(string[] args)
        {
            m_Serial = Serial.Parse(args[1]);
            m_Gfx = Convert.ToUInt16(args[2]);
        }

        public override bool Perform()
        {
            PlayerData.DoubleClick(m_Serial);
            return true;
        }

        public override string ToScript()
        {
            return $"dclick '{m_Serial.ToString()}'";
        }

        public override string Serialize()
        {
            return DoSerialize(m_Serial.Value, m_Gfx);
        }

        public override string ToString()
        {
            return Language.Format(LocString.DClickA1, m_Serial);
        }
    }

    public class DoubleClickTypeAction : MacroAction
    {
        private ushort m_Gfx;
        public bool m_Item;

        public DoubleClickTypeAction(string[] args)
        {
            m_Gfx = Convert.ToUInt16(args[1]);
            try
            {
                m_Item = Convert.ToBoolean(args[2]);
            }
            catch
            {
            }
        }

        public DoubleClickTypeAction(ushort gfx, bool item)
        {
            m_Gfx = gfx;
            m_Item = item;
        }

        public override bool Perform()
        {
            Serial click = Serial.Zero;

            if (m_Item)
            {
                Item item = World.Player.Backpack != null ? World.Player.Backpack.FindItemById(m_Gfx) : null;
                ArrayList list = new ArrayList();
                if (item == null)
                {
                    foreach (Item i in World.Items.Values)
                    {
                        if (i.ItemID == m_Gfx && i.RootContainer == null)
                        {
                            if (Config.GetBool("RangeCheckDoubleClick"))
                            {
                                if (Utility.InRange(World.Player.Position, i.Position, 2))
                                {
                                    list.Add(i);
                                }
                            }
                            else
                            {
                                list.Add(i);
                            }
                        }
                    }

                    if (list.Count == 0)
                    {
                        foreach (Item i in World.Items.Values)
                        {
                            if (i.ItemID == m_Gfx && !i.IsInBank)
                            {
                                if (Config.GetBool("RangeCheckDoubleClick"))
                                {
                                    if (Utility.InRange(World.Player.Position, i.Position, 2) ||
                                        i.RootContainer == World.Player)
                                    {
                                        list.Add(i);
                                    }
                                }
                                else
                                {
                                    list.Add(i);
                                }
                            }
                        }
                    }

                    if (list.Count > 0)
                        click = ((Item) list[Utility.Random(list.Count)]).Serial;
                }
                else
                {
                    click = item.Serial;
                }
            }
            else
            {
                ArrayList list = new ArrayList();
                foreach (Mobile m in World.MobilesInRange())
                {
                    if (m.Body == m_Gfx)
                    {
                        if (Config.GetBool("RangeCheckDoubleClick"))
                        {
                            if (Utility.InRange(World.Player.Position, m.Position, 2))
                            {
                                list.Add(m);
                            }
                        }
                        else
                        {
                            list.Add(m);
                        }
                    }
                }

                if (list.Count > 0)
                    click = ((Mobile) list[Utility.Random(list.Count)]).Serial;
            }

            if (click != Serial.Zero)
                PlayerData.DoubleClick(click);
            else
                World.Player.SendMessage(MsgLevel.Force, "No item of type {0}",
                    m_Item ? $"0x{m_Gfx:X}" : $"(Character) 0x{m_Gfx:X}");
            return true;
        }

        public override string ToScript()
        {
            return $"dclicktype '{m_Gfx}'";
        }

        public override string Serialize()
        {
            return DoSerialize(m_Gfx, m_Item);
        }

        public override string ToString()
        {
            // Original zeigt (ItemID)m_Gfx als Item-Namen (Ultima-SDK); hier nur Hex.
            return Language.Format(LocString.DClickA1,
                m_Item ? $"0x{m_Gfx:X}" : $"(Character) 0x{m_Gfx:X}");
        }
    }

    public class LiftAction : MacroWaitAction
    {
        private ushort m_Amount;
        private Serial m_Serial;
        private ushort m_Gfx;

        private static Item m_LastLift;

        public static Item LastLift
        {
            get { return m_LastLift; }
            set { m_LastLift = value; }
        }

        public LiftAction(string[] args)
        {
            m_Serial = Serial.Parse(args[1]);
            m_Amount = Convert.ToUInt16(args[2]);
            m_Gfx = Convert.ToUInt16(args[3]);
        }

        public LiftAction(Serial ser, ushort amount, ushort gfx)
        {
            m_Serial = ser;
            m_Amount = amount;
            m_Gfx = gfx;
        }

        private int m_Id;

        public override bool Perform()
        {
            Item item = World.FindItem(m_Serial);
            if (item != null)
            {
                m_LastLift = item;
                m_Id = DragDropManager.Drag(item, m_Amount <= item.Amount ? m_Amount : item.Amount);
            }
            else
            {
                World.Player.SendMessage(MsgLevel.Warning, "Macro item out of range.");
            }

            return false;
        }

        public override bool PerformWait()
        {
            return DragDropManager.LastIDLifted < m_Id;
        }

        public override string ToScript()
        {
            return $"lift '{m_Serial}' {m_Amount}";
        }

        public override string Serialize()
        {
            return DoSerialize(m_Serial.Value, m_Amount, m_Gfx);
        }

        public override string ToString()
        {
            return Language.Format(LocString.LiftA10, m_Serial, m_Amount);
        }
    }

    public class LiftTypeAction : MacroWaitAction
    {
        private ushort m_Gfx;
        private ushort m_Amount;

        public LiftTypeAction(string[] args)
        {
            m_Gfx = Convert.ToUInt16(args[1]);
            m_Amount = Convert.ToUInt16(args[2]);
        }

        public LiftTypeAction(ushort gfx, ushort amount)
        {
            m_Gfx = gfx;
            m_Amount = amount;
        }

        private int m_Id;

        public override bool Perform()
        {
            Item item = World.Player.Backpack != null ? World.Player.Backpack.FindItemById(m_Gfx) : null;

            if (item != null)
            {
                ushort amount = m_Amount;
                if (item.Amount < amount)
                    amount = item.Amount;
                LiftAction.LastLift = item;
                m_Id = DragDropManager.Drag(item, amount);
            }
            else
            {
                World.Player.SendMessage(MsgLevel.Warning, "No item of type 0x{0:X}", m_Gfx);
            }

            return false;
        }

        public override bool PerformWait()
        {
            return DragDropManager.LastIDLifted < m_Id && !DragDropManager.Empty;
        }

        public override string ToScript()
        {
            return $"lifttype '{m_Gfx}' {m_Amount}";
        }

        public override string Serialize()
        {
            return DoSerialize(m_Gfx, m_Amount);
        }

        public override string ToString()
        {
            return Language.Format(LocString.LiftA10, m_Amount, $"0x{m_Gfx:X}");
        }
    }

    public class DropAction : MacroAction
    {
        private Serial m_To;
        private Point3D m_At;
        private Layer m_Layer;

        public DropAction(string[] args)
        {
            m_To = Serial.Parse(args[1]);
            m_At = Point3D.Parse(args[2]);
            try
            {
                m_Layer = (Layer) Byte.Parse(args[3]);
            }
            catch
            {
                m_Layer = Layer.Invalid;
            }
        }

        public DropAction(Serial to, Point3D at) : this(to, at, 0)
        {
        }

        public DropAction(Serial to, Point3D at, Layer layer)
        {
            m_To = to;
            m_At = at;
            m_Layer = layer;
        }

        public override bool Perform()
        {
            if (DragDropManager.Holding != null)
            {
                if (m_Layer > Layer.Invalid && m_Layer <= Layer.LastUserValid)
                {
                    Mobile m = World.FindMobile(m_To);
                    if (m != null)
                        DragDropManager.Drop(DragDropManager.Holding, m, m_Layer);
                }
                else
                {
                    DragDropManager.Drop(DragDropManager.Holding, m_To, m_At);
                }
            }
            else
            {
                World.Player.SendMessage(MsgLevel.Warning, "Macro is not holding anything.");
            }

            return true;
        }

        public override string ToScript()
        {
            if (!m_To.IsValid)
            {
                return $"droprelloc {m_At.X} {m_At.Y}";
            }

            return m_Layer != Layer.Invalid ? $"drop '{m_To}' {m_Layer}" : $"drop '{m_To}' {m_At.X} {m_At.Y} {m_At.Z}";
        }

        public override string Serialize()
        {
            return DoSerialize(m_To, m_At, (byte) m_Layer);
        }

        public override string ToString()
        {
            if (m_Layer != Layer.Invalid)
                return Language.Format(LocString.EquipTo, m_To, m_Layer);
            else
                return Language.Format(LocString.DropA2, m_To.IsValid ? m_To.ToString() : "Ground", m_At);
        }
    }

    public class DropRelLocAction : MacroAction
    {
        private sbyte[] m_Loc;

        public DropRelLocAction(string[] args)
        {
            m_Loc = new sbyte[3]
            {
                Convert.ToSByte(args[1]),
                Convert.ToSByte(args[2]),
                Convert.ToSByte(args[3])
            };
        }

        public DropRelLocAction(sbyte x, sbyte y, sbyte z)
        {
            m_Loc = new sbyte[3] {x, y, z};
        }

        public override bool Perform()
        {
            if (DragDropManager.Holding != null)
                DragDropManager.Drop(DragDropManager.Holding, null,
                    new Point3D((ushort) (World.Player.Position.X + m_Loc[0]),
                        (ushort) (World.Player.Position.Y + m_Loc[1]), (short) (World.Player.Position.Z + m_Loc[2])));
            else
                World.Player.SendMessage("Macro is not holding anything.");
            return true;
        }

        public override string ToScript()
        {
            return $"droprelloc {m_Loc[0]} {m_Loc[1]}";
        }

        public override string Serialize()
        {
            return DoSerialize(m_Loc[0], m_Loc[1], m_Loc[2]);
        }

        public override string ToString()
        {
            return Language.Format(LocString.DropRelA3, m_Loc[0], m_Loc[1], m_Loc[2]);
        }
    }

    public class GumpResponseAction : MacroAction
    {
        private int m_ButtonID;
        private int[] m_Switches;
        private GumpTextEntry[] m_TextEntries;

        public GumpResponseAction(string[] args)
        {
            m_ButtonID = Convert.ToInt32(args[1]);
            m_Switches = new int[Convert.ToInt32(args[2])];
            for (int i = 0; i < m_Switches.Length; i++)
                m_Switches[i] = Convert.ToInt32(args[3 + i]);
            m_TextEntries = new GumpTextEntry[Convert.ToInt32(args[3 + m_Switches.Length])];
            for (int i = 0; i < m_TextEntries.Length; i++)
            {
                string[] split = args[4 + m_Switches.Length + i].Split('&');

                m_TextEntries[i] = new GumpTextEntry(Convert.ToUInt16(split[0]), split[1]);
            }
        }

        public GumpResponseAction(int button, int[] switches, GumpTextEntry[] entries)
        {
            m_ButtonID = button;
            m_Switches = switches;
            m_TextEntries = entries;
        }

        public override bool Perform()
        {
            ClientProxy.SendToClient(new CloseGump(World.Player.CurrentGumpI));
            ClientProxy.SendToServer(new GumpResponse(World.Player.CurrentGumpS, World.Player.CurrentGumpI,
                m_ButtonID, m_Switches, m_TextEntries));
            World.Player.HasGump = false;
            World.Player.HasCompressedGump = false;
            return true;
        }

        public override string ToScript()
        {
            return m_ButtonID == 0 ? "gumpclose" : $"gumpresponse {m_ButtonID}";
        }

        public override string Serialize()
        {
            ArrayList list = new ArrayList(3 + m_Switches.Length + m_TextEntries.Length);
            list.Add(m_ButtonID);
            list.Add(m_Switches.Length);
            list.AddRange(m_Switches);
            list.Add(m_TextEntries.Length);
            for (int i = 0; i < m_TextEntries.Length; i++)
                list.Add($"{m_TextEntries[i].EntryID}&{m_TextEntries[i].Text}");
            return DoSerialize((object[]) list.ToArray(typeof(object)));
        }

        public override string ToString()
        {
            if (m_ButtonID != 0)
                return Language.Format(LocString.GumpRespB, m_ButtonID);
            else
                return Language.Format(LocString.CloseGump);
        }
    }

    public class MenuResponseAction : MacroAction
    {
        private ushort m_Index, m_ItemID, m_Hue;

        public MenuResponseAction(string[] args)
        {
            m_Index = Convert.ToUInt16(args[1]);
            m_ItemID = Convert.ToUInt16(args[2]);
            m_Hue = Convert.ToUInt16(args[3]);
        }

        public MenuResponseAction(ushort idx, ushort iid, ushort hue)
        {
            m_Index = idx;
            m_ItemID = iid;
            m_Hue = hue;
        }

        public override bool Perform()
        {
            ClientProxy.SendToServer(new MenuResponse(World.Player.CurrentMenuS, World.Player.CurrentMenuI, m_Index,
                m_ItemID, m_Hue));
            World.Player.HasMenu = false;
            return true;
        }

        public override string ToScript()
        {
            return $"menuresponse {m_Index} {m_ItemID} {m_Hue}";
        }

        public override string Serialize()
        {
            return DoSerialize(m_Index, m_ItemID, m_Hue);
        }

        public override string ToString()
        {
            return Language.Format(LocString.MenuRespA1, m_Index);
        }
    }

    public class AbsoluteTargetAction : MacroAction
    {
        private TargetInfo m_Info;

        public AbsoluteTargetAction(string[] args)
        {
            m_Info = new TargetInfo();

            m_Info.Type = Convert.ToByte(args[1]);
            m_Info.Flags = Convert.ToByte(args[2]);
            m_Info.Serial = Convert.ToUInt32(args[3]);
            m_Info.X = Convert.ToUInt16(args[4]);
            m_Info.Y = Convert.ToUInt16(args[5]);
            m_Info.Z = Convert.ToInt16(args[6]);
            m_Info.Gfx = Convert.ToUInt16(args[7]);
        }

        public AbsoluteTargetAction(TargetInfo info)
        {
            m_Info = new TargetInfo();
            m_Info.Type = info.Type;
            m_Info.Flags = info.Flags;
            m_Info.Serial = info.Serial;
            m_Info.X = info.X;
            m_Info.Y = info.Y;
            m_Info.Z = info.Z;
            m_Info.Gfx = info.Gfx;
        }

        public override bool Perform()
        {
            Targeting.Target(m_Info);
            return true;
        }

        public override string ToScript()
        {
            return $"target {m_Info.Serial}";
        }

        public override string Serialize()
        {
            return DoSerialize(m_Info.Type, m_Info.Flags, m_Info.Serial.Value, m_Info.X, m_Info.Y, m_Info.Z,
                m_Info.Gfx);
        }

        public override string ToString()
        {
            return Language.GetString(LocString.AbsTarg);
        }
    }

    public class AbsoluteTargetVariableAction : MacroAction
    {
        private readonly string _variableName;

        public AbsoluteTargetVariableAction(string[] args)
        {
            _variableName = args.Length > 1 ? args[1] : args[0];
        }

        public string VariableName
        {
            get { return _variableName; }
        }

        public override bool Perform()
        {
            MacroVariables.MacroVariable mV = MacroVariables.Find(_variableName);

            if (mV != null)
            {
                Targeting.Target(mV.TargetInfo);
                return true;
            }

            World.Player?.SendMessage(MsgLevel.Warning, $"Macro variable '{_variableName}' not found");
            return false;
        }

        public override string ToScript()
        {
            return $"target '{_variableName}'";
        }

        public override string Serialize()
        {
            return DoSerialize(_variableName);
        }

        public override string ToString()
        {
            return $"{Language.GetString(LocString.AbsTarg)} (${_variableName})";
        }
    }

    public class DoubleClickVariableAction : MacroAction
    {
        private readonly string _variableName;

        public DoubleClickVariableAction(string[] args)
        {
            _variableName = args.Length > 1 ? args[1] : args[0];
        }

        public string VariableName
        {
            get { return _variableName; }
        }

        public override bool Perform()
        {
            MacroVariables.MacroVariable mV = MacroVariables.Find(_variableName);

            if (mV != null && mV.TargetInfo.Serial != Serial.Zero)
            {
                PlayerData.DoubleClick(mV.TargetInfo.Serial);
                return true;
            }

            World.Player?.SendMessage(MsgLevel.Warning, $"Macro variable '{_variableName}' not found");
            return false;
        }

        public override string ToScript()
        {
            return $"dclick '{_variableName}'";
        }

        public override string Serialize()
        {
            return DoSerialize(_variableName);
        }

        public override string ToString()
        {
            return $"DoubleClick (${_variableName})";
        }
    }

    public class TargetTypeAction : MacroAction
    {
        private bool m_Mobile;
        private ushort m_Gfx;
        private object _previousObject;

        public TargetTypeAction(string[] args)
        {
            m_Mobile = Convert.ToBoolean(args[1]);
            m_Gfx = Convert.ToUInt16(args[2]);
        }

        public TargetTypeAction(bool mobile, ushort gfx)
        {
            m_Mobile = mobile;
            m_Gfx = gfx;
        }

        public override bool Perform()
        {
            ArrayList list = new ArrayList();
            if (m_Mobile)
            {
                foreach (Mobile find in World.MobilesInRange())
                {
                    if (find.Body == m_Gfx)
                    {
                        if (Config.GetBool("RangeCheckTargetByType"))
                        {
                            if (Utility.InRange(World.Player.Position, find.Position, 2))
                            {
                                list.Add(find);
                            }
                        }
                        else
                        {
                            list.Add(find);
                        }
                    }
                }
            }
            else
            {
                foreach (Item i in World.Items.Values)
                {
                    if (i.ItemID == m_Gfx && !i.IsInBank)
                    {
                        if (Config.GetBool("RangeCheckTargetByType"))
                        {
                            if (Utility.InRange(World.Player.Position, i.Position, 2) ||
                                i.RootContainer == World.Player)
                            {
                                list.Add(i);
                            }
                        }
                        else
                        {
                            list.Add(i);
                        }
                    }
                }
            }

            if (list.Count > 0)
            {
                if (Config.GetBool("DiffTargetByType") && list.Count > 1)
                {
                    object currentObject = list[Utility.Random(list.Count)];

                    while (_previousObject != null && _previousObject == currentObject)
                    {
                        currentObject = list[Utility.Random(list.Count)];
                    }

                    Targeting.Target(currentObject);

                    _previousObject = currentObject;
                }
                else
                {
                    Targeting.Target(list[Utility.Random(list.Count)]);
                }
            }
            else
            {
                World.Player.SendMessage(MsgLevel.Warning, "No item of type {0}",
                    m_Mobile ? $"Character [{m_Gfx}]" : $"0x{m_Gfx:X}");
            }

            return true;
        }

        public override string ToScript()
        {
            return $"targettype '{m_Gfx}'";
        }

        public override string Serialize()
        {
            return DoSerialize(m_Mobile, m_Gfx);
        }

        public override string ToString()
        {
            if (m_Mobile)
                return Language.Format(LocString.TargByType, m_Gfx);
            else
                return Language.Format(LocString.TargByType, $"0x{m_Gfx:X}");
        }
    }

    public class TargetRelLocAction : MacroAction
    {
        private sbyte m_X, m_Y;

        public TargetRelLocAction(string[] args)
        {
            m_X = Convert.ToSByte(args[1]);
            m_Y = Convert.ToSByte(args[2]);
        }

        public TargetRelLocAction(sbyte x, sbyte y)
        {
            m_X = x;
            m_Y = y;
        }

        public override bool Perform()
        {
            // Razor CE ermittelt hier das Bodentile (Ultima-SDK Map.GetTileNear)
            // fuer Z + Tile-Gfx. Ohne Map-Daten (Phase 2c): Ground-Target auf
            // Spieler-Z mit Gfx 0 — Server akzeptieren das fuer Location-Targets.
            ushort x = (ushort) (World.Player.Position.X + m_X);
            ushort y = (ushort) (World.Player.Position.Y + m_Y);
            short z = (short) World.Player.Position.Z;
            try
            {
                Targeting.Target(new Point3D(x, y, z), 0);
            }
            catch (Exception e)
            {
                World.Player.SendMessage(MsgLevel.Debug, "Error Executing TargetRelLoc: {0}", e.Message);
            }

            return true;
        }

        public override string ToScript()
        {
            return $"targetrelloc {m_X} {m_Y}";
        }

        public override string Serialize()
        {
            return DoSerialize(m_X, m_Y);
        }

        public override string ToString()
        {
            return Language.Format(LocString.TargRelLocA3, m_X, m_Y, 0);
        }
    }

    public class LastTargetAction : MacroAction
    {
        public LastTargetAction()
        {
        }

        public override bool Perform()
        {
            if (!Targeting.DoLastTarget())
                Targeting.ResendTarget();
            return true;
        }

        public override string ToString()
        {
            return $"Exec: {Language.GetString(LocString.LastTarget)}";
        }

        public override string ToScript()
        {
            return "lasttarget";
        }
    }

    public class SetLastTargetAction : MacroWaitAction
    {
        public SetLastTargetAction()
        {
        }

        public override string ToString()
        {
            return Language.GetString(LocString.SetLT);
        }

        public override string ToScript()
        {
            return "setlasttarget";
        }
    }

    public class SetMacroVariableTargetAction : MacroWaitAction
    {
        private string m_VarName;
        private MacroVariables.MacroVariable m_MacroVariable;

        public SetMacroVariableTargetAction(string[] args)
        {
            m_VarName = args.Length > 1 ? args[1] : args[0];

            // Bewusste Abweichung vom Original: CE loest die Variable schon im
            // Konstruktor auf und mangelt unbekannte Namen zu "?name?" — wird die
            // Macro-Datei VOR dem Profil geladen, zerstoert ein Save den Namen.
            // Hier: Aufloesung lazy in Perform, der Name bleibt immer roh.
        }

        public SetMacroVariableTargetAction(string varName)
        {
            m_VarName = varName;
        }

        public string VariableName
        {
            get { return m_VarName; }
        }

        public override bool Perform()
        {
            m_MacroVariable = MacroVariables.Find(m_VarName);

            if (m_MacroVariable == null)
            {
                World.Player?.SendMessage(MsgLevel.Warning, $"Macro variable '{m_VarName}' not found");
                return false;
            }

            m_MacroVariable.TargetSetMacroVariable();
            return !PerformWait();
        }

        public override bool PerformWait()
        {
            if (m_MacroVariable == null)
                return false;

            return !m_MacroVariable.TargetWasSet;
        }

        public override string ToString()
        {
            return $"Set Macro Variable (${m_VarName})";
        }

        public override string ToScript()
        {
            return $"setvar {m_VarName}";
        }

        public override string Serialize()
        {
            return DoSerialize(m_VarName);
        }
    }

    public class SpeechAction : MacroAction
    {
        private MessageType m_Type;
        private ushort m_Font;
        private ushort m_Hue;
        private string m_Lang;
        private ArrayList m_Keywords;
        private string m_Speech;

        public SpeechAction(string[] args)
        {
            m_Type = ((MessageType) Convert.ToInt32(args[1])) & ~MessageType.Encoded;
            m_Hue = Convert.ToUInt16(args[2]);
            m_Font = Convert.ToUInt16(args[3]);
            m_Lang = args[4];

            int count = Convert.ToInt32(args[5]);
            if (count > 0)
            {
                m_Keywords = new ArrayList(count);
                m_Keywords.Add(Convert.ToUInt16(args[6]));

                for (int i = 1; i < count; i++)
                    m_Keywords.Add(Convert.ToByte(args[6 + i]));
            }

            m_Speech = args[6 + count];
        }

        public SpeechAction(MessageType type, ushort hue, ushort font, string lang, ArrayList kw, string speech)
        {
            m_Type = type;
            m_Hue = hue;
            m_Font = font;
            m_Lang = lang;
            m_Keywords = kw;
            m_Speech = speech;
        }

        public string Speech
        {
            get { return m_Speech; }
        }

        // Fuer den Edit-Dialog der UI (Text aendern, Rest der Speech beibehalten).
        public MessageType Type
        {
            get { return m_Type; }
        }

        public ushort Hue
        {
            get { return m_Hue; }
        }

        public ushort Font
        {
            get { return m_Font; }
        }

        public string Lang
        {
            get { return m_Lang; }
        }

        public ArrayList Keywords
        {
            get { return m_Keywords; }
        }

        public override bool Perform()
        {
            // Razor CE: fuehrt "-command"-Speech als Razor-Kommando aus
            // (Command.List) — Kommandos sind in Phase 2c noch nicht portiert,
            // solche Texte gehen als normale Speech raus.

            int hue = m_Hue;

            if (m_Type != MessageType.Emote)
            {
                if (World.Player.SpeechHue == 0)
                    World.Player.SpeechHue = m_Hue;
                hue = World.Player.SpeechHue;
            }

            ClientProxy.SendToServer(new ClientUniMessage(m_Type, hue, m_Font, m_Lang, m_Keywords, m_Speech));
            return true;
        }

        public override string ToScript()
        {
            switch (m_Type)
            {
                case MessageType.Emote:
                    return $"emote '{m_Speech}'";
                case MessageType.Whisper:
                    return $"whisper '{m_Speech}'";
                case MessageType.Yell:
                    return $"yell '{m_Speech}'";
                case MessageType.Alliance:
                    return $"alliance '{m_Speech}'";
                case MessageType.Guild:
                    return $"guild '{m_Speech}'";
                case MessageType.Regular:
                default:
                    return $"say '{m_Speech}'";
            }
        }

        public override string Serialize()
        {
            ArrayList list = new ArrayList(6);
            list.Add((int) m_Type);
            list.Add(m_Hue);
            list.Add(m_Font);
            list.Add(m_Lang);
            if (m_Keywords != null && m_Keywords.Count > 1)
            {
                list.Add((int) m_Keywords.Count);
                for (int i = 0; i < m_Keywords.Count; i++)
                    list.Add(m_Keywords[i]);
            }
            else
            {
                list.Add("0");
            }

            list.Add(m_Speech);

            return DoSerialize((object[]) list.ToArray(typeof(object)));
        }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            switch (m_Type)
            {
                case MessageType.Emote:
                    sb.Append("Emote: ");
                    break;
                case MessageType.Whisper:
                    sb.Append("Whisper: ");
                    break;
                case MessageType.Yell:
                    sb.Append("Yell: ");
                    break;
                case MessageType.Alliance:
                    sb.Append("Alliance: ");
                    break;
                case MessageType.Guild:
                    sb.Append("Guild: ");
                    break;
                case MessageType.Regular:
                default:
                    sb.Append("Say: ");
                    break;
            }

            sb.Append(m_Speech);
            return sb.ToString();
        }
    }

    public class OverheadMessageAction : MacroAction
    {
        private ushort _hue;
        private string _message;

        public OverheadMessageAction(string[] args)
        {
            _hue = Convert.ToUInt16(args[1]);

            List<string> message = new List<string>();

            for (int i = 2; i < args.Length; i++)
            {
                message.Add(args[i]);
            }

            _message = string.Join(" ", message);
        }

        public OverheadMessageAction(ushort hue, string message)
        {
            _hue = hue;
            _message = message;
        }

        // Fuer den Edit-Dialog der UI (Text aendern, Hue beibehalten).
        public ushort Hue
        {
            get { return _hue; }
        }

        public string Message
        {
            get { return _message; }
        }

        public override string ToScript()
        {
            return $"overhead '{_message}' {_hue}";
        }

        public override string Serialize()
        {
            ArrayList list = new ArrayList(2) {_hue, _message};

            return DoSerialize((object[]) list.ToArray(typeof(object)));
        }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append($"Overhead ({_hue}): ");
            sb.Append(_message);
            return sb.ToString();
        }
    }

    public class UseSkillAction : MacroAction
    {
        private int m_Skill;

        public UseSkillAction(string[] args)
        {
            m_Skill = Convert.ToInt32(args[1]);
        }

        public UseSkillAction(int sk)
        {
            m_Skill = sk;
        }

        public override bool Perform()
        {
            ClientProxy.SendToServer(new UseSkill(m_Skill));

            // Razor CE: StealthSteps.Hide() bei Stealth — StealthSteps kommt spaeter.

            World.Player.LastSkill = m_Skill;

            return true;
        }

        public override string ToScript()
        {
            // Original: Skills.GetSkillDisplayName(m_Skill)
            return $"skill '{m_Skill}'";
        }

        public override string Serialize()
        {
            return DoSerialize(m_Skill);
        }

        public override string ToString()
        {
            return Language.Format(LocString.UseSkillA1, m_Skill);
        }
    }

    public class ExtCastSpellAction : MacroAction
    {
        private int m_SpellID;
        private Serial m_Book;

        public ExtCastSpellAction(string[] args)
        {
            m_SpellID = Convert.ToInt32(args[1]);
            m_Book = Serial.Parse(args[2]);
        }

        public ExtCastSpellAction(int s, Serial book)
        {
            m_SpellID = s;
            m_Book = book;
        }

        public override bool Perform()
        {
            // Razor CE: m_Spell.OnCast(...) — Spell-Klasse (Timer/Mantras) folgt
            // spaeter; hier nur LastSpell + Paket (Datenpfad werktreu).
            World.Player.LastSpell = m_SpellID;
            ClientProxy.SendToServer(new ExtCastSpell(m_Book, (ushort) m_SpellID));
            return true;
        }

        public override string ToScript()
        {
            return $"cast '{m_SpellID}'";
        }

        public override string Serialize()
        {
            return DoSerialize(m_SpellID, m_Book.Value);
        }

        public override string ToString()
        {
            return Language.Format(LocString.CastSpellA1, m_SpellID);
        }
    }

    public class BookCastSpellAction : MacroAction
    {
        private int m_SpellID;
        private Serial m_Book;

        public BookCastSpellAction(string[] args)
        {
            m_SpellID = Convert.ToInt32(args[1]);
            m_Book = Serial.Parse(args[2]);
        }

        public BookCastSpellAction(int s, Serial book)
        {
            m_SpellID = s;
            m_Book = book;
        }

        public override bool Perform()
        {
            // Razor CE: m_Spell.OnCast(new CastSpellFromBook(...)).
            World.Player.LastSpell = m_SpellID;
            ClientProxy.SendToServer(new CastSpellFromBook(m_Book, (ushort) m_SpellID));
            return true;
        }

        public override string ToScript()
        {
            return $"cast '{m_SpellID}'";
        }

        public override string Serialize()
        {
            return DoSerialize(m_SpellID, m_Book.Value);
        }

        public override string ToString()
        {
            return Language.Format(LocString.CastSpellA1, m_SpellID);
        }
    }

    public class MacroCastSpellAction : MacroAction
    {
        private int m_SpellID;

        public MacroCastSpellAction(string[] args)
        {
            m_SpellID = Convert.ToInt32(args[1]);
        }

        public MacroCastSpellAction(int s)
        {
            m_SpellID = s;
        }

        public override bool Perform()
        {
            // Razor CE: m_Spell.OnCast(new CastSpellFromMacro(...)).
            World.Player.LastSpell = m_SpellID;
            ClientProxy.SendToServer(new CastSpellFromMacro((ushort) m_SpellID));
            return true;
        }

        public override string ToScript()
        {
            return $"cast '{m_SpellID}'";
        }

        public override string Serialize()
        {
            return DoSerialize(m_SpellID);
        }

        public override string ToString()
        {
            return Language.Format(LocString.CastSpellA1, m_SpellID);
        }
    }

    public class SetAbilityAction : MacroAction
    {
        private AOSAbility m_Ability;

        public SetAbilityAction(string[] args)
        {
            m_Ability = (AOSAbility) Convert.ToInt32(args[1]);
        }

        public SetAbilityAction(AOSAbility a)
        {
            m_Ability = a;
        }

        public override bool Perform()
        {
            ClientProxy.SendToServer(new UseAbility(m_Ability));
            return true;
        }

        public override string ToScript()
        {
            return $"setability '{m_Ability}'";
        }

        public override string Serialize()
        {
            return DoSerialize((int) m_Ability);
        }

        public override string ToString()
        {
            return Language.Format(LocString.SetAbilityA1, m_Ability);
        }
    }

    public class DressAction : MacroWaitAction
    {
        private string m_Name;

        public DressAction(string[] args)
        {
            m_Name = args[1];
        }

        public DressAction(string name)
        {
            m_Name = name;
        }

        public override bool Perform()
        {
            DressList list = DressList.Find(m_Name);
            if (list != null)
            {
                list.Dress();
                return false;
            }
            else
            {
                return true;
            }
        }

        public override bool PerformWait()
        {
            return !ActionQueue.Empty;
        }

        public override string ToScript()
        {
            return $"dress '{m_Name}'";
        }

        public override string Serialize()
        {
            return DoSerialize(m_Name);
        }

        public override string ToString()
        {
            return Language.Format(LocString.DressA1, m_Name);
        }
    }

    public class UnDressAction : MacroWaitAction
    {
        private string m_Name;
        private byte m_Layer;

        public UnDressAction(string[] args)
        {
            try
            {
                m_Layer = Convert.ToByte(args[2]);
            }
            catch
            {
                m_Layer = 255;
            }

            if (m_Layer == 255)
                m_Name = args[1];
            else
                m_Name = "";
        }

        public UnDressAction(string name)
        {
            m_Name = name;
            m_Layer = 255;
        }

        public UnDressAction(byte layer)
        {
            m_Layer = layer;
            m_Name = "";
        }

        public override bool Perform()
        {
            if (m_Layer == 255)
            {
                DressList list = DressList.Find(m_Name);
                if (list != null)
                {
                    list.Undress();
                    return false;
                }
                else
                {
                    return true;
                }
            }
            else if (m_Layer == 0)
            {
                // Razor CE: HotKeys.UndressHotKeys.OnUndressAll()
                Dress.UndressAll();
                return false;
            }
            else
            {
                return !Dress.Unequip((Layer) m_Layer);
            }
        }

        public override bool PerformWait()
        {
            return !ActionQueue.Empty;
        }

        public override string ToScript()
        {
            if (m_Layer == 255)
            {
                return $"undress '{m_Name}'";
            }

            return m_Layer == 0 ? "undress" : $"undress '{m_Layer}'";
        }

        public override string Serialize()
        {
            return DoSerialize(m_Name, m_Layer);
        }

        public override string ToString()
        {
            if (m_Layer == 255)
                return Language.Format(LocString.UndressA1, m_Name);
            else if (m_Layer == 0)
                return Language.GetString(LocString.UndressAll);
            else
                return Language.Format(LocString.UndressLayerA1, (Layer) m_Layer);
        }
    }

    public class WalkAction : MacroWaitAction
    {
        private Direction m_Dir;
        private static DateTime m_LastWalk = DateTime.MinValue;

        public static DateTime LastWalkTime
        {
            get { return m_LastWalk; }
            set { m_LastWalk = value; }
        }

        public WalkAction(string[] args)
        {
            m_Dir = (Direction) (Convert.ToByte(args[1])) & Direction.Mask;
        }

        public WalkAction(Direction dir)
        {
            m_Dir = dir & Direction.Mask;
        }

        public override bool Perform()
        {
            return !PerformWait();
        }

        public override bool PerformWait()
        {
            if (m_LastWalk + TimeSpan.FromSeconds(0.4) >= DateTime.UtcNow)
            {
                return true;
            }
            else
            {
                m_LastWalk = DateTime.UtcNow;

                // Razor CE: Client.Instance.RequestMove(m_Dir) — hier ueber den
                // sanktionierten Walker des UOSagas-Clients.
                ClientProxy.RequestMove(m_Dir);
                return false;
            }
        }

        public override string ToScript()
        {
            return m_Dir == Direction.Mask ? $"walk 'Up'" : $"walk '{m_Dir}'";
        }

        public override string Serialize()
        {
            return DoSerialize((byte) m_Dir);
        }

        public override string ToString()
        {
            return Language.Format(LocString.WalkA1, m_Dir != Direction.Mask ? m_Dir.ToString() : "Up");
        }
    }

    public class WaitForMenuAction : MacroWaitAction
    {
        private uint m_MenuID;

        public WaitForMenuAction(uint gid)
        {
            m_MenuID = gid;
        }

        public WaitForMenuAction(string[] args)
        {
            if (args.Length > 1)
                m_MenuID = Convert.ToUInt32(args[1]);

            try
            {
                m_Timeout = TimeSpan.FromSeconds(Convert.ToDouble(args[2]));
            }
            catch
            {
            }
        }

        public override bool Perform()
        {
            return !PerformWait();
        }

        public override bool PerformWait()
        {
            // Razor CE vergleicht hier (vermutlich versehentlich) CurrentGumpI —
            // werktreu uebernommen.
            return !(World.Player.HasMenu && (World.Player.CurrentGumpI == m_MenuID || m_MenuID == 0));
        }

        public override string ToString()
        {
            if (m_MenuID == 0)
                return Language.GetString(LocString.WaitAnyMenu);
            else
                return Language.Format(LocString.WaitMenuA1, m_MenuID);
        }

        public override string ToScript()
        {
            return $"waitformenu {m_MenuID}";
        }

        public override string Serialize()
        {
            return DoSerialize(m_MenuID, m_Timeout.TotalSeconds);
        }

        public override bool CheckMatch(MacroAction a)
        {
            if (a is WaitForMenuAction)
            {
                if (m_MenuID == 0 || ((WaitForMenuAction) a).m_MenuID == m_MenuID)
                    return true;
            }

            return false;
        }
    }

    public class WaitForGumpAction : MacroWaitAction
    {
        private uint m_GumpID;
        private bool m_Strict;

        public WaitForGumpAction()
        {
            m_GumpID = 0;
            m_Strict = false;
        }

        public WaitForGumpAction(uint gid)
        {
            m_GumpID = gid;
            m_Strict = false;
        }

        public WaitForGumpAction(string[] args)
        {
            m_GumpID = Convert.ToUInt32(args[1]);
            try
            {
                m_Strict = Convert.ToBoolean(args[2]);
            }
            catch
            {
                m_Strict = false;
            }

            try
            {
                m_Timeout = TimeSpan.FromSeconds(Convert.ToDouble(args[3]));
            }
            catch
            {
            }
        }

        public override bool Perform()
        {
            return !PerformWait();
        }

        public override bool PerformWait()
        {
            return !((World.Player.HasGump || World.Player.HasCompressedGump) &&
                     (World.Player.CurrentGumpI == m_GumpID || !m_Strict || m_GumpID == 0));
        }

        public override string ToString()
        {
            if (m_GumpID == 0 || !m_Strict)
                return Language.GetString(LocString.WaitAnyGump);
            else
                return Language.Format(LocString.WaitGumpA1, m_GumpID);
        }

        public override string ToScript()
        {
            return $"waitforgump {m_GumpID}";
        }

        public override string Serialize()
        {
            return DoSerialize(m_GumpID, m_Strict, m_Timeout.TotalSeconds);
        }

        public override bool CheckMatch(MacroAction a)
        {
            if (a is WaitForGumpAction)
            {
                if (m_GumpID == 0 || ((WaitForGumpAction) a).m_GumpID == m_GumpID)
                    return true;
            }

            return false;
        }
    }

    public class WaitForTargetAction : MacroWaitAction
    {
        public WaitForTargetAction()
        {
            m_Timeout = TimeSpan.FromSeconds(30.0);
        }

        public WaitForTargetAction(string[] args)
        {
            try
            {
                m_Timeout = TimeSpan.FromSeconds(Convert.ToDouble(args[1]));
            }
            catch
            {
                m_Timeout = TimeSpan.FromSeconds(30.0);
            }
        }

        public override bool Perform()
        {
            return !PerformWait();
        }

        public override bool PerformWait()
        {
            return !Targeting.HasTarget;
        }

        public override string ToString()
        {
            return Language.GetString(LocString.WaitTarg);
        }

        public override string ToScript()
        {
            return $"waitfortarget";
        }

        public override string Serialize()
        {
            return DoSerialize(m_Timeout.TotalSeconds);
        }

        public override bool CheckMatch(MacroAction a)
        {
            return (a is WaitForTargetAction);
        }
    }

    public class PauseAction : MacroWaitAction
    {
        public PauseAction(string[] args)
        {
            m_Timeout = TimeSpan.Parse(args[1]);
        }

        public PauseAction(int ms)
        {
            m_Timeout = TimeSpan.FromMilliseconds(ms);
        }

        public PauseAction(TimeSpan time)
        {
            m_Timeout = time;
        }

        public override string ToScript()
        {
            return $"wait {m_Timeout.TotalMilliseconds}";
        }

        public override string Serialize()
        {
            return DoSerialize(m_Timeout);
        }

        public override bool Perform()
        {
            this.StartTime = DateTime.UtcNow;
            return !PerformWait();
        }

        public override bool PerformWait()
        {
            return (StartTime + m_Timeout >= DateTime.UtcNow);
        }

        public override string ToString()
        {
            return Language.Format(LocString.PauseA1, m_Timeout.TotalSeconds);
        }
    }

    public class WaitForStatAction : MacroWaitAction
    {
        private byte m_Direction;
        private int m_Value;
        private IfAction.IfVarType m_Stat;

        public byte Op
        {
            get { return m_Direction; }
        }

        public int Amount
        {
            get { return m_Value; }
        }

        public IfAction.IfVarType Stat
        {
            get { return m_Stat; }
        }

        public WaitForStatAction(string[] args)
        {
            m_Stat = (IfAction.IfVarType) Convert.ToInt32(args[1]);
            m_Direction = Convert.ToByte(args[2]);
            m_Value = Convert.ToInt32(args[3]);

            try
            {
                m_Timeout = TimeSpan.FromSeconds(Convert.ToDouble(args[4]));
            }
            catch
            {
                m_Timeout = TimeSpan.FromMinutes(60.0);
            }
        }

        public WaitForStatAction(IfAction.IfVarType stat, byte dir, int val)
        {
            m_Stat = stat;
            m_Direction = dir;
            m_Value = val;

            m_Timeout = TimeSpan.FromMinutes(60.0);
        }

        public override bool Perform()
        {
            return !PerformWait();
        }

        public override bool PerformWait()
        {
            if (m_Direction > 0)
            {
                // wait for m_Stat >= m_Value
                switch (m_Stat)
                {
                    case IfAction.IfVarType.Hits:
                        return World.Player.Hits < m_Value;
                    case IfAction.IfVarType.Mana:
                        return World.Player.Mana < m_Value;
                    case IfAction.IfVarType.Stamina:
                        return World.Player.Stam < m_Value;
                }
            }
            else
            {
                // wait for m_Stat <= m_Value
                switch (m_Stat)
                {
                    case IfAction.IfVarType.Hits:
                        return World.Player.Hits > m_Value;
                    case IfAction.IfVarType.Mana:
                        return World.Player.Mana > m_Value;
                    case IfAction.IfVarType.Stamina:
                        return World.Player.Stam > m_Value;
                }
            }

            return false;
        }

        public override string ToScript()
        {
            string op = m_Direction > 0 ? ">=" : "<=";
            string stat = "unknown";

            switch (m_Stat)
            {
                case IfAction.IfVarType.Hits:
                    stat = "hits";
                    break;
                case IfAction.IfVarType.Mana:
                    stat = "mana";
                    break;
                case IfAction.IfVarType.Stamina:
                    stat = "stam";
                    break;
            }

            return $"if {stat} {op} {m_Value}";
        }

        public override string Serialize()
        {
            return DoSerialize((int) m_Stat, m_Direction, m_Value, m_Timeout.TotalSeconds);
        }

        public override string ToString()
        {
            return Language.Format(LocString.WaitA3, m_Stat, m_Direction > 0 ? ">=" : "<=", m_Value);
        }
    }

    public class IfAction : MacroAction
    {
        public enum IfVarType : int
        {
            Hits = 0,
            Mana,
            Stamina,
            Poisoned,
            SysMessage,
            Weight,
            Mounted,
            RHandEmpty,
            LHandEmpty,

            BeginCountersMarker,

            Counter = 50,
            Skill = 100
        }

        // 0 <=,1 >=,2 <,3 >
        private sbyte m_Direction;
        private object m_Value;
        private IfVarType m_Var;
        private string m_Counter;
        private Assistant.Counter m_CountObj;
        private int m_SkillId = -1;

        public sbyte Op
        {
            get { return m_Direction; }
        }

        public object Value
        {
            get { return m_Value; }
        }

        public IfVarType Variable
        {
            get { return m_Var; }
        }

        public string Counter
        {
            get { return m_Counter; }
        }

        public int SkillId
        {
            get { return m_SkillId; }
        }

        public IfAction(string[] args)
        {
            m_Var = (IfVarType) Convert.ToInt32(args[1]);
            try
            {
                m_Direction = Convert.ToSByte(args[2]);
                if (m_Direction > 3)
                    m_Direction = 0;
            }
            catch
            {
                m_Direction = -1;
            }

            if (m_Var == IfVarType.SysMessage)
            {
                m_Value = args[3].ToLower();
            }
            else if (m_Var == IfVarType.Skill)
            {
                if (args[3] is string strVal)
                {
                    m_Value = strVal;
                }
                else
                {
                    m_Value = Convert.ToDouble(args[3]);
                }
            }
            else
            {
                if (args[3] is string strVal)
                {
                    m_Value = strVal;
                }
                else
                {
                    m_Value = Convert.ToInt32(args[3]);
                }
            }

            if (m_Var == IfVarType.Counter)
                m_Counter = args[4];

            if (m_Var == IfVarType.Skill)
                m_SkillId = Convert.ToInt32(args[4]);
        }

        public IfAction(IfVarType var, sbyte dir, int val)
        {
            m_Var = var;
            m_Direction = dir;
            m_Value = val;
        }

        public IfAction(IfVarType var, sbyte dir, string val)
        {
            m_Var = var;
            m_Direction = dir;
            m_Value = val;
        }

        public IfAction(IfVarType var, sbyte dir, int val, string counter)
        {
            m_Var = var;
            m_Direction = dir;
            m_Value = val;
            m_Counter = counter;
        }

        public IfAction(IfVarType var, sbyte dir, double val, int skillId)
        {
            m_Var = var;
            m_Direction = dir;
            m_Value = val;
            m_SkillId = skillId;
        }

        public IfAction(IfVarType var, string text)
        {
            m_Var = var;
            m_Value = text.ToLower();
        }

        public override bool Perform()
        {
            return true;
        }

        public bool Evaluate()
        {
            switch (m_Var)
            {
                case IfVarType.Hits:
                case IfVarType.Mana:
                case IfVarType.Stamina:
                case IfVarType.Weight:
                {
                    bool isNumeric = true;
                    int val;

                    if (m_Value is string value)
                    {
                        isNumeric = int.TryParse(value, out val);
                    }
                    else
                    {
                        val = Convert.ToInt32(m_Value);
                    }

                    if (!isNumeric && m_Value is string strVal)
                    {
                        if (strVal.Equals("{maxhp}"))
                        {
                            val = World.Player.HitsMax;
                        }
                        else if (strVal.Equals("{maxstam}"))
                        {
                            val = World.Player.StamMax;
                        }
                        else if (strVal.Equals("{maxmana}"))
                        {
                            val = World.Player.ManaMax;
                        }
                        else
                        {
                            val = 0;
                        }
                    }

                    switch (m_Direction)
                    {
                        case 0:
                            // if stat <= m_Value
                            switch (m_Var)
                            {
                                case IfVarType.Hits:
                                    return World.Player.Hits <= val;
                                case IfVarType.Mana:
                                    return World.Player.Mana <= val;
                                case IfVarType.Stamina:
                                    return World.Player.Stam <= val;
                                case IfVarType.Weight:
                                    return World.Player.Weight <= val;
                            }

                            break;
                        case 1:
                            // if stat >= m_Value
                            switch (m_Var)
                            {
                                case IfVarType.Hits:
                                    return World.Player.Hits >= val;
                                case IfVarType.Mana:
                                    return World.Player.Mana >= val;
                                case IfVarType.Stamina:
                                    return World.Player.Stam >= val;
                                case IfVarType.Weight:
                                    return World.Player.Weight >= val;
                            }

                            break;
                        case 2:
                            // if stat < m_Value
                            switch (m_Var)
                            {
                                case IfVarType.Hits:
                                    return World.Player.Hits < val;
                                case IfVarType.Mana:
                                    return World.Player.Mana < val;
                                case IfVarType.Stamina:
                                    return World.Player.Stam < val;
                                case IfVarType.Weight:
                                    return World.Player.Weight < val;
                            }

                            break;
                        case 3:
                            // if stat > m_Value
                            switch (m_Var)
                            {
                                case IfVarType.Hits:
                                    return World.Player.Hits > val;
                                case IfVarType.Mana:
                                    return World.Player.Mana > val;
                                case IfVarType.Stamina:
                                    return World.Player.Stam > val;
                                case IfVarType.Weight:
                                    return World.Player.Weight > val;
                            }

                            break;
                    }

                    return false;
                }

                case IfVarType.Poisoned:
                {
                    // Razor CE: zusaetzlich FeatureBit.BlockHealPoisoned.
                    return World.Player.Poisoned;
                }

                case IfVarType.SysMessage:
                {
                    string text = (string) m_Value;

                    return SystemMessages.Exists(text);
                }

                case IfVarType.Mounted:
                {
                    return World.Player.GetItemOnLayer(Layer.Mount) != null;
                }

                case IfVarType.RHandEmpty:
                {
                    return World.Player.GetItemOnLayer(Layer.RightHand) == null;
                }

                case IfVarType.LHandEmpty:
                {
                    return World.Player.GetItemOnLayer(Layer.LeftHand) == null;
                }

                case IfVarType.Skill:

                    double skillValToCompare;

                    if (m_Value is string skillVal)
                    {
                        double.TryParse(skillVal, out skillValToCompare);
                    }
                    else
                    {
                        skillValToCompare = Convert.ToDouble(m_Value);
                    }

                    switch (m_Direction)
                    {
                        case 0:
                            return World.Player.Skills[m_SkillId].Value <= skillValToCompare;
                        case 1:
                            return World.Player.Skills[m_SkillId].Value >= skillValToCompare;
                        case 2:
                            return World.Player.Skills[m_SkillId].Value < skillValToCompare;
                        case 3:
                            return World.Player.Skills[m_SkillId].Value > skillValToCompare;
                        default:
                            return World.Player.Skills[m_SkillId].Value <= skillValToCompare;
                    }

                case IfVarType.Counter:
                {
                    if (m_CountObj == null)
                    {
                        foreach (Assistant.Counter c in Assistant.Counter.List)
                        {
                            if (c.Name == m_Counter)
                            {
                                m_CountObj = c;
                                break;
                            }
                        }
                    }

                    if (m_CountObj == null || !m_CountObj.Enabled)
                        return false;

                    int val;

                    if (m_Value is string value)
                    {
                        int.TryParse(value, out val);
                    }
                    else
                    {
                        val = Convert.ToInt32(m_Value);
                    }

                    switch (m_Direction)
                    {
                        case 0:
                            return m_CountObj.Amount <= val;
                        case 1:
                            return m_CountObj.Amount >= val;
                        case 2:
                            return m_CountObj.Amount < val;
                        case 3:
                            return m_CountObj.Amount > val;
                        default:
                            return m_CountObj.Amount <= val;
                    }
                }

                default:
                    return false;
            }
        }

        public override string ToScript()
        {
            string op = "??";
            string expression = "unknown";

            bool useValue = true;

            switch (m_Direction)
            {
                case 0:
                    op = "<=";
                    break;
                case 1:
                    op = ">=";
                    break;
                case 2:
                    op = "<";
                    break;
                case 3:
                    op = ">";
                    break;
            }

            switch (m_Var)
            {
                case IfAction.IfVarType.Hits:
                    expression = "hits";
                    break;
                case IfAction.IfVarType.Mana:
                    expression = "mana";
                    break;
                case IfAction.IfVarType.Stamina:
                    expression = "stam";
                    break;
                case IfVarType.Poisoned:
                    expression = "poisoned";
                    break;
                case IfVarType.SysMessage:
                    expression = $"insysmsg '{m_Value}'";
                    useValue = false;
                    break;
                case IfVarType.Weight:
                    expression = "weight";
                    break;
                case IfVarType.Mounted:
                    expression = "mounted";
                    break;
                case IfVarType.RHandEmpty:
                    expression = "rhandempty";
                    break;
                case IfVarType.LHandEmpty:
                    expression = "lhandempty";
                    break;
                case IfVarType.Counter:
                    expression = $"count '{m_Counter}'";
                    break;
                case IfVarType.Skill:
                    expression = $"skill '{m_SkillId}'";
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            return useValue ? $"if {expression} {op} {m_Value}" : $"if {expression}";
        }

        public override string Serialize()
        {
            if (m_Var == IfVarType.Counter && m_Counter != null)
                return DoSerialize((int) m_Var, m_Direction, m_Value, m_Counter);
            else if (m_Var == IfVarType.Skill && m_SkillId != -1)
                return DoSerialize((int) m_Var, m_Direction, m_Value, m_SkillId);
            else
                return DoSerialize((int) m_Var, m_Direction, m_Value);
        }

        private string DirectionString()
        {
            switch (m_Direction)
            {
                case 0:
                    return "<=";
                case 1:
                    return ">=";
                case 2:
                    return "<";
                case 3:
                    return ">";
                default:
                    return "<=";
            }
        }

        public override string ToString()
        {
            switch (m_Var)
            {
                case IfVarType.Hits:
                case IfVarType.Mana:
                case IfVarType.Stamina:
                case IfVarType.Weight:
                    return $"If ( {m_Var} {DirectionString()} {m_Value} )";
                case IfVarType.Poisoned:
                    return "If ( Poisoned )";
                case IfVarType.SysMessage:
                {
                    string str = (string) m_Value;
                    if (str.Length > 10)
                        str = str.Substring(0, 7) + "...";
                    return $"If ( SysMessage \"{str}\" )";
                }

                case IfVarType.Skill:
                    return $"If ( \"{m_SkillId}\" {DirectionString()} {m_Value})";
                case IfVarType.Mounted:
                    return "If ( Mounted )";
                case IfVarType.RHandEmpty:
                    return "If ( R-Hand Empty )";
                case IfVarType.LHandEmpty:
                    return "If ( L-Hand Empty )";
                case IfVarType.Counter:
                    return $"If ( \"{m_Counter} count\" {DirectionString()} {m_Value} )";
                default:
                    return "If ( ??? )";
            }
        }
    }

    public class ElseAction : MacroAction
    {
        public ElseAction()
        {
        }

        public override bool Perform()
        {
            return true;
        }

        public override string ToString()
        {
            return "Else";
        }

        public override string ToScript()
        {
            return "else";
        }
    }

    public class EndIfAction : MacroAction
    {
        public EndIfAction()
        {
        }

        public override bool Perform()
        {
            return true;
        }

        public override string ToString()
        {
            return "End If";
        }

        public override string ToScript()
        {
            return "endif";
        }
    }

    public class HotKeyAction : MacroAction
    {
        // Datenhalter-Port: Original haelt KeyData (HotKey-System, Phase 2b).
        // Hier werden LocName (int) + StrName roh konserviert.
        private int m_LocName;
        private string m_StrName;

        public HotKeyAction(string[] args)
        {
            try
            {
                m_LocName = Convert.ToInt32(args[1]);
            }
            catch
            {
                m_LocName = 0;
            }

            m_StrName = args.Length > 2 ? args[2] : "";
        }

        public HotKeyAction(int locName, string strName)
        {
            m_LocName = locName;
            m_StrName = strName;
        }

        public int LocName
        {
            get { return m_LocName; }
        }

        public string StrName
        {
            get { return m_StrName; }
        }

        public string DisplayName
        {
            get { return m_LocName != 0 ? Language.GetString(m_LocName) : m_StrName; }
        }

        public override bool Perform()
        {
            // Razor CE: m_Key.Callback() — Hotkey ueber das HotKey-System
            // aufloesen (Phase 3c) und ausfuehren.
            KeyData kd = m_LocName != 0 ? HotKey.Get(m_LocName) : HotKey.Get(m_StrName);
            if (kd != null)
                kd.Callback();
            else
                Console.WriteLine($"[Razor] HotKeyAction '{DisplayName}' nicht gefunden (nicht registriert).");

            return true;
        }

        public override string ToScript()
        {
            return $"hotkey '{DisplayName}'";
        }

        public override string Serialize()
        {
            return DoSerialize(m_LocName, m_StrName == null ? "" : m_StrName);
        }

        public override string ToString()
        {
            return $"Exec: {DisplayName}";
        }
    }

    public class ForAction : MacroAction
    {
        private int m_Max, m_Count;

        public int Count
        {
            get { return m_Count; }
            set { m_Count = value; }
        }

        public int Max
        {
            get { return m_Max; }
        }

        public ForAction(string[] args)
        {
            m_Max = Convert.ToInt32(args[1]);
        }

        public ForAction(int max)
        {
            m_Max = max;
        }

        public override string ToScript()
        {
            return $"for {m_Max}";
        }

        public override string Serialize()
        {
            return DoSerialize(m_Max);
        }

        public override bool Perform()
        {
            return true;
        }

        public override string ToString()
        {
            return $"For ( 1 to {m_Max} )";
        }
    }

    public class EndForAction : MacroAction
    {
        public EndForAction()
        {
        }

        public override bool Perform()
        {
            return true;
        }

        public override string ToString()
        {
            return "End For";
        }

        public override string ToScript()
        {
            return "endfor";
        }
    }

    public class WhileAction : MacroAction
    {
        public enum WhileVarType : int
        {
            Hits = 0,
            Mana,
            Stamina,
            Poisoned,
            SysMessage,
            Weight,
            Mounted,
            RHandEmpty,
            LHandEmpty,

            BeginCountersMarker,

            Counter = 50,
            Skill = 100
        }

        // 0 <=,1 >=,2 <,3 >
        private sbyte m_Direction;
        private object m_Value;
        private WhileVarType m_Var;
        private string m_Counter;
        private Assistant.Counter m_CountObj;
        private int m_SkillId = -1;

        public sbyte Op
        {
            get { return m_Direction; }
        }

        public object Value
        {
            get { return m_Value; }
        }

        public WhileVarType Variable
        {
            get { return m_Var; }
        }

        public string Counter
        {
            get { return m_Counter; }
        }

        public int SkillId
        {
            get { return m_SkillId; }
        }

        public WhileAction(string[] args)
        {
            m_Var = (WhileVarType) Convert.ToInt32(args[1]);
            try
            {
                m_Direction = Convert.ToSByte(args[2]);
                if (m_Direction > 3)
                    m_Direction = 0;
            }
            catch
            {
                m_Direction = -1;
            }

            if (m_Var == WhileVarType.SysMessage)
            {
                m_Value = args[3].ToLower();
            }
            else if (m_Var == WhileVarType.Skill)
            {
                if (args[3] is string strVal)
                {
                    m_Value = strVal;
                }
                else
                {
                    m_Value = Convert.ToDouble(args[3]);
                }
            }
            else
            {
                if (args[3] is string strVal)
                {
                    m_Value = strVal;
                }
                else
                {
                    m_Value = Convert.ToInt32(args[3]);
                }
            }

            if (m_Var == WhileVarType.Counter)
                m_Counter = args[4];

            if (m_Var == WhileVarType.Skill)
                m_SkillId = Convert.ToInt32(args[4]);
        }

        public WhileAction(WhileVarType var, sbyte dir, int val)
        {
            m_Var = var;
            m_Direction = dir;
            m_Value = val;
        }

        public WhileAction(WhileVarType var, sbyte dir, string val)
        {
            m_Var = var;
            m_Direction = dir;
            m_Value = val;
        }

        public WhileAction(WhileVarType var, sbyte dir, int val, string counter)
        {
            m_Var = var;
            m_Direction = dir;
            m_Value = val;
            m_Counter = counter;
        }

        public WhileAction(WhileVarType var, sbyte dir, double val, int skillId)
        {
            m_Var = var;
            m_Direction = dir;
            m_Value = val;
            m_SkillId = skillId;
        }

        public WhileAction(WhileVarType var, string text)
        {
            m_Var = var;
            m_Value = text.ToLower();
        }

        public override bool Perform()
        {
            return true;
        }

        public bool Evaluate()
        {
            switch (m_Var)
            {
                case WhileVarType.Hits:
                case WhileVarType.Mana:
                case WhileVarType.Stamina:
                case WhileVarType.Weight:
                {
                    int val;

                    if (m_Value is string value)
                    {
                        int.TryParse(value, out val);
                    }
                    else
                    {
                        val = Convert.ToInt32(m_Value);
                    }

                    switch (m_Direction)
                    {
                        case 0:
                            // while stat <= m_Value
                            switch (m_Var)
                            {
                                case WhileVarType.Hits:
                                    return World.Player.Hits <= val;
                                case WhileVarType.Mana:
                                    return World.Player.Mana <= val;
                                case WhileVarType.Stamina:
                                    return World.Player.Stam <= val;
                                case WhileVarType.Weight:
                                    return World.Player.Weight <= val;
                            }

                            break;
                        case 1:
                            // while stat >= m_Value
                            switch (m_Var)
                            {
                                case WhileVarType.Hits:
                                    return World.Player.Hits >= val;
                                case WhileVarType.Mana:
                                    return World.Player.Mana >= val;
                                case WhileVarType.Stamina:
                                    return World.Player.Stam >= val;
                                case WhileVarType.Weight:
                                    return World.Player.Weight >= val;
                            }

                            break;
                        case 2:
                            // while stat < m_Value
                            switch (m_Var)
                            {
                                case WhileVarType.Hits:
                                    return World.Player.Hits < val;
                                case WhileVarType.Mana:
                                    return World.Player.Mana < val;
                                case WhileVarType.Stamina:
                                    return World.Player.Stam < val;
                                case WhileVarType.Weight:
                                    return World.Player.Weight < val;
                            }

                            break;
                        case 3:
                            // while stat > m_Value
                            switch (m_Var)
                            {
                                case WhileVarType.Hits:
                                    return World.Player.Hits > val;
                                case WhileVarType.Mana:
                                    return World.Player.Mana > val;
                                case WhileVarType.Stamina:
                                    return World.Player.Stam > val;
                                case WhileVarType.Weight:
                                    return World.Player.Weight > val;
                            }

                            break;
                    }

                    return false;
                }

                case WhileVarType.Poisoned:
                {
                    return World.Player.Poisoned;
                }

                case WhileVarType.SysMessage:
                {
                    string text = (string) m_Value;

                    return SystemMessages.Exists(text);
                }

                case WhileVarType.Mounted:
                {
                    return World.Player.GetItemOnLayer(Layer.Mount) != null;
                }

                case WhileVarType.RHandEmpty:
                {
                    return World.Player.GetItemOnLayer(Layer.RightHand) == null;
                }

                case WhileVarType.LHandEmpty:
                {
                    return World.Player.GetItemOnLayer(Layer.LeftHand) == null;
                }

                case WhileVarType.Skill:

                    double skillValToCompare;

                    if (m_Value is string skillVal)
                    {
                        double.TryParse(skillVal, out skillValToCompare);
                    }
                    else
                    {
                        skillValToCompare = Convert.ToDouble(m_Value);
                    }

                    switch (m_Direction)
                    {
                        case 0:
                            return World.Player.Skills[m_SkillId].Value <= skillValToCompare;
                        case 1:
                            return World.Player.Skills[m_SkillId].Value >= skillValToCompare;
                        case 2:
                            return World.Player.Skills[m_SkillId].Value < skillValToCompare;
                        case 3:
                            return World.Player.Skills[m_SkillId].Value > skillValToCompare;
                        default:
                            return World.Player.Skills[m_SkillId].Value <= skillValToCompare;
                    }

                case WhileVarType.Counter:
                {
                    if (m_CountObj == null)
                    {
                        foreach (Assistant.Counter c in Assistant.Counter.List)
                        {
                            if (c.Name == m_Counter)
                            {
                                m_CountObj = c;
                                break;
                            }
                        }
                    }

                    if (m_CountObj == null || !m_CountObj.Enabled)
                        return false;

                    int val;

                    if (m_Value is string value)
                    {
                        int.TryParse(value, out val);
                    }
                    else
                    {
                        val = Convert.ToInt32(m_Value);
                    }

                    switch (m_Direction)
                    {
                        case 0:
                            return m_CountObj.Amount <= val;
                        case 1:
                            return m_CountObj.Amount >= val;
                        case 2:
                            return m_CountObj.Amount < val;
                        case 3:
                            return m_CountObj.Amount > val;
                        default:
                            return m_CountObj.Amount <= val;
                    }
                }

                default:
                    return false;
            }
        }

        public override string ToScript()
        {
            string op = "??";
            string expression = "unknown";

            bool useValue = true;

            switch (m_Direction)
            {
                case 0:
                    op = "<=";
                    break;
                case 1:
                    op = ">=";
                    break;
                case 2:
                    op = "<";
                    break;
                case 3:
                    op = ">";
                    break;
            }

            switch (m_Var)
            {
                case WhileVarType.Hits:
                    expression = "hits";
                    break;
                case WhileVarType.Mana:
                    expression = "mana";
                    break;
                case WhileVarType.Stamina:
                    expression = "stam";
                    break;
                case WhileVarType.Poisoned:
                    expression = "poisoned";
                    break;
                case WhileVarType.SysMessage:
                    expression = $"insysmsg '{m_Value}'";
                    useValue = false;
                    break;
                case WhileVarType.Weight:
                    expression = "weight";
                    break;
                case WhileVarType.Mounted:
                    expression = "mounted";
                    break;
                case WhileVarType.RHandEmpty:
                    expression = "rhandempty";
                    break;
                case WhileVarType.LHandEmpty:
                    expression = "lhandempty";
                    break;
                case WhileVarType.Counter:
                    expression = $"count '{m_Counter}'";
                    break;
                case WhileVarType.Skill:
                    expression = $"skill '{m_SkillId}'";
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            return useValue ? $"while {expression} {op} {m_Value}" : $"while {expression}";
        }

        public override string Serialize()
        {
            if (m_Var == WhileVarType.Counter && m_Counter != null)
                return DoSerialize((int) m_Var, m_Direction, m_Value, m_Counter);
            else if (m_Var == WhileVarType.Skill && m_SkillId != -1)
                return DoSerialize((int) m_Var, m_Direction, m_Value, m_SkillId);
            else
                return DoSerialize((int) m_Var, m_Direction, m_Value);
        }

        private string DirectionString()
        {
            switch (m_Direction)
            {
                case 0:
                    return "<=";
                case 1:
                    return ">=";
                case 2:
                    return "<";
                case 3:
                    return ">";
                default:
                    return "<=";
            }
        }

        public override string ToString()
        {
            switch (m_Var)
            {
                case WhileVarType.Hits:
                case WhileVarType.Mana:
                case WhileVarType.Stamina:
                case WhileVarType.Weight:
                    return $"While ( {m_Var} {DirectionString()} {m_Value} )";
                case WhileVarType.Poisoned:
                    return "While ( Poisoned )";
                case WhileVarType.SysMessage:
                {
                    string str = (string) m_Value;
                    if (str.Length > 10)
                        str = str.Substring(0, 7) + "...";
                    return $"While ( SysMessage \"{str}\" )";
                }

                case WhileVarType.Skill:
                    return $"While ( \"{m_SkillId}\" {DirectionString()} {m_Value})";
                case WhileVarType.Mounted:
                    return "While ( Mounted )";
                case WhileVarType.RHandEmpty:
                    return "While ( R-Hand Empty )";
                case WhileVarType.LHandEmpty:
                    return "While ( L-Hand Empty )";
                case WhileVarType.Counter:
                    return $"While ( \"{m_Counter} count\" {DirectionString()} {m_Value} )";
                default:
                    return "While ( ??? )";
            }
        }
    }

    public class EndWhileAction : MacroAction
    {
        public EndWhileAction()
        {
        }

        public override bool Perform()
        {
            return true;
        }

        public override string ToString()
        {
            return "End While";
        }

        public override string ToScript()
        {
            return "endwhile";
        }
    }

    public class StartDoWhileAction : MacroAction
    {
        public StartDoWhileAction()
        {
        }

        public override bool Perform()
        {
            return true;
        }

        public override string ToString()
        {
            return "Do";
        }

        public override string ToScript()
        {
            return "# do-while not implemented, use while";
        }
    }

    public class DoWhileAction : MacroAction
    {
        public enum DoWhileVarType : int
        {
            Hits = 0,
            Mana,
            Stamina,
            Poisoned,
            SysMessage,
            Weight,
            Mounted,
            RHandEmpty,
            LHandEmpty,

            BeginCountersMarker,

            Counter = 50,
            Skill = 100
        }

        // 0 <=,1 >=,2 <,3 >
        private sbyte m_Direction;
        private object m_Value;
        private DoWhileVarType m_Var;
        private string m_Counter;
        private Assistant.Counter m_CountObj;
        private int m_SkillId = -1;

        public sbyte Op
        {
            get { return m_Direction; }
        }

        public object Value
        {
            get { return m_Value; }
        }

        public DoWhileVarType Variable
        {
            get { return m_Var; }
        }

        public string Counter
        {
            get { return m_Counter; }
        }

        public int SkillId
        {
            get { return m_SkillId; }
        }

        public DoWhileAction(string[] args)
        {
            m_Var = (DoWhileVarType) Convert.ToInt32(args[1]);
            try
            {
                m_Direction = Convert.ToSByte(args[2]);
                if (m_Direction > 3)
                    m_Direction = 0;
            }
            catch
            {
                m_Direction = -1;
            }

            if (m_Var == DoWhileVarType.SysMessage)
            {
                m_Value = args[3].ToLower();
            }
            else if (m_Var == DoWhileVarType.Skill)
            {
                if (args[3] is string strVal)
                {
                    m_Value = strVal;
                }
                else
                {
                    m_Value = Convert.ToDouble(args[3]);
                }
            }
            else
            {
                if (args[3] is string strVal)
                {
                    m_Value = strVal;
                }
                else
                {
                    m_Value = Convert.ToInt32(args[3]);
                }
            }

            if (m_Var == DoWhileVarType.Counter)
                m_Counter = args[4];

            if (m_Var == DoWhileVarType.Skill)
                m_SkillId = Convert.ToInt32(args[4]);
        }

        public DoWhileAction(DoWhileVarType var, sbyte dir, int val)
        {
            m_Var = var;
            m_Direction = dir;
            m_Value = val;
        }

        public DoWhileAction(DoWhileVarType var, sbyte dir, string val)
        {
            m_Var = var;
            m_Direction = dir;
            m_Value = val;
        }

        public DoWhileAction(DoWhileVarType var, sbyte dir, int val, string counter)
        {
            m_Var = var;
            m_Direction = dir;
            m_Value = val;
            m_Counter = counter;
        }

        public DoWhileAction(DoWhileVarType var, sbyte dir, double val, int skillId)
        {
            m_Var = var;
            m_Direction = dir;
            m_Value = val;
            m_SkillId = skillId;
        }

        public DoWhileAction(DoWhileVarType var, string text)
        {
            m_Var = var;
            m_Value = text.ToLower();
        }

        public override bool Perform()
        {
            return true;
        }

        public bool Evaluate()
        {
            switch (m_Var)
            {
                case DoWhileVarType.Hits:
                case DoWhileVarType.Mana:
                case DoWhileVarType.Stamina:
                case DoWhileVarType.Weight:
                {
                    int val;

                    if (m_Value is string value)
                    {
                        int.TryParse(value, out val);
                    }
                    else
                    {
                        val = Convert.ToInt32(m_Value);
                    }

                    switch (m_Direction)
                    {
                        case 0:
                            // do-while stat <= m_Value
                            switch (m_Var)
                            {
                                case DoWhileVarType.Hits:
                                    return World.Player.Hits <= val;
                                case DoWhileVarType.Mana:
                                    return World.Player.Mana <= val;
                                case DoWhileVarType.Stamina:
                                    return World.Player.Stam <= val;
                                case DoWhileVarType.Weight:
                                    return World.Player.Weight <= val;
                            }

                            break;
                        case 1:
                            // do-while stat >= m_Value
                            switch (m_Var)
                            {
                                case DoWhileVarType.Hits:
                                    return World.Player.Hits >= val;
                                case DoWhileVarType.Mana:
                                    return World.Player.Mana >= val;
                                case DoWhileVarType.Stamina:
                                    return World.Player.Stam >= val;
                                case DoWhileVarType.Weight:
                                    return World.Player.Weight >= val;
                            }

                            break;
                        case 2:
                            // do-while stat < m_Value
                            switch (m_Var)
                            {
                                case DoWhileVarType.Hits:
                                    return World.Player.Hits < val;
                                case DoWhileVarType.Mana:
                                    return World.Player.Mana < val;
                                case DoWhileVarType.Stamina:
                                    return World.Player.Stam < val;
                                case DoWhileVarType.Weight:
                                    return World.Player.Weight < val;
                            }

                            break;
                        case 3:
                            // do-while stat > m_Value
                            switch (m_Var)
                            {
                                case DoWhileVarType.Hits:
                                    return World.Player.Hits > val;
                                case DoWhileVarType.Mana:
                                    return World.Player.Mana > val;
                                case DoWhileVarType.Stamina:
                                    return World.Player.Stam > val;
                                case DoWhileVarType.Weight:
                                    return World.Player.Weight > val;
                            }

                            break;
                    }

                    return false;
                }

                case DoWhileVarType.Poisoned:
                {
                    return World.Player.Poisoned;
                }

                case DoWhileVarType.SysMessage:
                {
                    string text = (string) m_Value;

                    return SystemMessages.Exists(text);
                }

                case DoWhileVarType.Mounted:
                {
                    return World.Player.GetItemOnLayer(Layer.Mount) != null;
                }

                case DoWhileVarType.RHandEmpty:
                {
                    return World.Player.GetItemOnLayer(Layer.RightHand) == null;
                }

                case DoWhileVarType.LHandEmpty:
                {
                    return World.Player.GetItemOnLayer(Layer.LeftHand) == null;
                }

                case DoWhileVarType.Skill:

                    double skillValToCompare;

                    if (m_Value is string skillVal)
                    {
                        double.TryParse(skillVal, out skillValToCompare);
                    }
                    else
                    {
                        skillValToCompare = Convert.ToDouble(m_Value);
                    }

                    switch (m_Direction)
                    {
                        case 0:
                            return World.Player.Skills[m_SkillId].Value <= skillValToCompare;
                        case 1:
                            return World.Player.Skills[m_SkillId].Value >= skillValToCompare;
                        case 2:
                            return World.Player.Skills[m_SkillId].Value < skillValToCompare;
                        case 3:
                            return World.Player.Skills[m_SkillId].Value > skillValToCompare;
                        default:
                            return World.Player.Skills[m_SkillId].Value <= skillValToCompare;
                    }

                case DoWhileVarType.Counter:
                {
                    if (m_CountObj == null)
                    {
                        foreach (Assistant.Counter c in Assistant.Counter.List)
                        {
                            if (c.Name == m_Counter)
                            {
                                m_CountObj = c;
                                break;
                            }
                        }
                    }

                    if (m_CountObj == null || !m_CountObj.Enabled)
                        return false;

                    int val;

                    if (m_Value is string value)
                    {
                        int.TryParse(value, out val);
                    }
                    else
                    {
                        val = Convert.ToInt32(m_Value);
                    }

                    switch (m_Direction)
                    {
                        case 0:
                            return m_CountObj.Amount <= val;
                        case 1:
                            return m_CountObj.Amount >= val;
                        case 2:
                            return m_CountObj.Amount < val;
                        case 3:
                            return m_CountObj.Amount > val;
                        default:
                            return m_CountObj.Amount <= val;
                    }
                }

                default:
                    return false;
            }
        }

        public override string ToScript()
        {
            return "# do-while not implemented, use while";
        }

        public override string Serialize()
        {
            if (m_Var == DoWhileVarType.Counter && m_Counter != null)
                return DoSerialize((int) m_Var, m_Direction, m_Value, m_Counter);
            else if (m_Var == DoWhileVarType.Skill && m_SkillId != -1)
                return DoSerialize((int) m_Var, m_Direction, m_Value, m_SkillId);
            else
                return DoSerialize((int) m_Var, m_Direction, m_Value);
        }

        private string DirectionString()
        {
            switch (m_Direction)
            {
                case 0:
                    return "<=";
                case 1:
                    return ">=";
                case 2:
                    return "<";
                case 3:
                    return ">";
                default:
                    return "<=";
            }
        }

        public override string ToString()
        {
            switch (m_Var)
            {
                case DoWhileVarType.Hits:
                case DoWhileVarType.Mana:
                case DoWhileVarType.Stamina:
                case DoWhileVarType.Weight:
                    return $"Do While ( {m_Var} {DirectionString()} {m_Value} )";
                case DoWhileVarType.Poisoned:
                    return "Do While ( Poisoned )";
                case DoWhileVarType.SysMessage:
                {
                    string str = (string) m_Value;
                    if (str.Length > 10)
                        str = str.Substring(0, 7) + "...";
                    return $"Do While ( SysMessage \"{str}\" )";
                }

                case DoWhileVarType.Skill:
                    return $"Do While ( \"{m_SkillId}\" {DirectionString()} {m_Value})";
                case DoWhileVarType.Mounted:
                    return "Do While ( Mounted )";
                case DoWhileVarType.RHandEmpty:
                    return "Do While ( R-Hand Empty )";
                case DoWhileVarType.LHandEmpty:
                    return "Do While ( L-Hand Empty )";
                case DoWhileVarType.Counter:
                    return $"Do While ( \"{m_Counter} count\" {DirectionString()} {m_Value} )";
                default:
                    return "Do While ( ??? )";
            }
        }
    }

    public class ContextMenuAction : MacroAction
    {
        private ushort m_CtxName;
        private ushort m_Idx;
        private Serial m_Entity;

        public ContextMenuAction(Serial entity, ushort idx, ushort ctxName)
        {
            m_Entity = entity;
            m_Idx = idx;
            m_CtxName = ctxName;
        }

        public ContextMenuAction(string[] args)
        {
            m_Entity = Serial.Parse(args[1]);
            m_Idx = Convert.ToUInt16(args[2]);
            try
            {
                m_CtxName = Convert.ToUInt16(args[3]);
            }
            catch
            {
            }
        }

        public override bool Perform()
        {
            Serial s = m_Entity;

            if (s == Serial.Zero && World.Player != null)
                s = World.Player.Serial;

            ClientProxy.SendToServer(new ContextMenuRequest(s));
            ClientProxy.SendToServer(new ContextMenuResponse(s, m_Idx));
            return true;
        }

        public override string ToScript()
        {
            return $"menu {m_Entity} {m_Idx}";
        }

        public override string Serialize()
        {
            return DoSerialize(m_Entity, m_Idx, m_CtxName);
        }

        public override string ToString()
        {
            string ent;

            if (m_Entity == Serial.Zero)
                ent = "(self)";
            else
                ent = m_Entity.ToString();
            return String.Format("ContextMenu: {1} ({0})", ent, m_Idx);
        }
    }

    public class PromptAction : MacroAction
    {
        private string m_Response;

        public PromptAction(string[] args)
        {
            m_Response = args[1];
        }

        public PromptAction(string response)
        {
            m_Response = response;
        }

        public override bool Perform()
        {
            if (m_Response.Length > 1)
            {
                World.Player.ResponsePrompt(m_Response);

                return true;
            }

            return false;
        }

        public override string ToScript()
        {
            return $"promptresponse '{m_Response}'";
        }

        public override string Serialize()
        {
            return DoSerialize(m_Response);
        }

        public override string ToString()
        {
            return $"PromptAction: {m_Response}";
        }
    }

    public class WaitForPromptAction : MacroWaitAction
    {
        private uint m_PromptID;
        private bool m_Strict;

        public WaitForPromptAction()
        {
            m_PromptID = 0;
            m_Strict = false;
        }

        public WaitForPromptAction(uint gid)
        {
            m_PromptID = gid;
            m_Strict = false;
        }

        public WaitForPromptAction(string[] args)
        {
            m_PromptID = Convert.ToUInt32(args[1]);
            try
            {
                m_Strict = Convert.ToBoolean(args[2]);
            }
            catch
            {
                m_Strict = false;
            }

            try
            {
                m_Timeout = TimeSpan.FromSeconds(Convert.ToDouble(args[3]));
            }
            catch
            {
            }
        }

        public override bool Perform()
        {
            return !PerformWait();
        }

        public override bool PerformWait()
        {
            return !(World.Player.HasPrompt && (World.Player.PromptID == m_PromptID || !m_Strict || m_PromptID == 0));
        }

        public override string ToString()
        {
            if (m_PromptID == 0 || !m_Strict)
                return "Wait For Prompt (Any)";

            return $"Wait For Prompt ({m_PromptID})";
        }

        public override string ToScript()
        {
            return m_PromptID == 0 || !m_Strict ? "waitforprompt" : $"waitforprompt '{m_PromptID}'";
        }

        public override string Serialize()
        {
            return DoSerialize(m_PromptID, m_Strict, m_Timeout.TotalSeconds);
        }

        public override bool CheckMatch(MacroAction a)
        {
            if (a is WaitForGumpAction)
            {
                if (m_PromptID == 0 || ((WaitForPromptAction) a).m_PromptID == m_PromptID)
                    return true;
            }

            return false;
        }
    }
}
