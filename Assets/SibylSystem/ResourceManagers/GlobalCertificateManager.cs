using System.Net;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using UnityEngine;

public static class GlobalCertificateManager
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    public static void Initialize()
    {
        // 注册全局的证书验证回调
        ServicePointManager.ServerCertificateValidationCallback =
            MyRemoteCertificateValidationCallback;
        // Debug.Log("全局证书验证回调已注册，将接受所有HTTPS证书。");
    }

    /// <summary>
    /// 自定义的证书验证回调方法。
    /// </summary>
    /// <param name="sender">请求发送者</param>
    /// <param name="certificate">服务器提供的证书</param>
    /// <param name="chain">证书链</param>
    /// <param name="sslPolicyErrors">SSL策略错误</param>
    /// <returns>始终返回true，表示接受该证书</returns>
    public static bool MyRemoteCertificateValidationCallback(
        object sender,
        X509Certificate certificate,
        X509Chain chain,
        SslPolicyErrors sslPolicyErrors
    )
    {
        // 都是从的官方资源下载，hook 掉不验证证书可以提高下载速度，但是有一定的安全风险
        // return true;
        // Case 1: 证书本身没有问题，直接通过
        if (sslPolicyErrors == SslPolicyErrors.None)
        {
            return true;
        }
        // Case 2: 如果错误是 RemoteCertificateChainErrors，
        // 这通常意味着证书链有问题，比如找不到吊销列表或者根证书不受信任。
        // 我们将尝试进行一次忽略吊销检查的自定义验证。
        if ((sslPolicyErrors & SslPolicyErrors.RemoteCertificateChainErrors) != 0)
        {
            // 创建一个新的证书链对象
            X509Chain customChain = new X509Chain();

            // 设置自定义验证策略
            customChain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck; // <-- 核心：不检查证书吊销
            customChain.ChainPolicy.VerificationFlags =
                X509VerificationFlags.AllowUnknownCertificateAuthority; // 可选：如果你的服务器是自签名证书，需要加上这句。如果服务器证书是由受信任的公共CA颁发的，可以去掉这句。

            // 使用 X509Certificate2，因为它包含更完整的信息
            X509Certificate2 cert2 = new X509Certificate2(certificate);
            // 使用自定义策略进行验证
            bool isChainValid = customChain.Build(cert2);
            // 如果自定义验证构建成功，说明在忽略吊销检查的前提下，证书是可信的
            if (isChainValid)
            {
                return true;
            }
        }

        // Case 3: 对于其他错误（如名称不匹配 RemoteCertificateNameMismatch）或自定义验证失败，
        // 我们认为证书无效。
        Debug.LogErrorFormat("证书验证失败. SslPolicyErrors: {0}", sslPolicyErrors);
        return false;
    }
}
