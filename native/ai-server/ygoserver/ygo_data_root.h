#ifndef YGO_DATA_ROOT_H
#define YGO_DATA_ROOT_H
// ─────────────────────────────────────────────────────────────────────────────
// 移动线补入 —— 进程内服务端的「资源根」
//
// 真源：YGOProAndroid/tools/native-recipe/overrides/ygoserver/ygo_data_root.h
// 由 tools/native-recipe/_patch_data_root.py 拷进源码树，并在各 .cpp 顶部注入 include。
//
// 为什么需要它：
//   桌面版把 AI.Server.exe 当【独立进程】起，用 ProcessStartInfo.WorkingDirectory 隔离
//   OCG / RD 两套资源（见 Assets/SibylSystem/Room/AIRoom.cs 与 GameModeManager 的「人机路径」段）：
//     OCG : cwd = 数据根      资源 = ./cdb/*.cdb + ./expansions/*.ypk（脚本包） + ./config/lflist.conf
//     RD  : cwd = <根>/rd/ai  资源 = ./cdb/cards.cdb + ./script/*.lua（散文件） + ./config/lflist.conf
//   进程内（Android/iOS 不许 fork/exec）只有一个 cwd，而那个 cwd 必须留给游戏客户端
//   （texture / picture / 卡表都按它找）⇒ 只能给服务端加一个【可切换的资源根】。
//
// ⛔ g_ygo_data_root 为空串（默认）时，两个 helper 的输出与上游的原样拼接【逐字一致】
//    ⇒ 桌面线零影响，且可随时 A/B 对照。
// ⛔ OCG 局必须保持为空：OCG 的脚本装在 expansions/*.ypk 这个 irrlicht archive 里，
//    路径一旦加前缀，archive 查找就会失配（archive 内的条目名不含前缀），
//    表现为「OCG 卡的效果脚本全部找不到」——这是最难查的一类功能性缺失。
//    只有 RD 局才把它设成 <persistentDataPath>/rd/ai。
// ⛔ 资源根用【绝对路径】。别用相对路径：那会隐式依赖 cwd，等于把桌面那套限制又抄一遍。
// ─────────────────────────────────────────────────────────────────────────────
#include "bufferio.h"
#include <cstdio>
#include <cstddef>
#include <cwchar>

extern const char* g_ygo_data_root;

// 拼根。dst 必须是数组；cap 是该数组的【元素个数】（不是字节数）。
inline void YgoRootPath(char* dst, size_t cap, const char* rel) {
	if (g_ygo_data_root && g_ygo_data_root[0])
		snprintf(dst, cap, "%s/%s", g_ygo_data_root, rel);
	else
		snprintf(dst, cap, "%s", rel);
}

inline void YgoRootPathW(wchar_t* dst, size_t cap, const wchar_t* rel) {
	if (g_ygo_data_root && g_ygo_data_root[0]) {
		wchar_t wroot[1024];
		BufferIO::DecodeUTF8(g_ygo_data_root, wroot);
		swprintf(dst, cap, L"%ls/%ls", wroot, rel);
	} else {
		swprintf(dst, cap, L"%ls", rel);
	}
}

#endif // YGO_DATA_ROOT_H
