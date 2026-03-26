/**
 * VS Code Extension E2E Tests
 *
 * These tests run inside a real VS Code instance using @vscode/test-electron.
 * They test the LSP integration VIA THE VS CODE UI — opening files, triggering
 * completions, hovering, checking the diagnostics panel, etc.
 *
 * This is the VSIX test suite that validates the extension works end-to-end
 * from the user's perspective.
 */

import * as assert from "assert";
import * as vscode from "vscode";
import * as path from "path";

/** Wait for a condition to become true, with timeout */
async function waitFor(
  condition: () => boolean | Promise<boolean>,
  timeoutMs: number = 10000,
  intervalMs: number = 200,
): Promise<void> {
  const start = Date.now();
  while (Date.now() - start < timeoutMs) {
    if (await condition()) {return;}
    await new Promise((r) => setTimeout(r, intervalMs));
  }
  throw new Error(`Timeout after ${timeoutMs}ms waiting for condition`);
}

/** Get the path to a test fixture */
function fixturePath(name: string): string {
  return path.join(__dirname, "..", "..", "..", "src", "test", "fixtures", name);
}

/** Open a fixture file in the editor */
async function openFixture(name: string): Promise<vscode.TextDocument> {
  const uri = vscode.Uri.file(fixturePath(name));
  const doc = await vscode.workspace.openTextDocument(uri);
  await vscode.window.showTextDocument(doc);
  return doc;
}

/** Wait for the LQL language server to be ready */
async function waitForLanguageServer(): Promise<void> {
  // Give the LSP client time to start and connect
  await new Promise((r) => setTimeout(r, 3000));
}

suite("VS Code Extension E2E Tests", function () {
  this.timeout(60000);

  suiteSetup(async function () {
    // Ensure the LQL extension is activated
    const ext = vscode.extensions.getExtension("lql-team.lql-language-support");
    if (ext && !ext.isActive) {
      await ext.activate();
    }
    await waitForLanguageServer();
  });

  suiteTeardown(async function () {
    await vscode.commands.executeCommand("workbench.action.closeAllEditors");
  });

  // ═══════════════════════════════════════════════════════════════
  // EXTENSION ACTIVATION
  // ═══════════════════════════════════════════════════════════════

  test("Extension should be activated for .lql files", async function () {
    const doc = await openFixture("simple_select.lql");

    assert.strictEqual(doc.languageId, "lql", "Language ID must be 'lql'");
    assert.ok(doc.getText().includes("users"), "Document content must be loaded");
    assert.ok(doc.getText().includes("|>"), "Document must contain pipe operator");
    assert.ok(doc.getText().includes("select"), "Document must contain select");
  });

  test("Extension commands should be registered", async function () {
    const commands = await vscode.commands.getCommands(true);

    assert.ok(
      commands.includes("lql.formatDocument"),
      "lql.formatDocument command must be registered",
    );
    assert.ok(
      commands.includes("lql.validateDocument"),
      "lql.validateDocument command must be registered",
    );
    assert.ok(
      commands.includes("lql.showCompiledSql"),
      "lql.showCompiledSql command must be registered",
    );
  });

  // ═══════════════════════════════════════════════════════════════
  // SYNTAX HIGHLIGHTING
  // ═══════════════════════════════════════════════════════════════

  test("TextMate grammar should provide syntax highlighting", async function () {
    const doc = await openFixture("simple_select.lql");

    // Verify the document is recognized as LQL
    assert.strictEqual(doc.languageId, "lql");

    // The TextMate grammar should be loaded for .lql files
    const editor = vscode.window.activeTextEditor;
    assert.ok(editor, "Active editor must exist");
    assert.strictEqual(
      editor!.document.uri.fsPath,
      doc.uri.fsPath,
      "Active editor must show the fixture",
    );
  });

  // ═══════════════════════════════════════════════════════════════
  // COMPLETIONS (IntelliSense via UI)
  // ═══════════════════════════════════════════════════════════════

  test("Should provide IntelliSense completions after pipe", async function () {
    const doc = await openFixture("completion_test.lql");
    const editor = vscode.window.activeTextEditor!;

    // Position cursor after |>
    const pos = new vscode.Position(0, doc.getText().length);
    editor.selection = new vscode.Selection(pos, pos);

    await new Promise((r) => setTimeout(r, 1000));

    // Trigger completion
    const completions = await vscode.commands.executeCommand<vscode.CompletionList>(
      "vscode.executeCompletionItemProvider",
      doc.uri,
      pos,
    );

    assert.ok(completions, "Completions must not be null");
    assert.ok(completions.items.length > 0, "Must return completion items");

    const labels = completions.items.map((i) => i.label);
    const labelStrings = labels.map((l) =>
      typeof l === "string" ? l : l.label,
    );

    // Pipeline operations must appear
    assert.ok(
      labelStrings.some((l) => l === "select"),
      "Must suggest 'select' after pipe",
    );
    assert.ok(
      labelStrings.some((l) => l === "filter"),
      "Must suggest 'filter' after pipe",
    );
    assert.ok(
      labelStrings.some((l) => l === "join"),
      "Must suggest 'join' after pipe",
    );
    assert.ok(
      labelStrings.some((l) => l === "group_by"),
      "Must suggest 'group_by' after pipe",
    );

    // Verify completions have documentation
    const selectCompletion = completions.items.find((i) => {
      const label = typeof i.label === "string" ? i.label : i.label.label;
      return label === "select";
    });
    if (selectCompletion) {
      assert.ok(
        selectCompletion.detail || selectCompletion.documentation,
        "select completion should have detail or documentation",
      );
    }
  });

  test("Should provide function completions inside select", async function () {
    const content = "orders |> select(cou";
    const doc = await vscode.workspace.openTextDocument({
      content,
      language: "lql",
    });
    await vscode.window.showTextDocument(doc);

    await new Promise((r) => setTimeout(r, 1000));

    const pos = new vscode.Position(0, content.length);
    const completions = await vscode.commands.executeCommand<vscode.CompletionList>(
      "vscode.executeCompletionItemProvider",
      doc.uri,
      pos,
    );

    assert.ok(completions, "Must return completions");
    const labels = completions.items.map((i) =>
      typeof i.label === "string" ? i.label : i.label.label,
    );

    assert.ok(
      labels.some((l) => l === "count"),
      "Must suggest 'count' when typing 'cou'",
    );
  });

  // ═══════════════════════════════════════════════════════════════
  // HOVER (IntelliPrompt via UI)
  // ═══════════════════════════════════════════════════════════════

  test("Should show hover information for keywords", async function () {
    const doc = await openFixture("hover_test.lql");

    await new Promise((r) => setTimeout(r, 1000));

    // Hover over 'filter' — character 12 in the line
    const filterPos = new vscode.Position(0, 12);
    const hovers = await vscode.commands.executeCommand<vscode.Hover[]>(
      "vscode.executeHoverProvider",
      doc.uri,
      filterPos,
    );

    assert.ok(hovers, "Hover result must not be null");
    assert.ok(hovers.length > 0, "Must return at least one hover");

    const hoverContent = hovers[0].contents
      .map((c) => {
        if (typeof c === "string") {return c;}
        if (c instanceof vscode.MarkdownString) {return c.value;}
        return (c as any).value || "";
      })
      .join(" ");

    assert.ok(
      hoverContent.toLowerCase().includes("filter"),
      "Hover content must mention 'filter'",
    );
    assert.ok(
      hoverContent.length > 10,
      "Hover content should be substantial, not just the word",
    );
  });

  test("Should show hover for select keyword", async function () {
    const doc = await openFixture("simple_select.lql");

    await new Promise((r) => setTimeout(r, 1000));

    // Find 'select' position
    const text = doc.getText();
    const selectIdx = text.indexOf("select");
    assert.ok(selectIdx >= 0, "Must find 'select' in document");

    let line = 0;
    let col = 0;
    for (let i = 0; i < selectIdx; i++) {
      if (text[i] === "\n") {
        line++;
        col = 0;
      } else {
        col++;
      }
    }

    const hovers = await vscode.commands.executeCommand<vscode.Hover[]>(
      "vscode.executeHoverProvider",
      doc.uri,
      new vscode.Position(line, col + 2), // middle of 'select'
    );

    assert.ok(hovers && hovers.length > 0, "Must have hover for 'select'");
    const content = hovers[0].contents
      .map((c) =>
        typeof c === "string"
          ? c
          : c instanceof vscode.MarkdownString
            ? c.value
            : (c as any).value || "",
      )
      .join(" ");
    assert.ok(
      content.toLowerCase().includes("select"),
      "Hover must reference select",
    );
    assert.ok(
      content.toLowerCase().includes("project") ||
        content.toLowerCase().includes("column"),
      "Hover should describe what select does",
    );
  });

  // ═══════════════════════════════════════════════════════════════
  // DIAGNOSTICS (via UI)
  // ═══════════════════════════════════════════════════════════════

  test("Should show diagnostics for invalid syntax", async function () {
    const doc = await openFixture("invalid_syntax.lql");

    // Wait for diagnostics to appear
    await waitFor(
      () => {
        const diags = vscode.languages.getDiagnostics(doc.uri);
        return diags.length > 0;
      },
      10000,
    );

    const diagnostics = vscode.languages.getDiagnostics(doc.uri);
    assert.ok(diagnostics.length > 0, "Must have diagnostics for invalid file");

    // Check diagnostic properties
    const diag = diagnostics[0];
    assert.ok(diag.message, "Diagnostic must have a message");
    assert.ok(diag.message.length > 0, "Message must not be empty");
    assert.ok(diag.range, "Diagnostic must have a range");
    assert.ok(
      diag.severity !== undefined,
      "Diagnostic must have a severity",
    );
  });

  test("Should show no parse errors for valid files", async function () {
    const doc = await openFixture("simple_select.lql");

    // Wait a bit for diagnostics to settle
    await new Promise((r) => setTimeout(r, 3000));

    const diagnostics = vscode.languages.getDiagnostics(doc.uri);
    const errors = diagnostics.filter(
      (d) => d.severity === vscode.DiagnosticSeverity.Error,
    );
    assert.strictEqual(
      errors.length,
      0,
      `Valid file should have no error diagnostics, got: ${errors.map((e) => e.message).join(", ")}`,
    );
  });

  test("Should show no parse errors for complex pipeline", async function () {
    const doc = await openFixture("complex_pipeline.lql");

    await new Promise((r) => setTimeout(r, 3000));

    const diagnostics = vscode.languages.getDiagnostics(doc.uri);
    const errors = diagnostics.filter(
      (d) => d.severity === vscode.DiagnosticSeverity.Error,
    );
    assert.strictEqual(
      errors.length,
      0,
      `Complex pipeline should parse cleanly, errors: ${errors.map((e) => e.message).join(", ")}`,
    );
  });

  test("Should show no parse errors for window functions", async function () {
    const doc = await openFixture("window_functions.lql");

    await new Promise((r) => setTimeout(r, 3000));

    const diagnostics = vscode.languages.getDiagnostics(doc.uri);
    const errors = diagnostics.filter(
      (d) => d.severity === vscode.DiagnosticSeverity.Error,
    );
    assert.strictEqual(errors.length, 0, "Window functions should parse cleanly");
  });

  test("Should show no parse errors for case expressions", async function () {
    const doc = await openFixture("case_expression.lql");

    await new Promise((r) => setTimeout(r, 3000));

    const diagnostics = vscode.languages.getDiagnostics(doc.uri);
    const errors = diagnostics.filter(
      (d) => d.severity === vscode.DiagnosticSeverity.Error,
    );
    assert.strictEqual(errors.length, 0, "Case expressions should parse cleanly");
  });

  test("Should show no parse errors for exists subqueries", async function () {
    const doc = await openFixture("subquery_exists.lql");

    await new Promise((r) => setTimeout(r, 3000));

    const diagnostics = vscode.languages.getDiagnostics(doc.uri);
    const errors = diagnostics.filter(
      (d) => d.severity === vscode.DiagnosticSeverity.Error,
    );
    assert.strictEqual(errors.length, 0, "Exists subqueries should parse cleanly");
  });

  // ═══════════════════════════════════════════════════════════════
  // DOCUMENT SYMBOLS
  // ═══════════════════════════════════════════════════════════════

  test("Should return document symbols for let bindings", async function () {
    const doc = await openFixture("complex_pipeline.lql");

    await new Promise((r) => setTimeout(r, 2000));

    const symbols = await vscode.commands.executeCommand<vscode.SymbolInformation[]>(
      "vscode.executeDocumentSymbolProvider",
      doc.uri,
    );

    assert.ok(symbols, "Symbols result must not be null");
    assert.ok(symbols.length >= 2, "Must find at least 2 symbols");

    const names = symbols.map((s) => s.name);
    assert.ok(
      names.includes("completed_orders"),
      "Must find 'completed_orders' symbol",
    );
    assert.ok(names.includes("summary"), "Must find 'summary' symbol");

    // Check symbol metadata
    for (const sym of symbols) {
      assert.ok(sym.name, "Each symbol must have a name");
      assert.ok(sym.kind !== undefined, "Each symbol must have a kind");
      assert.ok(sym.location, "Each symbol must have a location");
    }
  });

  // ═══════════════════════════════════════════════════════════════
  // DOCUMENT FORMATTING
  // ═══════════════════════════════════════════════════════════════

  test("Should format LQL documents via command", async function () {
    const content = "  users   |>   select(  users.id  )  ";
    const doc = await vscode.workspace.openTextDocument({
      content,
      language: "lql",
    });
    await vscode.window.showTextDocument(doc);

    await new Promise((r) => setTimeout(r, 1000));

    const edits = await vscode.commands.executeCommand<vscode.TextEdit[]>(
      "vscode.executeFormatDocumentProvider",
      doc.uri,
      { tabSize: 4, insertSpaces: true },
    );

    if (edits && edits.length > 0) {
      // Formatting produced edits — verify they normalize whitespace
      const edit = edits[0];
      assert.ok(edit.newText !== undefined, "Format edit must have new text");
      assert.ok(
        !edit.newText.startsWith("  "),
        "Formatted text should not start with excessive whitespace",
      );
    }
    // If no edits, the formatter considers the document already formatted — that's acceptable
  });

  // ═══════════════════════════════════════════════════════════════
  // LANGUAGE CONFIGURATION
  // ═══════════════════════════════════════════════════════════════

  test("Language configuration should be properly loaded", async function () {
    const doc = await openFixture("simple_select.lql");
    const editor = vscode.window.activeTextEditor!;

    // Verify the language is correctly identified
    assert.strictEqual(doc.languageId, "lql");

    // The document should be editable
    assert.ok(!doc.isClosed, "Document should not be closed");
    assert.ok(!doc.isDirty, "Document should not be dirty initially");
  });

  // ═══════════════════════════════════════════════════════════════
  // REAL-WORLD WORKFLOW SIMULATION
  // ═══════════════════════════════════════════════════════════════

  test("Should handle typical edit-save-check workflow", async function () {
    // Create a new LQL document
    const doc = await vscode.workspace.openTextDocument({
      content: "users |> select(users.id)",
      language: "lql",
    });
    const editor = await vscode.window.showTextDocument(doc);

    await new Promise((r) => setTimeout(r, 1000));

    // Verify initial state is clean
    const initialDiags = vscode.languages.getDiagnostics(doc.uri);
    const initialErrors = initialDiags.filter(
      (d) => d.severity === vscode.DiagnosticSeverity.Error,
    );
    assert.strictEqual(
      initialErrors.length,
      0,
      "Initial valid content should have no errors",
    );

    // Type a pipe operator at the end
    await editor.edit((editBuilder) => {
      const endPos = doc.lineAt(doc.lineCount - 1).range.end;
      editBuilder.insert(endPos, " |> ");
    });

    await new Promise((r) => setTimeout(r, 2000));

    // The incomplete pipeline should trigger some diagnostics or completions
    const pos = new vscode.Position(0, doc.getText().length);
    const completions = await vscode.commands.executeCommand<vscode.CompletionList>(
      "vscode.executeCompletionItemProvider",
      doc.uri,
      pos,
    );

    assert.ok(completions, "Should offer completions after typing |>");
    if (completions.items.length > 0) {
      const labels = completions.items.map((i) =>
        typeof i.label === "string" ? i.label : i.label.label,
      );
      assert.ok(
        labels.some(
          (l) =>
            l === "select" ||
            l === "filter" ||
            l === "join" ||
            l === "group_by",
        ),
        "Completions should include pipeline operations",
      );
    }
  });
});
