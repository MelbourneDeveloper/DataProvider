import * as path from "path";
import * as fs from "fs";
import * as vscode from "vscode";
import {
  LanguageClient,
  LanguageClientOptions,
  ServerOptions,
  TransportKind,
} from "vscode-languageclient/node";

let client: LanguageClient | undefined;

/** Find the LSP binary. Tries bundled, release, debug, then PATH. */
function findServerBinary(context: vscode.ExtensionContext): string {
  const candidates = [
    path.join(context.extensionPath, "bin", "lql-lsp"),
    path.join(
      context.extensionPath,
      "..",
      "lql-lsp-rust",
      "target",
      "release",
      "lql-lsp",
    ),
    path.join(
      context.extensionPath,
      "..",
      "lql-lsp-rust",
      "target",
      "debug",
      "lql-lsp",
    ),
  ];

  for (const candidate of candidates) {
    if (fs.existsSync(candidate)) {
      return candidate;
    }
  }

  return "lql-lsp";
}

export function activate(context: vscode.ExtensionContext): void {
  const config = vscode.workspace.getConfiguration("lql");
  const serverEnabled = config.get<boolean>("languageServer.enabled", true);

  if (!serverEnabled) {
    return;
  }

  const serverBinary = findServerBinary(context);

  const serverOptions: ServerOptions = {
    run: {
      command: serverBinary,
      transport: TransportKind.stdio,
    },
    debug: {
      command: serverBinary,
      transport: TransportKind.stdio,
    },
  };

  const clientOptions: LanguageClientOptions = {
    documentSelector: [{ scheme: "file", language: "lql" }],
    synchronize: {
      fileEvents: vscode.workspace.createFileSystemWatcher("**/*.lql"),
    },
    outputChannelName: "LQL Language Server",
  };

  client = new LanguageClient(
    "lql-language-server",
    "LQL Language Server",
    serverOptions,
    clientOptions,
  );

  context.subscriptions.push(
    vscode.commands.registerCommand("lql.formatDocument", async () => {
      const editor = vscode.window.activeTextEditor;
      if (editor && editor.document.languageId === "lql") {
        await vscode.commands.executeCommand("editor.action.formatDocument");
      }
    }),
  );

  context.subscriptions.push(
    vscode.commands.registerCommand("lql.validateDocument", async () => {
      const editor = vscode.window.activeTextEditor;
      if (editor && editor.document.languageId === "lql") {
        vscode.window.showInformationMessage(
          "LQL validation triggered — check the Problems panel for diagnostics.",
        );
      }
    }),
  );

  context.subscriptions.push(
    vscode.commands.registerCommand("lql.showCompiledSql", async () => {
      vscode.window.showInformationMessage(
        "SQL compilation requires the LQL runtime. Use the CLI or Browser app.",
      );
    }),
  );

  client.start();
  context.subscriptions.push({
    dispose: () => {
      if (client) {
        client.stop();
      }
    },
  });
}

export function deactivate(): Thenable<void> | undefined {
  if (!client) {
    return undefined;
  }
  return client.stop();
}
