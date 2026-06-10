'use strict';

const test = require('node:test');
const assert = require('node:assert/strict');
const { analyzeTemplate } = require('../lib/template-visitor');
const { getCompiler } = require('./helpers');

async function analyze(template) {
  const compiler = await getCompiler();
  const result = analyzeTemplate(compiler, template, 'fixture.component.html');
  assert.equal(result.ok, true, 'template should parse: ' + result.errors.join('; '));
  return result;
}

function byTestId(result, testId) {
  return result.elements.find((e) => e.testId === testId);
}

test('extracts test ids, events, and click handler names', async () => {
  const result = await analyze(`
    <button data-testid="save-button" (click)="save()">Save</button>
  `);
  const button = byTestId(result, 'save-button');
  assert.ok(button);
  assert.equal(button.tag, 'button');
  assert.deepEqual(button.events, ['click']);
  assert.equal(button.handlers.click, 'save');
  assert.equal(button.text.value, 'Save');
});

test('marks elements inside @if as conditional with the condition text', async () => {
  const result = await analyze(`
    @if (user.isAdmin) {
      <button data-testid="admin-button">Admin</button>
    } @else {
      <span data-testid="no-access">No access</span>
    }
  `);
  const adminButton = byTestId(result, 'admin-button');
  assert.equal(adminButton.structure.conditional, true);
  assert.equal(adminButton.structure.condition, 'user.isAdmin');

  const noAccess = byTestId(result, 'no-access');
  assert.equal(noAccess.structure.conditional, true);
  assert.equal(noAccess.structure.condition, '!(user.isAdmin)');
});

test('marks elements inside @for as repeated with the item alias', async () => {
  const result = await analyze(`
    @for (item of items; track item.id) {
      <li data-testid="row-item">{{ item.name }}</li>
    }
  `);
  const row = byTestId(result, 'row-item');
  assert.equal(row.structure.repeated, true);
  assert.equal(row.structure.repeatAlias, 'item');
  assert.equal(row.structure.trackBy, 'item.id');
  assert.equal(row.text.interpolated, true);
});

test('handles legacy *ngIf and *ngFor microsyntax', async () => {
  const result = await analyze(`
    <div *ngIf="visible" data-testid="legacy-if">shown</div>
    <li *ngFor="let user of users" data-testid="legacy-for">{{ user.name }}</li>
  `);
  const legacyIf = byTestId(result, 'legacy-if');
  assert.equal(legacyIf.structure.conditional, true);
  assert.equal(legacyIf.structure.condition, 'visible');

  const legacyFor = byTestId(result, 'legacy-for');
  assert.equal(legacyFor.structure.repeated, true);
  assert.equal(legacyFor.structure.repeatAlias, 'user');
});

test('associates labels: label[for], wrapping label, mat-label, placeholder, aria-label', async () => {
  const result = await analyze(`
    <label for="email">Email address</label>
    <input id="email" type="email" />

    <label>Remember me <input type="checkbox" data-testid="remember" /></label>

    <mat-form-field>
      <mat-label>Country</mat-label>
      <mat-select formControlName="country"></mat-select>
    </mat-form-field>

    <input placeholder="Search..." data-testid="search" />
    <button aria-label="Close dialog" data-testid="close"></button>
  `);

  const email = result.elements.find((e) => e.id === 'email');
  assert.equal(email.labels.labelFor, 'Email address');

  const remember = byTestId(result, 'remember');
  assert.equal(remember.labels.wrappingLabel, 'Remember me');

  const country = result.elements.find((e) => e.tag === 'mat-select');
  assert.equal(country.labels.matLabel, 'Country');
  assert.equal(country.widget, 'matSelect');
  assert.equal(country.form.formControlName, 'country');

  assert.equal(byTestId(result, 'search').labels.placeholder, 'Search...');
  assert.equal(byTestId(result, 'close').aria.label, 'Close dialog');
});

test('tracks forms: formGroup, controls in template order, ngSubmit handler', async () => {
  const result = await analyze(`
    <form [formGroup]="checkoutForm" (ngSubmit)="onSubmit()">
      <input formControlName="email" type="email" />
      <mat-checkbox formControlName="subscribe"></mat-checkbox>
      <button type="submit">Order</button>
    </form>
  `);
  assert.equal(result.forms.length, 1);
  const form = result.forms[0];
  assert.equal(form.formGroup, 'checkoutForm');
  assert.equal(form.submitHandler, 'onSubmit');
  assert.deepEqual(form.controls.map((c) => c.name), ['email', 'subscribe']);
  assert.equal(form.controls[1].widget, 'matCheckbox');

  const email = result.elements.find((e) => e.form.formControlName === 'email');
  assert.equal(email.form.formGroup, 'checkoutForm');
  assert.ok(email.ancestry.landmark);
  assert.equal(email.ancestry.landmark.label, 'form(checkoutForm)');
});

test('classifies Material widgets and mat-table columns', async () => {
  const result = await analyze(`
    <mat-slide-toggle data-testid="dark-mode"></mat-slide-toggle>
    <input [matDatepicker]="picker" data-testid="due-date" />
    <button [matMenuTriggerFor]="menu" data-testid="more">More</button>

    <table mat-table [dataSource]="rows" data-testid="users-table">
      <ng-container matColumnDef="name">
        <th mat-header-cell *matHeaderCellDef>Name</th>
        <td mat-cell *matCellDef="let row">{{ row.name }}</td>
      </ng-container>
      <ng-container matColumnDef="status">
        <th mat-header-cell *matHeaderCellDef>Status</th>
        <td mat-cell *matCellDef="let row">{{ row.status }}</td>
      </ng-container>
    </table>
  `);

  assert.equal(byTestId(result, 'dark-mode').widget, 'matSlideToggle');
  assert.equal(byTestId(result, 'due-date').widget, 'matDatepicker');
  assert.equal(byTestId(result, 'more').widget, 'matMenuTrigger');

  const table = byTestId(result, 'users-table');
  assert.equal(table.widget, 'matTable');
  assert.equal(table.table.isMatTable, true);
  assert.deepEqual(table.table.columns, [
    { id: 'name', headerText: 'Name' },
    { id: 'status', headerText: 'Status' },
  ]);
});

test('collects child component candidates with structural context', async () => {
  const result = await analyze(`
    <app-filter-bar></app-filter-bar>
    @for (kpi of kpis; track kpi.id) {
      <app-kpi-card [kpi]="kpi"></app-kpi-card>
    }
    @if (showBanner) {
      <app-banner></app-banner>
    }
  `);
  const tags = result.childCandidates.map((c) => c.tag);
  assert.ok(tags.includes('app-filter-bar'));
  assert.ok(tags.includes('app-kpi-card'));
  assert.ok(tags.includes('app-banner'));

  const kpi = result.childCandidates.find((c) => c.tag === 'app-kpi-card');
  assert.equal(kpi.repeated, true);
  const banner = result.childCandidates.find((c) => c.tag === 'app-banner');
  assert.equal(banner.conditional, true);
});

test('landmark + heading ancestry disambiguates duplicated elements', async () => {
  const result = await analyze(`
    <section data-testid="shipping">
      <h2>Shipping</h2>
      <button (click)="save()">Save</button>
    </section>
    <section data-testid="billing">
      <h2>Billing</h2>
      <button (click)="save()">Save</button>
    </section>
  `);
  const buttons = result.elements.filter((e) => e.tag === 'button');
  assert.equal(buttons.length, 2);
  assert.equal(buttons[0].ancestry.landmark.testId, 'shipping');
  assert.equal(buttons[0].ancestry.headingText, 'Shipping');
  assert.equal(buttons[1].ancestry.landmark.testId, 'billing');
  assert.equal(buttons[1].ancestry.headingText, 'Billing');
});

test('routerLink and two-way bindings are captured', async () => {
  const result = await analyze(`
    <a routerLink="/settings" data-testid="settings-link">Settings</a>
    <input [(ngModel)]="query" data-testid="query" />
  `);
  const link = byTestId(result, 'settings-link');
  assert.equal(link.isRouterLink, true);
  assert.equal(link.routerLinkValue, '/settings');

  const query = byTestId(result, 'query');
  assert.ok(query.twoWay.includes('ngModel'));
  assert.equal(query.form.ngModel, 'query');
});

test('plain divs without facts are not emitted; ng-content hosts are', async () => {
  const result = await analyze(`
    <div><div class="wrapper"><p>Some text</p></div></div>
    <div class="card-body"><ng-content></ng-content></div>
  `);
  assert.ok(!result.elements.some((e) => e.tag === 'div' && !e.hasNgContent));
  assert.ok(result.elements.some((e) => e.tag === 'div' && e.hasNgContent));
  assert.ok(result.elements.some((e) => e.tag === 'p' && e.text.value === 'Some text'));
});

test('reports parse errors as a non-ok result', async () => {
  const compiler = await getCompiler();
  const result = analyzeTemplate(compiler, '@if (broken {', 'broken.html');
  assert.equal(result.ok, false);
  assert.ok(result.errors.length > 0);
});
