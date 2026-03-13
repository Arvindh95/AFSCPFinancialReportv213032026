using PX.Data;
using System;
using System.Collections.Generic;

namespace FinancialReport.Services
{
    public class AcumaticaCredentials
    {
        public string ClientId { get; set; }
        public string ClientSecret { get; set; }
        public string Username { get; set; }
        public string Password { get; set; }
        public string BaseURL { get; set; }
    }

    public static class CredentialProvider
    {
        private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(10);

        // Cache entry wraps credentials with the time they were loaded
        private static readonly Dictionary<string, (AcumaticaCredentials Creds, DateTime CachedAt)> _credentialCache
            = new Dictionary<string, (AcumaticaCredentials, DateTime)>(StringComparer.OrdinalIgnoreCase);

        private static readonly object _cacheLock = new object();

        public static AcumaticaCredentials GetCredentials(string tenant)
        {
            if (string.IsNullOrEmpty(tenant))
                throw new PXException(Messages.TenantNameRequired);

            // Return from cache if still within TTL
            lock (_cacheLock)
            {
                if (_credentialCache.TryGetValue(tenant, out var entry) &&
                    DateTime.UtcNow - entry.CachedAt < CacheTtl)
                {
                    PXTrace.WriteInformation($"[Cache] Credentials retrieved from cache for tenant: {tenant}");
                    return entry.Creds;
                }
            }

            PXTrace.WriteInformation($"[Decrypt] Loading credentials for tenant: {tenant}");
            PXGraph graph = new PXGraph();
            try
            {
                FLRTTenantCredentials record = PXSelect<FLRTTenantCredentials,
                    Where<FLRTTenantCredentials.tenantName, Equal<Required<FLRTTenantCredentials.tenantName>>>>
                    .Select(graph, tenant);

                if (record == null)
                {
                    PXTrace.WriteError($"No credentials found for tenant: {tenant}");
                    throw new PXException(Messages.TenantMissingFromDatabase);
                }

                var credentials = new AcumaticaCredentials
                {
                    ClientId     = record.ClientIDNew,
                    ClientSecret = record.ClientSecretNew,
                    Username     = record.UsernameNew,
                    Password     = record.PasswordNew,
                    BaseURL      = record.BaseURL
                };

                // Log only non-sensitive confirmation — no credential values
                PXTrace.WriteInformation($"[Decrypt] Credentials loaded for tenant {tenant} (CompanyNum {record.CompanyNum}).");

                lock (_cacheLock)
                {
                    _credentialCache[tenant] = (credentials, DateTime.UtcNow);
                }

                return credentials;
            }
            finally
            {
                graph.Clear();
            }
        }

        /// <summary>
        /// Clears all cached credentials. Call this when any tenant's credentials change.
        /// </summary>
        public static void ClearCache()
        {
            lock (_cacheLock)
            {
                int count = _credentialCache.Count;
                _credentialCache.Clear();
                if (count > 0)
                    PXTrace.WriteInformation($"[Cache] Cleared credential cache ({count} tenant(s)).");
            }
        }

        /// <summary>
        /// Clears cached credentials for a specific tenant.
        /// Call this from the RowPersisted event of FLRTTenantCredentials.
        /// </summary>
        public static void ClearCache(string tenant)
        {
            if (string.IsNullOrEmpty(tenant)) return;
            lock (_cacheLock)
            {
                if (_credentialCache.Remove(tenant))
                    PXTrace.WriteInformation($"[Cache] Cleared cached credentials for tenant: {tenant}.");
            }
        }
    }
}
