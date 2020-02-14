﻿using System;
using System.Threading.Tasks;
using OpenRasta.Diagnostics;
using OpenRasta.Web;

namespace OpenRasta.Pipeline
{
  public class LoggingMiddleware : IPipelineMiddleware
  {
    static readonly ILogger Log = NullLogger.Instance;//TraceSourceLogger.Instance;
    readonly IPipelineMiddleware _next;
    readonly string _log;

    public LoggingMiddleware(IPipelineMiddleware next)
    {
      _next = next;
      AbstractContributorMiddleware contrib;
      var isContrib = (contrib = next as AbstractContributorMiddleware) != null;
      _log = isContrib ? LogContributor(next, contrib) : LogMiddleware(next);
    }

    static string LogMiddleware(IPipelineMiddleware next)
    {
      return $"Executing middleware {next.GetType().Name}";
    }

    static string LogContributor(IPipelineMiddleware next, AbstractContributorMiddleware contrib)
    {
      return $"Executing contributor {contrib.ContributorCall.ContributorTypeName}." +
             $"{contrib.ContributorCall.Action.Method.Name} ({next.GetType().Name} middleware)";
    }

    public async Task Invoke(ICommunicationContext env)
    {
      using (Log.Operation(this, _log))
      {
        try
        {
          await _next.Invoke(env);
        }
        catch (Exception e)
        {
          Log.WriteError($"Exception has happened: {Environment.NewLine}{e}");
          throw;
        }
        finally
        {
          foreach (var error in env.ServerErrors)
          {
            Log.WriteError(error.ToString());
          }
        }
      }
    }
  }
}