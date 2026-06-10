'use strict';

// Compatibility shims over the TypeScript compiler API. The sidecar resolves
// typescript from the analyzed workspace, so it must tolerate the API surface
// of every version an Angular app may ship (TS 4.x through current).

/**
 * Returns the decorators of a node across TypeScript versions: TS >= 4.8 moved
 * decorators behind ts.getDecorators/canHaveDecorators; older versions exposed
 * node.decorators directly.
 */
function getDecorators(ts, node) {
  if (typeof ts.getDecorators === 'function') {
    if (typeof ts.canHaveDecorators === 'function' && !ts.canHaveDecorators(node)) {
      return [];
    }
    return ts.getDecorators(node) || [];
  }
  return node.decorators || [];
}

/**
 * Returns the modifiers of a node (TS >= 4.8 mixes decorators into modifiers;
 * filter to actual modifier keywords).
 */
function getModifiers(ts, node) {
  const mods = typeof ts.getModifiers === 'function'
    ? (ts.canHaveModifiers && !ts.canHaveModifiers(node) ? [] : ts.getModifiers(node) || [])
    : node.modifiers || [];
  return mods.filter((m) => m.kind !== ts.SyntaxKind.Decorator);
}

function hasExportModifier(ts, node) {
  return getModifiers(ts, node).some((m) => m.kind === ts.SyntaxKind.ExportKeyword);
}

module.exports = { getDecorators, getModifiers, hasExportModifier };
