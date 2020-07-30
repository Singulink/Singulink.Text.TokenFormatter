using System;
using System.Collections.Generic;

namespace Singulink.Text
{
    /// <summary>
    /// Provides options for controlling token formatting behavior.
    /// </summary>
    [Flags]
    public enum TokenOptions
    {
        /// <summary>
        /// Default value with no options set.
        /// </summary>
        None = 0,

        /// <summary>
        /// Indicates non-public properties can be accessed from tokens.
        /// </summary>
        NonPublicAccess = 1,

        /// <summary>
        /// Indicates that missing nullable keys should be treated as null values instead of throwing a <see cref="KeyNotFoundException"/> exception.
        /// </summary>
        AllowMissingKeys = 2,
    }
}
