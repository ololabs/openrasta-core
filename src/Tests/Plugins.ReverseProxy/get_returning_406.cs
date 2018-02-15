﻿using System;
using System.Net;
using System.Net.Http;
using OpenRasta.Plugins.ReverseProxy;
using Shouldly;
using Xunit;

namespace Tests.Plugins.ReverseProxy
{
  public class get_returning_406
  {
    readonly HttpResponseMessage response;

    public get_returning_406()
    {
      response = new ProxyServer()
          .FromServer("/proxy")
          .ToServer("/proxied")
          .AddHeader("Accept", "application/vnd.example")
          .GetAsync("http://localhost/proxy")
          .Result;
    }

    [Fact]
    public void response_status_code_is_correct()
    {
      response.StatusCode.ShouldBe(HttpStatusCode.NotAcceptable);
    }
  }
}