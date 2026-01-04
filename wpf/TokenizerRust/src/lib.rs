use std::ffi::{c_char, c_int, CStr};
use std::sync::Mutex;
use tokenizers::Tokenizer;

static TOKENIZER: Mutex<Option<Tokenizer>> = Mutex::new(None);

/// Initialize the tokenizer from a tokenizer.json file path
/// Returns 0 on success, negative on error
/// Can be called multiple times to reinitialize with a different tokenizer
#[no_mangle]
pub extern "C" fn tokenizer_initialize(path: *const c_char) -> c_int {
    if path.is_null() {
        return -1;
    }

    let c_str = unsafe { CStr::from_ptr(path) };
    let path_str = match c_str.to_str() {
        Ok(s) => s,
        Err(_) => return -2,
    };

    let tokenizer = match Tokenizer::from_file(path_str) {
        Ok(t) => t,
        Err(_) => return -3,
    };

    match TOKENIZER.lock() {
        Ok(mut guard) => {
            *guard = Some(tokenizer);
            0
        }
        Err(_) => -4, // Lock poisoned
    }
}

/// Encode text to token IDs with special tokens added
/// Returns number of tokens on success, negative on error
#[no_mangle]
pub extern "C" fn tokenizer_encode(
    text: *const c_char,
    out_ids: *mut c_int,
    max_len: usize,
) -> c_int {
    if text.is_null() || out_ids.is_null() {
        return -1;
    }

    let c_str = unsafe { CStr::from_ptr(text) };
    let text_str = match c_str.to_str() {
        Ok(s) => s,
        Err(_) => return -2,
    };

    let guard = match TOKENIZER.lock() {
        Ok(g) => g,
        Err(_) => return -5, // Lock poisoned
    };

    let tokenizer = match guard.as_ref() {
        Some(t) => t,
        None => return -3, // Not initialized
    };

    // Encode with add_special_tokens = true (CRITICAL!)
    let encoding = match tokenizer.encode(text_str, true) {
        Ok(enc) => enc,
        Err(_) => return -4,
    };

    let ids = encoding.get_ids();
    let len = ids.len().min(max_len);

    unsafe {
        for i in 0..len {
            *out_ids.add(i) = ids[i] as c_int;
        }
    }

    len as c_int
}

/// Free the tokenizer and allow reinitialization
#[no_mangle]
pub extern "C" fn tokenizer_free() {
    if let Ok(mut guard) = TOKENIZER.lock() {
        *guard = None;
    }
}
