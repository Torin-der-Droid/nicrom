using System.Collections.Generic;
using UnityEngine;
namespace NicromFPS
{
    // Operates on the existing generic FPS rig after its weapon animation has evaluated.
    public sealed class LeftHandGesturePose
    {
        readonly Transform upper, lower, hand;
        readonly List<Transform> fingers = new List<Transform>();
        readonly List<Quaternion> openFingers = new List<Quaternion>();
        readonly List<Transform> modified = new List<Transform>();
        readonly List<Quaternion> saved = new List<Quaternion>();
        readonly Vector3 palmNormal, fingerDirection;
        bool applied;
        public Transform Palm { get; }
        public Transform Hand => hand;

        public static bool TryCreate(Transform model, out LeftHandGesturePose pose)
        {
            pose = null;
            var names = new Dictionary<string, Transform>();
            foreach (var t in model.GetComponentsInChildren<Transform>(true))
                if (!names.ContainsKey(t.name)) names.Add(t.name, t);
            if (!names.TryGetValue("upperarm_L.L", out var upper)
                || !names.TryGetValue("lowerarm_L.L", out var lower)
                || !names.TryGetValue("hand_L.L", out var hand)
                || !names.TryGetValue("middle_L.L", out var middle)
                || !names.TryGetValue("index_L.L", out var index)
                || !names.TryGetValue("pink_L.L", out var pink)) return false;
            foreach (var skin in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                var bones = skin.bones;
                if (!skin.sharedMesh) continue;
                var bind = skin.sharedMesh.bindposes;
                var matrices = new Dictionary<Transform, Matrix4x4>();
                for (int i = 0; i < bones.Length && i < bind.Length; i++)
                    if (bones[i]) matrices[bones[i]] = bind[i];
                if (!matrices.ContainsKey(hand) || !matrices.ContainsKey(middle)
                    || !matrices.ContainsKey(index) || !matrices.ContainsKey(pink)) continue;
                pose = new LeftHandGesturePose(upper, lower, hand, middle, index, pink, matrices);
                return true;
            }
            return false;
        }

        LeftHandGesturePose(Transform upper, Transform lower, Transform hand, Transform middle,
            Transform index, Transform pink, Dictionary<Transform, Matrix4x4> bind)
        {
            this.upper = upper; this.lower = lower; this.hand = hand;
            Matrix4x4 handBind = bind[hand];
            Vector3 middleBase = (handBind * bind[middle].inverse).GetColumn(3);
            Vector3 across = (Vector3)(handBind * bind[index].inverse).GetColumn(3)
                - (Vector3)(handBind * bind[pink].inverse).GetColumn(3);
            fingerDirection = middleBase.normalized;
            // Left palm faces the negative X side in this rig's open bind pose.
            palmNormal = Vector3.Cross(across, fingerDirection).normalized;
            foreach (var finger in hand.GetComponentsInChildren<Transform>(true))
            {
                if (!LeftHandOpenPose.TryGet(finger.name, out var openRotation)) continue;
                fingers.Add(finger);
                openFingers.Add(openRotation);
            }
            modified.Add(upper); modified.Add(lower); modified.Add(hand); modified.AddRange(fingers);
            foreach (var t in modified) saved.Add(t.localRotation);
            var anchor = new GameObject("Left Palm Magic Origin");
            anchor.transform.SetParent(hand, false);
            anchor.transform.localPosition = middleBase * .5f;
            anchor.transform.localRotation = Quaternion.LookRotation(palmNormal, fingerDirection);
            Palm = anchor.transform;
        }

        public void Apply(Transform view, float weight)
        {
            Restore();
            if (!hand || !view || weight <= 0) return;
            weight = Mathf.Clamp01(weight);
            for (int i = 0; i < modified.Count; i++) saved[i] = modified[i].localRotation;
            applied = true;
            Vector3 desired = view.TransformPoint(new Vector3(-.20f, -.13f, .48f));
            Vector3 target = Vector3.Lerp(hand.position, desired, weight);
            Quaternion rotation = Quaternion.Slerp(hand.rotation,
                Quaternion.LookRotation(view.forward, view.up) * Quaternion.Inverse(Quaternion.LookRotation(palmNormal, fingerDirection)), weight);
            SolveArm(target, view.TransformDirection(new Vector3(-1, -.6f, -.15f)));
            hand.rotation = rotation;
            for (int i = 0; i < fingers.Count; i++)
                fingers[i].localRotation = Quaternion.Slerp(fingers[i].localRotation, openFingers[i], weight);
        }

        void SolveArm(Vector3 target, Vector3 elbowDirection)
        {
            Vector3 shoulder = upper.position;
            float a = Vector3.Distance(shoulder, lower.position), b = Vector3.Distance(lower.position, hand.position);
            if (a < .00001f || b < .00001f) return;
            Vector3 direction = target - shoulder;
            float distance = Mathf.Clamp(direction.magnitude, Mathf.Abs(a - b) + .0001f, (a + b) * .98f);
            direction.Normalize();
            Vector3 pole = Vector3.ProjectOnPlane(elbowDirection, direction).normalized;
            if (pole.sqrMagnitude < .1f) pole = Vector3.ProjectOnPlane(Vector3.up, direction).normalized;
            float along = (a * a + distance * distance - b * b) / (2 * distance);
            Vector3 elbow = shoulder + direction * along + pole * Mathf.Sqrt(Mathf.Max(0, a * a - along * along));
            upper.rotation = Quaternion.FromToRotation(lower.position - shoulder, elbow - shoulder) * upper.rotation;
            target = shoulder + direction * distance;
            lower.rotation = Quaternion.FromToRotation(hand.position - lower.position, target - lower.position) * lower.rotation;
        }

        public void Restore()
        {
            if (!applied) return;
            for (int i = 0; i < modified.Count; i++) if (modified[i]) modified[i].localRotation = saved[i];
            applied = false;
        }
        public void Dispose()
        {
            Restore();
            if (Palm) { if (Application.isPlaying) Object.Destroy(Palm.gameObject); else Object.DestroyImmediate(Palm.gameObject); }
        }
    }
}
