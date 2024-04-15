using System.CodeDom;
using System.CodeDom.Compiler;
using System.Collections;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Reflection;
using System.Resources;
using System.Resources.NetStandard;
using System.Runtime.CompilerServices;
using System.Security;

namespace ResGen
{
    /// <summary>Provides support for strongly typed resources. This class cannot be inherited.</summary>
    public static class StronglyTypedResourceBuilder
    {
        private const string ResMgrFieldName = "resourceMan";
        private const string ResMgrPropertyName = "ResourceManager";
        private const string CultureInfoFieldName = "resourceCulture";
        private const string CultureInfoPropertyName = "Culture";
        private static readonly char[] CharsToReplace = new char[30]
        {
            ' ',
            ' ',
            '.',
            ',',
            ';',
            '|',
            '~',
            '@',
            '#',
            '%',
            '^',
            '&',
            '*',
            '+',
            '-',
            '/',
            '\\',
            '<',
            '>',
            '?',
            '[',
            ']',
            '(',
            ')',
            '{',
            '}',
            '"',
            '\'',
            ':',
            '!'
        };
        private const char ReplacementChar = '_';
        private const string DocCommentSummaryStart = "<summary>";
        private const string DocCommentSummaryEnd = "</summary>";
        private const int DocCommentLengthThreshold = 512;

        /// <summary>Generates a class file that contains strongly typed properties that match the resources referenced in the specified collection.</summary>
        /// <param name="resourceList">An <see cref="T:System.Collections.IDictionary" /> collection where each dictionary entry key/value pair is the name of a resource and the value of the resource.</param>
        /// <param name="baseName">The name of the class to be generated.</param>
        /// <param name="generatedCodeNamespace">The namespace of the class to be generated.</param>
        /// <param name="codeProvider">A <see cref="T:System.CodeDom.Compiler.CodeDomProvider" /> class that provides the language in which the class will be generated.</param>
        /// <param name="internalClass">
        /// <see langword="true" /> to generate an internal class; <see langword="false" /> to generate a public class.</param>
        /// <param name="unmatchable">An array that contains each resource name for which a property cannot be generated. Typically, a property cannot be generated because the resource name is not a valid identifier.</param>
        /// <returns>A <see cref="T:System.CodeDom.CodeCompileUnit" /> container.</returns>
        /// <exception cref="T:System.ArgumentNullException">
        /// <paramref name="resourceList" />, <paramref name="basename" />, or <paramref name="codeProvider" /> is <see langword="null" />.</exception>
        /// <exception cref="T:System.ArgumentException">A resource node name does not match its key in <paramref name="resourceList" />.</exception>
        public static CodeCompileUnit Create(
            IDictionary resourceList,
            string baseName,
            string generatedCodeNamespace,
            CodeDomProvider codeProvider,
            bool internalClass,
            out string[] unmatchable)
        {
            return StronglyTypedResourceBuilder.Create(resourceList, baseName, generatedCodeNamespace, (string) null, codeProvider, internalClass, out unmatchable);
        }

        /// <summary>Generates a class file that contains strongly typed properties that match the resources referenced in the specified collection.</summary>
        /// <param name="resourceList">An <see cref="T:System.Collections.IDictionary" /> collection where each dictionary entry key/value pair is the name of a resource and the value of the resource.</param>
        /// <param name="baseName">The name of the class to be generated.</param>
        /// <param name="generatedCodeNamespace">The namespace of the class to be generated.</param>
        /// <param name="resourcesNamespace">The namespace of the resource to be generated.</param>
        /// <param name="codeProvider">A <see cref="T:System.CodeDom.Compiler.CodeDomProvider" /> object that provides the language in which the class will be generated.</param>
        /// <param name="internalClass">
        /// <see langword="true" /> to generate an internal class; <see langword="false" /> to generate a public class.</param>
        /// <param name="unmatchable">A <see cref="T:System.String" /> array that contains each resource name for which a property cannot be generated. Typically, a property cannot be generated because the resource name is not a valid identifier.</param>
        /// <returns>A <see cref="T:System.CodeDom.CodeCompileUnit" /> container.</returns>
        /// <exception cref="T:System.ArgumentNullException">
        /// <paramref name="resourceList" />, <paramref name="basename" />, or <paramref name="codeProvider" /> is <see langword="null" />.</exception>
        /// <exception cref="T:System.ArgumentException">A resource node name does not match its key in <paramref name="resourceList" />.</exception>
        public static CodeCompileUnit Create(
            IDictionary resourceList,
            string baseName,
            string generatedCodeNamespace,
            string resourcesNamespace,
            CodeDomProvider codeProvider,
            bool internalClass,
            out string[] unmatchable)
        {
            if (resourceList == null)
                throw new ArgumentNullException(nameof (resourceList));
            Dictionary<string, StronglyTypedResourceBuilder.ResourceData> resourceList1 = new Dictionary<string, StronglyTypedResourceBuilder.ResourceData>((IEqualityComparer<string>) StringComparer.InvariantCultureIgnoreCase);
            foreach (DictionaryEntry resource in resourceList)
            {
                StronglyTypedResourceBuilder.ResourceData resourceData;
                if (resource.Value is ResXDataNode resXdataNode)
                {
                    string key = (string) resource.Key;
                    if (key != resXdataNode.Name)
                        throw new ArgumentException(SR2.GetString("MismatchedResourceName", (object) key, (object) resXdataNode.Name));
                    resourceData = new StronglyTypedResourceBuilder.ResourceData(Type.GetType(resXdataNode.GetValueTypeName((AssemblyName[]) null)), resXdataNode.GetValue((AssemblyName[]) null).ToString());
                }
                else
                    resourceData = new StronglyTypedResourceBuilder.ResourceData(resource.Value == null ? typeof (object) : resource.Value.GetType(), resource.Value == null ? (string) null : resource.Value.ToString());
                resourceList1.Add((string) resource.Key, resourceData);
            }
            return StronglyTypedResourceBuilder.InternalCreate(resourceList1, baseName, generatedCodeNamespace, resourcesNamespace, codeProvider, internalClass, out unmatchable);
        }

        private static CodeCompileUnit InternalCreate(
            Dictionary<string, StronglyTypedResourceBuilder.ResourceData> resourceList,
            string baseName,
            string generatedCodeNamespace,
            string resourcesNamespace,
            CodeDomProvider codeProvider,
            bool internalClass,
            out string[] unmatchable)
        {
            if (baseName == null)
                throw new ArgumentNullException(nameof (baseName));
            if (codeProvider == null)
                throw new ArgumentNullException(nameof (codeProvider));
            ArrayList errors = new ArrayList(0);
            Hashtable reverseFixupTable;
            SortedList sortedList = StronglyTypedResourceBuilder.VerifyResourceNames(resourceList, codeProvider, errors, out reverseFixupTable);
            string str1 = baseName;
            if (!codeProvider.IsValidIdentifier(str1))
            {
                string str2 = StronglyTypedResourceBuilder.VerifyResourceName(str1, codeProvider);
                if (str2 != null)
                    str1 = str2;
            }
            if (!codeProvider.IsValidIdentifier(str1))
                throw new ArgumentException(SR2.GetString("InvalidIdentifier", (object) str1));
            if (!string.IsNullOrEmpty(generatedCodeNamespace) && !codeProvider.IsValidIdentifier(generatedCodeNamespace))
            {
                string str3 = StronglyTypedResourceBuilder.VerifyResourceName(generatedCodeNamespace, codeProvider, true);
                if (str3 != null)
                    generatedCodeNamespace = str3;
            }
            CodeCompileUnit e = new CodeCompileUnit();
            e.ReferencedAssemblies.Add("System.dll");
            e.UserData.Add((object) "AllowLateBound", (object) false);
            e.UserData.Add((object) "RequireVariableDeclaration", (object) true);
            CodeNamespace codeNamespace = new CodeNamespace(generatedCodeNamespace);
            codeNamespace.Imports.Add(new CodeNamespaceImport("System"));
            e.Namespaces.Add(codeNamespace);
            CodeTypeDeclaration codeTypeDeclaration = new CodeTypeDeclaration(str1);
            codeNamespace.Types.Add(codeTypeDeclaration);
            StronglyTypedResourceBuilder.AddGeneratedCodeAttributeforMember((CodeTypeMember) codeTypeDeclaration);
            TypeAttributes typeAttributes = internalClass ? TypeAttributes.NotPublic : TypeAttributes.Public;
            codeTypeDeclaration.TypeAttributes = typeAttributes;
            codeTypeDeclaration.Comments.Add(new CodeCommentStatement("<summary>", true));
            codeTypeDeclaration.Comments.Add(new CodeCommentStatement(SR2.GetString("ClassDocComment"), true));
            codeTypeDeclaration.Comments.Add(new CodeCommentStatement("</summary>", true));
            codeTypeDeclaration.CustomAttributes.Add(new CodeAttributeDeclaration(new CodeTypeReference(typeof (DebuggerNonUserCodeAttribute))
            {
                Options = CodeTypeReferenceOptions.GlobalReference
            }));
            codeTypeDeclaration.CustomAttributes.Add(new CodeAttributeDeclaration(new CodeTypeReference(typeof (CompilerGeneratedAttribute))
            {
                Options = CodeTypeReferenceOptions.GlobalReference
            }));
            bool useStatic = internalClass || codeProvider.Supports(GeneratorSupport.PublicStaticMembers);
            bool supportsTryCatch = codeProvider.Supports(GeneratorSupport.TryCatchStatements);
            bool useTypeInfo = codeProvider is ITargetAwareCodeDomProvider awareCodeDomProvider && !awareCodeDomProvider.SupportsProperty(typeof (Type), "Assembly", false);
            if (useTypeInfo)
                codeNamespace.Imports.Add(new CodeNamespaceImport("System.Reflection"));
            StronglyTypedResourceBuilder.EmitBasicClassMembers(codeTypeDeclaration, generatedCodeNamespace, baseName, resourcesNamespace, internalClass, useStatic, supportsTryCatch, useTypeInfo);
            foreach (DictionaryEntry dictionaryEntry in sortedList)
            {
                string key = (string) dictionaryEntry.Key;
                string resourceName = (string) reverseFixupTable[(object) key] ?? key;
                if (!StronglyTypedResourceBuilder.DefineResourceFetchingProperty(key, resourceName, (StronglyTypedResourceBuilder.ResourceData) dictionaryEntry.Value, codeTypeDeclaration, internalClass, useStatic))
                    errors.Add(dictionaryEntry.Key);
            }
            unmatchable = (string[]) errors.ToArray(typeof (string));
            CodeGenerator.ValidateIdentifiers((CodeObject) e);
            return e;
        }

        /// <summary>Generates a class file that contains strongly typed properties that match the resources in the specified .resx file.</summary>
        /// <param name="resxFile">The name of a .resx file used as input.</param>
        /// <param name="baseName">The name of the class to be generated.</param>
        /// <param name="generatedCodeNamespace">The namespace of the class to be generated.</param>
        /// <param name="codeProvider">A <see cref="T:System.CodeDom.Compiler.CodeDomProvider" /> class that provides the language in which the class will be generated.</param>
        /// <param name="internalClass">
        /// <see langword="true" /> to generate an internal class; <see langword="false" /> to generate a public class.</param>
        /// <param name="unmatchable">A <see cref="T:System.String" /> array that contains each resource name for which a property cannot be generated. Typically, a property cannot be generated because the resource name is not a valid identifier.</param>
        /// <returns>A <see cref="T:System.CodeDom.CodeCompileUnit" /> container.</returns>
        /// <exception cref="T:System.ArgumentNullException">
        /// <paramref name="basename" /> or <paramref name="codeProvider" /> is <see langword="null" />.</exception>
        public static CodeCompileUnit Create(
            string resxFile,
            string baseName,
            string generatedCodeNamespace,
            CodeDomProvider codeProvider,
            bool internalClass,
            out string[] unmatchable)
        {
            return StronglyTypedResourceBuilder.Create(resxFile, baseName, generatedCodeNamespace, (string) null, codeProvider, internalClass, out unmatchable);
        }

        /// <summary>Generates a class file that contains strongly typed properties that match the resources in the specified .resx file.</summary>
        /// <param name="resxFile">The name of a .resx file used as input.</param>
        /// <param name="baseName">The name of the class to be generated.</param>
        /// <param name="generatedCodeNamespace">The namespace of the class to be generated.</param>
        /// <param name="resourcesNamespace">The namespace of the resource to be generated.</param>
        /// <param name="codeProvider">A <see cref="T:System.CodeDom.Compiler.CodeDomProvider" /> class that provides the language in which the class will be generated.</param>
        /// <param name="internalClass">
        /// <see langword="true" /> to generate an internal class; <see langword="false" /> to generate a public class.</param>
        /// <param name="unmatchable">A <see cref="T:System.String" /> array that contains each resource name for which a property cannot be generated. Typically, a property cannot be generated because the resource name is not a valid identifier.</param>
        /// <returns>A <see cref="T:System.CodeDom.CodeCompileUnit" /> container.</returns>
        /// <exception cref="T:System.ArgumentNullException">
        /// <paramref name="basename" /> or <paramref name="codeProvider" /> is <see langword="null" />.</exception>
        public static CodeCompileUnit Create(
            string resxFile,
            string baseName,
            string generatedCodeNamespace,
            string resourcesNamespace,
            CodeDomProvider codeProvider,
            bool internalClass,
            out string[] unmatchable)
        {
            if (resxFile == null)
                throw new ArgumentNullException(nameof (resxFile));
            Dictionary<string, StronglyTypedResourceBuilder.ResourceData> resourceList = new Dictionary<string, StronglyTypedResourceBuilder.ResourceData>((IEqualityComparer<string>) StringComparer.InvariantCultureIgnoreCase);
            using (ResXResourceReader resXresourceReader = new ResXResourceReader(resxFile))
            {
                resXresourceReader.UseResXDataNodes = true;
                foreach (DictionaryEntry dictionaryEntry in resXresourceReader)
                {
                    ResXDataNode resXdataNode = (ResXDataNode) dictionaryEntry.Value;
                    StronglyTypedResourceBuilder.ResourceData resourceData = new StronglyTypedResourceBuilder.ResourceData(Type.GetType(resXdataNode.GetValueTypeName((AssemblyName[]) null)), resXdataNode.GetValue((AssemblyName[]) null).ToString());
                    resourceList.Add((string) dictionaryEntry.Key, resourceData);
                }
            }
            return StronglyTypedResourceBuilder.InternalCreate(resourceList, baseName, generatedCodeNamespace, resourcesNamespace, codeProvider, internalClass, out unmatchable);
        }

        private static void AddGeneratedCodeAttributeforMember(CodeTypeMember typeMember)
        {
            CodeAttributeDeclaration attributeDeclaration = new CodeAttributeDeclaration(new CodeTypeReference(typeof (GeneratedCodeAttribute)));
            attributeDeclaration.AttributeType.Options = CodeTypeReferenceOptions.GlobalReference;
            CodeAttributeArgument attributeArgument1 = new CodeAttributeArgument((CodeExpression) new CodePrimitiveExpression((object) typeof (StronglyTypedResourceBuilder).FullName));
            CodeAttributeArgument attributeArgument2 = new CodeAttributeArgument((CodeExpression) new CodePrimitiveExpression((object) "4.0.0.0"));
            attributeDeclaration.Arguments.Add(attributeArgument1);
            attributeDeclaration.Arguments.Add(attributeArgument2);
            typeMember.CustomAttributes.Add(attributeDeclaration);
        }

        private static void EmitBasicClassMembers(
            CodeTypeDeclaration srClass,
            string nameSpace,
            string baseName,
            string resourcesNamespace,
            bool internalClass,
            bool useStatic,
            bool supportsTryCatch,
            bool useTypeInfo)
        {
            string str = resourcesNamespace == null ? (nameSpace == null || nameSpace.Length <= 0 ? baseName : nameSpace + "." + baseName) : (resourcesNamespace.Length <= 0 ? baseName : resourcesNamespace + "." + baseName);
            CodeCommentStatement commentStatement1 = new CodeCommentStatement(SR2.GetString("ClassComments1"));
            srClass.Comments.Add(commentStatement1);
            CodeCommentStatement commentStatement2 = new CodeCommentStatement(SR2.GetString("ClassComments2"));
            srClass.Comments.Add(commentStatement2);
            CodeCommentStatement commentStatement3 = new CodeCommentStatement(SR2.GetString("ClassComments3"));
            srClass.Comments.Add(commentStatement3);
            CodeCommentStatement commentStatement4 = new CodeCommentStatement(SR2.GetString("ClassComments4"));
            srClass.Comments.Add(commentStatement4);
            CodeAttributeDeclaration attributeDeclaration1 = new CodeAttributeDeclaration(new CodeTypeReference(typeof (SuppressMessageAttribute)));
            attributeDeclaration1.AttributeType.Options = CodeTypeReferenceOptions.GlobalReference;
            attributeDeclaration1.Arguments.Add(new CodeAttributeArgument((CodeExpression) new CodePrimitiveExpression((object) "Microsoft.Performance")));
            attributeDeclaration1.Arguments.Add(new CodeAttributeArgument((CodeExpression) new CodePrimitiveExpression((object) "CA1811:AvoidUncalledPrivateCode")));
            CodeConstructor codeConstructor = new CodeConstructor();
            codeConstructor.CustomAttributes.Add(attributeDeclaration1);
            if (useStatic | internalClass)
                codeConstructor.Attributes = MemberAttributes.FamilyAndAssembly;
            else
                codeConstructor.Attributes = MemberAttributes.Public;
            srClass.Members.Add((CodeTypeMember) codeConstructor);
            CodeTypeReference codeTypeReference = new CodeTypeReference(typeof (ResourceManager), CodeTypeReferenceOptions.GlobalReference);
            CodeMemberField codeMemberField1 = new CodeMemberField(codeTypeReference, "resourceMan");
            codeMemberField1.Attributes = MemberAttributes.Private;
            if (useStatic)
                codeMemberField1.Attributes |= MemberAttributes.Static;
            srClass.Members.Add((CodeTypeMember) codeMemberField1);
            CodeTypeReference type = new CodeTypeReference(typeof (CultureInfo), CodeTypeReferenceOptions.GlobalReference);
            CodeMemberField codeMemberField2 = new CodeMemberField(type, "resourceCulture");
            codeMemberField2.Attributes = MemberAttributes.Private;
            if (useStatic)
                codeMemberField2.Attributes |= MemberAttributes.Static;
            srClass.Members.Add((CodeTypeMember) codeMemberField2);
            CodeMemberProperty codeMemberProperty1 = new CodeMemberProperty();
            srClass.Members.Add((CodeTypeMember) codeMemberProperty1);
            codeMemberProperty1.Name = "ResourceManager";
            codeMemberProperty1.HasGet = true;
            codeMemberProperty1.HasSet = false;
            codeMemberProperty1.Type = codeTypeReference;
            if (internalClass)
                codeMemberProperty1.Attributes = MemberAttributes.Assembly;
            else
                codeMemberProperty1.Attributes = MemberAttributes.Public;
            if (useStatic)
                codeMemberProperty1.Attributes |= MemberAttributes.Static;
            CodeAttributeDeclaration attributeDeclaration2 = new CodeAttributeDeclaration("System.ComponentModel.EditorBrowsableAttribute", new CodeAttributeArgument[1]
            {
                new CodeAttributeArgument((CodeExpression) new CodeFieldReferenceExpression((CodeExpression) new CodeTypeReferenceExpression(new CodeTypeReference(typeof (EditorBrowsableState))
                {
                    Options = CodeTypeReferenceOptions.GlobalReference
                }), "Advanced"))
            });
            attributeDeclaration2.AttributeType.Options = CodeTypeReferenceOptions.GlobalReference;
            codeMemberProperty1.CustomAttributes.Add(attributeDeclaration2);
            CodeMemberProperty codeMemberProperty2 = new CodeMemberProperty();
            srClass.Members.Add((CodeTypeMember) codeMemberProperty2);
            codeMemberProperty2.Name = "Culture";
            codeMemberProperty2.HasGet = true;
            codeMemberProperty2.HasSet = true;
            codeMemberProperty2.Type = type;
            if (internalClass)
                codeMemberProperty2.Attributes = MemberAttributes.Assembly;
            else
                codeMemberProperty2.Attributes = MemberAttributes.Public;
            if (useStatic)
                codeMemberProperty2.Attributes |= MemberAttributes.Static;
            codeMemberProperty2.CustomAttributes.Add(attributeDeclaration2);
            CodeFieldReferenceExpression referenceExpression1 = new CodeFieldReferenceExpression((CodeExpression) null, "resourceMan");
            CodeMethodInvokeExpression condition = new CodeMethodInvokeExpression(new CodeMethodReferenceExpression((CodeExpression) new CodeTypeReferenceExpression(typeof (object)), "ReferenceEquals"), new CodeExpression[2]
            {
                (CodeExpression) referenceExpression1,
                (CodeExpression) new CodePrimitiveExpression((object) null)
            });
            CodePropertyReferenceExpression referenceExpression2 = !useTypeInfo ? new CodePropertyReferenceExpression((CodeExpression) new CodeTypeOfExpression(new CodeTypeReference(srClass.Name)), "Assembly") : new CodePropertyReferenceExpression((CodeExpression) new CodeMethodInvokeExpression((CodeExpression) new CodeTypeOfExpression(new CodeTypeReference(srClass.Name)), "GetTypeInfo", new CodeExpression[0]), "Assembly");
            CodeObjectCreateExpression initExpression = new CodeObjectCreateExpression(codeTypeReference, new CodeExpression[2]
            {
                (CodeExpression) new CodePrimitiveExpression((object) str),
                (CodeExpression) referenceExpression2
            });
            CodeStatement[] codeStatementArray = new CodeStatement[2]
            {
                (CodeStatement) new CodeVariableDeclarationStatement(codeTypeReference, "temp", (CodeExpression) initExpression),
                (CodeStatement) new CodeAssignStatement((CodeExpression) referenceExpression1, (CodeExpression) new CodeVariableReferenceExpression("temp"))
            };
            codeMemberProperty1.GetStatements.Add((CodeStatement) new CodeConditionStatement((CodeExpression) condition, codeStatementArray));
            codeMemberProperty1.GetStatements.Add((CodeStatement) new CodeMethodReturnStatement((CodeExpression) referenceExpression1));
            codeMemberProperty1.Comments.Add(new CodeCommentStatement("<summary>", true));
            codeMemberProperty1.Comments.Add(new CodeCommentStatement(SR2.GetString("ResMgrPropertyComment"), true));
            codeMemberProperty1.Comments.Add(new CodeCommentStatement("</summary>", true));
            CodeFieldReferenceExpression referenceExpression3 = new CodeFieldReferenceExpression((CodeExpression) null, "resourceCulture");
            codeMemberProperty2.GetStatements.Add((CodeStatement) new CodeMethodReturnStatement((CodeExpression) referenceExpression3));
            CodePropertySetValueReferenceExpression right = new CodePropertySetValueReferenceExpression();
            codeMemberProperty2.SetStatements.Add((CodeStatement) new CodeAssignStatement((CodeExpression) referenceExpression3, (CodeExpression) right));
            codeMemberProperty2.Comments.Add(new CodeCommentStatement("<summary>", true));
            codeMemberProperty2.Comments.Add(new CodeCommentStatement(SR2.GetString("CulturePropertyComment1"), true));
            codeMemberProperty2.Comments.Add(new CodeCommentStatement(SR2.GetString("CulturePropertyComment2"), true));
            codeMemberProperty2.Comments.Add(new CodeCommentStatement("</summary>", true));
        }

        private static string TruncateAndFormatCommentStringForOutput(string commentString)
        {
            if (commentString != null)
            {
                if (commentString.Length > 512)
                    commentString = SR2.GetString("StringPropertyTruncatedComment", (object) commentString.Substring(0, 512));
                commentString = SecurityElement.Escape(commentString);
            }
            return commentString;
        }

        private static bool DefineResourceFetchingProperty(
            string propertyName,
            string resourceName,
            StronglyTypedResourceBuilder.ResourceData data,
            CodeTypeDeclaration srClass,
            bool internalClass,
            bool useStatic)
        {
            CodeMemberProperty codeMemberProperty = new CodeMemberProperty();
            codeMemberProperty.Name = propertyName;
            codeMemberProperty.HasGet = true;
            codeMemberProperty.HasSet = false;
            Type type = data.Type;
            if (type == (Type) null)
                return false;
            if (type == typeof (MemoryStream))
                type = typeof (UnmanagedMemoryStream);
            while (!type.IsPublic)
                type = type.BaseType;
            CodeTypeReference targetType = new CodeTypeReference(type);
            codeMemberProperty.Type = targetType;
            if (internalClass)
                codeMemberProperty.Attributes = MemberAttributes.Assembly;
            else
                codeMemberProperty.Attributes = MemberAttributes.Public;
            if (useStatic)
                codeMemberProperty.Attributes |= MemberAttributes.Static;
            CodePropertyReferenceExpression targetObject = new CodePropertyReferenceExpression((CodeExpression) null, "ResourceManager");
            CodeFieldReferenceExpression referenceExpression = new CodeFieldReferenceExpression(useStatic ? (CodeExpression) null : (CodeExpression) new CodeThisReferenceExpression(), "resourceCulture");
            bool flag1 = type == typeof (string);
            bool flag2 = type == typeof (UnmanagedMemoryStream) || type == typeof (MemoryStream);
            string empty1 = string.Empty;
            string empty2 = string.Empty;
            string b = StronglyTypedResourceBuilder.TruncateAndFormatCommentStringForOutput(data.ValueAsString);
            string a = string.Empty;
            if (!flag1)
                a = StronglyTypedResourceBuilder.TruncateAndFormatCommentStringForOutput(type.ToString());
            string methodName = !flag1 ? (!flag2 ? "GetObject" : "GetStream") : "GetString";
            string text;
            if (flag1)
                text = SR2.GetString("StringPropertyComment", (object) b);
            else if (b == null || string.Equals(a, b))
                text = SR2.GetString("NonStringPropertyComment", (object) a);
            else
                text = SR2.GetString("NonStringPropertyDetailedComment", (object) a, (object) b);
            codeMemberProperty.Comments.Add(new CodeCommentStatement("<summary>", true));
            codeMemberProperty.Comments.Add(new CodeCommentStatement(text, true));
            codeMemberProperty.Comments.Add(new CodeCommentStatement("</summary>", true));
            CodeExpression codeExpression = (CodeExpression) new CodeMethodInvokeExpression((CodeExpression) targetObject, methodName, new CodeExpression[2]
            {
                (CodeExpression) new CodePrimitiveExpression((object) resourceName),
                (CodeExpression) referenceExpression
            });
            CodeMethodReturnStatement methodReturnStatement;
            if (flag1 | flag2)
            {
                methodReturnStatement = new CodeMethodReturnStatement(codeExpression);
            }
            else
            {
                CodeVariableDeclarationStatement declarationStatement = new CodeVariableDeclarationStatement(typeof (object), "obj", codeExpression);
                codeMemberProperty.GetStatements.Add((CodeStatement) declarationStatement);
                methodReturnStatement = new CodeMethodReturnStatement((CodeExpression) new CodeCastExpression(targetType, (CodeExpression) new CodeVariableReferenceExpression("obj")));
            }
            codeMemberProperty.GetStatements.Add((CodeStatement) methodReturnStatement);
            srClass.Members.Add((CodeTypeMember) codeMemberProperty);
            return true;
        }

        /// <summary>Generates a valid resource string based on the specified input string and code provider.</summary>
        /// <param name="key">The string to verify and, if necessary, convert to a valid resource name.</param>
        /// <param name="provider">A <see cref="T:System.CodeDom.Compiler.CodeDomProvider" /> object that specifies the target language to use.</param>
        /// <returns>A valid resource name derived from the <paramref name="key" /> parameter. Any invalid tokens are replaced with the underscore (_) character, or <see langword="null" /> if the derived string still contains invalid characters according to the language specified by the <paramref name="provider" /> parameter.</returns>
        /// <exception cref="T:System.ArgumentNullException">
        /// <paramref name="key" /> or <paramref name="provider" /> is <see langword="null" />.</exception>
        public static string VerifyResourceName(string key, CodeDomProvider provider)
        {
            return StronglyTypedResourceBuilder.VerifyResourceName(key, provider, false);
        }

        private static string VerifyResourceName(
            string key,
            CodeDomProvider provider,
            bool isNameSpace)
        {
            if (key == null)
                throw new ArgumentNullException(nameof (key));
            if (provider == null)
                throw new ArgumentNullException(nameof (provider));
            foreach (char oldChar in StronglyTypedResourceBuilder.CharsToReplace)
            {
                if (!isNameSpace || oldChar != '.' && oldChar != ':')
                    key = key.Replace(oldChar, '_');
            }
            if (provider.IsValidIdentifier(key))
                return key;
            key = provider.CreateValidIdentifier(key);
            if (provider.IsValidIdentifier(key))
                return key;
            key = "_" + key;
            return provider.IsValidIdentifier(key) ? key : (string) null;
        }

        private static SortedList VerifyResourceNames(
            Dictionary<string, StronglyTypedResourceBuilder.ResourceData> resourceList,
            CodeDomProvider codeProvider,
            ArrayList errors,
            out Hashtable reverseFixupTable)
        {
            reverseFixupTable = new Hashtable(0, (IEqualityComparer) StringComparer.InvariantCultureIgnoreCase);
            SortedList sortedList = new SortedList((IComparer) StringComparer.InvariantCultureIgnoreCase, resourceList.Count);
            foreach (KeyValuePair<string, StronglyTypedResourceBuilder.ResourceData> resource in resourceList)
            {
                string str1 = resource.Key;
                if (string.Equals(str1, "ResourceManager") || string.Equals(str1, "Culture") || typeof (void) == resource.Value.Type)
                    errors.Add((object) str1);
                else if ((str1.Length <= 0 || str1[0] != '$') && (str1.Length <= 1 || str1[0] != '>' || str1[1] != '>'))
                {
                    if (!codeProvider.IsValidIdentifier(str1))
                    {
                        string key = StronglyTypedResourceBuilder.VerifyResourceName(str1, codeProvider, false);
                        if (key == null)
                        {
                            errors.Add((object) str1);
                            continue;
                        }
                        string str2 = (string) reverseFixupTable[(object) key];
                        if (str2 != null)
                        {
                            if (!errors.Contains((object) str2))
                                errors.Add((object) str2);
                            if (sortedList.Contains((object) key))
                                sortedList.Remove((object) key);
                            errors.Add((object) str1);
                            continue;
                        }
                        reverseFixupTable[(object) key] = (object) str1;
                        str1 = key;
                    }
                    StronglyTypedResourceBuilder.ResourceData resourceData = resource.Value;
                    if (!sortedList.Contains((object) str1))
                    {
                        sortedList.Add((object) str1, (object) resourceData);
                    }
                    else
                    {
                        string str3 = (string) reverseFixupTable[(object) str1];
                        if (str3 != null)
                        {
                            if (!errors.Contains((object) str3))
                                errors.Add((object) str3);
                            reverseFixupTable.Remove((object) str1);
                        }
                        errors.Add((object) resource.Key);
                        sortedList.Remove((object) str1);
                    }
                }
            }
            return sortedList;
        }

        internal sealed class ResourceData
        {
            private Type _type;
            private string _valueAsString;

            internal ResourceData(Type type, string valueAsString)
            {
                this._type = type;
                this._valueAsString = valueAsString;
            }

            internal Type Type => this._type;

            internal string ValueAsString => this._valueAsString;
        }
    }
}
