﻿using System;
using System.Globalization;
using System.Linq;
using NUnit.Framework;
using OpenRasta;
using OpenRasta.Codecs;
using OpenRasta.Configuration.MetaModel;
using OpenRasta.Configuration.MetaModel.Handlers;
using OpenRasta.DI;
using OpenRasta.Handlers;
using OpenRasta.Testing;
using OpenRasta.Tests.Unit.Fakes;
using OpenRasta.TypeSystem;
using OpenRasta.Web;
using Shouldly;

namespace MetaModelHandler_Specification
{
    public abstract class metamodelhandler_context<T> : context where T:IMetaModelHandler
    {
        protected IDependencyResolver Resolver;
        protected T Handler;
        protected IMetaModelRepository MetaModel;

        protected override void SetUp()
        {
            base.SetUp();
            Resolver = new InternalDependencyResolver();
            MetaModel = new MetaModelRepository(Resolver);
            DependencyManager.SetResolver(Resolver);
        }

        protected override void TearDown()
        {
            base.TearDown();
            DependencyManager.UnsetResolver();
        }

        protected void when_executing_the_handler()
        {
            Handler.PreProcess(MetaModel);
            Handler.Process(MetaModel);

        }
    }

    public class when_registering_dependencies : metamodelhandler_context<DependencyRegistrationMetaModelHandler>
    {
        protected override void SetUp()
        {
            base.SetUp();
            Handler = new DependencyRegistrationMetaModelHandler(Resolver);
        }
        [Test]
        public void a_dependency_is_registered_in_the_container()
        {
            MetaModel.CustomRegistrations.Add(new DependencyRegistrationModel(typeof(IMetaModelHandler), typeof(DependencyRegistrationMetaModelHandler), DependencyLifetime.Singleton));

            when_executing_the_handler();

            Resolver.HasDependencyImplementation(typeof(IMetaModelHandler), typeof(DependencyRegistrationMetaModelHandler))
                .LegacyShouldBeTrue();
        }
        [Test]
        public void cannot_add_registrations_for_null_types()
        {
            Executing(() => new DependencyRegistrationModel(null, typeof(IMetaModelHandler), DependencyLifetime.Transient))
                .LegacyShouldThrow<ArgumentNullException>();
            Executing(() => new DependencyRegistrationModel(typeof(IMetaModelHandler), null, DependencyLifetime.Transient))
                .LegacyShouldThrow<ArgumentNullException>();
        }
    }
    public class when_registering_uris : metamodelhandler_context<UriRegistrationMetaModelHandler>
    {
        TemplatedUriResolver UriResolver;
        protected override void SetUp()
        {
            base.SetUp();
            UriResolver = new TemplatedUriResolver();
            Handler = new UriRegistrationMetaModelHandler(UriResolver);
        }
        [Test]
        public void all_uris_for_a_resource_get_registered()
        {
            given_uri_registration();

            Handler.Process(MetaModel);

            var uri1 = UriResolver.Match("http://localhost/customer".ToUri());
            uri1.ResourceKey.LegacyShouldBeOfType<IType>().Name.LegacyShouldBe("Customer");
            uri1.UriName.LegacyShouldBe("model");

            var uri2 = UriResolver.Match("http://localhost/preferedCustomer".ToUri());
            uri2.ResourceKey.LegacyShouldBeOfType<IType>().Name.LegacyShouldBe("Customer");
            uri2.UriCulture.LegacyShouldBe(CultureInfo.GetCultureInfo("fr-FR"));

        }

        void given_uri_registration()
        {
            MetaModel.ResourceRegistrations.Add(new ResourceModel
                {
                    ResourceKey = typeof(Customer),
                    Uris =
                        {
                            new UriModel
                                {
                                    Name = "model", 
                                    Uri = "/customer"
                                },
                            new UriModel
                                {
                                    Name = "model2",
                                    Uri = "/preferedCustomer",
                                    Language = CultureInfo.GetCultureInfo("fr-FR")
                                }
                        }
                });
        }
    }
    public class when_registering_handlers : metamodelhandler_context<HandlerMetaModelHandler>
    {
        IHandlerRepository HandlerRepository;
        protected override void SetUp()
        {
            base.SetUp();
            HandlerRepository = new HandlerRepository();
            Handler = new HandlerMetaModelHandler(HandlerRepository);
        }
        [Test]
        public void all_registered_handlers_for_a_resource_key_are_registered()
        {
            given_handler_registration();

            Handler.Process(MetaModel);
            var typeSystem = TypeSystems.Default.FromClr(typeof(Customer));
            var registeredHandlers = HandlerRepository.GetHandlerTypesFor(typeSystem);

            registeredHandlers.Any(x => x.Name == "CustomerHandler").LegacyShouldBeTrue();
            registeredHandlers.Any(x => x.Name == "Object").LegacyShouldBeTrue();
                
        }

        void given_handler_registration()
        {
            var typeSystem = TypeSystems.Default;
            MetaModel.ResourceRegistrations.Add(new ResourceModel
            {
                ResourceKey = typeSystem.FromClr(typeof(Customer)),
                Handlers =
                    {
                        new HandlerModel(typeSystem.FromClr(typeof(CustomerHandler))),
                        new HandlerModel(typeSystem.FromClr(typeof(object)))
                    }
            });
        }
    }
    public class when_registering_non_IType_resource_keys : metamodelhandler_context<TypeRewriterMetaModelHandler>
    {
        ITypeSystem TypeSystem;

        protected override void SetUp()
        {
            base.SetUp();
            TypeSystem = TypeSystems.Default;
            Handler = new TypeRewriterMetaModelHandler(TypeSystem);
        }
        [Test]
        public void a_clr_type_is_changed_to_an_IType()
        {
            given_resource_registration();

            when_executing_the_handler();

            MetaModel.ResourceRegistrations[0].ResourceKey
                .LegacyShouldBeOfType<IType>()
                .CompareTo(TypeSystem.FromClr(typeof(Customer)))
                    .LegacyShouldBe(0);
        }
        void given_resource_registration()
        {
            MetaModel.ResourceRegistrations.Add(new ResourceModel() { ResourceKey = typeof(Customer) });
        }
    }
    public class when_registering_codecs : metamodelhandler_context<CodecMetaModelHandler>
    {
        CodecRepository CodecRepository { get; set; }

        protected override void SetUp()
        {
            base.SetUp();
            CodecRepository = new CodecRepository();
            Handler = new CodecMetaModelHandler(CodecRepository);
        }
        [Test]
        public void a_codec_registered_without_a_media_type_uses_theattribute()
        {
            given_codec_registration();
            when_executing_the_handler();

            var htmlCodec = CodecRepository.Where(x => x.CodecType == typeof(HtmlErrorCodec));

            htmlCodec.Count().LegacyShouldBe(2);
            htmlCodec.First().MediaType.Matches(MediaType.Xhtml).LegacyShouldBeTrue();
            htmlCodec.Skip(1).First().MediaType.Matches(MediaType.Html).LegacyShouldBeTrue();
        }
        [Test]
        public void all_registered_codecs_for_a_resource_key_are_registered()
        {
            given_codec_registration();

            when_executing_the_handler();

            CodecRepository.Count().LegacyShouldBe(4);
            var first = CodecRepository.First();
          first.CodecType.ShouldBe(typeof(CustomerCodec));
          first.MediaType.LegacyShouldBe(MediaType.Json);
            first.Extensions.LegacyShouldContain("json");
            first.Extensions.Count.LegacyShouldBe(1);

            var second = CodecRepository.Skip(1).First();
          second.CodecType.ShouldBe(typeof(CustomerCodec));
          second.MediaType.LegacyShouldBe(MediaType.Xml);
            second.Extensions.LegacyShouldContain("xml");
            second.Extensions.Count.LegacyShouldBe(1);


        }
        [Test]
        public void cannot_add_a_codec_not_implementing_the_correct_interfaces()
        {
            Executing(() => given_unknown_type_registered_as_codec())
                .LegacyShouldThrow<ArgumentOutOfRangeException>();
        }

        void given_unknown_type_registered_as_codec()
        {
            var typeSystem = TypeSystems.Default;
            MetaModel.ResourceRegistrations.Add(new ResourceModel
            {
                ResourceKey = typeSystem.FromClr<Customer>(),
                Codecs =
                    {
                        new CodecModel(typeof(string))
                    }
            });
        }

        void given_codec_registration()
        {
            var typeSystem = TypeSystems.Default;
            MetaModel.ResourceRegistrations.Add(new ResourceModel
            {
                ResourceKey = typeSystem.FromClr(typeof(Customer)),
                Codecs =
                    {
                        new CodecModel(typeof(CustomerCodec))
                            {
                                MediaTypes =
                                    {
                                        new MediaTypeModel
                                            {
                                                MediaType = MediaType.Json,
                                                Extensions = {"json"}
                                            },
                                        new MediaTypeModel
                                            {
                                                MediaType = MediaType.Xml,
                                                Extensions = { "xml" }
                                            }
                                    }
                            },
                            new CodecModel(typeof(HtmlErrorCodec))
                    }
            });
        }
    }
}
