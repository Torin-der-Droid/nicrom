using UnityEngine;

namespace CubeDungeon
{
    public enum BlockKind { Floor, Wall, Ceiling }

    [DisallowMultipleComponent]
    public sealed class CubeBlock : MonoBehaviour
    {
        public BlockKind kind;
    }
}
