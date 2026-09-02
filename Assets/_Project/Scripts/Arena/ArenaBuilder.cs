using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Swarm.Arena
{
    /// <summary>
    /// Lays the arena — ground, worn dirt, scattered props, the centre sigil and the ring wall —
    /// at scene load, on any scene that has a player in it.
    ///
    /// Built in code rather than authored into the scenes because the ground alone is ~19,600
    /// tiles and the wall another 440 sprites; committing that to Game.unity and TestStage.unity
    /// would bloat both scenes with objects nobody edits by hand. The seed is fixed, so the layout
    /// is identical every run and between the two scenes — it reads as one place, not as noise
    /// that reshuffles on restart.
    ///
    /// Art comes from Resources/Ground so nothing has to be wired into a scene. Missing sprites
    /// are skipped rather than logged: an arena with no props is still playable.
    /// </summary>
    public static class ArenaBuilder
    {
        private const string RootName = "Arena (Runtime)";
        private const int Seed = 20260901;

        // Depth is carried by the sorting layers (Ground < Decal < Prop < Wall < ...), so the
        // orders below only separate pieces that share one layer.
        // The player is clamped a unit inside the wall, so the wall never overlaps far enough to
        // need per-row sorting against characters — it can stay a flat layer of its own.
        private const int DirtOrder = 0;
        private const int SigilOrder = 1;

        // The wall sits just outside the clamp line so the player stands in front of the stones
        // rather than buried in them.
        private const float WallRadius = 70.5f;
        // The camera is orthographic size 6, so it shows ~10.6 units either side of the player.
        // Standing against the wall, anything short of that reads as the world simply ending in
        // black. This carries the grass well past the edge of what can ever be on screen.
        private const float GroundMargin = 16f;
        private const float DirtBandInner = 64.5f;
        private const float DirtBandOuter = 69.5f;
        private const int DirtPatchCount = 900;
        private const int PropCount = 700;
        // Sparser than inside: the ground outside the wall should read as the same field
        // continuing, not as a bare apron, but it is not somewhere the player goes.
        private const int OuterPropCount = 220;
        // Props are kept off the sigil and out of the spawn pocket the player starts in.
        private const float PropCentreClearance = 7f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            UnityEngine.SceneManagement.SceneManager.sceneLoaded -= HandleSceneLoaded;
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += HandleSceneLoaded;
            TryBuild();
        }

        private static void HandleSceneLoaded(UnityEngine.SceneManagement.Scene scene,
                                              UnityEngine.SceneManagement.LoadSceneMode mode) => TryBuild();

        private static void TryBuild()
        {
            // The title screen has no arena to draw.
            if (GameObject.FindGameObjectWithTag("Player") == null) return;
            if (GameObject.Find(RootName) != null) return;

            var grass = LoadAll("Grass_01", "Grass_02", "Grass_03", "Grass_04");
            if (grass.Count == 0) return;

            var root = new GameObject(RootName);
            var random = new System.Random(Seed);

            BuildGround(root.transform, grass, random);
            ScatterBand(root.transform, "Dirt", LoadAll("Dirt_Patch_01", "Dirt_Patch_02", "Dirt_Patch_03"),
                        DirtPatchCount, DirtBandInner, DirtBandOuter, SortingLayers.DECAL, DirtOrder, random);
            PlaceSigil(root.transform);
            ScatterBand(root.transform, "Prop",
                        LoadAll("Deco_Stone", "Deco_Flower", "Deco_TallGrass", "Deco_Bush"),
                        PropCount, PropCentreClearance, ArenaBounds.Radius - 1.5f, SortingLayers.PROP, 0, random);
            ScatterBand(root.transform, "OuterProp",
                        LoadAll("Deco_Stone", "Deco_Flower", "Deco_TallGrass", "Deco_Bush"),
                        OuterPropCount, ArenaBounds.Radius + 1.5f, ArenaBounds.Radius + GroundMargin,
                        SortingLayers.PROP, 0, random);
            BuildWall(root.transform, LoadAll("Wall_Stone_01", "Wall_Stone_02", "Wall_Stone_03"), random);
        }

        private static List<Sprite> LoadAll(params string[] names)
        {
            var sprites = new List<Sprite>(names.Length);
            foreach (var name in names)
            {
                var sprite = Resources.Load<Sprite>("Ground/" + name);
                if (sprite != null) sprites.Add(sprite);
            }

            return sprites;
        }

        /// <summary>The ground is a Tilemap rather than 19,600 SpriteRenderers: it batches the
        /// whole field into a handful of chunk meshes and costs one SetTilesBlock call to fill.</summary>
        private static void BuildGround(Transform parent, List<Sprite> grass, System.Random random)
        {
            var gridObject = new GameObject("Ground", typeof(Grid));
            gridObject.transform.SetParent(parent, false);

            // Ground sprites are 32px at 32 PPU, so one tile is exactly one world unit.
            var grid = gridObject.GetComponent<Grid>();
            grid.cellSize = Vector3.one;

            var mapObject = new GameObject("Tilemap", typeof(Tilemap), typeof(TilemapRenderer));
            mapObject.transform.SetParent(gridObject.transform, false);

            var renderer = mapObject.GetComponent<TilemapRenderer>();
            renderer.sortingLayerName = SortingLayers.GROUND;
            renderer.sortingOrder = 0;

            var tiles = new Tile[grass.Count];
            for (var i = 0; i < grass.Count; i++)
            {
                tiles[i] = ScriptableObject.CreateInstance<Tile>();
                tiles[i].sprite = grass[i];
            }

            // A square field covering the circle plus the margin. Filling the corners too is
            // cheaper than testing every cell, and it means the field is square-safe: there is no
            // angle from which the ground runs out before the screen does.
            var extent = Mathf.CeilToInt(ArenaBounds.Radius + GroundMargin);
            var size = extent * 2;
            var bounds = new BoundsInt(-extent, -extent, 0, size, size, 1);
            var block = new TileBase[size * size];
            for (var i = 0; i < block.Length; i++)
            {
                block[i] = tiles[random.Next(tiles.Length)];
            }

            mapObject.GetComponent<Tilemap>().SetTilesBlock(bounds, block);
        }

        private static void PlaceSigil(Transform parent)
        {
            var sprite = Resources.Load<Sprite>("Ground/Center_Sigil");
            if (sprite == null) return;

            CreateSprite(parent, "CentreSigil", sprite, Vector2.zero, 0f, SortingLayers.DECAL, SigilOrder);
        }

        /// <summary>Scatters <paramref name="count"/> sprites uniformly by area between two radii.
        /// Sampling on sqrt of the radius keeps the density even; sampling the radius directly
        /// would crowd everything toward the middle.</summary>
        private static void ScatterBand(Transform parent, string name, List<Sprite> sprites, int count,
                                        float innerRadius, float outerRadius, string sortingLayer,
                                        int sortingOrder, System.Random random)
        {
            if (sprites.Count == 0 || count <= 0) return;

            var group = new GameObject(name).transform;
            group.SetParent(parent, false);

            var innerSqr = innerRadius * innerRadius;
            var outerSqr = outerRadius * outerRadius;

            for (var i = 0; i < count; i++)
            {
                var radius = Mathf.Sqrt(Mathf.Lerp(innerSqr, outerSqr, (float)random.NextDouble()));
                var angle = (float)random.NextDouble() * Mathf.PI * 2f;
                var position = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;

                var sprite = sprites[random.Next(sprites.Count)];
                var instance = CreateSprite(group, name, sprite, position, 0f, sortingLayer, sortingOrder);
                // Mirroring doubles the apparent variety for free. Never rotated: these are lit
                // from above, and a rotated stone would light from the wrong side.
                if (random.Next(2) == 0)
                {
                    var scale = instance.transform.localScale;
                    scale.x = -scale.x;
                    instance.transform.localScale = scale;
                }
            }
        }

        /// <summary>The ring of stones. Each is one world unit wide, so the spacing that closes the
        /// circle exactly is one unit of arc: 2*pi*70 = 439.8, i.e. 440 stones.</summary>
        private static void BuildWall(Transform parent, List<Sprite> sprites, System.Random random)
        {
            if (sprites.Count == 0) return;

            var group = new GameObject("Wall").transform;
            group.SetParent(parent, false);

            var count = Mathf.RoundToInt(2f * Mathf.PI * ArenaBounds.Radius);
            for (var i = 0; i < count; i++)
            {
                var angle = i / (float)count * Mathf.PI * 2f;
                var position = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * WallRadius;
                CreateSprite(group, "Stone", sprites[random.Next(sprites.Count)], position, 0f,
                             SortingLayers.WALL, 0);
            }
        }

        private static GameObject CreateSprite(Transform parent, string name, Sprite sprite,
                                               Vector2 position, float rotationDegrees,
                                               string sortingLayer, int sortingOrder)
        {
            var instance = new GameObject(name, typeof(SpriteRenderer));
            instance.transform.SetParent(parent, false);
            instance.transform.localPosition = position;
            instance.transform.localRotation = Quaternion.Euler(0f, 0f, rotationDegrees);

            var renderer = instance.GetComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingLayerName = sortingLayer;
            renderer.sortingOrder = sortingOrder;
            return instance;
        }
    }
}
