using UnityEngine;

namespace CubeDungeon
{
    [CreateAssetMenu(menuName = "Cube Dungeon/Bauteile")]
    public sealed class CubeDungeonKit : ScriptableObject
    {
        public GameObject room, hall, ramp, stairSpace, wall, doorway, pillar, arch;
        public Material[] stonePalette;

        public GameObject[] Pieces => new[] { room, hall, ramp, stairSpace, wall, doorway, pillar, arch };

        public bool IsValid
        {
            get
            {
                foreach (var piece in Pieces) if (piece == null) return false;
                if (stonePalette == null || stonePalette.Length == 0) return false;
                foreach (var material in stonePalette) if (material == null) return false;
                return true;
            }
        }
    }
}
