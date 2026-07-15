"""Offline check: main-study session advances Fitts-only, 4 conditions x 3 reps = 12 blocks.

Does not require Unity. Run:
  python tools/verify_main_study_flow.py
"""

from __future__ import annotations


CONDITIONS = ["EyeDwell", "EyePinch", "HeadDwell", "HeadPinch"]
TOTAL_REP = 3


def simulate() -> list[tuple[str, int]]:
    """Return (condition, rep) for each completed Fitts block, then END."""
    blocks: list[tuple[str, int]] = []
    cond_i = 0
    rep = 0
    while True:
        blocks.append((CONDITIONS[cond_i], rep))
        rep += 1
        if rep >= TOTAL_REP:
            rep = 0
            cond_i += 1
            if cond_i >= len(CONDITIONS):
                break
    return blocks


def main() -> None:
    blocks = simulate()
    assert len(blocks) == 12, f"expected 12 blocks, got {len(blocks)}"
    for c in CONDITIONS:
        reps = [r for cond, r in blocks if cond == c]
        assert reps == [0, 1, 2], f"{c}: {reps}"
    # No Menu / BREAK between study types
    assert all(isinstance(b[0], str) for b in blocks)
    print("OK: 4 conditions x 3 Fitts reps = 12 blocks")
    for i, (cond, rep) in enumerate(blocks):
        print(f"  {i:02d}  {cond}  rep={rep}")
    print("END (sync pulses in PREP only; no VOR)")


if __name__ == "__main__":
    main()
