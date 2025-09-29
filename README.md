# Hollow Knight-style 2D Platformer Prototype

This Unity project contains a small 2D playground scene with movement and camera behaviour inspired by *Hollow Knight*. Use it as a starting point for tuning character feel or for building additional gameplay systems.

## Contents

- **Assets/Scenes/Prototype.unity** – Sample scene with a controllable player, a follow camera, and a handful of platforms (including a slope) for testing.
- **Assets/Scripts/PlayerMovement.cs** – Handles responsive platformer movement, including coyote time, jump buffering, variable jump height, and smoothed acceleration.
- **Assets/Scripts/CameraFollow2D.cs** – Implements a soft follow camera with dead-zone, horizontal look-ahead, falling bias, and smooth damping.

## Features

### Character Controller
- Late jump forgiveness (coyote time) and jump buffering.
- Variable jump height (tap for short hop, hold for full jump).
- Separate ground/air acceleration and deceleration for smooth control.
- Optional fall gravity multiplier to make descents feel snappier.
- Debug gizmo for the ground check in the Scene view.

### Camera
- Rectangular dead-zone/soft zone around the player.
- Velocity-based horizontal look-ahead.
- Downward bias while the player is falling.
- Smooth damping for responsive yet stable framing.

## Getting Started

1. Open the project in **Unity 2021.3** or newer (any recent LTS version should work).
2. Load `Assets/Scenes/Prototype.unity` and press Play.
3. Use the default Unity input axes:
   - `A` / `D` or Left / Right arrows to move.
   - `Space` (or the Jump input) to jump.
4. Adjust the serialized fields on the `PlayerMovement` and `CameraFollow2D` components to fine-tune the feel.

Feel free to replace the placeholder sprites and colliders with your own level art or expand the prototype with additional mechanics.
