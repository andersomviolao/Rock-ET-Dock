using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace Dock.App.Controls;

public sealed class AnimatedGifImage : Image
{
    public static readonly DependencyProperty SourcePathProperty =
        DependencyProperty.Register(
            nameof(SourcePath),
            typeof(string),
            typeof(AnimatedGifImage),
            new PropertyMetadata(null, OnSourcePathChanged));

    private readonly DispatcherTimer _timer = new();
    private readonly List<GifFrame> _frames = [];
    private int _frameIndex;

    public AnimatedGifImage()
    {
        _timer.Tick += (_, _) => ShowNextFrame();
        Loaded += (_, _) => Start();
        Unloaded += (_, _) => _timer.Stop();
    }

    public string? SourcePath
    {
        get => (string?)GetValue(SourcePathProperty);
        set => SetValue(SourcePathProperty, value);
    }

    private static void OnSourcePathChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
    {
        if (dependencyObject is AnimatedGifImage image)
        {
            image.LoadGif(e.NewValue as string);
        }
    }

    private void LoadGif(string? path)
    {
        _timer.Stop();
        _frames.Clear();
        _frameIndex = 0;
        Source = null;

        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return;
        }

        try
        {
            using var stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            var decoder = new GifBitmapDecoder(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
            foreach (var frame in decoder.Frames)
            {
                if (frame.CanFreeze)
                {
                    frame.Freeze();
                }

                _frames.Add(new GifFrame(frame, GetFrameDelay(frame)));
            }

            if (_frames.Count > 0)
            {
                Source = _frames[0].Image;
                Start();
            }
        }
        catch
        {
            _frames.Clear();
            Source = null;
        }
    }

    private void Start()
    {
        if (!IsLoaded || _frames.Count <= 1)
        {
            return;
        }

        _timer.Interval = _frames[_frameIndex].Delay;
        _timer.Start();
    }

    private void ShowNextFrame()
    {
        if (_frames.Count == 0)
        {
            _timer.Stop();
            return;
        }

        _frameIndex = (_frameIndex + 1) % _frames.Count;
        Source = _frames[_frameIndex].Image;
        _timer.Interval = _frames[_frameIndex].Delay;
    }

    private static TimeSpan GetFrameDelay(BitmapFrame frame)
    {
        const int defaultDelayMs = 100;

        try
        {
            if (frame.Metadata is BitmapMetadata metadata &&
                metadata.GetQuery("/grctlext/Delay") is ushort rawDelay &&
                rawDelay > 0)
            {
                return TimeSpan.FromMilliseconds(Math.Max(20, rawDelay * 10));
            }
        }
        catch
        {
            return TimeSpan.FromMilliseconds(defaultDelayMs);
        }

        return TimeSpan.FromMilliseconds(defaultDelayMs);
    }

    private sealed record GifFrame(ImageSource Image, TimeSpan Delay);
}
