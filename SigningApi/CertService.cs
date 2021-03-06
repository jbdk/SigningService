﻿using System;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using Outercurve.SigningApi.Properties;
using ServiceStack.Logging;

namespace Outercurve.SigningApi
{
    public class CertService
    {
        public X509Certificate2 Get(string name)
        {
            if (name == "--SAMPLE--")
            {
                return GetSample();
            }
            var s = name.Split('\\').Skip(1).ToArray();
            var store = new X509Store((StoreName) Enum.Parse(typeof (StoreName), s[1]), (StoreLocation) Enum.Parse(typeof (StoreLocation), s[0]));
            store.Open(OpenFlags.ReadOnly);
            var log = LogManager.GetLogger(GetType());

            log.DebugFormat("store name is {0}", name);
            log.Debug("Start Certs");
            foreach (var c in store.Certificates.Cast<X509Certificate2>())
            {
                
                log.DebugFormat("cert = {0}, {1} private key", c.Thumbprint, c.HasPrivateKey ? "has" : "does not have");
            }
            log.Debug("End Certs");
            return store.Certificates.Cast<X509Certificate2>().FirstOrDefault(c => c.Thumbprint == s[2]);

        }

        X509Certificate2 GetSample()
        {
            return new X509Certificate2(Resources.Sign, "", X509KeyStorageFlags.Exportable);
        }
    }
}