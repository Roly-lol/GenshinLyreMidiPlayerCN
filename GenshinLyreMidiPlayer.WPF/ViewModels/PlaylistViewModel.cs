using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Media;
using GenshinLyreMidiPlayer.WPF.Core.Errors;
using Melanchall.DryWetMidi.Core;
using Microsoft.Win32;
using ModernWpf;
using Stylet;
using MidiFile = GenshinLyreMidiPlayer.Data.Midi.MidiFile;

namespace GenshinLyreMidiPlayer.WPF.ViewModels
{
    public class PlaylistViewModel : Screen
    {
        public enum LoopState
        {
            None,
            Single,
            All
        }

        private readonly IEventAggregator _events;

        public PlaylistViewModel(IEventAggregator events) { _events = events; }

        public BindableCollection<MidiFile> Tracks { get; set; } = new();

        public BindableCollection<MidiFile>? ShuffledTracks { get; set; }

        public bool Shuffle { get; set; }

        public LoopState Loop { get; set; }

        public MidiFile? SelectedFile { get; set; }

        public MidiFile? OpenedFile { get; set; }

        public SolidColorBrush ShuffleStateColor => Shuffle
            ? new(ThemeManager.Current.ActualAccentColor)
            : Brushes.Gray;

        public Stack<MidiFile> History { get; } = new();

        public string LoopStateString =>
            Loop switch
            {
                LoopState.None   => "\xF5E7",
                LoopState.Single => "\xE8ED",
                LoopState.All    => "\xE8EE"
            };

        public void ToggleShuffle()
        {
            Shuffle = !Shuffle;

            if (Shuffle)
                ShuffledTracks = new(Tracks.OrderBy(_ => Guid.NewGuid()));

            RefreshPlaylist();
        }

        public void ToggleLoop()
        {
            var loopState = (int) Loop;
            var loopStates = Enum.GetValues(typeof(LoopState)).Length;

            var newState = (loopState + 1) % loopStates;
            Loop = (LoopState) newState;
        }

        public MidiFile? Next()
        {
            var playlist = GetPlaylist().ToList();

            if (Loop == LoopState.Single)
                return OpenedFile ?? playlist.FirstOrDefault();

            var next = playlist.FirstOrDefault();

            if (OpenedFile is not null)
            {
                var current = playlist.IndexOf(OpenedFile) + 1;

                if (Loop is LoopState.All)
                    current %= playlist.Count;

                next = playlist.ElementAtOrDefault(current);
            }

            return next;
        }

        public BindableCollection<MidiFile> GetPlaylist() => (Shuffle ? ShuffledTracks : Tracks)!;

        public async Task AddFiles()
        {
            var openFileDialog = new OpenFileDialog
            {
                Filter      = "MIDI file|*.mid;*.midi|All files (*.*)|*.*",
                Multiselect = true
            };

            if (openFileDialog.ShowDialog() != true)
                return;

            foreach (var fileName in openFileDialog.FileNames)
            {
                await AddFile(fileName);
            }

            ShuffledTracks = new(Tracks.OrderBy(_ => Guid.NewGuid()));
            RefreshPlaylist();

            if (OpenedFile is null && Tracks.Count > 0)
                _events.Publish(Next());
        }

        private async Task AddFile(string fileName, ReadingSettings? settings = null)
        {
            try
            {
                var file = new MidiFile(fileName, settings);
                Tracks.Add(file);
            }
            catch (Exception e)
            {
                settings ??= new();
                if (await ExceptionHandler.TryHandleException(e, settings))
                    await AddFile(fileName, settings);
            }
        }

        public void RemoveTrack()
        {
            if (SelectedFile is not null)
            {
                Tracks.Remove(SelectedFile);
                RefreshPlaylist();
            }
        }

        public void ClearPlaylist() { Tracks.Clear(); }

        public void RefreshPlaylist()
        {
            var playlist = GetPlaylist();
            foreach (var file in playlist)
            {
                file.Position = playlist.IndexOf(file);
            }
        }

        public void OnFileChanged(object sender, EventArgs e)
        {
            if (SelectedFile is not null)
                _events.Publish(SelectedFile);
        }

        public void Previous()
        {
            History.Pop();
            _events.Publish(History.Pop());
        }
    }
}