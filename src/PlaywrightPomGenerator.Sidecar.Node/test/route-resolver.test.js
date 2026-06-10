'use strict';

const test = require('node:test');
const assert = require('node:assert/strict');
const fs = require('fs');
const path = require('path');
const ts = require('typescript');
const { resolveRoutes } = require('../lib/route-resolver');
const { createPathResolver } = require('../lib/tsconfig-paths');
const { makeFixture } = require('./helpers');

function run(fixture, sourceRootRelative) {
  const warnings = [];
  const fileCache = new Map();
  const loadFile = (absPath) => {
    const key = path.resolve(absPath);
    if (fileCache.has(key)) {
      return fileCache.get(key);
    }
    let entry = null;
    try {
      if (fs.existsSync(key) && fs.statSync(key).isFile()) {
        const content = fs.readFileSync(key, 'utf8');
        entry = { sourceFile: ts.createSourceFile(key, content, ts.ScriptTarget.Latest, true), content };
      }
    } catch (e) {
      entry = null;
    }
    fileCache.set(key, entry);
    return entry;
  };
  const routes = resolveRoutes({
    root: fixture.root,
    sourceRoot: path.join(fixture.root, sourceRootRelative || 'src'),
    loadFile,
    pathResolver: createPathResolver(fixture.root),
    warnings,
  });
  return { routes, warnings };
}

test('builds a tree from provideRouter with children and joins full paths', () => {
  const fixture = makeFixture({
    'src/app/app.config.ts': `
      import { provideRouter } from '@angular/router';
      import { routes } from './app.routes';
      export const appConfig = { providers: [provideRouter(routes)] };
    `,
    'src/app/app.routes.ts': `
      import { Routes } from '@angular/router';
      import { ShellComponent } from './shell/shell.component';
      import { OrdersComponent } from './orders/orders.component';
      export const routes: Routes = [
        {
          path: '',
          component: ShellComponent,
          children: [
            { path: 'orders/:id', component: OrdersComponent, title: 'Order' },
            { path: '**', redirectTo: '' },
          ],
        },
      ];
    `,
    'src/app/shell/shell.component.ts': 'export class ShellComponent {}',
    'src/app/orders/orders.component.ts': 'export class OrdersComponent {}',
  });
  try {
    const { routes } = run(fixture);
    assert.equal(routes.tree.length, 1);
    const shell = routes.tree[0];
    assert.equal(shell.component, 'ShellComponent');
    assert.ok(shell.componentFilePath.endsWith('shell.component.ts'));
    assert.equal(shell.fullPath, '/');
    assert.equal(shell.isRoot, true);

    const orders = shell.children[0];
    assert.equal(orders.fullPath, '/orders/:id');
    assert.deepEqual(orders.pathParams, ['id']);
    assert.equal(orders.title, 'Order');

    const wildcard = shell.children[1];
    assert.equal(wildcard.wildcard, true);
    assert.equal(wildcard.redirectTo, '');

    const index = routes.componentRoutes.find((r) => r.componentClassName === 'OrdersComponent');
    assert.deepEqual(index.fullPaths, ['/orders/:id']);
  } finally {
    fixture.cleanup();
  }
});

test('resolves loadComponent and loadChildren through relative imports', () => {
  const fixture = makeFixture({
    'src/app/app.routes.ts': `
      import { Routes } from '@angular/router';
      export const routes: Routes = [
        {
          path: 'profile',
          loadComponent: () => import('./profile/profile.component').then(m => m.ProfileComponent),
        },
        {
          path: 'admin',
          loadChildren: () => import('./admin/admin.routes').then(m => m.ADMIN_ROUTES),
        },
      ];
    `,
    'src/app/profile/profile.component.ts': 'export class ProfileComponent {}',
    'src/app/admin/admin.routes.ts': `
      import { Routes } from '@angular/router';
      import { AdminHomeComponent } from './admin-home.component';
      export const ADMIN_ROUTES: Routes = [
        { path: '', component: AdminHomeComponent },
        { path: 'users/:userId', component: AdminHomeComponent },
      ];
    `,
    'src/app/admin/admin-home.component.ts': 'export class AdminHomeComponent {}',
  });
  try {
    const { routes } = run(fixture);
    const profile = routes.tree.find((r) => r.path === 'profile');
    assert.equal(profile.isLazy, true);
    assert.equal(profile.component, 'ProfileComponent');
    assert.ok(profile.componentFilePath.endsWith('profile.component.ts'));

    const admin = routes.tree.find((r) => r.path === 'admin');
    assert.equal(admin.children.length, 2);
    assert.equal(admin.children[1].fullPath, '/admin/users/:userId');
    assert.deepEqual(admin.children[1].pathParams, ['userId']);
  } finally {
    fixture.cleanup();
  }
});

test('resolves loadChildren through tsconfig path aliases', () => {
  const fixture = makeFixture({
    'tsconfig.json': JSON.stringify({
      compilerOptions: {
        baseUrl: '.',
        paths: { '@features/*': ['src/features/*'] },
      },
    }),
    'src/app/app.routes.ts': `
      import { Routes } from '@angular/router';
      export const routes: Routes = [
        { path: 'reports', loadChildren: () => import('@features/reports/reports.routes').then(m => m.REPORT_ROUTES) },
      ];
    `,
    'src/features/reports/reports.routes.ts': `
      import { Routes } from '@angular/router';
      import { ReportListComponent } from './report-list.component';
      export const REPORT_ROUTES: Routes = [{ path: '', component: ReportListComponent }];
    `,
    'src/features/reports/report-list.component.ts': 'export class ReportListComponent {}',
  });
  try {
    const { routes, warnings } = run(fixture);
    const reports = routes.tree.find((r) => r.path === 'reports');
    assert.equal(reports.loadChildren.resolved, true, 'alias should resolve: ' + warnings.join('; '));
    assert.equal(reports.children.length, 1);
    assert.equal(reports.children[0].component, 'ReportListComponent');
    assert.equal(reports.children[0].fullPath, '/reports');
  } finally {
    fixture.cleanup();
  }
});

test('resolves NgModule lazy targets via RouterModule.forChild', () => {
  const fixture = makeFixture({
    'src/app/app.routes.ts': `
      import { Routes } from '@angular/router';
      export const routes: Routes = [
        { path: 'legacy', loadChildren: () => import('./legacy/legacy.module').then(m => m.LegacyModule) },
      ];
    `,
    'src/app/legacy/legacy.module.ts': `
      import { NgModule } from '@angular/core';
      import { RouterModule } from '@angular/router';
      import { LegacyHomeComponent } from './legacy-home.component';
      @NgModule({
        imports: [RouterModule.forChild([{ path: '', component: LegacyHomeComponent }])],
      })
      export class LegacyModule {}
    `,
    'src/app/legacy/legacy-home.component.ts': 'export class LegacyHomeComponent {}',
  });
  try {
    const { routes } = run(fixture);
    const legacy = routes.tree.find((r) => r.path === 'legacy');
    assert.equal(legacy.children.length, 1);
    assert.equal(legacy.children[0].component, 'LegacyHomeComponent');
    assert.equal(legacy.children[0].fullPath, '/legacy');
  } finally {
    fixture.cleanup();
  }
});

test('default-exported lazy routes resolve with exportName default', () => {
  const fixture = makeFixture({
    'src/app/app.routes.ts': `
      import { Routes } from '@angular/router';
      export const routes: Routes = [
        { path: 'shop', loadChildren: () => import('./shop/shop.routes') },
      ];
    `,
    'src/app/shop/shop.routes.ts': `
      import { Routes } from '@angular/router';
      import { ShopComponent } from './shop.component';
      export default [{ path: '', component: ShopComponent }] satisfies Routes;
    `,
    'src/app/shop/shop.component.ts': 'export class ShopComponent {}',
  });
  try {
    const { routes } = run(fixture);
    const shop = routes.tree.find((r) => r.path === 'shop');
    assert.equal(shop.loadChildren.exportName, 'default');
    assert.equal(shop.children.length, 1);
    assert.equal(shop.children[0].component, 'ShopComponent');
  } finally {
    fixture.cleanup();
  }
});

test('guards, data keys and circular loadChildren do not break resolution', () => {
  const fixture = makeFixture({
    'src/app/app.routes.ts': `
      import { Routes } from '@angular/router';
      import { authGuard } from './auth.guard';
      import { SecureComponent } from './secure.component';
      export const routes: Routes = [
        {
          path: 'secure',
          component: SecureComponent,
          canActivate: [authGuard],
          data: { role: 'admin', audit: true },
        },
        { path: 'loop', loadChildren: () => import('./loop.routes').then(m => m.LOOP_ROUTES) },
      ];
    `,
    'src/app/auth.guard.ts': 'export const authGuard = () => true;',
    'src/app/secure.component.ts': 'export class SecureComponent {}',
    'src/app/loop.routes.ts': `
      import { Routes } from '@angular/router';
      export const LOOP_ROUTES: Routes = [
        { path: 'again', loadChildren: () => import('./loop.routes').then(m => m.LOOP_ROUTES) },
      ];
    `,
  });
  try {
    const { routes, warnings } = run(fixture);
    const secure = routes.tree.find((r) => r.path === 'secure');
    assert.deepEqual(secure.guards, ['authGuard']);
    assert.deepEqual(secure.dataKeys, ['role', 'audit']);
    assert.ok(warnings.some((w) => w.includes('circular')));
  } finally {
    fixture.cleanup();
  }
});

test('unresolvable lazy specifiers are recorded with a warning, not fatal', () => {
  const fixture = makeFixture({
    'src/app/app.routes.ts': `
      import { Routes } from '@angular/router';
      export const routes: Routes = [
        { path: 'ext', loadChildren: () => import('@some-lib/routes').then(m => m.LIB_ROUTES) },
      ];
    `,
  });
  try {
    const { routes, warnings } = run(fixture);
    const ext = routes.tree.find((r) => r.path === 'ext');
    assert.equal(ext.loadChildren.resolved, false);
    assert.ok(warnings.some((w) => w.includes('@some-lib/routes')));
  } finally {
    fixture.cleanup();
  }
});
