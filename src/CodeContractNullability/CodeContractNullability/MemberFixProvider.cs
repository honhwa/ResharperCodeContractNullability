﻿using System;
using System.Threading;
using System.Threading.Tasks;
using CodeContractNullability.NullabilityAttributes;
using JetBrains.Annotations;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.Simplification;

namespace CodeContractNullability
{
    public class MemberFixProvider
    {
        [NotNull]
        private static readonly SyntaxAnnotation NamespaceImportAnnotation = new SyntaxAnnotation();

        [NotNull]
        public async Task ProvideFixes(CodeFixContext context, bool appliesToItem)
        {
            foreach (Diagnostic diagnostic in context.Diagnostics)
            {
                NullabilityAttributeSymbols nullSymbols =
                    await GetNullabilityAttributesFromDiagnostic(context, diagnostic);

                SyntaxNode syntaxRoot =
                    await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
                SyntaxNode targetSyntax = syntaxRoot.FindNode(context.Span);

                FieldDeclarationSyntax fieldSyntax = targetSyntax is VariableDeclaratorSyntax
                    ? targetSyntax.GetAncestorOrThis<FieldDeclarationSyntax>()
                    : null;

                if (targetSyntax is MethodDeclarationSyntax || targetSyntax is IndexerDeclarationSyntax ||
                    targetSyntax is PropertyDeclarationSyntax || targetSyntax is ParameterSyntax || fieldSyntax != null)
                {
                    RegisterFixesForSyntaxNode(context, fieldSyntax ?? targetSyntax, diagnostic, nullSymbols,
                        appliesToItem);
                }
            }
        }

        [NotNull]
        private static async Task<NullabilityAttributeSymbols> GetNullabilityAttributesFromDiagnostic(
            CodeFixContext context, [NotNull] Diagnostic diagnostic)
        {
            NullabilityAttributeMetadataNames names =
                NullabilityAttributeMetadataNames.FromImmutableDictionary(diagnostic.Properties);

            Compilation compilation =
                await context.Document.Project.GetCompilationAsync(context.CancellationToken).ConfigureAwait(false);

            var attributeProvider = new CachingNullabilityAttributeProvider(names);
            NullabilityAttributeSymbols nullSymbols = attributeProvider.GetSymbols(compilation,
                context.CancellationToken);
            if (nullSymbols == null)
            {
                throw new InvalidOperationException("Internal error - failed to resolve attributes.");
            }
            return nullSymbols;
        }

        private void RegisterFixesForSyntaxNode(CodeFixContext context, [NotNull] SyntaxNode syntaxNode,
            [NotNull] Diagnostic diagnostic, [NotNull] NullabilityAttributeSymbols nullSymbols, bool appliesToItem)
        {
            RegisterFixForNotNull(context, syntaxNode, diagnostic, nullSymbols, appliesToItem);
            RegisterFixForCanBeNull(context, syntaxNode, diagnostic, nullSymbols, appliesToItem);
        }

        private void RegisterFixForNotNull(CodeFixContext context, [NotNull] SyntaxNode syntaxNode,
            [NotNull] Diagnostic diagnostic, [NotNull] NullabilityAttributeSymbols nullSymbols, bool appliesToItem)
        {
            INamedTypeSymbol notNullAttribute = appliesToItem ? nullSymbols.ItemNotNull : nullSymbols.NotNull;

            Func<CancellationToken, Task<Document>> fixForNotNull =
                cancellationToken =>
                    WithAttributeAsync(notNullAttribute, context.Document, syntaxNode, cancellationToken);

            string notNullText = "Decorate with " + GetDisplayNameFor(notNullAttribute);
            RegisterCodeFixFor(fixForNotNull, notNullText, context, diagnostic);
        }

        private void RegisterFixForCanBeNull(CodeFixContext context, [NotNull] SyntaxNode syntaxNode,
            [NotNull] Diagnostic diagnostic, [NotNull] NullabilityAttributeSymbols nullSymbols, bool appliesToItem)
        {
            INamedTypeSymbol canBeNullAttribute = appliesToItem ? nullSymbols.ItemCanBeNull : nullSymbols.CanBeNull;

            Func<CancellationToken, Task<Document>> fixForCanBeNull =
                cancellationToken =>
                    WithAttributeAsync(canBeNullAttribute, context.Document, syntaxNode, cancellationToken);

            string canBeNullText = "Decorate with " + GetDisplayNameFor(canBeNullAttribute);
            RegisterCodeFixFor(fixForCanBeNull, canBeNullText, context, diagnostic);
        }

        [NotNull]
        private static string GetDisplayNameFor([NotNull] INamedTypeSymbol attribute)
        {
            return attribute.Name.Replace("Attribute", "");
        }

        [NotNull]
        private async Task<Document> WithAttributeAsync([NotNull] INamedTypeSymbol attribute,
            [NotNull] Document document, [NotNull] SyntaxNode syntaxNode, CancellationToken cancellationToken)
        {
            DocumentEditor editor = await DocumentEditor.CreateAsync(document, cancellationToken).ConfigureAwait(false);

            // Add NotNull/CanBeNull/ItemNotNull/ItemCanBeNull attribute.
            SyntaxNode attributeSyntax =
                editor.Generator.Attribute(editor.Generator.TypeExpression(attribute))
                    .WithAdditionalAnnotations(Simplifier.Annotation, Formatter.Annotation, NamespaceImportAnnotation);
            editor.AddAttribute(syntaxNode, attributeSyntax);
            Document documentWithAttribute = editor.GetChangedDocument();

            // Add namespace import.
            Document documentWithImport =
                await
                    ImportAdder.AddImportsAsync(documentWithAttribute, NamespaceImportAnnotation, null,
                        cancellationToken).ConfigureAwait(false);

            // Simplify and reformat all annotated nodes.
            Document simplified = await SimplifyAsync(documentWithImport, cancellationToken).ConfigureAwait(false);
            SyntaxNode formatted = await FormatAsync(simplified, cancellationToken).ConfigureAwait(false);
            return simplified.WithSyntaxRoot(formatted);
        }

        private void RegisterCodeFixFor([NotNull] Func<CancellationToken, Task<Document>> applyFixAction,
            [NotNull] string description, CodeFixContext context, [NotNull] Diagnostic diagnostic)
        {
            CodeAction codeAction = CodeAction.Create(description, applyFixAction, description);
            context.RegisterCodeFix(codeAction, diagnostic);
        }

        [NotNull]
        private static async Task<Document> SimplifyAsync([NotNull] Document document,
            CancellationToken cancellationToken)
        {
            return await Simplifier.ReduceAsync(document, cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        [NotNull]
        private static async Task<SyntaxNode> FormatAsync([NotNull] Document document,
            CancellationToken cancellationToken)
        {
            SyntaxNode root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
            return Formatter.Format(root, document.Project.Solution.Workspace);
        }
    }
}