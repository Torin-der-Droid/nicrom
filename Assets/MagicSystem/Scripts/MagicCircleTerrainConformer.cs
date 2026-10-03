using System.Collections.Generic;
using Akila.FPSFramework;
using UnityEngine;

namespace MagicSystem
{
    /// <summary>Subdivides every flat circle layer and projects its vertices onto uneven ground.</summary>
    public sealed class MagicCircleTerrainConformer : MonoBehaviour
    {
        const int Resolution = 24;
        const float SurfaceOffset = .035f;
        const float ProbeHeight = 2.5f;
        const float ProbeDistance = 7f;
        const float MinimumUpNormal = .35f;
        static readonly int GroundMask = Physics.DefaultRaycastLayers & ~(1 << 4);
        readonly List<Mesh> generatedMeshes = new List<Mesh>();

        public void Conform(Transform ignoredRoot)
        {
            foreach (MeshFilter filter in GetComponentsInChildren<MeshFilter>(true))
            {
                Mesh source = filter.sharedMesh;
                if (!source) continue;
                Bounds bounds = source.bounds;
                if (bounds.size.x < .1f || bounds.size.y < .1f || bounds.size.z > .05f) continue;
                Mesh projected = CreateGrid(filter, bounds, ignoredRoot);
                generatedMeshes.Add(projected);
                filter.sharedMesh = projected;
            }
        }

        Mesh CreateGrid(MeshFilter filter, Bounds bounds, Transform ignoredRoot)
        {
            int side = Resolution + 1;
            var vertices = new Vector3[side * side];
            var uv = new Vector2[vertices.Length];
            var triangles = new int[Resolution * Resolution * 6];
            int vertex = 0;
            for (int y = 0; y <= Resolution; y++)
            {
                float v = y / (float)Resolution;
                for (int x = 0; x <= Resolution; x++, vertex++)
                {
                    float u = x / (float)Resolution;
                    Vector3 local = new Vector3(Mathf.Lerp(bounds.min.x, bounds.max.x, u),
                        Mathf.Lerp(bounds.min.y, bounds.max.y, v), bounds.center.z);
                    Vector3 world = filter.transform.TransformPoint(local);
                    if (TryProject(world, ignoredRoot, out Vector3 ground))
                        world.y = ground.y + SurfaceOffset;
                    vertices[vertex] = filter.transform.InverseTransformPoint(world);
                    uv[vertex] = new Vector2(u, v);
                }
            }
            int triangle = 0;
            for (int y = 0; y < Resolution; y++)
            for (int x = 0; x < Resolution; x++)
            {
                int a = y * side + x;
                int b = a + 1;
                int c = a + side;
                int d = c + 1;
                triangles[triangle++] = a; triangles[triangle++] = c; triangles[triangle++] = b;
                triangles[triangle++] = b; triangles[triangle++] = c; triangles[triangle++] = d;
            }
            var mesh = new Mesh { name = "Terrain Conforming Magic Circle" };
            mesh.vertices = vertices;
            mesh.uv = uv;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        public static bool TryFindGround(Vector3 near, Transform ignoredRoot, out Vector3 ground)
            => TryProject(near, ignoredRoot, out ground);

        static bool TryProject(Vector3 near, Transform ignoredRoot, out Vector3 ground)
        {
            Vector3 origin = near + Vector3.up * ProbeHeight;
            RaycastHit[] hits = Physics.RaycastAll(origin, Vector3.down, ProbeDistance, GroundMask,
                QueryTriggerInteraction.Ignore);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            foreach (RaycastHit hit in hits)
            {
                if (ignoredRoot && hit.transform.IsChildOf(ignoredRoot)) continue;
                if (hit.normal.y < MinimumUpNormal || HasDamageable(hit.collider)) continue;
                ground = hit.point;
                return true;
            }
            ground = default;
            return false;
        }

        static bool HasDamageable(Collider collider)
        {
            foreach (MonoBehaviour component in collider.GetComponentsInParent<MonoBehaviour>(true))
                if (component is IDamageable) return true;
            return false;
        }

        void OnDestroy()
        {
            foreach (Mesh mesh in generatedMeshes) if (mesh) Destroy(mesh);
        }
    }
}
