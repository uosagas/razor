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
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Assistant.VScripts.Core;
using Assistant.VScripts.Nodes;

namespace Assistant.VScripts.Engine;

public class VScriptEngine
{
    private NodeGraph _currentGraph;
    private VScriptContext _context;
    private CancellationTokenSource _cancellationTokenSource;
    private bool _isRunning;
    private Task _executionTask;

    // Execution visualization and debugging
    private string _currentExecutingNodeId = null;
    private bool _isPaused = false;
    private string _pausedAtNodeId = null;

    // Execution trail for visualization (keeps history of recent executions)
    private readonly Dictionary<string, DateTime> _nodeExecutionTimes = new(); // NodeId -> Last execution time
    private readonly Dictionary<string, DateTime> _linkActivationTimes = new(); // LinkId -> Last activation time
    private readonly Dictionary<string, string> _nodeErrors = new(); // NodeId -> Error message
    private readonly object _visualizationLock = new object();

    public bool IsRunning => _isRunning;
    public bool IsPaused => _isPaused;
    public VScriptContext Context => _context;
    public string CurrentExecutingNodeId => _currentExecutingNodeId;
    public string PausedAtNodeId => _pausedAtNodeId;

    public Dictionary<string, DateTime> GetNodeExecutionTimes()
    {
        lock (_visualizationLock)
        {
            return new Dictionary<string, DateTime>(_nodeExecutionTimes);
        }
    }

    public Dictionary<string, DateTime> GetLinkActivationTimes()
    {
        lock (_visualizationLock)
        {
            return new Dictionary<string, DateTime>(_linkActivationTimes);
        }
    }

    public Dictionary<string, string> GetNodeErrors()
    {
        lock (_visualizationLock)
        {
            return new Dictionary<string, string>(_nodeErrors);
        }
    }

    public void ClearNodeError(string nodeId)
    {
        lock (_visualizationLock)
        {
            _nodeErrors.Remove(nodeId);
        }
    }

    public VScriptEngine()
    {
        _context = new VScriptContext();
    }

    public void Resume()
    {
        _isPaused = false;
    }

    public void LoadGraph(NodeGraph graph)
    {
        _currentGraph = graph;
    }

    public void Start()
    {
        if (_isRunning || _currentGraph == null)
        {
            return;
        }

        _context.Clear();

        // Initialize local variables with their default values
        foreach (var variable in _currentGraph.Variables)
        {
            if (variable.DefaultValue != null)
            {
                _context.SetVariable(variable.Name, variable.DefaultValue);
            }
        }

        // Clear execution trail and errors
        lock (_visualizationLock)
        {
            _nodeExecutionTimes.Clear();
            _linkActivationTimes.Clear();
            _nodeErrors.Clear();
        }

        _cancellationTokenSource = new CancellationTokenSource();
        _isRunning = true;

        _executionTask = Task.Run(() => ExecuteGraph(_cancellationTokenSource.Token));
    }

    public void Stop()
    {
        if (!_isRunning)
        {
            return;
        }

        _cancellationTokenSource?.Cancel();
        _isRunning = false;
        _context.ShouldStop = true;
    }

    public void ExecuteGraphSynchronously(NodeGraph graph, VScriptContext parentContext)
    {
        // Save the current graph and context
        var previousGraph = _currentGraph;
        var previousContext = _context;

        try
        {
            // Set up the nested script execution
            _currentGraph = graph;
            _context = new VScriptContext
            {
                Variables = new Dictionary<string, object>(parentContext.Variables), // Copy parent variables
                CurrentScriptName = graph.Name // Set the current script name
            };

            // Find the Start node
            var startNode = graph.Nodes.FirstOrDefault(n => n is StartNode);
            if (startNode == null)
            {
                parentContext.ErrorMessage = "No Start node found in nested VScript!";
                return;
            }

            // Execute the nested script synchronously (no cancellation token needed)
            ExecuteNodeChain(startNode, CancellationToken.None);

            // Check for errors
            if (!string.IsNullOrEmpty(_context.ErrorMessage))
            {
                parentContext.ErrorMessage = $"Nested script error: {_context.ErrorMessage}";
            }
        }
        catch (Exception ex)
        {
            parentContext.ErrorMessage = $"Nested script error: {ex.Message}";
        }
        finally
        {
            // Restore the previous graph and context
            _currentGraph = previousGraph;
            _context = previousContext;
        }
    }

    /// <summary>
    /// Executes a script with explicit parameters and returns output values.
    /// Used by ExecuteScriptNode for nested script execution with parameter passing.
    /// </summary>
    /// <param name="graph">The script to execute</param>
    /// <param name="parentContext">Parent execution context</param>
    /// <param name="parameters">Dictionary of parameter names to values</param>
    /// <returns>Dictionary of output names to values, or null if execution failed</returns>
    public Dictionary<string, object> ExecuteGraphWithParameters(
        NodeGraph graph,
        VScriptContext parentContext,
        Dictionary<string, object> parameters)
    {
        // Save the current graph and context
        var previousGraph = _currentGraph;
        var previousContext = _context;

        try
        {
            // Set up the nested script execution
            _currentGraph = graph;
            _context = new VScriptContext
            {
                CurrentScriptName = graph.Name
            };

            // Initialize variables with their default values first
            foreach (var variable in graph.Variables)
            {
                if (variable.DefaultValue != null)
                {
                    _context.SetVariable(variable.Name, variable.DefaultValue);
                }
            }

            // Override with passed parameter values
            if (parameters != null)
            {
                foreach (var kvp in parameters)
                {
                    _context.SetVariable(kvp.Key, kvp.Value);
                }
            }

            // Find the Start node
            var startNode = graph.Nodes.FirstOrDefault(n => n is StartNode) as StartNode;
            if (startNode == null)
            {
                parentContext.ErrorMessage = "No Start node found in nested VScript!";
                return null;
            }

            // Rebuild the internal pin ID mappings from existing pins.
            // NOTE: Do NOT call UpdateParameterPins() here - that would regenerate pins with new GUIDs,
            // breaking all existing link connections. The pins are already correct from deserialization.
            startNode.RebuildParameterPinMapping();

            // Execute the nested script synchronously
            ExecuteNodeChain(startNode, CancellationToken.None);

            // Check for errors
            if (!string.IsNullOrEmpty(_context.ErrorMessage))
            {
                parentContext.ErrorMessage = $"Nested script error: {_context.ErrorMessage}";
                return null;
            }

            // Collect return values (variables with __return_ prefix set by ReturnNode)
            var returnValues = new Dictionary<string, object>();
            foreach (var variable in graph.Variables.Where(v => v.Scope == VariableScope.Output))
            {
                var returnValue = _context.GetVariable($"__return_{variable.Name}");
                if (returnValue != null)
                {
                    returnValues[variable.Name] = returnValue;
                }
            }

            return returnValues;
        }
        catch (Exception ex)
        {
            parentContext.ErrorMessage = $"Nested script error: {ex.Message}";
            return null;
        }
        finally
        {
            // Restore the previous graph and context
            _currentGraph = previousGraph;
            _context = previousContext;
        }
    }

    private void ExecuteGraph(CancellationToken cancellationToken)
    {
        try
        {
            // Set the current script name in context
            _context.CurrentScriptName = _currentGraph?.Name;

            // Find the Start node
            var startNode = _currentGraph.Nodes.FirstOrDefault(n => n is StartNode);
            if (startNode == null)
            {
                Message.Error("No Start node found in VScript!");
                _isRunning = false;
                return;
            }

            // Execute from start node
            ExecuteNodeChain(startNode, cancellationToken);

            if (!cancellationToken.IsCancellationRequested && string.IsNullOrEmpty(_context.ErrorMessage))
            {
                Message.Success("VScript completed successfully!");
            }
            else if (!cancellationToken.IsCancellationRequested)
            {
                // Razor-Zusatz: Report-Dialog fuer Node-Fehler (Kommentar +
                // optional Graph als Anhang, Dedupe 60s).
                ReportGraphError(_context.ErrorMessage);
            }
        }
        catch (Exception ex)
        {
            Message.Error($"VScript error: {ex.Message}");

            // Razor-Zusatz: unerwarteter Engine-Fehler — mit Stacktrace melden.
            ReportGraphError($"{ex.Message}\n\n{ex}");
        }
        finally
        {
            _isRunning = false;
        }
    }

    /// <summary>Razor-Zusatz: VScript-Fehler still in CrashLogs protokollieren
    /// (Graph als serialisiertes .vscript-JSON), kein Report-Fenster.</summary>
    private void ReportGraphError(string errorText)
    {
        try
        {
            string content = _currentGraph != null
                ? Assistant.VScripts.Data.VScriptSerializer.Serialize(_currentGraph)
                : null;

            CrashReporter.ReportScriptError("VScript", _currentGraph?.Name, content, errorText);
        }
        catch (Exception reportEx)
        {
            Console.WriteLine($"[Razor] VScript-Fehlerreport fehlgeschlagen: {reportEx.Message}");
        }
    }

    private void ExecuteNodeChain(VScriptNode node, CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested || _context.ShouldStop)
        {
            return;
        }

        try
        {
            // Record node execution for visualization
            lock (_visualizationLock)
            {
                _nodeExecutionTimes[node.Id] = DateTime.Now;
            }
            _currentExecutingNodeId = node.Id;

            // Check for breakpoint
            if (node.HasBreakpoint)
            {
                _isPaused = true;
                _pausedAtNodeId = node.Id;
                Message.Info($"Breakpoint hit at node: {node.Name}");

                // Wait until resumed
                while (_isPaused && !cancellationToken.IsCancellationRequested)
                {
                    Thread.Sleep(100);
                }

                _pausedAtNodeId = null;
            }

            // Evaluate input pins from connected nodes
            EvaluateInputPins(node);

            // Razor-Zusatz: Fehler aus der Pin-Auswertung (pure Daten-Nodes wie
            // "Get Diff Hits") sind bereits am VERURSACHER-Node attribuiert
            // (siehe EvaluateInputPins) — hier nur noch den Flow-Node markieren
            // und die Kette stoppen, statt den Fehler zu verschlucken.
            if (!string.IsNullOrEmpty(_context.ErrorMessage))
            {
                lock (_visualizationLock)
                {
                    _nodeErrors[node.Id] = _context.ErrorMessage;
                }
                Message.Error(_context.ErrorMessage);
                _context.ShouldStop = true;
                _currentExecutingNodeId = null;
                return;
            }

            // Execute the node
            node.Execute(_context);

            _currentExecutingNodeId = null;

            // Check if the node set an error message
            if (!string.IsNullOrEmpty(_context.ErrorMessage))
            {
                // Store error for visualization
                lock (_visualizationLock)
                {
                    _nodeErrors[node.Id] = _context.ErrorMessage;
                }
                Message.Error(_context.ErrorMessage);
                _context.ShouldStop = true;
                return;
            }

            // Special handling for ForLoopNode
            if (node is ForLoopNode loopNode)
            {
                ExecuteForLoop(loopNode, cancellationToken);
                return;
            }

            // Special handling for ForEachNode
            if (node is ForEachNode forEachNode)
            {
                ExecuteForEach(forEachNode, cancellationToken);
                return;
            }

            // Special handling for MoveItemsNode
            if (node is MoveItemsNode moveItemsNode)
            {
                ExecuteMoveItems(moveItemsNode, cancellationToken);
                return;
            }

            // Special handling for WhileLoopNode
            if (node is WhileLoopNode whileNode)
            {
                ExecuteWhileLoop(whileNode, cancellationToken);
                return;
            }

            // Special handling for BranchNode
            if (node is BranchNode branchNode)
            {
                ExecuteBranch(branchNode, cancellationToken);
                return;
            }

            // Special handling for SequenceNode
            if (node is SequenceNode sequenceNode)
            {
                ExecuteSequence(sequenceNode, cancellationToken);
                return;
            }

            // Find and execute connected nodes via flow pins
            var flowOutputPin = node.OutputPins.FirstOrDefault(p => p.Type == PinType.Flow);
            if (flowOutputPin != null)
            {
                var connectedNodes = _currentGraph.GetConnectedNodes(flowOutputPin.Id);
                foreach (var connectedNode in connectedNodes)
                {
                    // Find the link ID for visualization
                    var link = _currentGraph.Links.FirstOrDefault(l =>
                        l.StartPinId == flowOutputPin.Id &&
                        _currentGraph.GetNodeByPinId(l.EndPinId)?.Id == connectedNode.Id);

                    if (link != null)
                    {
                        // Record link activation for visualization
                        lock (_visualizationLock)
                        {
                            _linkActivationTimes[link.Id] = DateTime.Now;
                        }
                    }

                    ExecuteNodeChain(connectedNode, cancellationToken);
                }
            }
        }
        catch (Exception ex)
        {
            _context.ErrorMessage = $"Error in node '{node.Name}': {ex.Message}";
            // Store error for visualization
            lock (_visualizationLock)
            {
                _nodeErrors[node.Id] = _context.ErrorMessage;
            }
            Message.Error(_context.ErrorMessage);
            _context.ShouldStop = true;
        }
    }

    private void ExecuteForLoop(ForLoopNode loopNode, CancellationToken cancellationToken)
    {
        int firstIndex = loopNode.GetFirstIndex();
        int lastIndex = loopNode.GetLastIndex();

        var loopBodyPin = loopNode.GetLoopBodyPin();
        var completedPin = loopNode.GetCompletedPin();

        // Execute loop body for each iteration
        for (int i = firstIndex; i <= lastIndex; i++)
        {
            if (cancellationToken.IsCancellationRequested || _context.ShouldStop)
            {
                return;
            }

            // Reset break flag at start of each iteration
            _context.BreakRequested = false;

            // Set current index
            loopNode.SetCurrentIndex(i);

            // Execute nodes connected to Loop Body pin
            if (loopBodyPin != null)
            {
                var connectedNodes = _currentGraph.GetConnectedNodes(loopBodyPin.Id);
                foreach (var connectedNode in connectedNodes)
                {
                    // Find and record link activation for visualization
                    var link = _currentGraph.Links.FirstOrDefault(l =>
                        l.StartPinId == loopBodyPin.Id &&
                        _currentGraph.GetNodeByPinId(l.EndPinId)?.Id == connectedNode.Id);

                    if (link != null)
                    {
                        lock (_visualizationLock)
                        {
                            _linkActivationTimes[link.Id] = DateTime.Now;
                        }
                    }

                    ExecuteNodeChain(connectedNode, cancellationToken);

                    // Check if break was requested
                    if (_context.BreakRequested)
                    {
                        _context.BreakRequested = false; // Reset for next loop
                        goto LoopExit;
                    }
                }
            }

            // Add 20ms delay to prevent CPU overload
            try
            {
                Task.Delay(20, cancellationToken).Wait();
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }

        LoopExit:

        // Execute nodes connected to Completed pin
        if (completedPin != null)
        {
            var connectedNodes = _currentGraph.GetConnectedNodes(completedPin.Id);
            foreach (var connectedNode in connectedNodes)
            {
                // Find and record link activation for visualization
                var link = _currentGraph.Links.FirstOrDefault(l =>
                    l.StartPinId == completedPin.Id &&
                    _currentGraph.GetNodeByPinId(l.EndPinId)?.Id == connectedNode.Id);

                if (link != null)
                {
                    lock (_visualizationLock)
                    {
                        _linkActivationTimes[link.Id] = DateTime.Now;
                    }
                }

                ExecuteNodeChain(connectedNode, cancellationToken);
            }
        }
    }

    private void ExecuteForEach(ForEachNode forEachNode, CancellationToken cancellationToken)
    {
        var list = forEachNode.GetList();

        if (list == null)
        {
            _context.ErrorMessage = "ForEach: List is null";
            return;
        }

        var loopBodyPin = forEachNode.GetLoopBodyPin();
        var completedPin = forEachNode.GetCompletedPin();

        // Execute loop body for each element
        int index = 0;
        foreach (var element in list)
        {
            if (cancellationToken.IsCancellationRequested || _context.ShouldStop)
            {
                return;
            }

            // Reset break flag at start of each iteration
            _context.BreakRequested = false;

            // Set current element and index
            forEachNode.SetCurrentElement(element);
            forEachNode.SetCurrentIndex(index);

            // Execute nodes connected to Loop Body pin
            if (loopBodyPin != null)
            {
                var connectedNodes = _currentGraph.GetConnectedNodes(loopBodyPin.Id);
                foreach (var connectedNode in connectedNodes)
                {
                    // Find and record link activation for visualization
                    var link = _currentGraph.Links.FirstOrDefault(l =>
                        l.StartPinId == loopBodyPin.Id &&
                        _currentGraph.GetNodeByPinId(l.EndPinId)?.Id == connectedNode.Id);

                    if (link != null)
                    {
                        lock (_visualizationLock)
                        {
                            _linkActivationTimes[link.Id] = DateTime.Now;
                        }
                    }

                    ExecuteNodeChain(connectedNode, cancellationToken);

                    // Check if break was requested
                    if (_context.BreakRequested)
                    {
                        _context.BreakRequested = false; // Reset for next loop
                        goto LoopExit;
                    }
                }
            }

            // Add 20ms delay to prevent CPU overload
            try
            {
                Task.Delay(20, cancellationToken).Wait();
            }
            catch (OperationCanceledException)
            {
                return;
            }

            index++;
        }

        LoopExit:

        // Execute nodes connected to Completed pin
        if (completedPin != null)
        {
            var connectedNodes = _currentGraph.GetConnectedNodes(completedPin.Id);
            foreach (var connectedNode in connectedNodes)
            {
                // Find and record link activation for visualization
                var link = _currentGraph.Links.FirstOrDefault(l =>
                    l.StartPinId == completedPin.Id &&
                    _currentGraph.GetNodeByPinId(l.EndPinId)?.Id == connectedNode.Id);

                if (link != null)
                {
                    lock (_visualizationLock)
                    {
                        _linkActivationTimes[link.Id] = DateTime.Now;
                    }
                }

                ExecuteNodeChain(connectedNode, cancellationToken);
            }
        }
    }

    private void ExecuteMoveItems(MoveItemsNode moveItemsNode, CancellationToken cancellationToken)
    {
        var itemsToMove = moveItemsNode.GetItemsToMove();

        if (itemsToMove == null || itemsToMove.Count == 0)
        {
            // No items to move - set overall success to false and execute Completed pin
            moveItemsNode.SetOverallSuccess(false);

            var completedPin = moveItemsNode.GetCompletedPin();
            if (completedPin != null)
            {
                var connectedNodes = _currentGraph.GetConnectedNodes(completedPin.Id);
                foreach (var connectedNode in connectedNodes)
                {
                    var link = _currentGraph.Links.FirstOrDefault(l =>
                        l.StartPinId == completedPin.Id &&
                        _currentGraph.GetNodeByPinId(l.EndPinId)?.Id == connectedNode.Id);

                    if (link != null)
                    {
                        lock (_visualizationLock)
                        {
                            _linkActivationTimes[link.Id] = DateTime.Now;
                        }
                    }

                    ExecuteNodeChain(connectedNode, cancellationToken);
                }
            }
            return;
        }

        var loopBodyPin = moveItemsNode.GetLoopBodyPin();
        var completedPin2 = moveItemsNode.GetCompletedPin();
        var targetContainer = moveItemsNode.GetTargetContainer();

        if (targetContainer == 0)
        {
            _context.ErrorMessage = "Move Items: Target container must be specified";
            return;
        }

        bool overallSuccess = true;

        // Execute loop body for each item
        foreach (var item in itemsToMove)
        {
            if (cancellationToken.IsCancellationRequested || _context.ShouldStop)
            {
                return;
            }

            // Reset break flag at start of each iteration
            _context.BreakRequested = false;

            // Try to move the item
            bool success = false;
            bool moved = moveItemsNode.TryMoveItem(item, targetContainer, out success);

            if (!moved || !success)
            {
                overallSuccess = false;
            }

            // Set current item and success for this iteration
            moveItemsNode.SetCurrentItem(item, success);

            // Execute nodes connected to Loop Body pin
            if (loopBodyPin != null)
            {
                var connectedNodes = _currentGraph.GetConnectedNodes(loopBodyPin.Id);
                foreach (var connectedNode in connectedNodes)
                {
                    // Find and record link activation for visualization
                    var link = _currentGraph.Links.FirstOrDefault(l =>
                        l.StartPinId == loopBodyPin.Id &&
                        _currentGraph.GetNodeByPinId(l.EndPinId)?.Id == connectedNode.Id);

                    if (link != null)
                    {
                        lock (_visualizationLock)
                        {
                            _linkActivationTimes[link.Id] = DateTime.Now;
                        }
                    }

                    ExecuteNodeChain(connectedNode, cancellationToken);

                    // Check if break was requested
                    if (_context.BreakRequested)
                    {
                        _context.BreakRequested = false; // Reset for next loop
                        goto LoopExit;
                    }
                }
            }

            // Add delay after each item
            int delayMs = moveItemsNode.GetDelayMs();
            if (delayMs > 0)
            {
                try
                {
                    Task.Delay(delayMs, cancellationToken).Wait();
                }
                catch (OperationCanceledException)
                {
                    return;
                }
            }
        }

        LoopExit:

        // Set overall success
        moveItemsNode.SetOverallSuccess(overallSuccess);

        // Execute nodes connected to Completed pin
        if (completedPin2 != null)
        {
            var connectedNodes = _currentGraph.GetConnectedNodes(completedPin2.Id);
            foreach (var connectedNode in connectedNodes)
            {
                // Find and record link activation for visualization
                var link = _currentGraph.Links.FirstOrDefault(l =>
                    l.StartPinId == completedPin2.Id &&
                    _currentGraph.GetNodeByPinId(l.EndPinId)?.Id == connectedNode.Id);

                if (link != null)
                {
                    lock (_visualizationLock)
                    {
                        _linkActivationTimes[link.Id] = DateTime.Now;
                    }
                }

                ExecuteNodeChain(connectedNode, cancellationToken);
            }
        }
    }

    private void ExecuteWhileLoop(WhileLoopNode loopNode, CancellationToken cancellationToken)
    {
        var loopBodyPin = loopNode.GetLoopBodyPin();
        var completedPin = loopNode.GetCompletedPin();

        // Execute loop body while condition is true
        while (true)
        {
            if (cancellationToken.IsCancellationRequested || _context.ShouldStop)
            {
                return;
            }

            // Evaluate the condition
            EvaluateInputPins(loopNode);
            bool condition = loopNode.GetCondition();

            // If condition is false, exit loop
            if (!condition)
            {
                break;
            }

            // Reset break flag at start of each iteration
            _context.BreakRequested = false;

            // Execute nodes connected to Loop Body pin
            if (loopBodyPin != null)
            {
                var connectedNodes = _currentGraph.GetConnectedNodes(loopBodyPin.Id);
                foreach (var connectedNode in connectedNodes)
                {
                    // Find and record link activation for visualization
                    var link = _currentGraph.Links.FirstOrDefault(l =>
                        l.StartPinId == loopBodyPin.Id &&
                        _currentGraph.GetNodeByPinId(l.EndPinId)?.Id == connectedNode.Id);

                    if (link != null)
                    {
                        lock (_visualizationLock)
                        {
                            _linkActivationTimes[link.Id] = DateTime.Now;
                        }
                    }

                    ExecuteNodeChain(connectedNode, cancellationToken);

                    // Check if break was requested
                    if (_context.BreakRequested)
                    {
                        _context.BreakRequested = false; // Reset
                        goto LoopExit;
                    }
                }
            }

            // Add 20ms delay to prevent CPU overload
            try
            {
                Task.Delay(20, cancellationToken).Wait();
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }

        LoopExit:

        // Execute nodes connected to Completed pin
        if (completedPin != null)
        {
            var connectedNodes = _currentGraph.GetConnectedNodes(completedPin.Id);
            foreach (var connectedNode in connectedNodes)
            {
                // Find and record link activation for visualization
                var link = _currentGraph.Links.FirstOrDefault(l =>
                    l.StartPinId == completedPin.Id &&
                    _currentGraph.GetNodeByPinId(l.EndPinId)?.Id == connectedNode.Id);

                if (link != null)
                {
                    lock (_visualizationLock)
                    {
                        _linkActivationTimes[link.Id] = DateTime.Now;
                    }
                }

                ExecuteNodeChain(connectedNode, cancellationToken);
            }
        }
    }

    private void ExecuteBranch(BranchNode branchNode, CancellationToken cancellationToken)
    {
        // Get the condition value
        bool condition = branchNode.GetCondition();

        // Get the appropriate output pin based on condition
        var outputPin = condition ? branchNode.GetTruePin() : branchNode.GetFalsePin();

        if (outputPin != null)
        {
            // Execute nodes connected to the chosen path
            var connectedNodes = _currentGraph.GetConnectedNodes(outputPin.Id);
            foreach (var connectedNode in connectedNodes)
            {
                // Find the link ID for visualization
                var link = _currentGraph.Links.FirstOrDefault(l =>
                    l.StartPinId == outputPin.Id &&
                    _currentGraph.GetNodeByPinId(l.EndPinId)?.Id == connectedNode.Id);

                if (link != null)
                {
                    // Record link activation for visualization
                    lock (_visualizationLock)
                    {
                        _linkActivationTimes[link.Id] = DateTime.Now;
                    }
                }

                ExecuteNodeChain(connectedNode, cancellationToken);
            }
        }
    }

    private void ExecuteSequence(SequenceNode sequenceNode, CancellationToken cancellationToken)
    {
        // Execute all flow output pins in sequential order
        foreach (var outputPin in sequenceNode.OutputPins.Where(p => p.Type == PinType.Flow))
        {
            if (cancellationToken.IsCancellationRequested || _context.ShouldStop)
            {
                return;
            }

            // Execute nodes connected to this output pin
            var connectedNodes = _currentGraph.GetConnectedNodes(outputPin.Id);
            foreach (var connectedNode in connectedNodes)
            {
                // Find the link ID for visualization
                var link = _currentGraph.Links.FirstOrDefault(l =>
                    l.StartPinId == outputPin.Id &&
                    _currentGraph.GetNodeByPinId(l.EndPinId)?.Id == connectedNode.Id);

                if (link != null)
                {
                    // Record link activation for visualization
                    lock (_visualizationLock)
                    {
                        _linkActivationTimes[link.Id] = DateTime.Now;
                    }
                }

                ExecuteNodeChain(connectedNode, cancellationToken);
            }
        }
    }

    private void EvaluateInputPins(VScriptNode node)
    {
        foreach (var inputPin in node.InputPins.Where(p => p.Type != PinType.Flow))
        {
            // Find the link connected to this input pin
            var link = _currentGraph.Links.FirstOrDefault(l => l.EndPinId == inputPin.Id);
            if (link != null)
            {
                var sourcePin = _currentGraph.GetPin(link.StartPinId);
                if (sourcePin != null)
                {
                    // If the source node is a pure function (no flow pins), execute it first
                    var sourceNode = _currentGraph.GetNodeByPinId(sourcePin.Id);
                    if (sourceNode != null && !sourceNode.InputPins.Any(p => p.Type == PinType.Flow))
                    {
                        EvaluateInputPins(sourceNode);

                        // Razor-Zusatz: Fehler am VERURSACHER attribuieren — wirft
                        // oder meldet ein purer Daten-Node, wird ER markiert (und
                        // nicht nur der Flow-Node, der ihn angefragt hat).
                        if (!string.IsNullOrEmpty(_context.ErrorMessage))
                        {
                            return; // tieferer Node hat den Fehler bereits attribuiert
                        }

                        try
                        {
                            sourceNode.Execute(_context);
                        }
                        catch (Exception ex)
                        {
                            _context.ErrorMessage = $"Error in node '{sourceNode.Name}': {ex.Message}";
                        }

                        if (!string.IsNullOrEmpty(_context.ErrorMessage))
                        {
                            lock (_visualizationLock)
                            {
                                if (!_nodeErrors.ContainsKey(sourceNode.Id))
                                    _nodeErrors[sourceNode.Id] = _context.ErrorMessage;
                            }

                            return;
                        }
                    }

                    // Copy the value from the source pin
                    inputPin.Value = sourcePin.Value;
                }
            }
            else
            {
                // Clear Object pin values when not connected (they must be explicitly wired)
                if (inputPin.Type == PinType.Object)
                {
                    inputPin.Value = null;
                }
            }
        }
    }
}
