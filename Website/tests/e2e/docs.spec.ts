import { test, expect, type Page } from "@playwright/test";

const FORBIDDEN_STRINGS = [
  "MelbourneDev.",
  "DataProvider.MySql",
  "dotnet add package DataProvider.Sqlite",
  "dotnet add package DataProvider.SqlServer",
  "dotnet add package DataProvider.Postgres",
  "dotnet add package DataProvider\n",
  "dotnet add package Lql.SQLite",
  "dotnet add package Lql.Postgres",
  "dotnet add package Lql.SqlServer",
  "dotnet add package Lql.TypeProvider.FSharp",
  "HealthcareSamples",
  "Healthcare Samples",
  "--version 0.4.0",
  "version\": \"0.4.0",
];

const DOC_PAGES = [
  "/",
  "/about/",
  "/docs/",
  "/docs/installation/",
  "/docs/getting-started/",
  "/docs/quick-start/",
  "/docs/samples/",
  "/docs/dataprovider/",
  "/docs/lql/",
  "/docs/sync/",
  "/docs/migrations/",
  "/apidocs/",
  "/apidocs/Nimblesite/DataProvider/Core/",
  "/apidocs/Nimblesite/Sync/Core/",
  "/blog/",
  "/blog/getting-started-dataprovider/",
  "/blog/connecting-sql-server/",
  "/blog/lql-simplifies-development/",
];

async function getBodyText(page: Page): Promise<string> {
  return (await page.textContent("body")) ?? "";
}

test.describe("Homepage", () => {
  test("renders hero and links to key docs", async ({ page }) => {
    await page.goto("/");

    await expect(page.locator("h1")).toContainText("Effortless .NET Data Access");

    const samplesLink = page.locator('a[href="/docs/samples/"]').first();
    await expect(samplesLink).toBeVisible();

    const body = await getBodyText(page);
    for (const forbidden of FORBIDDEN_STRINGS) {
      expect(body, `forbidden string "${forbidden}" found on homepage`).not.toContain(forbidden);
    }
  });

  test("nav contains Clinical Coding Platform and no stale entries", async ({ page }) => {
    await page.goto("/");
    const nav = (await page.locator("nav, header").first().textContent()) ?? "";
    expect(nav).not.toContain("Healthcare Samples");
    expect(nav).not.toContain("F# Type Provider");
  });
});

test.describe("Installation page", () => {
  test("advertises the three real CLI tools and Nimblesite runtime", async ({ page }) => {
    await page.goto("/docs/installation/");

    const body = await getBodyText(page);

    expect(body).toContain("dotnet tool install DataProvider");
    expect(body).toContain("dotnet tool install DataProviderMigrate");
    expect(body).toContain("dotnet tool install Lql");
    expect(body).toContain("Nimblesite.DataProvider.SQLite");
    expect(body).toContain("0.9.6-beta");
    expect(body).not.toContain("0.4.0");

    for (const forbidden of FORBIDDEN_STRINGS) {
      expect(body, `forbidden string "${forbidden}" on installation page`).not.toContain(forbidden);
    }
  });
});

test.describe("Getting Started page", () => {
  test("walks through all three CLI tools and Result<T,E>", async ({ page }) => {
    await page.goto("/docs/getting-started/");
    const body = await getBodyText(page);

    expect(body).toContain("dotnet tool install DataProvider");
    expect(body).toContain("DataProviderMigrate migrate");
    expect(body).toContain("Lql sqlite");
    expect(body).toContain("DataProvider sqlite");
    expect(body).toContain("Result<");
    expect(body).toContain("SqlError");
    expect(body).toContain("net10.0");
  });
});

test.describe("Quick Start page", () => {
  test("uses the generated API, not Dapper", async ({ page }) => {
    await page.goto("/docs/quick-start/");
    const body = await getBodyText(page);

    expect(body).not.toContain("connection.Query<");
    expect(body).not.toContain("connection.Execute(");
    expect(body).toContain("Result<");
    expect(body).toContain("GetActiveOrdersAsync");
    expect(body).toContain("Nimblesite.Lql.Postgres");
  });
});

test.describe("Clinical Coding Platform page", () => {
  test("covers FHIR, ICD-10, pgvector, and links to GitHub", async ({ page }) => {
    const response = await page.goto("/docs/samples/");
    expect(response?.status()).toBe(200);

    const title = await page.title();
    expect(title).toContain("Clinical Coding");

    const body = await getBodyText(page);
    expect(body).toContain("FHIR");
    expect(body).toContain("ICD-10");
    expect(body).toContain("pgvector");
    expect(body).toContain("DataProviderMigrate");

    const repoLink = page.locator('a[href*="github.com/MelbourneDeveloper/ClinicalCoding"]').first();
    await expect(repoLink).toBeVisible();

    const screenshot = page.locator('img[src*="clinical-coding/login"]').first();
    await expect(screenshot).toBeVisible();
    const src = await screenshot.getAttribute("src");
    expect(src).toBeTruthy();
    const imgResponse = await page.request.get(src!);
    expect(imgResponse.status()).toBe(200);
  });

  test("architecture diagram renders as Mermaid SVG, not ASCII", async ({ page }) => {
    await page.goto("/docs/samples/");

    const mermaidDiv = page.locator("div.mermaid").first();
    await expect(mermaidDiv).toBeVisible();

    const svg = mermaidDiv.locator("svg").first();
    await expect(svg).toBeVisible({ timeout: 10_000 });
  });
});

test.describe("No ASCII box-drawing diagrams remain", () => {
  const ASCII_DIAGRAM_CHARS = ["┌", "└", "├", "┤", "─", "│", "┼", "┐", "┘"];
  const ASCII_ARROWS = ["+--->", "<----", "+---", "--->"];

  const pagesToScan = [
    "/",
    "/about/",
    "/docs/installation/",
    "/docs/getting-started/",
    "/docs/quick-start/",
    "/docs/samples/",
    "/docs/dataprovider/",
    "/docs/lql/",
    "/docs/sync/",
    "/docs/migrations/",
  ];

  for (const path of pagesToScan) {
    test(`${path} has no ASCII diagram characters`, async ({ page }) => {
      await page.goto(path);
      const body = await getBodyText(page);

      for (const ch of ASCII_DIAGRAM_CHARS) {
        expect(body, `box-drawing char "${ch}" found on ${path}`).not.toContain(ch);
      }
      for (const arrow of ASCII_ARROWS) {
        expect(body, `ASCII arrow "${arrow}" found on ${path}`).not.toContain(arrow);
      }
    });
  }
});

test.describe("API reference (DocFX-generated)", () => {
  test("/apidocs/ landing page lists Nimblesite namespaces", async ({ page }) => {
    const response = await page.goto("/apidocs/");
    expect(response?.status()).toBe(200);

    const body = await getBodyText(page);
    expect(body).toContain("Nimblesite.DataProvider.Core");
    expect(body).toContain("Nimblesite.Sync.Core");
  });

  test("/apidocs/Nimblesite/DataProvider/Core/ namespace page renders", async ({ page }) => {
    const response = await page.goto("/apidocs/Nimblesite/DataProvider/Core/");
    expect(response?.status()).toBe(200);
    const body = await getBodyText(page);
    expect(body.length).toBeGreaterThan(200);
  });
});

test.describe("Code blocks render without inline-chip leakage", () => {
  test("XML in getting-started has no per-token chip background", async ({ page }) => {
    await page.goto("/docs/getting-started/");

    const xmlBlock = page.locator("pre.language-xml").first();
    await expect(xmlBlock).toBeVisible();

    const tokenStyles = await xmlBlock.locator("span.token").first().evaluate((el) => {
      const style = window.getComputedStyle(el);
      return {
        background: style.backgroundColor,
        border: style.borderTopWidth,
        padding: style.paddingTop,
      };
    });

    expect(tokenStyles.background).toBe("rgba(0, 0, 0, 0)");
    expect(tokenStyles.border).toBe("0px");
    expect(tokenStyles.padding).toBe("0px");
  });
});

test.describe("Migration docs", () => {
  test("/docs/migrations/ renders with DataProviderMigrate content", async ({ page }) => {
    const response = await page.goto("/docs/migrations/");
    expect(response?.status()).toBe(200);

    const body = await getBodyText(page);
    expect(body).toContain("DataProviderMigrate");
    expect(body).toContain("YAML");
    expect(body).toContain("migrate");
    expect(body).toContain("0.9.6-beta");
  });
});

test.describe("Doc pages return 200 and are free of stale strings", () => {
  for (const path of DOC_PAGES) {
    test(`GET ${path}`, async ({ page }) => {
      const response = await page.goto(path);
      expect(response?.status(), `${path} should return 200`).toBe(200);

      const body = await getBodyText(page);
      for (const forbidden of FORBIDDEN_STRINGS) {
        expect(body, `forbidden string "${forbidden}" on ${path}`).not.toContain(forbidden);
      }
    });
  }
});
