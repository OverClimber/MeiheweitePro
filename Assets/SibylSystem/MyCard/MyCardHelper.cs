/*

MyCard Helper for MyCard API

Author: Nanahira (78877@qq.com)

IMPORTANT: This module is for intracting with MyCard API for allowing players to have ranked matches in MyCard ranking system. Please contact MyCard before forking or redistributing the project with this module, or including this module in other projects.

Please send emails to pokeboyexn@gmail.com for further information.


*/

using UnityEngine;
using UnityEngine.Networking;
using System;
using System.Text;
using System.Text.RegularExpressions;
using System.Collections;

[Serializable]
public class LoginUserObject
{
    public string username;
    public string avatar;
    public string token;
}

[Serializable]
public class LoginObject
{
    public LoginUserObject user;
    public string token;
}

[Serializable]
public class LoginRequest
{
    public string account;
    public string password;
}

[Serializable]
public class MatchResultObject
{
    public string address;
    public int port;
    public string password;
}

[Serializable]
public class U16SecretObject
{
    public int u16Secret;
}

[Serializable]
public class ArenaUserInfo
{
    public int exp;
    public int pt;
    public int entertain_win;
    public int entertain_lose;
    public int entertain_draw;
    public int entertain_all;
    public string entertain_wl_ratio;
    public int exp_rank;
    public int athletic_win;
    public int athletic_lose;
    public int athletic_draw;
    public int athletic_all;
    public string athletic_wl_ratio;
    public int arena_rank;
}

public enum MyCardRequestFailure
{
    None,
    Cancelled,
    MissingSession,
    Unauthorized,
    RateLimited,
    Server,
    Network,
    Http,
    InvalidResponse,
    CredentialStorage
}

public sealed class MyCardRequestResult
{
    public bool Success { get; private set; }
    public long StatusCode { get; private set; }
    public MyCardRequestFailure Failure { get; private set; }
    public string Message { get; private set; }

    public bool IsUnauthorized
    {
        get { return Failure == MyCardRequestFailure.Unauthorized; }
    }

    private MyCardRequestResult(bool success, long statusCode, MyCardRequestFailure failure, string message)
    {
        Success = success;
        StatusCode = statusCode;
        Failure = failure;
        Message = message;
    }

    public static MyCardRequestResult Succeeded(long statusCode)
    {
        return new MyCardRequestResult(true, statusCode, MyCardRequestFailure.None, null);
    }

    public static MyCardRequestResult Failed(long statusCode, MyCardRequestFailure failure, string message)
    {
        return new MyCardRequestResult(false, statusCode, failure, message);
    }
}

public class MyCardHelper
{
    [Serializable]
    private sealed class ApiErrorObject
    {
        public string message = null;
    }

    private const string MyCardApiBaseUrl = "https://sapi.moecube.com:444";
    private const int RequestTimeoutSeconds = 15;
    private const string AvatarAccountConfigKey = "mycard_avatar_account";
    private const string AvatarUrlConfigKey = "mycard_avatar_url";

    public string username = null;
    public string avatarUrl { get; private set; }
    private string token = null;
    private int u16Secret = -1;
    private UnityWebRequest currentRequest = null;
    private bool cancelRequested = false;
    private int requestGeneration = 0;

    public bool HasSession
    {
        get { return !string.IsNullOrEmpty(username) && !string.IsNullOrEmpty(token); }
    }

    public bool RestoreSession(string name)
    {
        ForgetSession();

        string restoredToken;
        if (string.IsNullOrEmpty(name) || !MyCardCredentialStore.TryLoad(name, out restoredToken))
        {
            return false;
        }

        username = name;
        token = restoredToken;
        if (string.Equals(
            Config.Get(AvatarAccountConfigKey, string.Empty),
            name,
            StringComparison.Ordinal))
        {
            avatarUrl = Config.Get(AvatarUrlConfigKey, string.Empty);
        }
        return true;
    }

    public void ClearSession()
    {
        string account = username;
        ForgetSession();

        if (!string.IsNullOrEmpty(account))
        {
            MyCardCredentialStore.Delete(account);
        }
    }

    private void ForgetSession()
    {
        username = null;
        avatarUrl = null;
        token = null;
        u16Secret = -1;
    }

    public void ResetCancellation()
    {
        cancelRequested = false;
        unchecked
        {
            requestGeneration++;
        }
    }

    public void CancelCurrentRequest()
    {
        cancelRequested = true;
        unchecked
        {
            requestGeneration++;
        }
        if (currentRequest != null)
        {
            UnityWebRequest request = currentRequest;
            currentRequest = null;
            try
            {
                request.Abort();
            }
            catch { }
        }
    }

    private bool IsRequestCancelled(int generation)
    {
        return cancelRequested || generation != requestGeneration;
    }

    private void ConfigureRequest(UnityWebRequest request)
    {
        currentRequest = request;
        request.timeout = RequestTimeoutSeconds;
        request.SetRequestHeader("User-Agent", "KoshiPro2iOS");
        request.SetRequestHeader("Accept", "application/json");
    }

    private void ClearCurrentRequest(UnityWebRequest request)
    {
        if (currentRequest == request)
        {
            currentRequest = null;
        }
    }

    private static string RedactSensitiveResponseFields(string response)
    {
        if (string.IsNullOrEmpty(response))
        {
            return string.Empty;
        }

        return Regex.Replace(
            response,
            @"(""[^""]*(?:token|secret|password)[^""]*""\s*:\s*)(?:""(?:\\.|[^""])*""|-?\d+(?:\.\d+)?|true|false|null)",
            "$1\"<redacted>\"",
            RegexOptions.IgnoreCase
        );
    }

    private static string GetLocalizedApiErrorMessage(string response)
    {
        if (string.IsNullOrEmpty(response))
        {
            return null;
        }

        try
        {
            ApiErrorObject error = JsonUtility.FromJson<ApiErrorObject>(response);
            if (error == null)
            {
                return null;
            }

            switch (error.message)
            {
                case "i_password_error":
                    return "密码错误";
                case "i_user_unexists":
                    return "用户不存在";
                default:
                    return null;
            }
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static void DebugLogResponse(string endpoint, UnityWebRequest request)
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
#if UNITY_EDITOR
        Debug.Assert(
            !RedactSensitiveResponseFields("{\"token\":\"sensitive\"}").Contains("sensitive")
            && GetLocalizedApiErrorMessage("{\"message\":\"i_password_error\"}") == "密码错误"
            && GetLocalizedApiErrorMessage("{\"message\":\"i_user_unexists\"}") == "用户不存在",
            "MyCard response handling check failed."
        );
#endif
        string response = request.downloadHandler == null
            ? string.Empty
            : request.downloadHandler.text;
        Debug.Log(
            string.Format(
                "[MyCard] {0} response (HTTP {1}): {2}",
                endpoint,
                request.responseCode,
                RedactSensitiveResponseFields(response)
            )
        );
#endif
    }

    private MyCardRequestResult BuildRequestFailure(
        UnityWebRequest request,
        string fallbackMessage,
        int generation)
    {
        if (IsRequestCancelled(generation))
        {
            return MyCardRequestResult.Failed(0, MyCardRequestFailure.Cancelled, "请求已取消");
        }

        long statusCode = request.responseCode;
        MyCardRequestFailure failure;

        if (statusCode == 401 || statusCode == 403)
        {
            failure = MyCardRequestFailure.Unauthorized;
        }
        else if (statusCode == 429)
        {
            failure = MyCardRequestFailure.RateLimited;
        }
        else if (statusCode >= 500)
        {
            failure = MyCardRequestFailure.Server;
        }
        else if (statusCode > 0)
        {
            failure = MyCardRequestFailure.Http;
        }
        else if (request.result == UnityWebRequest.Result.DataProcessingError)
        {
            failure = MyCardRequestFailure.InvalidResponse;
        }
        else
        {
            failure = MyCardRequestFailure.Network;
        }

        string response = request.downloadHandler == null
            ? null
            : request.downloadHandler.text;
        string message = GetLocalizedApiErrorMessage(response);
        if (string.IsNullOrEmpty(message))
        {
            message = statusCode > 0
                ? string.Format("{0} (HTTP {1})", fallbackMessage, statusCode)
                : fallbackMessage;
        }

        if (statusCode == 0 && !string.IsNullOrEmpty(request.error))
        {
            message += ": " + request.error;
        }

        return MyCardRequestResult.Failed(statusCode, failure, message);
    }

    private MyCardRequestResult InvalidResponse(long statusCode, string message)
    {
        return MyCardRequestResult.Failed(statusCode, MyCardRequestFailure.InvalidResponse, message);
    }

    private void Complete(Action<MyCardRequestResult> onComplete, MyCardRequestResult result)
    {
        if (onComplete != null)
        {
            onComplete(result);
        }
    }

    private void Complete(Action<MyCardRequestResult, MatchResultObject> onComplete, MyCardRequestResult result, MatchResultObject match)
    {
        if (onComplete != null)
        {
            onComplete(result, match);
        }
    }

    private void Complete(Action<MyCardRequestResult, ArenaUserInfo> onComplete, MyCardRequestResult result, ArenaUserInfo userInfo)
    {
        if (onComplete != null)
        {
            onComplete(result, userInfo);
        }
    }

    public IEnumerator Login(string name, string password, Action<MyCardRequestResult> onComplete)
    {
        int generation = requestGeneration;
        if (IsRequestCancelled(generation))
        {
            Complete(onComplete, MyCardRequestResult.Failed(0, MyCardRequestFailure.Cancelled, "请求已取消"));
            yield break;
        }

        LoginRequest data = new LoginRequest();
        data.account = name;
        data.password = password;
        byte[] dataBytes = Encoding.UTF8.GetBytes(JsonUtility.ToJson(data));
        data.account = null;
        data.password = null;

        using (UnityWebRequest request = new UnityWebRequest(MyCardApiBaseUrl + "/accounts/signin", UnityWebRequest.kHttpVerbPOST))
        {
            ConfigureRequest(request);
            request.uploadHandler = new UploadHandlerRaw(dataBytes);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");

            try
            {
                yield return request.SendWebRequest();
                DebugLogResponse("/accounts/signin", request);

                if (IsRequestCancelled(generation))
                {
                    Complete(onComplete, MyCardRequestResult.Failed(
                        0,
                        MyCardRequestFailure.Cancelled,
                        "请求已取消"));
                    yield break;
                }

                if (request.result != UnityWebRequest.Result.Success)
                {
                    Complete(onComplete, BuildRequestFailure(request, "MyCard 登录失败", generation));
                    yield break;
                }

                LoginObject resultObject;
                try
                {
                    resultObject = JsonUtility.FromJson<LoginObject>(request.downloadHandler.text);
                }
                catch (Exception)
                {
                    Complete(onComplete, InvalidResponse(request.responseCode, "登录响应解析失败"));
                    yield break;
                }

                if (resultObject == null || resultObject.user == null)
                {
                    Complete(onComplete, InvalidResponse(request.responseCode, "登录响应无效"));
                    yield break;
                }

                string resolvedUsername = string.IsNullOrEmpty(resultObject.user.username)
                    ? name
                    : resultObject.user.username;
                string resolvedToken = string.IsNullOrEmpty(resultObject.token)
                    ? resultObject.user.token
                    : resultObject.token;

                if (string.IsNullOrEmpty(resolvedUsername) || string.IsNullOrEmpty(resolvedToken))
                {
                    Complete(onComplete, InvalidResponse(request.responseCode, "登录响应无效"));
                    yield break;
                }

                if (!MyCardCredentialStore.Save(resolvedUsername, resolvedToken))
                {
                    Complete(onComplete, MyCardRequestResult.Failed(
                        request.responseCode,
                        MyCardRequestFailure.CredentialStorage,
                        "无法安全保存 MyCard 登录状态"));
                    yield break;
                }

                string previousUsername = username;
                if (!string.IsNullOrEmpty(previousUsername)
                    && !string.Equals(previousUsername, resolvedUsername, StringComparison.Ordinal))
                {
                    if (!MyCardCredentialStore.Delete(previousUsername))
                    {
                        MyCardCredentialStore.Delete(resolvedUsername);
                        Complete(onComplete, MyCardRequestResult.Failed(
                            request.responseCode,
                            MyCardRequestFailure.CredentialStorage,
                            "无法安全切换 MyCard 登录账号"));
                        yield break;
                    }
                }

                username = resolvedUsername;
                avatarUrl = resultObject.user.avatar;
                token = resolvedToken;
                u16Secret = -1;
                Config.Set(AvatarAccountConfigKey, resolvedUsername);
                Config.Set(AvatarUrlConfigKey, avatarUrl ?? string.Empty);
                Complete(onComplete, MyCardRequestResult.Succeeded(request.responseCode));
            }
            finally
            {
                Array.Clear(dataBytes, 0, dataBytes.Length);
                ClearCurrentRequest(request);
            }
        }
    }

    public IEnumerator GetArenaUserInfo(
        string name,
        Action<MyCardRequestResult, ArenaUserInfo> onComplete)
    {
        int generation = requestGeneration;
        if (string.IsNullOrEmpty(name))
        {
            Complete(
                onComplete,
                MyCardRequestResult.Failed(
                    0,
                    MyCardRequestFailure.MissingSession,
                    "缺少 MyCard 用户名"),
                null
            );
            yield break;
        }

        string url = MyCardApiBaseUrl
            + "/ygopro/arena/user?username="
            + UnityWebRequest.EscapeURL(name);
        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            ConfigureRequest(request);

            try
            {
                yield return request.SendWebRequest();
                DebugLogResponse("/ygopro/arena/user", request);

                if (request.result != UnityWebRequest.Result.Success)
                {
                    Complete(
                        onComplete,
                        BuildRequestFailure(request, "获取 MyCard 用户资料失败", generation),
                        null
                    );
                    yield break;
                }

                ArenaUserInfo userInfo;
                try
                {
                    userInfo = JsonUtility.FromJson<ArenaUserInfo>(
                        request.downloadHandler.text
                    );
                }
                catch (Exception)
                {
                    Complete(
                        onComplete,
                        InvalidResponse(request.responseCode, "用户资料响应解析失败"),
                        null
                    );
                    yield break;
                }

                if (userInfo == null)
                {
                    Complete(
                        onComplete,
                        InvalidResponse(request.responseCode, "用户资料响应无效"),
                        null
                    );
                    yield break;
                }

                Complete(
                    onComplete,
                    MyCardRequestResult.Succeeded(request.responseCode),
                    userInfo
                );
            }
            finally
            {
                ClearCurrentRequest(request);
            }
        }
    }

    public IEnumerator GetUserU16Secret(Action<MyCardRequestResult> onComplete)
    {
        int generation = requestGeneration;
        u16Secret = -1;

        if (IsRequestCancelled(generation))
        {
            Complete(onComplete, MyCardRequestResult.Failed(0, MyCardRequestFailure.Cancelled, "请求已取消"));
            yield break;
        }

        if (!HasSession)
        {
            Complete(onComplete, MyCardRequestResult.Failed(0, MyCardRequestFailure.MissingSession, "请重新登录 MyCard"));
            yield break;
        }

        using (UnityWebRequest request = UnityWebRequest.Get(MyCardApiBaseUrl + "/accounts/authUser"))
        {
            ConfigureRequest(request);
            request.SetRequestHeader("Authorization", "Bearer " + token);
            request.SetRequestHeader("Content-Type", "application/json; charset=utf-8");

            try
            {
                yield return request.SendWebRequest();

                if (IsRequestCancelled(generation))
                {
                    Complete(onComplete, MyCardRequestResult.Failed(
                        0,
                        MyCardRequestFailure.Cancelled,
                        "请求已取消"));
                    yield break;
                }

                if (request.result != UnityWebRequest.Result.Success)
                {
                    MyCardRequestResult failure = BuildRequestFailure(
                        request,
                        "获取用户密钥失败",
                        generation);
                    if (failure.IsUnauthorized)
                    {
                        ClearSession();
                    }
                    Complete(onComplete, failure);
                    yield break;
                }

                U16SecretObject resultObject;
                try
                {
                    resultObject = JsonUtility.FromJson<U16SecretObject>(request.downloadHandler.text);
                }
                catch (Exception)
                {
                    Complete(onComplete, InvalidResponse(request.responseCode, "用户密钥响应解析失败"));
                    yield break;
                }

                if (resultObject == null || resultObject.u16Secret < 0)
                {
                    Complete(onComplete, InvalidResponse(request.responseCode, "用户密钥响应无效"));
                    yield break;
                }

                u16Secret = resultObject.u16Secret;
                Complete(onComplete, MyCardRequestResult.Succeeded(request.responseCode));
            }
            finally
            {
                ClearCurrentRequest(request);
            }
        }
    }

    public IEnumerator RequestMatch(string matchType, Action<MyCardRequestResult, MatchResultObject> onComplete)
    {
        int generation = requestGeneration;
        if (IsRequestCancelled(generation))
        {
            Complete(onComplete, MyCardRequestResult.Failed(0, MyCardRequestFailure.Cancelled, "请求已取消"), null);
            yield break;
        }

        if (u16Secret < 0 || string.IsNullOrEmpty(username))
        {
            Complete(onComplete, MyCardRequestResult.Failed(0, MyCardRequestFailure.MissingSession, "请重新获取用户密钥"), null);
            yield break;
        }

        int requestSecret = u16Secret;
        u16Secret = -1;
        string authStr = "Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes(username + ":" + requestSecret));
        string url = MyCardApiBaseUrl + "/ygopro/match?locale=zh-CN&arena=" + UnityWebRequest.EscapeURL(matchType);

        using (UnityWebRequest request = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST))
        {
            ConfigureRequest(request);
            request.uploadHandler = new UploadHandlerRaw(new byte[0]);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Authorization", authStr);
            request.SetRequestHeader("Content-Type", "application/x-www-form-urlencoded");

            try
            {
                yield return request.SendWebRequest();

                if (IsRequestCancelled(generation))
                {
                    Complete(onComplete, MyCardRequestResult.Failed(
                        0,
                        MyCardRequestFailure.Cancelled,
                        "请求已取消"), null);
                    yield break;
                }

                if (request.result != UnityWebRequest.Result.Success)
                {
                    Complete(onComplete, BuildRequestFailure(
                        request,
                        "匹配请求失败",
                        generation), null);
                    yield break;
                }

                MatchResultObject matchResultObject;
                try
                {
                    matchResultObject = JsonUtility.FromJson<MatchResultObject>(request.downloadHandler.text);
                }
                catch (Exception)
                {
                    Complete(onComplete, InvalidResponse(request.responseCode, "匹配响应解析失败"), null);
                    yield break;
                }

                if (matchResultObject == null
                    || string.IsNullOrEmpty(matchResultObject.address)
                    || matchResultObject.port <= 0)
                {
                    Complete(onComplete, InvalidResponse(request.responseCode, "匹配响应无效"), null);
                    yield break;
                }

                if (matchResultObject.password == null)
                {
                    matchResultObject.password = string.Empty;
                }

                Complete(onComplete, MyCardRequestResult.Succeeded(request.responseCode), matchResultObject);
            }
            finally
            {
                ClearCurrentRequest(request);
            }
        }
    }
}
