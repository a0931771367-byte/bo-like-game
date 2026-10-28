using BoLike.Data;

namespace BoLike.Sim
{
    public enum Team { Player, Enemy }

    public struct Poison
    {
        public float dps;
        public float duration;
    }

    /// <summary>One hit landing on a fighter. Distances px, times ms, dir ±1.</summary>
    public class HitInfo
    {
        public int damage;
        public bool crit;
        public double mult;
        public Element element;
        public int dir;
        public float knock, launch, stun;
        /// <summary>Freeze duration in ms, 0 for none.</summary>
        public float freeze;
        public Poison? poison;
        public Fighter source;
    }
}
