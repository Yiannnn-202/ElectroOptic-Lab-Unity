# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

This is a Unity 2022.3.62f2c1 project for an electro-optic lab simulation. The project simulates crystal optics experiments including conoscopic interference patterns, polarized light propagation, and electro-optic modulation.

## Build and Run

This is a Unity project - open in Unity Editor (2022.3.62f2c1 or compatible) and build/run through Unity's standard build system. The main scene is `ElectroOptic-Lab/Assets/Scenes/Scene2.The Lab.unity`.

## Core Architecture

### Native Physics Engine Integration

The project uses a native C++ DLL (`CrystalPhysicsCore.dll` in `Assets/Plugins/x86_64/`) for crystal physics calculations. The integration is handled through:

- **NativeInterface.cs** (Scripts/DataContract/): Safe wrapper around DLL P/Invoke calls with validation
- **SimInputData/CrystalOutputData** (DataContract.cs): Structs matching C++ memory layout for data exchange
- The DLL calculates electro-optic coefficients, refractive indices under electric field, and rotation matrices

### Crystal Physics System

Located in `Scripts/Business_logic/`:

- **CrystalProfile.cs**: ScriptableObject defining crystal properties (refractive indices, electro-optic coefficients, default dimensions)
- **CrystalConfig.cs**: Runtime configuration struct containing crystal state (profile, rotation, electric field, light direction)
- **CrystalPhysicalCore.cs**: Core component that:
  1. Applies configurations via two-pass system (Probe Pass for geometry sensitivity, Render Pass for actual field)
  2. Calls native DLL for physics calculations
  3. Converts right-handed coordinate system from DLL to Unity's left-handed system
  4. Provides matrices and data to shaders
- **LabController.cs**: UI orchestrator that manages crystal configuration from UI controls (voltage, modulation mode, field axis)

### Optical Component System

Light propagation uses a chain-of-responsibility pattern:

- **IOpticalReceiver interface** (OpticalDef.cs): Components that can receive light implement this
- **LightData struct**: Carries intensity, polarization angle, and degree of polarization
- **LaserEmitter.cs**: Emits laser using LineRenderer and Raycast, calling `ReceiveLight()` on hit objects
- **PolarizerPhysics.cs**: Implements Malus's law for polarized light, chains to next receiver
- **DirectScreenController.cs**: Displays interference patterns on a screen with red dot tracking

### Shader Visualization

- **ConoscopicInterference.shader**: GPU-based visualization of interference patterns using Fresnel equations
- **CrystalVisualizer.cs**: Syncs crystal physics data to shader properties (refractive indices, rotation matrix, crystal length, wavelength)
- **Mat_Conoscopic.mat**: Material using the shader for visualization

### Scene Structure

Main scenes in `Assets/Scenes/`:
- **Scene0.Open Menu.unity**: Main menu
- **Scene1.intro.unity**: Introduction
- **Scene2.The Lab.unity**: Primary lab scene (main experiment area)
- **Scene2-preview.unity**: Preview/testing scene
- **SceneTest.unity**: Test scene
- **SceneTest2.unity**: Additional test scene

### Key Components and Controllers

- **OpticalRail.cs**: Defines rail constraints for optical component positioning (X-axis movement, fixed Y height)
- **RotateStandController.cs**: Controls polarizer rotation with double-click window system
- **RotateWindowController.cs**: Creates draggable UI windows for polarizer angle adjustment
- **PowerReadoutController.cs**: Simulates power meter readings with Gaussian beam profile
- **LaserStateController.cs**: Manages laser selection state
- **OpticalComponent_Keyboard.cs**: Keyboard controls for optical components

### Coordinate System Notes

The native DLL uses right-handed coordinates; Unity uses left-handed. Conversion is handled in `CrystalPhysicalCore.cs` via Z-flip on rotation matrices.

### Code Language

The codebase contains Chinese comments and variable names. When modifying or reading, be aware of the mixed language context.

## Working with Crystal Physics

When modifying crystal behavior:
1. Modify `CrystalProfile` assets (e.g., `KDP.asset`, `LiNbO3_Profile.asset` in Assets/) for new crystal types
2. Changes to physics require rebuilding the native C++ DLL and replacing it in `Assets/Plugins/x86_64/`
3. The data contracts in `DataContract.cs` must match the C++ struct layouts exactly

## Working with Optical Components

Optical components follow a chain pattern. When adding new components:
1. Implement `IOpticalReceiver`
2. In `ReceiveLight()`, process light and optionally call `ReceiveLight()` on next receiver via Raycast
3. Use LineRenderer for visualization of outgoing light
4. Ensure proper layer/collider setup for Raycast detection

## Working with UI Windows

Windowed UI (for polarizer adjustment, power meter, etc.) is created dynamically:
- Windows use Screen Space Overlay canvas
- Drag functionality via `SimpleDrag` class or EventTrigger
- Canvas scaling is set to 1920x1080 reference resolution
- Ensure EventSystem exists before creating UI elements
