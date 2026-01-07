# Content Rating Questionnaire Information

This document contains answers for the content rating questionnaires required by Google Play Store and Microsoft Store.

## App Description

**LLMClient** is an AI chat client application that allows users to interact with Large Language Models (LLMs) like GPT, Gemini, and local models. The app stores all data locally with encryption for privacy.

---

## Google Play Store - Content Rating (IARC)

### Violence
- **Does your app contain violence?** No
- **Does your app depict violence against specific groups?** No
- **Does your app contain graphic violence?** No

### Sexual Content
- **Does your app contain sexual content or nudity?** No
- **Does your app contain pornographic content?** No

### Language
- **Does your app contain profanity or crude humor?** No
- The app processes user-generated content via AI models. The AI responses may contain language based on user input, but the app itself does not generate inappropriate content.

### Controlled Substances
- **Does your app reference illegal drugs?** No
- **Does your app reference tobacco or alcohol?** No

### Gambling
- **Does your app contain gambling?** No
- **Does your app contain simulated gambling?** No

### User-Generated Content / User Interaction
- **Does your app allow users to interact with each other?** No
- **Does your app contain user-generated content?** Yes (user's own conversations)
  - Note: All content is private to the user's device. No sharing or social features.

### Personal Information
- **Does your app collect personal information?** Yes, locally only
  - Information collected: User conversations, AI memory data, API keys
  - Storage: Local encrypted database only
  - Sharing: None - all data stays on device

### Location
- **Does your app access location?** No

### Ads
- **Does your app contain ads?** No

### In-App Purchases
- **Does your app contain in-app purchases?** No

### Data Safety
- **Data collected**: User conversations, AI memories, API keys
- **Data shared with third parties**: Only when user actively sends messages to cloud AI providers (OpenAI, Google)
- **Data encrypted**: Yes (SQLCipher AES-256)
- **Data deletion**: User can delete all data from within the app

### Recommended Rating: **Everyone (E)** or **PEGI 3**

The app is a productivity/utility tool with no objectionable content.

---

## Microsoft Store - Age Rating

### Content Type
- **App Type**: Productivity / Utilities
- **Target Audience**: General audience, developers, professionals

### Content Descriptors
- [ ] Violence - No
- [ ] Fear - No
- [ ] Sexual Content - No
- [ ] Nudity - No
- [ ] Offensive Language - No
- [ ] Controlled Substances - No
- [ ] Gambling - No
- [ ] User Interaction - No (single-user app)
- [ ] User-Generated Content - Yes (private only)
- [ ] Online Connectivity - Yes (for cloud AI APIs)

### Privacy
- **Privacy Policy URL**: https://github.com/DamianTarnowski/LLMClient/blob/main/docs/PRIVACY_POLICY.md
- **Data Collection**: Local only
- **Third-party Services**: OpenAI API, Google Gemini API (user-configured)

### Recommended Rating: **3+** (suitable for all ages)

---

## Key Points for Reviewers

1. **No Backend Servers**: LLMClient does not operate any backend servers. All user data is stored locally.

2. **AI Content**: The app interfaces with AI models that generate text based on user input. The app itself does not generate content - it only displays responses from configured AI providers.

3. **Privacy-First**: All conversations and personal data are encrypted locally using industry-standard encryption (AES-256 via SQLCipher).

4. **API Keys**: Users must provide their own API keys for cloud AI services. The app securely stores these using platform-provided secure storage.

5. **Local Models**: Users can download and run AI models locally for complete offline and private operation.

6. **No Monetization**: The app is free, contains no ads, and has no in-app purchases.

7. **Open Source**: The app is open source under MIT license, allowing full transparency of its code and functionality.

---

## Contact for Store Reviews

- **Developer**: Damian Tarnowski
- **GitHub**: https://github.com/DamianTarnowski/LLMClient
- **Privacy Policy**: https://github.com/DamianTarnowski/LLMClient/blob/main/docs/PRIVACY_POLICY.md
