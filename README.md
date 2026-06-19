# Kamen Rider VR

Unity VR project based on Meta XR SDK and Meta Movement SDK. The current main scene is a Japanese street VR scene with a tracked BlackFullBody character, joystick locomotion, first-person head hiding, and head shadow support.

## Requirements

- Unity `2022.3.9f1`
- Meta Quest / Quest Link compatible runtime
- Unity Hub with Android Build Support if building for Quest
- Network access to Unity Package Manager, GitHub, and Meta package sources

On first load, Unity needs to download packages listed in `Packages/manifest.json`. Some Meta packages and the Movement SDK package may require VPN or another network path that can access GitHub/Meta package resources.

Important packages include:

- `com.meta.xr.sdk.all` `71.0.0`
- `com.meta.movement` from `https://github.com/oculus-samples/Unity-Movement.git#v71.0.1`
- `com.unity.animation.rigging` `1.2.1`
- `com.unity.render-pipelines.universal` `14.0.8`
- `com.unity.inputsystem` `1.13.1`

## Main Scene

Open this scene:

`Assets/Scenes/JapaneseStreetVR.unity`

This is the scene used for the final working configuration.

## Current Character Setup

The active character setup uses `BlackFullBody` with Meta Movement retargeting and joystick locomotion.

Key behavior:

- `PlayerController` drives movement through `MovementSDKLocomotion`.
- `OVRCameraRig` is under `PlayerController` so the player viewpoint follows locomotion.
- `MovementSdkOvrThumbstickInput` reads OVR thumbstick input and writes it to `MovementSDKLocomotion.UserInput`.
- `BlackFullBody` uses full-body retargeting.
- `RigBuilder` on `BlackFullBody` is intentionally disabled. Do not enable it unless you are deliberately changing the retargeting pipeline.
- A separate locomotion skeleton processor is used so `BlackFullBody` does not fight the official sample body for the same processor resources.

## Known Fixes Preserved In This Version

- Body tracking and joystick locomotion work together in `JapaneseStreetVR`.
- `BlackFullBodySkinnedMeshCullingFix` keeps the skinned mesh visible after locomotion moves the body away from its original bounds.
- Foot deformation alignment is disabled with `_alignFeetWeight: 0` to prevent ankle twisting when turning.
- First-person head hiding uses `BlackFullBodyFirstPersonHeadHider`.
- `FirstPersonNoDraw` hides the head from the player camera while still supporting a ShadowCaster pass, so the head can cast shadows.
- The visible head clone is placed on the `HiddenMesh` layer and is culled from first-person cameras.

## First Load Checklist

1. Open the project with Unity `2022.3.9f1`.
2. Make sure VPN/network access is available before Unity resolves packages.
3. Wait for Unity Package Manager to finish importing all packages.
4. Open `Assets/Scenes/JapaneseStreetVR.unity`.
5. Connect Quest / Quest Link and ensure the Meta XR runtime is active.
6. Enter Play Mode and test:
   - thumbstick movement
   - head and hand tracking
   - BlackFullBody visibility while moving
   - foot orientation while turning
   - first-person head hiding and head shadow

## Notes

- If the scene opens with missing packages, fix package download/network issues first before changing scene objects.
- If tracking stops working, check Meta XR runtime status and body tracking settings before modifying the character rig.
- If black screen or hourglass appears after editing the character, verify that `BlackFullBody` RigBuilder has not been re-enabled.
- If the body disappears after moving, check that `BlackFullBodySkinnedMeshCullingFix` is still attached and enabled.
- If ankles twist while turning, check that the deformation setting `_alignFeetWeight` remains `0`.

