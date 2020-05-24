using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using Terraria.ModLoader.IO;

public static class ModLoaderTools
{
    const string GAME_PATH = @"C:\Program Files (x86)\Steam\steamapps\common\Terraria\";
    public static TValue Get<TKey, TValue>(this Dictionary<TKey, TValue> dict, TKey key, TValue defaultValue = default) => dict.TryGetValue(key, out var value) ? value : defaultValue;

    public static void Main(string[] args)
    {
        try
        {
            if (args.Length != 2)
            {
                Console.WriteLine("Argument incorrect");
                Environment.Exit(1);
            }

            var savePath = Environment.ExpandEnvironmentVariables(@"%USERPROFILE%\Documents\My Games\Terraria\ModLoader\");
            Directory.CreateDirectory(savePath);
            var workspace = Environment.GetEnvironmentVariable("GITHUB_WORKSPACE");

            const string TML_CORE = "https://raw.githubusercontent.com/tModLoader/tModLoader/master/patches/tModLoader/Terraria.ModLoader/ModLoader.cs";
            var wc = new WebClient();
            Console.Write("tModLoader version: ");
            var coreMatcher = Regex.Match(wc.DownloadString(TML_CORE), "new Version\\((.*?)\\)");
            var tmlVercode = wc.DownloadString(TML_CORE);
            var version = new Version(coreMatcher.Groups[1].Value.Replace(",", ".").Replace(" ", ""));
            Console.WriteLine(version);

            switch (args[0])
            {
                case "setup":
                {
                    Setup(savePath, wc, version);
                    break;
                }
                case "build":
                {
                    var build = Process.Start(Path.Combine(GAME_PATH, "tModLoaderServer.exe"), $"-build \"{Path.Combine(workspace, args[1])}\" -unsafe");
                    build.WaitForExit();
                    Environment.Exit(build.ExitCode);
                    break;
                }
                case "publish":
                {
                    Publish(Path.Combine(workspace, args[1]), savePath, version);
                    break;
                }
                default:
                    Console.WriteLine($"Unrecognizable command {args[0]}");
                    Environment.Exit(1);
                    break;
            }
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }
    }

    private static void Publish(string path, string savePath, Version version)
    {
        var build = Path.Combine(path, "build.txt");
        if (!File.Exists(build))
        {
            Console.WriteLine($"{build} not found");
            Environment.Exit(1);
        }

        var buildProps = File.ReadAllLines(build).Select(line =>
        {
            var index = line.IndexOf("=");
            return (Key: line.Substring(0, index).Trim(), Value: line.Substring(index + 1).Trim());
        }).Where(item => item.Value.Length > 0).ToDictionary(item => item.Key, item => item.Value);

        if (string.IsNullOrEmpty(buildProps.Get("author")))
        {
            Console.WriteLine("author invalid");
            Environment.Exit(1);
        }

        var steamid64 = Environment.GetEnvironmentVariable("steamid64");
        if (steamid64.Length != 17)
        {
            Console.WriteLine("steamid64 environment variable invalid, publish skipped");
            Environment.Exit(0);
        }

        var passphrase = Environment.GetEnvironmentVariable("passphrase");
        if (passphrase.Length != 32)
        {
            Console.WriteLine("passphrase environment variable invalid, publish skipped");
            Environment.Exit(0);
        }

        var props = new Dictionary<string, string>
        {
            ["displayname"] = buildProps.Get("displayName"),
            ["displaynameclean"] = Regex.Replace(buildProps["displayName"], @"\[c/[0-9A-F]+:([^\]]*)\]", "$1"),
            ["name"] = Path.GetFileName(path.Trim('\\')),
            ["version"] = "v" + buildProps.Get("version", "1.0"),
            ["author"] = buildProps.Get("author"),
            ["homepage"] = buildProps.Get("homepage"),
            ["description"] = File.Exists(Path.Combine(path, "description.txt")) ? File.ReadAllText(Path.Combine(path, "description.txt")) : "",
            ["steamid64"] = steamid64,
            ["modloaderversion"] = "tModLoader v" + version,
            ["passphrase"] = passphrase,
            ["modreferences"] = buildProps.Get("modreferences"),
            ["modside"] = buildProps.Get("side")
        };

        ServicePointManager.Expect100Continue = false;
        var url = "http://javid.ddns.net/tModLoader/publishmod.php";

        using (var client = new WebClient())
        {
            ServicePointManager.ServerCertificateValidationCallback = (sender, certificate, chain, policyErrors) => true;
            var boundary = "---------------------------" + DateTime.Now.Ticks.ToString("x", System.Globalization.NumberFormatInfo.InvariantInfo);
            client.Headers["Content-Type"] = "multipart/form-data; boundary=" + boundary;

            var response = client.UploadData(new Uri(url), UploadFile.GetUploadFilesRequestData(props, boundary, new UploadFile
            {
                Name = "file",
                Filename = props["name"] + ".tmod",
                Content = File.ReadAllBytes(Path.Combine(savePath, "Mods", props["name"] + ".tmod"))
            }, File.Exists(Path.Combine(path, "icon.png")) ? new UploadFile
            {
                Name = "iconfile",
                Filename = "icon.png",
                Content = File.ReadAllBytes(Path.Combine(path, "icon.png"))
            } : null));
            Console.WriteLine(Encoding.UTF8.GetString(response));
        }
    }

    private static void Setup(string savePath, WebClient wc, Version version)
    {
        var directory = Environment.CurrentDirectory;
        var refPath = Path.Combine(savePath, "references");
        Directory.CreateDirectory(refPath);

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            var fileName = Path.Combine(refPath, "Mono.Cecil.dll");
            wc.DownloadFile("https://github.com/tModLoader/tModLoader/raw/master/references/Mono.Cecil.dll", fileName);
            Console.WriteLine("Download Mono.Cecil to " + fileName);

            fileName = Path.Combine(directory, "ModCompile_FNA.zip");
            wc.DownloadFile($"https://github.com/tModLoader/tModLoader/releases/download/v{version}/ModCompile_FNA.zip", fileName);
            Console.WriteLine("Download ModCompile FNA to " + fileName);
            Directory.CreateDirectory(Path.Combine(GAME_PATH, "ModCompile"));
            ZipFile.ExtractToDirectory(Path.Combine(directory, "ModCompile_FNA.zip"), Path.Combine(GAME_PATH, "ModCompile"));

            fileName = Path.Combine(directory, "tModLoader.zip");
            wc.DownloadFile($"https://github.com/tModLoader/tModLoader/releases/download/v{version}/tModLoader.Windows.v{version}.zip", fileName);
            Console.WriteLine("Download tModLoader Windows to " + fileName);
            ZipFile.ExtractToDirectory(Path.Combine(directory, "tModLoader.zip"), GAME_PATH);

            fileName = Path.Combine(refPath, "xnafx40_redist.msi");
            wc.DownloadFile("https://download.microsoft.com/download/5/3/A/53A804C8-EC78-43CD-A0F0-2FB4D45603D3/xnafx40_redist.msi", fileName);
            Console.WriteLine("Download XNA to " + fileName);
            Process.Start("msiexec", $"/i \"{fileName}\" /quiet").WaitForExit();
            Console.WriteLine("XNA installed.");
        }

        var types = Assembly.Load(File.ReadAllBytes(Path.Combine(refPath, "Mono.Cecil.dll"))).GetTypes();
        dynamic assemblyDef;
        foreach (var type in types)
        {
            if (type.FullName == "Mono.Cecil.AssemblyDefinition")
            {
                foreach (var method in type.GetMethods())
                {
                    if (method.ToString() == "Mono.Cecil.AssemblyDefinition ReadAssembly(System.String)")
                    {
                        assemblyDef = method.Invoke(null, new object[] { Path.Combine(GAME_PATH, "tModLoader.exe") });
                        foreach (var resource in assemblyDef.MainModule.Resources)
                        {
                            if (resource.Name.EndsWith(".dll"))
                            {
                                var fileName = Path.Combine(refPath, resource.Name);
                                Console.Write($"Write Resource {resource.Name} to {fileName}");
                                try
                                {
                                    File.WriteAllBytes(fileName, resource.GetResourceData());
                                    Console.WriteLine();
                                }
                                catch (Exception e)
                                {
                                    Console.WriteLine(" failed " + e.ToString());
                                }
                            }
                        }
                    }
                }
            }
        }

        // Generate tModLoader.targets
        Process.Start(Path.Combine(GAME_PATH, "tModLoaderServer.exe"), "-build");
    }
}