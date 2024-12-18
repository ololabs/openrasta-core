using System;
using System.Net.Http;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using OpenRasta.Hosting.Katana;
using Shouldly;
using Tests.Infrastructure;
using Xunit;

namespace Tests.Hosting.Owin
{
  public class hosting_on_pure_owin_on_asp_net_core : IDisposable
  {
    readonly HttpClient client;
    readonly TestServer server;

    public hosting_on_pure_owin_on_asp_net_core()
    {
      server = new TestServer(
        new WebHostBuilder()
          .Configure(app => app
            .Use(next => async httpContext =>
            {
              try
              {
                await next(httpContext);
              }
              catch(Exception ex)
              {
                // Default behavior is to write the stack trace
                httpContext.Response.StatusCode = 500;

                // Incredibly annoying TargetInvocationException
                var message = ex.InnerException?.Message ?? ex.Message;
                await httpContext.Response.WriteAsync(message);
              }
            })
            .UseOwin(builder =>
              builder.UseOpenRasta(
                new TaskApi(),
                startupProperties: new OpenRasta.Concordia.StartupProperties
                {
                  OpenRasta =
                  {
                    Errors =
                    {
                      HandleAllExceptions = false,
                      HandleCatastrophicExceptions = false,
                    },
                  },
                },
                onAppDisposing: app.ApplicationServices.GetService<IApplicationLifetime>().ApplicationStopping))));
      client = server.CreateClient();
    }
    
    [Fact]
    public async void can_get_list_of_tasks()
    {
      var response = await client.GetAsync("tasks");
      response.EnsureSuccessStatusCode();
    }

    [Fact]
    public async void can_get_silent_ping()
    {
      var response = await client.GetAsync("ping-silently");
      response.EnsureSuccessStatusCode();
      response.Content.Headers.TryGetValues("Content-Length", out _).ShouldBeFalse();
    }

    [Fact]
    public async void can_get_no_content_ping()
    {
      var response = await client.GetAsync("ping-empty-content");
      response.EnsureSuccessStatusCode();
      response.Content.Headers.ContentLength.ShouldBe(0);
    }

    [Fact]
    public async void can_get_handled_exception()
    {
      var response = await client.GetAsync("ping-exception");
      response.StatusCode.ShouldBe(System.Net.HttpStatusCode.InternalServerError);
      (await response.Content.ReadAsStringAsync()).ShouldBe("This is a test exception");
    }

    public void Dispose()
    {
      server?.Dispose();
    }
  }
}