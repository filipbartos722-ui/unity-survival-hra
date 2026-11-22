WORLD GEN 3.0 - Unity 2022.3 (Built-in Render Pipeline)

1) ZALOZ stare SmoothTerrain.cs a OreNode.cs.
2) Nahraď oba soubory a vloz GrassWindCutout.shader do Assets.
3) V Unity v SmoothTerrain Inspectoru nastav 10 textur Grass Textures (PNG v GrassTextures).
4) Nastav prefaby Coal/Iron/Gold/Ruby Ore (musi obsahovat OreNode a collider).
5) Odeber nebo deaktivuj GrassSpawner, OreSpawner a WindSystem, aby se generovani nezdvojovalo.
6) Nastav grassPatchesPerChunk, grassBladesPerPatch, emptyGrassChunkChance a coalPerChunk/ironPerChunk/goldPerChunk/rubyPerChunk.

DULEZITE: Tato prvni verze umistuje rudy na POVRCH, nikoliv jeste na steny jeskyni.
Trava je vykreslena jako krizove 2D plochy; aktualni verze ma pro kazdy trs GameObject (pro vyssi vykon v dalsi verzi spojit do mesh batches).
Wind shader je napsany pro BUILT-IN render pipeline; pokud pouzivas URP, shader bude treba prepsat.
Vytazene rudy zustavaji oznacene pouze po dobu behu hry, nikoli po restartu (bez save systemu).
Pozor: 10 textur jsou originalni proceduralne kreslene PNG s pruhlednym pozadim.
