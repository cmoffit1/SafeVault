using System;
namespace Api.Utilities
{
    /// <summary>
    /// Backwards-compatible input validation shim. The previous implementation
    /// attempted to remove SQL keywords which is unsafe and can mangle data.
    /// Prefer using <see cref="ValidationHelpers"/> helpers directly. This
    /// method performs a conservative normalization matching older behavior.
    /// </summary>
    public static class InputValidation
    {
        public static string Sanitize(string? input)
        {
            // Use the conservative normalizer from ValidationHelpers which trims,
            // normalizes Unicode, removes control characters and collapses whitespace.
            return ValidationHelpers.Sanitize(input);
        }
    }
}
