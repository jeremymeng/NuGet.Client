// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Xunit;

namespace NuGet.Protocol.Plugins.Tests
{
    public class InitializeRequestTests
    {
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public void Constructor_ThrowsForNullOrEmptyClientVersion(string clientVersion)
        {
            var exception = Assert.Throws<ArgumentException>(
                () => new InitializeRequest(
                    clientVersion,
                    culture: "a",
                    verbosity: Verbosity.Normal,
                    requestTimeout: TimeSpan.MaxValue));

            Assert.Equal("clientVersion", exception.ParamName);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public void Constructor_ThrowsForNullOrEmptyCulture(string culture)
        {
            var exception = Assert.Throws<ArgumentException>(
                () => new InitializeRequest(
                    clientVersion: "1.0.0",
                    culture: culture,
                    verbosity: Verbosity.Normal,
                    requestTimeout: TimeSpan.MaxValue));

            Assert.Equal("culture", exception.ParamName);
        }

        [Fact]
        public void Constructor_ThrowsForUndefinedVerbosity()
        {
            var exception = Assert.Throws<ArgumentException>(
                () => new InitializeRequest(
                    clientVersion: "1.0.0",
                    culture: "a",
                    verbosity: (Verbosity)int.MaxValue,
                    requestTimeout: TimeSpan.MaxValue));

            Assert.Equal("verbosity", exception.ParamName);
        }

        [Fact]
        public void Constructor_InitializesProperties()
        {
            var requestTimeout = new TimeSpan(days: 1, hours: 2, minutes: 3, seconds: 4, milliseconds: 5);
            var request = new InitializeRequest(
                clientVersion: "a",
                culture: "b",
                verbosity: Verbosity.Detailed,
                requestTimeout: requestTimeout);

            Assert.Equal("a", request.ClientVersion);
            Assert.Equal("b", request.Culture);
            Assert.Equal(Verbosity.Detailed, request.Verbosity);
            Assert.Equal(requestTimeout, request.RequestTimeout);
        }

        [Fact]
        public void JsonSerialization_ReturnsCorrectJson()
        {
            var requestTimeout = new TimeSpan(days: 1, hours: 2, minutes: 3, seconds: 4, milliseconds: 5);
            var request = new InitializeRequest(
                clientVersion: "a",
                culture: "b",
                verbosity: Verbosity.Detailed,
                requestTimeout: requestTimeout);

            var actualJson = TestUtilities.Serialize(request);
            var expectedJson = "{\"ClientVersion\":\"a\",\"Culture\":\"b\",\"Verbosity\":\"Detailed\",\"RequestTimeout\":\"1.02:03:04.0050000\"}";

            Assert.Equal(expectedJson, actualJson);
        }

        [Fact]
        public void JsonDeserialization_ReturnsCorrectObject()
        {
            var json = "{\"ClientVersion\":\"a\",\"Culture\":\"b\",\"Verbosity\":\"Detailed\",\"RequestTimeout\":\"1.02:03:04.0050000\"}";

            var request = TestUtilities.Deserialize<InitializeRequest>(json);

            Assert.NotNull(request);
            Assert.Equal("a", request.ClientVersion);
            Assert.Equal("b", request.Culture);
            Assert.Equal(Verbosity.Detailed, request.Verbosity);
            Assert.Equal(new TimeSpan(days: 1, hours: 2, minutes: 3, seconds: 4, milliseconds: 5), request.RequestTimeout);
        }

        [Theory]
        [InlineData("{\"Culture\":\"b\",\"Verbosity\":\"Detailed\",\"RequestTimeout\":\"1.02:03:04.0050000\"}")]
        [InlineData("{\"ClientVersion\":null,\"Culture\":\"b\",\"Verbosity\":\"Detailed\",\"RequestTimeout\":\"1.02:03:04.0050000\"}")]
        [InlineData("{\"ClientVersion\":\"\",\"Culture\":\"b\",\"Verbosity\":\"Detailed\",\"RequestTimeout\":\"1.02:03:04.0050000\"}")]
        [InlineData("{\"ClientVersion\":\"a\",\"Verbosity\":\"Detailed\",\"RequestTimeout\":\"1.02:03:04.0050000\"}")]
        [InlineData("{\"ClientVersion\":\"a\",\"Culture\":null,\"Verbosity\":\"Detailed\",\"RequestTimeout\":\"1.02:03:04.0050000\"}")]
        [InlineData("{\"ClientVersion\":\"a\",\"Culture\":\"\",\"Verbosity\":\"Detailed\",\"RequestTimeout\":\"1.02:03:04.0050000\"}")]
        public void JsonDeserialization_ThrowsForNullOrEmptyStringProperties(string json)
        {
            Assert.Throws<ArgumentNullException>(() => TestUtilities.Deserialize<HandshakeRequest>(json));
        }

        [Theory]
        [InlineData("{\"ClientVersion\":\"a\",\"Culture\":\"\",\"RequestTimeout\":\"1.02:03:04.0050000\"}")]
        [InlineData("{\"ClientVersion\":\"a\",\"Culture\":\"\",\"Verbosity\":null,\"RequestTimeout\":\"1.02:03:04.0050000\"}")]
        [InlineData("{\"ClientVersion\":\"a\",\"Culture\":\"\",\"Verbosity\":\"\",\"RequestTimeout\":\"1.02:03:04.0050000\"}")]
        [InlineData("{\"ClientVersion\":\"a\",\"Culture\":\"\",\"Verbosity\":\"abc\",\"RequestTimeout\":\"1.02:03:04.0050000\"}")]
        public void JsonDeserialization_ThrowsForInvalidVerbosity(string json)
        {
            Assert.Throws<ArgumentNullException>(() => TestUtilities.Deserialize<HandshakeRequest>(json));
        }

        [Theory]
        [InlineData("{\"ClientVersion\":\"a\",\"Culture\":\"b\",\"Verbosity\":\"Detailed\"}")]
        [InlineData("{\"ClientVersion\":\"a\",\"Culture\":\"b\",\"Verbosity\":\"Detailed\",\"RequestTimeout\":null}")]
        [InlineData("{\"ClientVersion\":\"a\",\"Culture\":\"b\",\"Verbosity\":\"Detailed\",\"RequestTimeout\":\"a\"}")]
        [InlineData("{\"ClientVersion\":\"a\",\"Culture\":\"b\",\"Verbosity\":\"Detailed\",\"RequestTimeout\":\"-00:01:00\"}")]
        public void JsonDeserialization_ThrowsForInvalidRequestTimeout(string json)
        {
            Assert.Throws<ArgumentNullException>(() => TestUtilities.Deserialize<HandshakeRequest>(json));
        }
    }
}