using System;
using System.Reflection;
using MediaBrowser.Controller.MediaEncoding;

namespace Emby.Plugin.AnimeThemesSync.Helpers;

internal static class FfmpegPathResolver
{
    public static string? ResolveEncoderPath(IMediaEncoder mediaEncoder)
    {
        if (mediaEncoder == null)
        {
            return null;
        }

        var directPath = GetStringProperty(mediaEncoder, "EncoderPath");
        if (!string.IsNullOrWhiteSpace(directPath))
        {
            return directPath;
        }

        var config = GetPropertyValue(mediaEncoder, "FfmpegConfiguration") ??
                     GetPropertyValue(mediaEncoder, "FfmpegConfig");
        return config == null ? null : GetStringProperty(config, "EncoderPath");
    }

    private static object? GetPropertyValue(object instance, string propertyName)
    {
        return instance.GetType().GetRuntimeProperty(propertyName)?.GetValue(instance);
    }

    private static string? GetStringProperty(object instance, string propertyName)
    {
        return GetPropertyValue(instance, propertyName) as string;
    }
}
