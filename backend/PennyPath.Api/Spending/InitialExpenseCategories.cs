using PennyPath.Api.Data;

namespace PennyPath.Api.Spending;

public static class InitialExpenseCategories
{
    public static void AddTo(PennyPathIdentityDbContext database, string ownerId)
    {
        Add("food", "Jedzenie", 0, ("groceries", "zakupy spożywcze"), ("restaurants", "restauracje"),
            ("drinks", "napoje"), ("other", "inne"));
        Add("transport", "Transport", 1, ("fuel", "paliwo"), ("public-transport", "komunikacja"), ("other", "inne"));
        Add("home", "Dom", 2, ("bills", "czynsz i opłaty"), ("furnishings", "wyposażenie"), ("other", "inne"));
        Add("health", "Zdrowie", 3, ("medicines", "leki"), ("consultations", "wizyty"), ("other", "inne"));
        Add("entertainment", "Rozrywka", 4, ("subscriptions", "subskrypcje"), ("outings", "wyjścia"), ("other", "inne"));
        Add("miscellaneous", "Pozostałe", 5, ("other", "inne"));

        void Add(string id, string name, int order, params (string Id, string Name)[] children)
        {
            database.ExpenseCategories.Add(new ExpenseCategory
            {
                OwnerId = ownerId, CategoryId = id, Name = name, DisplayOrder = order
            });
            for (var index = 0; index < children.Length; index++)
            {
                database.ExpenseSubcategories.Add(new ExpenseSubcategory
                {
                    OwnerId = ownerId, CategoryId = id, SubcategoryId = children[index].Id,
                    Name = children[index].Name, DisplayOrder = index
                });
            }
        }
    }
}
