# LiNbO3 Power Readout Vpi Fit

This experiment compares the current Scene3 fallback power-readout model with DLL-driven simulated optical power.

## DLL result

- Sensitivity: `1.071702726563e-10`
- Vpi: `147.662216 V`

## Fit result

| Data source | Fit model | Fitted Vpi (V) | Error (V) | Offset/base (uW) | Amplitude (uW) | Phase (rad) | RMSE (uW) |
| --- | --- | ---: | ---: | ---: | ---: | ---: | ---: |
| Scene3 formula noisy raw uW | zero phase | 147.690 | +0.028 | 1126.975 | 1172.923 | 0.000 | 0.805 |
| Scene3 formula recorded 0.01 mW | zero phase | 147.740 | +0.078 | 1128.455 | 1169.781 | 0.000 | 2.112 |
| DLL simulated, 0V phase compensated | zero phase | 147.740 | +0.078 | 1128.455 | 1169.781 | 0.000 | 2.112 |
| DLL simulated, raw static phase | zero phase | 114.560 | -33.102 | 1347.009 | 820.967 | 0.000 | 282.861 |
| DLL simulated, raw static phase | fitted phase | 147.610 | -0.052 | 1126.677 | 1173.311 | 0.688 | 2.319 |

## Raw phase check

- DLL zero-voltage retardance: `-17072.794199603271 rad`
- Actual equivalent fitted phase: `0.689859998168 rad`
- Phase-aware fit phase: `0.688358602571 rad`
- Phase error: `-0.001501395597 rad`

## Generated table

| Voltage (V) | Scene3 recorded (mW) | Sim compensated recorded (mW) | Sim raw recorded (mW) |
| ---: | ---: | ---: | ---: |
| 0.0 | 1.13 | 1.13 | 1.60 |
| 20.0 | 1.18 | 1.18 | 1.85 |
| 40.0 | 1.33 | 1.33 | 2.07 |
| 60.0 | 1.54 | 1.54 | 2.23 |
| 80.0 | 1.79 | 1.79 | 2.30 |
| 100.0 | 2.02 | 2.02 | 2.26 |
| 120.0 | 2.20 | 2.20 | 2.13 |
| 140.0 | 2.29 | 2.29 | 1.92 |
| 160.0 | 2.28 | 2.28 | 1.67 |
| 180.0 | 2.17 | 2.17 | 1.43 |
| 200.0 | 1.97 | 1.97 | 1.25 |
| 220.0 | 1.73 | 1.73 | 1.14 |
| 240.0 | 1.49 | 1.49 | 1.14 |
| 260.0 | 1.29 | 1.29 | 1.24 |
| 280.0 | 1.16 | 1.16 | 1.42 |
| 300.0 | 1.13 | 1.13 | 1.66 |

Zero-phase model:

```text
P(U) = offset + amplitude * sin^2(pi * U / (2 * Vpi))
```

Phase-aware model:

```text
P(U) = baseline + amplitude * sin^2(phase + pi * U / (2 * Vpi))
```
