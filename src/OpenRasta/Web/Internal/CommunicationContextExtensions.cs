using System;
using OpenRasta.Diagnostics;
using OpenRasta.Pipeline.Diagnostics;

namespace OpenRasta.Web.Internal
{
    public static class CommunicationContextExtensions
    {
        public static Uri GetRequestUriRelativeToRoot(this ICommunicationContext context)
        {
            var requestUri = context.Request.Uri;

            // Normalize non-standard port numbers based on the URI scheme.
            var port = requestUri.Scheme == "https" ? 443 : 80;
            var builder = new UriBuilder(requestUri.Scheme, requestUri.Host, port, requestUri.AbsolutePath);
            builder.Query = requestUri.Query.TrimStart('?');

            var uri = new Uri(builder.Uri.ToString());

            return context.ApplicationBaseUri
                .EnsureHasTrailingSlash()
                .MakeRelativeUri(uri) // Strip standard port numbers from the URL.
                .MakeAbsolute("http://localhost");
        }
        
    }
}
