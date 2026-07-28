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

using FluentAssertions;
using Starlark.Eval;
using Xunit;

namespace Copybara.Tests;

/// <summary>
/// Builds the reflective Starlark descriptors for the big modules. This catches
/// <c>[StarlarkMethod]</c>/<c>[Param]</c> mismatches (wrong arity, unparseable default value)
/// which would otherwise only surface when a real <c>copy.bara.sky</c> is loaded.
/// </summary>
public class StarlarkDescriptorTests
{
    [Fact]
    public void GitModule_DescriptorsResolve()
    {
        var methods = CallUtils.GetAnnotatedMethods(typeof(Copybara.Git.GitModule));
        methods.Keys.Should().Contain("integrate");

        var integrate = methods["integrate"];
        integrate.Parameters.Select(p => p.Name).Should().Equal(
            "label", "strategy", "ignore_errors", "allow_unrelated_history",
            "merge_commit_message");
        integrate.Parameters.Last().DefaultValue.Should().Be("${MERGE_MSG}");
    }

    [Fact]
    public void CoreModule_DescriptorsResolve()
    {
        CallUtils.GetAnnotatedMethods(typeof(Copybara.CoreModule)).Keys
            .Should().Contain("workflow");
    }
}
