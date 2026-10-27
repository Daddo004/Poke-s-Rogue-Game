using Android.Content;

namespace PokeRogue.Android;

/// <summary>
/// The game reads its content as plain files (as on the PC), so the APK's "content" assets are copied once into the
/// app's own folder. A stamp with the APK's install time says they are already there; an update copies them again.
/// Files the game wrote itself (card pictures fetched later) are left alone.
/// </summary>
public static class ContentUnpacker
{
    private const string Folder = "content";
    private const string StampName = ".apk-stamp";

    /// <summary>The content of this very APK is already unpacked.</summary>
    public static bool IsCurrent(Context context, string root)
    {
        var stampPath = Path.Combine(root, StampName);
        return File.Exists(stampPath) && File.ReadAllText(stampPath) == Stamp(context);
    }

    private static string Stamp(Context context) => $"{context.PackageManager!.GetPackageInfo(context.PackageName!, 0)!.LastUpdateTime}";

    public static void Unpack(Context context, string root, Action<int> progress)
    {
        var stamp = Stamp(context);
        var stampPath = Path.Combine(root, StampName);
        if (IsCurrent(context, root))
            return;

        var assets = context.Assets!;
        var files = new List<string>();
        Collect(assets, Folder, files);

        var buffer = new byte[1 << 16];
        var last = -1;
        for (var i = 0; i < files.Count; i++)
        {
            var target = Path.Combine(root, files[i][(Folder.Length + 1)..]);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            using (var source = assets.Open(files[i]))
            using (var output = File.Create(target))
            {
                int read;
                while ((read = source.Read(buffer, 0, buffer.Length)) > 0)
                    output.Write(buffer, 0, read);
            }

            var percent = (i + 1) * 100 / files.Count;
            if (percent != last)
            {
                last = percent;
                progress(percent);
            }
        }

        File.WriteAllText(stampPath, stamp);
    }

    /// <summary>Every file under <paramref name="path"/>: an asset folder lists its children, a file lists nothing.</summary>
    private static void Collect(global::Android.Content.Res.AssetManager assets, string path, List<string> files)
    {
        var children = assets.List(path);
        if (children is null || children.Length == 0)
        {
            files.Add(path);
            return;
        }

        foreach (var child in children)
            Collect(assets, path + "/" + child, files);
    }
}
