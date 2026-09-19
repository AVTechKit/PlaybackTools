using PlaybackTools.Mpv;

namespace PlaybackTools.Playback
{
    /// <summary>
    /// Owns the two-instance leap-frog crossfade engine (mpv-side state only).
    /// GL rendering itself still has to be driven per-frame from GLWpfControl's
    /// Render event since it needs a current GL context on that thread, so
    /// callers (View code-behind) call EnsureInitialized/RenderInto from those
    /// handlers each frame. The actual crossfade opacity animation also stays
    /// in the View, since animating WPF elements is UI work, not engine state -
    /// this class just tracks which player is active and handles the
    /// generation-guarded Stop() once a fade-out completes.
    /// </summary>
    public class PlaybackEngine
    {
        private readonly MpvPlayer _playerA = new();
        private readonly MpvPlayer _playerB = new();
        private bool _aReady;
        private bool _bReady;

        private bool _aIsActive = true;
        private int _generationA;
        private int _generationB;

        public void EnsureInitialized(int index)
        {
            if (index == 0)
            {
                if (_aReady) return;
                _playerA.InitializeOpenGL(startPaused: true);
                _aReady = true;
            }
            else
            {
                if (_bReady) return;
                _playerB.InitializeOpenGL(startPaused: true);
                _bReady = true;
            }
        }

        public bool HasActiveClip(int index) => PlayerFor(index).HasActiveClip;

        public void RenderInto(int index, int fbo, int width, int height) => PlayerFor(index).RenderIntoFbo(fbo, width, height);

        /// <summary>Loads a clip into the currently idle player and marks it active.
        /// Returns which player index (0=A, 1=B) is now incoming vs outgoing, so
        /// the caller knows which view to fade in vs out.</summary>
        public (int incomingIndex, int outgoingIndex) TriggerClip(string path, bool loopOn, double? inPoint = null, double? outPoint = null)
        {
            int incomingIndex = _aIsActive ? 1 : 0;
            int outgoingIndex = _aIsActive ? 0 : 1;

            if (incomingIndex == 0) _generationA++; else _generationB++;

            var incomingPlayer = PlayerFor(incomingIndex);
            incomingPlayer.LoadFile(path, inPoint);

            bool hasValidOutPoint = outPoint.HasValue && outPoint.Value > 0
                && (!inPoint.HasValue || outPoint.Value > inPoint.Value);

            if (loopOn && hasValidOutPoint)
            {
                incomingPlayer.SetProperty("ab-loop-a", (inPoint ?? 0).ToString());
                incomingPlayer.SetProperty("ab-loop-b", outPoint!.Value.ToString());
                incomingPlayer.SetProperty("ab-loop-count", "inf");
                incomingPlayer.SetProperty("loop-file", "no"); // ab-loop supersedes whole-file loop
            }
            else
            {
                incomingPlayer.SetProperty("ab-loop-a", "no");
                incomingPlayer.SetProperty("ab-loop-b", "no");
                incomingPlayer.SetProperty("loop-file", loopOn ? "inf" : "no");
            }

            incomingPlayer.SetProperty("pause", "no");

            _aIsActive = !_aIsActive;

            return (incomingIndex, outgoingIndex);
        }

        public void UpdateActiveAbLoop(double? inPoint, double? outPoint)
        {
            bool hasValidOutPoint = outPoint.HasValue && outPoint.Value > 0
                && (!inPoint.HasValue || outPoint.Value > inPoint.Value);

            if (!hasValidOutPoint) return;

            ActivePlayer.SetProperty("ab-loop-a", (inPoint ?? 0).ToString());
            ActivePlayer.SetProperty("ab-loop-b", outPoint!.Value.ToString());
        }

        public int GenerationFor(int index) => index == 0 ? _generationA : _generationB;

        public int ActiveIndex => _aIsActive ? 0 : 1;

        public void StopPlayer(int index, int expectedGeneration)
        {
            if (GenerationFor(index) != expectedGeneration) return;
            PlayerFor(index).Stop();
        }

        public void SetVolume(double volume)
        {
            _playerA.SetProperty("volume", volume.ToString());
            _playerB.SetProperty("volume", volume.ToString());
        }

        public void SetAudioDevice(string? deviceId)
        {
            string mpvValue = string.IsNullOrEmpty(deviceId) ? "auto" : $"wasapi/{deviceId}";
            _playerA.SetProperty("audio-device", mpvValue);
            _playerB.SetProperty("audio-device", mpvValue);
        }

        public void SetPlayerVolume(int index, double volume) => PlayerFor(index).SetProperty("volume", volume.ToString());

        public void Resume() => ActivePlayer.SetProperty("pause", "no");
        public void Pause() => ActivePlayer.SetProperty("pause", "yes");
        public void SeekActive(double seconds) => ActivePlayer.SetProperty("time-pos", seconds.ToString());

        /// <summary>Immediate hard stop - used by the manual transport Stop button, which
        /// per spec cuts instantly to black, unlike the Stop to Black end action.</summary>
        public void StopToBlack()
        {
            int index = _aIsActive ? 0 : 1;
            if (index == 0) _generationA++; else _generationB++;
            PlayerFor(index).Stop();
        }

        /// <summary>Claims the active player's generation (guarding it against being
        /// reclaimed mid-fade) without stopping it yet - used by the fade-based Stop to
        /// Black end action, which fades opacity/volume down first and calls StopPlayer
        /// afterward. Returns the generation the caller should pass to that later StopPlayer call.</summary>
        public int BeginStopToBlack()
        {
            int index = _aIsActive ? 0 : 1;
            if (index == 0) _generationA++; else _generationB++;
            return GenerationFor(index);
        }

        public string? GetActiveTimePosition() => ActivePlayer.GetProperty("time-pos");
        public string? GetActiveDuration() => ActivePlayer.GetProperty("duration");
        public string? GetActiveEofReached() => ActivePlayer.GetProperty("eof-reached");

        private MpvPlayer ActivePlayer => PlayerFor(_aIsActive ? 0 : 1);
        private MpvPlayer PlayerFor(int index) => index == 0 ? _playerA : _playerB;

        public void Dispose()
        {
            _playerA.Dispose();
            _playerB.Dispose();
        }
    }
}