# Release validation

Candidate: 2.0.0-rc.7. Hardware profile: INFINIX GL613 / BIOS 1.00.

## RC7 confirmation flow

- 85 checks pass. A shared confirmation policy makes performance modes, fan settings, lighting Apply/Off/Stop, USB charging, Windows-key lock and laptop-shortcut toggles direct actions. Inline lighting previews remain.
- GPU and Windows startup retain confirmation. GPU dialogs lead with a large orange RESTART REQUIRED heading; startup leads with START WITH WINDOWS: ON/OFF.
- The post-save GPU restart dialog uses the same highlighted design. Later is the default keyboard action, and no restart occurs during tests or installation.
- GPU confirmation was rendered and visually checked. Installed RC7 physical single-click flow remains pending.

## RC6 patterns, restart flow and tray lifecycle

- 84 checks pass, including exact four-zone directional mirroring, deterministic repeats, GPU pending-state boot identity and close-versus-exit policy.
- Fire and sunset now use the same explicit quarter-cycle zone spacing as the other travelling patterns. Direction is available on applicable Extra effects; preview, HID output, preferences and live transitions share the same field. Magnetic Slingshot remains symmetric.
- GPU settings appear first. A successful firmware request offers Restart now / later; the app retains the boot firmware setting and displays the new request as pending. Pending state survives restarting the app and clears only on a different Windows boot. Active MUX routing remains unmeasured.
- Automatic Extra-effect restoration suppresses its notifications. Window X hides silently without stopping lighting/shortcuts; tray Exit and installer exit signals request full cleanup. Windows shutdown is allowed to proceed.
- Physical RC6 direction, tray-close, silent cold-boot restoration and GPU activation after a user-triggered restart remain pending.

## RC5 lighting transitions, wide layout and USB charging

- 81 software checks pass. New checks cover the live update channel with matching acknowledgement, failure/timeout handling, phase-preserving brightness changes, retargeting, power-off after a completed fade, omitted redundant watchdog/static resets and USB retries.
- Running Extra effects change through a per-session memory channel in the same helper/HID connection. The current emitted frame blends over 200 ms; transition frames use a 33 ms schedule, then return to the 67 ms animation schedule. Off state waits up to one second or an update signal, skips identical packets and does not continuously transmit HID frames.
- EC lighting gates are written only when their state changes. Timeout-disable packets no longer carry RGB/brightness payload and are not reissued on each shortcut in the same active app session. Initial static-zone setup remains once when entering a fresh Extra connection, as required by the reviewed controller path.
- Wide/full-screen Lighting places keyboard and rear controls beside each other. The 1920-pixel wide layout and compact fallback passed geometry checks and DPI-aware visual rendering. Group headers open their effect list and use large chevrons; Four zones marks its extra zone settings.
- USB settings have dedicated ON/OFF controls and highlights based only on confirmed firmware values. The exact OEM EC/SMI write path is followed by bounded repeated firmware read-back. Optional installer -VerifyUsb checks both states and restores the original setting.
- RC5 installed successfully (2.0.0.6), with the installed EXE hash matching the packaged EXE. The user reported that the new layout/effect picker and F3 brightness/effect transitions are now good.
- On-device USB verification confirmed OFF and ON through the OEM firmware path, then restored the original OFF setting; report ok=true and original_restored=true. Real shutdown USB power remains pending a physical phone/port test. Firmware read-back does not measure the voltage/current of a shut-down USB port.

## RC4 feedback and device details

- Separate keyboard effect-name and brightness popups; large titles fit all ten Extra names at 100%, 125%, 150% and 200% scaling. Existing smooth update/position tests remain passing.
- F3 advances once after 800 ms and remains latched until release. Tap still changes brightness.
- Clean greeting without the cloud or OVERVIEW eyebrow; taller heading avoids clipped glyphs.
- Detailed device information loads in a read-only helper only on demand, cached for five minutes. Queries have bounded waits and partial failures leave the other sections available. Local inventory completed in 476 ms; no hardware settings changed.
- 72 desktop software checks pass. Physical RC4 F3/popup and installed-device-page checks remain pending.

## Completed baseline

- User physically confirmed fan/keyboard lighting, F1/F2, F3 tap/hold, performance button and chime, screenshot saving, popup smoothness/position and disabled slider-wheel adjustment.
- Original GPU methods were decoded from the supplied OEM assemblies: register C0, values 1=dGPU, 2=Hybrid, 3=iGPU. Requests now use bounded repeated read-back.
- Baseline existing UI process: 42.50 MiB private memory, 82.56 MiB working set, 46.875 ms CPU in a 20.10-second sample. This is one local sample, not a battery-life measurement or a guarantee for every machine.
- RC1 installed successfully to Program Files; the scheduled task points to the installed EXE and the original ControlCenter task is disabled. Measured launch: shortcuts ready at 303.2 ms, OEM hardware ready at 1183.8 ms after process start. This was a scheduled-task launch, not a cold-boot timing measurement.
- Live GPU request check: dGPU=1, Hybrid=2, iGPU=3 all confirmed; original raw value 1 restored and confirmed. Active routing after reboot remains unverified.
- Brightness burst check: first command 169.1 ms, reused-helper command 21.3 ms. Brightness restored to 50%; helper exited after the five-second idle timeout.
- Extra animation active, two app processes: 66.91 MiB total private bytes and 96.95 MiB summed working sets; 1015.625 ms CPU over 30.27 seconds (3.356% of one logical core). UI was visible; this is not a native-effects tray-idle or battery-life measurement, and summed working sets can include shared pages.
- RC2 corrects an observed transient hardware-lock collision during Extra animation and changes button background compositing. 64 desktop software checks pass; release-tooling checks cover parse/new-install/upgrade atomic writes and cleanup. RC2 still needs application and live visual verification.
- RC3 additionally clears every custom-button paint surface, uses an opaque bevel shadow, keeps orange selected fills during temporary disable, closes the verified OEM UI at administrator startup/open, and refreshes visible readings at 5s AC / 10s battery without stopping music. 69 desktop software checks and 68 headless checks pass. New tests exercise paint-only repaint, persistent orange fill, strict OEM identity matching, graceful/fallback shutdown ordering and polling policy.
- RC3 (file version 2.0.0.6) was installed and inspected live on 4 November 2026: black button leftovers were absent, Overview and Gaming remained orange-selected, and readings changed without a Refresh click (CPU 45→48°C, GPU 40→41°C, GPU fan 2000→1900 RPM). The user confirmed that opening Essentials automatically closed official Control Center.

## Release gates

| Check | Evidence / status |
|---|---|
| Software regression suite | 69 desktop checks and 68 headless checks passed; reports in the development workspace |
| Standalone ZIP and SHA-256 manifest | Generated by Build-Release.ps1; validate without the original OEM folder |
| Installer/upgrade/uninstall scripts | RC1 live install passed; startup task verified; atomic upgrade primitives passed; actual uninstall not executed |
| Three GPU request writes and restoration | Passed on RC1; `gpu-verification.json` confirms all three requests and original restoration |
| Post-reboot active iGPU/Hybrid/dGPU routing | Pending physical checks across the three modes; do not infer from EC read-back |
| Actual Windows reboot/sign-in startup | Pending reboot; scheduled-task start is a separate check |
| Final idle/burst resources | Burst and Extra-animation samples above; native-only tray-idle check pending |

Before each GPU reboot check, save work, select one mode, Apply, read the saved request, and restart manually. After sign-in confirm Essentials starts automatically, the request persists, display output works, and Windows/NVIDIA display diagnostics show the intended route. Record the result and restore the preferred mode when finished.

No claim of perfect operation on untested GTBook models/firmware or measured battery-life improvement is made by a passing software suite.
