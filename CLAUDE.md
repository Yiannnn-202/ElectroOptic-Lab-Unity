# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

This is a Unity 2022.3.62f2c1 project for an electro-optic lab simulation. The project simulates crystal optics experiments including conoscopic interference patterns, polarized light propagation, and electro-optic modulation.

## Multi-Project Structure

This repo contains multiple independent Unity projects:
- **ElectroOptic-Lab/** — Main simulation project (primary working directory for all code below)
- **3DAssets/** — Separate project for 3D model asset management
- **Screen/** — Separate screen-related project
- **TestRepo/** — Test/sandbox project

All paths below are relative to the **ElectroOptic-Lab/** project directory.

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
- **EOEnums.cs**: Enums for PropagationAxis, ElectricFieldAxis, ModulationMode

### Experiment Module (P0-P2)

The experiment module extends the base system without modifying original code:

**Data Transfer** (`Scripts/DataTransfer/`):
- **CrystalSelectionData**: Static class for cross-scene crystal profile selection
- **CrystalRuntime**: Static class providing runtime access to crystal components (Controller, TextureRenderer, PhysicalCore)

**Interfaces** (`Scripts/Experiment/Interfaces/`):
- **ICrystalSelectable**: Interface for crystal card selection in preview scene
- **ICrystalConfigurable**: Interface for crystal rotation/configuration control

**Controllers** (`Scripts/Experiment/Controller/`):
- **CrystalControllerWrapper**: Wraps CrystalPhysicalCore with rotation control (-15° to +15° range), implements ICrystalConfigurable
- **CrystalKnobBridge**: Bridges crystal box knob UI buttons to CrystalControllerWrapper for real-time rotation adjustment

**Initializer** (`Scripts/Experiment/Initializer/`):
- **CrystalComponentInitializer**: Auto-initializes crystal components at scene start (adds CrystalPhysicalCore, CrystalControllerWrapper, ConoscopicTextureRenderer; registers to CrystalRuntime; loads selected profile from CrystalSelectionData)

**Renderer** (`Scripts/Experiment/Renderer/`):
- **ConoscopicTextureRenderer**: Renders conoscopic interference pattern to RenderTexture using a dedicated offscreen camera and preview quad with ConoscopicInterference shader

**Crystal Selector** (`Scripts/UI/CrystalSelector/`):
- **CrystalCardSelector**: Handles crystal card click in Scene2-preview, saves selection to CrystalSelectionData and loads Scene2

### Optical Component System

Light propagation uses a chain-of-responsibility pattern:

- **IOpticalReceiver interface** (OpticalDef.cs): Components that can receive light implement this
- **LightData struct**: Carries intensity, polarization angle, and degree of polarization
- **LaserEmitter.cs**: Emits laser using LineRenderer and Raycast along `-transform.right`, calling `ReceiveLight()` on hit objects
- **PolarizerPhysics.cs**: Implements Malus's law for polarized light, chains to next receiver with fully polarized output (DOP=1)
- **DirectScreenController.cs**: Implements IOpticalReceiver, renders red dot tracking pattern to a 512x512 Texture2D

### Optical Rail and Placement System

- **OpticalRail.cs**: Defines rail with direction, length, and snap position calculation (clamps to X range, fixes Y to rail top, Z to zero)
- **OpticalComponent.cs** (OpticalComponent_Keyboard.cs): Optical component movement — click to pick up, A/D to move along rail axis, Space to drop/snap, double-click to remove from rail. Supports `isOnRail` state tracking used by UnifiedScreenPanel for mode switching
- **RailObjectMover.cs** (Assets/): Generic rail object mover with axis selection, limits, outline highlight, and close-up camera anchor support

### Laser Adjustment System

Located in `Scripts/Laser/`:

- **LaserStateController.cs**: Laser selection via click with Outline highlight toggle
- **LaserEmitterMover.cs**: Physical micro-adjustment of laser position (WASD within ±0.035m range), with Enter key to lock calibration
- **LaserKnobBridge.cs**: UI knob buttons that rotate the laser assembly for fine alignment

### Screen Display Module (New Unified Panel)

Located in `Scripts/UI/ScreenDisplay/`:

- **UnifiedScreenPanel.cs**: Bottom-left fixed panel with dual-layer display (red dot tracking / conoscopic interference). Automatically switches mode based on `OpticalComponent.isOnRail` state, with cross-fade transition animation
- **IScreenDataProvider.cs**: Interface decoupling data sources from display panel
- **DirectScreenDataProvider.cs**: Wraps DirectScreenController.SharedTexture for red dot display
- **ConoscopicScreenDataProvider.cs**: Wraps CrystalRuntime.TextureRenderer.RenderTexture for conoscopic display
- **ScreenMode.cs**: Enum (Direct / Conoscopic)
- **CanvasGroupTweener.cs**: Utility for CanvasGroup fade-in, fade-out, cross-fade animations

### Oscilloscope Module

Located in `Scripts/Oscilloscope/`:

- **OscilloscopeCore.cs**: Top-level orchestrator — takes OscilloscopeParameters, calls Bridge for DLL sensitivity, computes Vπ via VpiCalculator, generates waveform via WaveformCalculator. Uses dirty flag optimization, fires OnWaveformUpdated event for UI
- **OscilloscopeCrystalBridge.cs**: Wraps CrystalPhysicalCore for oscilloscope-specific usage — constructs CrystalConfig, calls ApplyConfig, returns Sensitivity. Caches last config state to avoid redundant DLL calls
- **OscilloscopeParameters.cs**: Serializable input container (VDC, Vm, frequency, modulation mode, field axis, compensator phase, intensity max)
- **VpiCalculator.cs**: Pure math — calculates half-wave voltage Vπ from wavelength, dimensions, sensitivity, and modulation mode
- **WaveformCalculator.cs**: Pure math engine — fills Ch1 (AC voltage) and Ch2 (transmitted intensity) waveform arrays
- **WaveformResult.cs**: Output container (ch1, ch2 float arrays, vPi, gamma0)
- **OscilloscopeWaveformGraphic.cs**: Custom uGUI Graphic subclass rendering waveform lines via OnPopulateMesh (no texture/material needed)
- **Scene4OscilloscopeDispatcher.cs**: Scene4 UI orchestrator — binds oscilloscope parameters to UI (voltage display, status, key-point recording), drives waveform refresh via OscilloscopeCore events, auto-binds UI references from DataCanvas hierarchy

### Conoscopic Analysis Module

Located in `Scripts/ConoscopicAnalysis/`, this module provides two independent computational pipelines for conoscopic interference, both using `ElectroOptics.ConoscopicAnalysis` namespace:

**CPU-based Intensity pipeline** (dirty-flag-driven MonoBehaviour):
- **ConoscopicIntensityCore.cs**: MonoBehaviour orchestrator — takes `ConoscopicIntensityParameters`, calls `CrystalPhysicalCore.ApplyConfig`, delegates to `ConoscopicIntensityCalculator.Compute`. Uses dirty-flag pattern like OscilloscopeCore.
- **ConoscopicIntensityCalculator.cs**: Static computation engine — computes interference intensity across a grid using Fresnel-based analytic formulas.
- **ConoscopicIntensityParameters.cs**: Serializable input (resolution, FOV, wavelength, phase scale, display mapping, laser color, crystal rotation).
- **ConoscopicIntensityResult.cs**: Output container (intensity array, min/max, validity flag).
- **ConoscopicIntensitySurfaceVisualizer.cs**: 3D mesh surface visualization of the computed intensity field.

**GPU-based Jones calculus pipeline** (shader-driven via `[ExecuteAlways]`):
- **ConoscopicJonesGpuCore.cs**: GPU orchestrator — uses `ElectroOptics/ConoscopicJonesIntensity` shader to compute intensity via Jones calculus on the GPU. Supports uniaxial and biaxial crystal modes. Reads back via `Graphics.Blit` + `ReadPixels` for min/max estimation.
- **ConoscopicJonesCpuReference.cs**: CPU reference implementation of Jones calculus for validation/comparison.
- **ConoscopicJonesParameters.cs**: Serializable input with dual-mode profile support (uniaxial via no/ne or biaxial via nx/ny/nz). Includes `biaxialDisplayMode` enum (ConoscopicTeaching, RawJones, PaperKtp1). Auto-resolves no/ne from principal indices. Supports polarizer/analyzer angle, crystal rotation, optic axis tilt, electric field, and KTP paper preset.
- **ConoscopicJonesResult.cs**: Output container referencing the computed RenderTexture.
- **ConoscopicJonesSurfaceVisualizer.cs**: 3D mesh visualization for Jones results.

Both pipelines bind to `CrystalPhysicalCore` to apply crystal configs and read principal indices / world-to-principal matrices. The Jones pipeline can also operate without a `CrystalPhysicalCore` by falling back to profile defaults.

### Testing

Editor tests (run via Unity Test Runner or menu commands):
- **OscilloscopeCalcTests.cs** (`Scripts/Oscilloscope/Editor/`): Tests for VpiCalculator and WaveformCalculator. Run via menu **ElectroOptics/Tests/Run Oscilloscope Calc Tests**. Covers extinction, frequency doubling, same-frequency modulation, compensator phase, and array-reuse validation.
- **ConoscopicIntensityCoreTests.cs** (`Scripts/ConoscopicAnalysis/Editor/`): Tests for ConoscopicIntensityCalculator against known analytic results.
- **ConoscopicJonesCoreTests.cs** (`Scripts/ConoscopicAnalysis/Editor/`): Tests for ConoscopicJonesCpuReference against ConoscopicJonesGpuCore.
- **PowerReadoutCalculatorTests.cs** (`Scripts/Power/Editor/`): Tests for PowerReadoutCalculator transmission and alignment efficiency math.
- **LiNbO3PowerReadoutVpiTests.cs** (`Scripts/Power/Editor/`): Tests for LiNbO3 Vπ calculation against expected values.

Editor-only visualization builders also exist in `ConoscopicAnalysis/Editor/` for constructing test scenes programmatically.

### Rotate Stand System

- **RotateStandController.cs**: Polarizer rotation stand — double-click opens dial window, single click selects (Outline highlight), A/D rotates. Global mutual exclusion prevents multiple selections
- **RotateWindowController.cs**: Dynamically created dial window showing current rotation angle with drag support
- **RotateVirtualKeys.cs**: Virtual UI buttons that drive RotateStandController rotation (alternative to A/D keys)

### Quiz System

Located in `Scripts/exercise/`:

- **QuizManager.cs**: Generates random quiz from 34-question bank, manages answer submission, scoring, and navigation
- **QuestionData.cs**: Question data structure (question text, 4 options, correct index, explanation)
- **QuestionItemUI.cs**: Individual question UI item with option selection and explanation reveal

### Power Meter

- **ReceiverStateController.cs** (Scripts/Receiver/): Receiver state machine (0=off, 1=monitoring/blue, 2=selected for adjustment/green). Double-click to power on/off, single click to toggle monitor/selected
- **PowerReadoutController.cs**: Power meter readout window with virtual adjustment via WASD, calculates power based on beam focus model

### Power Readout Math

Located in `Scripts/Power/` (separate from the Power Meter UI in `Scripts/Receiver/`):

- **IPowerReadoutSource.cs**: Interface exposing `CurrentStablePower`, `CurrentDisplayPower`, `CurrentAlignmentEfficiency` for any power-measuring component.
- **IVoltageSource.cs**: Interface for components that provide a voltage value.
- **PowerReadoutCalculator.cs**: Static calculator for electro-optic power transmission. Computes transmission via `sin²(πV/(2Vπ))` with leakage/visibility parameters, alignment efficiency from Gaussian beam focus model, and Perlin-noise display jitter. Pure math — no MonoBehaviour dependency.

### Voltage Switch / Camera Focus

Located in `Scripts/UI/VoltageSwitch/`:

- **CameraFocusController.cs**: Smooth camera focus transition to target transform based on click area
- **ClickAreaFocus.cs**: Clickable zones that trigger camera focus transitions

### Other Systems

- **CameraSwitch.cs**: Smooth camera transition between default, front, and top views
- **ExperimentCameraController.cs**: Close-up view system — moves camera to object's closeUpCameraAnchor, notifies RailObjectMover to show/hide close-up UI
- **RecordManager.cs**: Data recording table — records voltage/power pairs to table cells, supports delete and clear
- **KnobAdjuster.cs** (Scripts/ViewButton/): Hold-down UI knob that rotates a target 3D knob model
- **SceneLoad.cs** (Scripts/Buttons/): Button-based scene loading (used in main menu and navigation)

### Shader Visualization

- **ConoscopicInterference.shader**: GPU-based visualization of interference patterns using Fresnel equations. Used by ConoscopicTextureRenderer and CrystalVisualizer.
- **ConoscopicJonesIntensity.shader** (registered as `ElectroOptics/ConoscopicJonesIntensity`): GPU-based Jones calculus intensity computation. Used by ConoscopicJonesGpuCore for both uniaxial and biaxial crystal modes. Supports polarizer/analyzer angles, crystal rotation, optic axis tilt, electric field modulation, and KTP paper-mode display.
- **CrystalVisualizer.cs** (Scripts/ShaderScripts/): Syncs crystal physics data to shader properties (refractive indices, rotation matrix, crystal length, wavelength, FOV, base color)
- **Mat_Conoscopic.mat**: Material using the ConoscopicInterference shader for visualization

### Scene Structure

Main scenes in `Assets/Scenes/`:
- **Scene0.Open Menu.unity**: Main menu
- **Scene1.intro.unity**: Introduction / tutorial
- **Scene2-preview.unity**: Crystal selection preview scene (CrystalCardSelector)
- **Scene2.The Lab.unity**: Primary lab scene (main experiment area with rail system, laser, crystal, screen, oscilloscope)
- **Scene4_UIRebuild 1.unity**: Rebuild oscilloscope scene with waveform rendering, voltage/status UI, and key-point recording (Scene4OscilloscopeDispatcher)
- **Scene5.History Records.unity**: History records / data log viewer
- **Scene6_Quiz.unity**: Quiz/exercise scene

Work-in-progress / legacy scenes (not production):
- SceneTest.unity, SceneTest2.unity, test.unity
- Scene3.Exp1.unity, Scene3_UIRebuild.unity (earlier experiment UI prototypes)
- Scene4.Exp1 1.unity (earlier experiment UI prototype)

### Deprecated Code (not referenced in any scene)

- **CrystalInteract.cs**: Old crystal interaction (double-click select + WASD rotation + random initial deflection). Replaced by CrystalControllerWrapper + CrystalRotationPanel.
- **ScreenInteract.cs**: Old screen interaction (double-click toggles conoscopeUIPanel). Replaced by UnifiedScreenPanel.

### Coordinate System Notes

The native DLL uses right-handed coordinates; Unity uses left-handed. Conversion is handled in `CrystalPhysicalCore.cs` via Z-flip on rotation matrices.

### Packages / Plugins

- **QuickOutline** (Assets/QuickOutline/): Outline highlight effect used by selectable optical components
- **Postprocessing** (via Package Manager): Post-processing stack for visual effects
- **TextMesh Pro** (via Package Manager): Advanced text rendering for UI elements

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
| `ElectroOptics.Experiment.Controller` | CrystalControllerWrapper, CrystalKnobBridge |
| `ElectroOptics.Experiment.Initializer` | CrystalComponentInitializer |
| `ElectroOptics.Experiment.Renderer` | ConoscopicTextureRenderer |
| `ElectroOptics.UI.ScreenDisplay` | UnifiedScreenPanel, IScreenDataProvider, ScreenMode, CanvasGroupTweener |
| `ElectroOptics.UI.ControlPanel` | CrystalRotationPanel, RotationKnob, AngleDisplay |
| `ElectroOptics.UI.CrystalSelector` | CrystalCardSelector |
| `ElectroOptics.Oscilloscope` | OscilloscopeCore, OscilloscopeCrystalBridge, OscilloscopeParameters, WaveformCalculator, WaveformResult, VpiCalculator, OscilloscopeWaveformGraphic, Scene4OscilloscopeDispatcher |
| `ElectroOptics.ConoscopicAnalysis` | ConoscopicIntensityCore, ConoscopicIntensityCalculator, ConoscopicIntensityParameters, ConoscopicIntensityResult, ConoscopicIntensitySurfaceVisualizer, ConoscopicJonesGpuCore, ConoscopicJonesCpuReference, ConoscopicJonesParameters, ConoscopicJonesResult, ConoscopicJonesSurfaceVisualizer |

## Material Safety

When modifying materials at runtime, use `.material` (creates instance) not `.sharedMaterial` (modifies asset permanently).
