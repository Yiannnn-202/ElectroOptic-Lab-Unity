from __future__ import annotations

import argparse
import csv
import ctypes
import math
import os
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
DLL_DIR = ROOT / "ElectroOptic-Lab" / "Assets" / "Plugins" / "x86_64"
DLL_PATH = DLL_DIR / "CrystalPhysicsCore.dll"
CSV_PATH = Path(__file__).resolve().with_name("kdp_eo_response.csv")
MD_PATH = Path(__file__).resolve().with_name("kdp_eo_response.md")


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


def build_kdp_input(ez_v_per_m: float) -> SimInputData:
    pm_per_v_to_m_per_v = 1e-12
    r_values_pm_per_v = [
        0.0, 0.0, 0.0,
        0.0, 0.0, 0.0,
        0.0, 0.0, 0.0,
        8.8, 0.0, 0.0,
        0.0, 0.0, 0.0,
        0.0, 0.0, 10.3,
    ]

    data = SimInputData()
    data.static_n[:] = (1.511607, 1.511607, 1.469857)
    data.r_tensor[:] = tuple(value * pm_per_v_to_m_per_v for value in r_values_pm_per_v)
    data.dielectric[:] = (0.0, 0.0, 0.0)
    data.e_field_local[:] = (0.0, 0.0, ez_v_per_m)
    data.wave_vector[:] = (0.0, 0.0, 1.0)
    return data


def load_calculator() -> ctypes.CDLL:
    if not DLL_PATH.exists():
        raise FileNotFoundError(f"DLL not found: {DLL_PATH}")

    os.add_dll_directory(str(DLL_DIR))
    dll = ctypes.CDLL(str(DLL_PATH))
    func = dll.CalculateCrystalState
    func.argtypes = [ctypes.POINTER(SimInputData), ctypes.POINTER(CrystalOutputData)]
    func.restype = None
    return dll


def theoretical_kdp_indices(ez_v_per_m: float) -> tuple[float, float, float, float]:
    no = 1.511607
    ne = 1.469857
    r63 = 10.3e-12
    delta_n = 0.5 * no**3 * r63 * ez_v_per_m
    return no - delta_n, no + delta_n, ne, delta_n


def calculate_rows(e_fields: list[float]) -> list[dict[str, float]]:
    dll = load_calculator()
    rows: list[dict[str, float]] = []

    for ez in e_fields:
        sim_input = build_kdp_input(ez)
        output = CrystalOutputData()
        dll.CalculateCrystalState(ctypes.byref(sim_input), ctypes.byref(output))
        theory_x, theory_y, theory_z, delta_n = theoretical_kdp_indices(ez)
        rows.append(
            {
                "E_z_V_per_m": ez,
                "voltage_for_1mm_V": ez * 1e-3,
                "dll_nx_prime": output.n_prime[0],
                "dll_ny_prime": output.n_prime[1],
                "dll_nz_prime": output.n_prime[2],
                "theory_nx_prime": theory_x,
                "theory_ny_prime": theory_y,
                "theory_nz_prime": theory_z,
                "delta_n_theory": delta_n,
                "dll_delta_n_yx": output.n_prime[1] - output.n_prime[0],
                "sensitivity": output.sensitivity,
                "rotation_m00": output.rotation_matrix[0],
                "rotation_m01": output.rotation_matrix[1],
                "rotation_m10": output.rotation_matrix[3],
                "rotation_m11": output.rotation_matrix[4],
            }
        )

    return rows


def write_csv(rows: list[dict[str, float]], path: Path) -> None:
    with path.open("w", encoding="utf-8", newline="") as file:
        writer = csv.DictWriter(file, fieldnames=list(rows[0].keys()))
        writer.writeheader()
        writer.writerows(rows)


def write_markdown(rows: list[dict[str, float]], path: Path) -> None:
    lines = [
        "# KDP 电光响应 DLL 实验数据",
        "",
        "实验条件：KDP，$n_o=1.511607$，$n_e=1.469857$，$r_{63}=10.3\\ \\mathrm{pm/V}$，外加电场沿晶体 $z$ 轴，波矢取 $(0,0,1)$。",
        "",
        "| $E_z$ (V/m) | 等效 1 mm 电压 (V) | DLL $n'_x$ | DLL $n'_y$ | DLL $n'_z$ | 理论 $n'_{x}$ | 理论 $n'_{y}$ | $n'_y-n'_x$ |",
        "| ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |",
    ]

    for row in rows:
        lines.append(
            "| "
            f"{row['E_z_V_per_m']:.0f} | "
            f"{row['voltage_for_1mm_V']:.0f} | "
            f"{row['dll_nx_prime']:.9f} | "
            f"{row['dll_ny_prime']:.9f} | "
            f"{row['dll_nz_prime']:.9f} | "
            f"{row['theory_nx_prime']:.9f} | "
            f"{row['theory_ny_prime']:.9f} | "
            f"{row['dll_delta_n_yx']:.9e} |"
        )

    lines.extend(
        [
            "",
            "注：理论值采用一阶近似",
            "",
            "$$",
            "n_{x'}\\approx n_o-\\frac{1}{2}n_o^3r_{63}E",
            "$$",
            "",
            "$$",
            "n_{y'}\\approx n_o+\\frac{1}{2}n_o^3r_{63}E",
            "$$",
            "",
            "CSV 原始数据见 `kdp_eo_response.csv`。",
        ]
    )
    path.write_text("\n".join(lines) + "\n", encoding="utf-8")


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Generate KDP electro-optic response data with CrystalPhysicsCore.dll.")
    parser.add_argument(
        "--fields",
        nargs="*",
        type=float,
        default=[0.0, 2e5, 5e5, 1e6, 2e6, 5e6, 1e7],
        help="Electric fields Ez in V/m.",
    )
    return parser.parse_args()


def main() -> None:
    args = parse_args()
    rows = calculate_rows(args.fields)
    write_csv(rows, CSV_PATH)
    write_markdown(rows, MD_PATH)
    print(f"Wrote {CSV_PATH}")
    print(f"Wrote {MD_PATH}")


if __name__ == "__main__":
    main()
