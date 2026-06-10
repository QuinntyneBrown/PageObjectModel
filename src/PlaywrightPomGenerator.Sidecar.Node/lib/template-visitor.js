'use strict';

// Template analysis over the @angular/compiler R3 AST. Walks parseTemplate()
// output with a structural context stack (conditional / repeated / switch /
// projected + landmark ancestry) and emits raw element facts, form summaries,
// and child-component tag candidates. The compiler instance is the analyzed
// app's own, so its version always matches the app's template syntax.
//
// Never use bare instanceof against classes that may not exist in older
// compilers — all checks go through guarded type lookups or duck typing.

const { isStandardTag } = require('./util');

const INTERACTIVE_TAGS = new Set(['button', 'a', 'input', 'select', 'textarea', 'option', 'summary']);
const TEXT_TAGS = new Set([
  'h1', 'h2', 'h3', 'h4', 'h5', 'h6', 'p', 'span', 'label', 'strong', 'em', 'small', 'mark',
  'sub', 'sup', 'blockquote', 'cite', 'q', 'code', 'pre', 'abbr', 'address', 'time', 'figcaption',
]);
const LANDMARK_TAGS = new Set([
  'form', 'nav', 'header', 'footer', 'main', 'aside', 'section', 'article', 'dialog', 'fieldset',
  'mat-card', 'mat-dialog-content', 'mat-tab', 'mat-expansion-panel', 'mat-toolbar', 'mat-sidenav',
]);
const HEADING_TAGS = new Set(['h1', 'h2', 'h3', 'h4', 'h5', 'h6', 'mat-card-title']);

/**
 * @param {object} compiler  the loaded @angular/compiler module
 * @param {string} text      template source
 * @param {string} templatePath
 * @returns {{ok: boolean, errors: string[], elements: object[], forms: object[], childCandidates: object[]}}
 */
function analyzeTemplate(compiler, text, templatePath) {
  let parsed;
  try {
    parsed = compiler.parseTemplate(text, templatePath, { preserveWhitespaces: false });
  } catch (e) {
    return failure(['parseTemplate threw: ' + ((e && e.message) || String(e))]);
  }
  if (!parsed || !Array.isArray(parsed.nodes)) {
    return failure(['parseTemplate returned no nodes']);
  }
  if (parsed.errors && parsed.errors.length > 0) {
    return failure(parsed.errors.slice(0, 5).map((e) => String(e.msg || e.message || e)));
  }

  const T = {
    Element: compiler.TmplAstElement,
    Template: compiler.TmplAstTemplate,
    Text: compiler.TmplAstText,
    BoundText: compiler.TmplAstBoundText,
    Content: compiler.TmplAstContent,
    IfBlock: compiler.TmplAstIfBlock,
    ForLoopBlock: compiler.TmplAstForLoopBlock,
    SwitchBlock: compiler.TmplAstSwitchBlock,
    DeferredBlock: compiler.TmplAstDeferredBlock,
  };

  const state = {
    T,
    elements: [],
    forms: [],
    childCandidates: [],
    labelForIndex: new Map(), // for-id -> label text
    idTextIndex: new Map(),   // id -> static text (aria-labelledby resolution)
  };

  try {
    buildLabelIndexes(parsed.nodes, state);
    walkNodes(parsed.nodes, rootContext(), state);
  } catch (e) {
    return failure(['template walk failed: ' + ((e && e.message) || String(e))]);
  }

  return {
    ok: true,
    errors: [],
    elements: state.elements,
    forms: state.forms,
    childCandidates: state.childCandidates,
  };
}

function failure(errors) {
  return { ok: false, errors, elements: [], forms: [], childCandidates: [] };
}

function rootContext() {
  return {
    conditional: false,
    condition: null,
    repeated: false,
    repeatAlias: null,
    trackBy: null,
    switchCase: null,
    projected: false,
    landmark: null,
    headingHolder: { text: null },
    matLabel: null,
    wrappingLabel: null,
    currentForm: null,
    formGroupName: null,
    depth: 0,
  };
}

// --- node type tests (guarded instanceof + duck typing) -----------------------

function is(cls, node) {
  return !!cls && node instanceof cls;
}

function isElementNode(T, node) {
  if (is(T.Element, node)) {
    return true;
  }
  // Duck type: has a tag name + children + attribute collections, but no templateAttrs.
  return !!node && typeof node.name === 'string' && Array.isArray(node.children)
    && Array.isArray(node.attributes) && !Array.isArray(node.templateAttrs);
}

function isTemplateNode(T, node) {
  if (is(T.Template, node)) {
    return true;
  }
  return !!node && Array.isArray(node.templateAttrs);
}

function isTextNode(T, node) {
  if (is(T.Text, node)) {
    return true;
  }
  return !!node && typeof node.value === 'string' && !Array.isArray(node.children);
}

function isBoundTextNode(T, node) {
  if (is(T.BoundText, node)) {
    return true;
  }
  return !!node && node.value !== null && typeof node.value === 'object' && !Array.isArray(node.children)
    && !Array.isArray(node.attributes);
}

function isContentNode(T, node) {
  if (is(T.Content, node)) {
    return true;
  }
  return !!node && typeof node.selector === 'string' && typeof node.name !== 'string';
}

// --- pass 1: label indexes ----------------------------------------------------

function buildLabelIndexes(nodes, state) {
  const T = state.T;
  visit(nodes);

  function visit(list) {
    for (const node of list || []) {
      if (isElementNode(T, node)) {
        const attrs = staticAttrs(node);
        if (node.name === 'label' && (attrs['for'] || attrs['htmlFor'])) {
          const text = collectStaticText(T, node);
          if (text) {
            state.labelForIndex.set(attrs['for'] || attrs['htmlFor'], text);
          }
        }
        if (attrs['id']) {
          const text = collectStaticText(T, node);
          if (text) {
            state.idTextIndex.set(attrs['id'], text);
          }
        }
        visit(node.children);
      } else if (isTemplateNode(T, node)) {
        visit(node.children);
      } else if (is(T.IfBlock, node)) {
        for (const branch of node.branches || []) {
          visit(branch.children);
        }
      } else if (is(T.ForLoopBlock, node)) {
        visit(node.children);
        if (node.empty) {
          visit(node.empty.children);
        }
      } else if (is(T.SwitchBlock, node)) {
        for (const c of node.cases || []) {
          visit(c.children);
        }
      } else if (is(T.DeferredBlock, node)) {
        visit(node.children);
        for (const sub of ['placeholder', 'loading', 'error']) {
          if (node[sub]) {
            visit(node[sub].children);
          }
        }
      }
    }
  }
}

// --- pass 2: main walk ----------------------------------------------------------

function walkNodes(nodes, ctx, state) {
  const T = state.T;
  for (const node of nodes || []) {
    if (isElementNode(T, node)) {
      visitElement(node, ctx, state);
    } else if (isTemplateNode(T, node)) {
      visitStructuralTemplate(node, ctx, state);
    } else if (is(T.IfBlock, node)) {
      const branches = node.branches || [];
      const firstCondition = branches.length > 0 && branches[0].expression ? sourceOf(branches[0].expression) : null;
      for (const branch of branches) {
        const condition = branch.expression ? sourceOf(branch.expression) : firstCondition ? '!(' + firstCondition + ')' : 'else';
        walkNodes(branch.children, { ...ctx, conditional: true, condition }, state);
      }
    } else if (is(T.ForLoopBlock, node)) {
      const alias = node.item && node.item.name ? node.item.name : null;
      const trackBy = node.trackBy ? sourceOf(node.trackBy) : null;
      walkNodes(node.children, { ...ctx, repeated: true, repeatAlias: alias, trackBy }, state);
      if (node.empty) {
        walkNodes(node.empty.children, { ...ctx, conditional: true, condition: 'empty @for' }, state);
      }
    } else if (is(T.SwitchBlock, node)) {
      const switchExpr = node.expression ? sourceOf(node.expression) : '';
      for (const switchCase of node.cases || []) {
        const caseText = switchCase.expression ? switchExpr + ' === ' + sourceOf(switchCase.expression) : 'default';
        walkNodes(switchCase.children, { ...ctx, conditional: true, switchCase: caseText, condition: caseText }, state);
      }
    } else if (is(T.DeferredBlock, node)) {
      walkNodes(node.children, ctx, state);
      for (const sub of ['placeholder', 'loading', 'error']) {
        if (node[sub]) {
          walkNodes(node[sub].children, { ...ctx, conditional: true, condition: '@defer ' + sub }, state);
        }
      }
    }
    // Text / BoundText / Content handled by their parent element.
  }
}

/** *ngIf / *ngFor / *ngSwitchCase desugar into a Template wrapper around the element. */
function visitStructuralTemplate(node, ctx, state) {
  let next = ctx;
  for (const attr of node.templateAttrs || []) {
    const name = attr.name;
    if (name === 'ngIf') {
      next = { ...next, conditional: true, condition: boundValueSource(attr) };
    } else if (name === 'ngFor' || name === 'ngForOf') {
      let alias = null;
      for (const variable of node.variables || []) {
        if (variable.value === '$implicit') {
          alias = variable.name;
        }
      }
      next = { ...next, repeated: true, repeatAlias: alias };
    } else if (name === 'ngForTrackBy') {
      next = { ...next, trackBy: boundValueSource(attr) };
    } else if (name === 'ngSwitchCase') {
      const caseText = 'case ' + (boundValueSource(attr) || '');
      next = { ...next, conditional: true, switchCase: caseText, condition: caseText };
    } else if (name === 'ngSwitchDefault') {
      next = { ...next, conditional: true, switchCase: 'default', condition: 'default' };
    }
  }
  // ng-template without structural sugar = projected/deferred content.
  if (next === ctx && node.tagName === 'ng-template') {
    next = { ...ctx, projected: true };
  }
  walkNodes(node.children, next, state);
}

function visitElement(node, ctx, state) {
  const T = state.T;
  const tag = node.name;
  const attrs = staticAttrs(node);
  const inputs = boundInputs(node);   // name -> source text
  const outputs = boundOutputs(node); // name -> handler source
  const attrNames = Object.keys(attrs).concat(Object.keys(inputs));

  // Child-component candidate (project components + Material/CDK tags).
  if (!isStandardTag(tag)) {
    state.childCandidates.push({
      tag,
      attrNames,
      conditional: ctx.conditional,
      repeated: ctx.repeated,
    });
  }

  // Two-way bindings: x + xChange pairs (covers [(ngModel)] and custom models).
  const twoWay = Object.keys(inputs).filter((name) => Object.prototype.hasOwnProperty.call(outputs, name + 'Change'));

  const text = directText(T, node);
  const widget = classifyWidget(tag, attrs, inputs);
  const formControlName = attrs['formControlName'] || inputs['formControlName'] || null;
  const formGroupBinding = inputs['formGroup'] || null;
  const hasNgContentChild = (node.children || []).some((c) => isContentNode(T, c));

  // Form tracking: a [formGroup] element opens a form scope.
  let form = ctx.currentForm;
  let nextCtx = ctx;
  if (formGroupBinding) {
    form = {
      formGroup: formGroupBinding,
      controls: [],
      submitHandler: simpleHandlerName(outputs['ngSubmit']) || null,
    };
    state.forms.push(form);
    nextCtx = { ...nextCtx, currentForm: form };
  }
  const formGroupName = attrs['formGroupName'] || inputs['formGroupName'] || ctx.formGroupName || null;
  if (attrs['formGroupName'] || inputs['formGroupName']) {
    nextCtx = { ...nextCtx, formGroupName: attrs['formGroupName'] || inputs['formGroupName'] };
  }
  if (formControlName && form) {
    form.controls.push({
      name: formControlName,
      inputType: attrs['type'] || null,
      widget,
      tag,
    });
  }

  // Landmark push.
  const landmark = landmarkFor(T, node, tag, attrs, inputs, formGroupBinding);
  if (landmark) {
    nextCtx = { ...nextCtx, landmark, headingHolder: { text: null } };
  }
  // mat-form-field: associate the contained mat-label with descendants.
  if (tag === 'mat-form-field') {
    const matLabel = findDescendantText(T, node, 'mat-label');
    if (matLabel) {
      nextCtx = { ...nextCtx, matLabel };
    }
  }
  // A <label> without `for` labels its wrapped control.
  if (tag === 'label' && !attrs['for'] && !attrs['htmlFor']) {
    const labelText = collectStaticText(T, node);
    if (labelText) {
      nextCtx = { ...nextCtx, wrappingLabel: labelText };
    }
  }

  // Heading updates the holder so FOLLOWING siblings/descendants see it.
  if (HEADING_TAGS.has(tag) && text.value) {
    ctx.headingHolder.text = text.value;
  }

  const record = buildElementRecord(node, tag, attrs, inputs, outputs, twoWay, text, widget, {
    ctx,
    formControlName,
    formGroupName,
    formGroupBinding,
    form,
    hasNgContentChild,
    state,
  });

  if (shouldEmit(record, tag, hasNgContentChild)) {
    state.elements.push(record);
  }

  nextCtx = { ...nextCtx, depth: ctx.depth + 1 };
  walkNodes(node.children, nextCtx, state);
}

function buildElementRecord(node, tag, attrs, inputs, outputs, twoWay, text, widget, extra) {
  const { ctx, formControlName, formGroupName, formGroupBinding, form, hasNgContentChild, state } = extra;

  const handlers = {};
  for (const [eventName, source] of Object.entries(outputs)) {
    handlers[eventName] = simpleHandlerName(source) || source;
  }

  const labelledBy = attrs['aria-labelledby'] || null;
  const routerLink = attrs['routerLink'] !== undefined ? attrs['routerLink']
    : inputs['routerLink'] !== undefined ? inputs['routerLink'] : null;

  return {
    tag,
    testId: attrs['data-testid'] || null,
    id: attrs['id'] || null,
    classes: attrs['class'] ? attrs['class'].split(/\s+/).filter((c) => c.length > 0) : [],
    role: attrs['role'] || null,
    aria: {
      label: attrs['aria-label'] || null,
      labelledBy,
      labelledByText: labelledBy ? state.idTextIndex.get(labelledBy) || null : null,
      describedBy: attrs['aria-describedby'] || null,
    },
    labels: {
      wrappingLabel: ctx.wrappingLabel || null,
      labelFor: attrs['id'] ? state.labelForIndex.get(attrs['id']) || null : null,
      matLabel: ctx.matLabel || null,
      placeholder: attrs['placeholder'] || null,
    },
    text,
    attributes: attrs,
    events: Object.keys(outputs),
    handlers,
    twoWay,
    form: {
      formControlName,
      formGroupName: formGroupName || null,
      formGroup: form ? form.formGroup : formGroupBinding || null,
      ngModel: inputs['ngModel'] || null,
      inputType: attrs['type'] || null,
    },
    structure: {
      conditional: ctx.conditional,
      condition: ctx.condition,
      repeated: ctx.repeated,
      repeatAlias: ctx.repeatAlias,
      trackBy: ctx.trackBy,
      switchCase: ctx.switchCase,
      projected: ctx.projected,
      depth: ctx.depth,
    },
    ancestry: {
      landmark: ctx.landmark,
      headingText: ctx.headingHolder.text,
    },
    widget,
    isRouterLink: routerLink !== null,
    routerLinkValue: routerLink,
    table: tableFacts(state.T, node, tag, attrs, widget),
    hasNgContent: hasNgContentChild,
    line: sourceLine(node),
  };
}

function shouldEmit(record, tag, hasNgContentChild) {
  const lower = tag.toLowerCase();
  if (INTERACTIVE_TAGS.has(lower)) {
    return true;
  }
  if (record.testId || record.id) {
    return true;
  }
  if (record.role || record.aria.label) {
    return true;
  }
  if (record.events.length > 0 || record.twoWay.length > 0) {
    return true;
  }
  if (record.form.formControlName || record.form.ngModel || record.form.formGroup) {
    return true;
  }
  if (record.isRouterLink) {
    return true;
  }
  if (record.widget || (record.table && record.table.isTable)) {
    return true;
  }
  if (TEXT_TAGS.has(lower) && (record.text.value || record.text.interpolated)) {
    return true;
  }
  if (!isStandardTag(lower)) {
    return true;
  }
  if (hasNgContentChild) {
    return true;
  }
  if ((lower === 'li' || lower === 'td' || lower === 'th') && record.text.value) {
    return true;
  }
  return false;
}

// --- extraction helpers -----------------------------------------------------------

function staticAttrs(node) {
  const attrs = {};
  for (const attr of node.attributes || []) {
    attrs[attr.name] = attr.value;
  }
  return attrs;
}

function boundInputs(node) {
  const inputs = {};
  for (const input of node.inputs || []) {
    inputs[input.name] = boundValueSource(input);
  }
  return inputs;
}

function boundOutputs(node) {
  const outputs = {};
  for (const output of node.outputs || []) {
    outputs[output.name] = output.handler ? sourceOf(output.handler) : '';
  }
  return outputs;
}

function boundValueSource(attr) {
  return attr && attr.value ? sourceOf(attr.value) : null;
}

function sourceOf(astWithSource) {
  if (!astWithSource) {
    return null;
  }
  if (typeof astWithSource === 'string') {
    return astWithSource;
  }
  if (typeof astWithSource.source === 'string') {
    return astWithSource.source.trim();
  }
  return null;
}

function simpleHandlerName(source) {
  if (!source) {
    return null;
  }
  const match = /^\s*(?:this\.)?([\w$]+)\s*\(/.exec(source);
  return match ? match[1] : null;
}

/** Static + interpolation facts from DIRECT children only. */
function directText(T, node) {
  let staticText = '';
  let interpolated = false;
  for (const child of node.children || []) {
    if (isTextNode(T, child)) {
      staticText += child.value;
    } else if (isBoundTextNode(T, child)) {
      interpolated = true;
      // Interpolation static fragments: {{x}} world -> " world".
      try {
        const ast = child.value && child.value.ast;
        if (ast && Array.isArray(ast.strings)) {
          staticText += ast.strings.join(' ');
        }
      } catch (e) {
        // Best effort only.
      }
    }
  }
  const value = staticText.replace(/\s+/g, ' ').trim();
  return { value: value.length > 0 ? value : null, interpolated };
}

/** Whole-subtree static text, skipping mat-icon ligature text. */
function collectStaticText(T, node) {
  let out = '';
  visit(node);
  const value = out.replace(/\s+/g, ' ').trim();
  return value.length > 0 ? value : null;

  function visit(current) {
    for (const child of current.children || []) {
      if (isTextNode(T, child)) {
        out += child.value + ' ';
      } else if (isElementNode(T, child)) {
        if (child.name === 'mat-icon') {
          continue;
        }
        visit(child);
      } else if (isTemplateNode(T, child)) {
        visit(child);
      }
    }
  }
}

function findDescendantText(T, node, tagName) {
  let found = null;
  visit(node);
  return found;

  function visit(current) {
    for (const child of current.children || []) {
      if (found) {
        return;
      }
      if (isElementNode(T, child)) {
        if (child.name === tagName) {
          found = collectStaticText(T, child);
          return;
        }
        visit(child);
      } else if (isTemplateNode(T, child)) {
        visit(child);
      }
    }
  }
}

function landmarkFor(T, node, tag, attrs, inputs, formGroupBinding) {
  const testId = attrs['data-testid'] || null;
  if (testId) {
    return { label: testId, testId, role: null, accessibleName: null, selectorValue: `[data-testid="${testId}"]` };
  }
  if (attrs['id']) {
    return { label: attrs['id'], testId: null, role: null, accessibleName: null, selectorValue: '#' + attrs['id'] };
  }
  if (formGroupBinding) {
    return {
      label: 'form(' + formGroupBinding + ')',
      testId: null,
      role: tag === 'form' ? 'form' : null,
      accessibleName: null,
      selectorValue: tag === 'form' ? 'form' : tag,
    };
  }
  if (LANDMARK_TAGS.has(tag)) {
    let accessibleName = attrs['aria-label'] || null;
    if (!accessibleName && tag === 'fieldset') {
      accessibleName = findDescendantText(T, node, 'legend');
    }
    if (!accessibleName && tag === 'mat-card') {
      accessibleName = findDescendantText(T, node, 'mat-card-title');
    }
    return {
      label: accessibleName || tag,
      testId: null,
      role: roleForLandmarkTag(tag),
      accessibleName,
      selectorValue: tag,
    };
  }
  return null;
}

function roleForLandmarkTag(tag) {
  switch (tag) {
    case 'nav': return 'navigation';
    case 'header': return 'banner';
    case 'footer': return 'contentinfo';
    case 'main': return 'main';
    case 'aside': return 'complementary';
    case 'form': return 'form';
    case 'dialog': return 'dialog';
    case 'section': return 'region';
    case 'fieldset': return 'group';
    default: return null;
  }
}

function classifyWidget(tag, attrs, inputs) {
  const attrHas = (name) => attrs[name] !== undefined || inputs[name] !== undefined;

  // Attribute-based first: the interaction point for datepicker/autocomplete is the input.
  if (attrHas('matDatepicker')) {
    return 'matDatepicker';
  }
  if (attrHas('matAutocomplete')) {
    return 'matAutocomplete';
  }
  if (attrHas('matMenuTriggerFor')) {
    return 'matMenuTrigger';
  }
  if (attrHas('mat-dialog-close') || attrHas('matDialogClose')) {
    return 'matDialogClose';
  }
  if (attrHas('cdkDrag')) {
    return 'cdkDrag';
  }
  if (tag === 'table' && attrHas('mat-table')) {
    return 'matTable';
  }
  if (tag === 'button' || tag === 'a') {
    for (const name of ['mat-button', 'mat-raised-button', 'mat-flat-button', 'mat-stroked-button',
      'mat-icon-button', 'mat-fab', 'mat-mini-fab']) {
      if (attrHas(name)) {
        return 'matButton';
      }
    }
  }
  if (attrHas('matSort') && (tag === 'table' || tag === 'mat-table')) {
    return 'matTable';
  }

  switch (tag) {
    case 'mat-select': return 'matSelect';
    case 'mat-checkbox': return 'matCheckbox';
    case 'mat-radio-group': return 'matRadioGroup';
    case 'mat-radio-button': return 'matRadioButton';
    case 'mat-slide-toggle': return 'matSlideToggle';
    case 'mat-menu': return 'matMenu';
    case 'mat-tab-group': return 'matTabGroup';
    case 'mat-paginator': return 'matPaginator';
    case 'mat-table': return 'matTable';
    case 'mat-form-field': return 'matFormField';
    case 'mat-chip-grid':
    case 'mat-chip-listbox': return 'matChipGrid';
    default: return null;
  }
}

function tableFacts(T, node, tag, attrs, widget) {
  const isMatTable = widget === 'matTable';
  const isTable = isMatTable || tag === 'table';
  if (!isTable) {
    return null;
  }
  const columns = [];
  visit(node);
  return { isTable: true, isMatTable, columns };

  function visit(current) {
    for (const child of current.children || []) {
      if (isElementNode(T, child)) {
        const childAttrs = staticAttrs(child);
        const inputs = boundInputs(child);
        const columnDef = childAttrs['matColumnDef'] || inputs['matColumnDef'];
        if (columnDef) {
          columns.push({ id: columnDef, headerText: headerTextIn(child) });
        }
        visit(child);
      } else if (isTemplateNode(T, child)) {
        visit(child);
      }
    }
  }

  function headerTextIn(container) {
    let header = null;
    look(container);
    return header;

    function look(current) {
      for (const child of current.children || []) {
        if (header) {
          return;
        }
        if (isElementNode(T, child)) {
          const childAttrs = staticAttrs(child);
          if (child.name === 'mat-header-cell' || (child.name === 'th' && childAttrs['mat-header-cell'] !== undefined)) {
            header = collectStaticText(T, child);
            return;
          }
          look(child);
        } else if (isTemplateNode(T, child)) {
          look(child);
        }
      }
    }
  }
}

function sourceLine(node) {
  try {
    if (node.sourceSpan && node.sourceSpan.start && typeof node.sourceSpan.start.line === 'number') {
      return node.sourceSpan.start.line + 1;
    }
  } catch (e) {
    // optional
  }
  return null;
}

module.exports = { analyzeTemplate };
