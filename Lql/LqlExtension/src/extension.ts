import * as path from "path";
import * as fs from "fs";
import * as https from "https";
import * as http from "http";
import * as vscode from "vscode";
import {
  LanguageClient,
  LanguageClientOptions,
  ServerOptions,
  TransportKind,
} from "vscode-languageclient/node";

let client: LanguageClient | undefined;

const GITHUB_REPO = "MelbourneDeveloper/DataProvider";

/** Map platform/arch to the release asset name. */
function getLspAssetName(): string | undefined {
  const platform = process.platform;
  const arch = process.arch;

  if (platform === "linux" && arch === "x64") {
    return "lql-lsp-linux-x64";
  }
  if (platform === "darwin" && arch === "x64") {
    return "lql-lsp-darwin-x64";
  }
  if (platform === "darwin" && arch === "arm64") {
    return "lql-lsp-darwin-arm64";
  }
  if (platform === "win32" && arch === "x64") {
    return "lql-lsp-windows-x64.exe";
  }
  return undefined;
}

/** Get the extension version from package.json to find the matching GH release. */
function getExtensionVersion(context: vscode.ExtensionContext): string {
  return context.extension.packageJSON.version as string;
}

/** Follow redirects and download a URL to a file path. */
function downloadFile(url: string, dest: string): Promise<void> {
  return new Promise((resolve, reject) => {
    const get = url.startsWith("https") ? https.get : http.get;
    get(url, (response) => {
      if (
        response.statusCode &&
        response.statusCode >= 300 &&
        response.statusCode < 400 &&
        response.headers.location
      ) {
        downloadFile(response.headers.location, dest).then(resolve, reject);
        return;
      }
      if (response.statusCode !== 200) {
        reject(new Error(`Download failed: HTTP ${response.statusCode}`));
        return;
      }
      const file = fs.createWriteStream(dest);
      response.pipe(file);
      file.on("finish", () => {
        file.close();
        resolve();
      });
      file.on("error", (err) => {
        fs.unlinkSync(dest);
        reject(err);
      });
    }).on("error", reject);
  });
}

/** Download the LSP binary from the GitHub release matching the extension version. */
async function downloadLspBinary(
  context: vscode.ExtensionContext,
): Promise<string | undefined> {
  const assetName = getLspAssetName();
  if (!assetName) {
    return undefined;
  }

  const binDir = path.join(context.globalStorageUri.fsPath, "bin");
  const binaryName =
    process.platform === "win32" ? "lql-lsp.exe" : "lql-lsp";
  const binaryPath = path.join(binDir, binaryName);

  if (fs.existsSync(binaryPath)) {
    return binaryPath;
  }

  const version = getExtensionVersion(context);
  const releaseTag = `v${version}`;
  const downloadUrl = `https://github.com/${GITHUB_REPO}/releases/download/${releaseTag}/${assetName}`;

  fs.mkdirSync(binDir, { recursive: true });

  await vscode.window.withProgress(
    {
      location: vscode.ProgressLocation.Notification,
      title: "LQL: Downloading language server...",
      cancellable: false,
    },
    async () => {
      await downloadFile(downloadUrl, binaryPath);
      if (process.platform !== "win32") {
        fs.chmodSync(binaryPath, 0o755);
      }
    },
  );

  return binaryPath;
}

/** Find the LSP binary. Tries local dev paths, then downloads from GH release. */
async function findServerBinary(
  context: vscode.ExtensionContext,
): Promise<string> {
  // Local dev candidates (not bundled in VSIX, only for local development)
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

  // Check previously downloaded binary
  const binDir = path.join(context.globalStorageUri.fsPath, "bin");
  const binaryName =
    process.platform === "win32" ? "lql-lsp.exe" : "lql-lsp";
  const cachedBinary = path.join(binDir, binaryName);
  if (fs.existsSync(cachedBinary)) {
    return cachedBinary;
  }

  // Download from GitHub release
  const downloaded = await downloadLspBinary(context);
  if (downloaded) {
    return downloaded;
  }

  // Fallback to PATH
  return "lql-lsp";
}

export async function activate(
  context: vscode.ExtensionContext,
): Promise<void> {
  const config = vscode.workspace.getConfiguration("lql");
  const serverEnabled = config.get<boolean>("languageServer.enabled", true);

  if (!serverEnabled) {
    return;
  }

  let serverBinary: string;
  try {
    serverBinary = await findServerBinary(context);
  } catch (err) {
    const message = err instanceof Error ? err.message : String(err);
    vscode.window.showErrorMessage(
      `LQL: Failed to find or download language server: ${message}`,
    );
    return;
  }

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
