# Singulink.Text.TokenFormatter

[![Join the chat](https://badges.gitter.im/Singulink/community.svg)](https://gitter.im/Singulink/community?utm_source=badge&utm_medium=badge&utm_campaign=pr-badge&utm_content=badge)
[![View nuget packages](https://img.shields.io/nuget/v/Singulink.Text.TokenFormatter.svg)](https://www.nuget.org/packages/Singulink.Text.TokenFormatter/)
[![Build and Test](https://github.com/Singulink/Singulink.Text.TokenFormatter/workflows/build%20and%20test/badge.svg)](https://github.com/Singulink/Singulink.Text.TokenFormatter/actions?query=workflow%3A%22build+and+test%22)

TokenFormatter provides simple yet versatile tokenized string formatting, designed primarily for processing localized resource strings, exception messages and log data in a more friendly manner than brittle positional tokens in the `string.Format()` family of methods. This can be particularly useful when utilizing localized resource strings since your translaters will have much better context when they see values like `"{User.Name} performed {Action}"` instead of `"{0} performed {1}"`.

# Installation

Simply install the `Singulink.Text.TokenFormatter` package from NuGet into your project.

**Supported Runtimes**: Anywhere .NET Standard 2.0 is supported, including .NET Framework 4.6.1+ and .NET Core 2.0+.

# Usage

The only two types you need to worry about are `TokenFormatter` and `TokenOptions`. `TokenFormatter` has the following two static methods that do it all:

```c#
public static string Format(string format, object tokenValues, TokenOptions options = default);
public static string Format(string format, object tokenValues, IFormatProvider? formatProvider, TokenOptions options = default);
```

Format strings contain token declarations inside curly braces. The simplest form of this just contains a token key, i.e. `{TokenKey}`. Token keys map are used as string keys if `tokenValues` is an `IDictionary` otherwise they map to property names on other object types.

Tokens can contain subkeys seperated by dots (`.`) which will grab nested values, e.g. `{User.FirstName.Length}`. Tokens can also contain an optional format string following a colon (`:`), e.g. `{Transaction.Amount:C2}`.

Finally, any key or subkey can have a question mark (`?`) appended to it to indicate that the value may be null, e.g. `{User.MiddleName?.Length}`. If you do not indicate that a token value can be null then a `NullReferenceException` is thrown when a null value is encountered. Null values are simply replaced with an empty string by default, but you can specify a replacement string when the value is null by putting it after the question mark, like so: `{User?[unknown user].MiddleName?*no middle name*.Length}`. This example would output the string `"[unknown user]"` if the `User` key returns a null value or `"*no middle name*"` if the `MiddleName` subkey returns a null value.

Curly braces are escaped by doubling them up, so `"{{"` and `"}}"` are formatted as `"{"` and `"}"`, respectively. The inside of a token declaration cannot contain any curly braces anywhere. Additionally, token keys cannot contain question marks (`?`) or colons (`:`) as they are interpreted as nullability operators and token format delimiters, respectively.

`TokenOptions` has three available values:
- `None` - the default value with no options set
- `NonPublicAccess` - Indicates non-public properties can be accessed from tokens.
- `AllowMissingKeys` - Indicates that nullable keys that don't exist should be treated like `null` instead of throwing a `KeyNotFoundException` exception.

# Examples

The following are some examples to demonstrate how TokenFormatter works:

```c#
using Singulink.Text;

User? currentUser = GetCurrentlyLoggedInUser();
string url = HttpContext.Current.Request.Url;

// "[anonymous] just visited website.com"
// "mikernet just visited website.com"
TokenFormatter.Format("{User?[anonymous].Name} just visited {Url}", new { User = currentUser, Url = url });

var info = new Dictionary<string, object> {
  [Name] = "Mike",
  [Age] = 35,
  [Address] = new { Street = "123 Easy Street", City = "London", State = "ON" },
  [Income] = 99999.99m,
}

// "Mike (age 35) lives at 123 Easy Street, London, ON"
TokenFormatter.Format("{Name} (age {Age}) lives at {Address.Street}, {Address.City}, {Address.State}", info);

// "{Mike has an income of $99,999.99 and drives ?}"
TokenFormatter.Format("{{{Name} has an income of {Income:C2} and drives {Car??}}}", info, TokenOptions.AllowMissingKeys);
```
