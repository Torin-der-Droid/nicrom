using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace CubeDungeon
{
    // Combine visuals in small spatial groups. Original blocks and colliders remain
    // available for gameplay, while Unity culls each combined group independently.
    [ExecuteAlways]
    public sealed class CubeDungeonRenderBatches : MonoBehaviour
    {
        private readonly List<Mesh> meshes = new List<Mesh>();
        private readonly List<Renderer> ceilingBatches = new List<Renderer>();
        private readonly List<(Renderer renderer, bool ceiling)> originals = new List<(Renderer, bool)>();
        private CubeDungeonGenerator generator;
        public int SourceRendererCount => originals.Count;
        public int BatchCount => meshes.Count;

        public void Build(CubeDungeonGenerator owner)
        {
            if (generator || !owner || !owner.GeneratedRoot) return;
            generator = owner;
            var groups = new Dictionary<(Material, Vector3Int, bool), List<(MeshFilter, Renderer)>>();
            foreach (var block in owner.GeneratedRoot.GetComponentsInChildren<CubeBlock>())
            {
                var filter = block.GetComponent<MeshFilter>();
                var renderer = block.GetComponent<MeshRenderer>();
                if (!filter || !filter.sharedMesh || !filter.sharedMesh.isReadable || !renderer
                    || !renderer.sharedMaterial || filter.sharedMesh.subMeshCount != 1) continue;
                Vector3 p = transform.InverseTransformPoint(filter.transform.position);
                var cell = new Vector3Int(Mathf.FloorToInt(p.x / 12), Mathf.FloorToInt(p.y / 6), Mathf.FloorToInt(p.z / 12));
                var key = (renderer.sharedMaterial, cell, block.kind == BlockKind.Ceiling);
                if (!groups.TryGetValue(key, out var entries)) groups.Add(key, entries = new List<(MeshFilter, Renderer)>());
                entries.Add((filter, renderer));
            }
            foreach (var pair in groups)
            {
                if (pair.Value.Count < 2) continue;
                var instances = new CombineInstance[pair.Value.Count];
                for (int i = 0; i < instances.Length; i++)
                    instances[i] = new CombineInstance
                    {
                        mesh = pair.Value[i].Item1.sharedMesh,
                        transform = transform.worldToLocalMatrix * pair.Value[i].Item1.transform.localToWorldMatrix
                    };
                var mesh = new Mesh { name = "Dungeon render chunk", indexFormat = IndexFormat.UInt32 };
                mesh.CombineMeshes(instances, true, true);
                meshes.Add(mesh);
                var chunk = new GameObject("Cube render chunk", typeof(MeshFilter), typeof(MeshRenderer));
                chunk.transform.SetParent(transform, false);
                chunk.layer = pair.Value[0].Item2.gameObject.layer;
                chunk.GetComponent<MeshFilter>().sharedMesh = mesh;
                var combined = chunk.GetComponent<MeshRenderer>();
                combined.sharedMaterial = pair.Key.Item1;
                combined.shadowCastingMode = pair.Value[0].Item2.shadowCastingMode;
                combined.receiveShadows = pair.Value[0].Item2.receiveShadows;
                if (pair.Key.Item3) ceilingBatches.Add(combined);
                foreach (var entry in pair.Value)
                {
                    originals.Add((entry.Item2, pair.Key.Item3));
                    entry.Item2.enabled = false;
                }
            }
            owner.CeilingVisibilityChanged += SetCeilingsVisible;
            SetCeilingsVisible(owner.showCeilings);
            Debug.Log($"CubeDungeon rendering: {SourceRendererCount} cube renderers combined into {BatchCount} spatial groups.", this);
        }

        private void SetCeilingsVisible(bool visible)
        {
            foreach (var renderer in ceilingBatches) if (renderer) renderer.enabled = visible;
            foreach (var entry in originals) if (entry.ceiling && entry.renderer) entry.renderer.enabled = false;
        }

        private void OnDestroy()
        {
            if (generator) generator.CeilingVisibilityChanged -= SetCeilingsVisible;
            foreach (var mesh in meshes)
                if (mesh) { if (Application.isPlaying) Destroy(mesh); else DestroyImmediate(mesh); }
        }
    }
}
