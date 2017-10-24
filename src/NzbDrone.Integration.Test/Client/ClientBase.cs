﻿using System.Collections.Generic;
using System.Net;
using FluentAssertions;
using NLog;
using NzbDrone.Api;
using NzbDrone.Api.REST;
using NzbDrone.Common.Serializer;
using RestSharp;
using System.Linq;

namespace NzbDrone.Integration.Test.Client
{
    public class ClientBase<TResource> where TResource : RestResource, new()
    {
        private readonly IRestClient _restClient;
        private readonly string _resource;
        private readonly string _apiKey;
        private readonly Logger _logger;

        public ClientBase(IRestClient restClient, string apiKey, string resource = null)
        {
            if (resource == null)
            {
                resource = new TResource().ResourceName;
            }

            _restClient = restClient;
            _resource = resource;
            _apiKey = apiKey;

            _logger = LogManager.GetLogger("REST");
        }

        public List<TResource> All()
        {
            var request = BuildRequest();
            return Get<List<TResource>>(request);
        }

        public PagingResource<TResource> GetPaged(int pageNumber, int pageSize, string sortKey, string sortDir)
        {
            var request = BuildRequest();
            request.AddParameter("page", pageNumber);
            request.AddParameter("pageSize", pageSize);
            request.AddParameter("sortKey", sortKey);
            request.AddParameter("sortDir", sortDir);
            return Get<PagingResource<TResource>>(request);
        }

        public TResource Post(TResource body, HttpStatusCode statusCode = HttpStatusCode.Created)
        {
            var request = BuildRequest();
            request.AddBody(body);
            return Post<TResource>(request, statusCode);
        }

        public TResource Put(TResource body, HttpStatusCode statusCode = HttpStatusCode.Accepted)
        {
            var request = BuildRequest();
            request.AddBody(body);
            return Put<TResource>(request, statusCode);
        }

        public TResource Get(int id, HttpStatusCode statusCode = HttpStatusCode.OK)
        {
            var request = BuildRequest(id.ToString());
            return Get<TResource>(request, statusCode);
        }

        public TResource GetSingle(HttpStatusCode statusCode = HttpStatusCode.OK)
        {
            var request = BuildRequest();
            return Get<TResource>(request, statusCode);
        }

        public void Delete(int id)
        {
            var request = BuildRequest(id.ToString());
            Delete(request);
        }

        public List<dynamic> InvalidPost(TResource body)
        {
            var request = BuildRequest();
            request.AddBody(body);
            return Post<List<dynamic>>(request, HttpStatusCode.BadRequest);
        }

        public List<dynamic> InvalidPut(TResource body)
        {
            var request = BuildRequest();
            request.AddBody(body);
            return Put<List<dynamic>>(request, HttpStatusCode.BadRequest);
        }

        public RestRequest BuildRequest(string command = "")
        {
            var request = new RestRequest(_resource + "/" + command.Trim('/'))
                {
                    RequestFormat = DataFormat.Json,
                };

            request.AddHeader("Authorization", _apiKey);
            request.AddHeader("X-Api-Key", _apiKey);

            return request;
        }

        public T Get<T>(IRestRequest request, HttpStatusCode statusCode = HttpStatusCode.OK) where T : class, new()
        {
            request.Method = Method.GET;
            return Execute<T>(request, statusCode);
        }

        public T Post<T>(IRestRequest request, HttpStatusCode statusCode = HttpStatusCode.Created) where T : class, new()
        {
            request.Method = Method.POST;
            return Execute<T>(request, statusCode);
        }

        public T Put<T>(IRestRequest request, HttpStatusCode statusCode = HttpStatusCode.Accepted) where T : class, new()
        {
            request.Method = Method.PUT;
            return Execute<T>(request, statusCode);
        }

        public void Delete(IRestRequest request, HttpStatusCode statusCode = HttpStatusCode.OK)
        {
            request.Method = Method.DELETE;
            Execute<object>(request, statusCode);
        }

        private T Execute<T>(IRestRequest request, HttpStatusCode statusCode) where T : class, new()
        {
            _logger.Info("{0}: {1}", request.Method, _restClient.BuildUri(request));

            var response = _restClient.Execute(request);
            _logger.Info("Response: {0}", response.Content);

            if (response.ErrorException != null)
            {
                throw response.ErrorException;
            }

            AssertDisableCache(response.Headers);

            response.ErrorMessage.Should().BeNullOrWhiteSpace();

            response.StatusCode.Should().Be(statusCode);

            return Json.Deserialize<T>(response.Content);
        }

        private static void AssertDisableCache(IList<Parameter> headers)
        {
            headers.Single(c => c.Name == "Cache-Control").Value.Should().Be("no-cache, no-store, must-revalidate");
            headers.Single(c => c.Name == "Pragma").Value.Should().Be("no-cache");
            headers.Single(c => c.Name == "Expires").Value.Should().Be("0");
        }
    }
}