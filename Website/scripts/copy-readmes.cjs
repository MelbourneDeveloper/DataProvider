/**
 * Copy README files from source directories to docs with Eleventy frontmatter
 * READMEs are the source of truth - this script copies them to the website
 */

const fs = require('fs');
const path = require('path');

const REPO_ROOT = path.join(__dirname, '../..');
const DOCS_DIR = path.join(__dirname, '../src/docs');
const ASSETS_DIR = path.join(__dirname, '../src/assets/images');

// Map of README source paths to docs output and frontmatter
const README_MAPPINGS = [
  {
    source: path.join(REPO_ROOT, 'README.md'),
    output: path.join(DOCS_DIR, 'index.md'),
    frontmatter: {
      layout: 'layouts/docs.njk',
      title: 'Introduction',
      description: 'DataProvider - A comprehensive .NET toolkit for compile-time safe database access.'
    }
  },
  {
    source: path.join(REPO_ROOT, 'DataProvider/README.md'),
    output: path.join(DOCS_DIR, 'dataprovider.md'),
    frontmatter: {
      layout: 'layouts/docs.njk',
      title: 'DataProvider',
      description: 'Source generator that creates compile-time safe extension methods from SQL files.'
    }
  },
  {
    source: path.join(REPO_ROOT, 'Lql/README.md'),
    output: path.join(DOCS_DIR, 'lql.md'),
    frontmatter: {
      layout: 'layouts/docs.njk',
      title: 'Lambda Query Language (LQL)',
      description: 'A functional pipeline-style DSL that transpiles to SQL.'
    }
  },
  {
    source: path.join(REPO_ROOT, 'Sync/README.md'),
    output: path.join(DOCS_DIR, 'sync.md'),
    frontmatter: {
      layout: 'layouts/docs.njk',
      title: 'Sync Framework',
      description: 'Offline-first bidirectional synchronization framework for .NET.'
    }
  },
  {
    source: path.join(REPO_ROOT, 'Gatekeeper/README.md'),
    output: path.join(DOCS_DIR, 'gatekeeper.md'),
    frontmatter: {
      layout: 'layouts/docs.njk',
      title: 'Gatekeeper',
      description: 'WebAuthn authentication and role-based access control.'
    }
  },
  {
    source: path.join(REPO_ROOT, 'Migration/README.md'),
    output: path.join(DOCS_DIR, 'migrations.md'),
    frontmatter: {
      layout: 'layouts/docs.njk',
      title: 'Migrations',
      description: 'Database-agnostic schema migration framework for .NET.'
    }
  }
];

function generateFrontmatter(fm) {
  let yaml = '---\n';
  for (const [key, value] of Object.entries(fm)) {
    yaml += `${key}: "${value}"\n`;
  }
  yaml += '---\n\n';
  return yaml;
}

function processReadme(mapping) {
  if (!fs.existsSync(mapping.source)) {
    console.log(`SKIP: ${mapping.source} not found`);
    return false;
  }

  let content = fs.readFileSync(mapping.source, 'utf8');

  // Remove any existing frontmatter from README
  content = content.replace(/^---[\s\S]*?---\n*/, '');

  // Remove the first H1 heading (will be rendered from frontmatter title)
  content = content.replace(/^#\s+[^\n]+\n+/, '');

  // Fix relative links to point to correct locations
  // Convert ./Component/README.md links to /docs/component/
  content = content.replace(/\[([^\]]+)\]\(\.\/([^/]+)\/README\.md\)/g, '[$1](/docs/$2/)');
  content = content.replace(/\[([^\]]+)\]\(\.\/([^)]+)\.md\)/g, '[$1](/docs/$2/)');

  // Convert relative image paths
  content = content.replace(/!\[([^\]]*)\]\((?!http)([^)]+)\)/g, '![$1](/assets/images/$2)');

  const output = generateFrontmatter(mapping.frontmatter) + content;

  fs.writeFileSync(mapping.output, output);
  console.log(`Generated: ${path.relative(DOCS_DIR, mapping.output)}`);
  return true;
}

function copyImages() {
  console.log('Copying images from READMEs...\n');

  // Ensure assets directory exists
  if (!fs.existsSync(ASSETS_DIR)) {
    fs.mkdirSync(ASSETS_DIR, { recursive: true });
  }

  // Copy images referenced in READMEs from repo root
  const repoRootImages = ['lqldbbrowser.png'];
  for (const img of repoRootImages) {
    const src = path.join(REPO_ROOT, img);
    const dest = path.join(ASSETS_DIR, img);
    if (fs.existsSync(src)) {
      fs.copyFileSync(src, dest);
      console.log(`Copied image: ${img}`);
    } else {
      console.log(`SKIP: Image ${img} not found`);
    }
  }

  // Copy images from subdirectories (relative to each README)
  const subdirImages = [
    { src: 'Samples/image.png', dest: 'Samples/image.png' },
    { src: 'Samples/image-1.png', dest: 'Samples/image-1.png' }
  ];
  for (const img of subdirImages) {
    const src = path.join(REPO_ROOT, img.src);
    const destDir = path.join(ASSETS_DIR, path.dirname(img.dest));
    const dest = path.join(ASSETS_DIR, img.dest);
    if (fs.existsSync(src)) {
      if (!fs.existsSync(destDir)) {
        fs.mkdirSync(destDir, { recursive: true });
      }
      fs.copyFileSync(src, dest);
      console.log(`Copied image: ${img.src}`);
    }
  }
}

function main() {
  console.log('Copying README files to docs...\n');

  // Ensure docs directory exists
  if (!fs.existsSync(DOCS_DIR)) {
    fs.mkdirSync(DOCS_DIR, { recursive: true });
  }

  let count = 0;
  for (const mapping of README_MAPPINGS) {
    if (processReadme(mapping)) {
      count++;
    }
  }

  console.log(`\nCopied ${count} README files to docs.`);

  // Copy images referenced in READMEs
  copyImages();
}

main();
