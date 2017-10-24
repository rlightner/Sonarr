﻿using System;

namespace NzbDrone.Core.IndexerSearch.Definitions
{
    public class SpecialEpisodeSearchCriteria : SearchCriteriaBase
    {
        public string[] EpisodeQueryTitles { get; set; }

        public override string ToString()
        {
            return string.Format("[{0} : {1}]", Series.Title, string.Join(",", EpisodeQueryTitles));
        }
    }
}
