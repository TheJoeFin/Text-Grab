// ******************************************************************
// Copyright (c) Microsoft. All rights reserved.
// This code is licensed under the MIT License (MIT).
// THE CODE IS PROVIDED “AS IS”, WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED,
// INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT.
// IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM,
// DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT,
// TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH
// THE CODE OR THE USE OR OTHER DEALINGS IN THE CODE.
// ******************************************************************

/*
 * License for the RegisterActivator portion of code from FrecherxDachs

The MIT License (MIT)

Copyright (c) 2020 Michael Dietrich

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE.
 * */

using Microsoft.Win32;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;
using Windows.UI.Notifications;
using static Text_Grab.NotificationActivator;

namespace Text_Grab;

public class DesktopNotificationManagerCompat
{
    public const string TOAST_ACTIVATED_LAUNCH_ARG = "-ToastActivated";

    private static bool _registeredAumidAndComServer;
    private static string? _aumid;
    private static bool _registeredActivator;

    /// <summary>
    /// If you're not using MSIX or sparse packages, you must call this method to register your AUMID with the Compat library and to
    /// register your COM CLSID and EXE in LocalServer32 registry. Feel free to call this regardless, and we will no-op if running
    /// under Desktop Bridge. Call this upon application startup, before calling any other APIs.
    /// </summary>
    /// <param name="aumid">An AUMID that uniquely identifies your application.</param>
    public static void RegisterAumidAndComServer<T>(string? aumid)
        where T : NotificationActivator
    {
        if (string.IsNullOrWhiteSpace(aumid))
        {
            throw new ArgumentException("You must provide an AUMID.", nameof(aumid));
        }

        // If running as Desktop Bridge
        if (DesktopBridgeHelpers.IsRunningAsUwp())
        {
            // Clear the AUMID since Desktop Bridge doesn't use it, and then we're done.
            // Desktop Bridge apps are registered with platform through their manifest.
            // Their LocalServer32 key is also registered through their manifest.
            _aumid = null;
            _registeredAumidAndComServer = true;
            return;
        }

        _aumid = aumid;

        if (Process.GetCurrentProcess().MainModule is ProcessModule processModule)
        {
            if (processModule.FileName is String exePath)
                RegisterComServer<T>(exePath);

            _registeredAumidAndComServer = true;
        }

    }

    private static void RegisterComServer<T>(String exePath)
        where T : NotificationActivator
    {
        // We register the EXE to start up when the notification is activated
        string regString = String.Format("SOFTWARE\\Classes\\CLSID\\{{{0}}}", typeof(T).GUID);
        using (var key = Registry.CurrentUser.CreateSubKey(regString))
        {
            // Include a flag so we know this was a toast activation and should wait for COM to process
            // We also wrap EXE path in quotes for extra security
            key.SetValue("LocalServer32", '"' + exePath + '"' + " " + TOAST_ACTIVATED_LAUNCH_ARG);
        }

        if (IsElevated)
        {
            // For elevated apps, we need to ensure they'll activate in existing running process by adding
            // some values in local machine
            using (var key = Registry.LocalMachine.CreateSubKey(regString))
            {
                // Same as above, except also including AppId to link to our AppId entry below
                key.SetValue("LocalServer32", '"' + exePath + '"' + " " + TOAST_ACTIVATED_LAUNCH_ARG);
                key.SetValue("AppId", "{" + typeof(T).GUID + "}");
            }

            // This tells COM to match any client, so Action Center will activate our elevated process.
            // More info: https://docs.microsoft.com/windows/win32/com/runas
            using (var key = Registry.LocalMachine.CreateSubKey(String.Format("SOFTWARE\\Classes\\AppID\\{{{0}}}", typeof(T).GUID)))
            {
                key.SetValue("RunAs", "Interactive User");
            }
        }
    }

    /// <summary>
    /// Registers the activator type as a COM server client so that Windows can launch your activator.
    /// </summary>
    /// <typeparam name="T">Your implementation of NotificationActivator. Must have GUID and ComVisible attributes on class.</typeparam>
    public static void RegisterActivator<T>()
        where T : NotificationActivator, new()
    {
        // Big thanks to FrecherxDachs for figuring out the following code which works in .NET Core 3: https://github.com/FrecherxDachs/UwpNotificationNetCoreTest
        var uuid = typeof(T).GUID;
        uint _cookie;
        CoRegisterClassObject(uuid, new NotificationActivatorClassFactory<T>(), CLSCTX_LOCAL_SERVER,
            REGCLS_MULTIPLEUSE, out _cookie);

        _registeredActivator = true;
    }

    [ComImport]
    [Guid("00000001-0000-0000-C000-000000000046")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IClassFactory
    {
        [PreserveSig]
        int CreateInstance(IntPtr pUnkOuter, ref Guid riid, out IntPtr ppvObject);

        [PreserveSig]
        int LockServer(bool fLock);
    }

    private const int CLASS_E_NOAGGREGATION = -2147221232;
    private const int E_NOINTERFACE = -2147467262;
    private const int CLSCTX_LOCAL_SERVER = 4;
    private const int REGCLS_MULTIPLEUSE = 1;
    private const int S_OK = 0;
    private static readonly Guid IUnknownGuid = new Guid("00000000-0000-0000-C000-000000000046");

    private class NotificationActivatorClassFactory<T> : IClassFactory where T : NotificationActivator, new()
    {
        public int CreateInstance(IntPtr pUnkOuter, ref Guid riid, out IntPtr ppvObject)
        {
            ppvObject = IntPtr.Zero;

            if (pUnkOuter != IntPtr.Zero)
                Marshal.ThrowExceptionForHR(CLASS_E_NOAGGREGATION);

            if (riid == typeof(T).GUID || riid == IUnknownGuid)
                // Create the instance of the .NET object
                ppvObject = Marshal.GetComInterfaceForObject(new T(),
                    typeof(INotificationActivationCallback));
            else
                // The object that ppvObject points to does not support the
                // interface identified by riid.
                Marshal.ThrowExceptionForHR(E_NOINTERFACE);
            return S_OK;
        }

        public int LockServer(bool fLock)
        {
            return S_OK;
        }
    }

    [DllImport("ole32.dll")]
    private static extern int CoRegisterClassObject(
        [MarshalAs(UnmanagedType.LPStruct)] Guid rclsid,
        [MarshalAs(UnmanagedType.IUnknown)] object pUnk,
        uint dwClsContext,
        uint flags,
        out uint lpdwRegister);

    /// <summary>
    /// Creates a toast notifier. You must have called <see cref="RegisterActivator{T}"/> first (and also <see cref="RegisterAumidAndComServer(string)"/> if you're a classic Win32 app), or this will throw an exception.
    /// </summary>
    /// <returns></returns>
    public static ToastNotifier CreateToastNotifier()
    {
        EnsureRegistered();

        if (_aumid != null)
        {
            // Non-Desktop Bridge
            return ToastNotificationManager.CreateToastNotifier(_aumid);
        }
        else
        {
            // Desktop Bridge
            return ToastNotificationManager.CreateToastNotifier();
        }
    }

    /// <summary>
    /// Gets the <see cref="DesktopNotificationHistoryCompat"/> object. You must have called <see cref="RegisterActivator{T}"/> first (and also <see cref="RegisterAumidAndComServer(string)"/> if you're a classic Win32 app), or this will throw an exception.
    /// </summary>
    public static DesktopNotificationHistoryCompat History
    {
        get
        {
            EnsureRegistered();

            if (_aumid != null)
                return new DesktopNotificationHistoryCompat(_aumid);
            else
                return new DesktopNotificationHistoryCompat("");
        }
    }

    private static void EnsureRegistered()
    {
        // If not registered AUMID yet
        if (!_registeredAumidAndComServer)
        {
            // Check if Desktop Bridge
            if (DesktopBridgeHelpers.IsRunningAsUwp())
            {
                // Implicitly registered, all good!
                _registeredAumidAndComServer = true;
            }

            else
            {
                // Otherwise, incorrect usage
                throw new Exception("You must call RegisterAumidAndComServer first.");
            }
        }

        // If not registered activator yet
        if (!_registeredActivator)
        {
            // Incorrect usage
            throw new Exception("You must call RegisterActivator first.");
        }
    }

    /// <summary>
    /// Gets a boolean representing whether http images can be used within toasts. This is true if running with package identity (MSIX or sparse package).
    /// </summary>
    public static bool CanUseHttpImages { get { return DesktopBridgeHelpers.IsRunningAsUwp(); } }

    private static bool IsElevated
    {
        get
        {
            return new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator);
        }
    }

    /// <summary>
    /// Code from https://github.com/qmatteoq/DesktopBridgeHelpers/edit/master/DesktopBridge.Helpers/Helpers.cs
    /// </summary>
    private class DesktopBridgeHelpers
    {
        const long APPMODEL_ERROR_NO_PACKAGE = 15700L;

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        static extern int GetCurrentPackageFullName(ref int packageFullNameLength, StringBuilder packageFullName);

        private static bool? _isRunningAsUwp;
        public static bool IsRunningAsUwp()
        {
            if (_isRunningAsUwp == null)
            {
                if (IsWindows7OrLower)
                {
                    _isRunningAsUwp = false;
                }
                else
                {
                    int length = 0;
                    StringBuilder sb = new StringBuilder(0);
                    int result = GetCurrentPackageFullName(ref length, sb);

                    sb = new StringBuilder(length);
                    result = GetCurrentPackageFullName(ref length, sb);

                    _isRunningAsUwp = result != APPMODEL_ERROR_NO_PACKAGE;
                }
            }

            return _isRunningAsUwp.Value;
        }

        private static bool IsWindows7OrLower
        {
            get
            {
                int versionMajor = Environment.OSVersion.Version.Major;
                int versionMinor = Environment.OSVersion.Version.Minor;
                double version = versionMajor + (double)versionMinor / 10;
                return version <= 6.1;
            }
        }
    }
}

/// <summary>
/// Manages the toast notifications for an app including the ability the clear all toast history and removing individual toasts.
/// </summary>
public sealed class DesktopNotificationHistoryCompat
{
    private string _aumid;
    private ToastNotificationHistory _history;

    /// <summary>
    /// Do not call this. Instead, call <see cref="DesktopNotificationManagerCompat.History"/> to obtain an instance.
    /// </summary>
    /// <param name="aumid"></param>
    internal DesktopNotificationHistoryCompat(string aumid)
    {
        _aumid = aumid;
        _history = ToastNotificationManager.History;
    }

    /// <summary>
    /// Removes all notifications sent by this app from action center.
    /// </summary>
    public void Clear()
    {
        if (_aumid != null)
        {
            _history.Clear(_aumid);
        }
        else
        {
            _history.Clear();
        }
    }

    /// <summary>
    /// Gets all notifications sent by this app that are currently still in Action Center.
    /// </summary>
    /// <returns>A collection of toasts.</returns>
    public IReadOnlyList<ToastNotification> GetHistory()
    {
        return _aumid != null ? _history.GetHistory(_aumid) : _history.GetHistory();
    }

    /// <summary>
    /// Removes an individual toast, with the specified tag label, from action center.
    /// </summary>
    /// <param name="tag">The tag label of the toast notification to be removed.</param>
    public void Remove(string tag)
    {
        if (_aumid != null)
        {
            _history.Remove(tag, string.Empty, _aumid);
        }
        else
        {
            _history.Remove(tag);
        }
    }

    /// <summary>
    /// Removes a toast notification from the action using the notification's tag and group labels.
    /// </summary>
    /// <param name="tag">The tag label of the toast notification to be removed.</param>
    /// <param name="group">The group label of the toast notification to be removed.</param>
    public void Remove(string tag, string group)
    {
        if (_aumid != null)
        {
            _history.Remove(tag, group, _aumid);
        }
        else
        {
            _history.Remove(tag, group);
        }
    }

    /// <summary>
    /// Removes a group of toast notifications, identified by the specified group label, from action center.
    /// </summary>
    /// <param name="group">The group label of the toast notifications to be removed.</param>
    public void RemoveGroup(string group)
    {
        if (_aumid != null)
        {
            _history.RemoveGroup(group, _aumid);
        }
        else
        {
            _history.RemoveGroup(group);
        }
    }
}

/// <summary>
/// Apps must implement this activator to handle notification activation.
/// </summary>
public abstract class NotificationActivator : NotificationActivator.INotificationActivationCallback
{
    public void Activate(string appUserModelId, string invokedArgs, NOTIFICATION_USER_INPUT_DATA[] data, uint dataCount)
    {
        OnActivated(invokedArgs, new NotificationUserInput(data), appUserModelId);
    }

    /// <summary>
    /// This method will be called when the user clicks on a foreground or background activation on a toast. Parent app must implement this method.
    /// </summary>
    /// <param name="arguments">The arguments from the original notification. This is either the launch argument if the user clicked the body of your toast, or the arguments from a button on your toast.</param>
    /// <param name="userInput">Text and selection values that the user entered in your toast.</param>
    /// <param name="appUserModelId">Your AUMID.</param>
    public abstract void OnActivated(string arguments, NotificationUserInput userInput, string appUserModelId);

    // These are the new APIs for Windows 10
    #region NewAPIs
    [StructLayout(LayoutKind.Sequential), Serializable]
    public struct NOTIFICATION_USER_INPUT_DATA
    {
        [MarshalAs(UnmanagedType.LPWStr)]
        public string Key;

        [MarshalAs(UnmanagedType.LPWStr)]
        public string Value;
    }

    [ComImport,
    Guid("53E31837-6600-4A81-9395-75CFFE746F94"), ComVisible(true),
    InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface INotificationActivationCallback
    {
        void Activate(
            [In, MarshalAs(UnmanagedType.LPWStr)]
        string appUserModelId,
            [In, MarshalAs(UnmanagedType.LPWStr)]
        string invokedArgs,
            [In, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 3)]
        NOTIFICATION_USER_INPUT_DATA[] data,
            [In, MarshalAs(UnmanagedType.U4)]
        uint dataCount);
    }
    #endregion
}

/// <summary>
/// Text and selection values that the user entered on your notification. The Key is the ID of the input, and the Value is what the user entered.
/// </summary>
public class NotificationUserInput : IReadOnlyDictionary<string, string>
{
    private NotificationActivator.NOTIFICATION_USER_INPUT_DATA[] _data;

    internal NotificationUserInput(NotificationActivator.NOTIFICATION_USER_INPUT_DATA[] data)
    {
        _data = data;
    }

    public string this[string key] => _data.First(i => i.Key == key).Value;

    public IEnumerable<string> Keys => _data.Select(i => i.Key);

    public IEnumerable<string> Values => _data.Select(i => i.Value);

    public int Count => _data.Length;

    public bool ContainsKey(string key)
    {
        return _data.Any(i => i.Key == key);
    }

    public IEnumerator<KeyValuePair<string, string>> GetEnumerator()
    {
        return _data.Select(i => new KeyValuePair<string, string>(i.Key, i.Value)).GetEnumerator();
    }

    public bool TryGetValue(string key, out string value)
    {
        foreach (var item in _data)
        {
            if (item.Key == key)
            {
                value = item.Value;
                return true;
            }
        }

        value = string.Empty;
        return false;
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
}
