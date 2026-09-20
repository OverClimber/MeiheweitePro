# RD（超速决斗）数据分发布局与源包清单

_2026-09-19 建档。这份清单解决一件事：**RD 的运行目录不在版本控制里，换机器 / 清空 `output/` 后怎么把它重造出来。**

## 为什么要有这份文件

- RD 的源码（`GameModeManager` 等 15 个文件）在 `feature/rush-duel` 分支里，随 git 走，没问题。
- RD 的**数据**（卡表 / 禁限表 / 卡图 / 立绘，合计约 800 MB）**不进 git**，
  只落在产物侧 `output/Windows/rd/`，而 `/output/` 被 `.gitignore` 整个忽略。
- `rd/` **不在** `Assets/Editor/BuildHelper.RuntimeDataDirectories` 里（特意的：770 MB 不随每次构建 `File.Copy`）
  ⇒ **重建 `output/` 之后必须重跑 `devtools/unpack_rd.py`**，否则 RD 模式进去是空卡池。

## 源包清单（2026-09-17 收集，2026-09-19 实测指纹）

源目录：`D:\Game\试行pro2\参考内容\RD\`（工作区级目录，非 git；`--src` 可指向别处）

| 源包 | 字节数 | MD5 | 用途 |
|---|---|---|---|
| `RD正式卡.ypk` | 678666934 | `baa0cc89a9ad9824b212b071141941d4` | 正式卡 3324 张 → `rd_standard.cdb`；卡脚本 2905 个 → `rd/ai/script/`（顺手出卡图） |
| `RD先行卡 (15).ypk` | 30899139 | `a580c7fc294f5ffff44ff9c9a675476e` | 先行卡 76 张 → `rd_patch.cdb`；卡脚本 66 个 |
| `RD异画卡.ypk` | 19068646 | `ab95130d250bba405fc85ee463197469` | 异画 84 张 → `rd_alternate.cdb`；卡脚本 83 个（全是 `RD.AlternateCard` 一行，生成期内联） |
| `2026.7 禁限表RD补丁+使用说明 (2).zip` | 3702 | `e6300e1e1c666d01f7e47e6f31ba2b30` | 包内 `2026.7 RD 禁卡表补丁+使用说明/lflist.conf` → `rd/lflist.conf` |
| `【Pro2专用】RD立绘补丁.zip` | 78648770 | `400dfe50afcd9c9f1433bbf5dedc9721` | 立绘 → `rd/picture/closeup/`（`--all` 才铺） |
| `电脑版RD客户端.zip` | 840169284 | `5a5963f84731cb17be244e0374f25693` | RD 规则库 22 个 lua + **RD 版 WindBot** + RD `bot.conf` → `rd/ai/`（人机包，见下节） |

参照物（不入产物，仅供查证）：`RUSH DUEL基本规则 2025.4.pdf`（规则口径）、`rd服务器.txt`（rd 服地址）。

⚠ `电脑版RD客户端.zip` 在**工作区** `参考内容/` 下，**不在** `参考内容/RD/` 里 —— 所以它有自己一条默认路径
（脚本里 `DEFAULT_CLIENT`），也可以 `--client` 指定；`--client` 接受 zip，也接受已经解开的目录
（调试生成逻辑时解开一次、反复跑，比每次开 800 MB 的 zip 省事）。

三份 cdb 的 id 域互不重叠（正式 120000000~120308006、先行、异画各自独立），故装载顺序不影响身份。

## 落地布局（产物侧 `output/Windows/rd/`）

### 卡表 / 禁限表 / 图（客户端查询层用）

| 路径 | 来源 | 消费方 |
|---|---|---|
| `rd/cdb/rd_standard.cdb` | `RD正式卡.ypk` 内 `RD Standard.cdb` | `CardsManager` 的 RD 池 |
| `rd/cdb/rd_patch.cdb` | `RD先行卡 (15).ypk` 内 `RD Patch.cdb` | 同上 |
| `rd/cdb/rd_alternate.cdb` | `RD异画卡.ypk` 内 `RD Alternate.cdb` | 同上 |
| `rd/lflist.conf` | 禁限表 zip 内 `…/lflist.conf` | `BanlistManager` 的额外来源（RD 表） |
| `rd/picture/card/` | 三份 ypk 的 `pics/` 条目（3526 张） | `GameModeManager.CardPictureDir` |
| `rd/picture/closeup/` | `【Pro2专用】RD立绘补丁.zip`（1314 张） | `GameModeManager.CloseupPictureDir` |
| `rd/rd_source.txt` | 脚本运行时生成 | 排查「这份 rd/ 是哪来的」 |

### 人机包（`rd/ai/` —— RD 本地 AI 对局用）

| 路径 | 来源 | 消费方 / 为什么是这个位置 |
|---|---|---|
| `rd/ai/AI.Server.exe` | **产物根**那份 core 的拷贝 | `AIRoom.launch()` 以 `cwd = rd/ai` 起它。必须拷：core 的脚本/卡表路径全是相对 cwd 的（`./script/*.lua`、`./cdb/cards.cdb`、`config/lflist.conf`），而 RD 规则脚本与 OCG 那套不能共存 |
| `rd/ai/script/` | RD 客户端 `script/`（22 个规则库）+ 三份 ypk 的 3054 个卡脚本 | 同上。`utility.lua` 是**生成**的（RD 原文 + 内联 17 个规则库 + 兼容引导），`special.lua` 是**桩**（原文另存 `special.lua.txt`） |
| `rd/ai/cdb/cards.cdb` | 上面三份 RD cdb **合并**（3484 行） | AI.Server 只认 `cards.cdb` 这一个名字；WindBot 读 `../cdb/cards.cdb` —— 两边共用这一份 |
| `rd/ai/config/lflist.conf` | = `rd/lflist.conf` | AI.Server 读 `config/lflist.conf`。与客户端用同一份，两侧禁限口径才不会打架 |
| `rd/ai/WindBot/` | RD 客户端 `WindBot/`（exe + dll + Decks 9 + Dialogs 24 + pdb） | `AIRoom.launch()` 以 `cwd = rd/ai/WindBot` 起它。它读自己目录的 `Decks/`、`Dialogs/`，并读 `../cdb/cards.cdb` ⇒ 正好落在 `rd/ai/cdb/` |
| `rd/bot.conf` | RD 客户端 `bot.conf`（8 个可用机器人） | `AIRoom` 读（`GameModeManager.BotConf`）。OCG 仍读 `config/bot.conf` |
| `rd/replay/` | **运行期自建**（首次录/列时 `Directory.CreateDirectory`），unpack_rd.py 不铺 | RD 局录像的落盘与回放目录（`GameModeManager.ReplayDir`）。OCG 仍用产物根 `replay/`，两边互不看见 |

C# 侧对应的路径常量在 `Assets/SibylSystem/GameModeManager.cs` 的「人机（AI.Server + WindBot）路径」段，
与脚本里的 `AI_*` 常量、上表**必须逐字一致**；改一处要同时改三处。

#### 规则库为什么要「内联 + 兼容引导」

RD 的规则是**纯 lua** 实现的（`RDRule.lua` 用 `EFFECT_DISABLE_FIELD` 关掉最外两列、`EFFECT_DRAW_COUNT`
抽到 5 张、`EFFECT_SKIP_SP` 跳准备阶段……），不需要「RD 版 core」。但 RD 规则库假定 core 提供两样东西，
而 AI.Server.exe（以及我们客户端的 `ocgcore.dll`）**都没有**：

1. `Duel.LoadScript(name)` —— 二进制里 "LoadScript" 命中 0 次（只有 RD 端的 `ygopro.exe` 有）；
2. 决斗开始时调用 `Auxiliary.PreloadUds()` —— "PreloadUds" 命中 0 次。

原版 `special.lua` 又是 core **最先**加载的脚本，那时连 `Duel` 都还没有，于是 `Duel.LoadScript("RDBase.lua")`
必然失败 ⇒ `RushDuel` 永不建立 ⇒ 卡脚本里的 `RD.*` 全是 nil
（现象：`attempt to index a nil value (global 'RD')`）。

兼容层（源码 `devtools/rd_ai/bootstrap.lua`，入 git）做两件事：**生成期整段内联**规则库（不依赖运行时加载），
**时机改挂在 `GetID()`**（每张卡脚本第一句，本 core 上唯一「每局必走到、此刻 duel 已存在、`RegisterEffect` 可用」
的 lua 入口），在那里 `RD = RushDuel; RushDuel.Init()`。

验收：工作区 `_verify_rdai.py`（不在这棵 git 树里）。它把 `rd/ai/` 拷成副本、只在副本里插两个探针，
用**产物真字节**打一局，判据是 **怪物区/魔陷区各 3、先攻第 1 回合手牌 6、0 条 `global 'RD'`、双方报 Duel finished**。
2026-09-19 实测 PASS（起手 mz=3/3 sz=3/3 hand=6/5 lp=8000/8000，9 个回合、无 lua 报错）。

⚠ 生成侧有个坑已加护栏：`bootstrap.lua` 里的占位符**只许出现一次**（脚本会数，不是 1 就报错退出）。
早先头部注释里抄了一次，结果规则库被内联两份、`utility.lua` 从 258 KB 涨到 460 KB，
而现象是「看起来也能跑」—— 只有核尺寸/数标记才发现。

## 用法

```bash
python devtools/unpack_rd.py                 # 卡表 + 禁限表 + RD 人机包（默认，约 10 秒）
python devtools/unpack_rd.py --all           # 再连卡图 / 立绘（约 +760 MB）
python devtools/unpack_rd.py --no-ai         # 跳过人机包（只铺卡表/禁限表）
python devtools/unpack_rd.py --hash          # 顺带核源包 MD5（要读完 1.6 GB）
python devtools/unpack_rd.py --src D:/xx/RD  # 源包在别处
python devtools/unpack_rd.py --client D:/xx/电脑版RD客户端.zip   # 客户端包在别处（zip 或已解包目录）
python devtools/unpack_rd.py --dest D:/xx    # 产物根在别处
python devtools/unpack_rd.py --list          # 只列源包内容，不落盘
```

脚本每次都会把源包尺寸与上表指纹核对一遍，不符会打 `⚠` 告警（不阻断 —— 上游换包是正常事，
但「换了包」这件事得让人看见）。卡表落盘后按 `CardsManager.initialize` 同源 SQL 做装载校验，
读不出卡即退出码 1。人机包生成后还会做四项自检：规则库 17 个标记齐、`utility.lua` 尺寸在 250~300 KB、
合并卡表 3484 行、异画卡摊平数 > 0。

## 边界

- 本仓库里的代码只负责**分发**；RD 规则口径见工作区 `_plan_rdmode.md` §0。
- `rd/` 与 `参考内容/` 都是本机资料：换机器必须先把 `参考内容/RD/` 与 `参考内容/电脑版RD客户端.zip` 拷回来
  （按上表 MD5 核对）。
- `rd/ai/` 里只有 `AI.Server.exe` 是从**产物根**拷的 —— 它是我们自己的 core，必须与客户端同版本；
  其余全部来自 RD 客户端包。所以重建 `output/` 后重跑脚本时，**先构建出 `AI.Server.exe` 再跑**
  （脚本会检查，缺了会打 ⚠ 但不阻断其它步骤）。
- `_unpack_rd.py`（工作区根的那份）是同一脚本的早期副本，**今后以 `devtools/unpack_rd.py` 为准**。


## rd/update/ —— RD 数据的 ypk 更新通道（2026-09-19）

`rd/update/*.ypk` 是**运行期**的 RD 数据增量通道，与上面的随包分发互补：
包放进去，下次启动由 `RdDataUpdater`（`Assets/SibylSystem/ResourceManagers/RdDataUpdater.cs`）
在**任何数据库装载之前**自动应用 —— 装载读到的就是新数据，无需 reload。

- 包就是普通 zip；接受**两种条目路径口径**：
  ① rd/ 相对路径（白名单五类：`cdb/`（客户端 RD 池卡表）、`picture/`（卡图/立绘）、
     `ai/`（人机整包）、`lflist.conf`（禁限表）、`bot.conf`（机器人名单））；
  ② **RD 先行卡包原生布局**（2026-09-20 起，玩家拖进来的就是这种）：
     `RD Patch.cdb`（根部）→ 覆盖 `rd/cdb/rd_patch.cdb`；其他根部 `*.cdb` → `rd/cdb/<原名>`；
     `pics/…` → `rd/picture/card/…`；`script/…` → `rd/ai/script/…`；根部 `server.ini` 忽略。
- **AI 卡表同步**：包里带根部 `*.cdb` 时，内容会 INSERT OR REPLACE 进
  `rd/ai/cdb/cards.cdb`（AI.Server/WindBot 只认这一份合并表，口径 = unpack_rd.merge_cdbs：
  datas/texts 按 id 合、后者覆盖）。AI 表缺失/被占用只记台账，不判包失败。
- 打包：`python devtools/pack_rd_update.py <源目录> <输出.ypk>`（源目录布局 = rd/ 布局，
  白名单与本表一致）。
- 失败处理：打不开/条目非法/解压中断 → 包改名 `<原名>.failed`、rd/ 一个字节不动、
  原因落 `log/rd_update.log`；成功 → 删包、写台账。坏包绝不挡启动。
- 注意 `unpack_rd.py` 与 `rd/update/` 互不干扰（脚本只合并铺设白名单路径，
  不会清掉 update 目录）；`rd/ai/cdb/cards.cdb`（人机用）与客户端 cdb 是两份数据，
  包作者要么只发其一、要么两份都发。
