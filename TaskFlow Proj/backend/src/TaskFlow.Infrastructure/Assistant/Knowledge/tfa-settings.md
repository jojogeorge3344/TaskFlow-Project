---
title: TFA settings and AI models
roles: Administrator
others: The AI model behind TFA, and its API keys, are managed by an Administrator. Please contact your system administrator.
keywords: tfa settings, ai provider, ai providers, ai model, api key, gemini, openai, ollama, model, server, configure assistant, enable tfa, add model, test connection
link: /settings/tfa
---
Administrators open **Settings > TFA Settings**.

- Choose the **Model** and the **Server** it runs on. The two dropdowns are linked: choosing a model selects its server, and choosing a server selects its model.
- The model and server you choose and save is the only one offered in the TFA window.
- Choose **Add new model…** to add a model name that is not in the list. It is saved and offered from then on.
- For Ollama, enter the server address. For Google Gemini or OpenAI GPT, enter the API key. Keys are stored encrypted and are never shown again after saving. OpenAI has no free tier: it needs a paid key.
- Use **Test connection** to check the choice. Unsaved changes are saved first.
