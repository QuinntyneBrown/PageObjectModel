'use strict';

const test = require('node:test');
const assert = require('node:assert/strict');
const ts = require('typescript');
const { getDecorators, hasExportModifier } = require('../lib/ts-compat');
const { parse } = require('./helpers');

test('getDecorators returns class decorators on the current TS API', () => {
  const sf = parse(`
    @Component({ selector: 'app-x' })
    export class XComponent {}
  `);
  const cls = sf.statements.find((s) => ts.isClassDeclaration(s));
  const decorators = getDecorators(ts, cls);
  assert.equal(decorators.length, 1);
  assert.ok(ts.isCallExpression(decorators[0].expression));
});

test('getDecorators returns empty for undecorated nodes', () => {
  const sf = parse('export class Plain {}');
  const cls = sf.statements.find((s) => ts.isClassDeclaration(s));
  assert.deepEqual(getDecorators(ts, cls), []);
});

test('hasExportModifier distinguishes exported classes', () => {
  const sf = parse('export class A {}\nclass B {}');
  const [a, b] = sf.statements.filter((s) => ts.isClassDeclaration(s));
  assert.equal(hasExportModifier(ts, a), true);
  assert.equal(hasExportModifier(ts, b), false);
});
