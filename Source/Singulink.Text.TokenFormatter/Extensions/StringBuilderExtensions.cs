using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace Singulink.Text.Extensions
{
    internal static class StringBuilderExtensions
    {
        public static StringBuilder Append(this StringBuilder builder, ReadOnlySpan<char> value)
        {
            if (value.Length > 0) {
                unsafe {
                    fixed (char* valueChars = &MemoryMarshal.GetReference(value)) {
                        builder.Append(valueChars, value.Length);
                    }
                }
            }

            return builder;
        }
    }
}
