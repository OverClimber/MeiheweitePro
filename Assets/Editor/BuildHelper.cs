using UnityEditor;
using UnityEditor.Build.Reporting;
using System.IO;
using UnityEngine;

/// <summary>
/// Windows 桌面版构建入口。
///
/// 命令行：
///   Unity.exe -batchmode -nographics -projectPath &lt;工程&gt; -logFile &lt;日志&gt;
///             -executeMethod BuildHelper.BuildWindows -quit
///
/// 产物：&lt;工程&gt;/output/Windows/MeiheweitePro.exe（名字取自 PlayerSettings.productName）
/// </summary>
class BuildHelper
{
    const string MainScene = "Assets/main.unity";
    const string OutputDirectory = "output/Windows";

    /// <summary>
    /// Windows exe 图标源图（构建时嵌进 exe 资源，改图标必须重新构建，热换 DLL 不覆盖）。
    /// 同一张图交给平台所有尺寸槽位，Unity 按槽位（16~256）自动缩放生成 .ico。
    /// </summary>
    const string AppIconAsset = "Assets/Icon/AppIcon.png";

    /// <summary>
    /// 运行期数据目录：必须与 exe 同级。
    /// 这些目录放在工程根（不在 Assets 下），不参与编译，所以要随产物一起铺出去。
    /// </summary>
    static readonly string[] RuntimeDataDirectories = new string[]
    {
        "cdb",
        "config",
        "data",
        "deck",
        "expansions",
        "pack",
        "puzzle",
        "replay",
        "sound",
        "texture",
        "tools",
        "WindBot",
    };

    /// <summary>人机模式的外挂进程，必须与 exe 同级。</summary>
    static readonly string[] RuntimeDataFiles = new string[]
    {
        "AI.Server.exe",
    };

    /// <summary>
    /// 卡图目录体积很大（约 1.5 GB / 8700 个文件），铺设耗时明显。
    /// 出正式包时保持 true；只想快速验证功能时临时改为 false，
    /// 产物缺卡图只影响卡面显示，不影响对战逻辑（目录本身仍会建出来）。
    /// </summary>
    const bool StagePictures = true;
    const string PictureDirectory = "picture";

    /// <summary>工程根目录（编辑器下 Application.dataPath 为 &lt;工程&gt;/Assets）。</summary>
    static string ProjectRoot
    {
        get { return Path.GetDirectoryName(Application.dataPath); }
    }

    static void BuildWindows()
    {
        ApplyPlayerIcon();

        string[] levels = { MainScene };
        string productName = PlayerSettings.productName;
        string outputDirectory = Path.Combine(ProjectRoot, OutputDirectory);
        string exePath = Path.Combine(outputDirectory, productName + ".exe");

        Directory.CreateDirectory(outputDirectory);

        BuildPlayerOptions options = new BuildPlayerOptions();
        options.scenes = levels;
        options.locationPathName = exePath;
        options.target = BuildTarget.StandaloneWindows64;
        options.options = BuildOptions.None;

        BuildReport report = BuildPipeline.BuildPlayer(options);
        if (report.summary.result != BuildResult.Succeeded)
        {
            throw new System.Exception(
                "Windows 构建失败：" + report.summary.result
                + "（错误 " + report.summary.totalErrors + " 项）"
            );
        }

        StageWindowsRuntime(outputDirectory);
        RemoveBurstDebugInformation(outputDirectory);
        Debug.Log("BUILD_OK " + exePath);
    }

    /// <summary>
    /// 把 AppIcon.png 设为 Windows 平台的应用图标（写入 PlayerSettings，构建时嵌进 exe）。
    /// 导入设置要显式钉住：可读、关闭 mipmap、不缩 NPOT——否则默认导入可能把图
    /// 缩放/处理得不适合做图标槽位输入。
    /// </summary>
    static void ApplyPlayerIcon()
    {
        TextureImporter importer = AssetImporter.GetAtPath(AppIconAsset) as TextureImporter;
        if (importer == null)
        {
            Debug.LogWarning("找不到图标资源 " + AppIconAsset + "，本次构建沿用现有 exe 图标");
            return;
        }
        if (importer.isReadable != true || importer.npotScale != TextureImporterNPOTScale.None)
        {
            importer.isReadable = true;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.maxTextureSize = 512;
            importer.SaveAndReimport();
        }

        Texture2D icon = AssetDatabase.LoadAssetAtPath<Texture2D>(AppIconAsset);
        if (icon == null)
        {
            Debug.LogWarning("图标资源导入失败：" + AppIconAsset + "，本次构建沿用现有 exe 图标");
            return;
        }

        int[] sizes = PlayerSettings.GetIconSizesForTargetGroup(BuildTargetGroup.Standalone);
        Texture2D[] icons = new Texture2D[sizes.Length];
        for (int i = 0; i < sizes.Length; i++)
        {
            icons[i] = icon;
        }
        PlayerSettings.SetIconsForTargetGroup(BuildTargetGroup.Standalone, icons);
        Debug.Log("已设置 exe 图标：" + AppIconAsset + "（" + icon.width + "x" + icon.height
            + "，覆盖 " + sizes.Length + " 个尺寸槽位）");
    }

    /// <summary>
    /// 把工程根下的运行期数据铺到产物目录（与 exe 同级）。
    /// 逐文件合并覆盖，不做整体删除：游戏首次运行会在同一位置生成/更新内容，
    /// 整体重建会把这些文件一起清掉，而缺失文件未必能再次生成。
    /// </summary>
    static void StageWindowsRuntime(string outputDirectory)
    {
        foreach (string name in RuntimeDataFiles)
        {
            string source = Path.Combine(ProjectRoot, name);
            if (!File.Exists(source))
            {
                Debug.LogWarning("缺少运行期文件，产物将不含：" + name);
                continue;
            }
            File.Copy(source, Path.Combine(outputDirectory, name), true);
            Debug.Log("已铺入运行期文件：" + name);
        }

        foreach (string name in RuntimeDataDirectories)
        {
            string source = Path.Combine(ProjectRoot, name);
            if (!Directory.Exists(source))
            {
                Debug.LogWarning("缺少运行期目录，产物将不含：" + name);
                continue;
            }
            CopyDirectoryMerging(source, Path.Combine(outputDirectory, name));
            Debug.Log("已铺入运行期目录：" + name);
        }

        string pictureSource = Path.Combine(ProjectRoot, PictureDirectory);
        string pictureTarget = Path.Combine(outputDirectory, PictureDirectory);
        if (StagePictures && Directory.Exists(pictureSource))
        {
            CopyDirectoryMerging(pictureSource, pictureTarget);
            Debug.Log("已铺入卡图目录：" + PictureDirectory);
        }
        else
        {
            // 即使不铺卡图也要建出目录：游戏会按目录读卡面，缺目录比空目录更糟。
            Directory.CreateDirectory(pictureTarget);
            Debug.Log("卡图目录未铺入内容（StagePictures=" + StagePictures + "），已建空目录");
        }
    }

    /// <summary>把 source 的内容合并复制到 destination：同名文件覆盖，其余保留。</summary>
    static void CopyDirectoryMerging(string source, string destination)
    {
        Directory.CreateDirectory(destination);
        foreach (string file in Directory.GetFiles(source))
        {
            File.Copy(file, Path.Combine(destination, Path.GetFileName(file)), true);
        }
        foreach (string directory in Directory.GetDirectories(source))
        {
            CopyDirectoryMerging(
                directory,
                Path.Combine(destination, Path.GetFileName(directory))
            );
        }
    }

    /// <summary>
    /// Burst 编译会在输出目录留下 *_BurstDebugInformation_DoNotShip（名字本身就写着别随包发），
    /// 正式产物不需要，构建后清掉。
    /// </summary>
    static void RemoveBurstDebugInformation(string outputDirectory)
    {
        string[] leftovers = Directory.GetDirectories(outputDirectory, "*_DoNotShip");
        foreach (string directory in leftovers)
        {
            try
            {
                FileUtil.DeleteFileOrDirectory(directory);
                Debug.Log("已清理 Burst 调试目录：" + Path.GetFileName(directory));
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("清理 Burst 调试目录失败：" + e.Message);
            }
        }
    }
}
