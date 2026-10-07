namespace DomainLayer.Enums
{
    /// <summary>
    /// Activation state of a catalog entity (ERD §8). Persisted as <c>TINYINT</c>.
    /// </summary>
    public enum ActivationStatus : byte
    {
        /// <summary>The entity is active and usable. ERD value 0.</summary>
        Active = 0,

        /// <summary>The entity is inactive and excluded from operational flows. ERD value 1.</summary>
        Inactive = 1
    }
}
