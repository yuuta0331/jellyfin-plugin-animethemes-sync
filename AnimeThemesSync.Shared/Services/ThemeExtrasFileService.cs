using System;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using AnimeThemesSync.Shared.Configuration;

namespace AnimeThemesSync.Shared.Services;

public sealed record ThemeExtraFileResult(string Action, bool? HardLinkVerified = null, uint? LinkCount = null, string? FallbackReason = null);

/// <summary>
/// Creates browseable extras files from downloaded theme videos.
/// </summary>
public static class ThemeExtrasFileService
{
    /// <summary>
    /// Ensures an extras file exists by hard-linking or copying from the source.
    /// </summary>
    /// <param name="sourcePath">The downloaded source video path.</param>
    /// <param name="targetPath">The browseable extras path.</param>
    /// <param name="mode">The hard-link/copy mode.</param>
    /// <param name="overwrite">Whether an existing extras file should be replaced.</param>
    /// <returns>A short action string for logging.</returns>
    public static string EnsureExtraFile(string sourcePath, string targetPath, ExtrasLinkMode mode, bool overwrite)
        => EnsureExtraFileDetailed(sourcePath, targetPath, mode, overwrite).Action;

    /// <summary>
    /// Ensures an extras file exists by hard-linking or copying from the source.
    /// </summary>
    /// <param name="sourcePath">The downloaded source video path.</param>
    /// <param name="targetPath">The browseable extras path.</param>
    /// <param name="mode">The hard-link/copy mode.</param>
    /// <param name="overwrite">Whether an existing extras file should be replaced.</param>
    /// <returns>The performed action and hard-link verification details when available.</returns>
    public static ThemeExtraFileResult EnsureExtraFileDetailed(string sourcePath, string targetPath, ExtrasLinkMode mode, bool overwrite)
    {
        if (!File.Exists(sourcePath))
        {
            return new ThemeExtraFileResult("missing-source");
        }

        var targetDirectory = Path.GetDirectoryName(targetPath);
        if (!string.IsNullOrEmpty(targetDirectory))
        {
            Directory.CreateDirectory(targetDirectory);
        }

        if (File.Exists(targetPath))
        {
            var existingLinkInfo = GetHardLinkInfo(sourcePath, targetPath);
            if (!overwrite && (mode != ExtrasLinkMode.HardLinkOnly || existingLinkInfo?.IsSameFile == true))
            {
                return new ThemeExtraFileResult("exists", existingLinkInfo?.IsSameFile, existingLinkInfo?.LinkCount);
            }

            File.Delete(targetPath);
        }

        if (mode == ExtrasLinkMode.CopyOnly)
        {
            CopyFile(sourcePath, targetPath);
            return new ThemeExtraFileResult("copied");
        }

        try
        {
            CreateHardLink(sourcePath, targetPath);
            var linkInfo = GetHardLinkInfo(sourcePath, targetPath);
            if (linkInfo?.IsSameFile == false)
            {
                File.Delete(targetPath);
                throw new IOException("The extras hard link was created but verification showed it is not the same file as the source.");
            }

            return new ThemeExtraFileResult("hard-linked", linkInfo?.IsSameFile, linkInfo?.LinkCount);
        }
        catch (Exception ex) when (mode == ExtrasLinkMode.HardLinkWithCopyFallback)
        {
            CopyFile(sourcePath, targetPath);
            return new ThemeExtraFileResult("copied", FallbackReason: ex.Message);
        }
    }

    private static void CopyFile(string sourcePath, string targetPath)
    {
        var tempPath = targetPath + ".part";
        try
        {
            File.Copy(sourcePath, tempPath, overwrite: true);
            if (File.Exists(targetPath))
            {
                File.Delete(targetPath);
            }

            File.Move(tempPath, targetPath);
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
    }

    private static void CreateHardLink(string sourcePath, string targetPath)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            if (!CreateHardLinkWindows(targetPath, sourcePath, IntPtr.Zero))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }

            return;
        }

        if (CreateHardLinkUnix(sourcePath, targetPath) != 0)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }
    }

    private static HardLinkInfo? GetHardLinkInfo(string sourcePath, string targetPath)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return null;
        }

        var sourceInfo = GetWindowsFileIdentity(sourcePath);
        var targetInfo = GetWindowsFileIdentity(targetPath);
        return new HardLinkInfo(sourceInfo.FileId == targetInfo.FileId, targetInfo.LinkCount);
    }

    private static WindowsFileIdentity GetWindowsFileIdentity(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        if (!GetFileInformationByHandle(stream.SafeFileHandle, out var info))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }

        var fileIndex = ((ulong)info.FileIndexHigh << 32) | info.FileIndexLow;
        var fileId = string.Concat(
            info.VolumeSerialNumber.ToString("X8", CultureInfo.InvariantCulture),
            ":",
            fileIndex.ToString("X16", CultureInfo.InvariantCulture));
        return new WindowsFileIdentity(fileId, info.NumberOfLinks);
    }

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport("kernel32.dll", EntryPoint = "CreateHardLinkW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CreateHardLinkWindows(string lpFileName, string lpExistingFileName, IntPtr lpSecurityAttributes);

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetFileInformationByHandle(SafeFileHandle hFile, out ByHandleFileInformation lpFileInformation);

    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    [DllImport("libc", EntryPoint = "link", SetLastError = true)]
    private static extern int CreateHardLinkUnix(string oldPath, string newPath);

    [StructLayout(LayoutKind.Sequential)]
    private struct ByHandleFileInformation
    {
        public uint FileAttributes;
        public System.Runtime.InteropServices.ComTypes.FILETIME CreationTime;
        public System.Runtime.InteropServices.ComTypes.FILETIME LastAccessTime;
        public System.Runtime.InteropServices.ComTypes.FILETIME LastWriteTime;
        public uint VolumeSerialNumber;
        public uint FileSizeHigh;
        public uint FileSizeLow;
        public uint NumberOfLinks;
        public uint FileIndexHigh;
        public uint FileIndexLow;
    }

    private sealed record HardLinkInfo(bool IsSameFile, uint LinkCount);

    private sealed record WindowsFileIdentity(string FileId, uint LinkCount);
}
