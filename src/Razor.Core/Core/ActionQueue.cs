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

// Portiert aus Razor CE (Razor/Core/ActionQueue.cs): DragDropManager +
// ActionQueue (Objekt-Delay-Warteschlange fuer DoubleClick/Lift/Drop).
// ENTFERNT gegenueber Razor CE (dokumentiert):
//  * Datei-Logging (DragDrop.log), HotKey "DropCurrent"-Registrierung.
// Phase 2d: ScavengerAgent.Uncache bei Out-of-Range-Lifts nachgeruestet.
// Client-Zugriff laeuft ueber ClientProxy statt Client.Instance.

using System;
using System.Collections;
using System.Collections.Generic;

namespace Assistant
{
    public delegate void DropDoneCallback(Serial iser, Serial dser, Point3D newPos);

    public class DragDropManager
    {
        public enum ProcStatus
        {
            Nothing,
            Success,
            KeepWaiting,
            ReQueue
        }

        private class LiftReq
        {
            private static int NextID = 1;

            public LiftReq(Serial s, int a, bool cli, bool last)
            {
                Serial = s;
                Amount = a;
                FromClient = cli;
                DoLast = last;
                Id = NextID++;
            }

            public readonly Serial Serial;
            public readonly int Amount;
            public readonly int Id;
            public readonly bool FromClient;
            public readonly bool DoLast;

            public override string ToString()
            {
                return String.Format("{2}({0},{1},{3},{4})", Serial, Amount, Id, FromClient, DoLast);
            }
        }

        private class DropReq
        {
            public DropReq(Serial s, Point3D pt)
            {
                Serial = s;
                Point = pt;
            }

            public DropReq(Serial s, Layer layer)
            {
                Serial = s;
                Layer = layer;
            }

            public Serial Serial;
            public readonly Point3D Point;
            public readonly Layer Layer;
        }

        public static void DropCurrent()
        {
            if (m_Holding.IsItem)
            {
                if (World.Player.Backpack != null)
                    ClientProxy.SendToServer(new DropRequest(m_Holding, Point3D.MinusOne,
                        World.Player.Backpack.Serial));
                else
                    ClientProxy.SendToServer(new DropRequest(m_Holding, World.Player.Position, Serial.Zero));
            }
            else
            {
                World.Player.SendMessage(MsgLevel.Force, "You are not holding anything.");
            }

            Clear();
        }

        private static int m_LastID;

        private static Serial m_Pending, m_Holding;
        private static Item m_HoldingItem;
        private static bool m_ClientLiftReq = false;
        private static DateTime m_Lifted = DateTime.MinValue;

        private static readonly Dictionary<Serial, Queue<DropReq>>
            m_DropReqs = new Dictionary<Serial, Queue<DropReq>>();

        private static readonly LiftReq[] m_LiftReqs = new LiftReq[256];
        private static byte m_Front, m_Back;

        public static Item Holding
        {
            get { return m_HoldingItem; }
        }

        public static Serial Pending
        {
            get { return m_Pending; }
        }

        public static int LastIDLifted
        {
            get { return m_LastID; }
        }

        public static void Clear()
        {
            m_DropReqs.Clear();
            for (int i = 0; i < 256; i++)
                m_LiftReqs[i] = null;
            m_Front = m_Back = 0;
            m_Holding = m_Pending = Serial.Zero;
            m_HoldingItem = null;
            m_Lifted = DateTime.MinValue;
        }

        public static void DragDrop(Item i, Serial to)
        {
            Drag(i, i.Amount);
            Drop(i, to, Point3D.MinusOne);
        }

        public static void DragDrop(Item i, Item to)
        {
            Drag(i, i.Amount);
            Drop(i, to.Serial, Point3D.MinusOne);
        }

        public static void DragDrop(Item i, Point3D dest)
        {
            Drag(i, i.Amount);
            Drop(i, Serial.MinusOne, dest);
        }

        public static void DragDrop(Item i, int amount, Item to)
        {
            Drag(i, amount);
            Drop(i, to.Serial, Point3D.MinusOne);
        }

        public static void DragDrop(Item i, Mobile to, Layer layer, bool doLast)
        {
            Drag(i, i.Amount, false, doLast);
            Drop(i, to, layer);
        }

        public static void DragDrop(Item i, Mobile to, Layer layer)
        {
            Drag(i, i.Amount, false);
            Drop(i, to, layer);
        }

        public static int Drag(Item i, int amount, bool fromClient)
        {
            return Drag(i, amount, fromClient, false);
        }

        public static int Drag(Item i, int amount)
        {
            return Drag(i, amount, false, false);
        }

        public static bool Empty
        {
            get { return m_Back == m_Front; }
        }

        public static bool Full
        {
            get { return ((byte) (m_Back + 1)) == m_Front; }
        }

        public static int Drag(Item i, int amount, bool fromClient, bool doLast)
        {
            LiftReq lr = new LiftReq(i.Serial, amount, fromClient, doLast);
            LiftReq prev = null;

            if (Full)
            {
                World.Player.SendMessage(MsgLevel.Error, "Drag/Drop queue is full.");
                if (fromClient)
                    ClientProxy.SendToClient(new LiftRej());
                return 0;
            }

            if (m_Back >= m_LiftReqs.Length)
                m_Back = 0;

            if (m_Back <= 0)
                prev = m_LiftReqs[m_LiftReqs.Length - 1];
            else if (m_Back <= m_LiftReqs.Length)
                prev = m_LiftReqs[m_Back - 1];

            // if the current last req must stay last, then insert this one in its place
            if (prev != null && prev.DoLast)
            {
                if (m_Back <= 0)
                    m_LiftReqs[m_LiftReqs.Length - 1] = lr;
                else if (m_Back <= m_LiftReqs.Length)
                    m_LiftReqs[m_Back - 1] = lr;

                // and then re-insert it at the end
                lr = prev;
            }

            m_LiftReqs[m_Back++] = lr;

            ActionQueue.SignalLift(!fromClient);
            return lr.Id;
        }

        public static bool Drop(Item i, Mobile to, Layer layer)
        {
            if (m_Pending == i.Serial)
            {
                ClientProxy.SendToServer(new EquipRequest(i.Serial, to, layer));
                m_Pending = Serial.Zero;
                EndHolding(i.Serial);
                m_Lifted = DateTime.MinValue;
                return true;
            }
            else
            {
                bool add = false;

                for (byte j = m_Front; j != m_Back && !add; j++)
                {
                    if (m_LiftReqs[j] != null && m_LiftReqs[j].Serial == i.Serial)
                    {
                        add = true;
                        break;
                    }
                }

                if (add)
                {
                    if (!m_DropReqs.TryGetValue(i.Serial, out var q) || q == null)
                        m_DropReqs[i.Serial] = q = new Queue<DropReq>();

                    q.Enqueue(new DropReq(to == null ? Serial.Zero : to.Serial, layer));
                    return true;
                }
                else
                {
                    return false;
                }
            }
        }

        public static bool Drop(Item i, Serial dest, Point3D pt)
        {
            if (m_Pending == i.Serial)
            {
                ClientProxy.SendToServer(new DropRequest(i.Serial, pt, dest));
                m_Pending = Serial.Zero;
                EndHolding(i.Serial);
                m_Lifted = DateTime.MinValue;
                return true;
            }
            else
            {
                bool add = false;

                for (byte j = m_Front; j != m_Back && !add; j++)
                {
                    if (m_LiftReqs[j] != null && m_LiftReqs[j].Serial == i.Serial)
                    {
                        add = true;
                        break;
                    }
                }

                if (add)
                {
                    if (!m_DropReqs.TryGetValue(i.Serial, out var q) || q == null)
                        m_DropReqs[i.Serial] = q = new Queue<DropReq>();

                    q.Enqueue(new DropReq(dest, pt));
                    return true;
                }
                else
                {
                    return false;
                }
            }
        }

        public static bool Drop(Item i, Item to, Point3D pt)
        {
            return Drop(i, to == null ? Serial.MinusOne : to.Serial, pt);
        }

        public static bool Drop(Item i, Item to)
        {
            return Drop(i, to.Serial, Point3D.MinusOne);
        }

        public static bool LiftReject()
        {
            if (m_Holding == Serial.Zero)
                return true;

            m_Holding = m_Pending = Serial.Zero;
            m_HoldingItem = null;
            m_Lifted = DateTime.MinValue;

            return m_ClientLiftReq;
        }

        public static bool HasDragFor(Serial s)
        {
            for (byte j = m_Front; j != m_Back; j++)
            {
                if (m_LiftReqs[j] != null && m_LiftReqs[j].Serial == s)
                    return true;
            }

            return false;
        }

        public static bool CancelDragFor(Serial s)
        {
            if (Empty)
                return false;

            int skip = 0;
            for (byte j = m_Front; j != m_Back; j++)
            {
                if (skip == 0 && m_LiftReqs[j] != null && m_LiftReqs[j].Serial == s)
                {
                    m_LiftReqs[j] = null;
                    skip++;
                    if (j == m_Front)
                    {
                        m_Front++;
                        break;
                    }
                    else
                    {
                        m_Back--;
                    }
                }

                if (skip > 0)
                    m_LiftReqs[j] = m_LiftReqs[(byte) (j + skip)];
            }

            if (skip > 0)
            {
                m_LiftReqs[m_Back] = null;
                return true;
            }
            else
            {
                return false;
            }
        }

        public static bool EndHolding(Serial s)
        {
            if (m_Holding == s)
            {
                m_Holding = Serial.Zero;
                m_HoldingItem = null;
            }

            return true;
        }

        private static DropReq DequeueDropFor(Serial s)
        {
            DropReq dr = null;
            if (m_DropReqs.TryGetValue(s, out var q) && q != null)
            {
                if (q.Count > 0)
                    dr = q.Dequeue();
                if (q.Count <= 0)
                    m_DropReqs.Remove(s);
            }

            return dr;
        }

        public static void GracefulStop()
        {
            m_Front = m_Back = 0;

            if (m_Pending.IsValid)
            {
                m_DropReqs.TryGetValue(m_Pending, out var q);
                m_DropReqs.Clear();
                m_DropReqs[m_Pending] = q;
            }
        }

        public static ProcStatus ProcessNext(int numPending)
        {
            if (m_Pending != Serial.Zero)
            {
                if (m_Lifted + TimeSpan.FromMinutes(2) < DateTime.UtcNow)
                {
                    if (World.Player != null)
                    {
                        World.Player.SendMessage(MsgLevel.Force, "Lift timeout, forced drop to pack.");

                        if (World.Player.Backpack != null)
                            ClientProxy.SendToServer(new DropRequest(m_Pending, Point3D.MinusOne,
                                World.Player.Backpack.Serial));
                        else
                            ClientProxy.SendToServer(new DropRequest(m_Pending, World.Player.Position,
                                Serial.Zero));
                    }

                    m_Holding = m_Pending = Serial.Zero;
                    m_HoldingItem = null;
                    m_Lifted = DateTime.MinValue;
                }
                else
                {
                    return ProcStatus.KeepWaiting;
                }
            }

            if (m_Front == m_Back)
            {
                m_Front = m_Back = 0;
                return ProcStatus.Nothing;
            }

            LiftReq lr = m_LiftReqs[m_Front];

            if (numPending > 0 && lr != null && lr.DoLast)
                return ProcStatus.ReQueue;

            m_LiftReqs[m_Front] = null;
            m_Front++;
            if (lr != null)
            {
                Item item = World.FindItem(lr.Serial);
                if (item != null && item.Container == null && World.Player != null)
                {
                    // if the item is on the ground and out of range then dont grab it
                    if (Utility.Distance(item.GetWorldPosition(), World.Player.Position) > 3)
                    {
                        Agents.ScavengerAgent.Instance?.Uncache(item.Serial);
                        return ProcStatus.Nothing;
                    }
                }

                ClientProxy.SendToServer(new LiftRequest(lr.Serial, lr.Amount));

                m_LastID = lr.Id;
                m_Holding = lr.Serial;
                m_HoldingItem = World.FindItem(lr.Serial);
                m_ClientLiftReq = lr.FromClient;

                DropReq dr = DequeueDropFor(lr.Serial);
                if (dr != null)
                {
                    m_Pending = Serial.Zero;
                    EndHolding(lr.Serial);
                    m_Lifted = DateTime.MinValue;

                    if (dr.Serial.IsMobile && dr.Layer > Layer.Invalid && dr.Layer <= Layer.LastUserValid)
                        ClientProxy.SendToServer(new EquipRequest(lr.Serial, dr.Serial, dr.Layer));
                    else
                        ClientProxy.SendToServer(new DropRequest(lr.Serial, dr.Point, dr.Serial));
                }
                else
                {
                    m_Pending = lr.Serial;
                    m_Lifted = DateTime.UtcNow;
                }

                return ProcStatus.Success;
            }
            else
            {
                return ProcStatus.Nothing;
            }
        }
    }

    public class ActionQueue
    {
        private static Serial m_Last = Serial.Zero;
        private static readonly Queue m_Queue = new Queue();
        private static readonly ProcTimer m_Timer = new ProcTimer();
        private static int m_Total = 0;

        public static void DoubleClick(bool silent, Serial s)
        {
            if (s != Serial.Zero)
            {
                if (m_Last != s)
                {
                    m_Queue.Enqueue(s);
                    m_Last = s;
                    m_Total++;
                    if (m_Queue.Count == 1 && !m_Timer.Running)
                        m_Timer.StartMe();
                    else if (!silent && m_Total > 1)
                        World.Player.SendMessage($"Action queued: {m_Queue.Count} ({TimeLeft})");
                }
                else if (!silent)
                {
                    World.Player.SendMessage("Queue ignored (same action).");
                }
            }
        }

        public static void SignalLift(bool silent)
        {
            m_Queue.Enqueue(Serial.Zero);
            m_Total++;
            if (!m_Timer.Running)
                m_Timer.StartMe();
            else if (!silent && m_Total > 1)
                World.Player.SendMessage($"Lift queued: {m_Queue.Count} ({TimeLeft})");
        }

        public static void Stop()
        {
            if (m_Timer != null && m_Timer.Running)
                m_Timer.Stop();
            m_Queue.Clear();
            DragDropManager.Clear();
        }

        public static bool Empty
        {
            get { return m_Queue.Count <= 0 && !m_Timer.Running; }
        }

        public static string TimeLeft
        {
            get
            {
                if (m_Timer.Running)
                {
                    double time = Config.GetInt("ObjectDelay") / 1000.0;

                    if (!Config.GetBool("ObjectDelayEnabled"))
                    {
                        time = 0;
                    }

                    double init = 0;
                    if (m_Timer.LastTick != DateTime.MinValue)
                        init = time - (DateTime.UtcNow - m_Timer.LastTick).TotalSeconds;
                    time = init + time * m_Queue.Count;
                    if (time < 0)
                        time = 0;
                    return $"{time:F1} seconds";
                }
                else
                {
                    return "0.0 seconds";
                }
            }
        }

        private class ProcTimer : Timer
        {
            private DateTime m_StartTime;
            private DateTime m_LastTick;

            public DateTime LastTick
            {
                get { return m_LastTick; }
            }

            public ProcTimer() : base(TimeSpan.Zero, TimeSpan.Zero)
            {
            }

            public void StartMe()
            {
                m_LastTick = DateTime.UtcNow;
                m_StartTime = DateTime.UtcNow;

                OnTick();

                Delay = Interval;

                Start();
            }

            protected override void OnTick()
            {
                ArrayList requeue = null;

                m_LastTick = DateTime.UtcNow;

                if (m_Queue != null && m_Queue.Count > 0)
                {
                    this.Interval =
                        TimeSpan.FromMilliseconds(Config.GetBool("ObjectDelayEnabled")
                            ? Config.GetInt("ObjectDelay")
                            : 0);

                    while (m_Queue.Count > 0)
                    {
                        Serial s = (Serial) m_Queue.Peek();
                        if (s == Serial.Zero) // dragdrop action
                        {
                            DragDropManager.ProcStatus status = DragDropManager.ProcessNext(m_Queue.Count - 1);
                            if (status != DragDropManager.ProcStatus.KeepWaiting)
                            {
                                m_Queue.Dequeue(); // if not waiting then dequeue it

                                if (status == DragDropManager.ProcStatus.ReQueue)
                                    m_Queue.Enqueue(s);
                            }

                            if (status == DragDropManager.ProcStatus.KeepWaiting ||
                                status == DragDropManager.ProcStatus.Success)
                                break; // don't process more if we're waiting or we just processed something
                        }
                        else
                        {
                            m_Queue.Dequeue();
                            ClientProxy.SendToServer(new DoubleClick(s));
                            break;
                        }
                    }

                    if (requeue != null)
                    {
                        for (int i = 0; i < requeue.Count; i++)
                            m_Queue.Enqueue(requeue[i]);
                    }
                }
                else
                {
                    Stop();

                    if (m_Total > 1 && World.Player != null)
                        World.Player.SendMessage(
                            $"Queue finished: {m_Total} actions in {((DateTime.UtcNow - m_StartTime) - this.Interval).TotalSeconds:F1}s");

                    m_Last = Serial.Zero;
                    m_Total = 0;
                }
            }
        }
    }
}
