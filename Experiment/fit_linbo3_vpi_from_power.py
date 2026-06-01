from __future__ import annotations

import argparse
import csv
import ctypes
import math
import os
import random
from dataclasses import dataclass
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
DLL_DIR = ROOT / "ElectroOptic-Lab" / "Assets" / "Plugins" / "x86_64"
DLL_PATH = DLL_DIR / "CrystalPhysicsCore.dll"
CSV_PATH = Path(__file__).resolve().with_name("linbo3_power_vpi_fit.csv")
MD_PATH = Path(__file__).resolve().with_name("linbo3_power_vpi_fit.md")


class SimInputData(ctypes.Structure):
    _fields_ = [
        ("static_n", ctypes.c_double * 3),
        ("r_tensor", ctypes.c_double * 18),
        ("dielectric", ctypes.c_double * 3),
        ("e_field_local", ctypes.c_double * 3),
        ("wave_vector", ctypes.c_double * 3),
    ]


class CrystalOutputData(ctypes.Structure):
    _fields_ = [
        ("n_prime", ctypes.c_double * 3),
        ("rotation_matrix", ctypes.c_double * 9),
        ("sensitivity", ctypes.c_double),
    ]


@dataclass(frozen=True)
class Linbo3Profile:
    wavelength_nm: float = 633.0
    length_mm: float = 20.0
    thickness_mm: float = 1.0
    nx: float = 2.286
    ny: float = 2.286
    nz: float = 2.2
    r13_pm_per_v: float = 9.6
    r22_pm_per_v: float = -6.8
    r33_pm_per_v: float = 30.9
    r51_pm_per_v: float = 32.6


@dataclass(frozen=True)
class PowerModel:
    dark_power_uw: float = 0.2
    power_scale_uw: float = 2300.0
    leakage: float = 0.49
    visibility: float = 0.51
    phase_offset_rad: float = 0.0
    noise_amplitude_uw: float = 1.5


def load_calculator() -> ctypes.CDLL:
    if not DLL_PATH.exists():
        raise FileNotFoundError(f"DLL not found: {DLL_PATH}")

    if hasattr(os, "add_dll_directory"):
        os.add_dll_directory(str(DLL_DIR))

    dll = ctypes.CDLL(str(DLL_PATH))
    func = dll.CalculateCrystalState
    func.argtypes = [ctypes.POINTER(SimInputData), ctypes.POINTER(CrystalOutputData)]
    func.restype = None
    return dll


def build_linbo3_probe_input(profile: Linbo3Profile) -> SimInputData:
    pm_to_m = 1e-12
    data = SimInputData()
    data.static_n[:] = (profile.nx, profile.ny, profile.nz)
    data.r_tensor[:] = (
        0.0, 0.0, profile.r13_pm_per_v * pm_to_m,
        0.0, profile.r22_pm_per_v * pm_to_m, 0.0,
        0.0, 0.0, profile.r33_pm_per_v * pm_to_m,
        0.0, 0.0, 0.0,
        profile.r51_pm_per_v * pm_to_m, 0.0, 0.0,
        0.0, 0.0, 0.0,
    )
    data.dielectric[:] = (0.0, 0.0, 0.0)

    # Scene3CrystalBridge default: fieldAxis=Z, worldLightDirection=Y,
    # modulationMode=Transverse.
    data.e_field_local[:] = (0.0, 0.0, 1.0)
    data.wave_vector[:] = (0.0, 1.0, 0.0)
    return data


def build_linbo3_voltage_input(profile: Linbo3Profile, voltage_v: float) -> SimInputData:
    data = build_linbo3_probe_input(profile)
    e_z_v_per_m = voltage_v / (profile.thickness_mm * 1e-3)
    data.e_field_local[:] = (0.0, 0.0, e_z_v_per_m)
    return data


def calculate_scene3_vpi(profile: Linbo3Profile) -> tuple[float, float]:
    dll = load_calculator()
    sim_input = build_linbo3_probe_input(profile)
    output = CrystalOutputData()
    dll.CalculateCrystalState(ctypes.byref(sim_input), ctypes.byref(output))

    sensitivity = abs(output.sensitivity)
    wavelength_m = profile.wavelength_nm * 1e-9
    length_m = profile.length_mm * 1e-3
    thickness_m = profile.thickness_mm * 1e-3
    vpi = wavelength_m * thickness_m / (2.0 * length_m * sensitivity)
    return vpi, output.sensitivity


def calculate_retardance_rad(dll: ctypes.CDLL, profile: Linbo3Profile, voltage_v: float) -> tuple[float, float, float]:
    sim_input = build_linbo3_voltage_input(profile, voltage_v)
    output = CrystalOutputData()
    dll.CalculateCrystalState(ctypes.byref(sim_input), ctypes.byref(output))

    # Scene3 geometry is k=Y and E=Z, so the two transverse eigen-indices are
    # approximately the x and z principal indices returned by the DLL.
    nx_prime = output.n_prime[0]
    nz_prime = output.n_prime[2]
    wavelength_m = profile.wavelength_nm * 1e-9
    length_m = profile.length_mm * 1e-3
    retardance = 2.0 * math.pi * length_m * (nz_prime - nx_prime) / wavelength_m
    return retardance, nx_prime, nz_prime


def calculate_actual_raw_phase(profile: Linbo3Profile) -> tuple[float, float]:
    dll = load_calculator()
    zero_retardance, _, _ = calculate_retardance_rad(dll, profile, 0.0)

    # The raw simulated power is sin^2(retardance / 2). For this geometry the
    # DLL retardance decreases with voltage, while the phase-aware fit uses a
    # positive voltage slope, so the equivalent fitted phase is -delta0/2 mod pi.
    equivalent_phase = (-0.5 * zero_retardance) % math.pi
    return equivalent_phase, zero_retardance


def transmission(voltage: float, vpi: float, model: PowerModel) -> float:
    phase = model.phase_offset_rad + math.pi * voltage / (2.0 * vpi)
    return max(0.0, min(1.0, model.leakage + model.visibility * math.sin(phase) ** 2))


def stable_power_uw(voltage: float, vpi: float, model: PowerModel) -> float:
    return model.dark_power_uw + model.power_scale_uw * transmission(voltage, vpi, model)


def stable_power_from_transmission_uw(transmission_value: float, model: PowerModel) -> float:
    return model.dark_power_uw + model.power_scale_uw * max(0.0, min(1.0, transmission_value))


def noisy_rounded_power(stable_uw: float, rng: random.Random, noise_amplitude_uw: float) -> tuple[float, float, float]:
    noise_uw = rng.uniform(-noise_amplitude_uw, noise_amplitude_uw)
    measured_uw = max(0.0, stable_uw + noise_uw)
    recorded_mw = round(measured_uw / 1000.0, 2)
    return measured_uw, recorded_mw, recorded_mw * 1000.0


def generate_rows(
    voltages: list[float],
    vpi: float,
    model: PowerModel,
    seed: int,
    profile: Linbo3Profile,
) -> list[dict[str, float]]:
    rng = random.Random(seed)
    dll = load_calculator()
    zero_retardance, _, _ = calculate_retardance_rad(dll, profile, 0.0)

    rows: list[dict[str, float]] = []
    for voltage in voltages:
        fallback_stable_uw = stable_power_uw(voltage, vpi, model)
        fallback_measured_uw, fallback_recorded_mw, fallback_recorded_uw = noisy_rounded_power(
            fallback_stable_uw,
            rng,
            model.noise_amplitude_uw,
        )

        retardance, nx_prime, nz_prime = calculate_retardance_rad(dll, profile, voltage)
        raw_transmission = model.leakage + model.visibility * math.sin(retardance * 0.5) ** 2
        compensated_transmission = (
            model.leakage
            + model.visibility * math.sin((retardance - zero_retardance) * 0.5) ** 2
        )

        sim_raw_stable_uw = stable_power_from_transmission_uw(raw_transmission, model)
        sim_comp_stable_uw = stable_power_from_transmission_uw(compensated_transmission, model)
        sim_raw_measured_uw, sim_raw_recorded_mw, sim_raw_recorded_uw = noisy_rounded_power(
            sim_raw_stable_uw,
            rng,
            model.noise_amplitude_uw,
        )
        sim_comp_measured_uw, sim_comp_recorded_mw, sim_comp_recorded_uw = noisy_rounded_power(
            sim_comp_stable_uw,
            rng,
            model.noise_amplitude_uw,
        )

        rows.append(
            {
                "voltage_v": voltage,
                "dll_nx_prime": nx_prime,
                "dll_nz_prime": nz_prime,
                "dll_retardance_rad": retardance,
                "fallback_stable_power_uw": fallback_stable_uw,
                "fallback_measured_power_uw": fallback_measured_uw,
                "fallback_recorded_power_mw": fallback_recorded_mw,
                "fallback_recorded_power_uw": fallback_recorded_uw,
                "sim_raw_stable_power_uw": sim_raw_stable_uw,
                "sim_raw_measured_power_uw": sim_raw_measured_uw,
                "sim_raw_recorded_power_mw": sim_raw_recorded_mw,
                "sim_raw_recorded_power_uw": sim_raw_recorded_uw,
                "sim_comp_stable_power_uw": sim_comp_stable_uw,
                "sim_comp_measured_power_uw": sim_comp_measured_uw,
                "sim_comp_recorded_power_mw": sim_comp_recorded_mw,
                "sim_comp_recorded_power_uw": sim_comp_recorded_uw,
            }
        )
    return rows


def fit_vpi_from_power(
    rows: list[dict[str, float]],
    power_key: str,
    min_vpi: float,
    max_vpi: float,
    step_v: float,
) -> tuple[float, float, float, float]:
    best_vpi = float("nan")
    best_offset = float("nan")
    best_amplitude = float("nan")
    best_rmse = float("inf")

    candidate = min_vpi
    while candidate <= max_vpi + step_v * 0.5:
        x_values = [math.sin(math.pi * row["voltage_v"] / (2.0 * candidate)) ** 2 for row in rows]
        y_values = [row[power_key] for row in rows]
        offset, amplitude, rmse = fit_offset_amplitude(x_values, y_values)
        if amplitude >= 0.0 and rmse < best_rmse:
            best_vpi = candidate
            best_offset = offset
            best_amplitude = amplitude
            best_rmse = rmse
        candidate += step_v

    return best_vpi, best_offset, best_amplitude, best_rmse


def fit_vpi_with_phase_from_power(
    rows: list[dict[str, float]],
    power_key: str,
    min_vpi: float,
    max_vpi: float,
    step_v: float,
) -> tuple[float, float, float, float, float]:
    best_vpi = float("nan")
    best_baseline = float("nan")
    best_amplitude = float("nan")
    best_phase = float("nan")
    best_rmse = float("inf")

    candidate = min_vpi
    while candidate <= max_vpi + step_v * 0.5:
        rows_x: list[tuple[float, float, float]] = []
        y_values: list[float] = []
        for row in rows:
            x = math.pi * row["voltage_v"] / (2.0 * candidate)
            rows_x.append((1.0, math.cos(2.0 * x), math.sin(2.0 * x)))
            y_values.append(row[power_key])

        coeffs = solve_normal_3x3(rows_x, y_values)
        if coeffs is not None:
            b0, b1, b2 = coeffs
            rmse = math.sqrt(
                sum(
                    (b0 + b1 * cols[1] + b2 * cols[2] - y) ** 2
                    for cols, y in zip(rows_x, y_values)
                )
                / len(y_values)
            )
            if rmse < best_rmse:
                amplitude = 2.0 * math.sqrt(b1 * b1 + b2 * b2)
                baseline = b0 - amplitude * 0.5
                phase = 0.5 * math.atan2(b2, -b1)
                best_vpi = candidate
                best_baseline = baseline
                best_amplitude = amplitude
                best_phase = phase
                best_rmse = rmse
        candidate += step_v

    return best_vpi, best_baseline, best_amplitude, best_phase, best_rmse


def solve_normal_3x3(
    x_rows: list[tuple[float, float, float]],
    y_values: list[float],
) -> tuple[float, float, float] | None:
    matrix = [[0.0 for _ in range(3)] for _ in range(3)]
    vector = [0.0 for _ in range(3)]
    for cols, y in zip(x_rows, y_values):
        for row in range(3):
            vector[row] += cols[row] * y
            for col in range(3):
                matrix[row][col] += cols[row] * cols[col]

    return solve_3x3(matrix, vector)


def solve_3x3(matrix: list[list[float]], vector: list[float]) -> tuple[float, float, float] | None:
    a = [row[:] + [rhs] for row, rhs in zip(matrix, vector)]
    for pivot in range(3):
        pivot_row = max(range(pivot, 3), key=lambda idx: abs(a[idx][pivot]))
        if abs(a[pivot_row][pivot]) < 1e-12:
            return None
        if pivot_row != pivot:
            a[pivot], a[pivot_row] = a[pivot_row], a[pivot]

        pivot_value = a[pivot][pivot]
        for col in range(pivot, 4):
            a[pivot][col] /= pivot_value

        for row in range(3):
            if row == pivot:
                continue
            factor = a[row][pivot]
            for col in range(pivot, 4):
                a[row][col] -= factor * a[pivot][col]

    return a[0][3], a[1][3], a[2][3]


def fit_offset_amplitude(x_values: list[float], y_values: list[float]) -> tuple[float, float, float]:
    n = len(x_values)
    sum_x = sum(x_values)
    sum_y = sum(y_values)
    sum_xx = sum(x * x for x in x_values)
    sum_xy = sum(x * y for x, y in zip(x_values, y_values))
    denom = n * sum_xx - sum_x * sum_x
    if abs(denom) < 1e-12:
        return 0.0, 0.0, float("inf")

    amplitude = (n * sum_xy - sum_x * sum_y) / denom
    offset = (sum_y - amplitude * sum_x) / n
    rmse = math.sqrt(
        sum((offset + amplitude * x - y) ** 2 for x, y in zip(x_values, y_values)) / n
    )
    return offset, amplitude, rmse


def write_csv(rows: list[dict[str, float]], path: Path) -> None:
    with path.open("w", encoding="utf-8", newline="") as file:
        writer = csv.DictWriter(file, fieldnames=list(rows[0].keys()))
        writer.writeheader()
        writer.writerows(rows)


def write_markdown(
    path: Path,
    true_vpi: float,
    sensitivity: float,
    raw_fit: tuple[float, float, float, float],
    recorded_fit: tuple[float, float, float, float],
    sim_comp_fit: tuple[float, float, float, float],
    sim_raw_zero_phase_fit: tuple[float, float, float, float],
    sim_raw_phase_fit: tuple[float, float, float, float, float],
    actual_raw_phase: float,
    zero_retardance: float,
    rows: list[dict[str, float]],
) -> None:
    raw_vpi, raw_offset, raw_amp, raw_rmse = raw_fit
    recorded_vpi, recorded_offset, recorded_amp, recorded_rmse = recorded_fit
    sim_comp_vpi, sim_comp_offset, sim_comp_amp, sim_comp_rmse = sim_comp_fit
    sim_raw_zero_vpi, sim_raw_zero_offset, sim_raw_zero_amp, sim_raw_zero_rmse = sim_raw_zero_phase_fit
    sim_raw_phase_vpi, sim_raw_phase_base, sim_raw_phase_amp, sim_raw_phase_rad, sim_raw_phase_rmse = sim_raw_phase_fit

    lines = [
        "# LiNbO3 Power Readout Vpi Fit",
        "",
        "This experiment compares the current Scene3 fallback power-readout model with DLL-driven simulated optical power.",
        "",
        "## DLL result",
        "",
        f"- Sensitivity: `{sensitivity:.12e}`",
        f"- Vpi: `{true_vpi:.6f} V`",
        "",
        "## Fit result",
        "",
        "| Data source | Fit model | Fitted Vpi (V) | Error (V) | Offset/base (uW) | Amplitude (uW) | Phase (rad) | RMSE (uW) |",
        "| --- | --- | ---: | ---: | ---: | ---: | ---: | ---: |",
        f"| Scene3 formula noisy raw uW | zero phase | {raw_vpi:.3f} | {raw_vpi - true_vpi:+.3f} | {raw_offset:.3f} | {raw_amp:.3f} | 0.000 | {raw_rmse:.3f} |",
        f"| Scene3 formula recorded 0.01 mW | zero phase | {recorded_vpi:.3f} | {recorded_vpi - true_vpi:+.3f} | {recorded_offset:.3f} | {recorded_amp:.3f} | 0.000 | {recorded_rmse:.3f} |",
        f"| DLL simulated, 0V phase compensated | zero phase | {sim_comp_vpi:.3f} | {sim_comp_vpi - true_vpi:+.3f} | {sim_comp_offset:.3f} | {sim_comp_amp:.3f} | 0.000 | {sim_comp_rmse:.3f} |",
        f"| DLL simulated, raw static phase | zero phase | {sim_raw_zero_vpi:.3f} | {sim_raw_zero_vpi - true_vpi:+.3f} | {sim_raw_zero_offset:.3f} | {sim_raw_zero_amp:.3f} | 0.000 | {sim_raw_zero_rmse:.3f} |",
        f"| DLL simulated, raw static phase | fitted phase | {sim_raw_phase_vpi:.3f} | {sim_raw_phase_vpi - true_vpi:+.3f} | {sim_raw_phase_base:.3f} | {sim_raw_phase_amp:.3f} | {sim_raw_phase_rad:.3f} | {sim_raw_phase_rmse:.3f} |",
        "",
        "## Raw phase check",
        "",
        f"- DLL zero-voltage retardance: `{zero_retardance:.12f} rad`",
        f"- Actual equivalent fitted phase: `{actual_raw_phase:.12f} rad`",
        f"- Phase-aware fit phase: `{sim_raw_phase_rad:.12f} rad`",
        f"- Phase error: `{sim_raw_phase_rad - actual_raw_phase:+.12f} rad`",
        "",
        "## Generated table",
        "",
        "| Voltage (V) | Scene3 recorded (mW) | Sim compensated recorded (mW) | Sim raw recorded (mW) |",
        "| ---: | ---: | ---: | ---: |",
    ]
    for row in rows:
        lines.append(
            f"| {row['voltage_v']:.1f} | "
            f"{row['fallback_recorded_power_mw']:.2f} | "
            f"{row['sim_comp_recorded_power_mw']:.2f} | "
            f"{row['sim_raw_recorded_power_mw']:.2f} |"
        )
    lines.extend(
        [
            "",
            "Zero-phase model:",
            "",
            "```text",
            "P(U) = offset + amplitude * sin^2(pi * U / (2 * Vpi))",
            "```",
            "",
            "Phase-aware model:",
            "",
            "```text",
            "P(U) = baseline + amplitude * sin^2(phase + pi * U / (2 * Vpi))",
            "```",
        ]
    )
    path.write_text("\n".join(lines) + "\n", encoding="utf-8")


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description="Call CrystalPhysicsCore.dll, generate Scene3-like LiNbO3 power data, and fit Vpi back from it."
    )
    parser.add_argument("--max-voltage", type=float, default=300.0)
    parser.add_argument("--voltage-step", type=float, default=20.0)
    parser.add_argument("--noise-uw", type=float, default=1.5)
    parser.add_argument("--seed", type=int, default=20260601)
    parser.add_argument("--fit-min", type=float, default=60.0)
    parser.add_argument("--fit-max", type=float, default=240.0)
    parser.add_argument("--fit-step", type=float, default=0.01)
    return parser.parse_args()


def main() -> None:
    args = parse_args()
    if args.voltage_step <= 0:
        raise ValueError("--voltage-step must be positive")
    if args.fit_step <= 0:
        raise ValueError("--fit-step must be positive")

    profile = Linbo3Profile()
    model = PowerModel(noise_amplitude_uw=args.noise_uw)
    true_vpi, sensitivity = calculate_scene3_vpi(profile)
    actual_raw_phase, zero_retardance = calculate_actual_raw_phase(profile)

    count = int(math.floor(args.max_voltage / args.voltage_step)) + 1
    voltages = [i * args.voltage_step for i in range(count)]
    rows = generate_rows(voltages, true_vpi, model, args.seed, profile)

    raw_fit = fit_vpi_from_power(rows, "fallback_measured_power_uw", args.fit_min, args.fit_max, args.fit_step)
    recorded_fit = fit_vpi_from_power(rows, "fallback_recorded_power_uw", args.fit_min, args.fit_max, args.fit_step)
    sim_comp_fit = fit_vpi_from_power(rows, "sim_comp_recorded_power_uw", args.fit_min, args.fit_max, args.fit_step)
    sim_raw_zero_phase_fit = fit_vpi_from_power(rows, "sim_raw_recorded_power_uw", args.fit_min, args.fit_max, args.fit_step)
    sim_raw_phase_fit = fit_vpi_with_phase_from_power(rows, "sim_raw_recorded_power_uw", args.fit_min, args.fit_max, args.fit_step)

    write_csv(rows, CSV_PATH)
    write_markdown(
        MD_PATH,
        true_vpi,
        sensitivity,
        raw_fit,
        recorded_fit,
        sim_comp_fit,
        sim_raw_zero_phase_fit,
        sim_raw_phase_fit,
        actual_raw_phase,
        zero_retardance,
        rows,
    )

    print(f"DLL sensitivity: {sensitivity:.12e}")
    print(f"DLL Vpi: {true_vpi:.6f} V")
    print(f"Fit from noisy raw uW: {raw_fit[0]:.3f} V, error {raw_fit[0] - true_vpi:+.3f} V")
    print(
        "Fit from Scene3 rounded table: "
        f"{recorded_fit[0]:.3f} V, error {recorded_fit[0] - true_vpi:+.3f} V"
    )
    print(
        "Fit from DLL simulated compensated table: "
        f"{sim_comp_fit[0]:.3f} V, error {sim_comp_fit[0] - true_vpi:+.3f} V"
    )
    print(
        "Fit from DLL simulated raw table (zero-phase assumption): "
        f"{sim_raw_zero_phase_fit[0]:.3f} V, error {sim_raw_zero_phase_fit[0] - true_vpi:+.3f} V"
    )
    print(
        "Fit from DLL simulated raw table (phase-aware): "
        f"{sim_raw_phase_fit[0]:.3f} V, error {sim_raw_phase_fit[0] - true_vpi:+.3f} V"
    )
    print(
        "Raw phase check: "
        f"actual {actual_raw_phase:.6f} rad, fitted {sim_raw_phase_fit[3]:.6f} rad, "
        f"error {sim_raw_phase_fit[3] - actual_raw_phase:+.6f} rad"
    )
    print(f"Wrote {CSV_PATH}")
    print(f"Wrote {MD_PATH}")


if __name__ == "__main__":
    main()
