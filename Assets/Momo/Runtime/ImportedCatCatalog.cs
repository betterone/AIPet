namespace Momo
{
    // Appearance selection only. Breed IDs, personalities and saved memories stay stable.
    public static class ImportedCatCatalog
    {
        public static string ForBreed(int breed)
        {
            if(!ModelAvailability.CanUseImportedAppearance(breed))return null;
            switch (breed)
            {
                case 1: return "ImportedCats/BritishBlue/Model";
                case 7: return "ImportedCats/Ragdoll/Model";
                case 4: return "ImportedCats/White/Model";
                case 5: return "ImportedCats/Black/Model";
                case 6: return "ImportedCats/Calico/Model";
                default: return null;
            }
        }
        public static string MaterialFor(string model)
        { return model.Substring(0, model.LastIndexOf('/') + 1) + "Material"; }
    }
}
