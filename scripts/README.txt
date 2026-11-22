CROSSHAIR + CRAFTING TABLE INTERACTION FIX

1. Back up your project.
2. Replace Assets/Scripts/TerrainMiner.cs and Assets/Scripts/CraftingTable.cs with the two included scripts.
3. Keep InventorySystem.cs, TreeResource.cs, DroppedItem.cs and SmoothTerrain.cs unchanged.
4. Player: TerrainMiner component -> Cam = Main Camera; Inventory = Player's InventorySystem.
5. Player: TerrainMiner -> Range = 6, Mask = Everything (or a mask including CraftingTable layer).
6. CraftingTable prefab: add CraftingTable component and Box Collider. Disable Is Trigger on collider.
7. Make sure no other object blocks the ray between camera and table.
8. Press Play. Aim at table: crosshair turns green. Left-click opens 4x4 crafting.
9. Press Escape to close the crafting menu.

The crosshair is drawn automatically using OnGUI; no Canvas is needed.
It is hidden while the inventory or crafting UI is open.
This fix uses the existing InventorySystem.OpenCrafting(CraftingTable) API from Unity_Crafting_Trees_Inventory.zip.
