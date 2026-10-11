// Builds and runs the GraphNoteLM desktop app.
//
//   node desktop.mjs                   publish the backend, date-stamp the version, build the frontend, run the app
//   node desktop.mjs --build           same, but package the app with electron-builder instead of running it
//   node desktop.mjs --cache           same as the first, but reuse the last published backend instead of publishing it
//   node desktop.mjs --build --cache   same as --build, but reuse the last published backend instead of publishing it
//
// Lives in the graphnotelm-fe folder of the graphnotelm repo, whose root is the backend. Close the
// desktop app first — a running graphnotelm.exe is locked and the backend publish can't replace it.

import { spawnSync } from 'node:child_process'
import { copyFileSync, existsSync, readFileSync, writeFileSync } from 'node:fs'
import path from 'node:path'
import { fileURLToPath, pathToFileURL } from 'node:url'

const ROOT = path.dirname(fileURLToPath(import.meta.url))
const BACKEND_REPO = path.resolve(ROOT, '..')
const DESKTOP = path.join(ROOT, 'desktop-executable')

/**
 * The next version, year.month.day of today, with a two-digit year. Rebuilding on the same day adds a letter counter
 * that starts another letter once it passes "z": 26.10.5 → 26.10.5a → … → 26.10.5z → 26.10.5za.
 */
export function dateVersion(version, today = new Date()) {
  const base = `${today.getFullYear() % 100}.${today.getMonth() + 1}.${today.getDate()}`
  const suffix = version.startsWith(base) ? version.slice(base.length) : null
  if (suffix == null || !/^[a-z]*$/.test(suffix)) return base

  const last = suffix.at(-1)
  if (last === undefined || last === 'z') return `${base}${suffix}a`
  return `${base}${suffix.slice(0, -1)}${String.fromCharCode(last.charCodeAt(0) + 1)}`
}

/**
 * Sets the version in package.json and package-lock.json directly, keeping each file's line endings.
 * npm version would rewrite a letter suffix as a prerelease (26.10.5a → 26.10.5-a).
 */
function writeVersion(version) {
  for (const file of ['package.json', 'package-lock.json']) {
    const filePath = path.join(DESKTOP, file)
    if (!existsSync(filePath)) continue
    const text = readFileSync(filePath, 'utf8')
    const json = JSON.parse(text)
    json.version = version
    if (json.packages?.['']) json.packages[''].version = version
    const eol = text.includes('\r\n') ? '\r\n' : '\n'
    writeFileSync(filePath, (JSON.stringify(json, null, 2) + '\n').replace(/\n/g, eol))
  }
}

function run(step, command, cwd) {
  console.log(`\n▶ ${step}\n  ${command}  (in ${path.relative(path.dirname(ROOT), cwd) || cwd})`)
  // A shell resolves npm/dotnet on every platform (npm is npm.cmd on Windows).
  const result = spawnSync(command, { cwd, stdio: 'inherit', shell: true })
  if (result.status !== 0) {
    console.error(`\n✖ ${step} failed${result.status != null ? ` (exit code ${result.status})` : ''}.`)
    process.exit(result.status || 1)
  }
}

function main() {
  const args = process.argv.slice(2)
  const unknown = args.filter(a => a !== '--build' && a !== '--cache')
  if (unknown.length) {
    console.error(`Unknown option: ${unknown.join(' ')}\nUsage: node desktop.mjs [--build] [--cache]`)
    process.exit(1)
  }
  const packageApp = args.includes('--build')
  const useCachedBackend = args.includes('--cache')
  const backendDir = path.join(DESKTOP, 'backend')
  if (useCachedBackend) {
    // --cache: keep the backend already in desktop-executable/backend.
    if (!existsSync(path.join(backendDir, 'graphnotelm.exe'))) {
      console.error(`No published backend in ${backendDir} — run without --cache once to publish it.`)
      process.exit(1)
    }
    console.log(`\n▶ Reusing published backend in ${path.relative(path.dirname(ROOT), backendDir)}`)

    // The backend folder isn't in git, so a checkout or pull can remove its settings while the exe
    // stays. Without appsettings.json the backend starts without its local database and every request
    // fails. Publishing would copy it from the backend repo, so do the same here.
    const settings = path.join(backendDir, 'appsettings.json')
    if (!existsSync(settings)) {
      const source = path.join(BACKEND_REPO, 'appsettings.json')
      if (!existsSync(source)) {
        console.error(`No appsettings.json in ${backendDir}, and none in ${BACKEND_REPO} to copy. ` +
          'The backend needs one with a LocalDB connection string to run locally.')
        process.exit(1)
      }
      copyFileSync(source, settings)
      console.log(`  appsettings.json was missing — copied it from ${path.relative(path.dirname(ROOT), source)}`)
    }
  } else {
    if (!existsSync(path.join(BACKEND_REPO, 'graphnotelm.csproj'))) {
      console.error(`Backend not found at ${BACKEND_REPO} — expected this folder to be inside the graphnotelm repo.`)
      process.exit(1)
    }

    // 1. Publish the backend straight into the desktop app's backend folder. ReadyToRun precompiles it,
    //    which halves its startup (~1.05 s → ~0.5 s to listening) at the cost of a larger exe (116 → 176 MB).
    run('Publishing backend',
      `dotnet publish graphnotelm.csproj -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true /p:PublishReadyToRun=true -o "${backendDir}"`,
      BACKEND_REPO)
  }

  // 2. Date-stamp the desktop app's version.
  const current = JSON.parse(readFileSync(path.join(DESKTOP, 'package.json'), 'utf8')).version
  const next = dateVersion(current)
  console.log(`\n▶ Setting version ${current} → ${next}`)
  writeVersion(next)

  // 3. Build the frontend into desktop-executable/frontend/dist.
  run('Building frontend', 'npm run build:electron', ROOT)

  if (!existsSync(path.join(DESKTOP, 'node_modules')))
    run('Installing desktop dependencies', 'npm install', DESKTOP)

  // 4. Run the app, or package it into desktop-executable/release.
  if (packageApp) {
    run('Packaging app', 'npm run dist', DESKTOP)
    console.log(`\n✔ GraphNoteLM ${next} packaged in ${path.join(DESKTOP, 'release')}`)
  } else {
    run(`Starting GraphNoteLM ${next}`, 'npm start', DESKTOP)
  }
}

// Run only when executed directly, so dateVersion can be imported on its own.
if (process.argv[1] && import.meta.url === pathToFileURL(path.resolve(process.argv[1])).href) main()
