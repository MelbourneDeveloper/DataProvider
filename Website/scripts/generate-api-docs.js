#!/usr/bin/env node

/**
 * Generate API documentation for DataProvider packages.
 * Extracts content from DocFX YAML and wraps it in site templates.
 */

import fs from 'fs';
import path from 'path';
import { fileURLToPath } from 'url';
import yaml from 'js-yaml';

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);

const WEBSITE_DIR = path.dirname(__dirname);
const DOCFX_DIR = path.join(WEBSITE_DIR, 'docfx');
const API_OUTPUT_DIR = path.join(WEBSITE_DIR, 'src', 'apidocs');
const DOCFX_API_DIR = path.join(DOCFX_DIR, 'api');

const ensureDir = (dir) => fs.mkdirSync(dir, { recursive: true });
const cleanDir = (dir) => {
  fs.existsSync(dir) && fs.rmSync(dir, { recursive: true });
  ensureDir(dir);
};

const escapeYaml = (str) => str ? str.replace(/"/g, '\\"').replace(/\n/g, ' ') : '';

// Parse DocFX YAML file
const parseYamlFile = (filePath) => {
  const content = fs.readFileSync(filePath, 'utf-8');
  // DocFX uses --- separators for multiple documents
  const docs = content.split(/^---$/m).filter(d => d.trim());
  return docs.map(d => {
    try {
      return yaml.load(d);
    } catch {
      return null;
    }
  }).filter(Boolean);
};

// Convert DocFX type to readable string
const typeToString = (type) => {
  if (!type) return '';
  if (typeof type === 'string') return type;
  if (type.uid) return type.uid.split('.').pop();
  return '';
};

// Generate markdown for a namespace
const generateNamespaceMd = (ns, items) => {
  const classes = items.filter(i => i.type === 'Class');
  const interfaces = items.filter(i => i.type === 'Interface');
  const enums = items.filter(i => i.type === 'Enum');
  const structs = items.filter(i => i.type === 'Struct');

  let content = '';

  if (classes.length) {
    content += `\n## Classes\n\n`;
    content += `<dl>\n`;
    classes.forEach(c => {
      const name = c.name || c.uid?.split('.').pop() || 'Unknown';
      const summary = c.summary?.replace(/<[^>]*>/g, '') || '';
      content += `<dt><a href="/apidocs/${ns.uid}/${name}/">${name}</a></dt>\n`;
      content += `<dd>${summary}</dd>\n`;
    });
    content += `</dl>\n`;
  }

  if (structs.length) {
    content += `\n## Structs\n\n`;
    content += `<dl>\n`;
    structs.forEach(s => {
      const name = s.name || s.uid?.split('.').pop() || 'Unknown';
      const summary = s.summary?.replace(/<[^>]*>/g, '') || '';
      content += `<dt><a href="/apidocs/${ns.uid}/${name}/">${name}</a></dt>\n`;
      content += `<dd>${summary}</dd>\n`;
    });
    content += `</dl>\n`;
  }

  if (enums.length) {
    content += `\n## Enums\n\n`;
    content += `<dl>\n`;
    enums.forEach(e => {
      const name = e.name || e.uid?.split('.').pop() || 'Unknown';
      const summary = e.summary?.replace(/<[^>]*>/g, '') || '';
      content += `<dt><a href="/apidocs/${ns.uid}/${name}/">${name}</a></dt>\n`;
      content += `<dd>${summary}</dd>\n`;
    });
    content += `</dl>\n`;
  }

  return content;
};

// Generate markdown for a class/type
const generateTypeMd = (item, children) => {
  let content = '';

  if (item.summary) {
    content += `${item.summary.replace(/<[^>]*>/g, '')}\n\n`;
  }

  const props = children.filter(c => c.type === 'Property');
  const methods = children.filter(c => c.type === 'Method');
  const fields = children.filter(c => c.type === 'Field');
  const constructors = children.filter(c => c.type === 'Constructor');

  if (constructors.length) {
    content += `## Constructors\n\n`;
    constructors.forEach(c => {
      const name = c.name || 'Constructor';
      const summary = c.summary?.replace(/<[^>]*>/g, '') || '';
      content += `### ${name}\n\n`;
      if (c.syntax?.content) content += `\`\`\`csharp\n${c.syntax.content}\n\`\`\`\n\n`;
      if (summary) content += `${summary}\n\n`;
    });
  }

  if (props.length) {
    content += `## Properties\n\n`;
    content += `<dl>\n`;
    props.forEach(p => {
      const name = p.name || 'Property';
      const summary = p.summary?.replace(/<[^>]*>/g, '') || '';
      const returnType = p.syntax?.return?.type || '';
      content += `<dt><strong>${name}</strong> : ${typeToString(returnType)}</dt>\n`;
      content += `<dd>${summary}</dd>\n`;
    });
    content += `</dl>\n\n`;
  }

  if (methods.length) {
    content += `## Methods\n\n`;
    methods.forEach(m => {
      const name = m.name || 'Method';
      const summary = m.summary?.replace(/<[^>]*>/g, '') || '';
      content += `### ${name}\n\n`;
      if (m.syntax?.content) content += `\`\`\`csharp\n${m.syntax.content}\n\`\`\`\n\n`;
      if (summary) content += `${summary}\n\n`;
      if (m.syntax?.parameters?.length) {
        content += `**Parameters:**\n\n`;
        m.syntax.parameters.forEach(p => {
          content += `- \`${p.id}\` (${typeToString(p.type)}): ${p.description?.replace(/<[^>]*>/g, '') || ''}\n`;
        });
        content += `\n`;
      }
      if (m.syntax?.return?.type) {
        content += `**Returns:** ${typeToString(m.syntax.return.type)}\n\n`;
      }
    });
  }

  if (fields.length) {
    content += `## Fields\n\n`;
    content += `<dl>\n`;
    fields.forEach(f => {
      const name = f.name || 'Field';
      const summary = f.summary?.replace(/<[^>]*>/g, '') || '';
      content += `<dt><strong>${name}</strong></dt>\n`;
      content += `<dd>${summary}</dd>\n`;
    });
    content += `</dl>\n\n`;
  }

  return content;
};

// Create markdown file with frontmatter
const createMdFile = (outputPath, title, description, content, namespace = null) => {
  const md = `---
layout: layouts/api.njk
title: "${escapeYaml(title)}"
description: "${escapeYaml(description)}"
${namespace ? `namespace: "${namespace}"` : ''}
---

${content}
`;
  ensureDir(path.dirname(outputPath));
  fs.writeFileSync(outputPath, md);
};

// Process DocFX YAML files
const processDocFxYaml = () => {
  console.log('Processing DocFX YAML files...');

  if (!fs.existsSync(DOCFX_API_DIR)) {
    console.error(`DocFX API directory not found: ${DOCFX_API_DIR}`);
    console.log('Run "docfx metadata docfx.json" first in the docfx folder');
    process.exit(1);
  }

  const ymlFiles = fs.readdirSync(DOCFX_API_DIR).filter(f => f.endsWith('.yml') && f !== 'toc.yml');

  // Build a map of all items by UID
  const allItems = new Map();
  const namespaces = [];

  ymlFiles.forEach(file => {
    const docs = parseYamlFile(path.join(DOCFX_API_DIR, file));
    docs.forEach(doc => {
      if (doc?.items) {
        doc.items.forEach(item => {
          allItems.set(item.uid, item);
          if (item.type === 'Namespace') {
            namespaces.push(item);
          }
        });
      }
    });
  });

  console.log(`Found ${namespaces.length} namespaces, ${allItems.size} total items`);

  // Generate namespace pages
  namespaces.forEach(ns => {
    const nsItems = Array.from(allItems.values()).filter(
      i => i.namespace === ns.uid && i.type !== 'Namespace'
    );

    const content = generateNamespaceMd(ns, nsItems);
    const outputPath = path.join(API_OUTPUT_DIR, ns.uid, 'index.md');
    createMdFile(outputPath, ns.uid, `API reference for ${ns.uid} namespace`, content, ns.uid);
    console.log(`  Generated: ${ns.uid}/index.md`);

    // Generate type pages
    nsItems.filter(i => ['Class', 'Struct', 'Interface', 'Enum'].includes(i.type)).forEach(item => {
      const children = (item.children || []).map(uid => allItems.get(uid)).filter(Boolean);
      const typeContent = generateTypeMd(item, children);
      const typeName = item.name || item.uid.split('.').pop();
      const typeOutput = path.join(API_OUTPUT_DIR, ns.uid, typeName, 'index.md');
      createMdFile(typeOutput, typeName, item.summary?.replace(/<[^>]*>/g, '') || '', typeContent, ns.uid);
    });
  });

  // Create main index
  const indexContent = `
<p>Auto-generated API documentation from XML documentation comments.</p>

## Namespaces

<div class="features-grid">
${namespaces.map(ns => `  <a href="/apidocs/${ns.uid}/" class="card">
    <h3>${ns.uid}</h3>
    <p>${ns.summary?.replace(/<[^>]*>/g, '') || 'API reference'}</p>
  </a>`).join('\n')}
</div>

<p style="margin-top: var(--space-8);"><a href="https://lql.dev">LQL Documentation →</a></p>
`;

  createMdFile(
    path.join(API_OUTPUT_DIR, 'index.md'),
    'API Reference',
    'Complete API reference for DataProvider',
    indexContent
  );

  console.log('\n=== API documentation generation complete ===');
  console.log(`Output: ${API_OUTPUT_DIR}`);
};

// Main
cleanDir(API_OUTPUT_DIR);
processDocFxYaml();
