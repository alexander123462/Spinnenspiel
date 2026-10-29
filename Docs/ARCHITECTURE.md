# Architecture — Spinnenspiel

Sprigda: Unity 6 / HDRP, C#. Two asmdefs: `Spinnenspiel.Runtime` (Runtime) and an Editor-asmdef (`Spinnenspiel.EditorTools`). No Resources/Prefab-Workflow: Scene und Assets are built by **Editor-Builders** (idempotent, callable from CI).

## Layers

| Layer | Content | Rule |
| --- | --- | --- |
| `Runtime/Core` | GameLayers, SaveGame, Surface* | no dependencies on other layers |
| `Runtime/*` (Body, Legs, Movement, Silk, Insects, Audio, ...) | one system per folder | talk via small public APIs, not via scene-hierarchies |
| `Editor/Builders` | Scene-/Asset-Generators | only Editor-context, never in Player |
| `Tests/PlayMode` | verify the built scenes | CI runs them after each Builder |

## Data flow

Input (SpiderInput) → intent → SpiderMovement (root motion) → SpiderGaitController/SpiderLeg (foot placement) → LegIK. The surface is queried via SurfaceQuery/SpiderSurfaceDetector and fed into Audio (ProceduralAudio.Footsteps), Effects (SpiderFootEffects) and Silk (adhesion).

## Key classes

| Class | Job |
| --- | --- |
| SpiderMovement | root-motion, input-to-velocity, no leg logic |
| SpiderLeg | foot raycasts, TryFindFoothold, sets SurfaceType per step |
| SpiderGaitController | gait phases (GaitProfile), drives the 8 SpiderLegs |
| InsectSpawner | keeps N insects of up to 2 templates alive around `center` |
| FlyingInsect | flight behaviour, SetFlightParams(speed, size) |
| ProceduralAudio | generates AudioClip-variants per SurfaceType at runtime |

## Rules

1. **Builder = single entry point.** CI calls one static method (`Spinnenspiel.EditorTools.X.Build`); everything else is derived. Builders must be idempotent (re-run = same scene).
2. **Config as data on components**, tuning values live in the component's inspector fields — not hard-coded in behaviour classes.
3. **Tests run against built scenes**, never hand-made test-scenes — a broken Builder fails CI.
4. **JsonUtility**: only List<int/float/Vector3> in payloads (no nested custom classes).
