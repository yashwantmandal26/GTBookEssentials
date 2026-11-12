# Rear light bar

Choose an effect and brightness, optionally enable **Preview selected look**, then choose **Apply to bar**. The preview checkbox sends no commands. The large button becomes **Start music sync** for Rhythm/Beat and **Apply · turn off** at zero brightness.

| Effect | Controls | Operation |
|---|---|---|
| Static | Colour 1, brightness | Native controller |
| Breathing | Colour 1, brightness, breathing speed | Native controller |
| Loop / Cover | Colour 1, Colour 2, brightness | Native controller |
| Rhythm / Beat | Colour 1, brightness | Default playback output's volume meter, streamed while Essentials is open |

Music follows the default output selected when sync starts. After changing output devices, stop and restart music sync. The preview is a simulation and is not an audio measurement. Silence can leave the bar dark while the MUSIC badge indicates a live acknowledged stream.

**Turn off** stops music and disables the rear power gate. It does not require the UART to be available. **Stop music** ends only music streaming and restores a steady Colour 1 at the stream's brightness. **Check status** reads device power along with the dashboard readings; a power read cannot establish which native colour/effect is currently displayed.

The status badge distinguishes a draft preview, an acknowledged command/stream, a power-off read-back and an unknown state. Editing a draft keeps the last acknowledged illustration until Preview is enabled or Apply succeeds. Failed writes are marked unknown because a command could have partially applied.

## Implementation and checks

- Uses the existing reviewed ACPI/WMI power bit and internal SERIALIOUART1 port. It never scans unrelated serial ports for writes.
- Native packets retain the OEM 17-byte layout and mode IDs. Both colours of Loop/Cover are composed by `RearLighting.Frame`. The 0–44 brightness range is retained; positive percentages never quantise to zero.
- Gate reads retry up to eight times. UART writes require the four-byte acknowledgement; fragmented reads are reassembled, while invalid or empty replies fail. Ambiguous UART writes are not blindly resent.
- A rear-specific mutex prevents competing native/music writers. Music readiness follows its first complete acknowledged write, with an eight-second UI startup deadline. Stop, owner lifetime and transport errors end the stream. Static restoration is acknowledged before Stop music is reported complete.
- Software regression tests cover these contracts and the compact/wide layouts. `--render` / `--render-dark` on the test executable generate offline previews of all six rear effects.

## Physical check remaining

On INFINIX GL613 / BIOS 1.00, verify Static, Breathing, Loop and Cover; check both palette colours where enabled. Play audio on the default output and verify Rhythm/Beat, silence, Stop music and Turn off. Confirm that changing keyboard brightness or performance mode leaves sync active. Test 0%, minimum positive brightness, Check status and a sleep/resume cycle, then return to the preferred rear setting. UART acknowledgement confirms receipt; only this observation confirms the physical appearance.
