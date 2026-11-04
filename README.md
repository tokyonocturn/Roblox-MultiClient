# Roblox MultiClient

A small local Windows app that lets several Roblox clients run side by side. It is **not** a
launcher: open it once, leave it running, and start Roblox exactly as you always do: the website,
the desktop shortcut, the app. Every client you open stays an ordinary Roblox client.

Roblox MultiClient does not modify Roblox, does not touch accounts, and never makes a network
request of its own. Everything it needs (settings, logs) stays on the machine.

![Roblox MultiClient: multi-instance on, waiting for the first client](docs/dashboard.png)

---

## How it works

Roblox allows one client per session by claiming two named kernel objects when a client starts.
While those names are taken, the client's own single-instance check is disabled and further clients
start normally. Roblox MultiClient holds those two names (`ROBLOX_singletonMutex`,
`ROBLOX_singletonEvent`) for as long as it runs, and gives them back when it exits or when you turn
it off. The mechanism is tested against a stand-in client that enforces the same rule; a second
*real* client under a second account was not launched during verification, as recorded under
[Not proven](docs/VERIFICATION.md#not-proven).

That is the whole trick, and it is deliberately narrow:

- **Nothing to launch.** There is no install picker, no launch button, no arguments box. You open
  Roblox the normal way and press **Refresh** to list it.
- **Nothing to manage.** It watches the clients Windows reports (pid, version, start time and
  path) and never starts, stops, restarts or signals one. It has no handle to a client and no code
  that could kill one.
- **It stays out of the way.** Closing the window hides it to the notification area; multi-instance
  stays on. **Quit** (or the tray menu) turns it off and exits.

If a Roblox client is already running when you start MultiClient, that client owns the name and
further clients are refused until it exits. The window says so, keeps watching, and takes the name
over by itself the moment that client is gone. No rescan, no restart.

## Requirements

| | |
|---|---|
| Release executable | Windows 10 1809+ or Windows 11, x64. No .NET installation needed. |
| Building from source | .NET SDK 10.0.4xx, Windows, x64. |
| Running tests | The same SDK; the tests use a harmless fake client, never Roblox. |

## Install

1. Download `RobloxMultiClient-1.0.0-win-x64.zip` (or `-full.zip`, which also carries the source)
   from the releases page, or build it below.
2. Extract anywhere. The release is a single self-contained `RobloxMultiClient.exe`.
3. Run it. No installer, no admin rights, no runtime download.

Windows SmartScreen may warn about an unsigned binary; that is expected for an unsigned
open-source build. Verify the SHA-256 listed with the release if you want certainty.

## Use

1. **Start Roblox MultiClient first.** It claims the names at launch; a client that is already
   running has already secured itself.
2. Leave it running (in the tray is fine) and open Roblox as usual, as many times as you like.
3. The window lists every client it can see when you press **Refresh**; a client exit updates it by
   itself. **Turn off** gives the names back (normal one-client behaviour returns once a new client
   takes them); **Turn on** claims them again.
4. **Quit** exits. Closing the window only hides it.

The sidebar shows the live state: `Multi-instance` is `on`, `waiting` (a running client still holds
the name) or `off`, and `Clients` counts the Roblox processes the last refresh saw.

### Where data lives

```
%LOCALAPPDATA%\Camo\RobloxMultiClient\
├── settings.json                     window size, human-readable, safe to edit when the app is closed
└── logs\
    └── multiclient-YYYYMMDD.log      one file per day, plain text, appended
```

Nothing is written to the registry, to `Program Files`, or anywhere else.

## Build

```powershell
git clone <your fork>
cd RobloxMultiClient

dotnet build RobloxMultiClient.sln -c Release          # compile everything
dotnet run --project tests/RobloxMultiClient.Tests     # run the test suite
powershell -File scripts/publish.ps1                   # self-contained single-file executable
powershell -File scripts/package.ps1                   # release ZIP with docs and branding
```

`publish.ps1` produces `artifacts\publish\win-x64\RobloxMultiClient.exe` (self-contained single
file, no trimming, because WinForms is not trim-safe). `package.ps1` wraps it with the documentation,
licence and branding assets into `dist\RobloxMultiClient-1.0.0-win-x64.zip` and writes a `.sha256`
sidecar next to it. `package.ps1 -WithSource` produces `dist\RobloxMultiClient-1.0.0-full.zip`:
README, licence, docs, branding, the executable and every tracked source file in one folder, which
is the archive to upload or mirror. Builds are reproducible from any checkout path; the measured hash and the
settings that make that true are in [docs/VERIFICATION.md](docs/VERIFICATION.md).

There are no NuGet dependencies; the solution builds from a clean SDK with no network access
beyond the first restore of the SDK's own targeting packs.

## Tests

```powershell
dotnet run --project tests/RobloxMultiClient.Tests            # unit and process tests
powershell -File scripts/verify-gui.ps1                       # drives the real window (UI Automation)
```

The suite runs against `tests/FakeRobloxClient`, a console program published as
`RobloxPlayerBeta.exe` in a temporary folder. It opens no sockets, exits on request, and enforces
the client's single-instance rule: given the singleton name it calls `CreateEventW` and exits with
code 3 when another client holds the object. That is what makes "a second client is allowed"
observable instead of assumed: the GUI script opens clients outside the app, exactly like a user,
and walks all three states through the real window.

[docs/VERIFICATION.md](docs/VERIFICATION.md) records what both actually did on a real machine.

## Architecture

```
src/RobloxMultiClient.Core        platform-free domain: client model and monitor, settings, the
                                  singleton contract, errors, logging
src/RobloxMultiClient.Windows     the only code that talks to Windows: process observation, named
                                  kernel objects, JSON settings storage, file logging
src/RobloxMultiClient.App         WinForms shell: composition root, status presenter, theme,
                                  controls, window and tray icon
tools/BrandingGenerator           generates the Camo assets in assets/branding
tests/RobloxMultiClient.Tests     test runner and tests
tests/FakeRobloxClient            harmless stand-in for the Roblox client used by tests
```

One owner of state per concern: `ClientMonitor` owns the list of running clients,
`NamedSingletonGuard` owns the claimed names, `SettingsService` owns settings, `ErrorReporter` owns
the error surface, `FileAppLogger` owns the log. The window owns no application state: it renders a
snapshot from `StatusPresenter` and forwards gestures. Details in
[docs/ARCHITECTURE.md](docs/ARCHITECTURE.md).

## Privacy

No telemetry, analytics, crash reporting, update checks, remote configuration, cloud calls or
background connections. No sockets are opened at any point; [docs/PRIVACY.md](docs/PRIVACY.md)
records how that is enforced, and the test suite fails if a networking assembly is referenced or a
network API appears in the source.

## Branding

The Camo identity (palette, logotype, app icon, in-app marks) is generated from code by
`tools/BrandingGenerator`, so the repository contains real vector-drawn assets rather than
placeholders:

| Asset | Use |
|---|---|
| `assets/branding/camo-logo.png` | sidebar mark |
| `assets/branding/camo-icon.ico` | executable, window and tray icon (16 to 256 px) |
| `assets/branding/camo-logotype.png` | wordmark for docs and repository header |
| `assets/branding/camo-banner.png` | repository/social banner |
| `assets/branding/camo-sizes.png` | icon size sheet |
| `assets/branding/palette.json` | the same colours the app draws with |

Regenerate with `dotnet run --project tools/BrandingGenerator`.

## Limitations

- **The claim must come before the client.** A Roblox client that is already running keeps its
  event; MultiClient explains that and takes over once it exits.
- **Packaged (Microsoft Store) Roblox builds** appear in the list but Windows hides part of their
  details from this process; they are listed as an unknown version.
- **Sessions are independent processes, not independent accounts.** Roblox decides what an account
  may do; this app only removes the one-client-per-session restriction.
- **Unsigned build.** Sign the executable yourself if you distribute it widely.

## Legal

Roblox MultiClient is an independent, unofficial tool. It is not affiliated with, endorsed by, or
sponsored by Roblox Corporation. "Roblox" is a trademark of Roblox Corporation, used here only to
describe what the tool interoperates with. Running more than one client may conflict with Roblox's
terms of use or with the rules of individual experiences; you are responsible for how you use it.
The software is provided under the MIT licence, without warranty.

## Licence

[MIT](LICENSE) © 2026 Camo. Third-party components are listed in
[THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).
