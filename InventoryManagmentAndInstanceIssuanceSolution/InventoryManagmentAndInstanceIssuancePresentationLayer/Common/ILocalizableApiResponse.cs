// PresentationLayer/Common/ILocalizableApiResponse.cs
namespace InventoryManagmentAndInstanceIssuancePresentationLayer.Common
{
    /// <summary>Lets a culture-aware filter read an error's code/arg and replace its message
    /// on an <c>ApiResponse&lt;T&gt;</c> without knowing the generic payload type.</summary>
    public interface ILocalizableApiResponse
    {
        /// <summary>The error payload, or null on success responses.</summary>
        ApiError? Error { get; }

        /// <summary>Replaces the error message with its localized text.</summary>
        void ReplaceErrorMessage(string localizedMessage);
    }
}