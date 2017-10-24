using System;
using System.Linq;
using System.Collections.Generic;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Common.Http;
using NzbDrone.Core.MediaFiles.TorrentInfo;
using NzbDrone.Core.Download;
using NzbDrone.Core.Download.Clients.Transmission;

namespace NzbDrone.Core.Test.Download.DownloadClientTests.TransmissionTests
{
    [TestFixture]
    public class TransmissionFixture : DownloadClientFixtureBase<Transmission>
    {
        protected TransmissionSettings _settings;
        protected TransmissionTorrent _queued;
        protected TransmissionTorrent _downloading;
        protected TransmissionTorrent _failed;
        protected TransmissionTorrent _completed;
        protected TransmissionTorrent _magnet;
        protected Dictionary<string, object> _transmissionConfigItems;

        [SetUp]
        public void Setup()
        {
            _settings = new TransmissionSettings
            {
                Host = "127.0.0.1",
                Port = 2222,
                Username = "admin",
                Password = "pass"
            };

            Subject.Definition = new DownloadClientDefinition();
            Subject.Definition.Settings = _settings;

            _queued = new TransmissionTorrent
                    {
                        HashString = "HASH",
                        IsFinished = false,
                        Status = TransmissionTorrentStatus.Queued,
                        Name = _title,
                        TotalSize = 1000,
                        LeftUntilDone = 1000,
                        DownloadDir = "somepath"
                    };

            _downloading = new TransmissionTorrent
                {
                    HashString = "HASH",
                    IsFinished = false,
                    Status = TransmissionTorrentStatus.Downloading,
                    Name = _title,
                    TotalSize = 1000,
                    LeftUntilDone = 100,
                    DownloadDir = "somepath"
                };

            _failed = new TransmissionTorrent
                    {
                        HashString = "HASH",
                        IsFinished = false,
                        Status = TransmissionTorrentStatus.Stopped,
                        Name = _title,
                        TotalSize = 1000,
                        LeftUntilDone = 100,
                        ErrorString = "Error",
                        DownloadDir = "somepath"
                    };

            _completed = new TransmissionTorrent
                    {
                        HashString = "HASH",
                        IsFinished = true,
                        Status = TransmissionTorrentStatus.Stopped,
                        Name = _title,
                        TotalSize = 1000,
                        LeftUntilDone = 0,
                        DownloadDir = "somepath"
                    };

            _magnet = new TransmissionTorrent
            {
                HashString = "HASH",
                IsFinished = false,
                Status = TransmissionTorrentStatus.Downloading,
                Name = _title,
                TotalSize = 0,
                LeftUntilDone = 100,
                DownloadDir = "somepath"
            };

            Mocker.GetMock<ITorrentFileInfoReader>()
                  .Setup(s => s.GetHashFromTorrentFile(It.IsAny<byte[]>()))
                  .Returns("CBC2F069FE8BB2F544EAE707D75BCD3DE9DCF951");

            Mocker.GetMock<IHttpClient>()
                  .Setup(s => s.Get(It.IsAny<HttpRequest>()))
                  .Returns<HttpRequest>(r => new HttpResponse(r, new HttpHeader(), new byte[0]));

            _transmissionConfigItems = new Dictionary<string, object>();

            _transmissionConfigItems.Add("download-dir", @"C:/Downloads/Finished/transmission");
            _transmissionConfigItems.Add("incomplete-dir", null);
            _transmissionConfigItems.Add("incomplete-dir-enabled", false);

            Mocker.GetMock<ITransmissionProxy>()
                .Setup(v => v.GetConfig(It.IsAny<TransmissionSettings>()))
                .Returns(_transmissionConfigItems);

        }

        protected void GivenTvCategory()
        {
            _settings.TvCategory = "sonarr";
        }

        protected void GivenTvDirectory()
        {
            _settings.TvDirectory = @"C:/Downloads/Finished/sonarr";
        }

        protected void GivenFailedDownload()
        {
            Mocker.GetMock<ITransmissionProxy>()
                .Setup(s => s.AddTorrentFromUrl(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TransmissionSettings>()))
                .Throws<InvalidOperationException>();
        }

        protected void GivenSuccessfulDownload()
        {
            Mocker.GetMock<IHttpClient>()
                  .Setup(s => s.Get(It.IsAny<HttpRequest>()))
                  .Returns<HttpRequest>(r => new HttpResponse(r, new HttpHeader(), new byte[1000]));

            Mocker.GetMock<ITransmissionProxy>()
                .Setup(s => s.AddTorrentFromUrl(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TransmissionSettings>()))
                .Callback(PrepareClientToReturnQueuedItem);

            Mocker.GetMock<ITransmissionProxy>()
                .Setup(s => s.AddTorrentFromData(It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<TransmissionSettings>()))
                .Callback(PrepareClientToReturnQueuedItem);
        }
        
        protected virtual void GivenTorrents(List<TransmissionTorrent> torrents)
        {
            if (torrents == null)
            {
                torrents = new List<TransmissionTorrent>();
            }

            Mocker.GetMock<ITransmissionProxy>()
                .Setup(s => s.GetTorrents(It.IsAny<TransmissionSettings>()))
                .Returns(torrents);
        }

        protected void PrepareClientToReturnQueuedItem()
        {
            GivenTorrents(new List<TransmissionTorrent> 
                {
                    _queued
                });
        }

        protected void PrepareClientToReturnDownloadingItem()
        {
            GivenTorrents(new List<TransmissionTorrent> 
                {
                    _downloading
                });
        }

        protected void PrepareClientToReturnFailedItem()
        {
            GivenTorrents(new List<TransmissionTorrent> 
                {
                    _failed
                });
        }

        protected void PrepareClientToReturnCompletedItem()
        {
            GivenTorrents(new List<TransmissionTorrent>
                {
                    _completed
                });
        }

        protected void PrepareClientToReturnMagnetItem()
        {
            GivenTorrents(new List<TransmissionTorrent>
                {
                    _magnet
                });
        }

        [Test]
        public void queued_item_should_have_required_properties()
        {
            PrepareClientToReturnQueuedItem();
            var item = Subject.GetItems().Single();
            VerifyQueued(item);
        }

        [Test]
        public void downloading_item_should_have_required_properties()
        {
            PrepareClientToReturnDownloadingItem();
            var item = Subject.GetItems().Single();
            VerifyDownloading(item);
        }

        [Test]
        public void failed_item_should_have_required_properties()
        {
            PrepareClientToReturnFailedItem();
            var item = Subject.GetItems().Single();
            VerifyWarning(item);
        }

        [Test]
        public void completed_download_should_have_required_properties()
        {
            PrepareClientToReturnCompletedItem();
            var item = Subject.GetItems().Single();
            VerifyCompleted(item);
        }

        [Test]
        public void magnet_download_should_not_return_the_item()
        {
            PrepareClientToReturnMagnetItem();
            Subject.GetItems().Count().Should().Be(0);
        }

        [Test]
        public void Download_should_return_unique_id()
        {
            GivenSuccessfulDownload();

            var remoteEpisode = CreateRemoteEpisode();

            var id = Subject.Download(remoteEpisode);

            id.Should().NotBeNullOrEmpty();
        }

        [Test]
        public void Download_with_TvDirectory_should_force_directory()
        {
            GivenTvDirectory();
            GivenSuccessfulDownload();

            var remoteEpisode = CreateRemoteEpisode();

            var id = Subject.Download(remoteEpisode);

            id.Should().NotBeNullOrEmpty();

            Mocker.GetMock<ITransmissionProxy>()
                .Verify(v => v.AddTorrentFromData(It.IsAny<byte[]>(), @"C:/Downloads/Finished/sonarr", It.IsAny<TransmissionSettings>()), Times.Once());
        }

        [Test]
        public void Download_with_category_should_force_directory()
        {
            GivenTvCategory();
            GivenSuccessfulDownload();

            var remoteEpisode = CreateRemoteEpisode();

            var id = Subject.Download(remoteEpisode);

            id.Should().NotBeNullOrEmpty();

            Mocker.GetMock<ITransmissionProxy>()
                .Verify(v => v.AddTorrentFromData(It.IsAny<byte[]>(), @"C:/Downloads/Finished/transmission/sonarr", It.IsAny<TransmissionSettings>()), Times.Once());
        }

        [Test]
        public void Download_with_category_should_not_have_double_slashes()
        {
            GivenTvCategory();
            GivenSuccessfulDownload();

            _transmissionConfigItems["download-dir"] += "/";

            var remoteEpisode = CreateRemoteEpisode();

            var id = Subject.Download(remoteEpisode);

            id.Should().NotBeNullOrEmpty();

            Mocker.GetMock<ITransmissionProxy>()
                .Verify(v => v.AddTorrentFromData(It.IsAny<byte[]>(), @"C:/Downloads/Finished/transmission/sonarr", It.IsAny<TransmissionSettings>()), Times.Once());
        }

        [Test]
        public void Download_without_TvDirectory_and_Category_should_use_default()
        {
            GivenSuccessfulDownload();

            var remoteEpisode = CreateRemoteEpisode();

            var id = Subject.Download(remoteEpisode);

            id.Should().NotBeNullOrEmpty();

            Mocker.GetMock<ITransmissionProxy>()
                .Verify(v => v.AddTorrentFromData(It.IsAny<byte[]>(), null, It.IsAny<TransmissionSettings>()), Times.Once());
        }

        [TestCase("magnet:?xt=urn:btih:ZPBPA2P6ROZPKRHK44D5OW6NHXU5Z6KR&tr=udp", "CBC2F069FE8BB2F544EAE707D75BCD3DE9DCF951")]
        public void Download_should_get_hash_from_magnet_url(string magnetUrl, string expectedHash)
        {
            GivenSuccessfulDownload();

            var remoteEpisode = CreateRemoteEpisode();
            remoteEpisode.Release.DownloadUrl = magnetUrl;

            var id = Subject.Download(remoteEpisode);

            id.Should().Be(expectedHash);
        }

        [TestCase(TransmissionTorrentStatus.Stopped, DownloadItemStatus.Downloading)]
        [TestCase(TransmissionTorrentStatus.CheckWait, DownloadItemStatus.Downloading)]
        [TestCase(TransmissionTorrentStatus.Check, DownloadItemStatus.Downloading)]
        [TestCase(TransmissionTorrentStatus.Queued, DownloadItemStatus.Queued)]
        [TestCase(TransmissionTorrentStatus.Downloading, DownloadItemStatus.Downloading)]
        [TestCase(TransmissionTorrentStatus.SeedingWait, DownloadItemStatus.Completed)]
        [TestCase(TransmissionTorrentStatus.Seeding, DownloadItemStatus.Completed)]
        public void GetItems_should_return_queued_item_as_downloadItemStatus(TransmissionTorrentStatus apiStatus, DownloadItemStatus expectedItemStatus)
        {
            _queued.Status = apiStatus;

            PrepareClientToReturnQueuedItem();

            var item = Subject.GetItems().Single();

            item.Status.Should().Be(expectedItemStatus);
        }

        [TestCase(TransmissionTorrentStatus.Queued, DownloadItemStatus.Queued)]
        [TestCase(TransmissionTorrentStatus.Downloading, DownloadItemStatus.Downloading)]
        [TestCase(TransmissionTorrentStatus.Seeding, DownloadItemStatus.Completed)]
        public void GetItems_should_return_downloading_item_as_downloadItemStatus(TransmissionTorrentStatus apiStatus, DownloadItemStatus expectedItemStatus)
        {
            _downloading.Status = apiStatus;

            PrepareClientToReturnDownloadingItem();

            var item = Subject.GetItems().Single();

            item.Status.Should().Be(expectedItemStatus);
        }

        [TestCase(TransmissionTorrentStatus.Stopped, DownloadItemStatus.Completed, false)]
        [TestCase(TransmissionTorrentStatus.CheckWait, DownloadItemStatus.Downloading, true)]
        [TestCase(TransmissionTorrentStatus.Check, DownloadItemStatus.Downloading, true)]
        [TestCase(TransmissionTorrentStatus.Queued, DownloadItemStatus.Completed, true)]
        [TestCase(TransmissionTorrentStatus.SeedingWait, DownloadItemStatus.Completed, true)]
        [TestCase(TransmissionTorrentStatus.Seeding, DownloadItemStatus.Completed, true)]
        public void GetItems_should_return_completed_item_as_downloadItemStatus(TransmissionTorrentStatus apiStatus, DownloadItemStatus expectedItemStatus, bool expectedReadOnly)
        {
            _completed.Status = apiStatus;
            
            PrepareClientToReturnCompletedItem();

            var item = Subject.GetItems().Single();

            item.Status.Should().Be(expectedItemStatus);
            item.IsReadOnly.Should().Be(expectedReadOnly);
        }

        [Test]
        public void should_return_status_with_outputdirs()
        {
            var result = Subject.GetStatus();

            result.IsLocalhost.Should().BeTrue();
            result.OutputRootFolders.Should().NotBeNull();
            result.OutputRootFolders.First().Should().Be(@"C:\Downloads\Finished\transmission");
        }

        [Test]
        public void should_exclude_items_not_in_category()
        {
            GivenTvCategory();

            _downloading.DownloadDir = @"C:/Downloads/Finished/transmission/sonarr";

            GivenTorrents(new List<TransmissionTorrent> 
                {
                    _downloading,
                    _queued
                });

            var items = Subject.GetItems().ToList();

            items.Count.Should().Be(1);
            items.First().Status.Should().Be(DownloadItemStatus.Downloading);
        }

        public void should_exclude_items_not_in_TvDirectory()
        {
            GivenTvDirectory();

            _downloading.DownloadDir = @"C:/Downloads/Finished/sonarr/subdir";

            GivenTorrents(new List<TransmissionTorrent> 
                {
                    _downloading,
                    _queued
                });

            var items = Subject.GetItems().ToList();

            items.Count.Should().Be(1);
            items.First().Status.Should().Be(DownloadItemStatus.Downloading);
        }

        [Test]
        public void should_fix_forward_slashes()
        {
            WindowsOnly();

            _downloading.DownloadDir = @"C:/Downloads/Finished/transmission";

            GivenTorrents(new List<TransmissionTorrent> 
                {
                    _downloading
                });

            var items = Subject.GetItems().ToList();

            items.Should().HaveCount(1);
            items.First().OutputPath.Should().Be(@"C:\Downloads\Finished\transmission\" + _title);
        }

        [TestCase("2.84 ()")]
        [TestCase("2.84+ ()")]
        [TestCase("2.84 (other info)")]
        [TestCase("2.84 (2.84)")]
        public void should_version_should_only_check_version_number(string version)
        {
            Mocker.GetMock<ITransmissionProxy>()
                  .Setup(s => s.GetVersion(It.IsAny<TransmissionSettings>()))
                  .Returns(version);

            Subject.Test();
        }

        [TestCase(-1)] // Infinite/Unknown
        [TestCase(-2)] // Magnet Downloading
        public void should_ignore_negative_eta(int eta)
        {
            _completed.Eta = eta;

            PrepareClientToReturnCompletedItem();
            var item = Subject.GetItems().Single();
            item.RemainingTime.Should().NotHaveValue();
        }
    }
}
