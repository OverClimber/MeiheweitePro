#ifndef YGO_REPLAY_DIAG_H
#define YGO_REPLAY_DIAG_H

// ── [S4.8] 回放路径异常诊断 ────────────────────────────────────────────────────
//
// 只在【异常】分支被调用 ⇒ 正常路径零开销、零行为变化。
//
// 立案现场（2026-10-07 真机，release 包）：
//     撤回确认后 SIGABRT，两次（09:33:23 / 12:05:40）
//     Abort message: 'FORTIFY: memcpy: count 18446744073709544344 > SSIZE_MAX'
//     #04 __memcpy_chk+16  ←  #05 ygo::SingleDuel::EndDuel()+104  ←  #06 NetServer::StopServer()+72
//
// 反汇编【设备上那一份】libygoserver.so（BuildId c689c23f2caf4c9a22e455edece8f575351c5cf2，
// 与 layer/native/arm64-v8a/libygoserver.so 逐位一致）得到两条硬事实：
//
//   74854:  mov   w3, #8160          ; __memcpy_chk 的第 4 参 dst_len【编译期常量】
//   74858:  stp   q1, q0, [sp]       ; 第一次 memcpy(32B) 被 inline 了
//   7485c:  ldr   x1, [x19, #512]    ; comp_data
//   74860:  ldr   x2, [x19, #528]    ; comp_size  ← count
//   74864:  bl    __memcpy_chk        ; ← 崩溃点（＋104）
//
//   ① dst_len = 8160 = 8192 − 32 ⇒ `char replaybuf[0x2000]` 装不下
//      `sizeof(ReplayHeader)(32) + comp_size(最多 8192) = 8224` —— 最坏情况【必然】
//      短 32 字节。这是确定性缺陷，与那个垃圾 count 无关，本身就该修。
//   ② count 却报成 2^64−7272（＝ int32 −7272 的符号扩展），不可能来自
//      comp_size 的正常取值范围：
//        · Types.h 里 `_LZMA_NO_SYSTEM_SIZE_T` 全树未定义 ⇒ SizeT == size_t（64 位），
//          LzmaCompress/LzmaEncode/LzmaEnc_MemEncode 一路 size_t*，无窄化；
//        · LzmaEnc.c MyWrite 有 rem 溢出保护（rem < size 时截断并置 overflow），
//          出口只有 `*destLen -= outStream.rem` ⇒ 数学上恒落在 [0, MAX_COMP_SIZE]。
//      ⇒ 该字段是被【外部】写坏的（本对象 this+528），或存在一条尚未看到的路径。
//      静态分析到此为止，剩下的必须靠真机真值 —— 就是本文件的作用。
//
// 通道① stderr —— Unity Android 会把 native stderr 转进 logcat。
// 通道② <cwd>/replay/_replay_diag.txt（append）。
//        cwd = persistentDataPath 根（进程内服务端与客户端共用它）。
//        ./replay/ 不存在时退到 <cwd>/_replay_diag.txt —— 退到了本身就说明
//        Replay::BeginRecord() 的 MakeDir 也没成功，是一条独立线索。
//
// ⛔ 这个头【只做诊断】，不参与任何判定；删掉它不影响修复的正确性
//    （修复的两道闸在 _patch_replay_overflow.py 里，各自独立）。
#include <cstdio>
#include <cstddef>

namespace ygo {

inline void YgoReplayDiag(const char* where, size_t replay_size, size_t comp_size, int ret) {
	fprintf(stderr, "[replay-diag] %s replay=%llu comp=%llu (0x%llX) ret=%d\n",
	        where, (unsigned long long)replay_size, (unsigned long long)comp_size,
	        (unsigned long long)comp_size, ret);
	FILE* f = fopen("./replay/_replay_diag.txt", "a");
	if(!f)
		f = fopen("./_replay_diag.txt", "a");
	if(f) {
		fprintf(f, "[replay-diag] %s replay=%llu comp=%llu (0x%llX) ret=%d\n",
		        where, (unsigned long long)replay_size, (unsigned long long)comp_size,
		        (unsigned long long)comp_size, ret);
		fclose(f);
	}
}

}

#endif // YGO_REPLAY_DIAG_H
