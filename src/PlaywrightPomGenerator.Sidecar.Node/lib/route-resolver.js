'use strict';

// Route tree extraction over the TypeScript AST (no @angular/compiler needed).
//
// Discovers route arrays from provideRouter(...), RouterModule.forRoot/forChild(...),
// `Routes`/`Route[]`-typed consts and default-exported arrays; follows
// loadChildren/loadComponent dynamic import() specifiers (relative paths and
// tsconfig path aliases); builds a tree with joined full paths and a flat
// component-route index for the .NET host.

const path = require('path');
const ts = require('typescript');
const { collectTsFiles } = require('./util');

/**
 * @param {object} opts
 * @param {string} opts.root          workspace root
 * @param {string} opts.sourceRoot    project source root to scan for route declarations
 * @param {(absPath: string) => {sourceFile: import('typescript').SourceFile}|null} opts.loadFile
 * @param {{resolve: (spec: string) => string[]}} opts.pathResolver
 * @param {string[]} opts.warnings
 */
function resolveRoutes(opts) {
  const ctx = {
    loadFile: opts.loadFile,
    pathResolver: opts.pathResolver,
    warnings: opts.warnings,
    consumedArrays: new Set(), // array nodes already attached to the tree
    lazyVisited: new Set(),    // "file::export" cycle guard
  };

  const files = [];
  collectTsFiles(opts.sourceRoot, files);

  /** @type {{node: import('typescript').Node, file: string, isRoot: boolean}[]} */
  const rootArrays = [];
  const childArrays = [];
  /** @type {{node: import('typescript').Node, file: string, name: string}[]} */
  const routeConsts = [];

  for (const file of files) {
    const loaded = ctx.loadFile(file);
    if (!loaded) {
      continue;
    }
    discoverInFile(loaded.sourceFile, file, rootArrays, childArrays, routeConsts, ctx);
  }

  const tree = [];
  for (const entry of rootArrays) {
    if (ctx.consumedArrays.has(entry.node)) {
      continue;
    }
    ctx.consumedArrays.add(entry.node);
    tree.push(...parseRouteArray(entry.node, entry.file, ctx, { isRoot: true }));
  }

  // Unconsumed forChild arrays and exported route consts: emit so the host can
  // still use their paths (treated as absolute), but flagged as non-root.
  const orphanSources = childArrays.concat(routeConsts);
  for (const entry of orphanSources) {
    if (ctx.consumedArrays.has(entry.node)) {
      continue;
    }
    ctx.consumedArrays.add(entry.node);
    tree.push(...parseRouteArray(entry.node, entry.file, ctx, { isRoot: false }));
  }

  computeFullPaths(tree, '');

  const componentRoutes = [];
  indexComponentRoutes(tree, componentRoutes);

  return { tree, componentRoutes };
}

// --- discovery ---------------------------------------------------------------

function discoverInFile(sourceFile, file, rootArrays, childArrays, routeConsts, ctx) {
  walk(sourceFile);

  function walk(node) {
    if (ts.isCallExpression(node)) {
      const calleeText = node.expression.getText(sourceFile);
      if (/(^|\.)provideRouter$/.test(calleeText) || /RouterModule\s*\.\s*forRoot$/.test(calleeText)) {
        const array = resolveToArray(node.arguments[0], sourceFile, file, ctx);
        if (array) {
          rootArrays.push({ node: array.node, file: array.file, isRoot: true });
        }
      } else if (/RouterModule\s*\.\s*forChild$/.test(calleeText)) {
        const array = resolveToArray(node.arguments[0], sourceFile, file, ctx);
        if (array) {
          childArrays.push({ node: array.node, file: array.file });
        }
      }
    } else if (ts.isVariableStatement(node)) {
      for (const decl of node.declarationList.declarations) {
        if (!ts.isIdentifier(decl.name) || !decl.initializer) {
          continue;
        }
        const init = unwrapExpression(decl.initializer);
        if (!ts.isArrayLiteralExpression(init)) {
          continue;
        }
        if (isRoutesType(decl.type, sourceFile) || isRoutesSatisfies(decl.initializer, sourceFile)) {
          routeConsts.push({ node: init, file, name: decl.name.text });
        }
      }
    } else if (ts.isExportAssignment(node) && !node.isExportEquals) {
      const expr = unwrapExpression(node.expression);
      if (ts.isArrayLiteralExpression(expr) && isRoutesSatisfies(node.expression, sourceFile)) {
        routeConsts.push({ node: expr, file, name: 'default' });
      }
    }
    ts.forEachChild(node, walk);
  }
}

function isRoutesType(typeNode, sourceFile) {
  if (!typeNode) {
    return false;
  }
  const text = typeNode.getText(sourceFile).replace(/\s+/g, '');
  return text === 'Routes' || text === 'Route[]' || text === 'Array<Route>';
}

function isRoutesSatisfies(expr, sourceFile) {
  // `[...] satisfies Routes` or `[...] as Routes` — also returns true for a
  // bare array under an export default (lazy routes convention).
  if (ts.isSatisfiesExpression && ts.isSatisfiesExpression(expr)) {
    return isRoutesType(expr.type, sourceFile);
  }
  if (ts.isAsExpression(expr)) {
    return isRoutesType(expr.type, sourceFile);
  }
  return ts.isArrayLiteralExpression(expr);
}

function unwrapExpression(expr) {
  while (expr && ((ts.isSatisfiesExpression && ts.isSatisfiesExpression(expr)) || ts.isAsExpression(expr) || ts.isParenthesizedExpression(expr))) {
    expr = expr.expression;
  }
  return expr;
}

/** Resolves an expression (array literal or identifier, possibly imported) to an array literal node. */
function resolveToArray(expr, sourceFile, file, ctx) {
  if (!expr) {
    return null;
  }
  expr = unwrapExpression(expr);
  if (ts.isArrayLiteralExpression(expr)) {
    return { node: expr, file };
  }
  if (ts.isIdentifier(expr)) {
    return resolveExportedArray(file, expr.text, ctx, sourceFile);
  }
  return null;
}

/** Finds an array-initialized const `name` in `file` (local) or follows the import of `name`. */
function resolveExportedArray(file, name, ctx, sourceFile) {
  const loaded = ctx.loadFile(file);
  if (!loaded) {
    return null;
  }
  const sf = loaded.sourceFile;

  // Local const (or default export when name === 'default').
  let found = null;
  ts.forEachChild(sf, (node) => {
    if (found) {
      return;
    }
    if (name === 'default' && ts.isExportAssignment(node) && !node.isExportEquals) {
      const expr = unwrapExpression(node.expression);
      if (ts.isArrayLiteralExpression(expr)) {
        found = { node: expr, file };
      }
      return;
    }
    if (ts.isVariableStatement(node)) {
      for (const decl of node.declarationList.declarations) {
        if (ts.isIdentifier(decl.name) && decl.name.text === name && decl.initializer) {
          const init = unwrapExpression(decl.initializer);
          if (ts.isArrayLiteralExpression(init)) {
            found = { node: init, file };
          }
        }
      }
    }
  });
  if (found) {
    return found;
  }

  // Imported identifier — follow the import declaration.
  const target = resolveImportedSymbol(sf, file, name, ctx);
  if (target) {
    return resolveExportedArray(target.file, target.exportName, ctx, null);
  }
  return null;
}

/** Maps an identifier used in `file` to { file, exportName } via its import declaration. */
function resolveImportedSymbol(sourceFile, file, name, ctx) {
  let result = null;
  ts.forEachChild(sourceFile, (node) => {
    if (result || !ts.isImportDeclaration(node) || !node.importClause) {
      return;
    }
    const spec = ts.isStringLiteralLike(node.moduleSpecifier) ? node.moduleSpecifier.text : null;
    if (!spec) {
      return;
    }
    const clause = node.importClause;
    if (clause.name && clause.name.text === name) {
      result = { spec, exportName: 'default' };
    } else if (clause.namedBindings && ts.isNamedImports(clause.namedBindings)) {
      for (const binding of clause.namedBindings.elements) {
        if (binding.name.text === name) {
          result = { spec, exportName: (binding.propertyName || binding.name).text };
        }
      }
    }
  });
  if (!result) {
    return null;
  }
  const resolved = resolveModuleSpecifier(result.spec, path.dirname(file), ctx);
  return resolved ? { file: resolved, exportName: result.exportName } : null;
}

/** Resolves a module specifier to an existing .ts file (relative or tsconfig alias). */
function resolveModuleSpecifier(spec, fromDir, ctx) {
  const bases = spec.startsWith('.')
    ? [path.resolve(fromDir, spec)]
    : ctx.pathResolver.resolve(spec);
  for (const base of bases) {
    for (const candidate of [base, base + '.ts', path.join(base, 'index.ts')]) {
      if (candidate.includes(`${path.sep}node_modules${path.sep}`)) {
        continue;
      }
      const loaded = ctx.loadFile(candidate);
      if (loaded) {
        return candidate;
      }
    }
  }
  return null;
}

// --- route object parsing ------------------------------------------------------

function parseRouteArray(arrayNode, file, ctx, flags) {
  const loaded = ctx.loadFile(file);
  const sourceFile = loaded ? loaded.sourceFile : null;
  if (!sourceFile) {
    return [];
  }
  const routes = [];
  for (const element of arrayNode.elements) {
    const expr = unwrapExpression(element);
    if (ts.isObjectLiteralExpression(expr)) {
      const route = parseRouteObject(expr, sourceFile, file, ctx, flags);
      if (route) {
        routes.push(route);
      }
    } else if (ts.isSpreadElement(element) && ts.isIdentifier(element.expression)) {
      const spread = resolveExportedArray(file, element.expression.text, ctx, sourceFile);
      if (spread && !ctx.consumedArrays.has(spread.node)) {
        ctx.consumedArrays.add(spread.node);
        routes.push(...parseRouteArray(spread.node, spread.file, ctx, flags));
      } else if (!spread) {
        ctx.warnings.push(file + ': could not resolve spread routes ...' + element.expression.text);
      }
    }
  }
  return routes;
}

function parseRouteObject(obj, sourceFile, file, ctx, flags) {
  const route = {
    path: null,
    fullPath: null,
    component: null,
    componentFilePath: null,
    redirectTo: null,
    pathMatch: null,
    title: null,
    outlet: null,
    pathParams: [],
    wildcard: false,
    guards: [],
    dataKeys: [],
    loadComponent: null,
    loadChildren: null,
    isLazy: false,
    isRoot: !!flags.isRoot,
    sourceFile: file,
    children: [],
  };

  for (const prop of obj.properties) {
    if (!ts.isPropertyAssignment(prop) || !prop.name) {
      continue;
    }
    const name = prop.name.getText(sourceFile);
    const value = prop.initializer;

    switch (name) {
      case 'path':
        if (ts.isStringLiteralLike(value)) {
          route.path = value.text;
        } else {
          ctx.warnings.push(file + ': non-literal route path "' + value.getText(sourceFile) + '" — route skipped');
          return null;
        }
        break;
      case 'component':
        if (ts.isIdentifier(value)) {
          route.component = value.text;
          route.componentFilePath = resolveComponentFile(sourceFile, file, value.text, ctx);
        }
        break;
      case 'redirectTo':
        if (ts.isStringLiteralLike(value)) {
          route.redirectTo = value.text;
        }
        break;
      case 'pathMatch':
        if (ts.isStringLiteralLike(value)) {
          route.pathMatch = value.text;
        }
        break;
      case 'title':
        if (ts.isStringLiteralLike(value)) {
          route.title = value.text;
        } else if (ts.isIdentifier(value)) {
          route.title = value.text;
        }
        break;
      case 'outlet':
        if (ts.isStringLiteralLike(value)) {
          route.outlet = value.text;
        }
        break;
      case 'data':
        if (ts.isObjectLiteralExpression(value)) {
          for (const dataProp of value.properties) {
            if (ts.isPropertyAssignment(dataProp) && dataProp.name) {
              route.dataKeys.push(dataProp.name.getText(sourceFile).replace(/['"]/g, ''));
            }
          }
        }
        break;
      case 'canActivate':
      case 'canActivateChild':
      case 'canMatch':
      case 'canDeactivate':
        if (ts.isArrayLiteralExpression(value)) {
          for (const guard of value.elements) {
            route.guards.push(guard.getText(sourceFile));
          }
        }
        break;
      case 'children': {
        const array = resolveToArray(value, sourceFile, file, ctx);
        if (array) {
          ctx.consumedArrays.add(array.node);
          route.children = parseRouteArray(array.node, array.file, ctx, { isRoot: flags.isRoot });
        }
        break;
      }
      case 'loadComponent': {
        const target = parseDynamicImport(value, sourceFile);
        if (target) {
          route.isLazy = true;
          route.loadComponent = resolveLoadTarget(target, file, ctx);
          if (route.loadComponent.resolved) {
            route.componentFilePath = route.loadComponent.resolvedFile;
            route.component = route.loadComponent.exportName === 'default'
              ? findDefaultExportedClassName(route.loadComponent.resolvedFile, ctx)
              : route.loadComponent.exportName;
          }
        }
        break;
      }
      case 'loadChildren': {
        const target = parseDynamicImport(value, sourceFile);
        if (target) {
          route.isLazy = true;
          route.loadChildren = resolveLoadTarget(target, file, ctx);
          if (route.loadChildren.resolved) {
            route.children = resolveLazyChildren(route.loadChildren, ctx, flags);
          }
        }
        break;
      }
      default:
        break;
    }
  }

  if (route.path === '**') {
    route.wildcard = true;
  }
  return route;
}

/** Extracts { specifier, exportName } from `() => import('./x').then(m => m.X)` shapes. */
function parseDynamicImport(expr, sourceFile) {
  let body = expr;
  if (ts.isArrowFunction(expr) || ts.isFunctionExpression(expr)) {
    body = expr.body;
    if (ts.isBlock(body)) {
      const ret = body.statements.find((s) => ts.isReturnStatement(s) && s.expression);
      body = ret ? ret.expression : null;
    }
  }
  if (!body) {
    return null;
  }
  body = unwrapExpression(body);

  // import('spec') — bare promise: the default export is used.
  if (isImportCall(body)) {
    const spec = importSpecifier(body);
    return spec ? { specifier: spec, exportName: 'default' } : null;
  }

  // import('spec').then(m => m.X)
  if (ts.isCallExpression(body)
    && ts.isPropertyAccessExpression(body.expression)
    && body.expression.name.text === 'then'
    && isImportCall(body.expression.expression)) {
    const spec = importSpecifier(body.expression.expression);
    if (!spec) {
      return null;
    }
    let exportName = 'default';
    const thenArg = body.arguments[0];
    if (thenArg && (ts.isArrowFunction(thenArg) || ts.isFunctionExpression(thenArg))) {
      let thenBody = thenArg.body;
      if (ts.isBlock(thenBody)) {
        const ret = thenBody.statements.find((s) => ts.isReturnStatement(s) && s.expression);
        thenBody = ret ? ret.expression : null;
      }
      if (thenBody && ts.isPropertyAccessExpression(unwrapExpression(thenBody))) {
        exportName = unwrapExpression(thenBody).name.text;
      }
    }
    return { specifier: spec, exportName };
  }
  return null;
}

function isImportCall(node) {
  return ts.isCallExpression(node) && node.expression.kind === ts.SyntaxKind.ImportKeyword;
}

function importSpecifier(callNode) {
  const arg = callNode.arguments[0];
  return arg && ts.isStringLiteralLike(arg) ? arg.text : null;
}

function resolveLoadTarget(target, fromFile, ctx) {
  const resolvedFile = resolveModuleSpecifier(target.specifier, path.dirname(fromFile), ctx);
  if (!resolvedFile) {
    ctx.warnings.push(fromFile + ": could not resolve lazy import '" + target.specifier + "'");
  }
  return {
    specifier: target.specifier,
    exportName: target.exportName,
    resolvedFile: resolvedFile,
    resolved: !!resolvedFile,
  };
}

/** Loads the lazy target file and extracts its routes (exported array or NgModule forChild). */
function resolveLazyChildren(loadTarget, ctx, flags) {
  const key = loadTarget.resolvedFile + '::' + loadTarget.exportName;
  if (ctx.lazyVisited.has(key)) {
    ctx.warnings.push('circular loadChildren chain at ' + key + ' — recursion stopped');
    return [];
  }
  ctx.lazyVisited.add(key);
  try {
    // Named/default exported routes array.
    const array = resolveExportedArray(loadTarget.resolvedFile, loadTarget.exportName, ctx, null);
    if (array) {
      ctx.consumedArrays.add(array.node);
      return parseRouteArray(array.node, array.file, ctx, { isRoot: flags.isRoot });
    }
    // NgModule with RouterModule.forChild(...) in the target file.
    const forChild = findForChildArray(loadTarget.resolvedFile, ctx);
    if (forChild) {
      ctx.consumedArrays.add(forChild.node);
      return parseRouteArray(forChild.node, forChild.file, ctx, { isRoot: flags.isRoot });
    }
    ctx.warnings.push(loadTarget.resolvedFile + ': no routes found for lazy export ' + loadTarget.exportName);
    return [];
  } finally {
    ctx.lazyVisited.delete(key);
  }
}

function findForChildArray(file, ctx) {
  const loaded = ctx.loadFile(file);
  if (!loaded) {
    return null;
  }
  const sourceFile = loaded.sourceFile;
  let found = null;
  walk(sourceFile);
  return found;

  function walk(node) {
    if (found) {
      return;
    }
    if (ts.isCallExpression(node) && /RouterModule\s*\.\s*forChild$/.test(node.expression.getText(sourceFile))) {
      found = resolveToArray(node.arguments[0], sourceFile, file, ctx);
      return;
    }
    ts.forEachChild(node, walk);
  }
}

function resolveComponentFile(sourceFile, file, componentName, ctx) {
  // Same-file class declaration.
  let local = false;
  ts.forEachChild(sourceFile, (node) => {
    if (ts.isClassDeclaration(node) && node.name && node.name.text === componentName) {
      local = true;
    }
  });
  if (local) {
    return file;
  }
  const target = resolveImportedSymbol(sourceFile, file, componentName, ctx);
  return target ? target.file : null;
}

function findDefaultExportedClassName(file, ctx) {
  const loaded = ctx.loadFile(file);
  if (!loaded) {
    return null;
  }
  let name = null;
  ts.forEachChild(loaded.sourceFile, (node) => {
    if (name) {
      return;
    }
    if (ts.isClassDeclaration(node) && node.name) {
      const mods = node.modifiers || [];
      const isDefault = mods.some((m) => m.kind === ts.SyntaxKind.DefaultKeyword);
      if (isDefault) {
        name = node.name.text;
      }
    } else if (ts.isExportAssignment(node) && !node.isExportEquals && ts.isIdentifier(node.expression)) {
      name = node.expression.text;
    }
  });
  return name;
}

// --- full paths + index ----------------------------------------------------------

function computeFullPaths(routes, parentPath) {
  for (const route of routes) {
    const segment = route.path || '';
    const joined = [parentPath, segment].filter((s) => s.length > 0).join('/');
    route.fullPath = '/' + joined;
    route.pathParams = joined
      .split('/')
      .filter((s) => s.startsWith(':'))
      .map((s) => s.slice(1).replace(/[^\w]/g, ''));
    computeFullPaths(route.children, joined);
  }
}

function indexComponentRoutes(routes, out) {
  for (const route of routes) {
    if (route.component && route.componentFilePath) {
      let entry = out.find((e) => e.componentFilePath === route.componentFilePath && e.componentClassName === route.component);
      if (!entry) {
        entry = { componentFilePath: route.componentFilePath, componentClassName: route.component, fullPaths: [], titles: [] };
        out.push(entry);
      }
      entry.fullPaths.push(route.fullPath);
      if (route.title) {
        entry.titles.push(route.title);
      }
    }
    indexComponentRoutes(route.children, out);
  }
}

module.exports = { resolveRoutes };
