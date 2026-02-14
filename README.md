# Soulmates

A multiplayer mod that matches players into pairs, where each player in a pair shares damage of the other. Based on the "shared damage" mod.

## Features

Implemented:

* Sharing damage!
* Sharing hunger and weight!
* Sharing healing!
* Sharing bonks! (configurable)
* Sharing extra stamina gain! (configurable)
* Sharing extra stamina USE! (configurable)
* Shared lollipops and energy drinks! (configurable)
* Shared slipping! (configurable)
* Many other shared afflictions! (all configurable)
* New soulmates chosen at every biome!
* You can set fixed soulmates!

Implemented, but maybe buggy:

* Players that disconnect and rejoin should re-discover what their soulmate is from other players.
* Collecting players into larger soulbound groups than 2 players! (configurable)
* Sharing soulmate damage/healing at a different rate than one-to-one! (configurable)

## Planned features

* Fix bugs.

## Installation

Using Thunderstore should "just work".

## Manual installation

1. Download and extract this mod into `BepInEx/plugins`.

## Configuration

1. Run the mod once.
2. Open and edit the file `BepInEx/config/com.github.Wesmania.Soulmates.cfg` in PEAK's directory.

### Fixed soulmates

You can set fixed pairings of soulmates in the configuration. Check the "FixedSoulmates" key. **NOTE THAT**:

* Your pairings must have as many people as the "SoulmateGroupSize" value.
* Nicknames must match exactly and cannot repeat.
* Fixed pairings are HOST-LOCAL. **If your host changes, fixed pairings will no longer apply!**

## Multiplayer Compatibility

This mod uses custom network events. Because of this it's best that all players
in the lobby have the mod installed. If some players don't have the mod, weird
and bad things will probably happen.

From version 0.3.0, the mod uses Steam networking instead of Photon. **Bad
things might happen if you mix-and-match versions before and after 0.3.0.**
