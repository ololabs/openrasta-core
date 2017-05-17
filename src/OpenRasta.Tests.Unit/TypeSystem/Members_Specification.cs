#region License
/* Authors:
 *      Sebastien Lambla (seb@serialseb.com)
 * Copyright:
 *      (C) 2007-2009 Caffeine IT & naughtyProd Ltd (http://www.caffeine-it.com)
 * License:
 *      This file is distributed under the terms of the MIT License found at the end of this file.
 */
#endregion

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using OpenRasta.Binding;
using OpenRasta.Testing;
using OpenRasta.Tests.Unit.Fakes;
using OpenRasta.TypeSystem;
using OpenRasta.TypeSystem.ReflectionBased;
using Shouldly;
using TypeSystems = OpenRasta.TypeSystem.TypeSystems;

namespace Accessors_Specification
{
    public class when_creating_types : IType_context
    {
        [Test]
        public void a_reference_type_can_be_assigned_a_null_value()
        {
            new ReflectionBasedType(_typeSystem,typeof(string)).CanSetValue(null)
                .LegacyShouldBe(true);
        }

        [Test]
        public void a_reflection_type_cannot_be_equal_to_null()
        {
            new ReflectionBasedType(_typeSystem,typeof(string)).Equals(null)
                .LegacyShouldBeFalse();
        }

        [Test]
        public void a_type_can_be_created()
        {
            object result;
            new ReflectionBasedType(_typeSystem,typeof(string)).TryCreateInstance(new[] { "value" }, (str, type) => BindingResult.Success(str), out result)
                .LegacyShouldBeTrue();
            result.LegacyShouldBe("value");
        }

        [Test]
        public void a_type_creation_resulting_in_a_failure_doesnt_get_created()
        {
            object result;
            new ReflectionBasedType(_typeSystem,typeof(string)).TryCreateInstance(new[] { "value" }, (str, type) => BindingResult.Failure(), out result)
                .LegacyShouldBeFalse();
        }

        [Test]
        public void a_value_type_cannot_be_assigned_a_null_value()
        {
            new ReflectionBasedType(_typeSystem,typeof(int)).CanSetValue(null)
                .LegacyShouldBeFalse();
        }

        [Test]
        public void an_incorrect_type_cannot_be_assigned()
        {
            new ReflectionBasedType(_typeSystem,typeof(int)).CanSetValue("hello")
                .LegacyShouldBeFalse();
        }

        [Test, Ignore("no idea")]
        public void interfaces_cannot_be_initialized()
        {
            Executing((Action)GivenTypeFor<ICollection>)
                .ShouldThrow<NotSupportedException>();
        }

        [Test]
        public void the_name_is_the_type_name()
        {
            new ReflectionBasedType(_typeSystem,typeof(string)).Name
                .LegacyShouldBe("String");
        }

        [Test]
        public void two_reflection_based_types_are_equal_if_built_from_the_same_native_type()
        {
            var type1 = new ReflectionBasedType(_typeSystem,typeof(string));
            var type2 = new ReflectionBasedType(_typeSystem,typeof(string));
            type1.Equals(type2).LegacyShouldBeTrue();
            type1.GetHashCode().LegacyShouldBe(type2.GetHashCode());
        }

        [Test]
        public void two_reflection_based_types_are_not_equal_if_built_from_different_native_types()
        {
            var type1 = new ReflectionBasedType(_typeSystem,typeof(string));
            var type2 = new ReflectionBasedType(_typeSystem,typeof(object));
            type1.Equals(type2).LegacyShouldBeFalse();
            type1.GetHashCode().LegacyShouldNotBe(type2.GetHashCode());
        }
    }

    public class when_comparing_types : IType_context
    {
        [Test]
        public void a_type_compared_to_a_null_results_in_minus_one()
        {
            new ReflectionBasedType(_typeSystem,typeof(int)).CompareTo(null)
                .LegacyShouldBe(-1);
        }

        [Test]
        public void two_types_not_in_an_inheritance_hierarchy_compare_to_minus_one()
        {
            new ReflectionBasedType(_typeSystem,typeof(int)).CompareTo(new ReflectionBasedType(_typeSystem,typeof(string)))
                .LegacyShouldBe(-1);
        }
    }

    public class when_testing_if_a_member_is_assignable_to_another : IType_context
    {
        [Test]
        public void a_type_is_assingable_to_a_parent_type()
        {
            TypeForClr<Customer>().IsAssignableTo(TypeForClr<object>())
                .LegacyShouldBeTrue();
        }
        [Test]
        public void a_type_is_not_assignable_to_a_type_not_in_its_inheritance_hierarchy()
        {
            // I realize that theoretically, Frodo could be a Customer. But you know, Frodo doesn't really exist.
            TypeForClr<Customer>().IsAssignableTo(TypeForClr<Frodo>())
                .LegacyShouldBeFalse();
        }
    }

    public class when_accessing_properties_by_name : IType_context
    {
        [Test]
        public void a_nested_property_is_returned()
        {
            GivenTypeFor<Type>();
            ThenTheProperty("Namespace.Length").Type.Equals<int>().LegacyShouldBeTrue();
        }

        [Test]
        public void a_property_is_returned()
        {
            GivenTypeFor<string>();

            ThenTheProperty("Length").Type.Equals<int>().LegacyShouldBeTrue();
        }

        [Test]
        public void a_property_on_the_result_of_an_indexer_is_found()
        {
            GivenTypeFor<House>();
            ThenTheProperty("Customers:0.FirstName").Type.Equals<string>().LegacyShouldBeTrue();
        }

        [Test]
        public void a_wongly_formatted_property_returns_nothing()
        {
            GivenTypeFor<int>();
            ThenTheProperty(".").LegacyShouldBeNull();
        }

        [Test]
        public void an_indexer_for_a_type_without_an_indexer_returns_nothing()
        {
            GivenTypeFor<int>();
            ThenTheProperty(":0").LegacyShouldBeNull();
        }

        [Test]
        public void an_indexer_is_found_and_recognized()
        {
            GivenTypeFor<House>();
            ThenTheProperty("Customers:0").Type.Equals<Customer>().LegacyShouldBeTrue();
        }

        [Test]
        public void an_unknown_nested_property_returns_nothing()
        {
            GivenTypeFor<int>();
            ThenTheProperty("something.orsomethingelse").LegacyShouldBeNull();
        }

        [Test]
        public void an_unkown_property_returns_nothing()
        {
            GivenTypeFor<int>();
            ThenTheProperty("something").LegacyShouldBeNull();
        }

        [Test]
        public void can_successfully_assign_a_property()
        {
            GivenTypeFor<Customer>();
            var newCustomer = new Customer();
            ThenTheProperty("FirstName").TrySetValue(newCustomer, new[] { "Frodo" }, (str, t) => BindingResult.Success(str))
                .LegacyShouldBeTrue();
            newCustomer.FirstName.LegacyShouldBe("Frodo");
        }

        [Test]
        public void cannot_set_value_for_a_readonly_property()
        {
            GivenTypeFor<string>();

            ThenTheProperty("Length").TrySetValue(string.Empty, 20)
                .LegacyShouldBeFalse();
        }

        [Test]
        public void the_case_is_ignored()
        {
            GivenTypeFor<string>();

            ThenTheProperty("length").LegacyShouldBe(ThenTheProperty("Length"));
        }

        [Test]
        public void the_name_is_the_name_of_the_property()
        {
            GivenTypeFor<string>();

            ThenTheProperty("Length").Name.LegacyShouldBe("Length");
        }

        IProperty ThenTheProperty(string propertyName)
        {
            return Type.FindPropertyByPath(propertyName);
        }
    }
    public class when_accessing_methods : IType_context
    {
        [Test]
        public void all_the_methods_are_found()
        {
            GivenTypeFor<RingOfPower>();

            TheMethods.Count().LegacyShouldBe(typeof(RingOfPower).GetMethods(
                BindingFlags.Static | BindingFlags.FlattenHierarchy | BindingFlags.Public | BindingFlags.Instance
                ).Length);

          var method = TheMethods.First(x => x.Name == "RuleThemAll");
          method.ShouldNotBeNull();
          method.InputMembers.Count().LegacyShouldBe(0);
        }
        [Test]
        public void a_method_has_the_correct_parameter_name()
        {
            GivenTypeFor<RingOfPower>();

            var wornByMethod = Type.GetMethod("WornBy");
            wornByMethod
                .ShouldNotBeNull();
          wornByMethod.InputMembers.Count().LegacyShouldBe(1);

            wornByMethod.InputMembers.First().Name.LegacyShouldBe("frodo");
            wornByMethod.InputMembers.First().TypeName.LegacyShouldBe("Frodo");

        }
        [Test]
        public void a_method_name_search_is_case_insensitive()
        {
            GivenTypeFor<RingOfPower>();

            Type.GetMethod("WornBy")
                .LegacyShouldBeTheSameInstanceAs(Type.GetMethod("wornby"));
        }
        [Test]
        public void a_method_defined_in_the_base_type_has_the_correct_base_type_owner()
        {
            GivenTypeFor<RingOfPower>();

            Type.GetMethod("ToString").Owner.TypeName.LegacyShouldBe("Object");
        }
        protected ICollection<IMethod> TheMethods { get { return Type.GetMethods(); } }
    }

    public class RingOfPower
    {
        public void RuleThemAll() {}
        public void WornBy(Frodo frodo) {}
    }

    public class when_building_types_from_the_type_system : context
    {
        readonly ITypeSystem TypeSystem = TypeSystems.Default;

        [Test]
        public void the_instance_cannot_be_null()
        {
            Executing(() => TypeSystem.FromInstance(null))
                .ShouldThrow<ArgumentNullException>();
        }

        [Test]
        public void the_type_cannot_be_null()
        {
            Executing(() => TypeSystem.FromClr(null))
                .ShouldThrow<ArgumentNullException>();
        }
    }

    public class IType_context : context
    {
        
        protected ITypeSystem _typeSystem;

        public IType_context()
        {
            _typeSystem = TypeSystems.Default;
        }
        protected IType Type;

        protected void GivenTypeFor<TTarget>()
        {
            Type = TypeForClr<TTarget>();
        }

        protected IType TypeForClr<TTarget>()
        {
            return _typeSystem.FromClr(typeof(TTarget));
        }
    }
}

#region Full license
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
#endregion
