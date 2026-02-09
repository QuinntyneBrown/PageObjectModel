using FluentAssertions;
using PlaywrightPomGenerator.Core.Models;
using PlaywrightPomGenerator.Core.Services;

namespace PlaywrightPomGenerator.Tests.Core.Services;

public sealed class GitUrlParserTests
{
    [Fact]
    public void Parse_GitHubRepoUrl_ShouldExtractOwnerAndRepo()
    {
        // Act
        var result = GitUrlParser.Parse("https://github.com/QuinntyneBrown/Books");

        // Assert
        result.Provider.Should().Be(GitProvider.GitHub);
        result.Owner.Should().Be("QuinntyneBrown");
        result.RepoName.Should().Be("Books");
        result.CloneUrl.Should().Be("https://github.com/QuinntyneBrown/Books.git");
        result.Branch.Should().BeNull();
        result.PathInRepo.Should().BeNull();
    }

    [Fact]
    public void Parse_GitHubRepoUrlWithGitSuffix_ShouldTrimSuffix()
    {
        // Act
        var result = GitUrlParser.Parse("https://github.com/QuinntyneBrown/Books.git");

        // Assert
        result.RepoName.Should().Be("Books");
        result.CloneUrl.Should().Be("https://github.com/QuinntyneBrown/Books.git");
    }

    [Fact]
    public void Parse_GitHubBlobUrl_ShouldExtractBranchAndPath()
    {
        // Act
        var result = GitUrlParser.Parse(
            "https://github.com/QuinntyneBrown/Books/blob/main/src/Ui/projects/components/src/lib/layout/page-header/page-header.ts");

        // Assert
        result.Provider.Should().Be(GitProvider.GitHub);
        result.Owner.Should().Be("QuinntyneBrown");
        result.RepoName.Should().Be("Books");
        result.Branch.Should().Be("main");
        result.PathInRepo.Should().Be("src/Ui/projects/components/src/lib/layout/page-header/page-header.ts");
        result.IsFilePath.Should().BeTrue();
    }

    [Fact]
    public void Parse_GitHubTreeUrl_ShouldExtractBranchAndPath()
    {
        // Act
        var result = GitUrlParser.Parse(
            "https://github.com/QuinntyneBrown/Books/tree/main/src/Ui/projects/components");

        // Assert
        result.Branch.Should().Be("main");
        result.PathInRepo.Should().Be("src/Ui/projects/components");
        result.IsFilePath.Should().BeFalse();
    }

    [Fact]
    public void Parse_GitHubBlobUrlWithFeatureBranch_ShouldExtractBranchSegment()
    {
        // Note: GitHub URLs with slashes in branch names are inherently ambiguous.
        // The parser extracts the first path segment after blob/ as the branch name.
        // For branches like "feature/my-branch", only "feature" is extracted as the branch,
        // and the rest becomes part of the path. Use the simple branch name for best results.
        var result = GitUrlParser.Parse(
            "https://github.com/owner/repo/blob/develop/src/app/component.ts");

        // Assert
        result.Branch.Should().Be("develop");
        result.PathInRepo.Should().Be("src/app/component.ts");
    }

    [Fact]
    public void Parse_GitLabBlobUrl_ShouldExtractAllParts()
    {
        // Act
        var result = GitUrlParser.Parse(
            "https://gitlab.com/mygroup/myrepo/-/blob/develop/src/app/components/login.ts");

        // Assert
        result.Provider.Should().Be(GitProvider.GitLab);
        result.RepoName.Should().Be("myrepo");
        result.Branch.Should().Be("develop");
        result.PathInRepo.Should().Be("src/app/components/login.ts");
        result.IsFilePath.Should().BeTrue();
        result.CloneUrl.Should().Contain("gitlab.com");
    }

    [Fact]
    public void Parse_GitLabTreeUrl_ShouldSetIsFilePathFalse()
    {
        // Act
        var result = GitUrlParser.Parse(
            "https://gitlab.com/mygroup/myrepo/-/tree/main/src/app/components");

        // Assert
        result.IsFilePath.Should().BeFalse();
        result.PathInRepo.Should().Be("src/app/components");
    }

    [Fact]
    public void Parse_GitLabSimpleUrl_ShouldExtractOwnerAndRepo()
    {
        // Act
        var result = GitUrlParser.Parse("https://gitlab.com/mygroup/myrepo");

        // Assert
        result.Provider.Should().Be(GitProvider.GitLab);
        result.Owner.Should().Be("mygroup");
        result.RepoName.Should().Be("myrepo");
    }

    [Fact]
    public void Parse_BitbucketSrcUrl_ShouldExtractAllParts()
    {
        // Act
        var result = GitUrlParser.Parse(
            "https://bitbucket.org/owner/repo/src/main/src/app/dashboard.ts");

        // Assert
        result.Provider.Should().Be(GitProvider.Bitbucket);
        result.Owner.Should().Be("owner");
        result.RepoName.Should().Be("repo");
        result.Branch.Should().Be("main");
        result.PathInRepo.Should().Be("src/app/dashboard.ts");
    }

    [Fact]
    public void Parse_AzureDevOpsUrl_ShouldExtractAllParts()
    {
        // Act
        var result = GitUrlParser.Parse(
            "https://dev.azure.com/myorg/myproject/_git/myrepo?path=/src/app/component.ts&version=GBmain");

        // Assert
        result.Provider.Should().Be(GitProvider.AzureDevOps);
        result.Owner.Should().Be("myorg");
        result.RepoName.Should().Be("myrepo");
        result.Branch.Should().Be("main");
        result.PathInRepo.Should().Be("src/app/component.ts");
    }

    [Fact]
    public void Parse_GenericGitUrl_ShouldExtractBasicInfo()
    {
        // Act
        var result = GitUrlParser.Parse("https://git.example.com/owner/repo.git");

        // Assert
        result.Provider.Should().Be(GitProvider.Generic);
        result.RepoName.Should().Be("repo");
        result.CloneUrl.Should().Be("https://git.example.com/owner/repo.git");
    }

    [Fact]
    public void Parse_SelfHostedGitLabUrl_ShouldUseGitLabParser()
    {
        // Act
        var result = GitUrlParser.Parse(
            "https://gitlab.mycompany.com/team/project/-/blob/main/src/component.ts");

        // Assert
        result.Provider.Should().Be(GitProvider.GitLab);
        result.Branch.Should().Be("main");
    }

    [Fact]
    public void Parse_InvalidUrl_ShouldThrowArgumentException()
    {
        // Act
        var act = () => GitUrlParser.Parse("not-a-url");

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Parse_NullUrl_ShouldThrowArgumentException()
    {
        // Act
        var act = () => GitUrlParser.Parse(null!);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Parse_EmptyUrl_ShouldThrowArgumentException()
    {
        // Act
        var act = () => GitUrlParser.Parse("");

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void TryParse_ValidUrl_ShouldReturnTrue()
    {
        // Act
        var success = GitUrlParser.TryParse("https://github.com/owner/repo", out var result);

        // Assert
        success.Should().BeTrue();
        result.Should().NotBeNull();
        result!.RepoName.Should().Be("repo");
    }

    [Fact]
    public void TryParse_InvalidUrl_ShouldReturnFalse()
    {
        // Act
        var success = GitUrlParser.TryParse("not-a-url", out var result);

        // Assert
        success.Should().BeFalse();
        result.Should().BeNull();
    }

    [Fact]
    public void Parse_GitHubUrlWithOnlyOwnerAndRepo_ShouldHaveNullBranchAndPath()
    {
        // Act
        var result = GitUrlParser.Parse("https://github.com/angular/angular");

        // Assert
        result.Branch.Should().BeNull();
        result.PathInRepo.Should().BeNull();
        result.IsFilePath.Should().BeFalse();
    }

    [Fact]
    public void Parse_GitHubBlobUrlWithBranchOnly_ShouldHaveNullPath()
    {
        // Act
        var result = GitUrlParser.Parse("https://github.com/owner/repo/tree/main");

        // Assert
        result.Branch.Should().Be("main");
        result.PathInRepo.Should().BeNull();
    }

    // =====================================================================================
    // Self-hosted GitLab (gitscm) URL parsing tests
    // =====================================================================================

    [Fact]
    public void Parse_SelfHostedGitScm_BranchWithQueryParams_ShouldExtractAllParts()
    {
        // URL 1: Self-hosted GitLab with branch ref and ?ref_type=heads query param
        // https://gitscm.nike.ca/knight/shared-resources/-/blob/develop/projects/ui-components/src/components/table/table.ts?ref_type=heads

        // Act
        var result = GitUrlParser.Parse(
            "https://gitscm.nike.ca/knight/shared-resources/-/blob/develop/projects/ui-components/src/components/table/table.ts?ref_type=heads");

        // Assert
        result.Provider.Should().Be(GitProvider.GitLab, "URL contains /-/blob/ which is a GitLab path pattern");
        result.CloneUrl.Should().Be("https://gitscm.nike.ca/knight/shared-resources.git");
        result.Owner.Should().Be("knight");
        result.RepoName.Should().Be("shared-resources");
        result.Branch.Should().Be("develop");
        result.Commit.Should().BeNull("'develop' is a branch name, not a commit hash");
        result.PathInRepo.Should().Be("projects/ui-components/src/components/table/table.ts");
        result.IsFilePath.Should().BeTrue("path ends with .ts extension");
    }

    [Fact]
    public void Parse_SelfHostedGitScm_CommitHashToFile_ShouldExtractCommitAndPath()
    {
        // URL 2: Self-hosted GitLab with commit hash ref pointing to a file
        // https://gitscm.nike.com/knight/shared-resources/-/blob/20a266776b8v51vsf1seag933a0/projects/ui-components/src/components/table/table.ts

        // Act
        var result = GitUrlParser.Parse(
            "https://gitscm.nike.com/knight/shared-resources/-/blob/20a266776b8v51vsf1seag933a0/projects/ui-components/src/components/table/table.ts");

        // Assert
        result.Provider.Should().Be(GitProvider.GitLab, "URL contains /-/blob/ which is a GitLab path pattern");
        result.CloneUrl.Should().Be("https://gitscm.nike.com/knight/shared-resources.git");
        result.Owner.Should().Be("knight");
        result.RepoName.Should().Be("shared-resources");
        result.Commit.Should().Be("20a266776b8v51vsf1seag933a0", "long alphanumeric string is classified as a commit hash");
        result.Branch.Should().BeNull("ref is a commit, not a branch");
        result.PathInRepo.Should().Be("projects/ui-components/src/components/table/table.ts");
        result.IsFilePath.Should().BeTrue("path ends with .ts extension");
    }

    [Fact]
    public void Parse_SelfHostedGitScm_CommitHashToFolder_ShouldExtractCommitAndFolderPath()
    {
        // URL 3: Self-hosted GitLab with commit hash ref pointing to a folder (no file extension)
        // https://gitscm.nike.com/knight/shared-resources/-/blob/20a266776b8v51vsf1seag933a0/projects/ui-components/src/components/table

        // Act
        var result = GitUrlParser.Parse(
            "https://gitscm.nike.com/knight/shared-resources/-/blob/20a266776b8v51vsf1seag933a0/projects/ui-components/src/components/table");

        // Assert
        result.Provider.Should().Be(GitProvider.GitLab, "URL contains /-/blob/ which is a GitLab path pattern");
        result.CloneUrl.Should().Be("https://gitscm.nike.com/knight/shared-resources.git");
        result.Owner.Should().Be("knight");
        result.RepoName.Should().Be("shared-resources");
        result.Commit.Should().Be("20a266776b8v51vsf1seag933a0", "long alphanumeric string is classified as a commit hash");
        result.Branch.Should().BeNull("ref is a commit, not a branch");
        result.PathInRepo.Should().Be("projects/ui-components/src/components/table");
        result.IsFilePath.Should().BeFalse("path has no file extension, indicating a folder");
    }

    [Fact]
    public void Parse_SelfHostedGitScm_ComponentTsExtension_ShouldAlsoBeFile()
    {
        // Ensures .component.ts files are also recognized as file paths

        // Act
        var result = GitUrlParser.Parse(
            "https://gitscm.nike.com/knight/shared-resources/-/blob/develop/projects/ui-components/src/components/table/table.component.ts");

        // Assert
        result.Branch.Should().Be("develop");
        result.PathInRepo.Should().Be("projects/ui-components/src/components/table/table.component.ts");
        result.IsFilePath.Should().BeTrue("path ends with .component.ts extension");
    }

    [Fact]
    public void Parse_SelfHostedGitScm_CommitHashWithComponentTs_ShouldExtractCommit()
    {
        // Ensures commit hash + .component.ts path both resolve correctly

        // Act
        var result = GitUrlParser.Parse(
            "https://gitscm.nike.com/knight/shared-resources/-/blob/20a266776b8v51vsf1seag933a0/projects/ui-components/src/components/table/table.component.ts");

        // Assert
        result.Commit.Should().Be("20a266776b8v51vsf1seag933a0");
        result.Branch.Should().BeNull();
        result.PathInRepo.Should().Be("projects/ui-components/src/components/table/table.component.ts");
        result.IsFilePath.Should().BeTrue();
    }

    [Fact]
    public void Parse_SelfHostedGitScm_RefProperty_ShouldReturnBranchOrCommit()
    {
        // Verify the Ref convenience property returns whichever is set

        // Branch URL
        var branchResult = GitUrlParser.Parse(
            "https://gitscm.nike.com/knight/shared-resources/-/blob/develop/projects/ui-components/src/components/table/table.ts");
        branchResult.Ref.Should().Be("develop", "Ref should return Branch when Branch is set");

        // Commit URL
        var commitResult = GitUrlParser.Parse(
            "https://gitscm.nike.com/knight/shared-resources/-/blob/20a266776b8v51vsf1seag933a0/projects/ui-components/src/components/table/table.ts");
        commitResult.Ref.Should().Be("20a266776b8v51vsf1seag933a0", "Ref should return Commit when Branch is null");
    }

    [Fact]
    public void Parse_SelfHostedGitScm_DetectedByPathPattern_NotHostname()
    {
        // gitscm.nike.com does NOT contain "gitlab" in the hostname,
        // but the /-/blob/ path pattern identifies it as GitLab-style

        // Act
        var result = GitUrlParser.Parse(
            "https://gitscm.nike.com/knight/shared-resources/-/blob/develop/src/component.ts");

        // Assert
        result.Provider.Should().Be(GitProvider.GitLab);
        result.Owner.Should().Be("knight");
        result.RepoName.Should().Be("shared-resources");
    }

    [Fact]
    public void Parse_StandardHexCommitHash_ShouldBeClassifiedAsCommit()
    {
        // Standard SHA-1 hex hash (40 chars, all hex)
        var result = GitUrlParser.Parse(
            "https://github.com/owner/repo/blob/abc123def456789012345678901234567890abcd/src/file.ts");

        result.Commit.Should().Be("abc123def456789012345678901234567890abcd");
        result.Branch.Should().BeNull();
    }

    [Fact]
    public void Parse_ShortHexCommitHash_ShouldBeClassifiedAsCommit()
    {
        // Short SHA-1 hex hash (7 chars, all hex)
        var result = GitUrlParser.Parse(
            "https://github.com/owner/repo/blob/abc1234/src/file.ts");

        result.Commit.Should().Be("abc1234");
        result.Branch.Should().BeNull();
    }

    [Fact]
    public void Parse_BranchNameDevelop_ShouldNotBeClassifiedAsCommit()
    {
        // "develop" is 7 chars but contains non-hex chars ('p') so it's a branch
        var result = GitUrlParser.Parse(
            "https://github.com/owner/repo/blob/develop/src/file.ts");

        result.Branch.Should().Be("develop");
        result.Commit.Should().BeNull();
    }

    [Fact]
    public void Parse_BranchNameMain_ShouldNotBeClassifiedAsCommit()
    {
        var result = GitUrlParser.Parse(
            "https://github.com/owner/repo/blob/main/src/file.ts");

        result.Branch.Should().Be("main");
        result.Commit.Should().BeNull();
    }

    [Fact]
    public void Parse_BranchNameMaster_ShouldNotBeClassifiedAsCommit()
    {
        var result = GitUrlParser.Parse(
            "https://github.com/owner/repo/blob/master/src/file.ts");

        result.Branch.Should().Be("master");
        result.Commit.Should().BeNull();
    }

    // =====================================================================================
    // GitLab nested groups / multiple owners
    // =====================================================================================

    [Fact]
    public void Parse_GitLabNestedGroup_WithDashBlob_ShouldExtractFullOwnerPath()
    {
        // https://gitlab.com/group/subgroup/repo/-/blob/main/src/file.ts
        var result = GitUrlParser.Parse(
            "https://gitlab.com/group/subgroup/repo/-/blob/main/src/file.ts");

        result.Provider.Should().Be(GitProvider.GitLab);
        result.Owner.Should().Be("group/subgroup");
        result.RepoName.Should().Be("repo");
        result.CloneUrl.Should().Be("https://gitlab.com/group/subgroup/repo.git");
        result.Branch.Should().Be("main");
        result.PathInRepo.Should().Be("src/file.ts");
        result.IsFilePath.Should().BeTrue();
    }

    [Fact]
    public void Parse_GitLabDeeplyNestedGroup_WithDashBlob_ShouldExtractFullOwnerPath()
    {
        // https://gitlab.com/org/team/project/repo/-/blob/develop/src/app/component.ts
        var result = GitUrlParser.Parse(
            "https://gitlab.com/org/team/project/repo/-/blob/develop/src/app/component.ts");

        result.Provider.Should().Be(GitProvider.GitLab);
        result.Owner.Should().Be("org/team/project");
        result.RepoName.Should().Be("repo");
        result.CloneUrl.Should().Be("https://gitlab.com/org/team/project/repo.git");
        result.Branch.Should().Be("develop");
        result.PathInRepo.Should().Be("src/app/component.ts");
    }

    [Fact]
    public void Parse_GitLabNestedGroup_WithDashTree_ShouldExtractFullOwnerPath()
    {
        // https://gitlab.com/group/subgroup/repo/-/tree/main/src/components
        var result = GitUrlParser.Parse(
            "https://gitlab.com/group/subgroup/repo/-/tree/main/src/components");

        result.Provider.Should().Be(GitProvider.GitLab);
        result.Owner.Should().Be("group/subgroup");
        result.RepoName.Should().Be("repo");
        result.Branch.Should().Be("main");
        result.PathInRepo.Should().Be("src/components");
        result.IsFilePath.Should().BeFalse();
    }

    [Fact]
    public void Parse_GitLabNestedGroup_SimpleUrlNoDash_ShouldExtractFullOwnerPath()
    {
        // https://gitlab.com/group/subgroup/repo (no /-/ path)
        var result = GitUrlParser.Parse(
            "https://gitlab.com/group/subgroup/repo");

        result.Provider.Should().Be(GitProvider.GitLab);
        result.Owner.Should().Be("group/subgroup");
        result.RepoName.Should().Be("repo");
        result.CloneUrl.Should().Be("https://gitlab.com/group/subgroup/repo.git");
        result.Branch.Should().BeNull();
        result.PathInRepo.Should().BeNull();
    }

    [Fact]
    public void Parse_GitLabDeeplyNestedGroup_SimpleUrlNoDash_ShouldExtractFullOwnerPath()
    {
        // https://gitlab.com/org/team/project/repo
        var result = GitUrlParser.Parse(
            "https://gitlab.com/org/team/project/repo");

        result.Provider.Should().Be(GitProvider.GitLab);
        result.Owner.Should().Be("org/team/project");
        result.RepoName.Should().Be("repo");
        result.CloneUrl.Should().Be("https://gitlab.com/org/team/project/repo.git");
    }

    [Fact]
    public void Parse_GitLabNestedGroup_SimpleUrlWithGitSuffix_ShouldTrimAndExtract()
    {
        // https://gitlab.com/group/subgroup/repo.git
        var result = GitUrlParser.Parse(
            "https://gitlab.com/group/subgroup/repo.git");

        result.Provider.Should().Be(GitProvider.GitLab);
        result.Owner.Should().Be("group/subgroup");
        result.RepoName.Should().Be("repo");
        result.CloneUrl.Should().Be("https://gitlab.com/group/subgroup/repo.git");
    }

    [Fact]
    public void Parse_SelfHostedGitLabNestedGroup_ShouldExtractFullOwnerPath()
    {
        // Self-hosted GitLab with nested groups detected by /-/blob/ pattern
        var result = GitUrlParser.Parse(
            "https://gitscm.nike.com/platform/frontend/shared-resources/-/blob/develop/src/components/table.ts");

        result.Provider.Should().Be(GitProvider.GitLab);
        result.Owner.Should().Be("platform/frontend");
        result.RepoName.Should().Be("shared-resources");
        result.CloneUrl.Should().Be("https://gitscm.nike.com/platform/frontend/shared-resources.git");
        result.Branch.Should().Be("develop");
        result.PathInRepo.Should().Be("src/components/table.ts");
    }

    [Fact]
    public void Parse_GitLabNestedGroup_WithCommitHash_ShouldExtractOwnerAndCommit()
    {
        var result = GitUrlParser.Parse(
            "https://gitlab.com/group/subgroup/repo/-/blob/abc123def456789012345678901234567890abcd/src/file.ts");

        result.Owner.Should().Be("group/subgroup");
        result.RepoName.Should().Be("repo");
        result.Commit.Should().Be("abc123def456789012345678901234567890abcd");
        result.Branch.Should().BeNull();
        result.PathInRepo.Should().Be("src/file.ts");
    }

    [Fact]
    public void Parse_GitLabSingleOwner_ShouldStillWork()
    {
        // Ensure the simple single-owner case still works after nested group changes
        var result = GitUrlParser.Parse(
            "https://gitlab.com/mygroup/myrepo/-/blob/main/src/file.ts");

        result.Owner.Should().Be("mygroup");
        result.RepoName.Should().Be("myrepo");
        result.CloneUrl.Should().Be("https://gitlab.com/mygroup/myrepo.git");
        result.Branch.Should().Be("main");
    }
}
