using System;
using System.Collections.Generic;
using Akila.FPSFramework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace NicromFPS
{
    public sealed class CubeBuilder : IDisposable
    {
        private const float Size = 0.55f;
        private const float Reach = 5;
        private const int AimMask = Physics.DefaultRaycastLayers & ~(1 << 8) & ~(1 << 12);
        private const int CollisionMask = Physics.DefaultRaycastLayers & ~(1 << 12);
        private readonly Transform owner;
        private readonly ResourceInventory inventory;
        private readonly Material solidMaterial, previewTemplate;
        private readonly InputAction toggle = new InputAction("Build mode", InputActionType.Button, "<Keyboard>/t");
        private readonly InputAction rotate = new InputAction("Rotate wall", InputActionType.Button, "<Keyboard>/r");
        private readonly InputAction place = new InputAction("Place cube", InputActionType.Button, "<Mouse>/leftButton");
        private readonly InputAction cancel = new InputAction("Cancel building", InputActionType.Button, "<Mouse>/rightButton");
        private readonly Dictionary<Firearm, (bool input, bool prevented)> weapons = new Dictionary<Firearm, (bool, bool)>();
        private GameObject preview;
        private Material previewMaterial;
        private bool active, waitForRelease;
        private float yaw;

        public CubeBuilder(Transform owner, ResourceInventory inventory, Material solidMaterial, Material previewTemplate)
        {
            this.owner = owner;
            this.inventory = inventory;
            this.solidMaterial = solidMaterial;
            this.previewTemplate = previewTemplate;
            toggle.Enable();
            rotate.Enable();
            place.Enable();
            cancel.Enable();
        }

        // Called before the weapon scripts, so a build click never also fires a gun.
        public string Tick(Transform player, Camera camera, bool canInteract)
        {
            if (!canInteract)
            {
                Cancel();
                return null;
            }
            if (!active && waitForRelease)
            {
                if (place.IsPressed() || cancel.IsPressed()) BlockWeapons(player);
                else { RestoreWeapons(); waitForRelease = false; }
            }
            if (toggle.WasPressedThisFrame())
            {
                active = !active;
                if (active) yaw = Mathf.Round(player.eulerAngles.y / 90) * 90;
                if (!active) EndPreview();
            }
            if (active && cancel.WasPressedThisFrame())
            {
                active = false;
                EndPreview();
            }
            if (!active) return null;

            BlockWeapons(player);
            if (!previewTemplate || !solidMaterial) return "Baumaterial fehlt";
            EnsurePreview();
            Physics.SyncTransforms();
            if (rotate.WasPressedThisFrame()) yaw += 90;
            Quaternion rotation = Quaternion.Euler(0, yaw, 0);
            bool supported = FindPlacement(camera, rotation, out Vector3 position);
            preview.transform.SetPositionAndRotation(position, rotation);
            preview.SetActive(true);
            bool clear = supported;
            for (int i = 0; i < WallBuildProgress.Width && clear; i++)
            {
                Vector3 center = position + rotation * new Vector3((i - 2) * Size, 0, 0);
                foreach (Collider obstacle in Physics.OverlapBox(center, Vector3.one * (Size * 0.5f - 0.006f),
                    rotation, CollisionMask, QueryTriggerInteraction.Collide))
                    if (!obstacle.isTrigger || obstacle.GetComponentInParent<WallBlueprint>()) clear = false;
            }
            previewMaterial.SetColor("_BaseColor", clear ? new Color(1, 1, 1, 0.38f) : new Color(1, 0.2f, 0.18f, 0.38f));

            if (clear && place.WasPressedThisFrame())
            {
                // A plan is free. Resources are spent only when F completes individual blocks.
                var wall = new GameObject("Wand-Bauplan (5 Würfel pro Schicht)");
                SceneManager.MoveGameObjectToScene(wall, owner.gameObject.scene);
                wall.transform.SetParent(owner, false);
                wall.transform.SetPositionAndRotation(position, rotation);
                wall.AddComponent<WallBlueprint>().Initialize(solidMaterial, previewTemplate);
                Physics.SyncTransforms();
                active = false;
                EndPreview();
                return "Bauplan gesetzt · Mit [F] Würfel hinzufügen";
            }
            return clear ? "[Linksklick] Wand planen · [R] Drehen · [T / Rechtsklick] Beenden"
                : "Wandfläche blockiert / uneben · [R] Drehen · [T] Beenden";
        }

        private bool FindPlacement(Camera camera, Quaternion rotation, out Vector3 position)
        {
            Ray ray = new Ray(camera.transform.position, camera.transform.forward);
            position = ray.GetPoint(3);
            if (!Physics.Raycast(ray, out RaycastHit hit, Reach, AimMask, QueryTriggerInteraction.Ignore)) return false;
            if (hit.collider.GetComponent<CubePickup>() || hit.collider.GetComponentInParent<WallBlueprint>()
                || hit.collider.GetComponent<PlacedCube>() || hit.collider.gameObject.layer == 4
                || Vector3.Angle(hit.normal, Vector3.up) > 25) return false;
            float highest = hit.point.y, lowest = hit.point.y;
            for (int i = 0; i < WallBuildProgress.Width; i++)
                for (int x = -1; x <= 1; x += 2)
                    for (int z = -1; z <= 1; z += 2)
                    {
                        Vector3 offset = rotation * new Vector3((i - 2) * Size + x * Size * 0.46f, 0, z * Size * 0.46f);
                        Vector3 corner = hit.point + offset + Vector3.up * 0.6f;
                        if (!Physics.Raycast(corner, Vector3.down, out RaycastHit support, 1.2f, AimMask, QueryTriggerInteraction.Ignore)
                            || support.collider != hit.collider || Vector3.Angle(support.normal, Vector3.up) > 25) return false;
                        highest = Mathf.Max(highest, support.point.y);
                        lowest = Mathf.Min(lowest, support.point.y);
                    }
            position = new Vector3(hit.point.x, highest + Size * 0.5f + 0.008f, hit.point.z);
            // Keep a level wall and reject steep patches instead of leaving a floating foundation.
            return highest - lowest <= 0.2f && Vector3.Distance(ray.origin, position) <= Reach;
        }

        private void EnsurePreview()
        {
            if (preview) return;
            preview = new GameObject("Weiße Wandvorschau (5 Würfel)");
            SceneManager.MoveGameObjectToScene(preview, owner.gameObject.scene);
            preview.transform.SetParent(owner, false);
            previewMaterial = new Material(previewTemplate);
            for (int i = 0; i < WallBuildProgress.Width; i++)
            {
                var block = GameObject.CreatePrimitive(PrimitiveType.Cube);
                block.transform.SetParent(preview.transform, false);
                block.transform.localPosition = new Vector3((i - 2) * Size, 0, 0);
                block.transform.localScale = Vector3.one * Size;
                block.layer = 2;
                block.GetComponent<Collider>().enabled = false;
                var renderer = block.GetComponent<Renderer>();
                renderer.sharedMaterial = previewMaterial;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }
        }

        private void BlockWeapons(Transform player)
        {
            foreach (Firearm firearm in player.GetComponentsInChildren<Firearm>(true))
            {
                if (!weapons.ContainsKey(firearm)) weapons.Add(firearm, (firearm.isInputActive, firearm.firePrevented));
                firearm.isInputActive = false;
                firearm.firePrevented = true;
                firearm.isFiring = false;
            }
        }

        private void RestoreWeapons()
        {
            foreach (var entry in weapons)
                if (entry.Key)
                {
                    entry.Key.isInputActive = entry.Value.input;
                    entry.Key.firePrevented = entry.Value.prevented;
                }
            weapons.Clear();
        }

        private void EndPreview()
        {
            if (preview) preview.SetActive(false);
            waitForRelease = true;
        }

        public void Cancel()
        {
            active = false;
            waitForRelease = false;
            if (preview) preview.SetActive(false);
            RestoreWeapons();
        }

        public void Dispose()
        {
            Cancel();
            toggle.Dispose(); rotate.Dispose(); place.Dispose(); cancel.Dispose();
            if (preview) UnityEngine.Object.Destroy(preview);
            if (previewMaterial) UnityEngine.Object.Destroy(previewMaterial);
        }
    }
}
