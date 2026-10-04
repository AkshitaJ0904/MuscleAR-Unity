# MuscleAR – Unity AR anatomy lab

Unity **2022.3.62f3** (built-in render pipeline, no extra packages besides uGUI). Everything – the human body, the lab,
the AR camera, the 16 screens – is generated from code at runtime, so there are no assets to import.

## Run it
1. Unity Hub → **Add project from disk** → pick this folder (`AR`) → open with **2022.3.62f3**.
2. The scene `Assets/Scenes/MuscleAR.unity` opens automatically (or: menu **MuscleAR ▸ Open MuscleAR Scene**).
3. Press **Play**. (Optional: **MuscleAR ▸ Game View -> Phone Portrait**. Any Game-view size works – the app letterboxes itself to a 390×844 phone.)

Pressing Play in *any* scene also boots the app (`AppFlow` has a `RuntimeInitializeOnLoadMethod`).

## Flow (matches the Figma storyboard)
| # | Screen | Tag | What happens |
|---|--------|-----|--------------|
| 01 | Home | ENTRY | Logo + "Enter Anatomy Lab" over the orbiting lab |
| 02 | Onboarding | ORIENT | "Your Anatomy Lab. Anywhere." → Continue |
| 03 | Choose mode | SETUP | AR Explorer / Learn / Quiz → Start Exploring |
| 04 | Detect table surface | SCAN | Simulated plane detection: sweep, feature points, grid, "Surface detected ✓" → Place Anatomy |
| 05 | Place model | PLACE | Model pops onto the table, hologram callouts, "Tap to place" / Place Here ✓ |
| 06 | Model placed | AR ACTIVE | "Ready to explore", gesture bar (Move / Zoom / Rotate / Tap) |
| 07 | Move model | GESTURE | Drag to move the model on the table plane |
| 08 | Rotate & zoom | GESTURE | *(inferred – not in the screenshots)* twist/drag to rotate, pinch/scroll to zoom |
| 09 | Select a body part | DISCOVER | Low side camera + callouts; tap any muscle (raycast) |
| 10 | Educational toast | LEARN | Highlighted muscle + Location / Origin / Insertion / Function card |
| 11 | Explore layers | EXPLORE | Skin / Muscle / Tendon / Bone layer chips |
| 12 | Muscle details | ANATOMY | Split view: whole body above, close-up below, origin/insertion/action chips |
| 13 | Muscle in action | ACTION | *(inferred)* elbow-flexion animation 0–145° with slider |
| 14 | Explore another muscle | DISCOVER | Deltoid card, tap hint, tap any muscle to switch |
| 15 | Learning complete | PROGRESS | Stats, Explore More (→14), Take Quiz |
| 16 | Quiz | QUIZ | *(extra)* 5 questions – the model highlights the muscle, you name it |

Screens 08 and 13 are missing from the shared Figma frames (the numbering jumps 07→09 and 12→14), so they were
designed to fit: 08 completes the Move/Zoom/Rotate/Tap gesture bar, 13 uses the "BICEPS BRACHII 145° FLEXION" callout visible in 15.

## Controls (Editor) / touch
| Action | Mouse / keyboard | Touch |
|---|---|---|
| Move (screens 05–07) | left-drag | one-finger drag |
| Rotate | left-drag (explore screens), right-drag, **Q / E** | drag or two-finger twist |
| Zoom | scroll wheel, **+ / –** | pinch |
| Select muscle | click | tap |
| Next / back | **→ / Space**, **←** (or the storyboard sidebar) | on-screen buttons |
| Live camera | **C** (or the camera button on screen 04) | same button |

A **storyboard sidebar** (01–16) is shown in the black margin of wide Game views; press **F1** to toggle it.

## The AR camera
* In the Editor the "camera feed" is a virtual anatomy lab with a walnut table. **C** switches to the computer's live
  webcam (`WebCamTexture`) as the background – on a phone it uses the rear camera; if no camera is available it falls back silently.
* Surface detection is **simulated** (`SurfaceScanner`): it animates a scan and always finds the virtual table. For real plane
  detection on a device, add AR Foundation + ARKit/ARCore and drive `SurfaceScanner.Detected` / the placement plane from `ARPlaneManager`.
  (No iOS/Android build modules are installed in this Unity install, so this build targets the Editor.)

## The human body (`Assets/Scripts/Anatomy`)
Built entirely from swept-ellipse meshes (`MeshFactory`) – 1.75 m, anatomical left = +X:
* **Skin** shell, **Muscle** layer (~60 named muscle bellies with fibre texture, tendons, tappable colliders: pectoralis major,
  deltoid (3 heads), biceps (2 heads), brachialis, triceps, forearm flexors/extensors, rectus abdominis with inscriptions and linea alba,
  obliques, serratus, trapezius, sternocleidomastoid, facial muscles, quadriceps, sartorius, adductors, TFL, glutei, tibialis, gastrocnemius, soleus, peroneus…),
  **Tendon** layer, **Bone** layer (skull, mandible, spine, 12 rib pairs + cartilage, sternum, clavicles, scapulae, humerus/radius/ulna, hands, pelvis, femur/tibia/fibula/patella, feet) and organs (heart, lungs, brain).
* The elbow is a real pivot: the forearm, hand and their tendons swing (screen 13).
* Muscle data (origin / insertion / function) lives in `MuscleDB.cs`.

## Code map
* `Core/AppFlow.cs` – bootstraps everything, owns the screen state machine and the phone viewport.
* `UI/ScreenBase.cs`, `ScreensEntry.cs`, `ScreensLearn.cs` – the 16 screens. `UIKit.cs` – procedural sprites/icons and widgets.
* `World/` – `LabEnvironment`, `CameraRig`, `SurfaceScanner`, `ModelController` (move/rotate/zoom/pick), `HoloLabels`, `InputRouter`, `PassthroughFeed`.
* `Editor/` – `ProjectSetup` (scene, player settings, game-view helper), `DevTools` (headless screenshots).
* QA: launch with `-qatour` to walk every screen and save screenshots to `Shots/`.
