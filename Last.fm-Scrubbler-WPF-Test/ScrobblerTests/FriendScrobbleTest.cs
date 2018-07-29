﻿using IF.Lastfm.Core.Api;
using IF.Lastfm.Core.Api.Helpers;
using IF.Lastfm.Core.Objects;
using IF.Lastfm.Core.Scrobblers;
using Scrubbler.Interfaces;
using Moq;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Scrubbler.ViewModels.ScrobbleViewModels;

namespace Scrubbler.Test.ScrobblerTests
{
  /// <summary>
  /// Tests for the <see cref="FriendScrobbleViewModel"/>.
  /// </summary>
  [TestFixture]
  class FriendScrobbleTest
  {
    /// <summary>
    /// Tests the <see cref="FriendScrobbleViewModel.Scrobble"/> function.
    /// </summary>
    /// <returns>Task.</returns>
    [Test]
    public async Task ScrobbleTest()
    {
      //given: FriendScrobbleViewModel with mocked userapi
      var expected = TestHelper.CreateGenericScrobbles(3);

      IEnumerable<Scrobble> actual = null;
      Mock<IUserScrobbler> scrobblerMock = new Mock<IUserScrobbler>(MockBehavior.Strict);
      scrobblerMock.Setup(u => u.ScrobbleAsync(It.IsAny<IEnumerable<Scrobble>>(), false)).Callback<IEnumerable<Scrobble>, bool>((s, c) => actual = s)
                                                                                  .Returns(Task.Run(() => new ScrobbleResponse()));
      scrobblerMock.Setup(u => u.IsAuthenticated).Returns(true);


      Mock<IUserApi> userApiMock = new Mock<IUserApi>();
      userApiMock.Setup(i => i.GetRecentScrobbles(It.IsAny<string>(), It.IsAny<DateTimeOffset?>(), It.IsAny<int>(), It.IsAny<int>())).Returns(()
                                                  => Task.Run(() => PageResponse<LastTrack>.CreateSuccessResponse(expected.ToLastTracks())));

      FriendScrobbleViewModel vm = new FriendScrobbleViewModel(null, userApiMock.Object)
      {
        Scrobbler = scrobblerMock.Object,
        Username = "TestUser"
      };

      await vm.FetchScrobbles();
      vm.CheckAll();

      // when: scrobbling the fetched tracks.
      await vm.Scrobble();

      // we add 1 second to each TimePlayed of the expected because the vm does this too so you can scrobble from yourself...
      for(int i = 0; i < expected.Length; i++)
        expected[i] = expected[i].CloneWithAddedSecond();

      // then: scrobbled tracks should be equal to the given tracks.
      Assert.That(actual.IsEqualScrobble(expected), Is.True);
    }
  }
}