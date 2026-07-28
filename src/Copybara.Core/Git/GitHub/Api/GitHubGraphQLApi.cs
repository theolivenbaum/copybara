/*
 * Copyright (C) 2023 Google Inc.
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
using System.Globalization;
using System.Text.Json.Serialization;
using Copybara.Common;
using Copybara.Exceptions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using ProfilerT = Copybara.Profiler.Profiler;

namespace Copybara.Git.GitHub.Api;

/// <summary>GraphQL implementation for GitHub client.</summary>
public class GitHubGraphQLApi
{
    private static readonly ILogger Logger = NullLogger.Instance;

    private const int CheckRunsMaxPageSize = 10;

    private readonly IGitHubApiTransport _transport;
    private readonly ProfilerT _profiler;

    public GitHubGraphQLApi(IGitHubApiTransport transport, ProfilerT profiler)
    {
        _transport = Preconditions.CheckNotNull(transport);
        _profiler = Preconditions.CheckNotNull(profiler);
    }

    /// <summary>GraphQL request body.</summary>
    public class GraphQLRequest
    {
        [JsonPropertyName("query")]
        public string? Query { get; set; }

        [JsonPropertyName("variables")]
        public Dictionary<string, object>? Variables { get; set; }

        public GraphQLRequest(string query, Dictionary<string, object> variables)
        {
            Query = query;
            Variables = variables;
        }

        public GraphQLRequest()
        {
        }

        public string? GetQuery() => Query;

        public Dictionary<string, object>? GetVariables() => Variables;

        public override string ToString() =>
            $"GraphQLRequest{{variables={Variables}, query={Query}}}";
    }

    /// <summary>Sets GraphQL first parameters for the getCommitHistory call.</summary>
    public class GetCommitHistoryParams
    {
        private readonly int _commits;
        private readonly int _pullRequests;
        private readonly int _reviews;

        public GetCommitHistoryParams()
        {
        }

        public GetCommitHistoryParams(int commits, int pullRequests, int reviews)
        {
            _commits = commits;
            _pullRequests = pullRequests;
            _reviews = reviews;
        }

        public int GetCommits() => _commits;

        public int GetPullRequests() => _pullRequests;

        public int GetReviews() => _reviews;

        public GetCommitHistoryParams GetCopyWithCommits(int commits) =>
            new(commits, _pullRequests, _reviews);
    }

    public async Task<CommitHistoryResponse> GetCommitHistoryAsync(
        string org, string repo, string branch, GetCommitHistoryParams @params)
    {
        ValidationException.CheckCondition(
            !string.IsNullOrEmpty(org)
            && !string.IsNullOrEmpty(repo)
            && !string.IsNullOrEmpty(branch),
            "Attempted to query for GitHub commit history, but received a empty/null value: org={0},"
            + " repo={1}, branch={2}",
            org,
            repo,
            branch);

        const string getCommitHistoryQuery =
            "query ($repoName: String!, $repoOwner:String!, $branch: String!,"
            + "$numberOfCommits: Int, $numberOfPRs: Int, "
            + "$numberOfReviews: Int) {\n"
            + "repository(name: $repoName, owner: $repoOwner) {\n"
            + "ref(qualifiedName: $branch) {\n"
            + "target {\n"
            + "... on Commit {\n"
            + "id\n"
            + "history(first: $numberOfCommits) {\n"
            + "nodes {\n"
            + "id\n"
            + "oid\n"
            + "associatedPullRequests(first: $numberOfPRs) {\n"
            + "edges {\n"
            + "node {\n"
            + "title\n"
            + "mergedBy {\n"
            + "login\n"
            + "}\n"
            + "author {\n"
            + "login\n"
            + "}\n"
            + "reviewDecision\n"
            + "latestOpinionatedReviews(first: $numberOfReviews)"
            + "{\n"
            + "edges {\n"
            + "node {\n"
            + "author {\n"
            + "login\n"
            + "}\n"
            + "state\n"
            + "}\n"
            + "}\n"
            + "}\n"
            + "}\n"
            + "}\n"
            + "}\n"
            + "}\n"
            + "}\n"
            + "}\n"
            + "}\n"
            + "}\n"
            + "}\n";

        var variables = new Dictionary<string, object>
        {
            ["repoOwner"] = org,
            ["repoName"] = repo,
            ["branch"] = branch,
            ["numberOfCommits"] = @params.GetCommits(),
            ["numberOfPRs"] = @params.GetPullRequests(),
            ["numberOfReviews"] = @params.GetReviews(),
        };

        using ProfilerTaskScope ignore = new(_profiler.Start("github_api_get_commit_history"));
        return (await _transport.PostAsync<CommitHistoryResponse>(
            "/graphql",
            new GraphQLRequest(getCommitHistoryQuery, variables),
            "POST GraphQL").ConfigureAwait(false))!;
    }

    /// <summary>
    /// Returns the check runs for <paramref name="sha"/> whose name matches one of
    /// <paramref name="checkNames"/>, by issuing a single GraphQL query with one server-side
    /// <c>checkRuns(filterBy: {checkName: …})</c> connection per requested name.
    /// </summary>
    public async Task<ImmutableArray<CheckRun>> GetCheckRunsByNameFilterAsync(
        string owner, string repo, string sha, IReadOnlySet<string> checkNames)
    {
        if (checkNames.Count == 0)
        {
            return ImmutableArray<CheckRun>.Empty;
        }

        // Generate filters for each check name.
        var checkRunFilters = new List<string>();
        int idx = 0;
        foreach (string checkName in checkNames)
        {
            checkRunFilters.Add(
                string.Format(
                    CultureInfo.InvariantCulture,
                    """
                            filter_{0}: checkRuns(first: 100, filterBy: {{checkName: "{1}"}}) {{
                              nodes {{
                                id
                                name
                                status
                                conclusion
                                detailsUrl
                                checkSuite {{
                                  commit {{
                                    oid
                                  }}
                                  app {{
                                    databaseId
                                    name
                                    slug
                                  }}
                                }}
                              }}
                            }}
                    """,
                    idx++,
                    checkName));
        }

        string query =
            string.Format(
                CultureInfo.InvariantCulture,
                """
                    query ($owner: String!, $repo: String!, $sha: String!, $suiteCursor: String) {{
                      repository(owner: $owner, name: $repo) {{
                        object(expression: $sha) {{
                          ... on Commit {{
                            associatedPullRequests(first: 1) {{
                              nodes {{
                                number
                              }}
                            }}
                            checkSuites(first: 100, after: $suiteCursor) {{
                              pageInfo {{
                                hasNextPage
                                endCursor
                              }}
                              nodes {{
                                {0}
                              }}
                            }}
                          }}
                        }}
                      }}
                    }}
                """,
                string.Join("\n", checkRunFilters));

        var checkRuns = ImmutableArray.CreateBuilder<CheckRun>();
        bool hasNextPage = true;
        string? cursor = null;
        int pagesQueried = 0;
        while (hasNextPage && pagesQueried < CheckRunsMaxPageSize)
        {
            using ProfilerTaskScope ignore =
                new(_profiler.Start("github_api_get_check_runs_history"));
            var variables = new Dictionary<string, object>
            {
                ["owner"] = owner,
                ["repo"] = repo,
                ["sha"] = sha,
            };
            if (cursor != null)
            {
                variables["suiteCursor"] = cursor;
            }

            Logger.LogInformation("Querying check runs with variables: {Variables}", variables);
            GetFilteredCheckRunsResponse? response =
                await _transport.PostAsync<GetFilteredCheckRunsResponse>(
                    "/graphql",
                    new GraphQLRequest(query, variables),
                    "POST GraphQL").ConfigureAwait(false);
            pagesQueried++;

            if (response?.GetData()?.GetRepository()?.GetObject() == null)
            {
                Logger.LogInformation(
                    "Response was unexpectedly null for GetCheckRunsByNameFilter(owner: {Owner},"
                        + " repo: {Repo}, sha: {Sha}, checkNames: {CheckNames})",
                    owner,
                    repo,
                    sha,
                    checkNames);
                break;
            }

            checkRuns.AddRange(GetCheckRunsFromResponse(response, sha));

            // Update cursor for next page of check suites.
            var checkSuites = response.GetData()!.GetRepository()!.GetObject()!.GetCheckSuites();
            if (checkSuites == null)
            {
                break;
            }
            var pageInfo = checkSuites.GetPageInfo();
            if (pageInfo != null && pageInfo.HasNextPage())
            {
                cursor = pageInfo.GetEndCursor();
            }
            else
            {
                hasNextPage = false;
            }
        }

        return checkRuns.ToImmutable();
    }

    private static ImmutableArray<CheckRun> GetCheckRunsFromResponse(
        GetFilteredCheckRunsResponse? response, string sha)
    {
        var commitObj = response?.GetData()?.GetRepository()?.GetObject();
        if (commitObj == null)
        {
            return ImmutableArray<CheckRun>.Empty;
        }

        var pullRequests = ImmutableArray<CheckRun.CheckRunPullRequest>.Empty;
        var prNodes = commitObj.GetAssociatedPullRequests()?.GetNodes();
        if (prNodes != null)
        {
            pullRequests =
                prNodes
                    .Select(prNode => new CheckRun.CheckRunPullRequest(prNode.GetNumber()))
                    .ToImmutableArray();
        }

        var checkSuites = commitObj.GetCheckSuites();
        if (checkSuites == null)
        {
            return ImmutableArray<CheckRun>.Empty;
        }

        var checkRuns = ImmutableArray.CreateBuilder<CheckRun>();
        foreach (var suiteNode in checkSuites.GetNodes() ?? new List<GetFilteredCheckRunsResponse.CheckSuiteNode>())
        {
            foreach (var runNode in suiteNode.GetCheckRuns())
            {
                checkRuns.Add(ConvertToCheckRun(runNode, sha, pullRequests));
            }
        }
        return checkRuns.ToImmutable();
    }

    private static CheckRun ConvertToCheckRun(
        GetFilteredCheckRunsResponse.CheckRunNode runNode,
        string commitSha,
        ImmutableArray<CheckRun.CheckRunPullRequest> pullRequests)
    {
        // Upstream throws on a status GitHub's GraphQL schema has but the REST enum doesn't (e.g.
        // WAITING, REQUESTED); treat those as PENDING rather than failing the whole migration.
        CheckRunStatus apiStatus =
            runNode.GetStatus() == null
            || !Enum.TryParse(runNode.GetStatus(), ignoreCase: true, out CheckRunStatus parsedStatus)
                ? CheckRunStatus.PENDING
                : parsedStatus;

        CheckRunConclusion? apiConclusion = null;
        if (runNode.GetConclusion() != null)
        {
            string normalized = runNode.GetConclusion()!.Replace("_", "");
            apiConclusion =
                Enum.TryParse(normalized, ignoreCase: true, out CheckRunConclusion parsedConclusion)
                    ? parsedConclusion
                    : CheckRunConclusion.NONE;
        }

        GitHubApp? apiApp = null;
        var checkSuite = runNode.GetCheckSuite();
        if (checkSuite != null)
        {
            string? oid = checkSuite.GetCommit()?.GetOid();
            if (oid != null)
            {
                commitSha = oid;
            }
            var app = checkSuite.GetApp();
            if (app != null)
            {
                apiApp = new GitHubApp(app.GetDatabaseId() ?? 0, app.GetSlug(), app.GetName());
            }
        }

        return new CheckRun
        {
            DetailUrl = runNode.GetDetailsUrl(),
            Status = apiStatus,
            Conclusion = apiConclusion,
            Sha = commitSha,
            Name = runNode.GetName(),
            App = apiApp,
            Output = null,
            PullRequests = pullRequests.ToList(),
        };
    }

    private readonly struct ProfilerTaskScope : IDisposable
    {
        private readonly ProfilerT.ProfilerTask _task;

        public ProfilerTaskScope(ProfilerT.ProfilerTask task) => _task = task;

        public void Dispose() => _task.Close();
    }
}
