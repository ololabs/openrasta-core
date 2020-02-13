using System.Configuration;

namespace OpenRasta.Pipeline
{
  public class LoggingMiddlewareFactory : IPipelineMiddlewareFactory
  {
    public IPipelineMiddleware Compose(IPipelineMiddleware next)
    {
      if (!bool.TryParse(ConfigurationManager.AppSettings["openrasta:DisableLoggingMiddleware"], out var setting) || !setting)
      {
        return new LoggingMiddleware(next);
      }
      return next;
    }
  }
}