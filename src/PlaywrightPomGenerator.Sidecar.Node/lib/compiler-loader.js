'use strict';

// Locates and loads @angular/compiler from the ANALYZED APP's node_modules so the
// compiler version always matches the app's template syntax. Falls back to the
// sidecar's own devDependency (dev/test only — never shipped in the nupkg).
//
// Angular <= 12 shipped UMD (plain require works). Angular 13+ ships ESM-only
// FESM bundles: require() throws ERR_REQUIRE_ESM on older Node, so we resolve
// the package entry point manually and use a dynamic import(). Node >= 20.19
// can require() ESM directly, which the first attempt covers.

const fs = require('fs');
const path = require('path');
const { pathToFileURL } = require('url');

/**
 * Loads @angular/compiler for the given workspace root.
 * @returns {Promise<{ compiler: object, version: string|null, source: string } | null>}
 */
async function loadCompiler(root) {
  for (const pkgDir of candidateDirs(root)) {
    const loaded = await tryLoad(pkgDir);
    if (loaded) {
      return loaded;
    }
  }
  return null;
}

function candidateDirs(root) {
  const dirs = [];
  if (root) {
    // The app's own node_modules, then hoisted monorepo roots (max 4 levels up).
    let current = path.resolve(root);
    for (let i = 0; i <= 4 && current; i++) {
      dirs.push(path.join(current, 'node_modules', '@angular', 'compiler'));
      const parent = path.dirname(current);
      if (parent === current) {
        break;
      }
      current = parent;
    }
  }
  // Sidecar devDependency (dev/test scenarios).
  try {
    const local = require.resolve('@angular/compiler/package.json', { paths: [path.join(__dirname, '..')] });
    dirs.push(path.dirname(local));
  } catch (e) {
    // Not installed locally — fine.
  }
  const unique = [];
  const seen = new Set();
  for (const dir of dirs) {
    let real = dir;
    try {
      real = fs.realpathSync(dir); // pnpm symlinks
    } catch (e) {
      continue; // does not exist
    }
    if (!seen.has(real)) {
      seen.add(real);
      unique.push(real);
    }
  }
  return unique;
}

async function tryLoad(pkgDir) {
  const pkgJsonPath = path.join(pkgDir, 'package.json');
  let pkg;
  try {
    pkg = JSON.parse(fs.readFileSync(pkgJsonPath, 'utf8'));
  } catch (e) {
    return null;
  }
  const version = typeof pkg.version === 'string' ? pkg.version : null;

  // 1) Plain require — UMD-era Angular, or Node new enough to require() ESM.
  try {
    const compiler = require(pkgDir);
    if (isCompiler(compiler)) {
      return { compiler, version, source: pkgDir };
    }
  } catch (e) {
    // Fall through to the ESM path.
  }

  // 2) Resolve the ESM entry from package.json and import it dynamically.
  const entry = resolveEsmEntry(pkg);
  if (!entry) {
    return null;
  }
  try {
    const mod = await import(pathToFileURL(path.join(pkgDir, entry)).href);
    const compiler = mod && isCompiler(mod) ? mod : mod && mod.default && isCompiler(mod.default) ? mod.default : null;
    if (compiler) {
      return { compiler, version, source: pkgDir };
    }
  } catch (e) {
    return null;
  }
  return null;
}

function resolveEsmEntry(pkg) {
  const dot = pkg.exports && (pkg.exports['.'] || pkg.exports);
  if (typeof dot === 'string') {
    return dot;
  }
  if (dot && typeof dot === 'object') {
    for (const key of ['module', 'import', 'es2022', 'esm2022', 'es2020', 'esm2020', 'default']) {
      const value = dot[key];
      if (typeof value === 'string') {
        return value;
      }
      if (value && typeof value === 'object' && typeof value.default === 'string') {
        return value.default;
      }
    }
  }
  return pkg.module || pkg.main || null;
}

function isCompiler(mod) {
  return !!mod && typeof mod.parseTemplate === 'function';
}

module.exports = { loadCompiler };
