using System;
using System.Runtime.InteropServices;

internal static class MyCardCredentialStore
{
    private const string PasswordAccountPrefix = "__password__:";

#if (UNITY_IOS || UNITY_IPHONE) && !UNITY_EDITOR
    [DllImport("__Internal", CallingConvention = CallingConvention.Cdecl)]
    private static extern int KoishiMyCardKeychainSet(
        [MarshalAs(UnmanagedType.LPUTF8Str)] string account,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string token);

    [DllImport("__Internal", CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr KoishiMyCardKeychainGet(
        [MarshalAs(UnmanagedType.LPUTF8Str)] string account);

    [DllImport("__Internal", CallingConvention = CallingConvention.Cdecl)]
    private static extern void KoishiMyCardKeychainFree(IntPtr value);

    [DllImport("__Internal", CallingConvention = CallingConvention.Cdecl)]
    private static extern int KoishiMyCardKeychainDelete(
        [MarshalAs(UnmanagedType.LPUTF8Str)] string account);
#else
    private static string memoryAccount;
    private static string memoryToken;
#endif

    public static bool Save(string account, string token)
    {
        if (string.IsNullOrEmpty(account) || string.IsNullOrEmpty(token))
        {
            return false;
        }

#if (UNITY_IOS || UNITY_IPHONE) && !UNITY_EDITOR
        try
        {
            return KoishiMyCardKeychainSet(account, token) == 1;
        }
        catch (Exception)
        {
            return false;
        }
#else
        memoryAccount = account;
        memoryToken = token;
        return true;
#endif
    }

    public static bool TryLoad(string account, out string token)
    {
        token = null;
        if (string.IsNullOrEmpty(account))
        {
            return false;
        }

#if (UNITY_IOS || UNITY_IPHONE) && !UNITY_EDITOR
        IntPtr value = IntPtr.Zero;
        try
        {
            value = KoishiMyCardKeychainGet(account);
            if (value == IntPtr.Zero)
            {
                return false;
            }

            token = Marshal.PtrToStringUTF8(value);
            return !string.IsNullOrEmpty(token);
        }
        catch (Exception)
        {
            token = null;
            return false;
        }
        finally
        {
            if (value != IntPtr.Zero)
            {
                try
                {
                    KoishiMyCardKeychainFree(value);
                }
                catch (Exception) { }
            }
        }
#else
        if (!string.Equals(memoryAccount, account, StringComparison.Ordinal)
            || string.IsNullOrEmpty(memoryToken))
        {
            return false;
        }

        token = memoryToken;
        return true;
#endif
    }

    public static bool Delete(string account)
    {
        if (string.IsNullOrEmpty(account))
        {
            return true;
        }

#if (UNITY_IOS || UNITY_IPHONE) && !UNITY_EDITOR
        try
        {
            return KoishiMyCardKeychainDelete(account) == 1;
        }
        catch (Exception)
        {
            return false;
        }
#else
        if (string.Equals(memoryAccount, account, StringComparison.Ordinal))
        {
            memoryAccount = null;
            memoryToken = null;
        }
        return true;
#endif
    }

    public static bool SavePassword(string account, string password)
    {
        if (string.IsNullOrEmpty(account) || string.IsNullOrEmpty(password))
        {
            return false;
        }

#if (UNITY_IOS || UNITY_IPHONE) && !UNITY_EDITOR
        bool saved = Save(PasswordAccountPrefix + account, password);
        if (saved)
        {
            try
            {
                Config.Set("mycard_password", string.Empty);
            }
            catch (Exception) { }
        }
        return saved;
#else
        try
        {
            Config.Set("mycard_password", password);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
#endif
    }

    public static bool TryLoadPassword(string account, out string password)
    {
        password = null;
        if (string.IsNullOrEmpty(account))
        {
            return false;
        }

#if (UNITY_IOS || UNITY_IPHONE) && !UNITY_EDITOR
        if (TryLoad(PasswordAccountPrefix + account, out password))
        {
            return true;
        }

        string legacyPassword = Config.Get("mycard_password", string.Empty);
        if (string.Equals(
                Config.Get("mycard_username", string.Empty),
                account,
                StringComparison.Ordinal)
            && !string.IsNullOrEmpty(legacyPassword)
            && SavePassword(account, legacyPassword))
        {
            password = legacyPassword;
            return true;
        }
        password = null;
        return false;
#else
        if (!string.Equals(
            Config.Get("mycard_username", string.Empty),
            account,
            StringComparison.Ordinal))
        {
            return false;
        }

        password = Config.Get("mycard_password", string.Empty);
        return !string.IsNullOrEmpty(password);
#endif
    }
}
