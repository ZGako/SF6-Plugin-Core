namespace SF6_Plugin_Core.Memory;

/// <summary>
///     Provides type-checked RE Engine memory marshaling, field copying, GUID field writes, and closed generic
///     collection instantiation over REFramework <see cref="ManagedObject" /> pointers.
/// </summary>
public static class MemoryHelpers
{
    /// <summary>
    ///     Resolves a raw native/VM pointer address to a <typeparamref name="T" /> proxy after verifying TDB
    ///     inheritance compatibility.
    /// </summary>
    /// <typeparam name="T">The expected REFramework-generated interface or proxy class (must expose <c>REFType</c>).</typeparam>
    /// <param name="address">The raw unmanaged address of an RE Engine managed object (for example, a <c>[MethodHook]</c> argument slot).</param>
    /// <returns>A typed proxy wrapping the <see cref="ManagedObject" /> at <paramref name="address" />.</returns>
    /// <exception cref="ArgumentException">
    ///     Thrown when <paramref name="address" /> does not resolve to a valid RE Engine <see cref="ManagedObject" />.
    /// </exception>
    /// <exception cref="InvalidCastException">
    ///     Thrown when the runtime TDB type at <paramref name="address" /> does not derive from <typeparamref name="T" />,
    ///     or when <see cref="ManagedObject.As{T}" /> fails to construct a proxy.
    /// </exception>
    public static T GetAddressAs<T>(ulong address) where T : class
    {
        var managedObj = ManagedObject.ToManagedObject(address)
                         ?? throw new ArgumentException(
                             $"Failed to convert pointer address {address} to a ManagedObject.");

        var actualType = managedObj.GetTypeDefinition();

        // FAST PATH: Reads the instantly accessible cached variable from our nested class
        var requestedType = TypeDefCache<T>.RequestedType;

        if (actualType != null && requestedType != null)
            if (!actualType.IsDerivedFrom(requestedType))
                throw new InvalidCastException(
                    $"Strict Cast Failed: Object at address {address} is of type '{actualType.FullName}', " +
                    $"which does not match or derive from the requested type '{requestedType.FullName}'.");

        // API.LogWarning($"Type definitions missing for strict validation on {address}. Proceeding with blind cast.");
        return managedObj.As<T>() ??
               throw new InvalidCastException($"Proxy creation failed for object at address {address}.");
    }

    /// <summary>
    ///     Casts an object (such as the return value of a dynamic VM <c>Call</c>) to a <typeparamref name="T" /> proxy
    ///     after validating its TDB type hierarchy.
    /// </summary>
    /// <typeparam name="T">The expected REFramework-generated interface or proxy class (must expose <c>REFType</c>).</typeparam>
    /// <param name="obj">A boxed <see cref="ManagedObject" /> obtained from REFramework APIs.</param>
    /// <param name="exceptionMessage">Optional custom message used when throwing on a null or incompatible object.</param>
    /// <returns>A typed proxy wrapping <paramref name="obj" />.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="obj" /> is <see langword="null" /> or not a <see cref="ManagedObject" />.</exception>
    /// <exception cref="InvalidCastException">
    ///     Thrown when the object's TDB type does not derive from <typeparamref name="T" />, or proxy creation fails.
    /// </exception>
    public static T GetObjectAs<T>(this object obj, string? exceptionMessage = null) where T : class
    {
        var managedObj = obj as ManagedObject ??
                         throw new ArgumentException(exceptionMessage ?? "Object is not a ManagedObject or is null.");
        var actualType = managedObj.GetTypeDefinition();

        // FAST PATH: Reads the instantly accessible cached variable from our nested class
        var requestedType = TypeDefCache<T>.RequestedType;

        if (actualType != null && requestedType != null)
            if (!actualType.IsDerivedFrom(requestedType))
                throw new InvalidCastException(exceptionMessage ??
                                               $"Strict Cast Failed: Object at address {managedObj.GetAddress()} is of type '{actualType.FullName}', " +
                                               $"which does not match or derive from the requested type '{requestedType.FullName}'.");

        // API.LogWarning($"Type definitions missing for strict validation on {address}. Proceeding with blind cast.");
        return managedObj.As<T>() ?? throw new InvalidCastException(exceptionMessage ??
                                                                    $"Proxy creation failed for object at address {managedObj.GetAddress()}.");
    }

    private static int GetValueTypeFieldSize(TypeDefinition fieldType)
    {
        if (fieldType.IsEnum())
            fieldType = fieldType.UnderlyingType ?? fieldType;

        return fieldType.FullName switch
        {
            "System.Boolean" or "System.Byte" or "System.SByte" => 1,
            "System.Int16" or "System.UInt16" or "System.Char" => 2,
            "System.Int32" or "System.UInt32" or "System.Single" => 4,
            "System.Int64" or "System.UInt64" or "System.Double" or "System.IntPtr" or "System.UIntPtr" => 8,
            _ => (int)fieldType.ValueTypeSize
        };
    }

    /// <summary>
    ///     Shallow-copies all non-static TDB fields defined on <paramref name="typeDef" /> from
    ///     <paramref name="source" /> to <paramref name="target" />, no-oping when both point to zero or identical
    ///     addresses.
    /// </summary>
    /// <remarks>
    ///     Value-type fields are blitted via raw memory copy using TDB primitive/<c>ValueTypeSize</c> metadata, whereas
    ///     reference fields copy only the pointer (clearing to <see langword="null" /> if the source reference is null).
    /// </remarks>
    /// <param name="target">The destination RE Engine instance receiving the copied field values.</param>
    /// <param name="source">The source RE Engine instance whose fields are read.</param>
    /// <param name="typeDef">The TDB <see cref="TypeDefinition" /> enumerating the fields to copy.</param>
    public static void CopyFieldsFrom(this ManagedObject target, ManagedObject source, TypeDefinition typeDef)
    {
        var sourceAddr = source.GetAddress();
        var targetAddr = target.GetAddress();
        if (sourceAddr == 0 || targetAddr == 0 || sourceAddr == targetAddr)
            return;

        foreach (var field in typeDef.GetFields())
        {
            if (field.IsStatic()) continue;
            try
            {
                var srcFieldPtr = field.GetDataRaw(sourceAddr, false);
                var dstFieldPtr = field.GetDataRaw(targetAddr, false);
                if (srcFieldPtr == 0 || dstFieldPtr == 0)
                    continue;

                if (field.Type.IsValueType())
                {
                    var size = GetValueTypeFieldSize(field.Type);
                    if (size <= 0)
                        continue;

                    var buffer = new byte[size];
                    Marshal.Copy((IntPtr)srcFieldPtr, buffer, 0, size);
                    Marshal.Copy(buffer, 0, (IntPtr)dstFieldPtr, size);
                }
                else
                {
                    var srcObjPtr = Marshal.ReadIntPtr((IntPtr)srcFieldPtr);
                    var dstObjPtr = Marshal.ReadIntPtr((IntPtr)dstFieldPtr);
                    if (srcObjPtr == dstObjPtr)
                        continue;

                    if (srcObjPtr == IntPtr.Zero)
                    {
                        field.SetDataBoxed(targetAddr, null, false);
                    }
                    else
                    {
                        var value = field.GetDataBoxed(sourceAddr, false);
                        field.SetDataBoxed(targetAddr, value, false);
                    }
                }
            }
            catch (Exception ex)
            {
                API.LogWarning($"Failed to copy field {field.Name}: {ex.Message}");
            }
        }
    }

    /// <summary>
    ///     Writes a 16-byte <see cref="Guid" /> directly into a named field on <paramref name="obj" />.
    /// </summary>
    /// <remarks>
    ///     Used to populate engine <c>System.Guid</c> fields (such as <c>_MessageID</c> on UI data objects) without
    ///     allocating an intermediate RE Engine boxed <c>System.Guid</c>. Logs a warning and returns if the object,
    ///     field, or raw field address is invalid.
    /// </remarks>
    /// <param name="obj">The target <see cref="ManagedObject" /> containing the <see cref="Guid" /> field.</param>
    /// <param name="fieldName">The exact TDB field name on <paramref name="obj" />.</param>
    /// <param name="guid">The <see cref="Guid" /> value whose raw bytes are copied into engine memory.</param>
    public static void SetGuidField(this ManagedObject obj, string fieldName, Guid guid)
    {
        var addr = obj.GetAddress();
        if (addr == 0)
            return;

        var typeDef = obj.GetTypeDefinition();
        var field = typeDef.GetField(fieldName);
        if (field == null)
        {
            API.LogWarning($"Field {fieldName} not found on {typeDef.FullName}");
            return;
        }

        var destRaw = field.GetDataRaw(addr, false);
        if (destRaw == 0)
        {
            API.LogWarning($"Field {fieldName} resolved to null address on {typeDef.FullName}");
            return;
        }

        Marshal.Copy(guid.ToByteArray(), 0, (IntPtr)destRaw, 16);
    }

    /// <summary>
    ///     Instantiates a closed <c>System.Collections.Generic.List&lt;T&gt;</c> on the RE Engine VM via cached
    ///     <c>MakeGenericType</c> and <c>Activator.CreateInstance</c> metadata.
    /// </summary>
    /// <typeparam name="T">The RE Engine element type (for example <see cref="app.Option.OptionUnitBase" />).</typeparam>
    /// <param name="globalize">
    ///     When <see langword="true" /> (the default), calls <c>Globalize()</c> on the returned list; the caller is
    ///     then responsible for calling <c>ReleaseIfGlobalized()</c> when discarding it.
    /// </param>
    /// <returns>
    ///     The newly created VM list as a <see cref="ManagedObject" />, or <see langword="null" /> if reflection metadata
    ///     resolution or instantiation failed.
    /// </returns>
    public static ManagedObject? CreateGenericList<T>(bool globalize = true) where T : class
    {
        var closedListRuntimeType = ListTypeCache<T>.ClosedListRuntimeType;
        var createInstanceMethod = ListReflectionCache.CreateInstanceMethod;

        if (closedListRuntimeType == null || createInstanceMethod == null)
        {
            API.LogError(
                $"[MemoryHelpers] Failed to resolve cached RuntimeType or Activator for List<{typeof(T).FullName}>.");
            return null;
        }

        var newListMo = createInstanceMethod.InvokeBoxed(
            typeof(ManagedObject),
            null,
            new object[] { closedListRuntimeType }
        ) as ManagedObject;

        if (newListMo == null)
        {
            API.LogError($"[MemoryHelpers] Activator.CreateInstance failed for List<{typeof(T).FullName}>.");
            return null;
        }

        if (globalize)
            newListMo.Globalize();

        return newListMo;
    }

    // public static ManagedObject? CreateGenericList<T>(bool globalize = true) where T : class
    // {
    //     var tdb = API.GetTDB();
    //
    //     // system.type typedef
    //     var sysTypeDef = tdb.FindType("System.Type");
    //     // get elementRuntime type
    //     var elemRuntimeType = TypeDefCache<T>.RequestedType?.RuntimeType;
    //     // Get the RuntimeType of the open generic: typeof(System.Collections.Generic.List<>)
    //     var openListRuntimeType = tdb.FindType("System.Collections.Generic.List`1")?.RuntimeType;
    //     if (openListRuntimeType == null)
    //     {
    //         var getTypeMethod = sysTypeDef.GetMethod("GetType(System.String)");
    //         var typeNameStr = VM.CreateString("System.Collections.Generic.List`1");
    //         openListRuntimeType =
    //             getTypeMethod.InvokeBoxed(typeof(ManagedObject), null, [typeNameStr]) as ManagedObject;
    //     }
    //
    //     // create an array to be able to create with said type as its backing array
    //     var typeArrayMo = sysTypeDef.CreateManagedArray(1);
    //     typeArrayMo.As<_System.Array>().SetValue(elemRuntimeType, 0);
    //
    //     var closedListRuntimeType = (openListRuntimeType as IObject)
    //         .Call("MakeGenericType(System.Type[])", typeArrayMo) as ManagedObject;
    //
    //     // Create the actual List<app.Option.OptionUnitBase> instance from closedListRuntimeType
    //     var activatorType = tdb.FindType("System.Activator");
    //     var createInstanceMethod = activatorType.GetMethod("CreateInstance(System.Type)");
    //
    //     var newListMo = createInstanceMethod.InvokeBoxed(
    //         typeof(ManagedObject),
    //         null,
    //         [closedListRuntimeType]
    //     ) as ManagedObject;
    //
    //     return newListMo;
    // }

    /// <summary>
    ///     A nested static class that caches the TypeDefinition of a given type T for fast access.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    private static class TypeDefCache<T> where T : class
    {
        public static readonly TypeDefinition? RequestedType;

        static TypeDefCache()
        {
            const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Public |
                                                         System.Reflection.BindingFlags.Static;

            // Your exact reflection logic, but it only ever runs once!
            RequestedType = (typeof(T).GetField("REFType", flags)?.GetValue(null)
                             ?? typeof(T).GetProperty("REFType", flags)?.GetValue(null)) as TypeDefinition;
        }
    }

    /// <summary>
    ///     Caches global reflection metadata needed to construct generic <c>List&lt;T&gt;</c> instances.
    /// </summary>
    private static class ListReflectionCache
    {
        public static readonly TypeDefinition? SysTypeDef;
        public static readonly ManagedObject? OpenListRuntimeType;
        public static readonly Method? CreateInstanceMethod;

        static ListReflectionCache()
        {
            var tdb = API.GetTDB();
            SysTypeDef = tdb.FindType("System.Type");

            OpenListRuntimeType = tdb.FindType("System.Collections.Generic.List`1")?.RuntimeType;
            if (OpenListRuntimeType == null && SysTypeDef != null)
            {
                var getTypeMethod = SysTypeDef.GetMethod("GetType(System.String)");
                var typeNameStr = VM.CreateString("System.Collections.Generic.List`1");
                OpenListRuntimeType = getTypeMethod?.InvokeBoxed(
                    typeof(ManagedObject),
                    null,
                    new object[] { typeNameStr }
                ) as ManagedObject;
            }

            OpenListRuntimeType?.Globalize();

            var activatorType = tdb.FindType("System.Activator");
            CreateInstanceMethod = activatorType?.GetMethod("CreateInstance(System.Type)");
        }
    }

    /// <summary>
    ///     Caches the closed System.RuntimeType for <c>List&lt;T&gt;</c> using the exact MakeGenericType method.
    /// </summary>
    private static class ListTypeCache<T> where T : class
    {
        public static readonly ManagedObject? ClosedListRuntimeType;

        static ListTypeCache()
        {
            // 1. Get the RuntimeType of the element T
            var elemRuntimeType = TypeDefCache<T>.RequestedType?.RuntimeType;
            var openListRuntimeType = ListReflectionCache.OpenListRuntimeType;
            var sysTypeDef = ListReflectionCache.SysTypeDef;

            if (elemRuntimeType == null || openListRuntimeType == null || sysTypeDef == null)
                return;

            // 2. Create System.Type[] containing elemRuntimeType
            var typeArrayMo = sysTypeDef.CreateManagedArray(1);
            typeArrayMo.As<_System.Array>().SetValue(elemRuntimeType, 0);

            // 3. Build the closed RuntimeType via MakeGenericType
            ClosedListRuntimeType = (openListRuntimeType as IObject)
                ?.Call("MakeGenericType(System.Type[])", typeArrayMo) as ManagedObject;

            ClosedListRuntimeType?.Globalize();
        }
    }
}