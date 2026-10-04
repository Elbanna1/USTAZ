using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Shouldly;
using Ustaz.Application.Common.Interfaces;
using Ustaz.Application.Common.Models;
using Ustaz.Domain.Exceptions;
using Ustaz.Infrastructure.Options;
using Ustaz.Infrastructure.Services.Storage;

namespace Ustaz.Application.Tests.Handlers;

/// <summary>
/// Private file storage is the part of the platform a hostile upload reaches first, so these
/// tests cover the traversal, typing and sizing rules directly against a real temporary root.
/// </summary>
public class FileStorageTests : IDisposable
{
    private readonly string _root;
    private readonly LocalFileStorage _storage;

    public FileStorageTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "ustaz-storage-tests-" + Guid.NewGuid().ToString("N"));

        _storage = new LocalFileStorage(
            Options.Create(new StorageOptions { RootPath = _root, MaxFileSizeInMegabytes = 1 }),
            NullLogger<LocalFileStorage>.Instance,
            contentRootPath: _root);
    }

    private static FileUploadRequest Upload(
        string fileName = "answer.pdf",
        string contentType = "application/pdf",
        string content = "%PDF-1.7 hello") =>
        new()
        {
            Content = new MemoryStream(Encoding.UTF8.GetBytes(content)),
            FileName = fileName,
            ContentType = contentType,
            Length = Encoding.UTF8.GetByteCount(content)
        };

    private static FileStorageScope Scope(Guid? tenantId = null, Guid? ownerId = null) => new()
    {
        Area = FileStorageArea.Messages,
        TenantId = tenantId ?? Guid.NewGuid(),
        OwnerId = ownerId
    };

    [Fact]
    public async Task A_stored_file_lands_under_its_workspace_and_reads_back()
    {
        var tenantId = Guid.NewGuid();

        var stored = await _storage.SaveAsync(Upload(), Scope(tenantId), CancellationToken.None);

        stored.StorageKey.ShouldStartWith("tenants/" + tenantId.ToString("D") + "/messages/");
        stored.FileName.ShouldBe("answer.pdf");
        stored.SizeInBytes.ShouldBeGreaterThan(0);

        await using var content = await _storage.OpenAsync(stored.StorageKey, CancellationToken.None);
        content.ShouldNotBeNull();

        using var reader = new StreamReader(content!);
        (await reader.ReadToEndAsync()).ShouldStartWith("%PDF");
    }

    [Fact]
    public async Task The_stored_name_is_generated_so_a_hostile_filename_cannot_choose_the_path()
    {
        var stored = await _storage.SaveAsync(
            Upload(fileName: "../../../etc/passwd.pdf"), Scope(), CancellationToken.None);

        stored.StorageKey.ShouldNotContain("..");
        stored.StorageKey.ShouldNotContain("etc");

        // The display name keeps something readable, minus any directory part.
        stored.FileName.ShouldBe("passwd.pdf");
    }

    [Fact]
    public async Task An_executable_is_refused_whatever_content_type_it_claims()
    {
        var exception = await Should.ThrowAsync<BusinessRuleException>(() =>
            _storage.SaveAsync(
                Upload(fileName: "payload.exe", contentType: "application/pdf"),
                Scope(),
                CancellationToken.None));

        exception.Code.ShouldBe("FILE_TYPE_NOT_ALLOWED");
    }

    [Fact]
    public async Task A_file_with_no_extension_is_refused()
    {
        var exception = await Should.ThrowAsync<BusinessRuleException>(() =>
            _storage.SaveAsync(Upload(fileName: "payload"), Scope(), CancellationToken.None));

        exception.Code.ShouldBe("FILE_TYPE_NOT_ALLOWED");
    }

    [Fact]
    public async Task A_file_above_the_configured_ceiling_is_refused()
    {
        var oversized = new string('x', 2 * 1024 * 1024);

        var exception = await Should.ThrowAsync<BusinessRuleException>(() =>
            _storage.SaveAsync(Upload(content: oversized), Scope(), CancellationToken.None));

        exception.Code.ShouldBe("FILE_TOO_LARGE");
    }

    [Fact]
    public async Task An_empty_file_is_refused()
    {
        var exception = await Should.ThrowAsync<BusinessRuleException>(() =>
            _storage.SaveAsync(Upload(content: string.Empty), Scope(), CancellationToken.None));

        exception.Code.ShouldBe("FILE_EMPTY");
    }

    [Theory]
    [InlineData("../../../etc/passwd")]
    [InlineData("tenants/../../../secrets.txt")]
    [InlineData("./../../outside.pdf")]
    public async Task A_key_that_points_outside_the_root_is_refused(string key)
    {
        var exception = await Should.ThrowAsync<BusinessRuleException>(() =>
            _storage.OpenAsync(key, CancellationToken.None));

        exception.Code.ShouldBe("FILE_KEY_INVALID");
    }

    [Fact]
    public async Task An_absolute_key_is_refused()
    {
        var absolute = Path.Combine(Path.GetTempPath(), "anything.pdf");

        await Should.ThrowAsync<BusinessRuleException>(() =>
            _storage.OpenAsync(absolute, CancellationToken.None));
    }

    [Fact]
    public async Task An_unknown_key_reads_as_missing_rather_than_throwing()
    {
        var missing = await _storage.OpenAsync(
            "tenants/" + Guid.NewGuid().ToString("D") + "/messages/nothing.pdf", CancellationToken.None);

        missing.ShouldBeNull();
    }

    [Fact]
    public async Task A_key_is_recognised_as_belonging_to_its_own_workspace_only()
    {
        var tenantId = Guid.NewGuid();
        var stored = await _storage.SaveAsync(Upload(), Scope(tenantId), CancellationToken.None);

        _storage.BelongsToTenant(stored.StorageKey, tenantId).ShouldBeTrue();
        _storage.BelongsToTenant(stored.StorageKey, Guid.NewGuid()).ShouldBeFalse();
    }

    [Fact]
    public async Task Deleting_removes_the_file_and_deleting_again_is_harmless()
    {
        var stored = await _storage.SaveAsync(Upload(), Scope(), CancellationToken.None);

        await _storage.DeleteAsync(stored.StorageKey, CancellationToken.None);
        (await _storage.OpenAsync(stored.StorageKey, CancellationToken.None)).ShouldBeNull();

        await _storage.DeleteAsync(stored.StorageKey, CancellationToken.None);
    }

    [Fact]
    public void The_root_is_created_relative_to_the_content_root_when_configured_relatively()
    {
        var contentRoot = Path.Combine(Path.GetTempPath(), "ustaz-content-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(contentRoot);

        try
        {
            _ = new LocalFileStorage(
                Options.Create(new StorageOptions { RootPath = "storage" }),
                NullLogger<LocalFileStorage>.Instance,
                contentRoot);

            // Deployment-relative, never anchored to the process's working directory.
            Directory.Exists(Path.Combine(contentRoot, "storage")).ShouldBeTrue();
        }
        finally
        {
            Directory.Delete(contentRoot, recursive: true);
        }
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }
}
