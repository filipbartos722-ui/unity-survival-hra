WORLD GENERATION UPDATE 2.0 — PRACOVNI INTEGRACNI VERZE
Unity 2022.3.x — NEOTESTOVANO V UNITY

Tento balicek obsahuje realne upravene puvodni soubory SmoothTerrain.cs, InventorySystem.cs, CraftingTable.cs
z predchozich ZIPu v konverzaci a nove komponenty. Udelejte ZALOHU projektu.

CO JE IMPLEMENTOVANO:
- SmoothTerrain: volitelne volani OreSpawner a GrassSpawner pri nacteni a odlozeni sloupce.
- OreNode: samostatne 3D rudy, pocet hitu, pozadovana uroven krumpace, drop.
- OreMiningTool: podrzeni leveho tlacitka, Stone Pickaxe ID 1, Iron Pickaxe ID 39.
- OreSpawner: deterministicky spawn odkrytych rud a zapamatovani vytezenych rud V RAM.
- GrassSpawner: trsy travy na povrchu, vypinatelne v Inspectoru.
- WindSystem: globalni shader parametry pro vitr (vyzaduje shader s podporou _SurvivalWind).
- WaterSystem: OMEZENY PROTOTYP tekouci vody v bunkach, lze vypnout.
- BucketTool: nabirani a vylevani vody s ID 40 a 41.
- FurnaceStation: Coal + Iron/Gold Ore => ingoty, postupne taveni i pri zavrenem okne.
- Station16x16: posouvatelna 16x16 mrizka, Anvil umi Iron Pickaxe, Stone Carving zatim bez receptu.
- CraftingTable: recepty Bucket, Furnace, Stone Carving, Anvil.
- InventorySystem: propojeni FurnaceStation a Station16x16 po umisteni.

DULEZITA OMEZENI — NENI TO HOTOVA PLNE INTEGROVANA HRA:
- Rudy a trava se spawnuji pouze tam, kde existuje vhodny collider terenu; spawn do sten jeskyne neni implementovan.
- Ukladani vytezenych rud funguje pouze do vypnuti hry; chybi trvale save/load.
- Voda je prototyp: bunkove kostky, ne plynula hladina; bez generovani jezer/reky.
- Voda neni propojena s vykopavanim terenu a nema realistickou fyziku.
- Voda pouziva GameObject na kazdou bunku, proto je limitovana; pro velke svety nutny chunk mesh.
- GrassSpawner pouziva jednotlive prefaby, doporuceno male mnozstvi a pozdeji GPU instancing.
- Vitr nezacne sam hybat libovolnym 3D modelem: shader musi podporovat globalni parametry.
- Neni hotove plavani, animace krumpace, 3D modely ani zvuky.
- 16x16 crafting UI je funkcni prototyp, neni integrovane s blokovanim ostatnich menu.
- Zmeny existujicich skriptu nebyly kompilovany v Unity.

NAVRZENE ITEM ID — PRIDEJTE DO InventorySystem.customItems:
32 Coal (Material), 33 Iron Ore (Material), 34 Gold Ore (Material), 35 Ruby (Material)
36 Iron Ingot (Material), 37 Gold Ingot (Material), 39 Iron Pickaxe (Tool, stack 1)
40 Empty Bucket (Tool, stack 1), 41 Water Bucket (Tool, stack 1)
42 Furnace (Placeable, stack 1), 43 Stone Carving Station (Placeable, stack 1)
44 Anvil (Placeable, stack 1)

PREFABY A INSPECTOR:
1. Priradte nove skripty do Assets/Scripts, stare tri soubory nahradte po zaloze.
2. Na objekt SmoothTerrain pridejte OreSpawner a GrassSpawner, oba propojte pres 'terrain'.
   Do SmoothTerrain.oreSpawner a .grassSpawner priradte odpovidajici komponenty.
3. Ore prefab: 3D model, Collider, OreNode. U Iron: hitsRequired=2, minimumPickaxeTier=1, dropItemId=33.
   U Ruby: hitsRequired=4, minimumPickaxeTier=2, dropItemId=35.
   Coal navrh: 1 hit, tier 1, drop 32. Gold: 3 hity, tier 2, drop 34.
4. Do OreSpawner.ores vlozte ctyri OreEntry a prefaby. Pozor na Collider Distance v terenu:
   generovani objektu zavisi na aktivnich MeshColliderech a muze preskakovat spawn.
5. GrassSpawner.grassPrefab = model trsu travy (bez collideru), tuftsPerColumn napr. 4.
6. Na Player pridejte OreMiningTool a BucketTool; priradte inventory a playerCamera.
7. WaterSystem dejte do sceny, nastavte material a maxCells (napr. 128 pro prvni test).
   Na SmoothTerrain vypnete 'generateWater', aby se voda neprekryvala s puvodni plochou.
   WaterSystem.solidMask musi zahrnovat teren a vyloucit hrace i vodu.
   Pro test pridani vody lze volat WaterSystem.AddWater(new Vector3Int(x,y,z)).
8. Vytvorte Furnace prefab s Collider + FurnaceStation, Anvil a Stone Carving prefaby
   s Collider + Station16x16 (zvolte typ). V customItems nastavte jejich placedPrefab.
9. Station16x16 potrebuje vyplnene reference 'user' a 'viewCamera' — pri umisteni
   pomoci aktualizovaneho InventorySystem se doplni automaticky.
10. CraftingTable pouziva 4x4 vzory:
    Bucket: I..I / I..I / .II.
    Furnace: SSSS / S..S / S..S / SSSS
    Stone Carving: IIII / SSSS / SSSS / SSSS
    Anvil: IIII / IIII / .II. / IIII
    Iron Pickaxe na Anvilu (NAVRZENY recept): III / .W. / .W.

POZOR: Stare systemy mohou pouzivat Mouse0, Mouse1 nebo F soucasne.
Pro produkcni verzi je treba centralni interaction controller a jednotne UI blokovani.
