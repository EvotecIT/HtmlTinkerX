namespace HtmlTinkerX;

using System;

/// <summary>Immutable device characteristics applied to each isolated browser PDF context.</summary>
public sealed class HtmlBrowserPdfDeviceEmulation {
    /// <summary>Initializes browser device emulation.</summary>
    public HtmlBrowserPdfDeviceEmulation(
        float? deviceScaleFactor = null,
        bool? isMobile = null,
        bool? hasTouch = null) {
        if (deviceScaleFactor.HasValue
            && (deviceScaleFactor.Value <= 0F
                || float.IsNaN(deviceScaleFactor.Value)
                || float.IsInfinity(deviceScaleFactor.Value))) {
            throw new ArgumentOutOfRangeException(nameof(deviceScaleFactor));
        }

        DeviceScaleFactor = deviceScaleFactor;
        IsMobile = isMobile;
        HasTouch = hasTouch;
    }

    /// <summary>Gets the browser-context device pixel ratio.</summary>
    public float? DeviceScaleFactor { get; }

    /// <summary>Gets whether the browser context emulates mobile layout behavior.</summary>
    public bool? IsMobile { get; }

    /// <summary>Gets whether the browser context exposes touch input.</summary>
    public bool? HasTouch { get; }
}
