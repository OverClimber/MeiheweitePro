using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>
/// Maintains the upstream mapping from prerelease card IDs to their official IDs.
/// A validated local copy is kept so deck editing does not depend on the network.
/// </summary>
public static class PrereleaseCardMap
{
    public const string SourceUrl =
        "https://ygocdb-mirror.moecube.com/api/v0/idChangelogArray.jsonp";
    public const string CachePath = "updates/idChangelogArray.json";

    [Serializable]
    private class MappingPayload
    {
        public int[] oldIDsArray;
        public int[] newIDsArray;
    }

    private static Dictionary<int, int> mappings = new Dictionary<int, int>();
    private static bool initialized;
    private static bool cacheLoaded;

    public static event Action Changed;

    public static int Count
    {
        get { return mappings.Count; }
    }

    public static bool HasData
    {
        get { return mappings.Count > 0; }
    }

    public static bool IsRefreshing { get; private set; }

    public static string LastError { get; private set; }

    public static void Initialize(MonoBehaviour coroutineRunner)
    {
        if (initialized)
        {
            return;
        }
        initialized = true;

        string cacheError;
        cacheLoaded = TryLoadCache(out cacheError);
        if (!cacheLoaded && !string.IsNullOrEmpty(cacheError))
        {
            LastError = cacheError;
        }

        if (coroutineRunner != null)
        {
            coroutineRunner.StartCoroutine(RefreshOnline());
        }
    }

    public static bool TryLoadJson(string json, out string error)
    {
        Dictionary<int, int> parsed;
        if (!TryParseJson(json, out parsed, out error))
        {
            return false;
        }

        mappings = parsed;
        LastError = null;
        NotifyChanged();
        return true;
    }

    public static bool TryGetOfficialId(int prereleaseId, out int officialId)
    {
        return mappings.TryGetValue(prereleaseId, out officialId);
    }

    public static IEnumerator RefreshOnline()
    {
        if (IsRefreshing)
        {
            yield break;
        }

        IsRefreshing = true;
        LastError = null;
        NotifyChanged();

        bool downloadSucceeded = false;
        string downloadError = null;
        string validationError = null;

        yield return UnityFileDownloader.DownloadFileWithHeadCheck(
            SourceUrl,
            CachePath,
            (success) => { downloadSucceeded = success; },
            null,
            null,
            (path) =>
            {
                try
                {
                    Dictionary<int, int> ignored;
                    return TryParseJson(
                        File.ReadAllText(path),
                        out ignored,
                        out validationError
                    );
                }
                catch (Exception e)
                {
                    validationError = "读取转正记录失败：" + e.Message;
                    return false;
                }
            },
            (reason) => { downloadError = reason; },
            !cacheLoaded
        );

        if (downloadSucceeded)
        {
            string cacheError;
            cacheLoaded = TryLoadCache(out cacheError);
            if (!cacheLoaded)
            {
                LastError = cacheError;
            }
        }
        else
        {
            LastError = !string.IsNullOrEmpty(validationError)
                ? validationError
                : downloadError;
        }

        IsRefreshing = false;
        NotifyChanged();
    }

    private static bool TryLoadCache(out string error)
    {
        error = null;
        if (!File.Exists(CachePath))
        {
            return false;
        }

        try
        {
            return TryLoadJson(File.ReadAllText(CachePath), out error);
        }
        catch (Exception e)
        {
            error = "读取本地转正记录失败：" + e.Message;
            return false;
        }
    }

    private static bool TryParseJson(
        string json,
        out Dictionary<int, int> parsed,
        out string error
    )
    {
        parsed = null;
        error = null;

        if (string.IsNullOrEmpty(json))
        {
            error = "转正记录为空。";
            return false;
        }

        // The current endpoint returns JSON despite the .jsonp suffix. Keeping
        // this extraction also lets us tolerate a future callback wrapper.
        int objectStart = json.IndexOf('{');
        int objectEnd = json.LastIndexOf('}');
        if (objectStart < 0 || objectEnd <= objectStart)
        {
            error = "转正记录不是有效的 JSON 对象。";
            return false;
        }

        MappingPayload payload;
        try
        {
            payload = JsonUtility.FromJson<MappingPayload>(
                json.Substring(objectStart, objectEnd - objectStart + 1)
            );
        }
        catch (Exception e)
        {
            error = "解析转正记录失败：" + e.Message;
            return false;
        }

        if (payload == null
            || payload.oldIDsArray == null
            || payload.newIDsArray == null)
        {
            error = "转正记录缺少 ID 数组。";
            return false;
        }
        if (payload.oldIDsArray.Length != payload.newIDsArray.Length)
        {
            error = "转正记录的新旧 ID 数组长度不一致。";
            return false;
        }
        if (payload.oldIDsArray.Length == 0)
        {
            error = "转正记录中没有可用映射。";
            return false;
        }

        var candidate = new Dictionary<int, int>(payload.oldIDsArray.Length);
        for (int i = 0; i < payload.oldIDsArray.Length; i++)
        {
            int oldId = payload.oldIDsArray[i];
            int newId = payload.newIDsArray[i];
            if (oldId <= 0 || newId <= 0 || oldId == newId)
            {
                error = "转正记录包含无效 ID（索引 " + i + "）。";
                return false;
            }
            if (candidate.ContainsKey(oldId))
            {
                error = "转正记录包含重复的先行卡 ID：" + oldId;
                return false;
            }
            candidate.Add(oldId, newId);
        }

        parsed = candidate;
        return true;
    }

    private static void NotifyChanged()
    {
        Action handler = Changed;
        if (handler != null)
        {
            handler();
        }
    }
}
