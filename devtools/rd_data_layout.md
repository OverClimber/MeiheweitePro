# RD（超速决斗）数据分发布局与源包清单

_2026-09-19 建档。这份清单解决一件事：**RD 的运行目录不在版本控制里，换机器 / 清空 `output/` 后怎么把它重造出来。**

## 为什么要有这份文件

- RD 的源码（`GameModeManager` 等 15 个文件）在 `feature/rush-duel` 分支里，随 git 走，没问题。
- RD 的**数据**（卡表 / 禁限表 / 卡图 / 立绘，合计约 800 MB）**不进 git**，
  只落在产物侧 `output/Windows/rd/`，而 `/output/` 被 `.gitignore` 整个忽略。
- `rd/` **不在** `Assets/Editor/BuildHelper.RuntimeDataDirectories` 里（特意的：770 MB 不随每次构建 `File.Copy`）
  ⇒ **重建 `output/` 之后必须重跑 `devtools/unpack_rd.py`**，否则 RD 模式进去是空卡池。

## 随构建走的 RD 贴图：场地图（2026-09-20 新增，2026-09-21 盘面重做）

RD 只有 3 个怪兽区 + 3 个魔陷区，没有灵摆区、没有额外怪兽区、没有第 1/5 列。
原生那张（`newfield.png`，OCG 的 5 列盘面 + 两侧外挂列）在 RD 局里又宽又空，
所以客户端在 RD 局换用**重排过的紧凑盘面** `texture/duel/newfield_rd.png`：
牌堆（卡组/墓地/场地魔法/额外卡组）**收进中区第 1/5 列**，与怪兽/魔陷排对齐、与主格同缩放（92×116）。

    我方半场（下半）        左列(第1列)      中区 3 列        右列(第5列)
      近排（靠中线）         场地魔法 ⭐      怪兽区 ×3        墓地 ⚷
      远排（靠玩家）         额外卡组 🌀      魔陷区 ×3        卡组（素格）

    对手半场上下镜像（对手的卡组落在我方视角的左侧）。

⛔ **曾经有第二副摆法** `texture/duel/newfield_rd_small.png`（由设置 `rdPileSame_`
  「附属格（卡组等）等大」挑：关掉 = 牌堆格仍在第 1/5 列、中心与开档**逐点重合**、
  只是整格缩到 1/1.21 = 76×96）—— **2026-09-21 整体删除**。删的依据是实测：
  两档的牌堆格**中心逐点重合**，而 `get_point_worldposition_rd` 本来也不读那个开关
  ⇒ 两档卡的落点完全相同，差别只剩「卡相对格子的大小」（卡背 106px vs 格子 111/92px，
  肉眼看不出来）⇒ 白背一张贴图 + 一个开关 + 一套跨档判据。**别再引回来。**

⛔ 牌堆格**别照抄 `newfield.png` 外挂格自己的 y**（2026-09-21 的第一版就是：
  场地/墓地 542-657、卡组/额外 673-788）。那套 y 属于**不缩放的左右切片**，而第 1/5 列
  画在**中块**里、中块整体乘 `fieldSize` ⇒ 同一个 y 会被再放大一次：屏幕上比卡低
  30~70px，卡就落在两个格子中间（卡走世界坐标，不跟着贴图这一刀）。

- **牌堆格用原生画法**（92×116，比怪兽/魔陷格 106×116 窄；92 ≈ 一张卡的宽度）。
  这是 2026-09-21 第二版修的：第一版把图标缩进 106 宽的怪兽格里，格子被撑大、
  图标反而显小 —— 与原盘面「卡正好填满牌堆格」是两种观感。
  现在是**整格 1:1 搬运**（连边框带图标，零缩放），只有落点变了；
  `--verify` 对产物**逐像素**比源格。
- **除外区不画格子**（参考盘面里没这个位置；外挂列（含 ☒ 格）整列擦掉）。
  它的落脚点仍在盘面外沿，真有卡被除外才看得见卡，平时不可见；
  点击查看被除外的卡靠的是不可见的 `gameHiddenButton`，与贴图无关。
- 消费方：`GameField.RdFieldPath`（= `NewFieldPathRD`，它缺了才回落 OCG 那张）
  → `NewFieldPath` / `OldFieldPath` —— **RD 局无论 core 给的 MasterRule 是 3（联机）
  还是 5（单机），两条支路都换成 RD 这张图**，所以联机/单机看到的是同一副盘面；
  OCG 侧完全不变（MR3 用 `field.png`、MR4+ 用 `newfield.png`）。
- 格位坐标：`Ocgcore.get_point_worldposition_rd()`，**不看 MasterRule**，也不看任何设置
  —— 格子中心是「贴图与坐标共同的事实」，改贴图必须同时改坐标（见 `make_rd_field.py` 文件头）。
  ⛔ 曾经有过 `get_point_worldposition_rd_smallPile()`（把「小一档」那副的 z 换成 OCG 外挂格
  那两档 ∓14.6 / ∓9 / ∓3）—— 随「双摆法」一起**废掉**了，别再引回来：
  那套点画在中块里会被 `fieldSize` 再放大一次，卡就落在两个格子中间。
  ⛔ 别再把 RD 的牌堆往 `get_point_worldposition_ocg` 上引 —— RD 的图是从 `newfield.png`
  派生的另一张图，照 OCG 那套 x（∓15.2/14.65）牌堆就挂到盘面外的空列去了。
- 这张图**不是**外购素材，是从 `newfield.png` 派生的：
  `python devtools/make_rd_field.py --apply`（依赖 Pillow；不带参数出前后对照预览图，
  `--detect` 重测格子边框坐标，`--verify` 对产物复核「擦干净 / 牌堆格逐像素等于源格 /
  没误伤 / 无区外像素 / 牌堆格尺寸 92×116」）。
  牌堆格（⭐⚷🌀 与两个素格）是从原图外挂列上**整格抠过来**的，不是重画的。
- `texture/` 在 `.gitignore` 里 ⇒ 它与 `rd/` 一样**不进 git**；
  但 `texture/` **在** `BuildHelper.RuntimeDataDirectories` 里 ⇒ 只要工程侧这张文件在，
  构建会照常把它铺进产物，**不需要**像 `rd/` 那样每次重建后重跑。
- 只有换机器 / 重新 clone（工程里没有这张图）时才需要跑一次生成脚本。
  当时的效果是**回落成 OCG 那张**（盘面偏宽、牌堆落在最外侧，不会白板、不会崩）。

## 源包清单（2026-09-17 收集，2026-09-19 实测指纹）

源目录：工作区级目录 `参考内容/RD/`（与 `YGOProUnity_V2/` 平级、非 git；`--src` 可指向别处）

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
