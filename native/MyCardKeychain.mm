// Implements the existing MyCardCredentialStore C ABI. No credentials are logged.
#include <CoreFoundation/CoreFoundation.h>
#include <Security/Security.h>
#include <stdlib.h>
#include <string.h>

static CFMutableDictionaryRef Query(const char *account) {
    if (!account || !*account) return NULL;
    CFStringRef name = CFStringCreateWithCString(NULL, account, kCFStringEncodingUTF8);
    CFStringRef bundle = CFBundleGetIdentifier(CFBundleGetMainBundle());
    if (!name || !bundle) { if (name) CFRelease(name); return NULL; }
    CFStringRef service = CFStringCreateWithFormat(NULL, NULL, CFSTR("%@.mycard"), bundle);
    CFMutableDictionaryRef query = CFDictionaryCreateMutable(NULL, 0,
        &kCFTypeDictionaryKeyCallBacks, &kCFTypeDictionaryValueCallBacks);
    if (!service || !query) {
        if (service) CFRelease(service);
        if (query) CFRelease(query);
        CFRelease(name);
        return NULL;
    }
    CFDictionarySetValue(query, kSecClass, kSecClassGenericPassword);
    CFDictionarySetValue(query, kSecAttrService, service);
    CFDictionarySetValue(query, kSecAttrAccount, name);
    CFRelease(service); CFRelease(name);
    return query;
}

extern "C" __attribute__((visibility("default")))
int KoishiMyCardKeychainSet(const char *account, const char *token) {
    if (!token || !*token) return 0;
    CFMutableDictionaryRef query = Query(account);
    if (!query) return 0;
    CFDataRef data = CFDataCreate(NULL, (const UInt8 *)token, (CFIndex)strlen(token));
    CFMutableDictionaryRef values = CFDictionaryCreateMutable(NULL, 0,
        &kCFTypeDictionaryKeyCallBacks, &kCFTypeDictionaryValueCallBacks);
    if (!data || !values) {
        if (data) CFRelease(data);
        if (values) CFRelease(values);
        CFRelease(query); return 0;
    }
    CFDictionarySetValue(values, kSecValueData, data);
    CFDictionarySetValue(values, kSecAttrAccessible, kSecAttrAccessibleAfterFirstUnlockThisDeviceOnly);
    OSStatus result = SecItemUpdate(query, values);
    if (result == errSecItemNotFound) {
        CFDictionarySetValue(query, kSecValueData, data);
        CFDictionarySetValue(query, kSecAttrAccessible, kSecAttrAccessibleAfterFirstUnlockThisDeviceOnly);
        result = SecItemAdd(query, NULL);
        if (result == errSecDuplicateItem) {
            CFDictionaryRemoveValue(query, kSecValueData);
            CFDictionaryRemoveValue(query, kSecAttrAccessible);
            result = SecItemUpdate(query, values);
        }
    }
    CFRelease(values); CFRelease(data); CFRelease(query);
    return result == errSecSuccess ? 1 : 0;
}

extern "C" __attribute__((visibility("default")))
char *KoishiMyCardKeychainGet(const char *account) {
    CFMutableDictionaryRef query = Query(account);
    if (!query) return NULL;
    CFDictionarySetValue(query, kSecReturnData, kCFBooleanTrue);
    CFDictionarySetValue(query, kSecMatchLimit, kSecMatchLimitOne);
    CFTypeRef result = NULL;
    OSStatus status = SecItemCopyMatching(query, &result);
    CFRelease(query);
    if (status != errSecSuccess || !result) { if (result) CFRelease(result); return NULL; }
    char *output = NULL;
    if (CFGetTypeID(result) == CFDataGetTypeID()) {
        CFDataRef data = (CFDataRef)result;
        CFIndex size = CFDataGetLength(data);
        if (size > 0) {
            output = (char *)malloc((size_t)size + 1);
            if (output) { memcpy(output, CFDataGetBytePtr(data), (size_t)size); output[size] = 0; }
        }
    }
    CFRelease(result);
    return output;
}

extern "C" __attribute__((visibility("default")))
void KoishiMyCardKeychainFree(char *value) { free(value); }

extern "C" __attribute__((visibility("default")))
int KoishiMyCardKeychainDelete(const char *account) {
    CFMutableDictionaryRef query = Query(account);
    if (!query) return 0;
    OSStatus status = SecItemDelete(query);
    CFRelease(query);
    return status == errSecSuccess || status == errSecItemNotFound ? 1 : 0;
}
