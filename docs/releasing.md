# Releasing ChessRealms.Engine

Releases are explicit and tag-driven. Pull requests and pushes to `main` run
ordinary CI, but only a validated `v*` tag can reach the protected NuGet
publication job. A manual run of the release workflow is always a non-publishing
dry run.

## One-time external setup

Create the GitHub Environment `nuget-production`. Require a reviewer for
production publication, restrict deployments to tags matching the release
policy, and prevent administrator bypass where the repository plan supports
those controls. Define the GitHub Actions environment variable `NUGET_USER` as
the nuget.org username/profile that owns or can publish `ChessRealms.Engine`.
It is a profile name, not an email address or API key; do not store NuGet
credentials in the repository.

On nuget.org, create a Trusted Publishing policy with:

- GitHub owner: `ChessRealms`
- Repository: `Engine`
- Workflow filename: `release.yml`
- Environment: `nuget-production`
- Package scope: `ChessRealms.Engine`

The workflow requests an OIDC token only in the protected publish job and
exchanges it for a short-lived NuGet credential immediately before publication.

## Release procedure

1. Decide the SemVer increment.
2. Open a release PR that updates `VersionPrefix` in
   `src/ChessRealms.Engine/ChessRealms.Engine.csproj`.
3. Merge the release PR after CI succeeds.
4. Create the matching tag on that merged `main` commit, for example `v1.0.0`.
5. Push the tag.
6. Review the completed package validation and approve the
   `nuget-production` environment deployment.
7. Verify NuGet indexing and the generated GitHub Release and its attachments.

The workflow requires the tag commit to be reachable from `main`, requires the
tag's stable version core to equal `VersionPrefix`, and packages with the exact
validated tag version. It publishes the already validated artifact rather than
building again. The GitHub Release is created only after NuGet accepts the
package.

For a safe rehearsal, dispatch the `Release` workflow manually. Leave the
version blank to use `VersionPrefix`, or enter an allowed package version without
the leading `v`; its stable core must still match `VersionPrefix`. Manual runs
validate, restore, build, test, pack, inspect, and consume the package, but cannot
publish or create a GitHub Release.

## Versioning policy

- PATCH releases contain backward-compatible fixes and internal improvements.
- MINOR releases add backward-compatible public features.
- MAJOR releases contain breaking public API or behavioral contract changes.
- Prereleases use `alpha.N`, `beta.N`, or `rc.N`, such as `v1.1.0-alpha.1`,
  `v1.1.0-beta.1`, or `v1.1.0-rc.1`.
- The Git tag includes `v`; the NuGet version excludes it.

Versions are chosen deliberately in the release PR. Commit messages do not
increment versions automatically.

A prerelease uses the future stable version in `VersionPrefix`: for example,
`VersionPrefix` `1.1.0` pairs with tag `v1.1.0-rc.1`. A hotfix branches from the
appropriate supported state, applies the fix, and merges the release change to
`main`; then tag the merged `main` commit with the incremented PATCH version.
Never move or reuse a published version tag.

## Failures and reruns

Package version collisions fail publication; the workflow deliberately does not
use `--skip-duplicate`. It also refuses to overwrite an existing GitHub Release.
If publication fails, diagnose the cause and rerun only after confirming that
the package version was not accepted by NuGet. If NuGet publication succeeds but
GitHub Release creation fails before a release is created, use GitHub's
**Re-run failed jobs** action so the successful publish job is not repeated. The
release job will reuse the retained workflow artifact. Do not rerun all jobs
after a successful NuGet publication. If a failed operation nevertheless created
an incomplete GitHub Release, the workflow will refuse to overwrite it; inspect
and repair it deliberately, or remove the incomplete release before rerunning.
If NuGet accepted the package but the publish step itself was reported as failed,
do not retry publication: create the GitHub Release deliberately from the
retained, checksum-verified workflow artifacts.

After the first successful `1.0.0` publication is available from the configured
package source, add `PackageValidationBaselineVersion` `1.0.0` as an immediate
follow-up. It is intentionally not configured beforehand because restore and
package validation must not depend on an unpublished baseline.
