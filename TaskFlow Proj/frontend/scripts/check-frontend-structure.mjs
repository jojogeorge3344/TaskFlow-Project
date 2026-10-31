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
/** Every non-spec .ts file under src/app, with its source. */
const codeFiles = new Map();
const rel = (full) => path.relative(appRoot, full).split(path.sep).join('/');
const fail = (full, why) => problems.push(`${rel(full)}: ${why}`);

/** A <name>.model.ts may sit beside the component, service or directive it was taken from. */
const isSiblingModel = (file, siblings) => {
  const match = /^(.+)\.model\.ts$/.exec(file);
  return !!match && siblings.some((other) => new RegExp(`^${match[1].replace(/[.*+?^${}()|[\]\\]/g, '\\$&')}\\.(component|service|directive)\\.ts$`).test(other));
};

/** A component's template or stylesheet sits beside its .component.ts; a stylesheet without "component" in its name is
 * a shared stylesheet for the components in that folder. */
const isComponentFile = (file, siblings) => {
  if (file.endsWith('.component.ts')) return true;
  const asset = /^(.+)\.component\.(html|css)$/.exec(file);
  if (asset) return siblings.includes(`${asset[1]}.component.ts`);
  return file.endsWith('.css');
};

/** Checks the files of a layer folder (and, for shared components, of their own sub-folders). */
function checkLayerFiles(dir, layer) {
  const entries = fs.readdirSync(dir, { withFileTypes: true });
  const files = entries.filter((entry) => entry.isFile()).map((entry) => entry.name);
  for (const file of files) {
    const full = path.join(dir, file);
    const spec = file.endsWith('.spec.ts');
    const ok = {
      pages: () => isComponentFile(file, files) || spec || isSiblingModel(file, files),
      components: () => isComponentFile(file, files) || spec || isSiblingModel(file, files),
      services: () => file.endsWith('.service.ts') || spec || isSiblingModel(file, files),
      models: () => /\.models?\.ts$/.test(file) || spec,
      utils: () => file.endsWith('.ts') && !/\.(component|service|directive)\.ts$/.test(file),
      directives: () => file.endsWith('.directive.ts') || spec || isSiblingModel(file, files),
      auth: () => file.endsWith('.ts') && !/\.(component|directive)\.ts$/.test(file),
      testing: () => file.endsWith('.ts'),
      // The app shell: its components, the layout service and plain navigation data.
      layouts: () => isComponentFile(file, files) || spec || isSiblingModel(file, files) || file.endsWith('.service.ts')
        || (file.endsWith('.ts') && !/\.(directive|routes)\.ts$/.test(file)),
      routes: () => /\.routes(\.[\w-]+)?\.spec\.ts$/.test(file) || file.endsWith('.routes.ts'),
      config: () => file.endsWith('.ts') && !/\.(component|service|directive|routes)\.ts$/.test(file),
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
    checkLayerFiles(full, entry.name);
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
    const source = fs.readFileSync(full, 'utf8');
    // Read the import declarations themselves, so text in comments or strings cannot hide or fake a use.
    const injectsHttp = /import\s+(?:type\s+)?\{[^}]*\bHttpClient\b[^}]*\}\s*from\s*['"]@angular\/common\/http['"]/.test(source);
    if (injectsHttp && !/(^|\/)services\//.test(where) && !where.startsWith('shared/auth/')) {
      fail(full, 'uses HttpClient outside a services/ folder or shared/auth/');
    }
    codeFiles.set(full, source);
  }
})(appRoot);

// ---------------------------------------------------------------- imports: routed screens and shared code
/** The project files a file imports, statically or with import(). */
function importsOf(file) {
  const targets = [];
  for (const match of codeFiles.get(file).matchAll(/(?:from\s*|import\s*\(\s*)['"](\.{1,2}\/[^'"]+)['"]/g)) {
    const target = `${path.resolve(path.dirname(file), match[1])}.ts`;
    if (codeFiles.has(target)) targets.push(target);
  }
  return targets;
}

// A route's lazily loaded component is a routed screen and belongs in a pages/ folder (the shell is a layout).
for (const [file, source] of codeFiles) {
  if (!file.endsWith('.routes.ts')) continue;
  for (const match of source.matchAll(/loadComponent\s*:\s*\(\)\s*=>\s*import\(\s*['"]([^'"]+)['"]\s*\)/g)) {
    const target = `${path.resolve(path.dirname(file), match[1])}.ts`;
    if (!/(^|\/)pages\//.test(rel(target)) && !rel(target).startsWith('layouts/')) {
      fail(target, `is routed from ${rel(file)} but is not in a pages/ folder`);
    }
  }
}

// Code in shared/ must be used by two or more modules, or by the app itself (root component, shell, config, routes).
// A shared file that imports another passes its users on, so a helper used only through one module's widget is caught.
const importers = new Map();
for (const file of codeFiles.keys()) {
  if (file.endsWith('.spec.ts')) continue;
  for (const target of importsOf(file)) {
    if (!importers.has(target)) importers.set(target, new Set());
    importers.get(target).add(file);
  }
}
const ownersCache = new Map();
function ownersOf(file, visiting = new Set()) {
  if (ownersCache.has(file)) return ownersCache.get(file);
  const owners = new Set();
  visiting.add(file);
  for (const importer of importers.get(file) ?? []) {
    const where = rel(importer);
    const module = /^modules\/([^/]+)\//.exec(where);
    if (module) owners.add(module[1]);
    else if (/^(layouts|config|routes)\//.test(where) || where.startsWith('app.component')) owners.add('(app)');
    else if (where.startsWith('shared/') && !visiting.has(importer)) for (const owner of ownersOf(importer, visiting)) owners.add(owner);
  }
  visiting.delete(file);
  ownersCache.set(file, owners);
  return owners;
}
for (const file of codeFiles.keys()) {
  const where = rel(file);
  if (!where.startsWith('shared/') || where.startsWith('shared/testing/') || file.endsWith('.spec.ts')) continue;
  const owners = ownersOf(file);
  if (owners.size === 0) fail(file, 'is in shared/ but no module uses it');
  else if (owners.size === 1 && !owners.has('(app)')) fail(file, `is in shared/ but only the ${[...owners][0]} module uses it`);
}

if (problems.length === 0) {
  console.log('Frontend structure: OK');
} else {
  console.log(`Frontend structure: ${problems.length} problem(s)`);
  for (const problem of problems) console.log(`  ${problem}`);
  if (!warnOnly) process.exit(1);
}
