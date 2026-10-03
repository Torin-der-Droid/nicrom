using UnityEngine;

namespace NicromFPS
{
    [RequireComponent(typeof(BoxCollider), typeof(Rigidbody))]
    public sealed class DungeonGate : MonoBehaviour
    {
        public bool returnsToWorld;
        public bool advancesDungeon;
        private DungeonTravelManager manager;
        private bool occupied;

        private void Awake() => Initialize(DungeonTravelManager.Instance, returnsToWorld);

        public void Initialize(DungeonTravelManager owner, bool isReturn)
        {
            manager = owner;
            returnsToWorld = isReturn;
            advancesDungeon = false;
            var trigger = GetComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.center = Vector3.up * 1.4f;
            trigger.size = new Vector3(2, 2.8f, 0.7f);
            var rigid = GetComponent<Rigidbody>();
            rigid.isKinematic = true;
            rigid.useGravity = false;
        }

        public void InitializeAdvance(DungeonTravelManager owner)
        {
            Initialize(owner, false);
            advancesDungeon = true;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (occupied || !other.GetComponent<NicromTerrainSpawn>()) return;
            occupied = true;
            if (!manager) manager = DungeonTravelManager.Instance;
            if (manager)
            {
                if (advancesDungeon) manager.AdvanceDungeon(other.gameObject);
                else manager.Travel(other.gameObject, returnsToWorld);
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (other.GetComponent<NicromTerrainSpawn>()) occupied = false;
        }

        public void ResetOccupancy() => occupied = false;
    }
}
