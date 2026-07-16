using UnityEngine;

namespace StudyDesign
{
    /// <summary>
    /// Fitts ring layouts for MainStudy (applied via ControlTargets.ApplyRingLayout).
    ///
    /// Soft set A: 6 distinct IDs spanning ~2.0–3.0 (OpenEye-friendly; skips 3.25/3.5).
    /// Both A (ring radius) and W (target size) still vary — ring is not fixed.
    ///
    /// Geometry:
    ///   depth = 1 m, N = 11 targets, stepNum = 5
    ///   ring_radius_m = tan(ring_radius_deg)
    ///   A = ChordFactor * ring_radius_m
    ///   ID = log2(A / W + 1)   (Shannon)
    ///   width convention: W = 4 * tan(target_half_angle_deg)
    ///
    /// Timed trial: rings are used in a random order each block;
    /// after every ring appears once, the order is reshuffled.
    /// </summary>
    public static class FittsRingPresets
    {
        public const int TargetCount = 11;
        public const int StepNum = 5;
        public const float DepthM = 1f;

        /// <summary>A = ChordFactor * ring_radius_m for stepNum=5, N=11.</summary>
        public static readonly float ChordFactor = 2f * Mathf.Sin(StepNum * Mathf.PI / TargetCount);

        public static readonly FittsRingLayout[] Rings =
        {
            // Soft set for OpenEye dwell/pinch: IDs ~2.0–3.0 (harder rings amplify tracker noise)
            FittsRingLayout.FromAngles(0, "current", ringRadiusDeg: 15f, targetHalfAngleDeg: 2.5f),
            FittsRingLayout.FromDesignId(1, "id_2_2", ringRadiusDeg: 15.5f, designIdBits: 2.2f),
            FittsRingLayout.FromDesignId(2, "id_2_4", ringRadiusDeg: 16f, designIdBits: 2.4f),
            FittsRingLayout.FromDesignId(3, "id_2_6", ringRadiusDeg: 16.5f, designIdBits: 2.6f),
            FittsRingLayout.FromDesignId(4, "id_2_8", ringRadiusDeg: 17f, designIdBits: 2.8f),
            FittsRingLayout.FromDesignId(5, "id_3_0", ringRadiusDeg: 18f, designIdBits: 3.0f),
        };

        public static FittsRingLayout Get(int index)
        {
            if (index < 0 || index >= Rings.Length)
                return Rings[0];
            return Rings[index];
        }
    }

    [System.Serializable]
    public struct FittsRingLayout
    {
        public int index;
        public string name;
        public float ring_radius_deg;
        public float target_half_angle_deg;
        public float design_id_bits;

        public float ring_radius_m;
        public float amplitude_m;
        public float width_m;
        public float index_of_difficulty_bits;

        public static FittsRingLayout FromAngles(int id, string name, float ringRadiusDeg, float targetHalfAngleDeg)
        {
            float r = Mathf.Tan(ringRadiusDeg * Mathf.Deg2Rad);
            float w = 4f * Mathf.Tan(targetHalfAngleDeg * Mathf.Deg2Rad);
            float a = FittsRingPresets.ChordFactor * r;
            return new FittsRingLayout
            {
                index = id,
                name = name,
                ring_radius_deg = ringRadiusDeg,
                target_half_angle_deg = targetHalfAngleDeg,
                design_id_bits = float.NaN,
                ring_radius_m = r,
                amplitude_m = a,
                width_m = w,
                index_of_difficulty_bits = FittsMetrics.IndexOfDifficulty(a, w),
            };
        }

        /// <summary>Pick ring radius; solve W so ID = designIdBits exactly.</summary>
        public static FittsRingLayout FromDesignId(int id, string name, float ringRadiusDeg, float designIdBits)
        {
            float r = Mathf.Tan(ringRadiusDeg * Mathf.Deg2Rad);
            float a = FittsRingPresets.ChordFactor * r;
            float w = a / (Mathf.Pow(2f, designIdBits) - 1f);
            float halfDeg = Mathf.Atan(w / 4f) * Mathf.Rad2Deg;
            return new FittsRingLayout
            {
                index = id,
                name = name,
                ring_radius_deg = ringRadiusDeg,
                target_half_angle_deg = halfDeg,
                design_id_bits = designIdBits,
                ring_radius_m = r,
                amplitude_m = a,
                width_m = w,
                index_of_difficulty_bits = designIdBits,
            };
        }
    }
}
