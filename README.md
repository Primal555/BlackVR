# Kamen Rider VR

这是一个基于 Unity、Meta XR SDK 和 Meta Movement SDK 的 VR 体感项目。当前主场景为 Japanese Street VR，角色身体使用 Meta Movement 的 retargeting 方案驱动，支持头显与双手控制器跟踪、摇杆移动、动作音效、离线语音指令识别、Black 变身切换，以及第一人称头部隐藏但保留阴影投射。

## 环境要求

- Unity `2022.3.9f1`
- Meta Quest / Quest Link 兼容运行环境
- 如需打包到 Quest，需要安装 Android Build Support
- 首次打开项目时，需要能够访问 Unity Package Manager、GitHub 和 Meta 相关包源

首次加载项目时，Unity 会根据 `Packages/manifest.json` 下载依赖包。部分 Meta 包和 Movement SDK 包可能需要 VPN 或其它能够访问 GitHub / Meta 包源的网络环境。

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
6. 如果 Meta XR Project Setup Tool 出现配置警告，等待包导入完成后点击 `Fix All`。
7. 进入 Play Mode 后测试摇杆移动、头显/双手跟踪、声控和变身切换。

## 当前玩法

- 左摇杆：驱动 `PlayerController` 移动。
- 头显与双手控制器：驱动角色身体、头部和双手跟踪。
- 快速挥动控制器：播放 Black 动作音效。
- 同时按下左右手的 index trigger 和 hand trigger：播放 Black 握拳音效。
- 喊出已录制的变身语音模板，例如 `henshin` / `black变身音效` 对应模板：播放变身音效，并把角色无缝切换到 `BlackFullBody`。
- 变身后按左手 Y 键：播放变身音效，并恢复到变身前模型。

## 离线声控说明

当前声控没有使用联网服务，也没有使用 `UnityEngine.Windows.Speech.KeywordRecognizer`。语音识别走本地模板匹配流程：

- `VoiceTemplateCommandRecognizer` 使用 Unity `Microphone` 采集声音。
- 录制好的语音模板位于 `Assets/Audio/VoiceCommandTemplates/`。
- 识别到命令后，会播放绑定音效，并通过事件留出后续扩展空间，例如动画、粒子、模型替换等。
- `HenshinModelSwitcher` 监听 `black变身音效` 命令，负责变身模型切换。

重要参数：

- `voiceThreshold`：判断当前是否有人声输入。太低容易被环境噪声触发，太高会漏掉较轻的声音。
- `silenceToEndSeconds`：低于音量阈值持续多久后，认为一句话已经结束。
- `minUtteranceSeconds` / `maxUtteranceSeconds`：限制有效语音片段的最短和最长时间。
- `recognitionCooldownSeconds`：识别成功后的冷却时间，防止同一句话连续触发。
- `defaultMinSimilarity`：模板匹配最低相似度。太低容易误触发，太高容易识别失败。

## 角色控制结构

当前启用的主要角色结构如下：

- `PlayerController` 通过 `MovementSDKLocomotion` 负责移动。
- `OVRCameraRig` 位于 `PlayerController` 下，使玩家视角跟随 locomotion。
- `MovementSdkOvrThumbstickInput` 读取 OVR 摇杆输入，并写入 `MovementSDKLocomotion.UserInput`。
- `BlackFullBody` 使用 full-body retargeting。
- `BlackFullBody` 上的 `RigBuilder` 是故意禁用的，不要重新启用，否则可能导致黑屏、小沙漏或身体跟踪异常。
- `BlackFullBody` 使用独立 locomotion skeleton processor，避免和官方 sample body 抢占同一套 processor 资源。
- `HenshinModelSwitcher` 只切换 Renderer 可见性，不关闭角色 GameObject，避免重启 OVRBody / Retargeting / Animator。

## 关键修复记录

- `JapaneseStreetVR` 中的身体跟踪和摇杆移动可以同时工作。
- `BlackFullBodySkinnedMeshCullingFix` 用于扩大 skinned mesh bounds，避免角色移动一段距离后被错误剔除而消失。
- 脚部 deformation alignment 已关闭，关键配置为 `_alignFeetWeight: 0`，用于避免转身时脚踝异常扭曲。
- 第一人称头部隐藏由 `BlackFullBodyFirstPersonHeadHider` 实现。
- `FirstPersonNoDraw` 让第一人称相机看不到头部，同时保留 ShadowCaster pass，使头部仍能投射阴影。
- 变身前隐藏模型时会强制 Animator `AlwaysAnimate`，避免不可见 Renderer 导致骨骼/跟踪链路被裁剪。
- 左手 Y 已从 Jump 绑定中解绑，专门用于变身后的解除变身。

## Meta XR 配置提示

首次从 git clone 或 zip 解压后打开项目时，Meta XR Project Setup Tool 可能会显示一些配置警告或报错。这通常是正常现象，不一定说明项目文件缺失。

原因是项目不会提交 Unity 自动生成的 `Library/` 缓存，而 Meta XR 的一部分检查依赖当前机器、Unity Editor、Android Build Support、Quest / Link runtime 和 Package 导入状态。换电脑或重新 clone 后，Unity 会重新导入资源、重新下载包，并重新运行 Meta XR 的项目配置检查。

建议处理方式：

1. 先等待 Unity Package Manager 完成所有包下载和导入。
2. 打开 Meta XR Project Setup Tool。
3. 如果仍有配置警告，点击 `Fix All`。
4. 完成后再进入 Play Mode 测试。

如果包还没有导入完成就立刻运行 `Fix All`，部分提示可能会反复出现。优先确认网络和依赖包下载正常。

## 注意事项

- 不要提交 `Library/` 缓存。它体积很大，并且依赖本机 Unity 环境；fresh clone 后让 Unity 重新下载和导入依赖包更稳定。
- 如果场景打开后显示缺少 package，请先解决网络和包下载问题，不要急着修改场景对象。
- 如果头显或手柄跟踪失效，优先检查 Meta XR runtime、Quest / Link 连接状态和 body tracking 设置。
- 如果进入 Play Mode 后出现黑屏或小沙漏，检查 `BlackFullBody` 的 `RigBuilder` 是否被重新启用。
- 如果角色移动后身体消失，检查 `BlackFullBodySkinnedMeshCullingFix` 是否仍然挂载并启用。
- 如果转身时脚踝扭曲，检查 deformation 配置中的 `_alignFeetWeight` 是否仍为 `0`。
