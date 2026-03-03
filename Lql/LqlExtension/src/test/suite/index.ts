import * as path from "path";

export function run(): Promise<void> {
  // eslint-disable-next-line @typescript-eslint/no-var-requires
  const Mocha = require("mocha") as typeof import("mocha");
  // eslint-disable-next-line @typescript-eslint/no-var-requires
  const glob = require("glob") as typeof import("glob");

  const mocha = new (Mocha as any)({
    ui: "tdd",
    color: true,
    timeout: 60000,
  });

  const testsRoot = path.resolve(__dirname, ".");

  return new Promise<void>((resolve, reject) => {
    (glob as any)(
      "**/**.test.js",
      { cwd: testsRoot },
      (err: Error | null, files: string[]) => {
        if (err) {
          return reject(err);
        }

        files.forEach((f: string) =>
          mocha.addFile(path.resolve(testsRoot, f)),
        );

        try {
          mocha.run((failures: number) => {
            if (failures > 0) {
              reject(new Error(`${failures} tests failed.`));
            } else {
              resolve();
            }
          });
        } catch (runErr) {
          reject(runErr);
        }
      },
    );
  });
}
