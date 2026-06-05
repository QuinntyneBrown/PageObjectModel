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
//
// TypeScript is resolved from (1) this directory's node_modules, then (2) NODE_PATH
// (set by the host to the target workspace's node_modules). Either is fine.
// ---------------------------------------------------------------------------

const fs = require('fs');
const path = require('path');
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

const rl = readline.createInterface({ input: process.stdin });

rl.on('line', (line) => {
  let msg;
  try {
    msg = JSON.parse(line);
  } catch (e) {
    return;
  }
  if (!msg || typeof msg !== 'object') {
    return;
  }
  try {
    if (msg.method === 'ping') {
      respond(msg.id, 'pong');
    } else if (msg.method === 'discoverInjectionTokens') {
      respond(msg.id, discoverInjectionTokens(msg.params && msg.params.root));
    } else {
      respondError(msg.id, 'unknown method: ' + msg.method);
    }
  } catch (e) {
    respondError(msg.id, (e && e.message) || String(e));
  }
});

rl.on('close', () => process.exit(0));

function respond(id, result) {
  process.stdout.write(JSON.stringify({ jsonrpc: '2.0', id: id, result: result }) + '\n');
}

function respondError(id, message) {
  process.stdout.write(JSON.stringify({ jsonrpc: '2.0', id: id, error: { message: message } }) + '\n');
}

// ---------------------------------------------------------------------------

function discoverInjectionTokens(root) {
  const warnings = [];
  if (!root || !fs.existsSync(root)) {
    return { tokens: [], warnings: ['path not found: ' + root] };
  }

  const files = [];
  collectTsFiles(root, files);

  /** @type {Map<string, { members: any[], extendsNames: string[], file: string }>} */
  const interfaces = new Map();
  /** @type {{ tokenName: string, interfaceName: string, description: string|null, file: string }[]} */
  const tokens = [];

  for (const file of files) {
    let content;
    try {
      content = fs.readFileSync(file, 'utf8');
    } catch (e) {
      continue;
    }
    const sourceFile = ts.createSourceFile(file, content, ts.ScriptTarget.Latest, true);
    const diagnostics = sourceFile.parseDiagnostics || [];
    if (diagnostics.length > 0) {
      warnings.push('skipped ' + file + ': ' + ts.flattenDiagnosticMessageText(diagnostics[0].messageText, ' '));
      continue;
    }

    ts.forEachChild(sourceFile, (node) => {
      if (ts.isInterfaceDeclaration(node)) {
        if (!interfaces.has(node.name.text)) {
          interfaces.set(node.name.text, {
            members: node.members.map((m) => describeMember(m, sourceFile)).filter(Boolean),
            extendsNames: heritageNames(node, ts.SyntaxKind.ExtendsKeyword, sourceFile),
            file: file,
          });
        }
      } else if (ts.isVariableStatement(node)) {
        for (const decl of node.declarationList.declarations) {
          const token = tryInjectionToken(decl, sourceFile, file);
          if (token) {
            tokens.push(token);
          }
        }
      }
    });
  }

  const result = [];
  const seen = new Set();
  for (const token of tokens) {
    const key = token.tokenName + '::' + token.interfaceName;
    if (seen.has(key)) {
      continue;
    }
    seen.add(key);

    const resolved = resolveInterface(token.interfaceName, interfaces, new Set());
    if (!resolved) {
      if (/^[A-Z]/.test(token.interfaceName)) {
        warnings.push(
          'interface ' + token.interfaceName + ' (token ' + token.tokenName + ') was not found in the scanned files',
        );
      }
      continue;
    }

    result.push({
      tokenName: token.tokenName,
      interfaceName: token.interfaceName,
      description: token.description,
      tokenFile: token.file,
      interfaceFile: resolved.file,
      members: resolved.members,
    });
  }

  return { tokens: result, warnings: warnings };
}

function resolveInterface(name, interfaces, visiting) {
  const entry = interfaces.get(name);
  if (!entry) {
    return null;
  }
  if (visiting.has(name)) {
    return { file: entry.file, members: entry.members };
  }
  visiting.add(name);

  const byName = new Map();
  for (const member of entry.members) {
    byName.set(member.name, member);
  }
  for (const parent of entry.extendsNames) {
    const parentName = lastTypeSegment(stripGenerics(parent));
    const parentResolved = resolveInterface(parentName, interfaces, visiting);
    if (parentResolved) {
      for (const member of parentResolved.members) {
        if (!byName.has(member.name)) {
          byName.set(member.name, member); // own members win over inherited
        }
      }
    }
  }

  visiting.delete(name);
  return { file: entry.file, members: Array.from(byName.values()) };
}

function tryInjectionToken(decl, sourceFile, file) {
  if (!ts.isIdentifier(decl.name)) {
    return null;
  }
  const init = decl.initializer;
  if (!init || !ts.isNewExpression(init)) {
    return null;
  }
  if (!/(^|\.)InjectionToken$/.test(init.expression.getText(sourceFile))) {
    return null;
  }
  const typeArg =
    init.typeArguments && init.typeArguments.length > 0 ? collapse(init.typeArguments[0].getText(sourceFile)) : '';
  const interfaceName = lastTypeSegment(stripGenerics(typeArg));
  let description = null;
  if (init.arguments && init.arguments.length > 0 && ts.isStringLiteralLike(init.arguments[0])) {
    description = collapse(init.arguments[0].text);
  }
  return { tokenName: decl.name.text, interfaceName: interfaceName, description: description, file: file };
}

function describeMember(member, sourceFile) {
  if (ts.isMethodSignature(member) && member.name) {
    const returnType = member.type ? collapse(member.type.getText(sourceFile)) : 'void';
    const params = member.parameters.map((p) => ({
      name: p.name.getText(sourceFile),
      type: p.type ? collapse(p.type.getText(sourceFile)) : 'unknown',
    }));
    return {
      name: member.name.getText(sourceFile),
      isMethod: true,
      parameterNames: params.map((p) => p.name),
      parametersText: params.map((p) => p.name + ': ' + p.type).join(', '),
      returnType: returnType,
      isObservable: /^Observable\b/.test(returnType),
      returnsVoid: returnType === 'void',
    };
  }
  if (ts.isPropertySignature(member) && member.name) {
    const type = member.type ? collapse(member.type.getText(sourceFile)) : 'unknown';
    return {
      name: member.name.getText(sourceFile),
      isMethod: false,
      parameterNames: [],
      parametersText: '',
      returnType: type,
      isObservable: /^Observable\b/.test(type),
      returnsVoid: false,
    };
  }
  return null;
}

function heritageNames(node, keyword, sourceFile) {
  const names = [];
  if (!node.heritageClauses) {
    return names;
  }
  for (const clause of node.heritageClauses) {
    if (clause.token !== keyword) {
      continue;
    }
    for (const type of clause.types) {
      names.push(collapse(type.getText(sourceFile)));
    }
  }
  return names;
}

function collectTsFiles(dir, out) {
  let entries;
  try {
    entries = fs.readdirSync(dir, { withFileTypes: true });
  } catch (e) {
    return;
  }
  for (const entry of entries) {
    const full = path.join(dir, entry.name);
    if (entry.isDirectory()) {
      if (entry.name === 'node_modules' || entry.name === 'dist' || entry.name.startsWith('.')) {
        continue;
      }
      collectTsFiles(full, out);
    } else if (entry.isFile()) {
      const n = entry.name;
      if (n.endsWith('.ts') && !n.endsWith('.spec.ts') && !n.endsWith('.d.ts')) {
        out.push(full);
      }
    }
  }
}

function lastTypeSegment(name) {
  const idx = name.lastIndexOf('.');
  return idx >= 0 ? name.slice(idx + 1) : name;
}

function stripGenerics(name) {
  const idx = name.indexOf('<');
  return idx >= 0 ? name.slice(0, idx).trim() : name.trim();
}

function collapse(text) {
  return String(text).replace(/\s+/g, ' ').trim();
}
