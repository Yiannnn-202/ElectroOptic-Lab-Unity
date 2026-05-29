# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

This is a Unity 2022.3.62f2c1 project for an electro-optic lab simulation. The project simulates crystal optics experiments including conoscopic interference patterns, polarized light propagation, and electro-optic modulation.

## Multi-Project Structure

This repo contains multiple Unity projects:
- **ElectroOptic-Lab/** — Main simulation project (primary working directory for all code below)
- **3DAssets/** — Legacy project for 3D model asset management
- **Screen/** — Legacy screen-related project
- **TestRepo/** — Test/sandbox project
- **Docs/** — Structured documentation (PRD, Architecture, API, DevLog, Guide, Plan)

All paths below are relative to the **ElectroOptic-Lab/** project directory.

## Build and Run

This is a Unity project - open in Unity Editor (2022.3.62f2c1 or compatible) and build/run through Unity's standard build system. The main scene is `ElectroOptic-Lab/Assets/Scenes/Scene2.The Lab.unity`.

## Development Principle

**Decoupled design**: New code should not modify original code. Use wrapper/adapter patterns to extend functionality. The experiment module (P0-P2) was built following this principle - all new code lives in separate directories under `Scripts/DataTransfer/`, `Scripts/Experiment/`, and `Scripts/UI/`.

## Assembly Structure

The project's own C# scripts have **no `.asmdef` files** — all game code compiles into the default `Assembly-CSharp.dll` and editor scripts into `Assembly-CSharp-Editor.dll`. The only custom assemblies are in the third-party **XCharts** plugin (`XCharts.Runtime`, `XCharts.Editor`, `XCharts.Examples`). If the project grows large, adding asmdef files would improve iteration speed.

## Authoritative Experiment Workflow (Scene2.The Lab)

This is the correct five-step experiment procedure. Any discussion about experiment flow should reference this order:

1. **Place the light screen** — pick up the screen from the component area and snap it onto the optical rail. The `UnifiedScreenPanel` shows the red dot tracking view (laser → screen directly).

2. **Align the laser to screen center** — enter close-up view, micro-adjust the laser position (`LaserEmitterMover`, WASD ±0.035m range) until the red dot is centered on the screen crosshair, ensuring beam collimation.

3. **Extinction verification (polarizer + analyzer)** — place the polarizer and analyzer on the rail in sequence (laser → polarizer → analyzer → screen). Rotate the analyzer so its transmission axis is orthogonal to the polarizer's. Observe the spot on screen gradually darkening to extinction — this verifies Malus's law. The optical path is: laser → polarizer(∥vertical) → analyzer(∥horizontal) → screen (dark).

4. **Place crystal + beam expander → observe conoscopic interference** — insert the crystal box between polarizer and analyzer; insert the beam expander in front of the crystal box (converts converging light to conical light incident on the crystal). When the expander, crystal box, and screen are all on the rail, `UnifiedScreenPanel` auto-switches to conoscopic interference mode. Double-click the crystal box for close-up view and operate the knobs to adjust crystal pitch/yaw while observing real-time interference pattern changes.

5. **Remove screen and expander → place photodetector → enter experiment** — remove the screen and beam expander (restoring parallel-beam path). Place the photodetector behind the crystal box. Power on → micro-align → proceed to electro-optic modulation experiments: extremum method for Vπ measurement (`RecordManager` records voltage-power data), and Scene4 oscilloscope for frequency-doubling distortion observation.

**Source**: `memory/project_experiment_workflow.md`

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
- **CrystalWorkingGeometry.cs**: Resolves working geometry (light direction, E-field direction, modulation mode) for conoscopic and oscilloscope contexts. Contains special KTP handling that forces Vector3.up light direction and transverse modulation mode.

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
- **PowerReadoutController.cs** (Scripts/ root): Power meter readout window implementing `IPowerReadoutSource`. Virtual adjustment via WASD (±0.15m initial random deviation, 0.2 m/s adjust speed), calculates power via Gaussian beam focus model. Supports dark power, leakage, visibility, and phase offset parameters. Auto-resolves half-wave voltage from RecordManager or crystal config.

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
- **CrystalStateController.cs** (Scripts/ root): Simple crystal click-to-toggle selection with color change (green/original). Separate from the CrystalControllerWrapper experiment system.
- **FocusableItem.cs** (Scripts/ root): Component providing closeUpCameraAnchor transform for ExperimentCameraController close-up views.
- **Cardclick.cs** (Scripts/ root): Simple scene loader (loads Scene2.The Lab). Legacy utility.

### Scene3 Data Analysis (Scripts/Scene3_UIRebuild/)

- **UIStateManager.cs**: Data fitting and analysis orchestrator — performs nonlinear curve fitting on recorded voltage/power data using **MathNet.Numerics**, renders scatter + fit curves via **XCharts**, computes residual chart, and extracts Vπ from fitted extrema.
- **KnobToggleController.cs**: UI knob with Outline highlight toggle on pointer click.

### Debug Tools

- **CoreDebugger.cs** (Scripts/Business_logic/): Inspector-driven debug harness for CrystalPhysicalCore — exposes Euler angles, light direction, voltage, thickness, and E-field direction for real-time experimentation without the full LabController pipeline.
- **RenderStateDiagnostics.cs** (Scripts/Debug/): Comprehensive render state dump tool — logs camera HDR, PostProcessLayer, Global Volume effects (Bloom), directional light, RenderSettings, QualitySettings, area lights, and Outline components. Attach to camera for scene comparison debugging.
- **BridgeLayerTest.cs** (Scripts/DataContract/): Bridge layer test for NativeInterface validation.

### Shader Visualization

| Shader | File | Purpose |
|--------|------|---------|
| Conoscopic Interference | `ConoscopicInterference.shader` | GPU Fresnel-based interference pattern visualization. Used by ConoscopicTextureRenderer and CrystalVisualizer. |
| Conoscopic Jones Intensity | `ConoscopicJonesIntensity.shader` | GPU Jones calculus intensity computation (registered as `ElectroOptics/ConoscopicJonesIntensity`). Supports uniaxial/baixial modes, polarizer/analyzer angles, crystal rotation, optic axis tilt, E-field modulation, and KTP paper-mode display. Used by ConoscopicJonesGpuCore. |
| Intensity Vertex Color | `ConoscopicIntensityVertexColor.shader` | Vertex-color shader for 3D mesh surface visualization of computed intensity fields. Used by ConoscopicIntensitySurfaceVisualizer and ConoscopicJonesSurfaceVisualizer. |
| Dot Tracking | `DotTracking.shader` | Light spot / red dot rendering on screen. |
| Waveform Line | `WaveformLine.shader` | Oscilloscope waveform line rendering. Used by OscilloscopeWaveformGraphic. |
| Outline Fill / Mask | `OutlineFill.shader`, `OutlineMask.shader` | QuickOutline package shaders for selection highlight effect. |

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
- **Scene7_Report.unity**: Experiment report generation and export

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
- **XCharts** (Assets/): Unity charting library used by UIStateManager for scatter plots, line charts, and residual charts in data analysis
- **MathNet.Numerics** (`Assets/Plugins/MathNet.Numerics.dll`): .NET numerical library used by UIStateManager for nonlinear least-squares curve fitting (`Fit.Curve`)

Package registry is `https://packages.unity.cn` (Unity China CDN). Contributors outside China may need to switch to `https://packages.unity.com`.

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
| `ElectroOptics` | CrystalProfile, CrystalConfig, CrystalPhysicalCore, CrystalWorkingGeometry (original core) |
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
| `ElectroOptics.Power` | IPowerReadoutSource, IVoltageSource, PowerReadoutCalculator |

## Material Safety

When modifying materials at runtime, use `.material` (creates instance) not `.sharedMaterial` (modifies asset permanently).

## Documentation

Structured design docs live in `Docs/` at the repo root:

| Directory | Contents |
|-----------|----------|
| `PRD/` | Product requirements — what to build and acceptance criteria |
| `Architecture/` | Architecture design — codebase audit, biaxial display, interaction systems |
| `API/` | Public API and interface contracts |
| `DevLog/` | Development logs — implementation process, decisions, version records |
| `Guide/` | Editor setup guides and configuration steps |
| `Plan/` | Phased development plans and task breakdowns |

Key docs: [Codebase Audit](Docs/Architecture/Architecture_Codebase_Audit.md) (enabled vs deprecated logic), [ScreenDisplay API](Docs/API/ScreenDisplay_API.md), [Biaxial Conoscopic Display](Docs/Architecture/Architecture_Biaxial_Conoscopic_Display.md).

## Standalone Validation Tools

The `Experiment/` directory at the repo root contains a standalone Python tool for validating `CrystalPhysicsCore.dll` without Unity:

- **`generate_kdp_eo_data.py`**: Calls `CrystalPhysicsCore.dll` via Python `ctypes`, duplicating the C# struct layouts (`SimInputData`, `CrystalOutputData`) in Python. Computes KDP electro-optic response (refractive indices under E-field along Z axis) and compares DLL output against first-order analytic theory. Outputs `kdp_eo_response.csv` and `kdp_eo_response.md`.

Usage: `python Experiment/generate_kdp_eo_data.py --fields 0 2e5 5e5 1e6 2e6 5e6 1e7`

This tool is useful for DLL regression testing when modifying the native code without opening Unity.

## StreamingAssets

Runtime web content loaded by Unity scenes:

| Directory | File | Purpose |
|-----------|------|---------|
| `StreamingAssets/QuizWeb/` | `quiz.html` | HTML-based quiz interface loaded by Scene6_Quiz |
| `StreamingAssets/ReportWeb/` | `report.html`, `report.css`, `report.js` | Web-based experiment report system loaded by Scene7_Report |

These are rendered via Unity's web view component. Modifications to quiz content or report templates should be made here.

## Editor-Only Scene Builders

Two programmatic scene construction tools exist in `Scripts/ConoscopicAnalysis/Editor/` for building test/visualization scenes in the Editor:

- **ConoscopicIntensityVisualizationSceneBuilder.cs**: Constructs test scenes for the CPU-based intensity pipeline.
- **ConoscopicJonesVisualizationSceneBuilder.cs**: Constructs test scenes for the GPU-based Jones pipeline.

## .claude/ Directory

The `.claude/` directory at repo root contains Claude Code configuration for this repository:

- **`settings.local.json`**: Grants Bash permissions for `node *` commands.
- **`agents/prd-analyst.md`**: Custom agent definition for PRD analysis — use via the Agent tool with `subagent_type: "prd-analyst"` when generating product requirement documents from codebase analysis.

## Empty / Stale Directories

- **`Scripts/Polarizer View/`** — Exists but contains no scripts. May be a placeholder or stale artifact.
