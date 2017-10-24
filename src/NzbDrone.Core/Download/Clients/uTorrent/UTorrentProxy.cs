﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using NLog;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Rest;
using RestSharp;

namespace NzbDrone.Core.Download.Clients.UTorrent
{
    public interface IUTorrentProxy
    {
        int GetVersion(UTorrentSettings settings);
        Dictionary<string, string> GetConfig(UTorrentSettings settings);
        List<UTorrentTorrent> GetTorrents(UTorrentSettings settings);

        void AddTorrentFromUrl(string torrentUrl, UTorrentSettings settings);
        void AddTorrentFromFile(string fileName, byte[] fileContent, UTorrentSettings settings);
        void SetTorrentSeedingConfiguration(string hash, TorrentSeedConfiguration seedConfiguration, UTorrentSettings settings);

        void RemoveTorrent(string hash, bool removeData, UTorrentSettings settings);
        void SetTorrentLabel(string hash, string label, UTorrentSettings settings);
        void MoveTorrentToTopInQueue(string hash, UTorrentSettings settings);
    }

    public class UTorrentProxy : IUTorrentProxy
    {
        private readonly Logger _logger;
        private readonly CookieContainer _cookieContainer;
        private string _authToken;

        public UTorrentProxy(Logger logger)
        {
            _logger = logger;
            _cookieContainer = new CookieContainer();
        }

        public int GetVersion(UTorrentSettings settings)
        {
            var arguments = new Dictionary<string, object>();
            arguments.Add("action", "getsettings");

            var result = ProcessRequest(arguments, settings);

            return result.Build;
        }

        public Dictionary<string, string> GetConfig(UTorrentSettings settings)
        {
            var arguments = new Dictionary<string, object>();
            arguments.Add("action", "getsettings");

            var result = ProcessRequest(arguments, settings);

            var configuration = new Dictionary<string, string>();

            foreach (var configItem in result.Settings)
            {
                configuration.Add(configItem[0].ToString(), configItem[2].ToString());
            }

            return configuration;
        }

        public List<UTorrentTorrent> GetTorrents(UTorrentSettings settings)
        {
            var arguments = new Dictionary<string, object>();
            arguments.Add("list", 1);

            var result = ProcessRequest(arguments, settings);

            return result.Torrents;
        }

        public void AddTorrentFromUrl(string torrentUrl, UTorrentSettings settings)
        {
            var arguments = new Dictionary<string, object>();
            arguments.Add("action", "add-url");
            arguments.Add("s", torrentUrl);

            ProcessRequest(arguments, settings);
        }

        public void AddTorrentFromFile(string fileName, byte[] fileContent, UTorrentSettings settings)
        {
            var arguments = new Dictionary<string, object>();
            arguments.Add("action", "add-file");
            arguments.Add("path", string.Empty);

            var client = BuildClient(settings);

            // add-file should use POST unlike all other methods which are GET
            var request = new RestRequest(Method.POST);
            request.RequestFormat = DataFormat.Json;
            request.Resource = "/gui/";
            request.AddParameter("token", _authToken, ParameterType.QueryString);

            foreach (var argument in arguments)
            {
                request.AddParameter(argument.Key, argument.Value, ParameterType.QueryString);
            }
            
            request.AddFile("torrent_file", fileContent, fileName, @"application/octet-stream");

            ProcessRequest(request, client);
        }

        public void SetTorrentSeedingConfiguration(string hash, TorrentSeedConfiguration seedConfiguration, UTorrentSettings settings)
        {
            var arguments = new List<KeyValuePair<string, object>>();
            arguments.Add("action", "setprops");
            arguments.Add("hash", hash);

            arguments.Add("s", "seed_override");
            arguments.Add("v", 1);

            if (seedConfiguration.Ratio != null)
            {
                arguments.Add("s","seed_ratio");
                arguments.Add("v", Convert.ToInt32(seedConfiguration.Ratio.Value * 1000));
            }

            if (seedConfiguration.SeedTime != null)
            {
                arguments.Add("s", "seed_time");
                arguments.Add("v", Convert.ToInt32(seedConfiguration.SeedTime.Value.TotalSeconds));
            }

            ProcessRequest(arguments, settings);
        }

        public void RemoveTorrent(string hash, bool removeData, UTorrentSettings settings)
        {
            var arguments = new Dictionary<string, object>();

            if (removeData)
            {
                arguments.Add("action", "removedata");
            }
            else
            {
                arguments.Add("action", "remove");
            }

            arguments.Add("hash", hash);

            ProcessRequest(arguments, settings);
        }

        public void SetTorrentLabel(string hash, string label, UTorrentSettings settings)
        {
            var arguments = new Dictionary<string, object>();
            arguments.Add("action", "setprops");
            arguments.Add("hash", hash);

            arguments.Add("s", "label");
            arguments.Add("v", label);

            ProcessRequest(arguments, settings);
        }

        public void MoveTorrentToTopInQueue(string hash, UTorrentSettings settings)
        {
            var arguments = new Dictionary<string, object>();
            arguments.Add("action", "queuetop");
            arguments.Add("hash", hash);

            ProcessRequest(arguments, settings);
        }

        public UTorrentResponse ProcessRequest(IEnumerable<KeyValuePair<string, object>> arguments, UTorrentSettings settings)
        {
            var client = BuildClient(settings);

            var request = new RestRequest(Method.GET);
            request.RequestFormat = DataFormat.Json;
            request.Resource = "/gui/";
            request.AddParameter("token", _authToken, ParameterType.QueryString);

            foreach (var argument in arguments)
            {
                request.AddParameter(argument.Key, argument.Value, ParameterType.QueryString);
            }

            return ProcessRequest(request, client);
        }

        private UTorrentResponse ProcessRequest(IRestRequest request, IRestClient client)
        {
            _logger.Debug("Url: {0}", client.BuildUri(request));
            var clientResponse = client.Execute(request);

            if (clientResponse.StatusCode == HttpStatusCode.BadRequest)
            {
                // Token has expired. If the settings were incorrect or the API is disabled we'd have gotten an error 400 during GetAuthToken
                _logger.Debug("uTorrent authentication token error.");

                _authToken = GetAuthToken(client);

                request.Parameters.First(v => v.Name == "token").Value = _authToken;
                clientResponse = client.Execute(request);
            }
            else if (clientResponse.StatusCode == HttpStatusCode.Unauthorized)
            {
                throw new DownloadClientAuthenticationException("Failed to authenticate");
            }

            var uTorrentResult = clientResponse.Read<UTorrentResponse>(client);

            return uTorrentResult;
        }

        private string GetAuthToken(IRestClient client)
        {
            var request = new RestRequest();
            request.RequestFormat = DataFormat.Json;
            request.Resource = "/gui/token.html";

            _logger.Debug("Url: {0}", client.BuildUri(request));
            var response = client.Execute(request);

            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                throw new DownloadClientAuthenticationException("Failed to authenticate");
            }

            response.ValidateResponse(client);

            var xmlDoc = new System.Xml.XmlDocument();
            xmlDoc.LoadXml(response.Content);

            var authToken = xmlDoc.FirstChild.FirstChild.InnerText;

            _logger.Debug("uTorrent AuthToken={0}", authToken);

            return authToken;
        }

        private IRestClient BuildClient(UTorrentSettings settings)
        {
            var url = string.Format(@"http://{0}:{1}",
                                 settings.Host,
                                 settings.Port);

            var restClient = RestClientFactory.BuildClient(url);

            restClient.Authenticator = new HttpBasicAuthenticator(settings.Username, settings.Password);
            restClient.CookieContainer = _cookieContainer;

            if (_authToken.IsNullOrWhiteSpace())
            {
                // µTorrent requires a token and cookie for authentication. The cookie is set automatically when getting the token.
                _authToken = GetAuthToken(restClient);
            }

            return restClient;
        }
    }
}
