# Proposal

## Why

TFA depends on Ollama for both chat and document search: a fresh install enables Ollama for chat, and
the document index (`tfa-document-rag`) embeds every document with Ollama's `nomic-embed-text`. That
means every machine must install Ollama and pull two models, which is heavy on student laptops and slow
on CPU. Google Gemini offers a free tier through Google AI Studio (free API key, rate-limited Flash
models and a free embedding model), and TaskFlow already has a working `GeminiProvider`. Making Gemini
the default for chat and the only engine for document embeddings removes the Ollama dependency while
keeping TFA free to run.

## What Changes

- **Gemini is the default chat provider.** On first run, when a Gemini API key is supplied through
  configuration (`AiProviders:GeminiApiKey`, from .NET user-secrets or an environment variable), the
  seeder stores it encrypted in the database exactly as TFA Settings would, and enables Google Gemini
  with its default model. Ollama is no longer enabled on a fresh install.
- **Without a configured key**, a fresh install enables no provider; Google Gemini is listed first and
  TFA Settings explains where to get a free key. The key is never committed, logged or seeded from a
  file in the repository.
- **Configured key never overwrites an administrator's choice.** The configuration key is used only
  when the Gemini row has no saved key; after that, the key and the enabled provider are managed only in
  TFA Settings.
- **Document embeddings use Google Gemini** (`gemini-embedding-2` through `batchEmbedContents`, with
  the document and query task prefixes) and the Gemini API key saved in TFA Settings, whichever chat
  provider is selected. **BREAKING**: Ollama embeddings are removed, together with
  `DocumentKnowledge:OllamaBaseUrl`; the index is rebuilt with the Gemini model on the first start.
- **Document search needs a Gemini key.** With no Gemini key saved, documents are not indexed or
  searched; TFA still answers live-data questions and says the documents cannot be searched right now,
  and the server log names the missing key.
- **Privacy notice updated.** Document text is now sent to Google when it is indexed, and every message
  is sent to Google to search the documents, even when Ollama is the chat provider. The cloud notice and
  TFA Settings help text say so.
- **Ollama stays an optional chat provider**: still built in, seeded off, configurable in TFA Settings.
- `MinScore` default is re-tuned for Gemini embeddings; README and `scripts/setup-ai.*` describe the
  Gemini key as the default setup, with Ollama as optional.

## Capabilities

### New Capabilities

- None.

### Modified Capabilities

- `ai-provider-management`: fresh-install provider state changes (Gemini default when a key is
  configured, nothing enabled otherwise, Ollama no longer auto-enabled); a configured key is seeded
  once, encrypted, and never overrides saved settings.
- `tfa-document-knowledge`: documents are embedded with Google Gemini instead of a local Ollama model;
  document search requires a saved Gemini key; failure behaviour and the cloud notice cover document
  and message text sent to Google for embedding.

## Impact

- **Backend**: `AiProviderSeeder`, `AiProviderCatalog`, `AiProviderOptions`, new
  `GeminiEmbeddingClient` replacing `OllamaEmbeddingClient`, `EmbeddingTypes` (failure messages),
  `DocumentEmbeddingEndpoint` (resolves the Gemini key instead of an Ollama URL),
  `DocumentKnowledgeOptions` (embedding model default, `MinScore`, `OllamaBaseUrl` removed),
  `IntegrationRegistration`, `appsettings.json`; backend tests for the seeder, embedding client and
  indexer.
- **Frontend**: cloud notice and TFA Settings help text.
- **Data**: no schema migration; existing chunks embedded with `nomic-embed-text` are replaced on the
  next index run because the stored embedding model differs.
- **External**: Google Gemini API (free tier) becomes required for document search; free-tier rate
  limits apply and Google may use free-tier content to improve its products.
- **Docs**: README, `scripts/setup-ai.ps1` / `.sh`, TFA Settings and Assistant documents in
  `TF - Documents`.
- **OpenSpec**: modifies requirements added by the unarchived `tfa-assistant` and `tfa-document-rag`
  changes, so it should be archived after them.
