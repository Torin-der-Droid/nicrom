using Akila.FPSFramework;
using UnityEngine;
using UnityEngine.AI;
namespace NicromFPS
{
    public static class GoblinFactory
    {
        public static GoblinEnemy Spawn(Transform parent, Vector3 position, int number, GameObject modelPrefab,
            RuntimeAnimatorController animationController, Material goblinMaterial, AudioClip goblinCackle,
            float health, float attackDamage, float detectionRange, float moveSpeed, Transform target,
            bool dungeon, int agentType)
        {
            var root = new GameObject("Feind Goblin " + number);
            root.SetActive(false);
            root.transform.SetParent(parent, false);
            root.transform.position = position;
            GameObject model = Object.Instantiate(modelPrefab, root.transform);
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;
            Animator animator = model.GetComponentInChildren<Animator>();
            animator.runtimeAnimatorController = animationController;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            foreach (Renderer renderer in model.GetComponentsInChildren<Renderer>())
            {
                Material[] materials = renderer.sharedMaterials;
                for (int i = 0; i < materials.Length; i++) materials[i] = goblinMaterial;
                renderer.sharedMaterials = materials;
            }
            var collider = root.AddComponent<CapsuleCollider>();
            collider.height = 1.4f;
            collider.radius = 0.32f;
            collider.center = Vector3.up * 0.7f;
            var agent = root.AddComponent<NavMeshAgent>();
            agent.height = 1.4f;
            agent.baseOffset = 0;
            agent.radius = 0.32f;
            agent.speed = moveSpeed;
            agent.angularSpeed = 360;
            agent.acceleration = 12;
            agent.stoppingDistance = 1.45f;
            var enemy = root.AddComponent<GoblinEnemy>();
            var actor = root.AddComponent<Actor>();
            actor.actorName = "Goblin " + number;
            actor.type = "Goblin";
            actor.respawnable = false;
            actor.playerCardActive = false;
            actor.playerUIEnabled = false;
            agent.agentTypeID = agentType;
            root.SetActive(true);
            enemy.Initialize(animator, agent, collider, health, attackDamage, detectionRange);
            enemy.HuntPlayer = true;
            enemy.DungeonResident = dungeon;
            enemy.SetTarget(target);
            root.AddComponent<GoblinMinimapMarker>();
            if (goblinCackle) root.AddComponent<GoblinVoice>().Initialize(goblinCackle);
            return enemy;
        }
    }
}
