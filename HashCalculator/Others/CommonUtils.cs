﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Media;

namespace HashCalculator
{
    internal static class CommonUtils
    {
        private const double kb = 1024D;
        private const double mb = 1048576D;
        private const double gb = 1073741824D;
        private static readonly char[] dirSeparators = new char[] {
            Path.DirectorySeparatorChar,
            Path.AltDirectorySeparatorChar
        };

        public static void Swap<T>(ref T leftValue, ref T rightValue)
        {
            T temp;
            temp = leftValue;
            leftValue = rightValue;
            rightValue = temp;
        }

        public static string FileSizeCvt(long bytesLength)
        {
            double byteLenInUnits;
            byteLenInUnits = bytesLength / gb;
            if (byteLenInUnits >= 1)
            {
                return $"{byteLenInUnits:f2} GB";
            }
            byteLenInUnits = bytesLength / mb;
            if (byteLenInUnits >= 1)
            {
                return $"{byteLenInUnits:f1} MB";
            }
            byteLenInUnits = bytesLength / kb;
            if (byteLenInUnits >= 1)
            {
                return $"{byteLenInUnits:f1} KB";
            }
            return $"{bytesLength} B";
        }

        public static bool OpenFolderAndSelectItem(string path)
        {
            if (string.IsNullOrEmpty(path) || !Path.IsPathRooted(path))
            {
                return false;
            }
            path = path.Replace("/", "\\");
            SHELL32.SHParseDisplayName(
                path, IntPtr.Zero, out IntPtr nativePath, 0U, out _);
            if (nativePath == IntPtr.Zero)
            {
                return false;
            }
            int intResult = SHELL32.SHOpenFolderAndSelectItems(nativePath, 0U, null, 0U);
            Marshal.FreeCoTaskMem(nativePath);
            return 0 == intResult;
        }

        public static bool OpenFolderAndSelectItems(string folderPath, IEnumerable<string> files)
        {
            if (string.IsNullOrEmpty(folderPath)
                || !Path.IsPathRooted(folderPath)
                || !Directory.Exists(folderPath))
            {
                return false;
            }
            folderPath = folderPath.Replace("/", "\\");
            SHELL32.SHParseDisplayName(
                folderPath, IntPtr.Zero, out IntPtr folderID, 0U, out _);
            if (folderID == IntPtr.Zero)
            {
                return false;
            }
            if (files == null || !files.Any())
            {
                int intResult1 = SHELL32.SHOpenFolderAndSelectItems(folderID, 0U, null, 0U);
                Marshal.FreeCoTaskMem(folderID);
                return 0 == intResult1;
            }
            List<IntPtr> fileIDList = new List<IntPtr>();
            foreach (string fname in files)
            {
                string fileFullPath = Path.Combine(folderPath, fname);
                if (!File.Exists(fileFullPath))
                {
                    continue;
                }
                SHELL32.SHParseDisplayName(
                    fileFullPath, IntPtr.Zero, out IntPtr fileID, 0U, out _);
                if (fileID != null)
                {
                    fileIDList.Add(fileID);
                }
            }
            int intResult2 = 0;
            if (fileIDList.Any())
            {
                intResult2 = SHELL32.SHOpenFolderAndSelectItems(folderID, (uint)fileIDList.Count,
                   fileIDList.ToArray(), 0U);
            }
            Marshal.FreeCoTaskMem(folderID);
            foreach (IntPtr fileID in fileIDList)
            {
                Marshal.FreeCoTaskMem(fileID);
            }
            return 0 == intResult2;
        }

        public static string ToBase64String(byte[] passedInBytes)
        {
            if (passedInBytes != null)
            {
                return Convert.ToBase64String(passedInBytes);
            }
            return default(string);
        }

        public static string ToHexStringUpper(byte[] passedInBytes)
        {
            return ToHexString(passedInBytes, "X2");
        }

        public static string ToHexStringLower(byte[] passedInBytes)
        {
            return ToHexString(passedInBytes, "x2");
        }

        private static string ToHexString(byte[] passedInBytes, string format)
        {
            Debug.Assert(new string[] { "x2", "X2" }.Contains(format));
            if (passedInBytes != null)
            {
                StringBuilder stringBuilder = new StringBuilder(passedInBytes.Length * 2);
                for (int i = 0; i < passedInBytes.Length; ++i)
                {
                    stringBuilder.Append(passedInBytes[i].ToString(format));
                }
                return stringBuilder.ToString();
            }
            return default(string);
        }

        private static byte[] FromBase64String(string base64String)
        {
            if (!string.IsNullOrEmpty(base64String))
            {
                try
                {
                    return Convert.FromBase64String(base64String);
                }
                catch { }
            }
            return default(byte[]);
        }

        private static byte[] FromHexString(string hexString)
        {
            if (!string.IsNullOrEmpty(hexString) && hexString.Length % 2 == 0)
            {
                try
                {
                    byte[] resultBytes = new byte[hexString.Length / 2];
                    for (int i = 0; i < resultBytes.Length; ++i)
                    {
                        resultBytes[i] = Convert.ToByte(hexString.Substring(i * 2, 2), 16);
                    }
                    return resultBytes;
                }
                catch { }
            }
            return default(byte[]);
        }

        public static byte[] HashBytesFromString(string hashString)
        {
            if (!string.IsNullOrEmpty(hashString))
            {
                hashString = hashString.Trim(' ', '\r', '\n');
                if (!hashString.Contains(' '))
                {
                    if (FromHexString(hashString) is byte[] bytesGuessFromHex)
                    {
                        return bytesGuessFromHex;
                    }
                    else if (FromBase64String(hashString) is byte[] bytesGuessFromBase64)
                    {
                        return bytesGuessFromBase64;
                    }
                }
            }
            return default(byte[]);
        }

        public static bool IsValidHashBytesString(string hashString)
        {
            return HashBytesFromString(hashString) != default(byte[]);
        }

        public static bool SendToRecycleBin(IntPtr hParent, string path, bool silent = true)
        {
            FILEOP_FLAGS flags = FILEOP_FLAGS.FOF_ALLOWUNDO;
            if (silent)
            {
                flags |= FILEOP_FLAGS.FOF_SILENT;
                flags |= FILEOP_FLAGS.FOF_NOCONFIRMATION;
            }
            int operationResult;
            if (IntPtr.Size == 4)
            {
                SHFILEOPSTRUCTW32 data = new SHFILEOPSTRUCTW32
                {
                    hwnd = hParent,
                    wFunc = (uint)FileFuncFlags.FO_DELETE,
                    pFrom = path + '\0',
                    fFlags = (ushort)flags,
                };
                operationResult = SHELL32.SHFileOperationW32(ref data);
            }
            else
            {
                SHFILEOPSTRUCTW64 data = new SHFILEOPSTRUCTW64
                {
                    hwnd = hParent,
                    wFunc = (uint)FileFuncFlags.FO_DELETE,
                    pFrom = path + '\0',
                    fFlags = (ushort)flags,
                };
                operationResult = SHELL32.SHFileOperationW64(ref data);
            }
            return operationResult == 0;
        }

        public static bool IsPointToSameFile(string filePath1, string filePath2, out bool isSameFile)
        {
            int INVALID_HANDLE_VALUE = -1;
            IntPtr fileHandle1 = new IntPtr(INVALID_HANDLE_VALUE);
            IntPtr fileHandle2 = new IntPtr(INVALID_HANDLE_VALUE);
            bool executeResult = false;
            isSameFile = false;
            if (!File.Exists(filePath1) || !File.Exists(filePath2))
            {
                goto FinalizeAndReturnResult;
            }
            fileHandle1 = KERNEL32.CreateFileW(filePath1, 0U,
                FileShare.Read | FileShare.Write | FileShare.Delete, IntPtr.Zero, FileMode.Open,
                FileAttributes.Normal | FileAttributes.ReparsePoint, IntPtr.Zero);
            if (fileHandle1.ToInt32() == INVALID_HANDLE_VALUE)
            {
                goto FinalizeAndReturnResult;
            }
            fileHandle2 = KERNEL32.CreateFileW(filePath2, 0U,
                FileShare.Read | FileShare.Write | FileShare.Delete, IntPtr.Zero, FileMode.Open,
                FileAttributes.Normal | FileAttributes.ReparsePoint, IntPtr.Zero);
            if (fileHandle2.ToInt32() == INVALID_HANDLE_VALUE)
            {
                goto FinalizeAndReturnResult;
            }
            if (!KERNEL32.GetFileInformationByHandle(fileHandle1, out BY_HANDLE_FILE_INFORMATION fileInfo1) ||
                !KERNEL32.GetFileInformationByHandle(fileHandle2, out BY_HANDLE_FILE_INFORMATION fileInfo2))
            {
                goto FinalizeAndReturnResult;
            }
            isSameFile = fileInfo1.dwVolumeSerialNumber == fileInfo2.dwVolumeSerialNumber &&
                fileInfo1.nFileIndexLow == fileInfo2.nFileIndexLow && fileInfo1.nFileIndexHigh == fileInfo2.nFileIndexHigh;
            executeResult = true;
        FinalizeAndReturnResult:
            if (fileHandle1.ToInt32() != INVALID_HANDLE_VALUE)
            {
                KERNEL32.CloseHandle(fileHandle1);
            }
            if (fileHandle2.ToInt32() != INVALID_HANDLE_VALUE)
            {
                KERNEL32.CloseHandle(fileHandle2);
            }
            return executeResult;
        }

        private class CyclingDouble : IEnumerable<double>
        {
            private readonly double _maxValue;
            private readonly double _minValue;
            private readonly int _count;
            private readonly float _increments;

            public CyclingDouble(double minValue, double maxValue, int count)
            {
                this._minValue = minValue;
                this._maxValue = maxValue;
                if (count % 2 == 0)
                {
                    this._count = count;
                }
                else
                {
                    this._count = count + 1;
                }
                this._increments = (float)(maxValue / this._count);
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return this.GetEnumerator();
            }

            public IEnumerator<double> GetEnumerator()
            {
                Random random = new Random();
                double[] doubleHues = new double[this._count];
                // 色环随机起点，避免每次都是 0° (正红) 为起点
                double start = (random.NextDouble() * this._maxValue) + this._minValue;
                double splitPoint = start;
                int index = 0;
                while (start < this._maxValue && index < this._count)
                {
                    doubleHues[index++] = start;
                    start += this._increments;
                }
                if (splitPoint > this._minValue)
                {
                    start = start - this._maxValue + this._minValue;
                    while (start < splitPoint && index < this._count)
                    {
                        doubleHues[index++] = start;
                        start += this._increments;
                    }
                }
                index = 0;
                int middle = this._count / 2;
                int iBack = middle;
                bool arrayFrontEnd = true;
                while (index < middle || iBack < this._count)
                {
                    yield return arrayFrontEnd ? doubleHues[index++] : doubleHues[iBack++];
                    arrayFrontEnd = !arrayFrontEnd;
                }
            }
        }

        private static Color RgbDwordToColor(uint color)
        {
            // DWORD from ColorHLSToRGB: 0x00bbggrr(RGB)
            return Color.FromRgb((byte)(color & 0xFFu), (byte)((color & 0xFF00u) >> 8),
                (byte)((color & 0xFF0000u) >> 16));
        }

        public static Color[] ColorGenerator(int number)
        {
            List<Color> colors = new List<Color>(number);
            // 函数 ColorHLSToRGB 三个参数范围都是 0~240
            double MAX_HLS = 240.0;
            foreach (double H in new CyclingDouble(0.0, MAX_HLS, number))
            {
                colors.Add(RgbDwordToColor(SHLWAPI.ColorHLSToRGB((int)H, 200, 230)));
            }
            return colors.ToArray();
        }

        public static bool ShowWindowForeground(int processId)
        {
            IntPtr handle;
            try
            {
                handle = Process.GetProcessById(processId).MainWindowHandle;
            }
            catch (Exception)
            {
                return false;
            }
            if (handle != IntPtr.Zero)
            {
                if (USER32.IsIconic(handle))
                {
                    return USER32.ShowWindow(handle, SW.SW_RESTORE);
                }
                else if (USER32.IsWindowVisible(handle))
                {
                    bool executionResult = USER32.ShowWindow(handle, SW.SW_SHOW);
                    if ((USER32.GetWindowLongPtrW(handle, GWL.GWL_EXSTYLE) & WS.WS_EX_TOPMOST) != WS.WS_EX_TOPMOST)
                    {
                        uint uFlags = SWP.SWP_NOMOVE | SWP.SWP_NOSIZE;
                        USER32.SetWindowPos(handle, SWP.HWND_TOPMOST, 0, 0, 0, 0, uFlags);
                        USER32.SetWindowPos(handle, SWP.HWND_NOTOPMOST, 0, 0, 0, 0, uFlags);
                    }
                    return executionResult;
                }
            }
            return false;
        }

        public static bool ClipboardSetText(string text)
        {
            return ClipboardSetText(null, text);
        }

        public static bool ClipboardSetText(Window owner, string text)
        {
            string reasonForFailure = text != null ? null : "要复制的内容为空";
            if (reasonForFailure == null)
            {
                reasonForFailure = USER32.OpenClipboard(MainWindow.WndHandle) ?
                    null : "打开剪贴板失败，剪贴板可能正被其他程序占用";
                if (reasonForFailure == null)
                {
                    IntPtr hMem = IntPtr.Zero;
                    try
                    {
                        hMem = Marshal.StringToHGlobalUni(text);
                    }
                    catch (Exception e)
                    {
                        reasonForFailure = $"错误详情：{e.Message}";
                    }
                    if (reasonForFailure == null)
                    {
                        reasonForFailure = hMem != IntPtr.Zero && USER32.EmptyClipboard() ?
                            null : "无法为内容分配内存或无法取得剪贴板所有权";
                        if (reasonForFailure == null)
                        {
                            reasonForFailure = USER32.SetClipboardData(CF.CF_UNICODETEXT, hMem) == hMem ?
                                null : "无法将内容更新到剪贴板上";
                            if (reasonForFailure == null)
                            {
                                Settings.Current.ClipboardUpdatedByMe = true;
                            }
                        }
                    }
                    USER32.CloseClipboard();
                }
            }
            if (!string.IsNullOrEmpty(reasonForFailure))
            {
                MessageBox.Show(owner ?? MainWindow.This, reasonForFailure, "复制失败",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            return reasonForFailure == null;
        }

        public static bool ClipboardGetText(out string text)
        {
            return ClipboardGetText(null, out text);
        }

        public static bool ClipboardGetText(Window owner, out string text)
        {
            if (USER32.OpenClipboard(MainWindow.WndHandle))
            {
                try
                {
                    if (USER32.IsClipboardFormatAvailable(CF.CF_UNICODETEXT))
                    {
                        IntPtr hMem = USER32.GetClipboardData(CF.CF_UNICODETEXT);
                        if (hMem != IntPtr.Zero)
                        {
                            text = Marshal.PtrToStringUni(hMem);
                            return true;
                        }
                    }
                }
                catch (Exception e)
                {
                    MessageBox.Show(owner ?? MainWindow.This,
                        $"错误详情：{e.Message}", "读取剪贴板失败", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                finally
                {
                    USER32.CloseClipboard();
                }
            }
            text = default(string);
            return false;
        }

        public static bool TryAssignin<T1, T2>(T1 value, ref T2 variable)
            where T1 : struct, IConvertible
            where T2 : struct, IConvertible
        {
            try
            {
                checked
                {
                    T2 convertedToVariableTypeValue = (T2)Convert.ChangeType(value, typeof(T2));
                    variable = convertedToVariableTypeValue;
                    return true;
                }
            }
            catch (Exception)
            {
                return false;
            }
        }

        public static string GetRelativePath(string relativeTo, string path)
        {
            if (Path.IsPathRooted(relativeTo) &&
                Path.IsPathRooted(path) &&
                Path.GetPathRoot(relativeTo).Equals(Path.GetPathRoot(path), StringComparison.OrdinalIgnoreCase))
            {
                string[] partsPath = path.Split(dirSeparators, StringSplitOptions.RemoveEmptyEntries);
                string[] partsRelativeTo = relativeTo.Split(dirSeparators, StringSplitOptions.RemoveEmptyEntries);
                int commonPrefixLength = 0;
                while (commonPrefixLength < partsPath.Length &&
                    commonPrefixLength < partsRelativeTo.Length &&
                    partsPath[commonPrefixLength].Equals(partsRelativeTo[commonPrefixLength], StringComparison.OrdinalIgnoreCase))
                {
                    commonPrefixLength++;
                }
                List<string> partsRelativePath = new List<string>();
                for (int i = commonPrefixLength; i < partsRelativeTo.Length; ++i)
                {
                    partsRelativePath.Add("..");
                }
                partsRelativePath.AddRange(partsPath.Skip(commonPrefixLength).Take(partsPath.Length - commonPrefixLength));
                return Path.DirectorySeparatorChar.Join(partsRelativePath);
            }
            return default(string);
        }
    }
}
