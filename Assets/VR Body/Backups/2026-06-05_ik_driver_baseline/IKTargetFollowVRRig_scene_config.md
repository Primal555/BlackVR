# IKTargetFollowVRRig Baseline

Date: 2026-06-05

This backup records the simplified driver state after reverting the extra body anchor, pelvis, height lock, and dead-zone logic.

## Script

Backed up file:

- `Assets/VR Body/Backups/2026-06-05_ik_driver_baseline/IKTargetFollowVRRig.cs`

Original file at time of backup:

- `Assets/VR Body/IKTargetFollowVRRig.cs`

## Scene Component

Scene:

- `Assets/Scenes/JapaneseStreetVR.unity`

Component object:

- `Black`

Serialized component values visible in the scene file at backup time:

```yaml
turnSmoothness: 0.1
head:
  vrTarget: Head VR Target
  ikTarget: Head Target
  trackingPositionOffset: {x: 0, y: 0, z: 0}
  trackingRotationOffset: {x: 0, y: 0, z: 0}
leftHand:
  vrTarget: Left Hand VR Target
  ikTarget: Left Arm IK_target
  trackingPositionOffset: {x: 0, y: 0, z: 0}
  trackingRotationOffset: {x: 0, y: 0, z: 0}
rightHand:
  vrTarget: Right Hand VR Target
  ikTarget: Right Arm IK_target
  trackingPositionOffset: {x: 0, y: 0, z: 0}
  trackingRotationOffset: {x: 0, y: 0, z: 0}
headBodyPositionOffset: {x: 0, y: 0, z: 0}
headBodyYawOffset: 0
```

## Important Note

The simplified script currently drives the avatar root from `head.ikTarget.position`.
In the current hierarchy, `Head Target` is under the Black avatar rig, so this creates a feedback loop:

```text
Black root moves Head Target
Head Target drives Black root
```

This is the likely reason the avatar cannot be calibrated by offsets alone.
