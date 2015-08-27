﻿using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using CodeContractNullability.Test.TestDataBuilders;
using NUnit.Framework;

namespace CodeContractNullability.Test.Specs
{
    /// <summary>
    /// Tests for reporting item nullability diagnostics on properties.
    /// </summary>
    [TestFixture]
    internal class PropertyCollectionSpecs : ItemNullabilityNUnitRoslynTest
    {
        [Test]
        public void When_property_is_annotated_with_item_not_nullable_it_must_be_skipped()
        {
            // Arrange
            ParsedSourceCode source = new ClassSourceCodeBuilder()
                .WithNullabilityAttributes(new NullabilityAttributesBuilder()
                    .Imported())
                .Using(typeof (IEnumerable<>).Namespace)
                .InGlobalScope(@"
                    class C
                    {
                        [ItemNotNull]
                        IEnumerable<string> P { get; set; }
                    }
                ")
                .Build();

            // Act and assert
            VerifyItemNullabilityDiagnostic(source);
        }

        [Test]
        public void When_property_is_annotated_with_item_nullable_it_must_be_skipped()
        {
            // Arrange
            ParsedSourceCode source = new ClassSourceCodeBuilder()
                .WithNullabilityAttributes(new NullabilityAttributesBuilder()
                    .Imported())
                .Using(typeof (IEnumerable<>).Namespace)
                .InGlobalScope(@"
                    class C
                    {
                        [ItemCanBeNull]
                        IEnumerable<string> P { get; set; }
                    }
                ")
                .Build();

            // Act and assert
            VerifyItemNullabilityDiagnostic(source);
        }

        [Test]
        public void When_property_item_type_is_value_type_it_must_be_skipped()
        {
            // Arrange
            ParsedSourceCode source = new MemberSourceCodeBuilder()
                .WithNullabilityAttributes(new NullabilityAttributesBuilder())
                .Using(typeof (IList<>).Namespace)
                .InDefaultClass(@"
                    IList<int> P { get; set; }
                ")
                .Build();

            // Act and assert
            VerifyItemNullabilityDiagnostic(source);
        }

        [Test]
        public void When_property_item_type_is_generic_value_type_it_must_be_skipped()
        {
            // Arrange
            ParsedSourceCode source = new ClassSourceCodeBuilder()
                .WithNullabilityAttributes(new NullabilityAttributesBuilder())
                .Using(typeof (IEnumerable<>).Namespace)
                .InGlobalScope(@"
                    class C<T> where T : struct
                    {
                        IEnumerable<T> P { get; set; }
                    }
                ")
                .Build();

            // Act and assert
            VerifyItemNullabilityDiagnostic(source);
        }

        [Test]
        public void When_property_item_type_is_enum_it_must_be_skipped()
        {
            // Arrange
            ParsedSourceCode source = new MemberSourceCodeBuilder()
                .WithNullabilityAttributes(new NullabilityAttributesBuilder())
                .Using(typeof (IEnumerable<>).Namespace)
                .Using(typeof (BindingFlags).Namespace)
                .InDefaultClass(@"
                    IEnumerable<BindingFlags> P { get; set; }
                ")
                .Build();

            // Act and assert
            VerifyItemNullabilityDiagnostic(source);
        }

        [Test]
        public void When_property_item_type_is_nullable_it_must_be_reported_and_fixed()
        {
            // Arrange
            ParsedSourceCode source = new MemberSourceCodeBuilder()
                .WithNullabilityAttributes(new NullabilityAttributesBuilder())
                .Using(typeof (IEnumerable<>).Namespace)
                .InDefaultClass(@"
                    <annotate/> IEnumerable<int?> [|P|] { get; set; }
                ")
                .Build();

            // Act and assert
            VerifyItemNullabilityFix(source);
        }

        [Test]
        public void When_property_item_type_is_generic_nullable_it_must_be_reported_and_fixed()
        {
            // Arrange
            ParsedSourceCode source = new ClassSourceCodeBuilder()
                .WithNullabilityAttributes(new NullabilityAttributesBuilder())
                .Using(typeof (IEnumerable<>).Namespace)
                .InGlobalScope(@"
                    class C<T> where T : struct
                    {
                        <annotate/> IEnumerable<T?> [|P|] { get; set; }
                    }
                ")
                .Build();

            // Act and assert
            VerifyItemNullabilityFix(source);
        }

        [Test]
        public void When_property_item_type_is_reference_it_must_be_reported_and_fixed()
        {
            // Arrange
            ParsedSourceCode source = new MemberSourceCodeBuilder()
                .WithNullabilityAttributes(new NullabilityAttributesBuilder())
                .Using(typeof (IEnumerable<>).Namespace)
                .InDefaultClass(@"
                    <annotate/> IEnumerable<string> [|P|] { get; }
                ")
                .Build();

            // Act and assert
            VerifyItemNullabilityFix(source);
        }

        [Test]
        public void When_property_item_type_is_object_it_must_be_reported_and_fixed()
        {
            // Arrange
            ParsedSourceCode source = new MemberSourceCodeBuilder()
                .WithNullabilityAttributes(new NullabilityAttributesBuilder())
                .Using(typeof (ArrayList).Namespace)
                .InDefaultClass(@"
                    <annotate/> ArrayList [|P|] { get; }
                ")
                .Build();

            // Act and assert
            VerifyItemNullabilityFix(source);
        }

        [Test]
        public void When_property_is_compiler_generated_it_must_be_skipped()
        {
            // Arrange
            ParsedSourceCode source = new MemberSourceCodeBuilder()
                .WithNullabilityAttributes(new NullabilityAttributesBuilder())
                .Using(typeof (IEnumerable<>).Namespace)
                .Using(typeof (CompilerGeneratedAttribute).Namespace)
                .InDefaultClass(@"
                    [CompilerGenerated]
                    IEnumerable<string> P { get; set; }
                ")
                .Build();

            // Act and assert
            VerifyItemNullabilityDiagnostic(source);
        }

        [Test]
        public void When_property_is_not_debuggable_it_must_be_skipped()
        {
            // Arrange
            ParsedSourceCode source = new MemberSourceCodeBuilder()
                .WithNullabilityAttributes(new NullabilityAttributesBuilder())
                .Using(typeof (IEnumerable<>).Namespace)
                .Using(typeof (DebuggerNonUserCodeAttribute).Namespace)
                .InDefaultClass(@"
                    [DebuggerNonUserCode]
                    IEnumerable<string> P { get; set; }
                ")
                .Build();

            // Act and assert
            VerifyItemNullabilityDiagnostic(source);
        }

        [Test]
        public void When_return_type_of_index_property_is_collection_of_reference_type_it_must_be_reported_and_fixed()
        {
            // Arrange
            ParsedSourceCode source = new MemberSourceCodeBuilder()
                .WithNullabilityAttributes(new NullabilityAttributesBuilder())
                .Using(typeof (IEnumerable<>).Namespace)
                .InDefaultClass(@"
                    <annotate/> IEnumerable<int?> [|this|][int p]
                    {
                        get { throw new NotImplementedException(); }
                        set { throw new NotImplementedException(); }
                    }
                ")
                .Build();

            // Act and assert
            VerifyItemNullabilityFix(source);
        }

        [Test]
        public void When_property_in_base_class_is_annotated_it_must_be_skipped()
        {
            // Arrange
            ParsedSourceCode source = new ClassSourceCodeBuilder()
                .WithNullabilityAttributes(new NullabilityAttributesBuilder()
                    .Imported())
                .Using(typeof (IEnumerable<>).Namespace)
                .InGlobalScope(@"
                    class B
                    {
                        [ItemNotNull]
                        public virtual IEnumerable<string> P { get; set; }
                    }
                        
                    class D1 : B { }
                        
                    class D2 : D1
                    {
                        // implicitly inherits decoration from base class
                        public override IEnumerable<string> P { get; set; }
                    }
                ")
                .Build();

            // Act and assert
            VerifyItemNullabilityDiagnostic(source);
        }

        [Test]
        public void When_property_in_implicit_interface_is_annotated_it_must_be_skipped()
        {
            // Arrange
            ParsedSourceCode source = new ClassSourceCodeBuilder()
                .WithNullabilityAttributes(new NullabilityAttributesBuilder()
                    .Imported())
                .Using(typeof (IList<>).Namespace)
                .InGlobalScope(@"
                    interface I
                    {
                        [ItemCanBeNull]
                        IList<string> P { get; set; }
                    }
                        
                    class C : I
                    {
                        // implicitly inherits decoration from interface
                        public IList<string> P { get; set; }
                    }
                ")
                .Build();

            // Act and assert
            VerifyItemNullabilityDiagnostic(source);
        }

        [Test]
        public void When_property_in_explicit_interface_is_annotated_it_must_be_skipped()
        {
            // Arrange
            ParsedSourceCode source = new ClassSourceCodeBuilder()
                .WithNullabilityAttributes(new NullabilityAttributesBuilder()
                    .Imported())
                .Using(typeof (IEnumerable<>).Namespace)
                .InGlobalScope(@"
                    interface I
                    {
                        [ItemCanBeNull]
                        IEnumerable<string> P { get; set; }
                    }
                        
                    class C : I
                    {
                        // implicitly inherits decoration from interface
                        IEnumerable<string> I.P { get; set; }
                    }
                ")
                .Build();

            // Act and assert
            VerifyItemNullabilityDiagnostic(source);
        }

        [Test]
        public void
            When_property_in_implicit_interface_is_not_annotated_with_explicit_interface_it_must_be_reported_and_fixed()
        {
            // Arrange
            ParsedSourceCode source = new ClassSourceCodeBuilder()
                .WithNullabilityAttributes(new NullabilityAttributesBuilder()
                    .Imported())
                .Using(typeof (IEnumerable<>).Namespace)
                .InGlobalScope(@"
                    interface I
                    {
                        [ItemNotNull]
                        IEnumerable<string> P { get; set; }
                    }

                    class C : I
                    {
                        // implicitly inherits decoration from interface
                        IEnumerable<string> I.P { get; set; }

                        <annotate/> public IEnumerable<string> [|P|] { get; set; }
                    }
                ")
                .Build();

            // Act and assert
            VerifyItemNullabilityFix(source);
        }

        [Test]
        public void
            When_return_value_of_indexer_property_in_implicit_interface_is_not_annotated_it_must_be_reported_and_fixed()
        {
            // Arrange
            ParsedSourceCode source = new ClassSourceCodeBuilder()
                .WithNullabilityAttributes(new NullabilityAttributesBuilder()
                    .Imported())
                .Using(typeof (IEnumerable<>).Namespace)
                .InGlobalScope(@"
                    namespace N
                    {
                        interface I
                        {
                            [ItemCanBeNull]
                            IEnumerable<int?> this[char p] { get; set; }
                        }
                        
                        class C : I
                        {
                            // implicitly inherits decoration from interface
                            IEnumerable<int?> I.this[char p]
                            {
                                get { throw new NotImplementedException(); }
                                set { throw new NotImplementedException(); }
                            }

                            <annotate/> public IEnumerable<int?> [|this|][char p]
                            {
                                get { throw new NotImplementedException(); }
                                set { throw new NotImplementedException(); }
                            }
                        }
                    }
                ")
                .Build();

            // Act and assert
            VerifyItemNullabilityDiagnostic(source);
        }

        [Test]
        public void When_property_is_in_file_with_codegen_comment_at_top_it_must_be_skipped()
        {
            // Arrange
            ParsedSourceCode source = new MemberSourceCodeBuilder()
                .WithNullabilityAttributes(new NullabilityAttributesBuilder())
                .Using(typeof (IEnumerable<>).Namespace)
                .WithHeader(@"
//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//     Runtime Version:4.0.30319.0
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------
")
                .InDefaultClass(@"
                    IEnumerable<string> this[int index] 
                    { 
                        get 
                        { 
                            throw new NotImplementedException(); 
                        }
                        set
                        { 
                            throw new NotImplementedException(); 
                        }
                    }
                ")
                .Build();

            // Act and assert
            VerifyItemNullabilityDiagnostic(source);
        }

        [Test]
        public void When_property_type_is_lazy_it_must_be_reported_and_fixed()
        {
            // Arrange
            ParsedSourceCode source = new MemberSourceCodeBuilder()
                .WithNullabilityAttributes(new NullabilityAttributesBuilder())
                .InDefaultClass(@"
                    <annotate/> Lazy<string> [|P|] { get; set; }
                ")
                .Build();

            // Act and assert
            VerifyItemNullabilityFix(source);
        }

        [Test]
        public void When_property_type_is_task_it_must_be_skipped()
        {
            // Arrange
            ParsedSourceCode source = new MemberSourceCodeBuilder()
                .WithNullabilityAttributes(new NullabilityAttributesBuilder())
                .Using(typeof (Task).Namespace)
                .InDefaultClass(@"
                    public Task P { get; set; }
                ")
                .Build();

            // Act and assert
            VerifyItemNullabilityDiagnostic(source);
        }

        [Test]
        public void When_property_type_is_generic_task_it_must_be_reported_and_fixed()
        {
            // Arrange
            ParsedSourceCode source = new MemberSourceCodeBuilder()
                .WithNullabilityAttributes(new NullabilityAttributesBuilder())
                .Using(typeof (Task<>).Namespace)
                .InDefaultClass(@"
                    <annotate/> Task<string> [|P|] { get; set; }
                ")
                .Build();

            // Act and assert
            VerifyItemNullabilityFix(source);
        }
    }
}