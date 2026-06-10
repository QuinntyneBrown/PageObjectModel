#!/usr/bin/env node
'use strict';

// ---------------------------------------------------------------------------
// Playwright POM generator — TypeScript AST sidecar.
//
// Speaks newline-delimited JSON-RPC 2.0 over stdin/stdout. The .NET host spawns
// this process, sends a request, and reads a single response line.
//
// Methods:
//   ping                      -> "pong"
//   discoverInjectionTokens   -> { tokens: [...], warnings: [...] }
//     params: { root: "<directory>" }
//   analyzeProject            -> { schemaVersion, engine, projects: [...], warnings: [...] }
//     params: { root, projects: [{ name, sourceRoot, prefix }], options: { includeTemplateContent } }
//
// TypeScript is resolved from (1) this directory's node_modules, then (2) NODE_PATH
// (set by the host to the target workspace's node_modules). Either is fine.
// @angular/compiler (template analysis only) is resolved from the analyzed app —
// see lib/compiler-loader.js. Absence degrades template analysis, never crashes.
//
// This file must stay CommonJS: NODE_PATH (the production resolution mechanism)
// is honored by require() but ignored by ESM import resolution.
// ---------------------------------------------------------------------------

const readline = require('readline');

let ts;
try {
  ts = require('typescript');
} catch (e) {
  process.stderr.write(
    'ppg-sidecar: could not resolve the "typescript" package. Install it in the sidecar ' +
      'directory, or run against an Angular workspace that has typescript in node_modules.\n',
  );
  process.exit(2);
}

if (Number(process.versions.node.split('.')[0]) < 18) {
  process.stderr.write('ppg-sidecar: Node.js ' + process.versions.node + ' detected; Node 18+ is recommended.\n');
}

const { discoverInjectionTokens } = require('./lib/discover-tokens');
const { analyzeProject } = require('./lib/analyze-project');

const rl = readline.createInterface({ input: process.stdin });
let pending = 0;

rl.on('line', (line) => {
  let msg;
  try {
    msg = JSON.parse(line);
  } catch (e) {
    return;
  }
  if (!msg || typeof msg !== 'object' || !msg.method) {
    return;
  }
  pending += 1;
  Promise.resolve()
    .then(() => dispatch(msg))
    .then(
      (result) => respond(msg.id, result),
      (e) => respondError(msg.id, (e && e.message) || String(e)),
    )
    .finally(() => {
      pending -= 1;
    });
});

// Handlers may be async, so never process.exit() on stdin close — with stdin
// closed and no work pending, the event loop drains naturally after stdout
// flushes. process.exit() here would truncate large buffered responses.
rl.on('close', () => {});

function dispatch(msg) {
  switch (msg.method) {
    case 'ping':
      return 'pong';
    case 'discoverInjectionTokens':
      return discoverInjectionTokens(msg.params && msg.params.root);
    case 'analyzeProject':
      return analyzeProject(msg.params || {});
    default:
      throw new Error('unknown method: ' + msg.method);
  }
}

function respond(id, result) {
  process.stdout.write(JSON.stringify({ jsonrpc: '2.0', id: id, result: result }) + '\n');
}

function respondError(id, message) {
  process.stdout.write(JSON.stringify({ jsonrpc: '2.0', id: id, error: { message: message } }) + '\n');
}
