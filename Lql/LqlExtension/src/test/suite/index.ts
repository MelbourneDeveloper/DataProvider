import * as path from "path";
import * as glob from "glob";
import Mocha from "mocha";

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
