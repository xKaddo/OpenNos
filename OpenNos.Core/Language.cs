﻿/*
 * This file is part of the OpenNos Emulator Project. See AUTHORS file for Copyright information
 *
 * This program is free software; you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation; either version 2 of the License, or
 * (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 */

using OpenNos.Core.LanguageDetection;
using RestSharp;
using RestSharp.Deserializers;
using System.Configuration;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Resources;

namespace OpenNos.Core
{
    public class Language
    {
        #region Members

        private static Language instance;
        private ResourceManager _manager;
        private CultureInfo _resourceCulture;

        #endregion

        #region Instantiation

        private Language()
        {
            _resourceCulture = new CultureInfo(ConfigurationManager.AppSettings["Language"]);
            if (Assembly.GetEntryAssembly() != null)
            {
                _manager = new ResourceManager(Assembly.GetEntryAssembly().GetName().Name + ".Resource.LocalizedResources", Assembly.GetEntryAssembly());
            }
        }

        #endregion

        #region Properties

        public static Language Instance
        {
            get
            {
                return instance ?? (instance = new Language());
            }
        }

        #endregion

        #region Methods

        public bool CheckMessageIsCorrectLanguage(string completeTextString)
        {
            RestClient client = new RestClient("http://ws.detectlanguage.com");
            RestRequest request = new RestRequest("/0.2/detect", Method.POST);

            request.AddParameter("key", ConfigurationManager.AppSettings["DetectLanguageApiKey"]);
            request.AddParameter("q", completeTextString);

            IRestResponse response = client.Execute(request);
            JsonDeserializer deserializer = new JsonDeserializer();

            try
            {
                Result result = deserializer.Deserialize<Result>(response);
                Detection detection = result?.data?.detections.FirstOrDefault();

                if (detection == null)
                {
                    return true;
                }
                return detection.confidence < 10 || detection.language == _resourceCulture.TwoLetterISOLanguageName;
            }
            catch
            {
                return true;
            }
        }

        public string GetMessageFromKey(string message)
        {
            string resourceMessage = _manager != null ? _manager.GetString(message, _resourceCulture) : string.Empty;

            return !string.IsNullOrEmpty(resourceMessage) ? resourceMessage : $"#<{message}>";
        }

        #endregion
    }
}