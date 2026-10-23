// ⛔ 本文件是【层库改写版】，不是 MDPro3 上游原件。
//
// 上游原件：Tools/YGO Classes/ygoserver/serverapi.cpp
//           sha256 92a7c0c756d13bc4b0e4bc89904ded167166edfca5b965a0a2383b7a9565e998（831 B）
// 覆盖时机：tools/native-recipe/build_ygoserver.sh 在铺完上游源码后，
//           先校验上游指纹仍是上面那个，再用本文件覆盖 —— 指纹变了就中止、要人工比对。
//
// ── 为什么要改 ───────────────────────────────────────────────────────────────
// 上游用 `new char*[13]`，只有 argv[0..12] 共 13 个槽。
//
// 但本项目客户端（Assets/SibylSystem/Room/AIRoom.cs:850）下发的参数正好是 13 个 token：
//     7911 -1 5 0 <DuelRule> <nocheck> <noshuffle> 8000 <StartHand> 1 0 0 <seed>
// 加上 argv[0] 共需 14 个槽。
//
// 而服务端 main（ygoserver/gframe.cpp:114）会把 argv[13..15] 读成 pre_seed ——
// pre_seed 正是「撤回重开」唯一的重建依据（Assets/SibylSystem/Room/DuelTimeline.cs:14
// 「AI.Server 的 argv[13] 是 pre_seed，pre_seed>0 时它不再调 rd()」）。
//
// ⇒ 上游写法在这里就是【堆越界写】：argv[13] 落在数组之外，而越界那一格恰好是种子。
//   桌面版踩不到（真进程的 argv 由操作系统分配，给多少有多少）；
//   只有我们这次新走的「进程内 start_server」这条路才会踩，且正好踩在撤回功能上。
//
// ── 改法 ─────────────────────────────────────────────────────────────────────
// 先数 token、再按需分配；顺带补掉上游漏删每个 token 的内存泄漏。
// argv 的内容、顺序、argc 语义与原来完全一致，只是不再有固定上限。
// 另外把上游 `while (args[j] == ' ' && j < argLength)` 那种「先解引用后判边界」的
// 疑序一并改正（边界检查放前面）。

#include "serverapi.h"

#include <cstring>
#include <cstdlib>
#include <string>

// ── 资源根（移动线，2026-10-06 补入）──────────────────────────────────────────
// 桌面版把 AI.Server.exe 当独立进程起，用 WorkingDirectory 隔离 OCG / RD 两套资源；
// 进程内只有一个 cwd，而那个 cwd 必须留给游戏客户端（贴图 / 卡图 / 卡表都按它找）
// ⇒ 由 C# 侧每局调 set_data_root() 指定。空串 = 与上游原样拼接【逐字一致】（桌面线零影响）。
// ⛔ OCG 局必须传空串：OCG 的脚本装在 expansions/*.ypk 这个 irrlicht archive 里，
//    路径一旦加前缀，archive 查找就会失配 —— 表现为「OCG 卡的效果脚本全部找不到」。
// 细节见 tools/native-recipe/overrides/ygoserver/ygo_data_root.h。
// ⛔ 别把这里改成「进程级一次性设置」：同一进程里用户会先打 OCG 人机、再打 RD 人机。
const char* g_ygo_data_root = "";

namespace ygo {

	namespace {
		// argv[0] 用静态存储，避免把字符串字面量赋给 char*（原来那样写是 UB）。
		char kProgName[] = "ygoserver";

		// 退回上一个 token 的起点：把空格跳过。返回 false 表示已到串尾。
		bool SkipSpaces(const char* args, size_t len, size_t& j) {
			while (j < len && args[j] == ' ') { ++j; }
			return j < len;
		}
	}

	// C#（InProcessAiServer）在每次 start_server 之前调用。
	// buf 用函数内静态：保存的是根字符串本体 —— 入参是 C# marshal 出来的临时缓冲，
	// 只存指针会在调用返回后变成悬垂。c_str() 在 buf 下次被赋值之前一直有效，而调用方
	// 保证「设置 → 启动 → 结束」逐局串行，不存在并发改写。
	extern "C" DECL_DLLEXPORT void set_data_root(const char* root) {
		static std::string buf;
		buf = (root ? root : "");
		g_ygo_data_root = buf.c_str();
	}

	extern "C" DECL_DLLEXPORT int start_server(const char* args) {
		if (!args) { return -1; }
		const size_t argLength = strlen(args);

		// ① 先数出 token 个数 —— 不再有固定上限，给多少收多少。
		int tokens = 0;
		{
			size_t j = 0;
			while (SkipSpaces(args, argLength, j)) {
				++tokens;
				while (j < argLength && args[j] != ' ') { ++j; }
			}
		}

		const int argc = tokens + 1;
		// 多留一格置 nullptr，便于后续（或调试器）安全遍历。
		char** argv = new char*[static_cast<size_t>(argc) + 1];
		argv[0] = kProgName;
		argv[argc] = nullptr;

		// ② 再按刚才数出的个数切分。切不出 tokens 个就把余下补成 nullptr，绝不越界。
		int i = 1;
		{
			size_t j = 0;
			while (i <= tokens && SkipSpaces(args, argLength, j)) {
				size_t tokenLength = 0;
				while (j + tokenLength < argLength && args[j + tokenLength] != ' ') { ++tokenLength; }

				char* currentToken = new char[tokenLength + 1];
				if (tokenLength) { std::memcpy(currentToken, args + j, tokenLength); }
				currentToken[tokenLength] = '\0';

				argv[i++] = currentToken;
				j += tokenLength;
			}
			while (i <= tokens) { argv[i++] = nullptr; }
		}

		const int result = main(argc, argv);

		// ③ 上游只 delete[] 了数组本身，每个 token 漏了。这里补上（纯内存卫生，不改行为）。
		for (int k = 1; k < argc; ++k) { delete[] argv[k]; }
		delete[] argv;

		return result;
	}

	extern "C" DECL_DLLEXPORT void stop_server() {
		NetServer::StopServer();
	}
}
