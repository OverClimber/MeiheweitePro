# 第三方组件与许可

本工程为二次开发作品，包含下列第三方组件。各组件的版权归其各自作者所有，本工程不主张任何权利。

> 本页为**摘要**，不替代各组件原始许可文本。再分发时请一并保留本页与各组件的原始声明。

---

## 一、工程基座

| 组件 | 协议 | 说明 |
| --- | --- | --- |
| **YGOProUnity_V2** | **GNU GPL v3** | 本工程的直接上游基座（Unity 版 YGOPro v2 客户端）。全文见 [`LICENSE`](LICENSE) |

---

## 二、决斗内核与机器人

| 组件 | 协议 | 版权 | 在本仓库的位置 |
| --- | --- | --- | --- |
| **ocgcore**（ygopro-core） | **MIT** | Copyright (c) 2015 Fluorohydride | 预编译二进制 `Assets/Plugins/x64/ocgcore.dll`、`Assets/Plugins/x86/ocgcore.dll`；C++ 源码工程不随本仓库分发 |
| **WindBot** | **MIT** | Copyright (c) 2015-2017 IceYGO | 不随本仓库分发，见发布包内 `WindBot/LICENSE` |

ocgcore 的 MIT 许可要求：在软件的所有副本中保留上述版权声明与许可声明。

---

## 三、Unity 插件

| 组件 | 授权方式 | 版权 / 作者 | 位置 |
| --- | --- | --- | --- |
| **NGUI**（Next-Gen UI） | **Unity Asset Store 商业插件** | Tasharen Entertainment | `Assets/NGUI/` |
| **DOTween / DOTween Pro / DemiLib** | **Unity Asset Store 商业插件** | © 2014-2018 Daniele Giardini — Demigiant | `Assets/Plugins/Demigiant/` |
| **Highlighting System** | Unity Asset Store 插件 | — | `Assets/Plugins/high_light_plugin/` |

> ⚠ **注意**：以上三项为 Unity Asset Store 分发内容，随上游工程继承而来，**不属于** 本工程的 GPL-3.0 授权范围。
> Unity Asset Store 标准 EULA 允许将其用于已构建的产品，但对**原始资源文件本身的再分发**有额外限制。
> 若你计划以公开仓库形式发布本源码，建议自行确认上述插件的授权状态，或改为不随仓库提供、由使用者自行导入。

---

## 四、程序库

| 组件 | 协议 | 位置 |
| --- | --- | --- |
| **ICSharpCode.SharpZipLib** | MIT | `Assets/Plugins/ICSharpCode.SharpZipLib.dll` |
| **DotNetZip**（`Ionic.Zip.Unity.dll`） | MS-PL | `Assets/Plugins/Ionic.Zip.Unity.dll` |
| **SQLite**（`sqlite3.dll`） | Public Domain | `Assets/Plugins/{x64,x86}/sqlite3.dll` |
| **Mono 运行库**（`Mono.Data.Sqlite.dll`、`System.Data.dll`、`System.Drawing.dll`、`I18N.*.dll`） | MIT / MS-PL | `Assets/Plugins/` |
| **7-Zip**（插件形式集成） | LGPL-2.1（含 unRAR 限制条款） | `Assets/Plugins/7zip/` |
| `MeshExtrusion.cs`、`Perlin.cs` | Unity 社区公开脚本 | `Assets/Plugins/` |

---

## 五、美术与音频素材

`Assets/ArtSystem/`、`Assets/old/`、`Assets/face/`、`Assets/transUI/`、`Assets/modernUI/`、`Assets/Icon/` 等目录中的贴图、粒子与预制体，来源包括 Unity Asset Store 免费/商业素材包、公共素材站及上游工程自带内容，**随上游工程继承**。

这些素材的授权状态未在本工程内逐项标注，**不属于** GPL-3.0 授权范围。若对其中某项素材的授权有疑问，请自行核实或从发布内容中移除。

---

## 六、卡片数据与卡图（不含在本仓库）

卡片数据库、效果脚本、卡图与音效等资源的版权归 **KONAMI** 及卡牌数据社区所有，本项目不主张任何权利，亦不以本仓库形式再分发。使用者需自行准备。

---

## 七、免责声明

《游戏王》（Yu-Gi-Oh!）为 KONAMI 的注册商标与著作权作品。本工程与其无任何关联，未获其授权或认可，**仅用于技术学习与交流，不作商业用途**。
