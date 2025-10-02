using System;
using System.IO;
using System.Windows;
using System.Windows.Media;

namespace Caro_game.Services;

public sealed class AudioService
{
    private const string BackgroundMusicFile = "background-piano-music-_-samples-61960.mp3";
    private const string MoveSoundFile = "tick.mp3";
    private const string ErrorSoundFile = "erorr.mp3";
    private const string WinSoundFile = "win.mp3";
    private const string LoseSoundFile = "lose and no winner or loser.mp3";

    private static readonly Lazy<AudioService> _lazy = new(() => new AudioService());

    private readonly MediaPlayer _backgroundPlayer;
    private bool _isSoundEnabled = true;
    private bool _isMusicEnabled = true;
    private bool _isBackgroundPlaying;

    private AudioService()
    {
        _backgroundPlayer = new MediaPlayer
        {
            Volume = 0.35
        };

        _backgroundPlayer.MediaEnded += (_, _) =>
        {
            if (!_isMusicEnabled)
            {
                return;
            }

            _backgroundPlayer.Position = TimeSpan.Zero;
            _backgroundPlayer.Play();
        };
    }

    public static AudioService Instance => _lazy.Value;

    public void SetSoundEnabled(bool isEnabled)
    {
        _isSoundEnabled = isEnabled;
    }

    public void SetMusicEnabled(bool isEnabled)
    {
        _isMusicEnabled = isEnabled;
        if (!isEnabled)
        {
            StopBackgroundMusic();
        }
        else
        {
            PlayBackgroundMusic();
        }
    }

    public void PlayBackgroundMusic()
    {
        if (!_isMusicEnabled)
        {
            return;
        }

        ExecuteOnDispatcher(() =>
        {
            if (!_isBackgroundPlaying)
            {
                if (!TryOpen(_backgroundPlayer, BackgroundMusicFile))
                {
                    return;
                }
            }

            _backgroundPlayer.Position = TimeSpan.Zero;
            _backgroundPlayer.Play();
            _isBackgroundPlaying = true;
        });
    }

    public void StopBackgroundMusic()
    {
        ExecuteOnDispatcher(() =>
        {
            _backgroundPlayer.Stop();
            _isBackgroundPlaying = false;
        });
    }

    public void PlayMoveSound()
        => PlayOneShot(MoveSoundFile);

    public void PlayErrorSound()
        => PlayOneShot(ErrorSoundFile);

    public void PlayWinSound()
        => PlayOneShot(WinSoundFile);

    public void PlayLoseSound()
        => PlayOneShot(LoseSoundFile);

    private void PlayOneShot(string fileName)
    {
        if (!_isSoundEnabled)
        {
            return;
        }

        ExecuteOnDispatcher(() =>
        {
            var player = new MediaPlayer();
            if (!TryOpen(player, fileName))
            {
                player.Close();
                return;
            }

            player.Volume = 0.85;
            player.MediaEnded += OnOneShotEnded;
            player.MediaFailed += OnOneShotFailed;
            player.Play();
        });
    }

    private void OnOneShotEnded(object? sender, EventArgs e)
    {
        if (sender is MediaPlayer player)
        {
            CleanupPlayer(player);
        }
    }

    private void OnOneShotFailed(object? sender, ExceptionEventArgs e)
    {
        if (sender is MediaPlayer player)
        {
            CleanupPlayer(player);
        }
    }

    private void CleanupPlayer(MediaPlayer player)
    {
        player.Stop();
        player.Close();
        player.MediaEnded -= OnOneShotEnded;
        player.MediaFailed -= OnOneShotFailed;
    }

    private bool TryOpen(MediaPlayer player, string fileName)
    {
        try
        {
            var uri = ResolveSoundUri(fileName);
            player.Open(uri);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static Uri ResolveSoundUri(string fileName)
    {
        var baseDir = AppDomain.CurrentDomain.BaseDirectory;
        var soundDirectory = Path.Combine(baseDir, "Sounds");
        var fullPath = Path.Combine(soundDirectory, fileName);
        return new Uri(fullPath, UriKind.Absolute);
    }

    private static void ExecuteOnDispatcher(Action action)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher == null)
        {
            action();
            return;
        }

        if (dispatcher.CheckAccess())
        {
            action();
        }
        else
        {
            _ = dispatcher.BeginInvoke(action);
        }
    }
}
