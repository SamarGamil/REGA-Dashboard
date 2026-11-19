using AqarInsights.Api.Models.ForeignOwnership;

namespace AqarInsights.Api.Tests;

public class PropertyCategoriesTests
{
    [Theory]
    [InlineData("مبنى سكني", PropertyCategories.ResidentialBuilding)]
    [InlineData("مبنى  سكني", PropertyCategories.ResidentialBuilding)]
    [InlineData("مجمع سكني", PropertyCategories.ResidentialBuilding)]
    [InlineData("مبنى", PropertyCategories.ResidentialBuilding)]
    [InlineData("برج", PropertyCategories.ResidentialBuilding)]
    [InlineData("فيلا", PropertyCategories.ResidentialBuilding)]
    [InlineData("قصر", PropertyCategories.ResidentialBuilding)]
    [InlineData("عمارة سكنية", PropertyCategories.ResidentialBuilding)]
    [InlineData("أرض فضاء", PropertyCategories.VacantLand)]
    [InlineData("أرض مسورة", PropertyCategories.VacantLand)]
    [InlineData("ارض", PropertyCategories.VacantLand)]
    [InlineData("شقة سكنية", PropertyCategories.ResidentialApartment)]
    [InlineData("شقة", PropertyCategories.ResidentialApartment)]
    [InlineData("دور", PropertyCategories.Floor)]
    [InlineData("مبنى سكني/تجاري", PropertyCategories.MixedUse)]
    [InlineData("مجمع سكني/ تجاري", PropertyCategories.MixedUse)]
    [InlineData("مبنى تجاري", PropertyCategories.Other)]
    [InlineData("مجمع تجاري", PropertyCategories.Other)]
    [InlineData("مستودع", PropertyCategories.Other)]
    [InlineData("استراحة", PropertyCategories.Other)]
    [InlineData("مرفق صناعي", PropertyCategories.Other)]
    [InlineData("محل", PropertyCategories.Other)]
    [InlineData("مكتب", PropertyCategories.Other)]
    [InlineData("شاليه", PropertyCategories.Other)]
    [InlineData("مزرعة", PropertyCategories.Other)]
    [InlineData("زائدة تنظيمية", PropertyCategories.Other)]
    [InlineData("محطة وقود", PropertyCategories.Other)]
    [InlineData("اخرى", PropertyCategories.Other)]
    [InlineData("", PropertyCategories.Other)]
    [InlineData(null, PropertyCategories.Other)]
    public void Maps_raw_type_to_one_of_six_categories(string? raw, string expected)
        => Assert.Equal(expected, PropertyCategories.FromRaw(raw));

    [Fact]
    public void All_lists_the_six_categories_in_canonical_order()
        => Assert.Equal(
            ["مباني سكنية", "أرض شاغرة", "شقق سكنية", "دور", "مباني متعددة الأغراض", "فئات أخرى"],
            PropertyCategories.All);
}
