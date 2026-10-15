using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

// Builds five design-only map scenes (terrain shape, ground textures, liquid planes, markers)
// under Assets/Design, then renders overview shots into output/design/maps.
// Never touches SampleScene: each map scene is created additively, saved and closed.
// Collision, hazards, spawns and stage flow are left to the game side; see the Marker_/Zone_ objects.
public static class MapBuilder
{
    const string EnvRoot = "Assets/Design/Environment";
    const string SceneDir = "Assets/Design/Scenes";
    const string CaptureDir = "output/design/maps";
    const float Size = 96f;      // terrain is 96m; the play area is the middle 64m (walls at ±33 like SampleScene)
    const float MaxHeight = 24f;
    const int HeightRes = 257;
    const int SplatRes = 256;
    const float LiquidY = 2.4f;  // one flat plane per map; it shows only where the ground dips below it
    const int CaptureLayer = 30;
    const float BridgeGround = 3.15f; // meadow: height of the flattened pads the bridges stand on

    delegate float HeightFn(float x, float z);
    delegate void SplatFn(float x, float z, float h, float slope, float[] w);

    class MapDef
    {
        public string id, name;
        public HeightFn height;
        public SplatFn splat;
        public Func<float, float, Color>[] layers;
        public float[] tile;
        public Color liquid;
        public bool liquidGlows;
        public Color sun = Color.white;
        public Vector2 start, boss;
        public Color fog = Hex("#cfe6f0"), skyTint = Hex("#7fb8e6");
        public Action<Transform, string, Terrain> decorate; // root, asset dir, terrain
    }

    [MenuItem("Tools/Design/Build Maps")]
    public static void Build()
    {
        if (!LeaveDesignScenes()) return;
        Directory.CreateDirectory(SceneDir);
        Directory.CreateDirectory(CaptureDir);
        Directory.CreateDirectory(EnvRoot);
        AssetDatabase.Refresh();
        var terrainMat = LoadOrCreateMaterial(EnvRoot + "/M_Terrain_Design.mat", "Universal Render Pipeline/Terrain/Lit", Color.white, false);

        var shots = new List<Texture2D>();
        foreach (var def in Defs())
            shots.Add(BuildMap(def, terrainMat));

        // 3x2 contact sheet, map 1 top-left
        var sheet = new Texture2D(1920, 720, TextureFormat.RGB24, false);
        for (int i = 0; i < shots.Count; i++)
            sheet.SetPixels((i % 3) * 640, (1 - i / 3) * 360, 640, 360, shots[i].GetPixels());
        sheet.Apply();
        File.WriteAllBytes(CaptureDir + "/maps_sheet.png", sheet.EncodeToPNG());
        Object.DestroyImmediate(sheet);
        foreach (var s in shots) Object.DestroyImmediate(s);
        AssetDatabase.SaveAssets();
        Debug.Log("[MapBuilder] built " + shots.Count + " maps");
    }

    // The builders save over scenes under Assets/Design/Scenes, so none of them may be open
    // (and never in play mode). Falls back to the game scene, asking about unsaved edits first.
    public static bool LeaveDesignScenes()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("[Design] stop play mode before rebuilding maps");
            return false;
        }
        for (int i = 0; i < SceneManager.sceneCount; i++)
            if (SceneManager.GetSceneAt(i).path.StartsWith(SceneDir))
            {
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return false;
                EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity", OpenSceneMode.Single);
                break;
            }
        return true;
    }

    // ---------- map definitions ----------

    static IEnumerable<MapDef> Defs()
    {
        // 1. meadow: meandering river crossed by two land bridges, soft hills
        // Set dressing comes from Art/Blender/map_kits.py (exported into Map01_Meadow/Models).
        // The river is ~2.4m deep so the player floats in it (see WaterFloat) instead of wading.
        Func<float, float> riverX = z => 4f * Mathf.Sin(z * 0.08f);
        HeightFn meadow = (x, z) =>
        {
            float h = 3.3f + Bump(x, z, -16, 14, 9, 1.8f) + Bump(x, z, 20, -20, 8, 1.6f) + Bump(x, z, 18, 18, 6, 1.2f) + (Fbm(x * 0.05f, z * 0.05f) - 0.5f) * 1.2f;
            float river = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(2.5f, 8f, Mathf.Abs(x - riverX(z))) /* banks ≤ ~33°, the player climbs 45° */);
            float bridge = Mathf.Max(Band(z, -12f, 2.2f), Band(z, 14f, 2.2f));
            h -= 2.4f * river * (1f - bridge);
            // flat pads under the two bridges (deck plus end ramps, ~13.4 x 3.6 m) so they sit on the ground
            foreach (float bz in new[] { -12f, 14f })
            {
                float along = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(7.2f, 8.7f, Mathf.Abs(x - riverX(bz))));
                float across = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(2.8f, 4f, Mathf.Abs(z - bz)));
                h = Mathf.Lerp(h, BridgeGround, along * across);
            }
            return h + Rim(x, z, 7f);
        };
        yield return new MapDef
        {
            id = "Map01", name = "Meadow",
            height = meadow,
            // dirt only on the river banks; the outer rim stays grassy so it reads as hills, not a wall
            splat = (x, z, h, s, w) => { w[0] = 1; w[1] = Mathf.InverseLerp(3.0f, 2.5f, h) * (1f - Mathf.InverseLerp(30f, 34f, Mathf.Max(Mathf.Abs(x), Mathf.Abs(z)))); },
            layers = new Func<float, float, Color>[] { Grass, Dirt },
            tile = new[] { 6f, 8f },
            liquid = Hex("#36a8d6"),
            start = new Vector2(-18, -24), boss = new Vector2(18, 24),
            decorate = (root, dir, terrain) =>
            {
                var rng = new System.Random(11);
                Func<float, float, float> range = (a, b) => a + (float)rng.NextDouble() * (b - a);

                Spawn(Model(dir, "Meadow_Backdrop"), root, null, 0f, 0f, 0f, 1f);
                var bridge = Model(dir, "Meadow_Bridge");
                Spawn(bridge, root, terrain, riverX(-12f), -12f, 0f, 1f); // sits on the flat pad (BridgeGround)
                Spawn(bridge, root, terrain, riverX(14f), 14f, 0f, 1f);

                var stones = new[] { Model(dir, "Meadow_Stone0"), Model(dir, "Meadow_Stone1"), Model(dir, "Meadow_Stone2") };
                for (float z = -44f; z < 44f; z += range(2.5f, 4.5f))
                {
                    if (Mathf.Abs(z + 12f) < 4f || Mathf.Abs(z - 14f) < 4f) continue;
                    float side = rng.NextDouble() < 0.5 ? -1f : 1f;
                    SpawnSpaced(stones[rng.Next(3)], root, terrain, riverX(z) + side * range(6.5f, 8f), z, range(0, 360), range(0.5f, 1.3f), 0.1f);
                }
                for (int i = 0; i < 18; i++)
                    Spawn(stones[rng.Next(3)], root, terrain, range(-31, 31), range(-31, 31), range(0, 360), range(0.6f, 1.6f), 0.15f);

                // flowers grow in patches: pick a patch centre, then drop a few clumps of one colour around it
                var flowers = new[] { Model(dir, "Meadow_Flowers0"), Model(dir, "Meadow_Flowers1"), Model(dir, "Meadow_Flowers2") };
                for (int patch = 0, tries = 0; patch < 26 && tries < 500; tries++)
                {
                    float px = range(-36, 36), pz = range(-36, 36);
                    if (Mathf.Abs(px - riverX(pz)) < 8f) continue;
                    var kind = flowers[rng.Next(3)];
                    for (int c = 0; c < 6; c++)
                    {
                        float x = px + range(-2.5f, 2.5f), z = pz + range(-2.5f, 2.5f);
                        if (Mathf.Abs(x - riverX(z)) < 7f) continue;
                        Spawn(kind, root, terrain, x, z, range(0, 360), range(1.6f, 2.4f));
                    }
                    patch++;
                }

                ScatterGrass(root, terrain, dir, rng, (x, z) =>
                {
                    if (meadow(x, z) < 3.05f) return 0f; // river bed and banks stay bare
                    if (Mathf.Abs(x - riverX(z)) < 7.5f && (Mathf.Abs(z + 12f) < 3f || Mathf.Abs(z - 14f) < 3f)) return 0f; // bridge decks
                    float n = Fbm(x * 0.03f, z * 0.03f);
                    return n > 0.55f ? 1f : n > 0.4f ? 0.6f : 0.3f;
                });
            },
        };

        // 2. canyon: one winding floor path between cliffs, with two side pockets
        var canyon = SampleBezier(new[] { P(30, 180), P(30, 120), P(150, 130), P(150, 90), P(150, 90), P(150, 50), P(40, 60), P(40, 8) });
        yield return new MapDef
        {
            id = "Map02", name = "Canyon",
            height = (x, z) =>
            {
                float d = Mathf.Min(DistToPath(canyon, x, z), Mathf.Min(Dist(x, z, P(125, 155)) - 3f, Dist(x, z, P(150, 30)) - 3f));
                float wall = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(5.5f, 8.5f, d));
                return 2f + wall * (7f + Fbm(x * 0.12f, z * 0.12f) * 2f) + Rim(x, z, 4f);
            },
            splat = (x, z, h, s, w) => { w[0] = 1; w[1] = Mathf.InverseLerp(3f, 4.5f, h) + Mathf.InverseLerp(0.4f, 0.7f, s); },
            layers = new Func<float, float, Color>[] { Sand, Rock },
            tile = new[] { 6f, 7f },
            liquid = Color.clear,
            sun = new Color(1f, 0.92f, 0.8f),
            fog = Hex("#f0d6b0"), skyTint = Hex("#e8b27a"),
            start = P(30, 168), boss = P(40, 22),
            decorate = (root, dir, terrain) =>
            {
                var rng = new System.Random(22);
                Spawn(Model(dir, "Canyon_Backdrop"), root, null, 0f, 0f, 0f, 1f);
                var rocks = Models(dir, "Canyon_Rock0", "Canyon_Rock1", "Canyon_Rock2");
                // boulders hug the foot of the cliffs, pillars stand on the tops, plants dot both
                Scatter(rocks, root, terrain, rng, 55, (x, z, h) => h > 2.3f && h < 4.5f, 0.8f, 1.9f, 0.3f);
                Scatter(rocks, root, terrain, rng, 25, (x, z, h) => h > 8.5f, 1.0f, 2.2f, 0.3f);
                Scatter(Models(dir, "Canyon_Pillar0", "Canyon_Pillar1"), root, terrain, rng, 10, (x, z, h) => h > 8.5f, 0.8f, 1.4f, 0.2f, 31f);
                Scatter(Models(dir, "Canyon_Cactus0", "Canyon_Cactus1"), root, terrain, rng, 12, (x, z, h) => h < 2.3f && Mathf.Abs(x) < 31f && Mathf.Abs(z) < 31f && Near(terrain, x, z, 3.5f), 0.8f, 1.2f);
                Scatter(Models(dir, "Canyon_Cactus0", "Canyon_Cactus1"), root, terrain, rng, 14, (x, z, h) => h > 8.5f, 0.9f, 1.4f);
                Scatter(Models(dir, "Canyon_Shrub"), root, terrain, rng, 70, (x, z, h) => h < 2.4f || h > 8.5f, 0.7f, 1.4f);
            },
        };

        // 3. melted wax lake: four islands joined by narrow causeways
        var islands = new[] { new Vector3(-17.8f, -19.6f, 10f), new Vector3(14.2f, -14.2f, 8.5f), new Vector3(-10.7f, 12.4f, 7.8f), new Vector3(17.8f, 16f, 9.2f) };
        var links = new[] { 0, 1, 0, 2, 2, 3, 1, 3 };
        yield return new MapDef
        {
            id = "Map03", name = "WaxLake",
            height = (x, z) =>
            {
                float land = 0f;
                foreach (var isl in islands)
                    land = Mathf.Max(land, 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(isl.z - 2.5f, isl.z + 2.5f, Dist(x, z, new Vector2(isl.x, isl.y)))));
                for (int i = 0; i < links.Length; i += 2)
                {
                    var a = islands[links[i]]; var b = islands[links[i + 1]];
                    float d = DistToSegment(new Vector2(x, z), new Vector2(a.x, a.y), new Vector2(b.x, b.y));
                    land = Mathf.Max(land, 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(1.6f, 5.6f, d)));
                }
                return Mathf.Lerp(1.3f, 3.2f + Fbm(x * 0.08f, z * 0.08f) * 0.6f, land) + Rim(x, z, 6f);
            },
            splat = (x, z, h, s, w) => { w[0] = 1; w[1] = Mathf.InverseLerp(3.2f, 2.5f, h); },
            layers = new Func<float, float, Color>[] { Grass, Shore },
            tile = new[] { 6f, 6f },
            liquid = Hex("#f3a3c0"),
            sun = new Color(1f, 0.95f, 0.95f),
            fog = Hex("#f6d8e6"), skyTint = Hex("#e9a8c8"),
            start = new Vector2(-19, -21), boss = new Vector2(17.8f, 16f),
            decorate = (root, dir, terrain) =>
            {
                var rng = new System.Random(33);
                Spawn(Model(dir, "Lake_Backdrop"), root, null, 0f, 0f, 0f, 1f);
                var candles = Models(dir, "Lake_Candle0", "Lake_Candle1", "Lake_Candle2");
                // big candles stand in the melted wax, small ones light the island shores
                Scatter(candles, root, terrain, rng, 14, (x, z, h) => h < 1.5f, 0.8f, 1.8f, 0f, 40f);
                Scatter(candles, root, terrain, rng, 10, (x, z, h) => h > 3.2f && Near(terrain, x, z, 3f), 0.22f, 0.32f);
                foreach (var pad in Scatter(Models(dir, "Lake_WaxPad0", "Lake_WaxPad1"), root, terrain, rng, 34, (x, z, h) => h < 2.0f, 0.5f, 1.3f, 0f, 40f))
                    pad.position = new Vector3(pad.position.x, LiquidY + 0.03f, pad.position.z);

                string meadowDir = EnvRoot + "/Map01_Meadow";
                Scatter(Models(meadowDir, "Meadow_Flowers0", "Meadow_Flowers1", "Meadow_Flowers2"), root, terrain, rng, 50, (x, z, h) => h > 3.3f, 1.4f, 2.2f, 0f, 30f);
                ScatterGrass(root, terrain, dir, rng, (x, z) =>
                {
                    float h = terrain.SampleHeight(new Vector3(x, 0f, z));
                    return h < 3.3f || Mathf.Max(Mathf.Abs(x), Mathf.Abs(z)) > 33f ? 0f : Fbm(x * 0.03f, z * 0.03f) > 0.45f ? 0.9f : 0.5f;
                });
            },
        };

        // 4. crystal plateau: three square tiers, two ramps per step
        yield return new MapDef
        {
            id = "Map04", name = "CrystalPlateau",
            height = (x, z) =>
            {
                float r = Mathf.Max(Mathf.Abs(x), Mathf.Abs(z));
                float h = 2f + 3f * Step(r, 24.9f) + 3f * Step(r, 14.2f);
                if (Mathf.Abs(x) < 3f && z > -29f && z < -21f) h = Mathf.Lerp(2f, 5f, Mathf.InverseLerp(-29f, -21f, z));
                if (Mathf.Abs(x) < 3f && z < 29f && z > 21f) h = Mathf.Lerp(2f, 5f, Mathf.InverseLerp(29f, 21f, z));
                if (Mathf.Abs(z) < 3f && x > 11f && x < 18f) h = Mathf.Lerp(8f, 5f, Mathf.InverseLerp(11f, 18f, x));
                if (Mathf.Abs(z) < 3f && x < -11f && x > -18f) h = Mathf.Lerp(8f, 5f, Mathf.InverseLerp(-11f, -18f, x));
                return h + (Fbm(x * 0.1f, z * 0.1f) - 0.5f) * 0.3f + Rim(x, z, 8f);
            },
            splat = (x, z, h, s, w) => { w[0] = 1; w[1] = Mathf.InverseLerp(6.5f, 7.5f, h); w[2] = Mathf.InverseLerp(0.45f, 0.7f, s); },
            layers = new Func<float, float, Color>[] { Stone, Tile, Cliff },
            tile = new[] { 6f, 4f, 5f },
            liquid = Color.clear,
            sun = new Color(0.95f, 0.92f, 1f),
            start = new Vector2(0, -30), boss = Vector2.zero,
            fog = Hex("#d6cdf2"), skyTint = Hex("#9a8fe0"),
            decorate = (root, dir, terrain) =>
            {
                var rng = new System.Random(44);
                Spawn(Model(dir, "Plateau_Backdrop"), root, null, 0f, 0f, 0f, 1f);
                // crystals everywhere except the ramps and the boss circle
                Scatter(Models(dir, "Plateau_Crystals0", "Plateau_Crystals1", "Plateau_Crystals2"), root, terrain, rng, 34,
                    (x, z, h) => Mathf.Abs(x) > 4f && Mathf.Abs(z) > 4f && x * x + z * z > 49f, 0.7f, 1.6f, 0.1f, 31f);
                // ruined columns line the rim of each tier, skipping the ramp gaps
                var columns = Models(dir, "Plateau_Column0", "Plateau_Column1");
                foreach (float edge in new[] { 23.2f, 12.6f })
                    for (float t = -edge; t <= edge; t += edge / 3f)
                        foreach (var p in new[] { new Vector2(t, edge), new Vector2(t, -edge), new Vector2(edge, t), new Vector2(-edge, t) })
                        {
                            if (Mathf.Abs(p.x) < 5f || Mathf.Abs(p.y) < 5f || rng.NextDouble() < 0.25) continue;
                            SpawnSpaced(columns[rng.Next(2)], root, terrain, p.x, p.y, (float)rng.NextDouble() * 360f, 0.9f + (float)rng.NextDouble() * 0.3f, 0.1f);
                        }
            },
        };

        // 5. crater arena: bowl with a ring track and four molten wax pools
        var pools = new[] { new Vector2(-12.4f, 10.7f), new Vector2(14.2f, -10.7f), new Vector2(11.4f, 13.5f), new Vector2(-14.2f, -12.4f) };
        yield return new MapDef
        {
            id = "Map05", name = "Crater",
            height = (x, z) =>
            {
                float r = Mathf.Sqrt(x * x + z * z);
                float h = 3f + Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(21f, 29f, r)) * 5f + (Fbm(x * 0.09f, z * 0.09f) - 0.5f) * 0.6f;
                foreach (var p in pools) h -= 1.6f * (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(2f, 5.5f, Dist(x, z, p))));
                return h + Rim(x, z, 5f);
            },
            splat = (x, z, h, s, w) =>
            {
                float r = Mathf.Sqrt(x * x + z * z);
                w[0] = 1; w[1] = Mathf.InverseLerp(4f, 6f, h) + Mathf.InverseLerp(0.45f, 0.7f, s);
                w[2] = Band(r, 13.5f, 1.8f) * (1f - w[1]);
            },
            layers = new Func<float, float, Color>[] { RedClay, Basalt, Track },
            tile = new[] { 6f, 6f, 4f },
            liquid = Hex("#ff8a2a"), liquidGlows = true,
            sun = new Color(1f, 0.8f, 0.62f),
            fog = Hex("#7a4436"), skyTint = Hex("#c0603a"),
            start = new Vector2(0, -24), boss = Vector2.zero,
            decorate = (root, dir, terrain) =>
            {
                var rng = new System.Random(55);
                Spawn(Model(dir, "Crater_Backdrop"), root, null, 0f, 0f, 0f, 1f);
                Func<float, float, float> radius = (x, z) => Mathf.Sqrt(x * x + z * z);
                Func<float, float, float> nearestPool = (x, z) => { float d = float.MaxValue; foreach (var p in pools) d = Mathf.Min(d, Dist(x, z, p)); return d; };
                Scatter(Models(dir, "Crater_Basalt0", "Crater_Basalt1", "Crater_Basalt2"), root, terrain, rng, 40, (x, z, h) => radius(x, z) > 21f && radius(x, z) < 31f, 0.8f, 1.7f, 0.2f, 32f);
                Scatter(Models(dir, "Crater_Vent"), root, terrain, rng, 8, (x, z, h) => radius(x, z) > 15f && radius(x, z) < 20f && nearestPool(x, z) > 7f, 0.8f, 1.2f, 0.1f);
                // wax stalagmites ring the molten pools and dot the floor away from the track and the boss
                Scatter(Models(dir, "Crater_Stalagmite"), root, terrain, rng, 14, (x, z, h) => nearestPool(x, z) > 6f && nearestPool(x, z) < 7.5f, 0.8f, 1.3f);
                Scatter(Models(dir, "Crater_Stalagmite"), root, terrain, rng, 10, (x, z, h) => radius(x, z) > 5f && radius(x, z) < 20f && Mathf.Abs(radius(x, z) - 13.5f) > 2.5f && nearestPool(x, z) > 6.5f, 0.7f, 1.1f);
            },
        };
    }

    // ---------- build ----------

    static Texture2D BuildMap(MapDef def, Material terrainMat)
    {
        solids.Clear();
        string dir = $"{EnvRoot}/{def.id}_{def.name}";
        Directory.CreateDirectory(dir);
        AssetDatabase.Refresh();

        // the asset must exist before SetAlphamaps, or the splat textures are never saved as sub-assets
        string dataPath = $"{dir}/TD_{def.id}.asset";
        var data = AssetDatabase.LoadAssetAtPath<TerrainData>(dataPath);
        if (data == null)
        {
            data = new TerrainData();
            AssetDatabase.CreateAsset(data, dataPath);
        }
        data.detailPrototypes = new DetailPrototype[0]; // earlier builds used terrain details for grass
        data.heightmapResolution = HeightRes;
        data.size = new Vector3(Size, MaxHeight, Size);
        var heights = new float[HeightRes, HeightRes];
        for (int j = 0; j < HeightRes; j++)
            for (int i = 0; i < HeightRes; i++)
                heights[j, i] = Mathf.Clamp01(def.height(ToWorld(i, HeightRes), ToWorld(j, HeightRes)) / MaxHeight);
        data.SetHeights(0, 0, heights);

        var layers = new TerrainLayer[def.layers.Length];
        for (int k = 0; k < layers.Length; k++)
        {
            string texPath = $"{dir}/T_{def.id}_Ground{k}.png";
            SaveTexture(texPath, def.layers[k]);
            var layer = new TerrainLayer
            {
                diffuseTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath),
                tileSize = new Vector2(def.tile[k], def.tile[k]),
            };
            Matte(layer);
            layers[k] = SaveAsset(layer, $"{dir}/TL_{def.id}_Ground{k}.terrainlayer");
        }
        data.terrainLayers = layers;
        data.alphamapResolution = SplatRes;
        var splat = new float[SplatRes, SplatRes, layers.Length];
        var w = new float[layers.Length];
        float step = Size / (SplatRes - 1);
        for (int j = 0; j < SplatRes; j++)
            for (int i = 0; i < SplatRes; i++)
            {
                float x = ToWorld(i, SplatRes), z = ToWorld(j, SplatRes);
                float h = def.height(x, z);
                float slope = new Vector2(def.height(x + step, z) - def.height(x - step, z), def.height(x, z + step) - def.height(x, z - step)).magnitude / (2f * step);
                Array.Clear(w, 0, w.Length);
                def.splat(x, z, h, slope, w);
                // later layers paint over earlier ones
                float rest = 1f;
                for (int k = w.Length - 1; k >= 0; k--)
                {
                    float v = Mathf.Clamp01(k == 0 ? 1f : w[k]) * rest;
                    splat[j, i, k] = v;
                    rest -= v;
                }
            }
        data.SetAlphamaps(0, 0, splat);
        EditorUtility.SetDirty(data);
        AssetDatabase.SaveAssets();

        var previous = SceneManager.GetActiveScene();
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        SceneManager.SetActiveScene(scene);
        ApplyAtmosphere(def, dir); // RenderSettings belong to the active scene, so this lands in the map scene

        var root = new GameObject($"{def.id}_{def.name}").transform;
        var terrainGo = Terrain.CreateTerrainGameObject(data);
        terrainGo.name = "Terrain";
        terrainGo.transform.SetParent(root, false);
        terrainGo.transform.position = new Vector3(-Size * 0.5f, 0f, -Size * 0.5f);
        var terrain = terrainGo.GetComponent<Terrain>();
        terrain.materialTemplate = terrainMat;

        var sun = new GameObject("Sun").AddComponent<Light>();
        sun.transform.SetParent(root, false);
        sun.type = LightType.Directional;
        ApplySun(sun, def);

        if (def.liquid.a > 0f)
        {
            var liquid = GameObject.CreatePrimitive(PrimitiveType.Plane);
            liquid.name = "Zone_Liquid";
            Object.DestroyImmediate(liquid.GetComponent<Collider>());
            liquid.transform.SetParent(root, false);
            liquid.transform.position = new Vector3(0f, LiquidY, 0f);
            liquid.transform.localScale = new Vector3(Size / 10f, 1f, Size / 10f);
            liquid.GetComponent<MeshRenderer>().sharedMaterial = LiquidMaterial(def, dir);
        }

        if (def.liquid.a > 0f && !def.liquidGlows)
        {
            // the water volume (everything below the waterline; land is above it). WaterFloat keeps
            // the player and the monsters inside it floating half submerged.
            // Ignore Raycast layer so the game's raycasts never hit this trigger.
            var waterVolume = new GameObject("Zone_WaterVolume", typeof(BoxCollider), typeof(Rigidbody), typeof(WaterFloat));
            waterVolume.layer = 2;
            waterVolume.transform.SetParent(root, false);
            waterVolume.transform.position = new Vector3(0f, LiquidY - 2f, 0f);
            var volume = waterVolume.GetComponent<BoxCollider>();
            volume.isTrigger = true;
            volume.size = new Vector3(Size, 4f, Size);
            waterVolume.GetComponent<Rigidbody>().isKinematic = true;
        }

        // Thick solid blocks just outside the ±33 walls. The game's spawner skips any point that
        // overlaps a collider, so monsters can no longer appear (and get stuck) beyond the walls.
        var outside = new GameObject("Zone_OutOfBounds").transform;
        outside.SetParent(root, false);
        foreach (var (center, size) in new[]
        {
            (new Vector3(0f, 10f, 48f), new Vector3(Size + 30f, 60f, 30f)),
            (new Vector3(0f, 10f, -48f), new Vector3(Size + 30f, 60f, 30f)),
            (new Vector3(48f, 10f, 0f), new Vector3(30f, 60f, Size + 30f)),
            (new Vector3(-48f, 10f, 0f), new Vector3(30f, 60f, Size + 30f)),
        })
        {
            var box = new GameObject("Block").AddComponent<BoxCollider>();
            box.transform.SetParent(outside, false);
            box.transform.position = center;
            box.size = size;
        }

        var markers = new GameObject("Markers").transform;
        markers.SetParent(root, false);
        Marker(markers, "Marker_Start", def.start, terrain);
        Marker(markers, "Marker_Boss", def.boss, terrain);

        if (def.decorate != null)
        {
            var decor = new GameObject("Decor").transform;
            decor.SetParent(root, false);
            def.decorate(decor, dir, terrain);
            // nothing in Decor moves, so let Unity merge it into static batches at build/play time
            foreach (var t in decor.GetComponentsInChildren<Transform>())
                GameObjectUtility.SetStaticEditorFlags(t.gameObject, StaticEditorFlags.BatchingStatic);
        }

        EditorSceneManager.SaveScene(scene, $"{SceneDir}/{def.id}_{def.name}.unity");
        var shot = Capture(root, def);
        EditorSceneManager.CloseScene(scene, true);
        if (previous.IsValid()) SceneManager.SetActiveScene(previous);
        return shot;
    }

    static void Marker(Transform parent, string name, Vector2 xz, Terrain terrain)
    {
        var go = new GameObject(name).transform;
        go.SetParent(parent, false);
        var p = new Vector3(xz.x, 0f, xz.y);
        go.position = new Vector3(p.x, terrain.SampleHeight(p), p.z);
    }

    static void ApplyAtmosphere(MapDef def, string dir)
    {
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogStartDistance = 55f;
        RenderSettings.fogEndDistance = 170f;
        RenderSettings.fogColor = def.fog;
        var sky = LoadOrCreateMaterial($"{dir}/M_{def.id}_Sky.mat", "Game/StylizedSky", Color.white, false);
        sky.SetColor("_ZenithColor", def.skyTint * 0.85f);
        sky.SetColor("_HorizonColor", def.fog); // distant hills fade into the horizon, not into a band
        sky.SetColor("_GroundColor", def.fog * 0.92f);
        sky.SetColor("_CloudColor", Color.Lerp(def.fog, Color.white, 0.75f));
        sky.SetColor("_CloudShade", Color.Lerp(def.fog, def.skyTint, 0.5f));
        RenderSettings.skybox = sky;
        // the fill stays well below the sun so lit and shaded sides read apart; the sky tint keeps
        // shadows cool instead of grey
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = Color.Lerp(def.skyTint, Color.white, 0.15f) * 0.8f;
        RenderSettings.ambientEquatorColor = def.fog * 0.65f;
        RenderSettings.ambientGroundColor = def.fog * 0.3f;
    }

    static void ApplySun(Light sun, MapDef def)
    {
        // warm key light from the side rather than behind the camera, so shapes get a lit and a shaded face
        sun.color = def.sun * new Color(1f, 0.95f, 0.86f);
        sun.intensity = 1.7f;
        sun.shadows = LightShadows.Soft;
        sun.transform.rotation = Quaternion.Euler(45f, 120f, 0f);
    }

    static Material LiquidMaterial(MapDef def, string dir)
    {
        string path = $"{dir}/M_{def.id}_Liquid.mat";
        if (def.liquidGlows) return LoadOrCreateMaterial(path, "Universal Render Pipeline/Lit", def.liquid, true);
        var mat = LoadOrCreateMaterial(path, "Game/StylizedWater", def.liquid, false);
        mat.SetColor("_DeepColor", def.liquid * 0.7f);
        mat.SetColor("_SkyColor", Color.Lerp(def.skyTint, Color.white, 0.5f));
        return mat;
    }

    // Re-applies lighting, sky, water, grass and ground colors to the saved maps without rebuilding them,
    // so components added to the scenes after the build (navigation) are kept.
    [MenuItem("Tools/Design/Refresh Map Look")]
    public static void RefreshLook()
    {
        if (!LeaveDesignScenes()) return;
        var previous = SceneManager.GetActiveScene();
        foreach (var def in Defs())
        {
            string dir = $"{EnvRoot}/{def.id}_{def.name}";
            for (int k = 0; k < def.layers.Length; k++)
                SaveTexture($"{dir}/T_{def.id}_Ground{k}.png", def.layers[k]);
            var scene = EditorSceneManager.OpenScene($"{SceneDir}/{def.id}_{def.name}.unity", OpenSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            ApplyAtmosphere(def, dir);
            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var light in root.GetComponentsInChildren<Light>(true))
                    if (light.name == "Sun") ApplySun(light, def);
                foreach (var r in root.GetComponentsInChildren<MeshRenderer>(true))
                    if (r.name == "Zone_Liquid") r.sharedMaterial = LiquidMaterial(def, dir);
            }
            EditorSceneManager.SaveScene(scene);
            EditorSceneManager.CloseScene(scene, true);
        }
        if (previous.IsValid()) SceneManager.SetActiveScene(previous);

        foreach (var guid in AssetDatabase.FindAssets("Grass_ t:Mesh", new[] { EnvRoot }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (!path.Contains("/Models/Generated/Grass_")) continue;
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            PaintBladeHeights(mesh);
            EditorUtility.SetDirty(mesh);
        }
        foreach (var guid in AssetDatabase.FindAssets("t:TerrainLayer", new[] { EnvRoot }))
        {
            var layer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(AssetDatabase.GUIDToAssetPath(guid));
            Matte(layer);
            EditorUtility.SetDirty(layer);
        }
        GrassMaterial(Model(EnvRoot + "/Map01_Meadow", "Meadow_GrassTuft"));
        AssetDatabase.SaveAssets();
        Debug.Log("[MapBuilder] refreshed lighting, water and grass on the saved maps");
    }

    // Imports a Blender-exported FBX from <dir>/Models and swaps its embedded materials for
    // editable M_ assets (instancing on, matte) so terrain details and batching work.
    static GameObject Model(string dir, string name)
    {
        string path = $"{dir}/Models/{name}.fbx";
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        var importer = (ModelImporter)AssetImporter.GetAtPath(path);
        bool changed = false;
        if (importer.importAnimation || importer.animationType != ModelImporterAnimationType.None)
        {
            importer.importAnimation = false;
            importer.animationType = ModelImporterAnimationType.None;
            changed = true;
        }
        foreach (var embedded in AssetDatabase.LoadAllAssetsAtPath(path))
        {
            if (!(embedded is Material source)) continue;
            var color = source.HasProperty("_BaseColor") ? source.GetColor("_BaseColor") : source.color;
            bool glow = source.name.Contains("Glow"); // flames, lava, crystals (see map_kits.py)
            var mat = LoadOrCreateMaterial($"{dir}/Models/M_{source.name}.mat", "Universal Render Pipeline/Lit", color, glow);
            mat.SetFloat("_Smoothness", glow || source.name.Contains("Wax") ? 0.45f : 0.15f);
            mat.enableInstancing = true;
            importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), source.name), mat);
            changed = true;
        }
        if (changed) importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<GameObject>(path);
    }

    static GameObject[] Models(string dir, params string[] names) => Array.ConvertAll(names, n => Model(dir, n));

    // Drops `count` random picks from `kinds` where ok(x, z, groundHeight) holds, inside ±area.
    static List<Transform> Scatter(GameObject[] kinds, Transform root, Terrain terrain, System.Random rng, int count,
        Func<float, float, float, bool> ok, float minScale, float maxScale, float sink = 0f, float area = 42f)
    {
        var placed = new List<Transform>();
        for (int tries = 0; placed.Count < count && tries < count * 40; tries++)
        {
            float x = ((float)rng.NextDouble() * 2f - 1f) * area, z = ((float)rng.NextDouble() * 2f - 1f) * area;
            if (!ok(x, z, terrain.SampleHeight(new Vector3(x, 0f, z)))) continue;
            float scale = Mathf.Lerp(minScale, maxScale, (float)rng.NextDouble());
            var prop = SpawnSpaced(kinds[rng.Next(kinds.Length)], root, terrain, x, z, (float)rng.NextDouble() * 360f, scale, sink * scale);
            if (prop != null) placed.Add(prop);
        }
        return placed;
    }

    // Spawn, but give up (and return null) when a solid prop would crowd another one.
    static Transform SpawnSpaced(GameObject source, Transform parent, Terrain terrain, float x, float z, float yaw, float scale, float sink = 0f)
    {
        int solidCount = solids.Count;
        var prop = Spawn(source, parent, terrain, x, z, yaw, scale, sink);
        if (solids.Count == solidCount || !CrowdsAnotherProp(solids[solids.Count - 1])) return prop;
        solids.RemoveAt(solids.Count - 1);
        Object.DestroyImmediate(prop.gameObject);
        return null;
    }

    // true when the ground `dist` metres away in any direction is more than 1.5m higher or lower (cliff foot, shore)
    static bool Near(Terrain terrain, float x, float z, float dist)
    {
        float h = terrain.SampleHeight(new Vector3(x, 0f, z));
        for (int i = 0; i < 8; i++)
        {
            float a = i * Mathf.PI / 4f;
            if (Mathf.Abs(terrain.SampleHeight(new Vector3(x + Mathf.Cos(a) * dist, 0f, z + Mathf.Sin(a) * dist)) - h) > 1.5f) return true;
        }
        return false;
    }

    // Grass tufts baked into 6x6 combined meshes per map. Terrain details did not show up in
    // off-screen renders, and plain meshes behave the same in every camera.
    // ponytail: ~66k grass triangles on the meadow, culled per chunk by distance; check on a real phone
    static void ScatterGrass(Transform root, Terrain terrain, string dir, System.Random rng, Func<float, float, float> chance)
    {
        var tuft = Model(EnvRoot + "/Map01_Meadow", "Meadow_GrassTuft");
        var tuftMesh = tuft.GetComponentInChildren<MeshFilter>().sharedMesh;
        var tuftMat = GrassMaterial(tuft);
        string genDir = dir + "/Models/Generated";
        Directory.CreateDirectory(genDir);
        AssetDatabase.Refresh();
        var grass = new GameObject("Grass").transform;
        grass.SetParent(root, false);
        const int chunks = 6;
        float span = 84f / chunks;
        var parts = new List<CombineInstance>();
        for (int cz = 0; cz < chunks; cz++)
            for (int cx = 0; cx < chunks; cx++)
            {
                var origin = new Vector3(-42f + (cx + 0.5f) * span, 0f, -42f + (cz + 0.5f) * span);
                parts.Clear();
                for (int k = 0; k < 300; k++)
                {
                    float x = origin.x + ((float)rng.NextDouble() - 0.5f) * span, z = origin.z + ((float)rng.NextDouble() - 0.5f) * span;
                    if (rng.NextDouble() > chance(x, z)) continue;
                    var p = new Vector3(x, terrain.SampleHeight(new Vector3(x, 0f, z)), z) - origin;
                    // knee-high at most, so small monsters stay visible
                    var scale = Vector3.one * (0.9f + (float)rng.NextDouble() * 0.6f);
                    parts.Add(new CombineInstance { mesh = tuftMesh, transform = Matrix4x4.TRS(p, Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f), scale) });
                }
                if (parts.Count == 0) continue;
                var mesh = new Mesh { indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
                mesh.CombineMeshes(parts.ToArray(), true, true);
                PaintBladeHeights(mesh);
                mesh = SaveAsset(mesh, $"{genDir}/Grass_{cx}_{cz}.asset");
                var chunk = new GameObject(mesh.name, typeof(MeshFilter), typeof(MeshRenderer));
                chunk.transform.SetParent(grass, false);
                chunk.transform.position = origin;
                chunk.GetComponent<MeshFilter>().sharedMesh = mesh;
                var mr = chunk.GetComponent<MeshRenderer>();
                mr.sharedMaterial = tuftMat;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                CullWhenSmall(chunk, 0.12f); // a 14m chunk drops out roughly 100m away, past the fog
            }
    }

    // A new TerrainLayer reads smoothness from the albedo alpha, which is opaque in the generated
    // ground textures, so the ground rendered fully glossy (wet-plastic hills).
    static void Matte(TerrainLayer layer)
    {
        layer.smoothnessSource = TerrainLayerSmoothnessSource.Constant;
        layer.smoothness = 0f;
    }

    // blades are single triangles drawn from both sides; Game/StylizedGrass tints and sways them
    static Material GrassMaterial(GameObject tuft)
    {
        var mat = tuft.GetComponentInChildren<MeshRenderer>().sharedMaterial;
        mat.shader = Shader.Find("Game/StylizedGrass");
        // root and tip stay close to the ground speckle colors so distant tufts blend instead of dotting the hills
        mat.SetColor("_BaseColor", Hex("#4a8a36"));
        mat.SetColor("_TipColor", Hex("#86bd57"));
        mat.doubleSidedGI = true;
        EditorUtility.SetDirty(mat);
        return mat;
    }

    // Vertex color R: 0 at a blade's root, 1 at its tip. Every blade is one triangle with its own
    // three vertices, and the tip is the highest of them.
    static void PaintBladeHeights(Mesh mesh)
    {
        var verts = mesh.vertices;
        var tris = mesh.triangles;
        var colors = new Color[verts.Length];
        for (int t = 0; t < tris.Length; t += 3)
        {
            int tip = tris[t];
            for (int k = 1; k < 3; k++)
                if (verts[tris[t + k]].y > verts[tip].y) tip = tris[t + k];
            for (int k = 0; k < 3; k++)
                colors[tris[t + k]] = tris[t + k] == tip ? Color.red : Color.black;
        }
        mesh.colors = colors;
    }

    static void CullWhenSmall(GameObject go, float screenHeight)
    {
        var lod = go.AddComponent<LODGroup>();
        lod.SetLODs(new[] { new LOD(screenHeight, go.GetComponentsInChildren<Renderer>()) });
        lod.RecalculateBounds();
    }

    // terrain == null keeps the given y of 0 (used for the backdrop ring, which sits at the origin)
    static Transform Spawn(GameObject source, Transform parent, Terrain terrain, float x, float z, float yaw, float scale, float sink = 0f)
    {
        var go = (GameObject)PrefabUtility.InstantiatePrefab(source, parent.gameObject.scene);
        go.transform.SetParent(parent, false);
        var p = new Vector3(x, 0f, z);
        if (terrain != null) p.y = terrain.SampleHeight(p) + terrain.transform.position.y - sink;
        go.transform.position = p;
        go.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        go.transform.localScale *= scale;

        // small props (flowers, shrubs, pads, pebbles) and the backdrop cast no shadows;
        // small props also vanish once they shrink below 1.5% of the screen height
        var renderers = go.GetComponentsInChildren<Renderer>();
        var bounds = renderers[0].bounds;
        foreach (var r in renderers) bounds.Encapsulate(r.bounds);
        bool small = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z) < 1.5f;
        if (small || terrain == null)
            foreach (var r in renderers) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        if (small) CullWhenSmall(go, 0.015f);

        // solid props block the player and the monsters (their movement raycasts slide along colliders);
        // plants and wax pads stay walk-through. Bridges keep the exact mesh so the deck is walkable.
        bool soft = source.name.Contains("Shrub") || source.name.Contains("Flowers") || source.name.Contains("Pad") || source.name.Contains("Grass");
        if (terrain != null && !small && !soft)
        {
            foreach (var mf in go.GetComponentsInChildren<MeshFilter>())
                mf.gameObject.AddComponent<MeshCollider>().convex = !source.name.Contains("Bridge");
            solids.Add(new Vector3(bounds.center.x, bounds.center.z, Mathf.Max(bounds.extents.x, bounds.extents.z)));
        }
        return go.transform;
    }

    // footprints (x, z, radius) of the solid props on the map being built
    static readonly List<Vector3> solids = new List<Vector3>();

    // Clear walkway required between two solid props: wider than the player (0.72 m) and the regular
    // monsters (~1 m), so nothing can wedge itself into a slit between props.
    const float PropGap = 1.6f;

    static bool CrowdsAnotherProp(Vector3 prop)
    {
        for (int i = 0; i < solids.Count - 1; i++)
            if (Vector2.Distance(new Vector2(prop.x, prop.y), new Vector2(solids[i].x, solids[i].y)) < prop.z + solids[i].z + PropGap)
                return true;
        return false;
    }

    // ---------- capture ----------

    static Texture2D Capture(Transform root, MapDef def)
    {
        // render only this map: move it to a spare layer and hide it from every other light
        SetLayer(root, CaptureLayer);
        var others = new List<(Light light, int mask)>();
        for (int s = 0; s < SceneManager.sceneCount; s++)
        {
            var sc = SceneManager.GetSceneAt(s);
            if (sc == root.gameObject.scene || !sc.isLoaded) continue;
            foreach (var go in sc.GetRootGameObjects())
                foreach (var l in go.GetComponentsInChildren<Light>())
                {
                    others.Add((l, l.cullingMask));
                    l.cullingMask &= ~(1 << CaptureLayer);
                }
        }

        var cam = new GameObject("CaptureCam").AddComponent<Camera>();
        cam.cullingMask = 1 << CaptureLayer;
        cam.fieldOfView = 45f;
        cam.farClipPlane = 300f;
        var start = new Vector3(def.start.x, 0f, def.start.y);
        cam.transform.position = start * 1.25f + new Vector3(-14f, 30f, 0f) + (start.sqrMagnitude < 1f ? new Vector3(0, 0, -40f) : Vector3.zero);
        cam.transform.LookAt(new Vector3(0f, 2f, 0f));

        var rt = new RenderTexture(640, 360, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        var big = new RenderTexture(1280, 720, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        var shot = Render(cam, rt);
        var full = Render(cam, big);
        File.WriteAllBytes($"{CaptureDir}/{def.id}_{def.name}.png", full.EncodeToPNG());
        Object.DestroyImmediate(full);

        // eye-level shot from the start marker toward the boss, roughly what the player sees
        var terrain = root.GetComponentInChildren<Terrain>();
        var boss = new Vector3(def.boss.x, 0f, def.boss.y);
        var fwd = boss - start;
        fwd.y = 0f;
        fwd.Normalize();
        start.y = terrain.SampleHeight(start);
        cam.fieldOfView = 55f;
        var eye = start - fwd * 5f;
        eye.y = Mathf.Max(start.y, terrain.SampleHeight(eye)) + 3.2f; // the start can sit on a slope
        cam.transform.position = eye;
        cam.transform.LookAt(start + fwd * 12f + Vector3.up * 1.2f);
        full = Render(cam, big);
        File.WriteAllBytes($"{CaptureDir}/{def.id}_{def.name}_ground.png", full.EncodeToPNG());
        Object.DestroyImmediate(full);
        rt.Release(); big.Release();
        Object.DestroyImmediate(rt); Object.DestroyImmediate(big);
        Object.DestroyImmediate(cam.gameObject);
        foreach (var (light, mask) in others) light.cullingMask = mask;
        return shot;
    }

    static Texture2D Render(Camera cam, RenderTexture rt)
    {
        cam.targetTexture = rt;
        cam.Render();
        var prev = RenderTexture.active;
        RenderTexture.active = rt;
        var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
        tex.Apply();
        RenderTexture.active = prev;
        cam.targetTexture = null;
        return tex;
    }

    static void SetLayer(Transform t, int layer)
    {
        t.gameObject.layer = layer;
        foreach (Transform c in t) SetLayer(c, layer);
    }

    // ---------- terrain shape helpers ----------

    static float ToWorld(int i, int res) => i / (float)(res - 1) * Size - Size * 0.5f;

    // sketch panel (180px, y down) -> world xz inside the ±32 play area
    static Vector2 P(float px, float py) => new Vector2(px / 180f * 64f - 32f, 32f - py / 180f * 64f);

    // The game's invisible walls sit at ±33, so the ground climbs steeply right there
    // (a visible bank where the wall is), then rolls on into hills toward the backdrop.
    static float Rim(float x, float z, float height)
    {
        float r = Mathf.Max(Mathf.Abs(x), Mathf.Abs(z));
        float bank = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(32.5f, 36f, r)) * 4.5f;
        return bank + Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(36f, 46f, r)) * (height + Fbm(x * 0.07f + 5f, z * 0.07f) * 4f);
    }

    static float Bump(float x, float z, float cx, float cz, float radius, float height)
    {
        float d2 = ((x - cx) * (x - cx) + (z - cz) * (z - cz)) / (radius * radius);
        return height * Mathf.Exp(-d2 * 2f);
    }

    static float Band(float v, float center, float halfWidth) => 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(halfWidth, halfWidth + 1.5f, Mathf.Abs(v - center)));

    static float Step(float r, float edge) => 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(edge - 0.6f, edge + 0.6f, r));

    static float Dist(float x, float z, Vector2 p) => Vector2.Distance(new Vector2(x, z), p);

    static List<Vector2> SampleBezier(Vector2[] c)
    {
        var pts = new List<Vector2>();
        for (int s = 0; s + 3 < c.Length; s += 4)
            for (int i = 0; i <= 24; i++)
            {
                float t = i / 24f, u = 1f - t;
                pts.Add(u * u * u * c[s] + 3f * u * u * t * c[s + 1] + 3f * u * t * t * c[s + 2] + t * t * t * c[s + 3]);
            }
        return pts;
    }

    static float DistToPath(List<Vector2> pts, float x, float z)
    {
        var p = new Vector2(x, z);
        float best = float.MaxValue;
        for (int i = 0; i + 1 < pts.Count; i++) best = Mathf.Min(best, DistToSegment(p, pts[i], pts[i + 1]));
        return best;
    }

    static float DistToSegment(Vector2 p, Vector2 a, Vector2 b)
    {
        var ab = b - a;
        float t = ab.sqrMagnitude < 1e-6f ? 0f : Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
        return Vector2.Distance(p, a + ab * t);
    }

    // ---------- tileable noise + ground recipes ----------

    static readonly int[] Octaves = { 4, 8, 16, 32 };
    static float[][,] grids;

    static float Fbm(float u, float v)
    {
        if (grids == null)
        {
            var rng = new System.Random(100);
            grids = new float[Octaves.Length][,];
            for (int o = 0; o < Octaves.Length; o++)
            {
                int n = Octaves[o];
                grids[o] = new float[n, n];
                for (int i = 0; i < n; i++)
                    for (int j = 0; j < n; j++) grids[o][i, j] = (float)rng.NextDouble();
            }
        }
        float sum = 0f, amp = 0.5f, total = 0f;
        for (int o = 0; o < Octaves.Length; o++)
        {
            sum += Lattice(grids[o], Octaves[o], u, v) * amp;
            total += amp;
            amp *= 0.5f;
        }
        return sum / total;
    }

    static float Lattice(float[,] g, int n, float u, float v)
    {
        float x = u * n, y = v * n;
        int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y);
        float fx = x - x0, fy = y - y0;
        fx = fx * fx * (3f - 2f * fx);
        fy = fy * fy * (3f - 2f * fy);
        int a = ((x0 % n) + n) % n, b = ((y0 % n) + n) % n, a1 = (a + 1) % n, b1 = (b + 1) % n;
        return Mathf.Lerp(Mathf.Lerp(g[a, b], g[a1, b], fx), Mathf.Lerp(g[a, b1], g[a1, b1], fx), fy);
    }

    static float Hash(float x, float y)
    {
        float h = Mathf.Sin(x * 127.1f + y * 311.7f) * 43758.5453f;
        return h - Mathf.Floor(h);
    }

    static Color Speckled(float u, float v, string dark, string light, float offset)
    {
        var c = Color.Lerp(Hex(dark), Hex(light), Fbm(u + offset, v + offset * 0.5f));
        float h = Hash(Mathf.Floor(u * 256f) + offset * 31f, Mathf.Floor(v * 256f));
        return h < 0.07f ? c * 0.85f : h > 0.96f ? Color.Lerp(c, Color.white, 0.15f) : c;
    }

    static Color Grass(float u, float v) => Speckled(u, v, "#5f9f48", "#79b457", 0f);
    static Color Dirt(float u, float v) => Speckled(u, v, "#b8956a", "#d6b88a", 0.37f);
    static Color Shore(float u, float v) => Speckled(u, v, "#e7c9a3", "#f6e0c2", 0.21f);
    static Color Sand(float u, float v) => Speckled(u, v, "#d9b47e", "#efd09c", 0.13f);
    static Color Stone(float u, float v) => Speckled(u, v, "#8670c8", "#a38fdc", 0.41f);
    static Color Cliff(float u, float v) => Speckled(u, v, "#5a4596", "#6f58ad", 0.77f);
    static Color RedClay(float u, float v) => Speckled(u, v, "#8a4f34", "#a8673f", 0.29f);
    static Color Basalt(float u, float v) => Speckled(u, v, "#3e2a2a", "#5a3b36", 0.63f);
    static Color Track(float u, float v) => Speckled(u, v, "#6a4030", "#7a4b36", 0.55f);

    static Color Rock(float u, float v)
    {
        var c = Speckled(u, v, "#8d5b3a", "#a9714b", 0.51f);
        float strata = Mathf.Sin(v * 40f + Fbm(u * 0.5f, v) * 8f) * 0.5f + 0.5f;
        return Color.Lerp(c, c * 0.82f, strata * 0.6f);
    }

    static Color Tile(float u, float v)
    {
        var c = Speckled(u, v, "#c2b0ee", "#d6caf6", 0.19f);
        float fu = u * 2f % 1f, fv = v * 2f % 1f;
        return fu < 0.02f || fv < 0.02f ? c * 0.8f : c;
    }

    // ---------- asset helpers ----------

    static Color Hex(string s)
    {
        ColorUtility.TryParseHtmlString(s, out var c);
        return c;
    }

    static void SaveTexture(string path, Func<float, float, Color> recipe)
    {
        const int n = 256;
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
        var px = new Color[n * n];
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                var c = recipe((x + 0.5f) / n, (y + 0.5f) / n);
                c.a = 1f;
                px[y * n + x] = c;
            }
        tex.SetPixels(px);
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.wrapMode = TextureWrapMode.Repeat;
        importer.mipmapEnabled = true;
        importer.maxTextureSize = 256;
        importer.SaveAndReimport();
    }

    static T SaveAsset<T>(T asset, string path) where T : Object
    {
        asset.name = Path.GetFileNameWithoutExtension(path);
        if (asset is Mesh) AssetDatabase.DeleteAsset(path); // CopySerialized leaves an existing mesh's geometry unchanged
        var existing = AssetDatabase.LoadAssetAtPath<T>(path);
        if (existing == null)
        {
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }
        EditorUtility.CopySerialized(asset, existing);
        Object.DestroyImmediate(asset);
        EditorUtility.SetDirty(existing);
        return existing;
    }

    static Material LoadOrCreateMaterial(string path, string shader, Color color, bool glow)
    {
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            mat = new Material(Shader.Find(shader));
            AssetDatabase.CreateAsset(mat, path);
        }
        else if (mat.shader.name != shader)
            mat.shader = Shader.Find(shader);
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
        if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", glow ? 0.3f : 0.75f);
        if (glow)
        {
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", color * 1.4f);
            mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
        }
        EditorUtility.SetDirty(mat);
        return mat;
    }
}
