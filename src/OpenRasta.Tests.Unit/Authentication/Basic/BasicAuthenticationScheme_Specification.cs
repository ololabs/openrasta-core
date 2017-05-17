﻿#pragma warning disable 618

using System;
using System.Text;
using Moq;
using NUnit.Framework;
using OpenRasta.Authentication;
using OpenRasta.Authentication.Basic;
using OpenRasta.Hosting.InMemory;
using OpenRasta.Testing;

namespace BasicAuthenticationScheme_Specification
{
    [TestFixture]
    public class BasicAuthenticationScheme_Specification
    {
        Mock<IBasicAuthenticator> _mockAuthenticator;
        InMemoryRequest _request;
        BasicAuthenticationScheme _basicScheme;

        [SetUp]
        public void BeforeEachTest()
        {
            _mockAuthenticator = new Mock<IBasicAuthenticator>();
            _request = new InMemoryRequest();
            _basicScheme = new BasicAuthenticationScheme(_mockAuthenticator.Object);
        }

        [TearDown]
        public void AfterEachTest()
        {
            _mockAuthenticator.VerifyAll();
        }

        [Test]
        public void Given_AValidBasicAuthHeader_When_TheRequestIsAuthenticated_Then_TheResult_IsSuccess_And_UsernameIsSet_And_RolesAreSet()
        {
            // given
            string validAuthString = "Basic U2F1c2FnZTphbmQgbWFzaA==";
            string username = "Sausage";
            string password = "and mash";

            string[] userRoles = new[] { "Admin", "Manager", "Developer" };

            _request.Headers["Authorization"] = validAuthString;

            _mockAuthenticator

                .Setup(auth => auth.Authenticate(It.Is<BasicAuthRequestHeader>(h => h.Username == username && h.Password == password)))
                .Returns(new AuthenticationResult.Success(username, userRoles));

            // when
            var result = _basicScheme.Authenticate(_request);

            // then
            result.LegacyShouldBeOfType<AuthenticationResult.Success>();
            var success = result as AuthenticationResult.Success;

            success.Username.LegacyShouldBe(username);
            success.Roles.LegacyShouldHaveSameElementsAs(userRoles);
        }

        [Test]
        public void Given_AMalformedBasicAuthHeader_When_TheRequestIsAuthenticated_Then_TheResult_IsMalformed()
        {
            // given
            string malformedAuthString = "Basic notAValidBase64String!!!";
            _request.Headers["Authorization"] = malformedAuthString;

            // when
            var result = _basicScheme.Authenticate(_request);

            // then
            result.LegacyShouldBeOfType<AuthenticationResult.MalformedCredentials>();
        }

        [Test]
        public void Given_ABasicAuthenticatorReturnsFailed_When_TheRequestIsAuthenticated_Then_TheResult_IsFailed()
        {
            // given
            string authString = "Basic U2F1c2FnZTphbmQgbWFzaA==";
            string username = "Sausage";
            string password = "and mash";
            _request.Headers["Authorization"] = authString;

            _mockAuthenticator
                .Setup(auth => auth.Authenticate(It.Is<BasicAuthRequestHeader>(h => h.Username == username && h.Password == password)))
                .Returns(new AuthenticationResult.Failed());

            // when
            var result = _basicScheme.Authenticate(_request);

            // then
            result.LegacyShouldBeOfType<AuthenticationResult.Failed>();
        }

        [Test]
        public void can_have_colon_in_password()
        {
            string username = "Sausage";
            string password = "and:mash";
            string authString = "Basic " + Convert.ToBase64String(Encoding.ASCII.GetBytes(username + ":" + password));
            _request.Headers["Authorization"] = authString;

            _mockAuthenticator
                .Setup(auth => auth.Authenticate(It.Is<BasicAuthRequestHeader>(h => h.Username == username && h.Password == password)))
                .Returns(new AuthenticationResult.Success(username));

            // when
            var result = _basicScheme.Authenticate(_request);

            // then
            result.LegacyShouldBeOfType<AuthenticationResult.Success>();
        }

        [Test]
        public void Given_ABasicAuthenticatorWithARealm_When_ChallengingAResponse_Then_TheResponseHasAWWWAuthenticateHeader()
        {
            // given
            string realm = "Lex Luthors Palace";
            var response = new InMemoryResponse();

            _mockAuthenticator
                .SetupGet(auth => auth.Realm)
                .Returns(realm);

            // when
            _basicScheme.Challenge(response);

            // then
            var expectedChallengeHeader = string.Format("Basic realm=\"{0}\"", realm);
            response.Headers.LegacyShouldContain("WWW-Authenticate", expectedChallengeHeader);
        }
    }
}
#pragma warning restore 618
