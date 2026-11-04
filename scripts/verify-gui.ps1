# End-to-end GUI verification for Roblox MultiClient.
#
# Drives the real window through UI Automation while the fictive client (which enforces the same
# named single-instance object the real client creates) is started directly, exactly as a user
# starts Roblox. Proves the three states the app exists for:
#   1. without MultiClient a second client refuses to start (exit code 3);
#   2. with MultiClient running, clients started outside it run side by side and are listed;
#   3. after MultiClient exits, the refusal is back.
# Also proves the window has no launcher, that turn off/on releases and reclaims the names, that the
# app owns no socket, and that it never closes a client it did not start.
#
# The names are private per run and handed to the app through RMC_SINGLETON_NAMES, so the live
# Roblox client on this machine is never used, claimed, started or signalled.
param(
    [string]$AppPath = "$PSScriptRoot\..\src\RobloxMultiClient.App\bin\Debug\net10.0-windows\win-x64\RobloxMultiClient.exe",
    [string]$SettingsPath = "$env:LOCALAPPDATA\Camo\RobloxMultiClient\settings.json"
)

$ErrorActionPreference = 'Stop'

# A failed run must not leave the app or the user's settings behind, or the next run starts dirty.
trap {
    Write-Output "verification failed: $($Error[0] | Out-String)"
    Write-Output ($_.ScriptStackTrace)
    if (Get-Variable -Name app -ErrorAction SilentlyContinue) {
        $running = Get-Variable -Name app -ValueOnly
        if ($running -and -not $running.HasExited) { $running.Kill() }
    }
    if (Get-Variable -Name settingsBackup -ErrorAction SilentlyContinue) {
        $backup = Get-Variable -Name settingsBackup -ValueOnly
        if ($null -ne $backup) { [System.IO.File]::WriteAllBytes($SettingsPath, $backup) }
    }
    exit 1
}

Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
Add-Type -TypeDefinition @'
using System;
using System.Text;
using System.Runtime.InteropServices;
public class Gui32 {
  public delegate bool EnumProc(IntPtr hWnd, IntPtr lParam);
  [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc cb, IntPtr lParam);
  [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint pid);
  [DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern int GetWindowTextW(IntPtr hWnd, StringBuilder s, int max);
  [DllImport("user32.dll")] public static extern bool PostMessage(IntPtr h, uint msg, IntPtr w, IntPtr l);
  [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
  [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr h, int cmd);
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);

  public static IntPtr Find(uint target, string titlePart) {
    IntPtr found = IntPtr.Zero;
    EnumWindows((h, l) => {
      uint pid; GetWindowThreadProcessId(h, out pid);
      if (pid == target) {
        var t = new StringBuilder(256); GetWindowTextW(h, t, 256);
        if (t.ToString().Contains(titlePart)) { found = h; return false; }
      }
      return true;
    }, IntPtr.Zero);
    return found;
  }
}
'@

function Find-ByAutomationId($root, $id) {
    $condition = New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::AutomationIdProperty, $id)
    return $root.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $condition)
}

# The window element can go stale while the list is being repainted, so every look-up starts from
# a fresh element instead of reusing one captured earlier.
function Get-Window {
    $condition = New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::NameProperty, 'Roblox MultiClient')
    [System.Windows.Automation.AutomationElement]::RootElement.FindFirst(
        [System.Windows.Automation.TreeScope]::Children, $condition)
}

function Find-Text($root, $text) {
    if (-not $root) { return $null }
    try {
        $all = $root.FindAll([System.Windows.Automation.TreeScope]::Descendants,
            [System.Windows.Automation.Condition]::TrueCondition)
    } catch [System.Windows.Automation.ElementNotAvailableException] {
        return $null
    }

    foreach ($element in $all) {
        if ($element.Current.Name -like "*$text*") { return $element }
    }
    return $null
}

# Clicks the control through the accessible Invoke action its button exposes. A screen-space
# pointer click belongs to whichever window is on top of that point, and raw mouse messages sent to
# a window that is not active are ignored by the button, and this desktop is in use, so the pointer is
# not a dependable way in. The effect of every click is asserted by the caller, so a click that does
# nothing fails the run rather than passing quietly.
function Click-Control([string]$id) {
    $window = Get-Window
    if (-not $window) { throw "the window disappeared before '$id' could be clicked" }
    $element = Find-ByAutomationId $window $id
    if (-not $element) { throw "control '$id' is not in the window" }

    try {
        $element.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
    } catch [System.Windows.Automation.ElementNotAvailableException] {
        # Quit tears its own window down while the invoke is still on the stack.
        if (-not $app.HasExited) { throw }
    }
    Start-Sleep -Milliseconds 200
}

function Wait-ForText([string]$text, [int]$timeoutMs = 8000) {
    $watch = [System.Diagnostics.Stopwatch]::StartNew()
    while ($watch.ElapsedMilliseconds -lt $timeoutMs) {
        if (Find-Text (Get-Window) $text) { return $true }
        Start-Sleep -Milliseconds 300
    }
    return $false
}

function Get-SandboxClients {
    @(Get-Process -Name RobloxPlayerBeta -ErrorAction SilentlyContinue | Where-Object { $_.Path -eq $playerPath })
}

# Starts a client the way a user would, outside this app, and leaves it in $script:started.
function Start-Client([string]$label) {
    $script:started = Start-Process -FilePath $playerPath -ArgumentList $clientArguments -PassThru
    Write-Output "$label started directly by the test, not by the app (pid $($script:started.Id))"
}

function Wait-ForObject([string]$name, [int]$timeoutMs = 5000) {
    $watch = [System.Diagnostics.Stopwatch]::StartNew()
    while ($watch.ElapsedMilliseconds -lt $timeoutMs) {
        try {
            ([System.Threading.EventWaitHandle]::OpenExisting($name)).Dispose()
            return $true
        } catch {
            Start-Sleep -Milliseconds 100
        }
    }
    return $false
}

function Wait-ForObjectGone([string]$name, [int]$timeoutMs = 5000) {
    $watch = [System.Diagnostics.Stopwatch]::StartNew()
    while ($watch.ElapsedMilliseconds -lt $timeoutMs) {
        try {
            ([System.Threading.EventWaitHandle]::OpenExisting($name)).Dispose()
        } catch {
            return $true
        }
        Start-Sleep -Milliseconds 100
    }
    return $false
}

function Assert-NoNetwork([string]$label) {
    $endpoints = @()
    $endpoints += Get-NetTCPConnection -OwningProcess $app.Id -ErrorAction SilentlyContinue
    $endpoints += Get-NetUDPEndpoint -OwningProcess $app.Id -ErrorAction SilentlyContinue
    if ($endpoints.Count -gt 0) {
        throw "the application owns $($endpoints.Count) network endpoint(s) $label`: $($endpoints | Out-String)"
    }
    Write-Output "network endpoints owned by the app $label`: 0"
}

# A client that holds the object, then a second one that must be refused.
function Assert-Refused([string]$label) {
    $script:holder = Start-Process -FilePath $playerPath -ArgumentList $clientArguments -PassThru
    if (-not (Wait-ForObject $eventName)) {
        $script:holder.Kill()
        throw "$label`: the first client never created the single-instance object"
    }

    $second = Start-Process -FilePath $playerPath -ArgumentList $clientArguments -PassThru
    if (-not $second.WaitForExit(8000)) {
        $second.Kill(); $script:holder.Kill()
        throw "$label`: the second client kept running although the object was held"
    }
    if ($second.ExitCode -ne 3) {
        $script:holder.Kill()
        throw "$label`: the second client exited with $($second.ExitCode) instead of refusing (3)"
    }

    Write-Output "$label`: a second client is refused (exit 3) while pid $($script:holder.Id) holds the object"
}

function Get-ClientRows {
    $list = Find-ByAutomationId (Get-Window) 'ClientList'
    if (-not $list) { return @() }

    try {
        $items = $list.FindAll([System.Windows.Automation.TreeScope]::Children,
            (New-Object System.Windows.Automation.PropertyCondition(
                [System.Windows.Automation.AutomationElement]::ControlTypeProperty,
                [System.Windows.Automation.ControlType]::ListItem)))
    } catch [System.Windows.Automation.ElementNotAvailableException] {
        return @()
    }

    return @($items)
}

function Get-ClientRowCount { (Get-ClientRows).Count }

function Test-ClientListed([int]$processId) {
    foreach ($row in Get-ClientRows) {
        if ($row.Current.Name -like "*$processId*") { return $true }
    }
    return $false
}

$repo = Resolve-Path (Join-Path $PSScriptRoot '..')
# A fixed short path under %TEMP%: stable across runs, so an aborted run's clients can be found and
# cleared, and short enough that the checkout may live at any depth without hitting MAX_PATH.
$sandbox = Join-Path ([System.IO.Path]::GetTempPath()) 'rmc-gui-sandbox'
$installDir = Join-Path $sandbox 'Roblox\Versions\version-gui-demo'
New-Item -ItemType Directory -Force -Path $installDir | Out-Null

# An aborted earlier run can leave a fictive client holding the sandbox binaries. Only processes
# whose path is inside this sandbox are touched; real Roblox clients are never candidates.
Get-Process -Name RobloxPlayerBeta -ErrorAction SilentlyContinue |
    Where-Object { $_.Path -and $_.Path.StartsWith($sandbox, [StringComparison]::OrdinalIgnoreCase) } |
    ForEach-Object { $_.Kill(); [void]$_.WaitForExit(5000) }

# A fresh clone may run this before the suite, so build the fictive client when it is absent.
$fakeClient = Join-Path $repo 'artifacts\fake\RobloxPlayerBeta.exe'
if (-not (Test-Path $fakeClient)) {
    dotnet build (Join-Path $repo 'tests\FakeRobloxClient\FakeRobloxClient.csproj') -c Debug | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'could not build the fictive client used for verification' }
}
if (-not (Test-Path $fakeClient)) { throw "the fictive client is missing at $fakeClient" }
Copy-Item -Path (Join-Path $repo 'artifacts\fake\RobloxPlayerBeta.*') -Destination $installDir -Force
$playerPath = Join-Path $installDir 'RobloxPlayerBeta.exe'

# Roblox clients already running are the user's; they must be exactly as found when this ends.
$externalBefore = @(Get-Process -Name RobloxPlayerBeta -ErrorAction SilentlyContinue |
    Where-Object { $_.Path -ne $playerPath }).Count
Write-Output "Roblox clients already running before the test: $externalBefore"

# Private kernel object names for this run; the app is pointed at them, not at the client's own.
$suffix = [Guid]::NewGuid().ToString('N')
$mutexName = "RMC_verify_mutex_$suffix"
$eventName = "RMC_verify_event_$suffix"
$clientArguments = "--singleton-event $eventName"
$env:RMC_SINGLETON_NAMES = "$mutexName;$eventName"
Write-Output "verification objects: $mutexName, $eventName"

# Seed the window size, and restore the previous settings afterwards so verification does not
# change the user's own window.
$dataDir = Split-Path $SettingsPath -Parent
$settingsBackup = if (Test-Path $SettingsPath) { [System.IO.File]::ReadAllBytes($SettingsPath) } else { $null }
New-Item -ItemType Directory -Force -Path $dataDir | Out-Null
@{ WindowWidth = 880; WindowHeight = 560 } | ConvertTo-Json | Set-Content -Path $SettingsPath -Encoding UTF8
Write-Output "seeded settings: $SettingsPath"

# --- State 1: no MultiClient, a second client refuses to start -------------------------------
Assert-Refused 'state 1'

# --- Start the enabler while that client still owns the name ---------------------------------
$app = Start-Process -FilePath $AppPath -PassThru
Write-Output "app pid: $($app.Id)"

$window = $null
for ($i = 0; $i -lt 20 -and -not $window; $i++) {
    $condition = New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::NameProperty, 'Roblox MultiClient')
    $window = [System.Windows.Automation.AutomationElement]::RootElement.FindFirst(
        [System.Windows.Automation.TreeScope]::Children, $condition)
    if (-not $window) { Start-Sleep -Milliseconds 500 }
}
if (-not $window) { throw 'the status window was not found' }
Write-Output "window title: $($window.Current.Name)"

Assert-NoNetwork 'while idle'

$windowRect = $window.Current.BoundingRectangle
Write-Output "window rect: $($windowRect.Width)x$($windowRect.Height)"
if ($windowRect.Width -lt 780 -or $windowRect.Height -lt 520) {
    throw "the window is not at its minimum layout size: $($windowRect.Width)x$($windowRect.Height)"
}

# The window must be an enabler, not a launcher: only its own controls may exist.
$ids = @('ToggleButton', 'RefreshButton', 'QuitButton', 'ClientList')
foreach ($id in $ids) {
    $element = Find-ByAutomationId $window $id
    if (-not $element) { throw "control '$id' missing from the window" }
    $r = $element.Current.BoundingRectangle
    if ($r.Width -lt 20 -or $r.Height -lt 10) { throw "control '$id' is degenerate: $($r.Width)x$($r.Height)" }
    if ($r.Left -lt $windowRect.Left -or $r.Top -lt $windowRect.Top -or
        $r.Right -gt $windowRect.Right -or $r.Bottom -gt $windowRect.Bottom) {
        throw "control '$id' falls outside the window: $r inside $windowRect"
    }
    Write-Output ("  {0,-14} {1}x{2}" -f $id, [int]$r.Width, [int]$r.Height)
}

foreach ($launcher in @('LaunchButton', 'RestartButton', 'CloseButton', 'CloseAllButton', 'InstallCombo', 'ArgumentsBox')) {
    if (Find-ByAutomationId $window $launcher) { throw "launcher control '$launcher' is still in the window" }
}
Write-Output 'no launch/close/restart controls: this is an enabler, not a launcher'

# A client owns the name at this point, so the app must report waiting rather than pretend.
if (-not (Wait-ForText 'Waiting')) {
    throw 'the window did not report that a running client still holds the name'
}
Write-Output 'app reports waiting while the pre-existing client holds the name'
if (-not (Test-ClientListed $script:holder.Id)) {
    throw 'the running client is missing from the list although the app watches every client'
}


# That client exits; the app must take the name over by itself.
$script:holder.Kill()
[void]$script:holder.WaitForExit(5000)
if (-not (Wait-ForText 'start Roblox however you normally would')) {
    throw 'the app did not take the single-instance name after the client exited'
}
Write-Output "app reclaimed the name automatically once the client exited (rows now: $(Get-ClientRowCount))"

# --- Background: closing the window must not turn multi-instance off ------------------------
$hwnd = [Gui32]::Find([uint32]$app.Id, 'Roblox MultiClient')
[void][Gui32]::PostMessage($hwnd, 0x0010, [IntPtr]::Zero, [IntPtr]::Zero)   # WM_CLOSE
Start-Sleep -Seconds 2
if ($app.HasExited) { throw 'closing the window exited the app instead of hiding it' }
if ([Gui32]::IsWindowVisible($hwnd)) { throw 'the window stayed visible after being closed' }
Write-Output 'closing the window hid it and kept the app running'

Start-Client 'client A'
$clientA = $script:started
Start-Sleep -Seconds 2
if ($clientA.HasExited) {
    throw 'a client started while the window was hidden did not stay running'
}
Write-Output 'multi-instance is still on while the window is hidden'

[void][Gui32]::ShowWindow($hwnd, 5)   # SW_SHOW
[void][Gui32]::SetForegroundWindow($hwnd)
Start-Sleep -Seconds 1
if (-not (Get-Window)) { throw 'the window did not come back' }
Write-Output 'window restored from the background'

# --- State 2: clients started outside the app run side by side -------------------------------
Start-Client 'client B'
$clientB = $script:started
Start-Sleep -Seconds 3

$running = @(Get-SandboxClients)
Write-Output "state 2: clients started outside the app, running: $($running.Count) (pids: $($running.Id -join ', '))"
if ($running.Count -ne 2) { throw "expected two concurrent clients, found $($running.Count)" }
if (-not $clientA.HasExited -and -not $clientB.HasExited) { Write-Output 'both clients are alive' }

# The list is updated on demand, not by a background timer: ask for it the way a user would.
Click-Control 'RefreshButton'
Start-Sleep -Milliseconds 500
# Assert the two clients by pid: the user's own sessions may come and go during the run.
foreach ($client in @($clientA, $clientB)) {
    if (-not (Test-ClientListed $client.Id)) {
        throw "client $($client.Id), started outside the app, is missing from the list"
    }
}
Write-Output "state 2: both clients started outside the app are listed (rows: $(Get-ClientRowCount))"
Assert-NoNetwork 'with clients running'

# --- Turn off: the name is free again, so normal single-client behaviour returns ------------
Click-Control 'ToggleButton'
if (-not (Wait-ForText 'Roblox will allow one client at a time')) {
    throw 'the window did not report multi-instance as off'
}
Write-Output 'turned multi-instance off from the window'
# Off must mean the name is really free, or the next check would race a stale object.
if (-not (Wait-ForObjectGone $eventName)) {
    throw 'turning multi-instance off did not release the single-instance name'
}

Start-Client 'client C'
$clientC = $script:started
if (-not (Wait-ForObject $eventName)) {
    $clientC.Kill()
    throw 'after turning off, a client could not create the single-instance object'
}
$refusedWhileOff = Start-Process -FilePath $playerPath -ArgumentList $clientArguments -PassThru
if (-not $refusedWhileOff.WaitForExit(8000)) {
    $refusedWhileOff.Kill(); $clientC.Kill()
    throw 'after turning off, a second client was still allowed'
}
if ($refusedWhileOff.ExitCode -ne 3) {
    $clientC.Kill()
    throw "after turning off, the second client exited with $($refusedWhileOff.ExitCode) instead of 3"
}
Write-Output 'off: a new client holds the object again and the next one is refused (exit 3)'

# --- Turn on again: two fresh clients start normally ----------------------------------------
$clientC.Kill()
[void]$clientC.WaitForExit(5000)
Start-Sleep -Seconds 1

Click-Control 'ToggleButton'
if (-not (Wait-ForText 'start Roblox however you normally would')) {
    throw 'the window did not report multi-instance as on again'
}

Start-Client 'client D'
$clientD = $script:started
Start-Client 'client E'
$clientE = $script:started
Start-Sleep -Seconds 2
$running = @(Get-SandboxClients)
Write-Output "after turning on again: clients running: $($running.Count) (pids: $($running.Id -join ', '))"
if ($running.Count -ne 4) { throw "expected four clients total, found $($running.Count)" }
Write-Output 'on again: further clients start while earlier ones keep running'

# --- Quit: the claim is released and the app exits -------------------------------------------
$before = @(Get-SandboxClients).Count
$rect = (Get-Window).Current.BoundingRectangle
Write-Output "window before quit: $($rect.Width)x$($rect.Height)"
Click-Control 'QuitButton'
if (-not $app.WaitForExit(10000)) {
    $left = [Gui32]::Find([uint32]$app.Id, 'Roblox MultiClient')
    throw "the application did not exit after Quit was clicked (window still present: $($left -ne [IntPtr]::Zero))"
}
Write-Output 'app exited cleanly from its Quit control'
if (@(Get-SandboxClients).Count -ne $before) {
    throw 'the app closed a client it did not start'
}
Write-Output "clients still running after the app exited: $before (none were closed by it)"

$externalAfter = @(Get-Process -Name RobloxPlayerBeta -ErrorAction SilentlyContinue |
    Where-Object { $_.Path -ne $playerPath }).Count
Write-Output "Roblox clients present before the test that still run: $externalAfter of $externalBefore"
if ($externalAfter -ne $externalBefore) {
    throw 'a Roblox client that existed before the test was closed'
}

# --- State 3: MultiClient is gone, so the refusal is back -----------------------------------
$clientA.Kill(); $clientB.Kill(); $clientD.Kill(); $clientE.Kill()
Start-Sleep -Seconds 1
foreach ($process in @($clientA, $clientB, $clientD, $clientE)) {
    if (-not $process.HasExited) { [void]$process.WaitForExit(5000) }
}

Assert-Refused 'state 3'
$script:holder.Kill()
[void]$script:holder.WaitForExit(5000)

$saved = Get-Content -Path $SettingsPath -Raw | ConvertFrom-Json
Write-Output "persisted window: $($saved.WindowWidth)x$($saved.WindowHeight)"
# The window opened at the seeded 880x560 (asserted above); what is persisted must be the size it
# actually had when it closed, which can differ from the seed after a hide and restore.
if ($saved.WindowWidth -ne [int]$rect.Width -or $saved.WindowHeight -ne [int]$rect.Height) {
    throw "the persisted size $($saved.WindowWidth)x$($saved.WindowHeight) is not the size at quit $([int]$rect.Width)x$([int]$rect.Height)"
}

$log = Get-ChildItem -Path (Join-Path $dataDir 'logs') -Filter 'multiclient-*.log' |
    Sort-Object LastWriteTime -Descending | Select-Object -First 1
Write-Output "log: $($log.FullName)"
Select-String -Path $log.FullName -Pattern 'Multi-instance enabled|Multi-instance disabled|Singleton object|Roblox clients now running|exited' |
    Select-Object -Last 8 |
    ForEach-Object { '  ' + $_.Line }

if ($null -ne $settingsBackup) {
    [System.IO.File]::WriteAllBytes($SettingsPath, $settingsBackup)
    Write-Output 'previous settings restored'
} else {
    Remove-Item -Path $SettingsPath -Force
    Write-Output 'verification settings removed (there were none before)'
}
