using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Scripting;
#if !UNITY_EDITOR
using System.Threading;
using System.Threading.Tasks;
#endif

/// <summary>
/// 去掉开屏的「Made with Unity」启动屏。
///
/// 用的是 Unity 自己留的官方接口 SplashScreen.Stop —— 不改二进制、不碰授权文件：
///   [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSplashScreen)]
/// 让这段代码正好在「显示启动画面之前」这一刻被引擎调用，在里面立刻把启动屏停掉，
/// 于是引擎直接进游戏。这个枚举值本身就是为了「抢在启动序列之前跑一段代码」而存在的。
///
/// 几点说明：
///   · 必须是运行时（非 Editor）代码，所以整段用 #if !UNITY_EDITOR 包起来。
///   · [Preserve] 是给托管代码剥离（IL2CPP / 代码裁剪）留的保险：这个方法没有任何
///     显式调用点，不加标记有被裁掉的风险。
///   · Stop 放在独立线程里调：此刻主线程还在启动序列内，同步调用容易与启动流程
///     互相等待。
///   · 带一个有界的重试：Stop 早于启动屏开始绘制时可能落空，靠 SplashScreen.isFinished
///     判断是否已经收工，最多重试 1 秒。跑不动也不影响游戏启动本身。
/// </summary>
[Preserve]
public static class SkipUnityLogo
{
    /// <summary>最多重试次数 × 间隔 = 1 秒，之后放弃（不阻塞启动）。</summary>
    private const int MaxAttempts = 20;
    private const int RetryIntervalMs = 50;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSplashScreen)]
    private static void BeforeSplashScreen()
    {
        QuickTestTrace.Log("splash", "BeforeSplashScreen enter");
#if !UNITY_EDITOR
        Task.Run(() =>
        {
            for (int i = 0; i < MaxAttempts; i++)
            {
                try
                {
                    if (SplashScreen.isFinished)
                    {
                        QuickTestTrace.Log("splash", "启动屏已结束（第 " + (i + 1) + " 次检查）");
                        return;
                    }
                    SplashScreen.Stop(SplashScreen.StopBehavior.StopImmediate);
                }
                catch (System.Exception)
                {
                    // 启动屏停不掉不该影响游戏启动本身，吞掉即可。
                }
                Thread.Sleep(RetryIntervalMs);
            }
            QuickTestTrace.Log("splash", "重试 " + MaxAttempts + " 次后启动屏仍未结束");
        });
#endif
    }
}
