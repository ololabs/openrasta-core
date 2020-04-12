using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace OpenRasta.TypeSystem.ReflectionBased
{
    public class ReflectionBasedMethod : IMethod
    {
        readonly MethodInfo _methodInfo;
        readonly Lazy<Expression<Func<object, object[], object>>> _lazyExpression;
        readonly Lazy<Func<object, object[], object>> _lazyFunc;

        internal ReflectionBasedMethod(IMember ownerType, MethodInfo methodInfo)
        {
            _methodInfo = methodInfo;
            _lazyExpression = new Lazy<Expression<Func<object, object[], object>>>(CreateExpression);
            _lazyFunc = new Lazy<Func<object, object[], object>>(() => _lazyExpression.Value.Compile());
            Owner = ownerType;
            TypeSystem = TypeSystems.Default;
            EnsureInputMembersExist();
            EnsureOutputMembersExist();
        }

        public IEnumerable<IParameter> InputMembers { get; private set; }

        public string Name
        {
            get { return _methodInfo.Name; }
        }

        public IEnumerable<IMember> OutputMembers { get; private set; }

        public IMember Owner { get; set; }
        public ITypeSystem TypeSystem { get; set; }

        public Expression<Func<object, object[], object>> InvocationExpression => _lazyExpression.Value;

        public override string ToString()
        {
            return
              $"{Owner.TypeName}::{_methodInfo.Name}({string.Join(", ", _methodInfo.GetParameters().Select(x => $"{x.ParameterType.Name} {x.Name}").ToArray())})";
        }

        public T FindAttribute<T>() where T : class
        {
            return FindAttributes<T>().FirstOrDefault();
        }

        public IEnumerable<T> FindAttributes<T>() where T : class
        {
            return _methodInfo.GetCustomAttributes(typeof(T), true).Cast<T>();
        }

        public IEnumerable<object> Invoke(object target, params object[] members)
        {
            return new[] { _lazyFunc.Value(target, members) };
        }

        void EnsureInputMembersExist()
        {
            if (InputMembers == null)
            {
                InputMembers = _methodInfo.GetParameters()
                    .Where(x => !x.IsOut)
                    .Select(x => (IParameter)new ReflectionBasedParameter(this, x)).ToList().AsReadOnly();
            }
        }

        void EnsureOutputMembersExist()
        {
            if (OutputMembers == null)
            {
                var outputParameters = new List<IMember>();
                outputParameters.Add(TypeSystem.FromClr(_methodInfo.ReturnType));
                foreach (var outOrRefParameter in InputMembers.Where(x => x.IsOutput))
                    outputParameters.Add(outOrRefParameter);
                OutputMembers = outputParameters.AsReadOnly();
            }
        }

        private Expression<Func<object, object[], object>> CreateExpression()
        {
            var instanceParameterExpression = Expression.Parameter(typeof(object), "obj");
            var argsArrayExpression = Expression.Parameter(typeof(object[]), "args");

            var parameterExpressions = GetParameterExpressions();

            var instanceExpression = _methodInfo.IsStatic == false ? Expression.Convert(instanceParameterExpression, _methodInfo.ReflectedType) : null;
            var callExpression = Expression.Call(instanceExpression, _methodInfo, parameterExpressions);

            if (_methodInfo.ReturnType == typeof(void))
            {
                var action = Expression.Lambda<Action<object, object[]>>(callExpression, instanceParameterExpression, argsArrayExpression).Compile();
                Func<object, object[], object> func =
                    (obj, args) =>
                    {
                        action(obj, args);
                        return null;
                    };

                var callFuncExpression = Expression.Call(func.Method);

                return Expression.Lambda<Func<object, object[], object>>(callFuncExpression, instanceParameterExpression, argsArrayExpression);
            }

            var castReturnExpression = Expression.Convert(callExpression, typeof(object));
            var lambdaExpression = Expression.Lambda<Func<object, object[], object>>(castReturnExpression, instanceParameterExpression, argsArrayExpression);

            return lambdaExpression;

            Expression[] GetParameterExpressions()
            {
                var parameters = _methodInfo.GetParameters();
                if (parameters.Length == 0)
                {
                    return Array.Empty<ParameterExpression>();
                }

                var expressions = new Expression[parameters.Length];
                for (var i = 0; i < parameters.Length; i++)
                {
                    var parameter = parameters[i];

                    if (!parameter.IsOut)
                    {
                        var argExpression = Expression.ArrayIndex(argsArrayExpression, Expression.Constant(i));
                        expressions[i] = Expression.Convert(argExpression, parameter.ParameterType);
                    }
                    else
                    {
                        // Any `out` variables will have their values discarded.
                        expressions[i] = Expression.Default(parameter.ParameterType.GetElementType());
                    }
                }

                return expressions;
            }
        }
    }
}