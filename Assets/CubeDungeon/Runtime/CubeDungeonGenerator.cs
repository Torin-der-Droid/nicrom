using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

namespace CubeDungeon
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Cube Dungeon/Cube Dungeon Generator")]
    public sealed class CubeDungeonGenerator : MonoBehaviour
    {
        public CubeDungeonKit kit;
        public int seed = 42;
        public Vector3Int gridSize = new Vector3Int(20, 10, 20);
        [Min(2)] public int roomCount = 10;
        public Vector2Int minimumRoomSize = new Vector2Int(2, 2);
        public Vector2Int maximumRoomSize = new Vector2Int(4, 4);
        [Range(0, 1)] public float extraConnections = .5f;
        public bool showCeilings = true;
        public bool generateOnStart = true;

        [SerializeField, HideInInspector] Transform generatedRoot;
        [SerializeField, HideInInspector] DungeonGenerator engine;
        [SerializeField, HideInInspector] int blockCount, rampCount, generatedRoomCount, failedConnections;
        [SerializeField, HideInInspector] string lastMessage;

        public Transform GeneratedRoot => generatedRoot;
        public int BlockCount => blockCount;
        public int RampCount => rampCount;
        public int GeneratedRoomCount => generatedRoomCount;
        public int FailedConnections => failedConnections;
        public string LastMessage => lastMessage;
        public bool IsGenerating { get; private set; }
        public event Action<bool> CeilingVisibilityChanged;

        void Start()
        {
            if (generateOnStart) Generate();
        }

        public bool ValidateSettings(out string error)
        {
            if (kit == null) kit = Resources.Load<CubeDungeonKit>("CubeDungeonKit");
            error = null;
            if (kit == null || !kit.IsValid)
                error = "Cube-Dungeon-Bauteile fehlen. Tools > Cube Dungeon > Bauteile erstellen ausführen.";
            else if (gridSize.x < 4 || gridSize.z < 4 || gridSize.y < 1 ||
                gridSize.x > 64 || gridSize.z > 64 || gridSize.y > 16)
                error = "Raster: X/Z zwischen 4 und 64, Ebenen zwischen 1 und 16 einstellen.";
            else if (roomCount < 2 || roomCount > 100)
                error = "Die Raumanzahl muss zwischen 2 und 100 liegen.";
            else if (minimumRoomSize.x < 1 || minimumRoomSize.y < 1 ||
                maximumRoomSize.x < minimumRoomSize.x || maximumRoomSize.y < minimumRoomSize.y ||
                maximumRoomSize.x > gridSize.x || maximumRoomSize.y > gridSize.z)
                error = "Raumgrößen müssen positiv, aufsteigend und kleiner als das Raster sein.";
            else if (float.IsNaN(extraConnections) || extraConnections < 0 || extraConnections > 1)
                error = "Zusätzliche Verbindungen müssen zwischen 0 und 1 liegen.";
            else if (transform.lossyScale != Vector3.one)
                error = "Der Generator und seine Eltern müssen Skalierung (1, 1, 1) haben, damit Cubes einen Meter groß bleiben.";
            return error == null;
        }

        [ContextMenu("Generieren")]
        public void Generate()
        {
            if (IsGenerating) return;
            if (!ValidateSettings(out var error))
            {
                lastMessage = error;
                Debug.LogWarning(error, this);
                return;
            }

            IsGenerating = true;
            var randomState = Random.state;
            Transform pending = null;
            try
            {
                Random.InitState(seed);
                if (engine == null)
                {
                    var engineObject = new GameObject("Daedalus Layout");
                    engineObject.transform.SetParent(transform, false);
                    engine = engineObject.AddComponent<DungeonGenerator>();
                    engine.enabled = false;
                }

                // Daedalus produces world coordinates. Build at the origin, then attach
                // the finished result in generator-local space, preserving moves/rotations.
                pending = new GameObject("Cube Dungeon").transform;
                var rooms = NewGroup("Räume", pending);
                var paths = NewGroup("Gänge und Rampen", pending);
                engine.Configure(pending, rooms, paths, kit.Pieces, gridSize, roomCount,
                    new Vector3Int(minimumRoomSize.x, 1, minimumRoomSize.y),
                    new Vector3Int(maximumRoomSize.x, 1, maximumRoomSize.y), extraConnections);
                engine.GenerateImmediate();
                FinishBlocks(pending);
                var previous = generatedRoot;
                generatedRoot = pending;
                generatedRoot.SetParent(transform, false);
                pending = null;
                if (previous != null) Remove(previous.gameObject);
                generatedRoomCount = engine.GetRooms().Count;
                failedConnections = engine.FailedConnections;
                rampCount = generatedRoot.GetComponentsInChildren<MeshCollider>().Length;
                SetCeilingsVisible(showCeilings);
                lastMessage = $"Seed {seed}: {generatedRoomCount} Räume, {blockCount:N0} Cubes, {rampCount} Rampen.";
                if (failedConnections > 0)
                    lastMessage += $" {failedConnections} Gangverbindungen konnten nicht erzeugt werden. Anderen Seed oder mehr Platz wählen.";
                if (generatedRoomCount < roomCount)
                    lastMessage += $" Für {roomCount - generatedRoomCount} Räume war kein Platz vorhanden.";
                Debug.Log(lastMessage, this);
            }
            catch (Exception exception)
            {
                lastMessage = "Erzeugung fehlgeschlagen: " + exception.Message;
                Debug.LogException(exception, this);
            }
            finally
            {
                if (pending != null) Remove(pending.gameObject);
                Random.state = randomState;
                IsGenerating = false;
            }
        }

        void FinishBlocks(Transform output)
        {
            var occupied = new Dictionary<Vector3Int, CubeBlock>();
            foreach (var block in output.GetComponentsInChildren<CubeBlock>())
            {
                var key = Vector3Int.RoundToInt(block.transform.position);
                if ((block.transform.position - (Vector3)key).sqrMagnitude > .0001f)
                    throw new InvalidOperationException("Ein Bauteil liegt außerhalb des Cube-Rasters.");
                if (occupied.TryGetValue(key, out var existing))
                {
                    // A floor shared with the ceiling below must stay visible when
                    // ceiling renderers are hidden for editing.
                    if (block.kind < existing.kind)
                    {
                        occupied[key] = block;
                        Remove(existing.gameObject);
                    }
                    else Remove(block.gameObject);
                    continue;
                }
                occupied.Add(key, block);
            }
            CloseWallSeams(output, occupied);
            // One-meter walls extend farther into a stairwell than Daedalus'
            // thin meshes. Cut a three-meter-wide, three-meter-high opening
            // following each ramp, including the upper stair-space walls.
            foreach (var ramp in output.GetComponentsInChildren<MeshCollider>())
            {
                var removed = new List<Vector3Int>();
                foreach (var pair in occupied)
                {
                    var p = ramp.transform.parent.InverseTransformPoint(pair.Value.transform.position);
                    const float epsilon = .0001f;
                    float lowZ = Mathf.Max(-.5f, p.z - .1f);
                    float highZ = Mathf.Min(.5f, p.z + .1f);
                    if (p.x - .1f < .3f - epsilon && p.x + .1f > -.3f + epsilon &&
                        highZ > lowZ + epsilon && p.y + .1f > lowZ + epsilon &&
                        p.y - .1f < highZ + .6f - epsilon)
                        removed.Add(pair.Key);
                }
                foreach (var key in removed)
                {
                    Remove(occupied[key].gameObject);
                    occupied.Remove(key);
                }
            }
            blockCount = occupied.Count;
            foreach (var pair in occupied)
            {
                var block = pair.Value;
                var p = pair.Key;
                // Stable variation independent of Unity.Random and traversal order.
                uint hash = unchecked((uint)(p.x * 73856093 ^ p.y * 19349663 ^ p.z * 83492791 ^ seed));
                block.GetComponent<Renderer>().sharedMaterial = kit.stonePalette[hash % kit.stonePalette.Length];
                block.name = $"{block.kind} [{p.x}, {p.y}, {p.z}]";
            }
        }

        static void CloseWallSeams(Transform output, Dictionary<Vector3Int, CubeBlock> occupied)
        {
            // Walls are four cubes high. In rooms the floor/ceiling slab fills
            // the fifth row between floors. Stairwells omit that slab, leaving
            // a one-cube slit between the lower and upper wall sections.
            var missing = new List<Vector3Int>();
            foreach (var pair in occupied)
            {
                if (pair.Value.kind != BlockKind.Wall) continue;
                var middle = pair.Key + Vector3Int.up;
                if (!occupied.TryGetValue(middle + Vector3Int.up, out var upper) ||
                    upper.kind != BlockKind.Wall) continue;

                if (!occupied.TryGetValue(middle, out var existing))
                    missing.Add(middle);
                else if (existing.kind == BlockKind.Ceiling)
                    // This cube also closes a wall, so hiding the ceiling must
                    // not turn it into a window between two wall sections.
                    existing.kind = BlockKind.Wall;
            }

            if (missing.Count == 0) return;
            var joins = NewGroup("Wandanschlüsse", output);
            foreach (var position in missing)
            {
                var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                cube.transform.SetParent(joins, false);
                cube.transform.position = position;
                var block = cube.AddComponent<CubeBlock>();
                block.kind = BlockKind.Wall;
                occupied.Add(position, block);
            }
            // Only existing wall columns are joined, keeping openings between
            // parallel ramps intact. The ramp clearance pass runs afterwards.
        }

        public void SetCeilingsVisible(bool visible)
        {
            showCeilings = visible;
            if (generatedRoot == null) return;
            foreach (var block in generatedRoot.GetComponentsInChildren<CubeBlock>())
                if (block.kind == BlockKind.Ceiling) block.GetComponent<Renderer>().enabled = visible;
            CeilingVisibilityChanged?.Invoke(visible);
        }

        public bool TryGetPlayerSpawnPosition(out Vector3 position)
        {
            position = default;
            if (generatedRoot == null) return false;

            // Room and floor coordinates share the same cell center. The room
            // floor cube is centered 3 m below it and is 1 m thick, so its top
            // surface is exactly 2.5 m below the center. Compute this directly:
            // physics queries can miss while Unity is still syncing new cubes.
            if (engine != null && engine.GetRooms().Count > 0)
            {
                var room = engine.GetRooms()[0];
                if (room.cells != null && room.cells.Count > 0)
                {
                    position = transform.TransformPoint(room.center + Vector3.down * 2.5f) + Vector3.up * .04f;
                    return true;
                }
            }

            // The layout data can be unavailable when a scene was saved or
            // restored separately from its generator. In that case, choose the
            // floor cube with the most neighboring floor tiles, which puts the
            // player in the interior of a room instead of at an exposed edge.
            var floorTiles = new HashSet<Vector3Int>();
            foreach (var block in generatedRoot.GetComponentsInChildren<CubeBlock>())
                if (block.kind == BlockKind.Floor)
                    floorTiles.Add(Vector3Int.RoundToInt(generatedRoot.InverseTransformPoint(block.transform.position)));

            var bestTile = default(Vector3Int);
            var bestScore = 0;
            foreach (var tile in floorTiles)
            {
                var score = 0;
                for (var x = -1; x <= 1; x++)
                    for (var z = -1; z <= 1; z++)
                        if (floorTiles.Contains(tile + new Vector3Int(x, 0, z))) score++;
                if (score <= bestScore) continue;
                bestScore = score;
                bestTile = tile;
            }

            if (bestScore == 0) return false;
            position = generatedRoot.TransformPoint((Vector3)bestTile + Vector3.up * .54f);
            return true;
        }

        [ContextMenu("Neuer Seed")]
        public void GenerateNewSeed()
        {
            seed = Guid.NewGuid().GetHashCode();
            Generate();
        }

        [ContextMenu("Dungeon löschen")]
        public void Clear()
        {
            if (generatedRoot != null) Remove(generatedRoot.gameObject);
            generatedRoot = null;
            blockCount = rampCount = generatedRoomCount = failedConnections = 0;
            lastMessage = "Dungeon gelöscht.";
        }

        static Transform NewGroup(string name, Transform parent)
        {
            var group = new GameObject(name).transform;
            group.SetParent(parent, false);
            return group;
        }

        static void Remove(GameObject target)
        {
            target.SetActive(false);
            if (Application.isPlaying) Destroy(target);
            else DestroyImmediate(target);
        }
    }
}
