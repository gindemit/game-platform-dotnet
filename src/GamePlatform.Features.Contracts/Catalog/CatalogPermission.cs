namespace GamePlatform.Features.Contracts.Catalog
{

    /// <summary>Visibility is deliberately separate from every authority permission.</summary>
    public enum CatalogPermission
    {
        Visible, Use, Grant, Spend, Submit
    }
}
