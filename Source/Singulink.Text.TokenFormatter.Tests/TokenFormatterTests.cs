using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Singulink.Text.Tests
{
    [TestClass]
    public class TokenFormatterTests
    {
        [TestMethod]
        public void BasicValue()
        {
            const string format = "Name: {Name}";
            const string expected = "Name: Bob";

            var tokenObject = new { Name = "Bob" };

            var tokenDictionary = new Dictionary<string, object> {
                ["Name"] = "Bob",
            };

            TestFormat(expected, format, tokenObject, tokenDictionary);
        }

        [TestMethod]
        public void Property()
        {
            const string expected = "Name: Bob, Age: 42";
            const string format = "Name: {Person.Name}, Age: {Person.Age}";

            var tokenObject = new { Person = new { Name = "Bob", Age = 42 } };

            var tokenDictionary = new Dictionary<string, object> {
                ["Person"] = new Dictionary<string, object> {
                    ["Name"] = "Bob",
                    ["Age"] = 42,
                },
            };

            TestFormat(expected, format, tokenObject, tokenDictionary);
        }

        [TestMethod]
        public void MultipleSubKeys()
        {
            const string expected = "Name Length: 3";
            const string format = "Name Length: {Person.Name.Length}";

            var tokenDictionary = new Dictionary<string, object> {
                ["Person"] = new Dictionary<string, object> {
                    ["Name"] = "Bob",
                    ["Name.Length"] = 3,
                },
            };

            var tokenObject = new { Person = new { Name = "Bob" } };

            TestFormat(expected, format, tokenDictionary, tokenObject);
        }

        [TestMethod]
        public void NullValueReplacement()
        {
            const string format = "Names: {Name?[null]}, {Person?[?].Name?[null]}";
            const string expected = "Names: [null], [null]";

            var tokenObject = new { Name = (string)null, Person = new { Name = (string)null } };

            var tokenDictionary = new Dictionary<string, object?> {
                ["Name"] = null,
                ["Person"] = new Dictionary<string, object?> {
                    ["Name"] = null,
                },
            };

            TestFormat(expected, format, tokenObject, tokenDictionary);
        }

        [TestMethod]
        public void NullTargetReplacement()
        {
            const string format = "Name: {Person?[?].Name?[null]}";
            const string expected = "Name: [?]";

            var tokenObject = new { Person = (object)null };

            var tokenDictionary = new Dictionary<string, object?> {
                ["Person"] = null,
            };

            TestFormat(expected, format, tokenObject, tokenDictionary);
        }

        [TestMethod]
        public void TokenValueNotFound()
        {
            string format = "Name: {x?[missing]}";
            var obj = new Dictionary<string, object>();

            var ex = Assert.ThrowsException<KeyNotFoundException>(() => TokenFormatter.Format(format, obj));
            Assert.IsTrue(ex.Message.Contains("'x'", StringComparison.Ordinal));
            ex = Assert.ThrowsException<KeyNotFoundException>(() => TokenFormatter.Format(format, obj));
            Assert.IsTrue(ex.Message.Contains("'x'", StringComparison.Ordinal));

            Assert.AreEqual("Name: [missing]", TokenFormatter.Format(format, obj, TokenOptions.AllowMissingKeys));
        }

        [TestMethod]
        public void EscapedTokenMarkers()
        {
            Assert.AreEqual("{", TokenFormatter.Format("{{", new object()));
            Assert.AreEqual("}{", TokenFormatter.Format("}}{{", new object()));
            Assert.AreEqual("{0}", TokenFormatter.Format("{{{Length}}}", string.Empty));
            Assert.AreEqual("{Length}", TokenFormatter.Format("{{Length}}", string.Empty));
            Assert.ThrowsException<FormatException>(() => TokenFormatter.Format("Unescaped { brace", string.Empty));
            Assert.ThrowsException<FormatException>(() => TokenFormatter.Format("Unescaped } brace", string.Empty));
        }

        [TestMethod]
        public void TokenValueFormat()
        {
            Assert.AreEqual("0005", TokenFormatter.Format("{Value:D4}", new { Value = 5 }));
            Assert.AreEqual("0005", TokenFormatter.Format("{Object.Value:D4}", new { Object = new { Value = 5 } }));
        }

        [TestMethod]
        public void PropertyAccessModifier()
        {
            var obj = new PropertyAccess();

            Assert.AreEqual("Value", TokenFormatter.Format("{PublicValue}", obj));
            Assert.AreEqual("Value", TokenFormatter.Format("{InternalValue}", obj, TokenOptions.NonPublicAccess));

            Assert.ThrowsException<KeyNotFoundException>(() => TokenFormatter.Format("{InternalValue}", obj));
        }

        private static void TestFormat(string expected, string format, params object[] tokenValueObjects) => TestFormat(expected, format, TokenOptions.None, tokenValueObjects);

        private static void TestFormat(string expected, string format, TokenOptions options, params object[] tokenValueObjects)
        {
            foreach (object tokenValues in tokenValueObjects) {
                string result = TokenFormatter.Format(format, tokenValues, options);
                Assert.AreEqual(expected, result);
            }
        }

        private sealed class PropertyAccess
        {
            internal string InternalValue { get; } = "Value";

            public string PublicValue { get; } = "Value";
        }
    }
}
