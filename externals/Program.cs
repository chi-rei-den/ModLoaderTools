using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

public static class SetupModLoader
{
    public static void Main(string[] args)
    {
        try
        {
            var directory = Environment.CurrentDirectory;
            var referencesDirectory = Environment.ExpandEnvironmentVariables(@"%USERPROFILE%\Documents\My Games\Terraria\ModLoader\references");
            Directory.CreateDirectory(referencesDirectory);
            const string TML_CORE = "https://raw.githubusercontent.com/tModLoader/tModLoader/master/patches/tModLoader/Terraria.ModLoader/ModLoader.cs";
            var wc = new System.Net.WebClient();
            Console.Write("tModLoader version: ");
            var coreMatcher = Regex.Match(wc.DownloadString(TML_CORE), "new Version\\((.*?)\\)");
            var tmlVercode = wc.DownloadString(TML_CORE);
            var version = new Version(coreMatcher.Groups[1].Value.Replace(",", ".").Replace(" ", ""));
            Console.WriteLine(version);

            var fileName = Path.Combine(referencesDirectory, "Mono.Cecil.dll");
            wc.DownloadFile("https://github.com/tModLoader/tModLoader/raw/master/references/Mono.Cecil.dll", fileName);
            Console.WriteLine("Download Mono.Cecil to " + fileName);

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                fileName = Path.Combine(directory, "ModCompile_XNA.zip");
                wc.DownloadFile("https://github.com/tModLoader/tModLoader/releases/download/v" + version + "/ModCompile_XNA.zip", fileName);
                Console.WriteLine("Download ModCompile XNA to " + fileName);
                ZipFile.ExtractToDirectory(Path.Combine(directory, "ModCompile_XNA.zip"), referencesDirectory);

                fileName = Path.Combine(directory, "xnafx40_redist.msi");
                wc.DownloadFile("https://download.microsoft.com/download/5/3/A/53A804C8-EC78-43CD-A0F0-2FB4D45603D3/xnafx40_redist.msi", fileName);
                Console.WriteLine("Download XNA to " + fileName);
                Process.Start("msiexec", $"/i \"{fileName}\" /quiet").WaitForExit();
                Console.WriteLine("XNA installed.");
            }

            var types = Assembly.Load(File.ReadAllBytes(Path.Combine(referencesDirectory, "Mono.Cecil.dll"))).GetTypes();
            dynamic assemblyDef;
            foreach (var type in types)
            {
                if (type.FullName == "Mono.Cecil.AssemblyDefinition")
                {
                    foreach (var method in type.GetMethods())
                    {
                        if (method.ToString() == "Mono.Cecil.AssemblyDefinition ReadAssembly(System.String)")
                        {
                            assemblyDef = method.Invoke(null, new object[] { Path.Combine(referencesDirectory, "tModLoader.XNA.exe") });
                            foreach (var resource in assemblyDef.MainModule.Resources)
                            {
                                if (resource.Name.EndsWith(".dll"))
                                {
                                    fileName = Path.Combine(referencesDirectory, resource.Name);
                                    Console.Write("Write Resource " + resource.Name + " to " + fileName);
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
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }
    }
}