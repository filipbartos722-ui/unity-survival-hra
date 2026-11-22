WORLD GENERATION 3.4 – EXPERIMENTALNI DILCI UPDATE

Obsah:
- SmoothTerrain.cs (vychazi z WorldGen 3.3)
- CaveDarkness.cs (novy)

INSTALACE
1. ZALOHUJ puvodni SmoothTerrain.cs a scenu.
2. Nahrad pouze SmoothTerrain.cs a pridej CaveDarkness.cs.
3. Inventar a lucerna zustavaji z Lantern_3_4_Placement_Update.zip – NEPREPISUJ je starsimi verzemi.
4. Pridej CaveDarkness na kameru hrace a nastav referenci na SmoothTerrain.
5. V SmoothTerrain nastav initialColumnsToPrepare=32, initialColumnsPerFrame=1, backgroundColumnInterval=0.08.
6. Pro rudy nastav caveFloorOresOnly=true, oreFloorInset=0.12, oreGenerationDistance=2.
7. Zkontroluj, ze teren ma MeshCollider v okoli hrace; umisteni rud zavisi na fyzikalnim raycastu.

OMEZENI / DULEZITE
- Pocet 32 znamena 32 CHUNKOVYCH SLOUPCU, ne 32 trojrozmernych chunku.
- Pocatecni nahravani probiha po snimcich, NENI zde loading screen a hrac neni automaticky zamknut.
- Generovani zustava na hlavnimu vlakne. Jeden narocny chunk muze zpusobit zasek.
- Rudam se hleda realna podlaha jeskyne pomoci raycastu proti terrain MeshCollideru.
  Pokud neni vhodna podlaha/collider, ruda se preskoci.
- Rudy na podlaze jeskyne jsou skryte do doby, nez hrac kope v jejich blizkosti.
  Neni to jeste plnohodnotny system automatickeho odhaleni pri vstupu do jeskyne.
- Tma je resena globalnim fogem, nikoliv lokalnim volumetrickym svetlem.
  Z jeskyne muze ovlivnit pohled ven. Lucerna s Point Light se projevi podle render pipeline.
- Neprobehla kompilace ani test v Unity. Pokud se objevi chyby, posli cely Console stacktrace.
- Graficke assety, shadery, barvy a materialy nebyly meneny.
