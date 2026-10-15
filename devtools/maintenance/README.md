# 项目维护与验证

## 2026-10-12 卡组拖拽验证

`drag_geometry_gate.py` 在隔离 Unity 编辑器加载当批 DLL，测试相机投影、槽位、列表迁移和单击松手；源码在 `devtools/tests/DeckDragUnityTests.cs`。`deck_runtime_smoke.py` 在已有独立播放器验证 OCG/RD 编辑器数量、顺序和槽位，不模拟人工拖拽，结束后保留候选供试用。二者任何失败均非零退出；正式运行目录不变。记录见 `_takeover_audit/drag_20261012/维护记录.md`。

`stage_android.py` 支持 `MEI_ANDROID_FIRSTPASS` 指定已验证的 Android firstpass；其同目录须有 StbImageSharp.dll。原工作副本不可用时可用保存的移动依赖，不能换成桌面 firstpass。`MEI_ANDROID_EDITOR` 仍指定编译参考编辑器；源码编译不等于 Android 完整构建或真机验收。

## 2026-10-11 撤回性能验证

`undo_performance_gate.py` 提取当前生产撤回、网络调度和自动开局方法，编译运行协议夹具，覆盖两万步历史、深落点、分叉、目标不代答、超时与旧房间协程失效。它不运行 Unity/原生对局，毫秒输出不能作为游戏性能结论。

`gameplay_case.py` 可传 `--artifacts 候选目录 --runtime 独立播放器目录 --pid 进程号`，核对候选 DLL 哈希后保存当次结果。每次卡组测试返回编辑器后，应重启独立播放器再跑下一场；编辑器不消费主菜单 shell 命令。正式 Windows 目录被拒绝。当前候选、基线、Android 补丁链证据与实际短局耗时见项目根 `_takeover_audit/undo_performance_20261011/维护记录.md`；没有替换正式运行目录。

优先级：功能保持 > 性能 > 美观。项目根 `AGENTS.md` 是后续维护规则。

## 权威目录

- `YGOProUnity_V2/Assets`：桌面源码；Unity 版本以 `ProjectSettings/ProjectVersion.txt` 为准。
- `YGOProAndroid/LAYER.lock.json`、`layer`、`patches`：安卓固定基线与增量层。不要覆盖用户已有修改，也不要将桌面 Program/Menu 整文件覆盖到安卓。
- `devtools/maintenance`、`devtools/tests`：可重跑工具与测试源码。
- 根 `_takeover_audit/implementation_日期`：本批日志、编译产物、验证清单；不是正式安装目录。

## 命令

从项目根运行，Python 路径使用本机已配置的 Python：

```text
python YGOProUnity_V2/devtools/maintenance/run.py
python YGOProUnity_V2/devtools/maintenance/stage_android.py
python YGOProUnity_V2/devtools/maintenance/run.py --runtime
```

第一条执行严格补丁检查、桌面源码编译与生产代码故障测试，**不代表对局回归通过**。
第二条从固定安卓基线构建隔离源码，逐条严格应用 C# 补丁并执行移动编译闸；不修改外部工作副本，也不代表 APK/真机通过。
第三条额外要求原有四项真实运行回归。当前仓库缺少四个验证脚本，命令必须返回失败。旧日志不作为当前 DLL 的通行证；完整发布验收仍要求恢复验收源码并完成实际执行。2026-10-11 用户实测独立候选后明确要求合并到本地 Windows 目录，本次按该授权部署同一已验证产物，记录有限覆盖范围，不将完整发布验收改成通过。
`--generate-maintenance-patch` 仅用于维护本批 `0019` 补丁：它保留安卓专有启动、触控与解码差异，锚点漂移即失败。

现有编译辅助脚本仍有本机 Unity/参考 DLL 路径依赖。桌面编辑器路径可通过 `MEI_DESKTOP_EDITOR` 指定，移动线通过 `MEI_ANDROID_EDITOR` 指定。运行失败应先修配置，禁止为制造绿灯减少源码或跳过错误。

## 实际运行验收

必须使用独立副本与本批 DLL 哈希，记录测试时间、设备、日志和截图：

1. OCG/RD 菜单、卡组检索、卡图/立绘、译名与模式来回切换。
2. OCG 人机与快速测试开局、卡组备注、撤回重建、预览取消/确认、结束后正确返回。
3. RD 人机开局、极大合体/单体立绘、连续多局与撤回。
4. 本地 HTTP 故障场景（成功混合 404、503、断网、写盘失败），用户卡图在下载途中出现时保持不覆盖。
5. 安卓首次安装、分片损坏/中断、重试、已有用户资源保留与设备实际开局。

`MaintenanceTests` 使用真实 C# 辅助类验证事务恢复、缓存分类/寿命、ZIP 优先级与过期队列结果；不能代替 Unity 协程、GPU、玩法和 Android 文件系统测试。

`prepare_runtime.py` 在闸通过后创建独立播放器副本，不使用链接，也不覆盖已存在的运行副本。保留证据后先清理再重建。

## 清理与发布

每批先保存受影响文件的基线和哈希；完成后保留源码、清单、必要日志和候选 DLL。
临时运行副本、安卓重建目录、故障夹具和编译依赖副本可用 `clean.py --execute` 清理；它只允许删除本批明确列举的目录/文件，并检查绝对路径位于指定产物目录内。
不要删除历史用户资料、卡组、录像、自定义包/卡图、`Assets/old` 或仍有效的 OCG/RD 资源。
测试失败或缺少运行证据时，正式安装目录保持原版本；后续部署必须重新验证当前源码与产物哈希。

ZIP 索引、事件唤醒和 CPU 缓冲释放已实现。缓存 LRU、上传帧预算、GC 改动与全局 UI 改造等待真实性能/交互基线，不能按未测假设直接上线。

## 后续问题复测工具

`export_issue_patch.py` 从保存的桌面基线生成 `0021` 共享玩法修复；`0022` 只适配安卓本地服务器的经典连锁消息。新增补丁后运行 `stage_android.py` 检查整个固定补丁链。

`gameplay_case.py --mode OCG|RD --kind ai|test --deck 卡组名 --replay-count 10` 验证已启动的独立桌面候选。启动前创建仅用于此副本的 `log/qt_debug.on`（内容为有效秒数），启动时限已过应重新启动，不借用旧轨迹。工具核对候选 DLL 哈希，验证真实开局、开局消息重放、返回位置；`[err]` 或重放分叉必须失败。开局重放不能代替人工预览/确认或完整一局验收。

`prepare_android_player.py --resume`、`build_android_player.py` 仅处理带本项目 owner 标记的独立构建目录；测试包名与正式包名不同。`verify_apk_native.py` 对照构建输入检查包内八个原生库。`device_gameplay_case.py` 使用已连接设备的独立测试应用，核对已安装 APK 哈希、记录每次重放后的 meminfo；`--leave-duel` 可保留对局进行手动 UI 验证，此时不声称结束返回已通过。上述工具目前保留本机 SDK/设备及证据目录配置，迁移机器前需要更新配置。

`native_script_smoke.py` 在真实桌面核心初始化官方补缺 Lua；`field_texture_gate.py` 在真实 Unity 对实际场地图逐像素比较新旧切片。二者都在结果中注明覆盖范围，不据此声称整场对局或所有卡片效果分支通过。

`native_script_conditions.py` 读取维护故障测试导出的生产兼容代码，在真实桌面核心复现未修复的塞尼特接口错误，再验证接口、通常怪兽筛选、盈彩月夜代价筛选及已有实现保留。先运行 `run.py`；这些条件测试不覆盖所有效果结算分支。

`native_script_smoke.py --installed` 使用独立播放器实际的扩展包、基础 ZIP 与补缺散装脚本进行初始化检查；默认模式使用官方下载的脚本夹具。

`native_script_activation.py` 使用实际已安装脚本和注册到核心的 Effect，调用原生 `IsActivatable` 检查六张塞尼特卡和盈彩月夜的 19 个正/反例。它检查代价、对象与发动资格，不代替实际事件触发、完整连锁结算和 GUI。`--profile formal` 只读检查正式 Windows 输出，部署后应在该目录通过；部署前旧版本失败的证据已归档。

`card_validation_gate.py` 先核对当前源码与已编译候选哈希，再顺序执行实际安装脚本初始化、发动资格、接口兼容及脚本包内容保留检查。任一错误必须非零退出，结果与日志保存在维护证据目录；不会改正式资源。新候选编译后需先同步独立运行副本，再运行此闸，不能借旧 DLL 获得通过。

`verify_script_compatibility.py` 比较独立运行目录的原脚本包备份和兼容后的包，核对每个条目的原始字节，只允许 utility.lua 追加生产兼容代码。`gameplay_matrix.py` 汇总指定候选哈希的开局重放检查；它不替代完整发布验收。

设备暂不可用时，可运行 `gameplay_matrix.py --platform desktop` 生成明确限定电脑端的汇总。默认仍要求两端当前候选都通过；游戏日志的重建耗时与工具总耗时分别列出。

可用 `clean.py --execute --only compile-dependencies --only fault-fixture --only patch-fixture --only android-desired` 清理本批重复依赖和过程夹具，保留运行副本、候选、哈希与日志。兼容代码测试导出独立保留，原生条件测试无需被删夹具；再跑完整故障测试由 `run.py` 恢复所需依赖。

## Windows 本地部署与回滚

`deploy_windows.py --date 20261011` 默认只预览。用户明确授权合并测试版本后，用 `--execute` 部署同一候选：核对源码、DLL、依赖和资源基线，重新运行卡牌验证，只替换 DLL、兼容脚本包并补齐缺失脚本。保留原文件、逐文件安装日志和无关文件清单；正式目录的原生检查失败时自动恢复。不能在播放器运行时执行。

`deploy_windows.py --date 20261011 --rollback` 从本批备份恢复，发现后续用户改动就拒绝覆盖。备份和部署证据在 `_takeover_audit/windows_deployment_20261011`。此部署不更改安卓正式应用，也不代表完整发布验收通过。

`native_script_smoke.py --installed --all-loose` 检查独立候选全部散装卡牌脚本；加 `--profile formal` 检查 Windows 正式目录。没有测试脚本、卡表缺卡、脚本缺失或任何 Lua 错误都必须失败。此检查仍只覆盖初始化。
