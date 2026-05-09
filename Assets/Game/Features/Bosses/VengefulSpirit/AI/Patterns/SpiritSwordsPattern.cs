using System;
using System.Collections;
using Game.Features.Bosses.VengefulSpirit.SpectralSwords;
using Game.Features.Bosses.VengefulSpirit.Teleport;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Game.Features.Bosses.VengefulSpirit.AI.Patterns {
    /// <summary>
    /// "Boss teleports to a higher-level casting point. Starts casting magic. Spirit swords
    /// appear in the air at predefined anchors. After casting is complete, set isCasting =
    /// false. Boss stays for 1 second. Then boss disappears and spawns in another location."
    ///
    /// Phase 1: pick one entry randomly.
    /// Phase 2: fire from every entry in array order, separated by <see cref="waveSpacing"/>.
    ///
    /// Each entry pairs a sword spawn anchor (where swords appear) with a separate boss
    /// teleport anchor (where the boss stands during the cast). Both are direct refs in
    /// inspector — no name-based lookup. The boss casting anchor is typically on the
    /// OPPOSITE side from the sword anchor for visual separation.
    /// </summary>
    public class SpiritSwordsPattern : VengefulSpiritPattern {
        [Serializable]
        public struct CastEntry {
            [Tooltip("Where swords appear in the air.")]
            public SpectralSwordSpawnAnchor swordAnchor;

            [Tooltip("Where the boss stands to cast this wave. Typically on the OPPOSITE side from the sword anchor.")]
            public TeleportAnchor bossPosition;
        }

        [Header("Spirit Swords")]
        [Tooltip("Cast pairings. Phase 1 picks one entry randomly; phase 2 fires from all entries in array order.")]
        [SerializeField]
        private CastEntry[] castEntries;

        [Tooltip("Idle hold after each sword cast completes, before the boss disappears.")]
        [SerializeField]
        private float postCastIdle = 1f;

        [Tooltip("Spacing between successive sword waves in phase 2.")]
        [SerializeField]
        private float waveSpacing = 0.5f;

        [Tooltip("Anchors the boss may relocate to after the sequence ends. Should not " +
                 "include the boss casting positions used by this pattern.")]
        [SerializeField]
        private TeleportAnchor[] postCastRelocationAnchors;

        public override bool CanRun(VengefulSpiritPatternContext ctx) {
            return HasUsableEntry();
        }

        public override IEnumerator Run(VengefulSpiritPatternContext ctx) {
            CastEntry[] toFire = SelectEntries(ctx.Phase);
            for (int i = 0; i < toFire.Length; i++) {
                CastEntry entry = toFire[i];
                if (entry.swordAnchor == null) {
                    continue;
                }

                if (entry.bossPosition != null) {
                    ctx.Boss.RequestTeleport(entry.bossPosition);
                    yield return ctx.WaitForBusyCycle();
                    if (ctx.BossDead) {
                        yield break;
                    }
                }

                ctx.Boss.RequestSwordCast(entry.swordAnchor);
                yield return ctx.WaitForBusyCycle();
                if (ctx.BossDead) {
                    yield break;
                }

                yield return WaitSeconds(postCastIdle, ctx);
                if (ctx.BossDead) {
                    yield break;
                }

                if (i < toFire.Length - 1) {
                    yield return WaitSeconds(waveSpacing, ctx);
                    if (ctx.BossDead) {
                        yield break;
                    }
                }
            }

            // Spec: "Then boss disappears and spawns in another location."
            TeleportAnchor away = PickRelocationAnchor(ctx);
            if (away != null) {
                ctx.Boss.RequestTeleport(away);
                yield return ctx.WaitForBusyCycle();
            }
        }

        private bool HasUsableEntry() {
            if (castEntries == null) {
                return false;
            }
            for (int i = 0; i < castEntries.Length; i++) {
                if (castEntries[i].swordAnchor != null) {
                    return true;
                }
            }
            return false;
        }

        private CastEntry[] SelectEntries(VengefulSpiritPhase phase) {
            if (castEntries == null || castEntries.Length == 0) {
                return Array.Empty<CastEntry>();
            }
            if (phase == VengefulSpiritPhase.One) {
                int idx = Random.Range(0, castEntries.Length);
                return new[] { castEntries[idx] };
            }
            return castEntries;
        }

        private TeleportAnchor PickRelocationAnchor(VengefulSpiritPatternContext ctx) {
            if (postCastRelocationAnchors == null || postCastRelocationAnchors.Length == 0) {
                return null;
            }
            int closestIdx = -1;
            float closestSqr = float.PositiveInfinity;
            int validCount = 0;
            for (int i = 0; i < postCastRelocationAnchors.Length; i++) {
                TeleportAnchor a = postCastRelocationAnchors[i];
                if (a == null) {
                    continue;
                }
                validCount++;
                float d = (a.transform.position - ctx.Boss.transform.position).sqrMagnitude;
                if (d < closestSqr) {
                    closestSqr = d;
                    closestIdx = i;
                }
            }
            if (validCount == 0) {
                return null;
            }
            if (validCount == 1) {
                return postCastRelocationAnchors[closestIdx];
            }
            int target = Random.Range(0, validCount - 1);
            int seen = 0;
            for (int i = 0; i < postCastRelocationAnchors.Length; i++) {
                TeleportAnchor a = postCastRelocationAnchors[i];
                if (a == null || i == closestIdx) {
                    continue;
                }
                if (seen == target) {
                    return a;
                }
                seen++;
            }
            return null;
        }

        private static IEnumerator WaitSeconds(float seconds, VengefulSpiritPatternContext ctx) {
            float t = 0f;
            while (t < seconds) {
                if (ctx.BossDead) {
                    yield break;
                }
                t += Time.deltaTime;
                yield return null;
            }
        }
    }
}
