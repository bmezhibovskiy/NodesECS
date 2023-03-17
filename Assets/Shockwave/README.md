# Shockwaves

Shockwaves is an HDRP shader that renders circular shockwave distortions.

## Installation

- In your HDRP global settings asset, add `com.borismez.ShockwavesHDRP.Shockwave` to the `After Post Process` section of `Custom Post Process Orders`. (You might need to scroll down to the bottom).
- Put the ShockwaveManager prefab into your scene, or do the following three steps:
	- Create an empty GameObject in your scene.
	- Add the `ShockwaveManager` script to the GameObject.
	- Add a `Volume` component.
- In the `Volume` component:
	- Populate the profile with your HDRP Settings Volume Profile.
	- Click `Add Override` to add the `Shockwave` override to the Volume, if it doesn't already exist.

## Usage

To see if the shockwaves work, click the `Debug Mouse Click Shockwaves` checkbox in the inspector of the ShockwaveManager component. Your mouse clicks should generate shockwaves at the mouse location.

The `ShockwaveManager` component can be passed to other scripts, or it can be added to an existing GameObject along with the Volume component.

The `ShockwaveManager` component has a function `AddShockwave` which can be used to create a shockwave. The parameters are described below:
- `Vector3 worldPos` The position in world space of the center of the shockwave.
- `Camera camera` The camera is used to calculate screen space coordinates.
- `maxTime` The time at which the shockwave gets removed. Example: `Time.time + 1.0f`
- `gauge` How thin the shockwave is, applied multiplicatively. Default 1.
- `intensity` The amplitude of the shockwave, applied multiplicatively. Default 1.
- `decaySpeed` How quickly the shockwave dissipates, applied multiplicatively. Default 1.