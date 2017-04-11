// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Xunit;

namespace NuGet.Protocol.Plugins.Tests
{
    public class ProgressTests
    {
        [Theory]
        [InlineData(double.NaN)]
        [InlineData(double.NegativeInfinity)]
        [InlineData(double.PositiveInfinity)]
        [InlineData(double.MinValue)]
        [InlineData(double.MaxValue)]
        [InlineData(-0.1)]
        [InlineData(2D)]
        public void Constructor_ThrowsForInvalidProgressPercent(double progressPercent)
        {
            var exception = Assert.Throws<ArgumentException>(() => new Progress(progressPercent));

            Assert.Equal("progressPercent", exception.ParamName);
        }

        [Theory]
        [InlineData(0D)]
        [InlineData(0.5)]
        [InlineData(1D)]
        public void Constructor_InitializesProperty(double progressPercent)
        {
            var progress = new Progress(progressPercent);

            Assert.Equal(progressPercent, progress.ProgressPercent);
        }

        [Fact]
        public void JsonSerialization_ReturnsCorrectJson()
        {
            var progress = new Progress(progressPercent: 0.5);
            var json = TestUtilities.Serialize(progress);

            Assert.Equal("{\"ProgressPercent\":0.5}", json);
        }

        [Theory]
        [InlineData("{\"ProgressPercent\":0}", "a", 0D)]
        [InlineData("{\"ProgressPercent\":0.5}", "a", 0.5)]
        [InlineData("{\"ProgressPercent\":1}", "a", 1D)]
        public void JsonDeserialization_ReturnsCorrectObject(string json, string requestId, double progressPercent)
        {
            var progress = TestUtilities.Deserialize<Progress>(json);

            Assert.Equal(progressPercent, progress.ProgressPercent);
        }
    }
}