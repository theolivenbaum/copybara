/*
 * Copyright (C) 2016 Google LLC
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
using Copybara.Common;
using Copybara.Exceptions;
using Copybara.Util;

namespace Copybara.Git;

/// <summary>
/// Common arguments for <see cref="GitDestination"/>, <see cref="GitOrigin"/>, and other Git
/// components. Port of <c>com.google.copybara.git.GitOptions</c>.
/// </summary>
public class GitOptions : IOption
{
    public const string UseCredentialsFromConfig = "--use-credentials-from-config";

    private readonly GeneralOptions _generalOptions;
    private string? _partialCacheFilePrefix;

    public string? GetCredentialHelperStorePath() => CredentialHelperStorePath;

    [Flag(
        "--git-cmd-config",
        "This is a repeatable flag used to set command level configurations, currently only applies"
            + " to git merge operations.")]
    internal Dictionary<string, string> GitOptionsParams { get; set; } = new();

    [Flag(
        "--git-http-follow-redirects",
        "Whether git should follow HTTP redirects.")]
    internal string? HttpFollowRedirects { get; set; }

    [Flag(
        "--git-push-option",
        "This is a repeatable flag used to set git push level flags to send to git servers.")]
    internal List<string> GitPushOptions { get; set; } = new();

    [Flag(
        "--allowed-git-push-options",
        "This is a flag used to allowlist push options sent to git servers.")]
    internal List<string>? AllowedGitPushOptions { get; set; }

    [Flag(
        "--git-credential-helper-store-file",
        "Credentials store file to be used. See https://git-scm.com/docs/git-credential-store")]
    public string? CredentialHelperStorePath { get; set; }

    [Flag(
        "--nogit-credential-helper-store",
        "Disable using credentials store. See https://git-scm.com/docs/git-credential-store")]
    internal bool NoCredentialHelperStore { get; set; }

    [Flag(
        "--nogit-prompt",
        "Disable username/password prompt and fail if no credentials are found.")]
    public bool NoGitPrompt { get; set; }

    [Flag(
        "--git-visit-changes-page-size",
        "Size of the git log page used for visiting changes.",
        Hidden = true)]
    internal int VisitChangePageSize { get; set; } = 200;

    [Flag("--git-tag-overwrite", "If set, copybara will force update existing git tag")]
    internal bool GitTagOverwrite { get; set; }

    [Flag(
        "--experiment-checkout-affected-files",
        "If set, copybara will only checkout affected files at git origin. Note that this is"
            + " experimental.")]
    internal bool ExperimentCheckoutAffectedFiles { get; set; }

    [Flag(
        "--git-no-verify",
        "Pass the '--no-verify' option to git pushes and commits to disable git commit hooks.")]
    public bool GitNoVerify { get; set; }

    [Flag(
        UseCredentialsFromConfig,
        "If the config includes credentials, use these.",
        Hidden = true,
        Arity = 1)]
    public bool UseConfigCredentials { get; set; }

    [Flag(
        "--workflow-credential-helper-path",
        "Path to store the credential helper created from supplied credentials.",
        Hidden = true)]
    public string? WorkflowCredentialHelperPath { get; set; }

    [Flag(
        "--git-origin-fetch-depth",
        "Use a shallow clone of the specified depth for git.origin.")]
    internal int? FetchDepth { get; set; }

    [Flag(
        "--git-ls-remote-limit",
        "Limit the number of ls-remote rows is visible to Copybara.")]
    private int GitLsRemoteLimit { get; set; } = int.MaxValue;

    public int? GetFetchDepth() => FetchDepth;

    /// <summary>Credential helper file path for config-based creds.</summary>
    public string GetConfigCredsFile(GeneralOptions generalOpts)
    {
        WorkflowCredentialHelperPath ??= generalOpts.GetDirFactory().NewTempDir("creds");
        return Path.Combine(WorkflowCredentialHelperPath, ".cred_helper");
    }

    public GitOptions(GeneralOptions generalOptions)
    {
        _generalOptions = Preconditions.CheckNotNull(generalOptions);
    }

    private GitOptions(GeneralOptions generalOptions, string? partialCacheFilePrefix)
    {
        _generalOptions = Preconditions.CheckNotNull(generalOptions);
        _partialCacheFilePrefix = partialCacheFilePrefix;
    }

    public string GetRepoStorage() => _generalOptions.GetDirFactory().GetCacheDir("git_repos");

    public GitRepository CachedBareRepoForUrl(string url) =>
        CachedBareRepoForUrl(url, fetchUrl: url);

    /// <summary>
    /// Returns a newly initialized bare repository created at a cache location resolved from
    /// <paramref name="cacheUrl"/>, additionally validating the repository object format against
    /// the remote <paramref name="fetchUrl"/>.
    /// </summary>
    /// <param name="cacheUrl">the url used to resolve the local directory name in the cache.</param>
    /// <param name="fetchUrl">
    /// the remote url used to check the repository object format, or null to skip the check.
    /// </param>
    public GitRepository CachedBareRepoForUrl(string cacheUrl, string? fetchUrl)
    {
        Preconditions.CheckNotNull(cacheUrl);
        try
        {
            return CreateBareRepo(
                _generalOptions, FileUtil.ResolveDirInCache(cacheUrl, GetRepoStorage()), fetchUrl);
        }
        catch (IOException e)
        {
            throw new RepoException("Cannot create a cached repo for " + cacheUrl, e);
        }
    }

    /// <summary>Create a newly initialized repository from the cached location.</summary>
    public GitRepository CachedBareRepoForUrl(string url, IGitRepositoryHook? gitRepositoryHook) =>
        CachedBareRepoForUrl(url, fetchUrl: url, gitRepositoryHook);

    /// <summary>
    /// Returns a newly initialized bare repository created at a cache location resolved from
    /// <paramref name="cacheUrl"/> using the specified checkout hook, additionally validating the
    /// repository object format against the remote <paramref name="fetchUrl"/>.
    /// </summary>
    public GitRepository CachedBareRepoForUrl(
        string cacheUrl, string? fetchUrl, IGitRepositoryHook? gitRepositoryHook)
    {
        Preconditions.CheckNotNull(cacheUrl);
        try
        {
            return CreateBareRepo(
                _generalOptions,
                FileUtil.ResolveDirInCache(cacheUrl, GetRepoStorage()),
                gitRepositoryHook,
                fetchUrl);
        }
        catch (IOException e)
        {
            throw new RepoException("Cannot create a cached repo for " + cacheUrl, e);
        }
    }

    /// <summary>Rewrite url for submodule fetch.</summary>
    public virtual string RewriteSubmoduleUrl(string url) => url;

    /// <summary>Returns a <see cref="GitEnvironment"/> configured for the given options.</summary>
    public GitEnvironment GetGitEnvironment(IReadOnlyDictionary<string, string> env) =>
        new(env, NoGitPrompt);

    /// <summary>Create a new initialized repository in the location.</summary>
    public GitRepository CreateBareRepo(GeneralOptions generalOptions, string path) =>
        CreateBareRepo(generalOptions, path, fetchUrl: null);

    /// <summary>
    /// Create a new initialized repository in the location, checking and configuring the repository
    /// object format to match the remote <paramref name="fetchUrl"/> (null to skip the check).
    /// </summary>
    public virtual GitRepository CreateBareRepo(
        GeneralOptions generalOptions, string path, string? fetchUrl)
    {
        GitRepository repo =
            GitRepository.NewBareRepo(
                path,
                GetGitEnvironment(generalOptions.GetEnvironment()),
                generalOptions.IsVerbose(),
                generalOptions.RepoTimeout,
                GitNoVerify,
                GetPushOptionsValidator());
        return InitRepo(repo, fetchUrl);
    }

    /// <summary>Create a new initialized repository in the location.</summary>
    public GitRepository CreateBareRepo(
        GeneralOptions generalOptions, string path, IGitRepositoryHook? gitRepositoryHook) =>
        CreateBareRepo(generalOptions, path, gitRepositoryHook, fetchUrl: null);

    /// <summary>
    /// Create a new initialized repository in the location with the specified checkout hook,
    /// checking and configuring the repository object format to match the remote
    /// <paramref name="fetchUrl"/> (null to skip the check).
    /// </summary>
    public GitRepository CreateBareRepo(
        GeneralOptions generalOptions,
        string path,
        IGitRepositoryHook? gitRepositoryHook,
        string? fetchUrl) =>
        // TODO(port): NewBareRepo has no gitRepositoryHook overload yet, so the hook is dropped here
        // just as it was before fetchUrl was threaded through.
        CreateBareRepo(generalOptions, path, fetchUrl);

    public GitRepository InitRepo(GitRepository repo) => InitRepo(repo, fetchUrl: null);

    public virtual GitRepository InitRepo(GitRepository repo, string? fetchUrl)
    {
        repo.Init(fetchUrl);
        if (NoCredentialHelperStore)
        {
            return repo;
        }
        string? storePath = GetCredentialHelperStorePath();
        string path = storePath == null ? "" : " --file=" + storePath;
        repo.WithCredentialHelper("store" + path);
        repo.ReplaceLocalConfigField("fetch", "prune", "false");
        if (!string.IsNullOrEmpty(HttpFollowRedirects))
        {
            repo.WithHttpFollowRedirectsOption(HttpFollowRedirects);
        }
        return repo;
    }

    public string? GetPartialCacheFilePrefix() => _partialCacheFilePrefix;

    /// <summary>Returns the limit for the number of ls-remote rows to output.</summary>
    public int GetGitlsRemoteLimit() => GitLsRemoteLimit;

    public GitOptions SetPartialCacheFilePrefix(string partialCacheFilePrefix) =>
        new(_generalOptions, partialCacheFilePrefix);

    public GitRepository.PushOptionsValidator GetPushOptionsValidator()
    {
        // If unset, return an unset allowlist which means all options are a go. Not to be confused
        // with an empty allow list.
        if (AllowedGitPushOptions == null)
        {
            return new GitRepository.PushOptionsValidator(null);
        }
        return new GitRepository.PushOptionsValidator(AllowedGitPushOptions.ToImmutableArray());
    }

    /// <summary>Sets the limit for the number of ls-remote rows to fetch. Only used for testing.</summary>
    public void SetGitlsRemoteLimit(int limit) => GitLsRemoteLimit = limit;
}
