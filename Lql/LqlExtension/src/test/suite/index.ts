import * as path from "path";
import * as fs from "fs";
import * as glob from "glob";
import Mocha from "mocha";

function writeCoverageData(): void {
  const coverageData = (
    global as unknown as Record<string, unknown>
  ).__coverage__;
  if (coverageData === undefined || coverageData === null) {
    return;
  }
  const nycOutputDir = path.resolve(__dirname, "../../../.nyc_output");
  fs.mkdirSync(nycOutputDir, { recursive: true });
  fs.writeFileSync(
    path.join(nycOutputDir, "coverage.json"),
    JSON.stringify(coverageData),
  );
}

export function run(): Promise<void> {
  const mocha = new Mocha({
    ui: "tdd",
    color: true,
    timeout: 60000,
  });

  const testsRoot = path.resolve(__dirname, ".");
  const files = glob.sync("**/**.test.js", { cwd: testsRoot });

  files.forEach((f: string) => mocha.addFile(path.resolve(testsRoot, f)));

  return new Promise<void>((resolve, reject) => {
    try {
      mocha.run((failures: number) => {
        writeCoverageData();
        if (failures > 0) {
          reject(new Error(`${String(failures)} tests failed.`));
        } else {
          resolve();
        }
      });
    } catch (runErr: unknown) {
      reject(runErr instanceof Error ? runErr : new Error(String(runErr)));
    }
  });
}
