# 影月碰撞与受击测试

当前版本是站立受击目标：实体碰撞、挥拳命中、短暂后仰、少量火花及轻微击退。尚未加入追击、主动攻击、血量或死亡动画。

## 加入场景

1. 退出 Play Mode，在 Hierarchy 中将 JapaneseStreetVR 设为活动场景。
2. 点击 `Kamen Rider > Setup Shadow Moon Combat Target`。
3. 选中新建的 `ShadowMoon Combat Target`，确认其位于街道地面上，没有嵌入墙体，再保存场景。
4. 进入 Play Mode，用控制器快速挥向影月身体。普通走路接触会产生物理碰撞，达到碰撞速度阈值时轻微后仰；只有手部有效命中触发打击事件。

菜单读取当前玩家的 OVRCameraRig，向左右 Hand Anchor 添加 `VrHandStrikeDetector`。不会为影月添加 OVRBody、RetargetingLayer 或 RigBuilder。影月使用独立的待机 Animator Controller，关闭 Root Motion。

重复运行菜单会保留已有受击目标并刷新手部绑定，不重复创建。生成的控制器和火花材质位于 `Assets/Characters/ShadowMoon/Combat`。场景修改支持 Undo；菜单不会自动保存场景。

## 调节效果

影月根对象的 `Shadow Moon Hit Receiver`：

| 参数 | 含义 |
| --- | --- |
| Knockback Speed | 打击施加给影月的水平速度变化；设为 0 可关闭打击击退，普通物理碰撞仍会推动它 |
| Recoil Degrees | 受击后仰幅度 |
| Recoil Duration | 后仰到恢复的总时长 |
| Hit Cooldown | 影月两次有效反馈的最短间隔 |
| Bump Speed Threshold | 玩家身体碰撞触发轻微后仰的最低相对速度 |
| Hit Clip | 可选的受击音效；默认留空，需自行指定合适素材 |

左右手 Anchor 的 `Vr Hand Strike Detector`：

| 参数 | 含义 |
| --- | --- |
| Minimum Strike Speed | 控制器自身运动速度阈值，默认 1.5 米/秒 |
| Hit Radius | 手部检测球半径，默认 0.09 米 |
| Local Hit Offset | 相对 Anchor 的检测位置微调；选中对象可在 Scene 中看到黄色检测球 |
| Rearm Speed | 命中后，手速必须降至此值以下才能准备下一拳 |
| Strike Cooldown | 同一只手两次成功命中的最短间隔 |
| Hit Layers | 默认检测所有层，包含环境遮挡；不要只保留敌人层，否则无法检测墙壁阻挡 |

按控制器自身速度判断挥拳，并在当前 Tracking Space 内重建扫掠路径，以排除摇杆平移和转身带来的路径变化。失去追踪、恢复焦点或位置突跳后，需先放慢手部再出拳。双手没有新增实体碰撞体，也不会给玩家施加打击力。

影月当前使用一个直立胶囊碰撞体，不是逐骨骼受击盒。后仰是视觉层反馈，碰撞体保持直立；手臂末端和大幅后仰后的外观可能不完全贴合碰撞体。后续可加入头、胸、四肢受击盒和专用动画。

## 不戴头显测试

进入 Play Mode，打开 `Shadow Moon Hit Receiver` 组件右键菜单，选择 `Test Hit (Play Mode)`。可反复观察后仰、火花和击退；`Reset Target (Play Mode)` 恢复本次运行的初始位置。

这只验证影月反馈，不能代替真实控制器的命中、墙壁阻挡和移动测试。依次检查：静止碰触不会连续打击；快速挥拳只命中一次；收手后可再次打击；单纯摇杆移动不会算成挥拳；碰墙后不能沿同一拳继续命中墙后目标；变身前后头手跟踪与原有音效仍正常。

## 后续扩展

`On Hit` 和 `On Player Bump` 是 Inspector 可配置的 UnityEvent。可以接入受击动画、扣血、特效或 AI 状态变化。接收事件的脚本可读取 `LastHitPoint`、`LastHitSpeed` 获取最近一次挥拳命中的位置与速度。碰撞事件不计为挥拳，不更新这两个值。
