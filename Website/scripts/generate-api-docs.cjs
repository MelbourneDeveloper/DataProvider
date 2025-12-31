/**
 * Generate API documentation markdown from DocFX YAML output
 * Converts YAML files to markdown with Eleventy frontmatter
 */

const fs = require('fs');
const path = require('path');
const yaml = require('js-yaml');

const DOCFX_API_DIR = path.join(__dirname, '../docfx/api');
const OUTPUT_DIR = path.join(__dirname, '../src/apidocs');

// Ensure output directory exists
if (!fs.existsSync(OUTPUT_DIR)) {
  fs.mkdirSync(OUTPUT_DIR, { recursive: true });
}

function escapeHtml(text) {
  if (!text) return '';
  return text.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;');
}

function cleanMarkdown(text) {
  if (!text) return '';
  return text
    .replace(/<xref href="([^"]+)"[^>]*>([^<]*)<\/xref>/g, '`$2`')
    .replace(/<xref href="([^"]+)"[^>]*\/>/g, (_, uid) => '`' + uid.split('.').pop() + '`')
    .replace(/<see cref="[^"]*">([^<]*)<\/see>/g, '`$1`')
    .replace(/<see cref="[^"]*"\/>/g, '')
    .trim();
}

function formatExample(exampleArray) {
  if (!exampleArray || !Array.isArray(exampleArray) || exampleArray.length === 0) return '';

  let md = '\n## Example\n\n';
  for (const example of exampleArray) {
    // DocFX wraps examples in <pre><code class="lang-csharp">...</code></pre>
    // Extract the code content and convert to markdown code block
    let code = example;

    // Remove <pre><code> wrapper if present
    const codeMatch = code.match(/<pre><code[^>]*>([\s\S]*?)<\/code><\/pre>/);
    if (codeMatch) {
      code = codeMatch[1];
    }

    // Unescape HTML entities
    code = code
      .replace(/&lt;/g, '<')
      .replace(/&gt;/g, '>')
      .replace(/&amp;/g, '&')
      .replace(/&quot;/g, '"')
      .replace(/&#39;/g, "'");

    md += '```csharp\n' + code.trim() + '\n```\n\n';
  }
  return md;
}

function formatTypeName(type) {
  if (!type) return '';
  return type.replace(/\x60\d+/g, '').replace(/\{/g, '<').replace(/\}/g, '>').split('.').pop();
}

function generateNamespaceMarkdown(doc, types) {
  const nsTypes = types.filter(t => t.namespace === doc.name);
  let md = '---\nlayout: layouts/api.njk\ntitle: "' + doc.name + '"\ndescription: "API documentation for ' + doc.name + ' namespace"\nnamespace: "' + doc.name + '"\ntype: "namespace"\n---\n\n';
  
  if (doc.summary) md += cleanMarkdown(doc.summary) + '\n\n';

  const classes = nsTypes.filter(i => i.type === 'Class');
  const records = nsTypes.filter(i => i.type === 'Record');
  const interfaces = nsTypes.filter(i => i.type === 'Interface');
  const enums = nsTypes.filter(i => i.type === 'Enum');

  if (classes.length > 0) {
    md += '## Classes\n\n| Class | Description |\n|-------|-------------|\n';
    for (const item of classes) {
      const name = item.name.split('.').pop();
      const desc = cleanMarkdown(item.summary) || '';
      md += '| [' + name + '](' + name + '/) | ' + desc.split('\n')[0] + ' |\n';
    }
    md += '\n';
  }

  if (records.length > 0) {
    md += '## Records\n\n| Record | Description |\n|--------|-------------|\n';
    for (const item of records) {
      const name = item.name.split('.').pop();
      const desc = cleanMarkdown(item.summary) || '';
      md += '| [' + name + '](' + name + '/) | ' + desc.split('\n')[0] + ' |\n';
    }
    md += '\n';
  }

  if (interfaces.length > 0) {
    md += '## Interfaces\n\n| Interface | Description |\n|-----------|-------------|\n';
    for (const item of interfaces) {
      const name = item.name.split('.').pop();
      const desc = cleanMarkdown(item.summary) || '';
      md += '| [' + name + '](' + name + '/) | ' + desc.split('\n')[0] + ' |\n';
    }
    md += '\n';
  }

  if (enums.length > 0) {
    md += '## Enums\n\n| Enum | Description |\n|------|-------------|\n';
    for (const item of enums) {
      const name = item.name.split('.').pop();
      const desc = cleanMarkdown(item.summary) || '';
      md += '| [' + name + '](' + name + '/) | ' + desc.split('\n')[0] + ' |\n';
    }
    md += '\n';
  }

  return md;
}

function generateTypeMarkdown(doc, allItems) {
  const typeName = doc.name.split('.').pop();
  const namespace = doc.namespace || doc.name.split('.').slice(0, -1).join('.');

  let md = '---\nlayout: layouts/api.njk\ntitle: "' + typeName + '"\ndescription: "API documentation for ' + typeName + '"\nnamespace: "' + namespace + '"\ntype: "' + (doc.type || 'Type').toLowerCase() + '"\n---\n\n';
  md += '<div class="api-breadcrumb">Classes &gt; <a href="../">' + namespace + '</a> &gt; ' + typeName + '</div>\n\n';
  
  if (doc.summary) md += cleanMarkdown(doc.summary) + '\n\n';
  if (doc.syntax && doc.syntax.content) md += '```csharp\n' + doc.syntax.content + '\n```\n\n';

  // Add class-level examples
  if (doc.example && doc.example.length > 0) {
    md += formatExample(doc.example);
  }

  const children = allItems.filter(i => i.parent === doc.uid);
  const constructors = children.filter(c => c.type === 'Constructor');
  const properties = children.filter(c => c.type === 'Property');
  const methods = children.filter(c => c.type === 'Method');
  const fields = children.filter(c => c.type === 'Field');

  if (constructors.length > 0) {
    md += '## Constructors\n\n';
    for (const ctor of constructors) {
      md += '### ' + escapeHtml(typeName) + '\n\n';
      if (ctor.syntax && ctor.syntax.content) md += '```csharp\n' + ctor.syntax.content + '\n```\n\n';
      if (ctor.summary) md += cleanMarkdown(ctor.summary) + '\n\n';
      if (ctor.syntax && ctor.syntax.parameters && ctor.syntax.parameters.length > 0) {
        md += '| Parameter | Type | Description |\n|-----------|------|-------------|\n';
        for (const param of ctor.syntax.parameters) {
          md += '| `' + param.id + '` | `' + formatTypeName(param.type) + '` | ' + (cleanMarkdown(param.description) || '') + ' |\n';
        }
        md += '\n';
      }
    }
  }

  if (properties.length > 0) {
    md += '## Properties\n\n';
    for (const prop of properties) {
      const name = prop.name.split('.').pop();
      md += '### ' + name + '\n\n';
      if (prop.syntax && prop.syntax.content) md += '```csharp\n' + prop.syntax.content + '\n```\n\n';
      if (prop.summary) md += cleanMarkdown(prop.summary) + '\n\n';
    }
  }

  if (methods.length > 0) {
    md += '## Methods\n\n';
    for (const method of methods) {
      const name = method.name.split('.').pop();
      md += '### ' + escapeHtml(name) + '\n\n';
      if (method.syntax && method.syntax.content) md += '```csharp\n' + method.syntax.content + '\n```\n\n';
      if (method.summary) md += cleanMarkdown(method.summary) + '\n\n';
      if (method.syntax && method.syntax.parameters && method.syntax.parameters.length > 0) {
        md += '**Parameters:**\n\n| Name | Type | Description |\n|------|------|-------------|\n';
        for (const param of method.syntax.parameters) {
          md += '| `' + param.id + '` | `' + formatTypeName(param.type) + '` | ' + (cleanMarkdown(param.description) || '') + ' |\n';
        }
        md += '\n';
      }
      if (method.syntax && method.syntax.return) {
        md += '**Returns:** `' + formatTypeName(method.syntax.return.type) + '`';
        if (method.syntax.return.description) md += ' - ' + cleanMarkdown(method.syntax.return.description);
        md += '\n\n';
      }
      // Add method-level examples
      if (method.example && method.example.length > 0) {
        md += formatExample(method.example);
      }
    }
  }

  if (fields.length > 0) {
    md += '## Values\n\n| Name | Description |\n|------|-------------|\n';
    for (const field of fields) {
      const name = field.name.split('.').pop();
      const desc = cleanMarkdown(field.summary) || '';
      md += '| `' + name + '` | ' + desc.split('\n')[0] + ' |\n';
    }
    md += '\n';
  }

  return md;
}

function processYamlFiles() {
  if (!fs.existsSync(DOCFX_API_DIR)) {
    console.log('DocFX API directory not found at:', DOCFX_API_DIR);
    console.log('Run "docfx metadata docfx.json" first.');
    process.exit(1);
  }

  const files = fs.readdirSync(DOCFX_API_DIR).filter(f => f.endsWith('.yml') && f !== 'toc.yml');
  if (files.length === 0) {
    console.log('No YAML files found.');
    process.exit(1);
  }

  const allItems = [];
  const namespaces = new Map();

  for (const file of files) {
    const content = fs.readFileSync(path.join(DOCFX_API_DIR, file), 'utf8');
    try {
      const docs = yaml.loadAll(content);
      for (const doc of docs) {
        if (!doc || !doc.items) continue;
        for (const item of doc.items) {
          allItems.push(item);
          if (item.type === 'Namespace') namespaces.set(item.uid, item);
        }
      }
    } catch (e) {
      console.error('Error parsing ' + file + ':', e.message);
    }
  }

  console.log('Found ' + namespaces.size + ' namespaces and ' + allItems.length + ' total items');

  if (fs.existsSync(OUTPUT_DIR)) fs.rmSync(OUTPUT_DIR, { recursive: true });
  fs.mkdirSync(OUTPUT_DIR, { recursive: true });

  const sortedNamespaces = Array.from(namespaces.values()).sort((a, b) => a.name.localeCompare(b.name));
  let indexMd = '---\nlayout: layouts/api.njk\ntitle: "API Reference"\ndescription: "DataProvider API Reference"\ntype: "index"\n---\n\nAuto-generated API documentation from source code.\n\n## Namespaces\n\n| Namespace | Description |\n|-----------|-------------|\n';

  for (const ns of sortedNamespaces) {
    const summary = cleanMarkdown(ns.summary) || '';
    const nsPath = ns.name.replace(/\./g, '/');
    indexMd += '| [' + ns.name + '](' + nsPath + '/) | ' + summary.split('\n')[0] + ' |\n';
  }

  fs.writeFileSync(path.join(OUTPUT_DIR, 'index.md'), indexMd);
  console.log('Generated: apidocs/index.md');

  const types = allItems.filter(i => i.type && ['Class', 'Record', 'Interface', 'Enum', 'Struct'].includes(i.type));

  for (const [uid, ns] of namespaces) {
    const nsPath = ns.name.replace(/\./g, '/');
    const nsDir = path.join(OUTPUT_DIR, nsPath);
    fs.mkdirSync(nsDir, { recursive: true });

    fs.writeFileSync(path.join(nsDir, 'index.md'), generateNamespaceMarkdown(ns, types));
    console.log('Generated: ' + nsPath + '/index.md');

    const nsTypes = types.filter(t => t.namespace === ns.name);
    for (const type of nsTypes) {
      const typeName = type.name.split('.').pop();
      const typeDir = path.join(nsDir, typeName);
      fs.mkdirSync(typeDir, { recursive: true });
      fs.writeFileSync(path.join(typeDir, 'index.md'), generateTypeMarkdown(type, allItems));
      console.log('Generated: ' + nsPath + '/' + typeName + '/index.md');
    }
  }

  console.log('\nAPI documentation generation complete!');
}

processYamlFiles();
