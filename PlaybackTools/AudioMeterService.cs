using System;
using NAudio.CoreAudioApi;

namespace PlaybackTools
{
    /// <summary>Reads live peak levels from a specific Core Audio render device via
    /// NAudio's AudioMeterInformation - the same numbers Windows' own volume mixer
    /// peak meters use. Deliberately reads the actual output device rather than
    /// tapping mpv directly: since both crossfading players share one output device,
    /// this reflects exactly what's audible, mix included, for free.</summary>
    public class AudioMeterService : IDisposable
    {
        private MMDevice? _device;

        /// <summary>Switches which device is being metered - pass null for the current
        /// system default render device. Re-resolves once, at call time; does not
        /// track later default-device changes automatically.</summary>
        public void SetDevice(string? deviceId)
        {
            _device?.Dispose();
            _device = null;

            try
            {
                using var enumerator = new MMDeviceEnumerator();
                _device = string.IsNullOrEmpty(deviceId)
                    ? enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia)
                    : enumerator.GetDevice(deviceId);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AudioMeterService] Could not resolve device: {ex.Message}");
            }
        }

        /// <summary>Raw (unsmoothed) 0.0-1.0 peak levels for left/right. Returns (0,0) if
        /// no device is resolved. Mono devices mirror their single channel to both.</summary>
        public (float Left, float Right) ReadPeaks()
        {
            if (_device == null) return (0f, 0f);

            try
            {
                var peaks = _device.AudioMeterInformation.PeakValues;
                float left = peaks.Count > 0 ? peaks[0] : 0f;
                float right = peaks.Count > 1 ? peaks[1] : left;
                return (left, right);
            }
            catch
            {
                return (0f, 0f); // device unplugged mid-show, etc. - fail quiet, not crash
            }
        }

        public void Dispose() => _device?.Dispose();
    }
}