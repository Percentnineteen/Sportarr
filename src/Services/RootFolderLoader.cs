using Microsoft.EntityFrameworkCore;
using Sportarr.Api.Data;
using Sportarr.Api.Models;

namespace Sportarr.Api.Services;

/// <summary>
/// Single source of truth for "what root folders are configured": the
/// RootFolders table that the UI writes to. Earlier code kept a denormalized
/// copy in the MediaManagementSettings.RootFolders JSON column, which drifted
/// (it was only refreshed as a side effect of an import) and produced spurious
/// "No root folders configured" errors right after a folder was added. That
/// column is gone; everything reads the table through here.
///
/// The persisted Accessible / FreeSpace / TotalSpace columns were dropped, so
/// loaded rows arrive with defaults — DiskSpaceService.RefreshLiveState fills in
/// the live values (honoring Docker volume mapping) before callers inspect them.
/// </summary>
public static class RootFolderLoader
{
    public static async Task<List<RootFolder>> LoadAsync(
        SportarrDbContext db, DiskSpaceService diskSpaceService, CancellationToken ct = default)
    {
        var rootFolders = await db.RootFolders.ToListAsync(ct);
        diskSpaceService.RefreshLiveState(rootFolders);
        return rootFolders;
    }
}
