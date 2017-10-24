﻿using System;
using System.IO;
using NLog;
using NzbDrone.Common.EnvironmentInfo;

namespace NzbDrone.Common.Processes
{
    public interface IProvidePidFile
    {
        void Write();
    }

    public class PidFileProvider : IProvidePidFile
    {
        private readonly IAppFolderInfo _appFolderInfo;
        private readonly IProcessProvider _processProvider;
        private readonly Logger _logger;

        public PidFileProvider(IAppFolderInfo appFolderInfo, IProcessProvider processProvider, Logger logger)
        {
            _appFolderInfo = appFolderInfo;
            _processProvider = processProvider;
            _logger = logger;
        }

        public void Write()
        {
            if (OsInfo.IsWindows)
            {
                return;
            }

            var filename = Path.Combine(_appFolderInfo.AppDataFolder, "nzbdrone.pid");
            try
            {
                File.WriteAllText(filename, _processProvider.GetCurrentProcessId().ToString());
            }
            catch (Exception ex)
            {
                _logger.ErrorException("Unable to write PID file: " + filename, ex);
                throw;
            }
        }
    }
}
