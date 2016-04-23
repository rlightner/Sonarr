﻿namespace NzbDrone.Core.Download.Clients.Sabnzbd
{
    public enum SabnzbdDownloadStatus
    {
        Grabbing,
        Queued,
        Paused,
        Checking,
        Downloading,
        ToPP,
        QuickCheck,
        Verifying,
        Repairing,
        Fetching,       // Fetching additional blocks
        Extracting,
        Moving,
        Running,        // Running PP Script
        Completed,
        Failed,
        Deleted
    }
}
