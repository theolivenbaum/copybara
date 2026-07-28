# TODO — Copybara .NET 10 Port

Living work breakdown. Status legend: ✅ done · 🚧 in progress · ⬜ pending ·
🔬 needs investigation.

The port is large (~90k LOC engine + ~31k LOC Starlark interpreter). It is
organized in dependency order: foundation first, then the Starlark interpreter,
then the domain modules that depend on both.

---

## Phase 0 — Project setup

- ✅ Move original Java/Bazel tree into `java/` (reference only).
- ✅ Author `CLAUDE.md` and `TODO.md`.
- ✅ Create solution + project scaffold (`Copybara.slnx`):
  - `src/Copybara.Common`, `src/Starlark`, `src/Copybara.Core`,
    `src/Copybara.Cli` (`PackAsTool=true`, verified `dotnet pack` produces a tool
    package), `tests/Copybara.Tests`.
- ✅ Wire NuGet deps: `LibGit2Sharp`, `Microsoft.Extensions.Logging`, xUnit,
  FluentAssertions (`System.Collections.Immutable`/`System.Text.Json` are in-box).
- ✅ `.gitignore` for .NET (`bin/`, `obj/`, `*.user`, `nupkg/`).
- ✅ Whole solution builds clean (0 warnings/0 errors); 8 smoke tests pass.
- ⬜ CI: `dotnet build` + `dotnet test` GitHub Action.

## Phase 1 — Foundation (`Copybara.Common` + `Copybara.Core` primitives)

No Starlark dependency. Port these first; they unblock everything else.

- ✅ `Copybara.Common`: `Preconditions`, `ImmutableListMultimap<K,V>` + builder.
  (String helpers added ad hoc where needed.)
- ✅ `exception/` → `Copybara.Exceptions` — all 13 classes ported with correct
  hierarchy; `ValidationException.CheckCondition` handles printf-style `%s`
  format strings via an internal `%`→`{}` translator.
- ✅ `util/` core (subset) → `Copybara.Util`: `ExitCode`, full `Glob` subsystem
  (`Glob`/`GlobAtom`/`SequenceGlob`/`ReadablePathMatcher`/`GlobPathMatcher`/
  `IPathMatcher`), `FileUtil`, `DirFactory`, `Identity`, `TablePrinter`,
  `CommandOutput(WithStatus)`, `CommandRunner` + shell stack.
  - ⬜ Still TODO in util: `DiffUtil`, `CommandLineDiffUtil`, `MergeImportTool`,
    `AutoPatchUtil`, `ConsistencyFile`, `ApplyDestinationPatch`, `RenameDetector`,
    `ScpUtil`, `OriginUtil`, `RepositoryUtil`, `EnumMapConverter`.
- ✅ `util/console/` → `Copybara.Util.Console` — all 17 files (`Console`,
  `AnsiConsole`, `LogConsole`, `FileConsole`, `NoPromptConsole`,
  `CapturingConsole`, `MultiplexingConsole`, `PrefixConsole`, `Consoles`, …).
- ✅ `revision/` → `Copybara.Revision`: `IRevision`, `Change<R>`, `Changes`, `OriginRef`.
- ✅ `authoring/` → `Copybara.Authoring`: `Author`, `AuthorParser`, `Authoring`,
  `InvalidAuthorException` (as Starlark values).
- ✅ `templatetoken/` → `Copybara.TemplateToken`.
- ⬜ `profiler/` → `Copybara.Profiler`.

## Phase 2 — Starlark interpreter (`src/Starlark`)

Port of `java/third_party/bazel/main/java/net/starlark/java`. This is a
self-contained interpreter and the critical dependency for config loading.
Sub-packages: `annot`, `syntax` (lexer/parser/AST), `eval` (values, evaluator,
builtins), `lib` (json, proto, etc.), `spelling`.

- ✅ `annot/` — `[StarlarkBuiltin]`, `[StarlarkMethod]`, `[Param]`, `[ParamType]`.
- ✅ `syntax/` — `Lexer`, `Parser`, all AST node types, `Location`, `TokenKind`,
  `FileOptions`, `SyntaxError`, `NodeVisitor`, `StarlarkFile`/`Program`, plus the
  resolver/type-system subset (`Resolver`, `Types`, `StarlarkType`, `TypeChecker`,
  `TypeTagger`, `TypeConstructor`, `TypeContext`).
- 🚧 `eval/` — DONE: value types (`StarlarkInt/Float/List/Tuple/Dict/RangeList`,
  `Sequence`), `Mutability`, `StarlarkSemantics`, `Printer`, `Module`,
  `StarlarkThread`, `EvalUtils`, `StarlarkValue`/`NoneType`/`EvalException`/
  `Starlark` helpers, callable interfaces.
  - ✅ Evaluator + dispatch: `Eval` tree-walker, reflective dispatch
    (`CallUtils`/`MethodDescriptor`/`ParamDescriptor`/`BuiltinFunction`/
    `StarlarkFunction`) mapping `[StarlarkMethod]`→calls, `MethodLibrary` +
    `StringModule` builtins, and `Starlark.ExecFile`/`Eval`/`Call` drivers.
    Parse→resolve→execute of real Starlark works (functions, comprehensions,
    closures, builtins, string methods) — verified by xUnit `StarlarkEvalTests`.
  - ⬜ Still deferred: `StarlarkSet` + its EvalUtils operator branches, `float`
    builtin, flag-guarded params, dynamic arg/return type-checking.
- ✅ `spelling/` — `SpellChecker`.
- ⬜ `lib/json` — `Json` module (interop with `System.Text.Json`).
- ⬜ Reflection strategy: reflect over `[StarlarkMethod]`/`[Param]` at startup,
  cached per type (source generators a later optimization).
- 🔬 Validate with upstream Starlark eval tests once the evaluator lands.

## Phase 3 — Config model & core module (needs Phases 1–2)

- ✅ `config/` → `Copybara.Config`: `Config`, `ConfigFile`, `PathBasedConfigFile`,
  `MapConfigFile`, `IMigration`, `ConfigValidator`, `SkylarkParser` (wired to the
  ported interpreter), `ConfigLoader`/`IConfigLoaderProvider`, `ILabelsAwareModule`,
  `ConfigWithDependencies`.
- ✅ Top-level engine types: `IOption`/`Options`, `GeneralOptions`, `ModuleSet`/
  `ModuleSupplier`, `CoreModule`/`CoreGlobal`, `Workflow<O,D>` (+ non-generic
  `Workflow.Create` factory), `WorkflowOptions`, `WorkflowMode`, `WorkflowRunHelper`,
  `IMigration`, `IOrigin`/`IDestination`, `ITransformation`, `TransformWork`,
  `TransformResult`, `CheckoutPath`, `CheckoutFileSystem`, `Metadata`, `Info`,
  `MigrationInfo`, plus `profiler/`, `effect/`, `monitor/`, `action/`, `approval/`,
  `treestate/`, `version/`.

## Phase 4 — Transformations (`transform/`)

- ✅ `Replace`, `CopyOrMove`/`Remove`, `Sequence`, `ExplicitReversal`,
  `IReversibleFunction`, `SkylarkConsole`, `FilterReplace`, `VerifyMatch`,
  `TodoReplace`, `SkylarkTransformation`, `transform/metadata/*`, `transform/debug/*`.
- ⬜ `transform/patch/*` (needs DiffUtil/patch tooling).

## Phase 5 — Git support (`git/`) — uses LibGit2Sharp / git CLI

Largest single module (~175 files). Port in slices:

- ✅ Core plumbing: `GitRepository` (git-CLI-backed via `CommandRunner`, faithful
  to upstream which shells out), `GitRevision`, `GitRepoType`, `GitEnvironment`,
  `GitCredential`, `Refspec`, `FetchResult`, `MergeResult`, `IntegrateLabel`,
  `SameGitTree`, exceptions.
- ✅ Origin/Destination base: `GitOrigin`, `GitDestination`, `GitDestinationReader`,
  `ChangeReader`, `GitVisitorUtil`, `Mirror`, `GitIntegrateChanges`, options, write hooks.
- ✅ `GitModule` (the `git` Starlark module — all 19 git.* factories wired).
- ✅ GitHub: `github/api` client (~50 files) + providers (`GitHubPrOrigin`,
  `GitHubPrDestination`, `GitHubEndPoint`, write hooks, approvals validators, `github/util`).
- ✅ Gerrit: `gerritapi` client (30) + providers (`GerritOrigin`/`GerritDestination`/`GerritEndpoint`).
- ✅ GitLab: `gitlab/api` client (18) + providers (`GitLabMrOrigin`/`GitLabMrDestination`).
- ✅ `git/version/` selectors. ✅ `hg/` (Mercurial).

## Phase 6 — Other origins/destinations & modules

- ✅ `folder/` (FolderOrigin/FolderDestination/FolderModule) — first ported origin/destination pair.
- ✅ `remotefile/`, `archive/` (zip/tar/gzip; xz/bz2 = TODO), `hashing/`,
  `http/` (HttpClient), `format/` (buildifier), `buildozer/`, `toml/`, `json/`,
  `xml/`, `html/`, `re2/`, `credentials/`, `approval/`, `action/`, `treestate/`,
  `monitor/`, `effect/`, `checks/` (minimal stub).
- ✅ `hg/` (Mercurial), `go/`, `rust/`, `python/`, `tsjs/` (npm).
- ✅ `feedback/`, `checks/`, `regenerate/`, `onboard/`, `configgen/`,
  `doc/` (reflection-based reference generator), `transform/patch/`.
- ✅ `starlark/StarlarkUtil`, `archive/util`.

## Source port: COMPLETE

Every source package under `java/com/google/copybara/**` and the vendored
`net.starlark.java` interpreter has a C# counterpart. ~666 C# files / ~98.5k LOC.
Whole solution builds 0 warnings / 0 errors; tests pass.

Intentionally NOT ported (superseded/obsolete): `jcommander/*` converters/validators
(replaced by the custom `Copybara.Cli.ArgParser`).

### Remaining integration / follow-up work (not source-porting)
- **Wire `ModuleSupplier.GetModules()`** to register the ported Starlark modules
  (`Core`, `git`, `folder`, `format`, `http`, `hashing`, `archive`, `remotefile`,
  `toml`/`json`/`xml`/`html`/`re2`, `go`/`rust`/`python`/`npm`, `credentials`, …)
  and `NewOptions()` to register every `IOption`. Currently stubbed empty — this is
  what makes `copybara migrate` load a real `copy.bara.sky` end-to-end.
- **Wire CLI commands**: `RegenerateCmd`/`OnboardCmd`/`GeneratorCmd` engines exist in
  Core with `Run(...)` entry points; add thin `ICopybaraCmd` adapters in `Copybara.Cli`.
- **Archive xz/bz2** (`TAR_XZ`/`TAR_BZ2`) need a codec (no in-box option) — currently
  throw with a `TODO(port)`.
- **Expand test coverage**: port high-value suites from `java/javatests/` and add an
  end-to-end folder→folder `migrate` smoke test.
- Verify a few `structField`/reflective-dispatch edge cases against real configs.

## Phase 7 — CLI (`src/Copybara.Cli`)

- ✅ Arg parsing (custom `ArgParser` replacing JCommander, reading `[Flag]`
  attributes off option objects), matching upstream flag names.
- ✅ `Main` orchestration (mirrors `Main.java`): console setup, logging config,
  module set creation, command dispatch, exit codes, error handling.
- ✅ Commands: `MigrateCmd`, `InfoCmd`, `ValidateCmd` (+ version/help).
- ⬜ Commands not yet ported: `RegenerateCmd`, `OnboardCmd`/`GeneratorCmd`.
- ✅ `PackAsTool` + full NuGet metadata (icon, readme, license expression, copyright,
  project/repository URL, tags, Source Link).
- ✅ Version reporting: `Main.GetBuildInfo()` reads
  `AssemblyInformationalVersionAttribute` (the .NET equivalent of upstream's
  `/build-data.properties`), so `copybara version` reports the packed version plus the
  commit SHA when Source Link is on.
- ✅ CI: `.devops/build-nuget.yml` (Azure DevOps) — CalVer `yy.M.<buildId>`, restore →
  build → test → pack the tool → push to nuget.org via the `nuget-curiosity-org`
  service connection. Mirrors `curiosity-ai/hnsw-sharp`'s pipeline.

## Phase 8 — Tests, docs, polish

- ⬜ Port high-value tests from `javatests/` (glob, author, replace, workflow,
  git origin/destination round-trips).
- ⬜ End-to-end smoke test: folder→folder migrate with a `core.replace`.
- ⬜ Reference doc generation parity (optional).
- ⬜ Performance pass; RE2 semantics decision.

---

## Upstream sync log

Tracks merges of `google/copybara` master into `java/` and what each one implied for
the C# port.

### Sync: `80d188e` → `5be2789` (23 commits, 2026-07)

Ported to C#:

- `055fac9` empty-root glob/matcher fix — `GlobAtom.GetRelativePath` and
  `ReadablePathMatcher.RelativeGlob` already guarded the empty root (string paths), so
  the only live bug was `DestinationStatusVisitor` prepending `"/"` to change files;
  that prefix is gone. Regression tests added.
- `f5188fb` resolve `ConsistencyFileConfiguration` before `MergeImportConfiguration` in
  `CoreModule.Workflow` so `use_consistency_file` is derived from the resolved config.
- `ef45e5b` `experimental_iterative_merge_import` deprecated: `WorkflowModeRunner`
  ignores the flag value (warns when set) and keys iterative merge-import baseline
  tracking off `IsMergeImport()`.
- `6bd7119` `GIT_INTEGRATE_FAIL_IF_COMMON_BASELINE_NOT_FOUND` default flipped to `true`
  (`GitIntegrateChanges`, `GitModule`).
- `f538bf2` `git.integrate(allow_unrelated_history = …)`.
- `662e658` `git.integrate(merge_commit_message = …)` with `${MERGE_MSG}` /
  `${SUMMARY_FROM_TRANSFORM}` / `TransformResult` labels; `LabelTemplate` gained
  value equality + `ToString`.
- `f766499` generic `GITHUB_BASE_BRANCH_SHA` label alongside the deprecated
  `GITHUB_BASE_BRANCH_SHA1`, with fallback in `GitHubPrOrigin.FindBaselinesWithoutLabel`
  and `GitHubPreSubmitApprovalsProvider`.
- `d332c42` + `ca0d8d6` object-format-aware init: `GitRepository.Init(string? fetchUrl)`
  wipes/re-inits a cached repo whose local hash algorithm disagrees with the remote;
  `GitOptions.CachedBareRepoForUrl`/`CreateBareRepo`/`InitRepo` thread `fetchUrl`
  through; `GitOrigin` and `GitDestinationOptions` pass the real remote url. SHA-1 →
  SHA doc/naming cleanup applied to user-visible Starlark docs.
- `8152f4a` `TablePrinter` `AddRow`/`Build`/`Print` guarded by a lock.
- `e8a951f` `Consoles.PrintCauseChain` collects suppressed exceptions into a mutable
  list while walking the cause chain (mapped to `AggregateException.InnerExceptions`).
- `dad187c` `PatchingOptions.QuiltRefreshPatches` gates `quilt refresh`.
- `672f04d` + `74ac397` `ConfigGenHeuristics`: destination excludes collapse into dir
  globs via a new `DestinationExcludesGlob` scorer, and secondary similar destinations
  become `core.copy` (`IGeneratorTransformation` / `GeneratorCopy`;
  `GeneratorTransformations.GetMoves()` → `AsList()`).
- `e06d08d` + `5be2789` GitHub GraphQL filtered check runs:
  `GetFilteredCheckRunsResponse` (aliased `filter_N` connections read through
  `[JsonExtensionData]`), `GitHubGraphQLApi.GetCheckRunsByNameFilterAsync`, and the
  `use_graphql_api_for_check_runs` temporary-feature path in `GitHubPrOrigin`.
  `CheckRun`/`CheckRun.CheckRunPullRequest`/`GitHubApp` gained value equality.

Deliberately NOT ported (recorded here so the next sync doesn't re-litigate it):

- `c0c4a3c` + `930549a` + `55bd624` + `94cb8c7` — re-vendoring of
  `net.starlark.java` from Bazel master, which lands the **Starlark static type
  system** (`syntax/TypeTable`, `Types`, `TypeTagger`, `TypeChecker`, `StarlarkType`,
  `TypeConstructor(Value)`, `eval/CompactImmutableDict`, `Compactable`, plus a new
  `annot/StarlarkLibrary` annotation replacing `doc/annotations/Library` and a rule
  that `@StarlarkMethod` Java names must be unique across a `@StarlarkBuiltin`
  hierarchy). ~4.7k changed/new lines in the interpreter alone, none of it required
  for `copy.bara.sky` compatibility (the type syntax is off by default via
  `FileOptions`). Port as its own phase if/when configs start using type
  annotations. The `@StarlarkBuiltin` annotations upstream added to
  `ActionContext`, `CheckoutFileSystem`, `GoVersionObject`, `PullRequestOrIssue`,
  `Repository`, `CheckRun.PullRequest` exist only to satisfy that new interpreter
  restriction and are not needed by this port's dispatcher.
- `dad187c`'s `RegenerateCmd` half — `RegenerateCmd` is not ported yet (see Phase 7).
  The knob it flips (`PatchingOptions.QuiltRefreshPatches = false`) is in place, so the
  adapter just needs to set it.
- `6c1aaa3`, `99ab4eb`, `33a7e6c`-style test-only and formatting changes.

Known gaps surfaced while reviewing this sync (pre-existing, not caused by it):

- `MergeImportTool` has no C# counterpart at all; `Workflow` references merge-import
  config but the tool itself is missing.
- `AutoPatchUtil` still does `fileMatcher.RelativeTo("").Matches("/" + name)`, which
  cannot match now that relative matchers stay relative. Upstream has the identical
  bug (it was not fixed by `055fac9`), so the port was left alone — fix both or file
  upstream.

---

## Cross-cutting decisions / open questions

- ✅ **RE2 vs .NET Regex** — DECIDED: use the native .NET regex engine
  (`System.Text.RegularExpressions`). Accepted deviation from upstream re2j.
  Flag any divergences observed during porting/testing.
- 🔬 **Async vs sync** — repo I/O is naturally async in .NET; upstream is
  blocking. Decide per-boundary; keep the engine mostly synchronous initially to
  stay close to the source, use async only at process/network edges.
- 🔬 **Path handling** — Java uses `java.nio.file.Path` + Jimfs in tests. Use
  `string` + `System.IO`, and an in-memory filesystem abstraction for tests if
  needed.
- 🔬 **git CLI dependency** — LibGit2Sharp covers most, but shallow fetch,
  some refspec and credential-helper behaviors may still require the `git`
  binary. Keep `GitEnv`/`CommandRunner` available as a fallback.
</content>
