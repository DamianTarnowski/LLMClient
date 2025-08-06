use std::ffi::{CStr, CString};
use std::os::raw::c_char;
use std::ptr;
use once_cell::sync::OnceCell;
use tokenizers::Tokenizer;

static TOKENIZER: OnceCell<Tokenizer> = OnceCell::new();

#[no_mangle]
pub extern "C" fn tokenizer_init(path: *const c_char) -> i32 {
    let c_str = unsafe { CStr::from_ptr(path) };
    let path_str = match c_str.to_str() {
        Ok(s) => s,
        Err(_) => return -1,
    };
    println!("[tokenizer_rust] tokenizer_init | path={}", path_str);
    match Tokenizer::from_file(path_str) {
        Ok(tokenizer) => {
            let _ = TOKENIZER.set(tokenizer);
            0
        }
        Err(_) => -2,
    }
}

#[no_mangle]
pub extern "C" fn tokenizer_encode(text: *const c_char, out_ids: *mut i32, max_len: usize) -> i32 {
    let c_str = unsafe { CStr::from_ptr(text) };
    let text_str = match c_str.to_str() {
        Ok(s) => s,
        Err(_) => return -1,
    };
    let tokenizer = match TOKENIZER.get() {
        Some(t) => t,
        None => return -2,
    };
    let encoding = match tokenizer.encode(text_str, true) {
        Ok(enc) => enc,
        Err(_) => return -3,
    };
    let ids = encoding.get_ids();
    let ids_i32: Vec<i32> = ids.iter().map(|&id| id as i32).collect();
    let len = ids_i32.len().min(max_len);
    println!("[tokenizer_rust] encode | text='{}' | ids_len={} | first_ids={:?}", text_str.chars().take(30).collect::<String>(), len, &ids_i32[..len.min(10)]);
    unsafe {
        ptr::copy_nonoverlapping(ids_i32.as_ptr(), out_ids, len);
    }
    len as i32
}

#[no_mangle]
pub extern "C" fn tokenizer_decode(ids: *const i32, len: usize) -> *mut c_char {
    let tokenizer = match TOKENIZER.get() {
        Some(t) => t,
        None => return ptr::null_mut(),
    };
    let ids_slice = unsafe { std::slice::from_raw_parts(ids, len) };
    let tokens: Vec<u32> = ids_slice.iter().map(|&id| id as u32).collect();
    match tokenizer.decode(&tokens, true) {
        Ok(text) => CString::new(text).unwrap().into_raw(),
        Err(_) => ptr::null_mut(),
    }
}

#[no_mangle]
pub extern "C" fn tokenizer_cleanup() {
    // OnceCell nie pozwala na drop, ale można dodać logikę jeśli trzeba
} 