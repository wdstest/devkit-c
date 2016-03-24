﻿using System;
using System.CodeDom;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Xml.Linq;
using System.Xml.Schema;
using System.Xml.Serialization;

namespace Energistics.SchemaGatherer
{
    /// <summary>
    /// Encapsulates methods to generate base data object classes with default .net validation attributes
    /// </summary>
    public static class ValidationExtensions
    {
        /// <summary>
        /// Generates the data objects with code DOM.
        /// </summary>
        /// <param name="targetFolder">The target folder.</param>
        /// <param name="targetXmlFile">The target XML file.</param>
        /// <param name="nameSpace">The name space.</param>
        /// <param name="sourceFolder">The source folder.</param>
        public static void GenerateDataObjectsWithCodeDom(string targetFolder, string targetXmlFile, string nameSpace, string sourceFolder)
        {
            var includeSchemas = new Dictionary<string, XmlSchema>();
            var schemas = CompileXmlSchemas(targetXmlFile, sourceFolder, includeSchemas);

            var codeProvider = CodeDomProvider.CreateProvider("CS");
            var codeNamespace = new CodeNamespace(nameSpace);
            var codeCompileUnit = new CodeCompileUnit();

            var outputFile = Path.Combine(targetFolder, "DataObject.cs");
            var options = CodeGenerationOptions.GenerateProperties;
            var version = "2.0.50727.3038";

            var importer = new XmlSchemaImporter(schemas, options, codeProvider, new ImportContext(new CodeIdentifiers(), false));
            var exporter = new XmlCodeExporter(codeNamespace, codeCompileUnit, codeProvider, options, null);

            codeNamespace.Imports.Add(new CodeNamespaceImport(typeof(XmlRootAttribute).Namespace));
            codeCompileUnit.Namespaces.Add(codeNamespace);

            AddNamespaceComments(codeNamespace, version);
            UpdateGeneratedCodeAttribute(exporter, version);

            foreach (XmlSchema schema in schemas)
            {
                ImportXmlSchema(schema, importer, exporter);
            }

            foreach (XmlSchema schema in includeSchemas.Values)
            {
                ImportXmlSchema(schema, importer, exporter);
            }

            AddValidationAttributes(codeNamespace, schemas);

            using (var writer = new StreamWriter(outputFile, false, Encoding.UTF8))
            {
                codeProvider.GenerateCodeFromCompileUnit(codeCompileUnit, writer, null);
            }
        }

        private static void AddNamespaceComments(CodeNamespace codeNamespace, string version)
        {
            codeNamespace.Comments.Add(new CodeCommentStatement(""));
            codeNamespace.Comments.Add(new CodeCommentStatement(String.Format("This source code was auto-generated by xsd, Version={0}.", version)));
            codeNamespace.Comments.Add(new CodeCommentStatement(""));
        }

        private static CodeAttributeDeclaration UpdateGeneratedCodeAttribute(XmlCodeExporter exporter, string version)
        {
            var property = exporter.GetType().GetProperty("GeneratedCodeAttribute", BindingFlags.Instance | BindingFlags.NonPublic);
            var attribute = property.GetValue(exporter, null) as CodeAttributeDeclaration;

            attribute.Arguments[0].Value = new CodePrimitiveExpression("xsd");
            attribute.Arguments[1].Value = new CodePrimitiveExpression(version);

            return attribute;
        }

        private static void ImportXmlSchema(XmlSchema schema, XmlSchemaImporter importer, XmlCodeExporter exporter)
        {
            foreach (XmlSchemaElement element in schema.Elements.Values)
            {
                exporter.ExportTypeMapping(importer.ImportTypeMapping(element.QualifiedName));
            }
        }

        private static XmlSchemas CompileXmlSchemas(string targetXmlFile, string sourceFolder, Dictionary<string, XmlSchema> includeSchemas)
        {
            var ns = XNamespace.Get("http://microsoft.com/dotnet/tools/xsd/");
            var doc = XDocument.Load(targetXmlFile);

            var schemaUris = new Dictionary<XmlSchema, string>();
            var files = new List<string>();
            var schemas = new XmlSchemas();

            foreach (var schema in doc.Root.Element(ns + "generateClasses").Elements(ns + "schema"))
            {
                var path = schema.Value.ToLowerInvariant();

                if (files.Contains(path))
                {
                    continue;
                }

                using (var stream = File.OpenRead(path))
                {
                    var xsd = XmlSchema.Read(stream, null);
                    schemas.Add(xsd, new Uri(path));
                    includeSchemas.Add(path, xsd);
                    schemaUris.Add(xsd, path);
                    files.Add(path);

                    if (path.EndsWith("typ_catalog.xsd"))
                    {
                        path = Path.Combine(sourceFolder, "typ_catalog.xsd").ToLowerInvariant();
                        includeSchemas.Add(path, xsd);
                        schemaUris[xsd] = path;
                    }
                    if (path.EndsWith("typ_catalogprodml.xsd"))
                    {
                        path = Path.Combine(sourceFolder, "typ_catalogprodml.xsd").ToLowerInvariant();
                        includeSchemas.Add(path, xsd);
                        schemaUris[xsd] = path;
                    }
                }
            }

            foreach (XmlSchema schema in schemas)
            {
                if (String.IsNullOrWhiteSpace(schema.TargetNamespace))
                {
                    schema.TargetNamespace = null;
                }

                var uri = schemaUris[schema];
                LoadSchemaIncludes(schema, includeSchemas, sourceFolder, uri);
            }

            schemas.Compile(null, true);

            return schemas;
        }

        private static void LoadSchemaIncludes(XmlSchema schema, Dictionary<string, XmlSchema> includeSchemas, string sourceFolder, string topPath)
        {
            foreach (XmlSchemaExternal include in schema.Includes)
            {
                var location = include.SchemaLocation;

                if ((include is XmlSchemaImport) || String.IsNullOrWhiteSpace(location))
                {
                    continue;
                }

                var lower = Path.Combine(Path.GetDirectoryName(topPath), location).ToLowerInvariant();

                if (!File.Exists(lower))
                {
                    lower = Path.Combine(sourceFolder, location).ToLowerInvariant();
                }

                if (lower == topPath)
                {
                    include.Schema = new XmlSchema()
                    {
                        TargetNamespace = schema.TargetNamespace
                    };
                    include.SchemaLocation = null;
                    return;
                }

                XmlSchema item = null;

                if (!includeSchemas.TryGetValue(lower, out item) && File.Exists(lower))
                {
                    using (var stream = File.OpenRead(lower))
                    {
                        item = XmlSchema.Read(stream, null);
                        includeSchemas.Add(lower, item);
                        LoadSchemaIncludes(item, includeSchemas, sourceFolder, topPath);
                    }
                }

                if (includeSchemas.TryGetValue(lower, out item))
                {
                    include.Schema = item;
                    include.SchemaLocation = null;
                }
            }
        }

        private static void AddValidationAttributes(CodeNamespace codeNamespace, IEnumerable<XmlSchema> schemas)
        {
            var types = new List<string>();

            foreach (XmlSchema schema in schemas)
            {
                var schemaElement = schema.Elements.Values.Cast<XmlSchemaElement>().FirstOrDefault();

                if (schemaElement != null)
                {
                    AddValidationAttributes(codeNamespace, schemaElement, types);
                }
            }
        }

        private static void AddValidationAttributes(CodeNamespace codeNamespace, XmlSchemaElement schemaElement, List<string> types)
        {
            var typeDeclaration = codeNamespace.Types.Cast<CodeTypeDeclaration>()
                .FirstOrDefault(x => x.Name == schemaElement.SchemaTypeName.Name);

            if (typeDeclaration != null && !types.Contains(typeDeclaration.Name))
            {
                types.Add(typeDeclaration.Name);

                var schemaType = schemaElement.ElementSchemaType as XmlSchemaComplexType;
                if (schemaType != null)
                {
                    foreach (var attribute in schemaType.AttributeUses.Values.OfType<XmlSchemaAttribute>())
                    {
                        AddAttributeValidation(codeNamespace, typeDeclaration, attribute);
                    }

                    var schemaSequence = schemaType.ContentTypeParticle as XmlSchemaSequence;
                    if (schemaSequence != null)
                    {
                        var elements = schemaSequence.Items.OfType<XmlSchemaSequence>()
                            .SelectMany(x => x.Items.OfType<XmlSchemaElement>())
                            .Union(schemaSequence.Items.OfType<XmlSchemaElement>());

                        foreach (var element in elements)
                        {
                            AddElementValidation(codeNamespace, typeDeclaration, element);

                            AddValidationAttributes(codeNamespace, element, types);
                        }
                    }
                }
            }
        }

        private static void AddAttributeValidation(CodeNamespace codeNamespace, CodeTypeDeclaration typeDeclaration, XmlSchemaAttribute attribute)
        {
            var memberProperty = GetMemberProperty(typeDeclaration, attribute.Name);

            if (memberProperty != null)
            {
                AddDescriptionAttribute(typeDeclaration, memberProperty, GetAnnotation(attribute));
            }
        }

        private static void AddElementValidation(CodeNamespace codeNamespace, CodeTypeDeclaration typeDeclaration, XmlSchemaElement element)
        {
            var memberProperty = GetMemberProperty(typeDeclaration, element.Name);
            var restrictions = GetElementRestrictions(element);

            if (memberProperty != null)
            {
                if (element.MinOccurs > 0)
                {
                    AddRequiredAttribute(typeDeclaration, memberProperty);
                }

                if (restrictions.Any())
                {
                    if (memberProperty.Type.ArrayElementType == null)
                    {
                        AddValidationAttributes(typeDeclaration, memberProperty, restrictions);
                    }
                    else
                    {
                        var baseTypeDeclaration = codeNamespace.Types.Cast<CodeTypeDeclaration>()
                            .FirstOrDefault(x => x.Name == memberProperty.Type.BaseType);

                        if (baseTypeDeclaration != null)
                        {
                            var xmlTextProperty = baseTypeDeclaration.Members.OfType<CodeMemberProperty>()
                                .FirstOrDefault(x => Has<XmlTextAttribute>(x));

                            if (xmlTextProperty != null)
                            {
                                AddValidationAttributes(baseTypeDeclaration, xmlTextProperty, restrictions);
                            }
                        }
                    }
                }

                AddDescriptionAttribute(typeDeclaration, memberProperty, GetAnnotation(element));
            }
        }

        private static void AddRequiredAttribute(CodeTypeDeclaration typeDeclaration, CodeMemberProperty memberProperty)
        {
            memberProperty.CustomAttributes.Add(new CodeAttributeDeclaration(typeof(RequiredAttribute).FullName));
        }

        private static void AddValidationAttributes(CodeTypeDeclaration typeDeclaration, CodeMemberProperty memberProperty, IEnumerable<XmlSchemaFacet> facets)
        {
            //var length = facets.OfType<XmlSchemaLengthFacet>().FirstOrDefault();
            var pattern = facets.OfType<XmlSchemaPatternFacet>().FirstOrDefault();

            //var minLength = facets.OfType<XmlSchemaMinLengthFacet>().FirstOrDefault();
            var maxLength = facets.OfType<XmlSchemaMaxLengthFacet>().FirstOrDefault();

            var minInclusive = facets.OfType<XmlSchemaMinInclusiveFacet>().FirstOrDefault();
            var maxInclusive = facets.OfType<XmlSchemaMaxInclusiveFacet>().FirstOrDefault();

            //var minExclusive = facets.OfType<XmlSchemaMinExclusiveFacet>().FirstOrDefault();
            //var maxExclusive = facets.OfType<XmlSchemaMaxExclusiveFacet>().FirstOrDefault();

            if (pattern != null)
            {
                if (memberProperty.Type.BaseType == typeof(String).FullName && !Has<RegularExpressionAttribute>(memberProperty))
                {
                    memberProperty.CustomAttributes.Add(new CodeAttributeDeclaration(typeof(RegularExpressionAttribute).FullName,
                        new CodeAttributeArgument(new CodePrimitiveExpression(pattern.Value))));
                }
                else if (memberProperty.Type.BaseType == typeof(DateTime).FullName && pattern.Value != ".+")
                {
                    var memberField = GetMemberField(typeDeclaration, memberProperty.Name + "Field");
                    memberField.Type = new CodeTypeReference("Energistics.SchemaGatherer.Timestamp");
                    memberProperty.Type = memberField.Type;
                }
            }

            if (maxLength != null && memberProperty.Type.BaseType == typeof(String).FullName && !Has<StringLengthAttribute>(memberProperty))
            {
                memberProperty.CustomAttributes.Add(new CodeAttributeDeclaration(typeof(StringLengthAttribute).FullName,
                    new CodeAttributeArgument(new CodePrimitiveExpression(Int32.Parse(maxLength.Value)))));
            }

            if (minInclusive != null && maxInclusive != null && !Has<RangeAttribute>(memberProperty))
            {
                memberProperty.CustomAttributes.Add(new CodeAttributeDeclaration(typeof(RangeAttribute).FullName,
                    new CodeAttributeArgument(new CodePrimitiveExpression(Double.Parse(minInclusive.Value))),
                    new CodeAttributeArgument(new CodePrimitiveExpression(Double.Parse(maxInclusive.Value)))));
            }
        }

        private static void AddDescriptionAttribute(CodeTypeDeclaration typeDeclaration, CodeMemberProperty memberProperty, string description)
        {
            if (!String.IsNullOrEmpty(description) && !Has<DescriptionAttribute>(memberProperty))
            {
                memberProperty.CustomAttributes.Add(new CodeAttributeDeclaration(typeof(DescriptionAttribute).FullName,
                    new CodeAttributeArgument(new CodePrimitiveExpression(description))));

                memberProperty.Comments.Add(new CodeCommentStatement("<summary>" + description + "</summary>", true));
            }
        }

        private static bool Has<T>(CodeMemberProperty memberProperty)
        {
            return memberProperty.CustomAttributes
                .OfType<CodeAttributeDeclaration>()
                .Where(x => x.Name == typeof(T).FullName)
                .Any();
        }

        private static CodeMemberProperty GetMemberProperty(CodeTypeDeclaration typeDeclaration, string propertyName)
        {
            return typeDeclaration.Members.OfType<CodeMemberProperty>()
                .FirstOrDefault(x => x.Name == propertyName);
        }

        private static CodeMemberField GetMemberField(CodeTypeDeclaration typeDeclaration, string propertyName)
        {
            return typeDeclaration.Members.OfType<CodeMemberField>()
                .FirstOrDefault(x => x.Name == propertyName);
        }

        private static IEnumerable<XmlSchemaFacet> GetElementRestrictions(XmlSchemaElement schemaElement)
        {
            var facets = new List<XmlSchemaFacet>();

            GetElementTypeRestrictions(facets, schemaElement.ElementSchemaType as XmlSchemaSimpleType);

            var complexType = schemaElement.ElementSchemaType as XmlSchemaComplexType;
            if (complexType != null)
            {
                GetElementTypeRestrictions(facets, complexType.BaseXmlSchemaType as XmlSchemaSimpleType);
            }

            return facets;
        }

        private static void GetElementTypeRestrictions(List<XmlSchemaFacet> facets, XmlSchemaSimpleType elementType)
        {
            while (elementType != null)
            {
                var restrictions = elementType.Content as XmlSchemaSimpleTypeRestriction;

                if (restrictions != null)
                {
                    foreach (var facet in restrictions.Facets.OfType<XmlSchemaFacet>())
                    {
                        if (!facets.Any(x => x.GetType() == facet.GetType()))
                        {
                            facets.Add(facet);
                        }
                    }
                }

                elementType = elementType.BaseXmlSchemaType as XmlSchemaSimpleType;
            }
        }

        private static string GetAnnotation(XmlSchemaAnnotated annotated)
        {
            var description = string.Empty;

            if (annotated.Annotation != null)
            {
                description = annotated.Annotation.Items
                    .OfType<XmlSchemaDocumentation>()
                    .SelectMany(x => x.Markup)
                    .Select(x => x.InnerText
                        .Replace(Environment.NewLine, " ")
                        .Replace("\n", " ")
                        .Replace("\t", string.Empty)
                        .Trim())
                    .FirstOrDefault();
            }

            return description;
        }
    }
}
