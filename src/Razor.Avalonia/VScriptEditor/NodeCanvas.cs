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

// UOSagas-Razor: Node-Canvas des VScript-Editors (Phase 5c, UX-Schwung 2026-07).
//
// GEOMETRIE bleibt die des In-Client-Editors (Referenz: VScriptEditorComponent
// bzw. vscript-graph.ts — Node 200 breit, Header 26, Pin-Abstand 25, Pin-
// Radius 6, Grid 16/64; Kategorie-/Pin-Farben identisch): ein Graph muss in
// Client-Editor, Site-Vorschau und Razor-Editor deckungsgleich liegen.
// Der LOOK ist an den Unreal-Blueprint-Editor angelehnt: Gradient-Header,
// Flow-Pins als Chevrons, List-Pins als Raster, goldene Selektion.
//
// Ausfuehrungs-Visualisierung wie im Client: oranger Puls-Trail (1,5 s Fade)
// auf Nodes und Links, weisse Flow-Partikel entlang aktiver Links, gelbe
// Umrandung am Pause-Node (Breakpoint), roter Breakpoint-Punkt oben rechts,
// Pin-Werte werden bei Pause am Node eingeblendet. Fehler: roter Rahmen +
// Fehlertext direkt unter dem Node.
//
// Interaktion: Pan (rechte/mittlere Taste), Zoom (Rad, zum Cursor),
// Marquee-Mehrfachselektion (Aufziehen auf Leerflaeche, Ctrl+Klick toggelt),
// Node-Drag (bewegt die ganze Auswahl), Links aus Output- UND Input-Pins
// ziehen (inkompatible Pins dimmen), Fallenlassen auf Leerflaeche ->
// LinkDropped, Rechtsklick auf Node -> NodeContextRequested, Rechtsklick auf
// Leerflaeche -> PaletteRequested, Entf, B = Breakpoint, C = CommentBox um
// die Auswahl, Ctrl+C/X/V = Copy/Cut/Paste (Serializer-Klon, auch zwischen
// Scripts), CommentBoxes: Drag am Header (nimmt enthaltene Nodes mit),
// Doppelklick = Umbenennen, Entf = Loeschen.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Assistant.VScripts.Core;
using Assistant.VScripts.Data;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;

namespace Razor.UI.VScriptEditor
{
    public sealed class NodeCanvas : Control
    {
        // ---- Client-Geometrie (nicht aendern — muss zum In-Client-Editor passen)
        private const double NodeWidth = 200;
        private const double HeaderHeight = 26;
        private const double PinSpacing = 25;
        private const double PinRadius = 6;
        private const double GridMinor = 16;
        private const double GridMajor = 64;
        private const double CommentHeaderHeight = 30;

        // Trail-Dauer wie im Client (TRAIL_DURATION).
        private const double TrailDuration = 1.5;

        private static readonly Color CanvasBg = Color.Parse("#232323");
        private static readonly Color GridMinorColor = Color.Parse("#1E1E1E");
        private static readonly Color GridMajorColor = Color.Parse("#181818");
        private static readonly Color NodeBg = Color.FromArgb(242, 0x20, 0x20, 0x26);
        private static readonly Color NodeBorder = Color.Parse("#101010");
        private static readonly Color SelectionGold = Color.Parse("#FFB43C");
        private static readonly Color TrailOrange = Color.Parse("#FF8000");
        private static readonly Color TextColor = Colors.White;
        private static readonly Color TextMuted = Color.Parse("#B8B8B8");

        public NodeGraph Graph { get; set; }

        /// <summary>Id des gerade laufenden Nodes (zusaetzlich zum Trail).</summary>
        public string RunningNodeId { get; set; }

        /// <summary>Id des Nodes, an dem die Engine pausiert (Breakpoint).</summary>
        public string PausedNodeId { get; set; }

        /// <summary>Id des Delay-Nodes, der gerade wartet (Highlight + Countdown).</summary>
        public string DelayingNodeId { get; set; }

        /// <summary>Ende des laufenden Delays (UTC) fuer den ms-Countdown.</summary>
        public DateTime DelayUntilUtc { get; set; }

        /// <summary>NodeId -> Fehlermeldung (roter Rahmen + Text unterm Node).</summary>
        public Dictionary<string, string> NodeErrors { get; set; } = new();

        /// <summary>NodeId -> letzte Ausfuehrung (Engine-Trail, 1,5 s Fade).</summary>
        public Dictionary<string, DateTime> NodeExecutionTimes { get; set; } = new();

        /// <summary>LinkId -> letzte Aktivierung (Engine-Trail).</summary>
        public Dictionary<string, DateTime> LinkActivationTimes { get; set; } = new();

        /// <summary>Mehrfachauswahl; das erste Element ist der Primaer-Node.</summary>
        public List<VScriptNode> SelectedNodes { get; } = new();

        public VScriptNode SelectedNode => SelectedNodes.Count > 0 ? SelectedNodes[0] : null;
        public NodeLink SelectedLink { get; private set; }
        public CommentBox SelectedComment { get; private set; }

        public event Action GraphChanged;
        public event Action SelectionChanged;
        public event Action<VScriptNode> NodeDoubleClicked;
        public event Action<NodePin, VScriptNode> PinDoubleClicked;
        public event Action<Point> PaletteRequested; // Weltkoordinaten (Leerflaeche)
        public event Action<VScriptNode> NodeContextRequested; // Rechtsklick auf Node
        public event Action<CommentBox> CommentBoxDoubleClicked; // Umbenennen
        public event Action<Rect> CommentRequested; // C um die Auswahl (Welt-Bounds)

        // In-Node-Filter-UI (Find Items / Find Mobiles)
        public event Action<VScriptNode> FilterAddRequested;
        public event Action<VScriptNode, int> FilterEditRequested;
        public event Action<VScriptNode, int> FilterRemoveRequested;

        /// <summary>Pending-Link auf Leerflaeche fallen gelassen: Quelle + Weltposition.</summary>
        public event Action<NodePin, VScriptNode, Point> LinkDropped;

        private Point _pan = new(0, 0);
        private double _zoom = 1.0;

        private bool _panning;
        private Point _panStartScreen;
        private Point _panStartOffset;

        private VScriptNode _dragNode;
        private readonly Dictionary<VScriptNode, Point> _dragOffsets = new();

        private CommentBox _dragComment;
        private Point _dragCommentOffset;
        private readonly Dictionary<VScriptNode, Point> _dragCommentNodeOffsets = new();

        private bool _marquee;
        private Point _marqueeStart; // Welt
        private Point _marqueeCurrent;

        private NodePin _linkDragFrom; // Output- ODER Input-Pin
        private VScriptNode _linkDragFromNode;
        private Point _linkDragCurrent;

        private Point _lastPointerWorld;

        // Clipboard (statisch: Copy/Paste funktioniert auch zwischen Scripts).
        private static string _clipboardJson;

        // Undo/Redo: Snapshot-Stacks (Serializer-JSON), max. 50 Schritte.
        private readonly List<string> _undoStack = new();
        private readonly List<string> _redoStack = new();
        private string _pendingDragSnapshot; // bei Drag-Start gemerkt, beim ersten Move committet

        /// <summary>Statusmeldungen fuer die Statusleiste des Fensters.</summary>
        public event Action<string> Notify;

        /// <summary>Undo/Redo hat den Graphen durch eine neue Instanz ersetzt.</summary>
        public event Action<NodeGraph> GraphReplaced;

        private readonly DispatcherTimer _animTimer;

        public NodeCanvas()
        {
            Focusable = true;
            ClipToBounds = true;

            // Animations-Puls fuer Trail/Partikel — invalidiert nur, wenn etwas
            // Sichtbares laeuft (sonst idle).
            _animTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(33) };
            _animTimer.Tick += (s, e) =>
            {
                if (HasActiveVisuals())
                    InvalidateVisual();
            };

            AttachedToVisualTree += (s, e) => _animTimer.Start();
            DetachedFromVisualTree += (s, e) => _animTimer.Stop();
        }

        private bool HasActiveVisuals()
        {
            if (RunningNodeId != null || PausedNodeId != null || DelayingNodeId != null ||
                _linkDragFrom != null || _marquee)
                return true;

            // Fehler-Pins pulsieren, solange Fehler anstehen.
            if (NodeErrors.Count > 0)
                return true;

            DateTime cutoff = DateTime.Now.AddSeconds(-TrailDuration - 0.2);
            return NodeExecutionTimes.Values.Any(t => t > cutoff) ||
                   LinkActivationTimes.Values.Any(t => t > cutoff);
        }

        // ---- Koordinaten ---------------------------------------------------

        private Point WorldToScreen(Point world) =>
            new((world.X - _pan.X) * _zoom, (world.Y - _pan.Y) * _zoom);

        public Point ScreenToWorld(Point screen) =>
            new(screen.X / _zoom + _pan.X, screen.Y / _zoom + _pan.Y);

        /// <summary>Weltpunkt in der Mitte der aktuellen Sicht (fuer Sidebar-Einfuegen).</summary>
        public Point ViewCenterWorld() =>
            ScreenToWorld(new Point(Bounds.Width / 2, Bounds.Height / 2));

        /// <summary>Zentriert die Sicht auf einen Node (Zoom bleibt) — fuer Fehler/Breakpoints.</summary>
        public void CenterOnNode(string nodeId)
        {
            VScriptNode node = Graph?.Nodes.FirstOrDefault(n => n.Id == nodeId);
            if (node == null)
                return;

            Rect rect = NodeRect(node);
            _pan = new Point(
                rect.Center.X - Bounds.Width / (2 * _zoom),
                rect.Center.Y - Bounds.Height / (2 * _zoom));
            InvalidateVisual();
        }

        // ---- Undo/Redo -----------------------------------------------------

        /// <summary>VOR jeder Mutation aufrufen: legt einen Snapshot auf den Undo-Stack.</summary>
        public void PushUndo()
        {
            if (Graph == null)
                return;

            try
            {
                _undoStack.Add(VScriptSerializer.Serialize(Graph));
                if (_undoStack.Count > 50)
                    _undoStack.RemoveAt(0);
                _redoStack.Clear();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Razor/vscript-editor] Undo-Snapshot fehlgeschlagen: {ex.Message}");
            }
        }

        public void ClearHistory()
        {
            _undoStack.Clear();
            _redoStack.Clear();
            _pendingDragSnapshot = null;
        }

        private void TakePendingSnapshot()
        {
            try
            {
                _pendingDragSnapshot = Graph == null ? null : VScriptSerializer.Serialize(Graph);
            }
            catch
            {
                _pendingDragSnapshot = null;
            }
        }

        private void CommitPendingSnapshot()
        {
            if (_pendingDragSnapshot == null)
                return;

            _undoStack.Add(_pendingDragSnapshot);
            if (_undoStack.Count > 50)
                _undoStack.RemoveAt(0);
            _redoStack.Clear();
            _pendingDragSnapshot = null;
        }

        public void Undo()
        {
            RestoreFrom(_undoStack, _redoStack, "Undo");
        }

        public void Redo()
        {
            RestoreFrom(_redoStack, _undoStack, "Redo");
        }

        private void RestoreFrom(List<string> source, List<string> target, string label)
        {
            if (Graph == null || source.Count == 0)
            {
                Notify?.Invoke($"Nothing to {label.ToLowerInvariant()}.");
                return;
            }

            try
            {
                string current = VScriptSerializer.Serialize(Graph);
                string json = source[^1];
                source.RemoveAt(source.Count - 1);

                NodeGraph restored = VScriptSerializer.Deserialize(json);
                if (restored == null)
                    return;

                target.Add(current);
                if (target.Count > 50)
                    target.RemoveAt(0);

                Graph = restored;
                RefreshListElementTypes();
                SelectedNodes.Clear();
                SelectedLink = null;
                SelectedComment = null;
                GraphReplaced?.Invoke(restored);
                SelectionChanged?.Invoke();
                GraphChanged?.Invoke();
                Notify?.Invoke($"{label}.");
                InvalidateVisual();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Razor/vscript-editor] {label} fehlgeschlagen: {ex}");
            }
        }

        public void CenterOnGraph()
        {
            if (Graph == null || Graph.Nodes.Count == 0)
            {
                _pan = new Point(-40, -40);
                _zoom = 1.0;
                InvalidateVisual();
                return;
            }

            double minX = Graph.Nodes.Min(n => n.Position.X);
            double minY = Graph.Nodes.Min(n => n.Position.Y);
            _pan = new Point(minX - 60, minY - 60);
            _zoom = 1.0;
            InvalidateVisual();
        }

        // ---- Selektion -----------------------------------------------------

        public void SetSelection(VScriptNode node, NodeLink link)
        {
            SelectedNodes.Clear();
            if (node != null)
                SelectedNodes.Add(node);

            SelectedLink = link;
            SelectedComment = null;
            SelectionChanged?.Invoke();
            InvalidateVisual();
        }

        private void SelectSingle(VScriptNode node, bool toggle)
        {
            if (toggle)
            {
                if (!SelectedNodes.Remove(node))
                    SelectedNodes.Add(node);
            }
            else if (!SelectedNodes.Contains(node))
            {
                SelectedNodes.Clear();
                SelectedNodes.Add(node);
            }
            else
            {
                // Bereits Teil der Auswahl: Primaer-Node nach vorn.
                SelectedNodes.Remove(node);
                SelectedNodes.Insert(0, node);
            }

            SelectedLink = null;
            SelectedComment = null;
            SelectionChanged?.Invoke();
            InvalidateVisual();
        }

        // ---- Layout-Helfer -------------------------------------------------

        private const double FilterRowHeight = 17;

        /// <summary>Filterkette der Find-Nodes (null = Node hat keine Filter-UI).</summary>
        internal static List<Assistant.VScripts.Nodes.FindFilter> GetNodeFilters(VScriptNode node) => node switch
        {
            Assistant.VScripts.Nodes.FindItemsNode fi => fi.Filters,
            Assistant.VScripts.Nodes.FindMobilesNode fm => fm.Filters,
            _ => null
        };

        private static double PinAreaHeight(VScriptNode node)
        {
            int rows = Math.Max(node.InputPins.Count, node.OutputPins.Count);
            return HeaderHeight + 8 + rows * PinSpacing + 4;
        }

        private static double NodeHeight(VScriptNode node)
        {
            double h = PinAreaHeight(node);

            var filters = GetNodeFilters(node);
            if (filters != null)
                h += 4 + (filters.Count + 1) * FilterRowHeight + 4; // Zeilen + "+"-Zeile

            return h;
        }

        /// <summary>Oberkante des Filterbereichs (Weltkoordinaten).</summary>
        private static double FilterAreaTop(VScriptNode node) =>
            node.Position.Y + PinAreaHeight(node) + 4;

        // node.Width ist client-authoritativ (Basis 200; Conversion-Nodes 40).
        private static double GetNodeWidth(VScriptNode node) => node.Width;

        private Rect NodeRect(VScriptNode node) =>
            new(node.Position.X, node.Position.Y, GetNodeWidth(node), NodeHeight(node));

        private static Rect CommentRect(CommentBox box) =>
            new(box.Position.X, box.Position.Y, box.Size.X, box.Size.Y);

        private static Rect CommentHeaderRect(CommentBox box) =>
            new(box.Position.X, box.Position.Y, box.Size.X, CommentHeaderHeight);

        private Point PinCenter(VScriptNode node, NodePin pin)
        {
            bool isInput = pin.Kind == PinKind.Input;
            var list = isInput ? node.InputPins : node.OutputPins;
            int index = list.IndexOf(pin);
            double y = node.Position.Y + HeaderHeight + 8 + index * PinSpacing + PinSpacing / 2;
            double x = isInput ? node.Position.X : node.Position.X + GetNodeWidth(node);
            return new Point(x, y);
        }

        private static Color CategoryColor(VScriptNode node)
        {
            var v = node.GetTitleBarColor(); // Vector4 RGBA 0..1 (Client-Farben)
            return Color.FromArgb((byte) (v.W * 255), (byte) (v.X * 255), (byte) (v.Y * 255), (byte) (v.Z * 255));
        }

        private static Color PinColor(NodePin pin)
        {
            var v = pin.GetColor();
            return Color.FromArgb((byte) (v.W * 255), (byte) (v.X * 255), (byte) (v.Y * 255), (byte) (v.Z * 255));
        }

        private bool IsPinConnected(NodePin pin)
        {
            if (Graph == null)
                return false;

            return Graph.Links.Any(l => l.StartPinId == pin.Id || l.EndPinId == pin.Id);
        }

        /// <summary>Ist der Pin waehrend eines Link-Drags ein gueltiges Ziel?</summary>
        private bool IsPinCompatibleWithDrag(NodePin pin)
        {
            if (_linkDragFrom == null)
                return true;

            if (ReferenceEquals(pin, _linkDragFrom))
                return true; // Quelle selbst nicht dimmen

            return _linkDragFrom.Kind == PinKind.Output
                ? PinCompat.CanConnect(_linkDragFrom, pin)
                : PinCompat.CanConnect(pin, _linkDragFrom);
        }

        // ---- Rendering -----------------------------------------------------

        public override void Render(DrawingContext ctx)
        {
            var bounds = new Rect(Bounds.Size);
            ctx.FillRectangle(new SolidColorBrush(CanvasBg), bounds);

            DrawGrid(ctx, bounds);

            if (Graph == null)
                return;

            using (ctx.PushTransform(Matrix.CreateTranslation(-_pan.X, -_pan.Y) * Matrix.CreateScale(_zoom, _zoom)))
            {
                DrawCommentBoxes(ctx);
                DrawLinks(ctx);
                DrawPendingLink(ctx);

                foreach (var node in Graph.Nodes)
                    DrawNode(ctx, node);

                DrawMarquee(ctx);
            }
        }

        private void DrawGrid(DrawingContext ctx, Rect bounds)
        {
            var minorPen = new Pen(new SolidColorBrush(GridMinorColor), 1);
            var majorPen = new Pen(new SolidColorBrush(GridMajorColor), 1);

            double minorStep = GridMinor * _zoom;
            double majorStep = GridMajor * _zoom;
            if (minorStep < 4)
                minorStep = 0;

            double offX = (-_pan.X * _zoom) % majorStep;
            double offY = (-_pan.Y * _zoom) % majorStep;

            if (minorStep > 0)
            {
                double mOffX = (-_pan.X * _zoom) % minorStep;
                double mOffY = (-_pan.Y * _zoom) % minorStep;
                for (double x = mOffX; x < bounds.Width; x += minorStep)
                    ctx.DrawLine(minorPen, new Point(x, 0), new Point(x, bounds.Height));
                for (double y = mOffY; y < bounds.Height; y += minorStep)
                    ctx.DrawLine(minorPen, new Point(0, y), new Point(bounds.Width, y));
            }

            for (double x = offX; x < bounds.Width; x += majorStep)
                ctx.DrawLine(majorPen, new Point(x, 0), new Point(x, bounds.Height));
            for (double y = offY; y < bounds.Height; y += majorStep)
                ctx.DrawLine(majorPen, new Point(0, y), new Point(bounds.Width, y));
        }

        private void DrawCommentBoxes(DrawingContext ctx)
        {
            foreach (var box in Graph.CommentBoxes.OrderBy(b => b.ZOrder))
            {
                Rect rect = CommentRect(box);
                var color = Color.FromArgb((byte) (box.Color.W * 100), (byte) (box.Color.X * 255),
                    (byte) (box.Color.Y * 255), (byte) (box.Color.Z * 255));
                ctx.FillRectangle(new SolidColorBrush(color), rect, 4);

                var headerColor = Color.FromArgb((byte) Math.Min(255, box.Color.W * 190),
                    (byte) (box.Color.X * 255), (byte) (box.Color.Y * 255), (byte) (box.Color.Z * 255));
                ctx.FillRectangle(new SolidColorBrush(headerColor), CommentHeaderRect(box), 4);

                if (ReferenceEquals(box, SelectedComment))
                {
                    ctx.DrawRectangle(null,
                        new Pen(new SolidColorBrush(SelectionGold), 2 / _zoom), rect, 4, 4);
                }

                DrawText(ctx, box.Title ?? "", new Point(rect.X + 8, rect.Y + 7), TextColor, 13, true);
            }
        }

        private (double fade, double pulse) TrailAlpha(DateTime activation)
        {
            double t = (DateTime.Now - activation).TotalSeconds;
            if (t < 0 || t >= TrailDuration)
                return (0, 0);

            // Formeln aus dem Client-Editor (Fade + Sinus-Puls).
            double fade = 1.0 - t / TrailDuration;
            double pulse = fade * (0.65 + 0.35 * Math.Sin(t * 12.0));
            return (fade, pulse);
        }

        private void DrawLinks(DrawingContext ctx)
        {
            foreach (var link in Graph.Links)
            {
                if (link.StartPinId == null || link.EndPinId == null)
                    continue; // Null-Links alter Client-Dateien

                NodePin from = Graph.GetPin(link.StartPinId);
                NodePin to = Graph.GetPin(link.EndPinId);
                if (from == null || to == null)
                    continue;

                VScriptNode fromNode = Graph.GetNodeByPinId(from.Id);
                VScriptNode toNode = Graph.GetNodeByPinId(to.Id);

                Point p1 = PinCenter(fromNode, from);
                Point p2 = PinCenter(toNode, to);

                bool selected = ReferenceEquals(link, SelectedLink);
                Color c = selected ? SelectionGold : PinColor(from);
                double thickness = (from.Type == PinType.Flow ? 3 : 2) / _zoom + (selected ? 1 : 0);

                // Aktiver Trail: oranger Puls + dicker + Partikel.
                double trailFade = 0;
                double trailTime = 0;
                if (LinkActivationTimes.TryGetValue(link.Id, out DateTime act))
                {
                    (double fade, double pulse) = TrailAlpha(act);
                    if (fade > 0)
                    {
                        trailFade = fade;
                        trailTime = (DateTime.Now - act).TotalSeconds;
                        c = Color.FromArgb((byte) Math.Clamp(pulse * 255, 40, 255),
                            TrailOrange.R, TrailOrange.G, TrailOrange.B);
                        thickness = (4.0 + 2.0 * fade) / _zoom;
                    }
                }

                DrawBezier(ctx, p1, p2, new Pen(new SolidColorBrush(c), thickness));

                if (trailFade > 0)
                    DrawFlowParticles(ctx, p1, p2, trailTime, trailFade);
            }
        }

        private void DrawFlowParticles(DrawingContext ctx, Point p1, Point p2, double time, double fade)
        {
            var brush = new SolidColorBrush(Color.FromArgb((byte) (fade * 230), 255, 255, 255));
            double r = 3.0 / _zoom;

            for (int k = 0; k < 3; k++)
            {
                double t = (time * 0.8 + k / 3.0) % 1.0;
                ctx.DrawEllipse(brush, null, BezierPoint(p1, p2, t), r, r);
            }
        }

        private static Point BezierPoint(Point p1, Point p2, double t)
        {
            double dx = Math.Max(40, Math.Abs(p2.X - p1.X) * 0.5);
            Point c1 = new(p1.X + dx, p1.Y);
            Point c2 = new(p2.X - dx, p2.Y);

            double mt = 1 - t;
            double x = mt * mt * mt * p1.X + 3 * mt * mt * t * c1.X + 3 * mt * t * t * c2.X + t * t * t * p2.X;
            double y = mt * mt * mt * p1.Y + 3 * mt * mt * t * c1.Y + 3 * mt * t * t * c2.Y + t * t * t * p2.Y;
            return new Point(x, y);
        }

        private void DrawPendingLink(DrawingContext ctx)
        {
            if (_linkDragFrom == null)
                return;

            Point p1 = PinCenter(_linkDragFromNode, _linkDragFrom);
            Point p2 = _linkDragCurrent;

            // Aus Input-Pins zieht der Draht nach links (Quelle liegt rechts).
            if (_linkDragFrom.Kind == PinKind.Input)
                (p1, p2) = (p2, p1);

            DrawBezier(ctx, p1, p2,
                new Pen(new SolidColorBrush(PinColor(_linkDragFrom)), 2 / _zoom, dashStyle: DashStyle.Dash));
        }

        private void DrawMarquee(DrawingContext ctx)
        {
            if (!_marquee)
                return;

            var rect = new Rect(
                Math.Min(_marqueeStart.X, _marqueeCurrent.X),
                Math.Min(_marqueeStart.Y, _marqueeCurrent.Y),
                Math.Abs(_marqueeCurrent.X - _marqueeStart.X),
                Math.Abs(_marqueeCurrent.Y - _marqueeStart.Y));

            ctx.FillRectangle(
                new SolidColorBrush(Color.FromArgb(30, SelectionGold.R, SelectionGold.G, SelectionGold.B)), rect);
            ctx.DrawRectangle(null,
                new Pen(new SolidColorBrush(SelectionGold), 1 / _zoom, dashStyle: DashStyle.Dash), rect);
        }

        private static void DrawBezier(DrawingContext ctx, Point p1, Point p2, Pen pen)
        {
            double dx = Math.Max(40, Math.Abs(p2.X - p1.X) * 0.5);
            var geo = new StreamGeometry();
            using (var g = geo.Open())
            {
                g.BeginFigure(p1, false);
                g.CubicBezierTo(new Point(p1.X + dx, p1.Y), new Point(p2.X - dx, p2.Y), p2);
                g.EndFigure(false);
            }

            ctx.DrawGeometry(null, pen, geo);
        }

        private void DrawNode(DrawingContext ctx, VScriptNode node)
        {
            Rect rect = NodeRect(node);

            // Conversion-Nodes zeichnet der Client als kompakten Kreis (Width 40).
            if (node is Assistant.VScripts.Nodes.ConversionNode)
            {
                DrawConversionNode(ctx, node, rect);
                return;
            }

            // Schatten + Body
            ctx.FillRectangle(new SolidColorBrush(Color.FromArgb(120, 0, 0, 0)),
                rect.Translate(new Vector(3, 3)), 6);
            ctx.FillRectangle(new SolidColorBrush(NodeBg), rect, 6);

            // Header: Kategorie-Farbe als Gradient (UE-Look), untere Ecken quadratisch.
            Color cat = CategoryColor(node);
            var headerRect = new Rect(rect.X, rect.Y, rect.Width, HeaderHeight);
            var headerBrush = new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                EndPoint = new RelativePoint(1, 0, RelativeUnit.Relative),
                GradientStops =
                {
                    new GradientStop(Color.FromArgb(235, cat.R, cat.G, cat.B), 0),
                    new GradientStop(Color.FromArgb(150, (byte) (cat.R * 0.55), (byte) (cat.G * 0.55),
                        (byte) (cat.B * 0.55)), 1)
                }
            };
            ctx.FillRectangle(headerBrush, headerRect, 6);
            ctx.FillRectangle(headerBrush,
                new Rect(rect.X, rect.Y + HeaderHeight - 6, rect.Width, 6));

            DrawText(ctx, node.Name, new Point(rect.X + 8, rect.Y + 5), TextColor, 13, true);

            // Grundrahmen.
            ctx.DrawRectangle(null, new Pen(new SolidColorBrush(NodeBorder), 1 / _zoom), rect, 6, 6);

            // Fehler: roter Rahmen + Meldung direkt unter dem Node.
            if (NodeErrors != null && NodeErrors.TryGetValue(node.Id, out string error))
            {
                ctx.DrawRectangle(null, new Pen(new SolidColorBrush(Colors.Red), 2.5 / _zoom), rect, 6, 6);

                string msg = error ?? "Error";
                if (msg.Length > 64)
                    msg = msg.Substring(0, 62) + "…";

                var ft = new FormattedText(msg, CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                    new Typeface("Segoe UI", weight: FontWeight.SemiBold), 11,
                    new SolidColorBrush(Colors.White));
                var chip = new Rect(rect.X, rect.Bottom + 5, ft.Width + 12, ft.Height + 6);
                ctx.FillRectangle(new SolidColorBrush(Color.FromArgb(230, 0x8B, 0x1A, 0x1A)), chip, 4);
                ctx.DrawText(ft, new Point(chip.X + 6, chip.Y + 3));
            }

            // Execution-Trail: oranger Puls (Client-Formel).
            if (NodeExecutionTimes.TryGetValue(node.Id, out DateTime exec))
            {
                (_, double pulse) = TrailAlpha(exec);
                if (pulse > 0)
                {
                    var trail = Color.FromArgb((byte) Math.Clamp(pulse * 255, 0, 255),
                        TrailOrange.R, TrailOrange.G, TrailOrange.B);
                    ctx.DrawRectangle(null, new Pen(new SolidColorBrush(trail), 3.5 / _zoom), rect, 6, 6);
                }
            }

            // Pause am Breakpoint: gelbe, solide Umrandung.
            if (node.Id == PausedNodeId)
                ctx.DrawRectangle(null, new Pen(new SolidColorBrush(Colors.Yellow), 4 / _zoom), rect, 6, 6);

            // Aktiver Delay: solider oranger Rahmen solange gewartet wird +
            // ms-Countdown als Chip unter dem Node (der 33ms-Anim-Tick des
            // Canvas zaehlt die Anzeige herunter).
            if (node.Id == DelayingNodeId)
            {
                ctx.DrawRectangle(null, new Pen(new SolidColorBrush(TrailOrange), 3.5 / _zoom), rect, 6, 6);

                double remaining = Math.Max(0, (DelayUntilUtc - DateTime.UtcNow).TotalMilliseconds);
                var ms = new FormattedText($"{remaining:F0} ms", CultureInfo.InvariantCulture,
                    FlowDirection.LeftToRight, new Typeface("Segoe UI", weight: FontWeight.SemiBold), 11,
                    new SolidColorBrush(Colors.White));
                var chip = new Rect(rect.X, rect.Bottom + 5, ms.Width + 12, ms.Height + 6);
                ctx.FillRectangle(new SolidColorBrush(Color.FromArgb(230, 0xB0, 0x5A, 0x00)), chip, 4);
                ctx.DrawText(ms, new Point(chip.X + 6, chip.Y + 3));
            }

            // Selektion: goldener Rahmen + weicher Glow (UE-Look).
            if (SelectedNodes.Contains(node))
            {
                ctx.DrawRectangle(null,
                    new Pen(new SolidColorBrush(Color.FromArgb(70, SelectionGold.R, SelectionGold.G, SelectionGold.B)),
                        6 / _zoom), rect.Inflate(2 / _zoom), 8, 8);
                ctx.DrawRectangle(null, new Pen(new SolidColorBrush(SelectionGold), 2 / _zoom), rect, 6, 6);
            }

            // Breakpoint-Indikator oben rechts (roter Punkt mit weissem Ring).
            if (node.HasBreakpoint)
            {
                var bp = new Point(rect.Right - 10, rect.Y + 10);
                ctx.DrawEllipse(new SolidColorBrush(Colors.Red), null, bp, 5, 5);
                ctx.DrawEllipse(null, new Pen(new SolidColorBrush(Colors.White), 1.4), bp, 5.5, 5.5);
            }

            // Pins (implizite Player-Pins der GetPlayer*-Getter nur zeigen,
            // wenn tatsaechlich etwas verbunden ist — sonst gilt World.Player).
            foreach (var pin in node.InputPins)
            {
                if (PinCompat.IsImplicitPlayerPin(node, pin) && !IsPinConnected(pin))
                    continue;
                DrawPin(ctx, node, pin);
            }

            foreach (var pin in node.OutputPins)
                DrawPin(ctx, node, pin);

            // In-Node-Filter-UI (Find Items / Find Mobiles)
            var filters = GetNodeFilters(node);
            if (filters != null)
                DrawFilterSection(ctx, node, rect, filters);
        }

        /// <summary>Filterzeilen + "+"-Zeile am unteren Node-Rand (Razor-Zusatz).</summary>
        private void DrawFilterSection(DrawingContext ctx, VScriptNode node, Rect rect,
            List<Assistant.VScripts.Nodes.FindFilter> filters)
        {
            double top = FilterAreaTop(node);

            // Trennlinie
            ctx.DrawLine(new Pen(new SolidColorBrush(Color.FromArgb(90, 255, 255, 255)), 1 / _zoom),
                new Point(rect.X + 6, top - 2), new Point(rect.Right - 6, top - 2));

            for (int i = 0; i < filters.Count; i++)
            {
                var f = filters[i];
                NodePin pin = f.UsePin && f.PinId != null
                    ? node.InputPins.FirstOrDefault(p => p.Id == f.PinId)
                    : null;
                bool pinConnected = pin != null && IsPinConnected(pin);

                double y = top + i * FilterRowHeight;
                string text = Assistant.VScripts.Nodes.FindFilterCatalog.Describe(f, i, pin, pinConnected);

                DrawText(ctx, text, new Point(rect.X + 8, y + 1), TextMuted, 11, false);

                // Entfernen-Kreuz rechts
                DrawText(ctx, "✕", new Point(rect.Right - 16, y + 1), Color.Parse("#996666"), 11, false);
            }

            // "+ Add filter"-Zeile
            double plusY = top + filters.Count * FilterRowHeight;
            DrawText(ctx, "+ Add filter", new Point(rect.X + 8, plusY + 1), Color.Parse("#5FBF60"), 11, true);
        }

        private void DrawPin(DrawingContext ctx, VScriptNode node, NodePin pin)
        {
            Point c = PinCenter(node, pin);

            bool compatible = IsPinCompatibleWithDrag(pin);
            byte alpha = compatible ? (byte) 255 : (byte) 70;

            Color pc = PinColor(pin);
            var brush = new SolidColorBrush(Color.FromArgb(alpha, pc.R, pc.G, pc.B));
            bool connected = IsPinConnected(pin);

            if (pin.Type == PinType.Flow)
            {
                DrawFlowChevron(ctx, c, brush, connected);
            }
            else if (pin.IsList)
            {
                DrawListPin(ctx, c, brush, connected);
            }
            else if (connected)
            {
                ctx.DrawEllipse(brush, null, c, PinRadius, PinRadius);
            }
            else
            {
                ctx.DrawEllipse(null, new Pen(brush, 2), c, PinRadius - 1, PinRadius - 1);
            }

            // Kompatibles Ziel beim Link-Drag: dezenter weisser Ring.
            if (_linkDragFrom != null && compatible && !ReferenceEquals(pin, _linkDragFrom))
            {
                ctx.DrawEllipse(null,
                    new Pen(new SolidColorBrush(Color.FromArgb(140, 255, 255, 255)), 1.4 / _zoom),
                    c, PinRadius + 3, PinRadius + 3);
            }

            // Fehler am Node: unverbundene, leere Input-Pins sind die wahrscheinliche
            // Ursache — sie pulsieren rot ("Pin leuchtet, da nichts verbunden ist").
            if (NodeErrors != null && NodeErrors.ContainsKey(node.Id) &&
                pin.Kind == PinKind.Input && pin.Type != PinType.Flow && !connected && pin.Value == null)
            {
                double pulse = 0.45 + 0.45 * Math.Sin(DateTime.Now.TimeOfDay.TotalSeconds * 6);
                ctx.DrawEllipse(
                    new SolidColorBrush(Color.FromArgb((byte) (pulse * 90), 255, 40, 40)), null,
                    c, PinRadius + 6, PinRadius + 6);
                ctx.DrawEllipse(null,
                    new Pen(new SolidColorBrush(Color.FromArgb((byte) (pulse * 255), 255, 60, 60)), 2 / _zoom),
                    c, PinRadius + 4, PinRadius + 4);
            }

            // Label + ggf. Inline-Wert
            Color labelColor = Color.FromArgb(alpha, TextMuted.R, TextMuted.G, TextMuted.B);
            string label = pin.Name ?? string.Empty;

            if (pin.Kind == PinKind.Input && pin.Type != PinType.Flow && !connected && pin.Value != null)
            {
                string v = Convert.ToString(pin.Value, CultureInfo.InvariantCulture);
                if (v?.Length > 14)
                    v = v.Substring(0, 12) + "…";
                label = string.IsNullOrEmpty(label) ? $"[{v}]" : $"{label} [{v}]";
            }

            // Bei Pause am Node: aktuelle Pin-Werte einblenden (Debug-Sicht).
            if (node.Id == PausedNodeId && pin.Value != null && pin.Type != PinType.Flow &&
                (pin.Kind == PinKind.Output || connected))
            {
                string v = Convert.ToString(pin.Value, CultureInfo.InvariantCulture);
                if (v?.Length > 14)
                    v = v.Substring(0, 12) + "…";
                label = string.IsNullOrEmpty(label) ? $"= {v}" : $"{label} = {v}";
                labelColor = Colors.Yellow;
            }

            if (!string.IsNullOrEmpty(label))
            {
                if (pin.Kind == PinKind.Input)
                    DrawText(ctx, label, new Point(c.X + PinRadius + 4, c.Y - 8), labelColor, 12, false);
                else
                    DrawTextRight(ctx, label, new Point(c.X - PinRadius - 4, c.Y - 8), labelColor, 12);
            }
        }

        /// <summary>Kompakter Konvertierungs-Kreis (UE-Optik): Ring in der
        /// Kategorie-Farbe, Pfeil-Glyph, Pins links/rechts auf Pin-Hoehe.</summary>
        private void DrawConversionNode(DrawingContext ctx, VScriptNode node, Rect rect)
        {
            NodePin input = node.InputPins.FirstOrDefault();
            NodePin output = node.OutputPins.FirstOrDefault();
            if (input == null || output == null)
                return;

            Point pinY = PinCenter(node, input);
            var center = new Point(rect.X + rect.Width / 2, pinY.Y);
            const double r = 13;

            // Schatten + Body + Ring
            ctx.DrawEllipse(new SolidColorBrush(Color.FromArgb(120, 0, 0, 0)), null,
                center.WithX(center.X + 2).WithY(center.Y + 2), r, r);
            ctx.DrawEllipse(new SolidColorBrush(NodeBg), null, center, r, r);
            ctx.DrawEllipse(null, new Pen(new SolidColorBrush(CategoryColor(node)), 2 / _zoom), center, r, r);

            // Zustands-Ringe (Fehler/Trail/Pause/Selektion)
            if (NodeErrors != null && NodeErrors.ContainsKey(node.Id))
                ctx.DrawEllipse(null, new Pen(new SolidColorBrush(Colors.Red), 2.5 / _zoom), center, r + 3, r + 3);

            if (NodeExecutionTimes.TryGetValue(node.Id, out DateTime exec))
            {
                (_, double pulse) = TrailAlpha(exec);
                if (pulse > 0)
                {
                    ctx.DrawEllipse(null,
                        new Pen(new SolidColorBrush(Color.FromArgb((byte) Math.Clamp(pulse * 255, 0, 255),
                            TrailOrange.R, TrailOrange.G, TrailOrange.B)), 3 / _zoom), center, r + 3, r + 3);
                }
            }

            if (node.Id == PausedNodeId)
                ctx.DrawEllipse(null, new Pen(new SolidColorBrush(Colors.Yellow), 3.5 / _zoom), center, r + 3, r + 3);

            if (SelectedNodes.Contains(node))
                ctx.DrawEllipse(null, new Pen(new SolidColorBrush(SelectionGold), 2 / _zoom), center, r + 3, r + 3);

            // Pfeil-Glyph (Konvertierungsrichtung), Farben der beiden Typen.
            var inBrush = new SolidColorBrush(PinColor(input));
            var outBrush = new SolidColorBrush(PinColor(output));
            ctx.DrawEllipse(inBrush, null, new Point(center.X - 6, center.Y), 2.5, 2.5);
            ctx.DrawEllipse(outBrush, null, new Point(center.X + 6, center.Y), 2.5, 2.5);
            ctx.DrawLine(new Pen(new SolidColorBrush(TextMuted), 1.4), new Point(center.X - 2.5, center.Y),
                new Point(center.X + 2.5, center.Y));

            ToolTipText(ctx, node, rect, center, r);

            // Pins
            DrawPin(ctx, node, input);
            DrawPin(ctx, node, output);
        }

        private void ToolTipText(DrawingContext ctx, VScriptNode node, Rect rect, Point center, double r)
        {
            // Name klein unter dem Kreis (z. B. "To String").
            var ft = new FormattedText(node.Name ?? "", CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                new Typeface("Segoe UI"), 10, new SolidColorBrush(TextMuted));
            ctx.DrawText(ft, new Point(center.X - ft.Width / 2, center.Y + r + 4));
        }

        /// <summary>Flow-Pin als UE-Chevron (Pfeil nach rechts).</summary>
        private static void DrawFlowChevron(DrawingContext ctx, Point c, IBrush brush, bool connected)
        {
            var geo = new StreamGeometry();
            using (var g = geo.Open())
            {
                g.BeginFigure(new Point(c.X - 5, c.Y - 6), connected);
                g.LineTo(new Point(c.X + 1, c.Y - 6));
                g.LineTo(new Point(c.X + 6, c.Y));
                g.LineTo(new Point(c.X + 1, c.Y + 6));
                g.LineTo(new Point(c.X - 5, c.Y + 6));
                g.EndFigure(true);
            }

            if (connected)
                ctx.DrawGeometry(brush, null, geo);
            else
                ctx.DrawGeometry(null, new Pen(brush, 1.6), geo);
        }

        /// <summary>List-Pin als 2x2-Raster (UE-Array-Optik).</summary>
        private static void DrawListPin(DrawingContext ctx, Point c, IBrush brush, bool connected)
        {
            const double s = 4.2; // Kachelgroesse
            const double gap = 1.6;

            for (int ix = 0; ix < 2; ix++)
            {
                for (int iy = 0; iy < 2; iy++)
                {
                    var r = new Rect(
                        c.X - s - gap / 2 + ix * (s + gap),
                        c.Y - s - gap / 2 + iy * (s + gap),
                        s, s);

                    if (connected)
                        ctx.FillRectangle(brush, r, 1);
                    else
                        ctx.DrawRectangle(null, new Pen(brush, 1.2), r, 1, 1);
                }
            }
        }

        private static void DrawText(DrawingContext ctx, string text, Point pos, Color color, double size, bool bold)
        {
            var ft = new FormattedText(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                new Typeface("Segoe UI", weight: bold ? FontWeight.SemiBold : FontWeight.Normal),
                size, new SolidColorBrush(color));
            ctx.DrawText(ft, pos);
        }

        private static void DrawTextRight(DrawingContext ctx, string text, Point rightPos, Color color, double size)
        {
            var ft = new FormattedText(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                new Typeface("Segoe UI"), size, new SolidColorBrush(color));
            ctx.DrawText(ft, new Point(rightPos.X - ft.Width, rightPos.Y));
        }

        // ---- Hit-Tests -----------------------------------------------------

        private (VScriptNode node, NodePin pin) HitPin(Point world)
        {
            if (Graph == null)
                return (null, null);

            double r = PinRadius + 4;
            for (int i = Graph.Nodes.Count - 1; i >= 0; i--)
            {
                var node = Graph.Nodes[i];
                foreach (var pin in node.InputPins.Concat(node.OutputPins))
                {
                    // Ausgeblendete implizite Player-Pins sind nicht anklickbar.
                    if (PinCompat.IsImplicitPlayerPin(node, pin) && !IsPinConnected(pin))
                        continue;

                    Point c = PinCenter(node, pin);
                    if (Math.Abs(world.X - c.X) <= r && Math.Abs(world.Y - c.Y) <= r)
                        return (node, pin);
                }
            }

            return (null, null);
        }

        private VScriptNode HitNode(Point world)
        {
            if (Graph == null)
                return null;

            for (int i = Graph.Nodes.Count - 1; i >= 0; i--)
            {
                if (NodeRect(Graph.Nodes[i]).Contains(world))
                    return Graph.Nodes[i];
            }

            return null;
        }

        private CommentBox HitCommentHeader(Point world)
        {
            if (Graph == null)
                return null;

            for (int i = Graph.CommentBoxes.Count - 1; i >= 0; i--)
            {
                if (CommentHeaderRect(Graph.CommentBoxes[i]).Contains(world))
                    return Graph.CommentBoxes[i];
            }

            return null;
        }

        private NodeLink HitLink(Point world)
        {
            if (Graph == null)
                return null;

            foreach (var link in Graph.Links)
            {
                if (link.StartPinId == null || link.EndPinId == null)
                    continue;

                NodePin from = Graph.GetPin(link.StartPinId);
                NodePin to = Graph.GetPin(link.EndPinId);
                if (from == null || to == null)
                    continue;

                Point p1 = PinCenter(Graph.GetNodeByPinId(from.Id), from);
                Point p2 = PinCenter(Graph.GetNodeByPinId(to.Id), to);

                for (double t = 0; t <= 1.0; t += 0.05)
                {
                    Point b = BezierPoint(p1, p2, t);
                    if (Math.Abs(world.X - b.X) < 6 && Math.Abs(world.Y - b.Y) < 6)
                        return link;
                }
            }

            return null;
        }

        // ---- Interaktion ---------------------------------------------------

        /// <summary>
        /// Ein Fehler in einem Input-Handler darf NIE den UI-Thread beenden
        /// (das wuerde das komplette Razor-Fenster schliessen): loggen,
        /// transienten Drag-Zustand verwerfen, weiterleben.
        /// </summary>
        private void GuardInput(Action action)
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Razor/vscript-editor] Eingabe-Fehler abgefangen: {ex}");
                _panning = false;
                _dragNode = null;
                _dragOffsets.Clear();
                _dragComment = null;
                _dragCommentNodeOffsets.Clear();
                _marquee = false;
                _linkDragFrom = null;
                _linkDragFromNode = null;
                InvalidateVisual();
            }
        }

        protected override void OnPointerPressed(PointerPressedEventArgs e)
        {
            GuardInput(() => HandlePointerPressed(e));
        }

        private void HandlePointerPressed(PointerPressedEventArgs e)
        {
            base.OnPointerPressed(e);
            Focus();

            Point screen = e.GetPosition(this);
            Point world = ScreenToWorld(screen);
            _lastPointerWorld = world;
            var props = e.GetCurrentPoint(this).Properties;
            bool ctrl = e.KeyModifiers.HasFlag(KeyModifiers.Control);

            if (props.IsRightButtonPressed || props.IsMiddleButtonPressed)
            {
                _panning = true;
                _panStartScreen = screen;
                _panStartOffset = _pan;
                e.Pointer.Capture(this);
                return;
            }

            // Pin zuerst (Link ziehen)
            var (pinNode, pin) = HitPin(world);
            if (pin != null)
            {
                if (e.ClickCount == 2)
                {
                    PinDoubleClicked?.Invoke(pin, pinNode);
                    return;
                }

                if (pin.Kind == PinKind.Input)
                {
                    // Input mit bestehendem Link: Link aufnehmen und umverlegen.
                    // Achtung Null-Links alter Client-Dateien (Save-Bug): EndPinId
                    // kann passen, waehrend StartPinId null ist oder ins Leere
                    // zeigt — solche Leichen entfernen wir und starten einen
                    // frischen Drag aus dem Input-Pin.
                    NodeLink existing = Graph.Links.FirstOrDefault(l => l.EndPinId == pin.Id);
                    if (existing != null)
                    {
                        NodePin src = existing.StartPinId != null ? Graph.GetPin(existing.StartPinId) : null;
                        PushUndo();
                        Graph.RemoveLink(existing.Id);
                        pin.Value = null; // getrennter Input verliert seinen Wert
                        GraphChanged?.Invoke();

                        if (src != null)
                        {
                            _linkDragFrom = src;
                            _linkDragFromNode = Graph.GetNodeByPinId(src.Id);
                            _linkDragCurrent = world;
                            e.Pointer.Capture(this);
                            InvalidateVisual();
                            return;
                        }
                    }
                }

                // Neuen Link ziehen — aus Output- ODER freiem Input-Pin.
                _linkDragFrom = pin;
                _linkDragFromNode = pinNode;
                _linkDragCurrent = world;
                e.Pointer.Capture(this);
                InvalidateVisual();
                return;
            }

            // Node
            VScriptNode node = HitNode(world);
            if (node != null)
            {
                // Klick in den Filterbereich (Find Items/Mobiles): Zeile editieren,
                // ✕ entfernt, "+"-Zeile fuegt hinzu — kein Drag.
                var nodeFilters = GetNodeFilters(node);
                if (nodeFilters != null && e.ClickCount == 1)
                {
                    double filterTop = FilterAreaTop(node);
                    if (world.Y >= filterTop)
                    {
                        int row = (int) ((world.Y - filterTop) / FilterRowHeight);
                        SelectSingle(node, false);

                        if (row == nodeFilters.Count)
                        {
                            FilterAddRequested?.Invoke(node);
                            return;
                        }

                        if (row >= 0 && row < nodeFilters.Count)
                        {
                            if (world.X > NodeRect(node).Right - 22)
                                FilterRemoveRequested?.Invoke(node, row);
                            else
                                FilterEditRequested?.Invoke(node, row);
                            return;
                        }
                    }
                }

                SelectSingle(node, ctrl);

                if (e.ClickCount == 2)
                {
                    NodeDoubleClicked?.Invoke(node);
                    return;
                }

                // Drag der ganzen Auswahl vorbereiten (Undo-Snapshot erst beim
                // ersten echten Move, damit Klicks den Stack nicht fluten).
                _dragNode = node;
                _dragOffsets.Clear();
                foreach (var sel in SelectedNodes)
                    _dragOffsets[sel] = new Point(world.X - sel.Position.X, world.Y - sel.Position.Y);

                TakePendingSnapshot();
                e.Pointer.Capture(this);
                return;
            }

            // CommentBox-Header (unter den Nodes)
            CommentBox comment = HitCommentHeader(world);
            if (comment != null)
            {
                SelectedNodes.Clear();
                SelectedLink = null;
                SelectedComment = comment;
                SelectionChanged?.Invoke();

                if (e.ClickCount == 2)
                {
                    CommentBoxDoubleClicked?.Invoke(comment);
                    InvalidateVisual();
                    return;
                }

                // Drag: Box + alle vollstaendig enthaltenen Nodes mitnehmen.
                _dragComment = comment;
                _dragCommentOffset = new Point(world.X - comment.Position.X, world.Y - comment.Position.Y);
                TakePendingSnapshot();
                _dragCommentNodeOffsets.Clear();
                Rect box = CommentRect(comment);
                foreach (var n in Graph.Nodes)
                {
                    Rect nr = NodeRect(n);
                    if (box.Contains(nr.TopLeft) && box.Contains(nr.BottomRight))
                        _dragCommentNodeOffsets[n] = new Point(world.X - n.Position.X, world.Y - n.Position.Y);
                }

                e.Pointer.Capture(this);
                InvalidateVisual();
                return;
            }

            // Link
            NodeLink hitLink = HitLink(world);
            if (hitLink != null)
            {
                SelectedNodes.Clear();
                SelectedLink = hitLink;
                SelectedComment = null;
                SelectionChanged?.Invoke();
                InvalidateVisual();
                return;
            }

            // Leerflaeche: Marquee-Selektion aufziehen.
            if (!ctrl)
            {
                SelectedNodes.Clear();
                SelectedLink = null;
                SelectedComment = null;
                SelectionChanged?.Invoke();
            }

            _marquee = true;
            _marqueeStart = world;
            _marqueeCurrent = world;
            e.Pointer.Capture(this);
            InvalidateVisual();
        }

        protected override void OnPointerMoved(PointerEventArgs e)
        {
            GuardInput(() => HandlePointerMoved(e));
        }

        private void HandlePointerMoved(PointerEventArgs e)
        {
            base.OnPointerMoved(e);
            Point screen = e.GetPosition(this);
            Point world = ScreenToWorld(screen);
            _lastPointerWorld = world;

            if (_panning)
            {
                _pan = new Point(
                    _panStartOffset.X - (screen.X - _panStartScreen.X) / _zoom,
                    _panStartOffset.Y - (screen.Y - _panStartScreen.Y) / _zoom);
                InvalidateVisual();
                return;
            }

            if (_dragNode != null)
            {
                CommitPendingSnapshot();

                foreach (var (sel, off) in _dragOffsets)
                {
                    sel.Position = new System.Numerics.Vector2(
                        (float) (world.X - off.X), (float) (world.Y - off.Y));
                }

                InvalidateVisual();
                return;
            }

            if (_dragComment != null)
            {
                CommitPendingSnapshot();
                _dragComment.Position = new System.Numerics.Vector2(
                    (float) (world.X - _dragCommentOffset.X), (float) (world.Y - _dragCommentOffset.Y));

                foreach (var (n, off) in _dragCommentNodeOffsets)
                {
                    n.Position = new System.Numerics.Vector2(
                        (float) (world.X - off.X), (float) (world.Y - off.Y));
                }

                InvalidateVisual();
                return;
            }

            if (_marquee)
            {
                _marqueeCurrent = world;
                InvalidateVisual();
                return;
            }

            if (_linkDragFrom != null)
            {
                _linkDragCurrent = world;
                InvalidateVisual();
            }
        }

        protected override void OnPointerReleased(PointerReleasedEventArgs e)
        {
            GuardInput(() => HandlePointerReleased(e));
        }

        private void HandlePointerReleased(PointerReleasedEventArgs e)
        {
            base.OnPointerReleased(e);
            Point screen = e.GetPosition(this);
            Point world = ScreenToWorld(screen);

            if (_panning)
            {
                _panning = false;
                e.Pointer.Capture(null);

                // Rechtsklick ohne Bewegung: Node-Kontextmenue bzw. Palette.
                if ((screen - _panStartScreen) is { X: > -3 and < 3, Y: > -3 and < 3 } &&
                    e.InitialPressMouseButton == MouseButton.Right)
                {
                    VScriptNode ctxNode = HitNode(world);
                    if (ctxNode != null)
                    {
                        if (!SelectedNodes.Contains(ctxNode))
                            SetSelection(ctxNode, null);
                        NodeContextRequested?.Invoke(ctxNode);
                    }
                    else
                    {
                        PaletteRequested?.Invoke(world);
                    }
                }

                return;
            }

            if (_dragNode != null)
            {
                _dragNode = null;
                _dragOffsets.Clear();
                e.Pointer.Capture(null);
                GraphChanged?.Invoke();
                return;
            }

            if (_dragComment != null)
            {
                _dragComment = null;
                _dragCommentNodeOffsets.Clear();
                e.Pointer.Capture(null);
                GraphChanged?.Invoke();
                return;
            }

            if (_marquee)
            {
                _marquee = false;
                e.Pointer.Capture(null);

                var rect = new Rect(
                    Math.Min(_marqueeStart.X, world.X),
                    Math.Min(_marqueeStart.Y, world.Y),
                    Math.Abs(world.X - _marqueeStart.X),
                    Math.Abs(world.Y - _marqueeStart.Y));

                if (rect.Width > 4 && rect.Height > 4 && Graph != null)
                {
                    foreach (var n in Graph.Nodes)
                    {
                        if (rect.Intersects(NodeRect(n)) && !SelectedNodes.Contains(n))
                            SelectedNodes.Add(n);
                    }

                    SelectionChanged?.Invoke();
                }

                InvalidateVisual();
                return;
            }

            if (_linkDragFrom != null)
            {
                NodePin source = _linkDragFrom;
                VScriptNode sourceNode = _linkDragFromNode;
                _linkDragFrom = null;
                _linkDragFromNode = null;
                e.Pointer.Capture(null);

                var (targetNode, targetPin) = HitPin(world);

                if (targetPin != null)
                {
                    TryConnect(source, targetPin);
                }
                else if (HitNode(world) == null)
                {
                    // Auf Leerflaeche fallen gelassen -> kontextgefilterte Palette.
                    LinkDropped?.Invoke(source, sourceNode, world);
                }

                InvalidateVisual();
            }
        }

        /// <summary>Verbindet zwei Pins richtungs-normalisiert (Output -> Input).
        /// Bei konvertierbaren Typen (Number/String/Boolean) wird wie im
        /// UE-/Client-Editor automatisch ein kompakter Conversion-Node
        /// dazwischengesetzt.</summary>
        public bool TryConnect(NodePin a, NodePin b, bool pushUndo = true)
        {
            NodePin output = a.Kind == PinKind.Output ? a : b;
            NodePin input = a.Kind == PinKind.Output ? b : a;

            if (!PinCompat.CanConnect(output, input))
                return false;

            if (pushUndo)
                PushUndo();

            // Ein Link pro Input-Pin: bestehenden ersetzen.
            NodeLink old = Graph.Links.FirstOrDefault(l => l.EndPinId == input.Id);
            if (old != null)
                Graph.RemoveLink(old.Id);

            VScriptNode conv = CreateConversionNode(output.Type, input.Type);
            if (conv != null)
            {
                // Konverter mittig zwischen die Pins setzen (Client: CreateConversionNode).
                Point p1 = PinCenter(Graph.GetNodeByPinId(output.Id), output);
                Point p2 = PinCenter(Graph.GetNodeByPinId(input.Id), input);
                conv.Position = new System.Numerics.Vector2(
                    (float) ((p1.X + p2.X) / 2 - conv.Width / 2),
                    (float) ((p1.Y + p2.Y) / 2 - 45));

                Graph.AddNode(conv);
                Graph.AddLink(new NodeLink(Graph.GetNextLinkId(), output.Id, conv.InputPins[0].Id));
                Graph.AddLink(new NodeLink(Graph.GetNextLinkId(), conv.OutputPins[0].Id, input.Id));
                Notify?.Invoke($"Inserted {conv.Name} converter ({output.Type} → {input.Type}).");
            }
            else
            {
                Graph.AddLink(new NodeLink(Graph.GetNextLinkId(), output.Id, input.Id));
            }

            // Client-Verhalten (UpdateListNodePinTypes): Liste an einen List-Node
            // anschliessen passt dessen Element-Pins an den Element-Typ an.
            PropagateListElementType(output, input);

            GraphChanged?.Invoke();
            InvalidateVisual();
            return true;
        }

        /// <summary>Element-Typ eines List-Nodes (ForEach, List-Operationen) an
        /// die angeschlossene Liste anpassen — Element-Pin wird z. B. von Any
        /// (grau) zu Object/Item (blau), auch grafisch.</summary>
        private void PropagateListElementType(NodePin output, NodePin input)
        {
            VScriptNode inputNode = Graph.GetNodeByPinId(input.Id);

            if (inputNode == null || !IsListNode(inputNode))
                return;

            if (input.Name != "List" || !input.IsList || !output.IsList)
                return;

            PinType elementType = output.Type;
            // Subtyp aus dem Pin-NAMEN ableiten ("Items" -> Item, "Mobiles" ->
            // Mobile) — ObjectSubType ist nicht bei allen Quell-Pins gesetzt
            // und wird zudem nicht mitserialisiert.
            ObjectSubType subType = PinCompat.ExpectedSubType(output);

            // Auch der List-Pin selbst faerbt sich nach dem Listentyp
            // (Any/grau -> Object/blau), der IsList-Raster-Look bleibt.
            input.Type = elementType;
            input.ObjectSubType = subType;

            foreach (var pin in inputNode.InputPins.Concat(inputNode.OutputPins))
            {
                if (pin.Type == PinType.Flow || pin.IsList)
                    continue;

                if (pin.Name is "Element" or "Current Element" or "Item")
                {
                    pin.Type = elementType;
                    pin.ObjectSubType = subType;
                }
            }
        }

        /// <summary>
        /// Laeuft die Element-Typ-Propagation ueber ALLE bestehenden List-Links
        /// (nach Laden/Undo/Paste noetig: Pin-Subtypen werden nicht
        /// serialisiert und stehen sonst auf dem Default).
        /// </summary>
        public void RefreshListElementTypes()
        {
            if (Graph == null)
                return;

            foreach (var link in Graph.Links)
            {
                if (link.StartPinId == null || link.EndPinId == null)
                    continue;

                NodePin output = Graph.GetPin(link.StartPinId);
                NodePin input = Graph.GetPin(link.EndPinId);
                if (output != null && input != null)
                    PropagateListElementType(output, input);
            }
        }

        private static bool IsListNode(VScriptNode node) =>
            node is Assistant.VScripts.Nodes.ForEachNode
                or Assistant.VScripts.Nodes.ListAddNode
                or Assistant.VScripts.Nodes.ListInsertNode
                or Assistant.VScripts.Nodes.ListRemoveNode
                or Assistant.VScripts.Nodes.ListSetNode
                or Assistant.VScripts.Nodes.ListGetNode
                or Assistant.VScripts.Nodes.ListContainsNode
                or Assistant.VScripts.Nodes.ListIndexOfNode;

        /// <summary>Conversion-Node fuer ein Typ-Paar (null = keine Konvertierung noetig/moeglich).</summary>
        private VScriptNode CreateConversionNode(PinType from, PinType to)
        {
            if (from == to || from == PinType.Any || to == PinType.Any)
                return null;

            string id = Graph.GetNextNodeId();
            string pinId = Graph.GetNextPinId();

            return (from, to) switch
            {
                (PinType.Number, PinType.String) => new Assistant.VScripts.Nodes.NumberToStringNode(id, pinId),
                (PinType.String, PinType.Number) => new Assistant.VScripts.Nodes.StringToNumberNode(id, pinId),
                (PinType.Boolean, PinType.String) => new Assistant.VScripts.Nodes.BooleanToStringNode(id, pinId),
                (PinType.String, PinType.Boolean) => new Assistant.VScripts.Nodes.StringToBooleanNode(id, pinId),
                (PinType.Number, PinType.Boolean) => new Assistant.VScripts.Nodes.NumberToBooleanNode(id, pinId),
                (PinType.Boolean, PinType.Number) => new Assistant.VScripts.Nodes.BooleanToNumberNode(id, pinId),
                _ => null
            };
        }

        protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
        {
            base.OnPointerWheelChanged(e);

            Point screen = e.GetPosition(this);
            Point before = ScreenToWorld(screen);

            _zoom = Math.Clamp(_zoom * (e.Delta.Y > 0 ? 1.1 : 1 / 1.1), 0.25, 2.0);

            Point after = ScreenToWorld(screen);
            _pan = new Point(_pan.X + (before.X - after.X), _pan.Y + (before.Y - after.Y));

            InvalidateVisual();
            e.Handled = true;
        }

        // ---- Tastatur ------------------------------------------------------

        protected override void OnKeyDown(KeyEventArgs e)
        {
            GuardInput(() => HandleKeyDown(e));
        }

        private void HandleKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);

            bool ctrl = e.KeyModifiers.HasFlag(KeyModifiers.Control);

            if (ctrl && e.Key == Key.C)
            {
                CopySelection();
                e.Handled = true;
                return;
            }

            if (ctrl && e.Key == Key.X)
            {
                CutSelection();
                e.Handled = true;
                return;
            }

            if (ctrl && e.Key == Key.V)
            {
                PasteClipboard();
                e.Handled = true;
                return;
            }

            if (ctrl && e.Key == Key.Z)
            {
                if (e.KeyModifiers.HasFlag(KeyModifiers.Shift))
                    Redo();
                else
                    Undo();
                e.Handled = true;
                return;
            }

            if (ctrl && e.Key == Key.Y)
            {
                Redo();
                e.Handled = true;
                return;
            }

            if (e.Key == Key.Delete)
            {
                DeleteSelection();
                e.Handled = true;
            }
            else if (e.Key == Key.B && SelectedNode != null)
            {
                PushUndo();
                SelectedNode.HasBreakpoint = !SelectedNode.HasBreakpoint;
                GraphChanged?.Invoke();
                InvalidateVisual();
                e.Handled = true;
            }
            else if (e.Key == Key.C && SelectedNodes.Count > 0)
            {
                // Kommentar um die Auswahl (UE: C).
                RequestCommentAroundSelection();
                e.Handled = true;
            }
        }

        /// <summary>Feuert CommentRequested mit den Bounds der aktuellen Auswahl.</summary>
        public void RequestCommentAroundSelection()
        {
            if (SelectedNodes.Count == 0)
                return;

            Rect bounds = SelectionBounds();
            CommentRequested?.Invoke(bounds.Inflate(new Thickness(30, CommentHeaderHeight + 16, 30, 24)));
        }

        private Rect SelectionBounds()
        {
            Rect bounds = NodeRect(SelectedNodes[0]);
            foreach (var n in SelectedNodes.Skip(1))
                bounds = bounds.Union(NodeRect(n));
            return bounds;
        }

        private void DeleteSelection()
        {
            var nodesToDelete = SelectedNodes
                .Where(n => !(n is Assistant.VScripts.Nodes.StartNode)).ToList();

            if (nodesToDelete.Count == 0 && SelectedLink == null && SelectedComment == null)
                return;

            PushUndo();

            foreach (var n in nodesToDelete)
                DeleteNodeAndCleanup(n, pushUndo: false);

            SelectedNodes.Clear();

            if (SelectedLink != null)
            {
                ClearLinkEndValue(SelectedLink);
                Graph.RemoveLink(SelectedLink.Id);
                SelectedLink = null;
            }

            if (SelectedComment != null)
            {
                Graph.RemoveCommentBox(SelectedComment.Id);
                SelectedComment = null;
            }

            SelectionChanged?.Invoke();
            GraphChanged?.Invoke();
            InvalidateVisual();
        }

        /// <summary>Getrennte Input-Pins verlieren ihren (ueberschatteten) Wert.</summary>
        private void ClearLinkEndValue(NodeLink link)
        {
            if (link?.EndPinId == null)
                return;

            NodePin end = Graph.GetPin(link.EndPinId);
            if (end != null && end.Kind == PinKind.Input)
                end.Value = null;
        }

        /// <summary>Entfernt einen Node und leert die Werte aller Inputs, die an
        /// seinen Outputs hingen (Disconnect = Wert weg).</summary>
        public void DeleteNodeAndCleanup(VScriptNode node, bool pushUndo = true)
        {
            if (Graph == null || node is Assistant.VScripts.Nodes.StartNode)
                return;

            if (pushUndo)
                PushUndo();

            var outputIds = node.OutputPins.Select(p => p.Id).ToHashSet();
            foreach (var link in Graph.Links.Where(l => l.StartPinId != null && outputIds.Contains(l.StartPinId)).ToList())
                ClearLinkEndValue(link);

            Graph.RemoveNode(node.Id);
            SelectedNodes.Remove(node);
            GraphChanged?.Invoke();
            InvalidateVisual();
        }

        // ---- Copy/Cut/Paste ------------------------------------------------

        /// <summary>Kopiert die Auswahl (ohne StartNode) als Serializer-Teilgraph.</summary>
        public void CopySelection()
        {
            var nodes = SelectedNodes
                .Where(n => !(n is Assistant.VScripts.Nodes.StartNode))
                .ToList();

            if (Graph == null || nodes.Count == 0)
            {
                Notify?.Invoke("Nothing selected to copy.");
                return;
            }

            try
            {
                // Teilgraph mit nur der Auswahl + den Links dazwischen — der
                // Serializer ist der verlaesslichste Klon-Weg (gleicher Code
                // wie die .vscript-Dateien).
                var ids = nodes.SelectMany(n => n.InputPins.Concat(n.OutputPins)).Select(p => p.Id).ToHashSet();
                var sub = new NodeGraph(Graph.Name);
                sub.Nodes.AddRange(nodes);
                sub.Links.AddRange(Graph.Links.Where(l =>
                    l.StartPinId != null && l.EndPinId != null &&
                    ids.Contains(l.StartPinId) && ids.Contains(l.EndPinId)));

                _clipboardJson = VScriptSerializer.Serialize(sub);
                Notify?.Invoke($"{nodes.Count} node(s) copied.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Razor/vscript-editor] Copy fehlgeschlagen: {ex}");
                Notify?.Invoke("Copy failed (see console).");
            }
        }

        public void CutSelection()
        {
            CopySelection();
            DeleteSelection();
        }

        public void PasteClipboard()
        {
            if (Graph == null || _clipboardJson == null)
            {
                Notify?.Invoke("Clipboard is empty.");
                return;
            }

            NodeGraph clone;
            try
            {
                clone = VScriptSerializer.Deserialize(_clipboardJson);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Razor/vscript-editor] Paste fehlgeschlagen: {ex}");
                Notify?.Invoke("Paste failed (see console).");
                return;
            }

            if (clone == null)
                return;

            var nodes = clone.Nodes.ToList();
            if (nodes.Count == 0)
                return;

            PushUndo();

            // Ziel: aktuelle Mausposition (bzw. Sichtmitte), relativ zur Kopie.
            double minX = nodes.Min(n => n.Position.X);
            double minY = nodes.Min(n => n.Position.Y);
            Point target = _lastPointerWorld == default ? ViewCenterWorld() : _lastPointerWorld;

            var pinMap = new Dictionary<string, string>();
            var pastedIds = new HashSet<string>();

            foreach (var node in nodes)
            {
                node.Id = Graph.GetNextNodeId();
                pastedIds.Add(node.Id);
                node.Position = new System.Numerics.Vector2(
                    (float) (node.Position.X - minX + target.X + 24),
                    (float) (node.Position.Y - minY + target.Y + 24));

                foreach (var pin in node.InputPins.Concat(node.OutputPins))
                {
                    string newId = Graph.GetNextPinId();
                    pinMap[pin.Id] = newId;
                    pin.Id = newId;
                    pin.NodeId = node.Id;
                }

                Graph.AddNode(node);
            }

            // Links zwischen den kopierten Nodes wiederherstellen.
            foreach (var link in clone.Links)
            {
                if (link.StartPinId == null || link.EndPinId == null)
                    continue;

                if (pinMap.TryGetValue(link.StartPinId, out string s) &&
                    pinMap.TryGetValue(link.EndPinId, out string t))
                {
                    Graph.AddLink(new NodeLink(Graph.GetNextLinkId(), s, t));
                }
            }

            RefreshListElementTypes();
            SelectedNodes.Clear();
            SelectedNodes.AddRange(Graph.Nodes.Where(n => pastedIds.Contains(n.Id)));
            SelectedLink = null;
            SelectedComment = null;
            SelectionChanged?.Invoke();
            GraphChanged?.Invoke();
            Notify?.Invoke($"{nodes.Count} node(s) pasted.");
            InvalidateVisual();
        }
    }
}
