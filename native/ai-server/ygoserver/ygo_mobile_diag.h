#ifndef YGO_MOBILE_DIAG_H
#define YGO_MOBILE_DIAG_H

// ── [S4.10] 移动端进程内服务端 · 运行时诊断 ────────────────────────────────────
//
// 只在【异常/关键】分支被调用 ⇒ 正常路径几乎零开销（一次 fopen/append）。
//
// 两个通道：
//   ① stderr —— Unity Android 会把 native stderr 转进 logcat。
//   ② <cwd>/_mobile_diag.txt（append）。cwd = persistentDataPath 根
//      （进程内服务端与游戏客户端共用同一个它）。
//
// ── 为什么需要它 ─────────────────────────────────────────────────────────────
// 2026-10-08 真机 qt_32119.log 09:09:58 复盘：客户端对 `MSG_SELECT_CARD(count=1,
// min=1, max=1)` 发出 `[01 00]`（唯一合法答案），core 却回 `MSG_RETRY`；而客户端
// `case GameMessage.Retry:` 什么都不做 ⇒ 对局永久停住。
//
// 客户端那一侧已经能证明「发出去的字节是 `01 00`」（日志里的 sha 反解），但**服务端
// 到底喂给 core 的是什么、core 判定的上下文是什么**，只有 native 侧能回答。
// 本头 + `_patch_mobile_runtime.py` 的三处埋点就是为此：
//   · SingleDuel::GetResponse —— 实际喂进 set_responseb 的字节（core 收到的就是它）
//   · SingleDuel 消息循环的 MSG_RETRY 分支 —— last_response / 玩家 state / 上一条应答
//   · NetServer::StopServer / ServerThread —— 拆栈竞态现场（另一条独立线索）
//
// ⛔ 本头【只做诊断】，不参与任何判定；删掉它不影响修复的正确性。
#include <cstdio>
#include <cstdarg>

namespace ygo {

inline void YgoMobileDiag(const char* tag, const char* fmt, ...) {
	FILE* f = fopen("./_mobile_diag.txt", "a");
	for (int pass = 0; pass < 2; ++pass) {
		FILE* out = (pass == 0) ? stderr : f;
		if(!out)
			continue;
		va_list ap;
		va_start(ap, fmt);
		fprintf(out, "[%s] ", tag);
		vfprintf(out, fmt, ap);
		fprintf(out, "\n");
		va_end(ap);
		fflush(out);
	}
	if(f)
		fclose(f);
}

}

#endif // YGO_MOBILE_DIAG_H
