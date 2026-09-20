-- ============================================================================
-- RD 兼容层 —— 让 RD 规则层跑在「主线 core」的 AI.Server.exe 上
-- ============================================================================
-- 这是**源码**（入 git）。devtools/unpack_rd.py 会把它展开成产物里的
-- rd/ai/script/utility.lua：把下面那个占位符整行换成 17 个 RD 规则库的源码，
-- 再追加在 RD 版 utility.lua 的末尾（那份 utility.lua 是外部资料，不入 git）。
--
-- ⚠ 占位符在本文里**只许出现一次**（生成脚本会数，不是 1 就报错退出）。
--   写文档时别把那一行原样抄进来 —— 早先就是头部注释里抄了一次，
--   结果规则库被内联两份、文件从 264 KB 涨到 460 KB，而现象是「看起来也能跑」。
--
-- ── 为什么需要这一层（2026-09-19 实测取证，别按直觉改）──────────────────────
-- RD 的规则是**纯 lua** 实现的，不需要「RD 版 core」：
--   RDRule.lua 用 EFFECT_DISABLE_FIELD 关掉最外两列、EFFECT_DRAW_COUNT 抽到 5 张、
--   EFFECT_SET_SUMMON_COUNT_LIMIT 不限召唤次数、EFFECT_SKIP_SP 跳准备阶段……
-- 但 RD 规则库假定 core 提供两样东西，而 AI.Server.exe（以及我们客户端的
-- ocgcore.dll）**都没有**：
--   ① Duel.LoadScript(name) —— 二进制里 "LoadScript" 字符串命中 0 次；
--      只有 RD 端的 ygopro.exe 有（命中 1 次）。
--   ② 决斗开始时调用 Auxiliary.PreloadUds() —— "PreloadUds" 在两个主线核里命中 0 次。
-- 于是原版 special.lua 在这条链上必然失败：special.lua 其实是**最先**加载的
-- （在 constant/utility/procedure 之前，见 AI.Server.exe 里的字符串簇），它第 2 行
-- `Duel.LoadScript("RDBase.lua")` 时 Duel 还不存在 ⇒ 整个文件中断 ⇒ RushDuel 永不建立
-- ⇒ 卡脚本里的 `RD.*` 全是 nil（实测现象：`attempt to index a nil value (global 'RD')`）。
--
-- ── 本兼容层的做法 ────────────────────────────────────────────────────────
--   ① 规则库**不做运行时加载**：生成时整段内联。既不依赖 Duel.LoadScript，
--      也不依赖已被沙箱拿掉的 loadfile/dofile（实测在 AI.Server 里 loadfile 拿不到）。
--   ② 时机改挂在 GetID() 上：它是每张卡脚本的第一句，也是本 core 上唯一一个
--      「每局必走到、那一刻 duel 已存在、RegisterEffect 可用、抽卡阶段还没到」的 lua 入口。
--      实测取证：挂在这里注册的全局效果会在抽卡阶段正常触发。
--      另一条路 Auxiliary.PreloadUds 不可用（见上）。
--   ③ 只启动一次（_rd_booted）。**前提：一个 AI.Server 进程 = 一局**。
--      AIRoom 每局起新进程，撤回重开也是新进程，所以成立；若哪天改成常驻进程，
--      这里要跟着改成「按局重置」。
--
-- ── 实测判据（spike，2026-09-19；手牌数于同日修正）─────────────────────────
-- 兼容层生效后：双方怪物区 / 魔陷区各 3 格、LP 8000、先攻方第 1 回合手牌 5 张
-- （**初期手卡 4 张** + 先攻第 1 回合规则抽 1 张，见《RUSH DUEL基本规则 2025.4.pdf》）
-- —— 与 RD 口径一致。兼容层不生效时这些全是 OCG 的 5 格 / 5 张。
--
-- ⚠ 起手那 4 张**不是本层能管的**：core 的发牌先于任何 lua 效果，`RDRule.lua` 的
--   先攻抽卡是在起手之后**再加**一张。所以「4 张起手」只能靠 AI.Server 的 argv[8]
--   喂对（AIRoom 走 GameModeManager.StartHand，RD=4）。当年这里记的是「抽 5 + 规则抽 1 = 6」
--   并当成正确值，实际是起手多喂了一张 —— 别再把 6 当基线。
-- ============================================================================

do
    local _rd_booted = false
    local _rd_getid = GetID

    function GetID()
        if not _rd_booted then
            _rd_booted = true

            -- 防御：万一还有脚本按名字动态加载，报一条看得见的中文错，别静默失败。
            -- 卡脚本里的 `RD.AlternateCard(N)`（252 处：异画卡/网络复用另一张卡的实现）
            -- 已在**生成期**内联掉，正常对局走不到这里。
            if Duel.LoadScript == nil then
                Duel.LoadScript = function(name)
                    error("[RD兼容层] 本 core 没有 Duel.LoadScript，无法动态加载脚本：" .. tostring(name))
                end
            end

            -- ==== 以下由 unpack_rd.py 内联 17 个 RD 规则库（原文件同目录留档，便于比对）====
-- @@RD_LIBS_HERE@@
            -- ==== RD 规则库结束 ====

            RD = RushDuel        -- 卡脚本里的 `RD.*` 就是它（= 原版 special.lua 的那一行）
            RushDuel.Init()      -- 装规则：关外列、抽到 5、跳准备阶段、不限召唤次数……
        end
        return _rd_getid()
    end
end
