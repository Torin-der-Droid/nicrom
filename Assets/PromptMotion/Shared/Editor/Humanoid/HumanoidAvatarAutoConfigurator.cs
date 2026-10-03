using System.Collections.Generic;
using System.IO;
using System.Text;
using PromptMotion.Shared.Utilities;
using UnityEditor;
using UnityEngine;

namespace PromptMotion.Shared.Humanoid
{
    /// <summary>
    /// Best-effort auto-builder of a <see cref="Avatar"/> for a rigged character
    /// whose import settings did not produce one. Walks the GameObject's
    /// <see cref="Transform"/> hierarchy, maps common bone names onto Unity's
    /// <see cref="HumanBodyBones"/> slots, and asks <see cref="AvatarBuilder.BuildHumanAvatar"/>
    /// to produce a Humanoid avatar.
    /// </summary>
    /// <remarks>
    /// This is not mesh-to-rig auto-rigging (brief §10): it requires bones that
    /// already exist with sensible names. Per the brief (lines 218-227, 638)
    /// exotic rigs will fall through; the configurator surfaces diagnostic logs
    /// in the <c>log</c> out-parameter rather than throwing.
    /// </remarks>
    public static class HumanoidAvatarAutoConfigurator
    {
        /// <summary>
        /// Attempts to build a Humanoid avatar for <paramref name="character"/>.
        /// On success the avatar asset is saved next to the source FBX (or in the
        /// project root for scene-only objects with no FBX origin) and the
        /// resulting avatar is returned via <paramref name="avatar"/>.
        /// </summary>
        /// <param name="character">Prefab or scene GameObject. May not be null.</param>
        /// <param name="avatar">The created (or null on failure) <see cref="Avatar"/>.</param>
        /// <param name="log">Human-readable diagnostic log explaining what was found and what failed.</param>
        /// <returns>True on success, false otherwise.</returns>
        public static bool TryAutoConfigure(GameObject character, out Avatar avatar, out string log)
        {
            avatar = null;
            var sb = new StringBuilder();

            if (character == null)
            {
                log = "No character supplied.";
                return false;
            }

            var rootBone = FindRootBone(character.transform);
            if (rootBone == null)
            {
                sb.AppendLine($"Could not locate a plausible root bone (Hips/Pelvis/Root) under '{character.name}'.");
                log = sb.ToString();
                return false;
            }
            sb.AppendLine($"Root bone: {rootBone.name}");

            var humanBones = MapHumanBones(character.transform, sb);
            if (humanBones.Count == 0)
            {
                sb.AppendLine("No Humanoid bones could be mapped from the rig's bone names.");
                log = sb.ToString();
                return false;
            }

            var skeletonBones = CollectSkeletonBones(character.transform);
            sb.AppendLine($"Mapped {humanBones.Count} human bones; collected {skeletonBones.Count} skeleton bones.");

            var description = new HumanDescription
            {
                human = humanBones.ToArray(),
                skeleton = skeletonBones.ToArray(),
                upperArmTwist = 0.5f,
                lowerArmTwist = 0.5f,
                upperLegTwist = 0.5f,
                lowerLegTwist = 0.5f,
                armStretch = 0.05f,
                legStretch = 0.05f,
                feetSpacing = 0f,
                hasTranslationDoF = false
            };

            avatar = AvatarBuilder.BuildHumanAvatar(character, description);
            if (avatar == null || !avatar.isValid)
            {
                sb.AppendLine("AvatarBuilder produced a null or invalid avatar — bone mapping was insufficient.");
                avatar = null;
                log = sb.ToString();
                return false;
            }
            avatar.name = character.name + "_HumanoidAvatar";

            var assetPath = ResolveAvatarAssetPath(character);
            if (!string.IsNullOrEmpty(assetPath))
            {
                EnsureParentFolder(assetPath);
                AssetDatabase.CreateAsset(avatar, assetPath);
                AssetDatabase.SaveAssets();
                sb.AppendLine($"Saved avatar to {assetPath}.");
            }
            else
            {
                sb.AppendLine("Could not resolve an asset path; avatar exists in memory only.");
            }

            log = sb.ToString();
            Log.Info($"HumanoidAvatarAutoConfigurator built avatar for '{character.name}'.");
            return true;
        }

        private static Transform FindRootBone(Transform root)
        {
            string[] candidates = { "Hips", "Pelvis", "Root", "mixamorig:Hips", "Bip01 Pelvis" };
            foreach (var t in root.GetComponentsInChildren<Transform>(includeInactive: true))
            {
                foreach (var name in candidates)
                {
                    if (t.name.Equals(name, System.StringComparison.OrdinalIgnoreCase)) return t;
                }
            }
            // Fallback: first non-root child Transform.
            return root.childCount > 0 ? root.GetChild(0) : null;
        }

        private static List<HumanBone> MapHumanBones(Transform root, StringBuilder sb)
        {
            var transformsByName = new Dictionary<string, Transform>(System.StringComparer.OrdinalIgnoreCase);
            foreach (var t in root.GetComponentsInChildren<Transform>(includeInactive: true))
            {
                // First-write-wins: Unity expects unique bone names; if a rig has duplicates,
                // the first hit usually corresponds to the canonical bone.
                if (!transformsByName.ContainsKey(t.name)) transformsByName[t.name] = t;
            }

            var bones = new List<HumanBone>();
            foreach (var entry in BoneNameMap)
            {
                var human = entry.Key;
                foreach (var candidate in entry.Value)
                {
                    if (!transformsByName.TryGetValue(candidate, out var t)) continue;

                    bones.Add(new HumanBone
                    {
                        humanName = human,
                        boneName = t.name,
                        limit = new HumanLimit { useDefaultValues = true }
                    });
                    break;
                }
            }
            sb.AppendLine($"Bone mapping resolved {bones.Count} of {BoneNameMap.Count} expected slots.");
            return bones;
        }

        private static List<SkeletonBone> CollectSkeletonBones(Transform root)
        {
            var bones = new List<SkeletonBone>();
            foreach (var t in root.GetComponentsInChildren<Transform>(includeInactive: true))
            {
                bones.Add(new SkeletonBone
                {
                    name = t.name,
                    position = t.localPosition,
                    rotation = t.localRotation,
                    scale = t.localScale
                });
            }
            return bones;
        }

        private static string ResolveAvatarAssetPath(GameObject character)
        {
            var sourcePath = AssetDatabase.GetAssetPath(character);
            if (string.IsNullOrEmpty(sourcePath))
            {
                // Scene object with no asset origin — fall back to PromptMotion's Library output folder.
                return $"Assets/PromptMotion/Library/Generated/{character.name}_HumanoidAvatar.asset";
            }
            var dir = Path.GetDirectoryName(sourcePath)?.Replace('\\', '/') ?? string.Empty;
            return $"{dir}/{character.name}_HumanoidAvatar.asset";
        }

        /// <summary>
        /// Recursively creates each <c>Assets/...</c> ancestor folder of
        /// <paramref name="assetPath"/> so a subsequent
        /// <see cref="AssetDatabase.CreateAsset"/> doesn't throw on a missing
        /// directory. No-op when the folder already exists.
        /// </summary>
        private static void EnsureParentFolder(string assetPath)
        {
            var parent = Path.GetDirectoryName(assetPath)?.Replace('\\', '/');
            if (string.IsNullOrEmpty(parent)) return;
            if (AssetDatabase.IsValidFolder(parent)) return;

            var parts = parent.Split('/');
            if (parts.Length == 0 || parts[0] != "Assets") return;

            var current = parts[0];
            for (var i = 1; i < parts.Length; i++)
            {
                var next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[i]);
                }
                current = next;
            }
        }

        /// <summary>
        /// Mapping from each <see cref="HumanBodyBones"/> entry's string name to
        /// candidate bone names found in real rigs. Covers, in order:
        ///   * Unity's own Humanoid names (e.g. <c>LeftUpperLeg</c>) — surprising
        ///     to have to add, but rigs exported from Unity or copied from its
        ///     reference docs really do use these literally.
        ///   * The Mixamo style (<c>LeftUpLeg</c>) and its <c>mixamorig:</c> prefix.
        ///   * The Blender dot-suffix style (<c>UpLeg.L</c>, <c>Arm.R</c>) used
        ///     by Rigify and many community rigs.
        ///   * The <c>L_</c> / <c>R_</c> underscore-prefix style.
        ///   * The lowercase <c>_l</c> / <c>_r</c> suffix style (Synty, many UE
        ///     imports).
        /// First matching name in the list wins. If your rig uses none of these,
        /// the assist popup lets the user pick the right transform per slot.
        /// </summary>
        private static readonly Dictionary<string, string[]> BoneNameMap = new()
        {
            ["Hips"] = new[] { "Hips", "Pelvis", "mixamorig:Hips", "hips", "pelvis" },
            ["Spine"] = new[] { "Spine", "mixamorig:Spine", "spine", "spine_01" },
            ["Chest"] = new[] { "Chest", "Spine1", "mixamorig:Spine1", "spine_02" },
            ["UpperChest"] = new[] { "UpperChest", "Spine2", "mixamorig:Spine2", "spine_03" },
            ["Neck"] = new[] { "Neck", "mixamorig:Neck", "neck", "neck_01" },
            ["Head"] = new[] { "Head", "mixamorig:Head", "head" },

            ["LeftShoulder"] = new[] { "LeftShoulder", "Shoulder.L", "L_Shoulder", "shoulder_l", "clavicle_l", "mixamorig:LeftShoulder" },
            ["RightShoulder"] = new[] { "RightShoulder", "Shoulder.R", "R_Shoulder", "shoulder_r", "clavicle_r", "mixamorig:RightShoulder" },

            ["LeftUpperArm"] = new[] { "LeftUpperArm", "LeftArm", "Arm.L", "UpperArm.L", "L_UpperArm", "L_Arm", "upperarm_l", "arm_l", "mixamorig:LeftArm" },
            ["RightUpperArm"] = new[] { "RightUpperArm", "RightArm", "Arm.R", "UpperArm.R", "R_UpperArm", "R_Arm", "upperarm_r", "arm_r", "mixamorig:RightArm" },

            ["LeftLowerArm"] = new[] { "LeftLowerArm", "LeftForeArm", "ForeArm.L", "LowerArm.L", "L_LowerArm", "L_ForeArm", "lowerarm_l", "forearm_l", "mixamorig:LeftForeArm" },
            ["RightLowerArm"] = new[] { "RightLowerArm", "RightForeArm", "ForeArm.R", "LowerArm.R", "R_LowerArm", "R_ForeArm", "lowerarm_r", "forearm_r", "mixamorig:RightForeArm" },

            ["LeftHand"] = new[] { "LeftHand", "Hand.L", "L_Hand", "hand_l", "mixamorig:LeftHand" },
            ["RightHand"] = new[] { "RightHand", "Hand.R", "R_Hand", "hand_r", "mixamorig:RightHand" },

            ["LeftUpperLeg"] = new[] { "LeftUpperLeg", "LeftUpLeg", "UpLeg.L", "UpperLeg.L", "Thigh.L", "L_UpperLeg", "L_UpLeg", "L_Thigh", "upperleg_l", "thigh_l", "mixamorig:LeftUpLeg" },
            ["RightUpperLeg"] = new[] { "RightUpperLeg", "RightUpLeg", "UpLeg.R", "UpperLeg.R", "Thigh.R", "R_UpperLeg", "R_UpLeg", "R_Thigh", "upperleg_r", "thigh_r", "mixamorig:RightUpLeg" },

            ["LeftLowerLeg"] = new[] { "LeftLowerLeg", "LeftLeg", "Leg.L", "LowerLeg.L", "Calf.L", "Shin.L", "L_LowerLeg", "L_Leg", "L_Calf", "lowerleg_l", "calf_l", "shin_l", "mixamorig:LeftLeg" },
            ["RightLowerLeg"] = new[] { "RightLowerLeg", "RightLeg", "Leg.R", "LowerLeg.R", "Calf.R", "Shin.R", "R_LowerLeg", "R_Leg", "R_Calf", "lowerleg_r", "calf_r", "shin_r", "mixamorig:RightLeg" },

            ["LeftFoot"] = new[] { "LeftFoot", "Foot.L", "L_Foot", "foot_l", "mixamorig:LeftFoot" },
            ["RightFoot"] = new[] { "RightFoot", "Foot.R", "R_Foot", "foot_r", "mixamorig:RightFoot" },

            ["LeftToes"] = new[] { "LeftToes", "LeftToeBase", "ToeBase.L", "Toe.L", "L_Toes", "L_ToeBase", "toes_l", "toe_l", "ball_l", "mixamorig:LeftToeBase" },
            ["RightToes"] = new[] { "RightToes", "RightToeBase", "ToeBase.R", "Toe.R", "R_Toes", "R_ToeBase", "toes_r", "toe_r", "ball_r", "mixamorig:RightToeBase" }
        };

        /// <summary>
        /// The Humanoid slots Unity refuses to build an Avatar without. Used by
        /// the assist popup to decide which dropdowns are mandatory.
        /// </summary>
        public static readonly string[] RequiredHumanBoneSlots = new[]
        {
            "Hips", "Spine", "Head",
            "LeftUpperLeg", "LeftLowerLeg", "LeftFoot",
            "RightUpperLeg", "RightLowerLeg", "RightFoot",
            "LeftUpperArm", "LeftLowerArm", "LeftHand",
            "RightUpperArm", "RightLowerArm", "RightHand",
        };

        /// <summary>All Humanoid slots the configurator knows about (required + optional).</summary>
        public static IEnumerable<string> AllHumanBoneSlots => BoneNameMap.Keys;

        /// <summary>
        /// Run the name-based bone matching against <paramref name="character"/>
        /// and report which slots resolved automatically and which the user
        /// would need to map manually. Pure read; mutates nothing.
        /// </summary>
        public static (Dictionary<string, Transform> autoMapped, List<string> missingRequired, List<string> missingOptional)
            AnalyzeRig(GameObject character)
        {
            var mapped = new Dictionary<string, Transform>();
            var missingRequired = new List<string>();
            var missingOptional = new List<string>();
            if (character == null) return (mapped, missingRequired, missingOptional);

            var byName = new Dictionary<string, Transform>(System.StringComparer.OrdinalIgnoreCase);
            foreach (var t in character.transform.GetComponentsInChildren<Transform>(includeInactive: true))
            {
                if (!byName.ContainsKey(t.name)) byName[t.name] = t;
            }

            var requiredSet = new HashSet<string>(RequiredHumanBoneSlots);
            foreach (var entry in BoneNameMap)
            {
                Transform resolved = null;
                foreach (var candidate in entry.Value)
                {
                    if (byName.TryGetValue(candidate, out var t)) { resolved = t; break; }
                }
                if (resolved != null) mapped[entry.Key] = resolved;
                else if (requiredSet.Contains(entry.Key)) missingRequired.Add(entry.Key);
                else missingOptional.Add(entry.Key);
            }
            return (mapped, missingRequired, missingOptional);
        }

        /// <summary>
        /// Build an Avatar from an explicit slot→Transform mapping (typically
        /// what the assist popup produces). Slots absent from
        /// <paramref name="slotToTransform"/> are simply omitted from the
        /// HumanDescription — Unity tolerates missing optional bones, but
        /// missing required bones will cause BuildHumanAvatar to fail.
        /// </summary>
        public static bool TryBuildFromExplicitMapping(
            GameObject character,
            IDictionary<string, Transform> slotToTransform,
            out Avatar avatar,
            out string log)
        {
            avatar = null;
            var sb = new StringBuilder();
            if (character == null) { log = "No character supplied."; return false; }
            if (slotToTransform == null || slotToTransform.Count == 0)
            {
                log = "No bone mapping supplied."; return false;
            }

            var humanBones = new List<HumanBone>();
            foreach (var kv in slotToTransform)
            {
                if (kv.Value == null) continue;
                humanBones.Add(new HumanBone
                {
                    humanName = kv.Key,
                    boneName = kv.Value.name,
                    limit = new HumanLimit { useDefaultValues = true }
                });
            }

            var skeletonBones = CollectSkeletonBones(character.transform);
            var description = new HumanDescription
            {
                human = humanBones.ToArray(),
                skeleton = skeletonBones.ToArray(),
                upperArmTwist = 0.5f, lowerArmTwist = 0.5f,
                upperLegTwist = 0.5f, lowerLegTwist = 0.5f,
                armStretch = 0.05f, legStretch = 0.05f,
                feetSpacing = 0f, hasTranslationDoF = false,
            };

            avatar = AvatarBuilder.BuildHumanAvatar(character, description);
            if (avatar == null || !avatar.isValid)
            {
                sb.AppendLine("AvatarBuilder rejected the supplied mapping — a required bone slot is still missing or invalid.");
                avatar = null;
                log = sb.ToString();
                return false;
            }
            avatar.name = character.name + "_HumanoidAvatar";

            var assetPath = ResolveAvatarAssetPath(character);
            if (!string.IsNullOrEmpty(assetPath))
            {
                EnsureParentFolder(assetPath);
                AssetDatabase.CreateAsset(avatar, assetPath);
                AssetDatabase.SaveAssets();
                sb.AppendLine($"Saved avatar to {assetPath}.");
            }
            log = sb.ToString();
            Log.Info($"HumanoidAvatarAutoConfigurator built avatar for '{character.name}' from user-assisted mapping.");
            return true;
        }
    }
}
