import { fileURLToPath } from 'node:url';
import { build } from 'esbuild';
import { spawnSync } from 'node:child_process';
import { mkdir, rm, rmdir } from 'node:fs/promises';

const directory = new URL('../.test-output/', import.meta.url);
await mkdir(directory, { recursive: true });
const output = new URL('api-failures.test.mjs', directory);
try {
  await build({ entryPoints: ['tests/api-failures.test.ts'], outfile: fileURLToPath(output),
    bundle: true, platform: 'node', format: 'esm', packages: 'external', target: 'node24' });
  const result = spawnSync(process.execPath, ['--test', '--test-isolation=none', ...process.argv.slice(2), fileURLToPath(output)], { stdio: 'inherit' });
  if (result.error) throw result.error;
  process.exitCode = result.status ?? 1;
} finally {
  await rm(output, { force: true });
  await rmdir(directory).catch(() => undefined);
}
