using UnityEditor;
using UnityEngine;

/// <summary>
/// 为 trans_AIroom.prefab（人机房间界面）补齐 Windows 版人机所需的控件。
///
/// 背景：本工程取自 unity2021 分支，其人机界面预制件停留在旧的“进程内 ai.lua”时代，
/// 控件是 percyHint / start_ / exit_ / aideck_ / rank_ / life_ / first_ / unrand_ / god_ / mr4_。
/// 而 Windows 版成品的人机界面（参考内容 Assembly-CSharp.dll 反编译所得）另外还需要：
///   botdesc_    —— 显示当前选中对手的描述（UILabel）
///   lockhand_   —— 锁定手牌  （对应 AI.Server 的 Hand=1）
///   nocheck_    —— 不检查卡表（对应 AI.Server 第 6 个参数 T）
///   noshuffle_  —— 不洗牌    （对应 AI.Server 第 7 个参数 T）
/// AIRoom.cs 会按这些名字取控件；缺少时人机仍可开局（开关按 false 处理），但界面不完整。
///
/// 本脚本一次性运行即可，重复运行不会产生重复控件（幂等）：
///   Unity 菜单：MeiheweitePro / 修复人机界面（补 AI Room 控件）
///   命令行   ：Unity.exe -batchmode -quit -projectPath &lt;工程&gt; -executeMethod AIRoomUiFixer.FixBatch
///
/// 位置取自旧开关列的实测本地坐标，如与实际视觉不符，改下面的常量即可。
/// </summary>
public static class AIRoomUiFixer
{
    const string PrefabPath = "Assets/transUI/prefab/trans_AIroom.prefab";

    // 控件模板：percyHint 是全宽（430x22）、锚定 mainWindow 的 UILabel；first_ 是标准 NGUI 复选开关。
    const string LabelTemplate = "percyHint";
    const string ToggleTemplate = "first_";

    // 旧 ai.lua 时代的开关，新的人机实现（WindBot 路线）不读取它们，隐藏以免误用（可随时重新启用）。
    static readonly string[] ObsoleteControls = { "first_", "unrand_", "god_", "mr4_" };

    // 三个开关的位置：沿用旧开关列（x=17.6，行距约 24）。
    static readonly Vector3 LockHandPos = new Vector3(17.6f, 24.4f, 0f);
    static readonly Vector3 NoCheckPos = new Vector3(17.6f, 0.4f, 0f);
    static readonly Vector3 NoShufflePos = new Vector3(17.6f, -23.6f, 0f);

    // botdesc_ 用锚点定位（相对 mainWindow）：左侧内缩 27、右侧止于 -160（避开右列控件）、
    // 顶部下方 52~74（正好落在标题 percyHint 的下方、不会压到对手列表）。
    // UIRect.AnchorPoint.absolute is an int, so these must be int as well.
    const int DescLeft = 27;
    const int DescRight = -160;
    const int DescTop = -52;
    const int DescBottom = -74;

    [MenuItem("MeiheweitePro/修复人机界面（补 AI Room 控件）")]
    public static void FixFromMenu()
    {
        Fix();
        EditorUtility.DisplayDialog(
            "人机界面",
            "已补齐按钮/开关：botdesc_、lockhand_、nocheck_、noshuffle_。\n"
                + "旧时代的 first_ / unrand_ / god_ / mr4_ 已置为非激活（隐藏）。",
            "好"
        );
    }

    /// <summary>批处理入口（-executeMethod）。</summary>
    public static void FixBatch()
    {
        Fix();
        Debug.Log("AIRoomUiFixer：人机界面控件已补齐。");
    }

    static void Fix()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            Transform mainWindow = FindTransform(root.transform, "mainWindow");
            Transform host = mainWindow != null ? mainWindow : root.transform;

            GameObject labelTemplate = FindGameObject(root.transform, LabelTemplate);
            GameObject toggleTemplate = FindGameObject(root.transform, ToggleTemplate);

            if (labelTemplate == null || toggleTemplate == null)
            {
                Debug.LogError(
                    "AIRoomUiFixer：找不到控件模板（需要 "
                        + LabelTemplate
                        + " 与 "
                        + ToggleTemplate
                        + "），已中止。"
                );
                return;
            }

            // 1) 对手描述标签
            EnsureLabel(root, host, labelTemplate, "botdesc_", "请选择对手。");

            // 2) 三个对战开关（默认都不勾选，与 Windows 版默认行为一致）
            EnsureToggle(root, host, toggleTemplate, "lockhand_", LockHandPos, "锁定手牌");
            EnsureToggle(root, host, toggleTemplate, "nocheck_", NoCheckPos, "不检查卡表");
            EnsureToggle(root, host, toggleTemplate, "noshuffle_", NoShufflePos, "不洗牌");

            // 3) 隐藏旧时代开关
            foreach (string name in ObsoleteControls)
            {
                GameObject obsolete = FindGameObject(root.transform, name);
                if (obsolete != null && obsolete.activeSelf)
                {
                    obsolete.SetActive(false);
                    Debug.Log("AIRoomUiFixer：已隐藏旧控件 " + name);
                }
            }

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            AssetDatabase.Refresh();
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    static GameObject EnsureLabel(
        GameObject root,
        Transform host,
        GameObject template,
        string name,
        string text
    )
    {
        GameObject go = FindGameObject(root.transform, name);
        if (go == null)
        {
            go = Object.Instantiate(template, host);
            go.name = name;
            Debug.Log("AIRoomUiFixer：新建标签 " + name);
        }

        UILabel label = go.GetComponent<UILabel>();
        if (label == null)
        {
            Debug.LogError("AIRoomUiFixer：" + name + " 上找不到 UILabel。");
            return go;
        }

        label.text = text;
        label.leftAnchor.target = host;
        label.leftAnchor.relative = 0;
        label.leftAnchor.absolute = DescLeft;
        label.rightAnchor.target = host;
        label.rightAnchor.relative = 1;
        label.rightAnchor.absolute = DescRight;
        label.topAnchor.target = host;
        label.topAnchor.relative = 1;
        label.topAnchor.absolute = DescTop;
        label.bottomAnchor.target = host;
        label.bottomAnchor.relative = 1;
        label.bottomAnchor.absolute = DescBottom;
        label.ResetAndUpdateAnchors();
        return go;
    }

    static GameObject EnsureToggle(
        GameObject root,
        Transform host,
        GameObject template,
        string name,
        Vector3 position,
        string labelText
    )
    {
        GameObject go = FindGameObject(root.transform, name);
        if (go == null)
        {
            go = Object.Instantiate(template, host);
            go.name = name;
            go.transform.localPosition = position;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;
            Debug.Log("AIRoomUiFixer：新建开关 " + name);
        }
        else
        {
            go.transform.localPosition = position;
        }

        UIToggle toggle = go.GetComponent<UIToggle>();
        if (toggle == null)
        {
            Debug.LogError("AIRoomUiFixer：" + name + " 上找不到 UIToggle。");
            return go;
        }
        // group = 0 表示独立复选框（非互斥单选）。
        toggle.group = 0;
        toggle.startsActive = false;
        toggle.value = false;

        UILabel label = go.GetComponentInChildren<UILabel>(true);
        if (label != null)
        {
            label.text = labelText;
        }
        else
        {
            Debug.LogWarning("AIRoomUiFixer：" + name + " 的子件里没有 UILabel，文字未设置。");
        }
        return go;
    }

    static Transform FindTransform(Transform root, string name)
    {
        GameObject go = FindGameObject(root, name);
        return go != null ? go.transform : null;
    }

    static GameObject FindGameObject(Transform root, string name)
    {
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
        {
            if (t.name == name)
            {
                return t.gameObject;
            }
        }
        return null;
    }
}
