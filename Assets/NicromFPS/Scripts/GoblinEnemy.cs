using Akila.FPSFramework;
using TMPro;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Events;
using UnityEngine.UI;

namespace NicromFPS
{
    public sealed class GoblinEnemy : MonoBehaviour, IDamageable
    {
        private EnemyHealth health;
        private Animator animator;
        private NavMeshAgent agent;
        private CapsuleCollider body;
        private CharacterController playerBody;
        private Transform player;
        private IDamageable playerHealth;
        private Camera playerCamera;
        private Canvas healthCanvas;
        private Image healthFill;
        private TMP_Text healthText;
        private float attackDamage, detectionRange, nextSearch, nextPath, attackEnd, hitTime;
        private bool attacking, hitApplied, dying, alerted;
        private string currentAnimation;
        private float modelSoleHeight;
        private float nextPlayerDamage;
        private const float DamageInterval = 1f;
        private readonly UnityEvent death = new UnityEvent();
        public bool isDamagableDisabled { get; set; }
        public bool allowDamageableEffects { get; set; } = true;
        public bool DeadConfirmed { get; set; }
        public GameObject DamageSource { get; set; }
        public UnityEvent OnDeath => death;
        public bool DungeonResident { get; set; }
        public int DungeonLevel { get; set; } = 1;
        public bool EncounterPaused => DungeonResident
            ? !DungeonTravelManager.Instance || !DungeonTravelManager.Instance.IsDungeonActive(DungeonLevel)
                || DungeonTravelManager.Instance.IsTransitioning
            : DungeonTravelManager.OutsidePaused;
        public bool Paused => EncounterPaused || FPSFrameworkCore.IsPaused || !FPSFrameworkCore.IsActive;
        public void SetTarget(Transform target)
        {
            player = target;
            playerHealth = target ? target.GetComponent<IDamageable>() : null;
            playerBody = target ? target.GetComponent<CharacterController>() : null;
            playerCamera = null;
            if (target) foreach (var camera in target.GetComponentsInChildren<Camera>())
                if (camera.CompareTag("MainCamera")) playerCamera = camera;
        }
        private NavMeshQueryFilter NavigationFilter => new NavMeshQueryFilter { agentTypeID = agent.agentTypeID, areaMask = agent.areaMask };
        public bool HuntPlayer { get; set; }
        public bool IsDead => dying;
        public event System.Action Defeated;
        public float Health
        {
            get => health?.Current ?? 0;
            set { health?.Set(value); if (health != null && health.IsDead) Die(); }
        }

        public void Initialize(Animator animation, NavMeshAgent navigation, CapsuleCollider collider,
            float maximumHealth, float damage, float sightRange)
        {
            animator = animation;
            agent = navigation;
            body = collider;
            health = new EnemyHealth(maximumHealth);
            attackDamage = damage;
            detectionRange = sightRange;
            animator.Rebind();
            animator.Play("Idle01", 0, 0);
            animator.Update(0);
            MeasureModelSole();
            AlignModelToGround(true);
            CreateHealthBar();
            Play("Idle01");
        }

        public void Damage(float amount, GameObject source)
        {
            if (Paused || isDamagableDisabled || health == null || !health.Damage(amount)) return;
            DamageSource = source;
            alerted = true;
            if (health.IsDead) Die();
        }

        private void Update()
        {
            if (health == null) return;
            if (dying) { animator.speed = Paused ? 0 : 1; return; }
            if (Paused)
            {
                StopMoving();
                animator.speed = 0;
                // Freeze the current swing even if a menu pauses input without stopping game time.
                if (attacking) { attackEnd += Time.deltaTime; hitTime += Time.deltaTime; }
                nextPlayerDamage += Time.deltaTime;
                return;
            }
            animator.speed = 1;
            if ((!player || playerHealth == null || playerHealth.Health <= 0) && Time.time >= nextSearch)
            {
                if (!DungeonResident) FindPlayer();
                nextSearch = Time.time + 0.5f;
            }
            if (!player || playerHealth == null || playerHealth.Health <= 0)
            {
                attacking = false;
                StopMoving();
                Play("Idle01");
                return;
            }
            Vector3 toPlayer = player.position - transform.position;
            float distance = Vector3.ProjectOnPlane(toPlayer, Vector3.up).magnitude;
            bool visible = (!HuntPlayer || distance < 2) && CanSeePlayer();
            // CharacterController/NavMeshAgent contacts do not reliably send collision events.
            // Check their upright capsules directly, including the controller's contact skin.
            if (visible && TouchesPlayer()) TryDamagePlayer();
            if (playerHealth.Health <= 0) { StopMoving(); return; }
            if (attacking)
            {
                FacePlayer();
                if (!hitApplied && Time.time >= hitTime)
                {
                    hitApplied = true;
                    if (distance < 1.9f && Mathf.Abs(toPlayer.y) < 2 && visible)
                        TryDamagePlayer();
                }
                if (Time.time >= attackEnd) attacking = false;
                return;
            }
            if (distance <= 1.65f && visible)
            {
                StopMoving();
                FacePlayer();
                attacking = true;
                hitApplied = false;
                hitTime = Time.time + 0.6f;
                attackEnd = Time.time + 1.5f;
                Play("Attack01", true);
                return;
            }
            if (HuntPlayer || ((alerted || distance <= detectionRange) && distance < 45 && (visible || alerted)))
            {
                alerted = true;
                if (!agent.isOnNavMesh && NavMesh.SamplePosition(transform.position, out NavMeshHit recovery, 2, NavigationFilter))
                    agent.Warp(recovery.position);
                if (agent.isOnNavMesh)
                {
                    agent.isStopped = false;
                    if (Time.time >= nextPath)
                    {
                        nextPath = Time.time + 0.3f;
                        if (NavMesh.SamplePosition(player.position, out NavMeshHit destination, 3, NavigationFilter))
                            agent.SetDestination(destination.position);
                    }
                }
                Play(agent.velocity.sqrMagnitude > 0.02f ? "Move01" : "Idle01");
            }
            else
            {
                alerted = false;
                StopMoving();
                Play("Idle01");
            }
        }

        private void TryDamagePlayer()
        {
            if (Time.time < nextPlayerDamage || playerHealth.isDamagableDisabled || playerHealth.Health <= 0) return;
            nextPlayerDamage = Time.time + DamageInterval;
            playerHealth.Damage(attackDamage, gameObject);
        }

        private bool TouchesPlayer()
        {
            if (!body || !body.enabled || !playerBody || !playerBody.enabled) return false;
            Vector3 a = body.transform.TransformPoint(body.center);
            Vector3 b = playerBody.transform.TransformPoint(playerBody.center);
            Vector3 scaleA = body.transform.lossyScale;
            Vector3 scaleB = playerBody.transform.lossyScale;
            float radiusA = body.radius * Mathf.Max(Mathf.Abs(scaleA.x), Mathf.Abs(scaleA.z));
            float radiusB = playerBody.radius * Mathf.Max(Mathf.Abs(scaleB.x), Mathf.Abs(scaleB.z));
            float segmentA = Mathf.Max(0, body.height * Mathf.Abs(scaleA.y) * 0.5f - radiusA);
            float segmentB = Mathf.Max(0, playerBody.height * Mathf.Abs(scaleB.y) * 0.5f - radiusB);
            float verticalGap = Mathf.Max(0, Mathf.Abs(a.y - b.y) - segmentA - segmentB);
            float reach = radiusA + radiusB + Mathf.Max(0.02f, playerBody.skinWidth);
            return (a.x - b.x) * (a.x - b.x) + (a.z - b.z) * (a.z - b.z)
                + verticalGap * verticalGap <= reach * reach;
        }

        private void FindPlayer()
        {
            player = null;
            playerHealth = null;
            playerBody = null;
            foreach (NicromTerrainSpawn spawn in FindObjectsByType<NicromTerrainSpawn>())
            {
                if (spawn.gameObject.scene != gameObject.scene) continue;
                var damageable = spawn.GetComponent<IDamageable>();
                if (damageable == null || damageable.Health <= 0) continue;
                player = spawn.transform;
                playerHealth = damageable;
                playerBody = spawn.GetComponent<CharacterController>();
                foreach (Camera camera in spawn.GetComponentsInChildren<Camera>())
                    if (camera.CompareTag("MainCamera")) playerCamera = camera;
                break;
            }
        }

        private bool CanSeePlayer()
        {
            Vector3 center = playerBody ? playerBody.bounds.center : player.position;
            float headOffset = playerBody ? playerBody.bounds.extents.y * 0.65f : 0.5f;
            Vector3 origin = transform.position + Vector3.up * 1.2f;
            return HasClearSight(origin, center) || HasClearSight(origin, center + Vector3.up * headOffset);
        }

        private bool HasClearSight(Vector3 origin, Vector3 destination)
        {
            Vector3 delta = destination - origin;
            // Test only for obstructions before the target. Seeing the player must not depend
            // on a ray hitting a particular controller collider or geometry behind the player.
            foreach (RaycastHit hit in Physics.RaycastAll(origin, delta.normalized, delta.magnitude,
                Physics.DefaultRaycastLayers & ~(1 << 12), QueryTriggerInteraction.Ignore))
            {
                if (hit.collider.transform.IsChildOf(transform) || hit.collider.transform.IsChildOf(player)) continue;
                return false;
            }
            return true;
        }

        private void StopMoving()
        {
            if (agent && agent.enabled && agent.isOnNavMesh) agent.isStopped = true;
        }

        private void FacePlayer()
        {
            Vector3 direction = Vector3.ProjectOnPlane(player.position - transform.position, Vector3.up);
            if (direction.sqrMagnitude > 0.001f)
                transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(direction), 480 * Time.deltaTime);
        }

        private void Play(string state, bool restart = false)
        {
            if (!restart && currentAnimation == state) return;
            currentAnimation = state;
            animator.CrossFadeInFixedTime(state, 0.1f, 0, 0);
        }

        private void Die()
        {
            if (dying) return;
            dying = true;
            attacking = false;
            StopMoving();
            if (agent) agent.enabled = false;
            foreach (Collider hitbox in GetComponentsInChildren<Collider>()) hitbox.enabled = false;
            // Find the floor only after the corpse colliders are disabled, otherwise
            // mana can mistake the goblin itself for the ground.
            ManaOrb.Drop(transform.position, health.Maximum);
            animator.speed = 1;
            Play("Die01");
            if (healthCanvas) healthCanvas.gameObject.SetActive(false);
            // Actor records the player's kill before marking death confirmed.
            if (DamageSource) death.Invoke();
            DeadConfirmed = true;
            Defeated?.Invoke();
            Destroy(gameObject, 15);
        }

        private void LateUpdate()
        {
            if (!dying && animator) AlignModelToGround(false);
            if (dying || !healthCanvas) return;
            bool show = !EncounterPaused && playerCamera && Vector3.Distance(transform.position, playerCamera.transform.position) < 25;
            healthCanvas.gameObject.SetActive(show);
            if (!show) return;
            healthCanvas.transform.rotation = playerCamera.transform.rotation;
            healthFill.rectTransform.sizeDelta = new Vector2(140 * health.Current / health.Maximum, 10);
            healthText.text = $"Goblin  {Mathf.CeilToInt(health.Current)}/{health.Maximum:0}";
        }

        private void MeasureModelSole()
        {
            // Measure the actual foot geometry once in the idle pose. Do not use the renderer
            // bounds (they include every animation) or re-measure a raised foot during walking.
            foreach (SkinnedMeshRenderer renderer in animator.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                if (renderer.gameObject.name != "deb_Goblin01") continue;
                var mesh = new Mesh();
                renderer.BakeMesh(mesh);
                float bottom = float.PositiveInfinity;
                foreach (Vector3 vertex in mesh.vertices)
                    bottom = Mathf.Min(bottom, renderer.transform.TransformPoint(vertex).y);
                Destroy(mesh);
                if (!float.IsInfinity(bottom)) modelSoleHeight = bottom - animator.transform.position.y;
                break;
            }
        }

        private void AlignModelToGround(bool immediate)
        {
            Vector3 origin = transform.position + Vector3.up * 0.5f;
            float nearest = float.PositiveInfinity;
            float groundY = transform.position.y;
            foreach (RaycastHit hit in Physics.RaycastAll(origin, Vector3.down, 1.5f,
                Physics.DefaultRaycastLayers & ~(1 << 4) & ~(1 << 8) & ~(1 << 12), QueryTriggerInteraction.Ignore))
            {
                if (hit.collider.GetComponentInParent<GoblinEnemy>() || hit.normal.y < 0.5f || hit.distance >= nearest) continue;
                nearest = hit.distance;
                groundY = hit.point.y;
            }
            if (float.IsInfinity(nearest)) return;
            Vector3 position = animator.transform.position;
            float targetY = groundY - modelSoleHeight;
            position.y = immediate ? targetY : Mathf.Lerp(position.y, targetY, 1 - Mathf.Exp(-25 * Time.deltaTime));
            animator.transform.position = position;
        }

        private void CreateHealthBar()
        {
            var ui = new GameObject("Goblin Lebensbalken", typeof(RectTransform), typeof(Canvas));
            ui.transform.SetParent(transform, false);
            ui.transform.localPosition = Vector3.up * 1.8f;
            ui.transform.localScale = Vector3.one * 0.008f;
            healthCanvas = ui.GetComponent<Canvas>();
            healthCanvas.renderMode = RenderMode.WorldSpace;
            ui.GetComponent<RectTransform>().sizeDelta = new Vector2(140, 40);
            Image background = MakeImage("Hintergrund", ui.transform, new Color(0.1f, 0.05f, 0.05f, 0.9f));
            healthFill = MakeImage("Leben", background.transform, new Color(0.7f, 0.15f, 0.12f));
            healthFill.rectTransform.anchorMin = healthFill.rectTransform.anchorMax = new Vector2(0, 0.5f);
            healthFill.rectTransform.pivot = new Vector2(0, 0.5f);
            healthFill.rectTransform.anchoredPosition = Vector2.zero;
            var text = new GameObject("Lebenspunkte", typeof(RectTransform), typeof(TextMeshProUGUI));
            text.transform.SetParent(ui.transform, false);
            text.GetComponent<RectTransform>().sizeDelta = new Vector2(150, 28);
            text.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, 20);
            healthText = text.GetComponent<TextMeshProUGUI>();
            healthText.fontSize = 18;
            healthText.alignment = TextAlignmentOptions.Center;
            healthText.raycastTarget = false;
        }

        private static Image MakeImage(string name, Transform parent, Color color)
        {
            var obj = new GameObject(name, typeof(RectTransform), typeof(Image));
            obj.transform.SetParent(parent, false);
            obj.GetComponent<RectTransform>().sizeDelta = new Vector2(140, 10);
            Image image = obj.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }
    }
}
