# Four-zone animation design

Research checked on 5 November 2026. Fifteen Extra animations are available in RC16. The existing ten effect names and saved preferences remain compatible.

## Brand references

- [Acer Predator Helios 300 PH315-55 official guide, pages 49–50](https://global-download.acer.com/GDFiles/Document/User%20Manual/User%20Manual_Acer_2.0_A_A.pdf?BC=ACER&LC=en&OS=11M1&SC=PA_6&Step1=NOTEBOOK&Step2=PREDATOR&Step3=PREDATOR+PH315-55&acerid=637879390962549543): the RTX 3060 section explicitly describes four zones and lists Breathing, Wave, Shifting, Zoom, Meteor and Twinkling. This is the closest hardware reference. The per-key variants in the same manual have a different effect list; they are not treated as four-zone capabilities.
- [MSI's official keyboard overview](https://uk.msi.com/blog/msi-gaming-laptops-provide-dedicated-keyboard-design): Mystic Light supports the four-zone Pulse and Katana keyboards, with colour, speed and direction choices. The [MSI Center manual](https://download-2.msi.com/archive/mnu_exe/nb/Notebook_MSI_Center_EN.pdf) also documents customising individual zones. This supports making broad, readable zone motion and respecting the selected palette.
- [ASUS's official Aura guide](https://dlcdnets.asus.com/pub/ASUS/ODD/HDD/EHD-A1T/E13846_External_HDD_manual.pdf), page 14: documents the Aura effect families including Comet, Wave, Glowing Yoyo and Starry-Night. This is a general RGB style reference, not evidence of a specific four-zone laptop implementation.

The manuals establish the named effect families and controls, not their proprietary timing or algorithms. The timing, envelopes, colours and adaptations below are original choices for this project's four physical zones.

## Five new patterns

| Effect | Appearance | Adaptation / inspiration |
|---|---|---|
| Zoom Bloom | The two middle zones glow first, opening into the outer pair and folding smoothly back. Both colours evolve through the bloom. | Centre-to-edge movement inspired by Acer's Zoom family; one-dimensional radial motion suited to four zones. |
| Meteor Shower | Two offset colour crests travel together with overlapping soft trails. | Acer's Meteor and Aura's Comet families; broader crests avoid losing a tiny point between zones. |
| Starlight | Each zone develops its own soft glint over a low ambient glow. | Acer Twinkling / Aura Starry-Night; staggered deterministic envelopes replace per-key random flashes. |
| Colour Cascade | A curtain of one colour fills consecutive zones and recedes into the second colour. | A new interpretation of zone-based shifting and wave effects. |
| Silk Flow | Two broad, intertwined colour currents move with a restrained brightness swell. | A gentler original variation on the wave family for everyday ambient lighting. |

All five use Colour 1 and Colour 2. Identical selections stay that colour. Zoom Bloom and Starlight have no left/right control because their motion is symmetric or scattered; the other three mirror the actual four-zone order.

## Improvements to the existing ten

- Aurora: two overlapping wave envelopes, a luminous crest and deeper colour folds.
- Rainbow Prism: full-spectrum drift with a small eased phase sway and subtle luminosity changes.
- Blazing Fire: layered ember movement, crimson lows and gold peaks without abrupt random flicker.
- Comet Trail: a broader head and two overlapping tail envelopes for a longer, softer wake.
- Ocean Tide: layered cobalt-to-turquoise swells with darker troughs.
- Sunset Glow: a continuous ruby/coral/gold/magenta palette with eased colour stops.
- Neon Pulse: softer pulse shoulders and more pronounced light/dark separation.
- Candy Chase: bright, eased ribbons with restrained luminosity modulation.
- Magnetic Slingshot: better defined ribbons and deeper space around their collision.
- Orbital Eclipse: a softer moving shadow and a distinct coloured rim.

## Rendering and validation

Every animation is computed as four colours and uses the existing static-zone HID packets. No per-key lighting, vertical movement, key reactions or native firmware effect is implied. Each effect loops after six seconds at speed 4; speeds 1–9 scale time within the existing bounds. Hardware remains on the existing 67 ms schedule, with unchanged stop/ownership checks and 200 ms live transition blending.

The preview and device output share `ExtraEffects.Frame`. All RGB interpolation follows hue to retain saturation instead of mixing through grey; neutral user choices remain available. The keyboard preview uses a dark bed and low-opacity zone diffusion to make the four coloured regions easier to read.

Run `tests/run.ps1` for software checks, then `Build-Release.ps1` for a local installer and ZIPs. See [VALIDATION.md](VALIDATION.md) for the measured software checks and the remaining physical checks. No OEM software or code is bundled.
