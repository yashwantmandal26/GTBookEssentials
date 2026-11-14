# GTBOOK Essentials — Implementation Progress

## Current Phase
- **Phase 3: Small-Window Scrolling, Full Disk Capacity Formatting, Visual QA, and Final Verification**
- **Status:** Complete (Final Handoff)

---

## 14 Audit Findings Checklist

| ID | Priority | Description | Implementation Status | Phase | Verification Evidence |
|---|---|---|---|---|---|
| 1 | P1 | Elevated updates execute from user-writable storage | **Implemented** | Phase 1 | `UpdateStaging.cs`, `AppUpdater.cs`; unit tests for safe extraction paths, reparse point rejection, hash verification, payload re-check before launch, and medium-integrity DACL denial fixture. |
| 2 | P1 | The performance monitor resumes device access while a conflict remains | **Implemented** | Phase 1 | `PerformanceModes.cs`; conflict state is latched, provider access gated before EC transaction, 0 EC reads while conflicted, responsive 250ms interval recovery upon clearance, fail-closed on exception. |
| 3 | P2 | Turbo claims full fans when firmware does not confirm them | **Implemented** | Phase 1 | `PerformanceModes.cs`, `Feedback.cs`, `MainWindow.cs`; unconfirmed fan override returns `partial=True`, `mode=Performance`, `cooling=Firmware auto`; "Full fans ON" is never shown without confirmation; failed auto-cooling restore on exit is reported as failure. |
| 4 | P2 | Startup and ordinary mode changes overwrite the user's Windows power plan | **Implemented** | Phase 1 | `EnergySaver.cs`, `MainWindow.cs`; `IPowerBackend` abstraction introduced; ordinary Quiet/Balanced/Performance sync never alters user's base plan; Turbo ownership recorded and restored only if still running app-owned Turbo plan; user-selected plans respected; startup reconciliation; safe fallback on missing saved plan; monotonic async serialization. |
| 5 | P2 | Running animations ignore some recognized conflicting controllers | **Implemented** | Phase 1 | `Core.cs`, `ExtraEffects.cs`; all recognized process names and case variants checked; every nonempty conflict halts running extra animations with original message preserved; clean recovery after clearance. |
| 6 | P2 | Rear “Turn off” is undone by restart or resume | **Implemented** | Phase 2 | `MainWindow.cs`, `Tests.cs`; persistent `rear-power-enabled` flag saved only on acknowledged Off; startup and resume initialize only when enabled; user's custom `rear.json` preset is preserved; failed Off does not persist false. |
| 7 | P2 | Enabling “Keep keyboard awake” during an animation silently fails | **Implemented** | Phase 2 | `MainWindow.cs`, `ExtraEffects.cs`, `Tests.cs`; live update sets `ArmKeyboardAwake = request.KeepAwake`; worker's `!awake` guard sends single 0x19 on false->true; repeated updates send 0 additional writes; failure does not mark configured. |
| 8 | P2 | Supported small windows hide actions without allowing scrolling | **Implemented** | Phase 3 | `MainWindow.cs`, `Tests.cs`; enabled `page.AutoScroll = true` on `GradientFlowPanel`; horizontal scroll suppressed; all cards reflow and fit within client width; tested reachability down to minimum supported size (760x560 client / 950x700 @ 125% DPI); all actions (e.g. Lighting "Apply to bar", "Turn off") reachable; regression test proves vertical clipping if `AutoScroll` is disabled. |
| 9 | P2 | GPU read errors and pending requests are presented as active hardware state | **Implemented** | Phase 2 | `Core.cs`, `MainWindow.cs`, `GpuModes.cs`, `Tests.cs`; typed sensor statuses (`Ok`, `Busy`, `AccessDenied`, `Error`, `Asleep`); `GpuModes.Apply` does not overwrite active `gpu-mode` before reboot; distinct UI tile states; sensor errors never report dGPU off. |
| 10 | P2 | A Start-menu launch can silently leave the application hidden | **Implemented** | Phase 2 | `Start.ps1`, `Launcher.cs`, `MainWindow.cs`, `Tests.cs`; bounded 3000ms show handshake; `foregroundRequested` prevents `Shown` event from hiding window when explicitly opened; fallback launches foreground directly. |
| 11 | P2 | Installer rollback omits metadata and shortcuts | **Implemented** | Phase 2 | `release/Install.ps1`, `Tests.cs`; transactional snapshot and rollback covering `install-state.json`, Start menu shortcuts, and HKLM uninstall registry key with resilient per-step try/catches. |
| 12 | P2 | RC tags can enter the stable channel and collapse to the final version | **Implemented** | Phase 2 | `AppUpdater.cs`, `.github/workflows/build.yml`, `Tests.cs`; SemVersion 2.0.0 parser/comparer; stable channel rejects prereleases; preview channel supports RC1->RC2->final; CI sets `--prerelease` on suffix tags and retains InformationalVersion. |
| 13 | P3 | External keyboards lose their normal Insert key behavior | **Implemented** | Phase 2 | `Screenshots.cs`, `LaptopShortcuts.cs`, `MainWindow.cs`, `Tests.cs`; independent `ScreenshotInterception` and `OemInsertRemap` preferences; standard external Insert (`0x2d`, extended=true) passes through by default; PrtScn captured; numpad Insert preserved. |
| 14 | P3 | A completely full disk is reported as unavailable free space | **Implemented** | Phase 3 | `DeviceInformation.cs`, `DeviceSpecView.cs`, `Tests.cs`; `DeviceInformation.Bytes(value, allowZero: false)` preserves `"Unavailable"` for invalid/zero capacity while `FreeBytes(value)` formats 0 free space as `"0 GB"`; `DeviceSpecView` regex matches `"0 GB free of ... GB"`, rendering a truthful 100% used progress bar and subtitle. |
| Dev 1 | — | Stale parent release workflow | **Documented** | Phase 2 | `docs/PARENT_WORKFLOW_MIGRATION.md` created; parent repository working tree left untouched. |
| Dev 2 | — | Local “latest startup” recovery can select an older build | **Implemented** | Phase 2 | `Launch-Latest.ps1`, `release/Launcher.cs`, `Tests.cs`; resolves latest build from `out/latest.json`; gracefully requests exit of older process; direct execution without task loops. |

---

## Files Changed and Resulting Behavior

### Phase 1 Files
1. **`src/UpdateStaging.cs` (New file)**
   - Establishes protected staging under `%ProgramData%\GTBookEssentials\staging`.
   - Rejects reparse points, validates path lineages, and enforces Administrator/SYSTEM-only ACLs.
   - Rejects zip traversal (`..`) and resolves system executables via absolute paths.
   - Re-verifies SHA-256 on disk immediately before launch.
2. **`src/AppUpdater.cs`**
   - Routes downloads to secure staging and verifies hashes.
3. **`src/PerformanceModes.cs`**
   - In `ModeObserver`: latches conflict state; gates EC access before any transaction; fail-closed on exception; 250ms polling during conflict for prompt clearance recovery.
   - In `ApplyModeWithCooling`: verifies fan override read-back; reports partial cooling truthfully.
4. **`src/Feedback.cs`**
   - Never displays "Full fans ON" without confirmation; reports cooling restoration failures.
5. **`src/MainWindow.cs` (Phase 1 additions)**
   - Mode tile and confirmed state derived from verified results.
   - Startup reconciliation of Turbo ownership (`EnergySaver.ReconcileStartup`).
6. **`src/EnergySaver.cs`**
   - `IPowerBackend` abstraction; ordinary mode sync never alters base plans; Turbo ownership tracking and restoration; safe fallback to Balanced.
7. **`src/Core.cs` (Phase 1 additions)**
   - Structured conflict detection `CheckConflict()` for all recognized utility processes and scripts; `ConflictProbe` test delegate.
8. **`src/ExtraEffects.cs`**
   - Conflict monitor stops animations on any nonempty conflict with original message.

### Phase 2 Files
1. **`src/MainWindow.cs` (Phase 2 additions)**
   - **Audit #6:** On acknowledged `rear-off`, saves `Store.SaveFlag("rear-power-enabled", false)` without modifying `rear.json`. In `InitializeHardware()`, checks `rear-power-enabled` before restoring rear lighting on startup or resume. Failed commands do not persist false.
   - **Audit #7:** In live animation updates, passes `request.ArmKeyboardAwake = request.KeepAwake`. UI configured state is set only after successful channel acknowledgement.
   - **Audit #9:** In `UpdateMetricTiles()`, handles typed sensor statuses (`Ok`, `Busy`, `AccessDenied`, `Error`, `Asleep`). Sensor errors display truthful descriptions ("Busy", "Access denied", "Unavailable") instead of claiming dGPU is asleep or off.
   - **Audit #10:** Added `foregroundRequested` flag set by `ShowWithDeviceControl()`. In `base.Shown`, prevents hiding the window to tray when an explicit show was requested during or before initialization.
   - **Audit #13:** In `InitializeLaptopKeys()`, loads and applies `Store.Flag("screenshot-intercept", true)` and `Store.Flag("oem-insert-remap", false)`. In Settings keys card, added `screenshotBox` and `oemInsertBox` checkboxes with dynamic wrapping.
2. **`src/Core.cs` (Phase 2 additions)**
   - **Audit #9:** In telemetry loop for `"gpu"`, returns typed statuses: `"Ok"` for valid readings, `"Asleep"` for 0°C, `"Busy"` for deferred leases, `"AccessDenied"` for permission exceptions, and `"Error"` for transport failures.
3. **`src/GpuModes.cs`**
   - **Audit #9:** In `Apply()`, sets `gpu-mode-requested` and `gpu-restart-required`, but removed premature overwrite of active `gpu-mode` in the reply. Active routing is preserved until Windows reboots.
4. **`src/AppUpdater.cs` (Phase 2 additions)**
   - **Audit #12:** Implemented `SemVersion` class supporting SemVer 2.0.0 parsing, precedence ordering, and build metadata rules. Updated `IsNewer()` and `ParseReleaseJson()` with `allowPrerelease` channel policy. Stable channel excludes prereleases even if the GitHub API reports `prerelease: false`. Added `FormatSemVersion()` for version labels.
5. **`.github/workflows/build.yml`**
   - **Audit #12:** Preserves full version with prerelease suffix in `AssemblyInformationalVersion`. Added `--prerelease` argument to `gh release create` when tag contains `-rc`, `-beta`, `-alpha`, or `-preview`.
6. **`release/Start.ps1`**
   - **Audit #10:** Replaced fixed 400ms sleep with a bounded 3000ms polling handshake for `Local\GTBookEssentialsShow`. Falls back to direct foreground execution if task launch fails to deliver.
7. **`release/Launcher.cs`**
   - **Audit #10 & Dev Issue:** Checks existing show signal first; runs task with bounded 3000ms wait; falls back to launching target binary directly without recursive task re-invocation.
8. **`release/Install.ps1`**
   - **Audit #11:** Snapshot and rollback expanded to cover `install-state.json`, Start menu shortcuts (`GTBOOK Essentials.lnk`, `Uninstall GTBOOK Essentials.lnk`), and HKLM uninstall registry keys using resilient try/catch blocks.
9. **`Launch-Latest.ps1` & `Fix-Latest-Startup.ps1`**
   - **Dev Issue:** Resolves target executable from `out/latest.json`; gracefully requests exit of older running builds via `Local\GTBookEssentialsExit`; executes target directly without launcher-task loops.
10. **`docs/PARENT_WORKFLOW_MIGRATION.md` (New file)**
    - Documents stale `Infinix_Toolkit` path in enclosing repository's `.github/workflows/release.yml` and migration paths, leaving parent repository working tree completely untouched.
11. **`src/Tests.cs` (Phase 2 additions)**
    - Fixed OEM top-row shared key assertion to test remap flag.
    - Added 8 comprehensive Phase 2 regression suites covering all 7 Phase 2 findings plus the launcher repair issue.

### Phase 3 Files
1. **`src/MainWindow.cs` (Phase 3 additions)**
   - **Audit #8:**
     - Set `page.AutoScroll = true;` with horizontal scrolling suppressed (`page.HorizontalScroll.Maximum = 0; page.HorizontalScroll.Visible = false; page.HorizontalScroll.Enabled = false;`).
     - Exposed internal members (`page`, `ShowPage`, `FitCards`, `lightingShowRear`, `settingsShowSecondary`) to enable deterministic layout testing.
     - Updated `RenderPages` array to include `760` resolution (`int[] array = new int[] { 1920, 1080, 860, 760 };`) and client size `num==1920?1080:(num==760?560:800)`.
     - In `VerifyAllPagesNoTextClipping`, removed width < 860 exemption, testing all 4 pages across all supported widths (1920, 1280, 1080, 860, 760) down to 760x560 without clipping.
2. **`src/DeviceInformation.cs` (Phase 3 additions)**
   - **Audit #14:**
     - Updated `Bytes(object value, bool allowZero = false)`: returns `"Unavailable"` for invalid, negative, or zero capacity when `allowZero` is false (preventing invalid zero total capacity).
     - Added `FreeBytes(object value)`: calls `Bytes(value, allowZero: true)` so zero free space on a full partition formats truthfully as `"0 GB"`.
     - In `Storage space` section query, uses `FreeBytes(r["FreeSpace"]) + " free of " + Bytes(r["Size"])`.
3. **`src/DeviceSpecView.cs` (Phase 3 additions)**
   - **Audit #14:**
     - Exposed `internal List<SpecItem> Items` for test assertions.
     - Matches `"0 GB free of <total> GB"` via existing regex `@([0-9\.]+)\s*GB\s*free\s*of\s*([0-9\.]+)\s*GB`, correctly computing `free = 0`, `percent = 100 * (1 - 0) = 100%`, setting `BarPercent = 100f`, `BarText = "100% used"`, and `Subtitle = "100% used"`.
4. **`src/PerformanceModes.cs` (Phase 3 refinement)**
   - Latched conflict wait interval adjusted to 250ms during active conflict for prompt recovery while remaining completely locked out of EC access while conflict persists.
5. **`src/Tests.cs` (Phase 3 additions)**
   - Added `using System.Drawing;`.
   - Added test `"Full disk capacity formats truthful zero and near-zero free space metrics"` covering `DeviceInformation.Bytes`, `FreeBytes`, null/negative/invalid handling, and `DeviceSpecView` parsing of 0 GB free (100% used bar), 0.5 GB free (near-full bar), and unavailable free space.
   - Added test `"Small window reachability preserves scrolling and exposes all action controls down to 760x560"` covering `page.AutoScroll`, horizontal scroll suppression, reachability of all 4 pages and subtabs down to 760x560, action button reachability ("Apply to bar", "Turn off"), and negative regression proving vertical overflow if AutoScroll is disabled.
   - Suite total: **125 passed, 0 failed**.

---

## Commands Run and Results

| Command | Working Directory | Result | Notes |
|---|---|---|---|
| `powershell -ExecutionPolicy Bypass -File .\GTBookEssentials\tests\run.ps1` | Workspace root | **125 passed, 0 failed** | Baseline: 109. Phase 1: 115. Phase 2: 123. Phase 3: 125 passing tests. |
| `powershell -ExecutionPolicy Bypass -File .\GTBookEssentials\tests\release-tools.ps1` | Workspace root | **Passed** | Script parsing, atomic writes, installer gate, device support recovery states. |
| `powershell -ExecutionPolicy Bypass -File .\GTBookEssentials\build.ps1 -OutputPath .\GTBookEssentials\tests\out\phase3\GTBook-verified.exe` | Workspace root | **Built successfully** | Production compilation to isolated path `tests/out/phase3/GTBook-verified.exe`. |
| `git -C GTBookEssentials status -s` | GTBookEssentials | Clean diff | Only application repository files modified. All test artifacts ignored. |
| `git status -s` | Enclosing repo | Unchanged | Parent repository untouched. |

---

## Remaining Failures and Unverified Checks

- **Automated tests:** 0 failures (125 passed, 0 failed).
- **Physical hardware validation still pending (requires real GL613 laptop and test environment):**
  - Verification of physical rear light bar remaining dark across system sleep/wake cycles and cold reboots.
  - Physical USB HID keyboard traffic capture verifying single 0x19 packet emitted upon live KeepAwake toggle.
  - Physical discrete GPU sleep state verification on real hardware across mux switch reboots.
  - Verification of Start menu launch timing on physical machine under cold boot load.
  - Real Windows UAC elevation staging directory creation with standard medium-integrity token denial under live Windows OS security accounts.
  - Physical touch and mouse wheel interaction on 125%/150% high-DPI panels down to minimum window bounds.

---

## Implementation Summary & Handoff
All 14 application audit findings and both developer/repository issues have been resolved, regression tested, and verified against the repository's build scripts and test harnesses:
- **Phase 1 (Security & Hardware State Correctness):** Audit findings #1, #2, #3, #4, #5 implemented and verified.
- **Phase 2 (Preferences, Startup & Distribution):** Audit findings #6, #7, #9, #10, #11, #12, #13, Dev #1, Dev #2 implemented and verified.
- **Phase 3 (Reachability, Formatting & Final Verification):** Audit findings #8, #14, visual responsiveness down to 760x560, full disk formatting, full test suite (125 tests), release tooling, and production compilation verified.
