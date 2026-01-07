# Privacy Policy for LLMClient

**Last Updated: January 7, 2026**

## Introduction

LLMClient ("we", "our", or "the app") is committed to protecting your privacy. This Privacy Policy explains how we handle your information when you use our application.

## Data Collection and Storage

### What We Collect

LLMClient collects and stores the following data **locally on your device only**:

1. **Conversations**: All chat messages between you and AI models
2. **API Keys**: Your personal API keys for OpenAI, Google Gemini, and other AI providers
3. **User Memories**: Personal information extracted from conversations to personalize AI responses (name, preferences, interests, etc.)
4. **App Settings**: Your preferences and configuration choices
5. **Local Model Files**: Downloaded AI model files for offline use

### How Data is Stored

- **All data is stored locally** on your device in an encrypted SQLite database using SQLCipher
- **API keys** are stored using platform-secure storage (Android Keystore, Windows DPAPI)
- **No data is transmitted** to our servers - we do not operate any backend servers
- **No analytics or telemetry** is collected by the app

## Third-Party Services

When you use LLMClient with cloud AI providers, your messages are sent to:

- **OpenAI** (if configured) - Subject to [OpenAI's Privacy Policy](https://openai.com/privacy)
- **Google Gemini** (if configured) - Subject to [Google's Privacy Policy](https://policies.google.com/privacy)
- **Other OpenAI-compatible providers** (if configured) - Subject to their respective privacy policies

**Important**: When using cloud AI models, your conversations are processed by these third-party services according to their privacy policies. LLMClient does not control how these services handle your data.

### Local Models

When using local AI models (ONNX, LLamaSharp), all processing happens on your device. No data is sent to any external service.

## Data You Control

You have full control over your data:

- **Export**: Export conversations to JSON, Markdown, or TXT files
- **Delete**: Delete individual conversations or all data
- **Memory Management**: View, edit, or delete stored memories
- **API Keys**: Remove API keys at any time

## Children's Privacy

LLMClient is not intended for children under 13. We do not knowingly collect information from children.

## Data Security

We implement security measures including:

- **AES-256 encryption** for the local database (SQLCipher)
- **Secure storage** for sensitive credentials
- **No network transmission** of personal data to our servers

## Changes to This Policy

We may update this Privacy Policy. Changes will be reflected in the "Last Updated" date.

## Contact

For privacy concerns, please open an issue on our GitHub repository:
https://github.com/DamianTarnowski/LLMClient

---

# Polityka Prywatności LLMClient (Polski)

**Ostatnia aktualizacja: 7 stycznia 2026**

## Wprowadzenie

LLMClient ("my", "nasza" lub "aplikacja") zobowiązuje się do ochrony Twojej prywatności. Niniejsza Polityka Prywatności wyjaśnia, jak postępujemy z Twoimi danymi podczas korzystania z aplikacji.

## Gromadzenie i Przechowywanie Danych

### Co Zbieramy

LLMClient zbiera i przechowuje następujące dane **wyłącznie lokalnie na Twoim urządzeniu**:

1. **Konwersacje**: Wszystkie wiadomości czatu między Tobą a modelami AI
2. **Klucze API**: Twoje osobiste klucze API dla OpenAI, Google Gemini i innych dostawców AI
3. **Wspomnienia Użytkownika**: Informacje osobiste wyodrębnione z konwersacji w celu personalizacji odpowiedzi AI (imię, preferencje, zainteresowania itp.)
4. **Ustawienia Aplikacji**: Twoje preferencje i wybory konfiguracyjne
5. **Pliki Modeli Lokalnych**: Pobrane pliki modeli AI do użytku offline

### Jak Dane są Przechowywane

- **Wszystkie dane są przechowywane lokalnie** na Twoim urządzeniu w zaszyfrowanej bazie SQLite przy użyciu SQLCipher
- **Klucze API** są przechowywane przy użyciu bezpiecznego magazynu platformy (Android Keystore, Windows DPAPI)
- **Żadne dane nie są przesyłane** na nasze serwery - nie prowadzimy żadnych serwerów backendowych
- **Nie zbieramy żadnych analityk ani telemetrii**

## Usługi Stron Trzecich

Gdy używasz LLMClient z chmurowymi dostawcami AI, Twoje wiadomości są wysyłane do:

- **OpenAI** (jeśli skonfigurowane) - Podlega [Polityce Prywatności OpenAI](https://openai.com/privacy)
- **Google Gemini** (jeśli skonfigurowane) - Podlega [Polityce Prywatności Google](https://policies.google.com/privacy)
- **Innych dostawców kompatybilnych z OpenAI** - Podlega ich odpowiednim politykom prywatności

**Ważne**: Podczas korzystania z chmurowych modeli AI, Twoje konwersacje są przetwarzane przez te usługi stron trzecich zgodnie z ich politykami prywatności.

### Modele Lokalne

Podczas korzystania z lokalnych modeli AI (ONNX, LLamaSharp), całe przetwarzanie odbywa się na Twoim urządzeniu. Żadne dane nie są wysyłane do zewnętrznych usług.

## Dane Pod Twoją Kontrolą

Masz pełną kontrolę nad swoimi danymi:

- **Eksport**: Eksportuj konwersacje do plików JSON, Markdown lub TXT
- **Usuwanie**: Usuń pojedyncze konwersacje lub wszystkie dane
- **Zarządzanie Pamięcią**: Przeglądaj, edytuj lub usuwaj zapisane wspomnienia
- **Klucze API**: Usuń klucze API w dowolnym momencie

## Prywatność Dzieci

LLMClient nie jest przeznaczony dla dzieci poniżej 13 roku życia. Nie zbieramy świadomie informacji od dzieci.

## Bezpieczeństwo Danych

Wdrażamy środki bezpieczeństwa obejmujące:

- **Szyfrowanie AES-256** dla lokalnej bazy danych (SQLCipher)
- **Bezpieczne przechowywanie** poufnych danych uwierzytelniających
- **Brak transmisji sieciowej** danych osobowych na nasze serwery

## Zmiany w Polityce

Możemy aktualizować tę Politykę Prywatności. Zmiany będą odzwierciedlone w dacie "Ostatnia aktualizacja".

## Kontakt

W przypadku pytań dotyczących prywatności, proszę utworzyć zgłoszenie w naszym repozytorium GitHub:
https://github.com/DamianTarnowski/LLMClient
