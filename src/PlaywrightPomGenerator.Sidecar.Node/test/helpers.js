'use strict';

// Shared fixture helpers for the sidecar node:test suites.

const fs = require('fs');
const os = require('os');
const path = require('path');
const ts = require('typescript');

/**
 * Writes a fixture tree into a fresh temp directory.
 * @param {Record<string, string>} files relative path -> content
 * @returns {{ root: string, cleanup: () => void }}
 */
function makeFixture(files) {
  const root = fs.mkdtempSync(path.join(os.tmpdir(), 'ppg-sidecar-test-'));
  for (const [relative, content] of Object.entries(files)) {
    const full = path.join(root, relative);
    fs.mkdirSync(path.dirname(full), { recursive: true });
    fs.writeFileSync(full, content, 'utf8');
  }
  return {
    root,
    cleanup() {
      try {
        fs.rmSync(root, { recursive: true, force: true });
      } catch (e) {
        // best effort
      }
    },
  };
}

function parse(content, fileName) {
  return ts.createSourceFile(fileName || 'fixture.ts', content, ts.ScriptTarget.Latest, true);
}

/** Loads @angular/compiler from the sidecar's own devDependency. */
let compilerPromise = null;
function getCompiler() {
  if (!compilerPromise) {
    const { loadCompiler } = require('../lib/compiler-loader');
    // A root with no node_modules forces the loader onto the sidecar devDependency fallback.
    compilerPromise = loadCompiler(os.tmpdir()).then((loaded) => {
      if (!loaded) {
        throw new Error('test setup: @angular/compiler not installed — run npm install in the sidecar directory');
      }
      return loaded.compiler;
    });
  }
  return compilerPromise;
}

module.exports = { makeFixture, parse, getCompiler };
