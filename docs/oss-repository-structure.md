# OSS Repository Structure

This note records the repository structure ArcadeRelay uses for public open
source readiness.

## Research Basis

- GitHub community profiles check for files such as README, license, code of
  conduct, and contributing guidelines:
  https://docs.github.com/en/communities/setting-up-your-project-for-healthy-contributions/about-community-profiles-for-public-repositories
- GitHub supports issue templates, pull request templates, security policies,
  and support files in standard locations:
  https://docs.github.com/en/communities/using-templates-to-encourage-useful-issues-and-pull-requests/about-issue-and-pull-request-templates
- OpenSSF Best Practices emphasizes clear project description, contribution
  process, license location, release notes, vulnerability reporting, and basic
  build/test documentation:
  https://www.bestpractices.dev/en/criteria/0
- OpenSSF Scorecard evaluates security-health heuristics and is useful as a
  later measurement tool, but it is not a universal requirement:
  https://github.com/ossf/scorecard
- Semantic Versioning is the intended release scheme once the public contract is
  stable:
  https://semver.org/

## Adopted Layout

```text
README.md
LICENSE
CONTRIBUTING.md
CODE_OF_CONDUCT.md
SECURITY.md
SUPPORT.md
MAINTAINERS.md
GOVERNANCE.md
CHANGELOG.md
.github/
  CODEOWNERS
  pull_request_template.md
  ISSUE_TEMPLATE/
    bug_report.yml
    feature_request.yml
    documentation.yml
    question.yml
    security_contact.yml
    config.yml
docs/
  oss-repository-structure.md
```

## Deferred Items

- Dependabot configuration: add after package manifests or GitHub Actions
  workflows exist on the default branch.
- CI workflow: add when there is a stable root-level verification command.
- OpenSSF Scorecard workflow: add when GitHub Actions policy and token
  permissions are decided.
- Published project website: README is the current project website.
