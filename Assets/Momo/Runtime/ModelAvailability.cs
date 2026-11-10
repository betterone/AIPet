namespace Momo
{
    // The two imports added on 2026-11-09. Breed identity and save IDs never change;
    // this policy only decides which appearance can be presented on each platform.
    public static class ModelAvailability
    {
        public const int BritishBlue = 1;
        public const int Calico = 6;

        public static bool IsTodayModel(int breed)
        { return breed == BritishBlue || breed == Calico; }

        // iOS deliberately omits today's two imported appearances. Existing saves keep
        // their breed and memories, and CatView falls back to the original appearance.
        public static bool IsVisibleInBoutique(int breed)
        {
#if UNITY_IOS && !UNITY_EDITOR
            return !IsTodayModel(breed);
#else
            return true;
#endif
        }

        // Web is a focused showcase: the other companions remain visible as "pending"
        // but cannot be selected or adopted from the first web release.
        public static bool IsPendingInBoutique(int breed)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return !IsTodayModel(breed);
#else
            return false;
#endif
        }

        public static bool CanSelectInBoutique(int breed)
        { return IsVisibleInBoutique(breed) && !IsPendingInBoutique(breed); }

        public static bool CanUseImportedAppearance(int breed)
        { return IsVisibleInBoutique(breed); }

        public static bool WebShowcase
        {
            get
            {
#if UNITY_WEBGL && !UNITY_EDITOR
                return true;
#else
                return false;
#endif
            }
        }
    }
}
