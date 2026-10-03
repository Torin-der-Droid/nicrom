using UnityEngine;
using UnityEngine.AI;

namespace NicromFPS
{
    // Marks finished building blocks as snapping targets, separate from loose resource cubes.
    public sealed class PlacedCube : MonoBehaviour
    {
        private void Awake()
        {
            // Finished wall blocks must also block enemy navigation after the initial bake.
            var obstacle = gameObject.AddComponent<NavMeshObstacle>();
            obstacle.shape = NavMeshObstacleShape.Box;
            obstacle.size = Vector3.one;
            obstacle.carving = true;
            obstacle.carveOnlyStationary = true;
        }
    }
}
