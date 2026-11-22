OPRAVA TEZENI TERENU / BEDROCK / HLINY
Unity 2022.3.x - navazuje na World Generation Update 2.0 a Ore_Terrain_Same_Progress_Bar

SOUBORY:
- TerrainMiner.cs: vypnuto pridavani terenu pravym tlacitkem; zadne dropy z bedrocku; hlina se da tezit i prazdnou rukou; drop Dirt ID 45.
- SmoothTerrain.cs: verejne funkce IsBedrockAt / IsDirtAt pro rozhodovani o dropu.
- InventorySystem.cs: nazev Dirt pro ID 45 (ikonu a prefab musis nastavit v Inspectoru).

INSTALACE:
1. Udelej zalohu projektu. Ukonci Play Mode.
2. Nahraď presne tyto tri soubory v Assets/Scripts (nesmis mit duplicitni tridy v jinych slozkach).
3. Player > InventorySystem > Custom Items: pridej novy prvek itemId=45, displayName=Dirt, category=Material, maxStack=69. Prirad icon a droppedPrefab (muzes docasne pouzit kamen s hnedym materialem).
4. Player > TerrainMiner: Dirt Item Id=45, Dirt Drop Amount=1, Allow Hand Dirt Mining=true.
5. Otestuj: pravym tlacitkem se teren netvori; na bedrocku nevznikaji dropy; hlinu lze tezit prazdnou rukou nebo krumpacem, hlina dava Dirt, hlubsi kamen Stone.

POZOR:
- Skripty vychazeji z dostupnych ZIPu, nikoli ze souboru aktualne ulozenych ve tvem Unity projektu. Pokud jsi je od te doby menil, zmeny se mohou prepsat.
- V Inspectoru na screenshotu je Missing (Mono Script). Ten je potreba samostatne vyresit: otevri Console, oprav compile chyby nebo odeber chybejici komponentu po overeni, co byla zac.
- Pro jednoduchost se druh dropu urcuje podle hloubky v proceduralnim terenu; v blizkosti hranice vrstev muze byt orientacni.
- Test v Unity zde neni mozny.
