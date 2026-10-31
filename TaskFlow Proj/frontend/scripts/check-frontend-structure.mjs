// Checks the frontend folder layout (openspec change frontend-module-structure, design D6).
// Usage: node scripts/check-frontend-structure.mjs [--warn]
// Lists every offending path and exits 1, or with --warn lists them and exits 0.
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const frontend = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const appRoot = path.join(frontend, 'src', 'app');
const warnOnly = process.argv.includes('--warn');

/** The module tree, mirroring the backend modules (kebab-case), plus the frontend-only auth and legal modules. */
const MODULES = [
  'auth', 'auth/login', 'auth/account-recovery', 'auth/security-setup',
  'legal',
  'dashboard', 'dashboard/analytics',
  'monitoring', 'monitoring/task-management', 'monitoring/task-management/discussion',
  'monitoring/task-management/activity', 'monitoring/board', 'monitoring/employee-workload',
  'management', 'management/employee-management', 'management/project-management',
  'management/project-management/positions', 'management/department-management',
  'communication', 'communication/mail', 'communication/notification',
  'reports',
  'settings', 'settings/application-settings', 'settings/reminder-settings', 'settings/role-configuration',
  'settings/tfa-settings',
  'profile',
  'assistant',
];
const LAYERS = ['pages', 'components', 'services', 'models', 'utils'];
const TOP_LEVEL = ['modules', 'shared', 'layouts', 'routes', 'config'];
const SHARED = ['components', 'directives', 'services', 'utils', 'auth', 'testing'];

const problems = [];
const rel = (full) => path.relative(appRoot, full).split(path.sep).join('/');
const fail = (full, why) => problems.push(`${rel(full)}: ${why}`);

/** A <name>.model.ts may sit beside the component, service or directive it was taken from. */
const isSiblingModel = (file, siblings) => {
  const match = /^(.+)\.model\.ts$/.exec(file);
  return !!match && siblings.some((other) => new RegExp(`^${match[1].replace(/[.*+?^${}()|[\]\\]/g, '\\$&')}\\.(component|service|directive)\\.ts$`).test(other));
};

/** Checks the files of a layer folder (and, for shared components, of their own sub-folders). */
function checkLayerFiles(dir, layer) {
  const entries = fs.readdirSync(dir, { withFileTypes: true });
  const files = entries.filter((entry) => entry.isFile()).map((entry) => entry.name);
  for (const file of files) {
    const full = path.join(dir, file);
    const spec = file.endsWith('.spec.ts');
    const ok = {
      pages: () => /\.component\.(ts|html|css)$/.test(file) || spec || file.endsWith('.css') || isSiblingModel(file, files),
      components: () => /\.component\.(ts|html|css)$/.test(file) || spec || file.endsWith('.css') || isSiblingModel(file, files),
      services: () => file.endsWith('.service.ts') || spec || isSiblingModel(file, files),
      models: () => /\.models?\.ts$/.test(file) || spec,
      utils: () => file.endsWith('.ts'),
      directives: () => file.endsWith('.directive.ts') || spec || isSiblingModel(file, files),
      auth: () => file.endsWith('.ts'),
      testing: () => file.endsWith('.ts'),
    }[layer]();
    if (!ok) fail(full, `does not belong in a ${layer}/ folder`);
  }
  for (const entry of entries.filter((item) => item.isDirectory())) {
    if (layer === 'components') checkLayerFiles(path.join(dir, entry.name), 'components');
    else fail(path.join(dir, entry.name), `unexpected folder inside ${layer}/`);
  }
}

function checkModule(dir, modulePath) {
  if (!MODULES.includes(modulePath)) {
    fail(dir, 'is not a module or sub-module of the backend tree');
    return;
  }
  const topLevel = !modulePath.includes('/');
  for (const entry of fs.readdirSync(dir, { withFileTypes: true })) {
    const full = path.join(dir, entry.name);
    if (entry.isFile()) {
      const routes = topLevel && (entry.name === `${modulePath}.routes.ts` || entry.name === `${modulePath}.routes.spec.ts`);
      if (!routes) fail(full, `only ${topLevel ? `${modulePath}.routes.ts` : 'layer folders'} may sit directly in a module folder`);
    } else if (LAYERS.includes(entry.name)) {
      checkLayerFiles(full, entry.name);
    } else {
      checkModule(full, `${modulePath}/${entry.name}`);
    }
  }
}

// ---------------------------------------------------------------- top level
for (const entry of fs.readdirSync(appRoot, { withFileTypes: true })) {
  const full = path.join(appRoot, entry.name);
  if (entry.isFile()) {
    if (!/^app\.component\.(ts|html|css|spec\.ts)$/.test(entry.name)) fail(full, 'only the root app.component files may sit in src/app');
    continue;
  }
  if (!TOP_LEVEL.includes(entry.name)) {
    fail(full, entry.name === 'core' || entry.name === 'features' ? 'legacy folder must not exist' : 'unknown top-level folder');
    continue;
  }
  if (entry.name === 'modules') {
    for (const module of fs.readdirSync(full, { withFileTypes: true })) {
      if (module.isDirectory()) checkModule(path.join(full, module.name), module.name);
      else fail(path.join(full, module.name), 'files may not sit directly in modules/');
    }
  } else if (entry.name === 'shared') {
    for (const part of fs.readdirSync(full, { withFileTypes: true })) {
      if (part.isDirectory() && SHARED.includes(part.name)) checkLayerFiles(path.join(full, part.name), part.name);
      else fail(path.join(full, part.name), `shared/ holds only ${SHARED.join(', ')}`);
    }
  } else {
    for (const sub of fs.readdirSync(full, { withFileTypes: true })) {
      if (sub.isDirectory()) fail(path.join(full, sub.name), `${entry.name}/ has no sub-folders`);
    }
  }
}

// ---------------------------------------------------------------- whole tree
(function walk(dir) {
  const entries = fs.readdirSync(dir, { withFileTypes: true });
  if (entries.length === 0) fail(dir, 'empty folder');
  for (const entry of entries) {
    const full = path.join(dir, entry.name);
    if (entry.isDirectory()) {
      walk(full);
      continue;
    }
    if (!entry.name.endsWith('.ts') || entry.name.endsWith('.spec.ts')) continue;
    const where = rel(full);
    const injectsHttp = /\bHttpClient\b/.test(fs.readFileSync(full, 'utf8').replace(/\/\/[^\n]*|\/\*[\s\S]*?\*\//g, ''));
    if (injectsHttp && !/(^|\/)services\//.test(where) && !where.startsWith('shared/auth/')) {
      fail(full, 'uses HttpClient outside a services/ folder or shared/auth/');
    }
  }
})(appRoot);

if (problems.length === 0) {
  console.log('Frontend structure: OK');
} else {
  console.log(`Frontend structure: ${problems.length} problem(s)`);
  for (const problem of problems) console.log(`  ${problem}`);
  if (!warnOnly) process.exit(1);
}
