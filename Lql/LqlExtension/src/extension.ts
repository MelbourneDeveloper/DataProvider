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
let outputChannel: vscode.OutputChannel;

const GITHUB_REPO = "MelbourneDeveloper/DataProvider";

function log(msg: string): void {
  outputChannel.appendLine(`[LQL] ${msg}`);
}

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
  const pkg = context.extension.packageJSON as Record<string, unknown>;
  return String(pkg.version);
}

/** Follow redirects and download a URL to a file path. */
function downloadFile(url: string, dest: string): Promise<void> {
  return new Promise((resolve, reject) => {
    const get = url.startsWith("https") ? https.get : http.get;
    get(url, (response) => {
      const statusCode = response.statusCode ?? 0;
      const location = response.headers.location ?? "";
      if (statusCode >= 300 && statusCode < 400 && location !== "") {
        downloadFile(location, dest).then(resolve, reject);
        return;
      }
      if (statusCode !== 200) {
        reject(new Error(`Download failed: HTTP ${String(statusCode)}`));
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
): Promise<string> {
  const assetName = getLspAssetName();
  if (assetName === undefined) {
    throw new Error(`Unsupported platform: ${process.platform} ${process.arch}`);
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

export async function activate(
  context: vscode.ExtensionContext,
): Promise<void> {
  outputChannel = vscode.window.createOutputChannel("LQL Language Server");
  log("Extension activating...");
  log(`Platform: ${process.platform} ${process.arch}`);
  log(`Global storage: ${context.globalStorageUri.fsPath}`);

  const config = vscode.workspace.getConfiguration("lql");
  const serverEnabled = config.get<boolean>("languageServer.enabled") ?? true;

  if (!serverEnabled) {
    log("Language server disabled in settings.");
    return;
  }

  let serverBinary: string;
  const customPath = config.get<string>("languageServer.path") ?? "";
  if (customPath !== "") {
    if (!fs.existsSync(customPath)) {
      log(`ERROR: Custom LSP path does not exist: ${customPath}`);
      vscode.window.showErrorMessage(
        `LQL: Custom language server path not found: ${customPath}`,
      );
      return;
    }
    serverBinary = customPath;
    log(`LSP binary (custom): ${serverBinary}`);
  } else {
    try {
      serverBinary = await downloadLspBinary(context);
      log(`LSP binary: ${serverBinary}`);
    } catch (err) {
      const message = err instanceof Error ? err.message : String(err);
      log(`ERROR: ${message}`);
      vscode.window.showErrorMessage(
        `LQL: Failed to download language server: ${message}`,
      );
      return;
    }
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

  // Build initializationOptions from VS Code settings
  const connectionString = config.get<string>("database.connectionString") ?? "";
  const aiProvider = config.get<string>("ai.provider") ?? "";
  const aiEndpoint = config.get<string>("ai.endpoint") ?? "http://localhost:11434/api/generate";
  const aiModel = config.get<string>("ai.model") ?? "qwen2.5-coder:1.5b";

  const initOptions: Record<string, unknown> = {};
  if (connectionString !== "") {
    initOptions.connectionString = connectionString;
    log(`Database: ${connectionString}`);
  }
  if (aiProvider !== "") {
    initOptions.aiProvider = {
      provider: aiProvider,
      endpoint: aiEndpoint,
      model: aiModel,
      enabled: true,
    };
    log(`AI provider: ${aiProvider} (${aiModel})`);
  }

  const clientOptions: LanguageClientOptions = {
    documentSelector: [{ scheme: "file", language: "lql" }],
    synchronize: {
      fileEvents: vscode.workspace.createFileSystemWatcher("**/*.lql"),
    },
    outputChannel,
    initializationOptions: initOptions,
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
      if (editor?.document.languageId === "lql") {
        await vscode.commands.executeCommand("editor.action.formatDocument");
      }
    }),
  );

  context.subscriptions.push(
    vscode.commands.registerCommand("lql.validateDocument", () => {
      const editor = vscode.window.activeTextEditor;
      if (editor?.document.languageId === "lql") {
        void vscode.window.showInformationMessage(
          "LQL validation triggered — check the Problems panel for diagnostics.",
        );
      }
    }),
  );

  context.subscriptions.push(
    vscode.commands.registerCommand("lql.showCompiledSql", () => {
      void vscode.window.showInformationMessage(
        "SQL compilation requires the LQL runtime. Use the CLI or Browser app.",
      );
    }),
  );

  log("Starting LSP client...");
  void client.start();
  log("LSP client started.");

  context.subscriptions.push({
    dispose: () => {
      if (client) {
        void client.stop();
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
