// SINGLE PLACE for every environment's URLs. Change ONLY the ACTIVE line below.
// All API calls in the app (/api/v1/...) get the active environment's apiBaseUrl prefixed automatically,
// so individual services never contain a URL.

const ENVIRONMENTS = {
  // Local run: '' = relative /api calls, forwarded by proxy.conf.json to the local backend.
  local: {
    apiBaseUrl: ''
  },
  develop: {
    apiBaseUrl: 'https://taskflow-project-1-z76p.onrender.com'
  },
  qa: {
    apiBaseUrl: 'https://taskflow-qa-api.example.com'
  },
  production: {
    apiBaseUrl: 'https://taskflow-prod-api.example.com'
  }
} as const;

// >>> Switch environment here: 'local' | 'develop' | 'qa' | 'production'
//const ACTIVE: keyof typeof ENVIRONMENTS = 'local';
const ACTIVE: keyof typeof ENVIRONMENTS = 'develop';
// const ACTIVE: keyof typeof ENVIRONMENTS = 'qa';
//const ACTIVE: keyof typeof ENVIRONMENTS = 'production';


export const API_BASE_URL: string = ENVIRONMENTS[ACTIVE].apiBaseUrl;
