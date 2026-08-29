using System.Collections.Generic;
using System.IO;
using NavMeshPlus.Components;
using NavMeshPlus.Components.Editors;
using NavMeshPlus.Extensions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Tilemaps;

namespace Sussy.EditorTools
{
    /// Builds a complete, playable placeholder level so the simulation can be worked on
    /// before any art exists. Everything it makes is ordinary scene content, so it can be
    /// hand-edited afterwards and the real tilemap can replace the generated one in place.
    public static class PlaceholderLevelBuilder
    {
        const string ProtoFolder = "Assets/Content/Prototypes";

        // 3 rooms joined by a corridor. '#' wall, '.' floor, 'o' social spot.
        // Rows are listed top-down for readability and flipped when built.
        static readonly string[] Map =
        {
            "########################",
            "#........##............#",
            "#........##....o.......#",
            "#...o....##............#",
            "#........##............#",
            "#........##............#",
            "#####..###############.#",
            "#......................#",
            "#......................#",
            "#.####################.#",
            "#.#..................#.#",
            "#.#..................#.#",
            "#.#........o.........#.#",
            "#.#..................#.#",
            "#.####################.#",
            "#......................#",
            "########################",
        };

        struct ProtoSpec
        {
            public string Name;
            public Tag    Tags;
            public Color  Color;
            public string Residue;
        }

        // Tag balance rule: every tag on at least two objects, every verb with at least
        // two valid targets. Break this and the belief generator runs out of material.
        static readonly ProtoSpec[] Protos =
        {
            new() { Name = "Kettle",      Tags = Tag.LiquidHolder | Tag.Carryable | Tag.Cleanable,             Color = new Color(0.8f, 0.8f, 0.85f), Residue = "the kettle is full of paperclips" },
            new() { Name = "Mug",         Tags = Tag.LiquidHolder | Tag.Carryable | Tag.Cleanable,             Color = new Color(0.9f, 0.6f, 0.4f),  Residue = "there is a mug in the bin, again" },
            new() { Name = "Sandwich",    Tags = Tag.Edible | Tag.Carryable | Tag.Cleanable,                   Color = new Color(0.9f, 0.8f, 0.5f),  Residue = "someone took one bite out of the sandwich" },
            new() { Name = "Fruit Bowl",  Tags = Tag.Edible | Tag.ItemHolder | Tag.Cleanable,                  Color = new Color(0.6f, 0.8f, 0.4f),  Residue = "the fruit bowl has a stapler in it" },
            new() { Name = "Filing Box",  Tags = Tag.ItemHolder | Tag.Carryable | Tag.Cleanable,               Color = new Color(0.7f, 0.6f, 0.45f), Residue = "the filing box has been rearranged" },
            new() { Name = "Server Rack", Tags = Tag.Cleanable | Tag.Hittable,                                 Color = new Color(0.3f, 0.35f, 0.4f), Residue = "the server rack smells of coffee" },
            new() { Name = "Fuse Box",    Tags = Tag.Cleanable | Tag.Hittable,                                 Color = new Color(0.5f, 0.45f, 0.3f), Residue = "who has been feeding the fuse box?" },
            new() { Name = "Chair",       Tags = Tag.Sittable | Tag.Cleanable | Tag.Hittable,                  Color = new Color(0.45f, 0.5f, 0.6f), Residue = "the chair is soaking wet" },
            new() { Name = "Couch",       Tags = Tag.Sittable | Tag.Cleanable,                                 Color = new Color(0.55f, 0.4f, 0.5f), Residue = "there are crumbs pressed into the couch" },
            new() { Name = "Desk Phone",  Tags = Tag.EarHoldable | Tag.Carryable | Tag.Cleanable,              Color = new Color(0.25f, 0.25f, 0.3f), Residue = "the phone handset is on backwards" },
            new() { Name = "Radio",       Tags = Tag.EarHoldable | Tag.Cleanable | Tag.Hittable,               Color = new Color(0.6f, 0.35f, 0.25f), Residue = "the radio is tuned to static" },
            new() { Name = "Lab Coat",    Tags = Tag.Wearable | Tag.Carryable | Tag.Cleanable,                 Color = new Color(0.92f, 0.92f, 0.95f), Residue = "a lab coat is folded into a very small square" },
            new() { Name = "Hard Hat",    Tags = Tag.Wearable | Tag.Carryable | Tag.Cleanable,                 Color = new Color(0.95f, 0.75f, 0.2f), Residue = "the hard hat is full of water" },
            new() { Name = "Mop",         Tags = Tag.Carryable | Tag.Cleanable | Tag.Hittable,                 Color = new Color(0.5f, 0.4f, 0.3f),  Residue = "the mop has been put back damp" },
            new() { Name = "Water Cooler",Tags = Tag.LiquidHolder | Tag.Cleanable | Tag.Hittable,              Color = new Color(0.5f, 0.7f, 0.85f), Residue = "the water cooler is empty and warm" },
        };

        static readonly string[] Names = { "Ash", "Bex", "Cal", "Dee", "Ell", "Fen", "Gus", "Hal" };
        static readonly string[] Roles = { "Janitor", "Technician", "Cook", "Analyst", "Security", "Intern", "Nurse", "Clerk" };

        [MenuItem("Sussy/Build Placeholder Level")]
        public static void Build()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            EnsureProtoAssets(out var protos);

            var (grid, walls, floorCells) = BuildTilemaps();
            var surface = BuildNavMeshSurface(walls);

            var objects = PlaceObjects(protos, floorCells);
            var npcs    = PlaceNpcs(floorCells);
            var social  = PlaceSocialPoints();
            var feeds   = BuildFeeds();

            BuildCamera();
            var director = BuildDirector(objects, npcs, social, feeds);
            BuildHud(director);

            // Bake through the asset manager, not BuildNavMesh, so the NavMeshData is written
            // to disk and survives the scene save.
            NavMeshAssetManager.instance.StartBakingSurfaces(new Object[] { surface });

            Directory.CreateDirectory("Assets/Scenes");
            EditorSceneManager.SaveScene(scene, "Assets/Scenes/Game.unity");
            Selection.activeObject = director;
            Debug.Log($"Placeholder level built: {objects.Count} objects, {npcs.Count} NPCs, {feeds.Count} feeds.");
        }

        // ------------------------------------------------------------------ prototypes

        static void EnsureProtoAssets(out List<ObjectPrototype> protos)
        {
            Directory.CreateDirectory(ProtoFolder);
            protos = new List<ObjectPrototype>();

            foreach (var spec in Protos)
            {
                string path = $"{ProtoFolder}/Obj_{spec.Name.Replace(" ", "")}.asset";
                var proto = AssetDatabase.LoadAssetAtPath<ObjectPrototype>(path);
                if (proto == null)
                {
                    proto = ScriptableObject.CreateInstance<ObjectPrototype>();
                    AssetDatabase.CreateAsset(proto, path);
                }
                proto.DisplayName      = spec.Name;
                proto.Tags             = spec.Tags;
                proto.PlaceholderColor = spec.Color;
                proto.ResidueLine      = spec.Residue;
                proto.BlocksMovement   = true;
                EditorUtility.SetDirty(proto);
                protos.Add(proto);
            }
            AssetDatabase.SaveAssets();
        }

        // ------------------------------------------------------------------ tilemap

        static (Grid, Tilemap, List<Vector3>) BuildTilemaps()
        {
            var gridGo = new GameObject("Grid");
            var grid = gridGo.AddComponent<Grid>();
            grid.cellSize = Vector3.one;

            var floorTm = MakeTilemap(gridGo, "Tilemap_Floor", 0);
            var wallTm  = MakeTilemap(gridGo, "Tilemap_Walls", 1);

            var floorTile = MakeTile(new Color(0.16f, 0.18f, 0.17f));
            var wallTile  = MakeTile(new Color(0.34f, 0.36f, 0.40f));

            var floorCells = new List<Vector3>();
            int h = Map.Length;

            for (int row = 0; row < h; row++)
            {
                string line = Map[row];
                int y = h - 1 - row;                 // flip so y grows upward like the camera
                for (int x = 0; x < line.Length; x++)
                {
                    var pos = new Vector3Int(x, y, 0);
                    if (line[x] == '#')
                    {
                        wallTm.SetTile(pos, wallTile);
                    }
                    else
                    {
                        floorTm.SetTile(pos, floorTile);
                        floorCells.Add(new Vector3(x + 0.5f, y + 0.5f, 0f));
                    }
                }
            }

            // Walls become NavMesh obstacles via their collider plus a Not Walkable modifier.
            var col = wallTm.gameObject.AddComponent<TilemapCollider2D>();
            col.compositeOperation = Collider2D.CompositeOperation.Merge;
            var rb = wallTm.gameObject.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Static;
            wallTm.gameObject.AddComponent<CompositeCollider2D>().geometryType =
                CompositeCollider2D.GeometryType.Polygons;

            var mod = wallTm.gameObject.AddComponent<NavMeshModifier>();
            mod.overrideArea = true;
            mod.area = 1;                            // built-in "Not Walkable" area

            return (grid, wallTm, floorCells);
        }

        static Tilemap MakeTilemap(GameObject parent, string name, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            var tm = go.AddComponent<Tilemap>();
            var r = go.AddComponent<TilemapRenderer>();
            r.sortingOrder = order;
            return tm;
        }

        static Tile MakeTile(Color c)
        {
            var tex = new Texture2D(16, 16) { filterMode = FilterMode.Point };
            var px = new Color[16 * 16];
            for (int i = 0; i < px.Length; i++) px[i] = c;
            tex.SetPixels(px);
            tex.Apply();

            var tile = ScriptableObject.CreateInstance<Tile>();
            tile.sprite = Sprite.Create(tex, new Rect(0, 0, 16, 16), new Vector2(0.5f, 0.5f), 16f);
            return tile;
        }

        // ------------------------------------------------------------------ navmesh

        static NavMeshSurface BuildNavMeshSurface(Tilemap walls)
        {
            var go = new GameObject("NavMesh");
            // Face the surface toward a standard 2D camera so it bakes in the XY plane.
            go.transform.rotation = Quaternion.Euler(-90f, 0f, 0f);

            var surface = go.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.All;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            surface.defaultArea = 0;

            go.AddComponent<CollectSources2d>().compressBounds = true;
            return surface;
        }

        // ------------------------------------------------------------------ contents

        static List<WorldObject> PlaceObjects(List<ObjectPrototype> protos, List<Vector3> floorCells)
        {
            var root = new GameObject("Objects").transform;
            var used = new HashSet<int>();
            var result = new List<WorldObject>();

            for (int i = 0; i < protos.Count; i++)
            {
                Vector3 pos = TakeCell(floorCells, used);
                var go = new GameObject($"Obj_{protos[i].DisplayName}");
                go.transform.SetParent(root, false);
                go.transform.position = pos;

                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = MakeSquareSprite();
                sr.color = protos[i].PlaceholderColor;
                sr.sortingOrder = 5;
                go.transform.localScale = Vector3.one * 0.8f;

                var wo = go.AddComponent<WorldObject>();
                wo.Prototype = protos[i];
                result.Add(wo);
            }
            return result;
        }

        static List<Npc> PlaceNpcs(List<Vector3> floorCells)
        {
            var root = new GameObject("NPCs").transform;
            var used = new HashSet<int>();
            var result = new List<Npc>();

            for (int i = 0; i < 6; i++)
            {
                var go = new GameObject($"NPC_{Names[i]}");
                go.transform.SetParent(root, false);
                go.transform.position = TakeCell(floorCells, used);

                var body = new GameObject("Body");
                body.transform.SetParent(go.transform, false);
                var sr = body.AddComponent<SpriteRenderer>();
                sr.sprite = MakeSquareSprite();
                sr.color = Color.HSVToRGB(i / 6f, 0.55f, 0.95f);
                sr.sortingOrder = 10;
                body.transform.localScale = Vector3.one * 0.6f;

                var agent = go.AddComponent<NavMeshAgent>();
                agent.radius = 0.25f;
                agent.height = 0.5f;
                agent.speed = 2.2f;
                agent.acceleration = 20f;
                agent.angularSpeed = 0f;
                agent.stoppingDistance = 0.6f;
                agent.autoBraking = true;
                agent.updateRotation = false;
                agent.updateUpAxis = false;

                go.AddComponent<AgentOverride2d>();

                var npc = go.AddComponent<Npc>();
                npc.PersonName = Names[i];
                npc.Role = Roles[i];
                npc.Body = body.transform;
                result.Add(npc);
            }
            return result;
        }

        static List<Transform> PlaceSocialPoints()
        {
            var root = new GameObject("SocialPoints").transform;
            var result = new List<Transform>();
            int h = Map.Length;

            for (int row = 0; row < h; row++)
            {
                for (int x = 0; x < Map[row].Length; x++)
                {
                    if (Map[row][x] != 'o') continue;
                    var go = new GameObject($"Social_{result.Count}");
                    go.transform.SetParent(root, false);
                    go.transform.position = new Vector3(x + 0.5f, h - 1 - row + 0.5f, 0f);
                    result.Add(go.transform);
                }
            }
            return result;
        }

        static List<CameraFeed> BuildFeeds()
        {
            var root = new GameObject("Feeds").transform;
            var specs = new (string name, Rect rect)[]
            {
                ("CAM1 Kitchen", new Rect(0, 10, 11, 7)),
                ("CAM2 Lounge",  new Rect(11, 10, 13, 7)),
                ("CAM3 Server",  new Rect(0, 0, 24, 10)),
            };

            var result = new List<CameraFeed>();
            for (int i = 0; i < specs.Length; i++)
            {
                var go = new GameObject(specs[i].name);
                go.transform.SetParent(root, false);
                var feed = go.AddComponent<CameraFeed>();
                feed.FeedName = specs[i].name;
                feed.RoomId = i;
                feed.View = specs[i].rect;
                result.Add(feed);
            }
            return result;
        }

        static void BuildCamera()
        {
            var go = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = go.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 6f;
            cam.backgroundColor = new Color(0.05f, 0.06f, 0.06f);
            cam.clearFlags = CameraClearFlags.SolidColor;
            go.transform.position = new Vector3(12f, 8f, -10f);
            go.AddComponent<FeedCamera>();
        }

        static NightDirector BuildDirector(List<WorldObject> objects, List<Npc> npcs,
                                           List<Transform> social, List<CameraFeed> feeds)
        {
            var go = new GameObject("NightDirector");
            var d = go.AddComponent<NightDirector>();
            d.Objects = new List<WorldObject>(objects);
            foreach (var n in npcs) d.Objects.Add(n);      // NPCs are valid verb targets
            d.Npcs = npcs;
            d.SocialPoints = social;
            d.Feeds = feeds;
            return d;
        }

        static void BuildHud(NightDirector director)
        {
            var go = new GameObject("HUD");
            go.AddComponent<GameHud>().Director = director;
        }

        // ------------------------------------------------------------------ helpers

        static Vector3 TakeCell(List<Vector3> cells, HashSet<int> used)
        {
            for (int i = 0; i < 200; i++)
            {
                int idx = Random.Range(0, cells.Count);
                if (used.Add(idx)) return cells[idx];
            }
            return cells[0];
        }

        static Sprite _square;

        static Sprite MakeSquareSprite()
        {
            if (_square != null) return _square;
            var tex = new Texture2D(8, 8) { filterMode = FilterMode.Point };
            var px = new Color[64];
            for (int i = 0; i < 64; i++) px[i] = Color.white;
            tex.SetPixels(px);
            tex.Apply();
            _square = Sprite.Create(tex, new Rect(0, 0, 8, 8), new Vector2(0.5f, 0.5f), 8f);
            return _square;
        }
    }
}
