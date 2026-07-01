using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Text_Grab.Interfaces;
using Text_Grab.Properties;
using Windows.Media.Core;
using Windows.Media.Playback;
using Windows.Media.SpeechSynthesis;

namespace Text_Grab.Services;

public class WindowsSpeechEngine : ITtsEngine
{
    public async Task SpeakAsync(string text, CancellationToken ct)
    {
        using SpeechSynthesizer synthesizer = new();

        string voiceName = Settings.Default.TtsVoiceName;
        if (!string.IsNullOrEmpty(voiceName))
        {
            VoiceInformation? voice = SpeechSynthesizer.AllVoices
                .FirstOrDefault(v => v.DisplayName == voiceName);
            if (voice is not null)
                synthesizer.Voice = voice;
        }

        SpeechSynthesisStream stream = await synthesizer.SynthesizeTextToStreamAsync(text).AsTask();

        ct.ThrowIfCancellationRequested();

        TaskCompletionSource<bool> tcs = new();

        using MediaPlayer player = new();
        player.Source = MediaSource.CreateFromStream(stream, stream.ContentType);

        player.MediaEnded += (s, e) => tcs.TrySetResult(true);
        player.MediaFailed += (s, e) => tcs.TrySetException(new System.Exception(e.ErrorMessage));

        using CancellationTokenRegistration registration = ct.Register(() =>
        {
            player.Pause();
            tcs.TrySetCanceled();
        });

        player.Play();
        await tcs.Task;
    }
}
