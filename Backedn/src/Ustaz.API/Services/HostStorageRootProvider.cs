using Ustaz.Application.Common.Interfaces;

namespace Ustaz.API.Services;

/// <summary>Points private file storage at the deployed application's content root.</summary>
public sealed class HostStorageRootProvider(IWebHostEnvironment environment) : IStorageRootProvider
{
    public string ContentRootPath { get; } = environment.ContentRootPath;
}
