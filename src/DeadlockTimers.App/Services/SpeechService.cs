using System.Speech.Synthesis;

namespace DeadlockTimers.App.Services;

public sealed class SpeechService : IDisposable
{
    private readonly SpeechSynthesizer _synth = new();
    public bool Muted { get; set; }

    public SpeechService(int volume, int rate, string voice)
    {
        _synth.SetOutputToDefaultAudioDevice();
        _synth.Volume = Math.Clamp(volume, 0, 100);
        _synth.Rate = Math.Clamp(rate, -10, 10);
        if (!string.IsNullOrWhiteSpace(voice))
        {
            try { _synth.SelectVoice(voice); } catch (ArgumentException) { /* voice not installed; keep default */ }
        }
    }

    public void Say(string text, bool force = false)
    {
        if (Muted && !force) return;
        _synth.SpeakAsync(text);
    }

    public void Dispose() => _synth.Dispose();
}
