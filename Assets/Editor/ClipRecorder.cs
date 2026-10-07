using UnityEditor;
using UnityEditor.Recorder;
using UnityEditor.Recorder.Encoder;
using UnityEditor.Recorder.Input;
using UnityEngine;

/// <summary>
/// Records the Game view to an MP4 while play mode runs, so gameplay footage can be handed
/// to an external tool. Lives in an Editor folder because UnityEditor.Recorder is
/// editor-only, and holds the controller in a static so it survives between the calls that
/// start and stop a clip.
/// </summary>
public static class ClipRecorder
{
    private static RecorderController controller;
    private static RecorderControllerSettings settings;
    private static MovieRecorderSettings movie;

    public static string LastPath { get; private set; }

    public static bool Recording
    {
        get { return controller != null && controller.IsRecording(); }
    }

    /// <summary>Starts a clip. Returns the file it will produce.</summary>
    /// <param name="fromCamera">Render straight off the main camera instead of scraping the
    /// Game view. An unfocused editor repaints the Game view only a few times a second, which
    /// made a 40s capture yield 4.7s of footage.</param>
    public static string Start(string clipName, int width, int height, int fps, bool fromCamera)
    {
        Stop();

        settings = ScriptableObject.CreateInstance<RecorderControllerSettings>();
        settings.SetRecordModeToManual();
        settings.FrameRate = fps;
        // constant frame rate: a variable one makes the video timeline drift away from the
        // gameplay, which matters when the footage is being used to place sounds
        settings.FrameRatePlayback = FrameRatePlayback.Constant;
        settings.CapFrameRate = true;

        movie = ScriptableObject.CreateInstance<MovieRecorderSettings>();
        movie.name = clipName;
        movie.Enabled = true;
        if (fromCamera)
        {
            movie.ImageInputSettings = new CameraInputSettings
            {
                Source = ImageSource.MainCamera,
                OutputWidth = width,
                OutputHeight = height,
                CaptureUI = false
            };
        }
        else
        {
            movie.ImageInputSettings = new GameViewInputSettings
            {
                OutputWidth = width,
                OutputHeight = height
            };
        }
        movie.AudioInputSettings.PreserveAudio = false;   // the game has no sound yet

        var encoder = new CoreEncoderSettings();
        encoder.Codec = CoreEncoderSettings.OutputCodec.MP4;
        encoder.EncodingQuality = CoreEncoderSettings.VideoEncodingQuality.High;
        movie.EncoderSettings = encoder;

        string folder = System.IO.Path.GetFullPath(
            System.IO.Path.Combine(Application.dataPath, "..", "Recordings"));
        if (!System.IO.Directory.Exists(folder)) System.IO.Directory.CreateDirectory(folder);
        movie.OutputFile = System.IO.Path.Combine(folder, clipName);

        settings.AddRecorderSettings(movie);

        controller = new RecorderController(settings);
        controller.PrepareRecording();
        controller.StartRecording();

        LastPath = movie.OutputFile + ".mp4";
        return LastPath;
    }

    public static void Stop()
    {
        if (controller != null && controller.IsRecording())
            controller.StopRecording();
        controller = null;
    }
}
