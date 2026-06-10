'use strict';

// Component metadata extraction over the TypeScript AST: @Component decorator
// contents, decorator and signal-based inputs/outputs, view queries, and
// best-effort MatDialog.open() linkage. Raw facts only — naming and selector
// strategy decisions live in the .NET host.

const fs = require('fs');
const path = require('path');
const ts = require('typescript');
const { getDecorators } = require('./ts-compat');
const { collapse } = require('./util');

/**
 * Scans a parsed source file for @Component classes.
 * @returns {Array<object>} component records (template analysis attached later).
 */
function scanSourceFile(sourceFile, filePath, root, warnings) {
  const components = [];
  ts.forEachChild(sourceFile, (node) => {
    if (!ts.isClassDeclaration(node) || !node.name) {
      return;
    }
    const decorator = findComponentDecorator(node, sourceFile);
    if (!decorator) {
      return;
    }
    const component = describeComponent(node, decorator, sourceFile, filePath, root, warnings);
    if (component) {
      components.push(component);
    }
  });
  return components;
}

function findComponentDecorator(node, sourceFile) {
  for (const decorator of getDecorators(ts, node)) {
    const expr = decorator.expression;
    if (ts.isCallExpression(expr) && /(^|\.)Component$/.test(expr.expression.getText(sourceFile))) {
      return expr;
    }
  }
  return null;
}

function describeComponent(node, decoratorCall, sourceFile, filePath, root, warnings) {
  const meta = decoratorCall.arguments && decoratorCall.arguments[0];
  if (!meta || !ts.isObjectLiteralExpression(meta)) {
    return null;
  }

  const className = node.name.text;
  let selector = null;
  let standalone = null;
  let templateUrl = null;
  let inlineTemplate = null;
  let inlineTemplateUnparseable = false;
  const importsIdentifiers = [];

  for (const prop of meta.properties) {
    if (!ts.isPropertyAssignment(prop) || !prop.name) {
      continue;
    }
    const name = prop.name.getText(sourceFile);
    const value = prop.initializer;
    if (name === 'selector' && ts.isStringLiteralLike(value)) {
      selector = value.text;
    } else if (name === 'standalone') {
      if (value.kind === ts.SyntaxKind.TrueKeyword) {
        standalone = true;
      } else if (value.kind === ts.SyntaxKind.FalseKeyword) {
        standalone = false;
      }
    } else if (name === 'templateUrl' && ts.isStringLiteralLike(value)) {
      templateUrl = resolveTemplateUrl(value.text, filePath, root);
    } else if (name === 'template') {
      if (ts.isNoSubstitutionTemplateLiteral(value) || ts.isStringLiteralLike(value)) {
        inlineTemplate = value.text;
      } else if (ts.isTemplateExpression(value)) {
        inlineTemplateUnparseable = true;
        warnings.push(filePath + ': inline template of ' + className + ' uses ${...} substitution — template analysis skipped');
      }
    } else if (name === 'imports' && ts.isArrayLiteralExpression(value)) {
      for (const element of value.elements) {
        if (ts.isIdentifier(element)) {
          importsIdentifiers.push(element.text);
        }
      }
    }
  }

  const selectors = selector
    ? selector.split(',').map((s) => s.trim()).filter((s) => s.length > 0)
    : [];

  const ports = describePorts(node, sourceFile);
  const dialogOpens = describeDialogOpens(node, sourceFile);

  return {
    className,
    filePath,
    selector: selectors.length > 0 ? selectors[0] : null,
    selectors,
    standalone,
    templateSource: templateUrl ? 'external' : inlineTemplate !== null || inlineTemplateUnparseable ? 'inline' : 'none',
    templateUrl,
    inlineTemplate,
    inlineTemplateUnparseable,
    inputs: ports.inputs,
    outputs: ports.outputs,
    queries: ports.queries,
    importsIdentifiers,
    dialogOpens,
  };
}

function resolveTemplateUrl(url, filePath, root) {
  if (url.startsWith('/')) {
    return path.resolve(root || path.dirname(filePath), url.slice(1));
  }
  return path.resolve(path.dirname(filePath), url);
}

// --- inputs / outputs / queries ---------------------------------------------

function describePorts(node, sourceFile) {
  const inputs = [];
  const outputs = [];
  const queries = [];

  for (const member of node.members) {
    const memberName = member.name && ts.isIdentifier(member.name) ? member.name.text : null;
    if (!memberName) {
      continue;
    }

    // Decorator forms: @Input() / @Input('alias') / @Input({ alias, required }) / @Output(...)
    for (const decorator of getDecorators(ts, member)) {
      const expr = decorator.expression;
      if (!ts.isCallExpression(expr)) {
        continue;
      }
      const decoratorName = expr.expression.getText(sourceFile).replace(/^.*\./, '');
      if (decoratorName === 'Input') {
        inputs.push({ name: memberName, kind: 'decorator', ...decoratorPortOptions(expr, sourceFile) });
      } else if (decoratorName === 'Output') {
        outputs.push({ name: memberName, kind: 'decorator', ...decoratorPortOptions(expr, sourceFile) });
      } else if (decoratorName === 'ViewChild' || decoratorName === 'ViewChildren') {
        queries.push({
          name: memberName,
          kind: decoratorName === 'ViewChild' ? 'viewChild' : 'viewChildren',
          signal: false,
          target: firstArgText(expr, sourceFile),
        });
      }
    }

    // Signal forms: x = input(), input.required(), output(), model(), viewChild(), ...
    if (ts.isPropertyDeclaration(member) && member.initializer && ts.isCallExpression(member.initializer)) {
      const call = member.initializer;
      const callee = call.expression.getText(sourceFile);
      if (callee === 'input' || callee === 'input.required') {
        inputs.push({
          name: memberName,
          kind: 'signal',
          required: callee === 'input.required',
          alias: signalAlias(call, sourceFile),
        });
      } else if (callee === 'output') {
        outputs.push({ name: memberName, kind: 'signal', required: false, alias: signalAlias(call, sourceFile) });
      } else if (callee === 'outputFromObservable') {
        outputs.push({ name: memberName, kind: 'signal', required: false, alias: null });
      } else if (callee === 'model' || callee === 'model.required') {
        // model() is a two-way port: surfaces as an input plus an implicit xChange output.
        inputs.push({
          name: memberName,
          kind: 'model',
          required: callee === 'model.required',
          alias: signalAlias(call, sourceFile),
        });
      } else if (callee === 'viewChild' || callee === 'viewChild.required' || callee === 'viewChildren') {
        queries.push({
          name: memberName,
          kind: callee === 'viewChildren' ? 'viewChildren' : 'viewChild',
          signal: true,
          target: firstArgText(call, sourceFile),
        });
      }
    }
  }

  return { inputs, outputs, queries };
}

function decoratorPortOptions(call, sourceFile) {
  let alias = null;
  let required = false;
  const arg = call.arguments && call.arguments[0];
  if (arg) {
    if (ts.isStringLiteralLike(arg)) {
      alias = arg.text;
    } else if (ts.isObjectLiteralExpression(arg)) {
      for (const prop of arg.properties) {
        if (!ts.isPropertyAssignment(prop) || !prop.name) {
          continue;
        }
        const name = prop.name.getText(sourceFile);
        if (name === 'alias' && ts.isStringLiteralLike(prop.initializer)) {
          alias = prop.initializer.text;
        } else if (name === 'required' && prop.initializer.kind === ts.SyntaxKind.TrueKeyword) {
          required = true;
        }
      }
    }
  }
  return { alias, required };
}

function signalAlias(call, sourceFile) {
  // input(default?, { alias }) / input.required({ alias }) — scan args for an options object.
  for (const arg of call.arguments || []) {
    if (ts.isObjectLiteralExpression(arg)) {
      for (const prop of arg.properties) {
        if (ts.isPropertyAssignment(prop) && prop.name && prop.name.getText(sourceFile) === 'alias'
          && ts.isStringLiteralLike(prop.initializer)) {
          return prop.initializer.text;
        }
      }
    }
  }
  return null;
}

function firstArgText(call, sourceFile) {
  const arg = call.arguments && call.arguments[0];
  if (!arg) {
    return null;
  }
  if (ts.isStringLiteralLike(arg)) {
    return arg.text;
  }
  return collapse(arg.getText(sourceFile));
}

// --- dialog linkage ----------------------------------------------------------

/**
 * Best-effort discovery of `this.<member>.open(SomeComponent)` calls where the
 * member looks like a MatDialog (typed MatDialog, assigned inject(MatDialog),
 * or simply named *dialog*). Absence is never an error.
 */
function describeDialogOpens(node, sourceFile) {
  const dialogMembers = collectDialogMembers(node, sourceFile);
  const opens = [];

  for (const member of node.members) {
    if (!ts.isMethodDeclaration(member) || !member.body || !member.name) {
      continue;
    }
    const handler = member.name.getText(sourceFile);
    walk(member.body);

    function walk(current) {
      if (ts.isCallExpression(current)
        && ts.isPropertyAccessExpression(current.expression)
        && current.expression.name.text === 'open'
        && ts.isPropertyAccessExpression(current.expression.expression)
        && current.expression.expression.expression.kind === ts.SyntaxKind.ThisKeyword) {
        const receiver = current.expression.expression.name.text;
        const firstArg = current.arguments && current.arguments[0];
        if (firstArg && ts.isIdentifier(firstArg) && /^[A-Z]/.test(firstArg.text)
          && (dialogMembers.has(receiver) || /dialog/i.test(receiver))) {
          opens.push({ handler, componentName: firstArg.text });
        }
      }
      ts.forEachChild(current, walk);
    }
  }

  return opens;
}

function collectDialogMembers(node, sourceFile) {
  const members = new Set();
  for (const member of node.members) {
    if (ts.isPropertyDeclaration(member) && member.name && ts.isIdentifier(member.name)) {
      const typeText = member.type ? member.type.getText(sourceFile) : '';
      const initText = member.initializer && ts.isCallExpression(member.initializer)
        ? member.initializer.getText(sourceFile)
        : '';
      if (/\bMatDialog\b/.test(typeText) || /inject\(\s*MatDialog\s*\)/.test(initText)) {
        members.add(member.name.text);
      }
    } else if (ts.isConstructorDeclaration(member)) {
      for (const param of member.parameters) {
        if (ts.isIdentifier(param.name) && param.type && /\bMatDialog\b/.test(param.type.getText(sourceFile))) {
          members.add(param.name.text);
        }
      }
    }
  }
  return members;
}

module.exports = { scanSourceFile };
