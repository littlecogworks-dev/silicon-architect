# Roadmap

## Current Priority

Stability-first v1 for a mobile-friendly merge idle prototype.

## Immediate Next Steps

1. Improve gameplay readability and feel
2. Add basic audio assets to the existing sound hooks
3. Tighten progression pacing and build-cost curve
4. Decide whether parks and power need clearer player-facing explanation
5. Wire a real rewarded-ad callback for the 2x income flow

## Short-Term Gameplay Work

- Better board progression over the first 3 to 5 minutes
- Clearer merge affordances
- More satisfying reward moments on upgrade
- Better support-role identity for Power Grid and Park tiles

## UX / Polish

- Touch readability pass
- Button language consistency
- Stronger tile-state readability at a glance
- Optional haptics and sound feedback on build/merge

## Technical Follow-Ups

- Extract interaction flow from `GridManager.cs` if the file grows again
- Keep player-facing text centralized in `CityTerminology.cs`
- Avoid renaming exported fields until the design stabilizes

## Release-Oriented Checklist

- Core loop fun for at least one short session
- No overlap/input regressions on mobile
- Cash flow readable without debug knowledge
- Audio assigned
- Android export still clean

## Not a Priority Right Now

- Full system rewrite
- Large content pipeline
- Deep narrative framing
- Risky serialized-field renames