namespace BoLike.Sim
{
    /// <summary>
    /// Visual effects the simulation asks for, in Phaser pixel space (x right, y down, z up).
    /// The game layer draws them; tests can pass a no-op.
    /// </summary>
    public interface IFx
    {
        void Number(float x, float y, string text, int color, bool big = false);
        void Spark(float x, float y, int color = 0xffffff, float size = 1f);
        void Slash(float x, float y, int facing, int color, float radius, float depthY);
        void Ring(float x, float y, float radius, int color, float depthY);
        void Pillar(float x, float y, int color);
        void Dust(float x, float y, int n = 5, float size = 1f);
        void Ghost(Fighter f);
        void Flash(float alpha = 0.8f, float ms = 260f);
        void Aura(float x, float y, int color);
        void Banner(string text, int color = 0xffffff, int size = 64);
        void Shake(float ms, float intensity);
    }

    public interface ISfx
    {
        void Play(string name, float volume = 1f);
    }

    public class NullFx : IFx
    {
        public void Number(float x, float y, string text, int color, bool big = false) { }
        public void Spark(float x, float y, int color = 0xffffff, float size = 1f) { }
        public void Slash(float x, float y, int facing, int color, float radius, float depthY) { }
        public void Ring(float x, float y, float radius, int color, float depthY) { }
        public void Pillar(float x, float y, int color) { }
        public void Dust(float x, float y, int n = 5, float size = 1f) { }
        public void Ghost(Fighter f) { }
        public void Flash(float alpha = 0.8f, float ms = 260f) { }
        public void Aura(float x, float y, int color) { }
        public void Banner(string text, int color = 0xffffff, int size = 64) { }
        public void Shake(float ms, float intensity) { }
    }

    public class NullSfx : ISfx
    {
        public void Play(string name, float volume = 1f) { }
    }
}
