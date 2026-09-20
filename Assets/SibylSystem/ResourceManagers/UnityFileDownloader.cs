using System;
using System.Collections;
using System.Globalization;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// 使用Unity官方推荐的UnityWebRequest来异步下载文件。
/// </summary>
public class UnityFileDownloader
{
    private const int UpdateRequestMaxAttempts = 3;
    private const int HeadRequestTimeoutSeconds = 15;
    private const int RangeRequestTimeoutSeconds = 300;
    private const int DownloadStallTimeoutSeconds = 30;
    private const long CdntxRangeThresholdBytes = 5L * 1024L * 1024L;
    private const long RangeChunkSizeBytes = 5L * 1024L * 1024L;

    public static IEnumerator DownloadFileAsync(
        string url,
        string filePath,
        Action<bool> onComplete,
        Action<float> onProgress = null,
        Func<string, bool> validateDownloadedFile = null,
        Action<string> onError = null
    )
    {
        // 确保目录存在
        try
        {
            string directoryPath = Path.GetDirectoryName(filePath);
            if (!Directory.Exists(directoryPath))
            {
                Directory.CreateDirectory(directoryPath);
            }
        }
        catch (Exception e)
        {
            CompleteFailure(onComplete, onError, "无法创建下载目录：" + e.Message);
            yield break; // 提前退出协程
        }

        string tempFilePath = filePath + ".tmp";
        try
        {
            if (File.Exists(tempFilePath))
            {
                File.Delete(tempFilePath);
            }
        }
        catch (Exception e)
        {
            CompleteFailure(onComplete, onError, "无法清理临时文件：" + e.Message);
            yield break;
        }

        // Debug.Log(string.Format("Downloading: {0} -> {1}", url, filePath));

        using (UnityWebRequest uwr = new UnityWebRequest(url, UnityWebRequest.kHttpVerbGET))
        {
            uwr.downloadHandler = new DownloadHandlerFile(tempFilePath);
            uwr.timeout = GetTimeoutForFile(filePath);

            bool stalled = false;
            yield return WaitForDownloadAsync(
                uwr,
                onProgress,
                () => { stalled = true; });

            if (!stalled && uwr.result == UnityWebRequest.Result.Success)
            {
                string installFailure;
                if (!TryInstallDownloadedFile(
                    tempFilePath,
                    filePath,
                    validateDownloadedFile,
                    out installFailure))
                {
                    CompleteFailure(onComplete, onError, installFailure);
                    yield break;
                }
                if (onComplete != null) onComplete.Invoke(true);
            }
            else
            {
                string requestFailure = stalled
                    ? "下载停滞：连续 30 秒未收到新数据"
                    : DescribeRequestFailure("下载失败", uwr);
                if (File.Exists(tempFilePath))
                {
                    try
                    {
                        File.Delete(tempFilePath);
                    }
                    catch (Exception) { }
                }
                CompleteFailure(onComplete, onError, requestFailure);
            }
        }
    }

    private static IEnumerator WaitForDownloadAsync(
        UnityWebRequest request,
        Action<float> onProgress,
        Action onStalled)
    {
        ulong lastDownloadedBytes = 0;
        // 使用实际经过的时间，避免游戏暂停或调整 timeScale 影响停滞检测。
        double lastProgressTime = Time.realtimeSinceStartupAsDouble;
        UnityWebRequestAsyncOperation operation = request.SendWebRequest();
        while (!operation.isDone)
        {
            ulong downloadedBytes = request.downloadedBytes;
            double now = Time.realtimeSinceStartupAsDouble;
            if (downloadedBytes > lastDownloadedBytes)
            {
                lastDownloadedBytes = downloadedBytes;
                lastProgressTime = now;
            }
            else if (now - lastProgressTime >= DownloadStallTimeoutSeconds)
            {
                request.Abort();
                onStalled.Invoke();
                yield break;
            }

            if (onProgress != null)
            {
                onProgress.Invoke(operation.progress);
            }
            yield return null;
        }
    }

    /// <summary>
    /// 仅检查资源是否有更新，不执行下载。
    /// 通过比较本地 ETag 和服务器 ETag 来判断。
    /// </summary>
    /// <param name="url">资源 URL</param>
    /// <param name="localFilePath">本地文件路径（用于定位 .etag 文件）</param>
    /// <param name="onComplete">回调：true = 有更新可用，false = 已是最新或检查失败</param>
    public static IEnumerator CheckForUpdateAsync(
        string url,
        string localFilePath,
        Action<bool> onComplete)
    {
        string etagFilePath = localFilePath + ".etag";
        string localEtag = null;
        bool localFileExists = File.Exists(localFilePath);

        // 1. 读取本地ETag（如果存在）
        if (File.Exists(etagFilePath))
        {
            try
            {
                localEtag = File.ReadAllText(etagFilePath);
            }
            catch (Exception)
            {
                localEtag = null; // 读取失败则当做不存在
            }
        }

        // 2. 探测远端元数据：HEAD 不可用时自动回退 GET，
        // 否则 CDN 拒绝 HEAD 会被误判成「无更新」（菜单 NEW 角标不亮）。
        RemoteFileMetadata metadata = null;
        yield return ProbeRemoteMetadata(
            url,
            localFilePath,
            false,
            (result) => { metadata = result; });

        string serverEtag = metadata == null ? null : metadata.Etag;
        if (metadata == null || !metadata.Succeeded || string.IsNullOrEmpty(serverEtag))
        {
            // 网络错误或服务器未提供 ETag，无法判断
            if (onComplete != null) onComplete.Invoke(false);
            yield break;
        }

        // 3. 比较ETag，判断是否有更新
        bool hasUpdate = !localFileExists
            || string.IsNullOrEmpty(localEtag)
            || !NormalizeEtag(localEtag).Equals(
                serverEtag,
                StringComparison.Ordinal);
        if (onComplete != null) onComplete.Invoke(hasUpdate);
    }

    /// <summary>
    /// 检查文件版本，如果不是最新则下载。
    /// 最终结果统一为成功(true)或失败(false)。
    /// </summary>
    /// <param name="url">文件下载地址</param>
    /// <param name="filePath">文件本地存储路径</param>
    /// <param name="onComplete">完成时的回调。true表示成功（已是最新或已下载），false表示失败。</param>
    /// <param name="onProgress">下载过程中的进度回调（仅在需要下载时触发）</param>
    /// <param name="onDownloadStarted">确认有更新且开始下载时触发</param>
    /// <param name="validateDownloadedFile">可选的临时文件校验。校验失败时保留现有文件。</param>
    /// <param name="onError">最终失败时返回可展示的具体原因。</param>
    /// <param name="forceDownload">为 true 时不读取或比较本地 ETag，始终重新下载。</param>
    public static IEnumerator DownloadFileWithHeadCheck(
        string url,
        string filePath,
        Action<bool> onComplete,
        Action<float> onProgress = null,
        Action onDownloadStarted = null,
        Func<string, bool> validateDownloadedFile = null,
        Action<string> onError = null,
        bool forceDownload = false)
    {
#if UNITY_EDITOR
        Debug.Assert(
            ShouldRetryRequest(UnityWebRequest.Result.ConnectionError, 0)
            && ShouldRetryRequest(UnityWebRequest.Result.ProtocolError, 503)
            && !ShouldRetryRequest(UnityWebRequest.Result.ProtocolError, 404),
            "Resource update retry classification failed."
        );
        Debug.Assert(
            CanCompareContentLengthToDownloadedFile(null)
            && CanCompareContentLengthToDownloadedFile("identity")
            && !CanCompareContentLengthToDownloadedFile("gzip")
            && !CanCompareContentLengthToDownloadedFile("br"),
            "Resource update content encoding classification failed."
        );
#endif
        string etagFilePath = filePath + ".etag";
        string localEtag = null;
        bool localFileExists = File.Exists(filePath);

        // 1. 普通更新读取本地 ETag；强制下载不检查本地 ETag。
        if (!forceDownload && File.Exists(etagFilePath))
        {
            try
            {
                localEtag = File.ReadAllText(etagFilePath);
            }
            catch (Exception e)
            {
                Debug.LogWarning(
                    "[资源更新] 读取本地 ETag 失败，将强制更新 "
                    + Path.GetFileName(filePath)
                    + "："
                    + e.Message);
                localEtag = null; // 读取失败则当做不存在
            }
        }

        // 2. 探测远端元数据；移动网络瞬时失败时自动重试，服务器拒绝 HEAD 时回退 GET。
        // 强制下载只依赖文件大小，普通更新还要求服务器返回 ETag。
        RemoteFileMetadata metadata = null;
        yield return ProbeRemoteMetadata(
            url,
            filePath,
            forceDownload,
            (result) => { metadata = result; });

        if (metadata == null || !metadata.Succeeded)
        {
            FinishWithFailure(
                filePath,
                onComplete,
                onError,
                metadata != null && !string.IsNullOrEmpty(metadata.Failure)
                    ? metadata.Failure
                    : "版本检查失败：未知错误");
            yield break;
        }

        string serverEtag = metadata.Etag;
        long serverContentLength = metadata.ContentLength;
        bool canCompareContentLengthToDownloadedFile =
            metadata.CanCompareContentLength;
        string resolvedDownloadUrl = string.IsNullOrEmpty(metadata.ResolvedUrl)
            ? url
            : metadata.ResolvedUrl;

        // Debug.Log(string.Format("版本比较: Local ETag='{0}', Server ETag='{1}'", localEtag, serverEtag));

        // 3. ETag 命中时仍需验证本地文件，避免损坏文件长期被当作最新版
        bool etagMatches = !forceDownload
            && localFileExists
            && !string.IsNullOrEmpty(localEtag)
            && NormalizeEtag(localEtag).Equals(
                serverEtag,
                StringComparison.Ordinal);
        string localValidationFailure = null;
        bool localFileIsValid = false;
        if (etagMatches)
        {
            // Content-Length 描述的是 HTTP 传输字节。使用 gzip/br 时，
            // Unity 会解压后再落盘，两者长度不可直接比较。
            localFileIsValid = TryValidateFileLength(
                filePath,
                canCompareContentLengthToDownloadedFile
                    ? serverContentLength
                    : -1L,
                out localValidationFailure);
            if (localFileIsValid)
            {
                localFileIsValid = TryValidateFile(
                    filePath,
                    validateDownloadedFile,
                    out localValidationFailure);
            }
        }
        if (localFileIsValid)
        {
            // Debug.Log(string.Format("[OK] 文件已是最新版本: {0}", Path.GetFileName(filePath)));
            if (onComplete != null) onComplete.Invoke(true); // 已是最新，也算成功
            yield break;
        }
        if (etagMatches)
        {
            Debug.LogWarning(
                "[资源更新] "
                + Path.GetFileName(filePath)
                + " 本地文件校验失败，将忽略 ETag 并重新下载："
                + localValidationFailure);
            try
            {
                File.Delete(etagFilePath);
            }
            catch (Exception) { }
        }

        // 4. ETag不匹配或本地文件不存在，执行下载
        // Debug.Log(string.Format("发现新版本或本地文件不存在，开始下载: {0}", Path.GetFileName(filePath)));

        if (onDownloadStarted != null)
        {
            try
            {
                onDownloadStarted.Invoke();
            }
            catch (Exception e)
            {
                FinishWithFailure(
                    filePath,
                    onComplete,
                    onError,
                    "准备下载失败：" + e.Message);
                yield break;
            }
        }

        // 编码后的 Content-Length 也不能作为解压后文件的分段边界。
        bool useRangeDownload = canCompareContentLengthToDownloadedFile
            && IsCdntxUrl(url)
            && serverContentLength > CdntxRangeThresholdBytes;

        // 4. 大于 5 MiB 的 CDNTX 文件按 5 MiB 分段；其他文件保持整文件下载。
        bool downloadSucceeded = false;
        string downloadFailure = null;
        int downloadAttempts = 0;
        if (useRangeDownload)
        {
            string attemptFailure = null;
            yield return DownloadFileInRangesAsync(
                resolvedDownloadUrl,
                filePath,
                serverContentLength,
                forceDownload ? null : serverEtag,
                (success) => { downloadSucceeded = success; },
                onProgress,
                validateDownloadedFile,
                (reason) => { attemptFailure = reason; }
            );
            downloadAttempts = 1;
            downloadFailure = string.IsNullOrEmpty(attemptFailure)
                ? "下载失败：未知错误"
                : attemptFailure;
        }
        else
        {
            for (int attempt = 1; attempt <= UpdateRequestMaxAttempts; attempt++)
            {
                downloadAttempts = attempt;
                string attemptFailure = null;
                yield return DownloadFileAsync(
                    url,
                    filePath,
                    (success) => { downloadSucceeded = success; },
                    onProgress,
                    validateDownloadedFile,
                    (reason) => { attemptFailure = reason; }
                );
                if (downloadSucceeded)
                {
                    break;
                }

                downloadFailure = string.IsNullOrEmpty(attemptFailure)
                    ? "下载失败：未知错误"
                    : attemptFailure;
                if (attempt < UpdateRequestMaxAttempts)
                {
                    LogRetry(filePath, "下载", attempt, downloadFailure);
                    yield return new WaitForSecondsRealtime(
                        GetRetryDelaySeconds(attempt));
                }
            }
        }

        if (!downloadSucceeded)
        {
            FinishWithFailure(
                filePath,
                onComplete,
                onError,
                useRangeDownload
                    ? downloadFailure
                    : WithAttemptCount(downloadFailure, downloadAttempts));
            yield break;
        }

        // 5. 文件已经校验并替换成功。服务器提供 ETag 时保存，供后续普通更新检查。
        bool etagSaved = false;
        string etagFailure = null;
        for (int attempt = 1;
            !string.IsNullOrEmpty(serverEtag)
                && attempt <= UpdateRequestMaxAttempts;
            attempt++)
        {
            try
            {
                File.WriteAllText(etagFilePath, serverEtag);
                etagSaved = true;
                break;
            }
            catch (Exception e)
            {
                etagFailure = "保存 ETag 失败：" + e.Message;
            }
            if (attempt < UpdateRequestMaxAttempts)
            {
                yield return new WaitForSecondsRealtime(GetRetryDelaySeconds(attempt));
            }
        }
        if (!string.IsNullOrEmpty(serverEtag) && !etagSaved)
        {
            Debug.LogWarning(
                "[资源更新] "
                + Path.GetFileName(filePath)
                + " 已成功安装，但 "
                + WithAttemptCount(etagFailure, UpdateRequestMaxAttempts)
                + "；下次启动会重新检查。"
            );
        }
        if (onComplete != null) onComplete.Invoke(true);
    }

    private static IEnumerator DownloadFileInRangesAsync(
        string url,
        string filePath,
        long contentLength,
        string expectedEtag,
        Action<bool> onComplete,
        Action<float> onProgress,
        Func<string, bool> validateDownloadedFile,
        Action<string> onError)
    {
        if (contentLength <= 0L)
        {
            CompleteFailure(
                onComplete,
                onError,
                "分段下载失败：服务器未返回有效的文件大小");
            yield break;
        }

        string tempFilePath = filePath + ".tmp";
        string partFilePath = tempFilePath + ".part";
        string preparationFailure;
        if (!TryPrepareDownloadFiles(
            filePath,
            tempFilePath,
            partFilePath,
            out preparationFailure))
        {
            CompleteFailure(onComplete, onError, preparationFailure);
            yield break;
        }

        long completedBytes = 0L;
        while (completedBytes < contentLength)
        {
            long rangeStart = completedBytes;
            long rangeEnd = Math.Min(
                rangeStart + RangeChunkSizeBytes - 1L,
                contentLength - 1L);
            long expectedPartLength = rangeEnd - rangeStart + 1L;
            bool partSucceeded = false;
            string partFailure = null;
            int partAttempts = 0;

            for (int attempt = 1; attempt <= UpdateRequestMaxAttempts; attempt++)
            {
                partAttempts = attempt;
                string cleanupFailure;
                if (!TryDeleteFile(partFilePath, out cleanupFailure))
                {
                    partFailure = "无法清理分段临时文件：" + cleanupFailure;
                    break;
                }

                bool responseAccepted = false;
                using (UnityWebRequest rangeRequest = new UnityWebRequest(
                    url,
                    UnityWebRequest.kHttpVerbGET))
                {
                    rangeRequest.downloadHandler = new DownloadHandlerFile(partFilePath);
                    rangeRequest.timeout = Math.Max(
                        GetTimeoutForFile(filePath),
                        RangeRequestTimeoutSeconds);
                    rangeRequest.SetRequestHeader(
                        "Range",
                        string.Format(
                            CultureInfo.InvariantCulture,
                            "bytes={0}-{1}",
                            rangeStart,
                            rangeEnd));
                    if (!string.IsNullOrEmpty(expectedEtag))
                    {
                        rangeRequest.SetRequestHeader("If-Range", expectedEtag);
                    }

                    bool stalled = false;
                    yield return WaitForDownloadAsync(
                        rangeRequest,
                        (progress) =>
                        {
                            if (onProgress != null)
                            {
                                double downloadedPartBytes = Math.Min(
                                    (double)rangeRequest.downloadedBytes,
                                    expectedPartLength);
                                onProgress.Invoke(
                                    (float)((completedBytes + downloadedPartBytes)
                                        / contentLength));
                            }
                        },
                        () => { stalled = true; });

                    if (stalled)
                    {
                        partFailure = string.Format(
                            CultureInfo.InvariantCulture,
                            "分段 {0}-{1} 下载停滞：连续 30 秒未收到新数据",
                            rangeStart,
                            rangeEnd);
                    }
                    else if (rangeRequest.result != UnityWebRequest.Result.Success)
                    {
                        partFailure = DescribeRequestFailure(
                            string.Format(
                                CultureInfo.InvariantCulture,
                                "分段 {0}-{1} 下载失败",
                                rangeStart,
                                rangeEnd),
                            rangeRequest);
                    }
                    else if (rangeRequest.responseCode != 206L)
                    {
                        partFailure = string.Format(
                            CultureInfo.InvariantCulture,
                            "分段下载失败：服务器未按 Range 返回数据（HTTP {0}）",
                            rangeRequest.responseCode);
                    }
                    else if (!HasExpectedContentRange(
                        rangeRequest.GetResponseHeader("Content-Range"),
                        rangeStart,
                        rangeEnd,
                        contentLength))
                    {
                        partFailure = "分段下载失败：服务器返回了不匹配的 Content-Range";
                    }
                    else
                    {
                        string responseEtag = NormalizeEtag(
                            rangeRequest.GetResponseHeader("ETag"));
                        if (!string.IsNullOrEmpty(expectedEtag)
                            && !string.IsNullOrEmpty(responseEtag)
                            && !responseEtag.Equals(
                                expectedEtag,
                                StringComparison.Ordinal))
                        {
                            partFailure = "分段下载失败：下载期间服务器 ETag 已变化";
                        }
                        else
                        {
                            responseAccepted = true;
                        }
                    }
                }

                if (responseAccepted)
                {
                    try
                    {
                        long actualPartLength = new FileInfo(partFilePath).Length;
                        if (actualPartLength == expectedPartLength)
                        {
                            partSucceeded = true;
                        }
                        else
                        {
                            partFailure = string.Format(
                                CultureInfo.InvariantCulture,
                                "分段下载失败：期望 {0} 字节，实际得到 {1} 字节",
                                expectedPartLength,
                                actualPartLength);
                        }
                    }
                    catch (Exception e)
                    {
                        partFailure = "检查分段文件失败：" + e.Message;
                    }
                }

                if (partSucceeded)
                {
                    break;
                }

                TryDeleteFileQuietly(partFilePath);
                if (attempt < UpdateRequestMaxAttempts)
                {
                    LogRetry(
                        filePath,
                        string.Format(
                            CultureInfo.InvariantCulture,
                            "分段 {0}-{1} 下载",
                            rangeStart,
                            rangeEnd),
                        attempt,
                        partFailure);
                    yield return new WaitForSecondsRealtime(
                        GetRetryDelaySeconds(attempt));
                }
            }

            if (!partSucceeded)
            {
                TryDeleteFileQuietly(partFilePath);
                TryDeleteFileQuietly(tempFilePath);
                CompleteFailure(
                    onComplete,
                    onError,
                    WithAttemptCount(partFailure, partAttempts));
                yield break;
            }

            try
            {
                using (FileStream destination = new FileStream(
                    tempFilePath,
                    FileMode.Append,
                    FileAccess.Write,
                    FileShare.None))
                using (FileStream source = new FileStream(
                    partFilePath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.Read))
                {
                    source.CopyTo(destination);
                }
            }
            catch (Exception e)
            {
                TryDeleteFileQuietly(partFilePath);
                TryDeleteFileQuietly(tempFilePath);
                CompleteFailure(
                    onComplete,
                    onError,
                    "拼接分段文件失败：" + e.Message);
                yield break;
            }

            TryDeleteFileQuietly(partFilePath);
            completedBytes += expectedPartLength;
            if (onProgress != null)
            {
                onProgress.Invoke((float)((double)completedBytes / contentLength));
            }
        }

        try
        {
            long combinedLength = new FileInfo(tempFilePath).Length;
            if (combinedLength != contentLength)
            {
                TryDeleteFileQuietly(tempFilePath);
                CompleteFailure(
                    onComplete,
                    onError,
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "分段拼接校验失败：期望 {0} 字节，实际得到 {1} 字节",
                        contentLength,
                        combinedLength));
                yield break;
            }
        }
        catch (Exception e)
        {
            TryDeleteFileQuietly(tempFilePath);
            CompleteFailure(onComplete, onError, "检查拼接文件失败：" + e.Message);
            yield break;
        }

        string installFailure;
        if (!TryInstallDownloadedFile(
            tempFilePath,
            filePath,
            validateDownloadedFile,
            out installFailure))
        {
            CompleteFailure(onComplete, onError, installFailure);
            yield break;
        }

        if (onComplete != null) onComplete.Invoke(true);
    }

    private static bool TryPrepareDownloadFiles(
        string filePath,
        string tempFilePath,
        string partFilePath,
        out string failure)
    {
        failure = null;
        try
        {
            string directoryPath = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directoryPath)
                && !Directory.Exists(directoryPath))
            {
                Directory.CreateDirectory(directoryPath);
            }

            if (File.Exists(tempFilePath))
            {
                File.Delete(tempFilePath);
            }
            if (File.Exists(partFilePath))
            {
                File.Delete(partFilePath);
            }
            return true;
        }
        catch (Exception e)
        {
            failure = "无法准备分段下载临时文件：" + e.Message;
            return false;
        }
    }

    private static bool TryInstallDownloadedFile(
        string tempFilePath,
        string filePath,
        Func<string, bool> validateDownloadedFile,
        out string failure)
    {
        failure = null;
        string validationFailure;
        if (!TryValidateFile(
            tempFilePath,
            validateDownloadedFile,
            out validationFailure))
        {
            TryDeleteFileQuietly(tempFilePath);
            failure = validationFailure;
            return false;
        }

        string backupFilePath = filePath + ".bak";
        bool backupCreated = false;
        try
        {
            if (File.Exists(backupFilePath))
            {
                File.Delete(backupFilePath);
            }
            if (File.Exists(filePath))
            {
                File.Copy(filePath, backupFilePath, true);
                backupCreated = true;
                File.Delete(filePath);
            }
            File.Move(tempFilePath, filePath);
            if (File.Exists(backupFilePath))
            {
                File.Delete(backupFilePath);
            }
            return true;
        }
        catch (Exception e)
        {
            failure = "替换本地文件失败：" + e.Message;
            try
            {
                if (backupCreated && File.Exists(backupFilePath))
                {
                    File.Copy(backupFilePath, filePath, true);
                    File.Delete(backupFilePath);
                }
                if (File.Exists(tempFilePath))
                {
                    File.Delete(tempFilePath);
                }
            }
            catch (Exception) { }
            return false;
        }
    }

    private static bool TryValidateFileLength(
        string filePath,
        long expectedLength,
        out string failure)
    {
        failure = null;
        if (expectedLength < 0L)
        {
            return true;
        }

        try
        {
            long actualLength = new FileInfo(filePath).Length;
            if (actualLength == expectedLength)
            {
                return true;
            }
            failure = string.Format(
                CultureInfo.InvariantCulture,
                "本地文件大小不匹配（期望 {0} 字节，实际 {1} 字节）",
                expectedLength,
                actualLength);
        }
        catch (Exception e)
        {
            failure = "读取本地文件大小失败：" + e.Message;
        }
        return false;
    }

    private static bool HasExpectedContentRange(
        string contentRange,
        long expectedStart,
        long expectedEnd,
        long expectedTotal)
    {
        string expected = string.Format(
            CultureInfo.InvariantCulture,
            "bytes {0}-{1}/{2}",
            expectedStart,
            expectedEnd,
            expectedTotal);
        return !string.IsNullOrEmpty(contentRange)
            && contentRange.Trim().Equals(
                expected,
                StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsCdntxUrl(string url)
    {
        Uri uri;
        return Uri.TryCreate(url, UriKind.Absolute, out uri)
            && (uri.Host.Equals(
                    "cdntx2.moecube.com",
                    StringComparison.OrdinalIgnoreCase)
                || uri.Host.Equals(
                    "cdntx.moecube.com",
                    StringComparison.OrdinalIgnoreCase));
    }

    private static long ParseContentLength(string contentLength)
    {
        long parsedLength;
        return long.TryParse(
            contentLength,
            NumberStyles.None,
            CultureInfo.InvariantCulture,
            out parsedLength)
            ? parsedLength
            : -1L;
    }

    private static bool CanCompareContentLengthToDownloadedFile(
        string contentEncoding)
    {
        if (string.IsNullOrEmpty(contentEncoding))
        {
            return true;
        }

        string[] encodings = contentEncoding.Split(',');
        for (int i = 0; i < encodings.Length; i++)
        {
            string encoding = encodings[i].Trim();
            if (encoding.Length > 0
                && !encoding.Equals(
                    "identity",
                    StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }
        return true;
    }

    private static string NormalizeEtag(string etag)
    {
        return string.IsNullOrEmpty(etag) ? null : etag.Trim();
    }

    private static bool TryDeleteFile(string filePath, out string failure)
    {
        failure = null;
        try
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
            return true;
        }
        catch (Exception e)
        {
            failure = e.Message;
            return false;
        }
    }

    private static void TryDeleteFileQuietly(string filePath)
    {
        string ignoredFailure;
        TryDeleteFile(filePath, out ignoredFailure);
    }

    private static bool TryValidateFile(
        string filePath,
        Func<string, bool> validateFile,
        out string failure)
    {
        failure = null;
        if (validateFile == null)
        {
            return true;
        }

        try
        {
            if (validateFile(filePath))
            {
                return true;
            }
            failure = "下载内容校验未通过";
        }
        catch (Exception e)
        {
            failure = "下载内容校验异常：" + e.Message;
        }
        return false;
    }

    /// <summary>
    /// 远端文件元数据（ETag / 大小）的探测结果。
    /// </summary>
    private sealed class RemoteFileMetadata
    {
        public bool Succeeded;
        public string Etag;
        public long ContentLength = -1L;
        public bool CanCompareContentLength = true;
        public string ResolvedUrl;
        public string Failure;
        public int Attempts;
    }

    /// <summary>
    /// 探测远端文件元数据：优先 HEAD；部分 CDN 边缘节点（如超先行卡最新指针 URL）只允许 GET、
    /// 对 HEAD 返回 405，此时回退为 1 字节 Range GET，避免版本检查与下载被直接判死。
    /// </summary>
    private static IEnumerator ProbeRemoteMetadata(
        string url,
        string filePath,
        bool forceDownload,
        Action<RemoteFileMetadata> onComplete)
    {
        RemoteFileMetadata metadata = new RemoteFileMetadata();
        metadata.ResolvedUrl = url;
        string headFailure = null;
        for (int attempt = 1; attempt <= UpdateRequestMaxAttempts; attempt++)
        {
            metadata.Attempts = attempt;
            bool shouldRetry = false;
            using (UnityWebRequest headRequest = UnityWebRequest.Head(url))
            {
                headRequest.timeout = HeadRequestTimeoutSeconds;
                yield return headRequest.SendWebRequest();

                if (headRequest.result == UnityWebRequest.Result.Success)
                {
                    metadata.Etag = NormalizeEtag(
                        headRequest.GetResponseHeader("ETag"));
                    metadata.ContentLength = ParseContentLength(
                        headRequest.GetResponseHeader("Content-Length"));
                    metadata.CanCompareContentLength =
                        CanCompareContentLengthToDownloadedFile(
                            headRequest.GetResponseHeader("Content-Encoding"));
                    if (!string.IsNullOrEmpty(headRequest.url))
                    {
                        metadata.ResolvedUrl = headRequest.url;
                    }
                    if (!forceDownload && string.IsNullOrEmpty(metadata.Etag))
                    {
                        headFailure = "版本检查失败：服务器未返回 ETag";
                        shouldRetry = true;
                    }
                    else
                    {
                        metadata.Succeeded = true;
                    }
                }
                else
                {
                    headFailure = DescribeRequestFailure("版本检查失败", headRequest);
                    shouldRetry = ShouldRetryRequest(
                        headRequest.result,
                        headRequest.responseCode);
                }
            }

            if (metadata.Succeeded)
            {
                break;
            }
            if (!shouldRetry || attempt >= UpdateRequestMaxAttempts)
            {
                break;
            }
            LogRetry(filePath, "版本检查", attempt, headFailure);
            yield return new WaitForSecondsRealtime(GetRetryDelaySeconds(attempt));
        }

        if (!metadata.Succeeded)
        {
            RemoteFileMetadata fallback = null;
            yield return ProbeRemoteMetadataViaRangeGet(
                url,
                filePath,
                forceDownload,
                (result) => { fallback = result; });

            string fallbackFailure = fallback == null
                ? "未知错误"
                : fallback.Failure;
            if (fallback != null && fallback.Succeeded)
            {
                Debug.LogWarning(
                    "[资源更新] "
                    + Path.GetFileName(filePath)
                    + " 的 HEAD 版本检查不可用，已改用 GET 回退探测："
                    + headFailure);
                metadata = fallback;
            }
            else
            {
                metadata.Failure = string.Format(
                    "{0}；GET 回退探测也失败：{1}",
                    string.IsNullOrEmpty(headFailure)
                        ? "版本检查失败"
                        : WithAttemptCount(headFailure, metadata.Attempts),
                    string.IsNullOrEmpty(fallbackFailure)
                        ? "未知错误"
                        : fallbackFailure);
            }
        }

        if (onComplete != null) onComplete.Invoke(metadata);
    }

    /// <summary>
    /// HEAD 不可用时的兜底探测：用 Range: bytes=0-0 只取 1 字节，
    /// 从 206 响应的 Content-Range 读出文件总大小，ETag 直接取自响应头。
    /// 服务器若忽略 Range 而返回 200，则退化为整文件读入内存（极小概率，仅影响内存占用）。
    /// </summary>
    private static IEnumerator ProbeRemoteMetadataViaRangeGet(
        string url,
        string filePath,
        bool forceDownload,
        Action<RemoteFileMetadata> onComplete)
    {
        RemoteFileMetadata metadata = new RemoteFileMetadata();
        metadata.ResolvedUrl = url;
        for (int attempt = 1; attempt <= UpdateRequestMaxAttempts; attempt++)
        {
            metadata.Attempts = attempt;
            bool shouldRetry = false;
            using (UnityWebRequest probeRequest = UnityWebRequest.Get(url))
            {
                probeRequest.downloadHandler = new DownloadHandlerBuffer();
                probeRequest.timeout = HeadRequestTimeoutSeconds;
                probeRequest.SetRequestHeader("Range", "bytes=0-0");
                yield return probeRequest.SendWebRequest();

                if (probeRequest.result == UnityWebRequest.Result.Success)
                {
                    metadata.Etag = NormalizeEtag(
                        probeRequest.GetResponseHeader("ETag"));
                    metadata.CanCompareContentLength =
                        CanCompareContentLengthToDownloadedFile(
                            probeRequest.GetResponseHeader("Content-Encoding"));
                    metadata.ContentLength = probeRequest.responseCode == 206L
                        ? ParseContentRangeTotal(
                            probeRequest.GetResponseHeader("Content-Range"))
                        : ParseContentLength(
                            probeRequest.GetResponseHeader("Content-Length"));
                    if (!string.IsNullOrEmpty(probeRequest.url))
                    {
                        metadata.ResolvedUrl = probeRequest.url;
                    }

                    if (metadata.ContentLength <= 0L)
                    {
                        metadata.Failure = "版本检查失败：服务器未返回有效的文件大小";
                        shouldRetry = true;
                    }
                    else if (!forceDownload && string.IsNullOrEmpty(metadata.Etag))
                    {
                        metadata.Failure = "版本检查失败：服务器未返回 ETag";
                        shouldRetry = true;
                    }
                    else
                    {
                        metadata.Succeeded = true;
                    }
                }
                else
                {
                    metadata.Failure = DescribeRequestFailure(
                        "版本检查失败（GET 回退）",
                        probeRequest);
                    shouldRetry = ShouldRetryRequest(
                        probeRequest.result,
                        probeRequest.responseCode);
                }
            }

            if (metadata.Succeeded)
            {
                break;
            }
            if (!shouldRetry || attempt >= UpdateRequestMaxAttempts)
            {
                break;
            }
            LogRetry(filePath, "版本检查（GET 回退）", attempt, metadata.Failure);
            yield return new WaitForSecondsRealtime(GetRetryDelaySeconds(attempt));
        }

        if (onComplete != null) onComplete.Invoke(metadata);
    }

    /// <summary>
    /// 从 Content-Range（形如 "bytes 0-0/14813656"）里取文件总大小；无法解析时返回 -1。
    /// </summary>
    private static long ParseContentRangeTotal(string contentRange)
    {
        if (string.IsNullOrEmpty(contentRange))
        {
            return -1L;
        }

        int slashIndex = contentRange.LastIndexOf('/');
        if (slashIndex < 0 || slashIndex == contentRange.Length - 1)
        {
            return -1L;
        }
        return ParseContentLength(contentRange.Substring(slashIndex + 1).Trim());
    }

    private static bool ShouldRetryRequest(
        UnityWebRequest.Result result,
        long statusCode)
    {
        return result == UnityWebRequest.Result.ConnectionError
            || result == UnityWebRequest.Result.DataProcessingError
            || statusCode == 408
            || statusCode == 425
            || statusCode == 429
            || statusCode >= 500;
    }

    private static string DescribeRequestFailure(
        string stage,
        UnityWebRequest request)
    {
        string detail = string.IsNullOrEmpty(request.error)
            ? request.result.ToString()
            : request.error;
        return request.responseCode > 0
            ? string.Format("{0}（HTTP {1}：{2}）", stage, request.responseCode, detail)
            : string.Format("{0}（{1}）", stage, detail);
    }

    private static float GetRetryDelaySeconds(int failedAttempt)
    {
        return failedAttempt;
    }

    private static string WithAttemptCount(string reason, int attempts)
    {
        return string.Format(
            "{0}（已尝试 {1} 次）",
            string.IsNullOrEmpty(reason) ? "未知错误" : reason,
            attempts);
    }

    private static void LogRetry(
        string filePath,
        string stage,
        int failedAttempt,
        string reason)
    {
        Debug.LogWarning(
            string.Format(
                "[资源更新] {0} {1}失败，{2:0.#} 秒后进行第 {3}/{4} 次尝试：{5}",
                Path.GetFileName(filePath),
                stage,
                GetRetryDelaySeconds(failedAttempt),
                failedAttempt + 1,
                UpdateRequestMaxAttempts,
                reason));
    }

    private static void CompleteFailure(
        Action<bool> onComplete,
        Action<string> onError,
        string reason)
    {
        if (onError != null)
        {
            try
            {
                onError.Invoke(reason);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }
        if (onComplete != null)
        {
            onComplete.Invoke(false);
        }
    }

    private static void FinishWithFailure(
        string filePath,
        Action<bool> onComplete,
        Action<string> onError,
        string reason)
    {
        Debug.LogWarning(
            "[资源更新] " + Path.GetFileName(filePath) + " 最终失败：" + reason);
        CompleteFailure(onComplete, onError, reason);
    }

    private static int GetTimeoutForFile(string filename)
    {
        string extension = Path.GetExtension(filename).ToLower();
        switch (extension)
        {
            case ".png":
                return 100;
            case ".jpg":
                return 100;
            case ".cdb":
                return 300;
            case ".conf":
                return 50;
            default:
                return 40;
        }
    }
}
