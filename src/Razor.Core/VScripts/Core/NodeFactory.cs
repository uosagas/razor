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
using Assistant.VScripts.Nodes;

namespace Assistant.VScripts.Core;

public class NodeDefinition
{
    public string Name { get; set; }
    public string TypeName { get; set; }
    public NodeCategory Category { get; set; }
    public string Description { get; set; }
    public Func<string, string, VScriptNode> Factory { get; set; }
    public bool HideInPalette { get; set; } // Hide from sidebar but show in context menus
    public bool IsExperimental { get; set; } // Show experimental warning

    public NodeDefinition(string name, string typeName, NodeCategory category, string description, Func<string, string, VScriptNode> factory, bool hideInPalette = false, bool isExperimental = false)
    {
        Name = name;
        TypeName = typeName;
        Category = category;
        Description = description;
        Factory = factory;
        HideInPalette = hideInPalette;
        IsExperimental = isExperimental;
    }
}

public static class NodeFactory
{
    private static readonly List<NodeDefinition> _nodeDefinitions = new();

    static NodeFactory()
    {
        RegisterDefaultNodes();
    }

    private static void RegisterDefaultNodes()
    {
        // Event Nodes
        Register("Start", nameof(StartNode), NodeCategory.Event,
            "Entry point for VScript execution",
            (id, pinId) => new StartNode(id, pinId));

        // UI Nodes
        Register("Print Message", nameof(PrintMessageNode), NodeCategory.UI,
            "Display a message to the user",
            (id, pinId) => new PrintMessageNode(id, pinId));

        // Variable Nodes are created via the sidebar Variables section, not the node palette

        // Math Nodes - Basic Operations
        Register("Add", nameof(AddNumbersNode), NodeCategory.Math,
            "Add two numbers together",
            (id, pinId) => new AddNumbersNode(id, pinId));

        Register("Subtract", nameof(SubtractNumbersNode), NodeCategory.Math,
            "Subtract B from A",
            (id, pinId) => new SubtractNumbersNode(id, pinId));

        Register("Multiply", nameof(MultiplyNumbersNode), NodeCategory.Math,
            "Multiply two numbers together",
            (id, pinId) => new MultiplyNumbersNode(id, pinId));

        Register("Divide", nameof(DivideNumbersNode), NodeCategory.Math,
            "Divide A by B",
            (id, pinId) => new DivideNumbersNode(id, pinId));

        Register("Modulo", nameof(ModuloNode), NodeCategory.Math,
            "Calculate remainder of A divided by B",
            (id, pinId) => new ModuloNode(id, pinId));

        Register("Power", nameof(PowerNode), NodeCategory.Math,
            "Raise base to exponent power",
            (id, pinId) => new PowerNode(id, pinId));

        // Math Nodes - Advanced
        Register("Square Root", nameof(SquareRootNode), NodeCategory.Math,
            "Calculate square root of value",
            (id, pinId) => new SquareRootNode(id, pinId));

        Register("Absolute", nameof(AbsoluteNode), NodeCategory.Math,
            "Get absolute value (always positive)",
            (id, pinId) => new AbsoluteNode(id, pinId));

        Register("Min", nameof(MinNode), NodeCategory.Math,
            "Get minimum of two numbers",
            (id, pinId) => new MinNode(id, pinId));

        Register("Max", nameof(MaxNode), NodeCategory.Math,
            "Get maximum of two numbers",
            (id, pinId) => new MaxNode(id, pinId));

        Register("Clamp", nameof(ClampNode), NodeCategory.Math,
            "Clamp value between min and max",
            (id, pinId) => new ClampNode(id, pinId));

        Register("Round", nameof(RoundNode), NodeCategory.Math,
            "Round to nearest integer",
            (id, pinId) => new RoundNode(id, pinId));

        Register("Floor", nameof(FloorNode), NodeCategory.Math,
            "Round down to integer",
            (id, pinId) => new FloorNode(id, pinId));

        Register("Ceiling", nameof(CeilingNode), NodeCategory.Math,
            "Round up to integer",
            (id, pinId) => new CeilingNode(id, pinId));

        // String Nodes
        Register("Concat Strings", nameof(ConcatenateStringsNode), NodeCategory.String,
            "Concatenate two strings together",
            (id, pinId) => new ConcatenateStringsNode(id, pinId));

        Register("Append String", nameof(AppendStringNode), NodeCategory.String,
            "Append strings with a separator",
            (id, pinId) => new AppendStringNode(id, pinId));

        Register("String Length", nameof(StringLengthNode), NodeCategory.String,
            "Get the length of a string",
            (id, pinId) => new StringLengthNode(id, pinId));

        Register("String Contains", nameof(StringContainsNode), NodeCategory.String,
            "Check if string contains another string",
            (id, pinId) => new StringContainsNode(id, pinId));

        Register("To Upper Case", nameof(StringToUpperNode), NodeCategory.String,
            "Convert string to upper case",
            (id, pinId) => new StringToUpperNode(id, pinId));

        Register("To Lower Case", nameof(StringToLowerNode), NodeCategory.String,
            "Convert string to lower case",
            (id, pinId) => new StringToLowerNode(id, pinId));

        // Flow Nodes
        Register("Delay", nameof(DelayNode), NodeCategory.Flow,
            "Pause execution for a specified time",
            (id, pinId) => new DelayNode(id, pinId));

        Register("Sequence", nameof(SequenceNode), NodeCategory.Flow,
            "Execute outputs in sequence",
            (id, pinId) => new SequenceNode(id, pinId));

        Register("Execute Script", nameof(ExecuteScriptNode), NodeCategory.Flow,
            "Execute another VScript with parameters",
            (id, pinId) => new ExecuteScriptNode(id, pinId));

        Register("Return", nameof(ReturnNode), NodeCategory.Flow,
            "End script execution and return output values",
            (id, pinId) => new ReturnNode(id, pinId));

        Register("For Loop", nameof(ForLoopNode), NodeCategory.Flow,
            "Execute loop body for each index in range",
            (id, pinId) => new ForLoopNode(id, pinId));

        Register("While Loop", nameof(WhileLoopNode), NodeCategory.Flow,
            "Execute loop body while condition is true",
            (id, pinId) => new WhileLoopNode(id, pinId));

        Register("For Each", nameof(ForEachNode), NodeCategory.Flow,
            "Execute loop body for each element in list",
            (id, pinId) => new ForEachNode(id, pinId));

        Register("Break", nameof(BreakNode), NodeCategory.Flow,
            "Exit the current loop",
            (id, pinId) => new BreakNode(id, pinId));

        // Logic Nodes
        Register("Branch", nameof(BranchNode), NodeCategory.Logic,
            "Execute different paths based on condition",
            (id, pinId) => new BranchNode(id, pinId));

        Register("Compare", nameof(CompareNumbersNode), NodeCategory.Logic,
            "Compare two numbers",
            (id, pinId) => new CompareNumbersNode(id, pinId));

        Register("NOT", nameof(NotNode), NodeCategory.Logic,
            "Invert boolean value",
            (id, pinId) => new NotNode(id, pinId));

        Register("AND", nameof(AndNode), NodeCategory.Logic,
            "Logical AND of two booleans",
            (id, pinId) => new AndNode(id, pinId));

        Register("OR", nameof(OrNode), NodeCategory.Logic,
            "Logical OR of two booleans",
            (id, pinId) => new OrNode(id, pinId));

        Register("Is Valid", nameof(IsValidNode), NodeCategory.Logic,
            "Check if a value is valid (not null, not empty)",
            (id, pinId) => new IsValidNode(id, pinId));

        // Game Nodes - References
        Register("Player", nameof(PlayerNode), NodeCategory.Game,
            "Get the player object",
            (id, pinId) => new PlayerNode(id, pinId));

        // Game Nodes - Player Properties (hidden from palette, only shown in Player object context menu)
        Register("Get Hits", nameof(GetPlayerHitsNode), NodeCategory.Game,
            "Get player's current hits",
            (id, pinId) => new GetPlayerHitsNode(id, pinId), hideInPalette: true);

        Register("Get Hits Max", nameof(GetPlayerHitsMaxNode), NodeCategory.Game,
            "Get player's maximum hits",
            (id, pinId) => new GetPlayerHitsMaxNode(id, pinId), hideInPalette: true);

        Register("Get Stamina", nameof(GetPlayerStaminaNode), NodeCategory.Game,
            "Get player's current stamina",
            (id, pinId) => new GetPlayerStaminaNode(id, pinId), hideInPalette: true);

        Register("Get Stamina Max", nameof(GetPlayerStaminaMaxNode), NodeCategory.Game,
            "Get player's maximum stamina",
            (id, pinId) => new GetPlayerStaminaMaxNode(id, pinId), hideInPalette: true);

        Register("Get Mana", nameof(GetPlayerManaNode), NodeCategory.Game,
            "Get player's current mana",
            (id, pinId) => new GetPlayerManaNode(id, pinId), hideInPalette: true);

        Register("Get Mana Max", nameof(GetPlayerManaMaxNode), NodeCategory.Game,
            "Get player's maximum mana",
            (id, pinId) => new GetPlayerManaMaxNode(id, pinId), hideInPalette: true);

        Register("Get Str", nameof(GetPlayerStrNode), NodeCategory.Game,
            "Get player's strength",
            (id, pinId) => new GetPlayerStrNode(id, pinId), hideInPalette: true);

        Register("Get Dex", nameof(GetPlayerDexNode), NodeCategory.Game,
            "Get player's dexterity",
            (id, pinId) => new GetPlayerDexNode(id, pinId), hideInPalette: true);

        Register("Get Int", nameof(GetPlayerIntNode), NodeCategory.Game,
            "Get player's intelligence",
            (id, pinId) => new GetPlayerIntNode(id, pinId), hideInPalette: true);

        Register("Get Hue", nameof(GetPlayerHueNode), NodeCategory.Game,
            "Get player's hue/color",
            (id, pinId) => new GetPlayerHueNode(id, pinId), hideInPalette: true);

        Register("Get Graphic", nameof(GetPlayerGraphicNode), NodeCategory.Game,
            "Get player's graphic ID",
            (id, pinId) => new GetPlayerGraphicNode(id, pinId), hideInPalette: true);

        Register("Get Serial", nameof(GetPlayerSerialNode), NodeCategory.Game,
            "Get player's serial number",
            (id, pinId) => new GetPlayerSerialNode(id, pinId), hideInPalette: true);

        Register("Get Distance", nameof(GetPlayerDistanceNode), NodeCategory.Game,
            "Get player's distance from self (always 0)",
            (id, pinId) => new GetPlayerDistanceNode(id, pinId), hideInPalette: true);

        Register("Get Direction", nameof(GetPlayerDirectionNode), NodeCategory.Game,
            "Get player's facing direction",
            (id, pinId) => new GetPlayerDirectionNode(id, pinId), hideInPalette: true);

        Register("Get Followers", nameof(GetPlayerFollowersNode), NodeCategory.Game,
            "Get player's current followers",
            (id, pinId) => new GetPlayerFollowersNode(id, pinId), hideInPalette: true);

        Register("Get Max Followers", nameof(GetPlayerMaxFollowersNode), NodeCategory.Game,
            "Get player's maximum followers",
            (id, pinId) => new GetPlayerMaxFollowersNode(id, pinId), hideInPalette: true);

        Register("Get Gold", nameof(GetPlayerGoldNode), NodeCategory.Game,
            "Get player's gold amount",
            (id, pinId) => new GetPlayerGoldNode(id, pinId), hideInPalette: true);

        Register("Get Luck", nameof(GetPlayerLuckNode), NodeCategory.Game,
            "Get player's luck",
            (id, pinId) => new GetPlayerLuckNode(id, pinId), hideInPalette: true);

        Register("Get Tithing Points", nameof(GetPlayerTithingPointsNode), NodeCategory.Game,
            "Get player's tithing points",
            (id, pinId) => new GetPlayerTithingPointsNode(id, pinId), hideInPalette: true);

        Register("Get Weight", nameof(GetPlayerWeightNode), NodeCategory.Game,
            "Get player's current weight",
            (id, pinId) => new GetPlayerWeightNode(id, pinId), hideInPalette: true);

        Register("Get Max Weight", nameof(GetPlayerMaxWeightNode), NodeCategory.Game,
            "Get player's maximum weight",
            (id, pinId) => new GetPlayerMaxWeightNode(id, pinId), hideInPalette: true);

        Register("Get Diff Weight", nameof(GetPlayerDiffWeightNode), NodeCategory.Game,
            "Get player's weight difference (Weight - MaxWeight)",
            (id, pinId) => new GetPlayerDiffWeightNode(id, pinId), hideInPalette: true);

        Register("Get Stats Cap", nameof(GetPlayerStatsCapNode), NodeCategory.Game,
            "Get player's stats cap",
            (id, pinId) => new GetPlayerStatsCapNode(id, pinId), hideInPalette: true);

        Register("Get Physical Resistance", nameof(GetPlayerPhysicalResistanceNode), NodeCategory.Game,
            "Get player's physical resistance",
            (id, pinId) => new GetPlayerPhysicalResistanceNode(id, pinId), hideInPalette: true);

        Register("Get Diff Hits", nameof(GetPlayerDiffHitsNode), NodeCategory.Game,
            "Get player's hits difference (Hits - HitsMax)",
            (id, pinId) => new GetPlayerDiffHitsNode(id, pinId), hideInPalette: true);

        Register("Is Destroyed", nameof(GetPlayerIsDestroyedNode), NodeCategory.Game,
            "Check if player is destroyed",
            (id, pinId) => new GetPlayerIsDestroyedNode(id, pinId), hideInPalette: true);

        Register("Is Running", nameof(GetPlayerIsRunningNode), NodeCategory.Game,
            "Check if player is running",
            (id, pinId) => new GetPlayerIsRunningNode(id, pinId), hideInPalette: true);

        Register("Is Paralyzed", nameof(GetPlayerIsParalyzedNode), NodeCategory.Game,
            "Check if player is paralyzed",
            (id, pinId) => new GetPlayerIsParalyzedNode(id, pinId), hideInPalette: true);

        Register("Is Dead", nameof(GetPlayerIsDeadNode), NodeCategory.Game,
            "Check if player is dead",
            (id, pinId) => new GetPlayerIsDeadNode(id, pinId), hideInPalette: true);

        Register("Is Hidden", nameof(GetPlayerIsHiddenNode), NodeCategory.Game,
            "Check if player is hidden",
            (id, pinId) => new GetPlayerIsHiddenNode(id, pinId), hideInPalette: true);

        Register("Is Poisoned", nameof(GetPlayerIsPoisonedNode), NodeCategory.Game,
            "Check if player is poisoned",
            (id, pinId) => new GetPlayerIsPoisonedNode(id, pinId), hideInPalette: true);

        Register("Is Mounted", nameof(GetPlayerIsMountedNode), NodeCategory.Game,
            "Check if player is mounted",
            (id, pinId) => new GetPlayerIsMountedNode(id, pinId), hideInPalette: true);

        Register("Is Human", nameof(GetPlayerIsHumanNode), NodeCategory.Game,
            "Check if player is human",
            (id, pinId) => new GetPlayerIsHumanNode(id, pinId), hideInPalette: true);

        Register("Is Yellow Hits", nameof(GetPlayerIsYellowHitsNode), NodeCategory.Game,
            "Check if player has yellow hits bar",
            (id, pinId) => new GetPlayerIsYellowHitsNode(id, pinId), hideInPalette: true);

        Register("Is Female", nameof(GetPlayerIsFemaleNode), NodeCategory.Game,
            "Check if player is female",
            (id, pinId) => new GetPlayerIsFemaleNode(id, pinId), hideInPalette: true);

        Register("Get Name", nameof(GetPlayerNameNode), NodeCategory.Game,
            "Get player's name",
            (id, pinId) => new GetPlayerNameNode(id, pinId), hideInPalette: true);

        Register("Get Title", nameof(GetPlayerTitleNode), NodeCategory.Game,
            "Get player's title",
            (id, pinId) => new GetPlayerTitleNode(id, pinId), hideInPalette: true);

        Register("Get Notoriety Flag", nameof(GetPlayerNotorietyFlagNode), NodeCategory.Game,
            "Get player's notoriety flag",
            (id, pinId) => new GetPlayerNotorietyFlagNode(id, pinId), hideInPalette: true);

        Register("Get Race", nameof(GetPlayerRaceNode), NodeCategory.Game,
            "Get player's race",
            (id, pinId) => new GetPlayerRaceNode(id, pinId), hideInPalette: true);

        // Mobile Nodes
        Register("Mobile", nameof(MobileNode), NodeCategory.Game,
            "Mobile object reference",
            (id, pinId) => new MobileNode(id, pinId), hideInPalette: true);

        Register("Get X", nameof(GetMobileXNode), NodeCategory.Game,
            "Get mobile's X coordinate",
            (id, pinId) => new GetMobileXNode(id, pinId), hideInPalette: true);

        Register("Get Y", nameof(GetMobileYNode), NodeCategory.Game,
            "Get mobile's Y coordinate",
            (id, pinId) => new GetMobileYNode(id, pinId), hideInPalette: true);

        Register("Get Z", nameof(GetMobileZNode), NodeCategory.Game,
            "Get mobile's Z coordinate",
            (id, pinId) => new GetMobileZNode(id, pinId), hideInPalette: true);

        Register("Get Hue", nameof(GetMobileHueNode), NodeCategory.Game,
            "Get mobile's hue/color",
            (id, pinId) => new GetMobileHueNode(id, pinId), hideInPalette: true);

        Register("Get Graphic", nameof(GetMobileGraphicNode), NodeCategory.Game,
            "Get mobile's graphic ID",
            (id, pinId) => new GetMobileGraphicNode(id, pinId), hideInPalette: true);

        Register("Get Name", nameof(GetMobileNameNode), NodeCategory.Game,
            "Get mobile's name",
            (id, pinId) => new GetMobileNameNode(id, pinId), hideInPalette: true);

        Register("Get Serial", nameof(GetMobileSerialNode), NodeCategory.Game,
            "Get mobile's serial number",
            (id, pinId) => new GetMobileSerialNode(id, pinId), hideInPalette: true);

        Register("Get Distance", nameof(GetMobileDistanceNode), NodeCategory.Game,
            "Get mobile's distance",
            (id, pinId) => new GetMobileDistanceNode(id, pinId), hideInPalette: true);

        Register("Get Direction", nameof(GetMobileDirectionNode), NodeCategory.Game,
            "Get mobile's facing direction",
            (id, pinId) => new GetMobileDirectionNode(id, pinId), hideInPalette: true);

        Register("Is Destroyed", nameof(GetMobileIsDestroyedNode), NodeCategory.Game,
            "Check if mobile is destroyed",
            (id, pinId) => new GetMobileIsDestroyedNode(id, pinId), hideInPalette: true);

        Register("Is Running", nameof(GetMobileIsRunningNode), NodeCategory.Game,
            "Check if mobile is running",
            (id, pinId) => new GetMobileIsRunningNode(id, pinId), hideInPalette: true);

        Register("Is Paralyzed", nameof(GetMobileIsParalyzedNode), NodeCategory.Game,
            "Check if mobile is paralyzed",
            (id, pinId) => new GetMobileIsParalyzedNode(id, pinId), hideInPalette: true);

        Register("Is Dead", nameof(GetMobileIsDeadNode), NodeCategory.Game,
            "Check if mobile is dead",
            (id, pinId) => new GetMobileIsDeadNode(id, pinId), hideInPalette: true);

        Register("Is Hidden", nameof(GetMobileIsHiddenNode), NodeCategory.Game,
            "Check if mobile is hidden",
            (id, pinId) => new GetMobileIsHiddenNode(id, pinId), hideInPalette: true);

        Register("Is Poisoned", nameof(GetMobileIsPoisonedNode), NodeCategory.Game,
            "Check if mobile is poisoned",
            (id, pinId) => new GetMobileIsPoisonedNode(id, pinId), hideInPalette: true);

        Register("Is Mounted", nameof(GetMobileIsMountedNode), NodeCategory.Game,
            "Check if mobile is mounted",
            (id, pinId) => new GetMobileIsMountedNode(id, pinId), hideInPalette: true);

        Register("Is Human", nameof(GetMobileIsHumanNode), NodeCategory.Game,
            "Check if mobile is human",
            (id, pinId) => new GetMobileIsHumanNode(id, pinId), hideInPalette: true);

        Register("Is Yellow Hits", nameof(GetMobileIsYellowHitsNode), NodeCategory.Game,
            "Check if mobile has yellow hits bar",
            (id, pinId) => new GetMobileIsYellowHitsNode(id, pinId), hideInPalette: true);

        Register("Is Female", nameof(GetMobileIsFemaleNode), NodeCategory.Game,
            "Check if mobile is female",
            (id, pinId) => new GetMobileIsFemaleNode(id, pinId), hideInPalette: true);

        Register("Get Title", nameof(GetMobileTitleNode), NodeCategory.Game,
            "Get mobile's title",
            (id, pinId) => new GetMobileTitleNode(id, pinId), hideInPalette: true);

        Register("Get Notoriety Flag", nameof(GetMobileNotorietyFlagNode), NodeCategory.Game,
            "Get mobile's notoriety flag",
            (id, pinId) => new GetMobileNotorietyFlagNode(id, pinId), hideInPalette: true);

        Register("Get Race", nameof(GetMobileRaceNode), NodeCategory.Game,
            "Get mobile's race",
            (id, pinId) => new GetMobileRaceNode(id, pinId), hideInPalette: true);

        Register("Get Hits", nameof(GetMobileHitsNode), NodeCategory.Game,
            "Get mobile's current hits",
            (id, pinId) => new GetMobileHitsNode(id, pinId), hideInPalette: true);

        Register("Get Hits Max", nameof(GetMobileHitsMaxNode), NodeCategory.Game,
            "Get mobile's maximum hits",
            (id, pinId) => new GetMobileHitsMaxNode(id, pinId), hideInPalette: true);

        Register("Get Diff Hits", nameof(GetMobileDiffHitsNode), NodeCategory.Game,
            "Get mobile's hits difference (Hits - HitsMax)",
            (id, pinId) => new GetMobileDiffHitsNode(id, pinId), hideInPalette: true);

        Register("Get Stamina", nameof(GetMobileStaminaNode), NodeCategory.Game,
            "Get mobile's current stamina",
            (id, pinId) => new GetMobileStaminaNode(id, pinId), hideInPalette: true);

        Register("Get Stamina Max", nameof(GetMobileStaminaMaxNode), NodeCategory.Game,
            "Get mobile's maximum stamina",
            (id, pinId) => new GetMobileStaminaMaxNode(id, pinId), hideInPalette: true);

        Register("Get Mana", nameof(GetMobileManaNode), NodeCategory.Game,
            "Get mobile's current mana",
            (id, pinId) => new GetMobileManaNode(id, pinId), hideInPalette: true);

        Register("Get Mana Max", nameof(GetMobileManaMaxNode), NodeCategory.Game,
            "Get mobile's maximum mana",
            (id, pinId) => new GetMobileManaMaxNode(id, pinId), hideInPalette: true);

        // Mobile Search
        Register("Find Mobiles", nameof(FindMobilesNode), NodeCategory.Game,
            "Search for mobiles by serial, type, name, or filter criteria",
            (id, pinId) => new FindMobilesNode(id, pinId));

        // Item Search
        Register("Find Items", nameof(FindItemsNode), NodeCategory.Game,
            "Search for items by layer, serial, type, name, or filter criteria",
            (id, pinId) => new FindItemsNode(id, pinId));

        // Gump Reference
        Register("Gump", nameof(GumpNode), NodeCategory.Game,
            "Get a gump reference by serial",
            (id, pinId) => new GumpNode(id, pinId));

        // Gump Operations
        Register("Wait For Gump", nameof(WaitForGumpNode), NodeCategory.Game,
            "Wait for a gump to appear with timeout",
            (id, pinId) => new WaitForGumpNode(id, pinId), hideInPalette: true);

        Register("Is Active", nameof(IsActiveGumpNode), NodeCategory.Game,
            "Check if a gump is currently active",
            (id, pinId) => new IsActiveGumpNode(id, pinId), hideInPalette: true);

        Register("Press Button", nameof(PressButtonGumpNode), NodeCategory.Game,
            "Press a button on a gump",
            (id, pinId) => new PressButtonGumpNode(id, pinId), hideInPalette: true);

        // Journal Reference
        Register("Journal", nameof(JournalNode), NodeCategory.Game,
            "Get the journal reference",
            (id, pinId) => new JournalNode(id, pinId));

        // Journal Operations
        Register("Contains", nameof(JournalContainsNode), NodeCategory.Game,
            "Check if journal contains text and return the entry",
            (id, pinId) => new JournalContainsNode(id, pinId), hideInPalette: true);

        Register("Clear Journal", nameof(ClearJournalNode), NodeCategory.Game,
            "Clear the journal (set start time filter)",
            (id, pinId) => new ClearJournalNode(id, pinId), hideInPalette: true);

        // Journal Entry Property Nodes
        Register("Get Text", nameof(GetJournalEntryTextNode), NodeCategory.Game,
            "Get journal entry text",
            (id, pinId) => new GetJournalEntryTextNode(id, pinId), hideInPalette: true);

        Register("Get Hue", nameof(GetJournalEntryHueNode), NodeCategory.Game,
            "Get journal entry hue/color",
            (id, pinId) => new GetJournalEntryHueNode(id, pinId), hideInPalette: true);

        Register("Get Name", nameof(GetJournalEntryNameNode), NodeCategory.Game,
            "Get journal entry speaker name",
            (id, pinId) => new GetJournalEntryNameNode(id, pinId), hideInPalette: true);

        Register("Get Time", nameof(GetJournalEntryTimeNode), NodeCategory.Game,
            "Get journal entry timestamp",
            (id, pinId) => new GetJournalEntryTimeNode(id, pinId), hideInPalette: true);

        Register("Get Text Type", nameof(GetJournalEntryTextTypeNode), NodeCategory.Game,
            "Get journal entry text type",
            (id, pinId) => new GetJournalEntryTextTypeNode(id, pinId), hideInPalette: true);

        // Item Reference (hidden property nodes shown in context menu)
        Register("Item", nameof(ItemNode), NodeCategory.Game,
            "Get an item reference",
            (id, pinId) => new ItemNode(id, pinId), hideInPalette: true);

        // Item Property Nodes
        Register("Get X", nameof(GetItemXNode), NodeCategory.Game,
            "Get item X position",
            (id, pinId) => new GetItemXNode(id, pinId), hideInPalette: true);

        Register("Get Y", nameof(GetItemYNode), NodeCategory.Game,
            "Get item Y position",
            (id, pinId) => new GetItemYNode(id, pinId), hideInPalette: true);

        Register("Get Z", nameof(GetItemZNode), NodeCategory.Game,
            "Get item Z position",
            (id, pinId) => new GetItemZNode(id, pinId), hideInPalette: true);

        Register("Get Hue", nameof(GetItemHueNode), NodeCategory.Game,
            "Get item hue/color",
            (id, pinId) => new GetItemHueNode(id, pinId), hideInPalette: true);

        Register("Get Graphic", nameof(GetItemGraphicNode), NodeCategory.Game,
            "Get item graphic ID",
            (id, pinId) => new GetItemGraphicNode(id, pinId), hideInPalette: true);

        Register("Get Name", nameof(GetItemNameNode), NodeCategory.Game,
            "Get item name",
            (id, pinId) => new GetItemNameNode(id, pinId), hideInPalette: true);

        Register("Get Serial", nameof(GetItemSerialNode), NodeCategory.Game,
            "Get item serial number",
            (id, pinId) => new GetItemSerialNode(id, pinId), hideInPalette: true);

        Register("Get Distance", nameof(GetItemDistanceNode), NodeCategory.Game,
            "Get item distance from player",
            (id, pinId) => new GetItemDistanceNode(id, pinId), hideInPalette: true);

        Register("Get Direction", nameof(GetItemDirectionNode), NodeCategory.Game,
            "Get item direction",
            (id, pinId) => new GetItemDirectionNode(id, pinId), hideInPalette: true);

        Register("Get Is Destroyed", nameof(GetItemIsDestroyedNode), NodeCategory.Game,
            "Check if item is destroyed",
            (id, pinId) => new GetItemIsDestroyedNode(id, pinId), hideInPalette: true);

        Register("Get Properties", nameof(GetItemPropertiesNode), NodeCategory.Game,
            "Get item properties (OPL data)",
            (id, pinId) => new GetItemPropertiesNode(id, pinId), hideInPalette: true);

        Register("Get Amount", nameof(GetItemAmountNode), NodeCategory.Game,
            "Get item stack amount",
            (id, pinId) => new GetItemAmountNode(id, pinId), hideInPalette: true);

        Register("Get Is Damageable", nameof(GetItemIsDamageableNode), NodeCategory.Game,
            "Check if item is damageable",
            (id, pinId) => new GetItemIsDamageableNode(id, pinId), hideInPalette: true);

        Register("Get Is Coin", nameof(GetItemIsCoinNode), NodeCategory.Game,
            "Check if item is a coin",
            (id, pinId) => new GetItemIsCoinNode(id, pinId), hideInPalette: true);

        Register("Get Is Corpse", nameof(GetItemIsCorpseNode), NodeCategory.Game,
            "Check if item is a corpse",
            (id, pinId) => new GetItemIsCorpseNode(id, pinId), hideInPalette: true);

        Register("Get Is Empty", nameof(GetItemIsEmptyNode), NodeCategory.Game,
            "Check if item/container is empty",
            (id, pinId) => new GetItemIsEmptyNode(id, pinId), hideInPalette: true);

        Register("Get Is Locked", nameof(GetItemIsLockedNode), NodeCategory.Game,
            "Check if item is locked/immovable",
            (id, pinId) => new GetItemIsLockedNode(id, pinId), hideInPalette: true);

        Register("Get Is Hidden", nameof(GetItemIsHiddenNode), NodeCategory.Game,
            "Check if item is hidden",
            (id, pinId) => new GetItemIsHiddenNode(id, pinId), hideInPalette: true);

        Register("Get Is Multi", nameof(GetItemIsMultiNode), NodeCategory.Game,
            "Check if item is a multi/house",
            (id, pinId) => new GetItemIsMultiNode(id, pinId), hideInPalette: true);

        Register("Get Is Lootable", nameof(GetItemIsLootableNode), NodeCategory.Game,
            "Check if item is lootable",
            (id, pinId) => new GetItemIsLootableNode(id, pinId), hideInPalette: true);

        Register("Get On Ground", nameof(GetItemOnGroundNode), NodeCategory.Game,
            "Check if item is on ground",
            (id, pinId) => new GetItemOnGroundNode(id, pinId), hideInPalette: true);

        Register("Get Displayed Graphic", nameof(GetItemDisplayedGraphicNode), NodeCategory.Game,
            "Get item displayed graphic ID",
            (id, pinId) => new GetItemDisplayedGraphicNode(id, pinId), hideInPalette: true);

        Register("Get Multi Graphic", nameof(GetItemMultiGraphicNode), NodeCategory.Game,
            "Get item multi graphic ID",
            (id, pinId) => new GetItemMultiGraphicNode(id, pinId), hideInPalette: true);

        Register("Get Layer", nameof(GetItemLayerNode), NodeCategory.Game,
            "Get item equipment layer",
            (id, pinId) => new GetItemLayerNode(id, pinId), hideInPalette: true);

        Register("Get Light ID", nameof(GetItemLightIDNode), NodeCategory.Game,
            "Get item light source ID",
            (id, pinId) => new GetItemLightIDNode(id, pinId), hideInPalette: true);

        Register("Get Root Container", nameof(GetItemRootContainerNode), NodeCategory.Game,
            "Get item root container serial",
            (id, pinId) => new GetItemRootContainerNode(id, pinId), hideInPalette: true);

        Register("Get Container", nameof(GetItemContainerNode), NodeCategory.Game,
            "Get item container serial",
            (id, pinId) => new GetItemContainerNode(id, pinId), hideInPalette: true);

        // Game Nodes - Item Actions
        Register("Move Items", nameof(MoveItemsNode), NodeCategory.Game,
            "Move items from one container to another with iteration",
            (id, pinId) => new MoveItemsNode(id, pinId), hideInPalette: false, isExperimental: true);

        // Game Nodes - Communication
        Register("Say", nameof(SayNode), NodeCategory.Game,
            "Send a message in chat",
            (id, pinId) => new SayNode(id, pinId));

        Register("Message Overhead", nameof(MessageOverheadNode), NodeCategory.Game,
            "Display a message above an entity or self",
            (id, pinId) => new MessageOverheadNode(id, pinId));

        // Game Nodes - Combat
        Register("Attack", nameof(AttackNode), NodeCategory.Game,
            "Attack a target by serial",
            (id, pinId) => new AttackNode(id, pinId));

        Register("Toggle War Mode", nameof(ToggleWarModeNode), NodeCategory.Game,
            "Toggle war mode on/off",
            (id, pinId) => new ToggleWarModeNode(id, pinId));

        Register("Pop Pouch", nameof(PopPouchNode), NodeCategory.Game,
            "Use a pouch item",
            (id, pinId) => new PopPouchNode(id, pinId));

        Register("Bandage Self", nameof(BandageSelfNode), NodeCategory.Game,
            "Apply bandage to self",
            (id, pinId) => new BandageSelfNode(id, pinId));

        Register("Cast Spell", nameof(CastSpellNode), NodeCategory.Game,
            "Cast a spell or bard song with optional target waiting",
            (id, pinId) => new CastSpellNode(id, pinId));

        Register("Clear Hands", nameof(ClearHandsNode), NodeCategory.Game,
            "Unequip items from hands",
            (id, pinId) => new ClearHandsNode(id, pinId));

        // Game Nodes - Items
        Register("Use", nameof(UseItemNode), NodeCategory.Game,
            "Use an item by serial or type",
            (id, pinId) => new UseItemNode(id, pinId));

        Register("Click Object", nameof(ClickObjectNode), NodeCategory.Game,
            "Single-click an object by serial",
            (id, pinId) => new ClickObjectNode(id, pinId));

        Register("Equip", nameof(EquipNode), NodeCategory.Game,
            "Equip an item by serial",
            (id, pinId) => new EquipNode(id, pinId));

        Register("Pickup", nameof(PickupNode), NodeCategory.Game,
            "Pick up an item by serial",
            (id, pinId) => new PickupNode(id, pinId));

        Register("Drop", nameof(DropNode), NodeCategory.Game,
            "Drop an item to backpack, container, or ground",
            (id, pinId) => new DropNode(id, pinId));

        // Game Nodes - Skills
        Register("Get Skill", nameof(GetSkillNode), NodeCategory.Game,
            "Get skill value (Base or Real)",
            (id, pinId) => new GetSkillNode(id, pinId));

        Register("Use Skill", nameof(UseSkillNode), NodeCategory.Game,
            "Use an actively usable skill",
            (id, pinId) => new UseSkillNode(id, pinId));

        // Targeting Nodes
        Register("Last Target", nameof(LastTargetNode), NodeCategory.Game,
            "Get the last target object",
            (id, pinId) => new LastTargetNode(id, pinId));

        Register("Set Last Target", nameof(SetLastTargetNode), NodeCategory.Game,
            "Set a serial as the last target",
            (id, pinId) => new SetLastTargetNode(id, pinId));

        // Target property nodes - hidden from palette, only shown in Target object context menu
        Register("Get Serial", nameof(GetTargetSerialNode), NodeCategory.Game,
            "Get target's serial number",
            (id, pinId) => new GetTargetSerialNode(id, pinId), hideInPalette: true);

        Register("Get Graphic", nameof(GetTargetGraphicNode), NodeCategory.Game,
            "Get target's graphic ID",
            (id, pinId) => new GetTargetGraphicNode(id, pinId), hideInPalette: true);

        Register("Get X", nameof(GetTargetXNode), NodeCategory.Game,
            "Get target's X coordinate",
            (id, pinId) => new GetTargetXNode(id, pinId), hideInPalette: true);

        Register("Get Y", nameof(GetTargetYNode), NodeCategory.Game,
            "Get target's Y coordinate",
            (id, pinId) => new GetTargetYNode(id, pinId), hideInPalette: true);

        Register("Get Z", nameof(GetTargetZNode), NodeCategory.Game,
            "Get target's Z coordinate",
            (id, pinId) => new GetTargetZNode(id, pinId), hideInPalette: true);

        Register("Is Entity", nameof(GetTargetIsEntityNode), NodeCategory.Game,
            "Check if target is an entity",
            (id, pinId) => new GetTargetIsEntityNode(id, pinId), hideInPalette: true);

        Register("Is Static", nameof(GetTargetIsStaticNode), NodeCategory.Game,
            "Check if target is a static",
            (id, pinId) => new GetTargetIsStaticNode(id, pinId), hideInPalette: true);

        Register("Is Land", nameof(GetTargetIsLandNode), NodeCategory.Game,
            "Check if target is land",
            (id, pinId) => new GetTargetIsLandNode(id, pinId), hideInPalette: true);

        Register("Selected Target", nameof(SelectedTargetNode), NodeCategory.Game,
            "Get the selected target serial",
            (id, pinId) => new SelectedTargetNode(id, pinId));

        Register("Set Selected Target", nameof(SetSelectedTargetNode), NodeCategory.Game,
            "Set a serial as the selected target",
            (id, pinId) => new SetSelectedTargetNode(id, pinId));

        Register("Execute Target", nameof(ExecuteTargetNode), NodeCategory.Game,
            "Execute targeting on a serial",
            (id, pinId) => new ExecuteTargetNode(id, pinId));

        Register("Execute Target Entity", nameof(ExecuteTargetEntityNode), NodeCategory.Game,
            "Execute targeting on Last/Self/Selected",
            (id, pinId) => new ExecuteTargetEntityNode(id, pinId));

        Register("Wait For Target", nameof(WaitForTargetNode), NodeCategory.Game,
            "Wait for targeting cursor with timeout",
            (id, pinId) => new WaitForTargetNode(id, pinId));

        Register("Target Cursor", nameof(TargetCursorNode), NodeCategory.Game,
            "Bring up targeting cursor and capture target",
            (id, pinId) => new TargetCursorNode(id, pinId));

        // List Nodes - Operations on list variables
        Register("Add", nameof(ListAddNode), NodeCategory.List,
            "Add element to end of list",
            (id, pinId) => new ListAddNode(id, pinId));

        Register("Insert", nameof(ListInsertNode), NodeCategory.List,
            "Insert element at specific index",
            (id, pinId) => new ListInsertNode(id, pinId));

        Register("Remove", nameof(ListRemoveNode), NodeCategory.List,
            "Remove element from list by value",
            (id, pinId) => new ListRemoveNode(id, pinId));

        Register("Remove At", nameof(ListRemoveAtNode), NodeCategory.List,
            "Remove element at specific index",
            (id, pinId) => new ListRemoveAtNode(id, pinId));

        Register("Get", nameof(ListGetNode), NodeCategory.List,
            "Get element at index",
            (id, pinId) => new ListGetNode(id, pinId));

        Register("Set", nameof(ListSetNode), NodeCategory.List,
            "Set element at index",
            (id, pinId) => new ListSetNode(id, pinId));

        Register("Count", nameof(ListCountNode), NodeCategory.List,
            "Get number of elements in list",
            (id, pinId) => new ListCountNode(id, pinId));

        Register("Clear", nameof(ListClearNode), NodeCategory.List,
            "Remove all elements from list",
            (id, pinId) => new ListClearNode(id, pinId));

        Register("Contains", nameof(ListContainsNode), NodeCategory.List,
            "Check if list contains element",
            (id, pinId) => new ListContainsNode(id, pinId));

        Register("Index Of", nameof(ListIndexOfNode), NodeCategory.List,
            "Find index of element in list",
            (id, pinId) => new ListIndexOfNode(id, pinId));
    }

    public static void Register(string name, string typeName, NodeCategory category, string description, Func<string, string, VScriptNode> factory, bool hideInPalette = false, bool isExperimental = false)
    {
        _nodeDefinitions.Add(new NodeDefinition(name, typeName, category, description, factory, hideInPalette, isExperimental));
    }

    public static VScriptNode Create(string typeName, string nodeId, string pinIdCounter)
    {
        var definition = _nodeDefinitions.FirstOrDefault(d => d.TypeName == typeName);
        return definition?.Factory(nodeId, pinIdCounter);
    }

    /// <summary>
    /// Creates a node and synchronizes it with the graph's variables.
    /// This ensures StartNode gets parameter pins and ReturnNode gets output pins
    /// based on currently defined script variables.
    /// </summary>
    public static VScriptNode CreateWithGraph(string typeName, string nodeId, string pinIdCounter, NodeGraph graph)
    {
        var node = Create(typeName, nodeId, pinIdCounter);
        if (node != null && graph != null)
        {
            SyncNodeWithVariables(node, graph.Variables);
        }
        return node;
    }

    /// <summary>
    /// Synchronizes a node's pins with the script variables.
    /// Call this after creating StartNode or ReturnNode to ensure they have
    /// the correct parameter/output pins based on current variables.
    /// </summary>
    public static void SyncNodeWithVariables(VScriptNode node, List<ScriptVariable> variables)
    {
        if (node is StartNode startNode)
        {
            startNode.UpdateParameterPins(variables);
        }
        else if (node is ReturnNode returnNode)
        {
            returnNode.UpdateOutputPins(variables);
        }
    }

    public static List<NodeDefinition> GetAllDefinitions()
    {
        return _nodeDefinitions;
    }

    public static NodeDefinition GetDefinition(string typeName)
    {
        return _nodeDefinitions.FirstOrDefault(d => d.TypeName == typeName);
    }

    public static List<NodeDefinition> GetDefinitionsByCategory(NodeCategory category)
    {
        return _nodeDefinitions.Where(d => d.Category == category).ToList();
    }

    public static Dictionary<NodeCategory, List<NodeDefinition>> GetCategorizedDefinitions()
    {
        return _nodeDefinitions
            .GroupBy(d => d.Category)
            .ToDictionary(g => g.Key, g => g.ToList());
    }
}
