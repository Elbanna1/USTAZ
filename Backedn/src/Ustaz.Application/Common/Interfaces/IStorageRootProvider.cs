namespace Ustaz.Application.Common.Interfaces;

/// <summary>
/// Supplies the host's content root to the storage layer. The Infrastructure project has no
/// web host of its own, so the API registers this and file storage stays deployment-relative
/// instead of anchored to whatever directory the process happened to start in.
/// </summary>
public interface IStorageRootProvider
{
    string ContentRootPath { get; }
}
