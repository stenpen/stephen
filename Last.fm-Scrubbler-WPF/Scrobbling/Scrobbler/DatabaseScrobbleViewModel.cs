﻿using Caliburn.Micro;
using DiscogsClient;
using DiscogsClient.Data.Query;
using IF.Lastfm.Core.Api;
using IF.Lastfm.Core.Api.Enums;
using IF.Lastfm.Core.Api.Helpers;
using IF.Lastfm.Core.Objects;
using Scrubbler.Helper;
using Scrubbler.Scrobbling.Data;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace Scrubbler.Scrobbling.Scrobbler
{
  /// <summary>
  /// Available databases to search.
  /// </summary>
  public enum Database
  {
    /// <summary>
    /// Search Last.fm.
    /// </summary>
    LastFm,

    /// <summary>
    /// Search Discogs.com
    /// </summary>
    Discogs,

    /// <summary>
    /// search musicbrainz.org
    /// </summary>
    MusicBrainz
  }

  /// <summary>
  /// Available search types.
  /// </summary>
  public enum SearchType
  {
    /// <summary>
    /// Search for an artist.
    /// </summary>
    Artist,

    /// <summary>
    /// Search for a release.
    /// </summary>
    Release
  }

  /// <summary>
  /// ViewModel for the <see cref="DatabaseScrobbleView"/>.
  /// </summary>
  public class DatabaseScrobbleViewModel : ScrobbleMultipleTimeViewModelBase<FetchedTrackViewModel>, IConductor, IHaveActiveItem
  {
    #region Properties

    /// <summary>
    /// Event that triggers when the activation of a new item has been processed.
    /// </summary>
    public event EventHandler<ActivationProcessedEventArgs> ActivationProcessed;

    /// <summary>
    /// String to search.
    /// </summary>
    public string SearchText
    {
      get { return _searchText; }
      set
      {
        _searchText = value;
        NotifyOfPropertyChange();
      }
    }
    private string _searchText;

    /// <summary>
    /// Which database to search for infos.
    /// </summary>
    public Database DatabaseToSearch
    {
      get { return _databaseToSearch; }
      set
      {
        _databaseToSearch = value;
        NotifyOfPropertyChange();
      }
    }
    private Database _databaseToSearch;

    /// <summary>
    /// Which type of data to search.
    /// </summary>
    public SearchType SearchType
    {
      get { return _searchType; }
      set
      {
        _searchType = value;
        NotifyOfPropertyChange();
      }
    }
    private SearchType _searchType;

    /// <summary>
    /// Maximum amount of search results.
    /// </summary>
    public int MaxResults
    {
      get { return _maxResults; }
      set
      {
        _maxResults = value;
        NotifyOfPropertyChange();
      }
    }
    private int _maxResults;

    /// <summary>
    /// The currently displayed item.
    /// </summary>
    public object ActiveItem
    {
      get => _conductor.ActiveItem;
      set
      {
        _conductor.ActiveItem = (IScreen)value;
        NotifyOfPropertyChange();
      }
    }

    #endregion Properties

    #region Member

    /// <summary>
    /// Last.fm artist api used to search for artists.
    /// </summary>
    private IArtistApi _lastfmArtistAPI;

    /// <summary>
    /// Last.fm album api used to search for albums.
    /// </summary>
    private IAlbumApi _lastfmAlbumAPI;

    private IDiscogsDataBaseClient _discogsClient;

    /// <summary>
    /// Conductor used for view switching.
    /// </summary>
    private Conductor<IScreen> _conductor;

    /// <summary>
    /// The last <see cref="ArtistResultViewModel"/>.
    /// </summary>
    private ArtistResultViewModel _artistResultVM;

    /// <summary>
    /// The last <see cref="ReleaseResultViewModel"/>.
    /// </summary>
    private ReleaseResultViewModel _releaseResultVM;

    #endregion Member

    #region Construction

    /// <summary>
    /// Constructor.
    /// </summary>
    /// <param name="windowManager">WindowManager used to display dialogs.</param>
    /// <param name="lastfmArtistAPI">Last.fm artist api used to search for artists.</param>
    /// <param name="lastfmAlbumAPI">Last.fm album api used to search for albums.</param>
    /// <param name="discogsClient">Client used to interact with Discogs.com</param>
    public DatabaseScrobbleViewModel(IExtendedWindowManager windowManager, IArtistApi lastfmArtistAPI, IAlbumApi lastfmAlbumAPI, IDiscogsDataBaseClient discogsClient)
      : base(windowManager, "Database Scrobbler")
    {
      _lastfmArtistAPI = lastfmArtistAPI;
      _lastfmAlbumAPI = lastfmAlbumAPI;
      _discogsClient = discogsClient ?? throw new ArgumentNullException(nameof(discogsClient));
      _conductor = new Conductor<IScreen>();
      _conductor.ActivationProcessed += _conductor_ActivationProcessed;
      DatabaseToSearch = Database.LastFm;
      SearchType = SearchType.Artist;
      MaxResults = 25;
      Scrobbles = new ObservableCollection<FetchedTrackViewModel>();
    }

    #endregion Construction

    /// <summary>
    /// Searches the entered <see cref="SearchText"/>.
    /// </summary>
    /// <returns>Task.</returns>
    public async Task Search()
    {
      EnableControls = false;

      try
      {
        if (SearchType == SearchType.Artist)
          await SearchArtist();
        else if (SearchType == SearchType.Release)
          await SearchRelease();
      }
      finally
      {
        EnableControls = true;
      }
    }

    /// <summary>
    /// Searches for artists with the entered <see cref="SearchText"/>.
    /// </summary>
    /// <returns>Task.</returns>
    private async Task SearchArtist()
    {
      try
      {
        OnStatusUpdated(string.Format("Trying to search for artist '{0}'...", SearchText));

        IEnumerable<Artist> fetchedArtists = new Artist[0];
        if (DatabaseToSearch == Database.LastFm)
          fetchedArtists = await SearchArtistLastFm();
        else if (DatabaseToSearch == Database.Discogs)
          fetchedArtists = await SearchArtistDiscogs();
        else if (DatabaseToSearch == Database.MusicBrainz)
          fetchedArtists = await SearchArtistMusicBrainz();

        if (fetchedArtists.Count() != 0)
        {
          // clean up old vm
          if (_artistResultVM != null)
          {
            _artistResultVM.ArtistClicked -= Artist_Clicked;
            DeactivateItem(_artistResultVM, true);
          }

          _artistResultVM = new ArtistResultViewModel(fetchedArtists);
          _artistResultVM.ArtistClicked += Artist_Clicked;
          ActivateItem(_artistResultVM);
          OnStatusUpdated(string.Format("Found {0} artists", fetchedArtists.Count()));
        }
        else
          OnStatusUpdated("Found no artists");
      }
      catch (Exception ex)
      {
        OnStatusUpdated(string.Format("Fatal error while searching for artist '{0}': {1}", SearchText, ex.Message));
      }
    }

    /// <summary>
    /// Searches for artists with the entered <see cref="SearchText"/>
    /// on Last.fm.
    /// </summary>
    /// <returns>Task.</returns>
    private async Task<IEnumerable<Artist>> SearchArtistLastFm()
    {
      var response = await _lastfmArtistAPI.SearchAsync(SearchText, 1, MaxResults);
      if (response.Success)
        return response.Content.Select(a => new Artist(a));
      else
        throw new Exception(response.Status.ToString());
    }

    /// <summary>
    /// Searches for artists with the entered <see cref="SearchText"/>
    /// on discogs.com.
    /// </summary>
    /// <returns>Task.</returns>
    private async Task<IEnumerable<Artist>> SearchArtistDiscogs()
    {
      var result = _discogsClient.SearchAsEnumerable(new DiscogsSearch() { query = SearchText, type = DiscogsEntityType.artist }, MaxResults);
      var artists = new List<Artist>();
      await Task.Run(() =>
      {
        foreach (var a in result)
          artists.Add(new Artist(a.title, a.id.ToString(), string.IsNullOrEmpty(a.thumb) ? null : new Uri(a.thumb)));
      });
      return artists;
    }

    /// <summary>
    /// Searches for artists with the entered <see cref="SearchText"/>
    /// on musicbrainz.org.
    /// </summary>
    /// <returns>Task.</returns>
    private async Task<IEnumerable<Artist>> SearchArtistMusicBrainz()
    {
      var found = await Hqub.MusicBrainz.API.Entities.Artist.SearchAsync(SearchText, MaxResults);
      return found.Items.Select(i => new Artist(i.Name, i.Id, null));
    }

    /// <summary>
    /// Searches for releases with the entered <see cref="SearchText"/>.
    /// </summary>
    /// <returns>Task.</returns>
    private async Task SearchRelease()
    {
      try
      {
        OnStatusUpdated(string.Format("Trying to search for release '{0}'", SearchText));

        IEnumerable<Release> releases = new Release[0];
        if (DatabaseToSearch == Database.LastFm)
          releases = await SearchReleaseLastFm();
        else if (DatabaseToSearch == Database.Discogs)
          releases = await SearchReleaseDiscogs();
        else if (DatabaseToSearch == Database.MusicBrainz)
          releases = await SearchReleaseMusicBrainz();

        if (releases.Count() != 0)
        {
          ActivateNewReleaseResultViewModel(releases, false);
          OnStatusUpdated(string.Format("Found {0} releases", releases.Count()));
        }
        else
          OnStatusUpdated("Found no releases");
      }
      catch (Exception ex)
      {
        OnStatusUpdated(string.Format("Fatal error while searching for release '{0}': {1}", SearchText, ex.Message));
      }
    }

    /// <summary>
    /// Searches for releases with the entered <see cref="SearchText"/>
    /// on Last.fm.
    /// </summary>
    /// <returns>Task.</returns>
    private async Task<IEnumerable<Release>> SearchReleaseLastFm()
    {
      var response = await _lastfmAlbumAPI.SearchAsync(SearchText, 1, MaxResults);
      if (response.Success)
        return response.Content.Select(r => new Release(r));
      else
        throw new Exception(response.Status.ToString());
    }

    /// <summary>
    /// Searches for releases with the entered <see cref="SearchText"/>
    /// on discogs.com.
    /// </summary>
    /// <returns>Task.</returns>
    private async Task<IEnumerable<Release>> SearchReleaseDiscogs()
    {
      var searchResults = _discogsClient.SearchAsEnumerable(new DiscogsSearch() { query = SearchText, type = DiscogsEntityType.master }, MaxResults);
      var releases = new List<Release>();
      await Task.Run(async () =>
      {
        foreach (var r in searchResults)
        {
          int id = r.id;
          if (r.type == DiscogsEntityType.master)
          {
            var rel = await _discogsClient.GetMasterAsync(r.id);
            if (rel.main_release != 0)
              id = rel.main_release;
          }
          releases.Add(new Release(r.title, null, id.ToString(), string.IsNullOrEmpty(r.thumb) ? null : new Uri(r.thumb)));
        }
      });
      return releases;
    }

    /// <summary>
    /// Searches for releases with the entered <see cref="SearchText"/>
    /// on musicbrainz.org
    /// </summary>
    /// <returns></returns>
    private async Task<IEnumerable<Release>> SearchReleaseMusicBrainz()
    {
      var found = await Hqub.MusicBrainz.API.Entities.ReleaseGroup.SearchAsync(SearchText, MaxResults);
      return found.Items.Select(i => new Release(i));
    }

    /// <summary>
    /// Fetches the release list of the clicked artist.
    /// </summary>
    /// <param name="sender">Clicked artist as <see cref="LastArtist"/>.</param>
    /// <param name="e">Ignored.</param>
    public async void Artist_Clicked(object sender, EventArgs e)
    {
      if (EnableControls)
      {
        EnableControls = false;

        try
        {
          var artist = sender as Artist;
          OnStatusUpdated(string.Format("Trying to fetch releases from artist '{0}'", artist.Name));

          IEnumerable<Release> releases = new Release[0];
          if (DatabaseToSearch == Database.LastFm)
            releases = await ArtistClickedLastFm(artist);
          else if (DatabaseToSearch == Database.Discogs)
            releases = await ArtistClickedDiscogs(artist);
          else if (DatabaseToSearch == Database.MusicBrainz)
            releases = await ArtistClickedMusicBrainz(artist);

          if (releases.Count() != 0)
          {
            ActivateNewReleaseResultViewModel(releases, true);
            OnStatusUpdated(string.Format("Successfully fetched releases from artist '{0}'", artist.Name));
          }
          else
            OnStatusUpdated(string.Format("Artist '{0} has no releases", artist.Name));
        }
        catch (Exception ex)
        {
          OnStatusUpdated(string.Format("Fatal error while fetching releases from artist: {0}", ex.Message));
        }
        finally
        {
          EnableControls = true;
        }
      }
    }

    /// <summary>
    /// Creates and activates a new <see cref="ReleaseResultViewModel"/>.
    /// </summary>
    /// <param name="releases">Release to create <see cref="ReleaseResultViewModel"/> for.</param>
    /// <param name="fetchedThroughArtist">If the <paramref name="releases"/> were fetched
    /// by clicking an artist.</param>
    private void ActivateNewReleaseResultViewModel(IEnumerable<Release> releases, bool fetchedThroughArtist)
    {
      // clean up old vm
      if (_releaseResultVM != null)
      {
        _releaseResultVM.ReleaseClicked -= Release_Clicked;
        _releaseResultVM.BackToArtistRequested -= Release_BackToArtistRequested;
        DeactivateItem(_releaseResultVM, true);
      }

      _releaseResultVM = new ReleaseResultViewModel(releases, fetchedThroughArtist);
      _releaseResultVM.ReleaseClicked += Release_Clicked;
      _releaseResultVM.BackToArtistRequested += Release_BackToArtistRequested;
      ActivateItem(_releaseResultVM);
    }

    /// <summary>
    /// Goes back to the artist when the user requests it.s
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void Release_BackToArtistRequested(object sender, EventArgs e)
    {
      BackToArtist();
    }

    /// <summary>
    /// Fetches the release list of the clicked artist via Last.fm.
    /// </summary>
    /// <param name="artist">Artist to fetch releases of.</param>
    /// <returns>Task.</returns>
    private async Task<IEnumerable<Release>> ArtistClickedLastFm(Artist artist)
    {
      var response = await _lastfmArtistAPI.GetTopAlbumsAsync(artist.Name, false, 1, MaxResults);
      if (response.Success && response.Status == LastResponseStatus.Successful)
        return response.Content.Select(r => new Release(r));
      else
        throw new Exception(response.Status.ToString());
    }

    /// <summary>
    /// Fetches the release list of the clicked artist via discogs.
    /// </summary>
    /// <param name="artist">Artist to fetch releases of.</param>
    /// <returns>Task.</returns>
    private async Task<IEnumerable<Release>> ArtistClickedDiscogs(Artist artist)
    {
      var a = _discogsClient.GetArtistReleaseAsEnumerable(int.Parse(artist.Mbid), null, MaxResults);
      var releases = new List<Release>();
      await Task.Run(() =>
      {
        foreach (var r in a)
          releases.Add(new Release(r.title, r.artist, (r.type == "master" && r.main_release != 0) ? r.main_release.ToString() : r.id.ToString(), string.IsNullOrEmpty(r.thumb) ? null : new Uri(r.thumb)));
      });

      return releases;
    }

    /// <summary>
    /// Fetches the release list of the clicked artist via musicbrainz.org.
    /// </summary>
    /// <param name="artist">Artist to fetch releases of.</param>
    /// <returns>Task.</returns>
    private async Task<IEnumerable<Release>> ArtistClickedMusicBrainz(Artist artist)
    {
      var r = await Hqub.MusicBrainz.API.Entities.ReleaseGroup.BrowseAsync("artist", artist.Mbid, MaxResults, 0, "artist-credits");
      return r.Items.Select(i => new Release(i));
    }

    /// <summary>
    /// Fetches the tracklist of the clicked release and displays it.
    /// </summary>
    /// <param name="sender">Clicked release as <see cref="LastAlbum"/>.</param>
    /// <param name="e">Ignored.</param>
    public async void Release_Clicked(object sender, EventArgs e)
    {
      if (EnableControls)
      {
        EnableControls = false;

        try
        {
          var release = sender as Release;
          OnStatusUpdated(string.Format("Trying to fetch tracklist from release '{0}'", release.Name));

          IEnumerable<Track> tracks = new Track[0];
          if (DatabaseToSearch == Database.LastFm)
            tracks = await FetchTracksLastFM(release);
          else if (DatabaseToSearch == Database.Discogs)
            tracks = await FetchTracksDiscogs(release);
          else if (DatabaseToSearch == Database.MusicBrainz)
            tracks = await FetchTracksMusicBrainz(release);

          foreach (var track in tracks)
          {
            var vm = new FetchedTrackViewModel(new ScrobbleBase(track), track.Image);
            Scrobbles.Add(vm);
          }

          if (Scrobbles.Count != 0)
            OnStatusUpdated(string.Format("Successfully fetched tracklist from release '{0}'", release.Name));
          else
            OnStatusUpdated(string.Format("Release '{0}' has no tracks", release.Name));
        }
        catch (Exception ex)
        {
          OnStatusUpdated(string.Format("Fatal error while fetching tracklist from release: {0}", ex.Message));
        }
        finally
        {
          EnableControls = true;
        }
      }
    }

    /// <summary>
    /// Fetches tracks of the given <paramref name="release"/> from Last.fm.
    /// </summary>
    /// <param name="release">Release to get tracks for.</param>
    /// <returns>Enumerable tracks of the given <paramref name="release"/>.</returns>
    private async Task<IEnumerable<Track>> FetchTracksLastFM(Release release)
    {
      LastResponse<LastAlbum> response = null;
      if (!string.IsNullOrEmpty(release.Mbid))
        response = await _lastfmAlbumAPI.GetInfoByMbidAsync(release.Mbid);
      else
        response = await _lastfmAlbumAPI.GetInfoAsync(release.ArtistName, release.Name);

      if (response.Success && response.Status == LastResponseStatus.Successful)
        return response.Content.Tracks.Select(t => new Track(t));
      else
        throw new Exception(response.Status.ToString());
    }

    /// <summary>
    /// Fetches tracks of the given <paramref name="release"/> from discogs.com.
    /// </summary>
    /// <param name="release">Release to get tracks for.</param>
    /// <returns>Enumerable tracks of the given <paramref name="release"/>.</returns>
    private async Task<IEnumerable<Track>> FetchTracksDiscogs(Release release)
    {
      var r = await _discogsClient.GetReleaseAsync(int.Parse(release.Mbid));
      return r.tracklist.Select(i => new Track(i.title, i.artists?.FirstOrDefault()?.name ?? r.artists.First().name, r.title, string.IsNullOrEmpty(r.thumb) ? null : new Uri(r.thumb)));
    }

    /// <summary>
    /// Fetches tracks of the given <paramref name="release"/> from musicbrainz.org.
    /// </summary>
    /// <param name="release">Release to get tracks for.</param>
    /// <returns>Enumerable tracks of the given <paramref name="release"/>.</returns>
    private async Task<IEnumerable<Track>> FetchTracksMusicBrainz(Release release)
    {
      var t = await Hqub.MusicBrainz.API.Entities.ReleaseGroup.GetAsync(release.Mbid, "releases");
      var r = await Hqub.MusicBrainz.API.Entities.Release.GetAsync(t.Releases.First().Id, "media", "recordings");
      return r.Media.First().Tracks.Select(i => new Track(i.Recording.Title, release.ArtistName, release.Name, release.Image));
    }

    /// <summary>
    /// Scrobbles the selected tracks.
    /// </summary>
    /// <remarks>
    /// Scrobbles will be 'reversed' meaning track 1 of the release
    /// will be scrobbled last.
    /// The first track to be scrobbled will have the <see cref="ScrobbleTimeViewModel.Time"/>
    /// as timestamp. The last track (track 1) will have the <see cref="ScrobbleTimeViewModel.Time"/>
    /// minus all the durations of the scrobbles before. 3 minute default duration.
    /// </remarks>
    /// <returns>Task.</returns>
    public override async Task Scrobble()
    {
      try
      {
        EnableControls = false;
        OnStatusUpdated("Trying to scrobble selected tracks...");

        var response = await Scrobbler.ScrobbleAsync(CreateScrobbles());
        if (response.Success && response.Status == LastResponseStatus.Successful)
          OnStatusUpdated("Successfully scrobbled selected tracks");
        else
          OnStatusUpdated(string.Format("Error while scrobbling selected tracks: {0}", response.Status));
      }
      catch (Exception ex)
      {
        OnStatusUpdated(string.Format("Fatal error while scrobbling selected tracks: {0}", ex.Message));
      }
      finally
      {
        EnableControls = true;
      }
    }

    /// <summary>
    /// Creates the list of scrobbles.
    /// </summary>
    /// <returns>List with scrobbles.</returns>
    protected override IEnumerable<Scrobble> CreateScrobbles()
    {
      DateTime finishingTime = ScrobbleTimeVM.Time;
      List<Scrobble> scrobbles = new List<Scrobble>();
      foreach (FetchedTrackViewModel vm in Scrobbles.Where(i => i.ToScrobble).Reverse())
      {
        scrobbles.Add(new Scrobble(vm.ArtistName, vm.AlbumName, vm.TrackName, finishingTime));
        if (vm.Duration.HasValue)
          finishingTime = finishingTime.Subtract(vm.Duration.Value);
        else
          finishingTime = finishingTime.Subtract(TimeSpan.FromMinutes(3.0));
      }

      return scrobbles;
    }

    /// <summary>
    /// Switches the <see cref="ActiveItem"/> to the artist view.
    /// </summary>
    public void BackToArtist()
    {
      DeactivateItem(ActiveItem, true);
      ActivateItem(_artistResultVM);
    }

    /// <summary>
    /// Switches the <see cref="ActiveItem"/> to the release view.
    /// </summary>
    public void BackFromTrackResult()
    {
      Scrobbles.Clear();
    }

    #region IConductor Implementation

    /// <summary>
    /// Activates the given <paramref name="item"/>.
    /// </summary>
    /// <param name="item">Item to activate.</param>
    public void ActivateItem(object item)
    {
      _conductor.ActivateItem((Screen)item);
      NotifyOfPropertyChange(() => ActiveItem);
    }

    /// <summary>
    /// Deactivates the given <paramref name="item"/>.
    /// </summary>
    /// <param name="item">Item to deactivate.</param>
    /// <param name="close">If true, the screen is closed.</param>
    public void DeactivateItem(object item, bool close)
    {
      _conductor.DeactivateItem((Screen)item, close);
      NotifyOfPropertyChange(() => ActiveItem);
    }

    /// <summary>
    /// Gets the children.
    /// </summary>
    /// <returns>Children.</returns>
    public IEnumerable GetChildren()
    {
      return _conductor.GetChildren();
    }

    /// <summary>
    /// Fires the <see cref="ActivationProcessed"/> event.
    /// </summary>
    /// <param name="sender">Ignored.</param>
    /// <param name="e">Ignored.</param>
    private void _conductor_ActivationProcessed(object sender, ActivationProcessedEventArgs e)
    {
      ActivationProcessed?.Invoke(sender, e);
    }

    #endregion IConductor Implementation
  }
}