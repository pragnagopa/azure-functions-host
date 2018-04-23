﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using Microsoft.Azure.WebJobs.Script.Config;
using Newtonsoft.Json;

namespace Microsoft.Azure.WebJobs.Script.WebHost.Models
{
    public class AssignmentContext
    {
        [JsonProperty("siteId")]
        public int SiteId { get; set; }

        [JsonProperty("siteName")]
        public string SiteName { get; set; }

        [JsonProperty("environment")]
        public Dictionary<string, string> Environment { get; set; }

        [JsonProperty("lastModifiedTime")]
        public DateTime LastModifiedTime { get; set; }

        public string ZipUrl
        {
            get
            {
                if (Environment.ContainsKey(ScriptConstants.RunFromZipSettingName))
                {
                    return Environment[ScriptConstants.RunFromZipSettingName];
                }
                else if (Environment.ContainsKey(ScriptConstants.RunFromZipAltSettingName))
                {
                    return Environment[ScriptConstants.RunFromZipAltSettingName];
                }
                else
                {
                    return string.Empty;
                }
            }
        }

        public bool Equals(AssignmentContext other)
        {
            if (other == null)
            {
                return false;
            }

            return SiteId == other.SiteId && LastModifiedTime.CompareTo(other.LastModifiedTime) == 0;
        }

        public void ApplyAppSettings()
        {
            // apply app settings
            foreach (var pair in Environment)
            {
                System.Environment.SetEnvironmentVariable(pair.Key, pair.Value);
                ScriptSettingsManager.Instance.SetSetting(pair.Key, pair.Value);
            }
        }
    }
}
