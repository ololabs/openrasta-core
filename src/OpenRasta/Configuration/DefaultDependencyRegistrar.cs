﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using OpenRasta.Binding;
using OpenRasta.Codecs;
using OpenRasta.CodeDom.Compiler;
using OpenRasta.Collections;
using OpenRasta.Configuration.MetaModel;
using OpenRasta.Configuration.MetaModel.Handlers;
using OpenRasta.DI;
using OpenRasta.Diagnostics;
using OpenRasta.Handlers;
using OpenRasta.OperationModel;
using OpenRasta.OperationModel.CodecSelectors;
using OpenRasta.OperationModel.Filters;
using OpenRasta.OperationModel.Hydrators;
using OpenRasta.OperationModel.Interceptors;
using OpenRasta.OperationModel.MethodBased;
using OpenRasta.Pipeline;
using OpenRasta.Pipeline.CallGraph;
using OpenRasta.Pipeline.Contributors;
using OpenRasta.TypeSystem;
using OpenRasta.TypeSystem.ReflectionBased;
using OpenRasta.TypeSystem.Surrogated;
using OpenRasta.TypeSystem.Surrogates;
using OpenRasta.TypeSystem.Surrogates.Static;
using OpenRasta.Web;
using RequestCodecSelector = OpenRasta.Pipeline.Contributors.RequestCodecSelector;

namespace OpenRasta.Configuration
{
  [SuppressMessage("ReSharper", "MemberCanBePrivate.Global")]
  public class DefaultDependencyRegistrar : IDependencyRegistrar
  {
    protected Type PathManagerType;

    [SuppressMessage("ReSharper", "VirtualMemberCallInConstructor")]
    public DefaultDependencyRegistrar()
    {
      CodecTypes = new List<Type>();
      PipelineContributorTypes = new List<Type>();
      CodeSnippetModifierTypes = new List<Type>();
      TraceSourceListenerTypes = new List<Type>();
      MetaModelHandlerTypes = new List<Type>();
      MethodFilterTypes = new List<Type>();
      OperationFilterTypes = new List<Type>();
      OperationHydratorTypes = new List<Type>();
      OperationCodecSelectorTypes = new List<Type>();
      SurrogateBuilders = new List<Type>();
      LogSourceTypes = new List<Type>();

      SetTypeSystem<ReflectionBasedTypeSystem>();
      SetMetaModelRepository<MetaModelRepository>();

      
      SetCodecRepository<CodecRepository>();
      SetHandlerRepository<HandlerRepository>();
      //SetLogger<TraceSourceLogger>();
      SetLogger<NullLogger>();
      SetErrorCollector<OperationContextErrorCollector>();
      SetObjectBinderLocator<DefaultObjectBinderLocator>();
      SetOperationCreator<MethodBasedOperationCreator>();
      SetOperationExecutor<OperationExecutor>();
      SetOperationInterceptorProvider<SystemAndAttributesOperationInterceptorProvider>();
      SetPathManager<PathManager>();

      AddMethodFilter<TypeExclusionMethodFilter<object>>();

      AddDefaultCodecs();
      AddDefaultContributors();
      AddCSharpCodeSnippetModifiers();
      AddDefaultMetaModelHandlers();
      AddOperationFilters();
      AddOperationHydrators();
      AddOperationCodecResolvers();
      AddLogSources();
      AddSurrogateBuilders();
    }

    void SetUriResolver(IDependencyResolver resolver)
    {
      resolver.AddDependency(typeof(IUriResolver),typeof(BaseAddressInjectingUriResolver), DependencyLifetime.Singleton);
    }

    protected IList<Type> CodeSnippetModifierTypes { get; }

    protected Type CodecRepositoryType { get; set; }
    protected IList<Type> CodecTypes { get; }
    protected Type ErrorCollectorType { get; set; }
    protected Type HandlerRepositoryType { get; set; }
    protected IList<Type> LogSourceTypes { get; set; }
    protected Type LogSourcedLoggerType { get; set; }
    protected Type LoggerType { get; set; }
    protected IList<Type> MetaModelHandlerTypes { get; }
    protected Type MetaModelRepositoryType { get; set; }
    protected IList<Type> MethodFilterTypes { get; set; }
    protected IList<Type> OperationCodecSelectorTypes { get; set; }
    protected Type OperationCreatorType { get; set; }
    protected Type OperationExecutorType { get; set; }
    protected IList<Type> OperationFilterTypes { get; set; }
    protected IList<Type> OperationHydratorTypes { get; set; }
    protected Type OperationInterceptorProviderType { get; set; }
    protected Type ParameterBinderLocatorType { get; set; }
    protected IList<Type> PipelineContributorTypes { get; }
    protected Type PipelineType { get; set; }
    protected IList<Type> SurrogateBuilders { get; }
    protected IList<Type> TraceSourceListenerTypes { get; }
    protected Type TypeSystemType { get; set; }
    public void AddCodeSnippetModifier<T>() where T : ICodeSnippetModifier
    {
      CodeSnippetModifierTypes.Add(typeof(T));
    }

    public void AddCodec<T>() where T : ICodec
    {
      CodecTypes.Add(typeof(T));
    }

    public void AddMetaModelHandler<T>() where T : IMetaModelHandler
    {
      MetaModelHandlerTypes.Add(typeof(T));
    }

    public void AddMethodFilter<T>() where T : IMethodFilter
    {
      MethodFilterTypes.Add(typeof(T));
    }

    public void AddOperationCodecSelector<T>()
    {
      OperationCodecSelectorTypes.Add(typeof(T));
    }

    public void AddPipelineContributor<T>() where T : IPipelineContributor
    {
      PipelineContributorTypes.Add(typeof(T));
    }

    public void AddSurrogateBuilders()
    {
      SurrogateBuilders.Add(typeof(ListIndexerSurrogateBuilder));
      SurrogateBuilders.Add(typeof(DateTimeSurrogate));
    }

    public void SetCodecRepository<T>() where T : ICodecRepository
    {
      CodecRepositoryType = typeof(T);
    }

    public void SetErrorCollector<T>()
    {
      ErrorCollectorType = typeof(T);
    }

    public void SetHandlerRepository<T>() where T : IHandlerRepository
    {
      HandlerRepositoryType = typeof(T);
    }

    public void SetLogger<T>() where T : ILogger
    {
      LoggerType = typeof(T);
    }

    public void SetMetaModelRepository<T>()
    {
      MetaModelRepositoryType = typeof(T);
    }

    public void SetObjectBinderLocator<T>() where T : IObjectBinderLocator
    {
      ParameterBinderLocatorType = typeof(T);
    }

    public void SetOperationExecutor<T>()
    {
      OperationExecutorType = typeof(T);
    }

    public void SetOperationInterceptorProvider<T>()
    {
      OperationInterceptorProviderType = typeof(T);
    }

    public void SetPathManager<T>()
    {
      PathManagerType = typeof(T);
    }

    public void SetPipeline<T>() where T : IPipeline
    {
      PipelineType = typeof(T);
    }

    public void SetTypeSystem<T>() where T : ITypeSystem
    {
      TypeSystemType = typeof(T);
    }

    public virtual void Register(IDependencyResolver resolver)
    {
      RegisterCoreComponents(resolver);
      RegisterSurrogateBuilders(resolver);
      RegisterLogging(resolver);
      RegisterMetaModelHandlers(resolver);
      RegisterContributors(resolver);
      RegisterCodeSnippeModifiers(resolver);
      RegisterMethodFilters(resolver);
      RegisterOperationModel(resolver);
      RegisterLogSources(resolver);
      RegisterCodecs(resolver);
      SetUriResolver(resolver);
      resolver.AddDependency(
        typeof(IRequestEntityReader),
        typeof(RequestEntityReaderHydrator),
        DependencyLifetime.Transient);
      resolver.AddDependency<IPipelineInitializer, ThreePhasePipelineInitializer>();
      resolver.AddDependency<IGenerateCallGraphs, TopologicalSortCallGraphGenerator>();
    }

    protected virtual void AddCSharpCodeSnippetModifiers()
    {
      AddCodeSnippetModifier<MarkupElementModifier>();
      AddCodeSnippetModifier<UnencodedOutputModifier>();
    }

    protected virtual void AddDefaultMetaModelHandlers()
    {
      AddMetaModelHandler<TypeRewriterMetaModelHandler>();
      AddMetaModelHandler<CodecMetaModelHandler>();
      AddMetaModelHandler<HandlerMetaModelHandler>();
      AddMetaModelHandler<UriRegistrationMetaModelHandler>();
      AddMetaModelHandler<DependencyRegistrationMetaModelHandler>();
      AddMetaModelHandler<DependencyFactoryHandler>();
      AddMetaModelHandler<OperationModelCreator>();
    }

    protected virtual void AddLogSources()
    {
      LogSourcedLoggerType = typeof(NullLogger<>); //typeof(TraceSourceLogger<>);
      LogSourceTypes.AddRange(
          typeof(ILogSource).Assembly.GetExportedTypes()
          .Where(x => !x.IsAbstract && !x.IsInterface && x.IsAssignableTo<ILogSource>()));
    }

    protected virtual void AddOperationCodecResolvers()
    {
      AddOperationCodecSelector<OperationModel.CodecSelectors.RequestCodecSelector>();
    }

    protected virtual void AddOperationFilter<T>() where T : IOperationFilter
    {
      OperationFilterTypes.Add(typeof(T));
    }

    protected virtual void AddOperationFilters()
    {
      AddOperationFilter<CompoundOperationFilter>();
    }

    protected virtual void AddOperationHydrator<T>()
    {
      OperationHydratorTypes.Add(typeof(T));
    }

    protected virtual void AddOperationHydrators()
    {
    }

    protected virtual void RegisterCodeSnippeModifiers(IDependencyResolver resolver)
    {
      CodeSnippetModifierTypes.ForEach(
        x => resolver.AddDependency(typeof(ICodeSnippetModifier), x, DependencyLifetime.Transient));
    }

    protected virtual void RegisterCodecs(IDependencyResolver resolver)
    {
      var repo = resolver.Resolve<ICodecRepository>();
      var typeSystem = resolver.Resolve<ITypeSystem>();
      foreach (Type codecType in CodecTypes)
      {
        if (!resolver.HasDependency(codecType))
          resolver.AddDependency(codecType, DependencyLifetime.Transient);
        IEnumerable<CodecRegistration> registrations = CodecRegistration.FromCodecType(codecType, typeSystem);
        registrations.ForEach(repo.Add);
      }
    }

    protected virtual void RegisterContributors(IDependencyResolver resolver)
    {
      PipelineContributorTypes.ForEach(
        x => resolver.AddDependency(typeof(IPipelineContributor), x, DependencyLifetime.Singleton));
    }

    protected virtual void RegisterCoreComponents(IDependencyResolver resolver)
    {
      resolver.AddDependency(typeof(ITypeSystem), TypeSystemType, DependencyLifetime.Singleton);
      resolver.AddDependency(typeof(IMetaModelRepository), MetaModelRepositoryType, DependencyLifetime.Singleton);
      
      
      
      resolver.AddDependency(typeof(ICodecRepository), CodecRepositoryType, DependencyLifetime.Singleton);
      resolver.AddDependency(typeof(IHandlerRepository), HandlerRepositoryType, DependencyLifetime.Singleton);
      resolver.AddDependency(typeof(IObjectBinderLocator), ParameterBinderLocatorType, DependencyLifetime.Singleton);
      resolver.AddDependency(typeof(IOperationCreator), OperationCreatorType, DependencyLifetime.Transient);
      resolver.AddDependency(typeof(IOperationExecutor), OperationExecutorType, DependencyLifetime.Transient);
      resolver.AddDependency(typeof(IErrorCollector), ErrorCollectorType, DependencyLifetime.Transient);
      resolver.AddDependency(typeof(IOperationInterceptorProvider), OperationInterceptorProviderType,
        DependencyLifetime.Transient);
      resolver.AddDependency(typeof(IPathManager), PathManagerType, DependencyLifetime.Singleton);
      resolver.AddDependency(typeof(ISurrogateProvider), typeof(SurrogateBuilderProvider),
        DependencyLifetime.Singleton);
    }

    [Conditional("DEBUG")]
    protected virtual void RegisterDefaultTraceListener(IDependencyResolver resolver)
    {
      if (!resolver.HasDependencyImplementation(typeof(TraceListener), typeof(DebuggerLoggingTraceListener)))
        resolver.AddDependency(typeof(TraceListener), typeof(DebuggerLoggingTraceListener),
          DependencyLifetime.Transient);
    }

    protected virtual void RegisterLogSources(IDependencyResolver resolver)
    {
      LogSourceTypes.ForEach(
        x =>
          resolver.AddDependency(typeof(ILogger<>).MakeGenericType(x), LogSourcedLoggerType.MakeGenericType(x),
            DependencyLifetime.Transient));
    }

    protected virtual void RegisterLogging(IDependencyResolver resolver)
    {
      resolver.AddDependency(typeof(ILogger), LoggerType, DependencyLifetime.Singleton);

      RegisterTraceSourceLiseners(resolver);
      RegisterDefaultTraceListener(resolver);
    }

    protected virtual void RegisterMetaModelHandlers(IDependencyResolver resolver)
    {
      MetaModelHandlerTypes.ForEach(
        x => resolver.AddDependency(typeof(IMetaModelHandler), x, DependencyLifetime.Transient));
    }

    protected virtual void RegisterMethodFilters(IDependencyResolver resolver)
    {
      MethodFilterTypes.ForEach(x => resolver.AddDependency(typeof(IMethodFilter), x, DependencyLifetime.Transient));
    }

    protected virtual void RegisterOperationModel(IDependencyResolver resolver)
    {
      OperationFilterTypes.ForEach(
        x => resolver.AddDependency(typeof(IOperationFilter), x, DependencyLifetime.Transient));
      
#pragma warning disable 618 - legacy support
      OperationHydratorTypes.ForEach(
        x => resolver.AddDependency(typeof(IOperationHydrator), x, DependencyLifetime.Transient));
#pragma warning restore 618
      
      OperationCodecSelectorTypes.ForEach(
        x => resolver.AddDependency(typeof(IOperationCodecSelector), x, DependencyLifetime.Transient));
    }

    protected virtual void RegisterSurrogateBuilders(IDependencyResolver resolver)
    {
      SurrogateBuilders.ForEach(
        x => resolver.AddDependency(typeof(ISurrogateBuilder), x, DependencyLifetime.Transient));
    }

    protected virtual void RegisterTraceSourceLiseners(IDependencyResolver resolver)
    {
      TraceSourceListenerTypes.ForEach(
        x => resolver.AddDependency(typeof(TraceListener), x, DependencyLifetime.Transient));
    }

    protected void SetOperationCreator<T>() where T : IOperationCreator
    {
      OperationCreatorType = typeof(T);
    }


    protected virtual void AddDefaultCodecs()
    {
      AddCodec<HtmlErrorCodec>();
      AddCodec<TextPlainCodec>();
      AddCodec<ApplicationXWwwFormUrlencodedKeyedValuesCodec>();
      AddCodec<ApplicationXWwwFormUrlencodedObjectCodec>();
      AddCodec<MultipartFormDataObjectCodec>();
      AddCodec<MultipartFormDataKeyedValuesCodec>();
      AddCodec<ApplicationOctetStreamCodec>();
      AddCodec<OperationResultCodec>();
    }

    protected virtual void AddDefaultContributors()
    {
      AddPipelineContributor<ResponseEntityCodecResolverContributor>();
      AddPipelineContributor<ResponseEntityWriterContributor>();
      AddPipelineContributor<PreExecutingContributor>();
      AddPipelineContributor<HttpMethodOverriderContributor>();
      AddPipelineContributor<UriDecoratorsContributor>();

      AddPipelineContributor<ResourceTypeResolverContributor>();
      AddPipelineContributor<HandlerResolverContributor>();
      AddPipelineContributor<NullAuthenticationContributor>();

      AddPipelineContributor<OperationCreatorContributor>();
      AddPipelineContributor<OperationFilterContributor>();
      AddPipelineContributor<RequestDecoderContributor>();
      AddPipelineContributor<RequestCodecSelector>();
      AddPipelineContributor<OperationInvokerContributor>();
      AddPipelineContributor<OperationResultInvokerContributor>();
      AddPipelineContributor<RequestResponseDisposer>();
    }
  }
}
