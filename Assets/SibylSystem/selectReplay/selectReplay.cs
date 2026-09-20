using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;

public class selectReplay : WindowServantSP
{
    UIselectableList superScrollView = null;

    string sort = "sortByTimeReplay";

    /// <summary>
    /// 本模式回放目录（末尾带 /）。RD = rd/replay/，OCG = replay/。
    /// RD 局录像落在 RD 自己的目录里，回放窗口也只列本模式那份 —— 两边互不看见。
    /// 改动路径口径必须同步 GameModeManager.ReplayDir 的注释（SaveRecord / Ocgcore 收尾同走）。
    /// </summary>
    private static string Dir
    {
        get { return GameModeManager.ReplayDir + "/"; }
    }

    public override void initialize()
    {
        createWindow(Program.I().remaster_replayManager);
        UIHelper.registEvent(gameObject, "exit_", onClickExit);
        superScrollView = gameObject.GetComponentInChildren<UIselectableList>();
        superScrollView.selectedAction = onSelected;
        UIHelper.registEvent(gameObject, "sort_", onSort);
        UIHelper.registEvent(gameObject, "launch_", onLaunch);
        UIHelper.registEvent(gameObject, "rename_", onRename);
        UIHelper.registEvent(gameObject, "delete_", onDelete);
        UIHelper.registEvent(gameObject, "yrp_", onYrp);
        UIHelper.registEvent(gameObject, "ydk_", onYdk);
        UIHelper.registEvent(gameObject, "god_", onGod);
        UIHelper.registEvent(gameObject, "value_", onValue);
        setSortLable();
        superScrollView.install();
        SetActiveFalse();
    }

    void onValue()
    {
        RMSshow_yesOrNo(
                 "onValue",
                 InterString.Get("您确定要删除所有未命名的录像？"),
                 new messageSystemValue { hint = "yes", value = "yes" },
                 new messageSystemValue { hint = "no", value = "no" });

    }

    private void setSortLable()
    {
        if (Config.Get(sort,"1") == "1")
        {
            UIHelper.trySetLableText(gameObject, "sort_", InterString.Get("时间排序"));
        }
        else
        {
            UIHelper.trySetLableText(gameObject, "sort_", InterString.Get("名称排序"));
        }
    }

    private void onLaunch()
    {
        if (!superScrollView.Selected())
        {
            return;
        }
        if (!isShowed)
        {
            return;
        }
        KF_replay(superScrollView.selectedString);
    }

    PrecyOcg precy;

    private void onGod()    
    {
        if (!superScrollView.Selected())
        {
            return;
        }
        if (!isShowed)
        {
            return;
        }
        KF_replay(superScrollView.selectedString,true);
    }

    private void onSort()
    {
        if (Config.Get(sort,"1") == "1")
        {
            Config.Set(sort, "0");
        }
        else
        {
            Config.Set(sort, "1");
        }
        setSortLable();
        printFile();
    }

    bool opYRP = false;

    private void onRename()
    {
        if (!superScrollView.Selected())
        {
            return;
        }
        string name = superScrollView.selectedString;
        if (name.Length > 4 && name.Substring(name.Length - 4, 4) == ".yrp")
        {
            opYRP = true;
            RMSshow_input("onRename", InterString.Get("请输入重命名后的录像名"), name.Substring(0, name.Length - 4));
        }
        else
        {
            opYRP = false;
            RMSshow_input("onRename", InterString.Get("请输入重命名后的录像名"), name);
        }
    }

    private void onDelete()
    {
        if (!superScrollView.Selected())
        {
            return;
        }
        RMSshow_yesOrNo(
                 "onDelete",
                 InterString.Get("删除[?],@n请确认。",
                 superScrollView.selectedString),
                 new messageSystemValue { hint = "yes", value = "yes" },
                 new messageSystemValue { hint = "no", value = "no" });
    }

    List<byte[]> getYRPbuffer(string path)
    {
        if (path.Substring(path.Length - 4, 4) == ".yrp")
        {
            return new List<byte[]> { File.ReadAllBytes(path) };
        }
        var returnValue = new List<byte[]>();
        try
        {
            var collection = TcpHelper.readPackagesInRecord(path);
            foreach (var item in collection)
            {
                if (item.Fuction == (int)YGOSharp.OCGWrapper.Enums.GameMessage.sibyl_replay)
                {
                    byte[] replay = item.Data.reader.ReadToEnd();
                    // TODO: don't include other replays
                    returnValue.Add(replay);
                }
            }
        }
        catch (Exception e) 
        {
            Debug.Log(e);
        }
        return returnValue;
    }

    Percy.YRP getYRP(byte[] buffer)
    {
        Percy.YRP returnValue = new Percy.YRP();
        try
        {
            BinaryReader reader = new BinaryReader(new MemoryStream(buffer));
            returnValue.ID= reader.ReadInt32();
            returnValue.Version= reader.ReadInt32();
            returnValue.Flag= reader.ReadInt32();
            returnValue.Seed= reader.ReadUInt32();
            returnValue.DataSize = reader.ReadInt32();
            returnValue.Hash = reader.ReadInt32();
            returnValue.Props= reader.ReadBytes(8);
            if (returnValue.ID == 0x32707279) // REPLAY_ID_YRP2
            {
                for (int i = 0; i < 8; i++)
                {
                    returnValue.SeedsV2[i] = reader.ReadUInt32();
                }
                for (int i = 0; i < 4; i++) // other flags, unused for now
                {
                    reader.ReadUInt32();
                }
            }
            byte[] raw = reader.ReadToEnd();
            if ((returnValue.Flag & 0x1) > 0)
            {
                SevenZip.Compression.LZMA.Decoder lzma = new SevenZip.Compression.LZMA.Decoder();
                lzma.SetDecoderProperties(returnValue.Props);
                MemoryStream decompressed = new MemoryStream();
                lzma.Code(new MemoryStream(raw), decompressed, raw.LongLength, returnValue.DataSize, null);
                raw = decompressed.ToArray();
            }
            reader = new BinaryReader(new MemoryStream(raw));
            if ((returnValue.Flag & 0x2) > 0)
            {
                Program.I().room.mode = 2;
                returnValue.playerData.Add(new Percy.YRP.PlayerData());
                returnValue.playerData.Add(new Percy.YRP.PlayerData());
                returnValue.playerData.Add(new Percy.YRP.PlayerData());
                returnValue.playerData.Add(new Percy.YRP.PlayerData());
                returnValue.playerData[0].name = reader.ReadUnicode(20);
                returnValue.playerData[1].name = reader.ReadUnicode(20);
                returnValue.playerData[2].name = reader.ReadUnicode(20);
                returnValue.playerData[3].name = reader.ReadUnicode(20);
                returnValue.StartLp = reader.ReadInt32();
                returnValue.StartHand = reader.ReadInt32();
                returnValue.DrawCount = reader.ReadInt32();
                returnValue.opt = reader.ReadUInt32();
                Program.I().ocgcore.MasterRule = (int)(returnValue.opt >> 16);
                for (int i = 0; i < 4; i++)
                {
                    int count = reader.ReadInt32();
                    for (int i2 = 0; i2 < count; i2++)
                    {
                        returnValue.playerData[i].main.Add(reader.ReadInt32());
                    }
                    count = reader.ReadInt32();
                    for (int i2 = 0; i2 < count; i2++)
                    {
                        returnValue.playerData[i].extra.Add(reader.ReadInt32());
                    }
                }
            }
            else
            {
                returnValue.playerData.Add(new Percy.YRP.PlayerData());
                returnValue.playerData.Add(new Percy.YRP.PlayerData());
                returnValue.playerData[0].name = reader.ReadUnicode(20);
                returnValue.playerData[1].name = reader.ReadUnicode(20);
                returnValue.StartLp = reader.ReadInt32();
                returnValue.StartHand = reader.ReadInt32();
                returnValue.DrawCount = reader.ReadInt32();
                returnValue.opt = reader.ReadUInt32();
                Program.I().ocgcore.MasterRule = (int)(returnValue.opt >> 16);
                for (int i = 0; i < 2; i++)
                {
                    int count = reader.ReadInt32();
                    for (int i2 = 0; i2 < count; i2++)
                    {
                        returnValue.playerData[i].main.Add(reader.ReadInt32());
                    }
                    count = reader.ReadInt32();
                    for (int i2 = 0; i2 < count; i2++)
                    {
                        returnValue.playerData[i].extra.Add(reader.ReadInt32());
                    }
                }
            }
            while (reader.BaseStream.Position < reader.BaseStream.Length)
            {
                returnValue.gameData.Add(reader.ReadBytes(reader.ReadByte()));
            }
        }
        catch (Exception e)
        {
            Debug.Log(e);
        }
        return returnValue;
    }

    private void onYdk()    
    {
        if (!superScrollView.Selected())
        {
            return;
        }
        try
        {
            Percy.YRP yrp;
            if (File.Exists(Dir + superScrollView.selectedString))    
            {
                yrp = getYRP(File.ReadAllBytes(Dir + superScrollView.selectedString));
            }
            else
            {
                yrp = getYRP(getYRPbuffer(Dir + superScrollView.selectedString + ".yrp3d")[0]);
            }
            for (int i = 0; i < yrp.playerData.Count; i++)  
            {
                string value = "#created by ygopro2\r\n#main\r\n";
                for (int i2 = 0; i2 < yrp.playerData[i].main.Count; i2++)
                {
                    value += yrp.playerData[i].main[i2].ToString() + "\r\n";
                }
                value += "#extra\r\n";
                for (int i2 = 0; i2 < yrp.playerData[i].extra.Count; i2++)
                {
                    value += yrp.playerData[i].extra[i2].ToString() + "\r\n";
                }
                // 卡组入库跟着当前模式走：RD 局导出的卡组落 deck_rd/，别混进 OCG 的卡组列表。
                string name = GameModeManager.DeckPath(superScrollView.selectedString + "_" + (i + 1).ToString());
                File.WriteAllText(name, value);
                RMSshow_none(InterString.Get("卡组入库：[?]", name));
            }
            if (yrp.playerData.Count == 0)
            {
                RMSshow_none(InterString.Get("录像没有录制完整。"));
                RMSshow_none(InterString.Get("MATCH局中可能只有最后一局决斗才包含卡组信息。"));
            }
        }
        catch (Exception)
        {
            RMSshow_none(InterString.Get("录像没有录制完整。"));
            RMSshow_none(InterString.Get("MATCH局中可能只有最后一局决斗才包含卡组信息。"));
        }
    }

    private void onYrp()
    {
        if (!superScrollView.Selected())
        {
            return;
        }
        try
        {
            if (File.Exists(Dir + superScrollView.selectedString + ".yrp3d"))  
            {
                var replays = getYRPbuffer(Dir + superScrollView.selectedString + ".yrp3d");
                for(int i = 1; i <= replays.Count; i++) {
                    string filename = Dir + superScrollView.selectedString + "-Game" + i + ".yrp";
                    File.WriteAllBytes(filename, replays[i - 1]);
                    RMSshow_none(InterString.Get("录像入库：[?]", filename));
                }
                printFile();
            }
            else
            {
                RMSshow_none(InterString.Get("录像没有录制完整。"));
                RMSshow_none(InterString.Get("MATCH局中可能只有最后一局决斗才包含旧版录像信息。"));
            }
        }
        catch (Exception)
        {
            RMSshow_none(InterString.Get("录像没有录制完整。"));
            RMSshow_none(InterString.Get("MATCH局中可能只有最后一局决斗才包含旧版录像信息。"));
        }
    }

    public override void ES_RMS(string hashCode, List<messageSystemValue> result)
    {
        base.ES_RMS(hashCode, result);
        if (hashCode == "onRename")
        {
            try
            {
                if (opYRP)
                {
                    System.IO.File.Move(Dir + superScrollView.selectedString, Dir + result[0].value + ".yrp");

                }else
                {
                    System.IO.File.Move(Dir + superScrollView.selectedString + ".yrp3d", Dir + result[0].value + ".yrp3d");

                }
                printFile();
                RMSshow_none(InterString.Get("重命名成功。"));
            }
            catch (Exception)
            {
                RMSshow_none(InterString.Get("重命名失败！请检查输入的文件名，以及文件夹权限。"));
            }
        }
        if (hashCode == "onDelete")
        {
            if (result[0].value == "yes")
            {
                try
                {
                    if (File.Exists(Dir + superScrollView.selectedString + ".yrp3d"))
                    {
                        System.IO.File.Delete(Dir + superScrollView.selectedString + ".yrp3d");
                        RMSshow_none(InterString.Get("[?]已经被删除。", superScrollView.selectedString));
                        printFile();
                    }
                    if (File.Exists(Dir + superScrollView.selectedString))
                    {
                        System.IO.File.Delete(Dir + superScrollView.selectedString);
                        RMSshow_none(InterString.Get("[?]已经被删除。", superScrollView.selectedString));
                        printFile();
                    }
                }
                catch (Exception)
                {
                }
            }
        }
        if (hashCode == "onValue")
        {
            if (result[0].value == "yes")
            {
                FileInfo[] fileInfos = (new DirectoryInfo("replay")).GetFiles();
                for (int i = 0; i < fileInfos.Length; i++)
                {
                    if (fileInfos[i].Name.Length == 21 || fileInfos[i].Name.Length == 25)
                    {
                        if (fileInfos[i].Name[2] == '-')
                        {
                            if (fileInfos[i].Name[5] == '「')
                            {
                                if (fileInfos[i].Name[8] == '：')
                                {
                                    try
                                    {
                                        File.Delete(Dir + fileInfos[i].Name);
                                    }
                                    catch (Exception)
                                    {
                                    }
                                }
                            }
                        }
                    }
                }
                RMSshow_none(InterString.Get("清理完毕。"));
                printFile();
            }
        }
    }

    string selectedTrace = "";    
    void onSelected()
    {
        if (selectedTrace == superScrollView.selectedString)    
        {
            KF_replay(selectedTrace);
        }
        selectedTrace = superScrollView.selectedString;
    }

    public override void preFrameFunction()
    {
        base.preFrameFunction();
        Menu.checkCommend();
    }

    /// <summary>
    /// 正在播的这段录像里内嵌的**原始 YRP**（`sibyl_replay` 包），给「卡组记牌」读本局卡组用。
    ///
    /// ⚠ 两点必须说清，否则会显示错的卡组：
    /// ① 只有**服务器发过 STOC_REPLAY**（联机对局）的录像里才有内嵌 YRP；AI 对局是本地起的，
    ///    没有这一步，`.yrp3d` 里就没有内嵌 YRP。这时 `lastReplayYrp` 为 null，
    ///    记牌按钮干脆不出现（宁可不显示，也别显示错的）。
    /// ② MATCH 录像可能有**多份**内嵌 YRP（每局一份），而包流里没有可靠的对应关系
    ///    → 这种情况按「说不清是谁的卡组」处理，同样不显示。所以这里只在**恰好一份**时才认。
    /// </summary>
    public static Percy.YRP lastReplayYrp = null;

    /// <summary>内嵌 YRP 的份数（0 = 没有、1 = 可用、>1 = MATCH 说不清）。仅供排查/日志。</summary>
    public static int lastReplayYrpCount = 0;

    void noteReplayYrp(string path)
    {
        lastReplayYrp = null;
        lastReplayYrpCount = 0;
        try
        {
            List<byte[]> buffers = getYRPbuffer(path);
            if (buffers == null)
            {
                return;
            }
            lastReplayYrpCount = buffers.Count;
            if (buffers.Count == 1)
            {
                lastReplayYrp = getYRP(buffers[0]);
            }
        }
        catch (Exception e)
        {
            Debug.Log(e);
            lastReplayYrp = null;
        }
    }

    public void KF_replay(string name, bool god = false)
    {
        try
        {
            lastReplayYrp = null;
            lastReplayYrpCount = 0;
            if (File.Exists(Dir + name + ".yrp3d"))
            {
                // 「卡组记牌」要读本局卡组，得从内嵌 YRP 里取（见 lastReplayYrp 的注释）。
                // 这一步跟播放本身无关，解析失败也只是记牌不出现，不影响录像播放。
                noteReplayYrp(Dir + name + ".yrp3d");
                if (god)
                {
                    RMSshow_none(InterString.Get("您正在观看旧版的录像（上帝视角），不保证稳定性。"));
                    if (precy != null)
                        precy.dispose();
                    precy = new PrecyOcg();
                    var replays = getYRPbuffer(Dir + name + ".yrp3d");
                    var collections = TcpHelper.getPackages(precy.ygopro.getYRP3dBuffer(getYRP(replays[replays.Count - 1])));
                    pushCollection(collections);
                }
                else
                {
                    var collection = TcpHelper.readPackagesInRecord(Dir + name + ".yrp3d");
                    pushCollection(collection);
                }
            }
            else
            {
                if (name.Length>4&&name.Substring(name.Length - 4, 4) == ".yrp")
                {
                    if (File.Exists(Dir + name))
                    {
                        noteReplayYrp(Dir + name);
                        RMSshow_none(InterString.Get("您正在观看旧版的录像（上帝视角），不保证稳定性。"));
                        if (precy != null)
                            precy.dispose();
                        precy = new PrecyOcg();
                        var collections = TcpHelper.getPackages(precy.ygopro.getYRP3dBuffer(getYRP(File.ReadAllBytes(Dir + name))));
                        pushCollection(collections);
                    }
                }
            }
        }
        catch (Exception)   
        {
            RMSshow_none(InterString.Get("录像没有录制完整。"));
            RMSshow_none(InterString.Get("MATCH局中可能只有最后一局决斗才包含旧版录像信息。"));
        }
    }

    private void pushCollection(List<Package> collection)
    {
        Program.I().ocgcore.returnServant = Program.I().selectReplay;
        Program.I().ocgcore.handler = (a) => { };
        Program.I().ocgcore.name_0 = Config.Get("name", "一秒一喵机会");
        Program.I().ocgcore.name_0_c = Program.I().ocgcore.name_0;
        Program.I().ocgcore.name_1 = "Percy AI";
        Program.I().ocgcore.name_0_tag = "---";
        Program.I().ocgcore.name_1_tag = "---";
        Program.I().ocgcore.timeLimit = 240;
        Program.I().ocgcore.lpLimit = 8000;
        Program.I().ocgcore.isFirst = true;
        Program.I().shiftToServant(Program.I().ocgcore);
        Program.I().ocgcore.InAI = false;
        Program.I().ocgcore.shiftCondition(Ocgcore.Condition.record);
        Program.I().ocgcore.flushPackages(collection);
    }

    public override void show()
    {
        base.show();
        printFile();
        Program.charge();
    }

    void printFile()
    {
        superScrollView.clear();
        // 惰性建目录：rd/replay/ 不在构建铺设名单里，RD 局录下第一局之前它可能还不存在
        // （DirectoryInfo.GetFiles 对不存在的目录会抛异常，整个列表就空了）。
        Directory.CreateDirectory(GameModeManager.ReplayDir);
        FileInfo[] fileInfos = (new DirectoryInfo(GameModeManager.ReplayDir)).GetFiles();
        if (Config.Get(sort, "1") == "1")
        {
            Array.Sort(fileInfos, UIHelper.CompareTime);
        }
        else
        {
            Array.Sort(fileInfos, UIHelper.CompareName);
        }
        int listed = 0;
        for (int i = 0; i < fileInfos.Length; i++)
        {
            if (fileInfos[i].Name.Length > 6)
            {
                if (fileInfos[i].Name.Length > 6 && fileInfos[i].Name.Substring(fileInfos[i].Name.Length - 6, 6) == ".yrp3d")
                {
                    superScrollView.add(fileInfos[i].Name.Substring(0, fileInfos[i].Name.Length - 6));
                    listed++;
                }
                if (fileInfos[i].Name.Length > 4 && fileInfos[i].Name.Substring(fileInfos[i].Name.Length - 4, 4) == ".yrp")
                {
                    superScrollView.add(fileInfos[i].Name);
                    listed++;
                }
            }
        }
        // 验收判据：本窗口列的是哪个模式的哪个目录、列了多少条。
        QuickTestTrace.Log("replay", "list mode=" + GameModeManager.ModeLabel
            + " dir=" + GameModeManager.ReplayDir + " count=" + listed);
    }

    void onClickExit()
    {
        if (Program.exitOnReturn)
            Program.I().menu.onClickExit();
        else
            Program.I().shiftToServant(Program.I().menu);
    }

}
