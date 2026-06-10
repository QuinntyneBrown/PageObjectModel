'use strict';

// analyzeProject orchestration: component scan -> template analysis -> route
// resolution -> child-component matching, assembled into the schemaVersion 1
// response consumed by the .NET host (AstProjectAnalyzer). The response shape
// is a frozen contract — additive changes only.

const fs = require('fs');
const path = require('path');
const ts = require('typescript');
const { collectTsFiles, libraryForTag } = require('./util');
const { loadCompiler } = require('./compiler-loader');
const { scanSourceFile } = require('./project-scanner');
const { analyzeTemplate } = require('./template-visitor');
const { resolveRoutes } = require('./route-resolver');
const { createPathResolver } = require('./tsconfig-paths');

const SCHEMA_VERSION = 1;
const MAX_FILES = 20000;

async function analyzeProject(params) {
  const root = params && typeof params.root === 'string' ? path.resolve(params.root) : null;
  if (!root || !fs.existsSync(root)) {
    throw new Error('analyzeProject: params.root not found: ' + (params && params.root));
  }
  const projects = Array.isArray(params.projects) && params.projects.length > 0
    ? params.projects
    : [{ name: path.basename(root), sourceRoot: root, prefix: null }];
  const includeTemplateContent = !!(params.options && params.options.includeTemplateContent);

  const warnings = [];
  const compilerInfo = await loadCompiler(root);
  if (!compilerInfo) {
    warnings.push('@angular/compiler not found under ' + root +
      ' — template analysis skipped (host falls back to regex template parsing)');
  }

  // Shared parsed-file cache: workspace libs referenced by several projects parse once.
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
        const sourceFile = ts.createSourceFile(key, content, ts.ScriptTarget.Latest, true);
        entry = { sourceFile, content, hasParseErrors: (sourceFile.parseDiagnostics || []).length > 0 };
      }
    } catch (e) {
      entry = null;
    }
    fileCache.set(key, entry);
    return entry;
  };
  const pathResolver = createPathResolver(root);

  const projectResults = [];
  const allComponents = [];

  for (const project of projects) {
    const projectWarnings = [];
    const sourceRoot = project.sourceRoot ? path.resolve(project.sourceRoot) : root;
    const files = [];
    collectTsFiles(sourceRoot, files);
    if (files.length > MAX_FILES) {
      projectWarnings.push('project ' + project.name + ' has ' + files.length +
        ' source files; analysis capped at ' + MAX_FILES);
      files.length = MAX_FILES;
    }

    const components = [];
    for (const file of files) {
      const loaded = loadFile(file);
      if (!loaded) {
        continue;
      }
      if (loaded.hasParseErrors) {
        const diag = loaded.sourceFile.parseDiagnostics[0];
        projectWarnings.push('skipped ' + file + ': ' +
          ts.flattenDiagnosticMessageText(diag.messageText, ' '));
        continue;
      }
      components.push(...scanSourceFile(loaded.sourceFile, file, root, projectWarnings));
    }

    for (const component of components) {
      attachTemplateAnalysis(component, compilerInfo, includeTemplateContent, projectWarnings);
    }

    const routes = resolveRoutes({
      root,
      sourceRoot,
      loadFile,
      pathResolver,
      warnings: projectWarnings,
    });

    projectResults.push({ name: project.name || path.basename(sourceRoot), components, routes, warnings: projectWarnings });
    allComponents.push(...components);
  }

  // Child-component matching needs the full cross-project selector index.
  const selectorIndex = buildSelectorIndex(allComponents);
  const prefixes = projects.map((p) => p.prefix).filter((p) => typeof p === 'string' && p.length > 0);
  for (const component of allComponents) {
    component.childComponents = matchChildComponents(component._childCandidates || [], selectorIndex, prefixes);
    delete component._childCandidates;
    delete component.inlineTemplate;
    delete component.inlineTemplateUnparseable;
  }

  return {
    schemaVersion: SCHEMA_VERSION,
    engine: {
      node: process.versions.node,
      typescript: ts.version,
      angularCompiler: compilerInfo ? compilerInfo.version : null,
      compilerSource: compilerInfo ? compilerInfo.source : null,
    },
    projects: projectResults,
    warnings,
  };
}

function attachTemplateAnalysis(component, compilerInfo, includeTemplateContent, warnings) {
  let templateText = null;
  component.templateParsed = false;
  component.templateErrors = [];
  component.template = null;
  component.templateContent = null;
  component._childCandidates = [];

  if (component.templateSource === 'external' && component.templateUrl) {
    try {
      templateText = fs.readFileSync(component.templateUrl, 'utf8');
    } catch (e) {
      component.templateErrors.push('template file not found: ' + component.templateUrl);
      warnings.push(component.filePath + ': template file not found: ' + component.templateUrl);
      return;
    }
  } else if (component.templateSource === 'inline') {
    if (component.inlineTemplateUnparseable) {
      component.templateErrors.push('inline template uses ${...} substitution');
      return;
    }
    templateText = component.inlineTemplate;
  } else {
    return; // no template at all
  }

  if (includeTemplateContent) {
    component.templateContent = templateText;
  }
  if (!compilerInfo) {
    return; // engine-level warning already covers the regex fallback
  }

  const result = analyzeTemplate(compilerInfo.compiler, templateText, component.templateUrl || component.filePath);
  if (!result.ok) {
    component.templateErrors.push(...result.errors);
    warnings.push(component.filePath + ': template analysis failed (' +
      (result.errors[0] || 'unknown') + ') — host falls back to regex for this component');
    return;
  }

  component.templateParsed = true;
  component.template = {
    elements: result.elements,
    forms: result.forms,
  };
  // Child-component matching happens after the cross-project selector index
  // exists; the matched list lands on component.childComponents.
  component._childCandidates = result.childCandidates;
}

// --- child component matching ------------------------------------------------------

/** Parses component selectors into matchers: tag, tag[attr], [attr] (comma lists already split). */
function buildSelectorIndex(components) {
  const matchers = [];
  for (const component of components) {
    for (const selector of component.selectors || []) {
      const parsed = parseSelectorPart(selector);
      if (parsed) {
        matchers.push({ ...parsed, component });
      }
    }
  }
  return matchers;
}

function parseSelectorPart(selector) {
  const match = /^([\w-]+)?((?:\[[^\]]+\])*)$/.exec(selector.trim());
  if (!match || (!match[1] && !match[2])) {
    return null;
  }
  const attrs = [];
  const attrText = match[2] || '';
  const attrRegex = /\[([^\]=]+)(?:=[^\]]*)?\]/g;
  let attrMatch;
  while ((attrMatch = attrRegex.exec(attrText)) !== null) {
    attrs.push(attrMatch[1].trim());
  }
  return { tag: match[1] ? match[1].toLowerCase() : null, attrs };
}

function matchChildComponents(candidates, selectorIndex, prefixes) {
  /** @type {Map<string, object>} */
  const byTag = new Map();
  for (const candidate of candidates) {
    const tag = candidate.tag.toLowerCase();
    let entry = byTag.get(tag);
    if (!entry) {
      const matched = selectorIndex.find((m) => selectorMatches(m, tag, candidate.attrNames));
      const library = matched ? null : libraryForTag(tag);
      const isPrefixed = prefixes.some((p) => tag.startsWith(p.toLowerCase() + '-'));
      if (!matched && !library && !isPrefixed) {
        continue; // unknown custom tag — appears as a generic element selector only
      }
      entry = {
        selector: tag,
        componentClassName: matched ? matched.component.className : null,
        componentFilePath: matched ? matched.component.filePath : null,
        count: 0,
        conditional: false,
        repeated: false,
        library,
      };
      byTag.set(tag, entry);
    }
    entry.count += 1;
    entry.conditional = entry.conditional || candidate.conditional;
    entry.repeated = entry.repeated || candidate.repeated;
  }
  return Array.from(byTag.values());
}

function selectorMatches(matcher, tag, attrNames) {
  if (matcher.tag && matcher.tag !== tag) {
    return false;
  }
  if (!matcher.tag && matcher.attrs.length === 0) {
    return false;
  }
  for (const attr of matcher.attrs) {
    if (!attrNames.some((name) => name === attr)) {
      return false;
    }
  }
  return true;
}

module.exports = { analyzeProject };
