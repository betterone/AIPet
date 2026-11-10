using System;
using System.Collections.Generic;

namespace Momo.Core
{
    // WorldState remains the version-one per-cat life model for compatibility.
    // HouseholdState is the authoritative shared world and version-three save root.
    [Serializable] public sealed class CatState
    {
        public int id;
        public WorldState life;
        public float radius = .78f;
        public float heading, socialCooldown, blockedSeconds;
        public int appearance = -1;
        public Activity waitingFor = Activity.Idle;
        public double queuedAt;
        public int attentionCatId;
        public float attentionSeconds;
    }
    [Serializable] public sealed class CatRelationship
    {
        public int from, to, meetings, games;
        public float familiarity = .25f, comfort = .5f;
    }
    [Serializable] public sealed class HouseholdState
    {
        public int schema = 3, nextCatId = 1, focusedCatId, toyOwnerId;
        public PlayProgress play = new PlayProgress();
        public long revision, lastUtcTicks;
        public double seconds, remainder;
        public float food = 90, water = 90;
        public Point ball = new Point(1,-1.2f);
        public bool ballTrapped, sound = true;
        public List<CatState> cats = new List<CatState>();
        public List<CatRelationship> relationships = new List<CatRelationship>();
    }
    public enum SocialKind { Sniff, Chase, RestTogether }
    public enum SocialPhase { Invitation, Approach, Together }
    // Transient: never restore a half-completed paired animation after suspension.
    public sealed class InteractionSession
    {
        public int first, second;
        public SocialKind kind;
        public SocialPhase phase;
        public double elapsed, phaseTime;
        public Point firstSlot, secondSlot;
    }
}
