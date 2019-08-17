using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using OpenRasta.Configuration.MetaModel;
using OpenRasta.Plugins.Hydra.Internal.Serialization.ExpressionTree;
using OpenRasta.TypeSystem.ReflectionBased;
using Utf8Json;

namespace OpenRasta.Plugins.Hydra.Internal.Serialization.Utf8JsonPrecompiled
{
  public static class TypeMethods
  {
    static readonly MethodInfo ResolverGetFormatterMethodInfo =
      typeof(CustomResolver).GetMethod(nameof(IJsonFormatterResolver.GetFormatter));

    static readonly MethodInfo EnumerableToArrayMethodInfo = typeof(Enumerable).GetMethod(nameof(Enumerable.ToArray));

    public static void ResourceDocument(
      Variable<JsonWriter> jsonWriter,
      ResourceModel model,
      Expression resource,
      Variable<SerializationContext> options,
      Action<ParameterExpression> defineVar,
      Action<Expression> addStatement,
      IMetaModelRepository models)
    {
      var uriResolverFunc = options.get_UriGenerator();

      var converter = options.get_BaseUri().ObjectToString();
      var contextUri = StringMethods.Concat(converter, New.Const(".hydra/context.jsonld"));

      var resolver = New.Var<CustomResolver>("resolver");

      defineVar(resolver);
      addStatement(Expression.Assign(resolver, Expression.New(typeof(CustomResolver))));

      foreach (var exp in WriteBeginObjectContext(jsonWriter, contextUri)) addStatement(exp);

      WriteNode(jsonWriter, model, resource, defineVar, addStatement, uriResolverFunc, models,
        new Stack<ResourceModel>(),
        resolver,
        true);

      addStatement(jsonWriter.WriteEndObject());
    }

    static InvocationExpression InvokeGetCurrentUriExpression(Expression resource, MemberExpression uriResolverFunc)
    {
      return Expression.Invoke(uriResolverFunc, resource);
    }

    static void WriteNode(
      Variable<JsonWriter> jsonWriter,
      ResourceModel model,
      Expression resource,
      Action<ParameterExpression> defineVar,
      Action<Expression> addStatement,
      MemberExpression uriResolver,
      IMetaModelRepository models,
      Stack<ResourceModel> recursionDefender,
      ParameterExpression jsonResolver,
      bool hasKeysInObject = false)
    {
      var resourceType = model.ResourceType;

      if (recursionDefender.Contains(model))
        throw new InvalidOperationException(
          $"Detected recursion, already processing {resourceType?.Name}: {string.Join("->", recursionDefender.Select(m => m.ResourceType?.Name).Where(n => n != null))}");


      recursionDefender.Push(model);

      void EnsureKeySeparator(Action<Expression> adder)
      {
        if (hasKeysInObject)
          adder(jsonWriter.WriteValueSeparator());
        hasKeysInObject = true;
      }

      var resourceRegistrationHydraType = GetHydraTypeName(model);
      var invokeGetCurrentUri = InvokeGetCurrentUriExpression(resource, uriResolver);

      var collectionItemTypes = CollectionItemTypes(resourceType).ToList();

      Type collectionItemType;
      if (collectionItemTypes.Count == 1 &&
          models.TryGetResourceModel((collectionItemType = collectionItemTypes.First()), out _))
      {
        var collectionType = HydraTypes.Collection.MakeGenericType(collectionItemType);
        var collectionCtor =
          collectionType.GetConstructor(new[] {typeof(IEnumerable<>).MakeGenericType(collectionItemType)});
        var collection = Expression.Variable(collectionType);

        defineVar(collection);
        addStatement(Expression.Assign(collection,
          Expression.New(
            collectionCtor,
            resource)));

        resource = collection;

        // if we have a generic list of sort, we hydra:Collection instead
        if (resourceType.IsGenericType) // IEnum<T>, List<T> etc
          resourceRegistrationHydraType = "hydra:Collection";

        resourceType = collectionType;
      }

      var publicProperties = resourceType
        .GetProperties(BindingFlags.Public | BindingFlags.Instance)
        .Where(IsNotIgnored)
        .ToList();

      var propNames = publicProperties.Select(GetJsonPropertyName);
      var overridesId = propNames.Any(name => name == "@id");
      var overridesType = propNames.Any(name => name == "@type");

      if (overridesId == false && model.Uris.Any())
      {
        EnsureKeySeparator(addStatement);

        foreach (var x in WriteId(jsonWriter, invokeGetCurrentUri)) addStatement(x);
      }

      if (overridesType == false)
      {
        EnsureKeySeparator(addStatement);

        foreach (var x in WriteType(jsonWriter, resourceRegistrationHydraType))
          addStatement(x);
      }


      foreach (var pi in publicProperties)
      {
        if (pi.GetIndexParameters().Any()) continue;

        if (pi.PropertyType.IsValueType && Nullable.GetUnderlyingType(pi.PropertyType) == null)
        {
          EnsureKeySeparator(addStatement);

          var propertyGet = Expression.MakeMemberAccess(resource, pi);
          WriteNodePropertyValue(
            jsonWriter,
            defineVar,
            addStatement,
            pi,
            jsonResolver,
            propertyGet);
          continue;
        }

        WriteNodeProperty(jsonWriter, resource, defineVar, addStatement, uriResolver, models, recursionDefender, pi,
          jsonResolver, EnsureKeySeparator);
      }

      foreach (var link in model.Links)
      {
        EnsureKeySeparator(addStatement);

        WriteNodeLink(jsonWriter, defineVar, addStatement, link.Relationship, link.Uri, invokeGetCurrentUri, link);
      }

      recursionDefender.Pop();
    }

    static bool IsNotIgnored(PropertyInfo pi)
    {
      return pi.CustomAttributes.Any(a => a.AttributeType.Name == "JsonIgnoreAttribute") == false;
    }


    static string UriStandardCombine(string current, Uri rel) => new Uri(new Uri(current), rel).ToString();

    static string UriSubResourceCombine(string current, Uri rel)
    {
      current = current[current.Length - 1] == '/' ? current : current + "/";
      return new Uri(new Uri(current), rel).ToString();
    }

    static void WriteNodeLink(Variable<JsonWriter> jsonWriter, Action<ParameterExpression> defineVar,
      Action<Expression> addStatement, string linkRelationship, Uri linkUri, InvocationExpression uriResolverFunc,
      ResourceLinkModel link)
    {
      addStatement(jsonWriter.WritePropertyName(linkRelationship));
      addStatement(jsonWriter.WriteBeginObject());
      addStatement(jsonWriter.WriteRaw(Nodes.IdProperty));

      var methodInfo = link.CombinationType == ResourceLinkCombination.SubResource
        ? typeof(TypeMethods).GetMethod(nameof(UriSubResourceCombine), BindingFlags.Static | BindingFlags.NonPublic)
        : typeof(TypeMethods).GetMethod(nameof(UriStandardCombine), BindingFlags.Static | BindingFlags.NonPublic);

      var uriCombine = Expression.Call(methodInfo, uriResolverFunc,
        Expression.Constant(linkUri, typeof(Uri)));
      addStatement(jsonWriter.WriteString(uriCombine));


      if (link.Type != null)
      {
        addStatement(jsonWriter.WriteValueSeparator());
        addStatement(jsonWriter.WritePropertyName("@type"));
        addStatement(jsonWriter.WriteString(link.Type));
      }

      addStatement(jsonWriter.WriteEndObject());
    }

    static void WriteNodeProperty(
      Variable<JsonWriter> jsonWriter,
      Expression resource,
      Action<ParameterExpression> declareVar,
      Action<Expression> addStatement,
      MemberExpression uriResolverFunc,
      IMetaModelRepository models,
      Stack<ResourceModel> recursionDefender,
      PropertyInfo pi,
      ParameterExpression resolver,
      Action<Action<Expression>> separator
    )
    {
      var propertyStatements = new List<Expression>();
      var propertyVars = new List<ParameterExpression>();

      separator(propertyStatements.Add);

      // var propertyValue;
      var propertyValue = Expression.Variable(pi.PropertyType, $"val{pi.DeclaringType.Name}{pi.Name}");
      declareVar(propertyValue);

      // propertyValue = resource.Property;
      addStatement(Expression.Assign(propertyValue, Expression.MakeMemberAccess(resource, pi)));


      if (models.TryGetResourceModel(pi.PropertyType, out var propertyResourceModel))
      {
        propertyStatements.Add(jsonWriter.WritePropertyName(GetJsonPropertyName(pi)));
        propertyStatements.Add(jsonWriter.WriteBeginObject());
        WriteNode(jsonWriter, propertyResourceModel, propertyValue, propertyVars.Add, propertyStatements.Add,
          uriResolverFunc, models, recursionDefender, resolver);
        propertyStatements.Add(jsonWriter.WriteEndObject());
      }
      else
      {
        // not an iri node itself, but is it a list of nodes?
        var itemResourceRegistrations = (
          from i in pi.PropertyType.GetInterfaces()
          where i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEnumerable<>)
          let itemType = i.GetGenericArguments()[0]
          where itemType != typeof(object)
          let resourceModels = models.ResourceRegistrations.Where(r => itemType.IsAssignableFrom(r.ResourceType))
          where resourceModels.Any()
          orderby resourceModels.Count() descending
          select new
          {
            itemType, models =
              (from possible in resourceModels
                orderby possible.ResourceType.GetInheritanceDistance(itemType)
                select possible).ToList()
          }).FirstOrDefault();

        if (itemResourceRegistrations == null)
        {
          // not a list of iri or blank nodes
          WriteNodePropertyValue(jsonWriter, propertyVars.Add, propertyStatements.Add, pi, resolver,
            Expression.MakeMemberAccess(resource, pi));
        }
        else
        {
          // it's a list of nodes
          var itemArrayType = itemResourceRegistrations.itemType.MakeArrayType();
          var itemArray = Expression.Variable(itemArrayType);

          var toArrayMethod = EnumerableToArrayMethodInfo
            .MakeGenericMethod(itemResourceRegistrations.itemType);
          var assign = Expression.Assign(itemArray, Expression.Call(toArrayMethod, propertyValue));
          propertyVars.Add(itemArray);
          propertyStatements.Add(assign);

          var i = New.Var<int>();
          
          propertyVars.Add(i);
          propertyStatements.Add(i.Assign(0));

          var itemVars = new List<ParameterExpression>();
          var itemStatements = new List<Expression>();

          var @break = Expression.Label("break");

          propertyStatements.Add(jsonWriter.WritePropertyName(GetJsonPropertyName(pi)));

          propertyStatements.Add(jsonWriter.WriteBeginArray());

          itemStatements.Add(Expression.IfThen(
            i.GreaterThan(0),
            jsonWriter.WriteValueSeparator()));
          
          itemStatements.Add(jsonWriter.WriteBeginObject());

          BlockExpression resourceBlock(ResourceModel r, ParameterExpression typed)
          {
            var vars = new List<ParameterExpression>();
            var statements = new List<Expression>();
            WriteNode(
              jsonWriter,
              r,
              typed,
              vars.Add, statements.Add,
              uriResolverFunc, models, recursionDefender, resolver);
            return Expression.Block(vars.ToArray(), statements.ToArray());
          }

          Expression renderBlock =
            Expression.Block(Expression.Throw(Expression.New(typeof(InvalidOperationException))));

          // with C : B : A, if is C else if is B else if is A else throw

          foreach (var specificModel in itemResourceRegistrations.models)
          {
            var typed = Expression.Variable(specificModel.ResourceType, "as" + specificModel.ResourceType.Name);
            itemVars.Add(typed);
            var @as = Expression.Assign(typed,
              Expression.TypeAs(Expression.ArrayAccess(itemArray, i), specificModel.ResourceType));
            renderBlock = Expression.IfThenElse(
              Expression.NotEqual(@as, Expression.Default(specificModel.ResourceType)),
              resourceBlock(specificModel, @typed),
              renderBlock);
          }

          itemStatements.Add(renderBlock);
          itemStatements.Add(Expression.PostIncrementAssign(i));
          itemStatements.Add(jsonWriter.WriteEndObject());
          var loop = Expression.Loop(
            Expression.IfThenElse(
              i.LessThan(Expression.MakeMemberAccess(itemArray, itemArrayType.GetProperty("Length"))),
              Expression.Block(itemVars.ToArray(), itemStatements.ToArray()),
              Expression.Break(@break)),
            @break
          );
          propertyStatements.Add(loop);
          propertyStatements.Add(jsonWriter.WriteEndArray());
        }
      }

      addStatement(Expression.IfThen(
        Expression.NotEqual(propertyValue, Expression.Default(pi.PropertyType)),
        Expression.Block(propertyVars.ToArray(), propertyStatements.ToArray())));
    }

    static IEnumerable<Type> CollectionItemTypes(Type type)
    {
      return from i in type.GetInterfaces()
        where i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEnumerable<>)
        select i.GetGenericArguments()[0];
    }

    static string GetHydraTypeName(ResourceModel model)
    {
      var hydraResourceModel = model.Hydra();
      return (hydraResourceModel.Vocabulary?.DefaultPrefix == null
               ? string.Empty
               : $"{hydraResourceModel.Vocabulary.DefaultPrefix}:") +
             model.ResourceType.Name;
    }

    static IEnumerable<Expression> WriteId(
      Variable<JsonWriter> jsonWriter,
      Expression uri)
    {
      yield return jsonWriter.WriteRaw(Nodes.IdProperty);
      yield return jsonWriter.WriteString(uri);
    }

    public static IEnumerable<Expression> WriteType(Variable<JsonWriter> jsonWriter, string type)
    {
      yield return jsonWriter.WriteRaw(Nodes.TypeProperty);
      yield return jsonWriter.WriteString(type);
    }

    static IEnumerable<Expression> WriteBeginObjectContext(Variable<JsonWriter> jsonWriter, Expression contextUri)
    {
      yield return jsonWriter.WriteRaw(Nodes.BeginObjectContext);
      yield return jsonWriter.WriteString(contextUri);
    }

    static void WriteNodePropertyValue(
      Variable<JsonWriter> jsonWriter,
      Action<ParameterExpression> declareVar,
      Action<Expression> addStatement,
      PropertyInfo pi,
      ParameterExpression jsonFormatterResolver, MemberExpression propertyGet)
    {
      var propertyName = GetJsonPropertyName(pi);

      addStatement(jsonWriter.WritePropertyName(propertyName));


      var propertyType = pi.PropertyType;
      var (formatterInstance, serializeMethod) =
        GetFormatter(declareVar, addStatement, jsonFormatterResolver, propertyType);

      var serializeFormatter =
        Expression.Call(formatterInstance, serializeMethod, jsonWriter, propertyGet, jsonFormatterResolver);
      addStatement(serializeFormatter);
    }


    static string GetJsonPropertyName(PropertyInfo pi)
    {
      return pi.CustomAttributes
               .Where(a => a.AttributeType.Name == "JsonPropertyAttribute")
               .SelectMany(a => a.ConstructorArguments)
               .Where(a => a.ArgumentType == typeof(string))
               .Select(a => (string) a.Value)
               .FirstOrDefault() ?? ToCamelCase(pi.Name);
    }

    static (ParameterExpression formatterInstance, MethodInfo serializeMethod) GetFormatter(
      Action<ParameterExpression> variable, Action<Expression> statement, ParameterExpression jsonFormatterResolver,
      Type propertyType)
    {
      var resolverGetFormatter = ResolverGetFormatterMethodInfo.MakeGenericMethod(propertyType);
      var jsonFormatterType = typeof(IJsonFormatter<>).MakeGenericType(propertyType);
      var serializeMethod = jsonFormatterType.GetMethod("Serialize",
        new[] {typeof(JsonWriter).MakeByRefType(), propertyType, typeof(IJsonFormatterResolver)});

      var formatterInstance = Expression.Variable(jsonFormatterType);
      statement(Expression.Assign(formatterInstance, Expression.Call(jsonFormatterResolver, resolverGetFormatter)));

      statement(Expression.IfThen(Expression.Equal(formatterInstance, Expression.Default(jsonFormatterType)),
        Expression.Throw(Expression.New(typeof(ArgumentNullException)))));
      variable(formatterInstance);
      return (formatterInstance, serializeMethod);
    }

    static string ToCamelCase(string piName)
    {
      return char.ToLowerInvariant(piName[0]) + piName.Substring(1);
    }

    public static IEnumerable<Type> GetInheritanceHierarchy(this Type type)
    {
      for (var current = type; current != null; current = current.BaseType)
      {
        yield return current;
      }
    }

    public static string GetRange(this Type type)
    {
      switch (type.Name)
      {
        case nameof(Int32):
          return "xsd:int";

        case nameof(String):
          return "xsd:string";

        case nameof(Boolean):
          return "xsd:boolean";

        case nameof(DateTime):
          return "xsd:datetime";

        case nameof(Decimal):
          return "xsd:decimal";

        case nameof(Double):
          return "xsd:double";

        case nameof(Uri):
          return "xsd:anyURI";

        default:
          return "xsd:string";
      }
    }
  }
}