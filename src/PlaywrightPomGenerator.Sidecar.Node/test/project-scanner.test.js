'use strict';

const test = require('node:test');
const assert = require('node:assert/strict');
const path = require('path');
const { scanSourceFile } = require('../lib/project-scanner');
const { parse } = require('./helpers');

function scan(content, filePath) {
  const file = filePath || path.join(__dirname, 'fixture', 'x.component.ts');
  const warnings = [];
  const components = scanSourceFile(parse(content, file), file, path.join(__dirname, 'fixture'), warnings);
  return { components, warnings };
}

test('extracts selector, templateUrl, standalone and decorator ports', () => {
  const { components } = scan(`
    import { Component, Input, Output, EventEmitter } from '@angular/core';

    @Component({
      selector: 'app-login, app-login-alt',
      templateUrl: './login.component.html',
      standalone: true,
    })
    export class LoginComponent {
      @Input() username = '';
      @Input('aliasName') aliased = '';
      @Input({ alias: 'objAlias', required: true }) withOptions!: string;
      @Output() submitted = new EventEmitter<void>();
    }
  `);

  assert.equal(components.length, 1);
  const c = components[0];
  assert.equal(c.className, 'LoginComponent');
  assert.equal(c.selector, 'app-login');
  assert.deepEqual(c.selectors, ['app-login', 'app-login-alt']);
  assert.equal(c.standalone, true);
  assert.equal(c.templateSource, 'external');
  assert.ok(c.templateUrl.endsWith('login.component.html'));

  assert.deepEqual(c.inputs.map((i) => [i.name, i.kind, i.alias, i.required]), [
    ['username', 'decorator', null, false],
    ['aliased', 'decorator', 'aliasName', false],
    ['withOptions', 'decorator', 'objAlias', true],
  ]);
  assert.deepEqual(c.outputs.map((o) => [o.name, o.kind]), [['submitted', 'decorator']]);
});

test('extracts signal-based ports and view queries', () => {
  const { components } = scan(`
    import { Component, input, output, model, viewChild } from '@angular/core';

    @Component({ selector: 'app-card', template: '<div></div>' })
    export class CardComponent {
      title = input<string>('');
      count = input.required<number>({ alias: 'itemCount' });
      closed = output<void>();
      expanded = model(false);
      header = viewChild('header');
    }
  `);

  const c = components[0];
  assert.deepEqual(c.inputs.map((i) => [i.name, i.kind, i.required, i.alias]), [
    ['title', 'signal', false, null],
    ['count', 'signal', true, 'itemCount'],
    ['expanded', 'model', false, null],
  ]);
  assert.deepEqual(c.outputs.map((o) => [o.name, o.kind]), [['closed', 'signal']]);
  assert.deepEqual(c.queries, [{ name: 'header', kind: 'viewChild', signal: true, target: 'header' }]);
});

test('inline template literal is captured; substitution template is flagged', () => {
  const plain = scan(`
    import { Component } from '@angular/core';
    @Component({ selector: 'app-a', template: \`<p>hello</p>\` })
    export class AComponent {}
  `);
  assert.equal(plain.components[0].templateSource, 'inline');
  assert.equal(plain.components[0].inlineTemplate, '<p>hello</p>');

  const substituted = scan(`
    import { Component } from '@angular/core';
    const part = '<b>x</b>';
    @Component({ selector: 'app-b', template: \`<p>\${part}</p>\` })
    export class BComponent {}
  `);
  assert.equal(substituted.components[0].inlineTemplateUnparseable, true);
  assert.equal(substituted.warnings.length, 1);
});

test('standalone defaults to null when unspecified', () => {
  const { components } = scan(`
    import { Component } from '@angular/core';
    @Component({ selector: 'app-c', template: '' })
    export class CComponent {}
  `);
  assert.equal(components[0].standalone, null);
});

test('detects MatDialog open calls by member type and by name', () => {
  const { components } = scan(`
    import { Component, inject } from '@angular/core';
    import { MatDialog } from '@angular/material/dialog';
    import { SettingsDialogComponent } from './settings-dialog.component';

    @Component({ selector: 'app-d', template: '' })
    export class DComponent {
      private readonly dialog = inject(MatDialog);
      openSettings(): void {
        this.dialog.open(SettingsDialogComponent, { width: '400px' });
      }
    }
  `);
  assert.deepEqual(components[0].dialogOpens, [
    { handler: 'openSettings', componentName: 'SettingsDialogComponent' },
  ]);
});

test('non-component classes are ignored', () => {
  const { components } = scan(`
    import { Injectable } from '@angular/core';
    @Injectable({ providedIn: 'root' })
    export class SomeService {}
    export class PlainClass {}
  `);
  assert.equal(components.length, 0);
});
