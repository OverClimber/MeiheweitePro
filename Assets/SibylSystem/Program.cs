using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System;
using Ionic.Zip;
using System.Text;

public class Program : MonoBehaviour
{

    #region Resources
    public Camera main_camera;
    public facer face;
    public Light light;
    public AudioSource audio;
    public AudioClip zhankai;
    public GameObject mod_ui_2d;
    public GameObject mod_ui_3d;
    public GameObject mod_winExplode;
    public GameObject mod_loseExplode;
    public GameObject mod_audio_effect;
    public GameObject mod_ocgcore_card;
    public GameObject mod_ocgcore_card_cloude;
    public GameObject mod_ocgcore_card_number_shower;
    public GameObject mod_ocgcore_card_figure_line;
    public GameObject mod_ocgcore_hidden_button;
    public GameObject mod_ocgcore_coin;
    public GameObject mod_ocgcore_dice;
    public GameObject mod_simple_quad;
    public GameObject mod_simple_ngui_background_texture;
    public GameObject mod_simple_ngui_text;
    public GameObject mod_ocgcore_number;
    public GameObject mod_ocgcore_decoration_chain_selecting;
    public GameObject mod_ocgcore_decoration_card_selected;
    public GameObject mod_ocgcore_decoration_card_selecting;
    public GameObject mod_ocgcore_decoration_card_active;
    public GameObject mod_ocgcore_decoration_spsummon;
    public GameObject mod_ocgcore_decoration_thunder;
    public GameObject mod_ocgcore_decoration_trap_activated;
    public GameObject mod_ocgcore_decoration_magic_activated;
    public GameObject mod_ocgcore_decoration_magic_zhuangbei;
    public GameObject mod_ocgcore_decoration_removed;
    public GameObject mod_ocgcore_decoration_tograve;
    public GameObject mod_ocgcore_decoration_card_setted;
    public GameObject mod_ocgcore_blood;
    public GameObject mod_ocgcore_blood_screen;
    public GameObject mod_ocgcore_bs_atk_decoration;
    public GameObject mod_ocgcore_bs_atk_line_earth;
    public GameObject mod_ocgcore_bs_atk_line_water;
    public GameObject mod_ocgcore_bs_atk_line_fire;
    public GameObject mod_ocgcore_bs_atk_line_wind;
    public GameObject mod_ocgcore_bs_atk_line_dark;
    public GameObject mod_ocgcore_bs_atk_line_light;
    public GameObject mod_ocgcore_cs_chaining;
    public GameObject mod_ocgcore_cs_end;
    public GameObject mod_ocgcore_cs_bomb;
    public GameObject mod_ocgcore_cs_negated;
    public GameObject mod_ocgcore_cs_mon_earth;
    public GameObject mod_ocgcore_cs_mon_water;
    public GameObject mod_ocgcore_cs_mon_fire;
    public GameObject mod_ocgcore_cs_mon_wind;
    public GameObject mod_ocgcore_cs_mon_light;
    public GameObject mod_ocgcore_cs_mon_dark;
    public GameObject mod_ocgcore_ss_summon_earth;
    public GameObject mod_ocgcore_ss_summon_water;
    public GameObject mod_ocgcore_ss_summon_fire;
    public GameObject mod_ocgcore_ss_summon_wind;
    public GameObject mod_ocgcore_ss_summon_dark;
    public GameObject mod_ocgcore_ss_summon_light;
    public GameObject mod_ocgcore_ol_earth;
    public GameObject mod_ocgcore_ol_water;
    public GameObject mod_ocgcore_ol_fire;
    public GameObject mod_ocgcore_ol_wind;
    public GameObject mod_ocgcore_ol_dark;
    public GameObject mod_ocgcore_ol_light;
    public GameObject mod_ocgcore_ss_spsummon_normal;
    public GameObject mod_ocgcore_ss_spsummon_ronghe;
    public GameObject mod_ocgcore_ss_spsummon_tongtiao;
    public GameObject mod_ocgcore_ss_spsummon_yishi;
    public GameObject mod_ocgcore_ss_spsummon_link;
    public GameObject mod_ocgcore_ss_p_idle_effect;
    public GameObject mod_ocgcore_ss_p_sum_effect;
    public GameObject mod_ocgcore_ss_dark_hole;
    public GameObject mod_ocgcore_ss_link_mark;
    public GameObject new_ui_menu;
    public GameObject new_ui_setting;
    public GameObject new_ui_book;
    public GameObject new_ui_selectServer;
    // ⚠ 占位字段，**不要删**。
    //
    // Unity 的 MonoBehaviour 序列化数据是按字段声明顺序读写的（字段名不进数据流），
    // 而这个字段后面跟着 new_ui_gameInfo / new_ui_cardDescription / … / remaster_* 一长串
    // UnityEngine.Object 引用。少一个字段，后面整块就错位：运行期会打印
    // 「A scripted object (probably Program?) has a different serialization layout
    //  when loading」，然后那批引用全部读成 null —— 表现为 selectDeck 构造时空引用、
    // 主菜单永远不显示，卡在启动画面。
    //
    // 本版不使用它，只求把位置占住。字段名可以随意改（名字不参与序列化）。
    public GameObject new_ui_legacySlot;
    public GameObject new_ui_gameInfo;
    public GameObject new_ui_cardDescription;
    public GameObject new_ui_search;
    public GameObject new_ui_searchDetailed;
    public GameObject new_ui_cardOnSearchList;
    public GameObject new_bar_changeSide;
    public GameObject new_bar_duel;
    public GameObject new_bar_room;
    public GameObject new_bar_editDeck;
    public GameObject new_bar_watchDuel;
    public GameObject new_bar_watchRecord;
    public GameObject new_mod_cardInDeckManager;
    public GameObject new_mod_tableInDeckManager;
    public GameObject new_ui_handShower;
    public GameObject new_ui_textMesh;
    public GameObject new_ui_superButton;
    public GameObject new_ui_superButtonTransparent;
    public GameObject new_ui_aiRoom;
    public GameObject new_ocgcore_field;
    public GameObject new_ocgcore_chainCircle;
    public GameObject new_ocgcore_wait;
    public GameObject new_mouse;
    public GameObject remaster_deckManager;
    public GameObject remaster_replayManager;
    public GameObject remaster_puzzleManager;
    public GameObject remaster_tagRoom;
    public GameObject remaster_room;
    public GameObject ES_1;
    public GameObject ES_2;
    public GameObject ES_2Force;
    public GameObject ES_3cancle;
    public GameObject ES_Single_multiple_window;
    public GameObject ES_Single_option;
    public GameObject ES_multiple_option;
    public GameObject ES_input;
    public GameObject ES_position;
    public GameObject ES_position3;
    public GameObject ES_Tp;
    public GameObject ES_Face;
    public GameObject ES_FS;
    public GameObject Pro1_CardShower;
    public GameObject Pro1_superCardShower;
    public GameObject Pro1_superCardShowerA;
    public GameObject New_arrow;
    public GameObject New_selectKuang;
    public GameObject New_chainKuang;
    public GameObject New_phase;
    public GameObject New_decker;
    public GameObject New_winCaculator;
    public GameObject New_winCaculatorRecord;
    public GameObject New_ocgcore_placeSelector;
    #endregion

    #region Initializement

    private static Program instance;

    public static Program I()
    {
        return instance;
    }

    public static int TimePassed()
    {
        return (int)(Time.time * 1000f);
    }

    private List<GameObject> allObjects = new List<GameObject>();

    void loadResource(GameObject g)
    {
        try
        {
            GameObject obj = GameObject.Instantiate(g) as GameObject;
            obj.SetActive(false);
            allObjects.Add(obj);
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.Log(e);
        }
    }

    void loadResources()
    {

        loadResource(mod_audio_effect);
        loadResource(mod_ocgcore_card);
        loadResource(mod_ocgcore_card_cloude);
        loadResource(mod_ocgcore_card_number_shower);
        loadResource(mod_ocgcore_card_figure_line);
        loadResource(mod_ocgcore_hidden_button);
        loadResource(mod_ocgcore_coin);
        loadResource(mod_ocgcore_dice);

        loadResource(mod_ocgcore_decoration_chain_selecting);
        loadResource(mod_ocgcore_decoration_card_selected);
        loadResource(mod_ocgcore_decoration_card_selecting);
        loadResource(mod_ocgcore_decoration_card_active);
        loadResource(mod_ocgcore_decoration_spsummon);
        loadResource(mod_ocgcore_decoration_thunder);
        loadResource(mod_ocgcore_cs_mon_earth);
        loadResource(mod_ocgcore_cs_mon_water);
        loadResource(mod_ocgcore_cs_mon_fire);
        loadResource(mod_ocgcore_cs_mon_wind);
        loadResource(mod_ocgcore_cs_mon_light);
        loadResource(mod_ocgcore_cs_mon_dark);
        loadResource(mod_ocgcore_decoration_trap_activated);
        loadResource(mod_ocgcore_decoration_magic_activated);
        loadResource(mod_ocgcore_decoration_magic_zhuangbei);

        loadResource(mod_ocgcore_decoration_removed);
        loadResource(mod_ocgcore_decoration_tograve);
        loadResource(mod_ocgcore_decoration_card_setted);
        loadResource(mod_ocgcore_blood);
        loadResource(mod_ocgcore_blood_screen);


        loadResource(mod_ocgcore_bs_atk_decoration);
        loadResource(mod_ocgcore_bs_atk_line_earth);
        loadResource(mod_ocgcore_bs_atk_line_water);
        loadResource(mod_ocgcore_bs_atk_line_fire);
        loadResource(mod_ocgcore_bs_atk_line_wind);
        loadResource(mod_ocgcore_bs_atk_line_dark);
        loadResource(mod_ocgcore_bs_atk_line_light);

        loadResource(mod_ocgcore_cs_chaining);
        loadResource(mod_ocgcore_cs_end);
        loadResource(mod_ocgcore_cs_bomb);
        loadResource(mod_ocgcore_cs_negated);

        loadResource(mod_ocgcore_ss_summon_earth);
        loadResource(mod_ocgcore_ss_summon_water);
        loadResource(mod_ocgcore_ss_summon_fire);
        loadResource(mod_ocgcore_ss_summon_wind);
        loadResource(mod_ocgcore_ss_summon_dark);
        loadResource(mod_ocgcore_ss_summon_light);

        loadResource(mod_ocgcore_ol_earth);
        loadResource(mod_ocgcore_ol_water);
        loadResource(mod_ocgcore_ol_fire);
        loadResource(mod_ocgcore_ol_wind);
        loadResource(mod_ocgcore_ol_dark);
        loadResource(mod_ocgcore_ol_light);

        loadResource(mod_ocgcore_ss_spsummon_normal);
        loadResource(mod_ocgcore_ss_spsummon_ronghe);
        loadResource(mod_ocgcore_ss_spsummon_tongtiao);
        loadResource(mod_ocgcore_ss_spsummon_link);
        loadResource(mod_ocgcore_ss_spsummon_yishi);
        loadResource(mod_ocgcore_ss_p_idle_effect);
        loadResource(mod_ocgcore_ss_p_sum_effect);
        loadResource(mod_ocgcore_ss_dark_hole);
        loadResource(mod_ocgcore_ss_link_mark);
    }

    public static float transparency = 0;

    //public static bool YGOPro1 = true;

    public static float getVerticalTransparency()
    {
        if (I().setting.setting.closeUp.value == false)
        {
            return 0;
        }
        return transparency;
    }

    public static GameObject ui_back_ground_2d = null;
    public static Camera camera_back_ground_2d = null;
    public static GameObject ui_container_3d = null;
    public static Camera camera_container_3d = null;
    public static Camera camera_game_main = null;
    public static GameObject ui_windows_2d = null;
    public static Camera camera_windows_2d = null;
    public static GameObject ui_main_2d = null;
    public static Camera camera_main_2d = null;
    public static GameObject ui_main_3d = null;
    public static Camera camera_main_3d = null;

    public static Vector3 cameraPosition = new Vector3(0, 23, -23);
    public static Vector3 cameraRotation = new Vector3(60, 0, 0);
    public static bool cameraFacing = false;

    public static float verticleScale = 5f;

    void initialize()
    {

        go(1, () =>
        {
            UIHelper.iniFaces();
            initializeALLcameras();
            fixALLcamerasPreFrame();
            backGroundPic = new BackGroundPic();
            servants.Add(backGroundPic);
            backGroundPic.fixScreenProblem();
        });
        go(300, () =>
        {
            InterString.initialize("config/translation.conf");
            GameTextureManager.initialize();
            Config.initialize("config/config.conf");
            // 卡组目录是运行期依赖：OCG 的 deck/ 由构建铺设，RD 的 deck_rd/ 只能运行期建。
            // 冷启动也要保证在（选卡组界面直接 DirectoryInfo 枚举，目录不在会抛）。
            GameModeManager.EnsureDeckDir();

            // 读盘之前先兑现上次没走完的数据更新事务：否则可能把「新一半旧一半」的
            // 三件套读进内存。回滚失败会置 Failed，之后的更新流程不会再动数据。
            ClientDataUpdater.RecoverTransaction();

            // RD 数据的 ypk 更新通道：rd/update/*.ypk 在任何数据库装载之前应用，
            // 装载读到的就是新数据（见 RdDataUpdater 头注释的包格式与失败口径）。
            RdDataUpdater.ApplyPendingPacks();

            if (!Directory.Exists("expansions"))
            {
                try
                {
                    Directory.CreateDirectory("expansions");
                }
                catch
                {
                }
            }

            if (!Directory.Exists("replay"))
            {
                try
                {
                    Directory.CreateDirectory("replay");
                }
                catch
                {
                }
            }

            var fileInfos = new FileInfo[0];

            // 起手先把上一轮遗留的调试开关作废掉（见 QuickTestTrace.SweepStaleSwitches）：
            // 残留的 qt_endduel.on 会让卡组编辑器一打开就自动开局，
            // 残留的 qt_autojoin.on 会让房间自动准备并自动答掉猜拳。
            QuickTestTrace.SweepStaleSwitches();

            // 启动耗时台账（只记数，不参与逻辑）：两池各花多久、各多少张。
            // 用来回答「RD 那套数据有没有拖慢 OCG 的启动」。
            System.Diagnostics.Stopwatch bootWatch = System.Diagnostics.Stopwatch.StartNew();

            if (Directory.Exists("expansions"))
            {
                fileInfos = (new DirectoryInfo("expansions")).GetFiles();
                foreach (FileInfo file in fileInfos)
                {
                    if (file.Name.ToLower().EndsWith(".ypk"))
                    {
                        GameZipManager.Zips.Add(new Ionic.Zip.ZipFile("expansions/" + file.Name));
                    }
                    if (file.Name.ToLower().EndsWith(".conf"))
                    {
                        GameStringManager.initialize("expansions/" + file.Name);
                    }
                    if (file.Name.ToLower().EndsWith(".cdb"))
                    {
                        YGOSharp.CardsManager.initialize("expansions/" + file.Name);
                    }
                }
            }

            if (Directory.Exists("cdb"))
            {
                fileInfos = (new DirectoryInfo("cdb")).GetFiles();
                foreach (FileInfo file in fileInfos)
                {
                    if (file.Name.ToLower().EndsWith(".conf"))
                    {
                        GameStringManager.initialize("cdb/" + file.Name);
                    }
                    if (file.Name.ToLower().EndsWith(".cdb"))
                    {
                        YGOSharp.CardsManager.initialize("cdb/" + file.Name);
                    }
                }
            }

            if (Directory.Exists("diy"))
            {
                fileInfos = (new DirectoryInfo("diy")).GetFiles();
                foreach (FileInfo file in fileInfos)
                {
                    if (file.Name.ToLower().EndsWith(".conf"))
                    {
                        GameStringManager.initialize("diy/" + file.Name);
                    }
                    if (file.Name.ToLower().EndsWith(".cdb"))
                    {
                        YGOSharp.CardsManager.initialize("diy/" + file.Name, true);
                    }
                }
            }

            if (Directory.Exists("data"))
            {
                fileInfos = (new DirectoryInfo("data")).GetFiles();
                foreach (FileInfo file in fileInfos)
                {
                    if (file.Name.ToLower().EndsWith(".zip"))
                    {
                        GameZipManager.Zips.Add(new Ionic.Zip.ZipFile("data/" + file.Name));
                    }
                }
            }

            foreach (ZipFile zip in GameZipManager.Zips)
            {
                if (zip.Name.ToLower().EndsWith("script.zip"))
                    continue;
                foreach (string file in zip.EntryFileNames)
                {
                    if (file.ToLower().EndsWith(".conf"))
                    {
                        MemoryStream ms = new MemoryStream();
                        ZipEntry e = zip[file];
                        e.Extract(ms);
                        GameStringManager.initializeContent(Encoding.UTF8.GetString(ms.ToArray()));
                    }
                    if (file.ToLower().EndsWith(".cdb"))
                    {
                        ZipEntry e = zip[file];
                        string tempfile = Path.Combine(Path.GetTempPath(), file);
                        e.Extract(Path.GetTempPath(), ExtractExistingFileAction.OverwriteSilently);
                        YGOSharp.CardsManager.initialize(tempfile, true);
                        File.Delete(tempfile);
                    }
                }
            }

            // ===== RD（超速决斗）数据通道：只认 rd/ 子目录，全部进 RD 池 =====
            // 路径与文件由 _unpack_rd.py 分发（见 _plan_rdmode.md §2「数据分发」）。
            // 目录不存在 = 没装 RD 数据 → 静默跳过：RD 模式会显示空卡池，而不是起不来。
            QuickTestTrace.Log("boot", "OCG 池装载 " + YGOSharp.CardsManager.CountOf(false)
                + " 张，耗时 " + bootWatch.ElapsedMilliseconds + "ms");
            bootWatch.Restart();
            LoadRdDatabase();
            QuickTestTrace.Log("boot", "RD 池装载 " + YGOSharp.CardsManager.CountOf(true)
                + " 张，耗时 " + bootWatch.ElapsedMilliseconds + "ms"
                + "；两池合计 " + (YGOSharp.CardsManager.CountOf(false) + YGOSharp.CardsManager.CountOf(true))
                + " 张");

            GameStringManager.initialize("config/strings.conf");
            YGOSharp.BanlistManager.initialize("config/lflist.conf");
            // RD 禁限表走**单独一个文件**追加，不并进 config/lflist.conf：
            // config/ 每次构建都被工程模板覆盖，在线卡表更新（ClientDataUpdater）也会整文件替换
            // —— 并进去的 RD 表会隔三差五消失。rd/ 是随包分发的数据，稳。
            YGOSharp.BanlistManager.AppendIfExists("rd/lflist.conf");

            YGOSharp.CardsManager.updateSetNames();

            if (Directory.Exists("pack"))
            {
                fileInfos = (new DirectoryInfo("pack")).GetFiles();
                foreach (FileInfo file in fileInfos)
                {
                    if (file.Name.ToLower().EndsWith(".db"))
                    {
                        YGOSharp.PacksManager.initialize("pack/" + file.Name);
                    }
                }
                YGOSharp.PacksManager.initializeSec();
            }

            initializeALLservants();
            loadResources();
            readParams();

            // 启动即检查在线数据更新（卡片库/禁限表/卡牌文本三件套）。
            // 不阻塞启动；CDN 不可达时只是把状态标成失败，不影响进游戏。
            StartCoroutine(ClientDataUpdater.UpdateCoroutine());
        });

    }

    /// <summary>
    /// RD 卡表装载：rd/cdb/*.cdb → CardsManager 的 **RD 池**（与 OCG 池彻底分离）。
    ///
    /// 启动链与 ReloadGameDatabases 共用这一处 —— 别再各写一份（同一个坑会修两遍）。
    /// rd/ 不在构建铺设名单里（卡图/立绘 ~700MB，不想每次构建都拷），
    /// 由 <c>_unpack_rd.py</c> 分发；重建 output/ 之后必须重跑那个脚本。
    /// </summary>
    private void LoadRdDatabase()
    {
        if (!Directory.Exists("rd/cdb"))
        {
            return;
        }
        FileInfo[] files = (new DirectoryInfo("rd/cdb")).GetFiles();
        for (int i = 0; i < files.Length; i++)
        {
            if (files[i].Name.ToLower().EndsWith(".cdb"))
            {
                YGOSharp.CardsManager.initializeRD("rd/cdb/" + files[i].Name);
            }
        }
    }

    /// <summary>
    /// 按启动时完全相同的顺序重建内存里的数据：清空三件套后重放
    /// expansions → cdb → diy → data 里的 zip → config，最后 updateSetNames。
    /// 顺序不能改：后面几层是叠加在 cdb/cards.cdb 之上的补充与覆盖。
    /// 只重读文件、不重装 Unity 资源，所以可以在游戏内直接生效、无需重启。
    /// </summary>
    public bool ReloadGameDatabases()
    {
        try
        {
            GameStringManager.Reset();
            YGOSharp.CardsManager.Reset();
            YGOSharp.BanlistManager.Reset();

            var fileInfos = new FileInfo[0];

            if (Directory.Exists("expansions"))
            {
                fileInfos = (new DirectoryInfo("expansions")).GetFiles();
                foreach (FileInfo file in fileInfos)
                {
                    if (file.Name.ToLower().EndsWith(".conf"))
                    {
                        GameStringManager.initialize("expansions/" + file.Name);
                    }
                    if (file.Name.ToLower().EndsWith(".cdb"))
                    {
                        YGOSharp.CardsManager.initialize("expansions/" + file.Name);
                    }
                }
            }

            if (Directory.Exists("cdb"))
            {
                fileInfos = (new DirectoryInfo("cdb")).GetFiles();
                foreach (FileInfo file in fileInfos)
                {
                    if (file.Name.ToLower().EndsWith(".conf"))
                    {
                        GameStringManager.initialize("cdb/" + file.Name);
                    }
                    if (file.Name.ToLower().EndsWith(".cdb"))
                    {
                        YGOSharp.CardsManager.initialize("cdb/" + file.Name);
                    }
                }
            }

            if (Directory.Exists("diy"))
            {
                fileInfos = (new DirectoryInfo("diy")).GetFiles();
                foreach (FileInfo file in fileInfos)
                {
                    if (file.Name.ToLower().EndsWith(".conf"))
                    {
                        GameStringManager.initialize("diy/" + file.Name);
                    }
                    if (file.Name.ToLower().EndsWith(".cdb"))
                    {
                        YGOSharp.CardsManager.initialize("diy/" + file.Name, true);
                    }
                }
            }

            // zip 句柄启动时已装进 GameZipManager.Zips，这里只重放内容，不重新打开文件。
            foreach (ZipFile zip in GameZipManager.Zips)
            {
                if (zip.Name.ToLower().EndsWith("script.zip"))
                {
                    continue;
                }
                foreach (string entry in zip.EntryFileNames)
                {
                    if (entry.ToLower().EndsWith(".conf"))
                    {
                        MemoryStream ms = new MemoryStream();
                        zip[entry].Extract(ms);
                        GameStringManager.initializeContent(Encoding.UTF8.GetString(ms.ToArray()));
                    }
                    if (entry.ToLower().EndsWith(".cdb"))
                    {
                        string tempfile = Path.Combine(Path.GetTempPath(), entry);
                        zip[entry].Extract(Path.GetTempPath(), ExtractExistingFileAction.OverwriteSilently);
                        YGOSharp.CardsManager.initialize(tempfile, true);
                        File.Delete(tempfile);
                    }
                }
            }

            LoadRdDatabase();

            GameStringManager.initialize("config/strings.conf");
            YGOSharp.BanlistManager.initialize("config/lflist.conf");
            YGOSharp.BanlistManager.AppendIfExists("rd/lflist.conf");
            YGOSharp.CardsManager.updateSetNames();

            return true;
        }
        catch (Exception e)
        {
            DEBUGLOG(e);
            return false;
        }
    }

    void readParams()
    {
        var args = Environment.GetCommandLineArgs();
        string nick = null;
        string host = null;
        string port = null;
        string password = null;
        string deck = null;
        string replay = null;
        string puzzle = null;
        bool join = false;
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i].ToLower() == "-n" && args.Length > i + 1)
            {
                nick = args[++i];
                if (nick.Contains(" "))
                    nick = "\"" + nick + "\"";
            }
            if (args[i].ToLower() == "-h" && args.Length > i + 1)
            {
                host = args[++i];
            }
            if (args[i].ToLower() == "-p" && args.Length > i + 1)
            {
                port = args[++i];
            }
            if (args[i].ToLower() == "-w" && args.Length > i + 1)
            {
                password = args[++i];
                if (password.Contains(" "))
                    password = "\"" + password + "\"";
            }
            if (args[i].ToLower() == "-d" && args.Length > i + 1)
            {
                deck = args[++i];
                if (deck.Contains(" "))
                    deck = "\"" + deck + "\"";
            }
            if (args[i].ToLower() == "-r" && args.Length > i + 1)
            {
                replay = args[++i];
                if (replay.Contains(" "))
                    replay = "\"" + replay + "\"";
            }
            if (args[i].ToLower() == "-s" && args.Length > i + 1)
            {
                puzzle = args[++i];
                if (puzzle.Contains(" "))
                    puzzle = "\"" + puzzle + "\"";
            }
            if (args[i].ToLower() == "-j")
            {
                join = true;
                GameModeManager.SetDeckInUse(deck);
            }
        }
        string cmdFile = "commamd.shell";
        if (join)
        {
            File.WriteAllText(cmdFile, "online " + nick + " " + host + " " + port + " 0x233 " + password, Encoding.UTF8);
            Program.exitOnReturn = true;
        }
        else if (deck != null)
        {
            File.WriteAllText(cmdFile, "edit " + deck, Encoding.UTF8);
            Program.exitOnReturn = true;
        }
        else if (replay != null)
        {
            File.WriteAllText(cmdFile, "replay " + replay, Encoding.UTF8);
            Program.exitOnReturn = true;
        }
        else if (puzzle != null)
        {
            File.WriteAllText(cmdFile, "puzzle " + puzzle, Encoding.UTF8);
            Program.exitOnReturn = true;
        }
    }

    public GameObject mouseParticle;

    static int lastChargeTime = 0;
    public static void charge()
    {
        if (Program.TimePassed() - lastChargeTime > 5 * 60 * 1000)
        {
            lastChargeTime = Program.TimePassed();
            try
            {
                GameTextureManager.clearAll();
                Resources.UnloadUnusedAssets();
                GC.Collect();
            }
            catch (System.Exception e)
            {
                UnityEngine.Debug.Log(e);
            }
        }
    }

    #endregion

    #region Tools

    public static GameObject pointedGameObject = null;

    public static Collider pointedCollider = null;

    public static bool InputGetMouseButtonDown_0;

    public static bool InputGetMouseButton_0;

    public static bool InputGetMouseButtonUp_0;

    public static bool InputGetMouseButtonDown_1;

    public static bool InputGetMouseButtonUp_1;

    public static bool InputEnterDown = false;

    public static float wheelValue = 0;

    public class delayedTask
    {
        public int timeToBeDone;
        public Action act;
    }

    static List<delayedTask> delayedTasks = new List<delayedTask>();

    public static void go(int delay_, Action act_)
    {
        delayedTasks.Add(new delayedTask
        {
            act = act_,
            timeToBeDone = delay_ + Program.TimePassed(),
        });
    }

    public static void notGo(Action act_)
    {
        List<delayedTask> rem = new List<delayedTask>();
        for (int i = 0; i < delayedTasks.Count; i++)
        {
            if (delayedTasks[i].act == act_)
            {
                rem.Add(delayedTasks[i]);
            }
        }
        for (int i = 0; i < rem.Count; i++)
        {
            delayedTasks.Remove(rem[i]);
        }
        rem.Clear();
    }

    int rayFilter = 0;

    public void initializeALLcameras()
    {
        for (int i = 0; i < 32; i++)
        {
            if (i == 15)
            {
                continue;
            }
            rayFilter |= (int)Math.Pow(2, i);
        }

        if (camera_game_main == null)
        {
            camera_game_main = this.main_camera;
        }
        camera_game_main.transform.position = new Vector3(0, 23, -23);
        camera_game_main.transform.eulerAngles = new Vector3(60, 0, 0);
        camera_game_main.transform.localScale = new Vector3(1, 1, 1);
        camera_game_main.rect = new Rect(0, 0, 1, 1);
        camera_game_main.depth = 0;
        camera_game_main.gameObject.layer = 0;
        camera_game_main.clearFlags = CameraClearFlags.Depth;

        if (ui_back_ground_2d == null)
        {
            ui_back_ground_2d = create(mod_ui_2d);
            camera_back_ground_2d = ui_back_ground_2d.transform.Find("Camera").GetComponent<Camera>();
        }
        camera_back_ground_2d.depth = -2;
        ui_back_ground_2d.layer = 8;
        ui_back_ground_2d.transform.Find("Camera").gameObject.layer = 8;
        camera_back_ground_2d.cullingMask = (int)Mathf.Pow(2, 8);
        camera_back_ground_2d.clearFlags = CameraClearFlags.Depth;

        if (ui_container_3d == null)
        {
            ui_container_3d = create(mod_ui_3d);
            camera_container_3d = ui_container_3d.transform.Find("Camera").GetComponent<Camera>();
        }
        camera_container_3d.depth = -1;
        ui_container_3d.layer = 9;
        ui_container_3d.transform.Find("Camera").gameObject.layer = 9;
        camera_container_3d.cullingMask = (int)Mathf.Pow(2, 9);
        camera_container_3d.fieldOfView = 75;
        camera_container_3d.rect = camera_game_main.rect;
        camera_container_3d.transform.position = new Vector3(0, 23, -23);
        camera_container_3d.transform.eulerAngles = new Vector3(60, 0, 0);
        camera_container_3d.transform.localScale = new Vector3(1, 1, 1);
        camera_container_3d.rect = new Rect(0, 0, 1, 1);
        camera_container_3d.clearFlags = CameraClearFlags.Depth;



        if (ui_main_2d == null)
        {
            ui_main_2d = create(mod_ui_2d);
            camera_main_2d = ui_main_2d.transform.Find("Camera").GetComponent<Camera>();
        }
        camera_main_2d.depth = 3;
        ui_main_2d.layer = 11;
        ui_main_2d.transform.Find("Camera").gameObject.layer = 11;
        camera_main_2d.cullingMask = (int)Mathf.Pow(2, 11);
        camera_main_2d.clearFlags = CameraClearFlags.Depth;


        if (ui_windows_2d == null)
        {
            ui_windows_2d = create(mod_ui_2d);
            camera_windows_2d = ui_windows_2d.transform.Find("Camera").GetComponent<Camera>();
        }
        camera_windows_2d.depth = 2;
        ui_windows_2d.layer = 19;
        ui_windows_2d.transform.Find("Camera").gameObject.layer = 19;
        camera_windows_2d.cullingMask = (int)Mathf.Pow(2, 19);
        camera_windows_2d.clearFlags = CameraClearFlags.Depth;


        if (ui_main_3d == null)
        {
            ui_main_3d = create(mod_ui_3d);
            camera_main_3d = ui_main_3d.transform.Find("Camera").GetComponent<Camera>();
        }
        camera_main_3d.depth = 1;
        ui_main_3d.layer = 10;
        ui_main_3d.transform.Find("Camera").gameObject.layer = 10;
        camera_main_3d.cullingMask = (int)Mathf.Pow(2, 10);
        camera_main_3d.fieldOfView = 75;
        camera_main_3d.rect = new Rect(0, 0, 1, 1);
        camera_main_3d.transform.position = new Vector3(0, 23, -23);
        camera_main_3d.transform.eulerAngles = new Vector3(60, 0, 0);
        camera_main_3d.transform.localScale = new Vector3(1, 1, 1);
        camera_main_3d.clearFlags = CameraClearFlags.Depth;




        camera_main_3d.transform.localPosition = camera_game_main.transform.position;
        camera_container_3d.transform.localPosition = camera_game_main.transform.position;

        camera_main_3d.transform.localEulerAngles = camera_game_main.transform.localEulerAngles;
        camera_container_3d.transform.localEulerAngles = camera_game_main.transform.localEulerAngles;

        camera_main_3d.fieldOfView = camera_game_main.fieldOfView;
        camera_container_3d.fieldOfView = camera_game_main.fieldOfView;

        camera_main_3d.rect = camera_game_main.rect;
        camera_container_3d.rect = camera_game_main.rect;
    }

    public static float deltaTime = 1f / 120f;

    public void fixALLcamerasPreFrame()
    {
        deltaTime = Time.deltaTime;
        if (deltaTime > 1f / 40f)
        {
            deltaTime = 1f / 40f;
        }
        if (camera_game_main != null)
        {
            camera_game_main.transform.position += (cameraPosition - camera_game_main.transform.position) * deltaTime * 3.5f;
            camera_container_3d.transform.localPosition = camera_game_main.transform.position;
            if (cameraFacing == false)
            {
                camera_game_main.transform.localEulerAngles += (cameraRotation - camera_game_main.transform.localEulerAngles) * deltaTime * 3.5f;
            }
            else
            {
                camera_game_main.transform.LookAt(Vector3.zero);
            }
            camera_container_3d.transform.localEulerAngles = camera_game_main.transform.localEulerAngles;
            camera_container_3d.fieldOfView = camera_game_main.fieldOfView;
            camera_container_3d.rect = camera_game_main.rect;
        }
    }

    public void fixScreenProblems()
    {
        for (int i = 0; i < servants.Count; i++)
        {
            servants[i].fixScreenProblem();
        }
    }

    public GameObject create(
        GameObject mod,
        Vector3 position = default(Vector3),
        Vector3 rotation = default(Vector3),
        bool fade = false,
        GameObject father = null,
        bool allParamsInWorld = true,
        Vector3 wantScale = default(Vector3)
        )
    {
        Vector3 scale = mod.transform.localScale;
        if (wantScale != default(Vector3))
        {
            scale = wantScale;
        }
        GameObject return_value = (GameObject)MonoBehaviour.Instantiate(mod);
        if (position != default(Vector3))
        {
            return_value.transform.position = position;
        }
        else
        {
            return_value.transform.position = Vector3.zero;
        }
        if (rotation != default(Vector3))
        {
            return_value.transform.eulerAngles = rotation;
        }
        else
        {
            return_value.transform.eulerAngles = Vector3.zero;
        }
        if (father != null)
        {
            return_value.transform.SetParent(father.transform, false);
            return_value.layer = father.layer;
            if (allParamsInWorld == true)
            {
                return_value.transform.position = position;
                return_value.transform.localScale = scale;
                return_value.transform.eulerAngles = rotation;
            }
            else
            {
                return_value.transform.localPosition = position;
                return_value.transform.localScale = scale;
                return_value.transform.localEulerAngles = rotation;
            }
        }
        else
        {
            return_value.layer = 0;
        }
        Transform[] Transforms = return_value.GetComponentsInChildren<Transform>();
        foreach (Transform child in Transforms)
        {
            child.gameObject.layer = return_value.layer;
        }
        if (fade == true)
        {
            return_value.transform.localScale = Vector3.zero;
            iTween.ScaleToE(return_value, scale, 0.3f);
        }
        return return_value;
    }

    public void destroy(GameObject obj, float time = 0, bool fade = false, bool instantNull = false)
    {
        try
        {
            if (obj != null)
            {
                if (fade)
                {
                    iTween.ScaleTo(obj, Vector3.zero, 0.4f);
                    MonoBehaviour.Destroy(obj, 0.6f);
                }
                else
                {
                    if (time != 0) MonoBehaviour.Destroy(obj, time);
                    else MonoBehaviour.Destroy(obj);
                }
                if (instantNull)
                {
                    obj = null;
                }
            }
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.Log(e);
        }
    }

    //public static void shiftCameraPan(Camera camera, bool enabled)
    //{
    //    cameraPaning = enabled;
    //    PanWithMouse panWithMouse = camera.gameObject.GetComponent<PanWithMouse>();
    //    if (panWithMouse == null)
    //    {
    //        panWithMouse = camera.gameObject.AddComponent<PanWithMouse>();
    //    }
    //    panWithMouse.enabled = enabled;
    //    if (enabled == false)
    //    {
    //        iTween.RotateTo(camera.gameObject, new Vector3(60, 0, 0), 0.6f);
    //    }
    //}

    public static void reMoveCam(float xINscreen)
    {
        float all = (float)Screen.width / 2f;
        float it = xINscreen - (float)Screen.width / 2f;
        float val = it / all;
        camera_game_main.rect = new Rect(val, 0, 1, 1);
        camera_container_3d.rect = camera_game_main.rect;
        camera_main_3d.rect = camera_game_main.rect;
    }

    public static void ShiftUIenabled(GameObject ui, bool enabled)
    {
        var all = ui.GetComponentsInChildren<BoxCollider>();
        for (int i = 0; i < all.Length; i++)
        {
            all[i].enabled = enabled;
        }
    }

    public static Texture2D GetTextureViaPath(string path)
    {
        FileStream file = new FileStream(path, FileMode.Open, FileAccess.Read);
        file.Seek(0, SeekOrigin.Begin);
        byte[] data = new byte[file.Length];
        file.Read(data, 0, (int)file.Length);
        file.Close();
        file.Dispose();
        file = null;
        Texture2D pic = new Texture2D(1024, 600);
        pic.LoadImage(data);
        return pic;
    }

    /// <summary>
    /// 用 SharpZipLib 把内存中的 zip/ypk 数据解压到指定目录（超先行卡包安装用）。
    /// 这里一律使用完全限定名，避免与工程既有的 Ionic.Zip.ZipFile 产生类型歧义。
    /// </summary>
    public void ExtractZipFile(byte[] data, string outFolder)
    {
        ICSharpCode.SharpZipLib.Zip.ZipFile zf = null;
        try
        {
            //use MemoryStream!!!!
            using (MemoryStream mstrm = new MemoryStream(data))
            {
                zf = new ICSharpCode.SharpZipLib.Zip.ZipFile(mstrm);

                foreach (ICSharpCode.SharpZipLib.Zip.ZipEntry zipEntry in zf)
                {
                    if (!zipEntry.IsFile)
                    {
                        continue;
                    }

                    string entryFileName = zipEntry.Name;
                    byte[] buffer = new byte[4096]; // 4K is optimum
                    Stream zipStream = zf.GetInputStream(zipEntry);

                    string fullZipToPath = Path.Combine(outFolder, entryFileName);
                    string directoryName = Path.GetDirectoryName(fullZipToPath);
                    if (directoryName.Length > 0)
                    {
                        Directory.CreateDirectory(directoryName);
                    }
                    using (FileStream streamWriter = File.Create(fullZipToPath))
                    {
                        ICSharpCode.SharpZipLib.Core.StreamUtils.Copy(zipStream, streamWriter, buffer);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Debug.Log(ex);
        }
        finally
        {
            if (zf != null)
            {
                zf.IsStreamOwner = true;
                zf.Close();
            }
        }
    }

    #endregion

    #region Servants

    List<Servant> servants = new List<Servant>();

    public Servant backGroundPic;
    public Menu menu;
    public SuperPreList superPreList;
    public Setting setting;
    public selectDeck selectDeck;
    public selectReplay selectReplay;
    public Room room;
    public CardDescription cardDescription;
    public DeckManager deckManager;
    public Ocgcore ocgcore;
    public SelectServer selectServer;
    public Book book;
    public puzzleMode puzzleMode;
    public AIRoom aiRoom;

    void initializeALLservants()
    {
        menu = new Menu();
        servants.Add(menu);
        superPreList = new SuperPreList();
        servants.Add(superPreList);
        setting = new Setting();
        servants.Add(setting);
        selectDeck = new selectDeck();
        servants.Add(selectDeck);
        room = new Room();
        servants.Add(room);
        cardDescription = new CardDescription();
        deckManager = new DeckManager();
        servants.Add(deckManager);
        ocgcore = new Ocgcore();
        servants.Add(ocgcore);
        selectServer = new SelectServer();
        servants.Add(selectServer);

        book = new Book();
        servants.Add(book);
        selectReplay = new selectReplay();
        servants.Add(selectReplay);
        puzzleMode = new puzzleMode();
        servants.Add(puzzleMode);
        aiRoom = new AIRoom();
        servants.Add(aiRoom);
    }

    public void shiftToServant(Servant to)
    {
        if (to != backGroundPic && backGroundPic.isShowed)
        {
            backGroundPic.hide();
        }
        if (to != menu && menu.isShowed)
        {
            menu.hide();
        }
        if (to != superPreList && superPreList.isShowed)
        {
            superPreList.hide();
        }
        if (to != setting && setting.isShowed)
        {
            setting.hide();
        }
        if (to != selectDeck && selectDeck.isShowed)
        {
            selectDeck.hide();
        }
        if (to != room && room.isShowed)
        {
            room.hide();
        }
        if (to != deckManager && deckManager.isShowed)
        {
            deckManager.hide();
        }
        if (to != ocgcore && ocgcore.isShowed)
        {
            ocgcore.hide();
        }
        if (to != selectServer && selectServer.isShowed)
        {
            selectServer.hide();
        }

        if (to != selectReplay && selectReplay.isShowed)
        {
            selectReplay.hide();
        }
        if (to != puzzleMode && puzzleMode.isShowed)
        {
            puzzleMode.hide();
        }
        if (to != aiRoom && aiRoom.isShowed)
        {
            aiRoom.hide();
        }

        if (to == backGroundPic && backGroundPic.isShowed == false) backGroundPic.show();
        if (to == menu && menu.isShowed == false) menu.show();
        if (to == superPreList && superPreList.isShowed == false) superPreList.show();
        if (to == setting && setting.isShowed == false) setting.show();
        if (to == selectDeck && selectDeck.isShowed == false) selectDeck.show();
        if (to == room && room.isShowed == false) room.show();
        if (to == deckManager && deckManager.isShowed == false) deckManager.show();
        if (to == ocgcore && ocgcore.isShowed == false) ocgcore.show();
        if (to == selectServer && selectServer.isShowed == false) selectServer.show();
        if (to == selectReplay && selectReplay.isShowed == false) selectReplay.show();
        if (to == puzzleMode && puzzleMode.isShowed == false) puzzleMode.show();
        if (to == aiRoom && aiRoom.isShowed == false) aiRoom.show();

    }

    #endregion

    #region MonoBehaviors

    void Start()
    {
        if (Screen.width < 100 || Screen.height < 100)
        {
            Screen.SetResolution(1300, 700, false);
        }
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = 144;
        mouseParticle = Instantiate(new_mouse);
        instance = this;
        initialize();
        go(500, () => { gameStart(); });
    }

    int preWid = 0;

    int preheight = 0;

    public static float _padScroll = 0;

    void OnGUI()
    {
        if (Event.current.type == EventType.ScrollWheel)
            _padScroll = -Event.current.delta.y / 100;
        else
            _padScroll = 0;
    }

    // ── 帧循环心跳（仅排查用）────────────────────────────────────────────────
    // 「对局明明开始了、界面上却什么都不发生」时，第一件必须分清的事是：帧循环还活着
    // （只是服务器/机器人没消息），还是主线程卡在某一行。收包是主线程在 Update 末尾
    // （TcpHelper.preFrameFunction）驱动的，所以主线程一卡，日志会连着 [stoc] 一起断掉，
    // 从日志上看不出是「没人说话」还是「听不见了」。这里每约 2 秒写一行，并带上阶段名
    // （走到哪个 servant），一跑就能把卡点框出来。只在 log/qt_debug.on 存在时有输出。
    private static float hbLast = -1000f;
    private static long hbCalls = 0;

    public static void Heartbeat(string stage)
    {
        hbCalls++;
        if (!QuickTestTrace.Enabled)
        {
            return;
        }
        float now = Time.realtimeSinceStartup;
        if (now - hbLast < 2f)
        {
            return;
        }
        hbLast = now;
        QuickTestTrace.Log("hb", stage
            + " calls=" + hbCalls
            + " rt=" + now.ToString("F1")
            + " time=" + Time.time.ToString("F1")
            + " ts=" + Time.timeScale.ToString("F2")
            + " frame=" + Time.frameCount
            + " focus=" + (Application.isFocused ? 1 : 0)
            + " scr=" + Screen.width + "x" + Screen.height);
    }

    /// <summary>
    /// 排查用（log/qt_debug.on）：把「光标在哪、NGUI 在那里命中了谁」落盘。
    ///
    /// 为什么需要它：验收脚本是用真光标（SetCursorPos + mouse_event）点按钮的，
    /// 一旦点不中，从服务端看就是「什么都没发生」，分不清是
    /// ①坐标算错、②被别的控件挡住、还是 ③按钮自己没注册回调。
    /// 这里复用的正是游戏每帧自己做悬停检测的那次 UICamera.Raycast 结果，
    /// 所以脚本把光标挪过去之后，轨迹里就能直接看到 Unity 认定的命中对象。
    /// 只在光标动了或按下左键时写，避免刷屏。
    /// </summary>
    static Vector3 probeMouseLast = new Vector3(-9999f, -9999f, 0f);
    static int probeMouseMs = -1;

    static void ProbeMouse(GameObject hoverobject)
    {
        if (!QuickTestTrace.Enabled)
        {
            return;
        }
        Vector3 m = Input.mousePosition;
        bool down = Input.GetMouseButtonDown(0);
        bool moved = Mathf.Abs(m.x - probeMouseLast.x) > 1f
            || Mathf.Abs(m.y - probeMouseLast.y) > 1f;
        if (!moved && !down)
        {
            return;
        }
        int now = TimePassed();
        if (!down && now - probeMouseMs < 60)
        {
            return;
        }
        probeMouseMs = now;
        probeMouseLast = m;
        QuickTestTrace.Log("mouse",
            "pos=(" + Mathf.RoundToInt(m.x) + "," + Mathf.RoundToInt(m.y) + ")"
            + " winPos=(" + Mathf.RoundToInt(m.x) + ","
            + Mathf.RoundToInt(Screen.height - m.y) + ")"
            + " down=" + (down ? 1 : 0)
            + " hover=" + ProbePathOf(hoverobject)
            + " scr=" + Screen.width + "x" + Screen.height);
    }

    /// <summary>把命中对象写成「父/子」路径，一眼看出是工具条上哪个按钮。</summary>
    static string ProbePathOf(GameObject go)
    {
        if (go == null)
        {
            return "none";
        }
        string p = go.name;
        Transform t = go.transform.parent;
        for (int i = 0; i < 3 && t != null; i++)
        {
            p = t.name + "/" + p;
            t = t.parent;
        }
        return p;
    }

    void Update()
    {
        Heartbeat("update-begin");
        // 排查用（log/qt_uidump.on）：把此刻屏幕上所有 UILabel 的文案倒进轨迹。
        // 挂在**这里**而不是 servant 的 preFrameFunction：那个钩子被
        // ServantWithCardDescription 这类不调 base 的 override 挡掉（卡组编辑器/对局整块漏导），
        // 而且够不着「高级搜索」那种独立根节点的窗口 —— 详见 QuickTestTrace.UiDumpAll 的头注。
        // 开关不在时零开销（内部带节流的 SwitchOn，2.5 次/秒上限）。
        QuickTestTrace.UiDumpAll();
        // 数据更新完成后的内存重载 + 事务提交。菜单是否可见由 ClientDataUpdater 自己判断
        // （不在主菜单就一直挂着），这里每帧兜底问一次 —— 不依赖菜单自身的生命周期。
        if (ClientDataUpdater.ReloadPending)
        {
            ClientDataUpdater.ApplyPendingReload();
        }
        // 自动化验收（log/qt_debug.on）时不许因为失焦就停循环。
        // Unity 播放器默认失焦即暂停：Time.time 不走、入站消息也不再处理，脚本又没法保证
        // 一直占着前台（SetForegroundWindow 会被系统拒绝），验收就会莫名其妙卡在半路
        // ——表现为「对局明明开着却什么也不发生」。只在这个调试开关存在时改，正常玩不受影响。
        // ⚠ 这一行在**每帧**跑，第二个操作数必须是带节流的 `SwitchOn`（内部「它不在」方向
        //   缓存 500ms）而不是裸 `File.Exists`：实测后者 17us/次，60fps 下就是单核 0.1% ——
        //   而正常玩这个文件永远不存在，白烧。见 QuickTestTrace.SwitchPollMs。
        if (!Application.runInBackground
            && QuickTestTrace.SwitchOn(QuickTestTrace.MasterSwitch))
        {
            Application.runInBackground = true;
            QuickTestTrace.Log("app", "qt_debug.on -> Application.runInBackground=true（失焦也继续跑）");
        }

        if (preWid != Screen.width || preheight != Screen.height)
        {
            Resources.UnloadUnusedAssets();
            onRESIZED();
        }
        fixALLcamerasPreFrame();
        wheelValue = UICamera.GetAxis("Mouse ScrollWheel") * 50;
        pointedGameObject = null;
        pointedCollider = null;
        Ray line = Camera.main.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;
        if (Physics.Raycast(line, out hit, (float)1000, rayFilter))
        {
            pointedGameObject = hit.collider.gameObject;
            pointedCollider = hit.collider;
        }
        GameObject hoverobject = UICamera.Raycast(Input.mousePosition) ? UICamera.lastHit.collider.gameObject : null;
        if (hoverobject != null)
        {
            if (hoverobject.layer == 11 || pointedGameObject == null)
            {
                pointedGameObject = hoverobject;
                pointedCollider = UICamera.lastHit.collider;
            }
        }
        ProbeMouse(hoverobject);
        InputGetMouseButtonDown_0 = Input.GetMouseButtonDown(0);
        InputGetMouseButtonUp_0 = Input.GetMouseButtonUp(0);
        InputGetMouseButtonDown_1 = Input.GetMouseButtonDown(1);
        InputGetMouseButtonUp_1 = Input.GetMouseButtonUp(1);
        InputEnterDown = Input.GetKeyDown(KeyCode.Return);
        InputGetMouseButton_0 = Input.GetMouseButton(0);
        for (int i = 0; i < servants.Count; i++)
        {
            Heartbeat("servant#" + i + " " + (servants[i] == null
                ? "null" : servants[i].GetType().Name) + "（进入）");
            servants[i].Update();
        }
        TcpHelper.preFrameFunction();
        // AI 对局的看门狗：子进程崩了 / 长时间没包时把玩家从卡死的对局里放出来。
        if (aiRoom != null)
        {
            aiRoom.WatchdogTick();
        }
        Heartbeat("update-end");
        delayedTask remove = null;
        while (true)
        {
            remove = null;
            for (int i = 0; i < delayedTasks.Count; i++)
            {
                if (Program.TimePassed() > delayedTasks[i].timeToBeDone)
                {
                    remove = delayedTasks[i];
                    try
                    {
                        remove.act();
                    }
                    catch (System.Exception e)
                    {
                        UnityEngine.Debug.Log(e);
                    }
                    break;
                }
            }
            if (remove != null)
            {
                delayedTasks.Remove(remove);
            }
            else
            {
                break;
            }
        }

    }

    private void onRESIZED()
    {
        preWid = Screen.width;
        preheight = Screen.height;
        //if (setting != null)
        //    setting.setScreenSizeValue();
        Program.notGo(fixScreenProblems);
        Program.go(500, fixScreenProblems);
    }

    public static void DEBUGLOG(object o)
    {
#if UNITY_EDITOR
        Debug.Log(o);
#endif
    }

    public static void PrintToChat(object o)
    {
        try
        {
            instance.cardDescription.mLog(o.ToString());
        }
        catch
        {
            DEBUGLOG(o);
        }
    }

    void gameStart()
    {
        if (UIHelper.shouldMaximize())
        {
            UIHelper.MaximizeWindow();
        }
        backGroundPic.show();
        shiftToServant(menu);
        // 启动落定后把每个 servant 的可见性结算一次（仅 log/qt_debug.on）。
        //
        // 排查用：窗口类 servant 的 initialize() 末尾都要自己 SetActiveFalse()，
        // 谁漏了就会一直亮在客户区中央（「开游戏先弹一下某个界面」多半是这个）。
        // 这种「创建出来就没藏过」不会有状态翻转，光看显示/隐藏轨迹是看不出来的，
        // 必须在启动后结算一次稳态，才能一眼看出谁亮着。
        Program.go(3000, DumpServantVisibility);
    }

    private void DumpServantVisibility()
    {
        if (!QuickTestTrace.Enabled)
        {
            return;
        }
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        for (int i = 0; i < servants.Count; i++)
        {
            Servant s = servants[i];
            if (s == null)
            {
                continue;
            }
            sb.Append(s.GetType().Name)
              .Append("=")
              .Append(s.gameObject != null && s.gameObject.activeSelf ? "亮" : "藏")
              .Append(" ");
        }
        QuickTestTrace.Log("vis", "steady " + sb.ToString());
    }

    public static bool Running = true;

    public static bool MonsterCloud = false;
    public static float fieldSize = 1;
    public static bool longField = false;

    public static bool noAccess = false;

    public static bool exitOnReturn = false;

    void OnApplicationQuit()
    {
        TcpHelper.SaveRecord();
        cardDescription.save();
        setting.saveWhenQuit();
        for (int i = 0; i < servants.Count; i++)
        {
            servants[i].OnQuit();
        }
        Running = false;
        try
        {
            TcpHelper.tcpClient.Close();
        }
        catch (System.Exception e)
        {
            //adeUnityEngine.Debug.Log(e);
        }
        Menu.deleteShell();
        foreach (ZipFile zip in GameZipManager.Zips)
        {
            zip.Dispose();
        }
        aiRoom.killServerProcess();
    }

    public void quit()
    {
        OnApplicationQuit();
    }

    #endregion

    public static void gugugu()
    {
        PrintToChat(InterString.Get("非常抱歉，因为技术原因，此功能暂时无法使用。请关注官方网站获取更多消息。"));
    }
}
