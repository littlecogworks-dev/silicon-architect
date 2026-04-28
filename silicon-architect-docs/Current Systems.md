# Current Systems

## Board

- Fixed `5x5` grid
- Each tile is a `MotherboardTile` in code, but displayed as a city district to the player
- Board is sized responsively for portrait play

## Economy

- Buildings generate passive income each simulation tick
- New Homes cost cash to place
- Build cost scales upward after each successful placement
- A temporary `2x Income` boost exists and is currently dev-triggered

## Merge Chain

- `Home` -> `Apartment` -> `Commercial Hub`
- `Power Grid` and `Park` are support roles
- Empty lots are valid build targets

## Simulation

- Pollution still uses local bleed between neighbors
- Power demand is compared against board capacity
- Under-supplied income districts lose output efficiency
- Parks provide cooling support by their existing support-role stats

## Feedback Layer

- Floating income text appears near active tiles
- Merge pulse includes wobble
- Placement and merge visuals animate the tile socket only to avoid overlap bugs
- Touch selection updates the bottom readout for the currently inspected tile

## UI

- Top HUD for city status
- Center board for tile interactions
- Bottom bar for selected tile info and build actions
- Double-income button is wired in UI flow already

## Code Structure

- `GridManager.cs`: board setup, simulation, interaction flow
- `GridManager.Hud.cs`: HUD binding, selected tile readout, floating text, income boost button state
- `MotherboardTile.cs`: tile visuals, touch/click input, per-tile animation, tooltip data
- `CityTerminology.cs`: centralized player-facing text

## Known Constraints

- Internal enum and exported field names still use some original hardware terms for safety
- Scene naming still includes legacy labels such as `SpawnTransistorButton`
- Real rewarded ads are not wired yet
- Audio hooks exist but assets are not assigned yet