'use strict';

const test = require('node:test');
const assert = require('node:assert/strict');
const path = require('path');
const { spawn } = require('child_process');
const { analyzeProject } = require('../lib/analyze-project');
const { makeFixture } = require('./helpers');

const MINI_APP = {
  'package.json': JSON.stringify({ dependencies: { '@angular/core': '^20.0.0' } }),
  'src/app/app.routes.ts': `
    import { Routes } from '@angular/router';
    import { DashboardComponent } from './dashboard/dashboard.component';
    export const routes: Routes = [
      { path: 'dashboard', component: DashboardComponent, title: 'Dashboard' },
    ];
  `,
  'src/app/app.config.ts': `
    import { provideRouter } from '@angular/router';
    import { routes } from './app.routes';
    export const appConfig = { providers: [provideRouter(routes)] };
  `,
  'src/app/dashboard/dashboard.component.ts': `
    import { Component, input } from '@angular/core';
    @Component({
      selector: 'app-dashboard',
      standalone: true,
      templateUrl: './dashboard.component.html',
    })
    export class DashboardComponent {
      title = input<string>('');
    }
  `,
  'src/app/dashboard/dashboard.component.html': `
    <h1 data-testid="dashboard-title">Dashboard</h1>
    @for (kpi of kpis; track kpi.id) {
      <app-kpi-card [kpi]="kpi"></app-kpi-card>
    }
    <button data-testid="refresh" (click)="refresh()">Refresh</button>
  `,
  'src/app/kpi-card/kpi-card.component.ts': `
    import { Component } from '@angular/core';
    @Component({
      selector: 'app-kpi-card',
      standalone: true,
      template: '<div data-testid="kpi-value">{{ value }}</div>',
    })
    export class KpiCardComponent {}
  `,
};

test('assembles components, templates, routes, and child component links', async () => {
  const fixture = makeFixture(MINI_APP);
  try {
    const result = await analyzeProject({
      root: fixture.root,
      projects: [{ name: 'mini-app', sourceRoot: path.join(fixture.root, 'src'), prefix: 'app' }],
    });

    assert.equal(result.schemaVersion, 1);
    assert.ok(result.engine.typescript);
    // The fixture root has no node_modules, so the loader falls back to the
    // sidecar's own devDependency — template analysis must be available.
    assert.ok(result.engine.angularCompiler, 'compiler expected via sidecar devDependency');

    assert.equal(result.projects.length, 1);
    const project = result.projects[0];
    assert.equal(project.components.length, 2);

    const dashboard = project.components.find((c) => c.className === 'DashboardComponent');
    assert.equal(dashboard.standalone, true);
    assert.equal(dashboard.templateParsed, true);
    assert.deepEqual(dashboard.inputs.map((i) => [i.name, i.kind]), [['title', 'signal']]);
    assert.ok(dashboard.template.elements.some((e) => e.testId === 'dashboard-title'));
    assert.ok(dashboard.template.elements.some((e) => e.testId === 'refresh'));

    assert.equal(dashboard.childComponents.length, 1);
    const kpiChild = dashboard.childComponents[0];
    assert.equal(kpiChild.selector, 'app-kpi-card');
    assert.equal(kpiChild.componentClassName, 'KpiCardComponent');
    assert.equal(kpiChild.repeated, true);
    assert.equal(kpiChild.library, null);

    const routeIndex = project.routes.componentRoutes.find((r) => r.componentClassName === 'DashboardComponent');
    assert.deepEqual(routeIndex.fullPaths, ['/dashboard']);

    // Internal scratch fields never leak into the response.
    assert.equal('inlineTemplate' in dashboard, false);
    assert.equal('_childCandidates' in dashboard, false);
  } finally {
    fixture.cleanup();
  }
});

test('missing template file degrades that component only', async () => {
  const fixture = makeFixture({
    'src/app/broken.component.ts': `
      import { Component } from '@angular/core';
      @Component({ selector: 'app-broken', templateUrl: './missing.html' })
      export class BrokenComponent {}
    `,
  });
  try {
    const result = await analyzeProject({
      root: fixture.root,
      projects: [{ name: 'x', sourceRoot: path.join(fixture.root, 'src'), prefix: 'app' }],
    });
    const broken = result.projects[0].components[0];
    assert.equal(broken.templateParsed, false);
    assert.ok(broken.templateErrors[0].includes('template file not found'));
  } finally {
    fixture.cleanup();
  }
});

test('throws on a missing root (transport surfaces it as an RPC error)', async () => {
  await assert.rejects(
    () => analyzeProject({ root: path.join(__dirname, 'does-not-exist-xyz') }),
    /not found/,
  );
});

// End-to-end: spawn the sidecar exactly like NodeSidecarTransport does (write one
// request line, close stdin, read one response line). This is the regression test
// for the async-dispatch fix: analyzeProject is async, and the old
// `rl.on('close', () => process.exit(0))` would kill the process before it responded.
test('spawned sidecar answers analyzeProject after stdin closes', async () => {
  const fixture = makeFixture(MINI_APP);
  try {
    const response = await invokeSidecar({
      jsonrpc: '2.0',
      id: 1,
      method: 'analyzeProject',
      params: { root: fixture.root, projects: [{ name: 'mini-app', sourceRoot: path.join(fixture.root, 'src'), prefix: 'app' }] },
    });
    assert.equal(response.id, 1);
    assert.ok(response.result, 'expected a result, got: ' + JSON.stringify(response).slice(0, 300));
    assert.equal(response.result.schemaVersion, 1);
    assert.equal(response.result.projects[0].components.length, 2);
  } finally {
    fixture.cleanup();
  }
});

test('spawned sidecar still answers ping and discoverInjectionTokens', async () => {
  const fixture = makeFixture({
    'src/storage.token.ts': `
      import { InjectionToken } from '@angular/core';
      export interface ILocalStorage { getItem(key: string): string | null; }
      export const LOCAL_STORAGE = new InjectionToken<ILocalStorage>('local storage');
    `,
  });
  try {
    const ping = await invokeSidecar({ jsonrpc: '2.0', id: 7, method: 'ping' });
    assert.equal(ping.result, 'pong');

    const tokens = await invokeSidecar({
      jsonrpc: '2.0', id: 8, method: 'discoverInjectionTokens', params: { root: fixture.root },
    });
    assert.equal(tokens.result.tokens.length, 1);
    assert.equal(tokens.result.tokens[0].interfaceName, 'ILocalStorage');
  } finally {
    fixture.cleanup();
  }
});

function invokeSidecar(request) {
  return new Promise((resolve, reject) => {
    const child = spawn(process.execPath, [path.join(__dirname, '..', 'sidecar.js')], {
      cwd: path.join(__dirname, '..'),
      stdio: ['pipe', 'pipe', 'pipe'],
    });
    let stdout = '';
    let stderr = '';
    const timer = setTimeout(() => {
      child.kill();
      reject(new Error('sidecar timed out. stderr: ' + stderr));
    }, 30000);
    child.stdout.on('data', (d) => { stdout += d; });
    child.stderr.on('data', (d) => { stderr += d; });
    child.on('error', (e) => { clearTimeout(timer); reject(e); });
    child.on('close', () => {
      clearTimeout(timer);
      const line = stdout.split('\n').find((l) => l.trim().length > 0);
      if (!line) {
        reject(new Error('no response line. stderr: ' + stderr));
        return;
      }
      try {
        resolve(JSON.parse(line));
      } catch (e) {
        reject(new Error('bad JSON response: ' + line.slice(0, 300)));
      }
    });
    child.stdin.write(JSON.stringify(request) + '\n');
    child.stdin.end();
  });
}
