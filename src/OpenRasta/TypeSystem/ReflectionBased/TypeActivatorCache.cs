using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq.Expressions;
using OpenRasta.DI;
using static System.Linq.Expressions.Expression;

namespace OpenRasta.TypeSystem.ReflectionBased
{
  public static class TypeActivatorCache
  {
    private static readonly ConcurrentDictionary<Type, Func<IDependencyResolver, object>> _funcCache =
      new ConcurrentDictionary<Type, Func<IDependencyResolver, object>>();

    private static readonly ConcurrentDictionary<Type, Func<IDependencyResolver, object>> _resolveCache =
      new ConcurrentDictionary<Type, Func<IDependencyResolver, object>>();

    public static Func<IDependencyResolver, object> GetActivator(Type type)
    {
      return _funcCache.GetOrAdd(type, CreateActivator);

      Func<IDependencyResolver, object> CreateActivator(Type t) =>
        GetActivatorExpression(t).Compile();
    }

    public static Func<IDependencyResolver, object> GetResolver(Type type)
    {
      return _resolveCache.GetOrAdd(type, CreateResolver);

      Func<IDependencyResolver, object> CreateResolver(Type t) =>
        GetResolverExpression(t).Compile();
    }

    public static Expression<Func<IDependencyResolver, object>> GetActivatorExpression(Type type)
    {
      if (type.IsInterface && type.IsGenericType)
      {
        if (type.GetGenericTypeDefinition() == typeof(IDictionary<,>))
        {
          type = typeof(Dictionary<,>).MakeGenericType(type.GetGenericArguments());
        }
        else if (typeof(IEnumerable).IsAssignableFrom(type))
        {
          type = typeof(List<>).MakeGenericType(type.GetGenericArguments());
        }
      }

      var resolverParamExpression = Parameter(typeof(IDependencyResolver), "resolver");

      Expression newInstanceExpression = New(type);

      if (typeof(IResolverAwareType).IsAssignableFrom(type))
      {
        var invokeResolveExpression = Invoke(GetResolverExpression(type), resolverParamExpression);
        var ifResolverNotNullExpression = ReferenceNotEqual(resolverParamExpression, Constant(null));

        newInstanceExpression = Condition(ifResolverNotNullExpression, invokeResolveExpression, Convert(newInstanceExpression, typeof(object)));
      }

      return Lambda<Func<IDependencyResolver, object>>(newInstanceExpression, resolverParamExpression);
    }

    public static Expression<Func<IDependencyResolver, object>> GetResolverExpression(Type type)
    {
      var resolverParamExpression = Parameter(typeof(IDependencyResolver), "resolver");
      var method = typeof(DependencyResolverExtensions).GetMethod(nameof(DependencyResolverExtensions.Resolve), new[] { typeof(IDependencyResolver), typeof(Type), typeof(UnregisteredAction) });
      var callExpression = Call(null, method, resolverParamExpression, Constant(type), Constant(UnregisteredAction.AddAsTransient));

      return Lambda<Func<IDependencyResolver, object>>(callExpression, resolverParamExpression);
    }
  }
}
