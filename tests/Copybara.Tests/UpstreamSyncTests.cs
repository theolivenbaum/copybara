/*
 * Copyright (C) 2026 Google LLC
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 * http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

using Copybara.TemplateToken;
using Copybara.Util;
using FluentAssertions;
using Xunit;

namespace Copybara.Tests;

/// <summary>
/// Tests for behavior brought over from upstream google/copybara during the reference-tree sync.
/// </summary>
public class UpstreamSyncTests
{
    /// <summary>
    /// Port of upstream <c>GlobTest.emptyRootTest</c>: a matcher relative to an empty root must
    /// match relative paths, not separator-prefixed ones.
    /// </summary>
    [Fact]
    public void Glob_RelativeToEmptyRoot_MatchesRelativePaths()
    {
        var matcher = Glob.CreateGlob(new[] { "foo/**" }).RelativeTo("");
        matcher.Matches("foo/bar").Should().BeTrue();
        matcher.Matches("bar/baz").Should().BeFalse();
        matcher.Matches("/foo/bar").Should().BeFalse();
    }

    [Fact]
    public void ReadablePathMatcher_RelativeGlobWithEmptyRoot_StaysRelative()
    {
        var matcher = ReadablePathMatcher.RelativeGlob("", "foo/**");
        matcher.Matches("foo/bar").Should().BeTrue();
        matcher.Matches("/foo/bar").Should().BeFalse();
    }

    [Fact]
    public void LabelTemplate_HasValueEqualityAndTemplateToString()
    {
        var a = new LabelTemplate("${MERGE_MSG}");
        var b = new LabelTemplate("${MERGE_MSG}");
        var c = new LabelTemplate("Merged: ${SUMMARY_FROM_TRANSFORM}");

        a.Should().Be(b);
        a.GetHashCode().Should().Be(b.GetHashCode());
        a.Should().NotBe(c);
        a.ToString().Should().Be("${MERGE_MSG}");
    }

    [Fact]
    public void LabelTemplate_ResolvesCustomLabels()
    {
        var template = new LabelTemplate("Merged: ${SUMMARY} \n\nOriginal: ${MERGE_MSG}");
        string resolved = template.Resolve(name => name switch
        {
            "SUMMARY" => "the summary",
            "MERGE_MSG" => "the default message",
            _ => null,
        });
        resolved.Should().Be("Merged: the summary \n\nOriginal: the default message");
    }

    [Fact]
    public void LabelTemplate_ThrowsWhenLabelMissing()
    {
        var template = new LabelTemplate("${MISSING}");
        var act = () => template.Resolve(_ => null);
        act.Should().Throw<LabelTemplate.LabelNotFoundException>()
            .Which.Label.Should().Be("MISSING");
    }

    [Fact]
    public void TablePrinter_AddRowIsThreadSafe()
    {
        var printer = new TablePrinter("a", "b");
        Parallel.For(0, 200, i => printer.AddRow(i, i * 2));

        // 200 rows + 3 separator lines + 1 header line.
        printer.Build().Count.Should().Be(204);
    }
}
