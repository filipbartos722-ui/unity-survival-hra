SURVIVAL EXPANSION - Unity 2022.3

REPLACE (back up your originals first):
  InventorySystem.cs
  CraftingTable.cs
NEW SCRIPTS:
  MobAI.cs
  MobSpawner.cs
  CampfireCooking.cs
  BossEncounter.cs
OPTIONAL (only if you do not already have it):
  SurvivalVitals.cs (the existing smooth-bar version)

ITEM IDs:
24 Raw Beef (+10 Hunger, right mouse)
25 Steak (+40 Hunger, right mouse)
26 Spear (left mouse, 25 damage, range 3.5)
27 Backpack (8 extra slots)
28 Bundle (3 extra slots)
29 Bear Claw (optional boss loot)
30 Leather (for bag crafting)
Existing IDs 0-23 are preserved.

WORK BENCH CRAFTING (4x4 grid, patterns can shift):
S=Stone (4), W=Wood (3), L=Leather (30), .=Empty
Spear (ID 26):
. S . .
S W S .
. W . .
. W . .

Bundle (ID 28):
L W L .
. L . .

Backpack (ID 27):
L L L .
L W L .
L L L .

Existing recipes (pickaxe, axe, workbench, walls and floors) remain.

COW:
Create cow prefab with Collider + MobAI.
Mob Type = Cow; Max Health = 65; Drop Item ID = 24; Min/Max Drop = 1/3;
Secondary Drop Item ID = 30; Secondary Drop Amount = 1.

BEAR:
Create bear prefab with Collider + MobAI.
Mob Type = Bear; Max Health = 150; Damage = 20; Drop Item ID = 30;
Secondary Drop Item ID = 0 (to disable).

BOSS:
Create boss prefab with Collider + MobAI + BossEncounter.
Mob Type = Boss; Max Health = 350; Damage = 30; Drop Item ID = 29.
Secondary Drop Item ID = 0. BossEncounter shows health bar nearby.

SPAWN:
Add MobSpawner to an EMPTY scene object near the desired biome, assign Mob Prefab,
Ground Mask and Player. Use one spawner for cows, another for bears.
Spawners are position-based; they do NOT automatically read SmoothTerrain biome data.

STEAK:
Create a campfire object with Collider + CampfireCooking.
Hold raw beef anywhere in inventory, look at campfire from <= 3m and press F.
After 5 seconds cooked steak drops near the campfire.

BACKPACK / BUNDLE:
Open Inventory (I). Put Backpack or Bundle in the BAG slot using the cursor.
Backpack = 8 extra slots, Bundle = 3 extra slots.

HELD ITEMS:
InventorySystem > Held Model Auto Fit > Auto Fit Held Models ON.
Max Held Model Size = 0.43 (reduce to 0.3 if still too large).
Adjust Held Local Position for bottom-right placement.
Assign icons and held prefabs for IDs 24-28 in the Survival Items section.

ANIMATIONS:
Optional Animator on MobAI with Moving bool, Attack and Hurt triggers.
When no animator is provided, mobs still move and attack without animations.

IMPORTANT:
- Existing save files or Inspector custom items using IDs 24-30 must be remapped.
- Models, audio and animation clips are NOT included. Assign your own prefabs.
- Requires the project's existing scripts: DroppedItem, TerrainMiner, TreeResource,
  SmoothTerrain, JukeboxPlayer and SurvivalVitals.
- No Unity Editor compilation/runtime test was possible here.
