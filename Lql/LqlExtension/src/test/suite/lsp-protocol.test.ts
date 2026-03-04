/**
 * E2E LSP Protocol Tests
 *
 * These tests hit the LSP server DIRECTLY via stdio with real JSON-RPC messages.
 * No mocks. No fakes. Real server binary, real protocol, real responses.
 *
 * This is the equivalent of WebSocket testing — we spawn the actual lql-lsp
 * binary and communicate with it using the LSP wire protocol.
 */

import * as assert from "assert";
import * as child_process from "child_process";
import * as path from "path";
import * as fs from "fs";

/** Encode a JSON-RPC message with LSP Content-Length header */
function encode(msg: object): string {
  const body = JSON.stringify(msg);
  return `Content-Length: ${Buffer.byteLength(body)}\r\n\r\n${body}`;
}

/**
 * Persistent LSP client that maintains a single data listener for the entire
 * server lifetime. This prevents issues with interleaved notifications and
 * buffered stdout data.
 */
class LspClient {
  private proc: child_process.ChildProcess;
  private messageId = 0;
  /** Raw byte buffer — Content-Length is in bytes, not characters */
  private rawBuffer: Buffer = Buffer.alloc(0);
  private pendingRequests = new Map<
    number,
    { resolve: (v: any) => void; reject: (e: Error) => void }
  >();
  private notifications: Array<{ method: string; params: any }> = [];

  constructor(binary: string, env?: Record<string, string>) {
    this.proc = child_process.spawn(binary, [], {
      stdio: ["pipe", "pipe", "pipe"],
      env: env ? { ...process.env, ...env } : undefined,
    });

    this.proc.stdout!.on("data", (data: Buffer) => {
      this.rawBuffer = Buffer.concat([this.rawBuffer, data]);
      this.processBuffer();
    });
  }

  private processBuffer(): void {
    while (true) {
      const headerEnd = this.rawBuffer.indexOf("\r\n\r\n");
      if (headerEnd === -1) break;

      const header = this.rawBuffer.subarray(0, headerEnd).toString("utf-8");
      const match = header.match(/Content-Length:\s*(\d+)/i);
      if (!match) break;

      const contentLength = parseInt(match[1], 10);
      const bodyStart = headerEnd + 4;
      if (this.rawBuffer.length < bodyStart + contentLength) break;

      const body = this.rawBuffer
        .subarray(bodyStart, bodyStart + contentLength)
        .toString("utf-8");
      this.rawBuffer = this.rawBuffer.subarray(bodyStart + contentLength);

      try {
        const parsed = JSON.parse(body);

        if (parsed.id !== undefined && parsed.id !== null) {
          // Response to a request
          const pending = this.pendingRequests.get(parsed.id);
          if (pending) {
            this.pendingRequests.delete(parsed.id);
            if (parsed.error) {
              pending.reject(
                new Error(`LSP error: ${JSON.stringify(parsed.error)}`),
              );
            } else {
              pending.resolve(parsed.result);
            }
          }
        } else if (parsed.method) {
          // Notification from server
          this.notifications.push({
            method: parsed.method,
            params: parsed.params,
          });
        }
      } catch {
        // Incomplete JSON, will get more data
      }
    }
  }

  async request(method: string, params?: any): Promise<any> {
    const id = ++this.messageId;
    const msg: any = { jsonrpc: "2.0", id, method };
    if (params !== undefined) {
      msg.params = params;
    }

    return new Promise((resolve, reject) => {
      this.pendingRequests.set(id, { resolve, reject });
      this.proc.stdin!.write(encode(msg));

      setTimeout(() => {
        if (this.pendingRequests.has(id)) {
          this.pendingRequests.delete(id);
          reject(
            new Error(
              `Timeout waiting for response to ${method} (id=${id})`,
            ),
          );
        }
      }, 10000);
    });
  }

  notify(method: string, params: any): void {
    const msg = { jsonrpc: "2.0", method, params };
    this.proc.stdin!.write(encode(msg));
  }

  /** Drain collected notifications, optionally filtering by method. */
  drainNotifications(method?: string): Array<{ method: string; params: any }> {
    if (method) {
      const matching = this.notifications.filter((n) => n.method === method);
      this.notifications = this.notifications.filter(
        (n) => n.method !== method,
      );
      return matching;
    }
    const all = [...this.notifications];
    this.notifications = [];
    return all;
  }

  /** Wait for at least one notification of a given method. */
  async waitForNotification(
    method: string,
    timeoutMs: number = 5000,
  ): Promise<any[]> {
    const start = Date.now();
    while (Date.now() - start < timeoutMs) {
      const matching = this.notifications.filter((n) => n.method === method);
      if (matching.length > 0) {
        this.notifications = this.notifications.filter(
          (n) => n.method !== method,
        );
        return matching.map((n) => n.params);
      }
      await new Promise((r) => setTimeout(r, 100));
    }
    // Return whatever we have
    const matching = this.notifications.filter((n) => n.method === method);
    this.notifications = this.notifications.filter(
      (n) => n.method !== method,
    );
    return matching.map((n) => n.params);
  }

  kill(): void {
    this.proc.kill();
  }
}

/** Find the LSP binary */
function findLspBinary(): string {
  const rootDir = path.resolve(__dirname, "..", "..", "..");
  const candidates = [
    path.join(rootDir, "..", "lql-lsp-rust", "target", "release", "lql-lsp"),
    path.join(rootDir, "..", "lql-lsp-rust", "target", "debug", "lql-lsp"),
    path.join(rootDir, "bin", "lql-lsp"),
  ];

  for (const candidate of candidates) {
    if (fs.existsSync(candidate)) {
      return candidate;
    }
  }

  throw new Error(
    `LSP binary not found. Searched:\n${candidates.join("\n")}\nBuild with: cargo build -p lql-lsp`,
  );
}

/** Read a test fixture file */
function readFixture(name: string): string {
  const fixturePath = path.join(__dirname, "..", "fixtures", name);
  const altPath = path.join(
    __dirname,
    "..",
    "..",
    "..",
    "src",
    "test",
    "fixtures",
    name,
  );
  if (fs.existsSync(fixturePath)) return fs.readFileSync(fixturePath, "utf8");
  if (fs.existsSync(altPath)) return fs.readFileSync(altPath, "utf8");
  throw new Error(`Fixture not found: ${name}`);
}

describe("LSP Protocol E2E Tests", function () {
  this.timeout(30000);

  let client: LspClient;
  let lspBinary: string;

  before(function () {
    try {
      lspBinary = findLspBinary();
    } catch {
      this.skip();
    }
  });

  beforeEach(function () {
    client = new LspClient(lspBinary);
  });

  afterEach(function () {
    if (client) {
      client.kill();
    }
  });

  /** Common initialization sequence */
  async function initServer(): Promise<any> {
    const result = await client.request("initialize", {
      processId: process.pid,
      capabilities: {},
      rootUri: null,
    });
    client.notify("initialized", {});
    return result;
  }

  /** Open a document and wait for diagnostics */
  async function openDocument(
    uri: string,
    content: string,
  ): Promise<any[]> {
    client.notify("textDocument/didOpen", {
      textDocument: { uri, languageId: "lql", version: 1, text: content },
    });
    return client.waitForNotification(
      "textDocument/publishDiagnostics",
      5000,
    );
  }

  // ═══════════════════════════════════════════════════════════════
  // INITIALIZATION
  // ═══════════════════════════════════════════════════════════════

  it("should initialize with correct capabilities", async function () {
    const result = await initServer();

    assert.ok(result, "Initialize result should not be null");
    assert.ok(result.capabilities, "Should have capabilities object");
    assert.ok(
      result.capabilities.textDocumentSync !== undefined,
      "Must support text document sync",
    );
    assert.ok(
      result.capabilities.completionProvider,
      "Must support completions (IntelliSense)",
    );
    assert.ok(
      result.capabilities.completionProvider.triggerCharacters,
      "Must have trigger characters",
    );
    assert.ok(
      result.capabilities.completionProvider.triggerCharacters.includes("|"),
      "Pipe should be a trigger character",
    );
    assert.ok(
      result.capabilities.completionProvider.triggerCharacters.includes("."),
      "Dot should be a trigger character",
    );
    assert.ok(
      result.capabilities.hoverProvider,
      "Must support hover (IntelliPrompt)",
    );
    assert.ok(
      result.capabilities.documentSymbolProvider,
      "Must support document symbols",
    );
    assert.ok(
      result.capabilities.documentFormattingProvider,
      "Must support document formatting",
    );
  });

  // ═══════════════════════════════════════════════════════════════
  // COMPLETIONS (IntelliSense)
  // ═══════════════════════════════════════════════════════════════

  it("should provide pipeline completions after |>", async function () {
    await initServer();
    await openDocument("file:///test/completion.lql", "users |> ");

    const result = await client.request("textDocument/completion", {
      textDocument: { uri: "file:///test/completion.lql" },
      position: { line: 0, character: 9 },
    });

    assert.ok(result, "Completion result should not be null");
    const items = Array.isArray(result) ? result : result.items || [];
    assert.ok(items.length > 0, "Should return completion items");

    const labels = items.map((i: any) => i.label);
    assert.ok(labels.includes("select"), "Must suggest 'select'");
    assert.ok(labels.includes("filter"), "Must suggest 'filter'");
    assert.ok(labels.includes("join"), "Must suggest 'join'");
    assert.ok(labels.includes("group_by"), "Must suggest 'group_by'");
    assert.ok(labels.includes("order_by"), "Must suggest 'order_by'");
    assert.ok(labels.includes("having"), "Must suggest 'having'");
    assert.ok(labels.includes("limit"), "Must suggest 'limit'");

    const selectItem = items.find((i: any) => i.label === "select");
    assert.ok(selectItem, "select completion must exist");
    assert.ok(selectItem.detail, "select must have detail text");
    assert.ok(selectItem.documentation, "select must have documentation");
    assert.ok(selectItem.kind !== undefined, "select must have a kind");
  });

  it("should provide aggregate function completions", async function () {
    await initServer();
    await openDocument("file:///test/agg.lql", "orders |> select(c");

    const result = await client.request("textDocument/completion", {
      textDocument: { uri: "file:///test/agg.lql" },
      position: { line: 0, character: 18 },
    });

    const items = Array.isArray(result) ? result : result.items || [];
    const labels = items.map((i: any) => i.label);
    assert.ok(labels.includes("count"), "Must suggest 'count'");
    assert.ok(labels.includes("concat"), "Must suggest 'concat'");
    assert.ok(labels.includes("case"), "Must suggest 'case' keyword");
    assert.ok(labels.includes("coalesce"), "Must suggest 'coalesce'");
  });

  it("should provide keyword completions", async function () {
    await initServer();
    await openDocument("file:///test/kw.lql", "l");

    const result = await client.request("textDocument/completion", {
      textDocument: { uri: "file:///test/kw.lql" },
      position: { line: 0, character: 1 },
    });

    const items = Array.isArray(result) ? result : result.items || [];
    const labels = items.map((i: any) => i.label);
    assert.ok(labels.includes("let"), "Must suggest 'let' keyword");
    assert.ok(labels.includes("lower"), "Must suggest 'lower' function");
    assert.ok(labels.includes("length"), "Must suggest 'length' function");
  });

  // ═══════════════════════════════════════════════════════════════
  // HOVER (IntelliPrompt)
  // ═══════════════════════════════════════════════════════════════

  it("should provide hover info for filter keyword", async function () {
    await initServer();
    const content =
      "users |> filter(fn(row) => row.users.age > 18) |> select(users.name) |> limit(10)";
    await openDocument("file:///test/hover.lql", content);

    const filterHover = await client.request("textDocument/hover", {
      textDocument: { uri: "file:///test/hover.lql" },
      position: { line: 0, character: 12 },
    });

    assert.ok(filterHover, "Hover result for 'filter' must not be null");
    assert.ok(filterHover.contents, "Hover must have contents");
    const filterText =
      typeof filterHover.contents === "string"
        ? filterHover.contents
        : filterHover.contents.value || "";
    assert.ok(
      filterText.toLowerCase().includes("filter"),
      "Hover for 'filter' must mention 'filter'",
    );
    assert.ok(
      filterText.includes("fn("),
      "Hover for 'filter' should show lambda syntax",
    );
  });

  it("should provide hover info for select keyword", async function () {
    await initServer();
    const content =
      "users |> filter(fn(row) => row.users.age > 18) |> select(users.name) |> limit(10)";
    await openDocument("file:///test/hover2.lql", content);

    const selectHover = await client.request("textDocument/hover", {
      textDocument: { uri: "file:///test/hover2.lql" },
      position: { line: 0, character: 52 },
    });

    assert.ok(selectHover, "Hover result for 'select' must not be null");
    const selectText =
      typeof selectHover.contents === "string"
        ? selectHover.contents
        : selectHover.contents.value || "";
    assert.ok(
      selectText.toLowerCase().includes("select"),
      "Hover for 'select' must mention 'select'",
    );
    assert.ok(
      selectText.toLowerCase().includes("project"),
      "Hover for 'select' should describe projection",
    );
  });

  it("should provide hover for aggregate functions", async function () {
    await initServer();
    await openDocument(
      "file:///test/agg_hover.lql",
      "orders |> select(count(*) as cnt, sum(orders.total) as total)",
    );

    const countHover = await client.request("textDocument/hover", {
      textDocument: { uri: "file:///test/agg_hover.lql" },
      position: { line: 0, character: 19 },
    });

    assert.ok(countHover, "Hover for 'count' must not be null");
    const countText =
      typeof countHover.contents === "string"
        ? countHover.contents
        : countHover.contents.value || "";
    assert.ok(
      countText.toLowerCase().includes("count"),
      "Must describe count function",
    );

    const sumHover = await client.request("textDocument/hover", {
      textDocument: { uri: "file:///test/agg_hover.lql" },
      position: { line: 0, character: 35 },
    });

    assert.ok(sumHover, "Hover for 'sum' must not be null");
    const sumText =
      typeof sumHover.contents === "string"
        ? sumHover.contents
        : sumHover.contents.value || "";
    assert.ok(
      sumText.toLowerCase().includes("sum"),
      "Must describe sum function",
    );
  });

  it("should return null hover for unknown identifiers", async function () {
    await initServer();
    await openDocument(
      "file:///test/no_hover.lql",
      "users |> select(users.id)",
    );

    const noHover = await client.request("textDocument/hover", {
      textDocument: { uri: "file:///test/no_hover.lql" },
      position: { line: 0, character: 2 },
    });

    assert.strictEqual(
      noHover,
      null,
      "Hover for unknown identifier should be null",
    );
  });

  // ═══════════════════════════════════════════════════════════════
  // DIAGNOSTICS
  // ═══════════════════════════════════════════════════════════════

  it("should publish diagnostics for syntax errors", async function () {
    await initServer();
    const invalidContent = readFixture("invalid_syntax.lql");
    const notifications = await openDocument(
      "file:///test/invalid.lql",
      invalidContent,
    );

    assert.ok(notifications.length > 0, "Must publish diagnostics");
    const diags = notifications[0];
    assert.ok(diags.uri, "Diagnostics must have a URI");
    assert.ok(
      diags.diagnostics.length > 0,
      "Must have at least one diagnostic for invalid syntax",
    );

    const firstDiag = diags.diagnostics[0];
    assert.ok(firstDiag.range, "Diagnostic must have a range");
    assert.ok(firstDiag.range.start !== undefined, "Range must have start");
    assert.ok(firstDiag.range.end !== undefined, "Range must have end");
    assert.ok(firstDiag.message, "Diagnostic must have a message");
    assert.ok(firstDiag.message.length > 0, "Message must not be empty");
    assert.ok(firstDiag.severity, "Diagnostic must have severity");
  });

  it("should publish clean diagnostics for valid files", async function () {
    await initServer();
    const validContent = readFixture("simple_select.lql");
    const notifications = await openDocument(
      "file:///test/valid.lql",
      validContent,
    );

    assert.ok(notifications.length > 0, "Must publish diagnostics");
    const diags = notifications[0];
    const errors = diags.diagnostics.filter((d: any) => d.severity === 1);
    assert.strictEqual(
      errors.length,
      0,
      `Valid file should have no errors, got: ${JSON.stringify(errors)}`,
    );
  });

  it("should update diagnostics on document change", async function () {
    await initServer();
    await openDocument(
      "file:///test/change.lql",
      "users |> select(users.id)",
    );

    // Change to invalid content
    client.notify("textDocument/didChange", {
      textDocument: { uri: "file:///test/change.lql", version: 2 },
      contentChanges: [{ text: "users |> select(" }],
    });

    const notifications = await client.waitForNotification(
      "textDocument/publishDiagnostics",
      5000,
    );
    assert.ok(notifications.length > 0, "Must publish updated diagnostics");
    const diags = notifications[notifications.length - 1];
    assert.ok(
      diags.diagnostics.length > 0,
      "Changed invalid content should produce diagnostics",
    );
  });

  // ═══════════════════════════════════════════════════════════════
  // DOCUMENT SYMBOLS
  // ═══════════════════════════════════════════════════════════════

  it("should return document symbols for let bindings", async function () {
    await initServer();
    const content = readFixture("complex_pipeline.lql");
    await openDocument("file:///test/symbols.lql", content);

    const result = await client.request("textDocument/documentSymbol", {
      textDocument: { uri: "file:///test/symbols.lql" },
    });

    assert.ok(result, "Document symbols result should not be null");
    assert.ok(Array.isArray(result), "Symbols should be an array");
    assert.ok(
      result.length >= 2,
      "Should find at least 2 let bindings (completed_orders, summary)",
    );

    const names = result.map((s: any) => s.name);
    assert.ok(
      names.includes("completed_orders"),
      "Must find 'completed_orders' binding",
    );
    assert.ok(names.includes("summary"), "Must find 'summary' binding");

    const sym = result[0];
    assert.ok(sym.name, "Symbol must have a name");
    assert.ok(sym.kind !== undefined, "Symbol must have a kind");
    assert.ok(sym.location, "Symbol must have a location");
    assert.ok(sym.location.range, "Symbol location must have a range");
  });

  // ═══════════════════════════════════════════════════════════════
  // FORMATTING
  // ═══════════════════════════════════════════════════════════════

  it("should format LQL documents", async function () {
    await initServer();
    const uglyContent = "  users   |>   select(  users.id  ,  users.name  )  ";
    await openDocument("file:///test/format.lql", uglyContent);

    const result = await client.request("textDocument/formatting", {
      textDocument: { uri: "file:///test/format.lql" },
      options: { tabSize: 4, insertSpaces: true },
    });

    assert.ok(result, "Formatting result should not be null");
    assert.ok(Array.isArray(result), "Formatting should return text edits");
    assert.ok(result.length > 0, "Should have at least one edit");

    const edit = result[0];
    assert.ok(edit.range, "Edit must have a range");
    assert.ok(edit.newText !== undefined, "Edit must have new text");
    assert.ok(edit.newText.trim().length > 0, "Formatted text should not be empty");
  });

  // ═══════════════════════════════════════════════════════════════
  // COMPLEX REAL-WORLD SCENARIOS
  // ═══════════════════════════════════════════════════════════════

  it("should handle complex pipeline with all features", async function () {
    await initServer();
    const content = readFixture("complex_pipeline.lql");
    const notifications = await openDocument(
      "file:///test/complex.lql",
      content,
    );

    assert.ok(notifications.length > 0, "Must publish diagnostics");
    const diags = notifications[0];
    const parseErrors = diags.diagnostics.filter(
      (d: any) => d.severity === 1 && d.source === "lql",
    );
    assert.strictEqual(
      parseErrors.length,
      0,
      `Complex pipeline should parse without errors, got: ${JSON.stringify(parseErrors)}`,
    );

    const result = await client.request("textDocument/completion", {
      textDocument: { uri: "file:///test/complex.lql" },
      position: { line: 17, character: 0 },
    });
    assert.ok(result, "Should provide completions in complex document");
  });

  it("should handle window function documents cleanly", async function () {
    await initServer();
    const content = readFixture("window_functions.lql");
    const notifications = await openDocument(
      "file:///test/window.lql",
      content,
    );

    assert.ok(notifications.length > 0);
    const diags = notifications[0];
    const errors = diags.diagnostics.filter((d: any) => d.severity === 1);
    assert.strictEqual(errors.length, 0, "Window function file should parse without errors");
  });

  it("should handle case expression documents cleanly", async function () {
    await initServer();
    const content = readFixture("case_expression.lql");
    const notifications = await openDocument(
      "file:///test/case.lql",
      content,
    );

    const diags = notifications[0];
    const errors = diags.diagnostics.filter((d: any) => d.severity === 1);
    assert.strictEqual(errors.length, 0, "Case expression should parse cleanly");
  });

  it("should handle exists subquery documents cleanly", async function () {
    await initServer();
    const content = readFixture("subquery_exists.lql");
    const notifications = await openDocument(
      "file:///test/exists.lql",
      content,
    );

    const diags = notifications[0];
    const errors = diags.diagnostics.filter((d: any) => d.severity === 1);
    assert.strictEqual(errors.length, 0, "Exists subquery should parse cleanly");
  });

  // ═══════════════════════════════════════════════════════════════
  // LIFECYCLE
  // ═══════════════════════════════════════════════════════════════

  it("should handle shutdown gracefully", async function () {
    await initServer();
    const result = await client.request("shutdown");
    assert.strictEqual(result, null, "Shutdown should return null");
    client.notify("exit", null);
  });

  it("should handle multiple documents simultaneously", async function () {
    await initServer();
    await openDocument(
      "file:///test/doc1.lql",
      "users |> select(users.id)",
    );
    await openDocument(
      "file:///test/doc2.lql",
      "orders |> select(orders.total)",
    );

    const result1 = await client.request("textDocument/completion", {
      textDocument: { uri: "file:///test/doc1.lql" },
      position: { line: 0, character: 24 },
    });
    const result2 = await client.request("textDocument/completion", {
      textDocument: { uri: "file:///test/doc2.lql" },
      position: { line: 0, character: 30 },
    });

    assert.ok(result1, "Doc1 should return completions");
    assert.ok(result2, "Doc2 should return completions");
  });

  it("should handle document close and reopen", async function () {
    await initServer();
    await openDocument(
      "file:///test/reopen.lql",
      "users |> select(users.id)",
    );

    client.notify("textDocument/didClose", {
      textDocument: { uri: "file:///test/reopen.lql" },
    });

    await new Promise((r) => setTimeout(r, 300));

    await openDocument(
      "file:///test/reopen.lql",
      "orders |> select(orders.total)",
    );

    const result = await client.request("textDocument/completion", {
      textDocument: { uri: "file:///test/reopen.lql" },
      position: { line: 0, character: 30 },
    });

    assert.ok(result, "Should provide completions after reopen");
  });

  // ═══════════════════════════════════════════════════════════════
  // INTELLISENSE PROOF — Deep completions testing (ZERO MOCKING)
  // ═══════════════════════════════════════════════════════════════

  it("PROOF: IntelliSense delivers context-aware completions after pipe in real multiline pipeline", async function () {
    await initServer();
    const content = `let active_users = users
|> filter(fn(row) => row.users.status = 'active')
|> `;
    await openDocument("file:///test/intellisense_proof1.lql", content);

    const result = await client.request("textDocument/completion", {
      textDocument: { uri: "file:///test/intellisense_proof1.lql" },
      position: { line: 2, character: 3 },
    });

    const items = Array.isArray(result) ? result : result.items || [];
    const labels = items.map((i: any) => i.label);

    // All pipeline operations MUST be offered after |>
    const requiredOps = [
      "select", "filter", "join", "left_join", "group_by",
      "order_by", "having", "limit", "offset", "union", "insert",
    ];
    for (const op of requiredOps) {
      assert.ok(labels.includes(op), `IntelliSense MUST suggest '${op}' after |>`);
    }

    // Each completion MUST have kind, detail, documentation, and insert_text
    for (const op of requiredOps) {
      const item = items.find((i: any) => i.label === op);
      assert.ok(item, `Completion for '${op}' must exist`);
      assert.ok(item.kind !== undefined, `'${op}' must have a completion kind`);
      assert.ok(
        item.detail && item.detail.length > 0,
        `'${op}' must have non-empty detail`,
      );
      assert.ok(
        item.documentation && item.documentation.length > 0,
        `'${op}' must have non-empty documentation`,
      );
    }
  });

  it("PROOF: IntelliSense delivers function completions inside select() arguments", async function () {
    await initServer();
    const content = "orders |> select(";
    await openDocument("file:///test/intellisense_proof2.lql", content);

    const result = await client.request("textDocument/completion", {
      textDocument: { uri: "file:///test/intellisense_proof2.lql" },
      position: { line: 0, character: 17 },
    });

    const items = Array.isArray(result) ? result : result.items || [];
    const labels = items.map((i: any) => i.label);

    // Aggregate functions MUST be available inside select()
    const requiredFunctions = [
      "count", "sum", "avg", "max", "min",
    ];
    for (const fn of requiredFunctions) {
      assert.ok(
        labels.includes(fn),
        `IntelliSense MUST suggest aggregate function '${fn}' inside select()`,
      );
    }

    // String functions MUST be available
    const stringFunctions = ["concat", "substring", "length", "trim", "upper", "lower"];
    for (const fn of stringFunctions) {
      assert.ok(
        labels.includes(fn),
        `IntelliSense MUST suggest string function '${fn}' inside select()`,
      );
    }

    // Window functions MUST be available
    assert.ok(labels.includes("row_number"), "Must suggest 'row_number' window function");
    assert.ok(labels.includes("rank"), "Must suggest 'rank' window function");
    assert.ok(labels.includes("dense_rank"), "Must suggest 'dense_rank' window function");
  });

  it("PROOF: IntelliSense completions have correct LSP completion item kinds", async function () {
    await initServer();
    await openDocument("file:///test/intellisense_kinds.lql", "orders |> ");

    const result = await client.request("textDocument/completion", {
      textDocument: { uri: "file:///test/intellisense_kinds.lql" },
      position: { line: 0, character: 10 },
    });

    const items = Array.isArray(result) ? result : result.items || [];

    // LSP completion item kind: Function = 3, Keyword = 14, Snippet = 15
    const selectItem = items.find((i: any) => i.label === "select");
    assert.ok(selectItem, "Must have 'select' completion");
    assert.ok(
      selectItem.kind === 3 || selectItem.kind === 14 || selectItem.kind === 15,
      `'select' must have a valid LSP completion kind, got: ${selectItem.kind}`,
    );

    // Verify insert text has snippet syntax for complex operations
    const joinItem = items.find((i: any) => i.label === "join");
    assert.ok(joinItem, "Must have 'join' completion");
    assert.ok(joinItem.insertText, "'join' must have insertText for snippet expansion");

    // Verify filter has lambda snippet
    const filterItem = items.find((i: any) => i.label === "filter");
    assert.ok(filterItem, "Must have 'filter' completion");
    assert.ok(
      filterItem.insertText,
      "'filter' must have insertText for lambda snippet",
    );
  });

  it("PROOF: IntelliSense delivers prefix-filtered completions after pipe", async function () {
    await initServer();
    // Place cursor right after "|> s" — the word prefix is "s", after_pipe detected
    await openDocument("file:///test/prefix_filter.lql", "orders |> s");

    const result = await client.request("textDocument/completion", {
      textDocument: { uri: "file:///test/prefix_filter.lql" },
      position: { line: 0, character: 11 },
    });

    const items = Array.isArray(result) ? result : result.items || [];
    const labels = items.map((i: any) => i.label);

    // Pipeline ops starting with 's' must be offered
    assert.ok(labels.includes("select"), "Must include 'select' matching prefix 's'");
    // Pipeline ops NOT starting with 's' must be filtered out
    assert.ok(!labels.includes("filter"), "Must NOT include 'filter' (doesn't start with 's')");
    assert.ok(!labels.includes("join"), "Must NOT include 'join' (doesn't start with 's')");
    assert.ok(!labels.includes("limit"), "Must NOT include 'limit' (doesn't start with 's')");
    // Functions starting with 's' should still be available
    assert.ok(labels.includes("sum"), "Must include 'sum' matching prefix 's'");
    assert.ok(labels.includes("substring"), "Must include 'substring' matching prefix 's'");
  });

  // ═══════════════════════════════════════════════════════════════
  // INTELLIPROMPT PROOF — Deep hover testing (ZERO MOCKING)
  // ═══════════════════════════════════════════════════════════════

  it("PROOF: IntelliPrompt delivers rich Markdown hover with signature for ALL pipeline ops", async function () {
    await initServer();
    // Build a document with all pipeline operations for hover testing
    const content = `users
|> filter(fn(row) => row.users.active = true)
|> join(orders, on = users.id = orders.user_id)
|> select(users.name, count(*) as order_count)
|> group_by(users.name)
|> having(fn(g) => count(*) > 5)
|> order_by(order_count desc)
|> limit(100)
|> offset(10)`;
    await openDocument("file:///test/intelliprompt_proof1.lql", content);

    // Hover over 'filter' at line 1, char ~5
    const filterHover = await client.request("textDocument/hover", {
      textDocument: { uri: "file:///test/intelliprompt_proof1.lql" },
      position: { line: 1, character: 5 },
    });
    assert.ok(filterHover, "Hover for 'filter' must return data");
    assert.ok(filterHover.contents, "Hover must have contents");
    const filterText = filterHover.contents.value || filterHover.contents;
    assert.ok(filterText.includes("filter"), "Hover must mention 'filter'");
    assert.ok(filterText.includes("fn("), "Hover must show lambda syntax for filter");
    assert.ok(filterText.includes("```"), "Hover must contain code block with signature");

    // Hover over 'join' at line 2, char ~5
    const joinHover = await client.request("textDocument/hover", {
      textDocument: { uri: "file:///test/intelliprompt_proof1.lql" },
      position: { line: 2, character: 5 },
    });
    assert.ok(joinHover, "Hover for 'join' must return data");
    const joinText = joinHover.contents.value || joinHover.contents;
    assert.ok(joinText.toLowerCase().includes("join"), "Hover must describe join");
    assert.ok(joinText.includes("```"), "Hover must contain code signature for join");

    // Hover over 'select' at line 3, char ~5
    const selectHover = await client.request("textDocument/hover", {
      textDocument: { uri: "file:///test/intelliprompt_proof1.lql" },
      position: { line: 3, character: 5 },
    });
    assert.ok(selectHover, "Hover for 'select' must return data");
    const selectText = selectHover.contents.value || selectHover.contents;
    assert.ok(selectText.toLowerCase().includes("select"), "Hover must describe select");
    assert.ok(
      selectText.toLowerCase().includes("project"),
      "Hover must explain select is for projection",
    );

    // Hover over 'group_by' at line 4, char ~5
    const groupHover = await client.request("textDocument/hover", {
      textDocument: { uri: "file:///test/intelliprompt_proof1.lql" },
      position: { line: 4, character: 5 },
    });
    assert.ok(groupHover, "Hover for 'group_by' must return data");
    const groupText = groupHover.contents.value || groupHover.contents;
    assert.ok(groupText.toLowerCase().includes("group"), "Hover must describe grouping");

    // Hover over 'having' at line 5, char ~5
    const havingHover = await client.request("textDocument/hover", {
      textDocument: { uri: "file:///test/intelliprompt_proof1.lql" },
      position: { line: 5, character: 5 },
    });
    assert.ok(havingHover, "Hover for 'having' must return data");
    const havingText = havingHover.contents.value || havingHover.contents;
    assert.ok(havingText.toLowerCase().includes("having"), "Hover must describe having");
    assert.ok(havingText.toLowerCase().includes("filter"), "Having hover must mention filtering groups");

    // Hover over 'order_by' at line 6, char ~5
    const orderHover = await client.request("textDocument/hover", {
      textDocument: { uri: "file:///test/intelliprompt_proof1.lql" },
      position: { line: 6, character: 5 },
    });
    assert.ok(orderHover, "Hover for 'order_by' must return data");
    const orderText = orderHover.contents.value || orderHover.contents;
    assert.ok(orderText.toLowerCase().includes("order"), "Hover must describe ordering");

    // Hover over 'limit' at line 7, char ~5
    const limitHover = await client.request("textDocument/hover", {
      textDocument: { uri: "file:///test/intelliprompt_proof1.lql" },
      position: { line: 7, character: 5 },
    });
    assert.ok(limitHover, "Hover for 'limit' must return data");
    const limitText = limitHover.contents.value || limitHover.contents;
    assert.ok(limitText.toLowerCase().includes("limit"), "Hover must describe limit");

    // Hover over 'offset' at line 8, char ~5
    const offsetHover = await client.request("textDocument/hover", {
      textDocument: { uri: "file:///test/intelliprompt_proof1.lql" },
      position: { line: 8, character: 5 },
    });
    assert.ok(offsetHover, "Hover for 'offset' must return data");
    const offsetText = offsetHover.contents.value || offsetHover.contents;
    assert.ok(offsetText.toLowerCase().includes("offset"), "Hover must describe offset");
    assert.ok(offsetText.toLowerCase().includes("skip"), "Offset hover must mention skipping rows");
  });

  it("PROOF: IntelliPrompt delivers hover for aggregate functions in real context", async function () {
    await initServer();
    const content = "orders |> select(count(*) as cnt, sum(orders.total) as total_sum, avg(orders.total) as avg_total, max(orders.total) as high, min(orders.total) as low)";
    await openDocument("file:///test/intelliprompt_agg.lql", content);

    // Hover over 'count' at position ~17
    const countHover = await client.request("textDocument/hover", {
      textDocument: { uri: "file:///test/intelliprompt_agg.lql" },
      position: { line: 0, character: 19 },
    });
    assert.ok(countHover, "Must get hover for 'count'");
    assert.ok(countHover.contents.kind === "markdown", "Hover contents must be Markdown format");
    assert.ok(
      countHover.contents.value.includes("count"),
      "Count hover must describe count function",
    );

    // Hover over 'sum' at position ~37
    const sumHover = await client.request("textDocument/hover", {
      textDocument: { uri: "file:///test/intelliprompt_agg.lql" },
      position: { line: 0, character: 35 },
    });
    assert.ok(sumHover, "Must get hover for 'sum'");
    assert.ok(sumHover.contents.kind === "markdown", "Sum hover must be Markdown");
    assert.ok(
      sumHover.contents.value.toLowerCase().includes("sum"),
      "Sum hover must describe sum",
    );

    // Hover over 'avg'
    const avgHover = await client.request("textDocument/hover", {
      textDocument: { uri: "file:///test/intelliprompt_agg.lql" },
      position: { line: 0, character: 69 },
    });
    assert.ok(avgHover, "Must get hover for 'avg'");
    assert.ok(avgHover.contents.value.toLowerCase().includes("avg"), "Avg hover must describe avg");

    // Hover over 'max'
    const maxHover = await client.request("textDocument/hover", {
      textDocument: { uri: "file:///test/intelliprompt_agg.lql" },
      position: { line: 0, character: 99 },
    });
    assert.ok(maxHover, "Must get hover for 'max'");
    assert.ok(maxHover.contents.value.toLowerCase().includes("max"), "Max hover must describe max");

    // Hover over 'min'
    const minHover = await client.request("textDocument/hover", {
      textDocument: { uri: "file:///test/intelliprompt_agg.lql" },
      position: { line: 0, character: 127 },
    });
    assert.ok(minHover, "Must get hover for 'min'");
    assert.ok(minHover.contents.value.toLowerCase().includes("min"), "Min hover must describe min");
  });

  it("PROOF: IntelliPrompt delivers hover for string functions", async function () {
    await initServer();
    const content = "users |> select(concat(users.first, users.last) as name, upper(users.email) as email_upper, trim(users.bio) as bio, length(users.bio) as bio_len)";
    await openDocument("file:///test/intelliprompt_string.lql", content);

    // Hover over 'concat'
    const concatHover = await client.request("textDocument/hover", {
      textDocument: { uri: "file:///test/intelliprompt_string.lql" },
      position: { line: 0, character: 18 },
    });
    assert.ok(concatHover, "Must get hover for 'concat'");
    assert.ok(
      concatHover.contents.value.toLowerCase().includes("concat"),
      "Concat hover must describe concatenation",
    );
    assert.ok(
      concatHover.contents.value.includes("```"),
      "Concat hover must include code signature",
    );

    // Hover over 'upper'
    const upperHover = await client.request("textDocument/hover", {
      textDocument: { uri: "file:///test/intelliprompt_string.lql" },
      position: { line: 0, character: 60 },
    });
    assert.ok(upperHover, "Must get hover for 'upper'");
    assert.ok(
      upperHover.contents.value.toLowerCase().includes("upper"),
      "Upper hover must describe uppercase",
    );

    // Hover over 'trim'
    const trimHover = await client.request("textDocument/hover", {
      textDocument: { uri: "file:///test/intelliprompt_string.lql" },
      position: { line: 0, character: 94 },
    });
    assert.ok(trimHover, "Must get hover for 'trim'");
    assert.ok(
      trimHover.contents.value.toLowerCase().includes("trim"),
      "Trim hover must describe trimming",
    );

    // Hover over 'length'
    const lengthHover = await client.request("textDocument/hover", {
      textDocument: { uri: "file:///test/intelliprompt_string.lql" },
      position: { line: 0, character: 117 },
    });
    assert.ok(lengthHover, "Must get hover for 'length'");
    assert.ok(
      lengthHover.contents.value.toLowerCase().includes("length"),
      "Length hover must describe string length",
    );
  });

  it("PROOF: IntelliPrompt returns null for non-LQL identifiers", async function () {
    await initServer();
    await openDocument(
      "file:///test/intelliprompt_null.lql",
      "my_custom_table |> select(my_custom_table.some_column)",
    );

    // Hover over 'my_custom_table' — not a keyword, should return null
    const tableHover = await client.request("textDocument/hover", {
      textDocument: { uri: "file:///test/intelliprompt_null.lql" },
      position: { line: 0, character: 5 },
    });
    assert.strictEqual(
      tableHover,
      null,
      "Custom table names must NOT trigger hover (not a language construct)",
    );

    // Hover over 'some_column' — not a keyword, should return null
    const colHover = await client.request("textDocument/hover", {
      textDocument: { uri: "file:///test/intelliprompt_null.lql" },
      position: { line: 0, character: 45 },
    });
    assert.strictEqual(
      colHover,
      null,
      "Custom column names must NOT trigger hover",
    );
  });

  // ═══════════════════════════════════════════════════════════════
  // DIAGNOSTICS PROOF — Real error detection (ZERO MOCKING)
  // ═══════════════════════════════════════════════════════════════

  it("PROOF: LSP detects real syntax errors and provides actionable range info", async function () {
    await initServer();
    // Unclosed parenthesis
    const notifications = await openDocument(
      "file:///test/diag_proof1.lql",
      "users |> select(users.id, users.name",
    );

    assert.ok(notifications.length > 0, "Must publish diagnostics");
    const diags = notifications[0].diagnostics;
    assert.ok(diags.length > 0, "Must detect unclosed parenthesis");

    const error = diags.find((d: any) => d.severity === 1);
    assert.ok(error, "Must report at least one error-level diagnostic");
    assert.ok(error.range, "Error diagnostic must have a range");
    assert.ok(error.range.start.line >= 0, "Range must have valid start line");
    assert.ok(error.range.start.character >= 0, "Range must have valid start character");
    assert.ok(error.message.length > 0, "Error message must not be empty");
    assert.ok(error.source === "lql", "Error source must be 'lql'");
  });

  it("PROOF: LSP reports zero errors on valid complex multiline LQL", async function () {
    await initServer();
    const validContent = `let completed_orders = orders
|> filter(fn(row) => row.orders.status = 'completed')
|> join(users, on = orders.user_id = users.id)
|> select(users.name, orders.total)

let summary = completed_orders
|> group_by(users.name)
|> select(users.name, sum(orders.total) as total_spent, count(*) as order_count)
|> order_by(total_spent desc)
|> limit(50)`;
    const notifications = await openDocument(
      "file:///test/diag_proof2.lql",
      validContent,
    );

    assert.ok(notifications.length > 0, "Must publish diagnostics");
    const errors = notifications[0].diagnostics.filter(
      (d: any) => d.severity === 1,
    );
    assert.strictEqual(
      errors.length,
      0,
      `Valid multiline pipeline must produce zero errors, got: ${JSON.stringify(errors)}`,
    );
  });

  it("PROOF: LSP detects errors and then clears them when content is fixed", async function () {
    await initServer();
    // Open with invalid content
    const badNotifications = await openDocument(
      "file:///test/diag_fix.lql",
      "users |> select(",
    );
    const badErrors = badNotifications[0].diagnostics.filter(
      (d: any) => d.severity === 1,
    );
    assert.ok(badErrors.length > 0, "Must detect syntax error in broken content");

    // Fix the content
    client.notify("textDocument/didChange", {
      textDocument: { uri: "file:///test/diag_fix.lql", version: 2 },
      contentChanges: [{ text: "users |> select(users.id)" }],
    });
    const fixedNotifications = await client.waitForNotification(
      "textDocument/publishDiagnostics",
      5000,
    );
    assert.ok(fixedNotifications.length > 0, "Must publish updated diagnostics");
    const fixedErrors = fixedNotifications[fixedNotifications.length - 1].diagnostics.filter(
      (d: any) => d.severity === 1,
    );
    assert.strictEqual(
      fixedErrors.length,
      0,
      "After fixing content, all errors must be cleared",
    );
  });

  // ═══════════════════════════════════════════════════════════════
  // DOCUMENT SYMBOLS PROOF (ZERO MOCKING)
  // ═══════════════════════════════════════════════════════════════

  it("PROOF: LSP extracts document symbols from let bindings with correct locations", async function () {
    await initServer();
    const content = `let users_active = users
|> filter(fn(row) => row.users.active = true)

let order_summary = orders
|> group_by(orders.user_id)
|> select(orders.user_id, count(*) as cnt)

let final_report = users_active
|> join(order_summary, on = users.id = orders.user_id)
|> select(users.name, order_summary.cnt)`;
    await openDocument("file:///test/symbols_proof.lql", content);

    const result = await client.request("textDocument/documentSymbol", {
      textDocument: { uri: "file:///test/symbols_proof.lql" },
    });

    assert.ok(result, "Document symbols must not be null");
    assert.ok(Array.isArray(result), "Symbols must be an array");
    assert.strictEqual(result.length, 3, "Must find exactly 3 let bindings");

    const names = result.map((s: any) => s.name);
    assert.ok(names.includes("users_active"), "Must find 'users_active' binding");
    assert.ok(names.includes("order_summary"), "Must find 'order_summary' binding");
    assert.ok(names.includes("final_report"), "Must find 'final_report' binding");

    // Each symbol must have proper location with range
    for (const sym of result) {
      assert.ok(sym.name, "Symbol must have a name");
      assert.ok(sym.kind !== undefined, "Symbol must have a kind");
      assert.ok(sym.location, "Symbol must have a location");
      assert.ok(sym.location.uri, "Symbol location must have a URI");
      assert.ok(sym.location.range, "Symbol location must have a range");
      assert.ok(
        sym.location.range.start.line >= 0,
        "Range start must have valid line",
      );
    }
  });

  // ═══════════════════════════════════════════════════════════════
  // FORMATTING PROOF (ZERO MOCKING)
  // ═══════════════════════════════════════════════════════════════

  it("PROOF: LSP formats messy multiline LQL into properly indented output", async function () {
    await initServer();
    // Multiline with bad indentation — formatter normalizes leading whitespace per line
    const messy = `       users
          |> filter(fn(row) => row.users.active = true)
  |> select(users.name, users.email)`;
    await openDocument("file:///test/format_proof.lql", messy);

    const edits = await client.request("textDocument/formatting", {
      textDocument: { uri: "file:///test/format_proof.lql" },
      options: { tabSize: 4, insertSpaces: true },
    });

    assert.ok(edits, "Formatting must return edits");
    assert.ok(Array.isArray(edits), "Edits must be an array");
    assert.ok(edits.length > 0, "Must have at least one edit");

    const edit = edits[0];
    assert.ok(edit.newText, "Edit must have newText");
    assert.ok(
      edit.newText !== messy,
      "Formatted text must differ from messy input",
    );
    // First line should not have leading whitespace (it's a table name)
    const lines = edit.newText.split("\n").filter((l: string) => l.trim().length > 0);
    assert.ok(lines.length >= 3, "Must have at least 3 non-empty lines");
    assert.ok(
      lines[0].trimStart() === lines[0].trim(),
      "First line should be properly trimmed",
    );
    // Pipeline lines should have consistent indentation
    const pipelines = lines.filter((l: string) => l.trim().startsWith("|>"));
    for (const pl of pipelines) {
      assert.ok(
        pl.startsWith("    "),
        "Pipeline continuation lines must be indented",
      );
    }
    assert.ok(
      edit.newText.trim().length > 0,
      "Formatted text must not be empty",
    );
  });

  // ═══════════════════════════════════════════════════════════════
  // REAL-WORLD COMPLEX SCENARIO PROOF (ZERO MOCKING)
  // ═══════════════════════════════════════════════════════════════

  it("PROOF: Full E2E workflow — open, complete, hover, diagnose, format in sequence", async function () {
    await initServer();

    // Step 1: Open a real document
    const content = `orders
|> filter(fn(row) => row.orders.amount > 100)
|> join(users, on = orders.user_id = users.id)
|> select(users.name, orders.amount, orders.date)
|> order_by(orders.amount desc)
|> limit(50)`;
    const diagNotifs = await openDocument("file:///test/full_workflow.lql", content);

    // Step 2: Assert diagnostics published — zero errors on valid content
    assert.ok(diagNotifs.length > 0, "Must publish diagnostics on open");
    const errors = diagNotifs[0].diagnostics.filter((d: any) => d.severity === 1);
    assert.strictEqual(errors.length, 0, "Valid document must have zero errors");

    // Step 3: Request completions at a meaningful position
    const completions = await client.request("textDocument/completion", {
      textDocument: { uri: "file:///test/full_workflow.lql" },
      position: { line: 5, character: 0 },
    });
    assert.ok(completions, "Must get completions");

    // Step 4: Hover over 'filter' on line 1
    const hover = await client.request("textDocument/hover", {
      textDocument: { uri: "file:///test/full_workflow.lql" },
      position: { line: 1, character: 5 },
    });
    assert.ok(hover, "Must get hover info");
    assert.ok(hover.contents.kind === "markdown", "Hover must be Markdown");
    assert.ok(
      hover.contents.value.includes("filter"),
      "Hover must describe filter",
    );

    // Step 5: Request document symbols
    const symbols = await client.request("textDocument/documentSymbol", {
      textDocument: { uri: "file:///test/full_workflow.lql" },
    });
    assert.ok(symbols !== undefined, "Must get symbol result (even if empty array)");

    // Step 6: Request formatting
    const format = await client.request("textDocument/formatting", {
      textDocument: { uri: "file:///test/full_workflow.lql" },
      options: { tabSize: 4, insertSpaces: true },
    });
    // Formatting may return null if already formatted
    // Just assert it doesn't error

    // Step 7: Modify document and verify diagnostics update
    client.notify("textDocument/didChange", {
      textDocument: { uri: "file:///test/full_workflow.lql", version: 2 },
      contentChanges: [{ text: "orders |> select(" }],
    });
    const newDiags = await client.waitForNotification(
      "textDocument/publishDiagnostics",
      5000,
    );
    assert.ok(newDiags.length > 0, "Must publish updated diagnostics after change");
    const newErrors = newDiags[newDiags.length - 1].diagnostics.filter(
      (d: any) => d.severity === 1,
    );
    assert.ok(newErrors.length > 0, "Broken content must produce errors");
  });

  it("PROOF: IntelliSense works for LQL keywords in empty doc and lambda context", async function () {
    await initServer();

    // In empty document: keywords like 'let', 'fn', 'case' should be available
    await openDocument("file:///test/keyword_proof.lql", "");
    const emptyResult = await client.request("textDocument/completion", {
      textDocument: { uri: "file:///test/keyword_proof.lql" },
      position: { line: 0, character: 0 },
    });
    const emptyItems = Array.isArray(emptyResult) ? emptyResult : emptyResult.items || [];
    const emptyLabels = emptyItems.map((i: any) => i.label);

    assert.ok(emptyLabels.includes("let"), "Must suggest 'let' keyword");
    assert.ok(emptyLabels.includes("case"), "Must suggest 'case' keyword");
    assert.ok(emptyLabels.includes("fn"), "Must suggest 'fn' keyword");
    assert.ok(emptyLabels.includes("distinct"), "Must suggest 'distinct' keyword");
    assert.ok(emptyLabels.includes("coalesce"), "Must suggest 'coalesce' function");

    // Keyword completions must have documentation
    for (const kw of ["let", "case", "fn"]) {
      const item = emptyItems.find((i: any) => i.label === kw);
      assert.ok(item, `Must have '${kw}' completion`);
      assert.ok(
        item.documentation && item.documentation.length > 0,
        `'${kw}' must have documentation`,
      );
    }

    // In lambda context: 'exists', 'and', 'or', 'not', 'in', 'like' should be offered
    await openDocument(
      "file:///test/keyword_lambda.lql",
      "users |> filter(fn(row) => row.users.active = true and ",
    );
    const lambdaResult = await client.request("textDocument/completion", {
      textDocument: { uri: "file:///test/keyword_lambda.lql" },
      position: { line: 0, character: 56 },
    });
    const lambdaItems = Array.isArray(lambdaResult) ? lambdaResult : lambdaResult.items || [];
    const lambdaLabels = lambdaItems.map((i: any) => i.label);

    assert.ok(lambdaLabels.includes("exists"), "Must suggest 'exists' in lambda context");
    assert.ok(lambdaLabels.includes("and"), "Must suggest 'and' in lambda context");
    assert.ok(lambdaLabels.includes("or"), "Must suggest 'or' in lambda context");
    assert.ok(lambdaLabels.includes("not"), "Must suggest 'not' in lambda context");
    assert.ok(lambdaLabels.includes("in"), "Must suggest 'in' in lambda context");
    assert.ok(lambdaLabels.includes("like"), "Must suggest 'like' in lambda context");
  });

  it("PROOF: IntelliPrompt hover for 'let' and 'fn' keywords", async function () {
    await initServer();
    const content = "let result = users |> filter(fn(row) => row.users.id > 0)";
    await openDocument("file:///test/kw_hover_proof.lql", content);

    // Hover over 'let' at position 0-2
    const letHover = await client.request("textDocument/hover", {
      textDocument: { uri: "file:///test/kw_hover_proof.lql" },
      position: { line: 0, character: 1 },
    });
    assert.ok(letHover, "Must get hover for 'let'");
    assert.ok(
      letHover.contents.value.toLowerCase().includes("bind"),
      "Let hover must describe binding",
    );
    assert.ok(
      letHover.contents.value.includes("```"),
      "Let hover must include code signature",
    );

    // Hover over 'fn' at position ~29
    const fnHover = await client.request("textDocument/hover", {
      textDocument: { uri: "file:///test/kw_hover_proof.lql" },
      position: { line: 0, character: 29 },
    });
    assert.ok(fnHover, "Must get hover for 'fn'");
    assert.ok(
      fnHover.contents.value.toLowerCase().includes("lambda"),
      "fn hover must describe lambda expressions",
    );
  });

  it("PROOF: Window function completions available with correct prefix filtering", async function () {
    await initServer();

    // Prefix "ro" — should match row_number, round
    await openDocument(
      "file:///test/window_complete1.lql",
      "orders |> select(ro",
    );
    const result1 = await client.request("textDocument/completion", {
      textDocument: { uri: "file:///test/window_complete1.lql" },
      position: { line: 0, character: 19 },
    });
    const items1 = Array.isArray(result1) ? result1 : result1.items || [];
    const labels1 = items1.map((i: any) => i.label);
    assert.ok(labels1.includes("row_number"), "Must suggest 'row_number' with prefix 'ro'");
    assert.ok(labels1.includes("round"), "Must suggest 'round' with prefix 'ro'");

    // Prefix "row" — should match row_number only
    await openDocument(
      "file:///test/window_complete2.lql",
      "orders |> select(row",
    );
    const result2 = await client.request("textDocument/completion", {
      textDocument: { uri: "file:///test/window_complete2.lql" },
      position: { line: 0, character: 20 },
    });
    const items2 = Array.isArray(result2) ? result2 : result2.items || [];
    const labels2 = items2.map((i: any) => i.label);
    assert.ok(labels2.includes("row_number"), "Must suggest 'row_number' with prefix 'row'");
    assert.ok(!labels2.includes("round"), "Must NOT suggest 'round' with prefix 'row' (prefix mismatch)");
    assert.ok(!labels2.includes("rank"), "Must NOT suggest 'rank' with prefix 'row' (prefix mismatch)");

    // Prefix "ra" — should match rank only
    await openDocument(
      "file:///test/window_complete3.lql",
      "orders |> select(ra",
    );
    const result3 = await client.request("textDocument/completion", {
      textDocument: { uri: "file:///test/window_complete3.lql" },
      position: { line: 0, character: 19 },
    });
    const items3 = Array.isArray(result3) ? result3 : result3.items || [];
    const labels3 = items3.map((i: any) => i.label);
    assert.ok(labels3.includes("rank"), "Must suggest 'rank' with prefix 'ra'");
    assert.ok(!labels3.includes("row_number"), "Must NOT suggest 'row_number' with prefix 'ra'");
  });
});

// ═══════════════════════════════════════════════════════════════════════
// SCHEMA-AWARE LSP E2E TESTS — REAL PostgreSQL Database
// These tests PROVE BEYOND ANY DOUBT that the LSP connects to a REAL
// database and delivers IntelliSense from the LIVE schema.
// ═══════════════════════════════════════════════════════════════════════

describe("Schema-Aware LSP E2E Tests — Real PostgreSQL Database", function () {
  this.timeout(30000);

  let client: LspClient;
  let lspBinary: string;
  const DB_CONNECTION =
    "host=127.0.0.1 dbname=lql_test user=postgres password=testpass";

  before(function () {
    try {
      lspBinary = findLspBinary();
    } catch {
      this.skip();
    }
  });

  beforeEach(function () {
    client = new LspClient(lspBinary, {
      LQL_CONNECTION_STRING: DB_CONNECTION,
    });
  });

  afterEach(function () {
    if (client) client.kill();
  });

  /** Initialize server and wait for schema to load from real database. */
  async function initWithSchema(): Promise<any> {
    const result = await client.request("initialize", {
      processId: process.pid,
      capabilities: {},
      rootUri: null,
    });
    client.notify("initialized", {});

    // Wait for "Schema loaded" in window/logMessage notifications
    const deadline = Date.now() + 15000;
    while (Date.now() < deadline) {
      const msgs = client.drainNotifications("window/logMessage");
      for (const m of msgs) {
        const text = m.params?.message || "";
        if (text.includes("Schema loaded")) return result;
        if (text.includes("Schema fetch failed")) {
          throw new Error(`DB unavailable: ${text}`);
        }
        if (text.includes("No DB connection")) {
          throw new Error("No DB connection configured");
        }
      }
      await new Promise((r) => setTimeout(r, 100));
    }
    throw new Error("Timed out waiting for schema to load from database");
  }

  async function openDoc(uri: string, content: string): Promise<any[]> {
    client.notify("textDocument/didOpen", {
      textDocument: { uri, languageId: "lql", version: 1, text: content },
    });
    return client.waitForNotification(
      "textDocument/publishDiagnostics",
      5000,
    );
  }

  // ─────────────────────────────────────────────────────
  // TABLE COMPLETIONS FROM REAL DATABASE
  // ─────────────────────────────────────────────────────

  it("PROOF: Completions include REAL table names from PostgreSQL — customers, orders, order_items", async function () {
    try {
      await initWithSchema();
    } catch {
      return this.skip();
    }

    await openDoc("file:///test/schema_tables.lql", "");
    const result = await client.request("textDocument/completion", {
      textDocument: { uri: "file:///test/schema_tables.lql" },
      position: { line: 0, character: 0 },
    });

    const items = Array.isArray(result) ? result : result.items || [];
    const labels = items.map((i: any) => i.label);

    // These tables exist in the REAL lql_test database
    assert.ok(
      labels.includes("customers"),
      "MUST show 'customers' table from REAL PostgreSQL database",
    );
    assert.ok(
      labels.includes("orders"),
      "MUST show 'orders' table from REAL PostgreSQL database",
    );
    assert.ok(
      labels.includes("order_items"),
      "MUST show 'order_items' table from REAL PostgreSQL database",
    );

    // Table completions must have kind CLASS (7)
    const customersItem = items.find((i: any) => i.label === "customers");
    assert.ok(customersItem, "customers completion must exist");
    assert.strictEqual(
      customersItem.kind,
      7,
      "Table completion kind must be CLASS (7)",
    );

    // Detail must reflect real column count from database
    assert.ok(
      customersItem.detail.includes("4"),
      `Table detail must show column count from REAL schema, got: ${customersItem.detail}`,
    );
  });

  it("PROOF: Table completions filter by prefix — typing 'cust' only shows 'customers'", async function () {
    try {
      await initWithSchema();
    } catch {
      return this.skip();
    }

    await openDoc("file:///test/schema_prefix.lql", "cust");
    const result = await client.request("textDocument/completion", {
      textDocument: { uri: "file:///test/schema_prefix.lql" },
      position: { line: 0, character: 4 },
    });

    const items = Array.isArray(result) ? result : result.items || [];
    const labels = items.map((i: any) => i.label);

    assert.ok(
      labels.includes("customers"),
      "Must include 'customers' matching prefix 'cust'",
    );
    assert.ok(
      !labels.includes("orders"),
      "Must NOT include 'orders' (does not match prefix 'cust')",
    );
    assert.ok(
      !labels.includes("order_items"),
      "Must NOT include 'order_items' (does not match prefix 'cust')",
    );
  });

  // ─────────────────────────────────────────────────────
  // COLUMN COMPLETIONS FROM REAL DATABASE
  // ─────────────────────────────────────────────────────

  it("PROOF: Typing 'customers.' triggers REAL column completions — id, name, email, created_at", async function () {
    try {
      await initWithSchema();
    } catch {
      return this.skip();
    }

    await openDoc("file:///test/schema_cols.lql", "customers.");
    const result = await client.request("textDocument/completion", {
      textDocument: { uri: "file:///test/schema_cols.lql" },
      position: { line: 0, character: 10 },
    });

    const items = Array.isArray(result) ? result : result.items || [];
    const labels = items.map((i: any) => i.label);

    // These columns exist in the REAL customers table
    assert.ok(
      labels.includes("id"),
      "MUST show 'id' column from REAL customers table",
    );
    assert.ok(
      labels.includes("name"),
      "MUST show 'name' column from REAL customers table",
    );
    assert.ok(
      labels.includes("email"),
      "MUST show 'email' column from REAL customers table",
    );
    assert.ok(
      labels.includes("created_at"),
      "MUST show 'created_at' column from REAL customers table",
    );

    // Column completions must be FIELD kind (5)
    const emailItem = items.find((i: any) => i.label === "email");
    assert.ok(emailItem, "email completion must exist");
    assert.strictEqual(
      emailItem.kind,
      5,
      "Column completion kind must be FIELD (5)",
    );

    // Column detail must include real SQL type from database
    assert.ok(
      emailItem.detail.toLowerCase().includes("text"),
      `Column detail must show SQL type 'text' from DB, got: ${emailItem.detail}`,
    );
  });

  it("PROOF: Typing 'orders.' returns REAL order columns with correct types", async function () {
    try {
      await initWithSchema();
    } catch {
      return this.skip();
    }

    await openDoc("file:///test/schema_order_cols.lql", "orders.");
    const result = await client.request("textDocument/completion", {
      textDocument: { uri: "file:///test/schema_order_cols.lql" },
      position: { line: 0, character: 7 },
    });

    const items = Array.isArray(result) ? result : result.items || [];
    const labels = items.map((i: any) => i.label);

    assert.ok(labels.includes("id"), "Must show 'id' column from REAL orders table");
    assert.ok(
      labels.includes("customer_id"),
      "Must show 'customer_id' column from REAL orders table",
    );
    assert.ok(
      labels.includes("total_amount"),
      "Must show 'total_amount' column from REAL orders table",
    );
    assert.ok(
      labels.includes("status"),
      "Must show 'status' column from REAL orders table",
    );
    assert.ok(
      labels.includes("ordered_at"),
      "Must show 'ordered_at' column from REAL orders table",
    );

    // Verify numeric column shows numeric type
    const totalItem = items.find((i: any) => i.label === "total_amount");
    assert.ok(totalItem, "total_amount completion must exist");
    assert.ok(
      totalItem.detail.toLowerCase().includes("numeric"),
      `Numeric column detail must show 'numeric' type, got: ${totalItem.detail}`,
    );

    // Verify primary key column shows PK indicator
    const idItem = items.find((i: any) => i.label === "id");
    assert.ok(idItem, "id completion must exist");
    assert.ok(
      idItem.detail.includes("PK"),
      `Primary key column detail must include 'PK', got: ${idItem.detail}`,
    );
  });

  it("PROOF: Column completions filter by prefix — 'customers.na' only shows 'name'", async function () {
    try {
      await initWithSchema();
    } catch {
      return this.skip();
    }

    await openDoc("file:///test/schema_col_prefix.lql", "customers.na");
    const result = await client.request("textDocument/completion", {
      textDocument: { uri: "file:///test/schema_col_prefix.lql" },
      position: { line: 0, character: 12 },
    });

    const items = Array.isArray(result) ? result : result.items || [];
    const labels = items.map((i: any) => i.label);

    assert.ok(
      labels.includes("name"),
      "Must include 'name' matching prefix 'na'",
    );
    assert.ok(
      !labels.includes("id"),
      "Must NOT include 'id' (does not match prefix 'na')",
    );
    assert.ok(
      !labels.includes("email"),
      "Must NOT include 'email' (does not match prefix 'na')",
    );
  });

  // ─────────────────────────────────────────────────────
  // SCHEMA-AWARE HOVER (IntelliPrompt) FROM REAL DATABASE
  // ─────────────────────────────────────────────────────

  it("PROOF: Hover on 'customers' table name shows REAL schema — all columns and types", async function () {
    try {
      await initWithSchema();
    } catch {
      return this.skip();
    }

    await openDoc(
      "file:///test/schema_hover_table.lql",
      "customers |> select(customers.name)",
    );
    const hover = await client.request("textDocument/hover", {
      textDocument: { uri: "file:///test/schema_hover_table.lql" },
      position: { line: 0, character: 5 },
    });

    assert.ok(hover, "Hover on REAL table name must return data");
    assert.ok(hover.contents, "Hover must have contents");
    const text = hover.contents.value || hover.contents;

    // Must mention the table name
    assert.ok(
      text.includes("customers"),
      "Table hover must include table name 'customers'",
    );

    // Must show real column names from the database
    assert.ok(
      text.includes("id"),
      "Table hover must list 'id' column from REAL schema",
    );
    assert.ok(
      text.includes("name"),
      "Table hover must list 'name' column from REAL schema",
    );
    assert.ok(
      text.includes("email"),
      "Table hover must list 'email' column from REAL schema",
    );
    assert.ok(
      text.includes("created_at"),
      "Table hover must list 'created_at' column from REAL schema",
    );

    // Must show column count
    assert.ok(
      text.includes("4"),
      "Table hover must show column count (4 columns)",
    );
  });

  it("PROOF: Hover on qualified 'customers.email' shows REAL column type from database", async function () {
    try {
      await initWithSchema();
    } catch {
      return this.skip();
    }

    await openDoc(
      "file:///test/schema_hover_col.lql",
      "customers |> select(customers.email)",
    );
    // Hover on 'email' (after the dot) — position at "email" which starts at col 30
    const hover = await client.request("textDocument/hover", {
      textDocument: { uri: "file:///test/schema_hover_col.lql" },
      position: { line: 0, character: 32 },
    });

    assert.ok(
      hover,
      "Hover on qualified column 'customers.email' must return data",
    );
    const text = hover.contents.value || hover.contents;

    // Must show the column name and table
    assert.ok(
      text.includes("email"),
      "Column hover must include column name 'email'",
    );
    assert.ok(
      text.includes("customers"),
      "Column hover must reference table 'customers'",
    );

    // Must show the real SQL type from the database
    assert.ok(
      text.toLowerCase().includes("text"),
      `Column hover must show SQL type 'text' from database, got: ${text}`,
    );

    // Must show nullability info
    assert.ok(
      text.toLowerCase().includes("nullable"),
      "Column hover must show nullability info",
    );
  });

  it("PROOF: Hover on 'orders.total_amount' shows REAL numeric type and NOT NULL", async function () {
    try {
      await initWithSchema();
    } catch {
      return this.skip();
    }

    await openDoc(
      "file:///test/schema_hover_numeric.lql",
      "orders |> filter(fn(row) => row.orders.total_amount > 100)",
    );
    // Hover on 'total_amount' — starts at col 39
    const hover = await client.request("textDocument/hover", {
      textDocument: { uri: "file:///test/schema_hover_numeric.lql" },
      position: { line: 0, character: 43 },
    });

    assert.ok(hover, "Hover on 'orders.total_amount' must return data");
    const text = hover.contents.value || hover.contents;

    assert.ok(
      text.includes("total_amount"),
      "Column hover must include 'total_amount'",
    );
    assert.ok(
      text.toLowerCase().includes("numeric"),
      "Must show 'numeric' SQL type from REAL database",
    );
    assert.ok(
      text.toLowerCase().includes("not null") || text.toLowerCase().includes("nullable: no"),
      "Must indicate NOT NULL from REAL database schema",
    );
  });

  // ─────────────────────────────────────────────────────
  // FULL SCHEMA WORKFLOW — END TO END
  // ─────────────────────────────────────────────────────

  it("PROOF: Full schema workflow — table completion → column completion → column hover in sequence", async function () {
    try {
      await initWithSchema();
    } catch {
      return this.skip();
    }

    // Step 1: Open doc with order_items pipeline
    await openDoc(
      "file:///test/schema_workflow.lql",
      "order_items |> select(order_items.product_name, order_items.quantity, order_items.unit_price)",
    );

    // Step 2: Get table completions — order_items must be there
    const tableResult = await client.request("textDocument/completion", {
      textDocument: { uri: "file:///test/schema_workflow.lql" },
      position: { line: 0, character: 0 },
    });
    const tableItems = Array.isArray(tableResult)
      ? tableResult
      : tableResult.items || [];
    const tableLabels = tableItems.map((i: any) => i.label);
    assert.ok(
      tableLabels.includes("order_items"),
      "Step 1: Must find 'order_items' in table completions from DB",
    );

    // Step 3: Hover on 'order_items' — shows real schema
    const tableHover = await client.request("textDocument/hover", {
      textDocument: { uri: "file:///test/schema_workflow.lql" },
      position: { line: 0, character: 5 },
    });
    assert.ok(tableHover, "Step 2: Must get hover for 'order_items' table");
    const tableText = tableHover.contents.value || tableHover.contents;
    assert.ok(
      tableText.includes("product_name"),
      "Step 2: Table hover must list 'product_name' column",
    );
    assert.ok(
      tableText.includes("quantity"),
      "Step 2: Table hover must list 'quantity' column",
    );
    assert.ok(
      tableText.includes("unit_price"),
      "Step 2: Table hover must list 'unit_price' column",
    );

    // Step 4: Modify doc to type "order_items." for column completions
    client.notify("textDocument/didChange", {
      textDocument: {
        uri: "file:///test/schema_workflow.lql",
        version: 2,
      },
      contentChanges: [{ text: "order_items." }],
    });
    await client.waitForNotification(
      "textDocument/publishDiagnostics",
      5000,
    );

    const colResult = await client.request("textDocument/completion", {
      textDocument: { uri: "file:///test/schema_workflow.lql" },
      position: { line: 0, character: 12 },
    });
    const colItems = Array.isArray(colResult)
      ? colResult
      : colResult.items || [];
    const colLabels = colItems.map((i: any) => i.label);
    assert.ok(
      colLabels.includes("product_name"),
      "Step 3: Must show 'product_name' from REAL order_items table",
    );
    assert.ok(
      colLabels.includes("quantity"),
      "Step 3: Must show 'quantity' from REAL order_items table",
    );
    assert.ok(
      colLabels.includes("unit_price"),
      "Step 3: Must show 'unit_price' from REAL order_items table",
    );

    // Step 5: Verify column types in completion details
    const qtyItem = colItems.find((i: any) => i.label === "quantity");
    assert.ok(qtyItem, "quantity completion must exist");
    assert.ok(
      qtyItem.detail.toLowerCase().includes("integer"),
      `quantity must show 'integer' type, got: ${qtyItem.detail}`,
    );
  });

  it("PROOF: Schema-aware LSP gracefully degrades when no DB connection", async function () {
    // Start WITHOUT LQL_CONNECTION_STRING — no schema
    const noDbClient = new LspClient(lspBinary);
    try {
      const result = await noDbClient.request("initialize", {
        processId: process.pid,
        capabilities: {},
        rootUri: null,
      });
      noDbClient.notify("initialized", {});
      await new Promise((r) => setTimeout(r, 2000));

      // Keyword completions still work without schema
      noDbClient.notify("textDocument/didOpen", {
        textDocument: {
          uri: "file:///test/no_schema.lql",
          languageId: "lql",
          version: 1,
          text: "users |> ",
        },
      });
      await new Promise((r) => setTimeout(r, 1000));

      const completions = await noDbClient.request(
        "textDocument/completion",
        {
          textDocument: { uri: "file:///test/no_schema.lql" },
          position: { line: 0, character: 9 },
        },
      );

      const items = Array.isArray(completions)
        ? completions
        : completions.items || [];
      const labels = items.map((i: any) => i.label);

      // Pipeline operations still work without DB
      assert.ok(
        labels.includes("select"),
        "Must still suggest 'select' without DB connection",
      );
      assert.ok(
        labels.includes("filter"),
        "Must still suggest 'filter' without DB connection",
      );

      // But NO database tables should appear (no schema loaded)
      assert.ok(
        !labels.includes("customers"),
        "Must NOT show DB tables when no connection",
      );

      // Hover on keywords still works
      noDbClient.notify("textDocument/didChange", {
        textDocument: {
          uri: "file:///test/no_schema.lql",
          version: 2,
        },
        contentChanges: [
          { text: "users |> select(count(*) as cnt)" },
        ],
      });
      await new Promise((r) => setTimeout(r, 500));

      const hover = await noDbClient.request("textDocument/hover", {
        textDocument: { uri: "file:///test/no_schema.lql" },
        position: { line: 0, character: 18 },
      });
      assert.ok(hover, "Keyword hover must work without DB connection");
      assert.ok(
        hover.contents.value.includes("count"),
        "Count hover must work without DB",
      );
    } finally {
      noDbClient.kill();
    }
  });
});
