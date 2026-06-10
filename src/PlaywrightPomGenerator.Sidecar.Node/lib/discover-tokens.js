'use strict';

// InjectionToken discovery for the `bridge` command. Moved verbatim from the
// v1 single-file sidecar — the response shape is a frozen contract with the
// .NET host (TypeScriptAnalyzer).

const fs = require('fs');
const ts = require('typescript');
const { collectTsFiles, collapse, lastTypeSegment, stripGenerics } = require('./util');

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

module.exports = { discoverInjectionTokens };
