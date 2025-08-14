namespace Lql.TypeProvider.FSharp

open System
open System.Collections.Generic
open System.Reflection
open Microsoft.FSharp.Core.CompilerServices
open Microsoft.FSharp.Quotations
open Microsoft.Data.Sqlite
open Lql
open Lql.SQLite
open Results

/// <summary>
/// Minimal Type Provider interface implementation for LQL
/// This uses the EXACT same pattern as FSharp.Data.SqlClient
/// </summary>
[<TypeProvider>]
type LqlTypeProvider(config: TypeProviderConfig) =
    let namespaceName = "Lql"
    let thisAssembly = Assembly.GetExecutingAssembly()
    let createRootType() =
        let t = ProvidedType(namespaceName, "LqlCommand", thisAssembly)
        t.DefineStaticParameters(
            [ProvidedStaticParameter("Query", typeof<string>)],
            fun typeName [| :? string as lqlQuery |] ->
                
                // *** THIS IS THE CRITICAL PART - COMPILE-TIME VALIDATION ***
                // Following EXACT SqlClient pattern with failwith
                if String.IsNullOrWhiteSpace lqlQuery then 
                    invalidArg "Query" "LQL query cannot be null or empty!"
                
                let result = LqlStatementConverter.ToStatement lqlQuery
                match result with
                | :? Results.Result<LqlStatement, SqlError>.Success as success ->
                    // Valid LQL - convert to SQL
                    let sqlResult = success.Value.ToSQLite()
                    match sqlResult with
                    | :? Results.Result<string, SqlError>.Success as sqlSuccess ->
                        let sql = sqlSuccess.Value
                        createValidatedType(typeName, lqlQuery, sql)
                    | :? Results.Result<string, SqlError>.Failure as sqlFailure ->
                        // THIS CAUSES F# COMPILATION TO FAIL
                        failwith $"❌ COMPILATION FAILED: SQL generation error - {sqlFailure.ErrorValue.Message} for LQL: '{lqlQuery}'"
                    | _ -> 
                        failwith $"❌ COMPILATION FAILED: Unknown SQL generation error for LQL: '{lqlQuery}'"
                | :? Results.Result<LqlStatement, SqlError>.Failure as failure ->
                    let error = failure.ErrorValue
                    let position = 
                        match error.Position with
                        | null -> ""
                        | pos -> $" at line {pos.Line}, column {pos.Column}"
                    // THIS CAUSES F# COMPILATION TO FAIL - EXACTLY LIKE SQLCLIENT
                    failwith $"❌ COMPILATION FAILED: Invalid LQL syntax - {error.Message}{position} in query: '{lqlQuery}'"
                | _ -> 
                    failwith $"❌ COMPILATION FAILED: Unknown LQL parsing error in query: '{lqlQuery}'"
        )
        t
    
    let createValidatedType(typeName: string, lqlQuery: string, sql: string) =
        let t = ProvidedType(namespaceName, typeName, thisAssembly)
        
        // Add Query property
        let queryProp = ProvidedProperty("Query", typeof<string>, getterCode = fun _ -> <@@ lqlQuery @@>)
        queryProp.AddXmlDoc($"The validated LQL query: {lqlQuery}")
        t.AddMember(queryProp)
        
        // Add Sql property
        let sqlProp = ProvidedProperty("Sql", typeof<string>, getterCode = fun _ -> <@@ sql @@>)
        sqlProp.AddXmlDoc($"The generated SQL: {sql}")
        t.AddMember(sqlProp)
        
        // Add Execute method
        let executeMethod = ProvidedMethod("Execute", 
            [ProvidedParameter("connectionString", typeof<string>)], 
            typeof<Result<ResizeArray<Map<string, obj>>, string>>,
            invokeCode = fun args -> 
                <@@
                    try
                        let connectionString = %%args.[0] : string
                        let results = ResizeArray<Map<string, obj>>()
                        use conn = new SqliteConnection(connectionString)
                        conn.Open()
                        use cmd = new SqliteCommand(sql, conn)
                        use reader = cmd.ExecuteReader()
                        
                        while reader.Read() do
                            let row = 
                                [| for i in 0 .. reader.FieldCount - 1 ->
                                    let name = reader.GetName(i)
                                    let value = if reader.IsDBNull(i) then null else reader.GetValue(i)
                                    (name, value) |]
                                |> Map.ofArray
                            results.Add(row)
                        
                        Ok results
                    with ex ->
                        Error ex.Message
                @@>)
        executeMethod.AddXmlDoc("Execute this compile-time validated LQL query")
        t.AddMember(executeMethod)
        
        t.AddXmlDoc($"✅ Compile-time validated LQL: '{lqlQuery}' → SQL: '{sql}'")
        t
    
    interface ITypeProvider with
        member this.GetNamespaces() = 
            [| ProvidedNamespace(namespaceName, [createRootType()]) |]
        
        member this.GetStaticParameters(typeWithoutArguments) = 
            typeWithoutArguments.GetStaticParameters()
            
        member this.ApplyStaticArguments(typeWithoutArguments, typeNameWithArguments, staticArguments) = 
            typeWithoutArguments.ApplyStaticArguments(typeNameWithArguments, staticArguments)
            
        member this.GetInvokerExpression(syntheticMethodBase, parameters) = 
            failwith "Not implemented for erased types"
            
        member this.Dispose() = ()
        
        [<CLIEvent>]
        member this.Invalidate = 
            let e = Event<EventHandler, EventArgs>()
            e.Publish

/// <summary>
/// Minimal ProvidedType implementation
/// </summary>
and ProvidedType(namespaceName: string, typeName: string, assembly: Assembly) =
    inherit Type()
    
    let mutable staticParams: ParameterInfo[] = [||]
    let mutable staticParamsApplier: (string -> obj[] -> Type) option = None
    let mutable members: MemberInfo list = []
    let mutable xmlDoc: string = ""
    
    member this.DefineStaticParameters(parameters: ProvidedStaticParameter[], applier: string -> obj[] -> Type) =
        staticParams <- parameters |> Array.map (fun p -> p :> ParameterInfo)
        staticParamsApplier <- Some applier
    
    member this.GetStaticParameters() = staticParams
    
    member this.ApplyStaticArguments(typeNameWithArguments: string, staticArguments: obj[]) =
        match staticParamsApplier with
        | Some applier -> applier typeNameWithArguments staticArguments
        | None -> failwith "No static parameter applier defined"
    
    member this.AddMember(memberInfo: MemberInfo) =
        members <- memberInfo :: members
    
    member this.AddXmlDoc(doc: string) =
        xmlDoc <- doc
    
    override this.Name = typeName
    override this.FullName = $"{namespaceName}.{typeName}"
    override this.Assembly = assembly
    override this.Namespace = namespaceName
    override this.BaseType = typeof<obj>
    override this.UnderlyingSystemType = this
    override this.IsGenericType = false
    override this.IsGenericTypeDefinition = false
    override this.GetGenericArguments() = [||]
    override this.GetCustomAttributes(inherit') = [||]
    override this.GetCustomAttributes(attributeType, inherit') = [||]
    override this.IsDefined(attributeType, inherit') = false
    override this.GetMembers(bindingAttr) = members |> List.toArray
    override this.GetMethods(bindingAttr) = [||]
    override this.GetProperties(bindingAttr) = [||]
    override this.GetFields(bindingAttr) = [||]
    override this.GetEvents(bindingAttr) = [||]
    override this.GetNestedTypes(bindingAttr) = [||]
    override this.GetConstructors(bindingAttr) = [||]
    override this.GetInterfaces() = [||]

/// <summary>
/// Minimal ProvidedNamespace implementation
/// </summary>
and ProvidedNamespace(namespaceName: string, types: Type[]) =
    interface IProvidedNamespace with
        member this.NamespaceName = namespaceName
        member this.GetTypes() = types
        member this.ResolveTypeName(typeName) = 
            types |> Array.tryFind (fun t -> t.Name = typeName)

/// <summary>
/// Minimal ProvidedStaticParameter implementation
/// </summary>
and ProvidedStaticParameter(name: string, parameterType: Type) =
    inherit ParameterInfo()
    override this.Name = name
    override this.ParameterType = parameterType
    override this.DefaultValue = null

/// <summary>
/// Minimal ProvidedProperty implementation
/// </summary>
and ProvidedProperty(propertyName: string, propertyType: Type, ?getterCode: Expr list -> Expr) =
    inherit PropertyInfo()
    let mutable xmlDoc = ""
    override this.Name = propertyName
    override this.PropertyType = propertyType
    override this.CanRead = getterCode.IsSome
    override this.CanWrite = false
    override this.GetIndexParameters() = [||]
    override this.GetValue(obj, invokeAttr, binder, index, culture) = failwith "Not implemented"
    override this.SetValue(obj, value, invokeAttr, binder, index, culture) = failwith "Not implemented"
    override this.GetAccessors(nonPublic) = [||]
    override this.GetGetMethod(nonPublic) = null
    override this.GetSetMethod(nonPublic) = null
    override this.Attributes = PropertyAttributes.None
    override this.DeclaringType = null
    override this.ReflectedType = null
    override this.GetCustomAttributes(inherit') = [||]
    override this.GetCustomAttributes(attributeType, inherit') = [||]
    override this.IsDefined(attributeType, inherit') = false
    member this.AddXmlDoc(doc: string) = xmlDoc <- doc

/// <summary>
/// Minimal ProvidedMethod implementation
/// </summary>
and ProvidedMethod(methodName: string, parameters: ProvidedParameter[], returnType: Type, ?invokeCode: Expr list -> Expr) =
    inherit MethodInfo()
    let mutable xmlDoc = ""
    override this.Name = methodName
    override this.ReturnType = returnType
    override this.GetParameters() = parameters |> Array.map (fun p -> p :> ParameterInfo)
    override this.Invoke(obj, invokeAttr, binder, parameters, culture) = failwith "Not implemented"
    override this.Attributes = MethodAttributes.Public ||| MethodAttributes.Static
    override this.CallingConvention = CallingConventions.Standard
    override this.DeclaringType = null
    override this.ReflectedType = null
    override this.MethodHandle = RuntimeMethodHandle()
    override this.GetCustomAttributes(inherit') = [||]
    override this.GetCustomAttributes(attributeType, inherit') = [||]
    override this.IsDefined(attributeType, inherit') = false
    override this.GetBaseDefinition() = this
    override this.GetMethodImplementationFlags() = MethodImplAttributes.IL
    member this.AddXmlDoc(doc: string) = xmlDoc <- doc

/// <summary>
/// Minimal ProvidedParameter implementation
/// </summary>
and ProvidedParameter(parameterName: string, parameterType: Type) =
    inherit ParameterInfo()
    override this.Name = parameterName
    override this.ParameterType = parameterType
    override this.DefaultValue = null

[<assembly: TypeProviderAssembly>]
do ()