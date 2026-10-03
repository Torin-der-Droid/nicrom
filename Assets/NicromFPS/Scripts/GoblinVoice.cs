using Akila.FPSFramework;
using UnityEngine;

namespace NicromFPS
{
    public sealed class GoblinVoice : MonoBehaviour
    {
        private GoblinEnemy enemy;
        private AudioSource voice;
        private float nextCall;
        private bool paused;
        private static float nextSharedCall;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetTiming() => nextSharedCall = 0;

        public void Initialize(AudioClip clip)
        {
            enemy = GetComponent<GoblinEnemy>();
            voice = gameObject.AddComponent<AudioSource>();
            voice.playOnAwake = false;
            voice.loop = false;
            voice.clip = clip;
            voice.spatialBlend = 1;
            voice.rolloffMode = AudioRolloffMode.Linear;
            voice.minDistance = 2;
            voice.maxDistance = 32;
            voice.volume = 0.45f;
            voice.dopplerLevel = 0;
            voice.priority = 180;
            nextCall = Time.time + Random.Range(1f, 7f);
        }

        private void Update()
        {
            if (!voice) return;
            if (!enemy || enemy.IsDead) { voice.Stop(); enabled = false; return; }
            bool shouldPause = enemy.Paused;
            if (shouldPause)
            {
                if (!paused) voice.Pause();
                paused = true;
                nextCall += Time.deltaTime;
                return;
            }
            if (paused) { voice.UnPause(); paused = false; }
            if (voice.isPlaying || Time.time < nextCall || Time.time < nextSharedCall) return;
            nextCall = Time.time + Random.Range(7f, 15f);
            var map = Minimap.Instance;
            if (!map || !map.player || (map.player.position - transform.position).sqrMagnitude > 32 * 32) return;
            voice.pitch = Random.Range(0.9f, 1.1f);
            voice.Play();
            // Limit overlapping calls even in large waves.
            nextSharedCall = Time.time + Mathf.Max(2, voice.clip.length / voice.pitch * 0.75f);
        }

        private void OnDisable() { if (voice) voice.Stop(); }
    }
}
