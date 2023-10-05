﻿using System.Diagnostics;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ActorGenerator;

[Generator]
public sealed class ActorClientGenerator : ISourceGenerator
{
    private sealed class ActorInterfaceSyntaxReceiver : ISyntaxContextReceiver
    {
        private readonly List<InterfaceDeclarationSyntax> models = new();

        public IEnumerable<InterfaceDeclarationSyntax> Models => this.models;

        #region ISyntaxContextReceiver Members

        public void OnVisitSyntaxNode(GeneratorSyntaxContext context)
        {
            if (context.Node is not InterfaceDeclarationSyntax interfaceDeclarationSyntax || interfaceDeclarationSyntax.BaseList is null)
            {
                return;
            }

            // TODO: Better qualify the IActor check.
            if (interfaceDeclarationSyntax.BaseList.Types.Any(t => t.Type.ToString() == "IActor"))
            {
                this.models.Add(interfaceDeclarationSyntax);
            }
        }

        #endregion
    }

    #region ISourceGenerator Members

    public void Execute(GeneratorExecutionContext context)
    {
        if (context.SyntaxContextReceiver is not ActorInterfaceSyntaxReceiver actorInterfaceSyntaxReceiver)
        {
            return;
        }

        foreach (var model in actorInterfaceSyntaxReceiver.Models)
        {
            var semanticModel = context.Compilation.GetSemanticModel(model.SyntaxTree);
            var symbol = semanticModel.GetDeclaredSymbol(model) as INamedTypeSymbol;

            if (symbol is null)
            {
                continue;
            }

            var actorInterfaceTypeName = symbol.Name;
            var fullyQualifiedActorInterfaceTypeName = symbol.ToString();

            var members = symbol.GetMembers().OfType<IMethodSymbol>().Where(m => m.MethodKind == MethodKind.Ordinary).ToList();

            var methodImplementations = String.Join("\n", members.Select(GenerateMethodImplementation));

            var source = $@"// <auto-generated/>
using Dapr.Actors;
using Dapr.Actors.Client;

namespace {"bar"}
{{
    public sealed class {actorInterfaceTypeName}ManualProxy : {fullyQualifiedActorInterfaceTypeName}
    {{
        private readonly ActorProxy actorProxy;

        public {actorInterfaceTypeName}ManualProxy(ActorProxy actorProxy)
        {{
            this.actorProxy = actorProxy;
        }}

        {methodImplementations}
    }}
}}
";
            // Add the source code to the compilation
            context.AddSource($"{actorInterfaceTypeName}.g.cs", source);
        }
    }

    public void Initialize(GeneratorInitializationContext context)
    {
        while (!Debugger.IsAttached)
        {
            System.Threading.Thread.Sleep(500);
        }

        context.RegisterForSyntaxNotifications(() => new ActorInterfaceSyntaxReceiver());
    }

    #endregion

    private static string GenerateMethodImplementation(IMethodSymbol method)
    {
        string methodName = method.Name;
        var returnType = method.ReturnType as INamedTypeSymbol;

        if (returnType is null)
        {
            // TODO: Return a diagnostic instead.
            throw new InvalidOperationException("Return type is not a named type symbol.");
        }

        var parameterType = method.Parameters.FirstOrDefault();

        var returnTypeArgument = returnType.TypeArguments.FirstOrDefault();

        if (returnTypeArgument is null)
        {
            return $@"
            public {returnType.ToString()} {methodName}({(parameterType is not null ? $"{parameterType.Type.ToString()} {parameterType.Name}" : "")})
            {{
                return this.actorProxy.InvokeMethodAsync(""{methodName}"");
            }}
            ";
        }
        else
        {
            return $@"
            public {returnType} {methodName}({(parameterType is not null ? $"{parameterType.Type.ToString()} {parameterType.Name}" : "")})
            {{
                return this.actorProxy.InvokeMethodAsync<{returnTypeArgument.ToString()}>(""{methodName}"");
            }}
            ";
        }
    }
}