Quick build (run from the graphnotelm-fe folder)
The backend is the graphnotelm repo this folder sits in (..). Close the desktop app first — a running graphnotelm.exe can't be replaced.

node desktop.mjs
Publishes the backend into desktop-executable/backend, sets the version in desktop-executable/package.json, builds the front end, and starts the app (npm start).

node desktop.mjs --build
Same, but packages the app (npm run dist) instead of starting it. The output is an installer, desktop-executable/release/GraphNoteLM Setup <version>.exe. It's an installer rather than a portable exe because a portable exe unpacks the whole app into %TEMP% on every launch, which adds several seconds to startup.

Add --cache to either command to skip publishing the backend and reuse the one already in desktop-executable/backend. That folder isn't in git, so a checkout or pull can remove its appsettings.json; --cache then copies it back from the backend's appsettings.json (../appsettings.json), as a publish would.

Every run sets the version to today's year.month.day with a two-digit year (26.10.5). Running again on the same day adds a letter: 26.10.5a, 26.10.5b, … 26.10.5z, then 26.10.5za, 26.10.5zb, and so on.

Manual steps

0. Go to GraphNoteLM, download the executable, and put .exe (ONLY) into backend folder.
dotnet publish graphnotelm.csproj -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true /p:PublishReadyToRun=true -o graphnotelm-fe/desktop-executable/backend
(run from the graphnotelm folder)

1. Build the Front End
npm run build:electron

2. Install dependencies
cd desktop-executable
npm install

3. Package Application
npm run dist
