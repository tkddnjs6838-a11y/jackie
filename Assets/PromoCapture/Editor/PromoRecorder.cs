using System.IO;
using UnityEditor;
using UnityEditor.Recorder;
using UnityEditor.Recorder.Encoder;
using UnityEditor.Recorder.Input;
using UnityEngine;

// Temporary: records PromoDirector's scripted run to an MP4, then leaves Play mode.
[InitializeOnLoad]
public static class PromoRecorder
{
    const string OutputKey = "PromoCapture.Output", FpsKey = "PromoCapture.Fps";
    static RecorderController controller;
    static int startFrame;

    static PromoRecorder()
    {
        EditorApplication.update += Tick;
        EditorApplication.playModeStateChanged += state => { if (state == PlayModeStateChange.ExitingPlayMode) Stop(); };
    }

    public static string Begin(string stages, string output, int fps)
    {
        if (EditorApplication.isPlaying) return "already playing";
        Directory.CreateDirectory(Path.GetDirectoryName(output));
        SessionState.SetBool(PromoDirector.RunKey, true);
        SessionState.SetString(PromoDirector.StagesKey, stages);
        SessionState.SetString(PromoDirector.MarksKey, "");
        SessionState.SetString(OutputKey, output);
        SessionState.SetInt(FpsKey, fps);
        EditorApplication.isPlaying = true;
        return "started";
    }

    static void Tick()
    {
        if (!EditorApplication.isPlaying || !SessionState.GetBool(PromoDirector.RunKey, false)) return;
        if (controller == null && PromoDirector.WantRecording && !PromoDirector.Finished) StartRecording();
        if (PromoDirector.Finished) { Stop(); EditorApplication.isPlaying = false; }
    }

    static void StartRecording()
    {
        var settings = ScriptableObject.CreateInstance<RecorderControllerSettings>();
        var movie = ScriptableObject.CreateInstance<MovieRecorderSettings>();
        movie.name = "Promo"; movie.Enabled = true;
        movie.EncoderSettings = new CoreEncoderSettings
        {
            Codec = CoreEncoderSettings.OutputCodec.MP4,
            EncodingQuality = CoreEncoderSettings.VideoEncodingQuality.Custom,
            TargetBitRate = 40f
        };
        movie.ImageInputSettings = new GameViewInputSettings { OutputWidth = 1920, OutputHeight = 1080 };
        movie.AudioInputSettings.PreserveAudio = true;
        movie.OutputFile = SessionState.GetString(OutputKey, "Recordings/promo");
        settings.AddRecorderSettings(movie);
        settings.SetRecordModeToManual();
        settings.FrameRatePlayback = FrameRatePlayback.Constant;
        settings.FrameRate = SessionState.GetInt(FpsKey, 30);
        settings.CapFrameRate = true;
        controller = new RecorderController(settings);
        controller.PrepareRecording();
        controller.StartRecording();
        startFrame = Time.frameCount;
        Debug.Log("PromoRecorder: recording to " + movie.OutputFile + " at frame " + startFrame);
    }

    static void Stop()
    {
        if (controller == null) return;
        if (controller.IsRecording()) controller.StopRecording();
        controller = null;
        var output = SessionState.GetString(OutputKey, "");
        File.WriteAllText(output + "-marks.txt", "recordStartFrame\t" + startFrame + "\n" + SessionState.GetString(PromoDirector.MarksKey, ""));
        SessionState.SetBool(PromoDirector.RunKey, false);
        Debug.Log("PromoRecorder: stopped");
    }
}
