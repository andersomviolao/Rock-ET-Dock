using System;
using System.Runtime.InteropServices;

namespace RockETDock.App.Services;

public sealed class WindowAnimationController
{
    private const uint SpiGetAnimation = 0x0048;
    private const uint SpiSetAnimation = 0x0049;
    private const uint SpifUpdateIniFile = 0x0001;
    private const uint SpifSendChange = 0x0002;

    private bool _capturedOriginal;
    private bool _originalMinimizeAnimationEnabled;
    private bool _disabledByRockEtDock;

    public void Apply(bool disableMinimizeAnimations)
    {
        if (disableMinimizeAnimations)
        {
            Disable();
        }
        else
        {
            Restore();
        }
    }

    public void Disable()
    {
        if (!_capturedOriginal)
        {
            _originalMinimizeAnimationEnabled = GetMinimizeAnimationEnabled();
            _capturedOriginal = true;
        }

        SetMinimizeAnimationEnabled(enabled: false);
        _disabledByRockEtDock = true;
    }

    public void Restore()
    {
        if (!_disabledByRockEtDock)
        {
            return;
        }

        if (_capturedOriginal)
        {
            SetMinimizeAnimationEnabled(_originalMinimizeAnimationEnabled);
        }

        _disabledByRockEtDock = false;
    }

    private static bool GetMinimizeAnimationEnabled()
    {
        var info = AnimationInfo.Create();
        if (!SystemParametersInfo(SpiGetAnimation, info.cbSize, ref info, 0))
        {
            return true;
        }

        return info.iMinAnimate != 0;
    }

    private static void SetMinimizeAnimationEnabled(bool enabled)
    {
        var info = AnimationInfo.Create();
        info.iMinAnimate = enabled ? 1 : 0;
        SystemParametersInfo(SpiSetAnimation, info.cbSize, ref info, SpifUpdateIniFile | SpifSendChange);
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SystemParametersInfo(uint uiAction, uint uiParam, ref AnimationInfo pvParam, uint fWinIni);

    [StructLayout(LayoutKind.Sequential)]
    private struct AnimationInfo
    {
        public uint cbSize;
        public int iMinAnimate;

        public static AnimationInfo Create()
        {
            return new AnimationInfo
            {
                cbSize = (uint)Marshal.SizeOf<AnimationInfo>()
            };
        }
    }
}
