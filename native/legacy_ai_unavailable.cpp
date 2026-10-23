#include <stdint.h>
// The legacy AI path is unavailable on iOS until its native runtime is ported.
// The generated managed startAI entry is made to return false before it can use these.
extern "C" int32_t get_ai_going_first_second(intptr_t, const char*) { return -1; }
extern "C" int32_t set_player_going_first_second(intptr_t, int32_t, const char*) { return -1; }
extern "C" void set_ai_id(intptr_t, int32_t) {}
