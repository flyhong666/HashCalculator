﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Resources;
using System.Threading.Tasks;
using Microsoft.Win32;

namespace HashCalculator
{
    internal static class ShellExtHelper
    {
        static ShellExtHelper()
        {
            nodeHashCalculatorAppPaths = new RegNode(keyNameHashCalculator)
            {
                Values = new RegValue[]
                {
                    new RegValue("", executablePath, RegistryValueKind.String),
                    new RegValue("Path", executableDir, RegistryValueKind.String)
                }
            };
        }

        private static Exception RegisterShellExtDll(string path, bool register)
        {
            try
            {
                string verbStr = register ? "注册" : "反注册";
                ProcessStartInfo startInfo = new ProcessStartInfo();
                startInfo.FileName = "regsvr32";
                startInfo.Arguments = register ? $"/s /n /i:user \"{path}\"" : $"/s /u /n /i:user \"{path}\"";
                startInfo.WindowStyle = ProcessWindowStyle.Hidden;
                Process process = new Process();
                process.StartInfo = startInfo;
                process.Start();
                process.WaitForExit();
                return process.ExitCode == 0 ? null : new Exception($"使用 regsvr32 命令{verbStr}外壳扩展失败");
            }
            catch (Exception exception)
            {
                return exception;
            }
        }

        private static void TerminateExplorer()
        {
            List<Process> killedExplorerProcessList = new List<Process>();
            foreach (Process process in Process.GetProcesses())
            {
                if (process.ProcessName.Equals("explorer", StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {
                        process.Kill();
                    }
                    catch (Exception) { }
                    killedExplorerProcessList.Add(process);
                }
            }
            foreach (Process killedExplorerProcess in killedExplorerProcessList)
            {
                try
                {
                    killedExplorerProcess.WaitForExit(3000);
                }
                catch (Exception) { }
            }
        }

        public static async Task<Exception> InstallShellExtension()
        {
            return await Task.Run(async () =>
            {
                try
                {
                    if (!Directory.Exists(shellExtensionsDir))
                    {
                        try
                        {
                            Directory.CreateDirectory(shellExtensionsDir);
                        }
                        catch
                        {
                            return new FileNotFoundException($"创建 \"{shellExtensionsDir}\" 目录失败");
                        }
                    }
                    if (string.IsNullOrEmpty(executablePath))
                    {
                        return new FileNotFoundException("没有获取到当前程序的可执行文件路径");
                    }
                    if (File.Exists(shellExtensionsPath))
                    {
                        await UninstallShellExtension();
                    }
                    if (AppLoading.Executing.GetManifestResourceStream(embeddedShellExtPath) is Stream manifest)
                    {
                        using (manifest)
                        {
                            manifest.ToNewFile(shellExtensionsPath);
                        }
                        Exception exception;
                        if ((exception = RegisterShellExtDll(shellExtensionsPath, true)) != null)
                        {
                            return exception;
                        }
                        SHELL32.SHChangeNotify(HChangeNotifyEventID.SHCNE_ASSOCCHANGED, HChangeNotifyFlags.SHCNF_IDLIST,
                            IntPtr.Zero, IntPtr.Zero);
                        return await RegUpdateAppPathAsync();
                    }
                    else
                    {
                        return new MissingManifestResourceException("找不到内嵌的外壳扩展模块资源");
                    }
                }
                catch (Exception exception)
                {
                    return exception;
                }
            });
        }

        public static async Task<Exception> UninstallShellExtension()
        {
            return await Task.Run(async () =>
            {
                if (File.Exists(shellExtensionsPath))
                {
                    Exception exception;
                    if ((exception = RegisterShellExtDll(shellExtensionsPath, false)) != null)
                    {
                        return exception;
                    }
                    try
                    {
                        File.Delete(shellExtensionsPath);
                    }
                    catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
                    {
                        TerminateExplorer();
                        try
                        {
                            File.Delete(shellExtensionsPath);
                        }
                        catch { }
                    }
                    SHELL32.SHChangeNotify(HChangeNotifyEventID.SHCNE_ASSOCCHANGED, HChangeNotifyFlags.SHCNF_IDLIST,
                        IntPtr.Zero, IntPtr.Zero);
                    return await RegDeleteAppPathAsync();
                }
                else
                {
                    return new FileNotFoundException($"没有在 \"{shellExtensionsDir}\" 目录找到外壳扩展模块");
                }
            });
        }

        private static async Task<Exception> HKCUWriteNode(string regPath, RegNode regNode)
        {
            Debug.Assert(regNode != null, $"Argument can not be null: {nameof(regNode)}");
            if (!string.IsNullOrEmpty(regPath))
            {
                return await Task.Run(() =>
                {
                    try
                    {
                        using (RegistryKey parent = Registry.CurrentUser.CreateSubKey(regPath, true))
                        {
                            if (parent == null)
                            {
                                return new Exception($"无法打开注册表节点：{regPath}");
                            }
                            if (!parent.WriteNode(regNode))
                            {
                                return new Exception($"写入注册表节点失败：{regNode.Name}");
                            }
                        }
                        return null;
                    }
                    catch (Exception exception)
                    {
                        return exception;
                    }
                });
            }
            return new Exception($"需要打开的注册表节点为空");
        }

        private static async Task<Exception> HKCUDeleteNode(string regPath, RegNode regNode)
        {
            Debug.Assert(regNode != null, $"Argument can not be null: {nameof(regNode)}");
            if (!string.IsNullOrEmpty(regPath))
            {
                return await Task.Run(() =>
                {
                    try
                    {
                        using (RegistryKey parent = Registry.CurrentUser.OpenSubKey(regPath, true))
                        {
                            if (parent != null)
                            {
                                parent.DeleteSubKeyTree(regNode.Name, false);
                                return null;
                            }
                            else
                            {
                                return new Exception($"无法打开注册表节点：{regPath}");
                            }
                        }
                    }
                    catch (Exception exception)
                    {
                        return exception;
                    }
                });
            }
            return new Exception($"需要打开的注册表节点为空");
        }

        public static async Task<Exception> RegUpdateAppPathAsync()
        {
            return await HKCUWriteNode(regPathAppPaths, nodeHashCalculatorAppPaths);
        }

        public static async Task<Exception> RegDeleteAppPathAsync()
        {
            return await HKCUDeleteNode(regPathAppPaths, nodeHashCalculatorAppPaths);
        }

        private const string keyNameHashCalculator = "HashCalculator.exe";
        private const string regPathAppPaths = "Software\\Microsoft\\Windows\\CurrentVersion\\App Paths";
        private static readonly RegNode nodeHashCalculatorAppPaths;
        private static readonly string executablePath = Assembly.GetExecutingAssembly().Location;
        private static readonly string executableDir = Path.GetDirectoryName(executablePath);
        private static readonly string shellExtensionName = Environment.Is64BitOperatingSystem ? "HashCalculator.dll" : "HashCalculator32.dll";
        private static readonly string embeddedShellExtPath = $"HashCalculator.ShellExt.{shellExtensionName}";
        private static readonly string shellExtensionsDir = Settings.ConfigDir.FullName;
        private static readonly string shellExtensionsPath = Path.Combine(shellExtensionsDir, shellExtensionName);
    }
}
