using System;

namespace OpenRasta.Web.Internal
{
    public static class CommunicationContextExtensions
    {
        public static Uri GetRequestUriRelativeToRoot(this ICommunicationContext context)
        {
            var requestUri = context.Request.Uri.IgnoreSchemePortAndAuthority(); 

            var result = context.ApplicationBaseUri.IgnoreSchemePortAndAuthority()
                .EnsureHasTrailingSlash()
                .MakeRelativeUri(requestUri)
                .MakeAbsolute("http://localhost");

            return result;
        }
    }
}
