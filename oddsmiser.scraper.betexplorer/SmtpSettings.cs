using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace oddsmiser.scraper.betexplorer
{
    [JsonObject("SmtpSettings")]
    public class SmtpSettings
    {
        [JsonProperty("Host")]
        public string Host { get; set; }

        [JsonProperty("Port")]
        public int Port { get; set; }

        [JsonProperty("UseSSL")]
        public bool UseSSL { get; set; }

        [JsonProperty("Name")]
        public string Name { get; set; }

        [JsonProperty("Username")]
        public string Username { get; set; }

        [JsonProperty("EmailAddress")]
        public string EmailAddress { get; set; }

        [JsonProperty("Password")]
        public string Password { get; set; }
    }
}
