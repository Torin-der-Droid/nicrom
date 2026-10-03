# PromptMotion — Unity Editor Tool Build Brief

This document is the complete build brief for the Unity editor portion of **PromptMotion**, an AI animation generation tool for the Unity Asset Store. It is written for an AI coding agent (Claude Code / Claude CLI with MCP filesystem access to a Unity project) to execute phase by phase.

Rename `PromptMotion` throughout if the product name changes — it is used as both the C# namespace root and the top-level folder under `Assets/`.

---

## 0. Product Context (read first, do not skip)

### 0.1 What we are building

A Unity **editor-only** tool. The user:
1. Opens a dockable editor window (`Window > PromptMotion`).
2. Drags their humanoid character prefab into a slot.
3. Types a text prompt (e.g. "a person does a jumping jack").
4. Clicks **Generate**.
5. The tool sends a request to a remote backend, receives an FBX animation file, auto-imports it as a Humanoid-retargeted `AnimationClip`, and plays it on their character in a preview viewport.

The user pays for generations via a credit balance tied to an API key. The free tier grants 2–3 generations.

### 0.2 What is out of scope for this Unity work

- The backend (separate project — Python service wrapping NVIDIA Kimodo). The Unity tool talks to it over HTTP/JSON.
- Payment / Stripe / credit top-up (happens on a web dashboard the user visits outside Unity — the tool just reads remaining balance from the API).
- SOMA → Humanoid bone remapping (done server-side, not in Unity).

Unity's responsibility: authenticate, send prompts, receive FBX, import cleanly, play back.

### 0.3 Why Humanoid Avatar is the foundation

Unity's **Humanoid Avatar** system handles retargeting for free. Both the server-returned FBX and the user's character must be configured as Humanoid. When that is true, Mecanim auto-remaps bone rotations at runtime regardless of proportions. **Do not write a custom retargeter.** If you find yourself wanting to, stop and ask.

---

## 1. Auto-Decision Policy

### 1.1 Decide and proceed without asking

- File, class, method, and variable naming (follow C# / Unity conventions: PascalCase for types and public members, `_camelCase` for private fields, `camelCase` for locals).
- Folder subdivision under `Assets/PromptMotion/` as long as the top-level layout in §3 is preserved.
- Whether to use UI Toolkit (UXML/USS) or IMGUI for a given window — **default to UI Toolkit** for new windows, IMGUI only for custom inspectors where it is simpler.
- Internal helper classes, extension methods, enums.
- XML doc comments on public API.
- Using `System.Threading.Tasks` (`async`/`await`) vs coroutines — **default to async/await** with `EditorApplication.update` pumping when needed; coroutines only if an existing Unity API requires them.
- JSON serialization library — use `Newtonsoft.Json` (Unity's `com.unity.nuget.newtonsoft-json`, already first-party).
- Exception handling, logging (use `Debug.Log` / `Debug.LogWarning` / `Debug.LogError` with the prefix `[PromptMotion]`).
- Test file placement under `Assets/PromptMotion/Tests/Editor/`.
- Caching strategy for downloaded FBX (put under `Assets/PromptMotion/Generated/` with timestamped filenames).
- Adding `.gitignore` entries within the package.

### 1.2 Ask the user before doing

- **Adding any UPM package** not listed in §2.2.
- **Changing the API contract** in §5 (request/response shapes).
- **Changing folder structure** at the top level beyond what §3 describes.
- **Renaming the product** or namespace root.
- **Adding runtime (non-editor) code** — this is an editor-only tool unless the user explicitly requests runtime pieces.
- **Bundling third-party assets** (sample characters, animations, art).
- **Changing the free-tier flow** (number of free generations, how they are granted).
- **Replacing Humanoid Avatar retargeting** with anything else.

### 1.3 Always do

- Write editor tests alongside each public class that has non-trivial logic (§9).
- Guard all editor code with `#if UNITY_EDITOR` or keep it inside an `Editor/` folder (the latter is preferred — `Editor/` asmdef excludes from builds automatically).
- Keep the main `.asmdef` `Editor`-only. No runtime assembly unless §1.2 is triggered.

---

## 2. Prerequisites and Assumptions

### 2.1 Unity version

Target **Unity 2022.3 LTS** or newer. If the project's `ProjectSettings/ProjectVersion.txt` shows anything older, stop and tell the user.

### 2.2 Required packages

Check `Packages/manifest.json` for each. If missing, add via the manifest (do not open Package Manager UI — direct manifest edit is cleaner for scripted setup):

- `com.unity.nuget.newtonsoft-json` ≥ 3.2.1 — JSON.
- `com.unity.ui` — UI Toolkit (built-in in 2022.3+, verify).
- `com.unity.test-framework` ≥ 1.3 — editor tests.

No other packages. If you feel the need for one, §1.2 applies.

### 2.3 Project state

Assume the user's Unity project may or may not already have characters, animations, or other assets. Do not touch anything outside `Assets/PromptMotion/`.

---

## 3. Top-Level Folder Layout

Create this structure at the start of Phase 1 and preserve it throughout.

```
Assets/PromptMotion/
├── Editor/
│   ├── PromptMotion.Editor.asmdef
│   ├── Windows/            # EditorWindow subclasses + UXML/USS
│   ├── Importers/          # AssetPostprocessor classes
│   ├── Http/               # API client, DTOs, mock client
│   ├── Settings/           # ScriptableObject settings + key storage
│   ├── Validation/         # Character validators
│   ├── Preview/            # Preview scene / playback helpers
│   └── Utilities/          # Misc helpers, logging
├── Tests/
│   └── Editor/
│       └── PromptMotion.Tests.Editor.asmdef
├── Generated/              # runtime-generated FBX output (gitignored)
│   └── .gitkeep
├── Samples~/               # bundled sample character & sample FBX for mock mode
│   └── MockGeneration/
│       └── sample_jumpingjack.fbx   (placeholder — user provides later)
├── Documentation~/
│   └── README.md
├── CHANGELOG.md
├── LICENSE.md
├── README.md
└── .gitignore              # ignores Generated/
```

Notes:
- `Samples~` with the tilde is Unity's convention for "content that doesn't compile into the project." Do not use `Samples` without the tilde — it gets scanned.
- The `Generated/` folder ships empty. The `.gitignore` inside should contain `*` and `!.gitignore` and `!.gitkeep`.

---

## 4. Implementation Phases

Execute in order. At the end of each phase, run the acceptance checks in §9 for that phase before moving on. If any check fails, fix before proceeding — do not accumulate failures across phases.

### Phase 1 — Skeleton, settings, assembly definitions

**Goal:** empty but compiling package with settings storage and a menu item that opens a blank editor window.

Files to create:

1. `Assets/PromptMotion/Editor/PromptMotion.Editor.asmdef`
   - Name: `PromptMotion.Editor`
   - Include Platforms: `Editor` only
   - References: `Unity.Newtonsoft.Json`
   - Auto Referenced: false (keep explicit)

2. `Assets/PromptMotion/Editor/Settings/PromptMotionSettings.cs`
   - A `ScriptableObject` singleton stored at `Assets/PromptMotion/Editor/Settings/Settings.asset`.
   - Fields: `string ApiBaseUrl` (default `"https://api.promptmotion.example/v1"` — placeholder, user will change), `bool UseMockClient` (default `true`), `int DefaultDurationSeconds` (default `4`), `string OutputFolder` (default `"Assets/PromptMotion/Generated"`).
   - Auto-create the asset on first access using `AssetDatabase.CreateAsset` if it doesn't exist.
   - Expose a static `Instance` accessor.

3. `Assets/PromptMotion/Editor/Settings/ApiKeyStorage.cs`
   - **Do not store the API key in the ScriptableObject.** It must not be committed to version control.
   - Store it in `EditorPrefs` under a project-scoped key: `"PromptMotion.ApiKey." + Application.dataPath.GetHashCode().ToString("X")`.
   - Public API: `static string Get()`, `static void Set(string key)`, `static void Clear()`, `static bool HasKey()`.

4. `Assets/PromptMotion/Editor/Windows/PromptMotionWindow.cs`
   - `EditorWindow` subclass.
   - Menu item: `[MenuItem("Window/PromptMotion/Generator")]`.
   - `[MenuItem("Window/PromptMotion/Settings")]` opens a separate settings window or focuses the Inspector on the settings asset — pick one (auto-decide).
   - Window title `"PromptMotion"`, min size 420 × 560, uses UI Toolkit.
   - Placeholder content for now: label reading `"PromptMotion — ready"`.

5. `Assets/PromptMotion/Editor/Utilities/Log.cs`
   - Static wrapper: `Info`, `Warn`, `Error`, each prepending `[PromptMotion]`.

6. Root files: `README.md`, `CHANGELOG.md`, `LICENSE.md` (MIT placeholder), `.gitignore` (ignores `Generated/*` except `.gitkeep`).

**Acceptance:** project compiles with zero errors. `Window > PromptMotion > Generator` opens the empty window. Settings asset auto-generates on first open.

---

### Phase 2 — Character validation

**Goal:** user can assign a character and get immediate feedback on whether it is usable as a Humanoid target.

Files to create:

1. `Assets/PromptMotion/Editor/Validation/HumanoidValidator.cs`
   - Input: `GameObject prefab` or `Animator animator`.
   - Produces a `ValidationReport` with:
     - `bool IsUsable` (overall pass/fail).
     - `List<ValidationIssue> Issues` where each issue has `Severity` (Error/Warning/Info), `string Message`, and `Action Fix` (nullable, optional auto-fix callback).
   - Checks:
     - Has an `Animator` component on root or child.
     - `Animator.avatar != null`.
     - `Animator.avatar.isHuman == true`.
     - `Animator.avatar.isValid == true`.
     - Scale is approximately uniform and non-zero.
     - Character is roughly in a T-pose on frame 0 (warning only — check a few key bone rotations, don't be strict).
   - Return issues ordered by severity.

2. `Assets/PromptMotion/Editor/Validation/ValidationReport.cs` and `ValidationIssue.cs` as simple POCOs.

3. Update `PromptMotionWindow.cs`:
   - Add an `ObjectField` (UI Toolkit) for `GameObject` (prefab or scene object).
   - On value change, run `HumanoidValidator` and render issues below the field with colored status icons.
   - If `IsUsable == false`, disable the Generate button (which we'll add in Phase 3).

**Acceptance:** dragging a Mixamo humanoid character shows a green "ready" state. Dragging a `Cube` shows clear errors. Validation runs under 100ms for a typical character.

---

### Phase 3 — Mock client and generation flow (no real HTTP yet)

**Goal:** clicking **Generate** returns a valid FBX (from the bundled sample) and imports it as a Humanoid animation clip. Full end-to-end flow minus the server.

Files to create:

1. `Assets/PromptMotion/Editor/Http/IGenerationClient.cs`

```csharp
using System.Threading;
using System.Threading.Tasks;

namespace PromptMotion.Http
{
    public interface IGenerationClient
    {
        Task<GenerationResponse> GenerateAsync(GenerationRequest request, CancellationToken ct);
        Task<AccountStatus> GetAccountStatusAsync(CancellationToken ct);
    }
}
```

2. `Assets/PromptMotion/Editor/Http/GenerationRequest.cs` and `GenerationResponse.cs` — DTOs per §5.1–5.2.

3. `Assets/PromptMotion/Editor/Http/AccountStatus.cs` — DTO per §5.3.

4. `Assets/PromptMotion/Editor/Http/LocalMockClient.cs`
   - Implements `IGenerationClient`.
   - `GenerateAsync` simulates a 3-second delay (`Task.Delay(3000, ct)`), then copies `Samples~/MockGeneration/sample_jumpingjack.fbx` into the configured `Generated/` folder with a new timestamped filename, returns `GenerationResponse` pointing to that path.
   - `GetAccountStatusAsync` returns a fake status with 10 credits, "mock@local" user, "free" plan.
   - If the sample FBX is missing, log a clear error and return a failure response. **Do not throw.**

5. `Assets/PromptMotion/Editor/Http/ClientFactory.cs`
   - Returns `LocalMockClient` when `PromptMotionSettings.Instance.UseMockClient == true`, otherwise returns `RemoteHttpClient` (Phase 5).

6. `Assets/PromptMotion/Editor/Windows/PromptMotionWindow.cs` — expand:
   - Add prompt text field (multiline), duration slider (1–10s), seed field (optional, -1 = random).
   - Generate button wired to:
     ```
     var client = ClientFactory.Create();
     var response = await client.GenerateAsync(request, token);
     ```
   - Progress bar during generation (`EditorUtility.DisplayProgressBar` is fine for the mock; in Phase 5 replace with a non-blocking UI progress element).
   - On success, invoke the importer (Phase 4) and the preview (Phase 6).
   - Handle cancellation (a Cancel button that triggers the `CancellationTokenSource`).

**Sample FBX sourcing:** ship a small placeholder FBX. If one is not yet available, create `Samples~/MockGeneration/README.txt` explaining that the user should drop their own placeholder in there. **Do not bundle a third-party animation without asking** (§1.2).

**Acceptance:** clicking Generate in mock mode writes a new FBX into `Assets/PromptMotion/Generated/`, the console logs the path, and no exceptions are thrown. Cancel button interrupts cleanly.

---

### Phase 4 — AssetPostprocessor for auto-Humanoid configuration

**Goal:** any FBX that lands in `Assets/PromptMotion/Generated/` gets auto-configured as a Humanoid-retargeted animation at import time, with a single default `AnimationClip` extracted. No manual model importer clicks required.

Files to create:

1. `Assets/PromptMotion/Editor/Importers/MotionFbxPostprocessor.cs`
   - Subclass `AssetPostprocessor`.
   - Override `OnPreprocessModel()`:
     - Guard: only act if `assetPath` starts with the configured output folder (via `PromptMotionSettings.Instance.OutputFolder`).
     - Cast `assetImporter` to `ModelImporter`.
     - Set `animationType = ModelImporterAnimationType.Human`.
     - Set `avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel`.
     - Set `importAnimation = true`.
     - Clip mode: import with root motion enabled (`motionNodeName = ""` to let Unity pick), no bake.
     - Disable mesh import if the FBX contains only animation data: `importVisibility = false`, `importCameras = false`, `importLights = false`, `meshCompression = ModelImporterMeshCompression.Off`, and if the skeleton-only, consider `importNormals = ModelImporterNormals.None`.
     - Ensure `optimizeGameObjects = false` (breaks bone access).
   - Override `OnPostprocessModel(GameObject g)`:
     - Log imported animation count.
     - If no animation clip found, log warning.

2. `Assets/PromptMotion/Editor/Importers/ImportedAnimationLocator.cs`
   - Static helper: given an FBX path, return the `AnimationClip` sub-asset inside it (`AssetDatabase.LoadAllAssetsAtPath` → filter by type and exclude preview clips — Unity auto-generates a `__preview__` clip that should be ignored).

**Acceptance:** copying any skeleton-only FBX into `Assets/PromptMotion/Generated/` auto-converts it to Humanoid. `ImportedAnimationLocator.GetClip(path)` returns a non-null `AnimationClip`. The clip plays if dragged onto a Humanoid character in the scene.

---

### Phase 5 — Remote HTTP client

**Goal:** `RemoteHttpClient` implementing `IGenerationClient` against the API contract in §5. Not yet tested end-to-end against a real server — that happens when the backend exists. Tests use a mocked `HttpClient` handler.

Files to create:

1. `Assets/PromptMotion/Editor/Http/RemoteHttpClient.cs`
   - Uses `UnityWebRequest` (preferred in editor for Unity auth handling) or `System.Net.Http.HttpClient` (cleaner async) — **default: `UnityWebRequest` with async wrapper**, because it respects Unity's editor proxy settings.
   - Reads API key from `ApiKeyStorage`. If missing, throw `InvalidOperationException` with a clear message.
   - `GenerateAsync`:
     - `POST {BaseUrl}/generate` with JSON body.
     - Headers: `Authorization: Bearer {apiKey}`, `Content-Type: application/json`, `X-Client: promptmotion-unity/{version}`.
     - Response is either `202 Accepted` with a `{ "job_id": "..." }` body (async flow) or `200 OK` with a `GenerationResponse` body (sync flow). Support both.
     - For the async flow, poll `GET {BaseUrl}/jobs/{jobId}` every 1s up to `timeoutSeconds` (default 120).
     - On completion, `fbx_url` in the response is downloaded as binary to `Generated/`.
     - On HTTP 401, throw `UnauthorizedException` with a message telling user to check API key.
     - On HTTP 402, throw `InsufficientCreditsException`.
     - On HTTP 429, respect `Retry-After` header and retry up to 3 times.
     - On HTTP 5xx, retry with exponential backoff up to 3 times.
   - `GetAccountStatusAsync`: `GET {BaseUrl}/account`.

2. `Assets/PromptMotion/Editor/Http/ApiExceptions.cs`
   - `UnauthorizedException`, `InsufficientCreditsException`, `GenerationFailedException`.

3. Window update: if generation throws `InsufficientCreditsException`, show a dialog with a button linking to the (future) web dashboard URL pulled from settings.

**Note on testing:** in Phase 9 tests, inject an `IHttpTransport` abstraction so the logic can be unit-tested without network calls. Auto-decide the shape of the abstraction.

**Acceptance:** compiles, toggling `UseMockClient = false` routes through `RemoteHttpClient`, tests cover 200/202/401/402/429/5xx paths.

---

### Phase 6 — Preview playback

**Goal:** after a successful generation, the clip plays automatically on the user's assigned character inside a preview viewport embedded in the editor window (or in a dedicated preview scene — your call).

Files to create:

1. `Assets/PromptMotion/Editor/Preview/AnimationPreviewRenderer.cs`
   - Uses `PreviewRenderUtility` to render the character with the clip sampled at a time value driven by editor delta time.
   - Controls: Play/Pause, scrub bar, loop toggle, speed (0.25x–2x).
   - Drive sampling via `clip.SampleAnimation(previewInstance, time)` — this works in-editor.

2. Integrate into `PromptMotionWindow.cs`: preview panel fills the bottom half of the window when a clip is loaded. If preview instantiation fails (non-humanoid, missing bones), fall back to showing the clip on a default skeleton mesh and warn.

**Known gotcha:** `PreviewRenderUtility` is quirky. If you hit rendering issues that take more than 30 minutes to debug, stop and ask before going deeper — there may be a simpler approach (e.g., instantiate into a hidden scene and render via a second camera).

**Acceptance:** generating a clip in mock mode results in the character animating in the window within 1 second of import completion. Play/pause/scrub work.

---

### Phase 7 — Credit balance and free-tier UI

**Goal:** the window shows remaining credits, account email, and plan tier. Free tier users see a "X/3 free generations remaining" badge.

Files to create:

1. `Assets/PromptMotion/Editor/Windows/Elements/AccountHeader.cs` — a UI Toolkit visual element displaying email, plan, credit balance, and a "Top up" button that opens a URL.
2. Refresh logic: call `GetAccountStatusAsync` on window open and after each successful generation. Cache for 30 seconds.
3. If the user has no API key set, display a prominent banner with an "Enter API key" button opening a modal for key entry. Validate the key by making a test `GetAccountStatusAsync` call before saving it.

**Acceptance:** balance updates after generation. Missing key is handled gracefully with an onboarding banner, not a crash.

---

### Phase 8 — Polish pass

Do all of these. They are small but non-negotiable for an Asset Store release.

1. Every public type gets an XML doc comment.
2. Add `PromptMotionWindow` → "Help" menu opening the README in the browser.
3. `Undo.RegisterCreatedObjectUndo` for any scene objects instantiated (preview prefabs).
4. Ensure the window survives domain reloads (`[SerializeField]` the critical state; use `OnEnable`/`OnDisable` correctly).
5. Defensive checks: generation folder exists before writing, create if missing.
6. User-visible strings routed through a single `Strings.cs` file (makes future localization trivial).
7. No hardcoded colors — use `EditorGUIUtility.isProSkin` branches or UI Toolkit USS variables.
8. Remove every `Debug.Log` that is not behind `PromptMotionSettings.Instance.VerboseLogging` (add that field). Errors and warnings always log.

---

## 5. API Contract (Unity ↔ backend)

This is the contract the Unity tool builds against. If the backend diverges later, update both sides. Do not change this unilaterally — §1.2.

### 5.1 Request — `POST /v1/generate`

```json
{
  "prompt": "a person does a jumping jack",
  "duration_seconds": 4,
  "seed": -1,
  "skeleton": "humanoid",
  "format": "fbx",
  "constraints": []
}
```

- `skeleton` currently always `"humanoid"`. Future values reserved.
- `format` currently always `"fbx"`.
- `constraints` reserved for future keyframe constraints; ship as empty array.

### 5.2 Response — `200 OK` (sync) or final job state

```json
{
  "job_id": "j_01HXYZ...",
  "status": "completed",
  "fbx_url": "https://cdn.promptmotion.example/j_01HXYZ.../out.fbx",
  "credits_charged": 1,
  "duration_seconds": 4,
  "created_at": "2026-04-24T12:34:56Z"
}
```

### 5.3 Account — `GET /v1/account`

```json
{
  "email": "user@example.com",
  "plan": "free",
  "credit_balance": 2,
  "free_generations_remaining": 3,
  "topup_url": "https://promptmotion.example/billing"
}
```

### 5.4 Job status — `GET /v1/jobs/{job_id}`

Returns the same shape as 5.2. `status` may be `"queued" | "running" | "completed" | "failed"`. When `failed`, include a `"error": { "code": "...", "message": "..." }` object.

### 5.5 Error shape (any non-2xx)

```json
{ "error": { "code": "insufficient_credits", "message": "Balance is 0." } }
```

---

## 6. Storage and Security

- **API key** never committed. `EditorPrefs` only (§Phase 1).
- **Settings asset** (`PromptMotionSettings`) contains no secrets — safe to commit.
- **Generated FBX** gitignored by default (the `Generated/` folder). Users who want to commit animations can move them elsewhere.
- No telemetry in this phase. If telemetry is added later, it is §1.2 and must be opt-in.

---

## 7. Logging

- `Log.Info` for lifecycle events only (window open, generation start, generation complete).
- `Log.Warn` for recoverable issues (sample FBX missing, validator warnings).
- `Log.Error` for failures (HTTP errors, import failures, exceptions).
- No logging in hot paths (preview sampling loop).
- Respect `PromptMotionSettings.Instance.VerboseLogging` for `Log.Info`. Warnings and errors always log regardless.

---

## 8. Coding Conventions

- Nullable reference types enabled at the asmdef level (`#nullable enable` file-by-file or project-wide — auto-decide).
- One public class per file.
- Async methods suffix `Async` and accept `CancellationToken ct` as the last parameter.
- No `async void` except for event handlers.
- No static mutable state except for the `Instance` singletons defined here.
- No reflection unless unavoidable.
- Use `EditorApplication.delayCall` to defer `AssetDatabase` operations out of `OnValidate`/`OnInspectorGUI`.

---

## 9. Testing and Acceptance

### 9.1 Test project setup

Create `Assets/PromptMotion/Tests/Editor/PromptMotion.Tests.Editor.asmdef`:
- Editor-only.
- References: `PromptMotion.Editor`, `UnityEngine.TestRunner`, `UnityEditor.TestRunner`, `Unity.Newtonsoft.Json`.
- Define constraints: `UNITY_INCLUDE_TESTS`.

### 9.2 Required tests (write these alongside each phase)

- `HumanoidValidatorTests` — validates known-good and known-bad inputs.
- `ApiKeyStorageTests` — set/get/clear round-trip, project isolation.
- `LocalMockClientTests` — returns a valid response path, handles missing sample FBX.
- `RemoteHttpClientTests` — uses an injected fake transport; covers 200, 202 + polling, 401, 402, 429 with retry, 5xx with retry, timeout.
- `MotionFbxPostprocessorTests` — copy a sample FBX into `Generated/`, verify it gets reimported as Humanoid.
- `GenerationRequestSerializationTests` — round-trip against the contract in §5.

### 9.3 Phase-level acceptance checks

Run at the end of each phase. All must pass before advancing.

**Phase 1:** project compiles, window opens, settings asset exists.
**Phase 2:** validator passes on a Mixamo rig, fails on a cube, runs <100ms.
**Phase 3:** mock generation produces an FBX file and triggers import.
**Phase 4:** FBX in `Generated/` is Humanoid after import; `GetClip` returns non-null.
**Phase 5:** all `RemoteHttpClient` test paths green.
**Phase 6:** preview plays within 1s of generation completion, scrub works.
**Phase 7:** credit balance renders, API-key-missing banner works.
**Phase 8:** no stray `Debug.Log`, all public API documented, domain reload survives.

### 9.4 Final acceptance (after Phase 8)

- Open a fresh Unity 2022.3 project, add the `PromptMotion` folder, open `Window > PromptMotion > Generator`.
- With `UseMockClient = true`, drag in any Humanoid character, type any prompt, click Generate. Expect a clip to play on the character within ~5 seconds of click.
- With `UseMockClient = false` but no API key set, expect the onboarding banner, no crash.
- With a stale/bad API key, expect a clear "Unauthorized" message, no crash.
- No warnings or errors in the Unity Console during normal flow.

---

## 10. Explicit Non-Goals

Do **not** build any of the following in this pass. If tempted, §1.2 applies.

- A custom retargeter (use Humanoid Avatar).
- Blend trees, animation state machines, or runtime controllers.
- Finger/facial animation support (Kimodo does not produce them).
- A custom FBX parser (let Unity's importer do its job).
- Anything interacting with Stripe or payment flows inside Unity.
- Auto-updates / self-updating package logic.
- Telemetry / analytics.
- Bundled third-party characters or animation clips (licensing risk).
- A runtime API for end-user games to call generation from gameplay — out of scope; the tool is an editor-time pipeline.

---

## 11. Common Pitfalls to Avoid

- **Do not** set `ModelImporter.animationType` in `OnPostprocessModel` — it must be in `OnPreprocessModel`, otherwise Unity re-imports in a loop.
- **Do not** call `AssetDatabase.Refresh()` inside an `OnPreprocess*` callback.
- **Do not** trust that `Animator.avatar` is non-null without also checking `isValid` — Unity has been known to produce technically-present-but-broken avatars.
- **Do not** use `JsonUtility` for the API payloads — it can't round-trip nested arrays and null-vs-missing semantics the way Newtonsoft can.
- **Do not** hold `AnimationClip` references across domain reloads without reloading them via `AssetDatabase.LoadAssetAtPath`.
- **Do not** block the main thread during HTTP — use `UnityWebRequestAsyncOperation` or async/await.
- **Do not** ship with `UseMockClient = false` as default; the user won't have a key yet on first install.

---

## 12. Checklist Before Handoff

When you believe the tool is ready for the user to test:

- [ ] All phases 1–8 acceptance checks pass.
- [ ] All tests in §9.2 pass in the Test Runner.
- [ ] No compile warnings in `PromptMotion.Editor.asmdef`.
- [ ] `README.md` is filled in with: what the tool does, how to install, how to enter an API key, how to switch to mock mode, how to report issues.
- [ ] `CHANGELOG.md` has a `0.1.0` entry.
- [ ] Opened the window on a clean project and ran through the §9.4 final acceptance manually.
- [ ] Reported remaining known issues to the user with concrete repro steps.

---

## 13. Working Cadence

- After each phase, post a short status update: what was built, what tests pass, any decisions made under §1.1 that were non-obvious.
- Before starting each phase, reread §1 (auto-decision policy) and §10 (non-goals). These drift in memory over long sessions.
- If a phase's acceptance fails and the fix is not obvious within 30 minutes of focused attempt, stop and ask rather than going deeper.
- Commit after each green phase if the user has a git repo. Use commit message format: `phase N: <one-line summary>`.

---

End of brief.
