using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using Singulink.Text.Extensions;

namespace Singulink.Text
{
    /// <summary>
    /// Provides tokenized string formatting, designed primarily for processing localized resource strings, exception messages and log data in a more friendly
    /// manner than the positional tokens in the <see cref="string.Format(string, object?)"/> family of methods.
    /// </summary>
    /// <remarks>
    /// <para>Format strings contain token declarations inside curly braces. The simplest form of this just contains a token key, i.e. <c>{TokenKey}</c>. Token
    /// keys map to property names for regular objects or string keys for <see cref="IDictionary"/> objects.
    /// </para>
    /// <para>Tokens can contain subkeys seperated by dots (<c>.</c>) which will grab nested values, e.g. <c>{User.FirstName.Length}</c>.
    /// </para>
    /// <para>Tokens can also contain an optional format string following a colon (<c>:</c>), e.g. <c>{Transaction.Amount:C2}</c>.
    /// </para>
    /// <para>Curly braces are escaped by doubling them up, so <c>"{{"</c> and <c>"}}"</c> are formatted as <c>"{"</c> and <c>"}"</c>, respectively.
    /// </para>
    /// <para>The inside of a token declaration cannot contain any curly braces anywhere. Additionally, token keys cannot contain question marks (<c>?</c>) or
    /// colons (<c>:</c>) as they are interpreted as nullability operators and token format delimiters, respectively.
    /// </para>
    /// </remarks>
    public static class TokenFormatter
    {
        /// <summary>
        /// Substitutes named tokens in the format string with values provided from a dictionary or an object.
        /// </summary>
        /// <param name="format">A string containing tokens to be replaced with values.</param>
        /// <param name="tokenValues">A dictionay or object that contains token names and values.</param>
        /// <param name="options">Optional additional options that control the formatting behavior.</param>
        /// <returns>The resulting string with token substitutions performed.</returns>
        /// <exception cref="FormatException">The tokenized string was not in the correct format.</exception>
        /// <exception cref="KeyNotFoundException">A token key was not found.</exception>
        /// <exception cref="NullReferenceException">A non-nullable token value was null.</exception>
        public static string Format(string format, object tokenValues, TokenOptions options = default)
        {
            return Format(format, tokenValues, null, options);
        }

        /// <summary>
        /// Substitutes named tokens in the format string with values provided from a dictionary or an object.
        /// </summary>
        /// <param name="format">A string containing tokens to be replaced with values.</param>
        /// <param name="tokenValues">A dictionay or object that contains token names and values.</param>
        /// <param name="formatProvider">Optional format provider that is used to format the token values.</param>
        /// <param name="options">Optional additional options that control the formatting behavior.</param>
        /// <returns>The resulting string with token substitutions performed.</returns>
        /// <exception cref="FormatException">The tokenized string was not in the correct format.</exception>
        /// <exception cref="KeyNotFoundException">A token key was not found.</exception>
        /// <exception cref="NullReferenceException">A non-nullable token value was null.</exception>
        public static string Format(string format, object tokenValues, IFormatProvider? formatProvider, TokenOptions options = default)
        {
            var builder = new StringBuilder(Math.Max(format.Length, 10));

            bool nonPublic = options.HasFlag(TokenOptions.NonPublicAccess);
            bool allowMissingKeys = options.HasFlag(TokenOptions.AllowMissingKeys);
            var remainingFormat = format.AsSpan();

            while (true) {
                int tokenStart = remainingFormat.IndexOfAny('{', '}');

                if (tokenStart < 0) {
                    builder.Append(remainingFormat);
                    return builder.ToString();
                }

                builder.Append(remainingFormat.Slice(0, tokenStart));

                char startMarker = remainingFormat[tokenStart];

                if (remainingFormat.Length > tokenStart + 1 && remainingFormat[tokenStart + 1] == startMarker) {
                    builder.Append(startMarker);
                    remainingFormat = remainingFormat.Slice(tokenStart + 2);
                    continue;
                }

                if (startMarker == '}')
                    throw GetFormatException("Closing token brace with no preceding opening brace.");

                var tokenContent = remainingFormat.Slice(tokenStart + 1);
                int tokenContentEnd = tokenContent.IndexOfAny('{', '}');

                if (tokenContentEnd < 0 || tokenContent[tokenContentEnd] != '}')
                    throw GetFormatException("Opening token brace without matching closing brace.");

                tokenContent = tokenContent.Slice(0, tokenContentEnd);

                if (tokenContent.Length == 0)
                    throw GetFormatException("Empty token declaration.");

                var token = new Token(tokenContent);
                object tokenValue = tokenValues;
                bool fullyResolved = false;

                while (true) {
                    string tokenKeyName = token.Key.Name.ToString();

                    if (!ResolveTokenSubValue(tokenKeyName, tokenValue, nonPublic, out tokenValue) && !allowMissingKeys)
                        throw new KeyNotFoundException($"Token key '{tokenKeyName}' not found when resolving token '{{{tokenContent.ToString()}}}'.");

                    if (tokenValue == null) {
                        if (!token.Key.IsNullable)
                            throw new NullReferenceException($"Non-nullable token key '{tokenKeyName}' returned a null value when resolving token '{{{tokenContent.ToString()}}}'.");

                        tokenValue = token.Key.NullSubstitute.ToString();
                        break;
                    }

                    if (!token.HasSubKey) {
                        fullyResolved = true;
                        break;
                    }

                    token.ProcessSubKey();
                }

                if (tokenValue != null) {
                    if (fullyResolved && token.HasFormat && tokenValue is IFormattable formattableValue)
                        builder.Append(formattableValue.ToString(token.Format.ToString(), formatProvider));
                    else
                        builder.Append(tokenValue);
                }

                remainingFormat = remainingFormat.Slice(tokenStart + tokenContentEnd + 2);
            }

            static bool ResolveTokenSubValue(string tokenKeyName, object currentValue, bool nonPublic, out object? value)
            {
                if (currentValue is IDictionary d) {
                    if (d.Contains(tokenKeyName)) {
                        value = d[tokenKeyName];
                        return true;
                    }

                    value = null;
                    return false;
                }

                var bindingFlags = BindingFlags.Instance | BindingFlags.Public;

                if (nonPublic)
                    bindingFlags |= BindingFlags.NonPublic;

                var property = currentValue.GetType().GetProperty(tokenKeyName, bindingFlags);

                if (property?.CanRead == true) {
                    value = property.GetValue(currentValue);
                    return true;
                }

                value = null;
                return false;
            }
        }

        private static FormatException GetFormatException(string messageEnd) => new FormatException("Tokenized string was not in a correct format: " + messageEnd);

        private ref struct Token
        {
            private ReadOnlySpan<char> _remainingPath;

            public TokenKey Key { get; internal set; }

            public ReadOnlySpan<char> Format { get; }

            public bool HasSubKey => !_remainingPath.IsEmpty;

            public bool HasFormat { get; }

            public Token(ReadOnlySpan<char> content)
            {
                int formatDelimiter = content.IndexOf(':');

                if (formatDelimiter < 0) {
                    _remainingPath = content;
                    Format = ReadOnlySpan<char>.Empty;
                    HasFormat = false;
                }
                else {
                    _remainingPath = content.Slice(0, formatDelimiter);
                    Format = content.Slice(formatDelimiter + 1);
                    HasFormat = true;
                }

                Key = ProcessKey(ref _remainingPath);
            }

            public void ProcessSubKey()
            {
                Key = ProcessKey(ref _remainingPath);
            }

            private static TokenKey ProcessKey(ref ReadOnlySpan<char> remainingPath)
            {
                if (remainingPath.IsEmpty)
                    throw new InvalidOperationException("Remaining path is empty.");

                int subkeyDelimiter = remainingPath.IndexOf('.');

                TokenKey key;

                if (subkeyDelimiter < 0) {
                    key = new TokenKey(remainingPath);
                    remainingPath = ReadOnlySpan<char>.Empty;
                }
                else {
                    key = new TokenKey(remainingPath.Slice(0, subkeyDelimiter));
                    remainingPath = remainingPath.Slice(subkeyDelimiter + 1);
                }

                return key;
            }
        }

        private ref struct TokenKey
        {
            public ReadOnlySpan<char> Name { get; }

            public ReadOnlySpan<char> NullSubstitute { get; }

            public bool IsNullable { get; }

            public TokenKey(ReadOnlySpan<char> value)
            {
                int nullableMarkerIndex = value.IndexOf('?');

                if (nullableMarkerIndex < 0) {
                    Name = value;
                    NullSubstitute = ReadOnlySpan<char>.Empty;
                    IsNullable = false;
                }
                else {
                    Name = value.Slice(0, nullableMarkerIndex);
                    NullSubstitute = value.Slice(nullableMarkerIndex + 1, value.Length - nullableMarkerIndex - 1);
                    IsNullable = true;
                }

                if (Name.Length == 0)
                    throw GetFormatException("Empty token key.");
            }
        }
    }
}
