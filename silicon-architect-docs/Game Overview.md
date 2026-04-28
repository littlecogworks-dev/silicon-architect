# Game Overview

## Elevator Pitch

Silicon Architect is a mobile-first merge idle game where the player builds a compact futuristic city on a fixed grid. Homes merge into higher-tier districts, parks reduce pollution pressure, and power constraints stop the board from becoming a mindless number climb.

## Core Fantasy

- Build a dense, efficient mini-city
- Merge low-tier buildings into stronger districts
- Balance growth against pollution and power limits
- Watch the board generate passive cash with satisfying visual feedback

## Current Theme Split

The project is intentionally hybrid right now.

- Player-facing theme: city-builder
- Internal mechanics flavor: circuitry / systems management

This keeps the game readable for players without throwing away the original prototype's simulation identity.

## Platform

- Primary target: Android
- Orientation: portrait
- Input focus: touch-first, desktop still usable during development

## Current Loop

1. Start with a seeded board
2. Earn passive cash from active buildings
3. Buy a new Home onto an empty lot
4. Merge matching buildings into better districts
5. Manage pollution and power draw as the city scales
6. Trigger temporary 2x income boosts

## Design Goal

Ship a stable, readable v1 quickly, then expand depth only where it improves retention or feel.