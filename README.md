# Kamen Rider VR

这是一个基于 Meta XR SDK 和 Meta Movement SDK 的 Unity VR 项目。当前主场景是 Japanese Street VR，包含可被头显和手柄跟踪的 `BlackFullBody` 角色、摇杆移动、第一人称头部隐藏，以及头部阴影投影支持。

## 环境要求

- Unity `2022.3.9f1`
- Meta Quest / Quest Link 兼容运行环境
- 如果需要打包到 Quest，需要在 Unity Hub 中安装 Android Build Support
- 首次打开项目时，需要能够访问 Unity Package Manager、GitHub 和 Meta 相关包源

首次加载项目时，Unity 会根据 `Packages/manifest.json` 下载依赖包。部分 Meta 包和 Movement SDK 包可能需要 VPN 或其他可访问 GitHub / Meta 包源的网络环境。

主要依赖包括：

- `com.meta.xr.sdk.all` `71.0.0`
- `com.meta.movement`，来源为 `https://github.com/oculus-samples/Unity-Movement.git#v71.0.1`
- `com.unity.animation.rigging` `1.2.1`
- `com.unity.render-pipelines.universal` `14.0.8`
- `com.unity.inputsystem` `1.13.1`

## 主场景

打开以下场景：

`Assets/Scenes/JapaneseStreetVR.unity`

这是当前最终可用配置所在的主场景。

## 首次打开项目

1. 使用 Unity `2022.3.9f1` 打开项目。
2. 在 Unity 解析包之前，确保当前网络可以访问 Unity Package Manager、GitHub 和 Meta 包源。
3. 等待 Unity Package Manager 完成所有依赖包下载和导入。
4. 打开 `Assets/Scenes/JapaneseStreetVR.unity`。
5. 连接 Quest / Quest Link，并确认 Meta XR runtime 正常运行。
6. 进入 Play Mode 后测试：
   - 摇杆移动
   - 头显和双手控制器跟踪
   - `BlackFullBody` 移动时是否保持可见
   - 转身时脚腕方向是否正常
   - 第一人称头部是否隐藏
   - 头部是否仍然能投射阴影

## Meta XR 配置提示

首次从 git clone 或 zip 解压后打开项目时，Meta XR Project Setup Tool 可能会显示一些配置警告或报错。这通常是正常现象，不一定说明项目文件缺失。

原因是项目不会提交 Unity 自动生成的 `Library/` 缓存，而 Meta XR 的一部分检查依赖当前机器、Unity Editor、Android Build Support、Quest / Link 运行环境和 Package 导入状态。换电脑或重新 clone 后，Unity 会重新导入资源、重新下载包，并重新运行 Meta XR 的项目配置检查。

建议处理方式：

1. 先等待 Unity Package Manager 完成所有包下载和导入。
2. 打开 Meta XR Project Setup Tool。
3. 如果仍有配置警告，点击 `Fix All`。
4. 完成后再进入 Play Mode 测试。

如果包还没有导入完成就立即运行 `Fix All`，部分提示可能会反复出现。优先确认网络和依赖包下载正常。

## 当前角色控制结构

当前启用的角色是 `BlackFullBody`，它使用 Meta Movement retargeting 和摇杆 locomotion。

关键结构：

- `PlayerController` 通过 `MovementSDKLocomotion` 负责移动。
- `OVRCameraRig` 位于 `PlayerController` 下，使玩家视角跟随 locomotion。
- `MovementSdkOvrThumbstickInput` 读取 OVR 摇杆输入，并写入 `MovementSDKLocomotion.UserInput`。
- `BlackFullBody` 使用 full-body retargeting。
- `BlackFullBody` 上的 `RigBuilder` 是故意禁用的。除非明确要重做 retargeting 流程，否则不要重新启用。
- `BlackFullBody` 使用独立的 locomotion skeleton processor，避免和官方 sample body 抢占同一套 processor 资源。

## 当前版本保留的关键修复

- `JapaneseStreetVR` 中的身体跟踪和摇杆移动可以同时工作。
- `BlackFullBodySkinnedMeshCullingFix` 用于扩大 skinned mesh bounds，避免角色移动一段距离后被错误剔除而消失。
- 脚部 deformation alignment 已关闭，关键配置为 `_alignFeetWeight: 0`，用于避免转身时脚腕异常扭曲。
- 第一人称头部隐藏由 `BlackFullBodyFirstPersonHeadHider` 实现。
- `FirstPersonNoDraw` 会让第一人称相机看不到头部，同时保留 ShadowCaster pass，使头部仍能投射阴影。
- 可见头部克隆体位于 `HiddenMesh` layer，并会被第一人称相机剔除。

## 注意事项

- 如果场景打开后显示缺少 package，请先解决网络和包下载问题，不要急着修改场景对象。
- 如果头显或手柄跟踪失效，优先检查 Meta XR runtime、Quest / Link 连接状态和 body tracking 设置。
- 如果进入 Play Mode 后出现黑屏或小沙漏，检查 `BlackFullBody` 的 `RigBuilder` 是否被重新启用。
- 如果角色移动后身体消失，检查 `BlackFullBodySkinnedMeshCullingFix` 是否仍然挂载并启用。
- 如果转身时脚腕扭曲，检查 deformation 配置中的 `_alignFeetWeight` 是否仍为 `0`。
- 不建议把 `Library/` 缓存提交到 git。它体积很大，而且依赖本机 Unity 环境；fresh clone 后让 Unity 重新下载和导入依赖包更稳定。

