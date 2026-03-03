"use strict";
var __createBinding = (this && this.__createBinding) || (Object.create ? (function(o, m, k, k2) {
    if (k2 === undefined) k2 = k;
    var desc = Object.getOwnPropertyDescriptor(m, k);
    if (!desc || ("get" in desc ? !m.__esModule : desc.writable || desc.configurable)) {
      desc = { enumerable: true, get: function() { return m[k]; } };
    }
    Object.defineProperty(o, k2, desc);
}) : (function(o, m, k, k2) {
    if (k2 === undefined) k2 = k;
    o[k2] = m[k];
}));
var __setModuleDefault = (this && this.__setModuleDefault) || (Object.create ? (function(o, v) {
    Object.defineProperty(o, "default", { enumerable: true, value: v });
}) : function(o, v) {
    o["default"] = v;
});
var __importStar = (this && this.__importStar) || function (mod) {
    if (mod && mod.__esModule) return mod;
    var result = {};
    if (mod != null) for (var k in mod) if (k !== "default" && Object.prototype.hasOwnProperty.call(mod, k)) __createBinding(result, mod, k);
    __setModuleDefault(result, mod);
    return result;
};
Object.defineProperty(exports, "__esModule", { value: true });
exports.deactivate = exports.activate = void 0;
const path = __importStar(require("path"));
const fs = __importStar(require("fs"));
const vscode = __importStar(require("vscode"));
const node_1 = require("vscode-languageclient/node");
let client;
/** Find the LSP binary. Tries bundled, release, debug, then PATH. */
function findServerBinary(context) {
    const candidates = [
        path.join(context.extensionPath, "bin", "lql-lsp"),
        path.join(context.extensionPath, "..", "lql-lsp-rust", "target", "release", "lql-lsp"),
        path.join(context.extensionPath, "..", "lql-lsp-rust", "target", "debug", "lql-lsp"),
    ];
    for (const candidate of candidates) {
        if (fs.existsSync(candidate)) {
            return candidate;
        }
    }
    return "lql-lsp";
}
function activate(context) {
    const config = vscode.workspace.getConfiguration("lql");
    const serverEnabled = config.get("languageServer.enabled", true);
    if (!serverEnabled) {
        return;
    }
    const serverBinary = findServerBinary(context);
    const serverOptions = {
        run: {
            command: serverBinary,
            transport: node_1.TransportKind.stdio,
        },
        debug: {
            command: serverBinary,
            transport: node_1.TransportKind.stdio,
        },
    };
    const clientOptions = {
        documentSelector: [{ scheme: "file", language: "lql" }],
        synchronize: {
            fileEvents: vscode.workspace.createFileSystemWatcher("**/*.lql"),
        },
        outputChannelName: "LQL Language Server",
    };
    client = new node_1.LanguageClient("lql-language-server", "LQL Language Server", serverOptions, clientOptions);
    context.subscriptions.push(vscode.commands.registerCommand("lql.formatDocument", async () => {
        const editor = vscode.window.activeTextEditor;
        if (editor && editor.document.languageId === "lql") {
            await vscode.commands.executeCommand("editor.action.formatDocument");
        }
    }));
    context.subscriptions.push(vscode.commands.registerCommand("lql.validateDocument", async () => {
        const editor = vscode.window.activeTextEditor;
        if (editor && editor.document.languageId === "lql") {
            vscode.window.showInformationMessage("LQL validation triggered — check the Problems panel for diagnostics.");
        }
    }));
    context.subscriptions.push(vscode.commands.registerCommand("lql.showCompiledSql", async () => {
        vscode.window.showInformationMessage("SQL compilation requires the LQL runtime. Use the CLI or Browser app.");
    }));
    client.start();
    context.subscriptions.push({
        dispose: () => {
            if (client) {
                client.stop();
            }
        },
    });
}
exports.activate = activate;
function deactivate() {
    if (!client) {
        return undefined;
    }
    return client.stop();
}
exports.deactivate = deactivate;
//# sourceMappingURL=extension.js.map