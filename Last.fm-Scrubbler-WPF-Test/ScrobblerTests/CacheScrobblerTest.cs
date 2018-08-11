﻿using IF.Lastfm.Core.Objects;
using IF.Lastfm.Core.Scrobblers;
using Moq;
using NUnit.Framework;
using Scrubbler.Helper;
using Scrubbler.Scrobbling;
using Scrubbler.Scrobbling.Scrobbler;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Scrubbler.Test.ScrobblerTests
{
  /// <summary>
  /// Tests for the <see cref="CacheScrobblerViewModel"/>.
  /// </summary>
  [TestFixture]
  class CacheScrobblerTest
  {
    /// <summary>
    /// Tests the <see cref="CacheScrobblerViewModel.Scrobble"/> function.
    /// </summary>
    /// <returns>Task.</returns>
    [Test]
    public async Task ScrobbleTest()
    {
      // given: CacheScrobbleViewModel and mocks
      var scrobbles = TestHelper.CreateGenericScrobbles(3);

      Mock<IUserScrobbler> scrobblerMock = new Mock<IUserScrobbler>(MockBehavior.Strict);
      scrobblerMock.Setup(u => u.GetCachedAsync()).Returns(Task.Run(() => scrobbles.AsEnumerable()));
      scrobblerMock.Setup(u => u.SendCachedScrobblesAsync()).Returns(Task.Run(() => new ScrobbleResponse()));
      scrobblerMock.Setup(u => u.IsAuthenticated).Returns(true);

      Mock<IExtendedWindowManager> windowManagerMock = new Mock<IExtendedWindowManager>(MockBehavior.Strict);

      CacheScrobblerViewModel vm = new CacheScrobblerViewModel(windowManagerMock.Object)
      {
        Scrobbler = scrobblerMock.Object
      };

      await vm.GetCachedScrobbles();

      // when: scrobbling the cached tracks
      await vm.Scrobble();

      // then: send has been called
      Assert.That(() => scrobblerMock.Verify(u => u.SendCachedScrobblesAsync(), Times.Once), Throws.Nothing);
    }
  }
}