import * as path from "path";
import Mocha from "mocha";
import { glob } from "glob";

export function run(): Promise<void> {
  const mocha = new Mocha({
    ui: "tdd",
    color: true,
    timeout: 60000,
  });

  const testsRoot = path.resolve(__dirname, ".");

  return new Promise<void>((resolve, reject) => {
    glob("**/**.test.js", { cwd: testsRoot })
      .then((files) => {
        files.forEach((f: string) =>
          mocha.addFile(path.resolve(testsRoot, f)),
        );

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
      })
      .catch((err: unknown) => {
        reject(err instanceof Error ? err : new Error(String(err)));
      });
  });
}
