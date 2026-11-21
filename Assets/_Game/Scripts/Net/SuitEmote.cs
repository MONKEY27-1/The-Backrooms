namespace Backrooms.Net
{
    /// Shared lobby/gameplay gestures. Short gestures return to idle after their duration; dances and sitting stay active.
    public static class SuitEmote
    {
        public const int None = 0, Wave = 1, ThumbsUp = 2, Point = 3, ComeHere = 4, Dance = 5, Sit = 6,
            Laugh = 7, Salute = 8, Facepalm = 9, Floss = 10, TakeL = 11;

        public static readonly string[] Names =
        { "", "Wave", "Thumbs Up", "Point Forward", "Come Here", "Dance", "Sit", "Laugh", "Salute", "Facepalm", "Floss", "Take the L" };

        public static bool Looping(int id) => id == Dance || id == Sit;
        public static float Duration(int id) => id switch { Wave => 2.8f, Point => 2f, ComeHere => 2.2f, Laugh => 2.5f, Salute => 2f, Facepalm => 2.5f, TakeL => 3f, _ => 2f };
    }
}
