using System.Collections.Generic;
using BoLike.Data;
using NUnit.Framework;
using UnityEngine;

namespace BoLike.Tests
{
    /// <summary>Every frame anims.json promises must exist, and all frames of one class must share a canvas size.</summary>
    public class AnimAssetsTests
    {
        [Test]
        public void EveryClassHasAllAnimFramesAtOneCanvasSize()
        {
            var missing = new List<string>();
            var sizes = new List<string>();
            foreach (var cls in GameDb.Classes)
            {
                var a = GameDb.Anim(cls.id);
                Assert.IsNotNull(a, $"anims.json has no entry for {cls.id}");
                Vector2? canvas = null;
                foreach (var (label, n) in new[] { ("idle", a.idle), ("walk", a.walk), ("attack", a.attack) })
                {
                    Assert.Greater(n, 0, $"{cls.id} {label} frame count");
                    for (int i = 0; i < n; i++)
                    {
                        string key = $"Sprites/anim/{cls.id}_{label}_{i}";
                        var s = Resources.Load<Sprite>(key);
                        if (s == null) { missing.Add(key); continue; }
                        var size = new Vector2(s.texture.width, s.texture.height);
                        canvas ??= size;
                        if (size != canvas.Value) sizes.Add($"{key} is {size} but {cls.id} canvas is {canvas.Value}");
                    }
                }
            }
            Assert.IsEmpty(missing, "missing animation frames:\n" + string.Join("\n", missing));
            Assert.IsEmpty(sizes, "frames with a different canvas size:\n" + string.Join("\n", sizes));
        }
    }
}
