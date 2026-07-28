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

using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Copybara.Git.GitHub.Api;

/// <summary>
/// POJO representing the response from <see cref="GitHubGraphQLApi"/>'s getCheckRuns query. Port of
/// <c>com.google.copybara.git.github.api.GetFilteredCheckRunsResponse</c>.
/// </summary>
public class GetFilteredCheckRunsResponse
{
    [JsonPropertyName("data")]
    public ResponseData? Data { get; set; }

    public ResponseData? GetData() => Data;

    /// <summary>Holds the data objects.</summary>
    public class ResponseData
    {
        [JsonPropertyName("repository")]
        public RepositoryData? Repository { get; set; }

        public RepositoryData? GetRepository() => Repository;
    }

    /// <summary>Holds repository data.</summary>
    public class RepositoryData
    {
        [JsonPropertyName("object")]
        public CommitObject? Object { get; set; }

        public CommitObject? GetObject() => Object;
    }

    /// <summary>Holds commit object details.</summary>
    public class CommitObject
    {
        [JsonPropertyName("checkSuites")]
        public CheckSuites? CheckSuites { get; set; }

        [JsonPropertyName("associatedPullRequests")]
        public AssociatedPullRequests? AssociatedPullRequests { get; set; }

        public CheckSuites? GetCheckSuites() => CheckSuites;

        public AssociatedPullRequests? GetAssociatedPullRequests() => AssociatedPullRequests;
    }

    /// <summary>Wrapper for associated pull requests.</summary>
    public class AssociatedPullRequests
    {
        [JsonPropertyName("nodes")]
        public List<PullRequestNode>? Nodes { get; set; }

        public List<PullRequestNode>? GetNodes() => Nodes;
    }

    /// <summary>Pull request node wrapper returning number.</summary>
    public class PullRequestNode
    {
        [JsonPropertyName("number")]
        public int Number { get; set; }

        public int GetNumber() => Number;
    }

    /// <summary>Holds list of check suites.</summary>
    public class CheckSuites
    {
        [JsonPropertyName("nodes")]
        public List<CheckSuiteNode>? Nodes { get; set; }

        [JsonPropertyName("pageInfo")]
        public PageInfo? PageInfo { get; set; }

        public List<CheckSuiteNode>? GetNodes() => Nodes;

        public PageInfo? GetPageInfo() => PageInfo;
    }

    /// <summary>Holds pagination information.</summary>
    public class PageInfo
    {
        [JsonPropertyName("hasNextPage")]
        public bool HasNextPageValue { get; set; }

        [JsonPropertyName("endCursor")]
        public string? EndCursor { get; set; }

        public bool HasNextPage() => HasNextPageValue;

        public string? GetEndCursor() => EndCursor;
    }

    /// <summary>
    /// Holds a check suite node. The <c>checkRuns</c> connections are requested under dynamically
    /// generated aliases (<c>filter_0</c>, <c>filter_1</c>, …), so they are captured as extension
    /// data rather than declared properties — the Java original extends <c>GenericJson</c> for the
    /// same reason.
    /// </summary>
    public class CheckSuiteNode
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonExtensionData]
        public Dictionary<string, JsonElement>? AliasedCheckRuns { get; set; }

        public string? GetId() => Id;

        public IReadOnlyList<CheckRunNode> GetCheckRuns()
        {
            if (AliasedCheckRuns == null)
            {
                return ImmutableArray<CheckRunNode>.Empty;
            }
            var runs = ImmutableArray.CreateBuilder<CheckRunNode>();
            foreach (var value in AliasedCheckRuns.Values)
            {
                if (value.ValueKind != JsonValueKind.Object
                    || !value.TryGetProperty("nodes", out JsonElement nodes)
                    || nodes.ValueKind != JsonValueKind.Array)
                {
                    continue;
                }
                foreach (JsonElement node in nodes.EnumerateArray())
                {
                    if (node.ValueKind == JsonValueKind.Object)
                    {
                        runs.Add(new CheckRunNode(node));
                    }
                }
            }
            return runs.ToImmutable();
        }
    }

    /// <summary>Node structure for check run details.</summary>
    public class CheckRunNode
    {
        public CheckRunNode(JsonElement json)
        {
            Id = GetString(json, "id");
            Name = GetString(json, "name");
            Status = GetString(json, "status");
            Conclusion = GetString(json, "conclusion");
            DetailsUrl = GetString(json, "detailsUrl");
            CheckSuite =
                json.TryGetProperty("checkSuite", out JsonElement suite)
                && suite.ValueKind == JsonValueKind.Object
                    ? new CheckSuiteDetails(suite)
                    : null;
        }

        public string? Id { get; }

        public string? Name { get; }

        public string? Status { get; }

        public string? Conclusion { get; }

        public string? DetailsUrl { get; }

        public CheckSuiteDetails? CheckSuite { get; }

        public string? GetId() => Id;

        public string? GetName() => Name;

        public string? GetStatus() => Status;

        public string? GetConclusion() => Conclusion;

        public string? GetDetailsUrl() => DetailsUrl;

        public CheckSuiteDetails? GetCheckSuite() => CheckSuite;
    }

    /// <summary>Holds check suite details.</summary>
    public class CheckSuiteDetails
    {
        public CheckSuiteDetails(JsonElement json)
        {
            Commit =
                json.TryGetProperty("commit", out JsonElement commit)
                && commit.ValueKind == JsonValueKind.Object
                    ? new CommitSha(commit)
                    : null;
            App =
                json.TryGetProperty("app", out JsonElement app)
                && app.ValueKind == JsonValueKind.Object
                    ? new AppDetails(app)
                    : null;
        }

        public CommitSha? Commit { get; }

        public AppDetails? App { get; }

        public CommitSha? GetCommit() => Commit;

        public AppDetails? GetApp() => App;
    }

    /// <summary>Commit SHA wrapper.</summary>
    public class CommitSha
    {
        public CommitSha(JsonElement json) => Oid = GetString(json, "oid");

        public string? Oid { get; }

        public string? GetOid() => Oid;
    }

    /// <summary>GitHub App metadata nested in a check suite.</summary>
    public class AppDetails
    {
        public AppDetails(JsonElement json)
        {
            DatabaseId =
                json.TryGetProperty("databaseId", out JsonElement dbId)
                && dbId.ValueKind == JsonValueKind.Number
                    ? dbId.GetInt32()
                    : null;
            Name = GetString(json, "name");
            Slug = GetString(json, "slug");
        }

        public int? DatabaseId { get; }

        public string? Name { get; }

        public string? Slug { get; }

        public int? GetDatabaseId() => DatabaseId;

        public string? GetName() => Name;

        public string? GetSlug() => Slug;
    }

    private static string? GetString(JsonElement json, string property) =>
        json.TryGetProperty(property, out JsonElement value)
        && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
}
