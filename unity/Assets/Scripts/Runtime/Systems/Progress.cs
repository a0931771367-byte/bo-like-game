using System.Collections.Generic;
using BoLike.Data;

namespace BoLike.Systems
{
    /// <summary>Chapter progress derived from the save (S1): what is unlocked, what to offer first, what a boss kill opens.</summary>
    public static class Progress
    {
        /// <summary>A chapter is open when it has no unlock rule or its boss has been killed once (罐子 11/1: no need to replay from chapter 1).</summary>
        public static bool ChapterUnlocked(SaveData save, ChapterDef ch) =>
            ch.unlock == null || string.IsNullOrEmpty(ch.unlock.bossKill) || save.bossFirstKills.Contains(ch.unlock.bossKill);

        public static bool ChapterCleared(SaveData save, ChapterDef ch)
        {
            if (!ch.Playable) return false;
            foreach (var st in ch.stages) if (!save.stagesCleared.Contains(st)) return false;
            return true;
        }

        /// <summary>The chapter whose unlock rule names this boss, if that chapter is playable; null otherwise.</summary>
        public static ChapterDef ChapterUnlockedBy(string bossId)
        {
            if (string.IsNullOrEmpty(bossId)) return null;
            foreach (var ch in GameDb.Chapters)
                if (ch.unlock != null && ch.unlock.bossKill == bossId && ch.Playable) return ch;
            return null;
        }

        /// <summary>Chapters the title screen can start right now, in play order.</summary>
        public static List<ChapterDef> PlayableChapters(SaveData save)
        {
            var list = new List<ChapterDef>();
            foreach (var ch in GameDb.Chapters) if (ch.Playable && ChapterUnlocked(save, ch)) list.Add(ch);
            return list;
        }

        /// <summary>The chapter to preselect: the last one picked if still valid, else the furthest unlocked one.</summary>
        public static ChapterDef DefaultChapter(SaveData save)
        {
            var open = PlayableChapters(save);
            foreach (var ch in open) if (ch.id == save.lastChapterId) return ch;
            return open[open.Count - 1];
        }

        /// <summary>First stage of a chapter not yet cleared, else its last stage (replay for cards).</summary>
        public static RunStageDef NextStage(SaveData save, ChapterDef ch)
        {
            foreach (var st in ch.stages) if (!save.stagesCleared.Contains(st)) return GameDb.RunStage(st);
            return GameDb.RunStage(ch.stages[ch.stages.Count - 1]);
        }

        /// <summary>The boss enemy of a chapter's last stage (for "defeat X to unlock" labels).</summary>
        public static string BossName(ChapterDef ch)
        {
            if (!ch.Playable) return null;
            var st = GameDb.RunStage(ch.stages[ch.stages.Count - 1]);
            return st.finale?.enemy != null ? GameDb.Enemy(st.finale.enemy).name : null;
        }
    }
}
