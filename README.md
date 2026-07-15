# QuestApp-MainStudy

Unity **main-study** Fitts app for Meta Quest (forked from [QuestApp-PracticeTask](../QuestApp-PracticeTask)). **PC-commanded**: Quest stays **IDLE** until OpenEye sends a condition; after save it returns to IDLE so you control rest.

Practice APK (`com.PracticeMG.MRstressPRACTICE`) and this APK (`com.PracticeMG.MRstress`) can be installed side-by-side.

## Android package

| Item | Value |
|------|--------|
| Package | `com.PracticeMG.MRstress` |
| Product name | `MRstress` |
| Unity | 2022.2.9f1 |

## Study design

| Parameter | Value |
|-----------|--------|
| Conditions | `EyeDwell`, `EyePinch`, `HeadDwell`, `HeadPinch` (PC chooses order) |
| Tasks | **Fitts only** (no Menu) |
| Reps per PC start | Configurable (default **3**) |
| Duration per rep | Configurable (default **5 min**); Fitts ring loops until time is up |
| Fitts ring | 11 targets, step ≈5 |
| Dwell to select | 1.0 s |
| Per-target timeout | 5.0 s |
| Trial log rate | 100 Hz |

## Session flow (PC commander)

1. Quest boots → **IDLE** (waiting for PC).
2. OpenEye **Main Study commander**: set `sub` / `subsub` / `condition` / `reps` → **Start condition**.
3. Quest → **BEFORE_TRIAL** → participant starts Fitts → saves JSON after each rep.
4. After all reps for that command → Quest → **IDLE** and sends `mainStudyDone`.
5. Rest as long as you want; start the next condition from the PC (or retry after a Neon reconnect).

### TCP messages

PC → Quest:

```json
{"type":"mainStudyStart","payload":{"sub_num":0,"subsub_num":0,"condition":"EyeDwell","reps":3,"duration_sec":300}}
```

Quest → PC:

```json
{"type":"mainStudyDone","payload":{"ok":true,"sub_num":0,"subsub_num":0,"condition":"EyeDwell","reps":3}}
```

### Clock sync

Neon-style **time-echo** (PC↔Quest round-trip, NTP midpoint). OpenEye GUI: **Start Quest↔PC time-echo** → `tXX/sync.json` with `offset_quest_to_pc_ns` (and `offset_phone_to_pc_ns` when Neon is connected). Quest replies to TCP `timeEcho`; no one-way syncPulse PREP.

## OpenEye integration

1. Start TCP on OpenEye; **Start Gaze Tracking** (+ Visualize) for eye conditions.
2. Launch MainStudy (`Start Main Study` → `com.PracticeMG.MRstress`), or open the APK manually.
3. **Start Quest↔PC time-echo** while MainStudy owns TCP.
4. **Main Study commander**: set `sub` / `subsub` / `condition` / `reps` / **each** (minutes, default 5) → **Start condition**.
5. When Quest finishes → `mainStudyDone` → IDLE; rest, then start the next condition.
6. Optional: **Start Practice** launches `com.PracticeMG.MRstressPRACTICE` (auto runner, no commander).

## Trial logging

Path on Quest:

```
/storage/emulated/0/Android/data/com.PracticeMG.MRstress/files/<sub_num>-<subsub_num>/
```

JSON envelope includes:

| Field | Example |
|-------|---------|
| `study_phase` | `"main"` |
| `study_type` | `"Fitts"` |
| `total_rep` | `3` |
| `condition` | `"EyeDwell"` |
| `repetition` | `0`–`N-1` |
| `trial_duration_sec` | `300` |
| `log_sample_rate_hz` | `100` |

## Build

1. Open **this** project in Unity 2022.2.9f1 (not PracticeTask).
2. Scene: `Assets/Scenes/PracticeScenes.unity`.
3. **File → Build Settings → Android** → build APK.
4. Restart OpenEye GUI after pull so the new commander UI / send helper loads.

## Related

| Path / repo | Role |
|-------------|------|
| [QuestApp-PracticeTask](../QuestApp-PracticeTask) | Auto practice (no PC commander) |
| [OpenEye](https://github.com/thommakoon/OpenEye) | Gaze map + Main Study commander |
| [gaze-gait-process](https://github.com/thommakoon/gaze-gait-process) | Parent monorepo |
