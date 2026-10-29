using BoLike.Sim;
using NUnit.Framework;

namespace BoLike.Tests
{
    public class FighterPhysicsTests
    {
        sealed class Dummy : Fighter
        {
            public Dummy(World w) : base(w, 0, 500, 40, 90) { }
            public override Team Team => Team.Player;
            public override void Update(float dt) { }
            public override bool TakeHit(HitInfo h) => false;
        }

        [Test]
        public void FallingThroughTheAirborneGapStillLands()
        {
            // During hit-stop a fall advances about 1 px per frame, so z can settle in (0, 0.5] with vz <= 0.
            // That used to switch gravity off (not Airborne) without ever reporting a landing.
            var f = new Dummy(new World()) { z = 0.3f, vz = -5f };
            bool landed = false;
            for (int i = 0; i < 60 && !landed; i++) landed = f.Physics(1f / 60f * 0.08f, -1000, 1000);
            Assert.IsTrue(landed, "fighter never landed from z=0.3");
            Assert.AreEqual(0f, f.z);
            Assert.AreEqual(0f, f.vz);
        }

        [Test]
        public void JumpLandsExactlyOnceAndThenStaysGrounded()
        {
            var f = new Dummy(new World()) { z = 1f, vz = 680f };
            int landings = 0;
            for (int i = 0; i < 600; i++) if (f.Physics(1f / 60f, -1000, 1000)) landings++;
            Assert.AreEqual(1, landings);
            Assert.AreEqual(0f, f.z);
        }
    }
}
