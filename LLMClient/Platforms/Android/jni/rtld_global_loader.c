#include <jni.h>
#include <dlfcn.h>
#include <android/log.h>
#include <string.h>

#define LOG_TAG "RtldGlobalLoader"
#define LOGI(...) __android_log_print(ANDROID_LOG_INFO, LOG_TAG, __VA_ARGS__)
#define LOGW(...) __android_log_print(ANDROID_LOG_WARN, LOG_TAG, __VA_ARGS__)
#define LOGE(...) __android_log_print(ANDROID_LOG_ERROR, LOG_TAG, __VA_ARGS__)

static void log_tvmffi_visibility(void) {
    dlerror();
    void *addr = dlsym(RTLD_DEFAULT, "TVMFFIFunctionCall");
    const char *err = dlerror();
    if (addr != NULL && err == NULL) {
        LOGI("dlsym RTLD_DEFAULT TVMFFIFunctionCall OK: %p", addr);
    } else {
        LOGW("dlsym RTLD_DEFAULT TVMFFIFunctionCall FAILED: %s", err ? err : "unknown error");
    }
}

JNIEXPORT jboolean JNICALL
Java_com_llmclient_mlcllm_RtldGlobalLoader_dlopenGlobal(JNIEnv *env, jclass clazz, jstring jpath) {
    const char *path = (*env)->GetStringUTFChars(env, jpath, NULL);
    if (!path) {
        LOGE("Failed to get path string");
        return JNI_FALSE;
    }

    LOGI("dlopen RTLD_GLOBAL: %s", path);
    void *handle = dlopen(path, RTLD_NOW | RTLD_GLOBAL);
    
    (*env)->ReleaseStringUTFChars(env, jpath, path);

    if (handle) {
        LOGI("dlopen RTLD_GLOBAL OK: %s", path);
        // Only log visibility checks for TVM-related libs to keep logs readable.
        if (strstr(path, "libtvm_ffi.so") != NULL || strstr(path, "libtvm4j_runtime_packed.so") != NULL) {
            log_tvmffi_visibility();
        }
        return JNI_TRUE;
    } else {
        const char *err = dlerror();
        LOGE("dlopen RTLD_GLOBAL FAILED: %s - %s", path, err ? err : "unknown error");
        return JNI_FALSE;
    }
}
