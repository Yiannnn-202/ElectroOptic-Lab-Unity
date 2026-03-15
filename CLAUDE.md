# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

This is a Unity 2022.3.62f2c1 project for an electro-optic lab simulation. The project simulates crystal optics experiments including conoscopic interference patterns, polarized light propagation, and electro-optic modulation.

## Build and Run

This is a Unity project - open in Unity Editor (2022.3.62f2c1 or compatible) and build/run through Unity's standard build system. The main scene is `ElectroOptic-Lab/Assets/Scenes/Scene2.The Lab.unity`.

## Development Principle

**Decoupled design**: New code should not modify original code. Use wrapper/adapter patterns to extend functionality. The experiment module (P0-P2) was built following this principle - all new code lives in separate directories under `Scripts/DataTransfer/`, `Scripts/Experiment/`, and `Scripts/UI/`.

## Core Architecture

### Native Physics Engine Integration

The project uses a native C++ DLL (`CrystalPhysicsCore.dll` in `Assets/Plugins/x86_64/`) for crystal physics calculations. The integration is handled through:

- **NativeInterface.cs** (Scripts/DataContract/): Safe wrapper around DLL P/Invoke calls with validation
- **SimInputData/CrystalOutputData** (DataContracts.cs): Structs matching C++ memory layout for data exchange
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

### Experiment Module (P0-P2)

The experiment module extends the base system without modifying original code:

**Data Transfer** (`Scripts/DataTransfer/`):
- **CrystalSelectionData**: Static class for cross-scene crystal profile selection
- **CrystalRuntime**: Static class providing runtime access to crystal components (Controller, TextureRenderer, PhysicalCore)

**Interfaces** (`Scripts/Experiment/Interfaces/`):
- **ICrystalSelectable**: Interface for crystal card selection in preview scene
- **ICrystalConfigurable**: Interface for crystal rotation/configuration control

**Controllers** (`Scripts/Experiment/`):
- **CrystalControllerWrapper**: Wraps CrystalPhysicalCore with rotation control (-15° to +15° range)
- **CrystalComponentInitializer**: Auto-initializes crystal components at scene start
- **ConoscopicTextureRenderer**: Renders interference pattern to RenderTexture for popup display

**UI** (`Scripts/UI/`):
- **ScreenPopupManager**: Double-click detection on screen, dispatches appropriate popup
- **ConoscopicWindowView**: Popup window displaying conoscopic interference pattern
- **CrystalRotationPanel**: Crystal XY rotation control panel with draggable knobs

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
- **Scene2-preview.unity**: Crystal selection preview scene
- **Scene2.The Lab.unity**: Primary lab scene (main experiment area)
- **Scene5.History Records.unity**: History records

### Coordinate System Notes

The native DLL uses right-handed coordinates; Unity uses left-handed. Conversion is handled in `CrystalPhysicalCore.cs` via Z-flip on rotation matrices.

### Code Language

The codebase contains Chinese comments and variable names. When modifying or reading, be aware of the mixed language context.

## Working with Crystal Physics

When modifying crystal behavior:
1. Modify `CrystalProfile` assets (e.g., `KDP.asset`, `LiNbO3_Profile.asset` in Assets/) for new crystal types
2. Changes to physics require rebuilding the native C++ DLL and replacing it in `Assets/Plugins/x86_64/`
3. The data contracts in `DataContracts.cs` must match the C++ struct layouts exactly
4. Crystal XY rotation is limited to -15° to +15° range

## Working with Optical Components

Optical components follow a chain pattern. When adding new components:
1. Implement `IOpticalReceiver`
2. In `ReceiveLight()`, process light and optionally call `ReceiveLight()` on next receiver via Raycast
3. Use LineRenderer for visualization of outgoing light
4. Ensure proper layer/collider setup for Raycast detection

## Working with UI Windows

Windowed UI is created dynamically at runtime:
- Windows use Screen Space Overlay canvas named "WindowsCanvas"
- Drag functionality via `SimpleDrag` class or EventTrigger
- Canvas scaling is set to 1920x1080 reference resolution
- Ensure EventSystem exists before creating UI elements
- Double-click detection uses 0.3s interval consistently

## Namespaces

| Namespace | Contains |
|-----------|----------|
| `ElectroOptics` | CrystalProfile, CrystalConfig, CrystalPhysicalCore (original) |
| `ElectroOptics.DataTransfer` | CrystalSelectionData, CrystalRuntime |
| `ElectroOptics.Experiment.Interfaces` | ICrystalSelectable, ICrystalConfigurable |
| `ElectroOptics.Experiment.Controller` | CrystalControllerWrapper |
| `ElectroOptics.Experiment.Initializer` | CrystalComponentInitializer |
| `ElectroOptics.Experiment.Renderer` | ConoscopicTextureRenderer |
| `ElectroOptics.UI.ScreenPopup` | ScreenPopupManager, ConoscopicWindowView |
| `ElectroOptics.UI.ControlPanel` | CrystalRotationPanel, RotationKnob, AngleDisplay |

## Material Safety

When modifying materials at runtime, use `.material` (creates instance) not `.sharedMaterial` (modifies asset permanently).
