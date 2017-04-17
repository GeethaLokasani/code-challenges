﻿using System;
using System.Collections.Generic;
using System.Configuration;
using System.Threading.Tasks;
using System.Web.Mvc;
using LabWeb.Models;
using Microsoft.Azure.Documents;
using Microsoft.Azure.Documents.Client;
using Microsoft.Azure.Documents.Linq;
using Newtonsoft.Json;

namespace LabWeb.Controllers
{
    public class HomeController : Controller
    {
        private object obj = new object();
        private DocumentClient _readonlyClient;

        public ActionResult Index()
        {
            return View();
        }

        public DocumentClient ReadOnlyClient
        {
            get
            {
                if (_readonlyClient == null)
                {
                    lock (obj)
                    {
                        _readonlyClient = new DocumentClient(new Uri(
                            ConfigurationManager.AppSettings["DocumentDBEndpoint"]),
                            ConfigurationManager.AppSettings["DocumentDBPrimaryReadonlyKey"]);
                    }
                }

                return _readonlyClient;
            }
        }

        public async Task<ActionResult> Query(string query)
        {
            var newModel = new QueryModel
            {
                Documents = new List<string>(),
                Query = query
            };
            int numRetries = 0;
            IDocumentQuery<dynamic> docQuery = null;

            if (docQuery != null)
            {
                do
                {
                    try
                    {
                        var results = await docQuery.ExecuteNextAsync();

                        foreach (dynamic result in results)
                        {
                            string json = result.ToString();
                            string formattedJson = (json.StartsWith("{", StringComparison.InvariantCulture) ||
                                                    json.StartsWith("[", StringComparison.InvariantCulture))
                                ? JsonConvert.SerializeObject(JsonConvert.DeserializeObject(json), Formatting.Indented)
                                : json;
                            newModel.Documents.Add(formattedJson);
                            newModel.Count++;
                        }
                        newModel.Error = null;
                        newModel.StatusCode = 200;
                        break;
                    }
                    catch (Exception e)
                    {
                        newModel.Documents.Clear();
                        numRetries++;
                        var exception = e.InnerException as DocumentClientException;
                        if (exception != null)
                        {
                            if (exception.StatusCode != null && (int) exception.StatusCode == 429)
                            {
                                numRetries--;
                            }
                            int startIndex = exception.Message.IndexOf("{", StringComparison.InvariantCulture);
                            int endIndex = exception.Message.LastIndexOf("}", StringComparison.InvariantCulture) + 1;
                            newModel.Error = (startIndex < 0 || endIndex < 0)
                                ? exception.Message
                                : exception.Message.Substring(startIndex, endIndex - startIndex);
                            if (exception.StatusCode != null) newModel.StatusCode = (int) exception.StatusCode;
                        }
                        else
                        {
                            newModel.Error = e.Message;
                        }
                    }
                } while (numRetries < 1);
            }

            Response.Write(JsonConvert.SerializeObject(newModel));
            return null;
        }
    }
}