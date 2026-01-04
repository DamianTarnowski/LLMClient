use std::ffi::{CStr, CString};
use std::os::raw::c_char;
use std::ptr;
use std::sync::RwLock;
use std::collections::HashMap;
use once_cell::sync::Lazy;
use tokenizers::Tokenizer;
use kitoken::Kitoken;

// Enum dla różnych typów tokenizerów
enum TokenizerType {
    HuggingFace(Tokenizer),
    Kitoken(Kitoken),
}

// Stary tokenizer dla kompatybilności wstecznej (e5)
static TOKENIZER: Lazy<RwLock<Option<Tokenizer>>> = Lazy::new(|| RwLock::new(None));

// Nowy multi-tokenizer: nazwa -> tokenizer (obsługuje oba typy)
static TOKENIZERS: Lazy<RwLock<HashMap<String, TokenizerType>>> = Lazy::new(|| RwLock::new(HashMap::new()));

// ============== LEGACY API (e5) ==============

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
            let mut guard = TOKENIZER.write().unwrap();
            *guard = Some(tokenizer);
            0
        }
        Err(e) => {
            println!("[tokenizer_rust] ERROR: {:?}", e);
            -2
        }
    }
}

#[no_mangle]
pub extern "C" fn tokenizer_encode(text: *const c_char, out_ids: *mut i32, max_len: usize) -> i32 {
    let c_str = unsafe { CStr::from_ptr(text) };
    let text_str = match c_str.to_str() {
        Ok(s) => s,
        Err(_) => return -1,
    };
    let guard = TOKENIZER.read().unwrap();
    let tokenizer = match guard.as_ref() {
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
    unsafe {
        ptr::copy_nonoverlapping(ids_i32.as_ptr(), out_ids, len);
    }
    len as i32
}

#[no_mangle]
pub extern "C" fn tokenizer_decode(ids: *const i32, len: usize) -> *mut c_char {
    let guard = TOKENIZER.read().unwrap();
    let tokenizer = match guard.as_ref() {
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
    let mut guard = TOKENIZER.write().unwrap();
    *guard = None;
}

// ============== MULTI-TOKENIZER API ==============

/// Inicjalizuje tokenizer pod podaną nazwą (np. "e5", "gemma")
/// Najpierw próbuje HuggingFace tokenizers, potem SentencePiece
#[no_mangle]
pub extern "C" fn tokenizer_init_named(name: *const c_char, path: *const c_char) -> i32 {
    let name_str = match unsafe { CStr::from_ptr(name) }.to_str() {
        Ok(s) => s.to_string(),
        Err(_) => return -1,
    };
    let path_str = match unsafe { CStr::from_ptr(path) }.to_str() {
        Ok(s) => s,
        Err(_) => return -1,
    };
    
    println!("[tokenizer_rust] tokenizer_init_named | name={} | path={}", name_str, path_str);
    
    // Próbuj HuggingFace tokenizers najpierw
    match Tokenizer::from_file(path_str) {
        Ok(tokenizer) => {
            println!("[tokenizer_rust] OK: loaded {} as HuggingFace tokenizer", name_str);
            let mut guard = TOKENIZERS.write().unwrap();
            guard.insert(name_str, TokenizerType::HuggingFace(tokenizer));
            return 0;
        }
        Err(e) => {
            println!("[tokenizer_rust] HuggingFace failed for {}: {:?}", name_str, e);
        }
    }
    
    // Fallback: próbuj SentencePiece (.model file)
    let sp_path = if path_str.ends_with(".json") {
        path_str.replace("tokenizer.json", "tokenizer.model")
    } else {
        path_str.to_string()
    };
    
    println!("[tokenizer_rust] Trying SentencePiece: {}", sp_path);
    match SentencePieceProcessor::open(&sp_path) {
        Ok(sp) => {
            println!("[tokenizer_rust] OK: loaded {} as SentencePiece", name_str);
            let mut guard = TOKENIZERS.write().unwrap();
            guard.insert(name_str, TokenizerType::SentencePiece(sp));
            0
        }
        Err(e) => {
            println!("[tokenizer_rust] ERROR: SentencePiece failed for {}: {:?}", name_str, e);
            -2
        }
    }
}

/// Koduje tekst używając tokenizera o podanej nazwie
#[no_mangle]
pub extern "C" fn tokenizer_encode_named(
    name: *const c_char,
    text: *const c_char,
    out_ids: *mut i32,
    max_len: usize
) -> i32 {
    let name_str = match unsafe { CStr::from_ptr(name) }.to_str() {
        Ok(s) => s,
        Err(_) => return -1,
    };
    let text_str = match unsafe { CStr::from_ptr(text) }.to_str() {
        Ok(s) => s,
        Err(_) => return -1,
    };
    
    let guard = TOKENIZERS.read().unwrap();
    let tokenizer = match guard.get(name_str) {
        Some(t) => t,
        None => {
            println!("[tokenizer_rust] ERROR: tokenizer '{}' not found", name_str);
            return -2;
        }
    };
    
    let ids_i32: Vec<i32> = match tokenizer {
        TokenizerType::HuggingFace(hf) => {
            match hf.encode(text_str, true) {
                Ok(enc) => enc.get_ids().iter().map(|&id| id as i32).collect(),
                Err(e) => {
                    println!("[tokenizer_rust] ERROR encoding HF: {:?}", e);
                    return -3;
                }
            }
        }
        TokenizerType::SentencePiece(sp) => {
            match sp.encode(text_str) {
                Ok(pieces) => pieces.iter().map(|p| p.id as i32).collect(),
                Err(e) => {
                    println!("[tokenizer_rust] ERROR encoding SP: {:?}", e);
                    return -3;
                }
            }
        }
    };
    
    let len = ids_i32.len().min(max_len);
    unsafe {
        ptr::copy_nonoverlapping(ids_i32.as_ptr(), out_ids, len);
    }
    len as i32
}

/// Dekoduje tokeny używając tokenizera o podanej nazwie
#[no_mangle]
pub extern "C" fn tokenizer_decode_named(
    name: *const c_char,
    ids: *const i32,
    len: usize
) -> *mut c_char {
    let name_str = match unsafe { CStr::from_ptr(name) }.to_str() {
        Ok(s) => s,
        Err(_) => return ptr::null_mut(),
    };
    
    let guard = TOKENIZERS.read().unwrap();
    let tokenizer = match guard.get(name_str) {
        Some(t) => t,
        None => return ptr::null_mut(),
    };
    
    let ids_slice = unsafe { std::slice::from_raw_parts(ids, len) };
    
    let text = match tokenizer {
        TokenizerType::HuggingFace(hf) => {
            let tokens: Vec<u32> = ids_slice.iter().map(|&id| id as u32).collect();
            match hf.decode(&tokens, true) {
                Ok(t) => t,
                Err(_) => return ptr::null_mut(),
            }
        }
        TokenizerType::SentencePiece(sp) => {
            let pieces: Vec<u32> = ids_slice.iter().map(|&id| id as u32).collect();
            match sp.decode_piece_ids(&pieces) {
                Ok(t) => t,
                Err(_) => return ptr::null_mut(),
            }
        }
    };
    
    CString::new(text).unwrap().into_raw()
}

/// Usuwa tokenizer o podanej nazwie
#[no_mangle]
pub extern "C" fn tokenizer_cleanup_named(name: *const c_char) -> i32 {
    let name_str = match unsafe { CStr::from_ptr(name) }.to_str() {
        Ok(s) => s,
        Err(_) => return -1,
    };
    
    let mut guard = TOKENIZERS.write().unwrap();
    if guard.remove(name_str).is_some() {
        0
    } else {
        -1
    }
}

/// Zwraca liczbę załadowanych tokenizerów
#[no_mangle]
pub extern "C" fn tokenizer_count() -> i32 {
    let guard = TOKENIZERS.read().unwrap();
    guard.len() as i32
}