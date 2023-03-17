# Shockwaves

Shockwaves is an HDRP shader that renders circular shockwave distortions.

## Installation

- In your HDRP global settings asset, add Shockwave to the `After Post Process` section of `Custom Post Process Orders`. (You might need to scroll down to the bottom).
- Create an empty GameObject in your scene.
- Add a `Volume` component, and populate the profile with your HDRP Settings Volume Profile.
- Add the `ShockwaveManager` script.

## Usage

To see if the shockwaves work, click the `Debug Mouse Click Shockwaves` checkbox in the inspector. Your mouse clicks should generate shockwaves at the mouse location.

The `ShockwaveManager` can be passed to other scripts, or it can be added to an existing GameObject along with the Volume component.

The `ShockwaveManager` component has a function `AddShockwave` which you can use to create a shockwave. The parameters are described below:
- `Vector3 worldPos` The position in world space of the center of the shockwave.
- `Camera camera` The camera is used to calculate screen space coordinates.
- `maxTime` The time at which the shockwave gets removed. Example: `Time.time + 1.0f`
- `gauge` How thin the shockwave is, applied multiplicatively. Default 1.
- `intensity` The amplitude of the shockwave, applied multiplicatively. Default 1.
- `decaySpeed` How quickly the shockwave dissipates, applied multiplicatively. Default 1.