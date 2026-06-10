'use strict';

// Shared helpers for the sidecar analysis modules.

const fs = require('fs');
const path = require('path');

/**
 * Recursively collects analyzable TypeScript files, skipping node_modules, dist,
 * hidden directories, spec files, and declaration files.
 */
function collectTsFiles(dir, out) {
  let entries;
  try {
    entries = fs.readdirSync(dir, { withFileTypes: true });
  } catch (e) {
    return;
  }
  for (const entry of entries) {
    const full = path.join(dir, entry.name);
    if (entry.isDirectory()) {
      if (entry.name === 'node_modules' || entry.name === 'dist' || entry.name.startsWith('.')) {
        continue;
      }
      collectTsFiles(full, out);
    } else if (entry.isFile()) {
      const n = entry.name;
      if (n.endsWith('.ts') && !n.endsWith('.spec.ts') && !n.endsWith('.d.ts')) {
        out.push(full);
      }
    }
  }
}

function collapse(text) {
  return String(text).replace(/\s+/g, ' ').trim();
}

function lastTypeSegment(name) {
  const idx = name.lastIndexOf('.');
  return idx >= 0 ? name.slice(idx + 1) : name;
}

function stripGenerics(name) {
  const idx = name.indexOf('<');
  return idx >= 0 ? name.slice(0, idx).trim() : name.trim();
}

// Standard HTML + SVG + Angular built-in tags. Anything outside this set is a
// candidate child component (or a known library element such as mat-*).
const STANDARD_TAGS = new Set([
  // document / sectioning
  'html', 'head', 'body', 'main', 'header', 'footer', 'nav', 'aside', 'section', 'article',
  'address', 'h1', 'h2', 'h3', 'h4', 'h5', 'h6', 'hgroup', 'search',
  // grouping
  'div', 'p', 'hr', 'pre', 'blockquote', 'ol', 'ul', 'li', 'dl', 'dt', 'dd',
  'figure', 'figcaption', 'menu',
  // text-level
  'a', 'em', 'strong', 'small', 's', 'cite', 'q', 'dfn', 'abbr', 'ruby', 'rt', 'rp',
  'data', 'time', 'code', 'var', 'samp', 'kbd', 'sub', 'sup', 'i', 'b', 'u', 'mark',
  'bdi', 'bdo', 'span', 'br', 'wbr',
  // embedded
  'img', 'iframe', 'embed', 'object', 'param', 'video', 'audio', 'source', 'track',
  'map', 'area', 'picture', 'canvas', 'svg', 'math',
  // svg children (common)
  'path', 'g', 'circle', 'rect', 'line', 'polyline', 'polygon', 'ellipse', 'text',
  'defs', 'use', 'symbol', 'clippath', 'lineargradient', 'radialgradient', 'stop',
  // tables
  'table', 'caption', 'colgroup', 'col', 'tbody', 'thead', 'tfoot', 'tr', 'td', 'th',
  // forms
  'form', 'label', 'input', 'button', 'select', 'datalist', 'optgroup', 'option',
  'textarea', 'output', 'progress', 'meter', 'fieldset', 'legend',
  // interactive
  'details', 'summary', 'dialog',
  // scripting / metadata
  'script', 'noscript', 'template', 'slot', 'style', 'link', 'meta', 'title', 'base',
  // Angular built-ins
  'ng-container', 'ng-content', 'ng-template',
]);

function isStandardTag(tag) {
  return STANDARD_TAGS.has(String(tag).toLowerCase());
}

// UI library prefixes that get widget-aware treatment (Material + CDK only).
const LIBRARY_PREFIXES = [
  { prefix: 'mat-', library: '@angular/material' },
  { prefix: 'cdk-', library: '@angular/cdk' },
];

function libraryForTag(tag) {
  const lower = String(tag).toLowerCase();
  for (const entry of LIBRARY_PREFIXES) {
    if (lower.startsWith(entry.prefix)) {
      return entry.library;
    }
  }
  return null;
}

module.exports = {
  collectTsFiles,
  collapse,
  lastTypeSegment,
  stripGenerics,
  isStandardTag,
  libraryForTag,
};
