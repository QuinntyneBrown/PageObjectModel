'use strict';

// tsconfig path-alias resolution for following loadChildren/loadComponent
// dynamic import specifiers. Reads tsconfig.json / tsconfig.base.json (JSONC
// via ts.readConfigFile), merges the extends chain (depth <= 5), and exposes a
// longest-prefix wildcard matcher.

const fs = require('fs');
const path = require('path');
const ts = require('typescript');

/**
 * Builds a resolver for the workspace root.
 * @returns {{ resolve: (specifier: string) => string[] }} candidate absolute paths (no extension probing).
 */
function createPathResolver(root) {
  const config = readMergedConfig(root);
  const baseUrl = config.baseUrl || root; // already absolute (resolved against the declaring config)
  const patterns = [];
  for (const [alias, targets] of Object.entries(config.paths || {})) {
    if (!Array.isArray(targets)) {
      continue;
    }
    const starIndex = alias.indexOf('*');
    patterns.push({
      alias,
      prefix: starIndex >= 0 ? alias.slice(0, starIndex) : alias,
      suffix: starIndex >= 0 ? alias.slice(starIndex + 1) : '',
      hasStar: starIndex >= 0,
      targets,
    });
  }
  // Longest prefix wins.
  patterns.sort((a, b) => b.prefix.length - a.prefix.length);

  return {
    resolve(specifier) {
      const results = [];
      for (const pattern of patterns) {
        if (pattern.hasStar) {
          if (specifier.startsWith(pattern.prefix) && specifier.endsWith(pattern.suffix)
            && specifier.length >= pattern.prefix.length + pattern.suffix.length) {
            const captured = specifier.slice(pattern.prefix.length, specifier.length - pattern.suffix.length || undefined);
            for (const target of pattern.targets) {
              results.push(path.resolve(baseUrl, target.replace('*', captured)));
            }
            break;
          }
        } else if (specifier === pattern.alias) {
          for (const target of pattern.targets) {
            results.push(path.resolve(baseUrl, target));
          }
          break;
        }
      }
      return results;
    },
  };
}

function readMergedConfig(root) {
  for (const name of ['tsconfig.base.json', 'tsconfig.json']) {
    const file = path.join(root, name);
    if (fs.existsSync(file)) {
      return mergeChain(file, 0);
    }
  }
  return {};
}

function mergeChain(configPath, depth) {
  if (depth > 5 || !fs.existsSync(configPath)) {
    return {};
  }
  const read = ts.readConfigFile(configPath, (p) => fs.readFileSync(p, 'utf8'));
  const config = read.config || {};
  const options = config.compilerOptions || {};

  let merged = {};
  if (typeof config.extends === 'string') {
    let parent = config.extends;
    if (parent.startsWith('.')) {
      parent = path.resolve(path.dirname(configPath), parent);
      if (!parent.endsWith('.json')) {
        parent += '.json';
      }
      merged = mergeChain(parent, depth + 1);
    }
    // Non-relative extends (npm packages) are skipped — alias bases from
    // published configs are rare and best-effort only.
  }

  return {
    // baseUrl is resolved against the directory of the config that declares it.
    baseUrl: options.baseUrl !== undefined
      ? path.resolve(path.dirname(configPath), options.baseUrl)
      : merged.baseUrl,
    paths: options.paths !== undefined ? options.paths : merged.paths,
  };
}

module.exports = { createPathResolver };
